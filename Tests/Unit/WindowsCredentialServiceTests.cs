using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Services;
using WhisperKey.Models;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class WindowsCredentialServiceTests
    {
        private Mock<ILogger<WindowsCredentialService>> _loggerMock = null!;
        private Mock<IAuditLoggingService> _auditServiceMock = null!;
        private Mock<ISecurityContextService> _securityContextServiceMock = null!;
        private WindowsCredentialService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<WindowsCredentialService>>();
            _auditServiceMock = new Mock<IAuditLoggingService>();
            _securityContextServiceMock = new Mock<ISecurityContextService>();
            
            _securityContextServiceMock.Setup(s => s.GetSecurityContextAsync())
                .ReturnsAsync(new SecurityContext { 
                    DeviceFingerprint = "test-device",
                    HashedIpAddress = "127.0.0.1",
                    ProcessId = 1234,
                    SessionId = "test-session"
                });

            _service = new WindowsCredentialService(
                _loggerMock.Object, 
                _auditServiceMock.Object, 
                _securityContextServiceMock.Object);
        }

        [TestMethod]
        public async Task StoreAndRetrieveCredential_Success()
        {
            // Note: This test interacts with the actual Windows Credential Manager
            // if run on Windows. We use a unique test key to avoid conflicts.
            string testKey = "Test_ApiKey_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string testValue = "sk-test-value-" + Guid.NewGuid().ToString("N");

            try
            {
                // Store
                bool storeResult = await _service.StoreCredentialAsync(testKey, testValue);
                Assert.IsTrue(storeResult);

                // Retrieve
                string? retrievedValue = await _service.RetrieveCredentialAsync(testKey);
                Assert.AreEqual(testValue, retrievedValue);

                // Exists
                bool exists = await _service.CredentialExistsAsync(testKey);
                Assert.IsTrue(exists);

                // Delete
                bool deleteResult = await _service.DeleteCredentialAsync(testKey);
                Assert.IsTrue(deleteResult);

                // Verify deleted
                string? deletedValue = await _service.RetrieveCredentialAsync(testKey);
                Assert.IsNull(deletedValue);
                
                bool stillExists = await _service.CredentialExistsAsync(testKey);
                Assert.IsFalse(stillExists);
            }
            finally
            {
                // Cleanup just in case
                await _service.DeleteCredentialAsync(testKey);
            }
        }

        [TestMethod]
        public async Task RetrieveNonExistentCredential_ReturnsNull()
        {
            string testKey = "NonExistent_" + Guid.NewGuid().ToString("N");
            string? result = await _service.RetrieveCredentialAsync(testKey);
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task DeleteNonExistentCredential_ReturnsTrue()
        {
            // Deleting something that doesn't exist should still return true (idempotent)
            string testKey = "NonExistent_" + Guid.NewGuid().ToString("N");
            bool result = await _service.DeleteCredentialAsync(testKey);
            Assert.IsTrue(result);
        }
    }
}
