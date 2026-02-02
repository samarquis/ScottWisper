using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class EnterpriseDeploymentTests
    {
        private IEnterpriseDeploymentService _deploymentService = null!;
        private IWebhookService _webhookService = null!;

        [TestInitialize]
        public void Setup()
        {
            _deploymentService = new EnterpriseDeploymentService(NullLogger<EnterpriseDeploymentService>.Instance);
            _webhookService = new WebhookService(NullLogger<WebhookService>.Instance);
        }

        #region Deployment Configuration Tests

        [TestMethod]
        public async Task Test_GenerateDeploymentConfig_MSI()
        {
            var config = await _deploymentService.GenerateDeploymentConfigAsync("TestCorp", DeploymentType.MSI);
            
            Assert.IsNotNull(config);
            Assert.AreEqual("TestCorp", config.OrganizationName);
            Assert.AreEqual(DeploymentType.MSI, config.Type);
            Assert.AreEqual(InstallationScope.PerMachine, config.Scope);
            Assert.IsTrue(config.AllUsers);
            Assert.IsFalse(config.CreateDesktopShortcut);
            Assert.IsTrue(config.CreateStartMenuShortcut);
            Assert.IsTrue(config.SilentOptions.Enabled);
            Assert.IsTrue(config.SilentOptions.SuppressUI);
        }

        [TestMethod]
        public async Task Test_GenerateDeploymentConfig_GPO()
        {
            var config = await _deploymentService.GenerateDeploymentConfigAsync("EnterpriseCo", DeploymentType.GPO);
            
            Assert.IsNotNull(config);
            Assert.AreEqual("EnterpriseCo", config.OrganizationName);
            Assert.AreEqual(DeploymentType.GPO, config.Type);
        }

        [TestMethod]
        public async Task Test_GenerateDeploymentConfig_DefaultSettings()
        {
            var config = await _deploymentService.GenerateDeploymentConfigAsync("TestOrg", DeploymentType.MSI);
            
            Assert.IsTrue(config.Settings.EnableAutoPunctuation);
            Assert.IsTrue(config.Settings.EnableVoiceCommands);
            Assert.IsTrue(config.Settings.EnableAuditLogging);
            Assert.AreEqual("General", config.Settings.ComplianceFramework);
            Assert.AreEqual(30, config.Settings.RetentionDays);
        }

        #endregion

        #region Webhook Service Tests

        [TestMethod]
        public async Task Test_Webhook_Configure()
        {
            var config = new WebhookConfig
            {
                Enabled = true,
                EndpointUrl = "https://api.example.com/webhook",
                AuthToken = "test-token-123",
                TriggerEvents = new System.Collections.Generic.List<WebhookEventType> { WebhookEventType.TranscriptionCompleted }
            };

            await _webhookService.ConfigureAsync(config);

            var retrievedConfig = _webhookService.GetConfig();
            Assert.AreEqual("https://api.example.com/webhook", retrievedConfig.EndpointUrl);
            Assert.IsTrue(_webhookService.IsEnabled);
        }

        [TestMethod]
        public async Task Test_Webhook_SendEvent()
        {
            var config = new WebhookConfig
            {
                Enabled = false, // Disable so we don't make real HTTP calls
                TriggerEvents = new System.Collections.Generic.List<WebhookEventType> { WebhookEventType.TranscriptionCompleted }
            };

            await _webhookService.ConfigureAsync(config);

            var result = await _webhookService.SendWebhookAsync(
                WebhookEventType.TranscriptionCompleted,
                new System.Collections.Generic.Dictionary<string, object> { ["test"] = true });

            Assert.IsTrue(result.Skipped); // Should be skipped since not enabled
        }

        [TestMethod]
        public void Test_Webhook_SetEnabled()
        {
            var config = new WebhookConfig { Enabled = false };
            _webhookService.ConfigureAsync(config).Wait();

            Assert.IsFalse(_webhookService.IsEnabled);

            _webhookService.SetEnabled(true);
            Assert.IsTrue(_webhookService.IsEnabled);

            _webhookService.SetEnabled(false);
            Assert.IsFalse(_webhookService.IsEnabled);
        }

        [TestMethod]
        public async Task Test_Webhook_Statistics_Empty()
        {
            var config = new WebhookConfig { Enabled = false };
            await _webhookService.ConfigureAsync(config);

            var stats = await _webhookService.GetStatisticsAsync();

            Assert.AreEqual(0, stats.TotalSent);
            Assert.AreEqual(0, stats.Successful);
            Assert.AreEqual(0, stats.Failed);
        }

        [TestMethod]
        public async Task Test_Webhook_PayloadGeneration()
        {
            var payload = new WebhookPayload
            {
                EventType = WebhookEventType.TranscriptionCompleted,
                UserId = "user123",
                SessionId = "session456",
                Data = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["transcription"] = "test text",
                    ["duration"] = 1000
                }
            };

            Assert.IsNotNull(payload.Id);
            Assert.IsNotNull(payload.Timestamp);
            Assert.AreEqual(WebhookEventType.TranscriptionCompleted, payload.EventType);
            Assert.IsTrue(payload.Data.Count > 0);
        }

        [TestMethod]
        public void Test_Webhook_SignatureGeneration()
        {
            var payload = new WebhookPayload
            {
                Id = "test-id-123",
                Timestamp = DateTime.UtcNow
            };

            var secret = "my-webhook-secret";
            var signature = payload.GenerateSignature(secret);

            Assert.IsFalse(string.IsNullOrEmpty(signature));
            // Signature should be base64
            try
            {
                Convert.FromBase64String(signature);
            }
            catch
            {
                Assert.Fail("Signature is not valid base64");
            }
        }

        #endregion

        #region MSI Installer Configuration Tests

        [TestMethod]
        public void Test_SilentInstallOptions_Defaults()
        {
            var options = new SilentInstallOptions();

            Assert.IsTrue(options.Enabled);
            Assert.IsTrue(options.SuppressUI);
            Assert.IsTrue(options.SuppressReboot);
            Assert.IsNotNull(options.MsiProperties);
            Assert.IsTrue(options.MsiProperties.ContainsKey("INSTALLDIR"));
            Assert.IsTrue(options.MsiProperties.ContainsKey("ALLUSERS"));
        }

        [TestMethod]
        public void Test_PreconfiguredSettings_Defaults()
        {
            var settings = new PreconfiguredSettings();

            Assert.IsTrue(settings.EnableAutoPunctuation);
            Assert.IsTrue(settings.EnableVoiceCommands);
            Assert.IsTrue(settings.EnableAuditLogging);
            Assert.AreEqual("General", settings.ComplianceFramework);
            Assert.AreEqual(30, settings.RetentionDays);
            Assert.AreEqual("en-US", settings.DefaultLanguage);
            Assert.IsFalse(settings.LockSettings);
        }

        [TestMethod]
        public void Test_EnterpriseDeploymentConfig_Properties()
        {
            var config = new EnterpriseDeploymentConfig
            {
                OrganizationName = "TestCorp",
                Type = DeploymentType.MSI,
                Scope = InstallationScope.PerMachine,
                LicenseKey = "AAAA-BBBB-CCCC-DDDD",
                IsTrial = false
            };

            Assert.AreEqual("TestCorp", config.OrganizationName);
            Assert.AreEqual(DeploymentType.MSI, config.Type);
            Assert.AreEqual(InstallationScope.PerMachine, config.Scope);
            Assert.AreEqual("AAAA-BBBB-CCCC-DDDD", config.LicenseKey);
            Assert.IsFalse(config.IsTrial);
            Assert.IsNotNull(config.SilentOptions);
            Assert.IsNotNull(config.Settings);
            Assert.IsNotNull(config.Webhook);
        }

        #endregion

        #region Installation Detection Tests

        [TestMethod]
        public async Task Test_DetectInstallation_NotInstalled()
        {
            var info = await _deploymentService.DetectInstallationAsync();

            // Should not throw, just return not installed
            Assert.IsNotNull(info);
            // Actual installation status depends on whether app is installed on this machine
        }

        [TestMethod]
        public async Task Test_GetInstalledVersion_NotInstalled()
        {
            var version = await _deploymentService.GetInstalledVersionAsync();

            // Will be null if not installed
            Assert.IsNull(version);
        }

        [TestMethod]
        public async Task Test_IsUpgradeNeeded_NotInstalled()
        {
            var needsUpgrade = await _deploymentService.IsUpgradeNeededAsync("2.0.0");

            Assert.IsTrue(needsUpgrade); // Should return true if not installed
        }

        [TestMethod]
        public async Task Test_IsUpgradeNeeded_LowerVersion()
        {
            // Mock would be needed to test with actual version
            // This tests the logic path
            var needsUpgrade = await _deploymentService.IsUpgradeNeededAsync("2.0.0");
            
            // Since we're not installed, it should return true
            Assert.IsTrue(needsUpgrade);
        }

        #endregion

        #region License Validation Tests

        [TestMethod]
        public async Task Test_ValidateLicenseKey_Valid()
        {
            var validKey = "AAAA-BBBB-CCCC-DDDD";
            var isValid = await _deploymentService.ValidateLicenseKeyAsync(validKey);

            Assert.IsTrue(isValid);
        }

        [TestMethod]
        public async Task Test_ValidateLicenseKey_InvalidFormat()
        {
            var invalidKeys = new[]
            {
                "invalid",
                "AAAA-BBBB-CCCC",
                "",
                null
            };

            foreach (var key in invalidKeys)
            {
                var isValid = await _deploymentService.ValidateLicenseKeyAsync(key!);
                Assert.IsFalse(isValid, $"Key '{key}' should be invalid");
            }
        }

        #endregion

        #region GPO Deployment Tests

        [TestMethod]
        public async Task Test_GenerateGpoScript()
        {
            var config = new GpoDeploymentConfig
            {
                GpoName = "WhisperKey-Test",
                OrganizationalUnit = "OU=Workstations,DC=example,DC=com",
                AssignmentType = GpoAssignmentType.Computer,
                ForceReinstall = false,
                UninstallExisting = false
            };

            var script = await _deploymentService.GenerateGpoScriptAsync(config);

            Assert.IsNotNull(script);
            Assert.IsTrue(script.Contains("WhisperKey GPO Deployment Script"));
            Assert.IsTrue(script.Contains("New-GPO"));
            Assert.IsTrue(script.Contains("Import-Module GroupPolicy"));
        }

        [TestMethod]
        public void Test_GpoDeploymentConfig_Properties()
        {
            var config = new GpoDeploymentConfig
            {
                GpoName = "Test-GPO",
                OrganizationalUnit = "OU=Test,DC=corp,DC=com",
                TargetComputers = "CN=Workstations,OU=Groups,DC=corp,DC=com",
                TargetUsers = "CN=Users,OU=Groups,DC=corp,DC=com",
                ForceReinstall = true,
                UninstallExisting = true,
                AssignmentType = GpoAssignmentType.User
            };

            Assert.AreEqual("Test-GPO", config.GpoName);
            Assert.AreEqual("OU=Test,DC=corp,DC=com", config.OrganizationalUnit);
            Assert.AreEqual("CN=Workstations,OU=Groups,DC=corp,DC=com", config.TargetComputers);
            Assert.AreEqual("CN=Users,OU=Groups,DC=corp,DC=com", config.TargetUsers);
            Assert.IsTrue(config.ForceReinstall);
            Assert.IsTrue(config.UninstallExisting);
            Assert.AreEqual(GpoAssignmentType.User, config.AssignmentType);
        }

        #endregion

        #region Webhook Config Tests

        [TestMethod]
        public void Test_WebhookConfig_Defaults()
        {
            var config = new WebhookConfig();

            Assert.IsFalse(config.Enabled);
            Assert.AreEqual(30, config.TimeoutSeconds);
            Assert.AreEqual(3, config.RetryCount);
            Assert.IsNotNull(config.TriggerEvents);
            Assert.IsNotNull(config.CustomHeaders);
        }

        [TestMethod]
        public void Test_WebhookConfig_CustomSettings()
        {
            var config = new WebhookConfig
            {
                Enabled = true,
                EndpointUrl = "https://hooks.example.com/WhisperKey",
                AuthToken = "auth-123",
                Secret = "webhook-secret",
                TimeoutSeconds = 60,
                RetryCount = 5,
                TriggerEvents = new System.Collections.Generic.List<WebhookEventType>
                {
                    WebhookEventType.TranscriptionCompleted,
                    WebhookEventType.Error
                },
                CustomHeaders = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["X-Organization-ID"] = "org-123",
                    ["X-Source"] = "WhisperKey"
                }
            };

            Assert.IsTrue(config.Enabled);
            Assert.AreEqual("https://hooks.example.com/WhisperKey", config.EndpointUrl);
            Assert.AreEqual("auth-123", config.AuthToken);
            Assert.AreEqual("webhook-secret", config.Secret);
            Assert.AreEqual(60, config.TimeoutSeconds);
            Assert.AreEqual(5, config.RetryCount);
            Assert.AreEqual(2, config.TriggerEvents.Count);
            Assert.AreEqual(2, config.CustomHeaders.Count);
        }

        #endregion

        #region Installation Result Tests

        [TestMethod]
        public void Test_DeploymentResult_Properties()
        {
            var result = new DeploymentResult
            {
                Success = true,
                ExitCode = 0,
                InstallPath = "C:\\Program Files\\WhisperKey",
                Version = "1.0.0",
                Duration = TimeSpan.FromSeconds(30)
            };

            Assert.IsTrue(result.Success);
            Assert.AreEqual(0, result.ExitCode);
            Assert.AreEqual("C:\\Program Files\\WhisperKey", result.InstallPath);
            Assert.AreEqual("1.0.0", result.Version);
            Assert.AreEqual(30, result.Duration.TotalSeconds);
        }

        [TestMethod]
        public void Test_InstallationInfo_Properties()
        {
            var info = new InstallationInfo
            {
                IsInstalled = true,
                InstallPath = "C:\\Program Files\\WhisperKey",
                Version = "1.0.0",
                ProductCode = "{12345678-1234-1234-1234-123456789012}",
                Scope = InstallationScope.PerMachine
            };

            Assert.IsTrue(info.IsInstalled);
            Assert.AreEqual("C:\\Program Files\\WhisperKey", info.InstallPath);
            Assert.AreEqual("1.0.0", info.Version);
            Assert.AreEqual("{12345678-1234-1234-1234-123456789012}", info.ProductCode);
            Assert.AreEqual(InstallationScope.PerMachine, info.Scope);
        }

        #endregion

        #region Deployment Tests

        [TestMethod]
        public async Task Test_ConfigureEnterpriseSettings_NotInstalled()
        {
            // Test when application is not installed
            var config = new EnterpriseDeploymentConfig
            {
                OrganizationName = "TestCorp",
                Settings = new PreconfiguredSettings
                {
                    ApiKey = "test-api-key",
                    EnableAuditLogging = true
                }
            };

            var result = await _deploymentService.ConfigureEnterpriseSettingsAsync(config);
            
            // Should return false since not installed
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task Test_GetDeploymentLog_NoLogs()
        {
            var logPath = await _deploymentService.GetDeploymentLogAsync();
            
            // May return null if no logs exist
            // Or may return path to an old log
            // Test just verifies it doesn't throw
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task Test_InstallSilently_MissingMSI()
        {
            var result = await _deploymentService.InstallSilentlyAsync("nonexistent.msi");

            Assert.IsFalse(result.Success);
            Assert.IsNotNull(result.ErrorMessage);
            Assert.IsTrue(result.ErrorMessage.Contains("not found"));
        }

        [TestMethod]
        public async Task Test_UninstallAsync_DefaultProductCode()
        {
            // This will attempt to uninstall with default product code
            // Since product is likely not installed, it should handle gracefully
            var result = await _deploymentService.UninstallAsync();

            // Exit code 1605 means product not installed - that's OK for our test
            Assert.IsTrue(result.ExitCode == 0 || result.ExitCode == 1605);
        }

        #endregion
    }
}
