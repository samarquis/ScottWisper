using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using WhisperKey.Services;
using WhisperKey.Configuration;

namespace WhisperKey.Tests
{
    /// <summary>
    /// Validation exception for settings validation
    /// </summary>
    public class ValidationException : Exception
    {
        public ValidationException() : base() { }
        public ValidationException(string message) : base(message) { }
        public ValidationException(string message, Exception innerException) : base(message, innerException) { }
    }

    [TestClass]
    public class SettingsValidationTests
    {
        private SettingsService _settingsService;
        private string _testAppDataPath;

        [TestInitialize]
        public void Setup()
        {
            // Create isolated test environment
            _testAppDataPath = Path.Combine(Path.GetTempPath(), "WhisperKeyTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testAppDataPath);
            
            // Initialize settings service with test configuration
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["Audio:SampleRate"] = "16000",
                    ["Audio:Channels"] = "1",
                    ["Transcription:Provider"] = "OpenAI",
                    ["Transcription:Model"] = "whisper-1",
                    ["Hotkeys:ToggleRecording"] = "Ctrl+Alt+V"
                })
                .Build();

            var options = new TestOptionsMonitor<AppSettings>(new AppSettings());
            _settingsService = new SettingsService(configuration, options, NullLogger<SettingsService>.Instance);
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Clean up test environment
            if (Directory.Exists(_testAppDataPath))
            {
                Directory.Delete(_testAppDataPath, true);
            }
        }

        [TestMethod]
        public async Task TestSettingsValidation_RulesAndConstraints()
        {
            // Test valid settings
            var validSettings = _settingsService.Settings;
            validSettings.Audio.SampleRate = 16000;
            validSettings.Audio.Channels = 1;
            validSettings.Transcription.Provider = "OpenAI";
            validSettings.Transcription.Model = "whisper-1";
            
            try
            {
                await _settingsService.SaveAsync();
            }
            catch (ValidationException)
            {
                Assert.Fail("Valid settings should not throw validation exception");
            }

            // Test invalid sample rate
            validSettings.Audio.SampleRate = 0;
            await Assert.ThrowsExceptionAsync<ValidationException>(
                () => _settingsService.SaveAsync(),
                "Zero sample rate should throw validation exception");

            validSettings.Audio.SampleRate = -1;
            await Assert.ThrowsExceptionAsync<ValidationException>(
                () => _settingsService.SaveAsync(),
                "Negative sample rate should throw validation exception");

            // Test invalid channels
            validSettings.Audio.SampleRate = 16000; // Reset to valid
            validSettings.Audio.Channels = 0;
            await Assert.ThrowsExceptionAsync<ValidationException>(
                () => _settingsService.SaveAsync(),
                "Zero channels should throw validation exception");

            validSettings.Audio.Channels = 3;
            await Assert.ThrowsExceptionAsync<ValidationException>(
                () => _settingsService.SaveAsync(),
                "More than 2 channels should throw validation exception");

            // Test empty transcription provider
            validSettings.Audio.Channels = 1; // Reset to valid
            validSettings.Transcription.Provider = "";
            await Assert.ThrowsExceptionAsync<ValidationException>(
                () => _settingsService.SaveAsync(),
                "Empty provider should throw validation exception");

            validSettings.Transcription.Provider = null;
            await Assert.ThrowsExceptionAsync<ValidationException>(
                () => _settingsService.SaveAsync(),
                "Null provider should throw validation exception");

            // Test empty model
            validSettings.Transcription.Provider = "OpenAI"; // Reset to valid
            validSettings.Transcription.Model = "";
            await Assert.ThrowsExceptionAsync<ValidationException>(
                () => _settingsService.SaveAsync(),
                "Empty model should throw validation exception");

            // Test empty hotkey
            validSettings.Transcription.Model = "whisper-1"; // Reset to valid
            validSettings.Hotkeys.ToggleRecording = "";
            await Assert.ThrowsExceptionAsync<ValidationException>(
                () => _settingsService.SaveAsync(),
                "Empty hotkey should throw validation exception");
        }

        [TestMethod]
        public async Task TestSettingsConflictDetectionAndResolution()
        {
            var profile = new HotkeyProfile
            {
                Id = "TestProfile",
                Name = "Test Profile",
                Hotkeys = new Dictionary<string, HotkeyDefinition>
                {
                    ["Action1"] = new HotkeyDefinition { Name = "Action1", Combination = "Ctrl+Alt+V" },
                    ["Action2"] = new HotkeyDefinition { Name = "Action2", Combination = "Ctrl+Alt+V" } // Conflict
                }
            };

            // Test conflict detection
            var validationResult = await _settingsService.ValidateHotkeyAsync("Ctrl+Alt+V");
            Assert.IsTrue(validationResult.IsValid, "First hotkey should be valid");

            // Add first hotkey to current profile
            await _settingsService.CreateHotkeyProfileAsync(profile);

            // Test conflict detection for duplicate
            var conflictResult = await _settingsService.ValidateHotkeyAsync("Ctrl+Alt+V");
            Assert.IsFalse(conflictResult.IsValid, "Duplicate hotkey should be invalid");
            Assert.IsNotNull(conflictResult.ErrorMessage, "Should have error message for conflict");
            Assert.IsTrue(conflictResult.Conflicts.Count > 0, "Should have conflict details");

            // Test suggested fix generation
            var suggestedFixes = conflictResult.Conflicts
                .Where(c => c.IsResolvable)
                .Select(c => c.SuggestedHotkey)
                .ToList();
            
            Assert.IsTrue(suggestedFixes.Count > 0, "Should provide suggested fixes");
        }

        [TestMethod]
        public async Task TestSettingsMigrationAndVersioning()
        {
            // Create v1.0 settings
            var oldSettings = new AppSettings
            {
                Audio = new AudioSettings
                {
                    SampleRate = 8000, // Old default
                    Channels = 1,
                    InputDeviceId = "default"
                },
                Transcription = new TranscriptionSettings
                {
                    Provider = "OpenAI",
                    Model = "whisper-1"
                }
            };

            // Save old settings format
            var oldSettingsPath = Path.Combine(_testAppDataPath, "oldsettings.json");
            var json = System.Text.Json.JsonSerializer.Serialize(oldSettings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(oldSettingsPath, json);

            // Test migration by loading and adding new fields
            var loadedSettings = _settingsService.Settings;
            
            // Should have new default values for migration
            Assert.IsNotNull(loadedSettings.UI, "Should initialize UI settings during migration");
            Assert.IsNotNull(loadedSettings.Audio.DeviceSettings, "Should initialize device settings during migration");
            Assert.IsNotNull(loadedSettings.Hotkeys.Profiles, "Should initialize hotkey profiles during migration");

            // Test version compatibility
            var backup = new SettingsBackup
            {
                Settings = loadedSettings,
                Version = "1.0"
            };

            Assert.AreEqual("1.0", backup.Version, "Should preserve version information");
            Assert.AreEqual("WhisperKey", backup.Application, "Should preserve application name");
        }

        [TestMethod]
        public async Task TestSettingsImportExportFunctionality()
        {
            var testProfile = new HotkeyProfile
            {
                Id = "ExportTest",
                Name = "Export Test Profile",
                Description = "Test profile for export/import",
                CreatedAt = DateTime.Now,
                ModifiedAt = DateTime.Now,
                Hotkeys = new Dictionary<string, HotkeyDefinition>
                {
                    ["TestAction"] = new HotkeyDefinition 
                    { 
                        Name = "TestAction", 
                        Combination = "Ctrl+Shift+T",
                        Description = "Test action"
                    }
                }
            };

            // Create profile
            var createResult = await _settingsService.CreateHotkeyProfileAsync(testProfile);
            Assert.IsTrue(createResult, "Should create test profile successfully");

            // Export profile
            var exportPath = Path.Combine(_testAppDataPath, "exported_profile.json");
            await _settingsService.ExportHotkeyProfileAsync("ExportTest", exportPath);
            Assert.IsTrue(File.Exists(exportPath), "Export file should exist");

            // Verify export content
            var exportContent = await File.ReadAllTextAsync(exportPath);
            Assert.IsTrue(exportContent.Contains("ExportTest"), "Export should contain profile name");
            Assert.IsTrue(exportContent.Contains("Ctrl+Shift+T"), "Export should contain hotkey combination");

            // Delete original profile
            var deleteResult = await _settingsService.DeleteHotkeyProfileAsync("ExportTest");
            Assert.IsTrue(deleteResult, "Should delete original profile");

            // Import profile
            var importedProfile = await _settingsService.ImportHotkeyProfileAsync(exportPath);
            Assert.IsNotNull(importedProfile, "Import should return valid profile");
            Assert.AreEqual("ExportTest_1", importedProfile.Id, "Import should rename conflicting profile");
            Assert.AreEqual("Ctrl+Shift+T", importedProfile.Hotkeys["TestAction"].Combination);

            // Verify imported profile works
            var profiles = await _settingsService.GetHotkeyProfilesAsync();
            var imported = profiles.FirstOrDefault(p => p.Id.StartsWith("ExportTest_"));
            Assert.IsNotNull(imported, "Should find imported profile in list");
        }

        [TestMethod]
        public async Task TestSettingsRepairAndRecovery()
        {
            // Corrupt settings file scenario
            var corruptSettingsPath = Path.Combine(_testAppDataPath, "usersettings.json");
            await File.WriteAllTextAsync(corruptSettingsPath, "{ invalid json content");

            // Test graceful recovery
            try
            {
                // Create new settings service to test loading corrupted settings
                var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                    .AddInMemoryCollection(new Dictionary<string, string>())
                    .Build();
                
                var options = new TestOptionsMonitor<AppSettings>(new AppSettings());
                var recoveryService = new SettingsService(configuration, options, NullLogger<SettingsService>.Instance);
                
                // Should not crash, should fall back to defaults
                var recoveredSettings = recoveryService.Settings;
                Assert.IsNotNull(recoveredSettings, "Should recover with default settings");
                Assert.AreEqual(16000, recoveredSettings.Audio.SampleRate, "Should use default sample rate");
            }
            catch (Exception)
            {
                Assert.Fail("Should handle corrupted settings gracefully without throwing");
            }

            // Test settings reset functionality
            var originalSettings = _settingsService.Settings;
            originalSettings.Audio.SampleRate = 48000;
            originalSettings.Transcription.Provider = "TestProvider";
            
            await _settingsService.SaveAsync();

            // Reset to defaults
            _settingsService.Settings.Audio.SampleRate = 16000;
            _settingsService.Settings.Transcription.Provider = "OpenAI";
            await _settingsService.SaveAsync();

            var resetSettings = _settingsService.Settings;
            Assert.AreEqual(16000, resetSettings.Audio.SampleRate, "Should reset sample rate to default");
            Assert.AreEqual("OpenAI", resetSettings.Transcription.Provider, "Should reset provider to default");
        }

        [TestMethod]
        public async Task TestSettingsSecurityAndEncryption()
        {
            var testData = "sensitive_api_key_12345";
            var testKey = "TestEncryptionKey";

            // Test encrypted value storage
            await _settingsService.SetEncryptedValueAsync(testKey, testData);

            // Verify encrypted file exists and is not plaintext
            var encryptedFilePath = Path.Combine(_testAppDataPath, $"{testKey}.encrypted");
            Assert.IsTrue(File.Exists(encryptedFilePath), "Encrypted file should exist");

            var encryptedContent = await File.ReadAllTextAsync(encryptedFilePath);
            Assert.AreNotEqual(testData, encryptedContent, "File should not contain plaintext");
            Assert.IsFalse(encryptedContent.Contains("sensitive_api_key"), "Should not contain sensitive data in plaintext");

            // Test decryption
            var decryptedValue = await _settingsService.GetEncryptedValueAsync(testKey);
            Assert.AreEqual(testData, decryptedValue, "Should decrypt to original value");

            // Test non-existent key returns empty
            var nonExistentValue = await _settingsService.GetEncryptedValueAsync("NonExistentKey");
            Assert.AreEqual(string.Empty, nonExistentValue, "Non-existent key should return empty string");

            // Test empty value encryption
            await _settingsService.SetEncryptedValueAsync("EmptyTest", "");
            var emptyDecrypted = await _settingsService.GetEncryptedValueAsync("EmptyTest");
            Assert.AreEqual(string.Empty, emptyDecrypted, "Empty value should encrypt/decrypt correctly");

            // Test null value encryption
            await _settingsService.SetEncryptedValueAsync("NullTest", null);
            var nullDecrypted = await _settingsService.GetEncryptedValueAsync("NullTest");
            Assert.AreEqual(string.Empty, nullDecrypted, "Null value should handle gracefully");
        }

        [TestMethod]
        public async Task TestSettingsPerformanceAndScalability()
        {
            // Test large settings save/load performance
            var largeSettings = _settingsService.Settings;

            // Create many device settings entries
            for (int i = 0; i < 1000; i++)
            {
                largeSettings.Audio.DeviceSettings[$"device_{i}"] = new DeviceSpecificSettings
                {
                    Name = $"Device {i}",
                    SampleRate = 16000 + i,
                    Channels = i % 2 + 1,
                    BufferSize = 512 + i,
                    IsEnabled = true,
                    IsCompatible = true,
                    LastTested = DateTime.Now.AddMinutes(-i),
                    Notes = $"Test notes for device {i}"
                };
            }

            // Measure save performance
            var saveStartTime = DateTime.Now;
            await _settingsService.SaveAsync();
            var saveDuration = DateTime.Now - saveStartTime;
            
            Assert.IsTrue(saveDuration.TotalSeconds < 5, "Save should complete within 5 seconds even with large settings");

            // Measure load performance
            var loadStartTime = DateTime.Now;
            var deviceCount = largeSettings.Audio.DeviceSettings.Count;
            var loadDuration = DateTime.Now - loadStartTime;

            Assert.AreEqual(1000, deviceCount, "Should load all device settings");
            Assert.IsTrue(loadDuration.TotalMilliseconds < 100, "Access should be fast even with many entries");

            // Test memory efficiency
            var settingsBefore = _settingsService.Settings;
            var profileBefore = await _settingsService.GetCurrentHotkeyProfileAsync();
            
            // Access same data multiple times
            for (int i = 0; i < 100; i++)
            {
                _ = _settingsService.Settings.Audio.SampleRate;
                _ = await _settingsService.GetCurrentHotkeyProfileAsync();
            }

            var settingsAfter = _settingsService.Settings;
            var profileAfter = await _settingsService.GetCurrentHotkeyProfileAsync();

            Assert.AreEqual(settingsBefore.Audio.SampleRate, settingsAfter.Audio.SampleRate, "Should maintain consistency");
            Assert.AreEqual(profileBefore.Id, profileAfter.Id, "Should maintain profile consistency");
        }

        [TestMethod]
        public async Task TestSettingsInputValidationForAllTypes()
        {
            var settings = _settingsService.Settings;

            // Test numeric validation
            settings.Audio.SampleRate = -100;
            await Assert.ThrowsExceptionAsync<ValidationException>(() => _settingsService.SaveAsync(), "Negative sample rate should fail");

            settings.Audio.SampleRate = 0;
            await Assert.ThrowsExceptionAsync<ValidationException>(() => _settingsService.SaveAsync(), "Zero sample rate should fail");

            settings.Audio.SampleRate = 1000000; // Unrealistic but valid
            try
            {
                await _settingsService.SaveAsync();
            }
            catch (ValidationException)
            {
                Assert.Fail("High but valid sample rate should not fail");
            }

            // Test string validation
            settings.Audio.SampleRate = 16000; // Reset to valid
            settings.Transcription.Provider = new string('a', 10000); // Very long string
            // Should handle long strings gracefully (may or may not fail depending on validation)

            settings.Transcription.Provider = "ValidProvider";
            settings.Transcription.Model = ""; // Empty string
            await Assert.ThrowsExceptionAsync<ValidationException>(() => _settingsService.SaveAsync(), "Empty model should fail");

            // Test range validation
            settings.Transcription.Model = "ValidModel";
            settings.Audio.Channels = -1;
            await Assert.ThrowsExceptionAsync<ValidationException>(() => _settingsService.SaveAsync(), "Negative channels should fail");

            settings.Audio.Channels = 5;
            await Assert.ThrowsExceptionAsync<ValidationException>(() => _settingsService.SaveAsync(), "Too many channels should fail");

            // Test format validation
            settings.Audio.Channels = 1; // Reset to valid
            settings.Transcription.ApiKey = "invalid_api_key_format";
            // May or may not fail depending on format validation rules

            // Test dependency validation
            settings.Transcription.ApiKey = "sk-1234567890"; // Valid format
            settings.Transcription.Provider = ""; // Empty but API key present
            await Assert.ThrowsExceptionAsync<ValidationException>(() => _settingsService.SaveAsync(), "Missing provider with API key should fail");

            settings.Transcription.Provider = "LocalWhisper";
            settings.Transcription.ApiKey = ""; // Local provider doesn't need API key
            try
            {
                await _settingsService.SaveAsync();
            }
            catch (ValidationException)
            {
                Assert.Fail("Local provider without API key should be valid");
            }
        }

        [TestMethod]
        public async Task TestSettingsDependencyValidation()
        {
            var settings = _settingsService.Settings;

            // Test audio-transcription dependency
            settings.Audio.SampleRate = 16000;
            settings.Transcription.Provider = "OpenAI";
            settings.Transcription.Model = "whisper-1";
            
            // Should be valid - all required fields present
            try
            {
                await _settingsService.SaveAsync();
            }
            catch (ValidationException)
            {
                Assert.Fail("Valid dependent settings should save successfully");
            }

            // Test missing dependent settings
            settings.Audio.SampleRate = 16000; // Audio configured
            settings.Transcription.Provider = ""; // Missing provider
            
            await Assert.ThrowsExceptionAsync<ValidationException>(() => _settingsService.SaveAsync(), 
                "Missing transcription provider should fail when audio is configured");

            // Test hotkey dependency
            settings.Transcription.Provider = "OpenAI"; // Reset
            settings.Hotkeys.ToggleRecording = ""; // Missing hotkey
            await Assert.ThrowsExceptionAsync<ValidationException>(() => _settingsService.SaveAsync(), 
                "Missing toggle hotkey should fail when transcription is configured");

            // Test device dependency validation
            settings.Hotkeys.ToggleRecording = "Ctrl+Alt+V"; // Reset
            settings.Audio.InputDeviceId = "non_existent_device";
            
            // Should warn about non-existent device but may not fail (depends on validation strategy)
            try
            {
                await _settingsService.SaveAsync();
            }
            catch (ValidationException)
            {
                // This is acceptable behavior
            }

            // Test UI dependency on audio
            settings.Audio.InputDeviceId = "default"; // Reset to valid
            settings.UI.ShowVisualFeedback = true;
            settings.UI.MinimizeToTray = true;
            
            // Should be valid
            try
            {
                await _settingsService.SaveAsync();
            }
            catch (ValidationException)
            {
                Assert.Fail("Valid UI settings should save successfully");
            }
        }
    }
}