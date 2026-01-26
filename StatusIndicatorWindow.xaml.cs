using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using System.ComponentModel;

namespace ScottWisper
{
    /// <summary>
    /// Interaction logic for StatusIndicatorWindow.xaml
    /// </summary>
    public partial class StatusIndicatorWindow : Window, INotifyPropertyChanged
    {
        private readonly DispatcherTimer _autoHideTimer;
        private Storyboard? _currentAnimation;
        private IFeedbackService.DictationStatus _currentStatus = IFeedbackService.DictationStatus.Idle;
        
        public event EventHandler? StatusUpdateRequested;

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
            // Stop any current animation
            _currentAnimation?.Stop();

            // Hide all indicators
            IdleIndicator.Visibility = Visibility.Collapsed;
            RecordingIndicator.Visibility = Visibility.Collapsed;
            ProcessingIndicator.Visibility = Visibility.Collapsed;
            CompleteIndicator.Visibility = Visibility.Collapsed;
            ErrorIndicator.Visibility = Visibility.Collapsed;

            // Show appropriate indicator and animation
            switch (CurrentStatus)
            {
                case IFeedbackService.DictationStatus.Idle:
                    IdleIndicator.Visibility = Visibility.Visible;
                    MainGrid.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 128, 128, 128));
                    break;

                case IFeedbackService.DictationStatus.Ready:
                    IdleIndicator.Visibility = Visibility.Visible;
                    MainGrid.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 68, 255, 68));
                    break;

                case IFeedbackService.DictationStatus.Recording:
                    RecordingIndicator.Visibility = Visibility.Visible;
                    MainGrid.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 68, 68));
                    _currentAnimation = (Storyboard)FindResource("PulseAnimation");
                    _currentAnimation.Begin(RecordingIndicator, true);
                    break;

                case IFeedbackService.DictationStatus.Processing:
                    ProcessingIndicator.Visibility = Visibility.Visible;
                    MainGrid.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 68, 68, 255));
                    _currentAnimation = (Storyboard)FindResource("SpinAnimation");
                    _currentAnimation.Begin(ProcessingIcon, true);
                    break;

                case IFeedbackService.DictationStatus.Complete:
                    CompleteIndicator.Visibility = Visibility.Visible;
                    MainGrid.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 68, 255, 68));
                    _currentAnimation = (Storyboard)FindResource("FadeAnimation");
                    _currentAnimation.Completed += (s, e) => HideStatus();
                    _currentAnimation.Begin(MainGrid, false);
                    break;

                case IFeedbackService.DictationStatus.Error:
                    ErrorIndicator.Visibility = Visibility.Visible;
                    MainGrid.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(128, 255, 68, 68));
                    _currentAnimation = (Storyboard)FindResource("ShakeAnimation");
                    _currentAnimation.Begin(ErrorIcon, true);
                    break;
            }

            UpdateTooltip();
        }

        private void UpdateTooltip()
        {
            var (title, details) = CurrentStatus switch
            {
                IFeedbackService.DictationStatus.Idle => ("ScottWisper - Idle", "Ready to start dictation"),
                IFeedbackService.DictationStatus.Ready => ("ScottWisper - Ready", "Press hotkey to begin recording"),
                IFeedbackService.DictationStatus.Recording => ("ScottWisper - Recording", "Listening for speech..."),
                IFeedbackService.DictationStatus.Processing => ("ScottWisper - Processing", "Transcribing audio to text"),
                IFeedbackService.DictationStatus.Complete => ("ScottWisper - Complete", "Transcription finished successfully"),
                IFeedbackService.DictationStatus.Error => ("ScottWisper - Error", "An error occurred during processing"),
                _ => ("ScottWisper", "Status unknown")
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

            // Check if this would overlap with cursor (simplified check)
            var cursorPos = System.Windows.Input.Mouse.GetPosition(this);
            // For now, use default positioning - cursor detection would need Win32 API calls
            {
                // Move to top-left corner to avoid cursor
                desiredLeft = workingArea.Left + margin;
                desiredTop = workingArea.Top + margin;
            }

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

        protected override void OnLocationChanged(EventArgs e)
        {
            base.OnLocationChanged(e);
            // Position memory could be implemented with settings file
            // For now, we'll use default auto-positioning
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region Accessibility Support

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Set window to be click-through when needed for non-intrusive behavior
            // This requires Windows API calls for full click-through functionality
        }

        #endregion
    }
}