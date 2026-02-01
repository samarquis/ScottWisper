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
                    _mainWindow.Show();
                });
                
                // Show startup notification
                await _bootstrapper.ShowStartupNotificationAsync();
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"Failed to start application: {ex.Message}", "ScottWisper Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    Shutdown();
                });
            }
        }
        
        protected override void OnExit(ExitEventArgs e)
        {
            // Unregister event handlers
            _eventCoordinator?.UnregisterEventHandlers();
            
            // Shutdown bootstrapper
            _bootstrapper?.Shutdown();
            
            base.OnExit(e);
        }
        
        private void ShowSettings()
        {
            Dispatcher.Invoke(() =>
            {
                if (_mainWindow != null)
                {
                    _mainWindow.Show();
                    _mainWindow.Activate();
                    
                    // Open settings window
                    var settingsWindow = new SettingsWindow();
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
    }
}
