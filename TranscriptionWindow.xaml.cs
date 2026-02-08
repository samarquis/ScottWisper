using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace WhisperKey
{
    public partial class TranscriptionWindow : Window
    {
        private readonly DispatcherTimer _autoHideTimer;
        private DateTime _lastActivity = DateTime.Now;
        private IWhisperService? _whisperService;
        private CostTrackingService? _costTrackingService;

        // Windows API for cursor position
        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        public TranscriptionWindow()
        {
            InitializeComponent();
            
            // Set up auto-hide timer
            _autoHideTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(30) // Auto-hide after 30 seconds of inactivity
            };
            _autoHideTimer.Tick += OnAutoHideTimer;
            
            // Window positioning
            this.Loaded += OnWindowLoaded;
            this.KeyDown += OnKeyDown;
            
            // Mouse activity tracking
            this.MouseMove += OnMouseMove;
            TranscriptionText.MouseMove += OnMouseMove;
        }

        public void InitializeServices(IWhisperService whisperService, CostTrackingService costTrackingService)
        {
            _whisperService = whisperService;
            _costTrackingService = costTrackingService;
            
            // Subscribe to transcription events
            if (_whisperService != null)
            {
                _whisperService.TranscriptionCompleted += OnTranscriptionCompleted;
                _whisperService.TranscriptionError += OnTranscriptionError;
            }
            
            if (_costTrackingService != null)
            {
                _costTrackingService.UsageUpdated += OnUsageUpdated;
                UpdateUsageDisplay();
            }
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            PositionWindowNearCursor();
            StartAutoHideTimer();
        }

        private void PositionWindowNearCursor()
        {
            try
            {
                // Get current cursor position
                GetCursorPos(out POINT cursorPos);
                var cursorX = cursorPos.X;
                var cursorY = cursorPos.Y;
                
                // Position window to the right of cursor, or centered if near edge
                var screenWidth = SystemParameters.PrimaryScreenWidth;
                var screenHeight = SystemParameters.PrimaryScreenHeight;
                
                var left = cursorX + 20;
                var top = cursorY - (this.Height / 2);
                
                // Adjust if window would be off-screen
                if (left + this.Width > screenWidth)
                    left = (int)(screenWidth - this.Width - 20);
                if (left < 0)
                    left = 20;
                if (top + this.Height > screenHeight)
                    top = (int)(screenHeight - this.Height - 20);
                if (top < 0)
                    top = 20;
                
                this.Left = left;
                this.Top = top;
            }
            catch
            {
                // Fallback to center screen
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                Hide();
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            _lastActivity = DateTime.Now;
            ResetAutoHideTimer();
        }

        private void OnTranscriptionCompleted(object? sender, string transcriptionText)
        {
            Dispatcher.Invoke(() =>
            {
                AppendTranscriptionText(transcriptionText);
                SetStatus(Status.Ready);
            });
        }

        private void OnTranscriptionError(object? sender, Exception error)
        {
            Dispatcher.Invoke(() =>
            {
                AppendTranscriptionText($"[Error: {error.Message}]");
                SetStatus(Status.Ready);
            });
        }

        private void OnUsageUpdated(object? sender, UsageStats stats)
        {
            Dispatcher.Invoke(UpdateUsageDisplay);
        }

        public void AppendTranscriptionText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            var currentText = TranscriptionText.Text;
            if (currentText == "Listening for your voice...")
            {
                TranscriptionText.Text = text;
            }
            else
            {
                TranscriptionText.Text = currentText + " " + text;
            }

            // Auto-scroll to bottom
            TextScrollViewer.ScrollToBottom();
            
            _lastActivity = DateTime.Now;
            ResetAutoHideTimer();
        }

        public void SetStatus(Status status)
        {
            Dispatcher.Invoke(() =>
            {
                switch (status)
                {
                    case Status.Ready:
                        StatusIndicator.Fill = new SolidColorBrush(Colors.LightGreen);
                        StatusText.Text = "Ready";
                        break;
                    case Status.Recording:
                        StatusIndicator.Fill = new SolidColorBrush(Colors.Red);
                        StatusText.Text = "Recording";
                        break;
                    case Status.Processing:
                        StatusIndicator.Fill = new SolidColorBrush(Colors.Gold);
                        StatusText.Text = "Processing";
                        break;
                }
            });
        }

        private void UpdateUsageDisplay()
        {
            if (_costTrackingService == null) return;
            
            var stats = _costTrackingService.GetUsageStats();
            UsageText.Text = $"{stats.RequestCount} requests | ${stats.EstimatedCost:F4}";
        }

        private void StartAutoHideTimer()
        {
            _autoHideTimer.Start();
        }

        private void ResetAutoHideTimer()
        {
            _autoHideTimer.Stop();
            _autoHideTimer.Start();
        }

        private void OnAutoHideTimer(object? sender, EventArgs e)
        {
            if (DateTime.Now.Subtract(_lastActivity).TotalSeconds > 30)
            {
                Hide();
            }
        }

        public void ShowForDictation()
        {
            SetStatus(Status.Ready);
            PositionWindowNearCursor();
            this.Show();
            this.Activate();
            StartAutoHideTimer();
        }

        public void SetRecordingStatus()
        {
            SetStatus(Status.Recording);
        }

        public void SetProcessingStatus()
        {
            SetStatus(Status.Processing);
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        protected override void OnClosed(EventArgs e)
        {
            // Unsubscribe from events
            if (_whisperService != null)
            {
                _whisperService.TranscriptionCompleted -= OnTranscriptionCompleted;
                _whisperService.TranscriptionError -= OnTranscriptionError;
            }
            
            if (_costTrackingService != null)
            {
                _costTrackingService.UsageUpdated -= OnUsageUpdated;
            }

            _autoHideTimer?.Stop();
            base.OnClosed(e);
        }

        public enum Status
        {
            Ready,
            Recording,
            Processing
        }
    }
}
