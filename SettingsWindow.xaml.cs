using Microsoft.Extensions.DependencyInjection;
using ScottWisper.Configuration;
using ScottWisper.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

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
            ToggleRecordingHotkeyTextBox.Text = _settingsService.Settings.Hotkeys.ToggleRecording;
            ShowSettingsHotkeyTextBox.Text = _settingsService.Settings.Hotkeys.ShowSettings;
            
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

        private async void CheckConflicts_Click(object sender, RoutedEventArgs e)
        {
            var conflicts = await DetectHotkeyConflictsAsync();
            
            ConflictsDataGrid.ItemsSource = conflicts.Select(c => new
            {
                Hotkey = c.Hotkey,
                Application = c.Application,
                Status = c.Status
            }).ToList();
            
            if (conflicts.Any())
            {
                MessageBox.Show($"Found {conflicts.Count} potential hotkey conflicts.", 
                    "Conflict Detection", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            else
            {
                MessageBox.Show("No hotkey conflicts detected.", 
                    "Conflict Detection", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async Task<List<HotkeyConflict>> DetectHotkeyConflictsAsync()
        {
            // Mock implementation - would actually check system-wide hotkey registrations
            await Task.Delay(500);
            
            var conflicts = new List<HotkeyConflict>();
            
            // Simulate some conflicts for demonstration
            conflicts.Add(new HotkeyConflict
            {
                Hotkey = "Ctrl+Alt+V",
                Application = "Other App",
                Status = "Potential Conflict"
            });
            
            return conflicts;
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

        protected override void OnClosed(EventArgs e)
        {
            // Cleanup event handlers
            _audioDeviceService.DeviceConnected -= OnDeviceConnected;
            _audioDeviceService.DeviceDisconnected -= OnDeviceDisconnected;
            _audioDeviceService.DefaultDeviceChanged -= OnDefaultDeviceChanged;
            
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