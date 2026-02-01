using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace WhisperKey
{
    /// <summary>
    /// Google Assistant-style visual listening indicator with pulsing animations and glowing ring effects
    /// </summary>
    public partial class ListeningIndicator : UserControl
    {
        private Storyboard? _pulseAnimation;
        private Storyboard? _rippleAnimation;
        private Storyboard? _idleAnimation;
        private IFeedbackService.DictationStatus _currentStatus = IFeedbackService.DictationStatus.Idle;
        
        // Color schemes for different states
        private static readonly Color GoogleBlue = Color.FromRgb(66, 133, 244);
        private static readonly Color GoogleBlueLight = Color.FromRgb(102, 157, 253);
        private static readonly Color GoogleBlueDark = Color.FromRgb(26, 115, 232);
        private static readonly Color SuccessGreen = Color.FromRgb(40, 167, 69);
        private static readonly Color SuccessGreenLight = Color.FromRgb(72, 187, 99);
        private static readonly Color ErrorRed = Color.FromRgb(220, 53, 69);
        private static readonly Color ErrorRedLight = Color.FromRgb(235, 87, 101);
        private static readonly Color ProcessingYellow = Color.FromRgb(255, 193, 7);
        private static readonly Color ProcessingYellowLight = Color.FromRgb(255, 214, 102);
        private static readonly Color IdleGray = Color.FromRgb(128, 128, 128);
        private static readonly Color IdleGrayLight = Color.FromRgb(160, 160, 160);

        public ListeningIndicator()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Get references to storyboards
            _pulseAnimation = (Storyboard)FindResource("PulseAnimation");
            _rippleAnimation = (Storyboard)FindResource("RippleAnimation");
            _idleAnimation = (Storyboard)FindResource("IdleAnimation");
            
            // Start with idle state
            SetStatus(IFeedbackService.DictationStatus.Idle);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            StopAllAnimations();
        }

        /// <summary>
        /// Updates the indicator based on dictation status
        /// </summary>
        public void SetStatus(IFeedbackService.DictationStatus status)
        {
            if (_currentStatus == status) return;
            
            _currentStatus = status;
            
            // Stop current animations
            StopAllAnimations();
            
            // Update visual appearance based on status
            switch (status)
            {
                case IFeedbackService.DictationStatus.Recording:
                    ApplyRecordingVisuals();
                    StartListeningAnimations();
                    break;
                    
                case IFeedbackService.DictationStatus.Processing:
                    ApplyProcessingVisuals();
                    StartProcessingAnimations();
                    break;
                    
                case IFeedbackService.DictationStatus.Complete:
                    ApplyCompleteVisuals();
                    StartCompleteAnimations();
                    break;
                    
                case IFeedbackService.DictationStatus.Error:
                    ApplyErrorVisuals();
                    StartErrorAnimations();
                    break;
                    
                case IFeedbackService.DictationStatus.Ready:
                    ApplyReadyVisuals();
                    StartReadyAnimations();
                    break;
                    
                case IFeedbackService.DictationStatus.Idle:
                default:
                    ApplyIdleVisuals();
                    StartIdleAnimations();
                    break;
            }
        }

        /// <summary>
        /// Starts the listening state with pulsing and ripple animations
        /// </summary>
        private void StartListeningAnimations()
        {
            _pulseAnimation?.Begin(this, true);
            _rippleAnimation?.Begin(this, true);
        }

        /// <summary>
        /// Starts processing state with faster pulse
        /// </summary>
        private void StartProcessingAnimations()
        {
            // Processing has a faster, more intense pulse
            _pulseAnimation?.Begin(this, true);
            if (_pulseAnimation != null)
            {
                _pulseAnimation.SetSpeedRatio(1.5);
            }
        }

        /// <summary>
        /// Starts complete state with success animation
        /// </summary>
        private void StartCompleteAnimations()
        {
            // Brief success pulse then idle
            _pulseAnimation?.Begin(this, true);
            if (_pulseAnimation != null)
            {
                _pulseAnimation.SetSpeedRatio(0.8);
            }
        }

        /// <summary>
        /// Starts error state with warning animation
        /// </summary>
        private void StartErrorAnimations()
        {
            // Error has a slower, more dramatic pulse
            _pulseAnimation?.Begin(this, true);
            if (_pulseAnimation != null)
            {
                _pulseAnimation.SetSpeedRatio(0.5);
            }
        }

        /// <summary>
        /// Starts ready state with gentle animation
        /// </summary>
        private void StartReadyAnimations()
        {
            _idleAnimation?.Begin(this, true);
        }

        /// <summary>
        /// Starts idle state with subtle breathing
        /// </summary>
        private void StartIdleAnimations()
        {
            _idleAnimation?.Begin(this, true);
        }

        /// <summary>
        /// Stops all running animations
        /// </summary>
        private void StopAllAnimations()
        {
            _pulseAnimation?.Stop(this);
            _rippleAnimation?.Stop(this);
            _idleAnimation?.Stop(this);
            
            // Reset to default speed
            if (_pulseAnimation != null)
            {
                _pulseAnimation.SetSpeedRatio(1.0);
            }
        }

        #region Visual State Methods

        private void ApplyRecordingVisuals()
        {
            // Google Assistant blue theme for recording
            ApplyColorScheme(GoogleBlue, GoogleBlueLight, GoogleBlueDark);
            MainCircle.Opacity = 1.0;
            ShowRings(true);
        }

        private void ApplyProcessingVisuals()
        {
            // Yellow/amber theme for processing
            ApplyColorScheme(ProcessingYellow, ProcessingYellowLight, ProcessingYellow);
            MainCircle.Opacity = 1.0;
            ShowRings(true);
        }

        private void ApplyCompleteVisuals()
        {
            // Green theme for success
            ApplyColorScheme(SuccessGreen, SuccessGreenLight, SuccessGreen);
            MainCircle.Opacity = 1.0;
            ShowRings(false);
        }

        private void ApplyErrorVisuals()
        {
            // Red theme for error
            ApplyColorScheme(ErrorRed, ErrorRedLight, ErrorRed);
            MainCircle.Opacity = 1.0;
            ShowRings(false);
        }

        private void ApplyReadyVisuals()
        {
            // Subtle green theme for ready
            ApplyColorScheme(SuccessGreen, SuccessGreenLight, SuccessGreen);
            MainCircle.Opacity = 0.8;
            ShowRings(false);
        }

        private void ApplyIdleVisuals()
        {
            // Gray theme for idle
            ApplyColorScheme(IdleGray, IdleGrayLight, IdleGray);
            MainCircle.Opacity = 0.7;
            ShowRings(false);
            
            // Hide ripples
            Ripple1.Opacity = 0;
            Ripple2.Opacity = 0;
        }

        private void ApplyColorScheme(Color mainColor, Color lightColor, Color darkColor)
        {
            // Update main circle gradient
            if (MainCircle.Fill is RadialGradientBrush gradient)
            {
                gradient.GradientStops[0].Color = lightColor;
                gradient.GradientStops[1].Color = mainColor;
                gradient.GradientStops[2].Color = darkColor;
            }
            
            // Update glow effect
            if (MainCircle.Effect is DropShadowEffect glow)
            {
                glow.Color = mainColor;
            }
            
            // Update outer rings
            OuterRing1.Stroke = new SolidColorBrush(mainColor);
            OuterRing2.Stroke = new SolidColorBrush(mainColor);
            OuterRing3.Stroke = new SolidColorBrush(mainColor);
            
            // Update ripples
            Ripple1.Stroke = new SolidColorBrush(mainColor);
            Ripple2.Stroke = new SolidColorBrush(lightColor);
        }

        private void ShowRings(bool show)
        {
            if (show)
            {
                OuterRing1.Opacity = 0.3;
                OuterRing2.Opacity = 0.2;
                OuterRing3.Opacity = 0.1;
            }
            else
            {
                OuterRing1.Opacity = 0;
                OuterRing2.Opacity = 0;
                OuterRing3.Opacity = 0;
            }
        }

        #endregion

        /// <summary>
        /// Gets or sets the current status
        /// </summary>
        public IFeedbackService.DictationStatus CurrentStatus
        {
            get => _currentStatus;
            set => SetStatus(value);
        }

        /// <summary>
        /// Shows the indicator with animation
        /// </summary>
        public void Show()
        {
            Visibility = Visibility.Visible;
            
            // Fade in
            var fadeIn = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            
            BeginAnimation(OpacityProperty, fadeIn);
        }

        /// <summary>
        /// Hides the indicator with animation
        /// </summary>
        public void Hide()
        {
            // Fade out
            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };
            
            fadeOut.Completed += (s, e) =>
            {
                Visibility = Visibility.Collapsed;
            };
            
            BeginAnimation(OpacityProperty, fadeOut);
        }

        /// <summary>
        /// Updates the microphone icon visibility
        /// </summary>
        public void ShowMicrophoneIcon(bool show)
        {
            MicrophoneIcon.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Sets the size of the indicator
        /// </summary>
        public void SetSize(double size)
        {
            Width = size;
            Height = size;
        }
    }
}
