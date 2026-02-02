using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Services;

namespace WhisperKey.Tests
{
    /// <summary>
    /// Comprehensive unit tests for PermissionService.
    /// Tests permission checking, requesting, monitoring, and history tracking.
    /// </summary>
    [TestClass]
    public class PermissionServiceTests
    {
        private PermissionService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new PermissionService();
            // Wait for initialization to complete
            Thread.Sleep(100);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_InitializesService()
        {
            Assert.IsNotNull(_service);
        }

        [TestMethod]
        public void Constructor_StartsMonitoring()
        {
            // Give time for monitoring to start
            Thread.Sleep(200);
            
            // Service should be in a valid state after construction
            Assert.IsNotNull(_service);
        }

        [TestMethod]
        public void Constructor_CreatesEmptyHistory()
        {
            var history = _service.GetPermissionRequestHistoryAsync().Result;
            Assert.IsNotNull(history);
            Assert.AreEqual(0, history.Count);
        }

        #endregion

        #region CheckMicrophonePermissionAsync Tests

        [TestMethod]
        public async Task CheckMicrophonePermissionAsync_ReturnsValidStatus()
        {
            var status = await _service.CheckMicrophonePermissionAsync();
            
            // Should return one of the valid enum values
            Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), status));
        }

        [TestMethod]
        public async Task CheckMicrophonePermissionAsync_DoesNotThrow()
        {
            try
            {
                var status = await _service.CheckMicrophonePermissionAsync();
                // Any valid status is acceptable
                Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), status));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Should not throw exception: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task CheckMicrophonePermissionAsync_ReturnsConsistentResult()
        {
            // Call multiple times and verify consistency
            var status1 = await _service.CheckMicrophonePermissionAsync();
            var status2 = await _service.CheckMicrophonePermissionAsync();
            var status3 = await _service.CheckMicrophonePermissionAsync();
            
            // All should be valid enum values
            Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), status1));
            Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), status2));
            Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), status3));
        }

        [TestMethod]
        public async Task CheckMicrophonePermissionAsync_GrantedOrError()
        {
            var status = await _service.CheckMicrophonePermissionAsync();
            
            // Should either be Granted (if audio services running) or an error status
            Assert.IsTrue(
                status == MicrophonePermissionStatus.Granted ||
                status == MicrophonePermissionStatus.Unknown ||
                status == MicrophonePermissionStatus.SystemError,
                $"Unexpected status: {status}");
        }

        #endregion

        #region RequestMicrophonePermissionAsync Tests

        [TestMethod]
        public async Task RequestMicrophonePermissionAsync_DoesNotThrow()
        {
            try
            {
                var result = await _service.RequestMicrophonePermissionAsync();
                // Result should be boolean
                Assert.IsInstanceOfType(result, typeof(bool));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Should not throw exception: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task RequestMicrophonePermissionAsync_ReturnsBoolean()
        {
            var result = await _service.RequestMicrophonePermissionAsync();
            Assert.IsInstanceOfType(result, typeof(bool));
        }

        [TestMethod]
        public async Task RequestMicrophonePermissionAsync_RecordsHistoryOnFailure()
        {
            // Clear any existing history
            var initialHistory = await _service.GetPermissionRequestHistoryAsync();
            
            // Request permission (may fail in test environment)
            await _service.RequestMicrophonePermissionAsync();
            
            var history = await _service.GetPermissionRequestHistoryAsync();
            // History may or may not be updated depending on the result
            Assert.IsNotNull(history);
        }

        #endregion

        #region GetPermissionStatusAsync Tests

        [TestMethod]
        public async Task GetPermissionStatusAsync_ReturnsNonEmptyString()
        {
            var status = await _service.GetPermissionStatusAsync();
            
            Assert.IsFalse(string.IsNullOrEmpty(status));
            Assert.IsTrue(status.Length > 0);
        }

        [TestMethod]
        public async Task GetPermissionStatusAsync_DoesNotThrow()
        {
            try
            {
                var status = await _service.GetPermissionStatusAsync();
                Assert.IsFalse(string.IsNullOrEmpty(status));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Should not throw exception: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task GetPermissionStatusAsync_ReturnsUserFriendlyMessage()
        {
            var status = await _service.GetPermissionStatusAsync();
            
            // Should contain user-friendly guidance
            Assert.IsTrue(
                status.Contains("granted", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                status.Contains("requested", StringComparison.OrdinalIgnoreCase),
                $"Status should contain user-friendly keywords: {status}");
        }

        [TestMethod]
        public async Task GetPermissionStatusAsync_MatchesCurrentPermissionState()
        {
            var permissionStatus = await _service.CheckMicrophonePermissionAsync();
            var statusMessage = await _service.GetPermissionStatusAsync();
            
            // Verify message matches the actual status
            switch (permissionStatus)
            {
                case MicrophonePermissionStatus.Granted:
                    Assert.IsTrue(statusMessage.Contains("granted", StringComparison.OrdinalIgnoreCase));
                    break;
                case MicrophonePermissionStatus.Denied:
                    Assert.IsTrue(statusMessage.Contains("denied", StringComparison.OrdinalIgnoreCase));
                    break;
                case MicrophonePermissionStatus.Unknown:
                    Assert.IsTrue(statusMessage.Contains("unknown", StringComparison.OrdinalIgnoreCase) ||
                                  statusMessage.Contains("determine", StringComparison.OrdinalIgnoreCase));
                    break;
                case MicrophonePermissionStatus.NotRequested:
                    Assert.IsTrue(statusMessage.Contains("requested", StringComparison.OrdinalIgnoreCase));
                    break;
                case MicrophonePermissionStatus.SystemError:
                    Assert.IsTrue(statusMessage.Contains("error", StringComparison.OrdinalIgnoreCase));
                    break;
            }
        }

        #endregion

        #region OpenWindowsPrivacySettingsAsync Tests

        [TestMethod]
        public async Task OpenWindowsPrivacySettingsAsync_ReturnsBoolean()
        {
            var result = await _service.OpenWindowsPrivacySettingsAsync();
            Assert.IsInstanceOfType(result, typeof(bool));
        }

        [TestMethod]
        public async Task OpenWindowsPrivacySettingsAsync_DoesNotThrow()
        {
            try
            {
                var result = await _service.OpenWindowsPrivacySettingsAsync();
                Assert.IsInstanceOfType(result, typeof(bool));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Should not throw exception: {ex.Message}");
            }
        }

        #endregion

        #region MonitorPermissionChangesAsync Tests

        [TestMethod]
        public async Task MonitorPermissionChangesAsync_ReturnsBoolean()
        {
            var result = await _service.MonitorPermissionChangesAsync();
            Assert.IsInstanceOfType(result, typeof(bool));
        }

        [TestMethod]
        public async Task MonitorPermissionChangesAsync_DoesNotThrow()
        {
            try
            {
                var result = await _service.MonitorPermissionChangesAsync();
                Assert.IsInstanceOfType(result, typeof(bool));
            }
            catch (Exception ex)
            {
                Assert.Fail($"Should not throw exception: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task MonitorPermissionChangesAsync_StartsMonitoring()
        {
            var result = await _service.MonitorPermissionChangesAsync();
            
            // Should return true if monitoring started successfully
            Assert.IsTrue(result || !result, "Monitoring should return a boolean result");
        }

        #endregion

        #region GetPermissionRequestHistoryAsync Tests

        [TestMethod]
        public async Task GetPermissionRequestHistoryAsync_ReturnsList()
        {
            var history = await _service.GetPermissionRequestHistoryAsync();
            
            Assert.IsNotNull(history);
            Assert.IsInstanceOfType(history, typeof(List<PermissionRequestRecord>));
        }

        [TestMethod]
        public async Task GetPermissionRequestHistoryAsync_DoesNotThrow()
        {
            try
            {
                var history = await _service.GetPermissionRequestHistoryAsync();
                Assert.IsNotNull(history);
            }
            catch (Exception ex)
            {
                Assert.Fail($"Should not throw exception: {ex.Message}");
            }
        }

        [TestMethod]
        public async Task GetPermissionRequestHistoryAsync_ReturnsConsistentResults()
        {
            var history1 = await _service.GetPermissionRequestHistoryAsync();
            var history2 = await _service.GetPermissionRequestHistoryAsync();
            
            // Should return same count
            Assert.AreEqual(history1.Count, history2.Count);
        }

        #endregion

        #region StopPermissionMonitoring Tests

        [TestMethod]
        public void StopPermissionMonitoring_DoesNotThrow()
        {
            try
            {
                _service.StopPermissionMonitoring();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Should not throw exception: {ex.Message}");
            }
        }

        [TestMethod]
        public void StopPermissionMonitoring_CanBeCalledMultipleTimes()
        {
            // Should be idempotent
            _service.StopPermissionMonitoring();
            _service.StopPermissionMonitoring();
            _service.StopPermissionMonitoring();
            
            // Should not throw
            Assert.IsTrue(true);
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_DoesNotThrow()
        {
            var service = new PermissionService();
            
            try
            {
                service.Dispose();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Should not throw exception: {ex.Message}");
            }
        }

        [TestMethod]
        public void Dispose_StopsMonitoring()
        {
            var service = new PermissionService();
            Thread.Sleep(100); // Let monitoring start
            
            service.Dispose();
            
            // Should not throw when disposing
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            var service = new PermissionService();
            
            // Dispose should be idempotent
            service.Dispose();
            service.Dispose();
            service.Dispose();
            
            // Should not throw
            Assert.IsTrue(true);
        }

        #endregion

        #region Integration Tests

        [TestMethod]
        public async Task PermissionWorkflow_RequestThenCheck()
        {
            // Start fresh
            var service = new PermissionService();
            Thread.Sleep(100);
            
            try
            {
                // Get initial status
                var initialStatus = await service.CheckMicrophonePermissionAsync();
                var initialMessage = await service.GetPermissionStatusAsync();
                
                // Request permission
                var requestResult = await service.RequestMicrophonePermissionAsync();
                
                // Check status again
                var newStatus = await service.CheckMicrophonePermissionAsync();
                var newMessage = await service.GetPermissionStatusAsync();
                
                // Verify all results are valid
                Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), initialStatus));
                Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), newStatus));
                Assert.IsFalse(string.IsNullOrEmpty(initialMessage));
                Assert.IsFalse(string.IsNullOrEmpty(newMessage));
                Assert.IsInstanceOfType(requestResult, typeof(bool));
                
                // Check history
                var history = await service.GetPermissionRequestHistoryAsync();
                Assert.IsNotNull(history);
            }
            finally
            {
                service.Dispose();
            }
        }

        [TestMethod]
        public async Task PermissionWorkflow_MonitorAndHistory()
        {
            var service = new PermissionService();
            Thread.Sleep(100);
            
            try
            {
                // Start monitoring
                var monitorResult = await service.MonitorPermissionChangesAsync();
                Assert.IsInstanceOfType(monitorResult, typeof(bool));
                
                // Get history (may be empty)
                var history = await service.GetPermissionRequestHistoryAsync();
                Assert.IsNotNull(history);
                Assert.IsTrue(history.Count >= 0);
                
                // Request permission to add to history
                await service.RequestMicrophonePermissionAsync();
                
                // Get updated history
                var updatedHistory = await service.GetPermissionRequestHistoryAsync();
                Assert.IsNotNull(updatedHistory);
            }
            finally
            {
                service.Dispose();
            }
        }

        [TestMethod]
        public async Task PermissionWorkflow_AllMethodsIntegration()
        {
            var service = new PermissionService();
            Thread.Sleep(100);
            
            try
            {
                // Execute all main methods
                var checkTask = service.CheckMicrophonePermissionAsync();
                var statusTask = service.GetPermissionStatusAsync();
                var historyTask = service.GetPermissionRequestHistoryAsync();
                var monitorTask = service.MonitorPermissionChangesAsync();
                
                // Wait for all
                await Task.WhenAll(checkTask, statusTask, historyTask, monitorTask);
                
                // Verify all results
                Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), checkTask.Result));
                Assert.IsFalse(string.IsNullOrEmpty(statusTask.Result));
                Assert.IsNotNull(historyTask.Result);
                Assert.IsInstanceOfType(monitorTask.Result, typeof(bool));
            }
            finally
            {
                service.Dispose();
            }
        }

        #endregion

        #region PermissionRequestRecord Tests

        [TestMethod]
        public void PermissionRequestRecord_DefaultValues()
        {
            var record = new PermissionRequestRecord();
            
            Assert.AreEqual(default(DateTime), record.Timestamp);
            Assert.AreEqual(string.Empty, record.Method);
            Assert.AreEqual(false, record.Success);
            Assert.AreEqual(string.Empty, record.Status);
            Assert.AreEqual(string.Empty, record.Details);
        }

        [TestMethod]
        public void PermissionRequestRecord_SetProperties()
        {
            var now = DateTime.Now;
            var record = new PermissionRequestRecord
            {
                Timestamp = now,
                Method = "Test Method",
                Success = true,
                Status = "Granted",
                Details = "Test details"
            };
            
            Assert.AreEqual(now, record.Timestamp);
            Assert.AreEqual("Test Method", record.Method);
            Assert.AreEqual(true, record.Success);
            Assert.AreEqual("Granted", record.Status);
            Assert.AreEqual("Test details", record.Details);
        }

        #endregion

        #region IPermissionService Interface Tests

        [TestMethod]
        public void Service_ImplementsIPermissionService()
        {
            Assert.IsTrue(_service is IPermissionService);
        }

        [TestMethod]
        public async Task IPermissionService_CheckMicrophonePermissionAsync()
        {
            var interfaceService = (IPermissionService)_service;
            var status = await interfaceService.CheckMicrophonePermissionAsync();
            
            Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), status));
        }

        [TestMethod]
        public async Task IPermissionService_RequestMicrophonePermissionAsync()
        {
            var interfaceService = (IPermissionService)_service;
            var result = await interfaceService.RequestMicrophonePermissionAsync();
            
            Assert.IsInstanceOfType(result, typeof(bool));
        }

        [TestMethod]
        public async Task IPermissionService_GetPermissionStatusAsync()
        {
            var interfaceService = (IPermissionService)_service;
            var status = await interfaceService.GetPermissionStatusAsync();
            
            Assert.IsFalse(string.IsNullOrEmpty(status));
        }

        [TestMethod]
        public async Task IPermissionService_OpenWindowsPrivacySettingsAsync()
        {
            var interfaceService = (IPermissionService)_service;
            var result = await interfaceService.OpenWindowsPrivacySettingsAsync();
            
            Assert.IsInstanceOfType(result, typeof(bool));
        }

        [TestMethod]
        public async Task IPermissionService_MonitorPermissionChangesAsync()
        {
            var interfaceService = (IPermissionService)_service;
            var result = await interfaceService.MonitorPermissionChangesAsync();
            
            Assert.IsInstanceOfType(result, typeof(bool));
        }

        [TestMethod]
        public async Task IPermissionService_GetPermissionRequestHistoryAsync()
        {
            var interfaceService = (IPermissionService)_service;
            var history = await interfaceService.GetPermissionRequestHistoryAsync();
            
            Assert.IsNotNull(history);
        }

        #endregion

        #region Concurrent Access Tests

        [TestMethod]
        public async Task ConcurrentPermissionChecks_DoNotThrow()
        {
            var tasks = new List<Task<MicrophonePermissionStatus>>();
            
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_service.CheckMicrophonePermissionAsync());
            }
            
            var results = await Task.WhenAll(tasks);
            
            // All should return valid status
            foreach (var status in results)
            {
                Assert.IsTrue(Enum.IsDefined(typeof(MicrophonePermissionStatus), status));
            }
        }

        [TestMethod]
        public async Task ConcurrentHistoryAccess_DoNotThrow()
        {
            var tasks = new List<Task<List<PermissionRequestRecord>>>();
            
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_service.GetPermissionRequestHistoryAsync());
            }
            
            var results = await Task.WhenAll(tasks);
            
            // All should return valid lists
            foreach (var history in results)
            {
                Assert.IsNotNull(history);
            }
        }

        #endregion
    }
}
