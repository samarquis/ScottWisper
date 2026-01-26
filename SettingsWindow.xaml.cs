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
        private readonly List<AudioDevice> _inputDevices = new List<AudioDevice>();
        private readonly List<AudioDevice> _outputDevices = new List<AudioDevice>();
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

        private void PopulateComboBox(ComboBox comboBox, List<AudioDevice> devices, string selectedDeviceId)
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
            AutoSwitchDevicesCheckBox.IsChecked = _settingsService.Settings.Audio.AutoSwitchDevices;
            PreferHighQualityCheckBox.IsChecked = _settingsService.Settings.Audio.PreferHighQualityDevices;
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

        private void UpdateDeviceDetails(AudioDevice? device)
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
            
            var testTasks = _inputDevices.Select(async device =>
            {
                try
                {
                    var testPassed = await _audioDeviceService.TestDeviceAsync(device.Id);
                    return new DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = testPassed,
                        TestTime = DateTime.Now,
                        ErrorMessage = testPassed ? "" : "Device test failed"
                    };
                }
                catch (Exception ex)
                {
                    return new DeviceTestingResult
                    {
                        DeviceId = device.Id,
                        DeviceName = device.Name,
                        TestPassed = false,
                        TestTime = DateTime.Now,
                        ErrorMessage = ex.Message
                    };
                }
            });
            
            var testResults = await Task.WhenAll(testTasks);
            
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

        private async Task ApplyButton_Click(object sender, RoutedEventArgs e)
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
}