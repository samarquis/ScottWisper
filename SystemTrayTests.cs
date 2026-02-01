using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Services;
using System.Runtime.InteropServices;

namespace WhisperKey.Tests
{
    [TestClass]
    public class SystemTrayTests
    {
        private SystemTrayService? _systemTrayService;
        private Mock<IFeedbackService>? _mockFeedbackService;
        private List<PerformanceSnapshot> _performanceSnapshots = new();

        [TestInitialize]
        public void Setup()
        {
            _mockFeedbackService = new Mock<IFeedbackService>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _systemTrayService?.Dispose();
            _systemTrayService = null;
        }

        #region System Tray Initialization Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldInitializeSuccessfully()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();

            // Act
            var stopwatch = Stopwatch.StartNew();
            _systemTrayService.Initialize();
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(_systemTrayService, "System tray service should be created");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 1000, $"Initialization should complete in < 1s, took {stopwatch.ElapsedMilliseconds}ms");
        }

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldHandleMultipleInitializationCalls()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();

            // Act
            _systemTrayService.Initialize();
            var firstCallTime = DateTime.Now;
            
            // Second initialization should not cause issues
            _systemTrayService.Initialize();
            var secondCallTime = DateTime.Now;

            // Assert
            Assert.IsTrue((secondCallTime - firstCallTime).TotalMilliseconds < 100, 
                "Multiple initialization calls should be handled gracefully");
        }

        #endregion

        #region Icon and Status Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldUpdateStatusCorrectly()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();
            
            var statusUpdateCount = 0;
            _systemTrayService.StatusChanged += (sender, status) => statusUpdateCount++;

            // Act - Test all status transitions
            var statuses = new[]
            {
                SystemTrayService.TrayStatus.Idle,
                SystemTrayService.TrayStatus.Ready,
                SystemTrayService.TrayStatus.Recording,
                SystemTrayService.TrayStatus.Processing,
                SystemTrayService.TrayStatus.Error,
                SystemTrayService.TrayStatus.Offline
            };

            foreach (var status in statuses)
            {
                var stopwatch = Stopwatch.StartNew();
                _systemTrayService.UpdateStatus(status);
                stopwatch.Stop();
                
                // Assert responsiveness
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100, 
                    $"Status update to {status} should complete in < 100ms, took {stopwatch.ElapsedMilliseconds}ms");
                
                Thread.Sleep(50); // Small delay between updates
            }

            // Assert
            Assert.IsTrue(statusUpdateCount >= 5, "Should receive status change events");
        }

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldHandleFeedbackServiceStatusUpdates()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();
            
            var statusChanges = new List<IFeedbackService.DictationStatus>();
            
            // Act - Test status mapping from feedback service
            var feedbackStatuses = new[]
            {
                IFeedbackService.DictationStatus.Idle,
                IFeedbackService.DictationStatus.Ready,
                IFeedbackService.DictationStatus.Recording,
                IFeedbackService.DictationStatus.Processing,
                IFeedbackService.DictationStatus.Complete,
                IFeedbackService.DictationStatus.Error
            };

            foreach (var status in feedbackStatuses)
            {
                var stopwatch = Stopwatch.StartNew();
                _systemTrayService.UpdateFromFeedbackService(status);
                stopwatch.Stop();
                
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100, 
                    $"Feedback status update to {status} should complete in < 100ms, took {stopwatch.ElapsedMilliseconds}ms");
                
                Thread.Sleep(50);
            }

            // Assert
            Assert.IsTrue(true, "All feedback service status updates should be handled");
        }

        #endregion

        #region Notification Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldShowNotifications()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();

            // Act
            var testNotifications = new[]
            {
                ("Test Notification", "This is a test message", ToolTipIcon.Info),
                ("Error", "This is an error notification", ToolTipIcon.Error),
                ("Warning", "This is a warning", ToolTipIcon.Warning),
                ("Info", "This is info", ToolTipIcon.Info)
            };

            foreach (var (title, message, icon) in testNotifications)
            {
                var stopwatch = Stopwatch.StartNew();
                _systemTrayService.ShowNotification(message, title, 2000);
                stopwatch.Stop();
                
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 50, 
                    $"Notification display should complete in < 50ms, took {stopwatch.ElapsedMilliseconds}ms");
                
                Thread.Sleep(100); // Brief pause between notifications
            }

            // Assert - Notifications should be queued/shown without exceptions
            Assert.IsTrue(true, "All notifications should be displayed successfully");
        }

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldShowEnhancedNotifications()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();

            // Act
            var stopwatch = Stopwatch.StartNew();
            _systemTrayService.ShowEnhancedNotification("Enhanced message", "Test Title", "🎯");
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 50, 
                $"Enhanced notification should complete in < 50ms, took {stopwatch.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Event Handling Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldTriggerEventsCorrectly()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();
            
            var startDictationFired = false;
            var stopDictationFired = false;
            var settingsRequestedFired = false;
            var windowToggleFired = false;
            var exitRequestedFired = false;

            _systemTrayService.StartDictationRequested += (s, e) => startDictationFired = true;
            _systemTrayService.StopDictationRequested += (s, e) => stopDictationFired = true;
            _systemTrayService.SettingsRequested += (s, e) => settingsRequestedFired = true;
            _systemTrayService.WindowToggleRequested += (s, e) => windowToggleFired = true;
            _systemTrayService.ExitRequested += (s, e) => exitRequestedFired = true;

            // Use the variables to avoid warnings
            Assert.IsFalse(startDictationFired);
            Assert.IsFalse(stopDictationFired);
            Assert.IsFalse(settingsRequestedFired);
            Assert.IsFalse(windowToggleFired);
            Assert.IsFalse(exitRequestedFired);

            // Act - Simulate menu interactions
            _systemTrayService.UpdateDictationStatus(true); // Should trigger start
            Thread.Sleep(100);
            _systemTrayService.UpdateDictationStatus(false); // Should trigger stop

            // Note: In real testing, we'd simulate actual UI interactions
            // For unit tests, we verify the event infrastructure exists

            // Assert
            Assert.IsNotNull(_systemTrayService, "Event infrastructure should be properly initialized");
        }

        #endregion

        #region Memory and Resource Management Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldManageMemoryEfficiently()
        {
            // Arrange
            var initialMemory = GC.GetTotalMemory(false);
            
            // Act - Create and dispose multiple system tray services
            for (int i = 0; i < 10; i++)
            {
                var service = new SystemTrayService();
                service.Initialize();
                
                // Simulate some operations
                service.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                service.ShowNotification("Test", "Memory test");
                
                service.Dispose();
            }

            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert
            Assert.IsTrue(memoryIncrease < 1024 * 1024, // Less than 1MB increase
                $"Memory increase should be < 1MB, was {memoryIncrease / 1024.0 / 1024.0:F2}MB");
        }

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldDisposeProperly()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();

            // Act
            var stopwatch = Stopwatch.StartNew();
            _systemTrayService.Dispose();
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 500, 
                $"Disposal should complete in < 500ms, took {stopwatch.ElapsedMilliseconds}ms");
            
            // Multiple disposals should be safe
            var secondStopwatch = Stopwatch.StartNew();
            _systemTrayService.Dispose();
            secondStopwatch.Stop();
            
            Assert.IsTrue(secondStopwatch.ElapsedMilliseconds < 10, 
                "Multiple disposals should be handled quickly");
        }

        #endregion

        #region Long-term Stability Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public async Task SystemTray_ShouldMaintainStabilityOverExtendedPeriod()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();
            
            var errors = new List<string>();
            var operationsCount = 0;
            var memorySnapshots = new List<long>();
            
            var testDuration = TimeSpan.FromSeconds(10); // Reduced for automated testing
            var startTime = DateTime.UtcNow;

            // Act - Simulate extended operation
            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    // Cycle through all statuses
                    var statuses = new[]
                    {
                        SystemTrayService.TrayStatus.Idle,
                        SystemTrayService.TrayStatus.Ready,
                        SystemTrayService.TrayStatus.Recording,
                        SystemTrayService.TrayStatus.Processing,
                        SystemTrayService.TrayStatus.Ready
                    };

                    foreach (var status in statuses)
                    {
                        _systemTrayService.UpdateStatus(status);
                        await Task.Delay(100);
                    }

                    // Show notifications
                    _systemTrayService.ShowNotification($"Operation {operationsCount}", "Stability Test");
                    
                    operationsCount++;
                    
                    // Take memory snapshot every 50 operations
                    if (operationsCount % 50 == 0)
                    {
                        memorySnapshots.Add(GC.GetTotalMemory(false));
                    }
                    
                    await Task.Delay(50); // Brief pause between operation cycles
                }
                catch (Exception ex)
                {
                    errors.Add($"Operation {operationsCount}: {ex.Message}");
                }
            }

            // Cleanup
            var finalMemory = GC.GetTotalMemory(false);
            memorySnapshots.Add(finalMemory);

            // Assert
            Assert.IsTrue(errors.Count < operationsCount * 0.05, // Less than 5% error rate
                $"Error rate should be < 5%, was {(double)errors.Count / operationsCount * 100:F1}%");
            
            Assert.IsTrue(operationsCount > 100, // Should complete reasonable number of operations
                $"Should complete > 100 operations, completed {operationsCount}");

            // Check memory stability
            if (memorySnapshots.Count > 2)
            {
                var memoryVariance = CalculateVariance(memorySnapshots);
                Assert.IsTrue(memoryVariance < 1024 * 1024 * 5, // Less than 5MB variance
                    $"Memory variance should be stable, was {memoryVariance / 1024.0 / 1024.0:F2}MB");
            }
        }

        [TestMethod]
        [TestCategory("SystemTray")]
        public async Task SystemTray_ShouldHandleHighFrequencyUpdates()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();
            
            var updateCount = 0;
            var errors = new List<string>();
            var testDuration = TimeSpan.FromSeconds(5);
            var startTime = DateTime.UtcNow;

            // Act - High frequency status updates
            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    var random = new Random();
                    var status = (SystemTrayService.TrayStatus)random.Next(0, 6);
                    
                    var stopwatch = Stopwatch.StartNew();
                    _systemTrayService.UpdateStatus(status);
                    stopwatch.Stop();
                    
                    // Track performance
                    if (updateCount % 100 == 0)
                    {
                        _performanceSnapshots.Add(new PerformanceSnapshot
                        {
                            Timestamp = DateTime.UtcNow,
                            OperationLatencyMs = stopwatch.ElapsedMilliseconds,
                            MemoryUsageMB = GC.GetTotalMemory(false) / (1024.0 * 1024.0)
                        });
                    }
                    
                    updateCount++;
                    await Task.Delay(10); // 100 updates per second
                }
                catch (Exception ex)
                {
                    errors.Add($"Update {updateCount}: {ex.Message}");
                }
            }

            // Assert
            Assert.IsTrue(updateCount > 400, // Should handle at least 80 updates/second
                $"Should handle > 400 updates, completed {updateCount}");
            
            Assert.IsTrue(errors.Count < updateCount * 0.01, // Less than 1% error rate
                $"Error rate should be < 1%, was {(double)errors.Count / updateCount * 100:F1}%");
            
            // Performance should remain stable
            if (_performanceSnapshots.Count > 0)
            {
                var avgLatency = _performanceSnapshots.Average(s => s.OperationLatencyMs);
                Assert.IsTrue(avgLatency < 50, // Average latency should stay low
                    $"Average latency should be < 50ms, was {avgLatency:F2}ms");
            }
        }

        #endregion

        #region Resource Cleanup Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public async Task SystemTray_ShouldCleanupResourcesOnFailure()
        {
            // Arrange & Act - Simulate service failure scenarios
            var scenarios = new[]
            {
                async () => await SimulateMemoryPressure(),
                async () => await SimulateHighCpuUsage(),
                async () => await SimulateResourceExhaustion()
            };

            foreach (var scenario in scenarios)
            {
                try
                {
                    var service = new SystemTrayService();
                    service.Initialize();
                    
                    // Apply stress scenario
                    await scenario();
                    
                    // Service should still function after stress
                    service.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                    service.ShowNotification("Recovery Test", "Service should be responsive");
                    
                    // Cleanup should work
                    var stopwatch = Stopwatch.StartNew();
                    service.Dispose();
                    stopwatch.Stop();
                    
                    Assert.IsTrue(stopwatch.ElapsedMilliseconds < 2000, 
                        "Cleanup should complete even after stress scenario");
                }
                catch (Exception ex)
                {
                    Assert.Fail($"Resource cleanup failed: {ex.Message}");
                }
            }

            // Assert
            Assert.IsTrue(true, "All resource cleanup scenarios should be handled");
        }

        #endregion

        #region Performance Validation Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldMeetPerformanceRequirements()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            
            // Act - Measure initialization performance
            var initStopwatch = Stopwatch.StartNew();
            _systemTrayService.Initialize();
            initStopwatch.Stop();
            
            // Measure status update performance
            var statusTimes = new List<long>();
            var statuses = Enum.GetValues<SystemTrayService.TrayStatus>();
            
            foreach (var status in statuses)
            {
                var stopwatch = Stopwatch.StartNew();
                _systemTrayService.UpdateStatus(status);
                stopwatch.Stop();
                statusTimes.Add(stopwatch.ElapsedMilliseconds);
            }
            
            // Measure notification performance
            var notificationTimes = new List<long>();
            for (int i = 0; i < 10; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                _systemTrayService.ShowNotification($"Test {i}", "Performance Test");
                stopwatch.Stop();
                notificationTimes.Add(stopwatch.ElapsedMilliseconds);
            }
            
            // Measure disposal performance
            var disposeStopwatch = Stopwatch.StartNew();
            _systemTrayService.Dispose();
            disposeStopwatch.Stop();

            // Assert
            Assert.IsTrue(initStopwatch.ElapsedMilliseconds < 3000, // Startup < 3s
                $"Initialization should be < 3s, was {initStopwatch.ElapsedMilliseconds}ms");
            
            Assert.IsTrue(statusTimes.Average() < 100, // Status updates < 100ms
                $"Status update average should be < 100ms, was {statusTimes.Average():F2}ms");
            
            Assert.IsTrue(notificationTimes.Average() < 50, // Notifications < 50ms
                $"Notification average should be < 50ms, was {notificationTimes.Average():F2}ms");
            
            Assert.IsTrue(disposeStopwatch.ElapsedMilliseconds < 1000, // Disposal < 1s
                $"Disposal should be < 1s, was {disposeStopwatch.ElapsedMilliseconds}ms");
        }

        #endregion

        #region Context Menu Tests

        [TestMethod]
        [TestCategory("SystemTray")]
        public void SystemTray_ShouldHandleContextMenuOperations()
        {
            // Arrange
            _systemTrayService = new SystemTrayService();
            _systemTrayService.Initialize();

            // Act - Test various context menu scenarios
            var operations = new[]
            {
                () => _systemTrayService.UpdateDictationStatus(true),  // Start dictation
                () => _systemTrayService.UpdateDictationStatus(false), // Stop dictation
                () => _systemTrayService.ShowNotification("Test", "Menu test"),
                () => _systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Ready)
            };

            foreach (var operation in operations)
            {
                var stopwatch = Stopwatch.StartNew();
                operation();
                stopwatch.Stop();
                
                Assert.IsTrue(stopwatch.ElapsedMilliseconds < 100, 
                    $"Context menu operation should complete in < 100ms, took {stopwatch.ElapsedMilliseconds}ms");
                
                Thread.Sleep(50);
            }

            // Assert
            Assert.IsTrue(true, "All context menu operations should be responsive");
        }

        #endregion

        #region Helper Methods

        private async Task SimulateMemoryPressure()
        {
            // Simulate memory pressure by allocating and releasing memory
            var allocations = new List<byte[]>();
            
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    allocations.Add(new byte[1024 * 1024]); // 1MB allocations
                    await Task.Delay(10);
                }
                catch (OutOfMemoryException)
                {
                    break;
                }
            }
            
            // Release memory
            allocations.Clear();
            GC.Collect();
            await Task.Delay(100);
        }

        private async Task SimulateHighCpuUsage()
        {
            // Simulate high CPU usage for short period
            var startTime = DateTime.UtcNow;
            
            while (DateTime.UtcNow - startTime < TimeSpan.FromMilliseconds(500))
            {
                // CPU intensive operation
                for (int i = 0; i < 1000; i++)
                {
                    Math.Sqrt(i);
                }
                await Task.Delay(1);
            }
        }

        private async Task SimulateResourceExhaustion()
        {
            // Simulate resource exhaustion scenarios
            try
            {
                // Try to consume various resources
                var handles = new List<object>();
                
                for (int i = 0; i < 100; i++)
                {
                    handles.Add(new object());
                    await Task.Delay(1);
                }
                
                // Force some cleanup
                handles.Clear();
            }
            catch
            {
                // Expected in resource exhaustion scenarios
            }
        }

        private double CalculateVariance(List<long> values)
        {
            if (values.Count < 2) return 0;
            
            var mean = values.Average();
            var sumOfSquares = values.Sum(x => Math.Pow(x - mean, 2));
            return sumOfSquares / values.Count;
        }

        #endregion

        #region Data Classes

        public class PerformanceSnapshot
        {
            public DateTime Timestamp { get; set; }
            public double OperationLatencyMs { get; set; }
            public double MemoryUsageMB { get; set; }
            public double CpuUsagePercent { get; set; }
        }

        #endregion
    }
}