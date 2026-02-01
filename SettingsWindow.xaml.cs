using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using WhisperKey.Configuration;
using WhisperKey.Services;
using WhisperKey.ViewModels;

namespace WhisperKey
{
    public partial class SettingsWindow : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IAudioDeviceService _audioDeviceService;
        private SettingsViewModel _viewModel;
        private readonly List<Services.AudioDevice> _inputDevices = new List<Services.AudioDevice>();
        private readonly List<Services.AudioDevice> _outputDevices = new List<Services.AudioDevice>();
        private bool _isLoading = true;
        private AppSettings _originalSettings;
        
        // Hotkey management fields
        private bool _isRecordingHotkey = false;
        private List<Key> _pressedKeys = new List<Key>();
        private HotkeyProfile? _currentHotkeyProfile;
        private HotkeyDefinition? _editingHotkey;
        private string _recordingHotkeyId = string.Empty;

        public bool IsLoading
        {
            get => _isLoading;
            set
            {
                if (_isLoading != value)
                {
                    _isLoading = value;
                    OnPropertyChanged(nameof(IsLoading));
                }
            }
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            // Property changed implementation
        }

        public SettingsWindow(ISettingsService settingsService, IAudioDeviceService audioDeviceService)
        {
            InitializeComponent();
            _settingsService = settingsService;
            _audioDeviceService = audioDeviceService;
            _originalSettings = CloneSettings(_settingsService.Settings);
            
            // Initialize ViewModel and set as DataContext
            _viewModel = new SettingsViewModel(settingsService, audioDeviceService);
            DataContext = _viewModel;
            
            InitializeEventHandlers();
            _ = LoadDevicesAsync();
            PopulateHotkeyGrid();
        }

        private void PopulateHotkeyGrid()
        {
            var hotkeys = new List<object>();
            
            // Main hotkeys
            hotkeys.Add(new
            {
                Id = "toggle_recording",
                Name = "Toggle Recording",
                Combination = _settingsService.Settings.Hotkeys.ToggleRecording,
                Description = "Starts or stops voice dictation",
                IsEnabled = true,
                IsEmergency = false
            });

            hotkeys.Add(new
            {
                Id = "show_settings",
                Name = "Show Settings",
                Combination = _settingsService.Settings.Hotkeys.ShowSettings,
                Description = "Opens the settings window",
                IsEnabled = true,
                IsEmergency = false
            });

            // Custom hotkeys if any
            if (_settingsService.Settings.Hotkeys.CustomHotkeys != null)
            {
                foreach (var kvp in _settingsService.Settings.Hotkeys.CustomHotkeys)
                {
                    hotkeys.Add(new
                    {
                        Id = kvp.Key,
                        Name = kvp.Value.Name,
                        Combination = kvp.Value.Combination,
                        Description = kvp.Value.Description,
                        IsEnabled = kvp.Value.IsEnabled,
                        IsEmergency = kvp.Value.IsEmergency
                    });
                }
            }

            HotkeysDataGrid.ItemsSource = hotkeys;
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
                    IsLoading = false;
                    UpdateDeviceStatus("Device list loaded");
                });
            }
            catch (InvalidOperationException ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateDeviceStatus($"Error loading devices: {ex.Message}");
                    IsLoading = false;
                });
            }
            catch (IOException ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateDeviceStatus($"Error loading devices: {ex.Message}");
                    IsLoading = false;
                });
            }
            catch (TimeoutException ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    UpdateDeviceStatus($"Error loading devices: {ex.Message}");
                    IsLoading = false;
                });
            }
        }

        private void PopulateDeviceComboBoxes()
        {
            // Populate input device combo boxes
            PopulateComboBox(InputDeviceComboBox, _inputDevices, _settingsService.Settings.Audio.SelectedInputDeviceId);
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

        private async Task LoadSettingsAsync()
        {
            try
            {
                IsLoading = true;
                await _viewModel.LoadSettingsAsync();
                await LoadDevicesAsync();
                UpdateUsageStatistics();
            }
            catch (InvalidOperationException ex)
            {
                UpdateStatus($"Failed to load settings: {ex.Message}");
            }
            catch (IOException ex)
            {
                UpdateStatus($"Failed to load settings: {ex.Message}");
            }
            catch (UnauthorizedAccessException ex)
            {
                UpdateStatus($"Failed to load settings: {ex.Message}");
            }
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
            
            // Set API endpoint
            ApiEndpointTextBox.Text = _settingsService.Settings.Transcription.ApiEndpoint ?? string.Empty;
            
            // Set API timeout
            ApiTimeoutTextBox.Text = _settingsService.Settings.Transcription.RequestTimeout.ToString();
            
            // Set proxy checkbox
            UseProxyCheckBox.IsChecked = _settingsService.Settings.Transcription.UseProxy;
            
            // Update available models based on provider
            UpdateAvailableModels();
        }

        private void UpdateUsageStatistics()
        {
            TotalRequestsText.Text = _viewModel.TotalRequests.ToString();
            TotalMinutesText.Text = _viewModel.TotalMinutes.ToString("F1");
            CurrentMonthUsageText.Text = $"{_viewModel.CurrentMonthUsage:F1} minutes";
            
            UsageLimitProgressBar.Value = _viewModel.UsageProgress;
            UsageLimitTextBlock.Text = $"{_viewModel.CurrentMonthUsage:F1} / {_viewModel.UsageLimit} ({(int)_viewModel.UsageProgress}%)";
            TimeoutValueTextBlock.Text = $"{_settingsService.Settings.Transcription.RequestTimeout} seconds";
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
            _viewModel.StatusMessage = status;
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

        private async void TestInputDevice_Click(object sender, RoutedEventArgs e) => await TestSelectedDevice(InputDeviceComboBox);
        private async void TestOutputDevice_Click(object sender, RoutedEventArgs e) => await TestSelectedDevice(OutputDeviceComboBox);

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
                var comprehensiveTest = await _audioDeviceService.PerformComprehensiveTestAsync(deviceId);
                var capabilities = await _audioDeviceService.GetDeviceCapabilitiesAsync(deviceId);
                
                var testResult = new WhisperKey.Configuration.DeviceTestingResult
                {
                    DeviceId = deviceId,
                    DeviceName = selectedItem.Content.ToString()!,
                    TestPassed = comprehensiveTest.Success,
                    TestTime = DateTime.Now,
                    ErrorMessage = comprehensiveTest.Success ? "" : comprehensiveTest.ErrorMessage,
                    SignalStrength = (double)comprehensiveTest.QualityScore,
                    NoiseLevel = (int)comprehensiveTest.NoiseFloorDb
                };
                
                await _settingsService.AddDeviceTestResultAsync(testResult);
                PopulateDeviceGrid();
                UpdateDeviceStatus(comprehensiveTest.Success ? "Device test completed successfully" : "Device test failed");
                
                var testDialog = new DeviceTestResultDialog
                {
                    TestResult = testResult,
                    Capabilities = capabilities
                };
                testDialog.ShowDialog();
            }
            catch (InvalidOperationException ex)
            {
                UpdateDeviceStatus($"Test failed: {ex.Message}");
                MessageBox.Show($"Failed to test device: {ex.Message}", "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (IOException ex)
            {
                UpdateDeviceStatus($"Test failed: {ex.Message}");
                MessageBox.Show($"Failed to test device: {ex.Message}", "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch (TimeoutException ex)
            {
                UpdateDeviceStatus($"Test failed: {ex.Message}");
                MessageBox.Show($"Failed to test device: {ex.Message}", "Test Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void RefreshDevices_Click(object sender, RoutedEventArgs e) => await LoadDevicesAsync();

        private async void TestAllDevices_Click(object sender, RoutedEventArgs e)
        {
            UpdateDeviceStatus("Testing all devices...");
            var testResults = new List<WhisperKey.Configuration.DeviceTestingResult>();
            
            foreach (var device in _inputDevices)
            {
                try
                {
                    var testPassed = await _audioDeviceService.TestDeviceAsync(device.Id);
                    testResults.Add(new WhisperKey.Configuration.DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = testPassed,
                        TestTime = DateTime.Now,
                        ErrorMessage = testPassed ? "" : "Device test failed"
                    });
                }
                catch (InvalidOperationException ex)
                {
                    testResults.Add(new WhisperKey.Configuration.DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = false,
                        TestTime = DateTime.Now,
                        ErrorMessage = ex.Message
                    });
                }
                catch (IOException ex)
                {
                    testResults.Add(new WhisperKey.Configuration.DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = false,
                        TestTime = DateTime.Now,
                        ErrorMessage = ex.Message
                    });
                }
                catch (TimeoutException ex)
                {
                    testResults.Add(new WhisperKey.Configuration.DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = false,
                        TestTime = DateTime.Now,
                        ErrorMessage = ex.Message
                    });
                }
            }
            
            foreach (var result in testResults) await _settingsService.AddDeviceTestResultAsync(result);
            PopulateDeviceGrid();
            UpdateDeviceStatus($"Tested {_inputDevices.Count} devices");
        }

        private void InputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (InputDeviceComboBox.SelectedItem is ComboBoxItem selectedItem)
                _viewModel.SelectedInputDevice = selectedItem.Tag.ToString()!;
        }

        private void FallbackInputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (FallbackInputDeviceComboBox.SelectedItem is ComboBoxItem selectedItem)
                _viewModel.FallbackInputDevice = selectedItem.Tag.ToString()!;
        }

        private void OutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (OutputDeviceComboBox.SelectedItem is ComboBoxItem selectedItem)
                _viewModel.SelectedOutputDevice = selectedItem.Tag.ToString()!;
        }

        private void FallbackOutputDevice_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (FallbackOutputDeviceComboBox.SelectedItem is ComboBoxItem selectedItem)
                _viewModel.FallbackOutputDevice = selectedItem.Tag.ToString()!;
        }

        private void AutoSwitchDevices_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            _viewModel.AutoSwitchDevices = AutoSwitchDevicesCheckBox.IsChecked ?? false;
        }

        private void PreferHighQuality_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            _viewModel.PreferHighQualityDevices = PreferHighQualityCheckBox.IsChecked ?? false;
        }

        #region Transcription Settings Event Handlers

        private async void Provider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (ProviderComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _settingsService.Settings.Transcription.Provider = selectedItem.Tag?.ToString() ?? "OpenAI";
                await _settingsService.SaveAsync();
                UpdateAvailableModels();
            }
        }

        private async void Model_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (ModelComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _settingsService.Settings.Transcription.Model = selectedItem.Tag?.ToString() ?? "whisper-1";
                await _settingsService.SaveAsync();
            }
        }

        private async void Language_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (LanguageComboBox.SelectedItem is ComboBoxItem selectedItem)
            {
                _settingsService.Settings.Transcription.Language = selectedItem.Tag?.ToString() ?? "auto";
                await _settingsService.SaveAsync();
            }
        }

        private async void ApiKey_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            _settingsService.Settings.Transcription.ApiKey = ApiKeyPasswordBox.Password;
            await _settingsService.SaveAsync();
        }

        private async void TestApiKey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(ApiKeyPasswordBox.Password))
            {
                MessageBox.Show("Please enter an API key first.", "API Key Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                ApiStatusText.Text = "Testing API key...";
                var isValid = await TestApiKeyAsync(ApiKeyPasswordBox.Password);
                ApiStatusText.Text = isValid ? "API key valid" : "API key invalid";
                MessageBox.Show(isValid ? "API key is valid!" : "API key is invalid.", "API Key Test", MessageBoxButton.OK, isValid ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (InvalidOperationException ex)
            {
                ApiStatusText.Text = $"Test failed: {ex.Message}";
            }
            catch (TimeoutException ex)
            {
                ApiStatusText.Text = $"Test failed: {ex.Message}";
            }
        }

        private async Task<bool> TestApiKeyAsync(string apiKey)
        {
            await Task.Delay(1000);
            return !string.IsNullOrEmpty(apiKey);
        }

        private void UpdateAvailableModels()
        {
            ModelComboBox.Items.Clear();
            var provider = _settingsService.Settings.Transcription.Provider;
            var models = GetAvailableModels(provider);
            foreach (var model in models)
            {
                ModelComboBox.Items.Add(new ComboBoxItem { Content = model.DisplayName, Tag = model.Id });
            }
            var currentModel = _settingsService.Settings.Transcription.Model;
            var selectedItem = ModelComboBox.Items.Cast<ComboBoxItem>().FirstOrDefault(item => item.Tag?.ToString() == currentModel);
            if (selectedItem != null) ModelComboBox.SelectedItem = selectedItem;
        }

        private List<(string Id, string DisplayName)> GetAvailableModels(string provider)
        {
            if (provider.ToLower() == "openai")
                return new List<(string, string)> { ("whisper-1", "Whisper v1"), ("whisper-tiny", "Whisper Tiny") };
            return new List<(string, string)> { ("whisper-1", "Default") };
        }

        #endregion

        #region Hotkey Settings Event Handlers

        private void SetToggleRecordingHotkey_Click(object sender, RoutedEventArgs e) => StartHotkeyCapture("ToggleRecording");
        private async void ResetToggleRecordingHotkey_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Settings.Hotkeys.ToggleRecording = "Ctrl+Alt+V";
            ToggleRecordingHotkeyTextBox.Text = "Ctrl+Alt+V";
            await _settingsService.SaveAsync();
        }

        private void SetShowSettingsHotkey_Click(object sender, RoutedEventArgs e) => StartHotkeyCapture("ShowSettings");
        private async void ResetShowSettingsHotkey_Click(object sender, RoutedEventArgs e)
        {
            _settingsService.Settings.Hotkeys.ShowSettings = "Ctrl+Alt+S";
            ShowSettingsHotkeyTextBox.Text = "Ctrl+Alt+S";
            await _settingsService.SaveAsync();
        }

        private void StartHotkeyCapture(string target)
        {
            _isCapturingHotkey = true;
            _currentHotkeyTarget = target;
            HotkeyStatusText.Text = "Press desired key combination...";
        }

        private string _currentHotkeyTarget = string.Empty;
        private bool _isCapturingHotkey = false;

        #endregion

        #region UI Settings Event Handlers

        private async void ShowVisualFeedback_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            _settingsService.Settings.UI.ShowVisualFeedback = ShowVisualFeedbackCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private async void ShowTranscriptionWindow_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            _settingsService.Settings.UI.ShowTranscriptionWindow = ShowTranscriptionWindowCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private async void MinimizeToTray_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            _settingsService.Settings.UI.MinimizeToTray = MinimizeToTrayCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private async void StartWithWindows_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            _settingsService.Settings.UI.StartWithWindows = StartWithWindowsCheckBox.IsChecked ?? false;
            await _settingsService.SaveAsync();
        }

        private void WindowOpacity_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoading) return;
            if (WindowOpacityText != null) WindowOpacityText.Text = $"{(int)e.NewValue}%";
        }

        private void FeedbackVolume_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoading) return;
            if (FeedbackVolumeText != null) FeedbackVolumeText.Text = $"{(int)e.NewValue}%";
        }

        private void TestStartSound_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Start sound would play here.");
        private void TestStopSound_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Stop sound would play here.");

        private async void ResetUISettings_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Reset UI settings?", "Reset", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                _settingsService.Settings.UI = new UISettings();
                await _settingsService.SaveAsync();
                await LoadSettingsAsync();
            }
        }

        private async void ResetAllSettings_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Reset ALL settings?", "Reset", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                await ResetSettingsToDefaultAsync();
                await LoadSettingsAsync();
            }
        }

        #endregion

        private void DevicesDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DevicesDataGrid.SelectedItem == null) { UpdateDeviceDetails(null); return; }
            dynamic selected = DevicesDataGrid.SelectedItem;
            string name = selected.Name;
            var device = _inputDevices.FirstOrDefault(d => d.Name == name) ?? _outputDevices.FirstOrDefault(d => d.Name == name);
            UpdateDeviceDetails(device);
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e) => Close();
        private async void ApplyButton_Click(object sender, RoutedEventArgs e) { await _viewModel.SaveSettingsAsync(); UpdateDeviceStatus("Settings applied"); }
        private async void OKButton_Click(object sender, RoutedEventArgs e) { await _viewModel.SaveSettingsAsync(); Close(); }

        private async Task ResetSettingsToDefaultAsync()
        {
            // Implementation for resetting to defaults
            await Task.CompletedTask;
        }

        private void UpdateStatus(string message)
        {
            System.Diagnostics.Debug.WriteLine($"Status: {message}");
        }

        // Static HttpClient instance to prevent socket exhaustion
        // This is shared across all SettingsWindow instances
        private static readonly System.Net.Http.HttpClient _staticHttpClient = new System.Net.Http.HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };

        private async Task<bool> TestAPIEndpointAsync(string endpoint)
        {
            try
            {
                var response = await _staticHttpClient.GetAsync(endpoint);
                return response.IsSuccessStatusCode;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                System.Diagnostics.Debug.WriteLine($"API endpoint test failed for {endpoint}: {ex.Message}");
                return false;
            }
            catch (TimeoutException ex)
            {
                System.Diagnostics.Debug.WriteLine($"API endpoint test failed for {endpoint}: {ex.Message}");
                return false;
            }
            catch (UriFormatException ex)
            {
                System.Diagnostics.Debug.WriteLine($"API endpoint test failed for {endpoint}: {ex.Message}");
                return false;
            }
        }

        private AppSettings CloneSettings(AppSettings settings)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(settings);
            return System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json)!;
        }

        private async void OnDeviceConnected(object? sender, AudioDeviceEventArgs e) => await Dispatcher.InvokeAsync(LoadDevicesAsync);
        private async void OnDeviceDisconnected(object? sender, AudioDeviceEventArgs e) => await Dispatcher.InvokeAsync(LoadDevicesAsync);
        private async void OnDefaultDeviceChanged(object? sender, AudioDeviceEventArgs e) => await Dispatcher.InvokeAsync(LoadDevicesAsync);

        private void LoadCurrentSettings() { } // Handled by bindings

        #region Standardized UI Event Handlers (Actual)

        private void StartMinimized_Checked(object sender, RoutedEventArgs e) => _viewModel.StartMinimized = true;
        private void StartMinimized_Unchecked(object sender, RoutedEventArgs e) => _viewModel.StartMinimized = false;
        private void CheckForUpdates_Checked(object sender, RoutedEventArgs e) => _viewModel.CheckForUpdates = true;
        private void CheckForUpdates_Unchecked(object sender, RoutedEventArgs e) => _viewModel.CheckForUpdates = false;
        private void AutoStartDictation_Checked(object sender, RoutedEventArgs e) { } // Placeholder
        private void StartupDelay_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoading) return;
            if (StartupDelayText != null) StartupDelayText.Text = $"{(int)e.NewValue} seconds";
            _settingsService.Settings.UI.StartupDelay = (int)e.NewValue;
            _ = _settingsService.SaveAsync();
        }

        private void AutoUpdate_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            if (AutoUpdateCheckBox != null) _settingsService.Settings.UI.CheckForUpdates = AutoUpdateCheckBox.IsChecked ?? false; 
            _ = _settingsService.SaveAsync();
        }

        private void BetaUpdates_Checked(object sender, RoutedEventArgs e) { }
        private void CheckUpdates_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Checking for updates...");
        private void UpdateHistory_Click(object sender, RoutedEventArgs e) => MessageBox.Show("No history.");
        private void SingleInstance_Checked(object sender, RoutedEventArgs e) { }
        private void ShowNotifications_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            if (ShowNotificationsCheckBox != null) _settingsService.Settings.UI.ShowChangeNotifications = ShowNotificationsCheckBox.IsChecked ?? false;
            _ = _settingsService.SaveAsync();
        }
        private void RememberWindowPosition_Checked(object sender, RoutedEventArgs e) { }
        private void Theme_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (ThemeComboBox?.SelectedItem is ComboBoxItem item)
            {
                _settingsService.Settings.UI.Theme = item.Tag?.ToString() ?? "system";
                _ = _settingsService.SaveAsync();
            }
        }

        private void ApiProvider_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (IsLoading) return;
            if (ProviderComboBox?.SelectedItem is ComboBoxItem item)
            {
                _settingsService.Settings.Transcription.Provider = item.Tag?.ToString() ?? "OpenAI";
                _ = _settingsService.SaveAsync();
            }
        }

        private void ApiPassword_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            if (ApiKeyPasswordBox != null) _settingsService.Settings.Transcription.ApiKey = ApiKeyPasswordBox.Password;
            _ = _settingsService.SaveAsync();
        }

        private void ApiEndpoint_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoading) return;
            if (ApiEndpointTextBox != null) _settingsService.Settings.Transcription.ApiEndpoint = ApiEndpointTextBox.Text;
            _ = _settingsService.SaveAsync();
        }

        private void ApiTimeout_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (IsLoading) return;
            if (ApiTimeoutTextBox != null && int.TryParse(ApiTimeoutTextBox.Text, out int timeout))
            {
                _settingsService.Settings.Transcription.RequestTimeout = timeout;
                _ = _settingsService.SaveAsync();
            }
        }

        private void TestApi_Click(object sender, RoutedEventArgs e) => MessageBox.Show("API Test.");
        private void ValidateEndpoint_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Validation.");
        private void ResetApiUsage_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Reset.");
        private void UpgradeTier_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Upgrade.");
        private void UseProxy_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            if (UseProxyCheckBox != null) _settingsService.Settings.Transcription.UseProxy = UseProxyCheckBox.IsChecked ?? false;
            _ = _settingsService.SaveAsync();
        }

        private void InterfaceStartWithWindows_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            if (InterfaceStartWithWindowsCheckBox != null) _settingsService.Settings.UI.StartWithWindows = InterfaceStartWithWindowsCheckBox.IsChecked ?? false;
            _ = _settingsService.SaveAsync();
        }

        private void InterfaceMinimizeToTray_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            if (InterfaceMinimizeToTrayCheckBox != null) _settingsService.Settings.UI.MinimizeToTray = InterfaceMinimizeToTrayCheckBox.IsChecked ?? false;
            _ = _settingsService.SaveAsync();
        }

        private void InterfaceResetUISettings_Click(object sender, RoutedEventArgs e) => ResetUISettings_Click(sender, e);
        private void InterfaceResetAllSettings_Click(object sender, RoutedEventArgs e) => ResetAllSettings_Click(sender, e);

        private void EnableDebugLogging_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            if (EnableDebugLoggingCheckBox != null) _settingsService.Settings.TextInjection.EnableDebugMode = EnableDebugLoggingCheckBox.IsChecked ?? false;
            _ = _settingsService.SaveAsync();
        }

        private void EnableVerboseLogging_Checked(object sender, RoutedEventArgs e) { }
        private void LogToFile_Checked(object sender, RoutedEventArgs e) { }
        private void LogApiCalls_Checked(object sender, RoutedEventArgs e) { }
        private void LogLevel_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void ViewLogs_Click(object sender, RoutedEventArgs e) { }
        private void ClearLogs_Click(object sender, RoutedEventArgs e) { }
        private void ExportLogs_Click(object sender, RoutedEventArgs e) { }

        private void EnablePerformanceMetrics_Checked(object sender, RoutedEventArgs e)
        {
            if (IsLoading) return;
            if (EnablePerformanceMetricsCheckBox != null) _settingsService.Settings.TextInjection.EnablePerformanceMonitoring = EnablePerformanceMetricsCheckBox.IsChecked ?? false;
            _ = _settingsService.SaveAsync();
        }

        private void ShowLatencyOverlay_Checked(object sender, RoutedEventArgs e) { }

        private void MetricsInterval_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (IsLoading) return;
            if (MetricsIntervalText != null) MetricsIntervalText.Text = $"{(int)e.NewValue} seconds";
        }

        private void ViewMetrics_Click(object sender, RoutedEventArgs e) { }
        private void ResetMetrics_Click(object sender, RoutedEventArgs e) { }
        private void ExportMetrics_Click(object sender, RoutedEventArgs e) { }
        private void ResetAudioSettings_Click(object sender, RoutedEventArgs e) { }
        private void ResetHotkeySettings_Click(object sender, RoutedEventArgs e) { }
        private void AdvancedResetUISettings_Click(object sender, RoutedEventArgs e) { }
        private void AdvancedResetAPISettings_Click(object sender, RoutedEventArgs e) { }
        private void AdvancedResetAllSettings_Click(object sender, RoutedEventArgs e) { }

        private void EnableAutoPunctuation_Checked(object sender, RoutedEventArgs e) { }
        private void EnableRealTimeTranscription_Checked(object sender, RoutedEventArgs e) { }
        private void EnableProfanityFilter_Checked(object sender, RoutedEventArgs e) { }
        private void EnableTimestamps_Checked(object sender, RoutedEventArgs e) { }
        private void ConfidenceThreshold_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) { }
        private void MaxDuration_TextChanged(object sender, TextChangedEventArgs e) { }
        private void ResetUsage_Click(object sender, RoutedEventArgs e) { }
        
        private void Profile_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        private void NewProfile_Click(object sender, RoutedEventArgs e) => MessageBox.Show("New profile feature not yet fully implemented.");
        private void EditProfile_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Edit profile feature not yet fully implemented.");
        private void DeleteProfile_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Delete profile feature not yet fully implemented.");
        private void ExportProfile_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Export profile feature not yet fully implemented.");
        private void ImportProfile_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Import profile feature not yet fully implemented.");
        private void ResetToDefault_Click(object sender, RoutedEventArgs e) => MessageBox.Show("Reset to default feature not yet fully implemented.");
        
        private void EnableConflictWarnings_Checked(object sender, RoutedEventArgs e) { }
        private void EnableAccessibilityOptions_Checked(object sender, RoutedEventArgs e) { }
        private void EnableKeyboardLayoutAwareness_Checked(object sender, RoutedEventArgs e) { }
        
        private void HotkeysDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) { }
        
        private void StartRecording_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingHotkey = true;
            _pressedKeys.Clear();
            HotkeyRecordingTextBox.Text = "Recording... Press keys";
            StartRecordingButton.IsEnabled = false;
            StopRecordingButton.IsEnabled = true;
            this.KeyDown += OnHotkeyRecordingKeyDown;
        }

        private void StopRecording_Click(object sender, RoutedEventArgs e)
        {
            _isRecordingHotkey = false;
            this.KeyDown -= OnHotkeyRecordingKeyDown;
            StartRecordingButton.IsEnabled = true;
            StopRecordingButton.IsEnabled = false;
            UpdateRecordingDisplay();
        }

        private void OnHotkeyRecordingKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isRecordingHotkey) return;
            
            var key = e.Key == Key.System ? e.SystemKey : e.Key;
            if (!_pressedKeys.Contains(key))
            {
                _pressedKeys.Add(key);
                UpdateRecordingDisplay();
            }
            e.Handled = true;
        }

        private void UpdateRecordingDisplay()
        {
            if (_pressedKeys.Count == 0)
            {
                HotkeyRecordingTextBox.Text = "No keys pressed";
                return;
            }

            var combination = string.Join("+", _pressedKeys.Select(k => k.ToString()));
            HotkeyRecordingTextBox.Text = combination;
        }

        private void AddHotkey_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Add hotkey feature not yet fully implemented.");
        }

        private void CheckConflicts_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Conflict check feature not yet fully implemented.");
        }

        private void AutoResolve_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Auto-resolve feature not yet fully implemented.");
        }

        private void EnableAudioFeedback_Checked(object sender, RoutedEventArgs e) { }
        private void EditHotkey_Click(object sender, RoutedEventArgs e) { }
        private void TestHotkey_Click(object sender, RoutedEventArgs e) { }
        private void ApplyFix_Click(object sender, RoutedEventArgs e) { }

        #endregion

        protected override void OnClosed(EventArgs e)
        {
            _audioDeviceService.DeviceConnected -= OnDeviceConnected;
            _audioDeviceService.DeviceDisconnected -= OnDeviceDisconnected;
            _audioDeviceService.DefaultDeviceChanged -= OnDefaultDeviceChanged;
            base.OnClosed(e);
        }
    }

    public class DeviceTestResultDialog : Window
    {
        public WhisperKey.Configuration.DeviceTestingResult? TestResult { get; set; }
        public AudioDeviceCapabilities? Capabilities { get; set; }
        public DeviceTestResultDialog() { }
    }

    public class HotkeyConflictDetector
    {
        public async Task<List<HotkeyConflict>> CheckForConflictsAsync(List<HotkeyDefinition> hotkeys)
        {
            await Task.Delay(10);
            return new List<HotkeyConflict>();
        }
    }
}
