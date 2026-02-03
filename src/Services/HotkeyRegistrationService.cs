using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using WhisperKey.Configuration;

namespace WhisperKey.Services
{
    /// <summary>
    /// Handles low-level hotkey registration with Windows API
    /// </summary>
    public class HotkeyRegistrationService : IDisposable
    {
        private readonly Win32HotkeyRegistrar _win32Registrar;
        private readonly ILogger<HotkeyRegistrationService>? _logger;
        private readonly IntPtr _windowHandle;
        private readonly Dictionary<string, int> _registeredHotkeys = new Dictionary<string, int>();
        private readonly Dictionary<int, HotkeyDefinition> _hotkeyById = new Dictionary<int, HotkeyDefinition>();
        private int _nextHotkeyId = 9000;
        private bool _disposed = false;

        public HotkeyRegistrationService(Win32HotkeyRegistrar win32Registrar, ILogger<HotkeyRegistrationService>? logger, IntPtr windowHandle)
        {
            _win32Registrar = win32Registrar ?? throw new ArgumentNullException(nameof(win32Registrar));
            _logger = logger;
            _windowHandle = windowHandle;
        }

        public bool RegisterHotkey(HotkeyDefinition hotkey)
        {
            if (hotkey == null || string.IsNullOrEmpty(hotkey.Combination))
            {
                _logger?.LogWarning("Invalid hotkey provided for registration");
                return false;
            }

            try
            {
                // Parse hotkey combination
                if (!TryParseHotkey(hotkey.Combination, out uint modifiers, out uint virtualKey))
                {
                    _logger?.LogWarning($"Failed to parse hotkey combination: {hotkey.Combination}");
                    return false;
                }

                // Unregister existing hotkey with same ID if it exists
                if (_registeredHotkeys.ContainsKey(hotkey.Id))
                {
                    UnregisterHotkey(hotkey.Id);
                }

                var hotkeyId = _nextHotkeyId++;
                var success = _win32Registrar.RegisterHotKey(_windowHandle, hotkeyId, modifiers, virtualKey);

                if (success)
                {
                    _registeredHotkeys[hotkey.Id] = hotkeyId;
                    _hotkeyById[hotkeyId] = hotkey;
                    
                    _logger?.LogInformation($"Successfully registered hotkey: {hotkey.Combination} (ID: {hotkeyId})");
                    return true;
                }
                else
                {
                    var errorCode = Marshal.GetLastWin32Error();
                    _logger?.LogWarning($"Failed to register hotkey {hotkey.Combination}. Win32 error: {errorCode}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error registering hotkey {hotkey.Combination}: {ex.Message}");
                return false;
            }
        }

        public bool UnregisterHotkey(string hotkeyId)
        {
            if (string.IsNullOrEmpty(hotkeyId) || !_registeredHotkeys.TryGetValue(hotkeyId, out var id))
            {
                _logger?.LogWarning($"Hotkey {hotkeyId} not found for unregistration");
                return false;
            }

            try
            {
                var success = _win32Registrar.UnregisterHotKey(_windowHandle, id);
                
                if (success)
                {
                    _registeredHotkeys.Remove(hotkeyId);
                    _hotkeyById.Remove(id);
                    _logger?.LogInformation($"Successfully unregistered hotkey: {hotkeyId}");
                    return true;
                }
                else
                {
                    _logger?.LogWarning($"Failed to unregister hotkey: {hotkeyId}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error unregistering hotkey {hotkeyId}: {ex.Message}");
                return false;
            }
        }

        public void UnregisterAllHotkeys()
        {
            var hotkeyIds = _registeredHotkeys.Keys.ToList();
            foreach (var hotkeyId in hotkeyIds)
            {
                UnregisterHotkey(hotkeyId);
            }
        }

        public bool IsHotkeyRegistered(string hotkeyId)
        {
            return _registeredHotkeys.ContainsKey(hotkeyId);
        }

        public Dictionary<string, HotkeyDefinition> GetRegisteredHotkeys()
        {
            return _registeredHotkeys.ToDictionary(
                kvp => kvp.Key,
                kvp => _hotkeyById.GetValueOrDefault(kvp.Value)
            );
        }

        public HotkeyDefinition? GetHotkeyById(int id)
        {
            return _hotkeyById.GetValueOrDefault(id);
        }

        private bool TryParseHotkey(string combination, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;

            if (string.IsNullOrEmpty(combination))
                return false;

            var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return false;

            // Parse modifiers
            for (int i = 0; i < parts.Length - 1; i++)
            {
                switch (parts[i].Trim().ToUpperInvariant())
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= 0x0002; // MOD_CONTROL
                        break;
                    case "ALT":
                        modifiers |= 0x0001; // MOD_ALT
                        break;
                    case "SHIFT":
                        modifiers |= 0x0004; // MOD_SHIFT
                        break;
                    case "WIN":
                    case "WINDOWS":
                        modifiers |= 0x0008; // MOD_WIN
                        break;
                    default:
                        return false;
                }
            }

            // Parse virtual key
            var keyPart = parts[^1].Trim();
            virtualKey = ParseVirtualKey(keyPart);
            
            return virtualKey != 0;
        }

        private uint ParseVirtualKey(string keyName)
        {
            var upperKeyName = keyName.ToUpperInvariant();
            
            // Special key mappings
            var specialKeys = new Dictionary<string, uint>
            {
                ["SPACE"] = 0x20,
                ["ENTER"] = 0x0D,
                ["RETURN"] = 0x0D,
                ["TAB"] = 0x09,
                ["DELETE"] = 0x2E,
                ["DEL"] = 0x2E,
                ["INSERT"] = 0x2D,
                ["INS"] = 0x2D,
                ["BACKSPACE"] = 0x08,
                ["BACK"] = 0x08,
                ["ESCAPE"] = 0x1B,
                ["ESC"] = 0x1B,
                ["PAGEUP"] = 0x21,
                ["PAGEDOWN"] = 0x22,
                ["HOME"] = 0x24,
                ["END"] = 0x23,
                ["LEFT"] = 0x25,
                ["RIGHT"] = 0x27,
                ["UP"] = 0x26,
                ["DOWN"] = 0x28,
                ["F1"] = 0x70,
                ["F2"] = 0x71,
                ["F3"] = 0x72,
                ["F4"] = 0x73,
                ["F5"] = 0x74,
                ["F6"] = 0x75,
                ["F7"] = 0x76,
                ["F8"] = 0x77,
                ["F9"] = 0x78,
                ["F10"] = 0x79,
                ["F11"] = 0x7A,
                ["F12"] = 0x7B
            };

            if (specialKeys.TryGetValue(upperKeyName, out var vk))
                return vk;

            // Try single character
            if (keyName.Length == 1)
                return (uint)char.ToUpperInvariant(keyName[0]);

            return 0;
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                UnregisterAllHotkeys();
                _disposed = true;
            }
        }
    }
}