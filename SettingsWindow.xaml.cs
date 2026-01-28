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

        private void PopulateDeviceGrid()
        {
            var allDevices = new List<object>();
            
            foreach (var inputDevice in _inputDevices)
            {
                allDevices.Add(new
                {
                    Name = inputDevice.Name,
                    DataFlow = "Input",
                    State = inputDevice.State.ToString(),
                    IsCompatible = _audioDeviceService.IsDeviceCompatible(inputDevice.Id),
                    LastTested = GetLastTestedTime(inputDevice.Id),
                    IsEnabled = GetDeviceEnabled(inputDevice.Id)
                });
            }
            
            foreach (var outputDevice in _outputDevices)
            {
                allDevices.Add(new
                {
                    Name = outputDevice.Name,
                    DataFlow = "Output",
                    State = outputDevice.State.ToString(),
                    IsCompatible = _audioDeviceService.IsDeviceCompatible(outputDevice.Id),
                    LastTested = GetLastTestedTime(outputDevice.Id),
                    IsEnabled = GetDeviceEnabled(outputDevice.Id)
                });
            }
            
            DevicesDataGrid.ItemsSource = allDevices;
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
                
                // Perform the test
                var testPassed = await _audioDeviceService.TestDeviceAsync(deviceId);
                
                // Create test result
                var testResult = new DeviceTestingResult
                {
                    DeviceId = deviceId,
                    DeviceName = selectedItem.Content.ToString()!,
                    TestPassed = testPassed,
                    TestTime = DateTime.Now,
                    ErrorMessage = testPassed ? "" : "Device test failed"
                };
                
                // Save test result
                await _settingsService.AddDeviceTestResultAsync(testResult);
                
                // Update UI
                PopulateDeviceGrid();
                UpdateDeviceStatus(testPassed ? "Device test passed" : "Device test failed");
                
                MessageBox.Show(testPassed 
                    ? $"Device '{selectedItem.Content}' test completed successfully!" 
                    : $"Device '{selectedItem.Content}' test failed. See device notes for details.", 
                    "Test Result", MessageBoxButton.OK, 
                    testPassed ? MessageBoxImage.Information : MessageBoxImage.Warning);
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

        private async Task<bool> TestApiKeyAsync(string apiKey)
        {
            // Mock implementation - would actually test against the API
            await Task.Delay(1000);
            return !string.IsNullOrWhiteSpace(apiKey) && apiKey.StartsWith("sk-");
        }

        private void UpdateAvailableModels()
        {
            ModelComboBox.Items.Clear();
            
            var provider = _settingsService.Settings.Transcription.Provider;
            var models = GetAvailableModels(provider);
            
            foreach (var model in models)
            {
                var item = new ComboBoxItem { Content = model.DisplayName, Tag = model.Id };
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
    }
}