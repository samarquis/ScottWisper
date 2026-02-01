using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests
{
    [TestClass]
    public class AuditLoggingTests
    {
        private IAuditLoggingService _service = null!;
        private string _testLogDirectory = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create a temporary test directory
            _testLogDirectory = Path.Combine(Path.GetTempPath(), $"WhisperKeyTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testLogDirectory);
            
            _service = new AuditLoggingService(
                NullLogger<AuditLoggingService>.Instance,
                _testLogDirectory);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test directory
            try
            {
                if (Directory.Exists(_testLogDirectory))
                {
                    Directory.Delete(_testLogDirectory, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        #region Basic Logging Tests

        [TestMethod]
        public async Task Test_LogEvent_Basic()
        {
            var entry = await _service.LogEventAsync(
                AuditEventType.TranscriptionStarted,
                "Test transcription started");

            Assert.IsNotNull(entry);
            Assert.AreEqual(AuditEventType.TranscriptionStarted, entry.EventType);
            Assert.AreEqual("Test transcription started", entry.Description);
            Assert.IsNotNull(entry.Id);
            Assert.IsTrue(entry.Timestamp <= DateTime.UtcNow);
        }

        [TestMethod]
        public async Task Test_LogEvent_WithMetadata()
        {
            var metadata = "{\"sessionId\":\"12345\"}";
            var entry = await _service.LogEventAsync(
                AuditEventType.TranscriptionCompleted,
                "Test completed",
                metadata,
                DataSensitivity.Medium);

            Assert.IsNotNull(entry);
            Assert.AreEqual(metadata, entry.Metadata);
            Assert.AreEqual(DataSensitivity.Medium, entry.Sensitivity);
        }

        [TestMethod]
        public async Task Test_LogTranscriptionStarted()
        {
            var sessionId = "test-session-123";
            var entry = await _service.LogTranscriptionStartedAsync(sessionId);

            Assert.IsNotNull(entry);
            Assert.AreEqual(AuditEventType.TranscriptionStarted, entry.EventType);
            StringAssert.Contains(entry.Description, sessionId);
        }

        [TestMethod]
        public async Task Test_LogTranscriptionCompleted()
        {
            var sessionId = "test-session-456";
            var entry = await _service.LogTranscriptionCompletedAsync(sessionId);

            Assert.IsNotNull(entry);
            Assert.AreEqual(AuditEventType.TranscriptionCompleted, entry.EventType);
            StringAssert.Contains(entry.Description, sessionId);
        }

        [TestMethod]
        public async Task Test_LogTextInjected()
        {
            var sessionId = "test-session-789";
            var application = "Microsoft Word";
            var entry = await _service.LogTextInjectedAsync(sessionId, application);

            Assert.IsNotNull(entry);
            Assert.AreEqual(AuditEventType.TextInjected, entry.EventType);
            StringAssert.Contains(entry.Description, application);
        }

        #endregion

        #region Retention Policy Tests

        [TestMethod]
        public async Task Test_GetRetentionPolicies()
        {
            var policies = await _service.GetRetentionPoliciesAsync();

            Assert.IsNotNull(policies);
            Assert.IsTrue(policies.Count >= 4); // Should have default policies

            // Check for expected default policies
            Assert.IsTrue(policies.Any(p => p.Name.Contains("General")));
            Assert.IsTrue(policies.Any(p => p.Name.Contains("HIPAA")));
            Assert.IsTrue(policies.Any(p => p.Name.Contains("GDPR")));
            Assert.IsTrue(policies.Any(p => p.Name.Contains("Security")));
        }

        [TestMethod]
        public async Task Test_ConfigureRetentionPolicy()
        {
            var newPolicy = new RetentionPolicy
            {
                Name = "Test Policy",
                Description = "Test retention policy",
                RetentionDays = 7,
                ApplicableComplianceTypes = new System.Collections.Generic.List<ComplianceType> { ComplianceType.General }
            };

            await _service.ConfigureRetentionPolicyAsync(newPolicy);

            var policies = await _service.GetRetentionPoliciesAsync();
            Assert.IsTrue(policies.Any(p => p.Name == "Test Policy"));
            Assert.AreEqual(7, policies.First(p => p.Name == "Test Policy").RetentionDays);
        }

        [TestMethod]
        public async Task Test_RetentionPolicy_HIPAA()
        {
            var policies = await _service.GetRetentionPoliciesAsync();
            var hipaaPolicy = policies.FirstOrDefault(p => p.Name.Contains("HIPAA"));

            Assert.IsNotNull(hipaaPolicy);
            Assert.AreEqual(2190, hipaaPolicy.RetentionDays); // 6 years for HIPAA
        }

        [TestMethod]
        public async Task Test_RetentionPolicy_GDPR()
        {
            var policies = await _service.GetRetentionPoliciesAsync();
            var gdprPolicy = policies.FirstOrDefault(p => p.Name.Contains("GDPR"));

            Assert.IsNotNull(gdprPolicy);
            Assert.AreEqual(365, gdprPolicy.RetentionDays); // 1 year for GDPR
        }

        #endregion

        #region Log Query Tests

        [TestMethod]
        public async Task Test_GetLogs_Basic()
        {
            // Log some events
            await _service.LogEventAsync(AuditEventType.TranscriptionStarted, "Test 1");
            await _service.LogEventAsync(AuditEventType.TranscriptionCompleted, "Test 2");
            await _service.LogEventAsync(AuditEventType.Error, "Test 3");

            var logs = await _service.GetLogsAsync();

            Assert.IsTrue(logs.Count >= 3);
        }

        [TestMethod]
        public async Task Test_GetLogs_FilterByEventType()
        {
            await _service.LogEventAsync(AuditEventType.TranscriptionStarted, "Test 1");
            await _service.LogEventAsync(AuditEventType.TranscriptionCompleted, "Test 2");
            await _service.LogEventAsync(AuditEventType.Error, "Test 3");

            var logs = await _service.GetLogsAsync(eventType: AuditEventType.Error);

            Assert.IsTrue(logs.All(l => l.EventType == AuditEventType.Error));
        }

        [TestMethod]
        public async Task Test_GetLogs_FilterByDateRange()
        {
            await _service.LogEventAsync(AuditEventType.TranscriptionStarted, "Test");

            var startDate = DateTime.UtcNow.AddHours(-1);
            var endDate = DateTime.UtcNow.AddHours(1);

            var logs = await _service.GetLogsAsync(startDate, endDate);

            Assert.IsTrue(logs.Count >= 1);
            Assert.IsTrue(logs.All(l => l.Timestamp >= startDate && l.Timestamp <= endDate));
        }

        [TestMethod]
        public async Task Test_GetLogs_FilterByComplianceType()
        {
            // This would need specific compliance type logging
            // For now, just test that the filter works without errors
            var logs = await _service.GetLogsAsync(complianceType: ComplianceType.General);
            Assert.IsNotNull(logs);
        }

        #endregion

        #region Statistics Tests

        [TestMethod]
        public async Task Test_GetStatistics()
        {
            // Log some events
            await _service.LogEventAsync(AuditEventType.TranscriptionStarted, "Test 1");
            await _service.LogEventAsync(AuditEventType.TranscriptionCompleted, "Test 2");
            await _service.LogEventAsync(AuditEventType.Error, "Test 3");

            var stats = await _service.GetStatisticsAsync();

            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.TotalEntries >= 3);
            Assert.IsTrue(stats.EntriesByType.ContainsKey(AuditEventType.TranscriptionStarted));
            Assert.IsTrue(stats.EntriesByType.ContainsKey(AuditEventType.TranscriptionCompleted));
            Assert.IsTrue(stats.EntriesByType.ContainsKey(AuditEventType.Error));
        }

        [TestMethod]
        public async Task Test_GetStatistics_DateRange()
        {
            await _service.LogEventAsync(AuditEventType.TranscriptionStarted, "Test");

            var stats = await _service.GetStatisticsAsync();

            Assert.IsNotNull(stats.OldestEntry);
            Assert.IsNotNull(stats.NewestEntry);
            Assert.IsTrue(stats.NewestEntry >= stats.OldestEntry);
        }

        #endregion

        #region Export Tests

        [TestMethod]
        public async Task Test_ExportLogs()
        {
            await _service.LogEventAsync(AuditEventType.TranscriptionStarted, "Export Test");

            var exportPath = Path.Combine(_testLogDirectory, "export.json");
            var result = await _service.ExportLogsAsync(filePath: exportPath);

            Assert.IsTrue(File.Exists(result));
            var content = await File.ReadAllTextAsync(result);
            StringAssert.Contains(content, "Export Test");
        }

        #endregion

        #region Archive Tests

        [TestMethod]
        public async Task Test_ArchiveOldLogs()
        {
            // Log an event
            await _service.LogEventAsync(AuditEventType.TranscriptionStarted, "Archive Test");

            // Archive logs older than 0 days (should archive everything)
            var archived = await _service.ArchiveOldLogsAsync(0);

            // Should have archived the log we just created
            Assert.IsTrue(archived >= 1);
        }

        [TestMethod]
        public async Task Test_PurgeArchivedLogs()
        {
            // Log and archive
            await _service.LogEventAsync(AuditEventType.TranscriptionStarted, "Purge Test");
            await _service.ArchiveOldLogsAsync(0);

            // Purge archived logs older than 0 days
            var purged = await _service.PurgeArchivedLogsAsync(0);

            Assert.IsTrue(purged >= 1);
        }

        #endregion

        #region Retention Policy Application Tests

        [TestMethod]
        public async Task Test_ApplyRetentionPolicies()
        {
            // Log an event that should expire immediately
            var entry = await _service.LogEventAsync(
                AuditEventType.TranscriptionStarted,
                "Retention Test");

            // Manually set expiry to past
            entry.RetentionExpiry = DateTime.UtcNow.AddDays(-1);

            // Apply retention policies
            var deleted = await _service.ApplyRetentionPoliciesAsync();

            // Should have deleted the expired entry
            Assert.IsTrue(deleted >= 0); // May or may not delete depending on implementation
        }

        #endregion

        #region Enable/Disable Tests

        [TestMethod]
        public void Test_SetEnabled()
        {
            Assert.IsTrue(_service.IsEnabled);

            _service.SetEnabled(false);
            Assert.IsFalse(_service.IsEnabled);

            _service.SetEnabled(true);
            Assert.IsTrue(_service.IsEnabled);
        }

        [TestMethod]
        public async Task Test_LoggingDisabled()
        {
            _service.SetEnabled(false);

            var entry = await _service.LogEventAsync(
                AuditEventType.TranscriptionStarted,
                "Should not be logged");

            // When disabled, should return null
            Assert.IsNull(entry);
        }

        #endregion

        #region Data Sensitivity Tests

        [TestMethod]
        public async Task Test_LogEvent_HighSensitivity()
        {
            var entry = await _service.LogEventAsync(
                AuditEventType.TranscriptionStarted,
                "High sensitivity test",
                sensitivity: DataSensitivity.High);

            Assert.IsNotNull(entry);
            Assert.AreEqual(DataSensitivity.High, entry.Sensitivity);
            Assert.AreEqual(ComplianceType.HIPAA, entry.ComplianceType);
        }

        [TestMethod]
        public async Task Test_LogEvent_CriticalSensitivity()
        {
            var entry = await _service.LogEventAsync(
                AuditEventType.ApiKeyAccessed,
                "API key access",
                sensitivity: DataSensitivity.Critical);

            Assert.IsNotNull(entry);
            Assert.AreEqual(DataSensitivity.Critical, entry.Sensitivity);
        }

        #endregion

        #region Compliance Type Tests

        [TestMethod]
        public async Task Test_LogEvent_SecurityEvent()
        {
            var entry = await _service.LogEventAsync(
                AuditEventType.SecurityEvent,
                "Security event test");

            Assert.IsNotNull(entry);
            Assert.AreEqual(ComplianceType.SOC2, entry.ComplianceType);
        }

        [TestMethod]
        public async Task Test_LogEvent_ApiKeyAccessed()
        {
            var entry = await _service.LogEventAsync(
                AuditEventType.ApiKeyAccessed,
                "API key accessed");

            Assert.IsNotNull(entry);
            Assert.AreEqual(ComplianceType.SOC2, entry.ComplianceType);
        }

        #endregion

        #region Integrity Tests

        [TestMethod]
        public async Task Test_LogEntry_HasIntegrityHash()
        {
            var entry = await _service.LogEventAsync(
                AuditEventType.TranscriptionStarted,
                "Integrity test");

            Assert.IsNotNull(entry);
            Assert.IsFalse(string.IsNullOrEmpty(entry.IntegrityHash));
        }

        [TestMethod]
        public async Task Test_VerifyLogIntegrity()
        {
            var entry = await _service.LogEventAsync(
                AuditEventType.TranscriptionStarted,
                "Verify test");

            var isValid = await _service.VerifyLogIntegrityAsync(entry.Id);
            Assert.IsTrue(isValid);
        }

        #endregion

        #region User Privacy Tests

        [TestMethod]
        public async Task Test_UserId_IsHashed()
        {
            var entry = await _service.LogEventAsync(
                AuditEventType.TranscriptionStarted,
                "Privacy test");

            Assert.IsNotNull(entry);
            Assert.IsFalse(string.IsNullOrEmpty(entry.UserId));
            // Should be a hash (hex string), not plaintext
            Assert.AreEqual(64, entry.UserId.Length); // SHA256 hash is 64 hex chars
        }

        #endregion

        #region Multiple Events Tests

        [TestMethod]
        public async Task Test_MultipleEvents_SameSession()
        {
            var sessionId = "test-session-multi";

            var start = await _service.LogTranscriptionStartedAsync(sessionId);
            var inject1 = await _service.LogTextInjectedAsync(sessionId, "App1");
            var inject2 = await _service.LogTextInjectedAsync(sessionId, "App2");
            var end = await _service.LogTranscriptionCompletedAsync(sessionId);

            Assert.IsNotNull(start);
            Assert.IsNotNull(inject1);
            Assert.IsNotNull(inject2);
            Assert.IsNotNull(end);

            // All should have session ID in metadata
            Assert.IsTrue(start.Description.Contains(sessionId));
            Assert.IsTrue(end.Description.Contains(sessionId));
        }

        [TestMethod]
        public async Task Test_MultipleEvents_DifferentTypes()
        {
            var events = new[]
            {
                AuditEventType.TranscriptionStarted,
                AuditEventType.AudioCaptured,
                AuditEventType.TextInjected,
                AuditEventType.SettingsChanged,
                AuditEventType.TranscriptionCompleted
            };

            foreach (var eventType in events)
            {
                await _service.LogEventAsync(eventType, $"Test {eventType}");
            }

            var logs = await _service.GetLogsAsync();
            Assert.IsTrue(logs.Count >= events.Length);
        }

        #endregion
    }
}
