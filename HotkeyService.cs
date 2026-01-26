using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ScottWisper
{
    public class HotkeyService : IDisposable
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID = 9000;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_SHIFT = 0x0004;
        private const uint VK_V = 0x56;

        private IntPtr _windowHandle;
        private HwndSource? _source;
        private bool _isRegistered;

        public event EventHandler? HotkeyPressed;

        public bool IsHotkeyRegistered => _isRegistered;

        public HotkeyService()
        {
            // Get the main window handle
            var mainWindow = System.Windows.Application.Current.MainWindow;
            if (mainWindow != null)
            {
                _windowHandle = new WindowInteropHelper(mainWindow).Handle;
                RegisterHotKey();
            }
        }

        private void RegisterHotKey()
        {
            if (_windowHandle == IntPtr.Zero) return;

            // Register Ctrl+Win+Shift+V hotkey
            bool success = RegisterHotKey(_windowHandle, HOTKEY_ID, 
                MOD_CONTROL | MOD_WIN | MOD_SHIFT, VK_V);

            if (success)
            {
                _isRegistered = true;
                _source = HwndSource.FromHwnd(_windowHandle);
                if (_source != null)
                {
                    _source.AddHook(WndProc);
                }
            }
            else
            {
                int errorCode = Marshal.GetLastWin32Error();
                System.Diagnostics.Debug.WriteLine($"Failed to register hotkey. Error: {errorCode}");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                if (hotkeyId == HOTKEY_ID)
                {
                    HotkeyPressed?.Invoke(this, EventArgs.Empty);
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            if (_isRegistered && _windowHandle != IntPtr.Zero)
            {
                UnregisterHotKey(_windowHandle, HOTKEY_ID);
                _isRegistered = false;
            }

            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}