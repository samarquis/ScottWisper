using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace ScottWisper
{
    /// <summary>
    /// Centralized feedback service for managing audio and visual notifications
    /// </summary>
    public class FeedbackService : IFeedbackService, IDisposable
    {
        private IFeedbackService.DictationStatus _currentStatus = IFeedbackService.DictationStatus.Idle;
        private Window? _statusIndicatorWindow;
        private bool _isDisposed = false;
        private readonly object _lockObject = new object();

        // Cache sound players for performance
        private readonly SoundPlayer? _readySound;
        private readonly SoundPlayer? _recordingSound;
        private readonly SoundPlayer? _processingSound;
        private readonly SoundPlayer? _successSound;
        private readonly SoundPlayer? _errorSound;

        public event EventHandler<IFeedbackService.DictationStatus>? StatusChanged;

        public IFeedbackService.DictationStatus CurrentStatus 
        { 
            get 
            { 
                lock (_lockObject)
                {
                    return _currentStatus;
                }
            }
            private set
            {
                lock (_lockObject)
                {
                    if (_currentStatus != value)
                    {
                        _currentStatus = value;
                        StatusChanged?.Invoke(this, value);
                    }
                }
            }
        }

        public FeedbackService()
        {
            // Initialize sound players
            _readySound = CreateToneSound(800, 100);   // High beep
            _recordingSound = CreateToneSound(600, 150);  // Lower beep
            _processingSound = CreateToneSound(400, 200);  // Low beep
            _successSound = CreateToneSound(1000, 300); // Success chirp
            _errorSound = CreateToneSound(300, 500);   // Error buzz
        }

        public async Task InitializeAsync()
        {
            await Task.CompletedTask;
            // Initialize any UI components if needed
        }

        public async Task SetStatusAsync(IFeedbackService.DictationStatus status, string? message = null)
        {
            if (_isDisposed)
                return;

            CurrentStatus = status;

            // Play audio feedback
            await PlayAudioFeedbackAsync(status);

            // Show visual feedback if message provided
            if (!string.IsNullOrEmpty(message))
            {
                await ShowNotificationAsync(GetStatusTitle(status), message);
            }

            // Update system tray if available
            UpdateSystemTrayStatus(status);

            await Task.CompletedTask;
        }

        public async Task ShowNotificationAsync(string title, string message, int duration = 3000)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Try to show system tray notification first
                if (Application.Current.Properties["SystemTray"] is SystemTrayService systemTray)
                {
                    systemTray.ShowNotification(message, title);
                }
                else
                {
                    // Fallback to message box for critical notifications
                    if (title.Contains("Error"))
                    {
                        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            });
        }

        public async Task PlayAudioFeedbackAsync(IFeedbackService.DictationStatus status)
        {
            await Task.Run(() =>
            {
                try
                {
                    var soundPlayer = status switch
                    {
                        IFeedbackService.DictationStatus.Ready => _readySound,
                        IFeedbackService.DictationStatus.Recording => _recordingSound,
                        IFeedbackService.DictationStatus.Processing => _processingSound,
                        IFeedbackService.DictationStatus.Complete => _successSound,
                        IFeedbackService.DictationStatus.Error => _errorSound,
                        _ => null
                    };

                    soundPlayer?.Play();
                }
                catch (Exception ex)
                {
                    // Fail silently for audio errors to not interrupt main functionality
                    System.Diagnostics.Debug.WriteLine($"Audio feedback error: {ex.Message}");
                }
            });
        }

        public async Task ShowStatusIndicatorAsync(IFeedbackService.DictationStatus status, int duration = 2000)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Create or update status indicator window
                    if (_statusIndicatorWindow == null)
                    {
                        _statusIndicatorWindow = CreateStatusIndicatorWindow();
                    }

                    UpdateStatusIndicatorAppearance(status);
                    
                    // Show window
                    _statusIndicatorWindow.Show();
                    _statusIndicatorWindow.Topmost = true;

                    // Auto-hide after duration
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(duration)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        HideStatusIndicator();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Status indicator error: {ex.Message}");
                }
            });
        }

        public async Task ClearStatusIndicatorAsync()
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                HideStatusIndicator();
            });
        }

        private Window CreateStatusIndicatorWindow()
        {
            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                ResizeMode = ResizeMode.NoResize,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                Width = 120,
                Height = 40,
                Left = SystemParameters.WorkArea.Width - 140,
                Top = SystemParameters.WorkArea.Height - 60
            };

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10),
                Background = Brushes.Black,
                Opacity = 0.8
            };

            var textBlock = new TextBlock
            {
                Name = "StatusText",
                Text = "Ready",
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            border.Child = textBlock;
            window.Content = border;

            return window;
        }

        private void UpdateStatusIndicatorAppearance(IFeedbackService.DictationStatus status)
        {
            if (_statusIndicatorWindow?.Content is Border border && border.Child is TextBlock textBlock)
            {
                var (color, text) = status switch
                {
                    IFeedbackService.DictationStatus.Idle => (Brushes.Gray, "Idle"),
                    IFeedbackService.DictationStatus.Ready => (Brushes.Green, "Ready"),
                    IFeedbackService.DictationStatus.Recording => (Brushes.Red, "Recording"),
                    IFeedbackService.DictationStatus.Processing => (Brushes.Yellow, "Processing"),
                    IFeedbackService.DictationStatus.Complete => (Brushes.Green, "Complete"),
                    IFeedbackService.DictationStatus.Error => (Brushes.Red, "Error"),
                    _ => (Brushes.Gray, "Unknown")
                };

                textBlock.Text = text;
                var baseColor = ((SolidColorBrush)color).Color;
                border.Background = new SolidColorBrush(Color.FromArgb(200, baseColor.R, baseColor.G, baseColor.B));
            }
        }

        private void HideStatusIndicator()
        {
            if (_statusIndicatorWindow != null)
            {
                _statusIndicatorWindow.Hide();
            }
        }

        private void UpdateSystemTrayStatus(IFeedbackService.DictationStatus status)
        {
            // Update system tray status if available
            if (Application.Current.Properties["SystemTray"] is SystemTrayService systemTray)
            {
                var isDictating = status == IFeedbackService.DictationStatus.Recording;
                systemTray.UpdateDictationStatus(isDictating);

                // Show balloon tip for status changes
                var message = status switch
                {
                    IFeedbackService.DictationStatus.Ready => "ScottWisper is ready",
                    IFeedbackService.DictationStatus.Recording => "Recording started",
                    IFeedbackService.DictationStatus.Processing => "Processing speech...",
                    IFeedbackService.DictationStatus.Complete => "Transcription complete",
                    IFeedbackService.DictationStatus.Error => "An error occurred",
                    _ => null
                };

                if (!string.IsNullOrEmpty(message))
                {
                    systemTray.ShowBalloonTip("Status Update", message);
                }
            }
        }

        private string GetStatusTitle(IFeedbackService.DictationStatus status)
        {
            return status switch
            {
                IFeedbackService.DictationStatus.Idle => "ScottWisper",
                IFeedbackService.DictationStatus.Ready => "Ready",
                IFeedbackService.DictationStatus.Recording => "Recording",
                IFeedbackService.DictationStatus.Processing => "Processing",
                IFeedbackService.DictationStatus.Complete => "Complete",
                IFeedbackService.DictationStatus.Error => "Error",
                _ => "Status"
            };
        }

        private SoundPlayer? CreateToneSound(int frequency, int duration)
        {
            try
            {
                // Create a simple WAV file in memory for the tone
                using (var stream = new MemoryStream())
                {
                    // WAV header
                    stream.Write(new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0, 4); // "RIFF"
                    stream.Write(BitConverter.GetBytes(36 + duration * 2), 0, 4); // File size - 8
                    stream.Write(new byte[] { 0x57, 0x41, 0x56, 0x45 }, 0, 4); // "WAVE"
                    stream.Write(new byte[] { 0x66, 0x6D, 0x74, 0x20 }, 0, 4); // "fmt "
                    stream.Write(BitConverter.GetBytes(16), 0, 4); // Subchunk1Size
                    stream.Write(new byte[] { 0x01, 0x00 }, 0, 2); // AudioFormat (PCM)
                    stream.Write(new byte[] { 0x01, 0x00 }, 0, 2); // NumChannels (mono)
                    stream.Write(BitConverter.GetBytes(44100), 0, 4); // SampleRate
                    stream.Write(BitConverter.GetBytes(88200), 0, 4); // ByteRate
                    stream.Write(new byte[] { 0x02, 0x00 }, 0, 2); // BlockAlign
                    stream.Write(new byte[] { 0x10, 0x00 }, 0, 2); // BitsPerSample
                    
                    // Data chunk
                    stream.Write(new byte[] { 0x64, 0x61, 0x74, 0x61 }, 0, 4); // "data"
                    stream.Write(BitConverter.GetBytes(duration * 2), 0, 4); // Subchunk2Size
                    
                    // Generate tone data
                    var samples = duration * 44100 / 1000;
                    for (int i = 0; i < samples; i++)
                    {
                        var value = (short)(Math.Sin(2 * Math.PI * frequency * i / 44100) * short.MaxValue * 0.3);
                        stream.Write(BitConverter.GetBytes(value), 0, 2);
                    }

                    return new SoundPlayer(stream);
                }
            }
            catch
            {
                return null;
            }
        }

        public async Task DisposeAsync()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _statusIndicatorWindow?.Close();
                _statusIndicatorWindow = null;
            });

            // Dispose sound players
            _readySound?.Dispose();
            _recordingSound?.Dispose();
            _processingSound?.Dispose();
            _successSound?.Dispose();
            _errorSound?.Dispose();
        }

        public void Dispose()
        {
            DisposeAsync().GetAwaiter().GetResult();
        }
    }
}