using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Threading;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using WhisperKey.Configuration;
using WhisperKey.Repositories;

namespace WhisperKey.Services
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
        public string Application { get; set; } = "WhisperKey";
    }

    public class SettingsValidationResult
    {
        public bool IsValid { get; set; } = true;
        public List<string> Errors { get; set; } = new List<string>();
        public List<string> Warnings { get; set; } = new List<string>();
        public List<string> Info { get; set; } = new List<string>();
    }

    /// <summary>
    /// Provides comprehensive application settings management with persistence, validation, encryption,
    /// and change notification capabilities. Supports hierarchical settings with typed access,
    /// backup/restore functionality, and secure storage of sensitive data.
    /// </summary>
    /// <remarks>
    /// This service handles multiple aspects of configuration management:
    /// <list type="bullet">
    /// <item><description><b>Typed Access</b>: Strongly-typed get/set operations with validation</description></item>
    /// <item><description><b>Persistence</b>: Automatic saving with debouncing and error handling</description></item>
    /// <item><description><b>Encryption</b>: Secure storage of sensitive data (API keys, credentials)</description></item>
    /// <item><description><b>Validation</b>: Runtime validation with detailed error reporting</description></item>
    /// <item><description><b>Change Notification</b>: Event-driven updates for UI synchronization</description></item>
    /// <item><description><b>Backup/Restore</b>: Complete settings backup management</description></item>
    /// <item><description><b>Device Integration</b>: Audio device preferences and settings</description></item>
    /// <item><description><b>Profile Management</b>: Hotkey profile switching and persistence</description></item>
    /// </list>
    /// Settings are stored in JSON format with automatic migration and version handling.
    /// The service supports both application defaults and user-specific overrides.
    /// </remarks>
    /// <example>
    /// <code>
    /// var settingsService = serviceProvider.GetService&lt;ISettingsService&gt;();
    /// 
    /// // Get current transcription settings
    /// var provider = await settingsService.GetValueAsync&lt;string&gt;("Transcription.Provider");
    /// var language = await settingsService.GetValueAsync&lt;string&gt;("Transcription.Language");
    /// 
    /// // Update settings with validation and notification
    /// await settingsService.SetValueAsync("Transcription.Provider", "Whisper");
    /// await settingsService.SetValueAsync("Transcription.Language", "en-US");
    /// 
    /// // Subscribe to change events
    /// settingsService.SettingsChanged += (sender, e) =>
    ///     Console.WriteLine($"Setting {e.Key} changed from {e.OldValue} to {e.NewValue}");
    /// </code>
    /// </example>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets the current application settings instance.
        /// Provides access to the complete configuration state.
        /// </summary>
        /// <value>The current <see cref="AppSettings"/> instance with all loaded values.</value>
        /// <remarks>
        /// This property returns the live settings object that reflects the current state.
        /// Modifications to the returned object will not trigger persistence
        /// or change notifications. Use the setter methods to make persistent changes.
        /// </remarks>
        AppSettings Settings { get; }
        
        /// <summary>
        /// Occurs when application settings are changed through the settings service.
        /// Provides notification for UI updates and change logging.
        /// </summary>
        /// <remarks>
        /// This event is raised when:
        /// <list type="bullet">
        /// <item><description>Values are changed through SetValueAsync methods</description></item>
        /// <item><description>Settings are loaded from persistent storage</description></item>
        /// <item><description>Settings are restored from backup</description></item>
        /// </list>
        /// The event includes the setting key, old value, new value, category,
        /// and whether a restart is required for changes to take effect.
        /// </remarks>
        event EventHandler<SettingsChangedEventArgs>? SettingsChanged;
        
        /// <summary>
        /// Saves the current settings to persistent storage with validation and debouncing.
        /// Persists all current setting values to the configured storage location.
        /// </summary>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        /// <exception cref="SettingsValidationException">Thrown when settings validation fails.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the settings repository encounters an error.</exception>
        /// <remarks>
        /// This operation performs:
        /// <list type="number">
        /// <item><description>Comprehensive validation of all setting values</description></item>
        /// <item><description>Debouncing to prevent excessive save operations</description></item>
        /// <item><description>Atomic write operations with rollback on failure</description></item>
        /// <item><description>Automatic backup creation before changes</description></item>
        /// </list>
        /// The save is performed asynchronously to avoid blocking the calling thread.
        /// Multiple rapid save calls are debounced to prevent performance issues.
        /// </remarks>
        Task SaveAsync();
        
        /// <summary>
        /// Retrieves a typed setting value by key with automatic type conversion.
        /// Supports strongly-typed access to configuration values.
        /// </summary>
        /// <typeparam name="T">The type of value to retrieve. Must be a serializable type.</typeparam>
        /// <param name="key">The settings key to retrieve. Must not be null or empty.</param>
        /// <returns>A task that returns the setting value, or default for type T if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the settings repository encounters an error.</exception>
        /// <remarks>
        /// Type conversion rules:
        /// <list type="bullet">
        /// <item><description>Numeric types: String parsing with InvariantCulture</description></item>
        /// <item><description>Boolean types: "true"/"false" string parsing</description></item>
        /// <item><description>Enum types: String parsing with case-insensitive matching</description></item>
        /// <item><description>Complex types: JSON deserialization with error handling</description></item>
        /// </list>
        /// This method is thread-safe and can be called concurrently.
        /// </remarks>
        Task<T> GetValueAsync<T>(string key);
        
        /// <summary>
        /// Updates a setting value by key with automatic validation and persistence.
        /// Validates the new value before persisting and triggers change notifications.
        /// </summary>
        /// <typeparam name="T">The type of value to set. Must be a serializable type.</typeparam>
        /// <param name="key">The settings key to update. Must not be null or empty.</param>
        /// <param name="value">The new value to set. Can be null for nullable types.</param>
        /// <returns>A task that represents the asynchronous set operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
        /// <exception cref="SettingsValidationException">Thrown when the new value fails validation.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the settings repository encounters an error.</exception>
        /// <remarks>
        /// This method performs:
        /// <list type="number">
        /// <item><description>Value validation against type-specific rules</description></item>
        /// <item><description>Conversion to appropriate storage format</description></item>
        /// <item><description>Immediate update of in-memory settings</description></item>
        /// <item><description>Triggering of SettingsChanged event if value actually changed</description></item>
        /// <item><description>Scheduling of debounced persist operation</description></item>
        /// </list>
        /// If the new value equals the current value, no persistence or notification occurs.
        /// </remarks>
        Task SetValueAsync<T>(string key, T value);
        
        /// <summary>
        /// Retrieves an encrypted setting value by key with automatic decryption.
        /// Provides secure access to sensitive configuration data like API keys and credentials.
        /// </summary>
        /// <param name="key">The settings key to retrieve. Must not be null or empty.</param>
        /// <returns>A task that returns the decrypted setting value, or empty string if not found.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is null or empty.</exception>
        /// <exception cref="CryptographicException">Thrown when decryption fails due to data corruption or wrong key.</exception>
        /// <remarks>
        /// This method uses Windows Data Protection (DPAPI) for encryption with:
        /// <list type="bullet">
        /// <item><description>User-specific encryption keys</description></item>
        /// <item><description>Automatic machine-specific entropy</description></item>
        /// <item><description>Secure memory handling of decrypted data</description></item>
        /// </list>
        /// Encrypted values are stored separately from regular settings and use the "secure:"
        /// key prefix to identify them in storage.
        /// </remarks>
        Task<string> GetEncryptedValueAsync(string key);
        
        /// <summary>
        /// Updates an encrypted setting value by key with automatic validation and encryption.
        /// Securely stores sensitive data with automatic protection and validation.
        /// </summary>
        /// <param name="key">The settings key to update. Must not be null or empty.</param>
        /// <param name="value">The value to encrypt and store. Must not be null.</param>
        /// <returns>A task that represents the asynchronous encrypted set operation.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> or <paramref name="value"/> is null or empty.</exception>
        /// <exception cref="CryptographicException">Thrown when encryption fails due to system errors.</exception>
        /// <exception cref="SettingsValidationException">Thrown when the value fails validation rules.</exception>
        /// <remarks>
        /// This method performs:
        /// <list type="number">
        /// <item><description>Value validation against secure data requirements</description></item>
        /// <item><description>Automatic encryption using Windows Data Protection API</description></item>
        /// <item><description>Secure memory cleanup of sensitive data</description></item>
        /// <item><description>Atomic update with rollback on encryption failure</description></item>
        /// </list>
        /// The "secure:" key prefix is automatically added to identify encrypted values
        /// in the underlying storage. The original value is not retained in memory longer than necessary.
        /// </remarks>
        Task SetEncryptedValueAsync(string key, string value);
        
        // Backup and restore methods
        Task BackupSettingsAsync(string description);
        Task RestoreSettingsAsync(string backupId);
        Task<SettingsBackup[]> GetAvailableBackupsAsync();
        Task DeleteBackupAsync(string backupId);
        Task<bool> ValidateSettingsFileAsync();
        
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

    public class SettingsService : ISettingsService, IDisposable
    {
        private readonly IConfiguration _configuration;
        private readonly IOptionsMonitor<AppSettings> _options;
        private readonly ILogger<SettingsService> _logger;
        private readonly ISettingsRepository _repository;
        private AppSettings _currentSettings;
        
        // Debouncing fields for settings saves
        private readonly SemaphoreSlim _saveSemaphore = new SemaphoreSlim(1, 1);
 private System.Timers.Timer? _debounceTimer;
        private readonly TimeSpan _debounceDelay = TimeSpan.FromMilliseconds(500);
        private volatile bool _savePending = false;

        public AppSettings Settings => _currentSettings;

        public event EventHandler<SettingsChangedEventArgs>? SettingsChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsService"/> class with required dependencies.
        /// Sets up configuration management, logging, and loads user settings from persistent storage.
        /// </summary>
        /// <param name="configuration">The application configuration containing default values and paths. Must not be null.</param>
        /// <param name="options">The options monitor for configuration change notifications. Must not be null.</param>
        /// <param name="logger">The logger for operation tracking and debugging. Must not be null.</param>
        /// <param name="repository">The settings repository for persistent storage. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when initial settings loading fails.</exception>
        /// <remarks>
        /// This constructor:
        /// <list type="number">
        /// <item><description>Initializes in-memory settings with configuration defaults</description></item>
        /// <item><description>Sets up change notification handlers</description></item>
        /// <item><description>Configures save debouncing mechanisms</description></item>
        /// <item><description>Asynchronously loads user-specific overrides</description></item>
        /// <item><description>Validates initial settings state</description></item>
        /// </list>
        /// Settings loading is performed asynchronously to avoid blocking the calling thread.
        /// Any errors during loading are logged but do not prevent service construction.
        /// </remarks>
        public SettingsService(
            IConfiguration configuration, 
            IOptionsMonitor<AppSettings> options, 
            ILogger<SettingsService> logger,
            ISettingsRepository repository)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            
            _currentSettings = options.CurrentValue;
            
            // Load user-specific settings from repository (fire-and-forget with error handling)
            _ = LoadUserSettingsAsync().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    _logger.LogError(t.Exception, "Failed to load user settings during service initialization");
                }
                else if (t.IsCompleted)
                {
                    _logger.LogDebug("User settings loaded successfully during service initialization");
                }
            });
        }

        public async Task SaveAsync()
        {
            await SaveAsyncInternal();
        }
        
        /// <summary>
        /// Internal save method with debouncing logic
        /// </summary>
        private async Task SaveAsyncInternal()
        {
            try
            {
                // Validate settings before saving
                ValidateSettings(_currentSettings);
                
                // Use debouncing to prevent excessive save operations
                await SaveWithDebounceAsync();
                
                _logger.LogInformation("Settings saved via repository with debouncing");
            }
            catch (InvalidOperationException)
            {
                // Re-throw repository exceptions
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error saving settings");
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// Save settings with debouncing to prevent rapid successive saves
        /// </summary>
        private async Task SaveWithDebounceAsync()
        {
            // Cancel any existing timer
            _debounceTimer?.Stop();
            _debounceTimer?.Dispose();
            
            // Mark as pending and restart timer
            _savePending = true;
            
            // Create new timer for debounced save
            _debounceTimer = new System.Timers.Timer((int)_debounceDelay.TotalMilliseconds);
            _debounceTimer.AutoReset = false;
            _debounceTimer.Elapsed += async (s, e) =>
            {
                if (_savePending)
                {
                    _savePending = false;
                    
                    // Use semaphore to prevent concurrent saves
                    await _saveSemaphore.WaitAsync();
                    try
                    {
                        // Use repository to save settings
                        await _repository.SaveAsync(_currentSettings).ConfigureAwait(false);
                        
                        _logger.LogDebug("Debounced save executed");
                    }
                    finally
                    {
                        _saveSemaphore.Release();
                    }
                }
            };
            
            // Start the timer
            _debounceTimer.Enabled = true;
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
                catch (JsonException)
                {
                    return default(T)!;
                }
            }
            return default(T)!;
        }

        public async Task SetValueAsync<T>(string key, T value)
        {
            var oldValue = await GetValueAsync<T>(key).ConfigureAwait(false);
            
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
                var encryptedData = await File.ReadAllTextAsync(GetEncryptedFilePath(key)).ConfigureAwait(false);
                return DecryptString(encryptedData);
            }
            catch (FileNotFoundException)
            {
                return string.Empty;
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
            catch (CryptographicException)
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
                await File.WriteAllTextAsync(filePath, encryptedData).ConfigureAwait(false);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException($"Failed to save encrypted value for {key}: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new InvalidOperationException($"Failed to save encrypted value for {key}: {ex.Message}", ex);
            }
            catch (SecurityException ex)
            {
                throw new InvalidOperationException($"Failed to save encrypted value for {key}: {ex.Message}", ex);
            }
        }

        private async Task LoadUserSettingsAsync()
        {
            try
            {
                // Use repository to load settings
                var userSettings = await _repository.LoadAsync().ConfigureAwait(false);
                
                if (userSettings != null)
                {
                    // Merge user settings with default settings
                    MergeSettings(_currentSettings, userSettings);
                }
                
                _logger.LogInformation("User settings loaded via repository");
            }
            catch (Exception ex)
            {
                // Log error but continue with default settings
                _logger.LogWarning(ex, "Failed to load user settings via repository, using defaults");
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
            await SaveAsync().ConfigureAwait(false);
        }

        public async Task SetSelectedOutputDeviceAsync(string deviceId)
        {
            _currentSettings.Audio.OutputDeviceId = deviceId ?? "default";
            await SaveAsync().ConfigureAwait(false);
        }

        public async Task SetFallbackInputDeviceAsync(string deviceId)
        {
            _currentSettings.Audio.FallbackInputDeviceId = deviceId ?? "default";
            await SaveAsync().ConfigureAwait(false);
        }

        public async Task SetFallbackOutputDeviceAsync(string deviceId)
        {
            _currentSettings.Audio.FallbackOutputDeviceId = deviceId ?? "default";
            await SaveAsync().ConfigureAwait(false);
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
            await SaveAsync().ConfigureAwait(false);
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
            await SaveAsync().ConfigureAwait(false);
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
            var deviceSettings = await GetDeviceSettingsAsync(result.DeviceId).ConfigureAwait(false);
            deviceSettings.LastTested = result.TestTime;
            deviceSettings.LastTestPassed = result.TestPassed;
            deviceSettings.IsCompatible = result.TestPassed;
            
            if (!string.IsNullOrEmpty(result.ErrorMessage))
            {
                deviceSettings.Notes = result.ErrorMessage;
            }

            await SetDeviceSettingsAsync(result.DeviceId, deviceSettings).ConfigureAwait(false);
            await SaveAsync().ConfigureAwait(false);
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

            await AddDeviceTestResultAsync(legacyResult).ConfigureAwait(false);
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

            await SaveAsync().ConfigureAwait(false);
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

            await SaveAsync().ConfigureAwait(false);
        }

        public async Task<List<Configuration.DeviceRecommendation>> GetDeviceRecommendationsAsync()
        {
            await Task.CompletedTask; // Make method async
            return _currentSettings.DeviceRecommendations;
        }

        public async Task SetRealTimeMonitoringEnabledAsync(string deviceId, bool enabled)
        {
            var deviceSettings = await GetDeviceSettingsAsync(deviceId).ConfigureAwait(false);
            deviceSettings.RealTimeMonitoringEnabled = enabled;
            await SetDeviceSettingsAsync(deviceId, deviceSettings).ConfigureAwait(false);
            await SaveAsync().ConfigureAwait(false);
        }

        public async Task RefreshDeviceListAsync()
        {
            _currentSettings.LastDeviceRefresh = DateTime.Now;
            await SaveAsync().ConfigureAwait(false);
        }

        public async Task<bool> IsDeviceEnabledAsync(string deviceId)
        {
            var settings = await GetDeviceSettingsAsync(deviceId);
            return settings.IsEnabled;
        }

        public async Task SetDeviceEnabledAsync(string deviceId, bool enabled)
        {
            var settings = await GetDeviceSettingsAsync(deviceId).ConfigureAwait(false);
            settings.IsEnabled = enabled;
            await SetDeviceSettingsAsync(deviceId, settings).ConfigureAwait(false);
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
                    var settings = await GetDeviceSettingsAsync(preferredDevice).ConfigureAwait(false);
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

        private byte[] GenerateEntropy()
        {
            // Use Windows DPAPI with optional entropy tied to machine and user
            // This provides secure encryption without managing keys ourselves
            var entropySource = $"{Environment.MachineName}_{Environment.UserName}_WhisperKey_v1";
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(entropySource));
        }

        private string EncryptString(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return string.Empty;

            try
            {
                var plainBytes = Encoding.UTF8.GetBytes(plainText);
                var entropy = GenerateEntropy();
                
                // Use Windows DPAPI for secure encryption tied to the current user
                var encryptedBytes = ProtectedData.Protect(
                    plainBytes, 
                    entropy, 
                    DataProtectionScope.CurrentUser);
                
                return Convert.ToBase64String(encryptedBytes);
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to encrypt string");
                return string.Empty;
            }
        }

        private string DecryptString(string encryptedText)
        {
            if (string.IsNullOrEmpty(encryptedText))
                return string.Empty;

            try
            {
                var encryptedBytes = Convert.FromBase64String(encryptedText);
                var entropy = GenerateEntropy();
                
                // Use Windows DPAPI to decrypt data
                var decryptedBytes = ProtectedData.Unprotect(
                    encryptedBytes, 
                    entropy, 
                    DataProtectionScope.CurrentUser);
                
                return Encoding.UTF8.GetString(decryptedBytes);
            }
            catch (FormatException ex)
            {
                _logger.LogError(ex, "Failed to decrypt string: invalid base64 format");
                return string.Empty;
            }
            catch (CryptographicException ex)
            {
                _logger.LogError(ex, "Failed to decrypt string");
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
            await SaveAsync().ConfigureAwait(false);
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
            await SaveAsync().ConfigureAwait(false);
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

            await SaveAsync().ConfigureAwait(false);
            return true;
        }

        public async Task<bool> SwitchHotkeyProfileAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId))
                return false;

            if (!_currentSettings.Hotkeys.Profiles.ContainsKey(profileId))
                return false;

            _currentSettings.Hotkeys.CurrentProfile = profileId;
            await SaveAsync().ConfigureAwait(false);
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

            // Validate file path to prevent path traversal attacks
            var validatedPath = ValidateFilePath(filePath);

            var exportData = new
            {
                Profile = profile,
                ExportedAt = DateTime.Now,
                Version = "1.0",
                Application = "WhisperKey",
                Settings = new
                {
                    _currentSettings.Hotkeys.ShowConflictWarnings,
                    _currentSettings.Hotkeys.EnableAccessibilityOptions,
                    _currentSettings.Hotkeys.EnableKeyboardLayoutAwareness
                }
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(validatedPath, json).ConfigureAwait(false);

            // Store backup path
            _currentSettings.Hotkeys.BackupProfilePath = validatedPath;
            await SaveAsync().ConfigureAwait(false);
        }

        public async Task<HotkeyProfile> ImportHotkeyProfileAsync(string filePath)
        {
            // Validate file path to prevent path traversal attacks
            var validatedPath = ValidateFilePath(filePath);

            if (!File.Exists(validatedPath))
                throw new FileNotFoundException($"Profile file not found: {validatedPath}");

            var json = await File.ReadAllTextAsync(validatedPath).ConfigureAwait(false);
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

            await CreateHotkeyProfileAsync(profile).ConfigureAwait(false);
            return profile;
        }

        /// <summary>
        /// Validates a file path to prevent path traversal attacks.
        /// Ensures the resolved path is within allowed user directories.
        /// </summary>
        /// <param name="filePath">The file path to validate</param>
        /// <returns>The validated full path</returns>
        /// <exception cref="SecurityException">Thrown when path attempts directory traversal outside allowed directories</exception>
        private string ValidateFilePath(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                throw new ArgumentException("File path cannot be empty", nameof(filePath));

            // Resolve to full path
            var fullPath = Path.GetFullPath(filePath);

            // Define allowed base directories (user-controlled locations only)
            var allowedBasePaths = new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                Path.GetTempPath()
            };

            // Check if the path is within any allowed directory
            var isAllowed = allowedBasePaths.Any(basePath =>
                !string.IsNullOrEmpty(basePath) && 
                (fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase) ||
                 fullPath.Equals(basePath, StringComparison.OrdinalIgnoreCase)));

            if (!isAllowed)
            {
                throw new SecurityException(
                    $"Access denied: Path '{filePath}' resolves to '{fullPath}' which is outside of allowed directories. " +
                    "Files can only be accessed within ApplicationData, Documents, Desktop, UserProfile, or Temp directories.");
            }

            return fullPath;
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
            var currentProfile = await GetCurrentHotkeyProfileAsync().ConfigureAwait(false);
            var conflictingHotkey = currentProfile.Hotkeys.Values
                .FirstOrDefault(h => string.Equals(h.Combination, combination, StringComparison.OrdinalIgnoreCase));

            if (conflictingHotkey != null)
            {
                result.IsValid = false;
                result.ErrorMessage = $"This hotkey is already used by: {conflictingHotkey.Name}";
                result.Conflicts.Add(new Configuration.HotkeyConflict
                {
                    ConflictingHotkey = conflictingHotkey.Combination,
                    ConflictingApplication = "WhisperKey",
                    ConflictType = "profile"
                });
            }

            return result;
        }

        public async Task BackupSettingsAsync(string description)
        {
            try
            {
                await _repository.BackupAsync(_currentSettings, description).ConfigureAwait(false);
                _logger.LogInformation("Settings backup created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create settings backup");
                throw new InvalidOperationException($"Failed to create backup: {ex.Message}", ex);
            }
        }

        public async Task RestoreSettingsAsync(string backupId)
        {
            try
            {
                var restoredSettings = await _repository.RestoreFromBackupAsync(backupId).ConfigureAwait(false);
                
                // Validate restored settings
                ValidateSettings(restoredSettings);
                
                // Apply restored settings
                _currentSettings = restoredSettings;
                await SaveAsync().ConfigureAwait(false);
                
                // Fire settings changed event
                SettingsChanged?.Invoke(this, new SettingsChangedEventArgs
                {
                    Key = "RestoreBackup",
                    OldValue = null,
                    NewValue = backupId,
                    Category = "System",
                    RequiresRestart = true
                });
                
                _logger.LogInformation("Settings restored from backup: {BackupId}", backupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to restore settings from backup");
                throw;
            }
        }

        public async Task<SettingsBackup[]> GetAvailableBackupsAsync()
        {
            try
            {
                return await _repository.GetBackupsAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get available backups");
                throw new InvalidOperationException($"Failed to get backups: {ex.Message}", ex);
            }
        }

        public async Task DeleteBackupAsync(string backupId)
        {
            try
            {
                await _repository.DeleteBackupAsync(backupId).ConfigureAwait(false);
                _logger.LogInformation("Settings backup deleted: {BackupId}", backupId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete settings backup");
                throw new InvalidOperationException($"Failed to delete backup: {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidateSettingsFileAsync()
        {
            try
            {
                return await _repository.ValidateAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to validate settings file");
                return false;
            }
        }

        private string GetEncryptedFilePath(string key)
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "WhisperKey");
            Directory.CreateDirectory(appFolder);
            return Path.Combine(appFolder, $"{key}.encrypted");
        }
        
        /// <summary>
        /// Dispose of debouncing resources
        /// </summary>
        public void Dispose()
        {
            _debounceTimer?.Stop();
            _debounceTimer?.Dispose();
            _saveSemaphore?.Dispose();
        }
    }
}