using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class HotkeyConflictDetectorTests
    {
        private Mock<ILogger<HotkeyConflictDetector>> _loggerMock = null!;
        private HotkeyConflictDetector _conflictDetector = null!;
        private HotkeySettings _testSettings = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<HotkeyConflictDetector>>();

            _testSettings = new HotkeySettings
            {
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
                }
            };

            _conflictDetector = new HotkeyConflictDetector(
                _loggerMock.Object
            );
        }

        [TestMethod]
        public void ValidateHotkey_ValidCombination_ReturnsValidResult()
        {
            var result = _conflictDetector.ValidateHotkey("Ctrl+Alt+Z", _testSettings);

            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));
            Assert.AreEqual(0, result.Conflicts.Count);
        }

        [TestMethod]
        public void ValidateHotkey_EmptyCombination_ReturnsInvalidResult()
        {
            var result = _conflictDetector.ValidateHotkey("", _testSettings);

            Assert.IsFalse(result.IsValid);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateHotkey_InvalidFormat_ReturnsInvalidResult()
        {
            var result = _conflictDetector.ValidateHotkey("InvalidFormat", _testSettings);

            Assert.IsFalse(result.IsValid);
            Assert.IsNotNull(result.ErrorMessage);
        }

        [TestMethod]
        public void ValidateHotkey_ConflictWithExisting_ReturnsConflictInfo()
        {
            var result = _conflictDetector.ValidateHotkey("Ctrl+Alt+V", _testSettings);

            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Conflicts.Count > 0);
            Assert.AreEqual("profile", result.Conflicts[0].ConflictType);
            Assert.AreEqual("toggle_recording", result.Conflicts[0].ConflictingHotkeyId);
        }

        [TestMethod]
        public void ValidateHotkey_ProblematicCombination_ReturnsWarning()
        {
            _testSettings.EnableAccessibilityOptions = true;

            var result = _conflictDetector.ValidateHotkey("Ctrl+Alt+Shift+Win+X", _testSettings);

            Assert.IsNotNull(result.WarningMessage);
        }

        [TestMethod]
        public void DetectConflicts_RegisteredHotkeys_ReturnsSystemConflicts()
        {
            var registeredHotkeys = new Dictionary<string, HotkeyDefinition>
            {
                ["system_hotkey"] = new HotkeyDefinition
                {
                    Id = "system_hotkey",
                    Name = "System Hotkey",
                    Combination = "Ctrl+Alt+V",
                    IsEnabled = true
                }
            };

            var conflicts = _conflictDetector.DetectConflicts("Ctrl+Alt+V", _testSettings, registeredHotkeys);

            Assert.IsTrue(conflicts.Count > 0);
            Assert.IsTrue(conflicts.Exists(c => c.ConflictType == "system"));
        }

        [TestMethod]
        public void DetectConflicts_NoConflicts_ReturnsEmptyList()
        {
            var registeredHotkeys = new Dictionary<string, HotkeyDefinition>();

            var conflicts = _conflictDetector.DetectConflicts("Ctrl+Alt+Z", _testSettings, registeredHotkeys);

            Assert.AreEqual(0, conflicts.Count);
        }

        [TestMethod]
        public void ParseHotkey_ValidCombination_ReturnsModifiersAndKey()
        {
            var (modifiers, key) = _conflictDetector.ParseHotkey("Ctrl+Alt+V");

            Assert.AreEqual(3, modifiers.Count); // Ctrl, Alt, V
            Assert.IsTrue(modifiers.Contains("Ctrl"));
            Assert.IsTrue(modifiers.Contains("Alt"));
            Assert.IsTrue(modifiers.Contains("V"));
        }

        [TestMethod]
        public void ParseHotkey_SingleKey_ReturnsSingleKey()
        {
            var (modifiers, key) = _conflictDetector.ParseHotkey("V");

            Assert.AreEqual(1, modifiers.Count);
            Assert.IsTrue(modifiers.Contains("V"));
        }

        [TestMethod]
        public void ParseHotkey_InvalidFormat_ReturnsEmpty()
        {
            var (modifiers, key) = _conflictDetector.ParseHotkey("Invalid+Format+With+Too+Many+Parts");

            Assert.AreEqual(0, modifiers.Count);
        }

        [TestMethod]
        public void IsProblematicCombination_TooManyModifiers_ReturnsTrue()
        {
            var result = _conflictDetector.IsProblematicCombination("Ctrl+Alt+Shift+Win+X", true);
            Assert.IsTrue(result);

            result = _conflictDetector.IsProblematicCombination("Ctrl+Alt+Shift+Win+X", false);
            Assert.IsFalse(result); // Should only warn when accessibility is enabled
        }

        [TestMethod]
        public void IsProblematicCombination_ReasonableCombination_ReturnsFalse()
        {
            var result = _conflictDetector.IsProblematicCombination("Ctrl+Alt+V", true);
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void GetConflictType_ProfileConflict_ReturnsProfile()
        {
            var conflict = _conflictDetector.GetConflictType("toggle_recording", _testSettings, new Dictionary<string, HotkeyDefinition>());
            Assert.AreEqual("profile", conflict);
        }

        [TestMethod]
        public void GetConflictType_SystemConflict_ReturnsSystem()
        {
            var registeredHotkeys = new Dictionary<string, HotkeyDefinition>
            {
                ["system_hotkey"] = new HotkeyDefinition
                {
                    Id = "system_hotkey",
                    Name = "System Hotkey",
                    Combination = "Ctrl+Alt+V",
                    IsEnabled = true
                }
            };

            var conflict = _conflictDetector.GetConflictType("system_hotkey", _testSettings, registeredHotkeys);
            Assert.AreEqual("system", conflict);
        }

        [TestMethod]
        public void GetConflictType_NoConflict_ReturnsNull()
        {
            var conflict = _conflictDetector.GetConflictType("nonexistent", _testSettings, new Dictionary<string, HotkeyDefinition>());
            Assert.IsNull(conflict);
        }
    }
}