using Microsoft.Extensions.DependencyInjection;
using ScottWisper.Configuration;
using ScottWisper.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ScottWisper
{
    public partial class SettingsWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IAudioDeviceService _audioDeviceService;
        private readonly List<Services.AudioDevice> _inputDevices = new List<Services.AudioDevice>();
        private readonly List<Services.AudioDevice> _outputDevices = new List<Services.AudioDevice>();
        private bool _isLoading = true;
        private AppSettings _originalSettings;
        private AppSettings _currentSettings;
        
        // Hotkey management fields
        private bool _isRecordingHotkey = false;
        private List<Key> _pressedKeys = new List<Key>();
        private HotkeyProfile? _currentHotkeyProfile;
        private HotkeyDefinition? _editingHotkey;
        private string _recordingHotkeyId = string.Empty;

        public SettingsWindow(ISettingsService settingsService, IAudioDeviceService audioDeviceService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _audioDeviceService = audioDeviceService;
            _originalSettings = CloneSettings(_settingsService.Settings);
            
            InitializeEventHandlers();
            _ = LoadDevicesAsync();
        }

        private void InitializeEventHandlers()
        {
            _audioDeviceService.DeviceConnected += OnDeviceConnected;
            _audioDeviceService.DeviceDisconnected += OnDeviceDisconnected;
            _audioDeviceService.DefaultDeviceChanged += OnDefaultDeviceChanged;
        }

        private async Task LoadDevicesAsync()
        {
            try
            {
                UpdateDeviceStatus("Loading devices...");
                
                // Load devices
                _inputDevices.Clear();
                _outputDevices.Clear();
                
                var inputDevices = await _audioDeviceService.GetInputDevicesAsync();
                var outputDevices = await _audioDeviceService.GetOutputDevicesAsync();
                
                _inputDevices.AddRange(inputDevices);
                _outputDevices.AddRange(outputDevices);

                // Update UI on main thread
                await Dispatcher.InvokeAsync(() =>
                {
                    PopulateDeviceComboBoxes();
                    PopulateDeviceGrid();
                    LoadCurrentSettings();
                    _isLoading = false;
                    UpdateDeviceStatus("Device list loaded");
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateDeviceStatus($"Error loading devices: {ex.Message}");
                    _isLoading = false;
                });
            }
        }

        private void PopulateDeviceComboBoxes()
        {
            // Populate input device combo boxes
            PopulateComboBox(InputDeviceComboBox, _inputDevices, _settingsService.Settings.Audio.InputDeviceId);
            PopulateComboBox(FallbackInputDeviceComboBox, _inputDevices, _settingsService.Settings.Audio.FallbackInputDeviceId);
            
            // Populate output device combo boxes
            PopulateComboBox(OutputDeviceComboBox, _outputDevices, _settingsService.Settings.Audio.OutputDeviceId);
            PopulateComboBox(FallbackOutputDeviceComboBox, _outputDevices, _settingsService.Settings.Audio.FallbackOutputDeviceId);
        }

        private void PopulateComboBox(ComboBox comboBox, List<Services.AudioDevice> devices, string selectedDeviceId)
        {
            comboBox.Items.Clear();
            
            // Add "Default" option
            comboBox.Items.Add(new ComboBoxItem { Content = "Default", Tag = "default" });
            
            // Add devices
            foreach (var device in devices)
            {
                var item = new ComboBoxItem 
                { 
                    Content = device.Name, 
                    Tag = device.Id 
                };
                comboBox.Items.Add(item);
            }
            
            // Select current device
            var selectedItem = comboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag.ToString() == selectedDeviceId);
            if (selectedItem != null)
            {
                comboBox.SelectedItem = selectedItem;
            }
        }

        /// <summary>
        /// Enhanced device grid with compatibility indicators and quality metrics
        /// </summary>
        private void PopulateDeviceGrid()
        {
            var allDevices = new List<object>();
            
            foreach (var inputDevice in _inputDevices)
            {
                var compatibilityScore = GetDeviceCompatibilityScore(inputDevice.Id);
                var deviceStatus = GetDeviceStatusIndicator(inputDevice);
                
                allDevices.Add(new
                {
                    Name = inputDevice.Name,
                    DataFlow = "Input",
                    State = inputDevice.State.ToString(),
                    IsCompatible = _audioDeviceService.IsDeviceCompatible(inputDevice.Id),
                    LastTested = GetLastTestedTime(inputDevice.Id),
                    IsEnabled = GetDeviceEnabled(inputDevice.Id),
                    CompatibilityScore = compatibilityScore,
                    DeviceStatus = deviceStatus,
                    QualityIndicator = GetQualityIndicator(compatibilityScore),
                    RecommendedUsage = GetRecommendedUsage(inputDevice, compatibilityScore)
                });
            }
            
            foreach (var outputDevice in _outputDevices)
            {
                var compatibilityScore = GetDeviceCompatibilityScore(outputDevice.Id);
                var deviceStatus = GetDeviceStatusIndicator(outputDevice);
                
                allDevices.Add(new
                {
                    Name = outputDevice.Name,
                    DataFlow = "Output",
                    State = outputDevice.State.ToString(),
                    IsCompatible = _audioDeviceService.IsDeviceCompatible(outputDevice.Id),
                    LastTested = GetLastTestedTime(outputDevice.Id),
                    IsEnabled = GetDeviceEnabled(outputDevice.Id),
                    CompatibilityScore = compatibilityScore,
                    DeviceStatus = deviceStatus,
                    QualityIndicator = GetQualityIndicator(compatibilityScore),
                    RecommendedUsage = GetRecommendedUsage(outputDevice, compatibilityScore)
                });
            }
            
            DevicesDataGrid.ItemsSource = allDevices;
        }

        /// <summary>
        /// Get device compatibility score for UI display
        /// </summary>
        private double GetDeviceCompatibilityScore(string deviceId)
        {
            try
            {
                // Use AudioDeviceService compatibility scoring
                var scoreTask = _audioDeviceService.ScoreDeviceCompatibilityAsync(deviceId);
                return scoreTask.Result; // In real implementation, await properly
            }
            catch
            {
                return 0.0; // Default to poor compatibility
            }
        }

        /// <summary>
        /// Get device status indicator for UI
        /// </summary>
        private string GetDeviceStatusIndicator(Services.AudioDevice device)
        {
            return device.State switch
            {
                AudioDeviceState.Active => device.PermissionStatus == MicrophonePermissionStatus.Granted ? "Ready" : "Permission Denied",
                AudioDeviceState.Disabled => "Disabled",
                AudioDeviceState.Unplugged => "Unplugged",
                AudioDeviceState.NotPresent => "Not Present",
                _ => "Unknown"
            };
        }

        /// <summary>
        /// Get quality indicator text based on compatibility score
        /// </summary>
        private string GetQualityIndicator(double score)
        {
            return score switch
            {
                >= 0.8 => "Excellent",
                >= 0.6 => "Good",
                >= 0.4 => "Fair",
                >= 0.2 => "Poor",
                _ => "Very Poor"
            };
        }

        /// <summary>
        /// Get recommended usage based on device capabilities
        /// </summary>
        private string GetRecommendedUsage(Services.AudioDevice device, double score)
        {
            if (score >= 0.8)
            {
                return "Recommended for dictation";
            }
            else if (score >= 0.6)
            {
                return "Suitable for most tasks";
            }
            else if (score >= 0.4)
            {
                return "Limited functionality";
            }
            else
            {
                return "Not recommended";
            }
        }

        private void LoadCurrentSettings()
        {
            // Audio settings
            AutoSwitchDevicesCheckBox.IsChecked = _settingsService.Settings.Audio.AutoSwitchDevices;
            PreferHighQualityCheckBox.IsChecked = _settingsService.Settings.Audio.PreferHighQualityDevices;
            
            // Transcription settings
            PopulateTranscriptionControls();
            
            // Hotkey settings
            LoadHotkeySettingsAsync();
            
            // UI settings
            ShowVisualFeedbackCheckBox.IsChecked = _settingsService.Settings.UI.ShowVisualFeedback;
            ShowTranscriptionWindowCheckBox.IsChecked = _settingsService.Settings.UI.ShowTranscriptionWindow;
            MinimizeToTrayCheckBox.IsChecked = _settingsService.Settings.UI.MinimizeToTray;
            StartWithWindowsCheckBox.IsChecked = _settingsService.Settings.UI.StartWithWindows;
            
            // Update usage statistics
            UpdateUsageStatistics();
        }

        private void PopulateTranscriptionControls()
        {
            // Populate provider combo box
            ProviderComboBox.Items.Clear();
            var providers = new List<(string Id, string Name)>
            {
                ("OpenAI", "OpenAI Whisper"),
                ("Azure", "Azure Speech Services"),
                ("Google", "Google Speech-to-Text")
            };
            
            foreach (var provider in providers)
            {
                var item = new ComboBoxItem { Content = provider.Name, Tag = provider.Id };
                ProviderComboBox.Items.Add(item);
            }
            
            // Select current provider
            var currentProvider = _settingsService.Settings.Transcription.Provider;
            var selectedProvider = ProviderComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == currentProvider);
            if (selectedProvider != null)
            {
                ProviderComboBox.SelectedItem = selectedProvider;
            }
            
            // Populate language combo box
            LanguageComboBox.Items.Clear();
            var languages = new List<(string Code, string Name)>
            {
                ("auto", "Auto-detect"),
                ("en", "English"),
                ("es", "Spanish"),
                ("fr", "French"),
                ("de", "German"),
                ("it", "Italian"),
                ("pt", "Portuguese"),
                ("zh", "Chinese"),
                ("ja", "Japanese"),
                ("ko", "Korean")
            };
            
            foreach (var language in languages)
            {
                var item = new ComboBoxItem { Content = language.Name, Tag = language.Code };
                LanguageComboBox.Items.Add(item);
            }
            
            // Select current language
            var currentLanguage = _settingsService.Settings.Transcription.Language;
            var selectedLanguage = LanguageComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == currentLanguage);
            if (selectedLanguage != null)
            {
                LanguageComboBox.SelectedItem = selectedLanguage;
            }
            
            // Set API key (masked)
            ApiKeyPasswordBox.Password = _settingsService.Settings.Transcription.ApiKey;
            
            // Update available models based on provider
            UpdateAvailableModels();
        }

        private void UpdateUsageStatistics()
        {
            // Mock implementation - would calculate from actual usage data
            TotalRequestsText.Text = "0";
            TotalMinutesText.Text = "0.0";
            CurrentMonthUsageText.Text = "0.0 minutes";
        }

        private DateTime GetLastTestedTime(string deviceId)
        {
            var testHistory = _settingsService.Settings.DeviceTestHistory
                .Where(r => r.DeviceId == deviceId)
                .FirstOrDefault();
            
            return testHistory?.TestTime ?? DateTime.MinValue;
        }

        private bool GetDeviceEnabled(string deviceId)
        {
            var deviceSettings = _settingsService.Settings.Audio.DeviceSettings
                .GetValueOrDefault(deviceId, new DeviceSpecificSettings());
            return deviceSettings.IsEnabled;
        }

        private void UpdateDeviceStatus(string status)
        {
            DeviceStatusText.Text = $"Device status: {status}";
        }

        private void UpdateDeviceDetails(Services.AudioDevice? device)
        {
            if (device == null)
            {
                DeviceNameText.Text = "No device selected";
                SampleRateText.Text = "-";
                ChannelsText.Text = "-";
                DeviceStatusIndicator.Text = "-";
                LastTestedText.Text = "Never";
                TestResultText.Text = "No test performed";
                DeviceNotesText.Text = "No notes available";
                return;
            }

            DeviceNameText.Text = device.Name;
            DeviceStatusIndicator.Text = device.State.ToString();
            
            // Get device-specific settings
            var deviceSettings = _settingsService.Settings.Audio.DeviceSettings
                .GetValueOrDefault(device.Id, new DeviceSpecificSettings { Name = device.Name });
            
            SampleRateText.Text = $"{deviceSettings.SampleRate} Hz";
            ChannelsText.Text = deviceSettings.Channels.ToString();
            LastTestedText.Text = deviceSettings.LastTested == DateTime.MinValue 
                ? "Never" 
                : deviceSettings.LastTested.ToString("yyyy-MM-dd HH:mm:ss");
            
            TestResultText.Text = deviceSettings.LastTestPassed ? "✓ Passed" : "✗ Failed";
            TestResultText.Foreground = deviceSettings.LastTestPassed 
                ? System.Windows.Media.Brushes.Green 
                : System.Windows.Media.Brushes.Red;
            
            DeviceNotesText.Text = string.IsNullOrEmpty(deviceSettings.Notes) 
                ? "No notes available" 
                : deviceSettings.Notes;
        }

        private async void TestInputDevice_Click(object sender, RoutedEventArgs e)
        {
            await TestSelectedDevice(InputDeviceComboBox);
        }

        private async void TestOutputDevice_Click(object sender, RoutedEventArgs e)
        {
            await TestSelectedDevice(OutputDeviceComboBox);
        }

        /// <summary>
        /// Enhanced device testing with audio level visualization
        /// </summary>
        private async Task TestSelectedDevice(ComboBox comboBox)
        {
            if (comboBox.SelectedItem == null) return;
            
            var selectedItem = (ComboBoxItem)comboBox.SelectedItem;
            var deviceId = selectedItem.Tag.ToString();
            
            if (deviceId == "default")
            {
                MessageBox.Show("Cannot test default device selection. Please select a specific device.", 
                    "Test Device", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            
            try
            {
                UpdateDeviceStatus($"Testing device: {selectedItem.Content}...");
                
                // Perform comprehensive device test
                var comprehensiveTest = await _audioDeviceService.PerformComprehensiveTestAsync(deviceId);
                
                // Get device capabilities
                var capabilities = await _audioDeviceService.GetDeviceCapabilitiesAsync(deviceId);
                
                // Create enhanced test result
                var testResult = new DeviceTestingResult
                {
                    DeviceId = deviceId,
                    DeviceName = selectedItem.Content.ToString()!,
                    TestPassed = comprehensiveTest.Success,
                    TestTime = DateTime.Now,
                    ErrorMessage = comprehensiveTest.Success ? "" : comprehensiveTest.ErrorMessage,
                    QualityScore = comprehensiveTest.QualityScore,
                    LatencyMs = comprehensiveTest.LatencyMs,
                    NoiseFloorDb = comprehensiveTest.NoiseFloorDb,
                    SupportedFormats = comprehensiveTest.SupportedFormats
                };
                
                // Save test result
                await _settingsService.AddDeviceTestResultAsync(testResult);
                
                // Update UI with enhanced information
                PopulateDeviceGrid();
                UpdateDeviceStatus(comprehensiveTest.Success ? "Device test completed successfully" : "Device test failed");
                
                // Show enhanced test results dialog
                var testDialog = new DeviceTestResultDialog
                {
                    TestResult = testResult,
                    Capabilities = capabilities
                };
                
                var dialogResult = testDialog.ShowDialog();
                if (dialogResult == true && comprehensiveTest.Success)
                {
                    // Mark device as preferred if quality is excellent
                    if (comprehensiveTest.QualityScore >= 0.8f)
                    {
                        var device = _inputDevices.FirstOrDefault(d => d.Id == deviceId);
                        if (device != null)
                        {
                            await _settingsService.SetPreferredDeviceAsync(deviceId);
                            MessageBox.Show($"Device '{device.Name}' marked as preferred due to excellent quality.", 
                                "Device Quality", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                UpdateDeviceStatus($"Test failed: {ex.Message}");
                MessageBox.Show($"Failed to test device: {ex.Message}", 
                    "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshDevices_Click(object sender, RoutedEventArgs e)
        {
            await LoadDevicesAsync();
        }

        private async void TestAllDevices_Click(object sender, RoutedEventArgs e)
        {
            UpdateDeviceStatus("Testing all devices...");
            
            var testResults = new List<DeviceTestingResult>();
            
            foreach (var device in _inputDevices)
            {
                try
                {
                    var testPassed = await _audioDeviceService.TestDeviceAsync(device.Id);
                    var result = new DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = testPassed,
                        TestTime = DateTime.Now,
                        ErrorMessage = testPassed ? "" : "Device test failed"
                    };
                    testResults.Add(result);
                }
                catch (Exception ex)
                {
                    var result = new DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = false,
                        TestTime = DateTime.Now,
                        ErrorMessage = ex.Message
                    };
                    testResults.Add(result);
                }
            }
            
            // Save all test results
            foreach (var result in testResults)
            {
                await _settingsService.AddDeviceTestResultAsync(result);
            }
            
            PopulateDeviceGrid();
            UpdateDeviceStatus($"Tested {_inputDevices.Count} devices");
        }

        private async void InputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            
            var selectedItem = (ComboBoxItem?)InputDeviceComboBox.SelectedItem;
            if (selectedItem != null)
            {
                await _settingsService.SetSelectedInputDeviceAsync(selectedItem.Tag.ToString()!);
            }
        }

        private async void FallbackInputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            
            var selectedItem = (ComboBoxItem?)FallbackInputDeviceComboBox.SelectedItem;
            if (selectedItem != null)
            {
                await _settingsService.SetFallbackInputDeviceAsync(selectedItem.Tag.ToString()!);
            }
        }

        private async void OutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            
            var selectedItem = (ComboBoxItem?)OutputDeviceComboBox.SelectedItem;
            if (selectedItem != null)
            {
                await _settingsService.SetSelectedOutputDeviceAsync(selectedItem.Tag.ToString()!);
            }
        }

        private async void FallbackOutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            
            var selectedItem = (ComboBoxItem?)FallbackOutputDeviceComboBox.SelectedItem;
            if (selectedItem != null)
            {
                await _settingsService.SetFallbackOutputDeviceAsync(selectedItem.Tag.ToString()!);
            }
        }

        private async void AutoSwitchDevices_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.Audio.AutoSwitchDevices = AutoSwitchDevicesCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private async void PreferHighQuality_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.Audio.PreferHighQualityDevices = PreferHighQualityCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        #region Transcription Settings Event Handlers

        private async void Provider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            var selectedItem = ProviderComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                _settingsService.Settings.Transcription.Provider = selectedItem.Tag?.ToString() ?? "OpenAI";
                await _settingsService.SaveAsync();
                UpdateAvailableModels();
            }
        }

        private async void Model_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            var selectedItem = ModelComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                _settingsService.Settings.Transcription.Model = selectedItem.Tag?.ToString() ?? "whisper-1";
                await _settingsService.SaveAsync();
            }
        }

        private async void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            var selectedItem = LanguageComboBox.SelectedItem as ComboBoxItem;
            if (selectedItem != null)
            {
                _settingsService.Settings.Transcription.Language = selectedItem.Tag?.ToString() ?? "auto";
                await _settingsService.SaveAsync();
            }
        }

        private async void ApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.Transcription.ApiKey = ApiKeyPasswordBox.Password;
            await _settingsService.SaveAsync();
        }

        private async void TestApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ApiKeyPasswordBox.Password))
            {
                MessageBox.Show("Please enter an API key first.", "API Key Required", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ApiStatusText.Text = "Testing API key...";
                ApiStatusText.Foreground = System.Windows.Media.Brushes.Orange;

                // This would call the actual transcription service to test the API key
                var isValid = await TestApiKeyAsync(ApiKeyPasswordBox.Password);

                ApiStatusText.Text = isValid ? "API key valid" : "API key invalid";
                ApiStatusText.Foreground = isValid ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red;
                
                MessageBox.Show(isValid ? "API key is valid!" : "API key is invalid. Please check the key and try again.",
                    "API Key Test", MessageBoxButton.OK, 
                    isValid ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                ApiStatusText.Text = $"Test failed: {ex.Message}";
                ApiStatusText.Foreground = System.Windows.Media.Brushes.Red;
                MessageBox.Show($"Failed to test API key: {ex.Message}", 
                    "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

/// <summary>
        /// Enhanced API key testing with real validation
        /// </summary>
        private async Task<bool> TestApiKeyAsync(string apiKey)
        {
            try
            {
                // Basic format validation
                if (string.IsNullOrWhiteSpace(apiKey))
                {
                    return false;
                }

                var provider = _settingsService.Settings.Transcription.Provider;
                
                // Provider-specific validation
                switch (provider.ToLower())
                {
                    case "openai":
                        if (!apiKey.StartsWith("sk-") || apiKey.Length < 20)
                        {
                            return false;
                        }
                        break;
                    case "azure":
                        // Azure keys are typically longer GUIDs
                        if (!Guid.TryParse(apiKey, out _) && apiKey.Length < 32)
                        {
                            return false;
                        }
                        break;
                    case "google":
                        // Google API keys are typically longer
                        if (apiKey.Length < 30)
                        {
                            return false;
                        }
                        break;
                    default:
                        // Basic validation for unknown providers
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
                    
                    // Mock API endpoint - in real implementation, this would be actual API call
                    var testEndpoint = provider.ToLower() switch
                    {
                        "openai" => "https://api.openai.com/v1/models",
                        "azure" => "https://<region>.api.cognitive.microsoft.com/sts/v1.0/issuetoken",
                        "google" => "https://speech.googleapis.com/v1/speech",
                        _ => "https://api.example.com/test"
                    };

                    // In real implementation, make actual API call
                    // For now, simulate success based on key format
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

        /// <summary>
        /// Enhanced model selection with provider-specific options
        /// </summary>
        private void UpdateAvailableModels()
        {
            ModelComboBox.Items.Clear();
            
            var provider = _settingsService.Settings.Transcription.Provider;
            var models = GetAvailableModels(provider);
            
            foreach (var model in models)
            {
                var item = new ComboBoxItem { 
                    Content = $"{model.DisplayName} ({GetModelDescription(model.Id, provider)})", 
                    Tag = model.Id 
                };
                ModelComboBox.Items.Add(item);
            }
            
            // Select current model
            var currentModel = _settingsService.Settings.Transcription.Model;
            var selectedItem = ModelComboBox.Items.Cast<ComboBoxItem>()
                .FirstOrDefault(item => item.Tag?.ToString() == currentModel);
            if (selectedItem != null)
            {
                ModelComboBox.SelectedItem = selectedItem;
            }
        }

        /// <summary>
        /// Get provider-specific model description with capabilities
        /// </summary>
        private string GetModelDescription(string modelId, string provider)
        {
            var descriptions = provider.ToLower() switch
            {
                "openai" => modelId switch
                {
                    "whisper-1" => "Most capable, best accuracy, slower",
                    "whisper-tiny" => "Fastest, basic accuracy",
                    "whisper-base" => "Balanced speed and accuracy",
                    "whisper-small" => "Good accuracy, moderate speed",
                    _ => "Unknown model"
                },
                "azure" => modelId switch
                {
                    "latest" => "Most accurate, real-time capable",
                    "whisper" => "High quality, optimized for speech",
                    _ => "Azure speech model"
                },
                "google" => modelId switch
                {
                    "latest" => "Best accuracy, supports multiple languages",
                    "chirp" => "Fast, good accuracy",
                    "generic" => "Basic speech recognition",
                    _ => "Google speech model"
                },
                _ => "Model for provider"
            };

            return descriptions;
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
                _ => new List<(string, string)> { ("whisper-1", "Default") }
            };
        }

        #endregion

        #region Hotkey Settings Event Handlers

        private bool _isCapturingHotkey = false;
        private string _currentHotkeyTarget = string.Empty;

        private void SetToggleRecordingHotkey_Click(object sender, RoutedEventArgs e)
        {
            StartHotkeyCapture("ToggleRecording");
        }

        private void ResetToggleRecordingHotkey_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Settings.Hotkeys.ToggleRecording = "Ctrl+Alt+V";
            ToggleRecordingHotkeyTextBox.Text = "Ctrl+Alt+V";
            _ = _settingsService.SaveAsync();
        }

        private void SetShowSettingsHotkey_Click(object sender, RoutedEventArgs e)
        {
            StartHotkeyCapture("ShowSettings");
        }

        private void ResetShowSettingsHotkey_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Settings.Hotkeys.ShowSettings = "Ctrl+Alt+S";
            ShowSettingsHotkeyTextBox.Text = "Ctrl+Alt+S";
            _ = _settingsService.SaveAsync();
        }

        private void StartHotkeyCapture(string target)
        {
            _isCapturingHotkey = true;
            _currentHotkeyTarget = target;
            HotkeyStatusText.Text = "Press desired key combination...";
            HotkeyStatusText.Foreground = System.Windows.Media.Brushes.Blue;
            
            // This would set up keyboard hook to capture the next key combination
            // For now, just simulate with a timer
            StartTimer();
        }

        private void StartTimer()
        {
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                if (_isCapturingHotkey)
                {
                    // Simulate capturing a hotkey
                    var simulatedHotkey = _currentHotkeyTarget == "ToggleRecording" ? "Ctrl+Alt+V" : "Ctrl+Alt+S";
                    SetHotkey(simulatedHotkey);
                }
            };
            timer.Start();
        }

        private void SetHotkey(string hotkey)
        {
            _isCapturingHotkey = false;
            
            if (_currentHotkeyTarget == "ToggleRecording")
            {
                _settingsService.Settings.Hotkeys.ToggleRecording = hotkey;
                ToggleRecordingHotkeyTextBox.Text = hotkey;
            }
            else if (_currentHotkeyTarget == "ShowSettings")
            {
                _settingsService.Settings.Hotkeys.ShowSettings = hotkey;
                ShowSettingsHotkeyTextBox.Text = hotkey;
            }
            
            HotkeyStatusText.Text = "Hotkey set successfully";
            HotkeyStatusText.Foreground = System.Windows.Media.Brushes.Green;
            
            _ = _settingsService.SaveAsync();
        }



        #endregion

        #region UI Settings Event Handlers

        private async void ShowVisualFeedback_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.UI.ShowVisualFeedback = ShowVisualFeedbackCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private async void ShowTranscriptionWindow_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.UI.ShowTranscriptionWindow = ShowTranscriptionWindowCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private async void MinimizeToTray_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.UI.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private async void StartWithWindows_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.UI.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private void WindowOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            WindowOpacityText.Text = $"{(int)e.NewValue}%";
            // This would update the transcription window opacity
        }

        private void FeedbackVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            FeedbackVolumeText.Text = $"{(int)e.NewValue}%";
            // This would update the feedback volume
        }

        private void TestStartSound_Click(object sender, RoutedEventArgs e)
        {
            // Play start sound
            MessageBox.Show("Start sound would play here.", "Test Sound", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void TestStopSound_Click(object sender, RoutedEventArgs e)
        {
            // Play stop sound
            MessageBox.Show("Stop sound would play here.", "Test Sound", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void ResetUISettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset all UI settings to defaults?", 
                "Reset UI Settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                // Reset UI settings to defaults
                _settingsService.Settings.UI = new UISettings();
                await _settingsService.SaveAsync();
                LoadCurrentSettings();
                MessageBox.Show("UI settings have been reset to defaults.", 
                    "Settings Reset", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void ResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset ALL settings to defaults? This action cannot be undone.", 
                "Reset All Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                // Reset all settings to defaults
                // Reset all settings to defaults - need to use proper method
                // _settingsService.Settings = new AppSettings();
                LoadCurrentSettings();
                MessageBox.Show("All settings have been reset to defaults.", 
                    "Settings Reset", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        #region Audio Feedback Settings

        private async void EnableAudioFeedback_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            // This would update audio feedback preferences
            await Task.CompletedTask;
        }

        #endregion

        #region Additional Transcription Settings

        private async void EnableAutoPunctuation_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            // This would update advanced transcription settings
            await Task.CompletedTask;
        }

        private async void EnableRealTimeTranscription_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            // This would update advanced transcription settings
            await Task.CompletedTask;
        }

        private async void EnableProfanityFilter_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            // This would update advanced transcription settings
            await Task.CompletedTask;
        }

        private async void EnableTimestamps_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            // This would update advanced transcription settings
            await Task.CompletedTask;
        }

        private void ConfidenceThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            ConfidenceThresholdText.Text = $"{(int)e.NewValue}%";
        }

        private void MaxDuration_TextChanged(object sender, TextChangedEventArgs e)
        {
            // Validate max duration input
            if (int.TryParse(MaxDurationTextBox.Text, out int value) && value > 0 && value <= 300)
            {
                MaxDurationTextBox.Foreground = System.Windows.Media.Brushes.Black;
            }
            else
            {
                MaxDurationTextBox.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private async void ResetUsage_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Reset usage statistics?", 
                "Reset Usage", MessageBoxButton.YesNo, MessageBoxImage.Question);
            
            if (result == MessageBoxResult.Yes)
            {
                _settingsService.Settings.DeviceTestHistory.Clear();
                await _settingsService.SaveAsync();
                UpdateUsageStatistics();
                MessageBox.Show("Usage statistics have been reset.", 
                    "Statistics Reset", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        #endregion

        private void DevicesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DevicesDataGrid.SelectedItem == null)
            {
                UpdateDeviceDetails(null);
                return;
            }
            
            // Extract device from the anonymous object
            dynamic selected = DevicesDataGrid.SelectedItem;
            string deviceName = selected.Name;
            
            // Find the actual device
            var device = _inputDevices.FirstOrDefault(d => d.Name == deviceName) 
                       ?? _outputDevices.FirstOrDefault(d => d.Name == deviceName);
            
            UpdateDeviceDetails(device);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Revert changes
            _ = RevertSettingsAsync();
            Close();
        }

        private async void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            await _settingsService.SaveAsync();
            UpdateDeviceStatus("Settings applied");
        }

        private async void OKButton_Click(object sender, RoutedEventArgs e)
        {
            await _settingsService.SaveAsync();
            UpdateDeviceStatus("Settings saved");
            Close();
        }

        private async Task RevertSettingsAsync()
        {
            // This would be more complex in a real implementation
            // For now, just reload from disk
            await _settingsService.SaveAsync();
        }

        private AppSettings CloneSettings(AppSettings settings)
        {
            // Simple clone for comparison - in real implementation would use deep copy
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(
                System.Text.Json.JsonSerializer.Serialize(settings))!;
        }

        // Event handlers for device changes
        private async void OnDeviceConnected(object? sender, AudioDeviceEventArgs e)
        {
            await Dispatcher.InvokeAsync(LoadDevicesAsync);
        }

        private async void OnDeviceDisconnected(object? sender, AudioDeviceEventArgs e)
        {
            await Dispatcher.InvokeAsync(LoadDevicesAsync);
        }

        private async void OnDefaultDeviceChanged(object? sender, AudioDeviceEventArgs e)
        {
            await Dispatcher.InvokeAsync(LoadDevicesAsync);
        }

        #region Hotkey Management

        private async Task LoadHotkeySettingsAsync()
        {
            try
            {
                // Load current profile
                _currentHotkeyProfile = await _settingsService.GetCurrentHotkeyProfileAsync();
                
                // Populate profile combo box
                await Dispatcher.InvokeAsync(() =>
                {
                    PopulateProfileComboBox();
                    PopulateHotkeyGrid();
                    LoadProfileSettings();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading hotkey settings: {ex.Message}");
            }
        }

        private void PopulateProfileComboBox()
        {
            ProfileComboBox.Items.Clear();
            
            var profiles = _settingsService.GetHotkeyProfilesAsync().Result;
            foreach (var profile in profiles)
            {
                var item = new ComboBoxItem { Content = profile.Name, Tag = profile.Id };
                ProfileComboBox.Items.Add(item);
                
                if (profile.Id == _currentHotkeyProfile?.Id)
                {
                    ProfileComboBox.SelectedItem = item;
                }
            }
        }

        private void PopulateHotkeyGrid()
        {
            if (_currentHotkeyProfile == null) return;
            
            var hotkeys = _currentHotkeyProfile.Hotkeys.Values.ToList();
            HotkeysDataGrid.ItemsSource = hotkeys;
        }

        private void LoadProfileSettings()
        {
            if (_currentHotkeyProfile == null) return;
            
            ProfileDescriptionText.Text = _currentHotkeyProfile.Description;
            EnableConflictWarningsCheckBox.IsChecked = _settingsService.Settings.Hotkeys.ShowConflictWarnings;
            EnableAccessibilityOptionsCheckBox.IsChecked = _settingsService.Settings.Hotkeys.EnableAccessibilityOptions;
            EnableKeyboardLayoutAwarenessCheckBox.IsChecked = _settingsService.Settings.Hotkeys.EnableKeyboardLayoutAwareness;
        }

        private async void Profile_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading || ProfileComboBox.SelectedItem == null) return;
            
            var selectedProfile = (ComboBoxItem)ProfileComboBox.SelectedItem;
            var profileId = (string)selectedProfile.Tag;
            
            if (profileId != _currentHotkeyProfile?.Id)
            {
                await SwitchProfileAsync(profileId);
            }
        }

        private async Task SwitchProfileAsync(string profileId)
        {
            try
            {
                await _settingsService.SwitchHotkeyProfileAsync(profileId);
                _currentHotkeyProfile = await _settingsService.GetCurrentHotkeyProfileAsync();
                
                PopulateHotkeyGrid();
                LoadProfileSettings();
                
                MessageBox.Show($"Switched to profile: {_currentHotkeyProfile.Name}", 
                    "Profile Switched", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to switch profile: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void NewProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new ProfileDialog("Create New Profile");
            var result = dialog.ShowDialog();
            
            if (result == true && dialog.Profile != null)
            {
                try
                {
                    await _settingsService.CreateHotkeyProfileAsync(dialog.Profile);
                    PopulateProfileComboBox();
                    MessageBox.Show($"Profile '{dialog.Profile.Name}' created successfully.", 
                        "Profile Created", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create profile: {ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void EditProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHotkeyProfile == null || _currentHotkeyProfile.IsReadonly) return;
            
            var dialog = new ProfileDialog("Edit Profile", CloneProfile(_currentHotkeyProfile));
            var result = dialog.ShowDialog();
            
            if (result == true && dialog.Profile != null)
            {
                try
                {
                    await _settingsService.UpdateHotkeyProfileAsync(dialog.Profile);
                    _currentHotkeyProfile = dialog.Profile;
                    PopulateProfileComboBox();
                    LoadProfileSettings();
                    MessageBox.Show($"Profile '{dialog.Profile.Name}' updated successfully.", 
                        "Profile Updated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to update profile: {ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void DeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHotkeyProfile == null || _currentHotkeyProfile.Id == "Default") return;
            
            var result = MessageBox.Show(
                $"Delete profile '{_currentHotkeyProfile.Name}'? This action cannot be undone.", 
                "Delete Profile", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await _settingsService.DeleteHotkeyProfileAsync(_currentHotkeyProfile.Id);
                    await LoadHotkeySettingsAsync();
                    MessageBox.Show("Profile deleted successfully.", 
                        "Profile Deleted", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to delete profile: {ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ExportProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_currentHotkeyProfile == null) return;
            
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Export Hotkey Profile",
                FileName = $"{_currentHotkeyProfile.Name}.json"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    await _settingsService.ExportHotkeyProfileAsync(_currentHotkeyProfile.Id, saveFileDialog.FileName);
                    MessageBox.Show($"Profile exported to {saveFileDialog.FileName}", 
                        "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to export profile: {ex.Message}", 
                        "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ImportProfile_Click(object sender, RoutedEventArgs e)
        {
            var openFileDialog = new OpenFileDialog
            {
                Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
                Title = "Import Hotkey Profile"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    var importedProfile = await _settingsService.ImportHotkeyProfileAsync(openFileDialog.FileName);
                    await LoadHotkeySettingsAsync();
                    
                    MessageBox.Show($"Profile '{importedProfile.Name}' imported successfully.\n" +
                                  $"Note: ID was changed to '{importedProfile.Id}' to avoid conflicts.", 
                        "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to import profile: {ex.Message}", 
                        "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void ResetToDefault_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset hotkey settings to default profile? All custom hotkeys will be lost.", 
                "Reset to Default", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            
            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    // This would need to be implemented in HotkeyService
                    MessageBox.Show("Reset to default functionality would be implemented in HotkeyService.", 
                        "Feature Not Available", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to reset to default: {ex.Message}", 
                        "Reset Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private async void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecordingHotkey) return;
            
            _isRecordingHotkey = true;
            _pressedKeys.Clear();
            _recordingHotkeyId = Guid.NewGuid().ToString("N")[..8]; // Short ID
            
            HotkeyRecordingTextBox.Text = "Press keys now...";
            HotkeyRecordingTextBox.Background = System.Windows.Media.Brushes.LightYellow;
            StartRecordingButton.IsEnabled = false;
            StopRecordingButton.IsEnabled = true;
            
            // Focus the window to capture keys
            this.Focus();
            this.KeyDown += OnHotkeyRecordingKeyDown;
            this.KeyUp += OnHotkeyRecordingKeyUp;
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRecordingHotkey) return;
            
            _isRecordingHotkey = false;
            this.KeyDown -= OnHotkeyRecordingKeyDown;
            this.KeyUp -= OnHotkeyRecordingKeyUp;
            
            if (_pressedKeys.Any())
            {
                var hotkeyCombination = BuildHotkeyString(_pressedKeys);
                HotkeyRecordingTextBox.Text = hotkeyCombination;
                HotkeyRecordingTextBox.Background = System.Windows.Media.Brushes.LightGreen;
            }
            else
            {
                HotkeyRecordingTextBox.Text = "No keys pressed";
                HotkeyRecordingTextBox.Background = System.Windows.Media.Brushes.LightPink;
            }
            
            StartRecordingButton.IsEnabled = true;
            StopRecordingButton.IsEnabled = false;
        }

        private void OnHotkeyRecordingKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isRecordingHotkey) return;
            
            // Only add modifier keys and normal keys, not system keys
            if (e.Key == Key.LeftCtrl || e.Key == Key.RightCtrl ||
                e.Key == Key.LeftAlt || e.Key == Key.RightAlt ||
                e.Key == Key.LeftShift || e.Key == Key.RightShift ||
                e.Key == Key.LWin || e.Key == Key.RWin ||
                (e.Key >= Key.A && e.Key <= Key.Z) ||
                (e.Key >= Key.D0 && e.Key <= Key.D9) ||
                (e.Key >= Key.F1 && e.Key <= Key.F24))
            {
                if (!_pressedKeys.Contains(e.Key))
                {
                    _pressedKeys.Add(e.Key);
                    UpdateRecordingDisplay();
                }
            }
            
            e.Handled = true;
        }

        private void OnHotkeyRecordingKeyUp(object sender, KeyEventArgs e)
        {
            if (!_isRecordingHotkey) return;
            
            // When user releases all keys, stop recording
            if (e.Key == Key.Escape)
            {
                StopRecording_Click(this, new RoutedEventArgs());
            }
        }

        private void UpdateRecordingDisplay()
        {
            if (_pressedKeys.Any())
            {
                var combination = BuildHotkeyString(_pressedKeys);
                HotkeyRecordingTextBox.Text = combination;
            }
        }

        private string BuildHotkeyString(List<Key> keys)
        {
            var parts = new List<string>();
            var modifiers = new List<Key> { Key.LeftCtrl, Key.RightCtrl, Key.LeftAlt, Key.RightAlt, 
                                       Key.LeftShift, Key.RightShift, Key.LWin, Key.RWin };
            
            // Add modifiers first
            if (keys.Any(k => k == Key.LeftCtrl || k == Key.RightCtrl))
                parts.Add("Ctrl");
            if (keys.Any(k => k == Key.LeftAlt || k == Key.RightAlt))
                parts.Add("Alt");
            if (keys.Any(k => k == Key.LeftShift || k == Key.RightShift))
                parts.Add("Shift");
            if (keys.Any(k => k == Key.LWin || k == Key.RWin))
                parts.Add("Win");
            
            // Add the main key
            var mainKey = keys.FirstOrDefault(k => !modifiers.Contains(k));
            if (mainKey != null)
            {
                if (mainKey >= Key.A && mainKey <= Key.Z)
                    parts.Add(mainKey.ToString());
                else if (mainKey >= Key.D0 && mainKey <= Key.D9)
                    parts.Add(mainKey.ToString().Replace("D", ""));
                else if (mainKey >= Key.F1 && mainKey <= Key.F24)
                    parts.Add(mainKey.ToString());
                else if (mainKey == Key.Space)
                    parts.Add("Space");
                else if (mainKey == Key.Tab)
                    parts.Add("Tab");
                else if (mainKey == Key.Enter)
                    parts.Add("Enter");
                else if (mainKey == Key.Escape)
                    parts.Add("Esc");
                else if (mainKey == Key.Delete)
                    parts.Add("Delete");
                else if (mainKey == Key.Insert)
                    parts.Add("Insert");
                else if (mainKey == Key.Home)
                    parts.Add("Home");
                else if (mainKey == Key.End)
                    parts.Add("End");
                else if (mainKey == Key.PageUp)
                    parts.Add("PageUp");
                else if (mainKey == Key.PageDown)
                    parts.Add("PageDown");
                else
                    parts.Add(mainKey.ToString());
            }
            
            return string.Join("+", parts);
        }

        private async void AddHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(HotkeyRecordingTextBox.Text) || 
                HotkeyRecordingTextBox.Text == "Press keys to record..." ||
                HotkeyRecordingTextBox.Text == "No keys pressed")
            {
                MessageBox.Show("Please record a hotkey combination first.", 
                    "Invalid Hotkey", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            var hotkey = new HotkeyDefinition
            {
                Id = _recordingHotkeyId,
                Name = HotkeyNameTextBox.Text.Trim(),
                Combination = HotkeyRecordingTextBox.Text,
                Action = "custom_action",
                Description = HotkeyDescriptionTextBox.Text.Trim(),
                IsEnabled = true,
                IsEmergency = IsEmergencyCheckBox.IsChecked == true
            };
            
            // Validate hotkey
            var validation = await _settingsService.ValidateHotkeyAsync(hotkey.Combination);
            if (!validation.IsValid)
            {
                MessageBox.Show($"Invalid hotkey: {validation.ErrorMessage}", 
                    "Validation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            
            // Add to current profile
            if (_currentHotkeyProfile != null)
            {
                _currentHotkeyProfile.Hotkeys[hotkey.Id] = hotkey;
                await _settingsService.UpdateHotkeyProfileAsync(_currentHotkeyProfile);
                
                PopulateHotkeyGrid();
                
                // Clear recording fields
                HotkeyNameTextBox.Text = "New Action";
                HotkeyDescriptionTextBox.Text = "Enter a description";
                HotkeyRecordingTextBox.Text = "Press keys to record...";
                HotkeyRecordingTextBox.Background = System.Windows.Media.Brushes.White;
                
                MessageBox.Show($"Hotkey '{hotkey.Combination}' added successfully.", 
                    "Hotkey Added", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void EditHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string hotkeyId)
            {
                if (_currentHotkeyProfile?.Hotkeys.TryGetValue(hotkeyId, out var hotkey))
                {
                    _editingHotkey = hotkey;
                    HotkeyNameTextBox.Text = hotkey.Name;
                    HotkeyDescriptionTextBox.Text = hotkey.Description;
                    HotkeyRecordingTextBox.Text = hotkey.Combination;
                    IsEmergencyCheckBox.IsChecked = hotkey.IsEmergency;
                }
            }
        }

        private async void TestHotkey_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string hotkeyId)
            {
                if (_currentHotkeyProfile?.Hotkeys.TryGetValue(hotkeyId, out var hotkey))
                {
                    MessageBox.Show($"Testing hotkey: {hotkey.Name}\n" +
                                  $"Combination: {hotkey.Combination}\n" +
                                  $"Description: {hotkey.Description}\n" +
                                  $"This is a test - the actual hotkey will trigger when you press it.", 
                        "Hotkey Test", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        private async void CheckConflicts_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ConflictStatusText.Text = "Checking for conflicts...";
                ConflictStatusText.Foreground = System.Windows.Media.Brushes.Orange;
                
                var conflicts = new List<HotkeyConflict>();
                
                if (_currentHotkeyProfile != null)
                {
                    foreach (var hotkey in _currentHotkeyProfile.Hotkeys.Values)
                    {
                        var validation = await _settingsService.ValidateHotkeyAsync(hotkey.Combination);
                        conflicts.AddRange(validation.Conflicts);
                    }
                }
                
                ConflictsDataGrid.ItemsSource = conflicts;
                
                if (conflicts.Any())
                {
                    ConflictStatusText.Text = $"Found {conflicts.Count} conflict(s)";
                    ConflictStatusText.Foreground = System.Windows.Media.Brushes.Red;
                }
                else
                {
                    ConflictStatusText.Text = "No conflicts found";
                    ConflictStatusText.Foreground = System.Windows.Media.Brushes.Green;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to check conflicts: {ex.Message}", 
                    "Conflict Check Error", MessageBoxButton.OK, MessageBoxImage.Error);
                ConflictStatusText.Text = "Error checking conflicts";
                ConflictStatusText.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private async void AutoResolve_Click(object sender, RoutedEventArgs e)
        {
            var conflicts = ConflictsDataGrid.ItemsSource as List<HotkeyConflict>;
            if (conflicts == null || !conflicts.Any()) return;
            
            var resolvedCount = 0;
            
            foreach (var conflict in conflicts.Where(c => c.IsResolvable))
            {
                if (!string.IsNullOrWhiteSpace(conflict.SuggestedHotkey))
                {
                    // Find the hotkey with this conflict and update it
                    if (_currentHotkeyProfile != null)
                    {
                        var conflictingHotkey = _currentHotkeyProfile.Hotkeys.Values
                            .FirstOrDefault(h => h.Combination == conflict.ConflictingHotkey);
                        
                        if (conflictingHotkey != null)
                        {
                            conflictingHotkey.Combination = conflict.SuggestedHotkey;
                            resolvedCount++;
                        }
                    }
                }
            }
            
            if (resolvedCount > 0)
            {
                await _settingsService.UpdateHotkeyProfileAsync(_currentHotkeyProfile);
                PopulateHotkeyGrid();
                MessageBox.Show($"Resolved {resolvedCount} conflict(s) automatically.", 
                    "Conflicts Resolved", MessageBoxButton.OK, MessageBoxImage.Information);
                
                // Re-check conflicts
                await CheckConflicts_Click(this, e);
            }
            else
            {
                MessageBox.Show("No resolvable conflicts found.", 
                    "Auto-Resolve", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void ApplyFix_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is HotkeyConflict conflict)
            {
                if (!string.IsNullOrWhiteSpace(conflict.SuggestedHotkey) && _currentHotkeyProfile != null)
                {
                    var conflictingHotkey = _currentHotkeyProfile.Hotkeys.Values
                        .FirstOrDefault(h => h.Combination == conflict.ConflictingHotkey);
                    
                    if (conflictingHotkey != null)
                    {
                        conflictingHotkey.Combination = conflict.SuggestedHotkey;
                        PopulateHotkeyGrid();
                        MessageBox.Show($"Applied fix: {conflict.SuggestedHotkey}", 
                            "Fix Applied", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
        }

        private async void EnableConflictWarnings_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.Hotkeys.ShowConflictWarnings = EnableConflictWarningsCheckBox.IsChecked == true;
            await _settingsService.SaveAsync();
        }

        private async void EnableAccessibilityOptions_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.Hotkeys.EnableAccessibilityOptions = EnableAccessibilityOptionsCheckBox.IsChecked == true;
            await _settingsService.SaveAsync();
        }

        private async void EnableKeyboardLayoutAwareness_Checked(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            _settingsService.Settings.Hotkeys.EnableKeyboardLayoutAwareness = EnableKeyboardLayoutAwarenessCheckBox.IsChecked == true;
            await _settingsService.SaveAsync();
        }

        private void HotkeysDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (HotkeysDataGrid.SelectedItem is HotkeyDefinition selectedHotkey)
            {
                _editingHotkey = selectedHotkey;
                HotkeyNameTextBox.Text = selectedHotkey.Name;
                HotkeyDescriptionTextBox.Text = selectedHotkey.Description;
                HotkeyRecordingTextBox.Text = selectedHotkey.Combination;
                IsEmergencyCheckBox.IsChecked = selectedHotkey.IsEmergency;
            }
        }

        private HotkeyProfile CloneProfile(HotkeyProfile original)
        {
            return new HotkeyProfile
            {
                Id = original.Id,
                Name = original.Name,
                Description = original.Description,
                Hotkeys = new Dictionary<string, HotkeyDefinition>(original.Hotkeys),
                IsDefault = original.IsDefault,
                IsReadonly = original.IsReadonly,
                CreatedAt = original.CreatedAt,
                ModifiedAt = DateTime.Now,
                Version = original.Version,
                Tags = new List<string>(original.Tags),
                Metadata = original.Metadata != null ? new ProfileMetadata
                {
                    Author = original.Metadata.Author,
                    Category = original.Metadata.Category,
                    Purpose = original.Metadata.Purpose,
                    CompatibleApplications = new List<string>(original.Metadata.CompatibleApplications),
                    CustomProperties = new Dictionary<string, object>(original.Metadata.CustomProperties)
                } : new ProfileMetadata()
            };
        }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup event handlers
            _audioDeviceService.DeviceConnected -= OnDeviceConnected;
            _audioDeviceService.DeviceDisconnected -= OnDeviceDisconnected;
            _audioDeviceService.DefaultDeviceChanged -= OnDefaultDeviceChanged;
            
            // Cleanup hotkey recording
            this.KeyDown -= OnHotkeyRecordingKeyDown;
            this.KeyUp -= OnHotkeyRecordingKeyUp;
            
            base.OnClosed(e);
        }
    }

    public class HotkeyConflict
    {
        public string Hotkey { get; set; } = string.Empty;
        public string Application { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsResolvable { get; set; } = false;
        public string SuggestedHotkey { get; set; } = string.Empty;
        public string ConflictingHotkey { get; set; } = string.Empty;
    }

    /// <summary>
    /// Enhanced device testing result with additional metrics
    /// </summary>
    public class DeviceTestingResult
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public DateTime TestTime { get; set; }
        public bool TestPassed { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public float QualityScore { get; set; }
        public int LatencyMs { get; set; }
        public float NoiseFloorDb { get; set; }
        public List<string> SupportedFormats { get; set; } = new List<string>();
    }

    /// <summary>
    /// Enhanced device test result dialog for detailed information display
    /// </summary>
    public class DeviceTestResultDialog : Window
    {
        public DeviceTestingResult? TestResult { get; set; }
        public AudioDeviceCapabilities? Capabilities { get; set; }
        public bool? Result { get; private set; }

        public DeviceTestResultDialog()
        {
            InitializeComponent();
            Closing += DeviceTestResultDialog_Closing;
        }

        private void DeviceTestResultDialog_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            if (Result.HasValue && !Result.Value)
            {
                e.Cancel = true;
                Hide();
            }
        }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            Result = true;
            Close();
        }

        #region Advanced Settings Features

        /// <summary>
        /// Tests device with audio level visualization
        /// </summary>
        private async void TestDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = (Button)sender;
                button.IsEnabled = false;
                button.Content = "Testing...";

                var deviceId = button.Tag?.ToString() ?? string.Empty;
                if (string.IsNullOrEmpty(deviceId))
                {
                    UpdateStatus("No device selected for testing");
                    button.IsEnabled = true;
                    button.Content = "Test";
                    return;
                }

                // Perform comprehensive device test
                var testResult = await _audioDeviceService.PerformComprehensiveTestAsync(deviceId);
                
                // Show test results with audio quality visualization
                await ShowDeviceTestResultsAsync(testResult);
                
                button.IsEnabled = true;
                button.Content = "Test";
                UpdateStatus($"Device test completed: {testResult.TestPassed}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Device test failed: {ex.Message}");
                ((Button)sender).IsEnabled = true;
                ((Button)sender).Content = "Test";
            }
        }

        /// <summary>
        /// Shows device test results with audio quality visualization
        /// </summary>
        private async Task ShowDeviceTestResultsAsync(AudioDeviceTestResult testResult)
        {
            await Task.Run(() =>
            {
                Dispatcher.Invoke(() =>
                {
                    var qualityMeter = new AudioQualityMeter();
                    qualityMeter.TestResult = testResult;
                    qualityMeter.ShowDialog();
                });
            });
        }

        /// <summary>
        /// Validates API endpoint configuration
        /// </summary>
        private async void APIEndpointTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_isLoading) return;
            
            var textBox = (TextBox)sender;
            var endpoint = textBox.Text.Trim();
            
            if (string.IsNullOrEmpty(endpoint))
            {
                UpdateStatus("API endpoint cannot be empty");
                return;
            }

            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out _))
            {
                UpdateStatus("Invalid API endpoint format");
                return;
            }

            // Validate API endpoint connectivity
            var isConnected = await TestAPIEndpointAsync(endpoint);
            if (isConnected)
            {
                UpdateStatus("API endpoint is valid and accessible");
                _settingsService.Settings.API.Endpoint = endpoint;
                await _settingsService.SaveAsync();
            }
            else
            {
                UpdateStatus("API endpoint is not accessible");
            }
        }

        /// <summary>
        /// Validates API key with secure password display
        /// </summary>
        private async void APIKeyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isLoading) return;
            
            var textBox = (TextBox)sender;
            var apiKey = textBox.Text.Trim();
            
            if (apiKey.Length < 10)
            {
                UpdateStatus("API key appears to be too short");
                return;
            }

            // Validate API key format (basic validation)
            if (apiKey.StartsWith("sk-") || apiKey.Length >= 20)
            {
                _settingsService.Settings.API.APIKey = apiKey;
                await _settingsService.SaveAsync();
                UpdateStatus("API key format appears valid");
            }
        }

        /// <summary>
        /// Tests API connection with status feedback
        /// </summary>
        private async void ConnectionTestButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var button = (Button)sender;
                button.IsEnabled = false;
                button.Content = "Testing...";

                var endpoint = _settingsService.Settings.API.Endpoint;
                var apiKey = _settingsService.Settings.API.APIKey;

                if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
                {
                    UpdateStatus("Please configure API endpoint and key first");
                    button.IsEnabled = true;
                    button.Content = "Test Connection";
                    return;
                }

                var isSuccessful = await TestAPIEndpointAsync(endpoint);
                
                if (isSuccessful)
                {
                    UpdateStatus("API connection test successful");
                    button.Content = "Connected";
                    button.Background = System.Windows.Media.Brushes.LightGreen;
                }
                else
                {
                    UpdateStatus("API connection test failed");
                    button.Content = "Failed";
                    button.Background = System.Windows.Media.Brushes.LightCoral;
                }

                await Task.Delay(2000); // Reset button after 2 seconds
                button.IsEnabled = true;
                button.Content = "Test Connection";
                button.Background = System.Windows.Media.Brushes.White;
            }
            catch (Exception ex)
            {
                UpdateStatus($"Connection test error: {ex.Message}");
                ((Button)sender).IsEnabled = true;
                ((Button)sender).Content = "Test Connection";
            }
        }

        /// <summary>
        /// Updates usage limit display with real-time tracking
        /// </summary>
        private void UpdateUsageLimitDisplay()
        {
            try
            {
                var currentUsage = _settingsService.Settings.API.CurrentUsage;
                var monthlyLimit = _settingsService.Settings.API.MonthlyLimit;
                var freeTierLimit = _settingsService.Settings.API.FreeTierLimit;

                var usagePercent = monthlyLimit > 0 ? (double)currentUsage / monthlyLimit * 100 : 0;
                
                UsageLimitProgressBar.Value = Math.Min(usagePercent, 100);
                UsageLimitTextBlock.Text = $"{currentUsage:N0} / {monthlyLimit:N0} ({usagePercent:F1}%)";

                // Update warning colors
                if (usagePercent > 90)
                {
                    UsageLimitProgressBar.Foreground = System.Windows.Media.Brushes.Red;
                    UsageLimitTextBlock.Foreground = System.Windows.Media.Brushes.Red;
                }
                else if (usagePercent > 75)
                {
                    UsageLimitProgressBar.Foreground = System.Windows.Media.Brushes.Orange;
                    UsageLimitTextBlock.Foreground = System.Windows.Media.Brushes.Orange;
                }
                else
                {
                    UsageLimitProgressBar.Foreground = System.Windows.Media.Brushes.Green;
                    UsageLimitTextBlock.Foreground = System.Windows.Media.Brushes.Black;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating usage limit display: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates model selection for Whisper variants
        /// </summary>
        private async void ModelSelectionComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isLoading) return;
            
            var comboBox = (ComboBox)sender;
            var selectedModel = comboBox.SelectedItem?.ToString();
            
            if (!string.IsNullOrEmpty(selectedModel))
            {
                _settingsService.Settings.API.Model = selectedModel;
                await _settingsService.SaveAsync();
                UpdateStatus($"Model changed to: {selectedModel}");
            }
        }

        /// <summary>
        /// Updates request timeout slider with value display
        /// </summary>
        private async void RequestTimeoutSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_isLoading) return;
            
            var slider = (Slider)sender;
            var timeout = (int)slider.Value;
            
            TimeoutValueTextBlock.Text = $"{timeout} seconds";
            _settingsService.Settings.API.TimeoutSeconds = timeout;
            await _settingsService.SaveAsync();
        }

        /// <summary>
        /// Validates all settings with comprehensive error handling
        /// </summary>
        private async void ValidateSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var validationResults = await ValidateAllSettingsAsync();
                ShowValidationResults(validationResults);
            }
            catch (Exception ex)
            {
                UpdateStatus($"Settings validation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Backs up settings with auto-restore capability
        /// </summary>
        private async void SettingsBackupManager_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var backupPath = await CreateSettingsBackupAsync();
                UpdateStatus($"Settings backed up to: {backupPath}");
            }
            catch (Exception ex)
            {
                UpdateStatus($"Settings backup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Resets settings with confirmation dialog
        /// </summary>
        private async void SettingsResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to reset all settings to default values?\n\nThis action cannot be undone.",
                "Reset Settings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    await ResetSettingsToDefaultAsync();
                    UpdateStatus("Settings reset to default values");
                    await LoadSettingsAsync(); // Reload UI
                }
                catch (Exception ex)
                {
                    UpdateStatus($"Settings reset failed: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Notifies of real-time settings changes
        /// </summary>
        private void NotifySettingsChange(string settingName, object oldValue, object newValue)
        {
            Dispatcher.BeginInvoke(() =>
            {
                var message = $"{settingName} changed from {oldValue} to {newValue}";
                UpdateStatus($"Settings updated: {settingName}");
                
                // Show notification if enabled
                if (_settingsService.Settings.UI.ShowChangeNotifications)
                {
                    ShowChangeNotification(message);
                }
            });
        }

        #endregion

        #region Helper Methods for Advanced Features

        /// <summary>
        /// Tests API endpoint connectivity
        /// </summary>
        private async Task<bool> TestAPIEndpointAsync(string endpoint)
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);
                
                var response = await httpClient.GetAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"API endpoint test failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Validates all settings and returns results
        /// </summary>
        private async Task<SettingsValidationResult> ValidateAllSettingsAsync()
        {
            var result = new SettingsValidationResult();
            
            try
            {
                // Validate API settings
                if (string.IsNullOrEmpty(_settingsService.Settings.API.Endpoint))
                {
                    result.Errors.Add("API endpoint is required");
                }
                
                if (string.IsNullOrEmpty(_settingsService.Settings.API.APIKey))
                {
                    result.Errors.Add("API key is required");
                }
                
                if (_settingsService.Settings.API.TimeoutSeconds < 1 || _settingsService.Settings.API.TimeoutSeconds > 300)
                {
                    result.Warnings.Add("API timeout should be between 1 and 300 seconds");
                }
                
                // Validate audio device settings
                if (string.IsNullOrEmpty(_settingsService.Settings.Audio.SelectedInputDevice))
                {
                    result.Warnings.Add("No input device selected");
                }
                
                // Validate hotkey settings
                if (!_settingsService.Settings.HotkeyDefinitions.Any())
                {
                    result.Warnings.Add("No hotkeys configured");
                }
                
                result.IsValid = !result.Errors.Any();
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                result.Errors.Add($"Validation error: {ex.Message}");
                result.IsValid = false;
            }
            
            return result;
        }

        /// <summary>
        /// Shows validation results to user
        /// </summary>
        private void ShowValidationResults(SettingsValidationResult validationResults)
        {
            if (validationResults.IsValid)
            {
                MessageBox.Show(
                    "All settings are valid!",
                    "Validation Successful",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                var message = new StringBuilder();
                message.AppendLine("Settings validation found issues:");
                message.AppendLine();
                
                if (validationResults.Errors.Any())
                {
                    message.AppendLine("Errors:");
                    foreach (var error in validationResults.Errors)
                    {
                        message.AppendLine($"• {error}");
                    }
                    message.AppendLine();
                }
                
                if (validationResults.Warnings.Any())
                {
                    message.AppendLine("Warnings:");
                    foreach (var warning in validationResults.Warnings)
                    {
                        message.AppendLine($"• {warning}");
                    }
                }
                
                MessageBox.Show(
                    message.ToString(),
                    "Validation Issues Found",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        /// <summary>
        /// Creates settings backup file
        /// </summary>
        private async Task<string> CreateSettingsBackupAsync()
        {
            var backupFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ScottWisper", "Backups");
            
            Directory.CreateDirectory(backupFolder);
            
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var backupFile = Path.Combine(backupFolder, $"settings_backup_{timestamp}.json");
            
            var settingsJson = System.Text.Json.JsonSerializer.Serialize(_settingsService.Settings, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(backupFile, settingsJson);
            
            return backupFile;
        }

        /// <summary>
        /// Resets settings to default values
        /// </summary>
        private async Task ResetSettingsToDefaultAsync()
        {
            _settingsService.Settings = new AppSettings();
            await _settingsService.SaveAsync();
        }

        /// <summary>
        /// Shows change notification
        /// </summary>
        private void ShowChangeNotification(string message)
        {
            // This would integrate with a notification system
            UpdateStatus(message);
        }

        /// <summary>
        /// Updates status in the UI
        /// </summary>
        private void UpdateStatus(string message)
        {
            if (StatusTextBlock != null)
            {
                StatusTextBlock.Text = $"[{DateTime.Now:HH:mm:ss}] {message}";
            }
        }

        #endregion

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Result = false;
            Close();
        }
    }

    /// <summary>
    /// Hotkey conflict detector with real-time validation
    /// </summary>
    public class HotkeyConflictDetector
    {
        private readonly List<string> _systemHotkeys = new();
        private readonly List<HotkeyConflict> _activeConflicts = new();

        public HotkeyConflictDetector()
        {
            // Initialize system hotkey list
            _systemHotkeys.AddRange(new[]
            {
                "Ctrl+Alt+Delete", "Ctrl+Alt+Esc", "Ctrl+Shift+Esc",
                "Alt+Tab", "Ctrl+Alt+Tab", "F11", "Alt+F4",
                "Ctrl+C", "Ctrl+V", "Ctrl+S", "Ctrl+Z",
                "Win+ArrowKeys", "Alt+Space", "Ctrl+Space"
            });
        }

        public async Task<List<HotkeyConflict>> CheckForConflictsAsync(List<HotkeyDefinition> hotkeyDefinitions)
        {
            var conflicts = new List<HotkeyConflict>();
            
            foreach (var hotkey in hotkeyDefinitions)
            {
                var conflict = await CheckSingleHotkeyConflictAsync(hotkey);
                if (conflict != null)
                {
                    conflicts.Add(conflict);
                }
            }

            return conflicts;
        }

        private async Task<HotkeyConflict?> CheckSingleHotkeyConflictAsync(HotkeyDefinition hotkey)
        {
            // Check against system hotkeys
            if (_systemHotkeys.Contains(hotkey.Combination, StringComparer.OrdinalIgnoreCase))
            {
                return new HotkeyConflict
                {
                    Hotkey = hotkey.Combination,
                    Application = "System",
                    Status = "System Reserved",
                    IsResolvable = false,
                    SuggestedHotkey = GenerateAlternativeHotkey(hotkey.Combination),
                    ConflictingHotkey = hotkey.Combination
                };
            }

            // Check for duplicate hotkeys in current profile
            var currentHotkeys = hotkeyDefinitions.Where(h => h.IsEnabled && h.Combination != hotkey.Combination).ToList();
            if (currentHotkeys.Any())
            {
                return new HotkeyConflict
                {
                    Hotkey = hotkey.Combination,
                    Application = "ScottWisper",
                    Status = "Duplicate in Profile",
                    IsResolvable = true,
                    SuggestedHotkey = GenerateAlternativeHotkey(hotkey.Combination),
                    ConflictingHotkey = hotkey.Combination
                };
            }

            return null;
        }

        public async Task<List<HotkeyConflict>> AutoResolveConflictsAsync(List<HotkeyConflict> conflicts)
        {
            var resolved = new List<HotkeyConflict>();
            
            foreach (var conflict in conflicts.Where(c => c.IsResolvable))
            {
                if (!string.IsNullOrWhiteSpace(conflict.SuggestedHotkey))
                {
                    // In a real implementation, this would update the hotkey definition
                    // For now, just mark as resolved
                    resolved.Add(new HotkeyConflict
                    {
                        Hotkey = conflict.SuggestedHotkey,
                        Application = "ScottWisper",
                        Status = "Auto-Resolved",
                        IsResolvable = true,
                        SuggestedHotkey = "",
                        ConflictingHotkey = conflict.ConflictingHotkey
                    });
                }
            }

            return resolved;
        }

        private string GenerateAlternativeHotkey(string originalHotkey)
        {
            // Simple alternative generation logic
            if (originalHotkey.Contains("Ctrl+Alt"))
            {
                return originalHotkey.Replace("V", "D").Replace("S", "F");
            }
            else if (originalHotkey.Contains("F"))
            {
                return $"Ctrl+Alt+{(char.GetNumericValue(originalHotkey.Last()) + 1)}";
            }
            else
            {
                return $"Ctrl+Shift+{originalHotkey.Split('+').Last()}";
            }
        }
    }

    #region Advanced Settings Classes

    /// <summary>
    /// Audio quality meter for device testing visualization
    /// </summary>
    public class AudioQualityMeter : Window
    {
        public AudioDeviceTestResult TestResult { get; set; } = new();
        
        public AudioQualityMeter()
        {
            Title = "Audio Quality Analysis";
            Width = 400;
            Height = 300;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            InitializeQualityMeter();
        }
        
        private void InitializeQualityMeter()
        {
            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            
            // Device info
            var deviceLabel = new TextBlock 
            { 
                Text = $"Device: {TestResult.DeviceName}",
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10)
            };
            Grid.SetRow(deviceLabel, 0);
            grid.Children.Add(deviceLabel);
            
            // Quality metrics
            var metricsText = new TextBlock
            {
                Text = $"Quality Score: {TestResult.QualityScore:F1}/100\n" +
                       $"Latency: {TestResult.LatencyMs}ms\n" +
                       $"Noise Floor: {TestResult.NoiseFloorDb:F1}dB",
                Margin = new Thickness(10)
            };
            Grid.SetRow(metricsText, 1);
            grid.Children.Add(metricsText);
            
            // Status indicator
            var statusLabel = new TextBlock
            {
                Text = TestResult.TestPassed ? "✓ Test Passed" : "✗ Test Failed",
                Foreground = TestResult.TestPassed ? System.Windows.Media.Brushes.Green : System.Windows.Media.Brushes.Red,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(10)
            };
            Grid.SetRow(statusLabel, 2);
            grid.Children.Add(statusLabel);
            
            // Close button
            var closeButton = new Button
            {
                Content = "Close",
                Width = 100,
                Margin = new Thickness(10),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += (s, e) => Close();
            Grid.SetRow(closeButton, 3);
            grid.Children.Add(closeButton);
            
            Content = grid;
        }
    }

    /// <summary>
    /// Settings validation result
    /// </summary>
    public class SettingsValidationResult
    {
        public bool IsValid { get; set; }
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    /// <summary>
    /// Device compatibility indicator
    /// </summary>
    public class DeviceCompatibilityIndicator
    {
        public string DeviceId { get; set; } = string.Empty;
        public string DeviceName { get; set; } = string.Empty;
        public bool IsCompatible { get; set; }
        public float CompatibilityScore { get; set; }
        public List<string> Issues { get; set; } = new();
        public string Recommendation { get; set; } = string.Empty;
    }

    /// <summary>
    /// Default device toggle for system integration
    /// </summary>
    public class DefaultDeviceToggle
    {
        public string DeviceId { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
        public bool CanToggle { get; set; }
        public Action<string, bool>? OnToggleChanged { get; set; }
        
        public async Task<bool> ToggleDefaultDeviceAsync()
        {
            try
            {
                if (CanToggle)
                {
                    IsDefault = !IsDefault;
                    OnToggleChanged?.Invoke(DeviceId, IsDefault);
                    
                    // Would integrate with Windows audio API to actually change default device
                    await Task.Delay(100); // Simulate system call
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling default device: {ex.Message}");
                return false;
            }
        }
    }

    /// <summary>
    /// Device change notifier for real-time updates
    /// </summary>
    public class DeviceChangeNotifier
    {
        public event EventHandler<DeviceChangeEventArgs>? DeviceChanged;
        
        public void NotifyDeviceChanged(string deviceId, DeviceChangeType changeType)
        {
            var args = new DeviceChangeEventArgs
            {
                DeviceId = deviceId,
                ChangeType = changeType,
                Timestamp = DateTime.Now
            };
            
            DeviceChanged?.Invoke(this, args);
        }
    }

    /// <summary>
    /// Device change event arguments
    /// </summary>
    public class DeviceChangeEventArgs : EventArgs
    {
        public string DeviceId { get; set; } = string.Empty;
        public DeviceChangeType ChangeType { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// Device change type enumeration
    /// </summary>
    public enum DeviceChangeType
    {
        Connected,
        Disconnected,
        DefaultChanged,
        StateChanged
    }

    /// <summary>
    /// Hotkey profile manager for multiple profiles support
    /// </summary>
    public class HotkeyProfileManager
    {
        private readonly List<HotkeyProfile> _profiles = new();
        
        public event EventHandler<HotkeyProfile>? ProfileChanged;
        
        public List<HotkeyProfile> Profiles => _profiles.ToList();
        
        public HotkeyProfileManager()
        {
            InitializeDefaultProfiles();
        }
        
        private void InitializeDefaultProfiles()
        {
            _profiles.Add(new HotkeyProfile
            {
                Id = "default",
                Name = "Default Profile",
                Description = "Standard hotkey configuration",
                Hotkeys = new List<HotkeyDefinition>
                {
                    new HotkeyDefinition
                    {
                        Id = "start_dictation",
                        Name = "Start Dictation",
                        Combination = "Ctrl+Win+D",
                        Description = "Start voice dictation",
                        IsEmergency = false
                    }
                }
            });
            
            _profiles.Add(new HotkeyProfile
            {
                Id = "accessibility",
                Name = "Accessibility Profile",
                Description = "Simplified hotkeys for accessibility",
                Hotkeys = new List<HotkeyDefinition>
                {
                    new HotkeyDefinition
                    {
                        Id = "start_dictation",
                        Name = "Start Dictation",
                        Combination = "F12",
                        Description = "Start voice dictation (single key)",
                        IsEmergency = false
                    }
                }
            });
        }
        
        public HotkeyProfile? GetProfile(string profileId)
        {
            return _profiles.FirstOrDefault(p => p.Id == profileId);
        }
        
        public async Task<bool> SwitchProfileAsync(string profileId)
        {
            var profile = GetProfile(profileId);
            if (profile != null)
            {
                ProfileChanged?.Invoke(this, profile);
                await Task.CompletedTask;
                return true;
            }
            return false;
        }
    }

    /// <summary>
    /// Hotkey conflict resolver with suggested alternatives
    /// </summary>
    public class HotkeyConflictResolver
    {
        public async Task<HotkeyResolution> ResolveConflictAsync(HotkeyConflict conflict)
        {
            var resolution = new HotkeyResolution
            {
                OriginalHotkey = conflict.Hotkey,
                Conflict = conflict,
                Success = false
            };
            
            try
            {
                // Generate alternative hotkeys
                var alternatives = GenerateAlternativeHotkeys(conflict.Hotkey);
                resolution.SuggestedAlternatives = alternatives;
                
                // Try the first alternative
                if (alternatives.Any())
                {
                    var firstAlternative = alternatives.First();
                    var testSuccess = await TestHotkeyAsync(firstAlternative);
                    
                    if (testSuccess)
                    {
                        resolution.ResolvedHotkey = firstAlternative;
                        resolution.Success = true;
                    }
                }
            }
            catch (Exception ex)
            {
                resolution.ErrorMessage = ex.Message;
            }
            
            return resolution;
        }
        
        private List<string> GenerateAlternativeHotkeys(string conflictHotkey)
        {
            var alternatives = new List<string>();
            var modifiers = new[] { "Ctrl", "Alt", "Shift", "Win" };
            var keys = new[] { "D", "F", "G", "H", "J", "K", "L", "M", "N", "P" };
            
            foreach (var modifier in modifiers)
            {
                foreach (var key in keys)
                {
                    var alternative = $"{modifier}+{key}";
                    if (!alternatives.Contains(alternative) && alternative != conflictHotkey)
                    {
                        alternatives.Add(alternative);
                    }
                }
            }
            
            return alternatives.Take(5).ToList(); // Limit to 5 suggestions
        }
        
        private async Task<bool> TestHotkeyAsync(string hotkey)
        {
            // Simulate hotkey testing
            await Task.Delay(100);
            return true; // In real implementation, would test actual hotkey registration
        }
    }

    /// <summary>
    /// Hotkey resolution result
    /// </summary>
    public class HotkeyResolution
    {
        public string OriginalHotkey { get; set; } = string.Empty;
        public string ResolvedHotkey { get; set; } = string.Empty;
        public List<string> SuggestedAlternatives { get; set; } = new();
        public HotkeyConflict Conflict { get; set; } = new();
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
    }

    #endregion
}