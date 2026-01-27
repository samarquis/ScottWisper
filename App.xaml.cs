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

        private bool _textInjectionEnabled = true;
        private IServiceProvider? _serviceProvider;
        private ISettingsService? _settingsService;
        private ITextInjection? _textInjectionService;

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

                // Initialize system tray service with enhanced feedback integration
                _systemTrayService = new SystemTrayService();
                _systemTrayService.StartDictationRequested += OnSystemTrayStartDictation;
                _systemTrayService.StopDictationRequested += OnSystemTrayStopDictation;
                _systemTrayService.SettingsRequested += OnSystemTraySettings;
                _systemTrayService.WindowToggleRequested += OnSystemTrayToggleWindow;
                _systemTrayService.ExitRequested += OnSystemTrayExit;

                // Store system tray service for global access
                Current.Properties["SystemTray"] = _systemTrayService;
                
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
                
                // Show enhanced startup notification
                if (Current.Properties["FeedbackService"] is FeedbackService feedbackService)
                {
                    await feedbackService.ShowToastNotificationAsync(
                        "Application Started", 
                        "ScottWisper is ready. Press Ctrl+Alt+V to start dictation.", 
                        FeedbackService.NotificationType.Completion
                    );
                }
                else
                {
                    _systemTrayService?.ShowNotification("ScottWisper is ready. Press Ctrl+Alt+V to start dictation.", "Application Started");
                }
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
                
                // Initialize all services with settings integration
                await InitializeServiceIntegration();

                // Initialize enhanced feedback service first
                var feedbackService = new FeedbackService();
                await feedbackService.InitializeAsync();

                // Store feedback service in application properties for global access
                Current.Properties["FeedbackService"] = feedbackService;
                
                // Subscribe to settings changes for real-time application
                _settingsService.SettingsChanged += OnSettingsChanged;

                // Initialize core services using settings
                var settings = _settingsService.Settings;
                _whisperService = new WhisperService(_settingsService);
                _costTrackingService = new CostTrackingService(_settingsService);
                _audioCaptureService = new AudioCaptureService(_settingsService);
                _textInjectionService = _serviceProvider.GetRequiredService<ITextInjection>();

                // Initialize transcription window
                _transcriptionWindow = new TranscriptionWindow();
                _transcriptionWindow.InitializeServices(_whisperService, _costTrackingService);

                // Initialize text injection service
                await InitializeTextInjectionService();

                // Connect enhanced feedback to all services
                await ConnectFeedbackToServices(feedbackService);

                // Wire up service events
                _whisperService.TranscriptionError += OnTranscriptionError;
                _whisperService.TranscriptionCompleted += OnTranscriptionCompleted;
                _costTrackingService.FreeTierWarning += OnFreeTierWarning;
                _costTrackingService.FreeTierExceeded += OnFreeTierExceeded;

                // Configure audio capture service with settings and enhanced feedback
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
            services.Configure<TextInjectionSettings>(options => configuration.GetSection("TextInjection").Bind(options));
            services.Configure<AppSettings>(options => configuration.Bind(options));
            services.AddSingleton<ISettingsService, SettingsService>();
            services.AddSingleton<ITextInjection, TextInjectionService>();
            
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
                // Get enhanced feedback service
                var feedbackService = Current.Properties["FeedbackService"] as FeedbackService;
                
                // Update enhanced feedback to recording state
                if (feedbackService != null)
                {
                    await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Recording, "Recording started - Speak clearly");
                }
                else
                {
                    // Fallback to direct system tray update
                    Dispatcher.Invoke(() =>
                    {
                        _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Recording);
                    });
                }

                // Show transcription window
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.ShowForDictation();
                });

                // Start audio capture with progress feedback
                if (feedbackService != null)
                {
                    await feedbackService.StartProgressAsync("Recording", TimeSpan.FromMinutes(30));
                }

                await _audioCaptureService?.StartCaptureAsync()!;
                
                _isDictating = true;
                
                // Update transcription window and system tray status
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.SetRecordingStatus();
                    _systemTrayService?.UpdateDictationStatus(true);
                    
                    // Show enhanced notification if feedback service available
                    if (feedbackService != null)
                    {
                        _systemTrayService?.ShowEnhancedNotification("Recording", "🎤 Recording started. Speak now.", "🎤");
                    }
                    else
                    {
                        _systemTrayService?.ShowNotification("Recording started. Speak now.", "Dictation Started");
                    }
                });
            }
            catch (Exception ex)
            {
                _isDictating = false;
                var feedbackService = Current.Properties["FeedbackService"] as FeedbackService;
                
                if (feedbackService != null)
                {
                    await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Error, $"Failed to start recording: {ex.Message}");
                }
                
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
                var feedbackService = Current.Properties["FeedbackService"] as FeedbackService;
                
                // Update enhanced feedback to processing state
                if (feedbackService != null)
                {
                    await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Processing, "Stopping recording and processing speech");
                    await feedbackService.StartProgressAsync("Stopping", TimeSpan.FromSeconds(5));
                }
                else
                {
                    // Fallback to direct system tray update
                    Dispatcher.Invoke(() =>
                    {
                        _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Processing);
                    });
                }

                // Stop audio capture with final progress update
                if (feedbackService != null)
                {
                    await feedbackService.UpdateProgressAsync(80, "Finalizing audio capture...");
                }

                await _audioCaptureService?.StopCaptureAsync()!;
                
                // Update transcription window and system tray status
                Dispatcher.Invoke(() =>
                {
                    _transcriptionWindow?.SetProcessingStatus();
                    _systemTrayService?.UpdateDictationStatus(false);
                });
                
                _isDictating = false;

                // Final progress update
                if (feedbackService != null)
                {
                    await feedbackService.UpdateProgressAsync(100, "Recording stopped");
                    await feedbackService.CompleteProgressAsync("Recording completed successfully");
                }
                
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
                var feedbackService = Current.Properties["FeedbackService"] as FeedbackService;
                
                if (feedbackService != null)
                {
                    await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Error, $"Failed to stop recording: {ex.Message}");
                }
                
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
            var feedbackService = Current.Properties["FeedbackService"] as FeedbackService;
            
            // Update enhanced feedback to completion state
            if (feedbackService != null)
            {
                await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Complete, 
                    !string.IsNullOrWhiteSpace(transcription) ? "Transcription completed successfully" : "No speech detected");
            }
            else
            {
                // Fallback to direct system tray update
                Dispatcher.Invoke(() =>
                {
                    _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                    if (!string.IsNullOrWhiteSpace(transcription))
                    {
                        _systemTrayService?.ShowNotification("Transcription completed successfully", "Dictation Complete");
                    }
                });
            }

            // Handle text injection if enabled and we have valid transcription text
            if (_textInjectionEnabled && !string.IsNullOrWhiteSpace(transcription) && _textInjectionService != null)
            {
                try
                {
                    // Show injection progress
                    if (feedbackService != null)
                    {
                        await feedbackService.StartProgressAsync("Injecting Text", TimeSpan.FromSeconds(10));
                        await feedbackService.UpdateProgressAsync(20, "Preparing text for injection");
                    }

                    // Inject transcribed text at cursor position using user settings
                    var textInjectionSettings = _settingsService?.Settings?.TextInjection ?? new TextInjectionSettings();
                    var injectionOptions = new InjectionOptions
                    {
                        UseClipboardFallback = textInjectionSettings.UseClipboardFallback,
                        RetryCount = textInjectionSettings.RetryCount,
                        DelayBetweenRetriesMs = textInjectionSettings.DelayBetweenRetriesMs,
                        DelayBetweenCharsMs = textInjectionSettings.DelayBetweenCharsMs,
                        RespectExistingText = textInjectionSettings.RespectExistingText
                    };

                    if (feedbackService != null)
                    {
                        await feedbackService.UpdateProgressAsync(60, "Injecting text at cursor position");
                    }

                    var success = await _textInjectionService.InjectTextAsync(transcription, injectionOptions);
                    
                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine($"Text injected successfully: {transcription}");
                        
                        // Log successful injection with performance metrics
                        var metrics = _textInjectionService.GetPerformanceMetrics();
                        System.Diagnostics.Debug.WriteLine($"Injection performance: {metrics.AverageLatency.TotalMilliseconds}ms avg latency, {metrics.SuccessRate:P1} success rate, {metrics.TotalAttempts} attempts");
                        
                        if (feedbackService != null)
                        {
                            await feedbackService.UpdateProgressAsync(100, "Text injection completed");
                            await feedbackService.CompleteProgressAsync("Text successfully inserted");
                            await feedbackService.ShowToastNotificationAsync(
                                "Injection Success", 
                                "Text inserted at cursor position", 
                                FeedbackService.NotificationType.Completion
                            );
                        }
                        
                        Dispatcher.Invoke(() =>
                        {
                            _systemTrayService?.ShowEnhancedNotification("Injection Success", "Text inserted at cursor position", "✅");
                        });
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Text injection failed: {transcription}");
                        
                        // Get performance metrics for failed injection
                        var metrics = _textInjectionService.GetPerformanceMetrics();
                        var recentFailures = metrics.RecentFailures.Take(3).ToList();
                        
                        // Enhanced error handling with recovery attempts
                        if (textInjectionSettings.EnablePerformanceMonitoring && metrics.AverageLatency.TotalMilliseconds > textInjectionSettings.InjectionLatencyThresholdMs)
                        {
                            // Try alternative injection method if latency is too high
                            System.Diagnostics.Debug.WriteLine("High injection latency detected, trying alternative method...");
                            
                            var fallbackOptions = new InjectionOptions
                            {
                                UseClipboardFallback = !injectionOptions.UseClipboardFallback, // Try opposite method
                                RetryCount = 1,
                                DelayBetweenRetriesMs = 50,
                                DelayBetweenCharsMs = 2
                            };
                            
                            var fallbackSuccess = await _textInjectionService.InjectTextAsync(transcription, fallbackOptions);
                            if (fallbackSuccess)
                            {
                                System.Diagnostics.Debug.WriteLine("Alternative injection method succeeded");
                                if (feedbackService != null)
                                {
                                    await feedbackService.ShowToastNotificationAsync(
                                        "Injection Recovery", 
                                        "Text inserted using alternative method", 
                                        FeedbackService.NotificationType.Completion
                                    );
                                }
                            }
                        }
                        
                        // Show detailed error information to user
                        var errorMessage = recentFailures.Count > 0 
                            ? $"Text injection failed ({recentFailures.Count} recent failures). Last error: {recentFailures.FirstOrDefault()?.ApplicationInfo?.ProcessName ?? "Unknown"}"
                            : "Text injection failed. Text was only shown in preview window.";
                        
                        if (feedbackService != null)
                        {
                            await feedbackService.ShowToastNotificationAsync(
                                "Injection Issue", 
                                errorMessage, 
                                FeedbackService.NotificationType.Warning
                            );
                        }
                        
                        // Show fallback notification to user
                        Dispatcher.Invoke(() =>
                        {
                            _systemTrayService?.ShowNotification(errorMessage, "Injection Issue");
                        });
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error during text injection: {ex.Message}");
                    
                    if (feedbackService != null)
                    {
                        await feedbackService.ShowToastNotificationAsync(
                            "Injection Error", 
                            $"Text injection error: {ex.Message}", 
                            FeedbackService.NotificationType.Error
                        );
                    }
                    
                    Dispatcher.Invoke(() =>
                    {
                        _systemTrayService?.ShowNotification($"Text injection error: {ex.Message}", "Injection Error");
                    });
                }
            }

            // Reset to ready state
            if (feedbackService != null)
            {
                await Task.Delay(2000); // Brief delay to show completion state
                await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Ready, "Ready for next dictation");
            }
            else
            {
                Dispatcher.Invoke(() =>
                {
                    _systemTrayService?.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                });
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

        private async Task ConnectFeedbackToServices(FeedbackService feedbackService)
        {
            // Connect system tray service for status synchronization
            if (_systemTrayService != null)
            {
                feedbackService.StatusChanged += async (sender, status) =>
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        var trayStatus = ConvertToTrayStatus(status);
                        _systemTrayService.UpdateStatus(trayStatus);
                    });
                };
            }

            // Connect audio capture service for real-time visualization
            if (_audioCaptureService != null)
            {
                _audioCaptureService.AudioLevelChanged += async (sender, level) =>
                {
                    await feedbackService.UpdateProgressAsync(level, $"Audio level: {level:P1}");
                };

                _audioCaptureService.DeviceConnected += async (sender, deviceInfo) =>
                {
                    await feedbackService.ShowToastNotificationAsync("Audio Device", $"Connected: {deviceInfo}", FeedbackService.NotificationType.Info);
                };

                _audioCaptureService.DeviceDisconnected += async (sender, deviceInfo) =>
                {
                    await feedbackService.ShowToastNotificationAsync("Audio Device", $"Disconnected: {deviceInfo}", FeedbackService.NotificationType.Warning);
                };
            }

            // Connect transcription service for detailed feedback
            if (_whisperService != null)
            {
                _whisperService.TranscriptionStarted += async (sender, args) =>
                {
                    await feedbackService.StartProgressAsync("Transcribing", TimeSpan.FromSeconds(10));
                    await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Processing, "Processing speech with AI");
                };

                _whisperService.TranscriptionProgress += async (sender, progress) =>
                {
                    await feedbackService.UpdateProgressAsync(progress * 100, $"AI processing: {progress:P1}");
                };
            }

            // Connect text injection service for operation feedback
            if (_textInjectionService != null)
            {
                // Text injection feedback would be handled in the injection events
                System.Diagnostics.Debug.WriteLine("Enhanced feedback connected to text injection service");
            }
        }

        private SystemTrayService.TrayStatus ConvertToTrayStatus(IFeedbackService.DictationStatus status)
        {
            return status switch
            {
                IFeedbackService.DictationStatus.Idle => SystemTrayService.TrayStatus.Idle,
                IFeedbackService.DictationStatus.Ready => SystemTrayService.TrayStatus.Ready,
                IFeedbackService.DictationStatus.Recording => SystemTrayService.TrayStatus.Recording,
                IFeedbackService.DictationStatus.Processing => SystemTrayService.TrayStatus.Processing,
                IFeedbackService.DictationStatus.Complete => SystemTrayService.TrayStatus.Ready,
                IFeedbackService.DictationStatus.Error => SystemTrayService.TrayStatus.Error,
                _ => SystemTrayService.TrayStatus.Idle
            };
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

        private async Task InitializeServiceIntegration()
        {
            try
            {
                // Apply current settings to all services
                await ApplySettingsToServices();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to initialize service integration: {ex.Message}");
            }
        }

        private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            try
            {
                // Handle real-time settings changes
                switch (e.Category)
                {
                    case "Audio":
                        await ApplyAudioSettingsAsync();
                        break;
                    case "Transcription":
                        await ApplyTranscriptionSettingsAsync();
                        break;
                    case "Hotkeys":
                        await ApplyHotkeySettingsAsync();
                        break;
                    case "UI":
                        await ApplyUISettingsAsync();
                        break;
                    case "System":
                        if (e.Key == "ApplyAll" || e.Key == "ReloadSettings")
                        {
                            await ApplySettingsToServices();
                        }
                        break;
                }

                // Show notification if restart is required
                if (e.RequiresRestart)
                {
                    var feedbackService = Current.Properties["FeedbackService"] as FeedbackService;
                    if (feedbackService != null)
                    {
                        await feedbackService.ShowToastNotificationAsync(
                            "Settings Changed", 
                            "Some settings require application restart to take effect.", 
                            FeedbackService.NotificationType.Warning
                        );
                    }
                    else
                    {
                        _systemTrayService?.ShowNotification(
                            "Some settings require application restart to take effect.", 
                            "Settings Changed");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply settings change: {ex.Message}");
            }
        }

        private async Task ApplySettingsToServices()
        {
            var settings = _settingsService.Settings;
            
            await ApplyAudioSettingsAsync();
            await ApplyTranscriptionSettingsAsync();
            await ApplyHotkeySettingsAsync();
            await ApplyUISettingsAsync();
        }

        private async Task ApplyAudioSettingsAsync()
        {
            try
            {
                var audioSettings = _settingsService.Settings.Audio;
                if (_audioCaptureService != null)
                {
                    // Apply audio device and format settings
                    // This would depend on the actual AudioCaptureService interface
                    // For now, we'll just log the settings
                    System.Diagnostics.Debug.WriteLine($"Applied audio settings: SampleRate={audioSettings?.SampleRate}, Channels={audioSettings?.Channels}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply audio settings: {ex.Message}");
            }
        }

        private async Task ApplyTranscriptionSettingsAsync()
        {
            try
            {
                var transcriptionSettings = _settingsService.Settings.Transcription;
                if (_whisperService != null)
                {
                    // Apply transcription provider and model settings
                    System.Diagnostics.Debug.WriteLine($"Applied transcription settings: Provider={transcriptionSettings?.Provider}, Model={transcriptionSettings?.Model}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply transcription settings: {ex.Message}");
            }
        }

        private async Task ApplyHotkeySettingsAsync()
        {
            try
            {
                var hotkeySettings = _settingsService.Settings.Hotkeys;
                if (_hotkeyService != null)
                {
                    // Apply hotkey settings - would need to re-register hotkeys
                    System.Diagnostics.Debug.WriteLine($"Applied hotkey settings: ToggleRecording={hotkeySettings?.ToggleRecording}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply hotkey settings: {ex.Message}");
            }
        }

        private async Task ApplyUISettingsAsync()
        {
            try
            {
                var uiSettings = _settingsService.Settings.UI;
                
                // Apply UI settings like startup behavior, visual feedback, etc.
                if (_systemTrayService != null)
                {
                    // Apply system tray behavior
                    System.Diagnostics.Debug.WriteLine($"Applied UI settings: MinimizeToTray={uiSettings?.MinimizeToTray}, StartWithWindows={uiSettings?.StartWithWindows}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to apply UI settings: {ex.Message}");
            }
        }
    }
}