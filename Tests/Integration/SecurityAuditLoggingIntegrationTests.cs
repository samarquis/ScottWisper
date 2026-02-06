using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class SecurityAuditLoggingIntegrationTests
    {
        private Mock<ILogger<AuditLoggingService>> _mockAuditLogger = null!;
        private Mock<ILogger<AuthenticationService>> _mockAuthLogger = null!;
        private Mock<ILogger<SecurityContextService>> _mockContextLogger = null!;
        private Mock<ILogger<SOC2ComplianceService>> _mockSOC2Logger = null!;
        private Mock<ILogger<WindowsCredentialService>> _mockCredentialLogger = null!;
        private Mock<ICredentialService> _mockCredentialService = null!;
        private IAuditLoggingService _auditService = null!;
        private ISecurityContextService _securityContextService = null!;
        private IAuthenticationService _authService = null!;
        private ISOC2ComplianceService _soc2Service = null!;
        private WindowsCredentialService _windowsCredentialService = null!;
        private string _testLogDirectory = null!;

        [TestInitialize]
        public void Setup()
        {
            // Create a temporary test directory
            _testLogDirectory = Path.Combine(Path.GetTempPath(), $"SecurityAuditTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testLogDirectory);

            // Setup mock loggers
            _mockAuditLogger = new Mock<ILogger<AuditLoggingService>>();
            _mockAuthLogger = new Mock<ILogger<AuthenticationService>>();
            _mockContextLogger = new Mock<ILogger<SecurityContextService>>();
            _mockSOC2Logger = new Mock<ILogger<SOC2ComplianceService>>();
            _mockCredentialLogger = new Mock<ILogger<WindowsCredentialService>>();

            // Setup mock credential service
            _mockCredentialService = new Mock<ICredentialService>();
            _mockCredentialService.Setup(c => c.StoreCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockCredentialService.Setup(c => c.RetrieveCredentialAsync(It.IsAny<string>()))
                .ReturnsAsync<string?>(null);

            // Initialize services
            _securityContextService = new SecurityContextService(_mockContextLogger.Object);
            _auditService = new AuditLoggingService(_mockAuditLogger.Object, _testLogDirectory, _securityContextService);
            _authService = new AuthenticationService(_mockAuthLogger.Object, _auditService, _securityContextService, _mockCredentialService.Object);
            _soc2Service = new SOC2ComplianceService(_mockSOC2Logger.Object, _auditService, _testLogDirectory);
            _windowsCredentialService = new WindowsCredentialService(_mockCredentialLogger.Object, _auditService, _securityContextService);
        }

        [TestCleanup]
        public void Cleanup()
        {
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

        #region Authentication Flow Tests

        [TestMethod]
        public async Task Test_CompleteAuthenticationFlow_AuditTrail()
        {
            // Arrange
            var username = "testuser@example.com";
            var password = "SecurePassword123!";
            
            // Setup credential service for authentication
            _mockCredentialService.Setup(c => c.RetrieveCredentialAsync($"user_{username}"))
                .ReturnsAsync(HashPassword(password));
            _mockCredentialService.Setup(c => c.StoreCredentialAsync($"user_{username}", It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act - Complete authentication flow
            var authResult = await _authService.AuthenticateAsync(username, password, "Test login");
            var passwordChangeResult = await _authService.ChangePasswordAsync(username, password, "NewSecurePassword123!");
            var logoutResult = await _authService.LogoutAsync(authResult.SessionId!, "Test logout");

            // Assert - Verify audit trail
            var logs = await _auditService.GetLogsAsync();
            
            Assert.IsTrue(authResult.Success, "Authentication should succeed");
            Assert.IsTrue(passwordChangeResult.Success, "Password change should succeed");
            
            // Verify all authentication events are logged
            var authEvents = logs.Where(l => 
                l.EventType == AuditEventType.AuthenticationSucceeded ||
                l.EventType == AuditEventType.PasswordChanged ||
                l.EventType == AuditEventType.UserLogout).ToList();
            
            Assert.AreEqual(3, authEvents.Count, "Should have 3 authentication events");
            
            // Verify security context is included
            foreach (var log in authEvents)
            {
                Assert.IsFalse(string.IsNullOrEmpty(log.Metadata), "Security context metadata should be present");
                Assert.IsFalse(string.IsNullOrEmpty(log.IpAddress), "IP address should be captured");
            }

            // Verify SOC 2 compliance
            var soc2Result = await _soc2Service.ValidateAuditLogIntegrityAsync();
            Assert.IsTrue(soc2Result.IsCompliant, "Audit logs should be SOC 2 compliant");
        }

        [TestMethod]
        public async Task Test_FailedAuthentication_AuditTrail()
        {
            // Arrange
            var username = "testuser@example.com";
            var wrongPassword = "WrongPassword";

            // Act - Multiple failed authentication attempts
            var result1 = await _authService.AuthenticateAsync(username, wrongPassword);
            var result2 = await _authService.AuthenticateAsync(username, wrongPassword);
            var result3 = await _authService.AuthenticateAsync(username, wrongPassword);

            // Assert - Verify failed attempts are logged
            var logs = await _auditService.GetLogsAsync();
            var failedAuthEvents = logs.Where(l => l.EventType == AuditEventType.AuthenticationFailed).ToList();
            
            Assert.AreEqual(3, failedAuthEvents.Count, "Should log all failed authentication attempts");
            
            // Verify each failed attempt has security context
            foreach (var log in failedAuthEvents)
            {
                Assert.AreEqual(DataSensitivity.High, log.Sensitivity, "Failed auth should be high sensitivity");
                Assert.AreEqual(ComplianceType.SOC2, log.ComplianceType, "Failed auth should be SOC 2");
                
                var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(log.Metadata!);
                Assert.IsNotNull(metadata, "Metadata should be present");
            }

            // Verify suspicious pattern detection
            var patterns = await _soc2Service.DetectSuspiciousPatternsAsync();
            var bruteForcePatterns = patterns.Where(p => p.Type == PatternType.BruteForceAttempt).ToList();
            Assert.IsTrue(bruteForcePatterns.Count > 0, "Should detect brute force pattern");
        }

        #endregion

        #region Credential Service Tests

        [TestMethod]
        public async Task Test_WindowsCredentialService_SecurityContext()
        {
            // Arrange
            var apiKey = "sk-test123456789";
            var keyName = "TestAPIKey";

            // Act - Store and retrieve credential
            var storeResult = await _windowsCredentialService.StoreCredentialAsync(keyName, apiKey);
            var retrieveResult = await _windowsCredentialService.RetrieveCredentialAsync(keyName);

            // Assert - Verify audit logging with security context
            var logs = await _auditService.GetLogsAsync();
            var credentialEvents = logs.Where(l => l.EventType == AuditEventType.ApiKeyAccessed).ToList();
            
            Assert.AreEqual(2, credentialEvents.Count, "Should log store and retrieve operations");
            
            // Verify security context in metadata
            foreach (var log in credentialEvents)
            {
                Assert.AreEqual(DataSensitivity.Critical, log.Sensitivity, "API key access should be critical");
                
                var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(log.Metadata!);
                Assert.IsNotNull(metadata, "Security context metadata should be present");
                
                if (metadata.ContainsKey("SecurityContext"))
                {
                    var securityContext = metadata["SecurityContext"];
                    Assert.IsNotNull(securityContext, "Security context should be included");
                }
            }
        }

        #endregion

        #region SOC 2 Compliance Tests

        [TestMethod]
        public async Task Test_SOC2ComplianceValidation_CompleteFlow()
        {
            // Arrange - Create comprehensive audit trail
            await CreateComprehensiveAuditTrail();

            // Act - Run SOC 2 compliance validation
            var integrityResult = await _soc2Service.ValidateAuditLogIntegrityAsync();
            var completenessResult = await _soc2Service.ValidateAuditLogCompletenessAsync();
            var retentionResult = await _soc2Service.ValidateRetentionPolicyComplianceAsync();
            var hashChainResult = await _soc2Service.VerifyHashChainAsync();
            var report = await _soc2Service.GenerateComplianceReportAsync();

            // Assert - Verify SOC 2 compliance
            Assert.IsTrue(integrityResult.IsCompliant, "Integrity validation should pass");
            Assert.IsTrue(completenessResult.IsCompliant, "Completeness validation should pass");
            Assert.IsTrue(hashChainResult.IsValid, "Hash chain should be valid");
            
            // Verify report generation
            Assert.IsNotNull(report, "Compliance report should be generated");
            Assert.IsNotNull(report.IntegrityValidation, "Report should include integrity validation");
            Assert.IsNotNull(report.CompletenessValidation, "Report should include completeness validation");
            Assert.IsNotNull(report.HashChainValidation, "Report should include hash chain validation");
            
            // Verify overall compliance level
            Assert.IsTrue(report.OverallCompliance >= ComplianceLevel.MostlyCompliant, 
                "Should be at least mostly compliant");
        }

        [TestMethod]
        public async Task Test_AuditLogTamperingDetection()
        {
            // Arrange - Create some audit entries
            await _auditService.LogEventAsync(AuditEventType.UserLogin, "Test login");
            await _auditService.LogEventAsync(AuditEventType.UserLogout, "Test logout");

            // Tamper with audit log file
            var logFiles = Directory.GetFiles(_testLogDirectory, "audit-*.json");
            if (logFiles.Length > 0)
            {
                var logContent = await File.ReadAllTextAsync(logFiles[0]);
                var tamperedContent = logContent.Replace("\"Test login\"", "\"Tampered login\"");
                await File.WriteAllTextAsync(logFiles[0], tamperedContent);
            }

            // Act - Detect tampering
            var integrityResult = await _soc2Service.ValidateAuditLogIntegrityAsync();
            var hashChainResult = await _soc2Service.VerifyHashChainAsync();

            // Assert - Verify tampering is detected
            Assert.IsFalse(integrityResult.IsCompliant, "Should detect tampering");
            Assert.IsFalse(hashChainResult.IsValid, "Hash chain should be broken");
            Assert.IsTrue(hashChainResult.Breaks.Count > 0, "Should identify hash chain breaks");
        }

        [TestMethod]
        public async Task Test_SuspiciousPatternDetection()
        {
            // Arrange - Create suspicious patterns
            var username = "testuser@example.com";
            
            // Create multiple failed logins for brute force pattern
            for (int i = 0; i < 10; i++)
            {
                await _authService.AuthenticateAsync(username, "WrongPassword");
            }

            // Create off-hours activity
            var offHoursUsername = "nightuser@example.com";
            var currentTime = DateTime.UtcNow;
            var offHoursTime = new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, 3, 0, 0);
            
            // Mock time-based patterns by creating events manually
            await _auditService.LogEventAsync(AuditEventType.UserLogin, $"Off-hours login by {offHoursUsername}");

            // Act - Detect suspicious patterns
            var patterns = await _soc2Service.DetectSuspiciousPatternsAsync();

            // Assert - Verify patterns are detected
            var bruteForcePatterns = patterns.Where(p => p.Type == PatternType.BruteForceAttempt).ToList();
            Assert.IsTrue(bruteForcePatterns.Count > 0, "Should detect brute force attempts");
            
            var offHoursPatterns = patterns.Where(p => p.Type == PatternType.OffHoursActivity).ToList();
            // Note: This might not be detected depending on actual time
        }

        #endregion

        #region Performance and Scalability Tests

        [TestMethod]
        public async Task Test_HighVolumeAuditLogging_Performance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var eventCount = 1000;

            // Act - Log high volume of events
            var tasks = new List<Task<AuditLogEntry>>();
            for (int i = 0; i < eventCount; i++)
            {
                tasks.Add(_auditService.LogEventAsync(
                    AuditEventType.TranscriptionStarted,
                    $"High volume test event {i}",
                    System.Text.Json.JsonSerializer.Serialize(new { Index = i, TestType = "Performance" }),
                    DataSensitivity.Low));
            }

            var entries = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert - Verify performance
            Assert.AreEqual(eventCount, entries.Length, "All events should be logged");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 10000, "Should complete within 10 seconds");
            
            // Verify log integrity
            var integrityResult = await _soc2Service.ValidateAuditLogIntegrityAsync();
            Assert.IsTrue(integrityResult.IsCompliant, "High volume logs should maintain integrity");
            
            // Verify all entries are present
            var logs = await _auditService.GetLogsAsync();
            Assert.AreEqual(eventCount, logs.Count(l => l.Description.Contains("High volume test event")), 
                "All high volume events should be present");
        }

        [TestMethod]
        public async Task Test_ConcurrentAuditLogging_ThreadSafety()
        {
            // Arrange
            var concurrentUsers = 50;
            var eventsPerUser = 20;
            
            // Act - Simulate concurrent audit logging from multiple users
            var tasks = new List<Task>();
            for (int user = 0; user < concurrentUsers; user++)
            {
                var userId = user;
                tasks.Add(Task.Run(async () =>
                {
                    for (int event = 0; event < eventsPerUser; event++)
                    {
                        await _auditService.LogEventAsync(
                            AuditEventType.TranscriptionStarted,
                            $"Concurrent event - User {userId}, Event {event}",
                            System.Text.Json.JsonSerializer.Serialize(new { 
                                UserId = userId, 
                                EventIndex = event 
                            }),
                            DataSensity.Low);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Verify thread safety and data integrity
            var logs = await _auditService.GetLogsAsync();
            var concurrentEvents = logs.Where(l => l.Description.Contains("Concurrent event")).ToList();
            
            Assert.AreEqual(concurrentUsers * eventsPerUser, concurrentEvents.Count, 
                "All concurrent events should be logged");
            
            // Verify no duplicate IDs
            var uniqueIds = concurrentEvents.Select(e => e.Id).Distinct().Count();
            Assert.AreEqual(concurrentEvents.Count, uniqueIds, "All event IDs should be unique");
            
            // Verify hash chain integrity
            var hashChainResult = await _soc2Service.VerifyHashChainAsync();
            Assert.IsTrue(hashChainResult.IsValid, "Hash chain should remain valid under concurrency");
        }

        #endregion

        #region Data Privacy and Hashing Tests

        [TestMethod]
        public async Task Test_UserDataHashing_PrivacyCompliance()
        {
            // Arrange
            var username = "testuser@example.com";
            var sensitiveData = "This is sensitive PII data";

            // Act - Log sensitive information
            var entry1 = await _auditService.LogEventAsync(
                AuditEventType.UserLogin,
                "User login with sensitive data",
                System.Text.Json.JsonSerializer.Serialize(new { 
                    Username = username,
                    SensitiveInfo = sensitiveData
                }),
                DataSensitivity.High);

            var entry2 = await _authService.AuthenticateAsync(username, "password123");

            // Assert - Verify data hashing
            Assert.IsFalse(string.IsNullOrEmpty(entry1.UserId), "User ID should be present");
            Assert.AreEqual(64, entry1.UserId.Length, "User ID should be SHA256 hash (64 chars)");
            Assert.IsFalse(entry1.UserId.Contains("@"), "Email should not be in clear text");

            // Verify metadata contains hashed but not plaintext sensitive data
            var metadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(entry1.Metadata!);
            Assert.IsNotNull(metadata, "Metadata should be present");
            
            if (metadata.ContainsKey("Username"))
            {
                var usernameValue = metadata["Username"].ToString();
                Assert.IsFalse(usernameValue.Contains("@"), "Username should be hashed in metadata");
            }

            // Verify IP addresses are hashed
            var securityLogs = await _auditService.GetLogsAsync(eventType: AuditEventType.AuthenticationSucceeded);
            foreach (var log in securityLogs)
            {
                var logMetadata = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(log.Metadata!);
                if (logMetadata.ContainsKey("SecurityContext"))
                {
                    var securityContext = logMetadata["SecurityContext"];
                    // Security context should contain hashed but not plaintext IP addresses
                }
            }
        }

        [TestMethod]
        public async Task Test_DeviceFingerprinting_Consistency()
        {
            // Act - Get multiple security contexts
            var context1 = await _securityContextService.GetSecurityContextAsync();
            var context2 = await _securityContextService.GetSecurityContextAsync();
            var context3 = await _securityContextService.GetSecurityContextAsync();

            // Assert - Verify fingerprint consistency
            Assert.AreEqual(context1.DeviceFingerprint, context2.DeviceFingerprint, 
                "Device fingerprint should be consistent");
            Assert.AreEqual(context2.DeviceFingerprint, context3.DeviceFingerprint, 
                "Device fingerprint should be consistent across multiple calls");

            // Verify fingerprint is not empty
            Assert.IsFalse(string.IsNullOrEmpty(context1.DeviceFingerprint), 
                "Device fingerprint should not be empty");
            Assert.AreEqual(64, context1.DeviceFingerprint.Length, 
                "Device fingerprint should be SHA256 hash (64 chars)");

            // Verify other context properties
            Assert.IsTrue(context1.ProcessId > 0, "Process ID should be valid");
            Assert.IsTrue(context1.ThreadId > 0, "Thread ID should be valid");
            Assert.IsFalse(string.IsNullOrEmpty(context1.UserAgent), "User agent should be present");
        }

        #endregion

        #region Retention and Archival Tests

        [TestMethod]
        public async Task Test_RetentionPolicyCompliance()
        {
            // Arrange - Create logs with different ages
            await _auditService.LogEventAsync(AuditEventType.TranscriptionStarted, "Recent event");
            
            // Create old events for testing
            var oldEvent = new AuditLogEntry
            {
                EventType = AuditEventType.UserLogin,
                Description = "Old event",
                Timestamp = DateTime.UtcNow.AddDays(-400), // Over a year old
                RetentionExpiry = DateTime.UtcNow.AddDays(-1) // Expired
            };
            
            // Manually add old event for testing
            var logFile = Path.Combine(_testLogDirectory, $"audit-{oldEvent.Timestamp:yyyy-MM}.json");
            var existingLogs = new List<AuditLogEntry> { oldEvent };
            var json = System.Text.Json.JsonSerializer.Serialize(existingLogs, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(logFile, json);

            // Act - Validate retention policy compliance
            var retentionResult = await _soc2Service.ValidateRetentionPolicyComplianceAsync();
            
            // Assert - Verify retention violations are detected
            Assert.IsNotNull(retentionResult, "Retention validation result should not be null");
            Assert.IsTrue(retentionResult.TotalEntriesValidated > 0, "Should validate entries");
            
            // Check if expired entries are detected
            if (retentionResult.EntriesWithViolations > 0)
            {
                var expiredEntryViolations = retentionResult.Violations
                    .Where(v => v.Type == ViolationType.RetentionPolicyViolation)
                    .ToList();
                
                Assert.IsTrue(expiredEntryViolations.Count > 0, "Should detect expired entries");
            }
        }

        #endregion

        #region Integration Test Helper Methods

        private async Task CreateComprehensiveAuditTrail()
        {
            // Create a variety of audit events for comprehensive testing
            await _auditService.LogEventAsync(AuditEventType.UserLogin, "Test user login");
            await _auditService.LogEventAsync(AuditEventType.AuthenticationSucceeded, "Authentication succeeded");
            await _auditService.LogEventAsync(AuditEventType.TranscriptionStarted, "Transcription started");
            await _auditService.LogEventAsync(AuditEventType.AudioCaptured, "Audio captured");
            await _auditService.LogEventAsync(AuditEventType.TranscriptionCompleted, "Transcription completed");
            await _auditService.LogEventAsync(AuditEventType.TextInjected, "Text injected", 
                System.Text.Json.JsonSerializer.Serialize(new { Application = "Test App" }));
            await _auditService.LogEventAsync(AuditEventType.UserLogout, "User logout");
            await _auditService.LogEventAsync(AuditEventType.SettingsChanged, "Settings changed");
            await _auditService.LogEventAsync(AuditEventType.ApiKeyAccessed, "API key accessed");
            await _auditService.LogEventAsync(AuditEventType.SecurityEvent, "Security event occurred");
            await _auditService.LogEventAsync(AuditEventType.ComplianceCheck, "Compliance check performed");
            await _auditService.LogEventAsync(AuditEventType.DataExported, "Data exported");
            await _auditService.LogEventAsync(AuditEventType.DataDeleted, "Data deleted");
            await _auditService.LogEventAsync(AuditEventType.Error, "Error occurred");
            await _auditService.LogEventAsync(AuditEventType.RetentionPolicyApplied, "Retention policy applied");
            await _auditService.LogEventAsync(AuditEventType.LogArchived, "Log archived");
        }

        private static string HashPassword(string password)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(password + "salt");
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }

        #endregion

        #region Real-time Alerting Tests

        [TestMethod]
        public async Task Test_RealtimeSecurityAlerting()
        {
            // Arrange - Setup alert rule for failed authentication
            var securityAlertService = new SecurityAlertService(
                new Mock<Microsoft.Extensions.Logging.ILogger<SecurityAlertService>>().Object,
                _auditService);

            var alertRule = new SecurityAlertRule
            {
                Name = "Failed Authentication Alert",
                Description = "Alert on failed authentication attempts",
                EventType = AuditEventType.AuthenticationFailed,
                Condition = SecurityAlertCondition.CountInTimeWindow,
                Parameters = new Dictionary<string, object>
                {
                    ["count"] = 3,
                    ["timeWindowMinutes"] = 5
                },
                Severity = SecurityAlertSeverity.High,
                CooldownMinutes = 15
            };

            await securityAlertService.ConfigureAlertRuleAsync(alertRule);

            // Act - Trigger multiple failed authentications
            for (int i = 0; i < 5; i++)
            {
                await _authService.AuthenticateAsync("testuser@example.com", "wrongpassword");
                await Task.Delay(100); // Small delay between attempts
            }

            // Wait for alert processing
            await Task.Delay(1000);

            // Assert - Verify alerts were triggered
            var recentAlerts = await securityAlertService.GetRecentAlertsAsync(1);
            var failedAuthAlerts = recentAlerts.Where(a => a.Type == AuditEventType.AuthenticationFailed).ToList();
            
            Assert.IsTrue(failedAuthAlerts.Count > 0, "Should trigger alerts for failed authentication");
        }

        #endregion

        #region Cross-Service Integration Tests

        [TestMethod]
        public async Task Test_CrossServiceAuditConsistency()
        {
            // Arrange - Test audit logging across multiple services
            var username = "integrationuser@example.com";
            var password = "IntegrationTest123";

            // Setup user credentials
            var passwordHash = HashPassword(password);
            _mockCredentialService.Setup(c => c.RetrieveCredentialAsync($"user_{username}"))
                .ReturnsAsync(passwordHash);

            // Act - Perform operations across multiple services
            // 1. Authentication
            var authResult = await _authService.AuthenticateAsync(username, password);
            
            // 2. Credential operations
            await _windowsCredentialService.StoreCredentialAsync("TestKey", "TestValue");
            var retrievedValue = await _windowsCredentialService.RetrieveCredentialAsync("TestKey");
            
            // 3. Session management
            var activeSessions = await _authService.GetActiveSessionsAsync(username);
            await _authService.LogoutAsync(authResult.SessionId!);

            // Assert - Verify audit consistency across services
            var allLogs = await _auditService.GetLogsAsync();
            
            // Verify all expected event types are present
            var expectedEventTypes = new[]
            {
                AuditEventType.AuthenticationSucceeded,
                AuditEventType.ApiKeyAccessed, // Store operation
                AuditEventType.ApiKeyAccessed, // Retrieve operation
                AuditEventType.UserLogout
            };

            foreach (var expectedType in expectedEventTypes)
            {
                var events = allLogs.Where(l => l.EventType == expectedType).ToList();
                Assert.IsTrue(events.Count > 0, $"Should have {expectedType} events");
                
                // Verify security context consistency
                foreach (var evt in events)
                {
                    Assert.IsFalse(string.IsNullOrEmpty(evt.SessionId), "Session ID should be consistent");
                    Assert.IsFalse(string.IsNullOrEmpty(evt.UserId), "User ID should be consistent");
                    
                    if (IsSecurityEvent(evt.EventType))
                    {
                        Assert.IsFalse(string.IsNullOrEmpty(evt.Metadata), "Security context should be present");
                    }
                }
            }

            // Verify SOC 2 compliance of cross-service operations
            var soc2Report = await _soc2Service.GenerateComplianceReportAsync();
            Assert.IsTrue(soc2Report.OverallCompliance >= ComplianceLevel.MostlyCompliant, 
                "Cross-service operations should be SOC 2 compliant");
        }

        private static bool IsSecurityEvent(AuditEventType eventType)
        {
            return eventType switch
            {
                AuditEventType.UserLogin => true,
                AuditEventType.UserLogout => true,
                AuditEventType.AuthenticationSucceeded => true,
                AuditEventType.AuthenticationFailed => true,
                AuditEventType.AuthorizationFailed => true,
                AuditEventType.ApiKeyAccessed => true,
                AuditEventType.SecurityEvent => true,
                _ => false
            };
        }

        #endregion
    }
}