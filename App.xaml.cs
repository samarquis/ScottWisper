using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using WhisperKey.Bootstrap;
using WhisperKey.Services;

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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // Register global exception handlers
            RegisterGlobalExceptionHandlers();
            
            // Initialize application asynchronously
            Task.Run(async () => await InitializeAsync()).ConfigureAwait(false);
        }
        
        private void RegisterGlobalExceptionHandlers()
        {
            // Handle unhandled exceptions in the current AppDomain
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                if (exception != null && !IsFatalException(exception))
                {
                    System.Diagnostics.Debug.WriteLine($"Unhandled AppDomain exception: {exception}");
                    LogException(exception, "AppDomain.UnhandledException");
                    
                    // Show error message on UI thread
                    Dispatcher.InvokeAsync(() =>
                    {
                        MessageBox.Show(
                            $"An unexpected error occurred:\n\n{exception.Message}\n\nThe application will continue running, but may be in an unstable state.",
                            "WhisperKey Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    });
                }
            };
            
            // Handle unobserved task exceptions
            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                var exception = args.Exception;
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
                    System.Diagnostics.Debug.WriteLine($"Dispatcher unhandled exception: {exception}");
                    LogException(exception, "DispatcherUnhandledException");
                    
                    // Show error and mark as handled to prevent application crash
                    MessageBox.Show(
                        $"An unexpected UI error occurred:\n\n{exception.Message}\n\nThe application will continue running.",
                        "WhisperKey Error",
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
                await ShowErrorAndShutdownAsync($"Failed to start application: {ex.Message}");
            }
            catch (System.IO.FileNotFoundException ex)
            {
                await ShowErrorAndShutdownAsync($"Required file not found: {ex.Message}");
            }
            catch (System.Configuration.ConfigurationErrorsException ex)
            {
                await ShowErrorAndShutdownAsync($"Configuration error: {ex.Message}");
            }
        }
        
        private async Task ShowErrorAndShutdownAsync(string message)
        {
            await Dispatcher.InvokeAsync(() =>
            {
                MessageBox.Show(message, "WhisperKey Error",
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

        private void OnMainWindowStartDictationRequested(object? sender, EventArgs e)
        {
            Task.Run(async () =>
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
            }).ConfigureAwait(false);
        }
    }
}
