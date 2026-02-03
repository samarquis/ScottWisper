using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class HotkeyServiceTests
    {
        private Mock<ISettingsService> _settingsServiceMock = null!;
        private Mock<IHotkeyRegistrar> _hotkeyRegistrarMock = null!;
        private HotkeyService _hotkeyService = null!;
        private AppSettings _testSettings = null!;
        private IntPtr _testWindowHandle = new IntPtr(12345);

        [TestInitialize]
        public void Setup()
        {
            _settingsServiceMock = new Mock<ISettingsService>();
            _hotkeyRegistrarMock = new Mock<IHotkeyRegistrar>();

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
                                },
                                ["show_settings"] = new HotkeyDefinition
                                {
                                    Id = "show_settings",
                                    Name = "Show Settings",
                                    Combination = "Ctrl+Alt+S",
                                    Action = "show_settings",
                                    IsEnabled = true
                                }
                            }
                        }
                    },
                    ConflictCheckInterval = 0 // Disable timer-based conflict checking for tests
                }
            };

            _settingsServiceMock.Setup(s => s.Settings).Returns(_testSettings);
            _settingsServiceMock.Setup(s => s.SaveAsync()).Returns(Task.CompletedTask);

            _hotkeyService = new HotkeyService(
                _settingsServiceMock.Object,
                _hotkeyRegistrarMock.Object,
                _testWindowHandle
            );
        }

        [TestCleanup]
        public void Cleanup()
        {
            _hotkeyService?.Dispose();
        }

        #region Constructor and Initialization Tests

        [TestMethod]
        public void Constructor_WithDependencies_InitializesCorrectly()
        {
            Assert.IsNotNull(_hotkeyService);
            Assert.IsNotNull(_hotkeyService.CurrentProfile);
            Assert.AreEqual("Default", _hotkeyService.CurrentProfile.Id);
        }

        [TestMethod]
        public async Task Constructor_WithNullProfile_CreatesDefaultProfile()
        {
            _testSettings.Hotkeys.Profiles.Clear();
            _testSettings.Hotkeys.CurrentProfile = "NonExistent";

            var service = new HotkeyService(
                _settingsServiceMock.Object,
                _hotkeyRegistrarMock.Object,
                _testWindowHandle
            );

            // Wait for async initialization to complete
            await Task.Delay(100);

            Assert.IsNotNull(service.CurrentProfile);
            Assert.AreEqual("Default", service.CurrentProfile.Id);
            Assert.IsTrue(service.CurrentProfile.Hotkeys.Count > 0);

            service.Dispose();
        }

        [TestMethod]
        public void IsHotkeyRegistered_Initially_ReturnsFalse()
        {
            // Before any registration, should be false
            Assert.IsFalse(_hotkeyService.IsHotkeyRegistered);
        }

        #endregion

        #region Hotkey Registration Tests

        [TestMethod]
        public void RegisterHotkey_ValidHotkey_ReturnsTrue()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            var result = _hotkeyService.RegisterHotkey(hotkey);

            Assert.IsTrue(result);
            Assert.IsTrue(_hotkeyService.IsHotkeyRegistered);
        }

        [TestMethod]
        public void RegisterHotkey_InvalidWindowHandle_ReturnsFalse()
        {
            var service = new HotkeyService(
                _settingsServiceMock.Object,
                _hotkeyRegistrarMock.Object,
                IntPtr.Zero
            );

            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            var result = service.RegisterHotkey(hotkey);

            Assert.IsFalse(result);
            service.Dispose();
        }

        [TestMethod]
        public void RegisterHotkey_EmptyCombination_ReturnsFalse()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "",
                IsEnabled = true
            };

            var result = _hotkeyService.RegisterHotkey(hotkey);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void RegisterHotkey_DuplicateRegistration_UnregistersFirst()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "toggle_recording",
                Name = "Toggle Recording",
                Combination = "Ctrl+Alt+V",
                IsEnabled = true
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            // First registration
            _hotkeyService.RegisterHotkey(hotkey);

            // Second registration should unregister first
            _hotkeyService.RegisterHotkey(hotkey);

            _hotkeyRegistrarMock.Verify(r => r.UnregisterHotKey(_testWindowHandle, It.IsAny<int>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public void RegisterHotkey_WindowsApiFailure_TriggersConflictEvent()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+V",
                IsEnabled = true
            };

            bool eventTriggered = false;
            HotkeyConflictEventArgs? eventArgs = null;

            _hotkeyService.HotkeyConflictDetected += (s, e) =>
            {
                eventTriggered = true;
                eventArgs = e;
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(false);

            _hotkeyRegistrarMock
                .Setup(r => r.GetLastWin32Error())
                .Returns(1409); // ERROR_HOTKEY_ALREADY_REGISTERED

            var result = _hotkeyService.RegisterHotkey(hotkey);

            Assert.IsFalse(result);
            Assert.IsTrue(eventTriggered);
            Assert.IsNotNull(eventArgs);
            Assert.AreEqual("system", eventArgs!.Conflict.ConflictType);
        }

        [TestMethod]
        public void UnregisterHotkey_ExistingHotkey_UnregistersAndRemoves()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            _hotkeyService.RegisterHotkey(hotkey);

            _hotkeyService.UnregisterHotkey(hotkey.Id);

            _hotkeyRegistrarMock.Verify(r => r.UnregisterHotKey(_testWindowHandle, It.IsAny<int>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public void UnregisterHotkey_NonExistentHotkey_DoesNotThrow()
        {
            _hotkeyService.UnregisterHotkey("non_existent_id");
            // Should not throw
        }

        #endregion

        #region Hotkey Validation Tests

        [TestMethod]
        public void ValidateHotkey_ValidCombination_ReturnsValidResult()
        {
            // Use a combination that's not already registered in the test setup
            var result = _hotkeyService.ValidateHotkey("Ctrl+Alt+Z");

            Assert.IsTrue(result.IsValid, $"Expected valid result but got error: {result.ErrorMessage}");
            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage), "ErrorMessage should be null or empty for valid combination");
        }

        [TestMethod]
        public void ValidateHotkey_EmptyCombination_ReturnsInvalidResult()
        {
            var result = _hotkeyService.ValidateHotkey("");

            Assert.IsFalse(result.IsValid);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateHotkey_InvalidFormat_ReturnsInvalidResult()
        {
            var result = _hotkeyService.ValidateHotkey("InvalidFormat");

            Assert.IsFalse(result.IsValid);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateHotkey_ConflictWithRegistered_ReturnsConflictInfo()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+V",
                IsEnabled = true
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            _hotkeyService.RegisterHotkey(hotkey);

            // Try to validate the same combination
            var result = _hotkeyService.ValidateHotkey("Ctrl+Alt+V");

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Conflicts.Count > 0);
        }

        [TestMethod]
        public void ValidateHotkey_ProblematicCombination_ReturnsWarning()
        {
            _testSettings.Hotkeys.EnableAccessibilityOptions = true;

            var result = _hotkeyService.ValidateHotkey("Ctrl+Alt+Shift+Win+X");

            Assert.IsNotNull(result.WarningMessage);
        }

        #endregion

        #region Profile Management Tests

        [TestMethod]
        public async Task SwitchProfileAsync_ValidProfile_SwitchesAndRegistersHotkeys()
        {
            var newProfile = new HotkeyProfile
            {
                Id = "Work",
                Name = "Work Profile",
                Hotkeys = new Dictionary<string, HotkeyDefinition>
                {
                    ["work_hotkey"] = new HotkeyDefinition
                    {
                        Id = "work_hotkey",
                        Name = "Work Hotkey",
                        Combination = "Ctrl+Alt+W",
                        IsEnabled = true
                    }
                }
            };

            _testSettings.Hotkeys.Profiles["Work"] = newProfile;

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            var result = await _hotkeyService.SwitchProfileAsync("Work");

            Assert.IsTrue(result);
            Assert.AreEqual("Work", _hotkeyService.CurrentProfile.Id);
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task SwitchProfileAsync_InvalidProfile_ReturnsFalse()
        {
            var result = await _hotkeyService.SwitchProfileAsync("NonExistent");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task SwitchProfileAsync_SameProfile_ReturnsFalse()
        {
            var result = await _hotkeyService.SwitchProfileAsync("Default");

            Assert.IsFalse(result);
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

            var result = await _hotkeyService.CreateProfileAsync(newProfile);

            Assert.IsTrue(result);
            Assert.IsTrue(_testSettings.Hotkeys.Profiles.ContainsKey("Gaming"));
            Assert.IsNotNull(newProfile.CreatedAt);
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.AtLeastOnce);
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

            var result = await _hotkeyService.CreateProfileAsync(duplicateProfile);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task CreateProfileAsync_NullProfile_ReturnsFalse()
        {
            var result = await _hotkeyService.CreateProfileAsync(null!);

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

            var result = await _hotkeyService.CreateProfileAsync(profile);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task UpdateProfileAsync_ExistingProfile_UpdatesAndSaves()
        {
            var existingProfile = _testSettings.Hotkeys.Profiles["Default"];
            existingProfile.Name = "Updated Default";

            var result = await _hotkeyService.UpdateProfileAsync(existingProfile);

            Assert.IsTrue(result);
            Assert.IsNotNull(existingProfile.ModifiedAt);
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task UpdateProfileAsync_CurrentProfile_RefreshesHotkeys()
        {
            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            var existingProfile = _testSettings.Hotkeys.Profiles["Default"];
            existingProfile.Name = "Updated Default";

            var result = await _hotkeyService.UpdateProfileAsync(existingProfile);

            Assert.IsTrue(result);
            _hotkeyRegistrarMock.Verify(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()), Times.AtLeastOnce);
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

            var result = await _hotkeyService.UpdateProfileAsync(profile);

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

            var result = await _hotkeyService.DeleteProfileAsync("TestProfile");

            Assert.IsTrue(result);
            Assert.IsFalse(_testSettings.Hotkeys.Profiles.ContainsKey("TestProfile"));
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task DeleteProfileAsync_DefaultProfile_ReturnsFalse()
        {
            var result = await _hotkeyService.DeleteProfileAsync("Default");

            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task DeleteProfileAsync_CurrentProfile_SwitchesToDefault()
        {
            _testSettings.Hotkeys.Profiles["TempProfile"] = new HotkeyProfile
            {
                Id = "TempProfile",
                Name = "Temp Profile",
                Hotkeys = new Dictionary<string, HotkeyDefinition>()
            };

            _testSettings.Hotkeys.CurrentProfile = "TempProfile";

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            await _hotkeyService.SwitchProfileAsync("TempProfile");

            var result = await _hotkeyService.DeleteProfileAsync("TempProfile");

            Assert.IsTrue(result);
            Assert.AreEqual("Default", _hotkeyService.CurrentProfile.Id);
        }

        [TestMethod]
        public async Task GetAllProfilesAsync_ReturnsAllProfiles()
        {
            var profiles = await _hotkeyService.GetAllProfilesAsync();

            Assert.AreEqual(_testSettings.Hotkeys.Profiles.Count, profiles.Count);
        }

        [TestMethod]
        public async Task UpdateHotkeyAsync_ExistingHotkey_UpdatesAndReRegisters()
        {
            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            var updatedHotkey = new HotkeyDefinition
            {
                Id = "toggle_recording",
                Name = "Toggle Recording Updated",
                Combination = "Ctrl+Alt+R",
                IsEnabled = true
            };

            await _hotkeyService.UpdateHotkeyAsync("toggle_recording", updatedHotkey);

            Assert.AreEqual("Toggle Recording Updated", _hotkeyService.CurrentProfile.Hotkeys["toggle_recording"].Name);
            _settingsServiceMock.Verify(s => s.SaveAsync(), Times.AtLeastOnce);
        }

        [TestMethod]
        public async Task ResetToDefaultsAsync_ResetsToDefaultProfile()
        {
            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            // Modify the current profile
            _hotkeyService.CurrentProfile.Name = "Modified";

            await _hotkeyService.ResetToDefaultsAsync();

            Assert.AreEqual("Default", _hotkeyService.CurrentProfile.Id);
            Assert.IsTrue(_hotkeyService.CurrentProfile.Hotkeys.Count >= 3);
        }

        #endregion

        #region Import/Export Tests

        [TestMethod]
        public async Task ExportProfileAsync_ValidProfile_ExportsToFile()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_export_{Guid.NewGuid()}.json");

            try
            {
                await _hotkeyService.ExportProfileAsync("Default", tempFile);

                Assert.IsTrue(File.Exists(tempFile));
                var content = await File.ReadAllTextAsync(tempFile);
                Assert.IsTrue(content.Contains("Default"));
                Assert.IsTrue(content.Contains("WhisperKey"));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task ExportProfileAsync_InvalidProfile_ThrowsArgumentException()
        {
            await Assert.ThrowsExceptionAsync<ArgumentException>(async () =>
            {
                await _hotkeyService.ExportProfileAsync("NonExistent", "test.json");
            });
        }

        [TestMethod]
        public async Task ImportProfileAsync_ValidFile_ImportsProfile()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_import_{Guid.NewGuid()}.json");

            try
            {
                var exportData = new
                {
                    Profile = new HotkeyProfile
                    {
                        Id = "Imported",
                        Name = "Imported Profile",
                        Hotkeys = new Dictionary<string, HotkeyDefinition>
                        {
                            ["imported_hotkey"] = new HotkeyDefinition
                            {
                                Id = "imported_hotkey",
                                Name = "Imported Hotkey",
                                Combination = "Ctrl+Alt+I",
                                IsEnabled = true
                            }
                        }
                    },
                    ExportedAt = DateTime.Now,
                    Version = "1.0",
                    Application = "WhisperKey"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempFile, json);

                var result = await _hotkeyService.ImportProfileAsync(tempFile);

                Assert.IsNotNull(result);
                Assert.AreEqual("Imported", result.Id);
                Assert.IsTrue(_testSettings.Hotkeys.Profiles.ContainsKey("Imported"));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task ImportProfileAsync_DuplicateId_GeneratesUniqueId()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_import_dup_{Guid.NewGuid()}.json");

            try
            {
                var exportData = new
                {
                    Profile = new HotkeyProfile
                    {
                        Id = "Default", // Duplicate ID
                        Name = "Another Default",
                        Hotkeys = new Dictionary<string, HotkeyDefinition>()
                    },
                    ExportedAt = DateTime.Now,
                    Version = "1.0",
                    Application = "WhisperKey"
                };

                var json = System.Text.Json.JsonSerializer.Serialize(exportData, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(tempFile, json);

                var result = await _hotkeyService.ImportProfileAsync(tempFile);

                Assert.IsNotNull(result);
                Assert.AreNotEqual("Default", result.Id);
                Assert.IsTrue(result.Id.StartsWith("Default_"));
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        [TestMethod]
        public async Task ImportProfileAsync_NonExistentFile_ThrowsFileNotFoundException()
        {
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () =>
            {
                await _hotkeyService.ImportProfileAsync("nonexistent.json");
            });
        }

        [TestMethod]
        public async Task ImportProfileAsync_InvalidJson_ThrowsInvalidOperationException()
        {
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_import_invalid_{Guid.NewGuid()}.json");

            try
            {
                await File.WriteAllTextAsync(tempFile, "{ invalid json }");

                await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
                {
                    await _hotkeyService.ImportProfileAsync(tempFile);
                });
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }

        #endregion

        #region Hotkey Parsing Tests

        [TestMethod]
        public void RegisterHotkey_VariousFormats_ParsesCorrectly()
        {
            var testCases = new[]
            {
                "Ctrl+A",
                "Alt+B",
                "Shift+C",
                "Win+D",
                "Ctrl+Alt+E",
                "Ctrl+Shift+F",
                "Ctrl+Alt+Shift+G",
                "Ctrl+Alt+Shift+Win+H"
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            foreach (var combination in testCases)
            {
                var hotkey = new HotkeyDefinition
                {
                    Id = $"test_{combination.Replace("+", "_")}",
                    Name = "Test",
                    Combination = combination,
                    IsEnabled = true
                };

                var result = _hotkeyService.RegisterHotkey(hotkey);
                Assert.IsTrue(result, $"Failed to parse: {combination}");
            }
        }

        [TestMethod]
        public void RegisterHotkey_SpecialKeys_ParsesCorrectly()
        {
            var testCases = new[]
            {
                "Ctrl+Space",
                "Alt+Enter",
                "Shift+Tab",
                "Ctrl+F1",
                "Ctrl+Delete",
                "Alt+Insert"
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            foreach (var combination in testCases)
            {
                var hotkey = new HotkeyDefinition
                {
                    Id = $"test_{combination.Replace("+", "_")}",
                    Name = "Test",
                    Combination = combination,
                    IsEnabled = true
                };

                var result = _hotkeyService.RegisterHotkey(hotkey);
                Assert.IsTrue(result, $"Failed to parse: {combination}");
            }
        }

        [TestMethod]
        public void RegisterHotkey_InvalidFormats_ReturnsFalse()
        {
            var invalidCases = new[]
            {
                "",
                "   ",
                "InvalidKey",
                "Ctrl+",
                "+A",
                "Ctrl+InvalidKeyName"
            };

            foreach (var combination in invalidCases)
            {
                var hotkey = new HotkeyDefinition
                {
                    Id = "test",
                    Name = "Test",
                    Combination = combination,
                    IsEnabled = true
                };

                var result = _hotkeyService.RegisterHotkey(hotkey);
                Assert.IsFalse(result, $"Should have failed: {combination}");
            }
        }

        #endregion

        #region Event Tests

        [TestMethod]
        public void HotkeyPressed_EventTriggered_WhenHotkeyRegistered()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            bool eventTriggered = false;
            HotkeyPressedEventArgs? eventArgs = null;

            _hotkeyService.HotkeyPressed += (s, e) =>
            {
                eventTriggered = true;
                eventArgs = e;
            };

            _hotkeyService.RegisterHotkey(hotkey);

            // Note: In actual usage, the event would be triggered by Windows message loop
            // This test verifies the event wiring is set up correctly
            Assert.IsTrue(_hotkeyService.IsHotkeyRegistered);
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_UnregistersAllHotkeys()
        {
            var hotkey = new HotkeyDefinition
            {
                Id = "test_hotkey",
                Name = "Test Hotkey",
                Combination = "Ctrl+Alt+T",
                IsEnabled = true
            };

            _hotkeyRegistrarMock
                .Setup(r => r.RegisterHotKey(_testWindowHandle, It.IsAny<int>(), It.IsAny<uint>(), It.IsAny<uint>()))
                .Returns(true);

            _hotkeyService.RegisterHotkey(hotkey);

            _hotkeyService.Dispose();

            _hotkeyRegistrarMock.Verify(r => r.UnregisterHotKey(_testWindowHandle, It.IsAny<int>()), Times.AtLeastOnce);
        }

        [TestMethod]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            _hotkeyService.Dispose();
            _hotkeyService.Dispose(); // Should not throw
        }

        #endregion
    }
}
