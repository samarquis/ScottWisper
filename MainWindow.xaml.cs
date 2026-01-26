using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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

        public MainWindow()
        {
            InitializeComponent();
            Loaded += MainWindow_Loaded;
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