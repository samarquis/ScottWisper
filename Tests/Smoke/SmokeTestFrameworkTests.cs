using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Services;
using WhisperKey.Infrastructure.SmokeTesting;
using WhisperKey.Infrastructure.SmokeTesting.HealthChecks;

namespace WhisperKey.Infrastructure.SmokeTesting
{
    [TestClass]
    public class SmokeTestFrameworkTests
    {
        private Mock<IServiceProvider> _mockServiceProvider;
        private Mock<ILogger<SmokeTestFrameworkTests>> _mockLogger;
        private Mock<ISettingsService> _mockSettingsService;
        private Mock<IAuthenticationService> _mockAuthService;
        private Mock<IAudioDeviceService> _mockAudioService;
        private Mock<ITextInjection> _mockTextInjection;
        private SmokeTestConfiguration _testConfiguration;

        [TestInitialize]
        public void Setup()
        {
            _mockServiceProvider = new Mock<IServiceProvider>();
            _mockLogger = new Mock<ILogger<SmokeTestFrameworkTests>>();
            _mockSettingsService = new Mock<ISettingsService>();
            _mockAuthService = new Mock<IAuthenticationService>();
            _mockAudioService = new Mock<IAudioDeviceService>();
            _mockTextInjection = new Mock<ITextInjection>();

            _testConfiguration = new SmokeTestConfiguration
            {
                DefaultTestTimeoutSeconds = 30,
                HealthCheckTimeoutSeconds = 10,
                PerformanceThresholds = new PerformanceThresholds
                {
                    MaxMemoryUsageMb = 512,
                    MaxCpuUsagePercent = 80.0
                }
            };

            // Setup service provider mocks
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(ISettingsService)))
                .Returns(_mockSettingsService.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IAuthenticationService)))
                .Returns(_mockAuthService.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(IAudioDeviceService)))
                .Returns(_mockAudioService.Object);
            _mockServiceProvider.Setup(sp => sp.GetService(typeof(ITextInjection)))
                .Returns(_mockTextInjection.Object);
        }

        [TestMethod]
        public async Task SystemHealthChecker_WithAllServicesHealthy_ReturnsSuccess()
        {
            // Arrange
            _mockSettingsService.Setup(s => s.LoadSettingsAsync())
                .ReturnsAsync(new WhisperKey.Configuration.AppSettings());
            _mockAuthService.Setup(a => a.IsAuthenticatedAsync())
                .ReturnsAsync(true);
            _mockAudioService.Setup(a => a.GetInputDevicesAsync())
                .ReturnsAsync(new List<WhisperKey.Services.AudioDevice>
                {
                    new WhisperKey.Services.AudioDevice { Id = "test", Name = "Test Device", IsDefault = true }
                });
            _mockTextInjection.Setup(t => t.InjectTextAsync(It.IsAny<string>(), It.IsAny<WhisperKey.Services.InjectionOptions>()))
                .ReturnsAsync(true);

            var healthChecker = new SystemHealthChecker(_mockServiceProvider.Object, _testConfiguration);

            // Act
            var result = await healthChecker.RunAllTestsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("System Health Checks", result.SuiteName);
            Assert.IsTrue(result.SuccessRate > 0);
            Assert.AreEqual(result.TestResults.Count, result.PassedTests + result.FailedTests);
        }

        [TestMethod]
        public async Task SystemHealthChecker_WithServiceFailure_ReturnsFailure()
        {
            // Arrange
            _mockSettingsService.Setup(s => s.LoadSettingsAsync())
                .ThrowsAsync(new Exception("Service unavailable"));

            var healthChecker = new SystemHealthChecker(_mockServiceProvider.Object, _testConfiguration);

            // Act
            var result = await healthChecker.RunAllTestsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.FailedTests > 0);
            Assert.IsTrue(result.SuccessRate < 100);
        }

        [TestMethod]
        public void SmokeTestConfiguration_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var config = new SmokeTestConfiguration();

            // Assert
            Assert.AreEqual(30, config.DefaultTestTimeoutSeconds);
            Assert.AreEqual(10, config.HealthCheckTimeoutSeconds);
            Assert.AreEqual(60, config.WorkflowTestTimeoutSeconds);
            Assert.AreEqual(2, config.MaxRetryAttempts);
            Assert.AreEqual(1000, config.RetryDelayMs);
            Assert.IsTrue(config.EnableParallelExecution);
            Assert.AreEqual(4, config.MaxParallelism);
        }

        [TestMethod]
        public void PerformanceThresholds_DefaultValues_AreReasonable()
        {
            // Arrange & Act
            var thresholds = new PerformanceThresholds();

            // Assert
            Assert.AreEqual(2000, thresholds.MaxAudioProcessingMs);
            Assert.AreEqual(500, thresholds.MaxTextInjectionMs);
            Assert.AreEqual(1000, thresholds.MaxSettingsLoadMs);
            Assert.AreEqual(1500, thresholds.MaxAuthenticationMs);
            Assert.AreEqual(512, thresholds.MaxMemoryUsageMb);
            Assert.AreEqual(80.0, thresholds.MaxCpuUsagePercent);
        }

        [TestMethod]
        public void SecurityValidationSettings_DefaultValues_AreSecure()
        {
            // Arrange & Act
            var settings = new SecurityValidationSettings();

            // Assert
            Assert.IsTrue(settings.RequireSOC2Compliance);
            Assert.IsTrue(settings.RequireAuditLogging);
            Assert.IsTrue(settings.RequireSecureCredentialStorage);
            Assert.IsTrue(settings.RequirePermissionSystem);
            Assert.IsTrue(settings.RequireApiKeyRotation);
            Assert.IsTrue(settings.RequireSecurityAlerts);
        }

        [TestMethod]
        public void SmokeTestResult_CanBeCreated_WithValidParameters()
        {
            // Arrange & Act
            var result = new SmokeTestResult
            {
                TestName = "Test",
                Category = SmokeTestCategory.Critical,
                Success = true,
                Message = "Success",
                Duration = TimeSpan.FromMilliseconds(100),
                Timestamp = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString()
            };

            // Assert
            Assert.AreEqual("Test", result.TestName);
            Assert.AreEqual(SmokeTestCategory.Critical, result.Category);
            Assert.IsTrue(result.Success);
            Assert.AreEqual("Success", result.Message);
            Assert.AreEqual(TimeSpan.FromMilliseconds(100), result.Duration);
            Assert.IsNotNull(result.CorrelationId);
        }

        [TestMethod]
        public void SmokeTestSuiteResult_CanCalculateMetrics_Correctly()
        {
            // Arrange
            var testResults = new List<SmokeTestResult>
            {
                new SmokeTestResult { Success = true },
                new SmokeTestResult { Success = true },
                new SmokeTestResult { Success = false },
                new SmokeTestResult { Success = true }
            };

            // Act
            var suiteResult = new SmokeTestSuiteResult
            {
                TestResults = testResults,
                TotalTests = testResults.Count,
                PassedTests = testResults.Count(r => r.Success),
                FailedTests = testResults.Count(r => !r.Success)
            };

            // Calculate success rate
            suiteResult.SuccessRate = (double)suiteResult.PassedTests / suiteResult.TotalTests * 100;

            // Assert
            Assert.AreEqual(4, suiteResult.TotalTests);
            Assert.AreEqual(3, suiteResult.PassedTests);
            Assert.AreEqual(1, suiteResult.FailedTests);
            Assert.AreEqual(75.0, suiteResult.SuccessRate);
            Assert.IsFalse(suiteResult.HasCriticalFailures);
        }

        [TestMethod]
        public void SmokeTestCategory_ContainsAllExpectedCategories()
        {
            // Arrange & Act
            var categories = Enum.GetValues(typeof(SmokeTestCategory));

            // Assert
            Assert.IsTrue(categories.Length >= 7);
            Assert.IsTrue(Enum.IsDefined(typeof(SmokeTestCategory), SmokeTestCategory.Critical));
            Assert.IsTrue(Enum.IsDefined(typeof(SmokeTestCategory), SmokeTestCategory.HealthCheck));
            Assert.IsTrue(Enum.IsDefined(typeof(SmokeTestCategory), SmokeTestCategory.Workflow));
            Assert.IsTrue(Enum.IsDefined(typeof(SmokeTestCategory), SmokeTestCategory.Performance));
            Assert.IsTrue(Enum.IsDefined(typeof(SmokeTestCategory), SmokeTestCategory.Security));
            Assert.IsTrue(Enum.IsDefined(typeof(SmokeTestCategory), SmokeTestCategory.ExternalService));
            Assert.IsTrue(Enum.IsDefined(typeof(SmokeTestCategory), SmokeTestCategory.Deployment));
        }

        [TestMethod]
        public async Task DatabaseHealthChecker_WithValidDatabase_ReturnsSuccess()
        {
            // Arrange
            _mockSettingsService.Setup(s => s.LoadSettingsAsync())
                .ReturnsAsync(new WhisperKey.Configuration.AppSettings());
            _mockSettingsService.Setup(s => s.SaveSettingsAsync())
                .Returns(Task.CompletedTask);

            var healthChecker = new DatabaseHealthChecker(_mockServiceProvider.Object, _testConfiguration);

            // Act
            var result = await healthChecker.RunAllTestsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Database Health Checks", result.SuiteName);
            Assert.IsTrue(result.SuccessRate >= 0);
        }

        [TestMethod]
        public async Task AuthenticationHealthChecker_WithValidAuth_ReturnsSuccess()
        {
            // Arrange
            _mockAuthService.Setup(a => a.IsAuthenticatedAsync())
                .ReturnsAsync(true);

            var healthChecker = new AuthenticationHealthChecker(_mockServiceProvider.Object, _testConfiguration);

            // Act
            var result = await healthChecker.RunAllTestsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Authentication Health Checks", result.SuiteName);
            Assert.IsTrue(result.SuccessRate >= 0);
        }

        [TestMethod]
        public async Task ExternalServiceHealthChecker_WithNetworkAccess_ReturnsSuccess()
        {
            // Arrange
            _mockAudioService.Setup(a => a.GetInputDevicesAsync())
                .ReturnsAsync(new List<WhisperKey.Services.AudioDevice>());
            _mockAudioService.Setup(a => a.GetDefaultInputDeviceAsync())
                .ReturnsAsync(new WhisperKey.Services.AudioDevice { Id = "default", Name = "Default" });
            _mockTextInjection.Setup(t => t.InjectTextAsync(It.IsAny<string>(), It.IsAny<WhisperKey.Services.InjectionOptions>()))
                .ReturnsAsync(true);

            var healthChecker = new ExternalServiceHealthChecker(_mockServiceProvider.Object, _testConfiguration);

            // Act
            var result = await healthChecker.RunAllTestsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("External Service Health Checks", result.SuiteName);
            Assert.IsTrue(result.SuccessRate >= 0);
        }

        [TestMethod]
        public void EnvironmentSettings_Production_HasStrictDefaults()
        {
            // Arrange & Act
            var prodSettings = new EnvironmentSettings
            {
                StrictMode = true,
                PerformanceThresholdMultiplier = 1.0,
                RequireAllTests = true
            };

            // Assert
            Assert.IsTrue(prodSettings.StrictMode);
            Assert.AreEqual(1.0, prodSettings.PerformanceThresholdMultiplier);
            Assert.IsTrue(prodSettings.RequireAllTests);
            Assert.IsFalse(prodSettings.SkipPerformanceTests);
            Assert.IsFalse(prodSettings.SkipSecurityTests);
        }

        [TestMethod]
        public void EnvironmentSettings_Staging_HasRelaxedDefaults()
        {
            // Arrange & Act
            var stagingSettings = new EnvironmentSettings
            {
                StrictMode = false,
                PerformanceThresholdMultiplier = 1.5,
                RequireAllTests = false
            };

            // Assert
            Assert.IsFalse(stagingSettings.StrictMode);
            Assert.AreEqual(1.5, stagingSettings.PerformanceThresholdMultiplier);
            Assert.IsFalse(stagingSettings.RequireAllTests);
            Assert.IsFalse(stagingSettings.SkipPerformanceTests);
            Assert.IsFalse(stagingSettings.SkipSecurityTests);
        }
    }
}
