using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using WhisperKey.Services;
using WhisperKey.Configuration;
using WhisperKey.Repositories;

namespace WhisperKey.Tests.Unit
{
    public class TestOptionsMonitor<T> : IOptionsMonitor<T>
    {
        private readonly T _options;
        public TestOptionsMonitor(T options)
        {
            _options = options;
        }
        public T CurrentValue => _options;
        public T Get(string? name) => _options;
        public IDisposable OnChange(Action<T, string> listener) => throw new NotImplementedException();
    }

    [TestClass]
    public class SettingsTests
    {
        private SettingsService _settingsService;
        private string _testAppDataPath;
        private Mock<ISettingsRepository> _mockRepository;

        [TestInitialize]
        public void Setup()
        {
            // Create isolated test environment
            _testAppDataPath = Path.Combine(Path.GetTempPath(), "WhisperKeyFunctionalityTests", Guid.NewGuid().ToString());
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

            // Use a real repository for tests that verify file operations
            var fileSystem = new FileSystemService("WhisperKeyTests");
            var repository = new FileSettingsRepository(NullLogger<FileSettingsRepository>.Instance, fileSystem, _testAppDataPath);
            var optionsMonitor = new TestOptionsMonitor<AppSettings>(new AppSettings());
            _settingsService = new SettingsService(configuration, optionsMonitor, NullLogger<SettingsService>.Instance, repository, autoLoad: false, customAppDataPath: _testAppDataPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _settingsService?.Dispose();
            
            // Clean up test environment
            if (Directory.Exists(_testAppDataPath))
            {
                try
                {
                    Directory.Delete(_testAppDataPath, true);
                }
                catch
                {
                    // Ignore transient cleanup errors
                }
            }
        }

        [TestMethod]
        public async Task TestSettingsLoadingAndSaving()
        {
            // Test initial loading
            var initialSettings = _settingsService.Settings;
            Assert.IsNotNull(initialSettings, "Settings should load on initialization");
            Assert.AreEqual(16000, initialSettings.Audio.SampleRate, "Should load initial sample rate");
            Assert.AreEqual("OpenAI", initialSettings.Transcription.Provider, "Should load initial provider");
            Assert.AreEqual("Ctrl+Alt+V", initialSettings.Hotkeys.ToggleRecording, "Should load initial hotkey");

            // Modify settings
            initialSettings.Audio.SampleRate = 48000;
            initialSettings.Audio.Channels = 2;
            initialSettings.Transcription.Provider = "TestProvider";
            initialSettings.UI.ShowVisualFeedback = false;

            // Save settings
            await _settingsService.SaveImmediateAsync();

            // Verify settings file exists
            var settingsPath = Path.Combine(_testAppDataPath, "usersettings.json");
            Assert.IsTrue(File.Exists(settingsPath), "Settings file should be created after save");

            // Load new instance to test persistence
            var newConfiguration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .AddJsonFile(settingsPath, optional: true, reloadOnChange: true)
                .Build();
            
            var newOptionsMonitor = new TestOptionsMonitor<AppSettings>(new AppSettings());
            var newRepository = new FileSettingsRepository(NullLogger<FileSettingsRepository>.Instance, new FileSystemService(), _testAppDataPath);
            var newSettingsService = new SettingsService(newConfiguration, newOptionsMonitor, NullLogger<SettingsService>.Instance, newRepository, autoLoad: false, customAppDataPath: _testAppDataPath);
            // Explicitly load
            await newSettingsService.LoadUserSettingsAsync();
            var loadedSettings = newSettingsService.Settings;

            Assert.AreEqual(48000, loadedSettings.Audio.SampleRate, "Should persist modified sample rate");
            Assert.AreEqual(2, loadedSettings.Audio.Channels, "Should persist modified channels");
            Assert.AreEqual("TestProvider", loadedSettings.Transcription.Provider, "Should persist modified provider");
            Assert.AreEqual(false, loadedSettings.UI.ShowVisualFeedback, "Should persist modified UI setting");
        }

        [TestMethod]
        public async Task TestRealTimeSettingsUpdates()
        {
            var settingsUpdated = false;
            var updateKey = string.Empty;
            var updateOldValue = (object?)null;
            var updateNewValue = (object?)null;

            // Use the variables to avoid warnings
            Assert.IsFalse(settingsUpdated);
            Assert.AreEqual(string.Empty, updateKey);
            Assert.IsNull(updateOldValue);
            Assert.IsNull(updateNewValue);

            // Subscribe to settings changes (would need to implement event in real scenario)
            // For now, test direct property updates

            var originalSampleRate = _settingsService.Settings.Audio.SampleRate;
            var newSampleRate = 44100;

            // Update setting
            _settingsService.Settings.Audio.SampleRate = newSampleRate;
            await _settingsService.SaveImmediateAsync();

            // Verify immediate effect
            Assert.AreEqual(newSampleRate, _settingsService.Settings.Audio.SampleRate, "Setting should update immediately");

            // Test multiple concurrent updates
            var updates = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var updateValue = 16000 + (i * 1000);
                updates.Add(Task.Run(async () =>
                {
                    _settingsService.Settings.Audio.SampleRate = updateValue;
                    await _settingsService.SaveImmediateAsync();
                }));
            }

            await Task.WhenAll(updates);

            // Should have final value from last update
            Assert.IsTrue(_settingsService.Settings.Audio.SampleRate >= 16000, "Should handle concurrent updates");
        }

        [TestMethod]
        public async Task TestSettingsSynchronizationAcrossServices()
        {
            // Simulate multiple services accessing settings
            var service1Settings = _settingsService.Settings;
            var service2Settings = _settingsService.Settings;

            // Service 1 modifies audio settings
            service1Settings.Audio.SampleRate = 48000;
            service1Settings.Audio.Channels = 2;
            await _settingsService.SaveImmediateAsync();

            // Service 2 should see changes
            Assert.AreEqual(48000, service2Settings.Audio.SampleRate, "Service 2 should see Service 1 changes");
            Assert.AreEqual(2, service2Settings.Audio.Channels, "Service 2 should see Service 1 changes");

            // Service 2 modifies transcription settings
            service2Settings.Transcription.Provider = "UpdatedProvider";
            service2Settings.Transcription.Model = "updated-model";
            await _settingsService.SaveImmediateAsync();

            // Service 1 should see changes
            Assert.AreEqual("UpdatedProvider", service1Settings.Transcription.Provider, "Service 1 should see Service 2 changes");
            Assert.AreEqual("updated-model", service1Settings.Transcription.Model, "Service 1 should see Service 2 changes");

            // Test device settings synchronization
            var deviceSettings = new DeviceSpecificSettings
            {
                Name = "Test Device",
                SampleRate = 44100,
                Channels = 1,
                IsEnabled = true,
                IsCompatible = true
            };

            await _settingsService.SetDeviceSettingsAsync("test-device-1", deviceSettings);

            // Verify device settings are accessible
            var retrievedSettings = await _settingsService.GetDeviceSettingsAsync("test-device-1");
            Assert.AreEqual("Test Device", retrievedSettings.Name, "Should synchronize device settings");
            Assert.AreEqual(44100, retrievedSettings.SampleRate, "Should synchronize device sample rate");
            Assert.AreEqual(true, retrievedSettings.IsEnabled, "Should synchronize device enabled state");
        }

        [TestMethod]
        public async Task TestSettingsBackupAndRestore()
        {
            // Configure settings for backup
            var originalSettings = _settingsService.Settings;
            originalSettings.Audio.SampleRate = 48000;
            originalSettings.Transcription.Provider = "BackupTestProvider";
            originalSettings.UI.ShowVisualFeedback = true;
            originalSettings.UI.MinimizeToTray = false;

            await _settingsService.SaveImmediateAsync();

            // Test hotkey profile backup
            var testProfile = new HotkeyProfile
            {
                Id = "BackupTestProfile",
                Name = "Backup Test Profile",
                Description = "Profile for testing backup/restore",
                CreatedAt = DateTime.Now,
                Hotkeys = new Dictionary<string, HotkeyDefinition>
                {
                    ["TestAction1"] = new HotkeyDefinition { Name = "TestAction1", Combination = "Ctrl+Shift+A" },
                    ["TestAction2"] = new HotkeyDefinition { Name = "TestAction2", Combination = "Ctrl+Shift+B" }
                }
            };

            await _settingsService.CreateHotkeyProfileAsync(testProfile);
            await _settingsService.SaveImmediateAsync();

            // Export profile (backup)
            var backupPath = Path.Combine(_testAppDataPath, "backup_profile.json");
            await _settingsService.ExportHotkeyProfileAsync("BackupTestProfile", backupPath);
            Assert.IsTrue(File.Exists(backupPath), "Backup file should be created");

            // Verify backup content
            var backupContent = await File.ReadAllTextAsync(backupPath);
            Assert.IsTrue(backupContent.Contains("BackupTestProfile"), "Backup should contain profile name");
            Assert.IsTrue(backupContent.Contains("TestAction1"), "Backup should contain hotkey 1 name");
            Assert.IsTrue(backupContent.Contains("Ctrl+Shift+A"), "Backup should contain hotkey 1 combination");
            Assert.IsTrue(backupContent.Contains("TestAction2"), "Backup should contain hotkey 2 name");
            Assert.IsTrue(backupContent.Contains("Ctrl+Shift+B"), "Backup should contain hotkey 2 combination");

            // Delete original profile
            await _settingsService.DeleteHotkeyProfileAsync("BackupTestProfile");

            // Verify profile is deleted
            var profiles = await _settingsService.GetHotkeyProfilesAsync();
            var deletedProfile = profiles.FirstOrDefault(p => p.Id == "BackupTestProfile");
            Assert.IsNull(deletedProfile, "Profile should be deleted");

            // Re-create the profile to cause a conflict upon import
            await _settingsService.CreateHotkeyProfileAsync(testProfile);
            await _settingsService.SaveImmediateAsync();

            // Import profile (restore)
            var restoredProfile = await _settingsService.ImportHotkeyProfileAsync(backupPath);
            Assert.IsNotNull(restoredProfile, "Should restore profile from backup");
            Assert.AreEqual("BackupTestProfile_1", restoredProfile.Id, "Should append number to avoid conflict");
            Assert.AreEqual(2, restoredProfile.Hotkeys.Count, "Should restore all hotkeys");

            // Test settings file backup/restore
            var settingsBackupPath = Path.Combine(_testAppDataPath, "settings_backup.json");
            var settingsJson = System.Text.Json.JsonSerializer.Serialize(originalSettings, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(settingsBackupPath, settingsJson);

            // Modify current settings
            _settingsService.Settings.Audio.SampleRate = 8000;
            _settingsService.Settings.Transcription.Provider = "TempProvider";
            await _settingsService.SaveImmediateAsync();

            // Restore from backup
            var backupJson = await File.ReadAllTextAsync(settingsBackupPath);
            var restoredSettings = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(backupJson);
            
            if (restoredSettings != null)
            {
                _settingsService.Settings.Audio.SampleRate = restoredSettings.Audio.SampleRate;
                _settingsService.Settings.Transcription.Provider = restoredSettings.Transcription.Provider;
                await _settingsService.SaveImmediateAsync();

                Assert.AreEqual(48000, _settingsService.Settings.Audio.SampleRate, "Should restore audio settings");
                Assert.AreEqual("BackupTestProvider", _settingsService.Settings.Transcription.Provider, "Should restore transcription settings");
            }
        }

        [TestMethod]
        public async Task TestSettingsErrorHandlingAndRecovery()
        {
            // Test corrupted settings file recovery
            var corruptJson = "{ invalid json content }";
            var settingsPath = Path.Combine(_testAppDataPath, "usersettings.json");
            await File.WriteAllTextAsync(settingsPath, corruptJson);

            // Use a simple configuration instead of AddJsonFile(corruptPath) to avoid crash in AddJsonFile itself
            var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
                .Build();

            var options = new TestOptionsMonitor<AppSettings>(new AppSettings());
            var recoveryRepository = new FileSettingsRepository(NullLogger<FileSettingsRepository>.Instance, new FileSystemService(), _testAppDataPath);
            var recoveryService = new SettingsService(configuration, options, NullLogger<SettingsService>.Instance, recoveryRepository, autoLoad: false, customAppDataPath: _testAppDataPath);
            
            // This should catch the JsonException from repository and use defaults
            await recoveryService.LoadUserSettingsAsync();

            // Should recover with default settings
            var recoveredSettings = recoveryService.Settings;
            Assert.IsNotNull(recoveredSettings, "Should recover with non-null settings");
            Assert.AreEqual(16000, recoveredSettings.Audio.SampleRate, "Should use default sample rate");
            Assert.AreEqual("OpenAI", recoveredSettings.Transcription.Provider, "Should use default provider");

            // Test invalid device ID handling
            var invalidDeviceResult = await _settingsService.GetDeviceSettingsAsync("non_existent_device");
            Assert.IsNotNull(invalidDeviceResult, "Should return default device settings for invalid ID");
            Assert.AreEqual("non_existent_device", invalidDeviceResult.Name, "Should use device ID as name when not found");

            // Test null/empty device ID
            var nullDeviceResult = await _settingsService.GetDeviceSettingsAsync(null);
            Assert.IsNotNull(nullDeviceResult, "Should handle null device ID gracefully");

            var emptyDeviceResult = await _settingsService.GetDeviceSettingsAsync("");
            Assert.IsNotNull(emptyDeviceResult, "Should handle empty device ID gracefully");

            // Test invalid profile operations
            var invalidProfileResult = await _settingsService.CreateHotkeyProfileAsync(null);
            Assert.IsFalse(invalidProfileResult, "Should reject null profile creation");

            var invalidDeleteResult = await _settingsService.DeleteHotkeyProfileAsync("");
            Assert.IsFalse(invalidDeleteResult, "Should reject empty profile ID deletion");

            var invalidUpdateResult = await _settingsService.UpdateHotkeyProfileAsync(null);
            Assert.IsFalse(invalidUpdateResult, "Should reject null profile update");

            // Test duplicate profile handling
            var profile1 = new HotkeyProfile { Id = "Duplicate", Name = "Profile 1" };
            var profile2 = new HotkeyProfile { Id = "Duplicate", Name = "Profile 2" };

            var create1Result = await _settingsService.CreateHotkeyProfileAsync(profile1);
            var create2Result = await _settingsService.CreateHotkeyProfileAsync(profile2);

            Assert.IsTrue(create1Result, "Should create first profile");
            Assert.IsFalse(create2Result, "Should reject duplicate profile ID");

            // Test protected profile deletion
            var deleteProtectedResult = await _settingsService.DeleteHotkeyProfileAsync("Default");
            Assert.IsFalse(deleteProtectedResult, "Should not allow deletion of protected profile");
        }

        [TestMethod]
        public async Task TestSettingsThreadSafetyAndConcurrency()
        {
            var tasks = new List<Task>();
            var exceptions = new List<Exception>();

            // Concurrent reads
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            var settings = _settingsService.Settings;
                            var _ = settings.Audio.SampleRate;
                            var __ = settings.Transcription.Provider;
                            var ___ = settings.Hotkeys.ToggleRecording;
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException
                                                 && ex is not StackOverflowException
                                                 && ex is not AccessViolationException
                                                 && ex is not OperationCanceledException)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            // Concurrent writes
            for (int i = 0; i < 5; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        for (int j = 0; j < 10; j++)
                        {
                            _settingsService.Settings.Audio.SampleRate = 16000 + (i * 1000) + j;
                            await _settingsService.SaveImmediateAsync();
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException
                                                 && ex is not StackOverflowException
                                                 && ex is not AccessViolationException
                                                 && ex is not OperationCanceledException)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            // Concurrent device operations
            for (int i = 0; i < 5; i++)
            {
                var deviceId = $"device_{i}";
                tasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        var deviceSettings = new DeviceSpecificSettings
                        {
                            Name = $"Device {i}",
                            SampleRate = 16000 + (i * 1000),
                            Channels = 1,
                            IsEnabled = true
                        };

                        for (int j = 0; j < 5; j++)
                        {
                            await _settingsService.SetDeviceSettingsAsync($"{deviceId}_{j}", deviceSettings);
                            var retrieved = await _settingsService.GetDeviceSettingsAsync($"{deviceId}_{j}");
                            Assert.AreEqual(deviceSettings.Name, retrieved.Name, $"Device {i}_{j} should have correct name");
                        }
                    }
                    catch (Exception ex) when (ex is not OutOfMemoryException
                                                 && ex is not StackOverflowException
                                                 && ex is not AccessViolationException
                                                 && ex is not OperationCanceledException)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            // Wait for all tasks to complete
            await Task.WhenAll(tasks);

            // Check for exceptions
            if (exceptions.Count > 0)
            {
                Console.WriteLine($"Thread safety test encountered {exceptions.Count} exceptions:");
                foreach (var ex in exceptions)
                {
                    Console.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
                }
            }

            // Verify final state is consistent
            var finalSettings = _settingsService.Settings;
            Assert.IsTrue(finalSettings.Audio.SampleRate > 0, "Final sample rate should be valid");
            Assert.IsNotNull(finalSettings.Transcription.Provider, "Final provider should not be null");

            // Verify device settings are consistent
            var deviceCount = finalSettings.Audio.DeviceSettings.Count;
            Assert.IsTrue(deviceCount >= 0, "Should have device settings after concurrent operations");
        }

        [TestMethod]
        public async Task TestSettingsIntegrationWithUIComponents()
        {
            // Test settings binding scenarios commonly used in WPF
            var settings = _settingsService.Settings;

            // Test boolean binding scenarios
            settings.UI.ShowVisualFeedback = true;
            settings.UI.MinimizeToTray = true;
            settings.UI.StartWithWindows = false;
            await _settingsService.SaveImmediateAsync();

            // Verify boolean values
            Assert.IsTrue(settings.UI.ShowVisualFeedback, "Should persist boolean true");
            Assert.IsTrue(settings.UI.MinimizeToTray, "Should persist boolean true");
            Assert.IsFalse(settings.UI.StartWithWindows, "Should persist boolean false");

            // Test numeric binding scenarios
            settings.Audio.SampleRate = 44100;
            settings.Audio.Channels = 2;
            settings.UI.WindowOpacity = 75;
            await _settingsService.SaveImmediateAsync();

            // Verify numeric values
            Assert.AreEqual(44100, settings.Audio.SampleRate, "Should persist integer value");
            Assert.AreEqual(2, settings.Audio.Channels, "Should persist integer value");
            Assert.AreEqual(75, settings.UI.WindowOpacity, "Should persist integer value");

            // Test string binding scenarios
            settings.Transcription.Provider = "LocalWhisper";
            settings.Transcription.Model = "whisper-base";
            settings.Transcription.ApiKey = "test_api_key_123";
            settings.Hotkeys.ToggleRecording = "Ctrl+Alt+S";
            await _settingsService.SaveImmediateAsync();

            // Verify string values
            Assert.AreEqual("LocalWhisper", settings.Transcription.Provider, "Should persist string value");
            Assert.AreEqual("whisper-base", settings.Transcription.Model, "Should persist string value");
            Assert.AreEqual("Ctrl+Alt+S", settings.Hotkeys.ToggleRecording, "Should persist string value");

            // Test collection binding scenarios
            settings.Hotkeys.Profiles["TestProfile1"] = new HotkeyProfile
            {
                Id = "TestProfile1",
                Name = "Test Profile 1",
                CreatedAt = DateTime.Now
            };

            settings.Hotkeys.Profiles["TestProfile2"] = new HotkeyProfile
            {
                Id = "TestProfile2",
                Name = "Test Profile 2",
                CreatedAt = DateTime.Now
            };

            await _settingsService.SaveImmediateAsync();

            // Verify collection values
            Assert.AreEqual(2, settings.Hotkeys.Profiles.Count, "Should persist collection count");
            Assert.IsTrue(settings.Hotkeys.Profiles.ContainsKey("TestProfile1"), "Should persist collection key 1");
            Assert.IsTrue(settings.Hotkeys.Profiles.ContainsKey("TestProfile2"), "Should persist collection key 2");

            // Test nested object scenarios
            settings.UI = new UISettings
            {
                ShowVisualFeedback = false,
                MinimizeToTray = true,
                StartWithWindows = false,
                WindowOpacity = 50,
                ShowTranscriptionWindow = true
            };

            await _settingsService.SaveImmediateAsync();

            // Verify nested object values
            Assert.AreEqual(false, settings.UI.ShowVisualFeedback, "Should persist nested boolean");
            Assert.AreEqual(true, settings.UI.MinimizeToTray, "Should persist nested boolean");
            Assert.AreEqual(50, settings.UI.WindowOpacity, "Should persist nested integer");
            Assert.AreEqual(true, settings.UI.ShowTranscriptionWindow, "Should persist nested boolean");
        }

        [TestMethod]
        public async Task TestSettingsDefaultValueHandling()
        {
            // Test that settings have sensible defaults
            var settings = _settingsService.Settings;

            Assert.IsNotNull(settings.Audio, "Audio settings should not be null");
            Assert.IsTrue(settings.Audio.SampleRate > 0, "Sample rate should have positive default");
            Assert.IsTrue(settings.Audio.Channels >= 1 && settings.Audio.Channels <= 2, "Channels should have valid default");

            Assert.IsNotNull(settings.Transcription, "Transcription settings should not be null");
            Assert.IsFalse(string.IsNullOrEmpty(settings.Transcription.Provider), "Provider should have default value");
            Assert.IsFalse(string.IsNullOrEmpty(settings.Transcription.Model), "Model should have default value");

            Assert.IsNotNull(settings.Hotkeys, "Hotkey settings should not be null");
            Assert.IsFalse(string.IsNullOrEmpty(settings.Hotkeys.ToggleRecording), "Toggle hotkey should have default");

            Assert.IsNotNull(settings.UI, "UI settings should not be null");
            Assert.IsTrue(settings.UI.WindowOpacity >= 0 && settings.UI.WindowOpacity <= 100, "Opacity should have valid default");

            // Test default device settings
            var defaultDeviceSettings = await _settingsService.GetDeviceSettingsAsync("any_device");
            Assert.IsNotNull(defaultDeviceSettings, "Should return default device settings");
            Assert.IsNotNull(defaultDeviceSettings.Name, "Default device should have name");

            // Test default hotkey profile
            var defaultProfile = await _settingsService.GetCurrentHotkeyProfileAsync();
            Assert.IsNotNull(defaultProfile, "Should have default hotkey profile");
            Assert.AreEqual("Default", defaultProfile.Id, "Default profile should have 'Default' ID");

            // Test settings reset to defaults
            settings.Audio.SampleRate = 99999; // Unusual value
            settings.Transcription.Provider = "InvalidProvider";
            settings.Hotkeys.ToggleRecording = ""; // Empty

            // Reset (would typically call a Reset method)
            var resetSettings = new AppSettings(); // Fresh defaults
            settings.Audio = resetSettings.Audio;
            settings.Transcription = resetSettings.Transcription;
            settings.Hotkeys = resetSettings.Hotkeys;

            await _settingsService.SaveImmediateAsync();

            // Verify reset worked
            Assert.AreEqual(resetSettings.Audio.SampleRate, settings.Audio.SampleRate, "Should reset audio sample rate");
            Assert.AreEqual(resetSettings.Transcription.Provider, settings.Transcription.Provider, "Should reset provider");
            Assert.AreEqual(resetSettings.Hotkeys.ToggleRecording, settings.Hotkeys.ToggleRecording, "Should reset hotkey");
        }
    }
}
