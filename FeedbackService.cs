using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using NAudio.Wave;
using NAudio.CoreAudioApi;

namespace ScottWisper
{
    /// <summary>
    /// Simple wave provider for raw audio data
    /// </summary>
    public class RawWaveProvider : IWaveProvider
    {
        private readonly Stream _stream;
        private WaveFormat _waveFormat;

        public RawWaveProvider(Stream stream)
        {
            _stream = stream;
            _waveFormat = new WaveFormat(44100, 1); // Default to 44.1kHz mono
        }

        public WaveFormat WaveFormat 
        { 
            get => _waveFormat; 
            set => _waveFormat = value; 
        }

        public int Read(byte[] buffer, int offset, int count)
        {
            return _stream.Read(buffer, offset, count);
        }
    }

    /// <summary>
    /// User preferences for feedback customization
    /// </summary>
    public class FeedbackPreferences
    {
        public bool AudioEnabled { get; set; } = true;
        public bool VisualEnabled { get; set; } = true;
        public bool ToastEnabled { get; set; } = true;
        public bool ProgressIndicatorsEnabled { get; set; } = true;
        public float Volume { get; set; } = 0.7f;
        public bool IsMuted { get; set; } = false;
        public FeedbackIntensity Intensity { get; set; } = FeedbackIntensity.Normal;
        public List<NotificationType> EnabledNotifications { get; set; } = new List<NotificationType>
        {
            NotificationType.StatusChange,
            NotificationType.Error,
            NotificationType.Completion
        };
        public bool ShowStatusHistory { get; set; } = true;
        public int MaxHistoryItems { get; set; } = 10;
        public bool UseAdvancedAnimations { get; set; } = true;
        public int ToastDuration { get; set; } = 3000;
    }

    /// <summary>
    /// Feedback intensity levels
    /// </summary>
    public enum FeedbackIntensity
    {
        Subtle,
        Normal,
        Prominent
    }

    /// <summary>
    /// Types of notifications that can be enabled/disabled
    /// </summary>
    public enum NotificationType
    {
        StatusChange,
        Error,
        Completion,
        Warning,
        Info
    }

    /// <summary>
    /// Status history entry for tracking and display
    /// </summary>
    public class StatusHistoryEntry
    {
        public DateTime Timestamp { get; set; }
        public IFeedbackService.DictationStatus Status { get; set; }
        public string? Message { get; set; }
        public TimeSpan Duration { get; set; }
        public NotificationType NotificationType { get; set; }
    }

    /// <summary>
    /// Progress indicator state for long operations
    /// </summary>
    public class ProgressState
    {
        public bool IsActive { get; set; }
        public string Operation { get; set; } = "";
        public double Progress { get; set; } = 0.0;
        public string? Details { get; set; }
        public DateTime StartTime { get; set; }
        public TimeSpan EstimatedDuration { get; set; }
    }

    /// <summary>
    /// Centralized feedback service for managing audio and visual notifications
    /// </summary>
    public class FeedbackService : IFeedbackService, IDisposable
    {
        private IFeedbackService.DictationStatus _currentStatus = IFeedbackService.DictationStatus.Idle;
        private StatusIndicatorWindow? _statusIndicatorWindow;
        private AudioVisualizer? _audioVisualizer;
        private bool _isDisposed = false;
        private readonly object _lockObject = new object();

        // Advanced audio feedback
        private readonly WaveOut? _waveOut;
        private MMDeviceEnumerator? _deviceEnumerator;
        
        // Cache simple tone players for fallback
        private readonly SoundPlayer? _readySound;
        private readonly SoundPlayer? _recordingSound;
        private readonly SoundPlayer? _processingSound;
        private readonly SoundPlayer? _successSound;
        private readonly SoundPlayer? _errorSound;

        // Enhanced feedback features
        private FeedbackPreferences _preferences;
        private readonly Queue<StatusHistoryEntry> _statusHistory;
        private ProgressState _currentProgress;
        private DateTime _lastStatusChange;
        private readonly Dictionary<IFeedbackService.DictationStatus, (float frequency, int duration)[]> _toneSequences;
        
        // Volume and mute state
        private float _volume = 0.8f;
        private bool _isMuted = false;

        public event EventHandler<IFeedbackService.DictationStatus>? StatusChanged;
        public event EventHandler<byte[]>? AudioDataForVisualization;
        public event EventHandler<StatusHistoryEntry>? StatusHistoryUpdated;
        public event EventHandler<ProgressState>? ProgressUpdated;

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
                        var oldStatus = _currentStatus;
                        _currentStatus = value;
                        
                        // Track status change
                        TrackStatusChange(oldStatus, value);
                        
                        StatusChanged?.Invoke(this, value);
                    }
                }
            }
        }

        public FeedbackPreferences Preferences 
        { 
            get => _preferences;
            set => _preferences = value ?? new FeedbackPreferences();
        }

        public IReadOnlyList<StatusHistoryEntry> StatusHistory 
        {
            get
            {
                lock (_lockObject)
                {
                    return _statusHistory.ToList().AsReadOnly();
                }
            }
        }

        public ProgressState CurrentProgress => _currentProgress;

        public FeedbackService()
        {
            try
            {
                _waveOut = new WaveOut();
                _deviceEnumerator = new MMDeviceEnumerator();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NAudio initialization failed: {ex.Message}");
            }
            
            // Initialize enhanced feedback features
            _preferences = new FeedbackPreferences();
            _statusHistory = new Queue<StatusHistoryEntry>();
            _currentProgress = new ProgressState();
            _lastStatusChange = DateTime.Now;
            
            // Initialize tone sequences for different intensities
            _toneSequences = InitializeToneSequences();
            
            // Initialize fallback sound players
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

            // Play audio feedback based on preferences
            if (_preferences.AudioEnabled)
            {
                await PlayAudioFeedbackAsync(status);
            }

            // Show visual feedback if message provided and enabled
            if (!string.IsNullOrEmpty(message) && _preferences.ToastEnabled)
            {
                var notificationType = DetermineNotificationType(status);
                if (_preferences.EnabledNotifications.Any(nt => nt.ToString() == notificationType.ToString()))
                {
                    await ShowNotificationAsync(GetStatusTitle(status), message, _preferences.ToastDuration);
                }
            }

            // Update system tray if available
            UpdateSystemTrayStatus(status);

            // Handle visualization
            await HandleVisualizationAsync(status);

            // Show enhanced visual feedback if enabled
            if (_preferences.VisualEnabled)
            {
                await ShowEnhancedVisualFeedbackAsync(status, message);
            }

            await Task.CompletedTask;
        }

        public async Task ShowNotificationAsync(string title, string message, int duration = 3000)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Try to show system tray notification first
                if (Application.Current.Properties["SystemTray"] is SystemTrayService systemTray)
                {
                    systemTray.ShowNotification(message, title, duration);
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

        public async Task ShowToastNotificationAsync(string title, string message, IFeedbackService.NotificationType type = IFeedbackService.NotificationType.Info)
        {
            if (!_preferences.ToastEnabled || !_preferences.EnabledNotifications.Any(nt => nt.ToString() == type.ToString()))
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // Create custom toast notification window
                    var toastWindow = CreateToastWindow(title, message, type);
                    toastWindow.Show();
                    
                    // Auto-hide after duration
                    var timer = new System.Windows.Threading.DispatcherTimer
                    {
                        Interval = TimeSpan.FromMilliseconds(_preferences.ToastDuration)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        toastWindow.Close();
                    };
                    timer.Start();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Toast notification error: {ex.Message}");
                }
            });
        }

        public async Task PlayAudioFeedbackAsync(IFeedbackService.DictationStatus status)
        {
            if (_preferences.IsMuted || !_preferences.AudioEnabled)
                return;

            await Task.Run(() =>
            {
                try
                {
                    // Try advanced tone generation first
                    if (_waveOut != null)
                    {
                        PlayAdvancedToneSequence(status);
                    }
                    else
                    {
                        // Fallback to simple sound players
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
                }
                catch (Exception ex)
                {
                    // Fail silently for audio errors to not interrupt main functionality
                    System.Diagnostics.Debug.WriteLine($"Audio feedback error: {ex.Message}");
                }
            });
        }

        public async Task StartProgressAsync(string operation, TimeSpan? estimatedDuration = null)
        {
            if (!_preferences.ProgressIndicatorsEnabled)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _currentProgress = new ProgressState
                {
                    IsActive = true,
                    Operation = operation,
                    Progress = 0.0,
                    StartTime = DateTime.Now,
                    EstimatedDuration = estimatedDuration ?? TimeSpan.FromSeconds(30)
                };

                ProgressUpdated?.Invoke(this, _currentProgress);
                
                // Show progress notification
                ShowToastNotificationAsync("Progress", $"{operation}...", IFeedbackService.NotificationType.Info);
            });
        }

        public async Task UpdateProgressAsync(double progress, string? details = null)
        {
            if (!_preferences.ProgressIndicatorsEnabled || !_currentProgress.IsActive)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _currentProgress.Progress = Math.Max(0, Math.Min(100, progress));
                if (!string.IsNullOrEmpty(details))
                {
                    _currentProgress.Details = details;
                }

                ProgressUpdated?.Invoke(this, _currentProgress);
            });
        }

        public async Task CompleteProgressAsync(string? completionMessage = null)
        {
            if (!_preferences.ProgressIndicatorsEnabled)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (_currentProgress.IsActive)
                {
                    _currentProgress.IsActive = false;
                    _currentProgress.Progress = 100.0;
                    
                    var message = completionMessage ?? $"{_currentProgress.Operation} completed";
                    ShowToastNotificationAsync("Complete", message, IFeedbackService.NotificationType.Completion);
                    
                    ProgressUpdated?.Invoke(this, _currentProgress);
                }
            });
        }

        public void UpdatePreferences(FeedbackPreferences preferences)
        {
            _preferences = preferences ?? new FeedbackPreferences();
            
            // Apply volume changes immediately
            SetVolume(_preferences.Volume);
            SetMuted(_preferences.IsMuted);
        }

        private void PlayAdvancedToneSequence(IFeedbackService.DictationStatus status)
        {
            if (!_toneSequences.TryGetValue(status, out var baseSequence))
                return;

            // Adjust sequence based on intensity
            var adjustedSequence = AdjustSequenceForIntensity(baseSequence, _preferences.Intensity);

            foreach (var (frequency, duration) in adjustedSequence)
            {
                PlayTone(frequency, duration, _preferences.Volume);
                Thread.Sleep(duration + 50); // Small gap between tones
            }
        }

        private void PlayTone(float frequency, int durationMs, float volume)
        {
            try
            {
                var sampleRate = 44100;
                var samples = durationMs * sampleRate / 1000;
                var buffer = new byte[samples * 2];
                
                for (int i = 0; i < samples; i++)
                {
                    var sample = (short)(Math.Sin(2 * Math.PI * frequency * i / sampleRate) * short.MaxValue * volume);
                    buffer[i * 2] = (byte)(sample & 0xFF);
                    buffer[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
                }

                var waveProvider = new RawWaveProvider(new MemoryStream(buffer))
                {
                    WaveFormat = new WaveFormat(sampleRate, 1)
                };

                using (var player = new WaveOut())
                {
                    player.Init(waveProvider);
                    player.Play();
                    while (player.PlaybackState == PlaybackState.Playing)
                    {
                        Thread.Sleep(10);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Tone playback error: {ex.Message}");
            }
        }

        public void SetVolume(float volume)
        {
            var clampedVolume = Math.Max(0, Math.Min(1, volume));
            if (_preferences.Volume != clampedVolume)
            {
                _preferences.Volume = clampedVolume;
            }
        }

        public void SetMuted(bool muted)
        {
            if (_preferences.IsMuted != muted)
            {
                _preferences.IsMuted = muted;
            }
        }

        public void SetAudioVisualizer(AudioVisualizer visualizer)
        {
            _audioVisualizer = visualizer;
        }

        public float GetVolume() => _volume;
        public bool IsMuted() => _isMuted;

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

        private StatusIndicatorWindow CreateStatusIndicatorWindow()
        {
            var window = new StatusIndicatorWindow();
            return window;
        }

        private void UpdateStatusIndicatorAppearance(IFeedbackService.DictationStatus status)
        {
            _statusIndicatorWindow?.UpdateStatus(status);
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

        private async Task HandleVisualizationAsync(IFeedbackService.DictationStatus status)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    switch (status)
                    {
                        case IFeedbackService.DictationStatus.Recording:
                            // Start visualization when recording begins
                            if (_audioVisualizer != null)
                            {
                                _audioVisualizer.StartVisualization();
                            }
                            break;

                        case IFeedbackService.DictationStatus.Processing:
                        case IFeedbackService.DictationStatus.Complete:
                        case IFeedbackService.DictationStatus.Error:
                        case IFeedbackService.DictationStatus.Idle:
                            // Stop visualization when recording ends
                            if (_audioVisualizer != null)
                            {
                                _audioVisualizer.StopVisualization();
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Visualization error: {ex.Message}");
                }
            });
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

        public async Task StartProgressAsync(string title, TimeSpan timeout)
        {
            // Simple implementation - could be enhanced with actual progress UI
            await ShowStatusIndicatorAsync(IFeedbackService.DictationStatus.Processing, (int)timeout.TotalMilliseconds);
        }

        public async Task UpdateProgressAsync(int percentage, string message)
        {
            // Simple implementation - could be enhanced with actual progress UI
            await SetStatusAsync(IFeedbackService.DictationStatus.Processing, $"{percentage}% - {message}");
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

            // Dispose audio resources
            _waveOut?.Dispose();
            _deviceEnumerator?.Dispose();

            // Dispose fallback sound players
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

        /// <summary>
        /// Forward audio data to visualization component
        /// </summary>
        /// <param name="audioData">Raw audio bytes</param>
        public void UpdateAudioVisualization(byte[] audioData)
        {
            AudioDataForVisualization?.Invoke(this, audioData);
            
            // Also update local audio visualizer if available
            if (_audioVisualizer != null)
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _audioVisualizer.UpdateAudioData(audioData);
                });
            }
        }

        // Enhanced feedback helper methods

        private Dictionary<IFeedbackService.DictationStatus, (float frequency, int duration)[]> InitializeToneSequences()
        {
            return new Dictionary<IFeedbackService.DictationStatus, (float, int)[]>
            {
                [IFeedbackService.DictationStatus.Ready] = new[] { (523.25f, 150), (659.25f, 150), (783.99f, 150) }, // C4→E4→G4
                [IFeedbackService.DictationStatus.Recording] = new[] { (261.63f, 200), (329.63f, 200), (392.00f, 200) }, // C4→E4→G4 (lower)
                [IFeedbackService.DictationStatus.Complete] = new[] { (523.25f, 150), (659.25f, 150), (783.99f, 150), (1046.50f, 300) }, // C5→E5→G5→C6
                [IFeedbackService.DictationStatus.Error] = new[] { (100.0f, 200), (100.0f, 200) }, // Low buzzer × 2
                [IFeedbackService.DictationStatus.Processing] = new[] { (440.0f, 100) }, // Attention chime
                [IFeedbackService.DictationStatus.Idle] = new[] { (440.0f, 50) } // Short indicator
            };
        }

        private (float frequency, int duration)[] AdjustSequenceForIntensity((float frequency, int duration)[] baseSequence, FeedbackIntensity intensity)
        {
            return intensity switch
            {
                FeedbackIntensity.Subtle => baseSequence.Select(t => (t.frequency, t.duration / 2)).ToArray(),
                FeedbackIntensity.Normal => baseSequence,
                FeedbackIntensity.Prominent => baseSequence.Select(t => (t.frequency * 1.1f, (int)(t.duration * 1.2))).ToArray(),
                _ => baseSequence
            };
        }

        private IFeedbackService.NotificationType DetermineNotificationType(IFeedbackService.DictationStatus status)
        {
            return status switch
            {
                IFeedbackService.DictationStatus.Error => IFeedbackService.NotificationType.Error,
                IFeedbackService.DictationStatus.Complete => IFeedbackService.NotificationType.Completion,
                IFeedbackService.DictationStatus.Processing => IFeedbackService.NotificationType.Info,
                IFeedbackService.DictationStatus.Recording => IFeedbackService.NotificationType.Info,
                _ => IFeedbackService.NotificationType.StatusChange
            };
        }

        private Window CreateToastWindow(string title, string message, IFeedbackService.NotificationType type)
        {
            var window = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ResizeMode = ResizeMode.NoResize,
                Width = 300,
                Height = 100
            };

            // Position toast in top-right corner of primary screen
            var primaryScreen = SystemParameters.PrimaryScreenWidth;
            var screenWidth = (double)primaryScreen;
            window.Left = screenWidth - 320;
            window.Top = 50;

            // Create toast content
            var grid = new Grid();
            grid.Background = type switch
            {
                IFeedbackService.NotificationType.Error => new SolidColorBrush(Color.FromArgb(200, 220, 53, 69)),
                IFeedbackService.NotificationType.Warning => new SolidColorBrush(Color.FromArgb(200, 255, 193, 7)),
                IFeedbackService.NotificationType.Completion => new SolidColorBrush(Color.FromArgb(200, 40, 167, 69)),
                _ => new SolidColorBrush(Color.FromArgb(200, 33, 150, 243))
            };

            // Add title
            var titleBlock = new TextBlock
            {
                Text = title,
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 5, 10, 0)
            };

            // Add message
            var messageBlock = new TextBlock
            {
                Text = message,
                FontSize = 12,
                Foreground = Brushes.White,
                Margin = new Thickness(10, 25, 10, 5),
                TextWrapping = TextWrapping.Wrap
            };

            grid.Children.Add(titleBlock);
            grid.Children.Add(messageBlock);
            window.Content = grid;

            // Add fade-in animation
            if (_preferences.UseAdvancedAnimations)
            {
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                window.BeginAnimation(Window.OpacityProperty, fadeIn);
            }

            return window;
        }

        private async Task ShowEnhancedVisualFeedbackAsync(IFeedbackService.DictationStatus status, string? message)
        {
            if (!_preferences.VisualEnabled)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Show status indicator with enhanced animations
                if (_preferences.UseAdvancedAnimations)
                {
                    ShowStatusIndicatorAsync(status, 2000);
                }
                
                // Show detailed status window for important status changes
                if (_preferences.ShowStatusHistory && ShouldShowDetailedStatus(status))
                {
                    ShowDetailedStatusWindow(status, message);
                }
            });
        }

        private bool ShouldShowDetailedStatus(IFeedbackService.DictationStatus status)
        {
            return status switch
            {
                IFeedbackService.DictationStatus.Complete => true,
                IFeedbackService.DictationStatus.Error => true,
                _ => false
            };
        }

        private void ShowDetailedStatusWindow(IFeedbackService.DictationStatus status, string? message)
        {
            // This would show a detailed status window - for now, just use the existing status indicator
            ShowStatusIndicatorAsync(status, 3000);
        }

        private void TrackStatusChange(IFeedbackService.DictationStatus oldStatus, IFeedbackService.DictationStatus newStatus)
        {
            var duration = DateTime.Now - _lastStatusChange;
            _lastStatusChange = DateTime.Now;

            var entry = new StatusHistoryEntry
            {
                Timestamp = DateTime.Now,
                Status = newStatus,
                Message = null, // Could be enhanced to include contextual messages
                Duration = duration,
                NotificationType = (NotificationType)DetermineNotificationType(newStatus)
            };

            // Add to history
            lock (_lockObject)
            {
                _statusHistory.Enqueue(entry);
                
                // Limit history size
                while (_statusHistory.Count > _preferences.MaxHistoryItems)
                {
                    _statusHistory.Dequeue();
                }
            }

            // Notify listeners
            StatusHistoryUpdated?.Invoke(this, entry);
        }
    }
}