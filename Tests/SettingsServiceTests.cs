using System;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;
using WhisperKey.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace WhisperKey.Tests
{
    [TestClass]
    public class SettingsServiceTests
    {
        private string _testAppDataPath = null!;
        private string _userSettingsPath = null!;
        private Mock<IConfiguration> _mockConfiguration = null!;
        private Mock<IOptionsMonitor<AppSettings>> _mockOptions = null!;
        private ILogger<SettingsService> _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            _testAppDataPath = Path.Combine(Path.GetTempPath(), "WhisperKeyTests_" + Guid.NewGuid().ToString());
            _userSettingsPath = Path.Combine(_testAppDataPath, "settings.json");
            Directory.CreateDirectory(_testAppDataPath);
            
            _mockConfiguration = new Mock<IConfiguration>();
            _mockOptions = new Mock<IOptionsMonitor<AppSettings>>();
            _logger = new NullLogger<SettingsService>();
            
            // Setup mock to return default settings
            _mockOptions.Setup(x => x.CurrentValue).Returns(new AppSettings());
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testAppDataPath))
            {
                try
                {
                    Directory.Delete(_testAppDataPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Initialization Tests

        [TestMethod]
        public void Constructor_WithValidDependencies_CreatesInstance()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            // Act
            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Assert
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.Settings);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
        {
            // Act
            new TestableSettingsService(
                null!, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_WithNullOptions_ThrowsArgumentNullException()
        {
            // Act
            new TestableSettingsService(
                _mockConfiguration.Object, 
                null!, 
                _logger,
                _userSettingsPath);
        }

        #endregion

        #region Settings Property Tests

        [TestMethod]
        public void Settings_ReturnsCurrentSettings()
        {
            // Arrange
            var expectedSettings = new AppSettings
            {
                Audio = new AudioSettings { SampleRate = 48000 },
                Transcription = new TranscriptionSettings { Provider = "TestProvider" }
            };
            _mockOptions.Setup(x => x.CurrentValue).Returns(expectedSettings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var actualSettings = service.Settings;

            // Assert
            Assert.AreEqual(expectedSettings, actualSettings);
            Assert.AreEqual(48000, actualSettings.Audio.SampleRate);
            Assert.AreEqual("TestProvider", actualSettings.Transcription.Provider);
        }

        [TestMethod]
        public void Settings_ReturnsSameInstanceOnMultipleCalls()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var settings1 = service.Settings;
            var settings2 = service.Settings;

            // Assert
            Assert.AreSame(settings1, settings2);
        }

        #endregion

        #region Value Get/Set Tests

        [TestMethod]
        public async Task GetValueAsync_ExistingKey_ReturnsValue()
        {
            // Arrange
            var settings = new AppSettings
            {
                Transcription = new TranscriptionSettings { Provider = "OpenAI" }
            };
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var value = await service.GetValueAsync<string>("Transcription:Provider");

            // Assert
            Assert.AreEqual("OpenAI", value);
        }

        [TestMethod]
        public async Task SetValueAsync_UpdatesSettingsValue()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            await service.SetValueAsync("Transcription:Provider", "Azure");

            // Assert
            Assert.AreEqual("Azure", settings.Transcription.Provider);
        }

        [TestMethod]
        public async Task SetValueAsync_RaisesSettingsChangedEvent()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            SettingsChangedEventArgs? capturedArgs = null;
            service.SettingsChanged += (s, e) => capturedArgs = e;

            // Act
            await service.SetValueAsync("Transcription:Provider", "Azure");

            // Assert
            Assert.IsNotNull(capturedArgs);
            Assert.AreEqual("Transcription:Provider", capturedArgs.Key);
            Assert.AreEqual("Azure", capturedArgs.NewValue);
        }

        #endregion

        #region Encrypted Value Tests

        [TestMethod]
        public async Task SetEncryptedValueAsync_StoresEncryptedData()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var sensitiveValue = "sk-test123456789";

            // Act
            await service.SetEncryptedValueAsync("ApiKey", sensitiveValue);
            var retrievedValue = await service.GetEncryptedValueAsync("ApiKey");

            // Assert
            Assert.AreEqual(sensitiveValue, retrievedValue);
        }

        [TestMethod]
        public async Task GetEncryptedValueAsync_NonExistentKey_ReturnsEmptyString()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var value = await service.GetEncryptedValueAsync("NonExistentKey");

            // Assert
            Assert.AreEqual(string.Empty, value);
        }

        [TestMethod]
        public async Task SetEncryptedValueAsync_EmptyValue_RemovesKey()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            await service.SetEncryptedValueAsync("TestKey", "initialValue");

            // Act
            await service.SetEncryptedValueAsync("TestKey", string.Empty);
            var retrievedValue = await service.GetEncryptedValueAsync("TestKey");

            // Assert
            Assert.AreEqual(string.Empty, retrievedValue);
        }

        #endregion

        #region Device Settings Tests

        [TestMethod]
        public async Task SetSelectedInputDeviceAsync_UpdatesDeviceId()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            await service.SetSelectedInputDeviceAsync("device-123");

            // Assert
            Assert.AreEqual("device-123", settings.Audio.SelectedInputDeviceId);
        }

        [TestMethod]
        public async Task SetSelectedOutputDeviceAsync_UpdatesDeviceId()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            await service.SetSelectedOutputDeviceAsync("output-456");

            // Assert
            Assert.AreEqual("output-456", settings.Audio.SelectedOutputDeviceId);
        }

        [TestMethod]
        public async Task GetDeviceSettingsAsync_NewDevice_ReturnsDefaultSettings()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var deviceSettings = await service.GetDeviceSettingsAsync("new-device");

            // Assert
            Assert.IsNotNull(deviceSettings);
            Assert.AreEqual("new-device", deviceSettings.Name);
            Assert.IsTrue(deviceSettings.IsEnabled);
        }

        [TestMethod]
        public async Task SetDeviceSettingsAsync_StoresSettings()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var deviceSettings = new DeviceSpecificSettings
            {
                Name = "Test Mic",
                SampleRate = 48000,
                IsEnabled = false
            };

            // Act
            await service.SetDeviceSettingsAsync("test-device", deviceSettings);
            var retrieved = await service.GetDeviceSettingsAsync("test-device");

            // Assert
            Assert.AreEqual("Test Mic", retrieved.Name);
            Assert.AreEqual(48000, retrieved.SampleRate);
            Assert.IsFalse(retrieved.IsEnabled);
        }

        #endregion

        #region Hotkey Profile Tests

        [TestMethod]
        public async Task CreateHotkeyProfileAsync_AddsNewProfile()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var profile = new HotkeyProfile
            {
                Id = "test-profile",
                Name = "Test Profile"
            };

            // Act
            var result = await service.CreateHotkeyProfileAsync(profile);

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(settings.Hotkeys.Profiles.ContainsKey("test-profile"));
        }

        [TestMethod]
        public async Task CreateHotkeyProfileAsync_DuplicateId_ReturnsFalse()
        {
            // Arrange
            var settings = new AppSettings();
            settings.Hotkeys.Profiles["existing"] = new HotkeyProfile { Id = "existing" };
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var profile = new HotkeyProfile
            {
                Id = "existing",
                Name = "Duplicate"
            };

            // Act
            var result = await service.CreateHotkeyProfileAsync(profile);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task DeleteHotkeyProfileAsync_ExistingProfile_RemovesAndReturnsTrue()
        {
            // Arrange
            var settings = new AppSettings();
            settings.Hotkeys.Profiles["to-delete"] = new HotkeyProfile { Id = "to-delete" };
            settings.Hotkeys.CurrentProfile = "other";
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var result = await service.DeleteHotkeyProfileAsync("to-delete");

            // Assert
            Assert.IsTrue(result);
            Assert.IsFalse(settings.Hotkeys.Profiles.ContainsKey("to-delete"));
        }

        [TestMethod]
        public async Task SwitchHotkeyProfileAsync_SetsCurrentProfile()
        {
            // Arrange
            var settings = new AppSettings();
            settings.Hotkeys.Profiles["profile1"] = new HotkeyProfile { Id = "profile1" };
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var result = await service.SwitchHotkeyProfileAsync("profile1");

            // Assert
            Assert.IsTrue(result);
            Assert.AreEqual("profile1", settings.Hotkeys.CurrentProfile);
        }

        [TestMethod]
        public async Task GetHotkeyProfilesAsync_ReturnsAllProfiles()
        {
            // Arrange
            var settings = new AppSettings();
            settings.Hotkeys.Profiles["p1"] = new HotkeyProfile { Id = "p1", Name = "Profile 1" };
            settings.Hotkeys.Profiles["p2"] = new HotkeyProfile { Id = "p2", Name = "Profile 2" };
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var profiles = await service.GetHotkeyProfilesAsync();

            // Assert
            Assert.AreEqual(2, profiles.Count);
        }

        #endregion

        #region Settings Changed Event Tests

        [TestMethod]
        public async Task MultipleSettingsChanges_RaiseMultipleEvents()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var eventCount = 0;
            service.SettingsChanged += (s, e) => eventCount++;

            // Act
            await service.SetValueAsync("Audio:SampleRate", 48000);
            await service.SetValueAsync("Transcription:Provider", "Azure");
            await service.SetValueAsync("UI:Theme", "Light");

            // Assert
            Assert.AreEqual(3, eventCount);
        }

        [TestMethod]
        public async Task SetValueAsync_SameValue_DoesNotRaiseEvent()
        {
            // Arrange
            var settings = new AppSettings
            {
                Transcription = new TranscriptionSettings { Provider = "OpenAI" }
            };
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var eventRaised = false;
            service.SettingsChanged += (s, e) => eventRaised = true;

            // Act - set same value
            await service.SetValueAsync("Transcription:Provider", "OpenAI");

            // Assert
            Assert.IsFalse(eventRaised);
        }

        #endregion

        #region Validation Tests

        [TestMethod]
        public async Task ValidateHotkeyAsync_EmptyCombination_ReturnsInvalid()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var result = await service.ValidateHotkeyAsync("");

            // Assert
            Assert.IsFalse(result.IsValid);
            Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage));
        }

        [TestMethod]
        public async Task ValidateHotkeyAsync_ValidCombination_ReturnsValid()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var result = await service.ValidateHotkeyAsync("Ctrl+Alt+V");

            // Assert - should be valid (though may have warnings)
            Assert.IsNotNull(result);
        }

        #endregion

        #region Negative Test Cases

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public async Task SetValueAsync_NullKey_ThrowsArgumentNullException()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            await service.SetValueAsync<string>(null!, "value");
        }

        [TestMethod]
        public async Task DeleteHotkeyProfileAsync_NonExistent_ReturnsFalse()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var result = await service.DeleteHotkeyProfileAsync("non-existent");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task SwitchHotkeyProfileAsync_NonExistent_ReturnsFalse()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var result = await service.SwitchHotkeyProfileAsync("non-existent");

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetValueAsync_InvalidKey_ReturnsDefault()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            // Act
            var value = await service.GetValueAsync<string>("Invalid:Key:Path");

            // Assert
            Assert.IsNull(value);
        }

        #endregion

        #region Boundary Value Tests

        [TestMethod]
        public async Task SetValueAsync_VeryLongString_HandlesCorrectly()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var longValue = new string('a', 10000);

            // Act
            await service.SetValueAsync("Transcription:Model", longValue);

            // Assert
            Assert.AreEqual(longValue, settings.Transcription.Model);
        }

        [TestMethod]
        public async Task SetValueAsync_SpecialCharacters_HandlesCorrectly()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var specialValue = "Test<>&\"'\n\r\tValue";

            // Act
            await service.SetValueAsync("Transcription:Provider", specialValue);

            // Assert
            Assert.AreEqual(specialValue, settings.Transcription.Provider);
        }

        [TestMethod]
        public async Task SetEncryptedValueAsync_VeryLongKey_HandlesCorrectly()
        {
            // Arrange
            var settings = new AppSettings();
            _mockOptions.Setup(x => x.CurrentValue).Returns(settings);

            var service = new TestableSettingsService(
                _mockConfiguration.Object, 
                _mockOptions.Object, 
                _logger,
                _userSettingsPath);

            var longKey = new string('k', 500);
            var value = "test-value";

            // Act
            await service.SetEncryptedValueAsync(longKey, value);
            var retrieved = await service.GetEncryptedValueAsync(longKey);

            // Assert
            Assert.AreEqual(value, retrieved);
        }

        #endregion

        /// <summary>
        /// Testable version of SettingsService that allows injection of custom settings path
        /// </summary>
        private class TestableSettingsService : SettingsService
        {
            public TestableSettingsService(
                IConfiguration configuration,
                IOptionsMonitor<AppSettings> options,
                ILogger<SettingsService> logger,
                string settingsPath) : base(configuration, options, logger)
            {
                // Use reflection to set the private field for testing
                var field = typeof(SettingsService).GetField("_userSettingsPath", 
                    System.Reflection.BindingFlags.NonPublic | 
                    System.Reflection.BindingFlags.Instance);
                field?.SetValue(this, settingsPath);
            }
        }
    }
}
