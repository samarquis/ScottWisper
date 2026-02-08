using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Validation;
using WhisperKey.Services.Database;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class ApiKeyManagementTests
    {
        private Mock<ICredentialService> _mockCredentialService = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private Mock<JsonDatabaseService> _mockDb = null!;
        private Mock<IInputValidationService> _mockValidationService = null!;
        private ApiKeyManagementService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockCredentialService = new Mock<ICredentialService>();
            _mockAuditService = new Mock<IAuditLoggingService>();
            
            var mockFileSystem = new Mock<IFileSystemService>();
            mockFileSystem.Setup(f => f.GetAppDataPath()).Returns(@"C:\Tests");
            
            _mockDb = new Mock<JsonDatabaseService>(mockFileSystem.Object, NullLogger<JsonDatabaseService>.Instance);
            _mockValidationService = new Mock<IInputValidationService>();

            _mockValidationService.Setup(v => v.Validate(It.IsAny<string>(), It.IsAny<ValidationRuleSet>()))
                .Returns(new WhisperKey.Services.Validation.ValidationResult { IsValid = true });

            _service = new ApiKeyManagementService(
                _mockCredentialService.Object,
                _mockAuditService.Object,
                _mockDb.Object,
                _mockValidationService.Object,
                NullLogger<ApiKeyManagementService>.Instance);
        }

        [TestMethod]
        public async Task RegisterKey_StoresInCredentialAndDb()
        {
            // Arrange
            _mockCredentialService.Setup(c => c.StoreCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync((ApiKeyMetadata?)null);

            // Act
            var result = await _service.RegisterKeyAsync("OpenAI", "Main Key", "sk-test", 30);

            // Assert
            Assert.IsTrue(result);
            _mockCredentialService.Verify(c => c.StoreCredentialAsync(It.Is<string>(s => s.Contains("OpenAI_v1")), "sk-test"), Times.Once);
            _mockDb.Verify(d => d.UpsertAsync(It.IsAny<string>(), It.IsAny<ApiKeyMetadata>(), It.IsAny<Func<ApiKeyMetadata, bool>>()), Times.Once);
        }

        [TestMethod]
        public async Task RotateKey_CreatesNewVersion()
        {
            // Arrange
            var metadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                ActiveVersionId = "v1",
                Versions = new List<ApiKeyVersion> { new ApiKeyVersion { Id = "v1", Version = 1 } }
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(metadata);
            
            _mockCredentialService.Setup(c => c.StoreCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.RotateKeyAsync("OpenAI", "sk-new");

            // Assert
            Assert.IsTrue(result);
            _mockCredentialService.Verify(c => c.StoreCredentialAsync(It.Is<string>(s => s.Contains("OpenAI_v2")), "sk-new"), Times.Once);
            Assert.AreEqual(2, metadata.Versions.Count);
        }

        [TestMethod]
        public async Task GetActiveKey_RetrievesFromCredentialManager()
        {
            // Arrange
            var metadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                ActiveVersionId = "v1",
                Versions = new List<ApiKeyVersion> { new ApiKeyVersion { Id = "v1", Version = 1, Status = ApiKeyStatus.Active, CredentialName = "ApiKey_OpenAI_v1" } }
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(metadata);
            
            _mockCredentialService.Setup(c => c.RetrieveCredentialAsync("ApiKey_OpenAI_v1"))
                .ReturnsAsync("sk-test");

            // Act
            var key = await _service.GetActiveKeyAsync("OpenAI");

            // Assert
            Assert.AreEqual("sk-test", key);
        }

        #region RevokeKeyAsync Tests

        [TestMethod]
        public async Task RevokeKey_ExistingProvider_RevokesAllVersions()
        {
            // Arrange
            var metadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                ActiveVersionId = "v1",
                Usage = new ApiKeyUsageStats(),
                Versions = new List<ApiKeyVersion>
                {
                    new ApiKeyVersion { Id = "v1", Version = 1, Status = ApiKeyStatus.Active },
                    new ApiKeyVersion { Id = "v2", Version = 2, Status = ApiKeyStatus.Active }
                }
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(metadata);
            _mockCredentialService.Setup(c => c.DeleteCredentialAsync(It.IsAny<string>())).ReturnsAsync(true);

            // Act
            var result = await _service.RevokeKeyAsync("OpenAI");

            // Assert
            Assert.IsTrue(result);
            _mockCredentialService.Verify(c => c.DeleteCredentialAsync("ApiKey_OpenAI_v1"), Times.Once);
            _mockCredentialService.Verify(c => c.DeleteCredentialAsync("ApiKey_OpenAI_v2"), Times.Once);
            Assert.AreEqual(ApiKeyStatus.Revoked, metadata.Versions[0].Status);
            Assert.AreEqual(ApiKeyStatus.Revoked, metadata.Versions[1].Status);
        }

        [TestMethod]
        public async Task RevokeKey_NonExistingProvider_ReturnsFalse()
        {
            // Arrange
            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync((ApiKeyMetadata?)null);

            // Act
            var result = await _service.RevokeKeyAsync("NonExistent");

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region ListKeysAsync Tests

        [TestMethod]
        public async Task ListKeys_ReturnsAllKeys()
        {
            // Arrange
            var keys = new List<ApiKeyMetadata>
            {
                new ApiKeyMetadata { Provider = "OpenAI", Name = "OpenAI Key" },
                new ApiKeyMetadata { Provider = "Azure", Name = "Azure Key" },
                new ApiKeyMetadata { Provider = "AWS", Name = "AWS Key" }
            };

            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(keys);

            // Act
            var result = await _service.ListKeysAsync();

            // Assert
            Assert.AreEqual(3, result.Count);
            Assert.IsTrue(result.Any(k => k.Provider == "OpenAI"));
            Assert.IsTrue(result.Any(k => k.Provider == "Azure"));
            Assert.IsTrue(result.Any(k => k.Provider == "AWS"));
        }

        [TestMethod]
        public async Task ListKeys_NoKeys_ReturnsEmptyList()
        {
            // Arrange
            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(new List<ApiKeyMetadata>());

            // Act
            var result = await _service.ListKeysAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        #endregion

        #region RecordUsageAsync Tests

        [TestMethod]
        public async Task RecordUsage_Success_UpdatesUsageStats()
        {
            // Arrange
            var metadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                ActiveVersionId = "v1",
                Usage = new ApiKeyUsageStats(),
                Versions = new List<ApiKeyVersion>
                {
                    new ApiKeyVersion { Id = "v1", Version = 1, Status = ApiKeyStatus.Active }
                }
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(metadata);

            // Act
            await _service.RecordUsageAsync("OpenAI", tokens: 100, cost: 0.02m, success: true);

            // Assert
            Assert.AreEqual(1, metadata.Usage.TotalRequests);
            Assert.AreEqual(100, metadata.Usage.TotalTokens);
            Assert.AreEqual(0.02m, metadata.Usage.TotalCost);
            Assert.AreEqual(0, metadata.Usage.FailedRequests);
            Assert.IsNotNull(metadata.Versions[0].LastUsedAt);
        }

        [TestMethod]
        public async Task RecordUsage_Failure_UpdatesFailedStats()
        {
            // Arrange
            var metadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                ActiveVersionId = "v1",
                Usage = new ApiKeyUsageStats(),
                Versions = new List<ApiKeyVersion>
                {
                    new ApiKeyVersion { Id = "v1", Version = 1, Status = ApiKeyStatus.Active }
                }
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(metadata);

            // Act
            await _service.RecordUsageAsync("OpenAI", tokens: 0, cost: 0, success: false, errorMessage: "Rate limited");

            // Assert
            Assert.AreEqual(1, metadata.Usage.TotalRequests);
            Assert.AreEqual(1, metadata.Usage.FailedRequests);
            Assert.AreEqual("Rate limited", metadata.Usage.LastErrorMessage);
            Assert.IsNotNull(metadata.Usage.LastErrorAt);
        }

        [TestMethod]
        public async Task RecordUsage_NonExistingProvider_DoesNotThrow()
        {
            // Arrange
            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync((ApiKeyMetadata?)null);

            // Act & Assert - Should not throw
            await _service.RecordUsageAsync("NonExistent", tokens: 100, cost: 0.02m, success: true);
        }

        #endregion

        #region CheckExpirationsAsync Tests

        [TestMethod]
        public async Task CheckExpirations_ExpiringSoon_ReturnsProviders()
        {
            // Arrange
            var keys = new List<ApiKeyMetadata>
            {
                new ApiKeyMetadata
                {
                    Provider = "OpenAI",
                    NotificationDays = 7,
                    ActiveVersionId = "v1",
                    Versions = new List<ApiKeyVersion>
                    {
                        new ApiKeyVersion
                        {
                            Id = "v1",
                            ExpiresAt = DateTime.UtcNow.AddDays(3), // Expires in 3 days
                            Status = ApiKeyStatus.Active
                        }
                    }
                },
                new ApiKeyMetadata
                {
                    Provider = "Azure",
                    NotificationDays = 7,
                    ActiveVersionId = "v1",
                    Versions = new List<ApiKeyVersion>
                    {
                        new ApiKeyVersion
                        {
                            Id = "v1",
                            ExpiresAt = DateTime.UtcNow.AddDays(30), // Expires in 30 days
                            Status = ApiKeyStatus.Active
                        }
                    }
                }
            };

            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(keys);

            // Act
            var result = await _service.CheckExpirationsAsync();

            // Assert
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("OpenAI", result[0]);
        }

        [TestMethod]
        public async Task CheckExpirations_NoExpiringKeys_ReturnsEmpty()
        {
            // Arrange
            var keys = new List<ApiKeyMetadata>
            {
                new ApiKeyMetadata
                {
                    Provider = "OpenAI",
                    NotificationDays = 7,
                    ActiveVersionId = "v1",
                    Versions = new List<ApiKeyVersion>
                    {
                        new ApiKeyVersion
                        {
                            Id = "v1",
                            ExpiresAt = DateTime.UtcNow.AddDays(30), // Not expiring soon
                            Status = ApiKeyStatus.Active
                        }
                    }
                }
            };

            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(keys);

            // Act
            var result = await _service.CheckExpirationsAsync();

            // Assert
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task CheckExpirations_AlreadyExpired_NotIncluded()
        {
            // Arrange
            var keys = new List<ApiKeyMetadata>
            {
                new ApiKeyMetadata
                {
                    Provider = "OpenAI",
                    NotificationDays = 7,
                    ActiveVersionId = "v1",
                    Versions = new List<ApiKeyVersion>
                    {
                        new ApiKeyVersion
                        {
                            Id = "v1",
                            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Already expired
                            Status = ApiKeyStatus.Active
                        }
                    }
                }
            };

            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(keys);

            // Act
            var result = await _service.CheckExpirationsAsync();

            // Assert - Already expired keys should not be included
            Assert.AreEqual(0, result.Count);
        }

        #endregion

        #region ProcessAutomaticRotationsAsync Tests

        [TestMethod]
        public async Task ProcessAutomaticRotations_ExpiredKey_MarksAsExpired()
        {
            // Arrange
            var keys = new List<ApiKeyMetadata>
            {
                new ApiKeyMetadata
                {
                    Provider = "OpenAI",
                    RotationDays = 30,
                    ActiveVersionId = "v1",
                    Versions = new List<ApiKeyVersion>
                    {
                        new ApiKeyVersion
                        {
                            Id = "v1",
                            Version = 1,
                            ExpiresAt = DateTime.UtcNow.AddDays(-1), // Expired
                            Status = ApiKeyStatus.Active
                        }
                    }
                }
            };

            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(keys);

            // Act
            await _service.ProcessAutomaticRotationsAsync();

            // Assert
            Assert.AreEqual(ApiKeyStatus.Expired, keys[0].Versions[0].Status);
            _mockDb.Verify(d => d.UpsertAsync(It.IsAny<string>(), keys[0], It.IsAny<Func<ApiKeyMetadata, bool>>()), Times.Once);
        }

        [TestMethod]
        public async Task ProcessAutomaticRotations_NonExpiredKey_NoChange()
        {
            // Arrange
            var keys = new List<ApiKeyMetadata>
            {
                new ApiKeyMetadata
                {
                    Provider = "OpenAI",
                    RotationDays = 30,
                    ActiveVersionId = "v1",
                    Versions = new List<ApiKeyVersion>
                    {
                        new ApiKeyVersion
                        {
                            Id = "v1",
                            Version = 1,
                            ExpiresAt = DateTime.UtcNow.AddDays(10), // Not expired
                            Status = ApiKeyStatus.Active
                        }
                    }
                }
            };

            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(keys);

            // Act
            await _service.ProcessAutomaticRotationsAsync();

            // Assert
            Assert.AreEqual(ApiKeyStatus.Active, keys[0].Versions[0].Status);
            _mockDb.Verify(d => d.UpsertAsync(It.IsAny<string>(), It.IsAny<ApiKeyMetadata>(), It.IsAny<Func<ApiKeyMetadata, bool>>()), Times.Never);
        }

        [TestMethod]
        public async Task ProcessAutomaticRotations_NoRotationDays_NoChange()
        {
            // Arrange
            var keys = new List<ApiKeyMetadata>
            {
                new ApiKeyMetadata
                {
                    Provider = "OpenAI",
                    RotationDays = 0, // No automatic rotation
                    ActiveVersionId = "v1",
                    Versions = new List<ApiKeyVersion>
                    {
                        new ApiKeyVersion
                        {
                            Id = "v1",
                            Version = 1,
                            ExpiresAt = DateTime.UtcNow.AddDays(-1),
                            Status = ApiKeyStatus.Active
                        }
                    }
                }
            };

            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(keys);

            // Act
            await _service.ProcessAutomaticRotationsAsync();

            // Assert - Should not process keys with RotationDays = 0
            _mockDb.Verify(d => d.UpsertAsync(It.IsAny<string>(), It.IsAny<ApiKeyMetadata>(), It.IsAny<Func<ApiKeyMetadata, bool>>()), Times.Never);
        }

        [TestMethod]
        public async Task ProcessAutomaticRotations_AlreadyExpired_NoChange()
        {
            // Arrange
            var keys = new List<ApiKeyMetadata>
            {
                new ApiKeyMetadata
                {
                    Provider = "OpenAI",
                    RotationDays = 30,
                    ActiveVersionId = "v1",
                    Versions = new List<ApiKeyVersion>
                    {
                        new ApiKeyVersion
                        {
                            Id = "v1",
                            Version = 1,
                            ExpiresAt = DateTime.UtcNow.AddDays(-1),
                            Status = ApiKeyStatus.Expired // Already expired
                        }
                    }
                }
            };

            _mockDb.Setup(d => d.QueryListAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(keys);

            // Act
            await _service.ProcessAutomaticRotationsAsync();

            // Assert - Should not update if already expired
            _mockDb.Verify(d => d.UpsertAsync(It.IsAny<string>(), It.IsAny<ApiKeyMetadata>(), It.IsAny<Func<ApiKeyMetadata, bool>>()), Times.Never);
        }

        #endregion

        #region GetMetadataAsync Tests

        [TestMethod]
        public async Task GetMetadata_ExistingProvider_ReturnsMetadata()
        {
            // Arrange
            var metadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                Name = "OpenAI Key"
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(metadata);

            // Act
            var result = await _service.GetMetadataAsync("OpenAI");

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("OpenAI", result.Provider);
        }

        [TestMethod]
        public async Task GetMetadata_NonExistingProvider_ReturnsNull()
        {
            // Arrange
            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync((ApiKeyMetadata?)null);

            // Act
            var result = await _service.GetMetadataAsync("NonExistent");

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task RegisterKey_InvalidProvider_ReturnsFalse()
        {
            // Arrange
            _mockValidationService.Setup(v => v.Validate(It.Is<string>(s => s == ""), It.IsAny<ValidationRuleSet>()))
                .Returns(new WhisperKey.Services.Validation.ValidationResult { IsValid = false, Errors = new List<string> { "Required" } });

            // Act
            var result = await _service.RegisterKeyAsync("", "Test", "sk-test");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task RegisterKey_InvalidKey_ReturnsFalse()
        {
            // Arrange
            _mockValidationService.Setup(v => v.Validate(It.Is<string>(s => s == "short"), It.IsAny<ValidationRuleSet>()))
                .Returns(new WhisperKey.Services.Validation.ValidationResult { IsValid = false, Errors = new List<string> { "Too short" } });

            // Act
            var result = await _service.RegisterKeyAsync("OpenAI", "Test", "short");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task RotateKey_InvalidKey_ReturnsFalse()
        {
            // Arrange
            _mockValidationService.Setup(v => v.Validate(It.Is<string>(s => s == "short"), It.IsAny<ValidationRuleSet>()))
                .Returns(new WhisperKey.Services.Validation.ValidationResult { IsValid = false, Errors = new List<string> { "Too short" } });

            // Act
            var result = await _service.RotateKeyAsync("OpenAI", "short");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task RotateKey_NonExistingProvider_ReturnsFalse()
        {
            // Arrange
            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync((ApiKeyMetadata?)null);

            // Act
            var result = await _service.RotateKeyAsync("NonExistent", "sk-new-valid-key");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task RegisterKey_CredentialStoreFailure_ReturnsFalse()
        {
            // Arrange
            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync((ApiKeyMetadata?)null);
            _mockCredentialService.Setup(c => c.StoreCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(false);

            // Act
            var result = await _service.RegisterKeyAsync("OpenAI", "Test", "sk-valid-key");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetActiveKey_NoActiveVersion_ReturnsNull()
        {
            // Arrange
            var metadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                ActiveVersionId = "v1",
                Versions = new List<ApiKeyVersion>
                {
                    new ApiKeyVersion { Id = "v1", Version = 1, Status = ApiKeyStatus.Revoked }
                }
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(metadata);

            // Act
            var result = await _service.GetActiveKeyAsync("OpenAI");

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetActiveKey_NoVersions_ReturnsNull()
        {
            // Arrange
            var metadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                ActiveVersionId = "v1",
                Versions = new List<ApiKeyVersion>()
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.IsAny<Func<ApiKeyMetadata, bool>>()))
                .ReturnsAsync(metadata);

            // Act
            var result = await _service.GetActiveKeyAsync("OpenAI");

            // Assert
            Assert.IsNull(result);
        }

        #endregion

        #region Multi-Provider Tests

        [TestMethod]
        public async Task MultipleProviders_IndependentOperations()
        {
            // Arrange
            var openAiMetadata = new ApiKeyMetadata
            {
                Provider = "OpenAI",
                ActiveVersionId = "v1",
                Versions = new List<ApiKeyVersion>
                {
                    new ApiKeyVersion { Id = "v1", Version = 1, Status = ApiKeyStatus.Active, CredentialName = "ApiKey_OpenAI_v1" }
                }
            };

            var azureMetadata = new ApiKeyMetadata
            {
                Provider = "Azure",
                ActiveVersionId = "v1",
                Versions = new List<ApiKeyVersion>
                {
                    new ApiKeyVersion { Id = "v1", Version = 1, Status = ApiKeyStatus.Active, CredentialName = "ApiKey_Azure_v1" }
                }
            };

            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.Is<Func<ApiKeyMetadata, bool>>(f => f(openAiMetadata))))
                .ReturnsAsync(openAiMetadata);
            _mockDb.Setup(d => d.QueryAsync<ApiKeyMetadata>(It.IsAny<string>(), It.Is<Func<ApiKeyMetadata, bool>>(f => f(azureMetadata))))
                .ReturnsAsync(azureMetadata);

            _mockCredentialService.Setup(c => c.RetrieveCredentialAsync("ApiKey_OpenAI_v1")).ReturnsAsync("sk-openai");
            _mockCredentialService.Setup(c => c.RetrieveCredentialAsync("ApiKey_Azure_v1")).ReturnsAsync("sk-azure");

            // Act
            var openAiKey = await _service.GetActiveKeyAsync("OpenAI");
            var azureKey = await _service.GetActiveKeyAsync("Azure");

            // Assert
            Assert.AreEqual("sk-openai", openAiKey);
            Assert.AreEqual("sk-azure", azureKey);
        }

        #endregion
    }
}
