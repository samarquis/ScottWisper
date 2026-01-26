using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScottWisper
{
    /// <summary>
    /// Enhanced status history item for display
    /// </summary>
    public class StatusHistoryItem : INotifyPropertyChanged
    {
        private IFeedbackService.DictationStatus _status;
        private DateTime _timestamp;
        private string? _message;
        private TimeSpan _duration;

        public IFeedbackService.DictationStatus Status 
        { 
            get => _status;
            set { _status = value; OnPropertyChanged(nameof(Status)); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(StatusColor)); }
        }

        public DateTime Timestamp 
        { 
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); OnPropertyChanged(nameof(TimeString)); }
        }

        public string? Message 
        { 
            get => _message;
            set { _message = value; OnPropertyChanged(nameof(Message)); OnPropertyChanged(nameof(HasMessage)); }
        }

        public TimeSpan Duration 
        { 
            get => _duration;
            set { _duration = value; OnPropertyChanged(nameof(Duration)); OnPropertyChanged(nameof(DurationString)); }
        }

        public string StatusText => Status.ToString();
        public string TimeString => Timestamp.ToString("HH:mm:ss");
        public string DurationString => Duration.TotalSeconds < 60 ? $"{Duration.TotalSeconds:F1}s" : $"{Duration.TotalMinutes:F1}m";
        public bool HasMessage => !string.IsNullOrEmpty(Message);
        
        public Brush StatusColor => Status switch
        {
            IFeedbackService.DictationStatus.Idle => Brushes.Gray,
            IFeedbackService.DictationStatus.Ready => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
            IFeedbackService.DictationStatus.Recording => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            IFeedbackService.DictationStatus.Processing => new SolidColorBrush(Color.FromRgb(255, 193, 7)),
            IFeedbackService.DictationStatus.Complete => new SolidColorBrush(Color.FromRgb(40, 167, 69)),
            IFeedbackService.DictationStatus.Error => new SolidColorBrush(Color.FromRgb(220, 53, 69)),
            _ => Brushes.Gray
        };

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IFeedbackService? _feedbackService;
        private ScottWisper.Services.ISettingsService? _settingsService;
        private bool _isHidden = false;
        private readonly List<StatusHistoryItem> _displayHistory = new();
        private DateTime _sessionStartTime = DateTime.Now;
        private int _todayRecordings = 0;

        // Windows API imports for hiding from Alt+Tab
        [DllImport("user32.dll")]
        private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int GWL_EX_STYLE = -20;
        private const int WS_EX_TOOLWINDOW = 0x00000080;
        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
            StateChanged += MainWindow_StateChanged;
            Closing += MainWindow_Closing;
        }

        private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Get services from application
            _feedbackService = Application.Current.Properties["FeedbackService"] as FeedbackService;
            
            // Get settings service from application properties or create new one
            if (Application.Current.Properties["SettingsService"] is ScottWisper.Services.ISettingsService settingsService)
            {
                _settingsService = settingsService;
            }
            
            // Initialize feedback service if needed
            if (_feedbackService == null)
            {
                _feedbackService = new FeedbackService();
                await _feedbackService.InitializeAsync();
            }

            // Subscribe to enhanced feedback events
            _feedbackService.StatusChanged += OnFeedbackStatusChanged;
            if (_feedbackService is FeedbackService enhancedFeedback)
            {
                enhancedFeedback.StatusHistoryUpdated += OnStatusHistoryUpdated;
                enhancedFeedback.ProgressUpdated += OnProgressUpdated;
            }

            // Subscribe to settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += OnSettingsChanged;
                // Apply current settings
                await ApplyCurrentSettingsAsync();
            }

            // Set initial status
            await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Ready, "Application ready");

            // Initialize UI components
            InitializeHistoryDisplay();
            UpdateQuickStats();

            // Initially hide window for background operation
            HideWindowFromAltTab();
            Hide();
            _isHidden = true;
        }

        private void OnFeedbackStatusChanged(object? sender, IFeedbackService.DictationStatus status)
        {
            // Update UI on dispatcher thread
            Dispatcher.Invoke(() =>
            {
                UpdateStatusDisplay(status);
            });
        }

        private void UpdateStatusDisplay(IFeedbackService.DictationStatus status)
        {
            // Update status text with enhanced formatting
            StatusText.Text = GetEnhancedStatusText(status);
            FooterStatus.Text = GetEnhancedStatusText(status);

            // Update status indicator color with smooth transitions
            var color = GetStatusColor(status);
            AnimateStatusIndicator(color);

            // Update time
            StatusTime.Text = DateTime.Now.ToString("HH:mm:ss");

            // Update main status message
            MainStatusMessage.Text = GetStatusMessage(status);

            // Update footer message
            FooterMessage.Text = GetDetailedStatusMessage(status);

            // Track recording events for stats
            if (status == IFeedbackService.DictationStatus.Recording)
            {
                _todayRecordings++;
                UpdateQuickStats();
            }
        }

        private string GetEnhancedStatusText(IFeedbackService.DictationStatus status)
        {
            return status switch
            {
                IFeedbackService.DictationStatus.Idle => "⚫ Idle",
                IFeedbackService.DictationStatus.Ready => "🟢 Ready",
                IFeedbackService.DictationStatus.Recording => "🔴 Recording",
                IFeedbackService.DictationStatus.Processing => "🟡 Processing",
                IFeedbackService.DictationStatus.Complete => "✅ Complete",
                IFeedbackService.DictationStatus.Error => "❌ Error",
                _ => status.ToString()
            };
        }

        private Color GetStatusColor(IFeedbackService.DictationStatus status)
        {
            return status switch
            {
                IFeedbackService.DictationStatus.Idle => Color.FromRgb(108, 117, 125),
                IFeedbackService.DictationStatus.Ready => Color.FromRgb(40, 167, 69),
                IFeedbackService.DictationStatus.Recording => Color.FromRgb(220, 53, 69),
                IFeedbackService.DictationStatus.Processing => Color.FromRgb(255, 193, 7),
                IFeedbackService.DictationStatus.Complete => Color.FromRgb(40, 167, 69),
                IFeedbackService.DictationStatus.Error => Color.FromRgb(220, 53, 69),
                _ => Colors.Gray
            };
        }

        private void AnimateStatusIndicator(Color targetColor)
        {
            var targetBrush = new SolidColorBrush(targetColor);
            
            if (_feedbackService is FeedbackService enhancedFeedback && enhancedFeedback.Preferences.UseAdvancedAnimations)
            {
                var animation = new ColorAnimation
                {
                    From = ((SolidColorBrush)StatusIndicator.Fill).Color,
                    To = targetColor,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                StatusIndicator.Fill = targetBrush;
                targetBrush.BeginAnimation(SolidColorBrush.ColorProperty, animation);
            }
            else
            {
                StatusIndicator.Fill = targetBrush;
            }
        }

        private string GetStatusMessage(IFeedbackService.DictationStatus status)
        {
            return status switch
            {
                IFeedbackService.DictationStatus.Idle => "Waiting for activation",
                IFeedbackService.DictationStatus.Ready => "Ready for voice dictation",
                IFeedbackService.DictationStatus.Recording => "Listening... Speak clearly",
                IFeedbackService.DictationStatus.Processing => "Converting speech to text",
                IFeedbackService.DictationStatus.Complete => "Text ready for insertion",
                IFeedbackService.DictationStatus.Error => "Please try again",
                _ => "Unknown status"
            };
        }

        private string GetDetailedStatusMessage(IFeedbackService.DictationStatus status)
        {
            var sessionDuration = DateTime.Now - _sessionStartTime;
            return status switch
            {
                IFeedbackService.DictationStatus.Idle => $"Application idle | Session: {sessionDuration:h\\:mm}",
                IFeedbackService.DictationStatus.Ready => $"Ready to record | Session: {sessionDuration:h\\:mm}",
                IFeedbackService.DictationStatus.Recording => "Recording speech... | Click to stop",
                IFeedbackService.DictationStatus.Processing => "Processing with AI | Please wait",
                IFeedbackService.DictationStatus.Complete => "Transcription completed successfully",
                IFeedbackService.DictationStatus.Error => "Error occurred | Check microphone and connection",
                _ => "Status unknown"
            };
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            // Handle minimize to tray behavior
            if (WindowState == WindowState.Minimized)
            {
                HideWindowFromAltTab();
                Hide();
                _isHidden = true;
            }
        }

        private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            // Cancel close event and hide to tray instead
            e.Cancel = true;
            HideWindowFromAltTab();
            Hide();
            _isHidden = true;
        }

        private void HideWindowFromAltTab()
        {
            // Hide window from Alt+Tab when minimized or hidden
            var hWnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hWnd != IntPtr.Zero)
            {
                int extendedStyle = GetWindowLong(hWnd, GWL_EX_STYLE);
                SetWindowLong(hWnd, GWL_EX_STYLE, extendedStyle | WS_EX_TOOLWINDOW);
            }
        }

        private void ShowWindowInAltTab()
        {
            // Show window in Alt+Tab when visible
            var hWnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            if (hWnd != IntPtr.Zero)
            {
                int extendedStyle = GetWindowLong(hWnd, GWL_EX_STYLE);
                SetWindowLong(hWnd, GWL_EX_STYLE, extendedStyle & ~WS_EX_TOOLWINDOW);
            }
        }

        // Public methods for system tray integration
        public void ShowFromTray()
        {
            ShowWindowInAltTab();
            Show();
            WindowState = WindowState.Normal;
            _isHidden = false;
            Activate();
            Focus();
        }

        public void HideToTray()
        {
            HideWindowFromAltTab();
            Hide();
            _isHidden = true;
        }

        public void ToggleVisibility()
        {
            if (_isHidden)
            {
                ShowFromTray();
            }
            else
            {
                HideToTray();
            }
        }

        public bool IsWindowHidden => _isHidden;

        // Public method for external components to update status
        private void OnStatusHistoryUpdated(object? sender, ScottWisper.StatusHistoryEntry entry)
        {
            Dispatcher.Invoke(() =>
            {
                var historyItem = new StatusHistoryItem
                {
                    Status = entry.Status,
                    Timestamp = entry.Timestamp,
                    Message = entry.Message,
                    Duration = entry.Duration
                };

                _displayHistory.Insert(0, historyItem);
                
                // Limit display to last 20 items
                while (_displayHistory.Count > 20)
                {
                    _displayHistory.RemoveAt(_displayHistory.Count - 1);
                }

                UpdateHistoryDisplay();
            });
        }

        private void OnProgressUpdated(object? sender, ScottWisper.ProgressState progress)
        {
            Dispatcher.Invoke(() =>
            {
                ProgressOperation.Text = progress.Operation;
                ProgressDetails.Text = progress.Details ?? "";
                ProgressBar.Value = progress.Progress;
                ProgressPercentage.Text = $"{progress.Progress:F0}%";
                
                ProgressSection.Visibility = progress.IsActive ? Visibility.Visible : Visibility.Collapsed;
            });
        }

        private void InitializeHistoryDisplay()
        {
            HistoryList.ItemsSource = _displayHistory;
            UpdateHistoryDisplay();
        }

        private void UpdateHistoryDisplay()
        {
            HistoryCount.Text = $"{_displayHistory.Count} items";
        }

        private void UpdateQuickStats()
        {
            var sessionDuration = DateTime.Now - _sessionStartTime;
            
            TodayRecordings.Text = _todayRecordings.ToString();
            UsageTime.Text = sessionDuration.TotalHours < 1 ? 
                              $"{sessionDuration.TotalMinutes:F0}m" : 
                              $"{sessionDuration.TotalHours:F1}h";
            
            // Simulate API usage (would come from actual tracking service)
            var estimatedUsage = _todayRecordings * 0.002; // $0.002 per recording
            ApiUsage.Text = $"${estimatedUsage:F3}";
        }

        // Button event handlers
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            // Settings functionality would be implemented here
            ShowNotification("Settings panel coming soon!", "Settings");
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            ShowNotification("Help documentation coming soon!", "Help");
        }

        private void ClearHistoryButton_Click(object sender, RoutedEventArgs e)
        {
            _displayHistory.Clear();
            UpdateHistoryDisplay();
            ShowNotification("Status history cleared", "History");
        }

        private void ShowNotification(string message, string title = "ScottWisper")
        {
            if (_feedbackService != null)
            {
                _ = _feedbackService.ShowNotificationAsync(title, message);
            }
        }

        public async Task UpdateFeedbackStatus(IFeedbackService.DictationStatus status, string? message = null)
        {
            if (_feedbackService != null)
            {
                await _feedbackService.SetStatusAsync(status, message);
            }
        }

        public IFeedbackService? FeedbackService => _feedbackService;

        private async void OnSettingsChanged(object? sender, ScottWisper.Services.SettingsChangedEventArgs e)
        {
            try
            {
                // Handle settings changes that affect the MainWindow
                switch (e.Category)
                {
                    case "UI":
                        await ApplyUISettingsAsync();
                        break;
                    case "System":
                        if (e.Key == "ApplyAll" || e.Key == "ReloadSettings")
                        {
                            await ApplyCurrentSettingsAsync();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply settings change in MainWindow: {ex.Message}");
            }
        }

        private async Task ApplyCurrentSettingsAsync()
        {
            try
            {
                if (_settingsService == null)
                    return;

                var settings = _settingsService.Settings;
                
                // Apply UI settings
                await ApplyUISettingsAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply current settings: {ex.Message}");
            }
        }

        private async Task ApplyUISettingsAsync()
        {
            try
            {
                if (_settingsService?.Settings?.UI == null)
                    return;

                var uiSettings = _settingsService.Settings.UI;
                
                // Apply window behavior settings
                if (uiSettings.MinimizeToTray && !_isHidden)
                {
                    HideWindowFromAltTab();
                    Hide();
                    _isHidden = true;
                }
                else if (!uiSettings.MinimizeToTray && _isHidden)
                {
                    Show();
                    _isHidden = false;
                }

                // Apply visual feedback settings
                if (uiSettings.ShowVisualFeedback)
                {
                    // Enable visual indicators
                }
                else
                {
                    // Disable visual indicators
                }

                // Apply other UI settings as needed
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply UI settings: {ex.Message}");
            }
        }

        protected override async void OnClosed(EventArgs e)
        {
            // Cleanup feedback service
            if (_feedbackService != null)
            {
                await _feedbackService.DisposeAsync();
            }

            // Unsubscribe from settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }

            base.OnClosed(e);
        }
    }
}