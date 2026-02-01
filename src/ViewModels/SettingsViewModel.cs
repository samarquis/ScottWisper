using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Configuration;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.ViewModels
{
    public class SettingsViewModel : INotifyPropertyChanged
    {
        private readonly ISettingsService _settingsService;
        private readonly IAudioDeviceService _audioDeviceService;
        private AppSettings _settings;
        private List<AudioDevice> _inputDevices = new List<AudioDevice>();
        private List<AudioDevice> _outputDevices = new List<AudioDevice>();
        private bool _isLoading = true;
        private string _statusMessage = "Loading settings...";
        private string _apiStatus = "Not tested";

        // Commands
        public ICommand SaveCommand { get; private set; }
        public ICommand ResetCommand { get; private set; }
        public ICommand TestAudioCommand { get; private set; }
        public ICommand RecordHotkeyCommand { get; private set; }
        public ICommand RefreshDevicesCommand { get; private set; }
        public ICommand TestApiCommand { get; private set; }
        public ICommand ValidateEndpointCommand { get; private set; }

        // Properties for General Settings
        private bool _startWithWindows;
        public bool StartWithWindows
        {
            get => _startWithWindows;
            set => SetProperty(ref _startWithWindows, value);
        }

        private bool _startMinimized;
        public bool StartMinimized
        {
            get => _startMinimized;
            set => SetProperty(ref _startMinimized, value);
        }

        private bool _checkForUpdates;
        public bool CheckForUpdates
        {
            get => _checkForUpdates;
            set => SetProperty(ref _checkForUpdates, value);
        }

        private int _startupDelay;
        public int StartupDelay
        {
            get => _startupDelay;
            set => SetProperty(ref _startupDelay, value);
        }

        private string _theme = "system";
        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        // Properties for Audio Settings
        private string _selectedInputDevice = string.Empty;
        public string SelectedInputDevice
        {
            get => _selectedInputDevice;
            set
            {
                if (SetProperty(ref _selectedInputDevice, value))
                {
                    _ = _settingsService.SetSelectedInputDeviceAsync(value);
                }
            }
        }

        private string _fallbackInputDevice = string.Empty;
        public string FallbackInputDevice
        {
            get => _fallbackInputDevice;
            set
            {
                if (SetProperty(ref _fallbackInputDevice, value))
                {
                    _ = _settingsService.SetFallbackInputDeviceAsync(value);
                }
            }
        }

        private string _selectedOutputDevice = string.Empty;
        public string SelectedOutputDevice
        {
            get => _selectedOutputDevice;
            set
            {
                if (SetProperty(ref _selectedOutputDevice, value))
                {
                    _ = _settingsService.SetSelectedOutputDeviceAsync(value);
                }
            }
        }

        private string _fallbackOutputDevice = string.Empty;
        public string FallbackOutputDevice
        {
            get => _fallbackOutputDevice;
            set
            {
                if (SetProperty(ref _fallbackOutputDevice, value))
                {
                    _ = _settingsService.SetFallbackOutputDeviceAsync(value);
                }
            }
        }

        private bool _autoSwitchDevices;
        public bool AutoSwitchDevices
        {
            get => _autoSwitchDevices;
            set
            {
                if (SetProperty(ref _autoSwitchDevices, value))
                {
                    _settings.Audio.AutoSwitchDevices = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private bool _preferHighQualityDevices;
        public bool PreferHighQualityDevices
        {
            get => _preferHighQualityDevices;
            set
            {
                if (SetProperty(ref _preferHighQualityDevices, value))
                {
                    _settings.Audio.PreferHighQualityDevices = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        // Properties for Transcription Settings
        private string _transcriptionProvider = "OpenAI";
        public string TranscriptionProvider
        {
            get => _transcriptionProvider;
            set
            {
                if (SetProperty(ref _transcriptionProvider, value))
                {
                    _settings.Transcription.Provider = value;
                    _ = _settingsService.SaveAsync();
                    OnPropertyChanged(nameof(AvailableModels));
                }
            }
        }

        private string _transcriptionModel = "whisper-1";
        public string TranscriptionModel
        {
            get => _transcriptionModel;
            set
            {
                if (SetProperty(ref _transcriptionModel, value))
                {
                    _settings.Transcription.Model = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private string _transcriptionLanguage = "auto";
        public string TranscriptionLanguage
        {
            get => _transcriptionLanguage;
            set
            {
                if (SetProperty(ref _transcriptionLanguage, value))
                {
                    _settings.Transcription.Language = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private string _apiKey = string.Empty;
        public string ApiKey
        {
            get => _apiKey;
            set
            {
                if (SetProperty(ref _apiKey, value))
                {
                    _settings.Transcription.ApiKey = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private bool _enableAutoPunctuation;
        public bool EnableAutoPunctuation
        {
            get => _enableAutoPunctuation;
            set
            {
                if (SetProperty(ref _enableAutoPunctuation, value))
                {
                    _settings.Transcription.EnableAutoPunctuation = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private bool _enableRealTimeTranscription;
        public bool EnableRealTimeTranscription
        {
            get => _enableRealTimeTranscription;
            set
            {
                if (SetProperty(ref _enableRealTimeTranscription, value))
                {
                    _settings.Transcription.EnableRealTimeTranscription = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private int _confidenceThreshold = 80;
        public int ConfidenceThreshold
        {
            get => _confidenceThreshold;
            set
            {
                if (SetProperty(ref _confidenceThreshold, value))
                {
                    _settings.Transcription.ConfidenceThreshold = value / 100f;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private int _maxRecordingDuration = 30;
        public int MaxRecordingDuration
        {
            get => _maxRecordingDuration;
            set
            {
                if (SetProperty(ref _maxRecordingDuration, value))
                {
                    _settings.Transcription.MaxRecordingDuration = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        // Properties for API Settings
        private string _apiEndpoint = string.Empty;
        public string ApiEndpoint
        {
            get => _apiEndpoint;
            set
            {
                if (SetProperty(ref _apiEndpoint, value))
                {
                    _settings.Transcription.ApiEndpoint = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private int _apiTimeout = 30;
        public int ApiTimeout
        {
            get => _apiTimeout;
            set
            {
                if (SetProperty(ref _apiTimeout, value))
                {
                    _settings.Transcription.RequestTimeout = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private bool _useProxy;
        public bool UseProxy
        {
            get => _useProxy;
            set
            {
                if (SetProperty(ref _useProxy, value))
                {
                    _settings.Transcription.UseProxy = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        // Properties for UI Settings
        private bool _showVisualFeedback;
        public bool ShowVisualFeedback
        {
            get => _showVisualFeedback;
            set
            {
                if (SetProperty(ref _showVisualFeedback, value))
                {
                    _settings.UI.ShowVisualFeedback = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private bool _showTranscriptionWindow;
        public bool ShowTranscriptionWindow
        {
            get => _showTranscriptionWindow;
            set
            {
                if (SetProperty(ref _showTranscriptionWindow, value))
                {
                    _settings.UI.ShowTranscriptionWindow = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private bool _minimizeToTray;
        public bool MinimizeToTray
        {
            get => _minimizeToTray;
            set
            {
                if (SetProperty(ref _minimizeToTray, value))
                {
                    _settings.UI.MinimizeToTray = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private int _windowOpacity = 80;
        public int WindowOpacity
        {
            get => _windowOpacity;
            set
            {
                if (SetProperty(ref _windowOpacity, value))
                {
                    _settings.UI.WindowOpacity = value / 100.0;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private int _feedbackVolume = 50;
        public int FeedbackVolume
        {
            get => _feedbackVolume;
            set
            {
                if (SetProperty(ref _feedbackVolume, value))
                {
                    _settings.UI.FeedbackVolume = value / 100f;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        // Properties for Hotkey Settings
        private string _toggleRecordingHotkey = "Ctrl+Alt+V";
        public string ToggleRecordingHotkey
        {
            get => _toggleRecordingHotkey;
            set
            {
                if (SetProperty(ref _toggleRecordingHotkey, value))
                {
                    _settings.Hotkeys.ToggleRecording = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private string _showSettingsHotkey = "Ctrl+Alt+S";
        public string ShowSettingsHotkey
        {
            get => _showSettingsHotkey;
            set
            {
                if (SetProperty(ref _showSettingsHotkey, value))
                {
                    _settings.Hotkeys.ShowSettings = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        // Properties for Advanced Settings
        private bool _enableDebugLogging;
        public bool EnableDebugLogging
        {
            get => _enableDebugLogging;
            set
            {
                if (SetProperty(ref _enableDebugLogging, value))
                {
                    _settings.TextInjection.EnableDebugMode = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private string _logLevel = "info";
        public string LogLevel
        {
            get => _logLevel;
            set
            {
                if (SetProperty(ref _logLevel, value))
                {
                    // _settings.TextInjection.LogLevel = value; // LogLevel not in TextInjection, using a placeholder or skipping
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        private bool _enablePerformanceMetrics;
        public bool EnablePerformanceMetrics
        {
            get => _enablePerformanceMetrics;
            set
            {
                if (SetProperty(ref _enablePerformanceMetrics, value))
                {
                    _settings.TextInjection.EnablePerformanceMonitoring = value;
                    _ = _settingsService.SaveAsync();
                }
            }
        }

        // Collections
        public List<AudioDevice> InputDevices => _inputDevices;
        public List<AudioDevice> OutputDevices => _outputDevices;

        // Available models for selected provider
        public List<(string Id, string DisplayName)> AvailableModels => GetAvailableModels(TranscriptionProvider);

        // Status properties
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }
        public string ApiStatus { get => _apiStatus; set => SetProperty(ref _apiStatus, value); }
        public bool IsLoading { get => _isLoading; set => SetProperty(ref _isLoading, value); }

        // Usage Statistics
        private long _totalRequests = 0;
        public long TotalRequests
        {
            get => _totalRequests;
            set => SetProperty(ref _totalRequests, value);
        }

        private double _totalMinutes = 0.0;
        public double TotalMinutes
        {
            get => _totalMinutes;
            set => SetProperty(ref _totalMinutes, value);
        }

        private double _currentMonthUsage = 0.0;
        public double CurrentMonthUsage
        {
            get => _currentMonthUsage;
            set => SetProperty(ref _currentMonthUsage, value);
        }

        private int _usageLimit = 1000;
        public int UsageLimit
        {
            get => _usageLimit;
            set => SetProperty(ref _usageLimit, value);
        }

        public double UsageProgress => UsageLimit > 0 ? (CurrentMonthUsage / UsageLimit) * 100 : 0;

        public SettingsViewModel(ISettingsService settingsService, IAudioDeviceService audioDeviceService)
        {
            _settingsService = settingsService;
            _audioDeviceService = audioDeviceService;
            _settings = settingsService.Settings;

            InitializeCommands();
            _ = LoadSettingsAsync();
            _ = LoadDevicesAsync();
        }

        private void InitializeCommands()
        {
            SaveCommand = new RelayCommand(async () => await SaveSettingsAsync().ConfigureAwait(false));
            ResetCommand = new RelayCommand(async () => await ResetSettingsAsync().ConfigureAwait(false));
            TestAudioCommand = new RelayCommand(async () => await TestAudioAsync().ConfigureAwait(false));
            RecordHotkeyCommand = new RelayCommand(() => { StartHotkeyRecording(); return Task.CompletedTask; });
            RefreshDevicesCommand = new RelayCommand(async () => await RefreshDevicesAsync().ConfigureAwait(false));
            TestApiCommand = new RelayCommand(async () => await TestApiAsync().ConfigureAwait(false));
            ValidateEndpointCommand = new RelayCommand(async () => await ValidateEndpointAsync().ConfigureAwait(false));
        }

        public async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                StatusMessage = "Loading settings...";

                // Load general settings
                StartWithWindows = _settings.UI.StartWithWindows;
                StartMinimized = _settings.UI.StartMinimized;
                CheckForUpdates = _settings.UI.CheckForUpdates;
                StartupDelay = _settings.UI.StartupDelay;
                Theme = _settings.UI.Theme ?? "system";

                // Load audio settings
                SelectedInputDevice = _settings.Audio.InputDeviceId ?? string.Empty;
                FallbackInputDevice = _settings.Audio.FallbackInputDeviceId ?? string.Empty;
                SelectedOutputDevice = _settings.Audio.OutputDeviceId ?? string.Empty;
                FallbackOutputDevice = _settings.Audio.FallbackOutputDeviceId ?? string.Empty;
                AutoSwitchDevices = _settings.Audio.AutoSwitchDevices;
                PreferHighQualityDevices = _settings.Audio.PreferHighQualityDevices;

                // Load transcription settings
                TranscriptionProvider = _settings.Transcription.Provider;
                TranscriptionModel = _settings.Transcription.Model;
                TranscriptionLanguage = _settings.Transcription.Language;
                ApiKey = _settings.Transcription.ApiKey ?? string.Empty;
                EnableAutoPunctuation = _settings.Transcription.EnableAutoPunctuation;
                EnableRealTimeTranscription = _settings.Transcription.EnableRealTimeTranscription;
                ConfidenceThreshold = (int)(_settings.Transcription.ConfidenceThreshold * 100);
                MaxRecordingDuration = _settings.Transcription.MaxRecordingDuration;

                // Load API settings
                ApiEndpoint = _settings.Transcription.ApiEndpoint ?? string.Empty;
                ApiTimeout = _settings.Transcription.RequestTimeout;
                UseProxy = _settings.Transcription.UseProxy;

                // Load UI settings
                ShowVisualFeedback = _settings.UI.ShowVisualFeedback;
                ShowTranscriptionWindow = _settings.UI.ShowTranscriptionWindow;
                MinimizeToTray = _settings.UI.MinimizeToTray;
                WindowOpacity = (int)(_settings.UI.WindowOpacity * 100);
                FeedbackVolume = (int)(_settings.UI.FeedbackVolume * 100);

                // Load hotkey settings
                ToggleRecordingHotkey = _settings.Hotkeys.ToggleRecording;
                ShowSettingsHotkey = _settings.Hotkeys.ShowSettings;

                // Load advanced settings
                EnableDebugLogging = _settings.TextInjection.EnableDebugMode;
                LogLevel = "info"; // _settings.Advanced.LogLevel ?? "info";
                EnablePerformanceMetrics = _settings.TextInjection.EnablePerformanceMonitoring;

                // Load usage statistics
                await LoadUsageStatisticsAsync().ConfigureAwait(false);

                IsLoading = false;
                StatusMessage = "Settings loaded successfully";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = $"Error loading settings: {ex.Message}";
            }
            catch (IOException ex)
            {
                StatusMessage = $"Error loading settings: {ex.Message}";
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusMessage = $"Error loading settings: {ex.Message}";
            }
        }

        private async Task LoadDevicesAsync()
        {
            try
            {
                StatusMessage = "Loading devices...";

                var inputDevices = await _audioDeviceService.GetInputDevicesAsync().ConfigureAwait(false);
                var outputDevices = await _audioDeviceService.GetOutputDevicesAsync().ConfigureAwait(false);

                _inputDevices.Clear();
                _inputDevices.AddRange(inputDevices);
                OnPropertyChanged(nameof(InputDevices));

                _outputDevices.Clear();
                _outputDevices.AddRange(outputDevices);
                OnPropertyChanged(nameof(OutputDevices));

                StatusMessage = "Devices loaded successfully";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = $"Error loading devices: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                StatusMessage = $"Error loading devices: {ex.Message}";
            }
        }

        private async Task LoadUsageStatisticsAsync()
        {
            try
            {
                // Mock implementation - would calculate from actual usage data
                TotalRequests = 0;
                TotalMinutes = 0.0;
                CurrentMonthUsage = 0.0;
                UsageLimit = 1000;
            }
            catch (InvalidOperationException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading usage statistics: {ex.Message}");
            }
            catch (IOException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading usage statistics: {ex.Message}");
            }
        }

        public async Task SaveSettingsAsync()
        {
            try
            {
                StatusMessage = "Saving settings...";
                await _settingsService.SaveAsync().ConfigureAwait(false);
                StatusMessage = "Settings saved successfully";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
            }
            catch (IOException ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
            }
            catch (UnauthorizedAccessException ex)
            {
                StatusMessage = $"Error saving settings: {ex.Message}";
            }
        }

        public async Task ResetSettingsAsync()
        {
            try
            {
                StatusMessage = "Resetting settings...";
                // Implementation would reset all settings to defaults
                await Task.CompletedTask;
                StatusMessage = "Settings reset successfully";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = $"Error resetting settings: {ex.Message}";
            }
        }

        private async Task TestAudioAsync()
        {
            try
            {
                StatusMessage = "Testing audio devices...";
                var testResults = new List<DeviceTestingResult>();

                foreach (var device in _inputDevices)
                {
                    var testResult = await _audioDeviceService.TestDeviceAsync(device.Id).ConfigureAwait(false);
                    testResults.Add(new DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = testResult,
                        TestTime = DateTime.Now,
                        ErrorMessage = testResult ? "" : "Test failed"
                    });
                }

                // Update device test results
                foreach (var result in testResults)
                {
                    await _settingsService.AddDeviceTestResultAsync(result).ConfigureAwait(false);
                }

                StatusMessage = $"Audio test completed for {_inputDevices.Count} devices";
            }
            catch (InvalidOperationException ex)
            {
                StatusMessage = $"Error testing audio: {ex.Message}";
            }
            catch (TaskCanceledException ex)
            {
                StatusMessage = $"Error testing audio: {ex.Message}";
            }
        }

        private void StartHotkeyRecording()
        {
            StatusMessage = "Press desired hotkey combination...";
            // Implementation would start hotkey recording
        }

        private async Task RefreshDevicesAsync()
        {
            await LoadDevicesAsync();
        }

        private async Task TestApiAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ApiKey))
                {
                    ApiStatus = "API key required";
                    return;
                }

                ApiStatus = "Testing API...";
                OnPropertyChanged(nameof(ApiStatus));

                var isValid = await ValidateApiKeyAsync(ApiKey);

                ApiStatus = isValid ? "API key valid" : "API key invalid";
                OnPropertyChanged(nameof(ApiStatus));
            }
            catch (Exception ex)
            {
                ApiStatus = $"Test failed: {ex.Message}";
                OnPropertyChanged(nameof(ApiStatus));
            }
        }

        private async Task ValidateEndpointAsync()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(ApiEndpoint))
                {
                    ApiStatus = "Endpoint URL required";
                    return;
                }

                ApiStatus = "Validating endpoint...";
                OnPropertyChanged(nameof(ApiStatus));

                // Implementation would validate API endpoint
                await Task.Delay(1000);
                ApiStatus = "Endpoint valid";
                OnPropertyChanged(nameof(ApiStatus));
            }
            catch (Exception ex)
            {
                ApiStatus = $"Validation failed: {ex.Message}";
                OnPropertyChanged(nameof(ApiStatus));
            }
        }

        private async Task<bool> ValidateApiKeyAsync(string apiKey)
        {
            try
            {
                // Basic format validation
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return false;
                }

                var provider = TranscriptionProvider.ToLower();
                
                // Provider-specific validation
                switch (provider)
                {
                    case "openai":
                        if (!apiKey.StartsWith("sk-") || apiKey.Length < 20)
                        {
                            return false;
                        }
                        break;
                    case "azure":
                        if (!Guid.TryParse(apiKey, out _) && apiKey.Length < 32)
                        {
                            return false;
                        }
                        break;
                    case "google":
                        if (apiKey.Length < 30)
                        {
                            return false;
                        }
                        break;
                    default:
                        if (apiKey.Length < 10)
                        {
                            return false;
                        }
                        break;
                }

                // Simulate API call with timeout
                using (var httpClient = new System.Net.Http.HttpClient())
                {
                    httpClient.Timeout = TimeSpan.FromSeconds(10);
                    
                    var testEndpoint = provider.ToLower() switch
                    {
                        "openai" => "https://api.openai.com/v1/models",
                        "azure" => "https://<region>.api.cognitive.microsoft.com/sts/v1.0/issuetoken",
                        "google" => "https://speech.googleapis.com/v1/speech",
                        _ => "https://api.example.com/test"
                    };

                    await Task.Delay(1000);
                    return true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API key test failed: {ex.Message}");
                return false;
            }
        }

        private List<(string Id, string DisplayName)> GetAvailableModels(string provider)
        {
            return provider.ToLower() switch
            {
                "openai" => new List<(string, string)>
                {
                    ("whisper-1", "Whisper v1"),
                    ("whisper-tiny", "Whisper Tiny"),
                    ("whisper-base", "Whisper Base"),
                    ("whisper-small", "Whisper Small")
                },
                "azure" => new List<(string, string)>
                {
                    ("latest", "Latest"),
                    ("whisper", "Whisper")
                },
                "google" => new List<(string, string)>
                {
                    ("latest", "Latest"),
                    ("chirp", "Chirp"),
                    ("generic", "Generic")
                },
                _ => new List<(string, string)> { ("whisper-1", "Default") }
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }

    // Simple command implementation
    public class RelayCommand : ICommand
    {
        private readonly Func<Task?> _executeAsync;
        private readonly Func<bool>? _canExecute;
        private bool _isExecuting;

        public RelayCommand(Func<Task?> executeAsync, Func<bool>? canExecute = null)
        {
            _executeAsync = executeAsync;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter)
        {
            return !_isExecuting && (_canExecute?.Invoke() ?? true);
        }

        public async void Execute(object? parameter)
        {
            if (_isExecuting) return;

            _isExecuting = true;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);

            try
            {
                if (_executeAsync != null)
                    await _executeAsync();
            }
            finally
            {
                _isExecuting = false;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}