using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class HotkeyProfileManagerTests
    {
        private Mock<ISettingsService> _settingsServiceMock = null!;
        private Mock<ILogger<HotkeyProfileManager>> _loggerMock = null!;
        private HotkeyProfileManager _profileManager = null!;
        private AppSettings _testSettings = null!;

        [TestInitialize]
        public void Setup()
        {
            _settingsServiceMock = new Mock<ISettingsService>();
            _loggerMock = new Mock<ILogger<HotkeyProfileManager>>();

            _testSettings = new AppSettings
            {
                Hotkeys = new HotkeySettings
                {
                    CurrentProfile = "Default",
                    Profiles = new Dictionary<string, HotkeyProfile>
                    {
                        ["Default"] = new HotkeyProfile
                        {
                            Id = "Default",
                            Name = "Default Profile",
                            Hotkeys = new Dictionary<string, HotkeyDefinition>
                            {
                                ["toggle_recording"] = new HotkeyDefinition
                                {
                                    Id = "toggle_recording",
                                    Name = "Toggle Recording",
                                    Combination = "Ctrl+Alt+V",
                                    Action = "toggle_recording",
                                    IsEnabled = true
                                }
                            }
                        }
                    }
                }
            };

            _settingsServiceMock.Setup(s => s.Settings).Returns(_testSettings);
            _settingsServiceMock.Setup(s => s.SaveAsync()).Returns(Task.CompletedTask);

            _profileManager = new HotkeyProfileManager(
                _settingsServiceMock.Object,
                _loggerMock.Object
            );
        }

        [TestMethod]
        public async Task CreateProfileAsync_NewProfile_CreatesAndSaves()
        {
            var newProfile = new HotkeyProfile
            {
                Id = "Gaming",
                Name = "Gaming Profile",
                Hotkeys = new Dictionary<string, HotkeyDefinition>()
            };

            var result = await _profileManager.CreateProfileAsync(newProfile);

            Assert.IsTrue(result);
            Assert.IsTrue(_testSettings.Hotkeys.Profiles.ContainsKey("Gaming"));
            Assert.IsNotNull(newProfile.CreatedAt);
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public async Task CreateProfileAsync_NullProfile_ReturnsFalse()
        {
            var result = await _profileManager.CreateProfileAsync(null!);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task CreateProfileAsync_DuplicateId_ReturnsFalse()
        {
            var duplicateProfile = new HotkeyProfile
            {
                Id = "Default",
                Name = "Duplicate",
                Hotkeys = new Dictionary<string, HotkeyDefinition>()
            };

            var result = await _profileManager.CreateProfileAsync(duplicateProfile);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task CreateProfileAsync_EmptyId_ReturnsFalse()
        {
            var profile = new HotkeyProfile
            {
                Id = "",
                Name = "Empty ID",
                Hotkeys = new Dictionary<string, HotkeyDefinition>()
            };

            var result = await _profileManager.CreateProfileAsync(profile);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UpdateProfileAsync_ExistingProfile_UpdatesAndSaves()
        {
            var existingProfile = _testSettings.Hotkeys.Profiles["Default"];
            existingProfile.Name = "Updated Default";

            var result = await _profileManager.UpdateProfileAsync(existingProfile);

            Assert.IsTrue(result);
            Assert.IsNotNull(existingProfile.ModifiedAt);
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public async Task UpdateProfileAsync_NonExistentProfile_ReturnsFalse()
        {
            var profile = new HotkeyProfile
            {
                Id = "NonExistent",
                Name = "Non Existent",
                Hotkeys = new Dictionary<string, HotkeyDefinition>()
            };

            var result = await _profileManager.UpdateProfileAsync(profile);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task DeleteProfileAsync_ExistingProfile_DeletesAndSaves()
        {
            _testSettings.Hotkeys.Profiles["TestProfile"] = new HotkeyProfile
            {
                Id = "TestProfile",
                Name = "Test Profile",
                Hotkeys = new Dictionary<string, HotkeyDefinition>()
            };

            var result = await _profileManager.DeleteProfileAsync("TestProfile");

            Assert.IsTrue(result);
            Assert.IsFalse(_testSettings.Hotkeys.Profiles.ContainsKey("TestProfile"));
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public async Task DeleteProfileAsync_DefaultProfile_ReturnsFalse()
        {
            var result = await _profileManager.DeleteProfileAsync("Default");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task SwitchProfileAsync_ValidProfile_SwitchesAndSaves()
        {
            var newProfile = new HotkeyProfile
            {
                Id = "Work",
                Name = "Work Profile",
                Hotkeys = new Dictionary<string, HotkeyDefinition>()
            };

            _testSettings.Hotkeys.Profiles["Work"] = newProfile;

            var result = await _profileManager.SwitchProfileAsync("Work");

            Assert.IsTrue(result);
            Assert.AreEqual("Work", _testSettings.Hotkeys.CurrentProfile);
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public async Task SwitchProfileAsync_InvalidProfile_ReturnsFalse()
        {
            var result = await _profileManager.SwitchProfileAsync("NonExistent");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task SwitchProfileAsync_SameProfile_ReturnsFalse()
        {
            var result = await _profileManager.SwitchProfileAsync("Default");
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task GetAllProfilesAsync_ReturnsAllProfiles()
        {
            var profiles = await _profileManager.GetAllProfilesAsync();
            Assert.AreEqual(_testSettings.Hotkeys.Profiles.Count, profiles.Count);
        }

        [TestMethod]
        public void GetCurrentProfile_ReturnsCurrentProfile()
        {
            var currentProfile = _profileManager.GetCurrentProfile();
            Assert.IsNotNull(currentProfile);
            Assert.AreEqual("Default", currentProfile.Id);
        }

        [TestMethod]
        public async Task AddHotkeyToProfileAsync_ValidProfile_AddsHotkey()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "new_hotkey",
                Name = "New Hotkey",
                Combination = "Ctrl+Alt+N",
                IsEnabled = true
            };

            var result = await _profileManager.AddHotkeyToProfileAsync("Default", hotkey);

            Assert.IsTrue(result);
            Assert.IsTrue(_testSettings.Hotkeys.Profiles["Default"].Hotkeys.ContainsKey("new_hotkey"));
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public async Task RemoveHotkeyFromProfileAsync_ValidProfile_RemovesHotkey()
        {
            var result = await _profileManager.RemoveHotkeyFromProfileAsync("Default", "toggle_recording");

            Assert.IsTrue(result);
            Assert.IsFalse(_testSettings.Hotkeys.Profiles["Default"].Hotkeys.ContainsKey("toggle_recording"));
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public async Task ProfileExistsAsync_ExistingProfile_ReturnsTrue()
        {
            var result = await _profileManager.ProfileExistsAsync("Default");
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task ProfileExistsAsync_NonExistentProfile_ReturnsFalse()
        {
            var result = await _profileManager.ProfileExistsAsync("NonExistent");
            Assert.IsFalse(result);
        }
    }
}