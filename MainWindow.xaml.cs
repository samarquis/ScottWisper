using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ScottWisper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private IFeedbackService? _feedbackService;
        private bool _isHidden = false;

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
            // Initialize feedback service
            _feedbackService = new FeedbackService();
            await _feedbackService.InitializeAsync();

            // Subscribe to status changes
            _feedbackService.StatusChanged += OnFeedbackStatusChanged;

            // Set initial status
            await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Ready, "Application ready");

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
            // Update status text
            StatusText.Text = status.ToString();
            FooterStatus.Text = status.ToString();

            // Update status indicator color
            var color = status switch
            {
                IFeedbackService.DictationStatus.Idle => Colors.Gray,
                IFeedbackService.DictationStatus.Ready => Colors.Green,
                IFeedbackService.DictationStatus.Recording => Colors.Red,
                IFeedbackService.DictationStatus.Processing => Colors.Yellow,
                IFeedbackService.DictationStatus.Complete => Colors.Green,
                IFeedbackService.DictationStatus.Error => Colors.Red,
                _ => Colors.Gray
            };

            StatusIndicator.Fill = new SolidColorBrush(color);

            // Update time
            StatusTime.Text = DateTime.Now.ToString("HH:mm:ss");

            // Update footer message based on status
            FooterMessage.Text = status switch
            {
                IFeedbackService.DictationStatus.Idle => "Application idle",
                IFeedbackService.DictationStatus.Ready => "Ready for dictation",
                IFeedbackService.DictationStatus.Recording => "Recording speech...",
                IFeedbackService.DictationStatus.Processing => "Processing transcription",
                IFeedbackService.DictationStatus.Complete => "Transcription completed",
                IFeedbackService.DictationStatus.Error => "Error occurred",
                _ => "Unknown status"
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
        public async Task UpdateFeedbackStatus(IFeedbackService.DictationStatus status, string? message = null)
        {
            if (_feedbackService != null)
            {
                await _feedbackService.SetStatusAsync(status, message);
            }
        }

        public IFeedbackService? FeedbackService => _feedbackService;

        protected override async void OnClosed(EventArgs e)
        {
            // Cleanup feedback service
            if (_feedbackService != null)
            {
                await _feedbackService.DisposeAsync();
            }

            base.OnClosed(e);
        }
    }
}