using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ScottWisper.Configuration;
using ScottWisper.Services;

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
        private IServiceProvider? _serviceProvider;
        private ISettingsService? _settingsService;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                // Initialize services
                await InitializeServices();

                _mainWindow = new MainWindow();
                
                // Initialize hotkey service
                _hotkeyService = new HotkeyService();
                _hotkeyService.HotkeyPressed += OnHotkeyPressed;
                
                // Initialize system tray service with error handling
                _systemTrayService = new SystemTrayService();
                _systemTrayService.StartDictationRequested += OnSystemTrayStartDictation;
                _systemTrayService.StopDictationRequested += OnSystemTrayStopDictation;
                _systemTrayService.SettingsRequested += OnSystemTraySettings;
                _systemTrayService.WindowToggleRequested += OnSystemTrayToggleWindow;
                _systemTrayService.ExitRequested += OnSystemTrayExit;
                
                try
                {
                    _systemTrayService.Initialize();
                    System.Diagnostics.Debug.WriteLine("SystemTrayService initialized successfully");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to initialize SystemTrayService: {ex.Message}");
                    MessageBox.Show($"Failed to initialize system tray: {ex.Message}", "ScottWisper Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
                
                // Initialize MainWindow (it will handle its own visibility)
                _mainWindow.Show();
                
                // Show startup notification
                _systemTrayService?.ShowNotification("ScottWisper is ready. Press Ctrl+Alt+V to start dictation.", "Application Started");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to start application: {ex.Message}", "ScottWisper Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private async Task InitializeServices()
        {
            try
            {
                // Setup configuration
                var services = new ServiceCollection();
                ConfigureConfiguration(services);
                
                _serviceProvider = services.BuildServiceProvider();
                _settingsService = _serviceProvider.GetRequiredService<ISettingsService>();

                // Initialize core services using settings
                var settings = _settingsService.Settings;
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

                // Configure audio capture service with settings
                _audioCaptureService.AudioDataCaptured += OnAudioDataAvailable;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to initialize services: {ex.Message}", "ScottWisper Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Current.Shutdown();
            }
        }

        private void ConfigureConfiguration(IServiceCollection services)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
                    "ScottWisper", 
                    "usersettings.json"), optional: true, reloadOnChange: true);

            var configuration = builder.Build();

            services.Configure<AudioSettings>(options => configuration.GetSection("Audio").Bind(options));
            services.Configure<TranscriptionSettings>(options => configuration.GetSection("Transcription").Bind(options));
            services.Configure<HotkeySettings>(options => configuration.GetSection("Hotkeys").Bind(options));
            services.Configure<UISettings>(options => configuration.GetSection("UI").Bind(options));
            services.Configure<AppSettings>(options => configuration.Bind(options));
            services.AddSingleton<ISettingsService, SettingsService>();
            
            // Also make configuration available for legacy use
            services.AddSingleton<IConfiguration>(configuration);
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
                // Update system tray to recording state
                Dispatcher.Invoke(() =>
                {
                    _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Recording);
                });

                // Show transcription window
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.ShowForDictation();
                });

                // Start audio capture
                await _audioCaptureService?.StartCaptureAsync()!;
                
                _isDictating = true;
                
                // Update transcription window and system tray status
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.SetRecordingStatus();
                    _systemTrayService?.UpdateDictationStatus(true);
                    _systemTrayService?.ShowNotification("Recording started. Speak now.", "Dictation Started");
                });
            }
            catch (Exception ex)
            {
                _isDictating = false;
                Dispatcher.Invoke(() =>
                {
                    _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Error);
                    _systemTrayService?.UpdateDictationStatus(false);
                    MessageBox.Show($"Failed to start dictation: {ex.Message}", "Dictation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    // Reset to ready state after error
                    _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                });
            }
        }

        private async Task StopDictationInternal()
        {
            try
            {
                // Update system tray to processing state
                Dispatcher.Invoke(() =>
                {
                    _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Processing);
                });

                // Stop audio capture
                await _audioCaptureService?.StopCaptureAsync()!;
                
                // Update transcription window and system tray status
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
                _isDictating = false;
                System.Diagnostics.Debug.WriteLine($"Error stopping dictation: {ex.Message}");
                Dispatcher.Invoke(() =>
                {
                    _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Error);
                    _systemTrayService?.UpdateDictationStatus(false);
                    // Reset to ready state after error
                    _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Ready);
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
            // Update system tray status to processing complete
            Dispatcher.Invoke(() =>
            {
                _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                if (!string.IsNullOrWhiteSpace(transcription))
                {
                    _systemTrayService?.ShowNotification("Transcription completed successfully", "Dictation Complete");
                }
            });

            // Handle text injection if enabled and we have valid transcription text
            if (_textInjectionEnabled && !string.IsNullOrWhiteSpace(transcription) && _textInjectionService != null)
            {
                try
                {
                    // Update system tray to processing state
                    Dispatcher.Invoke(() =>
                    {
                        _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Processing);
                    });

                    // Inject transcribed text at cursor position
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
                        Dispatcher.Invoke(() =>
                        {
                            _systemTrayService?.ShowNotification("Text inserted at cursor position", "Injection Success");
                        });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Text injection failed: {transcription}");
                        // Show fallback notification to user
                        Dispatcher.Invoke(() =>
                        {
                            _systemTrayService?.ShowNotification("Text injection failed. Text was only shown in preview window.", "Injection Issue");
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

            // Reset to ready state
            Dispatcher.Invoke(() =>
            {
                _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Ready);
            });
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

        private void OnSystemTrayToggleWindow(object? sender, EventArgs e)
        {
            // Toggle main window visibility from tray
            if (_mainWindow != null)
            {
                _mainWindow.ToggleVisibility();
            }
        }

        private void OnSystemTraySettings(object? sender, EventArgs e)
        {
            // Show main window from tray when settings requested
            if (_mainWindow != null)
            {
                if (_mainWindow.IsWindowHidden)
                {
                    _mainWindow.ShowFromTray();
                }
                else
                {
                    _mainWindow.Activate();
                    _mainWindow.Focus();
                }
            }
        }

        private void OnSystemTrayExit(object? sender, EventArgs e)
        {
            Current.Shutdown();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // Clean up resources
                if (_isDictating)
                {
                    try
                    {
                        Task.Run(async () => await StopDictationInternal()).Wait(5000); // Wait max 5 seconds
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error stopping dictation during shutdown: {ex.Message}");
                    }
                }

                // Dispose services in order
                _audioCaptureService?.Dispose();
                _whisperService?.Dispose();
                _costTrackingService?.Dispose();
                _textInjectionService?.Dispose();
                _hotkeyService?.Dispose();
                _systemTrayService?.Dispose();
                _transcriptionWindow?.Close();
                
                System.Diagnostics.Debug.WriteLine("Application shutdown completed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during application shutdown: {ex.Message}");
            }
            finally
            {
                base.OnExit(e);
            }
        }
    }
}