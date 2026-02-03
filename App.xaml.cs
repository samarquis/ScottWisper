using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WhisperKey.Bootstrap;
using WhisperKey.Services;
using WhisperKey.UI;

namespace WhisperKey
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider? _serviceProvider;
        private ApplicationBootstrapper? _bootstrapper;
        private EventCoordinator? _eventCoordinator;
        private DictationManager? _dictationManager;
        private MainWindow? _mainWindow;

        protected async override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Register global exception handlers
            RegisterGlobalExceptionHandlers();
            
            // Initialize application asynchronously
            await InitializeAsync();
        }
        
        private void RegisterGlobalExceptionHandlers()
        {
            // Handle unhandled exceptions in the current AppDomain
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                if (exception != null && !IsFatalException(exception))
                {
                    // Log technical details for debugging
                    System.Diagnostics.Debug.WriteLine($"Unhandled AppDomain exception: {exception}");
                    LogException(exception, "AppDomain.UnhandledException");
                    
                    // Show user-friendly message (technical details are in the log)
                    Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            "An unexpected error occurred in the background.\n\n" +
                            "The application will continue running, but you may want to restart it if you notice any issues.\n\n" +
                            "Technical details have been logged for troubleshooting.",
                            "WhisperKey",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }
            };
            
            // Handle unobserved task exceptions
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                var exception = args.Exception;
                // Log technical details only (no user message for background task failures)
                System.Diagnostics.Debug.WriteLine($"Unobserved task exception: {exception}");
                LogException(exception, "TaskScheduler.UnobservedTaskException");
                
                // Mark as observed to prevent process termination
                args.SetObserved();
            };
            
            // Handle dispatcher unhandled exceptions (UI thread)
            DispatcherUnhandledException += (sender, args) =>
            {
                var exception = args.Exception;
                if (!IsFatalException(exception))
                {
                    // Log technical details for debugging
                    System.Diagnostics.Debug.WriteLine($"Dispatcher unhandled exception: {exception}");
                    LogException(exception, "DispatcherUnhandledException");
                    
                    // Show user-friendly message without technical details
                    MessageBox.Show(
                        "An error occurred while processing your request.\n\n" +
                        "The application will continue running. If the problem persists, please restart WhisperKey.\n\n" +
                        "Technical details have been saved to the error log.",
                        "WhisperKey",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                    
                    args.Handled = true;
                }
            };
        }
        
        private void LogException(Exception exception, string source)
        {
            try
            {
                var logPath = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WhisperKey",
                    "logs");
                
                System.IO.Directory.CreateDirectory(logPath);
                
                var logFile = System.IO.Path.Combine(logPath, $"error_{DateTime.Now:yyyyMMdd}.log");
                var logEntry = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{source}] {exception.GetType().Name}: {exception.Message}\n{exception.StackTrace}\n\n";
                
                System.IO.File.AppendAllText(logFile, logEntry);
            }
            catch
            {
                // If logging fails, we can't do much about it
                System.Diagnostics.Debug.WriteLine("Failed to log exception");
            }
        }
        
        private async Task InitializeAsync()
        {
            try
            {
                // Configure services using ServiceConfiguration
                _serviceProvider = ServiceConfiguration.ConfigureServices();
                
                // Initialize bootstrapper
                _bootstrapper = new ApplicationBootstrapper(_serviceProvider);
                
                // Show first-time setup wizard if needed
                await ShowFirstTimeSetupIfNeededAsync();
                
                if (!await _bootstrapper.InitializeAsync())
                {
                    await Dispatcher.InvokeAsync(() => Shutdown());
                    return;
                }
                
                // Initialize system tray and hotkey service (non-UI operations)
                _bootstrapper.InitializeSystemTray();
                _bootstrapper.InitializeHotkeyService();
                
                // Create dictation manager
                _dictationManager = new DictationManager(_bootstrapper);
                
                // Setup event coordinator
                _eventCoordinator = new EventCoordinator(
                    _bootstrapper,
                    () => _dictationManager.ToggleAsync(),
                    () => _dictationManager.StartAsync(),
                    () => _dictationManager.StopAsync(),
                    ShowSettings,
                    ToggleMainWindow,
                    Shutdown
                );
                
                _eventCoordinator.RegisterEventHandlers();
                
                // Batch all UI initialization into a single dispatcher call with Background priority
                await Dispatcher.InvokeAsync(() =>
                {
                    // Register text injection service in Application properties for access from SettingsWindow
                    if (_bootstrapper?.TextInjectionService != null)
                    {
                        Application.Current.Properties["TextInjectionService"] = _bootstrapper.TextInjectionService;
                    }
                    
                    // Create and show main window
                    _mainWindow = new MainWindow();
                    _mainWindow.StartDictationRequested += OnMainWindowStartDictationRequested;
                    _mainWindow.Show();
                    
                    // Show startup notification on the bootstrapper
                    _ = _bootstrapper.ShowStartupNotificationAsync();
                }, System.Windows.Threading.DispatcherPriority.Background);
            }
            catch (InvalidOperationException ex)
            {
                LogException(ex, "InitializeAsync.InvalidOperation");
                await ShowErrorAndShutdownAsync(
                    "WhisperKey couldn't start properly due to an internal error.",
                    "Try restarting the application. If the problem persists, reinstall WhisperKey.");
            }
            catch (System.IO.FileNotFoundException ex)
            {
                LogException(ex, "InitializeAsync.FileNotFound");
                await ShowErrorAndShutdownAsync(
                    "A required file is missing.",
                    "Please reinstall WhisperKey to restore missing files.");
            }
            catch (System.Configuration.ConfigurationErrorsException ex)
            {
                LogException(ex, "InitializeAsync.Configuration");
                await ShowErrorAndShutdownAsync(
                    "WhisperKey's settings file is damaged.",
                    "Your settings will be reset to defaults. The application will create a new settings file on restart.");
            }
        }
        
        private async Task ShowFirstTimeSetupIfNeededAsync()
        {
            var settingsService = _serviceProvider?.GetRequiredService<ISettingsService>();
            if (settingsService == null) return;
            
            // Check if first-time setup is needed
            var settings = settingsService.Settings;
            if (!settings.FirstTimeSetupCompleted || settings.ShowSetupWizardOnStartup)
            {
                // Show wizard on UI thread
                var result = await Dispatcher.InvokeAsync(() =>
                {
                    var audioDeviceService = _serviceProvider!.GetRequiredService<IAudioDeviceService>();
                    var wizard = new FirstTimeSetupWizard(settingsService, audioDeviceService);
                    return wizard.ShowDialog() ?? false;
                }, System.Windows.Threading.DispatcherPriority.Background);
                
                // Mark setup as completed if user finished the wizard
                if (result)
                {
                    settings.FirstTimeSetupCompleted = true;
                    await settingsService.SaveAsync();
                }
                // If cancelled, continue anyway - don't block initialization
            }
        }
        
        private async Task ShowErrorAndShutdownAsync(string userMessage, string? actionMessage = null)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                var fullMessage = userMessage;
                if (!string.IsNullOrEmpty(actionMessage))
                {
                    fullMessage += $"\n\nWhat you can do:\n{actionMessage}";
                }
                
                MessageBox.Show(fullMessage, "WhisperKey Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            // Unregister event handlers
            _eventCoordinator?.UnregisterEventHandlers();

            // Unsubscribe from main window events
            if (_mainWindow != null)
            {
                _mainWindow.StartDictationRequested -= OnMainWindowStartDictationRequested;
            }

            // Shutdown bootstrapper
            _bootstrapper?.Shutdown();

            base.OnExit(e);
        }
        
        /// <summary>
        /// Determines if an exception is fatal and should not be caught.
        /// </summary>
        private static bool IsFatalException(Exception ex)
        {
            return ex is OutOfMemoryException ||
                   ex is StackOverflowException ||
                   ex is AccessViolationException;
        }
        
        private void ShowSettings()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_mainWindow != null && _serviceProvider != null)
                {
                    // Batch UI operations: show main window and open settings
                    _mainWindow.Show();
                    _mainWindow.Activate();
                    
                    // Open settings window with dependencies from service provider
                    var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                    var audioDeviceService = _serviceProvider.GetRequiredService<IAudioDeviceService>();
                    var settingsWindow = new SettingsWindow(settingsService, audioDeviceService);
                    settingsWindow.Show();
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }
        
        private void ToggleMainWindow()
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (_mainWindow == null) return;

                // Single UI operation - toggle visibility
                if (_mainWindow.IsVisible)
                {
                    _mainWindow.Hide();
                }
                else
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
            }, System.Windows.Threading.DispatcherPriority.Background);
        }

        private async void OnMainWindowStartDictationRequested(object? sender, EventArgs e)
        {
            try
            {
                if (_dictationManager != null)
                {
                    await _dictationManager.ToggleAsync();
                }
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"Start dictation error: {ex.Message}");
            }
        }
    }
}
