using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using ScottWisper.Configuration;

namespace ScottWisper.Services
{
    public class SettingsChangedEventArgs : EventArgs
    {
        public string Key { get; set; } = string.Empty;
        public object? OldValue { get; set; }
        public object? NewValue { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool RequiresRestart { get; set; }
    }

    public class SettingsBackup
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public string Description { get; set; } = string.Empty;
        public AppSettings Settings { get; set; } = new AppSettings();
        public string Version { get; set; } = "1.0";
        public string Application { get; set; } = "ScottWisper";
    }

    public class SettingsValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Info { get; set; } = new List<string>();
    }

    public interface ISettingsService
    {
        AppSettings Settings { get; }
        event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
        Task SaveAsync();
        Task<T> GetValueAsync<T>(string key);
        Task SetValueAsync<T>(string key, T value);
        Task<string> GetEncryptedValueAsync(string key);
        Task SetEncryptedValueAsync(string key, string value);
        
        // Audio device management methods
        Task SetSelectedInputDeviceAsync(string deviceId);
        Task SetSelectedOutputDeviceAsync(string deviceId);
        Task SetFallbackInputDeviceAsync(string deviceId);
        Task SetFallbackOutputDeviceAsync(string deviceId);
        Task<DeviceSpecificSettings> GetDeviceSettingsAsync(string deviceId);
        Task SetDeviceSettingsAsync(string deviceId, DeviceSpecificSettings settings);
Task AddDeviceTestResultAsync(Configuration.DeviceTestingResult result);
        Task<List<Configuration.DeviceTestingResult>> GetDeviceTestHistoryAsync(string deviceId);
        
        // Enhanced device testing methods
        Task AddAudioDeviceTestResultAsync(AudioDeviceTestResult result);
        Task<List<Configuration.AudioDeviceTestResult>> GetAudioDeviceTestHistoryAsync(string deviceId);
        Task SaveAudioQualityMetricsAsync(AudioQualityMetrics metrics);
        Task<List<AudioQualityMetrics>> GetAudioQualityHistoryAsync(string deviceId);
        Task SaveDeviceCompatibilityScoreAsync(Configuration.DeviceCompatibilityScore score);
Task<List<Configuration.DeviceRecommendation>> GetDeviceRecommendationsAsync();
        Task SetRealTimeMonitoringEnabledAsync(string deviceId, bool enabled);
        Task RefreshDeviceListAsync();
        Task<bool> IsDeviceEnabledAsync(string deviceId);
        Task SetDeviceEnabledAsync(string deviceId, bool enabled);
        Task<string> GetRecommendedDeviceAsync(DeviceType type);
        Task SetPreferredDeviceAsync(string deviceId, DeviceType deviceType);
        
        // Hotkey management methods
        Task<bool> CreateHotkeyProfileAsync(HotkeyProfile profile);
        Task<bool> UpdateHotkeyProfileAsync(HotkeyProfile profile);
        Task<bool> DeleteHotkeyProfileAsync(string profileId);
        Task<bool> SwitchHotkeyProfileAsync(string profileId);
        Task<List<HotkeyProfile>> GetHotkeyProfilesAsync();
        Task<HotkeyProfile> GetCurrentHotkeyProfileAsync();
        Task ExportHotkeyProfileAsync(string profileId, string filePath);
        Task<HotkeyProfile> ImportHotkeyProfileAsync(string filePath);
        Task<HotkeyValidationResult> ValidateHotkeyAsync(string combination);
    }

    public class SettingsService : ISettingsService
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<AppSettings> _options;
        private readonly string _userSettingsPath;
        private readonly string _encryptionKey;
        private AppSettings _currentSettings;

        public AppSettings Settings => _currentSettings;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        public SettingsService(IConfiguration configuration, IOptionsMonitor<AppSettings> options)
        {
            _configuration = configuration;
            _options = options;
            _currentSettings = options.CurrentValue;
            
            // Initialize user settings path in %APPDATA%
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScottWisper");
            Directory.CreateDirectory(appFolder);
            _userSettingsPath = Path.Combine(appFolder, "usersettings.json");
            
            // Initialize encryption key based on machine info
            _encryptionKey = GenerateMachineSpecificKey();
            
            // Load user-specific settings
            _ = LoadUserSettingsAsync();
        }

        public async Task SaveAsync()
        {
            try
            {
                // Validate settings before saving
                ValidateSettings(_currentSettings);
                
                var json = JsonSerializer.Serialize(_currentSettings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_userSettingsPath, json);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
            }
        }

        public async Task<T> GetValueAsync<T>(string key)
        {
            var value = _configuration[key];
            if (value != null)
            {
                try
                {
                    return JsonSerializer.Deserialize<T>(value);
                }
                catch
                {
                    return default(T)!;
                }
            }
            return default(T)!;
        }

        public async Task SetValueAsync<T>(string key, T value)
        {
            var oldValue = await GetValueAsync<T>(key);
            
            // For now, this will update the in-memory settings
            // In a full implementation, you'd want to update specific properties
            var json = JsonSerializer.Serialize(value);
            _configuration[key] = json;
            
            // Fire SettingsChanged event
            SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
            {
                Key = key,
                OldValue = oldValue,
                NewValue = value,
                Category = "General",
                RequiresRestart = false
            });
        }

        public async Task<string> GetEncryptedValueAsync(string key)
        {
            try
            {
                var encryptedData = await File.ReadAllTextAsync(GetEncryptedFilePath(key));
                return DecryptString(encryptedData);
            }
            catch (FileNotFoundException)
            {
                return string.Empty;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public async Task SetEncryptedValueAsync(string key, string value)
        {
            try
            {
                var encryptedData = EncryptString(value);
                var filePath = GetEncryptedFilePath(key);
                await File.WriteAllTextAsync(filePath, encryptedData);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to save encrypted value for {key}: {ex.Message}", ex);
            }
        }

        private async Task LoadUserSettingsAsync()
        {
            try
            {
                if (File.Exists(_userSettingsPath))
                {
                    var json = await File.ReadAllTextAsync(_userSettingsPath);
                    var userSettings = JsonSerializer.Deserialize<AppSettings>(json);
                    
                    if (userSettings != null)
                    {
                        // Merge user settings with default settings
                        MergeSettings(_currentSettings, userSettings);
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but continue with default settings
                System.Diagnostics.Debug.WriteLine($"Failed to load user settings: {ex.Message}");
            }
        }

        private void MergeSettings(AppSettings target, AppSettings source)
        {
            if (source.Audio != null)
            {
                target.Audio = source.Audio;
            }
            
            if (source.Transcription != null)
            {
                target.Transcription = source.Transcription;
            }
            
            if (source.Hotkeys != null)
            {
                target.Hotkeys = source.Hotkeys;
            }
            
            if (source.UI != null)
            {
                target.UI = source.UI;
            }
            
            if (source.Hotkeys?.Profiles != null)
            {
                // Merge profiles, with source taking precedence
                foreach (var profile in source.Hotkeys.Profiles)
                {
                    target.Hotkeys.Profiles[profile.Key] = profile.Value;
                }
            }
            
            if (source.Hotkeys?.CustomHotkeys != null)
            {
                foreach (var hotkey in source.Hotkeys.CustomHotkeys)
                {
                    target.Hotkeys.CustomHotkeys[hotkey.Key] = hotkey.Value;
                }
            }
        }

        public async Task SetSelectedInputDeviceAsync(string deviceId)
        {
            _currentSettings.Audio.InputDeviceId = deviceId ?? "default";
            await SaveAsync();
        }

        public async Task SetSelectedOutputDeviceAsync(string deviceId)
        {
            _currentSettings.Audio.OutputDeviceId = deviceId ?? "default";
            await SaveAsync();
        }

        public async Task SetFallbackInputDeviceAsync(string deviceId)
        {
            _currentSettings.Audio.FallbackInputDeviceId = deviceId ?? "default";
            await SaveAsync();
        }

public async Task SetFallbackOutputDeviceAsync(string deviceId)
        {
            _currentSettings.Audio.FallbackOutputDeviceId = deviceId ?? "default";
            await SaveAsync();
        }

        public async Task SetPreferredDeviceAsync(string deviceId, DeviceType deviceType)
        {
            switch (deviceType)
            {
                case DeviceType.Input:
                    _currentSettings.Audio.SelectedInputDeviceId = deviceId ?? "default";
                    break;
                case DeviceType.Output:
                    _currentSettings.Audio.SelectedOutputDeviceId = deviceId ?? "default";
                    break;
            }
            await SaveAsync();
        }

        public async Task<DeviceSpecificSettings> GetDeviceSettingsAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return new DeviceSpecificSettings();

            return _currentSettings.Audio.DeviceSettings.TryGetValue(deviceId, out var settings)
                ? settings
                : new DeviceSpecificSettings { Name = deviceId };
        }

        public async Task SetDeviceSettingsAsync(string deviceId, DeviceSpecificSettings settings)
        {
            if (string.IsNullOrEmpty(deviceId) || settings == null)
                return;

            _currentSettings.Audio.DeviceSettings[deviceId] = settings;
            await SaveAsync();
        }

        public async Task AddDeviceTestResultAsync(Configuration.DeviceTestingResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.DeviceId))
                return;

            // Add to test history
            _currentSettings.DeviceTestHistory.Insert(0, result);

            // Limit history size
            if (_currentSettings.DeviceTestHistory.Count > _currentSettings.MaxTestHistory)
            {
                _currentSettings.DeviceTestHistory = _currentSettings.DeviceTestHistory
                    .Take(_currentSettings.MaxTestHistory)
                    .ToList();
            }

            // Update device settings based on test result
            var deviceSettings = await GetDeviceSettingsAsync(result.DeviceId);
            deviceSettings.LastTested = result.TestTime;
            deviceSettings.LastTestPassed = result.TestPassed;
            deviceSettings.IsCompatible = result.TestPassed;
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                deviceSettings.Notes = result.ErrorMessage;
            }

            await SetDeviceSettingsAsync(result.DeviceId, deviceSettings);
            await SaveAsync();
        }

        public async Task AddAudioDeviceTestResultAsync(AudioDeviceTestResult result)
        {
            if (result == null || string.IsNullOrEmpty(result.DeviceId))
                return;

// Convert to legacy format for backward compatibility
            var legacyResult = new Configuration.DeviceTestingResult
            {
                DeviceId = result.DeviceId,
                DeviceName = result.DeviceName,
                TestTime = DateTime.Now, // Use current time since result.TestTime is TimeSpan
                TestPassed = result.Success,
                ErrorMessage = result.ErrorMessage,
                TestMetrics = new Dictionary<string, object> { ["Notes"] = string.IsNullOrEmpty(result.SupportedFormats) ? "No supported formats" : result.SupportedFormats }
            };

            await AddDeviceTestResultAsync(legacyResult);
        }

        public async Task<List<Configuration.DeviceTestingResult>> GetDeviceTestHistoryAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return new List<Configuration.DeviceTestingResult>();

            await Task.CompletedTask; // Make method async
            return _currentSettings.DeviceTestHistory
                .Where(r => r.DeviceId == deviceId)
                .ToList();
        }

        public async Task<List<Configuration.AudioDeviceTestResult>> GetAudioDeviceTestHistoryAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return new List<Configuration.AudioDeviceTestResult>();

            await Task.CompletedTask; // Make method async
            return _currentSettings.AudioDeviceTestHistory
                .Where(r => r.DeviceId == deviceId)
                .ToList();
        }

        public async Task SaveAudioQualityMetricsAsync(AudioQualityMetrics metrics)
        {
            if (metrics == null || string.IsNullOrEmpty(metrics.DeviceId))
                return;

            _currentSettings.AudioQualityHistory.Insert(0, metrics);

            // Limit history size
            if (_currentSettings.AudioQualityHistory.Count > _currentSettings.MaxQualityHistory)
            {
                _currentSettings.AudioQualityHistory = _currentSettings.AudioQualityHistory
                    .Take(_currentSettings.MaxQualityHistory)
                    .ToList();
            }

            await SaveAsync();
        }

        public async Task<List<AudioQualityMetrics>> GetAudioQualityHistoryAsync(string deviceId)
        {
            if (string.IsNullOrEmpty(deviceId))
                return new List<AudioQualityMetrics>();

            await Task.CompletedTask; // Make method async
            return _currentSettings.AudioQualityHistory
                .Where(m => m.DeviceId == deviceId)
                .ToList();
        }

        public async Task SaveDeviceCompatibilityScoreAsync(Configuration.DeviceCompatibilityScore score)
        {
            if (score == null || string.IsNullOrEmpty(score.DeviceId))
                return;

            _currentSettings.DeviceCompatibilityScores[score.DeviceId] = score;

            // Limit history size
            if (_currentSettings.DeviceCompatibilityScores.Count > _currentSettings.MaxCompatibilityHistory)
            {
                var oldest = _currentSettings.DeviceCompatibilityScores.Keys.First();
                _currentSettings.DeviceCompatibilityScores.Remove(oldest);
            }

            await SaveAsync();
        }

        public async Task<List<Configuration.DeviceRecommendation>> GetDeviceRecommendationsAsync()
        {
            await Task.CompletedTask; // Make method async
            return _currentSettings.DeviceRecommendations;
        }

        public async Task SetRealTimeMonitoringEnabledAsync(string deviceId, bool enabled)
        {
            var deviceSettings = await GetDeviceSettingsAsync(deviceId);
            deviceSettings.RealTimeMonitoringEnabled = enabled;
            await SetDeviceSettingsAsync(deviceId, deviceSettings);
            await SaveAsync();
        }

        public async Task RefreshDeviceListAsync()
        {
            _currentSettings.LastDeviceRefresh = DateTime.Now;
            await SaveAsync();
        }

        public async Task<bool> IsDeviceEnabledAsync(string deviceId)
        {
            var settings = await GetDeviceSettingsAsync(deviceId);
            return settings.IsEnabled;
        }

        public async Task SetDeviceEnabledAsync(string deviceId, bool enabled)
        {
            var settings = await GetDeviceSettingsAsync(deviceId);
            settings.IsEnabled = enabled;
            await SetDeviceSettingsAsync(deviceId, settings);
        }

        public async Task<string> GetRecommendedDeviceAsync(DeviceType type)
        {
            // Implementation would use AudioDeviceService to get device list
            // For now, return based on preferences or fallback
            if (type == DeviceType.Input)
            {
                // Try preferred devices first, then fallback
                foreach (var preferredDevice in _currentSettings.Audio.PreferredDevices)
                {
                    var settings = await GetDeviceSettingsAsync(preferredDevice);
                    if (settings.IsEnabled && settings.IsCompatible)
                    {
                        return preferredDevice;
                    }
                }
                return _currentSettings.Audio.InputDeviceId;
            }
            else // Output
            {
                return _currentSettings.Audio.OutputDeviceId;
            }
        }

        private void ValidateSettings(AppSettings settings)
        {
            // Audio settings validation
            if (settings.Audio.SampleRate <= 0)
            {
                throw new ValidationException("Sample rate must be greater than 0");
            }
            
            if (settings.Audio.Channels < 1 || settings.Audio.Channels > 2)
            {
                throw new ValidationException("Channels must be 1 or 2");
            }

            // Transcription settings validation
            if (string.IsNullOrWhiteSpace(settings.Transcription.Provider))
            {
                throw new ValidationException("Transcription provider is required");
            }

            if (string.IsNullOrWhiteSpace(settings.Transcription.Model))
            {
                throw new ValidationException("Transcription model is required");
            }

            // Hotkey settings validation
            if (string.IsNullOrWhiteSpace(settings.Hotkeys.ToggleRecording))
            {
                throw new ValidationException("Toggle recording hotkey is required");
            }

            // Validate device settings
            foreach (var deviceSettings in settings.Audio.DeviceSettings.Values)
            {
                if (deviceSettings.SampleRate <= 0)
                {
                    throw new ValidationException($"Device {deviceSettings.Name} has invalid sample rate");
                }

                if (deviceSettings.Channels < 1 || deviceSettings.Channels > 2)
                {
                    throw new ValidationException($"Device {deviceSettings.Name} has invalid channel count");
                }

                if (deviceSettings.BufferSize <= 0)
                {
                    throw new ValidationException($"Device {deviceSettings.Name} has invalid buffer size");
                }
            }
        }

        private string GenerateMachineSpecificKey()
        {
            // Use a combination of machine name and user name for encryption
            var machineKey = $"{Environment.MachineName}_{Environment.UserName}";
            var keyBytes = Encoding.UTF8.GetBytes(machineKey);
            
            // Use SHA256 to create a consistent 32-byte key
            using var sha256 = SHA256.Create();
            return Convert.ToBase64String(sha256.ComputeHash(keyBytes));
        }

        private string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                using var aes = Aes.Create();
                var key = Convert.FromBase64String(_encryptionKey);
                aes.Key = key;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                using var msEncrypt = new MemoryStream();
                
                // Write IV to the beginning of the stream
                msEncrypt.Write(aes.IV, 0, aes.IV.Length);
                
                using var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write);
                using var swEncrypt = new StreamWriter(csEncrypt);
                swEncrypt.Write(plainText);
                
                return Convert.ToBase64String(msEncrypt.ToArray());
            }
            catch
            {
                return string.Empty;
            }
        }

        private string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                var fullCipher = Convert.FromBase64String(encryptedText);
                
                using var aes = Aes.Create();
                var key = Convert.FromBase64String(_encryptionKey);
                aes.Key = key;
                
                // Extract IV from the beginning of the cipher text
                var iv = new byte[aes.BlockSize / 8];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aes.IV = iv;
                
                // Extract the actual cipher text
                var cipherText = new byte[fullCipher.Length - iv.Length];
                Array.Copy(fullCipher, iv.Length, cipherText, 0, cipherText.Length);
                
                using var decryptor = aes.CreateDecryptor();
                using var msDecrypt = new MemoryStream(cipherText);
                using var csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read);
                using var srDecrypt = new StreamReader(csDecrypt);
                
                return srDecrypt.ReadToEnd();
            }
            catch
            {
                return string.Empty;
            }
        }

        public async Task<bool> CreateHotkeyProfileAsync(HotkeyProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                return false;

            if (_currentSettings.Hotkeys.Profiles.ContainsKey(profile.Id))
                return false;

            profile.CreatedAt = DateTime.Now;
            profile.ModifiedAt = DateTime.Now;
            
            _currentSettings.Hotkeys.Profiles[profile.Id] = profile;
            await SaveAsync();
            return true;
        }

        public async Task<bool> UpdateHotkeyProfileAsync(HotkeyProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                return false;

            if (!_currentSettings.Hotkeys.Profiles.ContainsKey(profile.Id))
                return false;

            profile.ModifiedAt = DateTime.Now;
            _currentSettings.Hotkeys.Profiles[profile.Id] = profile;
            await SaveAsync();
            return true;
        }

        public async Task<bool> DeleteHotkeyProfileAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || profileId == "Default")
                return false;

            if (!_currentSettings.Hotkeys.Profiles.ContainsKey(profileId))
                return false;

            _currentSettings.Hotkeys.Profiles.Remove(profileId);

            // If deleting current profile, switch to default
            if (profileId == _currentSettings.Hotkeys.CurrentProfile)
            {
                _currentSettings.Hotkeys.CurrentProfile = "Default";
            }

            await SaveAsync();
            return true;
        }

        public async Task<bool> SwitchHotkeyProfileAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            if (!_currentSettings.Hotkeys.Profiles.ContainsKey(profileId))
                return false;

            _currentSettings.Hotkeys.CurrentProfile = profileId;
            await SaveAsync();
            return true;
        }

        public async Task<List<HotkeyProfile>> GetHotkeyProfilesAsync()
        {
            await Task.CompletedTask; // Make method async
            return _currentSettings.Hotkeys.Profiles.Values.ToList();
        }

        public async Task<HotkeyProfile> GetCurrentHotkeyProfileAsync()
        {
            var profileId = _currentSettings.Hotkeys.CurrentProfile;
            if (_currentSettings.Hotkeys.Profiles.TryGetValue(profileId, out var profile))
            {
                return profile;
            }
            
            // Return default profile if current not found
            return _currentSettings.Hotkeys.Profiles.TryGetValue("Default", out var defaultProfile) 
                ? defaultProfile 
                : new HotkeyProfile { Id = "Default", Name = "Default" };
        }

        public async Task ExportHotkeyProfileAsync(string profileId, string filePath)
        {
            if (!_currentSettings.Hotkeys.Profiles.TryGetValue(profileId, out var profile))
                throw new ArgumentException($"Profile {profileId} not found");

            var exportData = new
            {
                Profile = profile,
                ExportedAt = DateTime.Now,
                Version = "1.0",
                Application = "ScottWisper",
                Settings = new
                {
                    _currentSettings.Hotkeys.ShowConflictWarnings,
                    _currentSettings.Hotkeys.EnableAccessibilityOptions,
                    _currentSettings.Hotkeys.EnableKeyboardLayoutAwareness
                }
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);

            // Store backup path
            _currentSettings.Hotkeys.BackupProfilePath = filePath;
            await SaveAsync();
        }

        public async Task<HotkeyProfile> ImportHotkeyProfileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Profile file not found: {filePath}");

            var json = await File.ReadAllTextAsync(filePath);
            var importData = JsonSerializer.Deserialize<JsonElement>(json);
            
            var profile = JsonSerializer.Deserialize<HotkeyProfile>(
                importData.GetProperty("Profile").GetRawText());

            if (profile == null)
                throw new InvalidOperationException("Invalid profile file format");

            // Ensure unique ID
            var originalId = profile.Id;
            var counter = 1;
            while (_currentSettings.Hotkeys.Profiles.ContainsKey(profile.Id))
            {
                profile.Id = $"{originalId}_{counter++}";
            }

            await CreateHotkeyProfileAsync(profile);
            return profile;
        }

        public async Task<HotkeyValidationResult> ValidateHotkeyAsync(string combination)
        {
            // This is a basic validation - the comprehensive validation is in HotkeyService
            var result = new HotkeyValidationResult { IsValid = true };
            
            if (string.IsNullOrWhiteSpace(combination))
            {
                result.IsValid = false;
                result.ErrorMessage = "Hotkey combination cannot be empty";
                return result;
            }

            // Check against existing hotkeys in current profile
            var currentProfile = await GetCurrentHotkeyProfileAsync();
            var conflictingHotkey = currentProfile.Hotkeys.Values
                .FirstOrDefault(h => string.Equals(h.Combination, combination, StringComparison.OrdinalIgnoreCase));

            if (conflictingHotkey != null)
            {
                result.IsValid = false;
                result.ErrorMessage = $"This hotkey is already used by: {conflictingHotkey.Name}";
                result.Conflicts.Add(new Configuration.HotkeyConflict
                {
                    ConflictingHotkey = conflictingHotkey.Combination,
                    ConflictingApplication = "ScottWisper",
                    ConflictType = "profile"
                });
            }

            return result;
        }

        private string GetEncryptedFilePath(string key)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "ScottWisper");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, $"{key}.encrypted");
        }
    }
}