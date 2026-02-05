using System;

namespace WhisperKey
{
    /// <summary>
    /// Interface for registering and unregistering Windows hotkeys.
    /// Abstracts P/Invoke calls to enable unit testing.
    /// </summary>
    public interface IHotkeyRegistrar
    {
        /// <summary>
        /// Registers a hotkey with the Windows API.
        /// </summary>
        /// <param name="hWnd">Handle to the window that will receive hotkey messages</param>
        /// <param name="id">Unique identifier for the hotkey</param>
        /// <param name="fsModifiers">Modifier keys (Ctrl, Alt, Shift, Win)</param>
        /// <param name="vk">Virtual key code</param>
        /// <returns>True if registration succeeded, false otherwise</returns>
        bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        /// <summary>
        /// Unregisters a previously registered hotkey.
        /// </summary>
        /// <param name="hWnd">Handle to the window that registered the hotkey</param>
        /// <param name="id">Unique identifier for the hotkey</param>
        /// <returns>True if unregistration succeeded, false otherwise</returns>
        bool UnregisterHotKey(IntPtr hWnd, int id);

        /// <summary>
        /// Gets the last error code from the Windows API.
        /// </summary>
        /// <returns>The last Win32 error code</returns>
        int GetLastWin32Error();
    }
}
