using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class ApiKeyManagementTests
    {
        private Mock<ICredentialService> _mockCredentialService = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private Mock<IFileSystemService> _mockFileSystem = null!;
        private ApiKeyManagementService _service = null!;
        private string _testMetadataPath = @"C:\Tests\apikeys.json";

        [TestInitialize]
        public void Setup()
        {
            _mockCredentialService = new Mock<ICredentialService>();
            _mockAuditService = new Mock<IAuditLoggingService>();
            _mockFileSystem = new Mock<IFileSystemService>();

            _mockFileSystem.Setup(f => f.GetAppDataPath()).Returns(@"C:\Tests");
            _mockFileSystem.Setup(f => f.CombinePath(It.IsAny<string>(), "apikeys.json")).Returns(_testMetadataPath);

            _service = new ApiKeyManagementService(
                _mockCredentialService.Object,
                _mockAuditService.Object,
                _mockFileSystem.Object,
                NullLogger<ApiKeyManagementService>.Instance);
        }

        [TestMethod]
        public async Task RegisterKey_StoresInCredentialAndFileSystem()
        {
            // Arrange
            _mockCredentialService.Setup(c => c.StoreCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);
            _mockFileSystem.Setup(f => f.FileExists(_testMetadataPath)).Returns(false);

            // Act
            var result = await _service.RegisterKeyAsync("OpenAI", "Main Key", "sk-test", 30);

            // Assert
            Assert.IsTrue(result);
            _mockCredentialService.Verify(c => c.StoreCredentialAsync(It.Is<string>(s => s.Contains("OpenAI_v1")), "sk-test"), Times.Once);
            _mockFileSystem.Verify(f => f.WriteAllTextAsync(_testMetadataPath, It.IsAny<string>()), Times.Once);
            _mockAuditService.Verify(a => a.LogEventAsync(AuditEventType.ApiKeyAccessed, It.IsAny<string>(), It.IsAny<string>(), DataSensitivity.High), Times.Once);
        }

        [TestMethod]
        public async Task RotateKey_CreatesNewVersion()
        {
            // Arrange
            _mockFileSystem.Setup(f => f.FileExists(_testMetadataPath)).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(_testMetadataPath))
                .ReturnsAsync("{\"OpenAI\": {\"Provider\":\"OpenAI\",\"ActiveVersionId\":\"v1\",\"Versions\":[{\"Id\":\"v1\",\"Version\":1,\"CredentialName\":\"ApiKey_OpenAI_v1\",\"Status\":0}]}}");
            
            _mockCredentialService.Setup(c => c.StoreCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _service.RotateKeyAsync("OpenAI", "sk-new");

            // Assert
            Assert.IsTrue(result);
            _mockCredentialService.Verify(c => c.StoreCredentialAsync(It.Is<string>(s => s.Contains("OpenAI_v2")), "sk-new"), Times.Once);
            _mockFileSystem.Verify(f => f.WriteAllTextAsync(_testMetadataPath, It.Is<string>(s => s.Contains("v2"))), Times.Once);
        }

        [TestMethod]
        public async Task GetActiveKey_RetrievesFromCredentialManager()
        {
            // Arrange
            _mockFileSystem.Setup(f => f.FileExists(_testMetadataPath)).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(_testMetadataPath))
                .ReturnsAsync("{\"OpenAI\": {\"Provider\":\"OpenAI\",\"ActiveVersionId\":\"v1\",\"Versions\":[{\"Id\":\"v1\",\"Version\":1,\"CredentialName\":\"ApiKey_OpenAI_v1\",\"Status\":0}]}}");
            
            _mockCredentialService.Setup(c => c.RetrieveCredentialAsync("ApiKey_OpenAI_v1"))
                .ReturnsAsync("sk-test");

            // Act
            var key = await _service.GetActiveKeyAsync("OpenAI");

            // Assert
            Assert.AreEqual("sk-test", key);
        }

        [TestMethod]
        public async Task RecordUsage_UpdatesMetadata()
        {
            // Arrange
            _mockFileSystem.Setup(f => f.FileExists(_testMetadataPath)).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(_testMetadataPath))
                .ReturnsAsync("{\"OpenAI\": {\"Provider\":\"OpenAI\",\"ActiveVersionId\":\"v1\",\"Versions\":[{\"Id\":\"v1\",\"Version\":1,\"CredentialName\":\"ApiKey_OpenAI_v1\",\"Status\":0}],\"Usage\":{\"TotalRequests\":0}}}");

            // Act
            await _service.RecordUsageAsync("OpenAI", 100, 0.05m, true);

            // Assert
            _mockFileSystem.Verify(f => f.WriteAllTextAsync(_testMetadataPath, It.Is<string>(s => s.Contains("\"TotalRequests\": 1"))), Times.Once);
        }
    }
}