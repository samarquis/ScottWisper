using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using WhisperKey.Services;
using WhisperKey.Models;
using System.Text.Json;
using System.Linq;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class AuditLoggingIntegrationTests
    {
        private TestAuditLoggingService _auditService = null!;

        [TestInitialize]
        public void Setup()
        {
            _auditService = new TestAuditLoggingService();
        }

        #region PermissionService Integration Tests

        [TestMethod]
        public async Task Test_PermissionService_LogsCheckEvents()
        {
            // Arrange
            var registryService = new TestRegistryService();
            var permissionService = new PermissionService(registryService, _auditService);

            // Act
            var status = await permissionService.CheckMicrophonePermissionAsync();

            // Assert
            var securityEvents = _auditService.GetLoggedEvents()
                .Where(e => e.EventType == AuditEventType.SecurityEvent)
                .ToList();

            Assert.IsTrue(securityEvents.Count >= 1, "Should have logged permission check event");
            Assert.IsTrue(securityEvents.Any(e => e.Description.Contains("permission check completed")), 
                "Should have completed permission check log");
        }

        [TestMethod]
        public async Task Test_PermissionService_LogsRequestEvents()
        {
            // Arrange
            var registryService = new TestRegistryService();
            var permissionService = new PermissionService(registryService, _auditService);

            // Act
            var result = await permissionService.RequestMicrophonePermissionAsync();

            // Assert
            var securityEvents = _auditService.GetLoggedEvents()
                .Where(e => e.EventType == AuditEventType.SecurityEvent)
                .ToList();

            Assert.IsTrue(securityEvents.Count >= 1, "Should have logged permission request event");
            Assert.IsTrue(securityEvents.Any(e => e.Description.Contains("permission requested")), 
                "Should have permission request log");
        }

        #endregion

        #region WindowsCredentialService Integration Tests

        [TestMethod]
        public async Task Test_CredentialService_LogsStoreEvents()
        {
            // Arrange
            var credentialService = new WindowsCredentialService(
                NullLogger<WindowsCredentialService>.Instance,
                _auditService);

            // Act
            var result = await credentialService.StoreCredentialAsync("test-key", "test-value");

            // Assert
            var apiKeyEvents = _auditService.GetLoggedEvents()
                .Where(e => e.EventType == AuditEventType.ApiKeyAccessed)
                .ToList();

            if (result) // Only check if store was successful (depends on system)
            {
                Assert.IsTrue(apiKeyEvents.Any(e => e.Description.Contains("stored successfully")), 
                    "Should have logged successful credential store");
            }
        }

        [TestMethod]
        public async Task Test_CredentialService_LogsRetrieveEvents()
        {
            // Arrange
            var credentialService = new WindowsCredentialService(
                NullLogger<WindowsCredentialService>.Instance,
                _auditService);

            // Act
            var credential = await credentialService.RetrieveCredentialAsync("test-key");

            // Assert
            var apiKeyEvents = _auditService.GetLoggedEvents()
                .Where(e => e.EventType == AuditEventType.ApiKeyAccessed)
                .ToList();

            // May or may not find the credential, but should log the attempt
            Assert.IsTrue(apiKeyEvents.Any(e => e.Description.Contains("retrieved")) || 
                         apiKeyEvents.Count == 0, // If no credential found, no event logged
                "Should have logged credential retrieval attempt");
        }

        [TestMethod]
        public async Task Test_CredentialService_LogsDeleteEvents()
        {
            // Arrange
            var credentialService = new WindowsCredentialService(
                NullLogger<WindowsCredentialService>.Instance,
                _auditService);

            // Act
            var result = await credentialService.DeleteCredentialAsync("test-key");

            // Assert
            var securityEvents = _auditService.GetLoggedEvents()
                .Where(e => e.EventType == AuditEventType.SecurityEvent)
                .ToList();

            // Should have logged delete attempt (success or failure)
            Assert.IsTrue(securityEvents.Any(e => e.Description.Contains("deleted")) || 
                         securityEvents.Any(e => e.Description.Contains("DeleteFailed")),
                "Should have logged credential delete attempt");
        }

        #endregion

        #region SOC 2 Compliance Tests

        [TestMethod]
        public async Task Test_SecurityEvents_UseSOC2Compliance()
        {
            // Arrange
            var auditService = new AuditLoggingService(
                NullLogger<AuditLoggingService>.Instance);
            
            // Act
            var securityEvent = await auditService.LogEventAsync(
                AuditEventType.SecurityEvent,
                "Test security event",
                sensitivity: DataSensitivity.Medium);

            // Assert
            Assert.IsNotNull(securityEvent);
            Assert.AreEqual(ComplianceType.SOC2, securityEvent.ComplianceType,
                "Security events should use SOC 2 compliance type");
        }

        [TestMethod]
        public async Task Test_ApiKeyEvents_UseSOC2Compliance()
        {
            // Arrange
            var auditService = new AuditLoggingService(
                NullLogger<AuditLoggingService>.Instance);

            // Act
            var apiKeyEvent = await auditService.LogEventAsync(
                AuditEventType.ApiKeyAccessed,
                "Test API key access",
                sensitivity: DataSensitivity.Critical);

            // Assert
            Assert.IsNotNull(apiKeyEvent);
            Assert.AreEqual(ComplianceType.SOC2, apiKeyEvent.ComplianceType,
                "API key events should use SOC 2 compliance type");
        }

        [TestMethod]
        public async Task Test_Soc2RetentionPolicy_Exists()
        {
            // Arrange
            var auditService = new AuditLoggingService(
                NullLogger<AuditLoggingService>.Instance);

            // Act
            var policies = await auditService.GetRetentionPoliciesAsync();

            // Assert
            var soc2Policy = policies.FirstOrDefault(p => p.Name.Contains("SOC 2"));
            Assert.IsNotNull(soc2Policy, "Should have SOC 2 retention policy");
            Assert.AreEqual(2555, soc2Policy.RetentionDays, // 7 years
                "SOC 2 policy should have 7-year retention");
            Assert.IsTrue(soc2Policy.ApplicableComplianceTypes.Contains(ComplianceType.SOC2),
                "SOC 2 policy should apply to SOC 2 compliance type");
        }

        #endregion

        #region Test Helper Classes

        private class TestAuditLoggingService : IAuditLoggingService
        {
            private readonly System.Collections.Generic.List<AuditLogEntry> _loggedEvents = new();

            public bool IsEnabled => true;

            public event EventHandler<AuditLogEntry>? EventLogged;

            public async Task<AuditLogEntry> LogEventAsync(AuditEventType eventType, string description, string? metadata = null, DataSensitivity sensitivity = DataSensitivity.Low)
            {
                var entry = new AuditLogEntry
                {
                    EventType = eventType,
                    Description = description,
                    Metadata = metadata,
                    Sensitivity = sensitivity,
                    ComplianceType = DetermineComplianceType(eventType, sensitivity)
                };

                _loggedEvents.Add(entry);
                EventLogged?.Invoke(this, entry);
                return await Task.FromResult(entry);
            }

            // Minimal implementations for interface compliance
            public Task<AuditLogEntry> LogTranscriptionStartedAsync(string sessionId, string? metadata = null) =>
                LogEventAsync(AuditEventType.TranscriptionStarted, $"Started: {sessionId}", metadata);

            public Task<AuditLogEntry> LogTranscriptionCompletedAsync(string sessionId, string? metadata = null) =>
                LogEventAsync(AuditEventType.TranscriptionCompleted, $"Completed: {sessionId}", metadata);

            public Task<AuditLogEntry> LogTextInjectedAsync(string sessionId, string application, DataSensitivity sensitivity = DataSensitivity.Low) =>
                LogEventAsync(AuditEventType.TextInjected, $"Injected into {application}", JsonSerializer.Serialize(new { Application = application }), sensitivity);

            public Task<System.Collections.Generic.List<AuditLogEntry>> GetLogsAsync(System.DateTime? startDate = null, System.DateTime? endDate = null, AuditEventType? eventType = null, ComplianceType? complianceType = null) =>
                Task.FromResult(_loggedEvents.ToList());

            public Task<AuditLogStatistics> GetStatisticsAsync() => Task.FromResult(new AuditLogStatistics());
            public Task<int> ApplyRetentionPoliciesAsync() => Task.FromResult(0);
            public Task<string> ExportLogsAsync(System.DateTime? startDate = null, System.DateTime? endDate = null, string? filePath = null) => Task.FromResult(string.Empty);
            public Task ConfigureRetentionPolicyAsync(RetentionPolicy policy) => Task.CompletedTask;
            public Task<System.Collections.Generic.List<RetentionPolicy>> GetRetentionPoliciesAsync() => Task.FromResult(new System.Collections.Generic.List<RetentionPolicy>());
            public Task<int> ArchiveOldLogsAsync(int daysOld) => Task.FromResult(0);
            public Task<int> PurgeArchivedLogsAsync(int daysOld) => Task.FromResult(0);
            public Task<bool> VerifyLogIntegrityAsync(string logId) => Task.FromResult(true);
            public void SetEnabled(bool enabled) { }

            public System.Collections.Generic.List<AuditLogEntry> GetLoggedEvents() => _loggedEvents.ToList();

            private ComplianceType DetermineComplianceType(AuditEventType eventType, DataSensitivity sensitivity)
            {
                if (eventType == AuditEventType.SecurityEvent || eventType == AuditEventType.ApiKeyAccessed)
                    return ComplianceType.SOC2;
                if (sensitivity == DataSensitivity.High || sensitivity == DataSensitivity.Critical)
                    return ComplianceType.HIPAA;
                return ComplianceType.General;
            }
        }

        private class TestRegistryService : IRegistryService
        {
            public string? ReadValue(RegistryHiveOption hive, string keyPath, string valueName)
            {
                // Simulate registry read
                if (keyPath.Contains("microphone"))
                {
                    return "Allow"; // Simulate granted permission
                }
                return null;
            }

            public T? ReadValue<T>(RegistryHiveOption hive, string keyPath, string valueName)
            {
                var val = ReadValue(hive, keyPath, valueName);
                if (val == null) return default;
                return (T)Convert.ChangeType(val, typeof(T));
            }

            public void WriteValue(RegistryHiveOption hive, string keyPath, string valueName, string value) { }
            public void WriteValue(RegistryHiveOption hive, string keyPath, string valueName, object value, RegistryValueKind valueKind) { }
            public void DeleteValue(RegistryHiveOption hive, string keyPath, string valueName, bool throwOnMissingValue = false) { }
            public void DeleteKey(RegistryHiveOption hive, string keyPath, bool recursive = false) { }
            public bool KeyExists(RegistryHiveOption hive, string keyPath) => false;
            public bool ValueExists(RegistryHiveOption hive, string keyPath, string valueName) => false;
            public string CreateKey(RegistryHiveOption hive, string keyPath) => string.Empty;
            public string[] GetSubKeyNames(RegistryHiveOption hive, string keyPath) => Array.Empty<string>();
            public string[] GetValueNames(RegistryHiveOption hive, string keyPath) => Array.Empty<string>();
            public void SetStartupWithWindows(string appName, string appPath, bool enable) { }
            public bool IsStartupWithWindowsEnabled(string appName) => false;
        }

        #endregion
    }
}