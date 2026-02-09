using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Configuration;

namespace WhisperKey.Services
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
        Task<bool> SwitchProfileAsync(string profileId);
        Task<bool> CreateProfileAsync(HotkeyProfile profile);
        Task<bool> UpdateProfileAsync(HotkeyProfile profile);
        Task<bool> DeleteProfileAsync(string profileId);
        Task UpdateHotkeyAsync(string hotkeyId, HotkeyDefinition hotkey);
        Task ExportProfileAsync(string profileId, string filePath);
        Task<HotkeyProfile> ImportProfileAsync(string filePath);
        Task<List<HotkeyProfile>> GetAllProfilesAsync();
        Task ResetToDefaultsAsync();
    }
}
