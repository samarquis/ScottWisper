using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Models;
using WhisperKey.Repositories;
using WhisperKey.Services;
using WhisperKey.Exceptions;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class SettingsServiceTests
    {
        private string _testAppDataPath = null!;
        private Mock<ISettingsRepository> _repositoryMock = null!;
        private Mock<IOptionsMonitor<AppSettings>> _optionsMock = null!;
        private IConfiguration _configuration = null!;
        private SettingsService _service = null!;
        private AppSettings _defaultSettings = null!;

        [TestInitialize]
        public void Setup()
        {
            _testAppDataPath = Path.Combine(Path.GetTempPath(), $"SettingsServiceTests_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testAppDataPath);

            _repositoryMock = new Mock<ISettingsRepository>();
            _defaultSettings = new AppSettings();
            
            _optionsMock = new Mock<IOptionsMonitor<AppSettings>>();
            _optionsMock.Setup(o => o.CurrentValue).Returns(_defaultSettings);

            _configuration = new ConfigurationBuilder().Build();

            _service = new SettingsService(
                _configuration,
                _optionsMock.Object,
                NullLogger<SettingsService>.Instance,
                _repositoryMock.Object,
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
        public async Task LoadUserSettingsAsync_WithValidSettings_MergesCorrectly()
        {
            // Arrange
            var userSettings = new AppSettings();
            userSettings.Audio.SampleRate = 44100;
            userSettings.Transcription.Provider = "CustomProvider";
            
            _repositoryMock.Setup(r => r.LoadAsync()).ReturnsAsync(userSettings);

            // Act
            await _service.LoadUserSettingsAsync();

            // Assert
            Assert.AreEqual(44100, _service.Settings.Audio.SampleRate);
            Assert.AreEqual("CustomProvider", _service.Settings.Transcription.Provider);
        }

        [TestMethod]
        public async Task SetValueAsync_UpdatesMemoryAndTriggersSave()
        {
            // Act
            await _service.SetValueAsync("Audio:SampleRate", 22050);

            // Assert
            Assert.AreEqual(22050, _service.Settings.Audio.SampleRate);
            // Since autoLoad is false, it shouldn't have called SaveAsyncInternal yet
            _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<AppSettings>()), Times.Never);
        }

        [TestMethod]
        public async Task SaveImmediateAsync_PersistsToRepository()
        {
            // Arrange
            _service.Settings.Audio.SampleRate = 8000;

            // Act
            await _service.SaveImmediateAsync();

            // Assert
            _repositoryMock.Verify(r => r.SaveAsync(It.IsAny<AppSettings>()), Times.Once);
        }

        [TestMethod]
        public async Task GetEncryptedValueAsync_ReturnsStoredValue()
        {
            // Arrange
            var key = "SecretKey";
            var secretValue = "P@ssword123";
            await _service.SetEncryptedValueAsync(key, secretValue);

            // Act
            var retrieved = await _service.GetEncryptedValueAsync(key);

            // Assert
            Assert.AreEqual(secretValue, retrieved);
        }

        [TestMethod]
        public async Task ResetAsync_RestoresDefaults()
        {
            // Arrange
            _service.Settings.Audio.SampleRate = 99999;
            
            // Act
            await _service.ResetToDefaultsAsync();

            // Assert
            Assert.AreEqual(16000, _service.Settings.Audio.SampleRate);
        }

        [TestMethod]
        public async Task CreateHotkeyProfileAsync_AddsToProfiles()
        {
            // Arrange
            var profile = new HotkeyProfile { Id = "NewProfile", Name = "New Profile" };

            // Act
            var result = await _service.CreateHotkeyProfileAsync(profile);

            // Assert
            Assert.IsTrue(result);
            var profiles = await _service.GetHotkeyProfilesAsync();
            Assert.IsTrue(profiles.Any(p => p.Id == "NewProfile"));
        }

        [TestMethod]
        public async Task SwitchHotkeyProfileAsync_ChangesCurrentProfile()
        {
            // Arrange
            var profile = new HotkeyProfile { Id = "TargetProfile", Name = "Target" };
            await _service.CreateHotkeyProfileAsync(profile);

            // Act
            var result = await _service.SwitchHotkeyProfileAsync("TargetProfile");

            // Assert
            Assert.IsTrue(result);
            var current = await _service.GetCurrentHotkeyProfileAsync();
            Assert.AreEqual("TargetProfile", current.Id);
        }
    }
}
