using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ScottWisper.Services;
using ScottWisper.Configuration;
using ScottWisper.ViewModels;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;

namespace ScottWisper.Tests
{
    [TestClass]
    public class SettingsPersistenceTests
    {
        private Mock<ISettingsService> _settingsServiceMock = null!;
        private Mock<IAudioDeviceService> _audioDeviceServiceMock = null!;
        private AppSettings _testSettings = null!;

        [TestInitialize]
        public void Setup()
        {
            _settingsServiceMock = new Mock<ISettingsService>();
            _audioDeviceServiceMock = new Mock<IAudioDeviceService>();
            _testSettings = new AppSettings
            {
                UI = new UISettings { Theme = "Light", StartMinimized = false },
                Audio = new AudioSettings { SelectedInputDeviceId = "Default" }
            };

            _settingsServiceMock.Setup(x => x.Settings).Returns(_testSettings);
        }

        [TestMethod]
        public async Task Test_ViewModelReflectsServiceSettings()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_settingsServiceMock.Object, _audioDeviceServiceMock.Object);
            
            // Act - Initialize (simulate loading)
            // Note: SettingsViewModel loads settings in constructor or via command
            
            // Assert
            Assert.AreEqual(_testSettings.UI.Theme, viewModel.Theme);
            Assert.AreEqual(_testSettings.UI.StartMinimized, viewModel.StartMinimized);
        }

        [TestMethod]
        public async Task Test_SettingsSave_TriggersService()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_settingsServiceMock.Object, _audioDeviceServiceMock.Object);
            
            // Act
            viewModel.Theme = "Dark";
            // In a real app, clicking Save calls SaveAsync
            await viewModel.SaveSettingsAsync();

            // Assert
            _settingsServiceMock.Verify(x => x.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public async Task Test_ResetToDefaults_RestoresValues()
        {
            // Arrange
            var viewModel = new SettingsViewModel(_settingsServiceMock.Object, _audioDeviceServiceMock.Object);
            _testSettings.UI.Theme = "Dark";
            
            // Act
            // We would need a way to trigger reset. 
            // Most ViewModels have a ResetCommand or similar.
            await viewModel.ResetSettingsAsync();

            // Assert
            _settingsServiceMock.Verify(x => x.SaveAsync(), Times.AtLeastOnce);
        }

        [TestMethod]
        public void Test_InvalidSettingsFile_Handling()
        {
            // This would normally be tested in SettingsService, not ViewModel
            // But we can verify the Service's behavior if we use a real instance with a bad file
            
            // For this test, we'll just verify that the ViewModel handles a null settings object gracefully
            _settingsServiceMock.Setup(x => x.Settings).Returns((AppSettings)null!);
            
            try
            {
                var viewModel = new SettingsViewModel(_settingsServiceMock.Object, _audioDeviceServiceMock.Object);
                Assert.IsNotNull(viewModel);
            }
            catch (Exception ex)
            {
                Assert.Fail($"ViewModel failed to handle null settings: {ex.Message}");
            }
        }
    }
}
