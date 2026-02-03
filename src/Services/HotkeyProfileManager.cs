using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Services
{
    /// <summary>
    /// Manages hotkey profiles and their hotkey definitions
    /// </summary>
    public class HotkeyProfileManager
    {
        private readonly ISettingsService _settingsService;
        private readonly ILogger<HotkeyProfileManager>? _logger;
        private readonly Dictionary<string, HotkeyProfile> _profiles;

        public HotkeyProfileManager(ISettingsService settingsService, ILogger<HotkeyProfileManager>? logger = null)
        {
            _settingsService = settingsService;
            _logger = logger;
            _profiles = new Dictionary<string, HotkeyProfile>();
            InitializeProfiles();
        }

        public async Task<bool> CreateProfileAsync(HotkeyProfile profile)
        {
            try
            {
                if (profile == null || string.IsNullOrEmpty(profile.Id))
                {
                    _logger?.LogWarning("Invalid profile provided for creation");
                    return false;
                }

                var settings = _settingsService.Settings;
                if (settings.Hotkeys.Profiles.ContainsKey(profile.Id))
                {
                    _logger?.LogWarning($"Profile {profile.Id} already exists");
                    return false;
                }

                profile.CreatedAt = DateTime.Now;
                profile.ModifiedAt = DateTime.Now;
                profile.IsDefault = false;
                profile.Version = "1.0";
                profile.Hotkeys = profile.Hotkeys ?? new Dictionary<string, HotkeyDefinition>();

                settings.Hotkeys.Profiles[profile.Id] = profile;
                await _settingsService.SaveAsync();

                _logger?.LogInformation($"Profile {profile.Id} created successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to create profile: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateProfileAsync(HotkeyProfile profile)
        {
            try
            {
                if (profile == null || string.IsNullOrEmpty(profile.Id))
                {
                    _logger?.LogWarning("Invalid profile provided for update");
                    return false;
                }

                var settings = _settingsService.Settings;
                if (!settings.Hotkeys.Profiles.ContainsKey(profile.Id))
                {
                    _logger?.LogWarning($"Profile {profile.Id} not found for update");
                    return false;
                }

                profile.ModifiedAt = DateTime.Now;
                settings.Hotkeys.Profiles[profile.Id] = profile;
                await _settingsService.SaveAsync();

                _logger?.LogInformation($"Profile {profile.Id} updated successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to update profile: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> DeleteProfileAsync(string profileId)
        {
            try
            {
                if (string.IsNullOrEmpty(profileId))
                {
                    _logger?.LogWarning("Invalid profile ID provided for deletion");
                    return false;
                }

                var settings = _settingsService.Settings;
                if (!settings.Hotkeys.Profiles.ContainsKey(profileId))
                {
                    _logger?.LogWarning($"Profile {profileId} not found for deletion");
                    return false;
                }

                if (profileId == "Default")
                {
                    _logger?.LogWarning("Cannot delete the Default profile");
                    return false;
                }

                settings.Hotkeys.Profiles.Remove(profileId);
                
                if (settings.Hotkeys.CurrentProfile == profileId)
                {
                    settings.Hotkeys.CurrentProfile = "Default";
                    _logger?.LogInformation($"Switched to Default profile after deleting {profileId}");
                }

                await _settingsService.SaveAsync();
                _logger?.LogInformation($"Profile {profileId} deleted successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to delete profile: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> SwitchProfileAsync(string profileId)
        {
            try
            {
                if (string.IsNullOrEmpty(profileId))
                {
                    _logger?.LogWarning("Invalid profile ID provided for switch");
                    return false;
                }

                var settings = _settingsService.Settings;
                if (!settings.Hotkeys.Profiles.ContainsKey(profileId))
                {
                    _logger?.LogWarning($"Profile {profileId} not found for switch");
                    return false;
                }

                if (settings.Hotkeys.CurrentProfile == profileId)
                {
                    _logger?.LogInformation($"Already using profile {profileId}");
                    return false;
                }

                settings.Hotkeys.CurrentProfile = profileId;
                await _settingsService.SaveAsync();

                _logger?.LogInformation($"Switched to profile {profileId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to switch profile: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> AddHotkeyToProfileAsync(string profileId, HotkeyDefinition hotkey)
        {
            try
            {
                var settings = _settingsService.Settings;
                if (!settings.Hotkeys.Profiles.ContainsKey(profileId))
                {
                    _logger?.LogWarning($"Profile {profileId} not found");
                    return false;
                }

                settings.Hotkeys.Profiles[profileId].Hotkeys[hotkey.Id] = hotkey;
                await _settingsService.SaveAsync();

                _logger?.LogInformation($"Added hotkey {hotkey.Id} to profile {profileId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to add hotkey to profile: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> RemoveHotkeyFromProfileAsync(string profileId, string hotkeyId)
        {
            try
            {
                var settings = _settingsService.Settings;
                if (!settings.Hotkeys.Profiles.ContainsKey(profileId))
                {
                    _logger?.LogWarning($"Profile {profileId} not found");
                    return false;
                }

                if (settings.Hotkeys.Profiles[profileId].Hotkeys.Remove(hotkeyId))
                {
                    await _settingsService.SaveAsync();
                    _logger?.LogInformation($"Removed hotkey {hotkeyId} from profile {profileId}");
                    return true;
                }

                _logger?.LogWarning($"Hotkey {hotkeyId} not found in profile {profileId}");
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to remove hotkey from profile: {ex.Message}");
                return false;
            }
        }

        public async Task<List<HotkeyProfile>> GetAllProfilesAsync()
        {
            try
            {
                var settings = _settingsService.Settings;
                return settings.Hotkeys.Profiles.Values.ToList();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get all profiles: {ex.Message}");
                return new List<HotkeyProfile>();
            }
        }

        public HotkeyProfile GetCurrentProfile()
        {
            try
            {
                var settings = _settingsService.Settings;
                var currentProfileId = settings.Hotkeys.CurrentProfile;
                
                if (settings.Hotkeys.Profiles.TryGetValue(currentProfileId, out var profile))
                {
                    return profile;
                }

                _logger?.LogWarning($"Current profile {currentProfileId} not found, returning Default");
                return settings.Hotkeys.Profiles.GetValueOrDefault("Default") ?? CreateDefaultProfile();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to get current profile: {ex.Message}");
                return CreateDefaultProfile();
            }
        }

        public async Task<bool> ProfileExistsAsync(string profileId)
        {
            try
            {
                var settings = _settingsService.Settings;
                return settings.Hotkeys.Profiles.ContainsKey(profileId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to check profile existence: {ex.Message}");
                return false;
            }
        }

        private void InitializeProfiles()
        {
            try
            {
                var settings = _settingsService.Settings;
                
                // Ensure Default profile exists
                if (!settings.Hotkeys.Profiles.ContainsKey("Default"))
                {
                    settings.Hotkeys.Profiles["Default"] = CreateDefaultProfile();
                }

                // Ensure current profile is valid
                if (!settings.Hotkeys.Profiles.ContainsKey(settings.Hotkeys.CurrentProfile))
                {
                    settings.Hotkeys.CurrentProfile = "Default";
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to initialize profiles: {ex.Message}");
            }
        }

        private HotkeyProfile CreateDefaultProfile()
        {
            return new HotkeyProfile
            {
                Id = "Default",
                Name = "Default Profile",
                Hotkeys = new Dictionary<string, HotkeyDefinition>
                {
                    ["toggle_recording"] = new HotkeyDefinition
                    {
                        Id = "toggle_recording",
                        Name = "Toggle Recording",
                        Combination = "Ctrl+Alt+V",
                        Action = "toggle_recording",
                        IsEnabled = true
                    },
                    ["show_settings"] = new HotkeyDefinition
                    {
                        Id = "show_settings",
                        Name = "Show Settings",
                        Combination = "Ctrl+Alt+S",
                        Action = "show_settings",
                        IsEnabled = true
                    },
                    ["exit_application"] = new HotkeyDefinition
                    {
                        Id = "exit_application",
                        Name = "Exit Application",
                        Combination = "Ctrl+Alt+X",
                        Action = "exit_application",
                        IsEnabled = true
                    }
                }
            };
        }
    }
}