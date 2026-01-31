using System;
using System.Threading.Tasks;
using ScottWisper.Configuration;

namespace ScottWisper
{
    public interface IHotkeyService : IDisposable
    {
        event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
        event EventHandler<HotkeyConflictEventArgs>? HotkeyConflictDetected;
        bool IsHotkeyRegistered { get; }
        HotkeyProfile CurrentProfile { get; }
        bool RegisterHotkey(HotkeyDefinition hotkey);
        void UnregisterHotkey(string hotkeyId);
        HotkeyValidationResult ValidateHotkey(string combination);
    }
}