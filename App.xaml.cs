using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ScottWisper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private MainWindow? _mainWindow;
        private HotkeyService? _hotkeyService;
        private WhisperService? _whisperService;
        private AudioCaptureService? _audioCaptureService;
        private CostTrackingService? _costTrackingService;
        private TranscriptionWindow? _transcriptionWindow;
        private SystemTrayService? _systemTrayService;
        private bool _isDictating = false;
        private readonly object _dictationLock = new object();
        private TextInjectionService? _textInjectionService;
        private bool _textInjectionEnabled = true;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Initialize services
            await InitializeServices();

            _mainWindow = new MainWindow();
            
            // Initialize hotkey service
            _hotkeyService = new HotkeyService();
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            
            // Initialize system tray service
            _systemTrayService = new SystemTrayService();
            _systemTrayService.StartDictationRequested += OnSystemTrayStartDictation;
            _systemTrayService.StopDictationRequested += OnSystemTrayStopDictation;
            _systemTrayService.SettingsRequested += OnSystemTraySettings;
            _systemTrayService.ExitRequested += OnSystemTrayExit;
            _systemTrayService.Initialize();
            
            // Hide main window - run in system tray
            _mainWindow.ShowInTaskbar = false;
            _mainWindow.WindowState = WindowState.Minimized;
            _mainWindow.Hide();
        }

        private async Task InitializeServices()
        {
            try
            {
                // Initialize core services
                _whisperService = new WhisperService();
                _costTrackingService = new CostTrackingService();
                _audioCaptureService = new AudioCaptureService();
                _textInjectionService = new TextInjectionService();

                // Initialize transcription window
                _transcriptionWindow = new TranscriptionWindow();
                _transcriptionWindow.InitializeServices(_whisperService, _costTrackingService);

                // Initialize text injection service
                await InitializeTextInjectionService();

                // Wire up service events
                _whisperService.TranscriptionError += OnTranscriptionError;
                _whisperService.TranscriptionCompleted += OnTranscriptionCompleted;
                _costTrackingService.FreeTierWarning += OnFreeTierWarning;
                _costTrackingService.FreeTierExceeded += OnFreeTierExceeded;

                // Configure audio capture service
                _audioCaptureService.AudioDataCaptured += OnAudioDataAvailable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize services: {ex.Message}", "ScottWisper Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private async void OnHotkeyPressed(object? sender, EventArgs e)
        {
            await HandleDictationToggle();
        }

        private async Task HandleDictationToggle()
        {
            Task dictationTask;
            lock (_dictationLock)
            {
                if (_isDictating)
                {
                    // Stop dictation
                    dictationTask = StopDictationInternal();
                }
                else
                {
                    // Start dictation
                    dictationTask = StartDictationInternal();
                }
            }
            await dictationTask;
        }

        private async Task StartDictationInternal()
        {
            try
            {
                // Show transcription window
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.ShowForDictation();
                });

                // Start audio capture
                await _audioCaptureService?.StartCaptureAsync()!;
                
                _isDictating = true;
                
                // Update transcription window status
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.SetRecordingStatus();
                    _systemTrayService?.UpdateDictationStatus(true);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    MessageBox.Show($"Failed to start dictation: {ex.Message}", "Dictation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    _systemTrayService?.UpdateDictationStatus(false);
                });
            }
        }

        private async Task StopDictationInternal()
        {
            try
            {
                // Stop audio capture
                await _audioCaptureService?.StopCaptureAsync()!;
                
                // Update transcription window status
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.SetProcessingStatus();
                    _systemTrayService?.UpdateDictationStatus(false);
                });
                
                _isDictating = false;
                
                // Hide transcription window after a delay
                await Task.Delay(2000);
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.Hide();
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping dictation: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    _systemTrayService?.UpdateDictationStatus(false);
                });
            }
        }

        private async void OnAudioDataAvailable(object? sender, byte[] audioData)
        {
            if (!_isDictating || _whisperService == null || _costTrackingService == null) 
                return;

            try
            {
                // Process audio through Whisper API
                var transcription = await _whisperService.TranscribeAudioAsync(audioData);
                
                // Track usage
                _costTrackingService.TrackUsage(audioData.Length, true);
            }
            catch (Exception ex)
            {
                // Track failed usage
                _costTrackingService.TrackUsage(audioData.Length, false);
                
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.AppendTranscriptionText($"[Error: {ex.Message}]");
                });
            }
        }

        private async Task InitializeTextInjectionService()
        {
            if (_textInjectionService == null) return;

            try
            {
                var initialized = await _textInjectionService.InitializeAsync();
                if (!initialized)
                {
                    System.Diagnostics.Debug.WriteLine("Warning: TextInjectionService failed to initialize");
                    _textInjectionEnabled = false;
                }
                else
                {
                    _textInjectionEnabled = true;
                    System.Diagnostics.Debug.WriteLine("TextInjectionService initialized successfully");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing TextInjectionService: {ex.Message}");
                _textInjectionEnabled = false;
            }
        }

        private async void OnTranscriptionCompleted(object? sender, string transcription)
        {
            // Handle text injection if enabled and we have valid transcription text
            if (_textInjectionEnabled && !string.IsNullOrWhiteSpace(transcription) && _textInjectionService != null)
            {
                try
                {
                    // Inject the transcribed text at cursor position
                    var injectionOptions = new InjectionOptions
                    {
                        UseClipboardFallback = true,
                        RetryCount = 3,
                        DelayBetweenRetriesMs = 100,
                        DelayBetweenCharsMs = 5
                    };

                    var success = await _textInjectionService.InjectTextAsync(transcription, injectionOptions);
                    
                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Text injected successfully: {transcription}");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Text injection failed: {transcription}");
                        // Show fallback notification to user
                        Dispatcher.Invoke(() =>
                        {
                            _systemTrayService?.ShowNotification("Text injection failed. Text was only shown in the preview window.", "Injection Issue");
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during text injection: {ex.Message}");
                    Dispatcher.Invoke(() =>
                    {
                        _systemTrayService?.ShowNotification($"Text injection error: {ex.Message}", "Injection Error");
                    });
                }
            }
        }

        private void OnTranscriptionError(object? sender, Exception error)
        {
            // This event is already handled by TranscriptionWindow through direct subscription
            // This method can be used for additional error handling if needed
            System.Diagnostics.Debug.WriteLine($"Transcription error: {error.Message}");
        }

        private void OnFreeTierWarning(object? sender, FreeTierWarning warning)
        {
            Dispatcher.Invoke(() =>
            {
                var message = $"You've used {warning.UsagePercentage:F1}% of your free tier (${warning.MonthlyUsage.Cost:F2} of ${warning.Limit:F2}).\n\n" +
                             "Consider upgrading to continue using ScottWisper beyond the free tier.";
                
                MessageBox.Show(message, "Free Tier Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
            });
        }

        private void OnFreeTierExceeded(object? sender, FreeTierExceeded exceeded)
        {
            Dispatcher.Invoke(() =>
            {
                var message = $"You've exceeded your free tier limit (${exceeded.MonthlyUsage.Cost:F2} of ${exceeded.Limit:F2}).\n\n" +
                             "Please upgrade your plan to continue using ScottWisper.\n\n" +
                             "Current dictation will be paused until you upgrade.";
                
                MessageBox.Show(message, "Free Tier Exceeded", MessageBoxButton.OK, MessageBoxImage.Stop);
                
                // Stop active dictation if free tier exceeded
                if (_isDictating)
                {
                    Task.Run(async () => await StopDictationInternal());
                }
            });
        }

        private void OnSystemTrayStartDictation(object? sender, EventArgs e)
        {
            Task.Run(async () => await StartDictationInternal());
        }

        private void OnSystemTrayStopDictation(object? sender, EventArgs e)
        {
            Task.Run(async () => await StopDictationInternal());
        }

        private void OnSystemTraySettings(object? sender, EventArgs e)
        {
            // For now, show a simple message about settings
            MessageBox.Show("Settings window will be implemented in a future plan.", "Settings", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OnSystemTrayExit(object? sender, EventArgs e)
        {
            Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            // Clean up resources
            if (_isDictating)
            {
                Task.Run(async () => await StopDictationInternal()).Wait();
            }

            _audioCaptureService?.Dispose();
            _whisperService?.Dispose();
            _costTrackingService?.Dispose();
            _textInjectionService?.Dispose();
            _hotkeyService?.Dispose();
            _systemTrayService?.Dispose();
            _transcriptionWindow?.Close();
            
            base.OnExit(e);
        }
    }
}