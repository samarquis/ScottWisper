using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using System.ComponentModel;

namespace WhisperKey
{
    /// <summary>
    /// Interaction logic for StatusIndicatorWindow.xaml
    /// </summary>
    public partial class StatusIndicatorWindow : Window, INotifyPropertyChanged
    {
        private readonly DispatcherTimer _autoHideTimer;
        private IFeedbackService.DictationStatus _currentStatus = IFeedbackService.DictationStatus.Idle;

        #region Properties
        
        public IFeedbackService.DictationStatus CurrentStatus
        {
            get => _currentStatus;
            private set
            {
                if (_currentStatus != value)
                {
                    _currentStatus = value;
                    OnPropertyChanged(nameof(CurrentStatus));
                    UpdateVisuals();
                }
            }
        }

        public string CurrentStatusText
        {
            get => StatusText.Text;
            set
            {
                if (StatusText.Text != value)
                {
                    StatusText.Text = value;
                    OnPropertyChanged(nameof(CurrentStatusText));
                }
            }
        }

        public string CurrentStatusDescription
        {
            get => StatusDescription.Text;
            set
            {
                if (StatusDescription.Text != value)
                {
                    StatusDescription.Text = value;
                    OnPropertyChanged(nameof(CurrentStatusDescription));
                }
            }
        }

        #endregion

        public StatusIndicatorWindow()
        {
            InitializeComponent();
            
            // Set up auto-hide timer
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _autoHideTimer.Tick += AutoHideTimer_Tick;
            
            // Initialize tooltips
            UpdateTooltip();
            
            // Set initial state
            UpdateStatus(IFeedbackService.DictationStatus.Idle);
            
            // Apply click-through behavior for non-intrusive operation
            IsHitTestVisible = true;
        }

        #region Public Methods

        /// <summary>
        /// Updates the status indicator with new status and optional message
        /// </summary>
        public void UpdateStatus(IFeedbackService.DictationStatus status, string? message = null)
        {
            CurrentStatus = status;
            
            // Update text based on status
            var (text, description) = status switch
            {
                IFeedbackService.DictationStatus.Idle => ("Idle", "Ready for dictation"),
                IFeedbackService.DictationStatus.Ready => ("Ready", "Press hotkey to start"),
                IFeedbackService.DictationStatus.Recording => ("Recording", "Listening for speech..."),
                IFeedbackService.DictationStatus.Processing => ("Processing", "Transcribing audio..."),
                IFeedbackService.DictationStatus.Complete => ("Complete", "Transcription finished"),
                IFeedbackService.DictationStatus.Error => ("Error", "An error occurred"),
                _ => ("Unknown", "Status unknown")
            };

            CurrentStatusText = message ?? text;
            CurrentStatusDescription = description;
            
            // Start auto-hide timer for non-recording states
            if (status != IFeedbackService.DictationStatus.Recording)
            {
                _autoHideTimer.Stop();
                _autoHideTimer.Start();
            }
            else
            {
                _autoHideTimer.Stop();
            }

            // Show window if not already visible
            if (!IsVisible)
            {
                Show();
            }

            BringToFront();
        }

        /// <summary>
        /// Shows the status window with a specific duration
        /// </summary>
        public void ShowStatus(IFeedbackService.DictationStatus status, TimeSpan duration, string? message = null)
        {
            UpdateStatus(status, message);
            
            _autoHideTimer.Interval = duration;
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }

        /// <summary>
        /// Immediately hides the status window
        /// </summary>
        public void HideStatus()
        {
            _autoHideTimer.Stop();
            Hide();
        }

        #endregion

        #region Private Methods

        private void UpdateVisuals()
        {
            // Update the visual listening indicator (Google Assistant style)
            ListeningIndicator?.SetStatus(CurrentStatus);
            
            // Update border color to match status theme
            var borderColor = CurrentStatus switch
            {
                IFeedbackService.DictationStatus.Idle => System.Windows.Media.Color.FromArgb(200, 128, 128, 128),
                IFeedbackService.DictationStatus.Ready => System.Windows.Media.Color.FromArgb(200, 68, 255, 68),
                IFeedbackService.DictationStatus.Recording => System.Windows.Media.Color.FromArgb(200, 66, 133, 244), // Google Blue
                IFeedbackService.DictationStatus.Processing => System.Windows.Media.Color.FromArgb(200, 255, 193, 7), // Amber
                IFeedbackService.DictationStatus.Complete => System.Windows.Media.Color.FromArgb(200, 68, 255, 68), // Green
                IFeedbackService.DictationStatus.Error => System.Windows.Media.Color.FromArgb(200, 220, 53, 69), // Red
                _ => System.Windows.Media.Color.FromArgb(200, 128, 128, 128)
            };

            // Keep the status circle for backward compatibility (hidden)
            StatusCircle.Fill = new System.Windows.Media.SolidColorBrush(borderColor);
            
            // Update main border with subtle background tint
            MainBorder.Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromArgb(200, 45, 45, 48)); // Dark theme background

            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            var (title, details) = CurrentStatus switch
            {
                IFeedbackService.DictationStatus.Idle => ("WhisperKey - Idle", "Ready to start dictation"),
                IFeedbackService.DictationStatus.Ready => ("WhisperKey - Ready", "Press hotkey to begin recording"),
                IFeedbackService.DictationStatus.Recording => ("WhisperKey - Recording", "Listening for speech..."),
                IFeedbackService.DictationStatus.Processing => ("WhisperKey - Processing", "Transcribing audio to text"),
                IFeedbackService.DictationStatus.Complete => ("WhisperKey - Complete", "Transcription finished successfully"),
                IFeedbackService.DictationStatus.Error => ("WhisperKey - Error", "An error occurred during processing"),
                _ => ("WhisperKey", "Status unknown")
            };

            TooltipTitle.Text = title;
            TooltipDetails.Text = details;
        }

        private void BringToFront()
        {
            Topmost = false;
            Topmost = true;
        }

        private void AutoPositionWindow()
        {
            var workingArea = SystemParameters.WorkArea;
            var desiredWidth = 200;
            var desiredHeight = 80;
            var margin = 20;

            // Try bottom-right corner first
            var desiredLeft = workingArea.Right - desiredWidth - margin;
            var desiredTop = workingArea.Bottom - desiredHeight - margin;

            Left = desiredLeft;
            Top = desiredTop;
        }

        #endregion

        #region Event Handlers

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AutoPositionWindow();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // Ignore drag errors
                }
            }
        }

        private void AutoHideTimer_Tick(object? sender, EventArgs e)
        {
            _autoHideTimer.Stop();
            HideStatus();
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
}
