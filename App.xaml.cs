using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using ScottWisper.Bootstrap;
using ScottWisper.Services;

namespace ScottWisper
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
            
            // Initialize application asynchronously
            Task.Run(async () => await InitializeAsync()).ConfigureAwait(false);
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
                    Shutdown();
                    return;
                }
                
                // Initialize system tray
                _bootstrapper.InitializeSystemTray();
                
                // Initialize hotkey service
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
                
                // Show main window
                await Dispatcher.InvokeAsync(() =>
                {
                    _mainWindow = new MainWindow();
                    _mainWindow.StartDictationRequested += OnMainWindowStartDictationRequested;
                    _mainWindow.Show();
                });
                
                // Show startup notification
                await _bootstrapper.ShowStartupNotificationAsync();
            }
            catch (InvalidOperationException ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Failed to start application: {ex.Message}", "ScottWisper Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                });
            }
            catch (System.IO.FileNotFoundException ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Required file not found: {ex.Message}", "ScottWisper Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                });
            }
            catch (System.Configuration.ConfigurationErrorsException ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Configuration error: {ex.Message}", "ScottWisper Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                });
            }
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
            Dispatcher.Invoke(() =>
            {
                if (_mainWindow != null && _serviceProvider != null)
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                    
                    // Open settings window with dependencies from service provider
                    var settingsService = _serviceProvider.GetRequiredService<ISettingsService>();
                    var audioDeviceService = _serviceProvider.GetRequiredService<IAudioDeviceService>();
                    var settingsWindow = new SettingsWindow(settingsService, audioDeviceService);
                    settingsWindow.Show();
                }
            });
        }
        
        private void ToggleMainWindow()
        {
            Dispatcher.Invoke(() =>
            {
                if (_mainWindow == null) return;

                if (_mainWindow.IsVisible)
                {
                    _mainWindow.Hide();
                }
                else
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                }
            });
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
