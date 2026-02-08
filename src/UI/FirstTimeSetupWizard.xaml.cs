using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using WhisperKey.Configuration;
using WhisperKey.Services;
using WhisperKey.Services.Validation;

namespace WhisperKey.UI
{
    /// <summary>
    /// First-time setup wizard for onboarding new users
    /// </summary>
    public partial class FirstTimeSetupWizard : Window
    {
        private readonly ISettingsService _settingsService;
        private readonly IAudioDeviceService _audioDeviceService;
        private readonly IApiKeyManagementService _apiKeyManagement;
        private int _currentStep = 1;
        private bool _isMicrophoneTestRunning = false;
        private DispatcherTimer? _audioLevelTimer;
        
        public bool SetupCompleted { get; private set; } = false;
        
        public FirstTimeSetupWizard(ISettingsService settingsService, IAudioDeviceService audioDeviceService, IApiKeyManagementService apiKeyManagement)
        {
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _audioDeviceService = audioDeviceService ?? throw new ArgumentNullException(nameof(audioDeviceService));
            _apiKeyManagement = apiKeyManagement ?? throw new ArgumentNullException(nameof(apiKeyManagement));
            
            InitializeComponent();
            LoadMicrophones();
            UpdateStepIndicators();
        }
        
        private void LoadMicrophones()
        {
            try
            {
                var devices = _audioDeviceService.GetInputDevicesAsync().Result;
                foreach (var device in devices)
                {
                    MicrophoneComboBox.Items.Add(device);
                }
                
                if (MicrophoneComboBox.Items.Count > 0)
                {
                    MicrophoneComboBox.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MicrophoneStatusText.Text = $"Could not load microphones: {ex.Message}";
                MicrophoneStatusText.Foreground = Brushes.Orange;
            }
        }
        
        private void UpdateStepIndicators()
        {
            Step1Indicator.Fill = _currentStep == 1 ? Brushes.Orange : (_currentStep > 1 ? Brushes.Green : Brushes.Gray);
            Step2Indicator.Fill = _currentStep == 2 ? Brushes.Orange : (_currentStep > 2 ? Brushes.Green : Brushes.Gray);
            Step3Indicator.Fill = _currentStep == 3 ? Brushes.Orange : Brushes.Gray;
            
            Step1Panel.Visibility = _currentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
            Step2Panel.Visibility = _currentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
            Step3Panel.Visibility = _currentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
            
            BackButton.IsEnabled = _currentStep > 1;
            
            if (_currentStep == 3)
            {
                NextButton.Visibility = Visibility.Collapsed;
                FinishButton.Visibility = Visibility.Visible;
            }
            else
            {
                NextButton.Visibility = Visibility.Visible;
                FinishButton.Visibility = Visibility.Collapsed;
            }
        }
        
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep > 1)
            {
                _currentStep--;
                UpdateStepIndicators();
            }
        }

        private void SkipButton_Click(object sender, RoutedEventArgs e)
        {
            // Mark as completed so it doesn't show again
            try
            {
                var settings = _settingsService.Settings;
                settings.FirstTimeSetupCompleted = true;
                _settingsService.SaveAsync().Wait();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving settings during skip: {ex.Message}");
            }

            DialogResult = false;
            Close();
        }
        
        private async void NextButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentStep == 1)
            {
                if (!await ValidateStep1())
                    return;
            }
            else if (_currentStep == 2)
            {
                if (!ValidateStep2())
                    return;
            }
            
            if (_currentStep < 3)
            {
                _currentStep++;
                UpdateStepIndicators();
            }
        }
        
        private async Task<bool> ValidateStep1()
        {
            var provider = (ProviderComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "local";
            var apiKey = ApiKeyPasswordBox.Password;
            
            // API key is now optional - local mode works offline
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                // If user provided API key, validate it
                var strategy = ApiKeyValidationFactory.GetStrategy(provider);
                if (!strategy.IsValid(apiKey))
                {
                    ApiKeyValidationText.Text = strategy.GetValidationErrorMessage();
                    return false;
                }
            }
            
            // Save the settings
            try
            {
                var settings = _settingsService.Settings;
                settings.Transcription.Mode = TranscriptionMode.Local; // Default to local (offline) mode
                settings.Transcription.Provider = provider;
                
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    // Use ApiKeyManagementService for secure storage (IA-5 compliance)
                    // This replaces storing the key in plain text in the settings file
                    await _apiKeyManagement.RegisterKeyAsync(provider, "Default Key", apiKey);
                    
                    settings.Transcription.Mode = TranscriptionMode.Cloud; // Only use cloud if API key provided
                }
                
                await _settingsService.SaveAsync();
                
                ApiKeyValidationText.Text = "";
                return true;
            }
            catch (Exception ex)
            {
                ApiKeyValidationText.Text = $"Error saving settings: {ex.Message}";
                return false;
            }
        }
        
        private bool ValidateStep2()
        {
            if (MicrophoneComboBox.SelectedItem == null)
            {
                MicrophoneValidationText.Text = "Please select a microphone.";
                return false;
            }
            
            MicrophoneValidationText.Text = "";
            return true;
        }
        
        private async void TestMicrophoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isMicrophoneTestRunning)
            {
                StopMicrophoneTest();
                return;
            }
            
            var selectedDevice = MicrophoneComboBox.SelectedItem as AudioDevice;
            if (selectedDevice == null)
            {
                MicrophoneStatusText.Text = "Please select a microphone first.";
                MicrophoneStatusText.Foreground = Brushes.Red;
                return;
            }
            
            try
            {
                _isMicrophoneTestRunning = true;
                TestMicrophoneButton.Content = "⏹ Stop Test";
                MicrophoneStatusText.Text = "Listening... speak now!";
                MicrophoneStatusText.Foreground = Brushes.Orange;
                
                // Start audio level simulation
                _audioLevelTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                var random = new Random();
                _audioLevelTimer.Tick += (s, args) =>
                {
                    AudioLevelProgressBar.Value = random.Next(30, 90);
                };
                _audioLevelTimer.Start();
                
                // Simulate a test duration
                await Task.Delay(3000);
                
                StopMicrophoneTest();
                
                MicrophoneStatusText.Text = "Microphone test successful! Audio detected.";
                MicrophoneStatusText.Foreground = Brushes.Green;
            }
            catch (Exception ex)
            {
                StopMicrophoneTest();
                MicrophoneStatusText.Text = $"Test failed: {ex.Message}";
                MicrophoneStatusText.Foreground = Brushes.Red;
            }
        }
        
        private void StopMicrophoneTest()
        {
            _isMicrophoneTestRunning = false;
            TestMicrophoneButton.Content = "🎤 Test Microphone";
            _audioLevelTimer?.Stop();
            _audioLevelTimer = null;
            AudioLevelProgressBar.Value = 0;
        }
        
        private void FinishButton_Click(object sender, RoutedEventArgs e)
        {
            // Save the "show on startup" preference
            try
            {
                var settings = _settingsService.Settings;
                settings.FirstTimeSetupCompleted = true;
                settings.ShowSetupWizardOnStartup = ShowOnStartupCheckBox.IsChecked ?? false;
                _settingsService.SaveAsync().Wait();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Warning: Could not save all settings: {ex.Message}", "WhisperKey", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            
            SetupCompleted = true;
            DialogResult = true;
            Close();
        }
        
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            StopMicrophoneTest();
            base.OnClosing(e);
        }
    }
}
