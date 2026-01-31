using ScottWisper.Services;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ScottWisper.UI
{
    /// <summary>
    /// Permission request dialog for user-friendly microphone permission handling
    /// </summary>
    public partial class PermissionDialog : Window
    {
        private readonly IPermissionService _permissionService;
        private MicrophonePermissionStatus _currentStatus = MicrophonePermissionStatus.Unknown;

        public PermissionDialog(IPermissionService permissionService)
        {
            _permissionService = permissionService ?? throw new ArgumentNullException(nameof(permissionService));
            InitializeComponent();
            Loaded += PermissionDialog_Loaded;
        }

        /// <summary>
        /// Handles window loaded event to check initial permission status
        /// </summary>
        private async void PermissionDialog_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await CheckAndUpdateStatusAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading permission dialog: {ex.Message}");
                UpdateInstructionText("Error loading permission status. Please try restarting the application.");
            }
        }

        /// <summary>
        /// Handles request permission button click
        /// </summary>
        private async void RequestPermissionButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Requesting permission...", MicrophonePermissionStatus.Unknown);
                RequestPermissionButton.IsEnabled = false;

                var success = await _permissionService.RequestMicrophonePermissionAsync();

                if (success)
                {
                    UpdateStatus("Permission granted!", MicrophonePermissionStatus.Granted);
                    
                    // Auto-close after successful permission
                    await Task.Delay(2000);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    UpdateStatus("Permission denied", MicrophonePermissionStatus.Denied);
                    RequestPermissionButton.Content = "Request Again";
                    RequestPermissionButton.IsEnabled = true;
                    RetryButton.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error requesting permission: {ex.Message}");
                UpdateStatus("Error requesting permission", MicrophonePermissionStatus.SystemError);
                RequestPermissionButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Handles open settings button click
        /// </summary>
        private async void OpenSettingsButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Opening settings...", MicrophonePermissionStatus.Unknown);
                
                var success = await _permissionService.OpenWindowsPrivacySettingsAsync();

                if (success)
                {
                    UpdateStatus("Settings opened. Please enable microphone access.", MicrophonePermissionStatus.Unknown);
                    RetryButton.Visibility = Visibility.Visible;
                }
                else
                {
                    UpdateStatus("Failed to open settings", MicrophonePermissionStatus.SystemError);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error opening settings: {ex.Message}");
                UpdateStatus("Error opening settings", MicrophonePermissionStatus.SystemError);
            }
        }

        /// <summary>
        /// Handles retry button click
        /// </summary>
        private async void RetryButtonClick(object sender, RoutedEventArgs e)
        {
            try
            {
                UpdateStatus("Checking permission...", MicrophonePermissionStatus.Unknown);
                RetryButton.IsEnabled = false;

                await CheckAndUpdateStatusAsync();
                
                RetryButton.IsEnabled = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error retrying permission check: {ex.Message}");
                UpdateStatus("Error checking permission", MicrophonePermissionStatus.SystemError);
                RetryButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Checks current permission status and updates UI
        /// </summary>
        private async Task CheckAndUpdateStatusAsync()
        {
            try
            {
                var status = await _permissionService.CheckMicrophonePermissionAsync();
                _currentStatus = status;

                switch (status)
                {
                    case MicrophonePermissionStatus.Granted:
                        UpdateStatus("Permission granted!", status);
                        // Auto-close after successful permission
                        await Task.Delay(1500);
                        DialogResult = true;
                        Close();
                        break;

                    case MicrophonePermissionStatus.Denied:
                        UpdateStatus("Permission denied", status);
                        RetryButton.Visibility = Visibility.Visible;
                        RequestPermissionButton.Content = "Request Again";
                        break;

                    case MicrophonePermissionStatus.Unknown:
                        UpdateStatus("Checking permission...", status);
                        RequestPermissionButton.Visibility = Visibility.Visible;
                        break;

                    case MicrophonePermissionStatus.SystemError:
                        UpdateStatus("System error occurred", status);
                        RetryButton.Visibility = Visibility.Visible;
                        break;

                    default:
                        UpdateStatus("Unknown permission status", status);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking permission status: {ex.Message}");
                UpdateStatus("Error checking permission", MicrophonePermissionStatus.SystemError);
            }
        }

        /// <summary>
        /// Updates the status text only
        /// </summary>
        private void UpdateStatus(string statusText)
        {
            UpdateStatus(statusText, _currentStatus);
        }

        /// <summary>
        /// Updates the status display with appropriate colors and text
        /// </summary>
        private void UpdateStatus(string statusText, MicrophonePermissionStatus status)
        {
            try
            {
                StatusText.Text = statusText;
                _currentStatus = status;

                // Update status indicator color
                var brush = status switch
                {
                    MicrophonePermissionStatus.Granted => FindResource("StatusGreen") as System.Windows.Media.SolidColorBrush,
                    MicrophonePermissionStatus.Denied => FindResource("StatusRed") as System.Windows.Media.SolidColorBrush,
                    MicrophonePermissionStatus.Unknown => FindResource("StatusYellow") as System.Windows.Media.SolidColorBrush,
                    _ => FindResource("StatusYellow") as System.Windows.Media.SolidColorBrush,
                };

                if (StatusIndicator != null && brush != null)
                {
                    StatusIndicator.Fill = brush;
                }

                // Update instruction text based on status
                UpdateInstructionText(status);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating status: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates instruction text directly
        /// </summary>
        private void UpdateInstructionText(string instructionText)
        {
            if (InstructionText != null)
            {
                InstructionText.Text = instructionText;
            }
        }

        /// <summary>
        /// Updates instruction text based on permission status
        /// </summary>
        private void UpdateInstructionText(MicrophonePermissionStatus status)
        {
            try
            {
                var instructionText = status switch
                {
                    MicrophonePermissionStatus.Granted => "Microphone access has been granted! You can now use voice dictation.",
                    MicrophonePermissionStatus.Denied => "Microphone access was denied. Please use the 'Request Permission' button to try again, or click 'Open Settings' to enable it manually.",
                    MicrophonePermissionStatus.Unknown => "Checking microphone permission status...",
                    MicrophonePermissionStatus.SystemError => "A system error occurred while checking microphone permission. Please restart the application.",
                    _ => "Checking microphone permission status..."
                };

                UpdateInstructionText(instructionText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating instruction text: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the current permission status
        /// </summary>
        public MicrophonePermissionStatus CurrentStatus => _currentStatus;

        /// <summary>
        /// Shows the dialog and returns whether permission was granted
        /// </summary>
        public new bool? ShowDialog()
        {
            try
            {
                ShowDialog();
                return DialogResult;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing permission dialog: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handles window closing to ensure proper cleanup
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Ensure permission service monitoring continues
                if (_permissionService != null)
                {
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(1000); // Brief delay before continuing monitoring
                        // Permission monitoring would continue in background
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in permission dialog cleanup: {ex.Message}");
            }
            finally
            {
                base.OnClosed(e);
            }
        }
    }
}