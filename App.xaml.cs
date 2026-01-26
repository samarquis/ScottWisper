using System;
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

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            _mainWindow = new MainWindow();
            
            // Initialize hotkey service
            _hotkeyService = new HotkeyService();
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            
            // Show window for now - will hide when system tray is implemented
            _mainWindow.Show();
        }

        private void OnHotkeyPressed(object? sender, EventArgs e)
        {
            // Handle hotkey press - this will trigger dictation mode
            MessageBox.Show("Global hotkey (Ctrl+Win+Shift+V) pressed! Voice dictation activated.", "ScottWisper");
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _hotkeyService?.Dispose();
            base.OnExit(e);
        }
    }
}