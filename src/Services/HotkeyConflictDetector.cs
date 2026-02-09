using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using WhisperKey.Configuration;

namespace WhisperKey.Services
{
    /// <summary>
    /// Detects and validates hotkey conflicts
    /// </summary>
    public class HotkeyConflictDetector
    {
        private readonly ILogger<HotkeyConflictDetector>? _logger;

        public HotkeyConflictDetector(ILogger<HotkeyConflictDetector>? logger = null)
        {
            _logger = logger;
        }

        public HotkeyValidationResult ValidateHotkey(string combination, HotkeySettings settings)
        {
            var result = new HotkeyValidationResult
            {
                IsValid = true,
                Conflicts = new List<HotkeyConflict>()
            };

            try
            {
                // Validate format
                if (string.IsNullOrEmpty(combination))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Hotkey combination cannot be empty";
                    return result;
                }

                if (!IsValidFormat(combination))
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Invalid hotkey format. Use format like 'Alt+Space'";
                    return result;
                }

                // Parse and check for conflicts
                var (modifiers, key) = ParseHotkey(combination);
                if (modifiers.Count == 0)
                {
                    result.IsValid = false;
                    result.ErrorMessage = "Invalid hotkey combination";
                    return result;
                }

                // Check for problematic combinations
                if (IsProblematicCombination(combination, settings.EnableAccessibilityOptions))
                {
                    result.WarningMessage = "This hotkey combination may be difficult to use or conflict with system shortcuts";
                }

                // Check for conflicts with existing profiles
                var conflicts = DetectConflicts(combination, settings, new Dictionary<string, HotkeyDefinition>());
                if (conflicts.Count > 0)
                {
                    result.IsValid = false;
                    result.Conflicts = conflicts;
                    result.ErrorMessage = "Hotkey conflicts with existing shortcuts";
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.ErrorMessage = $"Error validating hotkey: {ex.Message}";
                _logger?.LogError(ex, $"Error validating hotkey {combination}");
            }

            return result;
        }

        public List<HotkeyConflict> DetectConflicts(string combination, HotkeySettings settings, Dictionary<string, HotkeyDefinition> registeredHotkeys)
        {
            var conflicts = new List<HotkeyConflict>();

            try
            {
                // Check conflicts in all profiles
                foreach (var profile in settings.Profiles.Values)
                {
                    foreach (var hotkey in profile.Hotkeys.Values)
                    {
                        if (string.Equals(hotkey.Combination, combination, StringComparison.OrdinalIgnoreCase))
                        {
                            conflicts.Add(new HotkeyConflict
                            {
                                ConflictType = "profile",
                                Hotkey = hotkey.Id,
                                ConflictingApplication = profile.Id,
                                ConflictingHotkeyName = hotkey.Name,
                                Application = "WhisperKey"
                            });
                        }
                    }
                }

                // Check conflicts with registered hotkeys
                foreach (var registeredHotkey in registeredHotkeys.Values)
                {
                        if (string.Equals(registeredHotkey.Combination, combination, StringComparison.OrdinalIgnoreCase))
                        {
                            conflicts.Add(new HotkeyConflict
                            {
                                ConflictType = "system",
                                Hotkey = registeredHotkey.Id,
                                ConflictingApplication = "System",
                                ConflictingHotkeyName = registeredHotkey.Name,
                                Application = registeredHotkey.Application ?? "Unknown"
                            });
                        }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error detecting conflicts for {combination}");
            }

            return conflicts;
        }

        public (List<string> modifiers, string key) ParseHotkey(string combination)
        {
            var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries);
            var modifiers = new List<string>();
            string key = "";

            if (parts.Length > 0)
            {
                // All parts except the last one are modifiers
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    modifiers.Add(parts[i].Trim());
                }
                
                // The last part is the key
                key = parts[^1].Trim();
            }

            return (modifiers, key);
        }

        public bool IsValidFormat(string combination)
        {
            if (string.IsNullOrEmpty(combination))
                return false;

            var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) // At least one modifier + key
                return false;

            // Check each part
            for (int i = 0; i < parts.Length - 1; i++) // Modifiers
            {
                var modifier = parts[i].Trim().ToUpperInvariant();
                if (!IsValidModifier(modifier))
                    return false;
            }

            // Check the key part
            var key = parts[^1].Trim();
            return IsValidKey(key);
        }

        public bool IsProblematicCombination(string combination, bool enableAccessibilityOptions)
        {
            var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries);
            
            // Check for too many modifiers
            if (parts.Length > 4 && enableAccessibilityOptions)
                return true;

            // Check for potentially problematic key combinations
            var key = parts[^1].Trim().ToUpperInvariant();
            var problematicKeys = new[] { "WIN", "LWIN", "RWIN", "APPS" };
            
            return problematicKeys.Contains(key);
        }

        public string? GetConflictType(string hotkeyId, HotkeySettings settings, Dictionary<string, HotkeyDefinition> registeredHotkeys)
        {
            // Check in profiles first
            foreach (var profile in settings.Profiles.Values)
            {
                if (profile.Hotkeys.ContainsKey(hotkeyId))
                    return "profile";
            }

            // Check in registered hotkeys
            if (registeredHotkeys.ContainsKey(hotkeyId))
                return "system";

            return null;
        }

        private bool IsValidModifier(string modifier)
        {
            var validModifiers = new[] { "CTRL", "CONTROL", "ALT", "SHIFT", "WIN", "WINDOWS" };
            return validModifiers.Contains(modifier);
        }

        private bool IsValidKey(string key)
        {
            if (key.Length == 1) // Single character
                return true;

            // Check against special keys
            var specialKeys = new[]
            {
                "SPACE", "ENTER", "RETURN", "TAB", "DELETE", "DEL", "INSERT", "INS",
                "BACKSPACE", "BACK", "ESCAPE", "ESC", "PAGEUP", "PAGEDOWN", "HOME", "END",
                "LEFT", "RIGHT", "UP", "DOWN",
                "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12"
            };

            return specialKeys.Contains(key.ToUpperInvariant());
        }
    }
}
