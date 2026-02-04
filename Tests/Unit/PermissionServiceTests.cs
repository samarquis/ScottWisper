using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Threading.Tasks;
using WhisperKey.Services;
using WhisperKey;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class PermissionServiceTests
    {
        private Mock<IRegistryService> _mockRegistryService;
        private PermissionService _permissionService;

        [TestInitialize]
        public void Setup()
        {
            _mockRegistryService = new Mock<IRegistryService>();
            _permissionService = new PermissionService(_mockRegistryService.Object);
        }
        
        [TestCleanup]
        public void Cleanup()
        {
            _permissionService.Dispose();
        }

        [TestMethod]
        public async Task CheckMicrophonePermissionAsync_ReturnsGranted_WhenRegistryAllows()
        {
            // Arrange
            _mockRegistryService.Setup(r => r.ReadValue(
                RegistryHiveOption.CurrentUser,
                It.Is<string>(s => s.Contains("ConsentStore\\microphone")),
                "Value"))
                .Returns("Allow");

            // Act
            var result = await _permissionService.CheckMicrophonePermissionAsync();

            // Assert
            Assert.AreEqual(MicrophonePermissionStatus.Granted, result);
        }

        [TestMethod]
        public async Task CheckMicrophonePermissionAsync_ReturnsDenied_WhenRegistryDenies()
        {
            // Arrange
            _mockRegistryService.Setup(r => r.ReadValue(
                RegistryHiveOption.CurrentUser,
                It.Is<string>(s => s.Contains("ConsentStore\\microphone")),
                "Value"))
                .Returns("Deny");

            // Act
            var result = await _permissionService.CheckMicrophonePermissionAsync();

            // Assert
            Assert.AreEqual(MicrophonePermissionStatus.Denied, result);
        }
        
        [TestMethod]
        public async Task GetPermissionStatusAsync_ReturnsCorrectMessage_WhenGranted()
        {
             // Arrange
            _mockRegistryService.Setup(r => r.ReadValue(
                RegistryHiveOption.CurrentUser,
                It.IsAny<string>(),
                "Value"))
                .Returns("Allow");
                
            // Act
            var message = await _permissionService.GetPermissionStatusAsync();
            
            // Assert
            Assert.IsTrue(message.Contains("granted"));
        }

        [TestMethod]
        public async Task GetPermissionStatusAsync_ReturnsCorrectMessage_WhenDenied()
        {
            // Arrange
            _mockRegistryService.Setup(r => r.ReadValue(
                RegistryHiveOption.CurrentUser,
                It.IsAny<string>(),
                "Value"))
                .Returns("Deny");

            // Act
            var message = await _permissionService.GetPermissionStatusAsync();

            // Assert
            Assert.IsTrue(message.Contains("denied"));
        }
    }
}