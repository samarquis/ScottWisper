using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Interop;
using System.Windows.Input;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey
{
    public class HotkeyPressedEventArgs : EventArgs
    {
        public HotkeyDefinition Hotkey { get; }
        public DateTime Timestamp { get; }

        public HotkeyPressedEventArgs(HotkeyDefinition hotkey)
        {
            Hotkey = hotkey;
            Timestamp = DateTime.Now;
        }
    }

    public class HotkeyConflictEventArgs : EventArgs
    {
        public Configuration.HotkeyConflict Conflict { get; }

        public HotkeyConflictEventArgs(Configuration.HotkeyConflict conflict)
        {
            Conflict = conflict;
        }
    }

    public class HotkeyService : IHotkeyService
    {
        private const int WM_HOTKEY = 0x0312;
        private const int HOTKEY_ID_BASE = 9000;
        private const uint MOD_ALT = 0x0001;
        private const uint MOD_CONTROL = 0x0002;
        private const uint MOD_SHIFT = 0x0004;
        private const uint MOD_WIN = 0x0008;
        private const uint MOD_NOREPEAT = 0x4000;

        // Special key name mappings for common aliases
        private static readonly Dictionary<string, Key> _specialKeyMappings = new Dictionary<string, Key>(StringComparer.OrdinalIgnoreCase)
        {
            ["SPACE"] = Key.Space,
            ["ENTER"] = Key.Enter,
            ["RETURN"] = Key.Enter,
            ["TAB"] = Key.Tab,
            ["DELETE"] = Key.Delete,
            ["DEL"] = Key.Delete,
            ["INSERT"] = Key.Insert,
            ["INS"] = Key.Insert,
            ["BACKSPACE"] = Key.Back,
            ["BACK"] = Key.Back,
            ["ESCAPE"] = Key.Escape,
            ["ESC"] = Key.Escape,
            ["PAGEUP"] = Key.PageUp,
            ["PAGEDOWN"] = Key.PageDown,
            ["HOME"] = Key.Home,
            ["END"] = Key.End,
            ["LEFT"] = Key.Left,
            ["RIGHT"] = Key.Right,
            ["UP"] = Key.Up,
            ["DOWN"] = Key.Down,
            ["F1"] = Key.F1,
            ["F2"] = Key.F2,
            ["F3"] = Key.F3,
            ["F4"] = Key.F4,
            ["F5"] = Key.F5,
            ["F6"] = Key.F6,
            ["F7"] = Key.F7,
            ["F8"] = Key.F8,
            ["F9"] = Key.F9,
            ["F10"] = Key.F10,
            ["F11"] = Key.F11,
            ["F12"] = Key.F12
        };

        private IntPtr _windowHandle;
        private HwndSource? _source;
        private readonly Dictionary<string, int> _registeredHotkeys = new Dictionary<string, int>();
        private readonly Dictionary<int, HotkeyDefinition> _hotkeyById = new Dictionary<int, HotkeyDefinition>();
        private readonly ISettingsService _settingsService;
        private readonly IHotkeyRegistrar _hotkeyRegistrar;
        private Timer? _conflictCheckTimer;
        private int _nextHotkeyId = HOTKEY_ID_BASE;

        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
        public event EventHandler<HotkeyConflictEventArgs>? HotkeyConflictDetected;

        public bool IsHotkeyRegistered => _registeredHotkeys.Any();
        public HotkeyProfile CurrentProfile { get; private set; } = new HotkeyProfile { Id = "Default", Name = "Default" };

        public HotkeyService(ISettingsService settingsService) : this(settingsService, new Win32HotkeyRegistrar(), null)
        {
        }

        public HotkeyService(ISettingsService settingsService, IHotkeyRegistrar hotkeyRegistrar, IntPtr? windowHandle = null)
        {
            _settingsService = settingsService;
            _hotkeyRegistrar = hotkeyRegistrar;
            
            if (windowHandle.HasValue)
            {
                _windowHandle = windowHandle.Value;
            }
            else
            {
                // Get the main window handle
                var mainWindow = System.Windows.Application.Current?.MainWindow;
                if (mainWindow != null)
                {
                    _windowHandle = new WindowInteropHelper(mainWindow).Handle;
                }
            }
            
            Initialize();
        }

        private void Initialize()
        {
            // Fire-and-forget with exception handling
            _ = InitializeAsync();
        }
        
        private async Task InitializeAsync()
        {
            try
            {
                await Task.Yield();
                await LoadCurrentProfileAsync();
                RegisterCurrentProfileHotkeys();
                StartConflictMonitoring();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing hotkey service: {ex.Message}");
            }
        }

        private async Task LoadCurrentProfileAsync()
        {
            try
            {
                var settings = _settingsService.Settings;
                var profileId = settings.Hotkeys.CurrentProfile;
                
                if (settings.Hotkeys.Profiles.TryGetValue(profileId, out var profile))
                {
                    CurrentProfile = profile;
                }
                else
                {
                    // Create default profile if not exists
                    CurrentProfile = CreateDefaultProfile();
                    settings.Hotkeys.Profiles[CurrentProfile.Id] = CurrentProfile;
                    await _settingsService.SaveAsync();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load hotkey profile: {ex.Message}");
                CurrentProfile = CreateDefaultProfile();
            }
        }

        private HotkeyProfile CreateDefaultProfile()
        {
            return new HotkeyProfile
            {
                Id = "Default",
                Name = "Default Profile",
                Description = "Default hotkey configuration",
                IsDefault = true,
                Hotkeys = new Dictionary<string, HotkeyDefinition>
                {
                    ["toggle_recording"] = new HotkeyDefinition
                    {
                        Id = "toggle_recording",
                        Name = "Toggle Recording",
                        Combination = "Ctrl+Alt+V",
                        Action = "toggle_recording",
                        IsEnabled = true,
                        Description = "Start or stop voice recording"
                    },
                    ["show_settings"] = new HotkeyDefinition
                    {
                        Id = "show_settings",
                        Name = "Show Settings",
                        Combination = "Ctrl+Alt+S",
                        Action = "show_settings",
                        IsEnabled = true,
                        Description = "Open the settings window"
                    },
                    ["emergency_stop"] = new HotkeyDefinition
                    {
                        Id = "emergency_stop",
                        Name = "Emergency Stop",
                        Combination = "Ctrl+Alt+Shift+X",
                        Action = "emergency_stop",
                        IsEnabled = true,
                        IsEmergency = true,
                        Description = "Immediately stop all recording and clear transcriptions"
                    }
                }
            };
        }

        private void RegisterCurrentProfileHotkeys()
        {
            UnregisterAllHotkeys();
            
            foreach (var hotkey in CurrentProfile.Hotkeys.Values.Where(h => h.IsEnabled))
            {
                RegisterHotkey(hotkey);
            }
        }

        public bool RegisterHotkey(HotkeyDefinition hotkey)
        {
            if (_windowHandle == IntPtr.Zero || string.IsNullOrEmpty(hotkey.Combination))
                return false;

            if (_registeredHotkeys.ContainsKey(hotkey.Id))
            {
                UnregisterHotkey(hotkey.Id);
            }

            if (TryParseHotkey(hotkey.Combination, out uint modifiers, out uint virtualKey))
            {
                int hotkeyId = _nextHotkeyId++;
                
                bool success = _hotkeyRegistrar.RegisterHotKey(_windowHandle, hotkeyId, modifiers | MOD_NOREPEAT, virtualKey);
                
                if (success)
                {
                    _registeredHotkeys[hotkey.Id] = hotkeyId;
                    _hotkeyById[hotkeyId] = hotkey;
                    
                    if (_source == null && _windowHandle != IntPtr.Zero)
                    {
                        _source = HwndSource.FromHwnd(_windowHandle);
                        if (_source != null)
                        {
                            _source.AddHook(WndProc);
                        }
                    }
                    
                    return true;
                }
                else
                {
                    int errorCode = _hotkeyRegistrar.GetLastWin32Error();
                    var conflict = DetectConflict(hotkey, errorCode);
                    if (conflict != null)
                    {
                        HotkeyConflictDetected?.Invoke(this, new HotkeyConflictEventArgs(conflict));
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"Failed to register hotkey {hotkey.Combination}. Error: {errorCode}");
                    return false;
                }
            }
            
            return false;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_HOTKEY)
            {
                int hotkeyId = wParam.ToInt32();
                if (_hotkeyById.TryGetValue(hotkeyId, out var hotkey))
                {
                    // Update usage statistics
                    hotkey.LastUsed = DateTime.Now;
                    hotkey.UsageCount++;
                    
                    HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(hotkey));
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void UnregisterHotkey(string hotkeyId)
        {
            if (_registeredHotkeys.TryGetValue(hotkeyId, out var id))
            {
                _hotkeyRegistrar.UnregisterHotKey(_windowHandle, id);
                _registeredHotkeys.Remove(hotkeyId);
                _hotkeyById.Remove(id);
            }
        }

        private void UnregisterAllHotkeys()
        {
            foreach (var kvp in _registeredHotkeys.ToList())
            {
                _hotkeyRegistrar.UnregisterHotKey(_windowHandle, kvp.Value);
            }
            
            _registeredHotkeys.Clear();
            _hotkeyById.Clear();
        }

        public HotkeyValidationResult ValidateHotkey(string combination)
        {
            var result = new HotkeyValidationResult { IsValid = true };
            
            // Check if combination is in valid format
            if (string.IsNullOrWhiteSpace(combination))
            {
                result.IsValid = false;
                result.ErrorMessage = "Hotkey combination cannot be empty";
                return result;
            }

            if (!TryParseHotkey(combination, out uint modifiers, out uint virtualKey))
            {
                result.IsValid = false;
                result.ErrorMessage = "Invalid hotkey format. Use combinations like 'Ctrl+Alt+V'";
                return result;
            }

            // Check for conflicts with registered hotkeys
            var conflictingHotkey = _registeredHotkeys.Values
                .FirstOrDefault(id => _hotkeyById.TryGetValue(id, out var hk) && 
                                   TryParseHotkey(hk.Combination, out uint mod, out uint vk) && 
                                   mod == modifiers && vk == virtualKey);

            if (conflictingHotkey > 0 && _hotkeyById.TryGetValue(conflictingHotkey, out var conflictDef))
            {
                result.IsValid = false;
                result.ErrorMessage = $"This hotkey conflicts with: {conflictDef.Name} ({conflictDef.Combination})";
                result.Conflicts.Add(new Configuration.HotkeyConflict
                {
                    ConflictingHotkey = conflictDef.Combination,
                    ConflictingApplication = "WhisperKey",
                    ConflictType = "application",
                    SuggestedHotkey = SuggestAlternativeHotkey(modifiers, virtualKey)
                });
            }

            // Add accessibility warnings if needed
            var settings = _settingsService.Settings;
            if (settings.Hotkeys.EnableAccessibilityOptions && IsProblematicHotkey(modifiers, virtualKey))
            {
                result.WarningMessage = "This hotkey may be difficult to use for users with accessibility needs";
            }

            return result;
        }

        private bool TryParseHotkey(string combination, out uint modifiers, out uint virtualKey)
        {
            modifiers = 0;
            virtualKey = 0;

            if (string.IsNullOrWhiteSpace(combination))
                return false;

            var parts = combination.Split('+', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts.Select(p => p.Trim().ToUpperInvariant()))
            {
                switch (part)
                {
                    case "CTRL":
                    case "CONTROL":
                        modifiers |= MOD_CONTROL;
                        break;
                    case "ALT":
                        modifiers |= MOD_ALT;
                        break;
                    case "SHIFT":
                        modifiers |= MOD_SHIFT;
                        break;
                    case "WIN":
                    case "WINDOWS":
                        modifiers |= MOD_WIN;
                        break;
                    default:
                        // Try to parse as virtual key
                        if (part.Length == 1)
                        {
                            virtualKey = (uint)char.ToUpper(part[0]);
                        }
                        else if (_specialKeyMappings.TryGetValue(part, out var mappedKey))
                        {
                            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(mappedKey);
                        }
                        else if (Enum.TryParse<Key>(part, out var key))
                        {
                            virtualKey = (uint)KeyInterop.VirtualKeyFromKey(key);
                        }
                        else
                        {
                            return false;
                        }
                        break;
                }
            }

            return virtualKey != 0;
        }

        private Configuration.HotkeyConflict? DetectConflict(HotkeyDefinition hotkey, int errorCode)
        {
            var conflictType = GetConflictType(errorCode);
            if (conflictType == null) return null;

            return new Configuration.HotkeyConflict
            {
                ConflictingHotkey = hotkey.Combination,
                ConflictType = conflictType,
                DetectedAt = DateTime.Now,
                IsResolvable = true,
                SuggestedHotkey = TryParseHotkey(hotkey.Combination, out uint modifiers, out uint virtualKey) 
                    ? SuggestAlternativeHotkey(modifiers, virtualKey) 
                    : ""
            };
        }

        private string? GetConflictType(int errorCode)
        {
            return errorCode switch
            {
                1409 => "system", // ERROR_HOTKEY_ALREADY_REGISTERED
                1414 => "system", // ERROR_HOTKEY_ALREADY_REGISTERED
                _ => null
            };
        }

        private string SuggestAlternativeHotkey(uint usedModifiers, uint usedVirtualKey)
        {
            // Try different modifier combinations
            var alternativeModifiers = new[]
            {
                MOD_CONTROL | MOD_ALT,
                MOD_CONTROL | MOD_SHIFT,
                MOD_ALT | MOD_SHIFT,
                MOD_CONTROL | MOD_WIN,
                MOD_ALT | MOD_WIN,
                MOD_SHIFT | MOD_WIN,
                MOD_CONTROL | MOD_ALT | MOD_SHIFT,
                MOD_CONTROL | MOD_ALT | MOD_WIN
            };

            foreach (var modifiers in alternativeModifiers)
            {
                if (modifiers != usedModifiers)
                {
                    return $"{ModifiersToString(modifiers)}+{((char)usedVirtualKey)}";
                }
            }

            // Try different virtual keys
            for (int c = 'A'; c <= 'Z'; c++)
            {
                if (c != usedVirtualKey)
                {
                    return $"{ModifiersToString(usedModifiers)}+{(char)c}";
                }
            }

            return "Ctrl+Alt+Space";
        }

        private string ModifiersToString(uint modifiers)
        {
            var parts = new List<string>();
            
            if ((modifiers & MOD_CONTROL) != 0) parts.Add("Ctrl");
            if ((modifiers & MOD_ALT) != 0) parts.Add("Alt");
            if ((modifiers & MOD_SHIFT) != 0) parts.Add("Shift");
            if ((modifiers & MOD_WIN) != 0) parts.Add("Win");
            
            return string.Join("+", parts);
        }

        private bool IsProblematicHotkey(uint modifiers, uint virtualKey)
        {
            // Check for combinations that might be difficult
            var requiresThreeFingers = (((modifiers & MOD_CONTROL) != 0 ? 1 : 0) + 
                                      ((modifiers & MOD_ALT) != 0 ? 1 : 0) + 
                                      ((modifiers & MOD_SHIFT) != 0 ? 1 : 0)) >= 2;
            
            var requiresSystemKey = (modifiers & MOD_WIN) != 0;
            
            return requiresThreeFingers && requiresSystemKey;
        }

        private void StartConflictMonitoring()
        {
            var settings = _settingsService.Settings;
            if (settings.Hotkeys.ConflictCheckInterval > 0)
            {
                _conflictCheckTimer = new Timer(ConflictCheckCallback, null, 
                    settings.Hotkeys.ConflictCheckInterval, 
                    settings.Hotkeys.ConflictCheckInterval);
            }
        }

        private void ConflictCheckCallback(object? state)
        {
            // Fire-and-forget with exception handling
            _ = ConflictCheckCallbackAsync(state);
        }
        
        private async Task ConflictCheckCallbackAsync(object? state)
        {
            try
            {
                // Check for system-level hotkey conflicts
                await Task.Run(() =>
                {
                    foreach (var hotkey in CurrentProfile.Hotkeys.Values.Where(h => h.IsEnabled))
                    {
                        var validation = ValidateHotkey(hotkey.Combination);
                        if (!validation.IsValid && validation.Conflicts.Any())
                        {
                            foreach (var conflict in validation.Conflicts)
                            {
                                HotkeyConflictDetected?.Invoke(this, new HotkeyConflictEventArgs(conflict));
                            }
                        }
                    }
                }).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in conflict check callback: {ex.Message}");
            }
        }

        public async Task<bool> SwitchProfileAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || profileId == CurrentProfile.Id)
                return false;

            var settings = _settingsService.Settings;
            if (!settings.Hotkeys.Profiles.TryGetValue(profileId, out var newProfile))
                return false;

            // Save current settings
            settings.Hotkeys.CurrentProfile = profileId;
            await _settingsService.SaveAsync();

            // Switch to new profile
            CurrentProfile = newProfile;
            RegisterCurrentProfileHotkeys();

            return true;
        }

        public async Task<bool> CreateProfileAsync(HotkeyProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                return false;

            var settings = _settingsService.Settings;
            if (settings.Hotkeys.Profiles.ContainsKey(profile.Id))
                return false;

            profile.CreatedAt = DateTime.Now;
            profile.ModifiedAt = DateTime.Now;
            
            settings.Hotkeys.Profiles[profile.Id] = profile;
            await _settingsService.SaveAsync();

            return true;
        }

        public async Task<bool> UpdateProfileAsync(HotkeyProfile profile)
        {
            if (profile == null || string.IsNullOrWhiteSpace(profile.Id))
                return false;

            var settings = _settingsService.Settings;
            if (!settings.Hotkeys.Profiles.ContainsKey(profile.Id))
                return false;

            profile.ModifiedAt = DateTime.Now;
            settings.Hotkeys.Profiles[profile.Id] = profile;
            await _settingsService.SaveAsync();

            // Refresh if this is the current profile
            if (profile.Id == CurrentProfile.Id)
            {
                CurrentProfile = profile;
                RegisterCurrentProfileHotkeys();
            }

            return true;
        }

        public async Task<bool> DeleteProfileAsync(string profileId)
        {
            if (string.IsNullOrWhiteSpace(profileId) || profileId == "Default")
                return false;

            var settings = _settingsService.Settings;
            if (!settings.Hotkeys.Profiles.ContainsKey(profileId))
                return false;

            settings.Hotkeys.Profiles.Remove(profileId);

            // If deleting current profile, switch to default
            if (profileId == CurrentProfile.Id)
            {
                await SwitchProfileAsync("Default");
            }

            await _settingsService.SaveAsync();
            return true;
        }

        public async Task UpdateHotkeyAsync(string hotkeyId, HotkeyDefinition hotkey)
        {
            if (CurrentProfile.Hotkeys.ContainsKey(hotkeyId))
            {
                CurrentProfile.Hotkeys[hotkeyId] = hotkey;
                CurrentProfile.ModifiedAt = DateTime.Now;
                
                await UpdateProfileAsync(CurrentProfile);
                
                // Re-register this hotkey
                RegisterHotkey(hotkey);
            }
        }

        public async Task ExportProfileAsync(string profileId, string filePath)
        {
            var settings = _settingsService.Settings;
            if (!settings.Hotkeys.Profiles.TryGetValue(profileId, out var profile))
                throw new ArgumentException($"Profile {profileId} not found");

            var exportData = new
            {
                Profile = profile,
                ExportedAt = DateTime.Now,
                Version = "1.0",
                Application = "WhisperKey"
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);

            // Store backup path
            settings.Hotkeys.BackupProfilePath = filePath;
            await _settingsService.SaveAsync();
        }

        public async Task<HotkeyProfile> ImportProfileAsync(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Profile file not found: {filePath}");

            try
            {
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                var importData = JsonSerializer.Deserialize<JsonElement>(json);
                
                var profile = JsonSerializer.Deserialize<HotkeyProfile>(
                    importData.GetProperty("Profile").GetRawText());

                if (profile == null)
                    throw new InvalidOperationException("Invalid profile file format");

                // Ensure unique ID
                var originalId = profile.Id;
                var counter = 1;
                while (_settingsService.Settings.Hotkeys.Profiles.ContainsKey(profile.Id))
                {
                    profile.Id = $"{originalId}_{counter++}";
                }

                await CreateProfileAsync(profile);
                return profile;
            }
            catch (System.Text.Json.JsonException ex)
            {
                throw new InvalidOperationException("Invalid profile file format", ex);
            }
        }

        public async Task<List<HotkeyProfile>> GetAllProfilesAsync()
        {
            var settings = _settingsService.Settings;
            return settings.Hotkeys.Profiles.Values.ToList();
        }

        public async Task ResetToDefaultsAsync()
        {
            var defaultProfile = CreateDefaultProfile();
            await UpdateProfileAsync(defaultProfile);
            await SwitchProfileAsync("Default");
        }

        public void Dispose()
        {
            _conflictCheckTimer?.Dispose();
            UnregisterAllHotkeys();

            if (_source != null)
            {
                _source.RemoveHook(WndProc);
                _source = null;
            }
        }
    }
}