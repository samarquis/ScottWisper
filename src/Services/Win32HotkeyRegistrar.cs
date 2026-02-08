using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace WhisperKey
{
    /// <summary>
    /// Windows-specific hotkey registrar using P/Invoke
    /// </summary>
    public class Win32HotkeyRegistrar : IHotkeyRegistrar
    {
        private readonly ILogger<Win32HotkeyRegistrar>? _logger;

        public Win32HotkeyRegistrar(ILogger<Win32HotkeyRegistrar>? logger = null)
        {
            _logger = logger;
        }

        public bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk)
        {
            try
            {
                var result = RegisterHotkey(hWnd, id, fsModifiers | MOD_NOREPEAT, vk);
                if (result)
                {
                    _logger?.LogInformation($"Successfully registered hotkey ID {id}");
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    _logger?.LogWarning($"Failed to register hotkey ID {id}. Error: {error}");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Exception registering hotkey ID {id}");
                return false;
            }
        }

        public bool UnregisterHotKey(IntPtr hWnd, int id)
        {
            try
            {
                var result = Unregisterhotkey(hWnd, id);
                if (result)
                {
                    _logger?.LogInformation($"Successfully unregistered hotkey ID {id}");
                }
                else
                {
                    var error = Marshal.GetLastWin32Error();
                    _logger?.LogWarning($"Failed to unregister hotkey ID {id}. Error: {error}");
                }
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Exception unregistering hotkey ID {id}");
                return false;
            }
        }

        public int GetLastWin32Error()
        {
            return Marshal.GetLastWin32Error();
        }

        #region Win32 API declarations

        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RegisterHotkey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool Unregisterhotkey(IntPtr hWnd, int id);

        #endregion
    }
}
