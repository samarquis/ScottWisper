using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Repositories;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class SettingsEncryptionUpgradeTests
    {
        private string _testAppDataPath = null!;
        private Mock<ISettingsRepository> _repositoryMock = null!;
        private Mock<ICredentialService> _credentialServiceMock = null!;
        private Mock<IOptionsMonitor<AppSettings>> _optionsMock = null!;
        private IConfiguration _configuration = null!;
        private SettingsService _service = null!;
        private AppSettings _defaultSettings = null!;

        [TestInitialize]
        public void Setup()
        {
            _testAppDataPath = Path.Combine(Path.GetTempPath(), $"SettingsEncryptionUpgradeTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testAppDataPath);

            _repositoryMock = new Mock<ISettingsRepository>();
            _credentialServiceMock = new Mock<ICredentialService>();
            _defaultSettings = new AppSettings();
            
            _optionsMock = new Mock<IOptionsMonitor<AppSettings>>();
            _optionsMock.Setup(o => o.CurrentValue).Returns(_defaultSettings);

            _configuration = new ConfigurationBuilder().Build();

            _service = new SettingsService(
                _configuration,
                _optionsMock.Object,
                NullLogger<SettingsService>.Instance,
                _repositoryMock.Object,
                credentialService: _credentialServiceMock.Object,
                autoLoad: false,
                customAppDataPath: _testAppDataPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service?.Dispose();
            if (Directory.Exists(_testAppDataPath))
            {
                try { Directory.Delete(_testAppDataPath, true); } catch { }
            }
        }

        [TestMethod]
        public async Task Encrypt_SameValue_ProducesDifferentCiphertexts()
        {
            // Arrange
            var key = "TestKey";
            var value = "SensitiveData123";
            var masterSecret = Convert.ToBase64String(new byte[32]); // Dummy master secret
            _credentialServiceMock.Setup(c => c.RetrieveCredentialAsync("MasterSecret")).ReturnsAsync(masterSecret);

            // Act
            await _service.SetEncryptedValueAsync(key, value);
            var filePath = GetEncryptedFilePath(key);
            var ciphertext1 = await File.ReadAllTextAsync(filePath);

            await _service.SetEncryptedValueAsync(key, value);
            var ciphertext2 = await File.ReadAllTextAsync(filePath);

            // Assert
            Assert.AreNotEqual(ciphertext1, ciphertext2, "Encryption should produce different ciphertexts due to random salt");
            
            var decrypted = await _service.GetEncryptedValueAsync(key);
            Assert.AreEqual(value, decrypted, "Decryption should still work correctly");
        }

        [TestMethod]
        public async Task Decrypt_LegacyFormat_WorksCorrectly()
        {
            // Arrange
            var key = "LegacyKey";
            var value = "LegacySecret";
            
            // Manually create a legacy-formatted encrypted file
            // Legacy format: Base64(DPAPI.Protect(plaintext, oldEntropy))
            var oldEntropySource = $"{Environment.MachineName}_{Environment.UserName}_WhisperKey_v1";
            byte[] oldEntropy;
            using (var sha256 = SHA256.Create())
            {
                oldEntropy = sha256.ComputeHash(Encoding.UTF8.GetBytes(oldEntropySource));
            }
            
            var plainBytes = Encoding.UTF8.GetBytes(value);
            var legacyEncryptedBytes = ProtectedData.Protect(plainBytes, oldEntropy, DataProtectionScope.CurrentUser);
            var legacyBase64 = Convert.ToBase64String(legacyEncryptedBytes);
            
            var filePath = GetEncryptedFilePath(key);
            await File.WriteAllTextAsync(filePath, legacyBase64);

            // Act
            var decrypted = await _service.GetEncryptedValueAsync(key);

            // Assert
            Assert.AreEqual(value, decrypted, "Should be able to decrypt legacy format using fallback");
        }

        [TestMethod]
        public async Task MasterSecret_IsGeneratedAndStored_WhenMissing()
        {
            // Arrange
            var key = "TestKey";
            var value = "Data";
            _credentialServiceMock.Setup(c => c.RetrieveCredentialAsync("MasterSecret")).ReturnsAsync((string?)null);
            _credentialServiceMock.Setup(c => c.StoreCredentialAsync("MasterSecret", It.IsAny<string>())).ReturnsAsync(true);

            // Act
            await _service.SetEncryptedValueAsync(key, value);

            // Assert
            _credentialServiceMock.Verify(c => c.RetrieveCredentialAsync("MasterSecret"), Times.Once);
            _credentialServiceMock.Verify(c => c.StoreCredentialAsync("MasterSecret", It.IsAny<string>()), Times.Once);
        }

        private string GetEncryptedFilePath(string key)
        {
            using var sha256 = SHA256.Create();
            var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(key));
            var safeFileName = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
            return Path.Combine(_testAppDataPath, $"{safeFileName}.encrypted");
        }
    }
}
