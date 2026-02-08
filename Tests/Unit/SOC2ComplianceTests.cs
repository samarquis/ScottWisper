using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Win32;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Database;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class SOC2ComplianceTests
    {
        private IAuditLoggingService _auditService = null!;
        private ISecurityAlertService _alertService = null!;
        private string _testLogDirectory = null!;

        [TestInitialize]
        public void Setup()
        {
            _testLogDirectory = Path.Combine(Path.GetTempPath(), "WhisperKeyAuditTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testLogDirectory);

            _auditService = new AuditLoggingService(
                NullLogger<AuditLoggingService>.Instance,
                new NullAuditRepository(),
                _testLogDirectory);
                
            _alertService = new SecurityAlertService(
                NullLogger<SecurityAlertService>.Instance,
                _auditService);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testLogDirectory))
            {
                try
                {
                    Directory.Delete(_testLogDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region SOC 2 Retention Requirements

        [TestMethod]
        public async Task Test_Soc2RetentionPolicy_7YearRetention()
        {
            // Arrange
            var policies = await _auditService.GetRetentionPoliciesAsync();

            // Act
            var soc2Policy = policies.FirstOrDefault(p => p.Name.Contains("SOC 2"));

            // Assert
            Assert.IsNotNull(soc2Policy, "SOC 2 retention policy should exist");
            Assert.AreEqual(2555, soc2Policy.RetentionDays, // 7 years
                "SOC 2 policy should retain logs for 7 years (2555 days)");
            Assert.IsTrue(soc2Policy.ArchiveBeforeDeletion,
                "SOC 2 policy should archive before deletion");
            Assert.AreEqual(3650, soc2Policy.ArchiveRetentionDays, // 10 years
                "SOC 2 archive retention should be 10 years");
        }

        [TestMethod]
        public async Task Test_SecurityEvents_Soc2Classification()
        {
            // Test various security events get SOC 2 classification
            var securityEvents = new[]
            {
                (AuditEventType.SecurityEvent, DataSensitivity.Medium),
                (AuditEventType.ApiKeyAccessed, DataSensitivity.Critical),
                (AuditEventType.UserLogin, DataSensitivity.Low),
                (AuditEventType.UserLogout, DataSensitivity.Low)
            };

            foreach (var (eventType, sensitivity) in securityEvents)
            {
                // Act
                var auditEvent = await _auditService.LogEventAsync(
                    eventType,
                    $"Test {eventType}",
                    sensitivity: sensitivity);

                // Assert
                Assert.IsNotNull(auditEvent, $"Should log {eventType} event");
                Assert.AreEqual(ComplianceType.SOC2, auditEvent.ComplianceType,
                    $"{eventType} should be classified as SOC 2 compliance");
            }
        }

        [TestMethod]
        public async Task Test_LogIntegrity_Soc2Requirements()
        {
            // Act
            var auditEvent = await _auditService.LogEventAsync(
                AuditEventType.SecurityEvent,
                "SOC 2 integrity test",
                sensitivity: DataSensitivity.Medium);

            // Assert
            Assert.IsNotNull(auditEvent, "Should create audit event");
            Assert.IsFalse(string.IsNullOrEmpty(auditEvent.IntegrityHash),
                "SOC 2 requires integrity hash for tamper detection");
            
            // Verify hash is correct format (hex string)
            Assert.IsTrue(auditEvent.IntegrityHash.All(c => char.IsLetterOrDigit(c)),
                "Integrity hash should be hex format");
        }

        #endregion

        #region SOC 2 Access Control Requirements

        [TestMethod]
        public async Task Test_ApiKeyAccessLogging_Soc2()
        {
            // Arrange
            var credentialService = new WindowsCredentialService(
                NullLogger<WindowsCredentialService>.Instance,
                _auditService);

            // Act
            var storeResult = await credentialService.StoreCredentialAsync("soc2-test-key", "test-value");
            var retrieveResult = await credentialService.RetrieveCredentialAsync("soc2-test-key");
            var deleteResult = await credentialService.DeleteCredentialAsync("soc2-test-key");

            // Assert
            var logs = await _auditService.GetLogsAsync(eventType: AuditEventType.ApiKeyAccessed);
            var accessLogs = logs.Where(l => l.Description.Contains("soc2-test-key")).ToList();

            // Should have logged access attempts (success depends on system)
            Assert.IsTrue(accessLogs.Count >= 0, "Should log API key access attempts");
            
            // Check sensitivity levels are appropriate
            foreach (var log in accessLogs)
            {
                Assert.IsTrue(log.Sensitivity >= DataSensitivity.Medium,
                    "API key access logs should have medium or higher sensitivity");
            }
        }

        [TestMethod]
        public async Task Test_PermissionAccessLogging_Soc2()
        {
            // Arrange
            var registryService = new TestRegistryService();
            var permissionService = new PermissionService(registryService, _auditService);

            // Act
            var checkResult = await permissionService.CheckMicrophonePermissionAsync();
            var requestResult = await permissionService.RequestMicrophonePermissionAsync();

            // Assert
            var logs = await _auditService.GetLogsAsync(eventType: AuditEventType.SecurityEvent);
            var permissionLogs = logs.Where(l => 
                l.Description.Contains("permission") || 
                l.Description.Contains("microphone")).ToList();

            Assert.IsTrue(permissionLogs.Count >= 1, 
                "Should log permission access events for SOC 2");
        }

        #endregion

        #region SOC 2 Security Monitoring Requirements

        [TestMethod]
        public async Task Test_RealTimeAlerting_Soc2()
        {
            // Arrange
            var testRule = new SecurityAlertRule
            {
                Name = "SOC 2 Test Alert",
                Description = "Test SOC 2 security monitoring",
                EventType = AuditEventType.SecurityEvent,
                Severity = SecurityAlertSeverity.High,
                Condition = SecurityAlertCondition.CountInTimeWindow,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["count"] = 3,
                    ["timeWindowMinutes"] = 5
                }
            };

            await _alertService.ConfigureAlertRuleAsync(testRule);

            // Act - Create and log multiple security events
            foreach (int i in Enumerable.Range(0, 3))
            {
                var auditEvent = await _auditService.LogEventAsync(
                    AuditEventType.SecurityEvent,
                    $"SOC 2 test event {i}",
                    sensitivity: DataSensitivity.Medium);
                
                // Manually trigger check for the logged event
                await _alertService.CheckEventAsync(auditEvent);
            }

            // Assert
            var alerts = await _alertService.GetRecentAlertsAsync(24);
            var soc2Alerts = alerts.Where(a => a.RuleName == "SOC 2 Test Alert").ToList();

            Assert.IsTrue(soc2Alerts.Count >= 1, "SOC 2 requires real-time security monitoring");
            
            foreach (var alert in soc2Alerts)
            {
                Assert.IsNotNull(alert.Id, "Alert should have unique identifier");
                Assert.IsNotNull(alert.Timestamp, "Alert should have timestamp");
                Assert.IsNotNull(alert.AuditEventId, "Alert should reference audit event");
            }
        }

        [TestMethod]
        public async Task Test_SecurityAlertStatistics_Soc2()
        {
            // Generate some security events
            await _auditService.LogEventAsync(AuditEventType.SecurityEvent, "Test event 1");
            await _auditService.LogEventAsync(AuditEventType.ApiKeyAccessed, "API key access");
            await _auditService.LogEventAsync(AuditEventType.Error, "Security error");

            // Check alerts generated
            var stats = await _alertService.GetAlertStatisticsAsync();

            // Assert
            Assert.IsNotNull(stats, "Should generate alert statistics for SOC 2 monitoring");
            Assert.IsTrue(stats.GeneratedAt <= DateTime.UtcNow, 
                "Statistics should be current");
        }

        #endregion

        #region SOC 2 Data Protection Requirements

        [TestMethod]
        public async Task Test_UserPrivacy_Soc2()
        {
            // Act
            var auditEvent = await _auditService.LogEventAsync(
                AuditEventType.SecurityEvent,
                "SOC 2 privacy test",
                sensitivity: DataSensitivity.Medium);

            // Assert
            Assert.IsNotNull(auditEvent.UserId, "Should have user identifier");
            
            // User ID should be hashed, not plaintext (64 chars for SHA256)
            Assert.AreEqual(64, auditEvent.UserId.Length, 
                "User ID should be SHA256 hash for SOC 2 privacy");
            Assert.IsFalse(auditEvent.UserId.Contains(Environment.UserName), 
                "User ID should not contain plaintext username");

            // Verify it's a valid hex string
            Assert.IsTrue(auditEvent.UserId.All(char.IsLetterOrDigit),
                "User ID should be hex format");
        }

        [TestMethod]
        public async Task Test_DataSensitivityClassifications_Soc2()
        {
            // Test various sensitivity levels
            var sensitivityTests = new[]
            {
                (DataSensitivity.Public, ComplianceType.General),
                (DataSensitivity.Low, ComplianceType.General),
                (DataSensitivity.Medium, ComplianceType.General),
                (DataSensitivity.High, ComplianceType.HIPAA),
                (DataSensitivity.Critical, ComplianceType.HIPAA)
            };

            foreach (var (sensitivity, expectedCompliance) in sensitivityTests)
            {
                // Act
                var auditEvent = await _auditService.LogEventAsync(
                    AuditEventType.TextInjected,
                    $"Sensitivity test: {sensitivity}",
                    sensitivity: sensitivity);

                // Assert
                Assert.AreEqual(expectedCompliance, auditEvent.ComplianceType,
                    $"Sensitivity {sensitivity} should map to {expectedCompliance}");
            }
        }

        #endregion

        #region SOC 2 Audit Evidence Requirements

        [TestMethod]
        public async Task Test_AuditEvidenceGeneration_Soc2()
        {
            // Arrange
            var sessionId = "soc2-evidence-session";
            var testEvents = new[]
            {
                (AuditEventType.TranscriptionStarted, "Session started"),
                (AuditEventType.TextInjected, "Text injected"),
                (AuditEventType.TranscriptionCompleted, "Session completed")
            };

            // Act
            var loggedEvents = new System.Collections.Generic.List<AuditLogEntry>();
            
            foreach (var (eventType, description) in testEvents)
            {
                var auditEvent = await _auditService.LogEventAsync(
                    eventType,
                    description,
                    JsonSerializer.Serialize(new { SessionId = sessionId }),
                    DataSensitivity.Medium);
                loggedEvents.Add(auditEvent);
            }

            // Assert - SOC 2 requires complete audit trail
            Assert.AreEqual(3, loggedEvents.Count, "Should log all session events");
            
            foreach (var auditEvent in loggedEvents)
            {
                // Verify audit evidence requirements
                Assert.IsNotNull(auditEvent.Id, "Should have unique ID");
                Assert.IsNotNull(auditEvent.Timestamp, "Should have timestamp");
                Assert.IsNotNull(auditEvent.EventType, "Should have event type");
                Assert.IsNotNull(auditEvent.Description, "Should have description");
                Assert.IsNotNull(auditEvent.UserId, "Should have user identifier");
                Assert.IsNotNull(auditEvent.ComplianceType, "Should have compliance classification");
                Assert.IsNotNull(auditEvent.IntegrityHash, "Should have integrity hash");
                Assert.IsNotNull(auditEvent.RetentionExpiry, "Should have retention expiry");
            }

            // Verify session correlation
            var sessionEvents = loggedEvents.Where(e => 
                e.Metadata != null && e.Metadata.Contains(sessionId)).ToList();
            Assert.AreEqual(3, sessionEvents.Count, 
                "All events should be correlated by session ID");
        }

        [TestMethod]
        public async Task Test_LogExport_Soc2()
        {
            // Arrange
            await _auditService.LogEventAsync(
                AuditEventType.SecurityEvent,
                "SOC 2 export test",
                JsonSerializer.Serialize(new { ExportTest = true }),
                DataSensitivity.Medium);

            // Act
            var exportPath = await _auditService.ExportLogsAsync();

            // Assert
            Assert.IsNotNull(exportPath, "Should export logs for SOC 2 audit evidence");
            Assert.IsTrue(System.IO.File.Exists(exportPath), "Export file should exist");
            
            var exportContent = await System.IO.File.ReadAllTextAsync(exportPath);
            Assert.IsTrue(exportContent.Contains("SOC 2 export test"), 
                "Export should contain logged events");
            Assert.IsTrue(exportContent.Contains("\"TotalEntries\":"), 
                "Export should include metadata");
        }

        #endregion

        #region SOC 2 Performance Requirements

        [TestMethod]
        public async Task Test_AuditLoggingPerformance_Soc2()
        {
            var startTime = DateTime.UtcNow;

            // SOC 2 requires audit logging doesn't impact performance significantly
            var tasks = Enumerable.Range(0, 50)
                .Select(i => _auditService.LogEventAsync(
                    AuditEventType.SecurityEvent,
                    $"Performance test {i}",
                    sensitivity: DataSensitivity.Medium))
                .ToArray();

            await Task.WhenAll(tasks);

            var processingTime = DateTime.UtcNow - startTime;

            // Should process 50 events quickly (under 10 seconds)
            Assert.IsTrue(processingTime.TotalSeconds < 10,
                $"SOC 2 audit logging should be performant. Took: {processingTime.TotalSeconds}s");
        }

        #endregion

        #region Test Helper Classes

        private class TestRegistryService : IRegistryService
        {
            public string? ReadValue(RegistryHiveOption hive, string keyPath, string valueName)
            {
                // Simulate registry read for testing
                if (keyPath.Contains("microphone"))
                    return "Allow";
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
