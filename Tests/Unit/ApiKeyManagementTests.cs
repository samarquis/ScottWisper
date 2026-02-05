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
    }
}
