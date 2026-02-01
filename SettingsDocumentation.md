# WhisperKey Settings Guide

## Overview

WhisperKey provides a comprehensive settings management system that allows you to configure every aspect of the voice dictation experience. Settings are organized into logical categories and automatically saved with encryption for sensitive data.

## Table of Contents

1. [Audio Device Settings](#audio-device-settings)
2. [Transcription Settings](#transcription-settings)
3. [Hotkey Configuration](#hotkey-configuration)
4. [Interface Settings](#interface-settings)
5. [Advanced Configuration](#advanced-configuration)
6. [Troubleshooting](#troubleshooting)
7. [Migration and Upgrade](#migration-and-upgrade)
8. [Developer API Reference](#developer-api-reference)

---

## Audio Device Settings

### Input Device Configuration

**Primary Input Device**: Select your main microphone for voice input.
- **Default**: Uses Windows default recording device
- **Recommendation**: Choose a dedicated USB microphone for best quality
- **Auto-switch**: Automatically falls back to secondary device if primary fails

**Fallback Input Device**: Backup microphone if primary device is unavailable.
- **Purpose**: Ensures continuous operation during device failures
- **Auto-detection**: System automatically tests and selects compatible devices

**Device Testing**: Test your audio devices before using them for dictation.
1. Select device from dropdown
2. Click "Test" button
3. Speak clearly for 5 seconds
4. Review test results for compatibility and quality

### Output Device Configuration

**Primary Output Device**: Select speaker for audio feedback.
- **Sounds**: Start recording, stop recording, transcription complete
- **Volume**: Adjustable feedback volume (0-100%)
- **Mute Option**: Disable audio feedback while keeping visual indicators

**Device Quality Metrics**: View detailed information about each device:
- **Sample Rate**: 8kHz to 192kHz (16kHz recommended for speech)
- **Channels**: Mono (1) or Stereo (2)
- **Latency**: Input/output delay in milliseconds
- **Compatibility Score**: 0-100 rating based on test results

### Device Recommendations

**Highly Compatible Devices**:
- USB microphones with dedicated drivers
- Professional audio interfaces
- Built-in laptop microphones (post-2020)

**Compatibility Considerations**:
- **Sample Rate**: 16kHz optimal for speech recognition
- **Format**: PCM, 16-bit preferred
- **Buffer Size**: 512-1024 samples for real-time processing

---

## Transcription Settings

### Provider Configuration

**OpenAI Whisper API** (Recommended):
- **Model**: `whisper-1` (balanced accuracy and speed)
- **API Key**: Required for cloud-based transcription
- **Endpoint**: `https://api.openai.com/v1/audio/transcriptions`

**Local Whisper Options**:
- **Model**: `whisper-base`, `whisper-small`, `whisper-medium`
- **Hardware**: GPU acceleration recommended for real-time use
- **Memory**: Minimum 4GB RAM for base model

### API Key Management

**Secure Storage**: API keys are encrypted using Windows DPAPI
- **Location**: `%APPDATA%\WhisperKey\`
- **Encryption**: AES-256 with machine-specific key
- **Backup**: Included in settings backup files

**API Key Testing**:
1. Enter your API key in the settings
2. Click "Test API Key"
3. Verify successful connection and quota status
4. Save settings to enable transcription

### Advanced Transcription Options

**Auto-punctuation**: Automatically adds periods, commas, and question marks.
- **Benefit**: More natural text output
- **Customization**: Disable for manual punctuation control

**Real-time Transcription**: Process audio as you speak.
- **Latency**: Sub-100ms processing
- **Accuracy**: Slightly reduced vs. batch processing
- **Resource Usage**: Higher CPU/GPU utilization

**Profanity Filter**: Automatically censor inappropriate language.
- **Level**: Mild, Moderate, Strict
- **Customization**: Add custom words to filter list

**Confidence Threshold**: Minimum confidence score for text acceptance.
- **Range**: 50-95%
- **Default**: 80%
- **Trade-off**: Higher threshold = fewer errors but may miss quiet speech

**Maximum Recording Duration**: Limit recording length to prevent excessive API usage.
- **Range**: 10-300 seconds
- **Default**: 30 seconds
- **Purpose**: Cost control and processing efficiency

---

## Hotkey Configuration

### Profile Management

**Hotkey Profiles**: Organize hotkeys for different use cases.
- **Default Profile**: Basic dictation controls
- **Custom Profiles**: Create specialized layouts (gaming, office, etc.)
- **Import/Export**: Share profiles between computers

**Profile Features**:
- **Conflict Detection**: Automatic identification of conflicting hotkeys
- **Emergency Hotkeys**: Always-available shortcuts for critical functions
- **Accessibility Mode**: Simplified hotkeys for users with accessibility needs

### Hotkey Recording

**Recording Process**:
1. Click "Record" button
2. Press desired key combination
3. Click "Stop" to finish
4. Add descriptive name and action

**Supported Modifiers**:
- **Ctrl** (Control)
- **Alt** (Alternate)
- **Shift** (Shift)
- **Win** (Windows key)

**Valid Combinations**:
- Single modifier + key: `Ctrl+V`, `Alt+F1`
- Double modifier + key: `Ctrl+Alt+V`, `Ctrl+Shift+Space`
- Triple modifier: `Ctrl+Alt+Shift+V` (rarely needed)

### Conflict Resolution

**System Conflicts**: Hotkeys used by Windows or other applications.
- **Detection**: Automatic identification during recording
- **Resolution**: Suggest alternative combinations
- **Administrator Rights**: Required for some system-level hotkeys

**Application Conflicts**: Hotkeys used within WhisperKey.
- **Prevention**: Duplicate hotkey detection
- **Auto-fix**: One-click conflict resolution
- **Warnings**: Optional notifications about potential conflicts

**Common Resolutions**:
- `Ctrl+V` → `Ctrl+Alt+V` (avoid clipboard conflict)
- `Alt+Tab` → `Alt+`+` (avoid Windows switching)
- `Win+R` → `Ctrl+Win+R` (avoid Run dialog)

---

## Interface Settings

### Visual Feedback

**Visual Indicators**: On-screen feedback for dictation status.
- **Status Colors**: Red (recording), Yellow (processing), Green (complete)
- **Position**: Automatic placement to avoid covering active window
- **Opacity**: Adjustable transparency (20-100%)

**Transcription Window**: Real-time text display during dictation.
- **Auto-positioning**: Intelligently places window to avoid interference
- **Font Scaling**: Respects Windows DPI and accessibility settings
- **Color Themes**: Light, Dark, High Contrast, System

**System Tray Integration**: Background operation with tray icon.
- **Icon States**: Different icons for recording, idle, error
- **Context Menu**: Quick access to common functions
- **Notifications**: Windows toast notifications for important events

### Audio Feedback

**Sound Events**: Audio cues for different operations.
- **Start Recording**: Ascending tone (440Hz)
- **Stop Recording**: Descending tone (220Hz)
- **Transcription Complete**: Success chime (880Hz + 440Hz)
- **Error**: Warning buzzer (low-frequency pulse)

**Volume Control**: Feedback volume independent of system volume.
- **Range**: 0-100%
- **Mute Option**: Disable audio while keeping visual feedback
- **Test**: Play test sounds to verify volume

### Behavior Settings

**Startup Options**:
- **Start with Windows**: Launch automatically on system boot
- **Minimize to Tray**: Keep running in background
- **Show on Startup**: Display main window when launched

**Window Management**:
- **Always on Top**: Keep transcription window visible
- **Auto-hide**: Hide window when not actively dictating
- **Remember Position**: Save window location between sessions

---

## Advanced Configuration

### Performance Tuning

**Audio Buffer Settings**:
- **Buffer Size**: 256-4096 samples (default: 512)
- **Trade-offs**: Smaller = lower latency, higher CPU usage
- **Recommendation**: 512 for most systems, 256 for low-latency needs

**Processing Threads**: Configure parallel processing.
- **Thread Count**: Auto-detect or manual (1-8)
- **GPU Acceleration**: Enable if compatible GPU available
- **Memory Usage**: Limit maximum RAM usage for large transcriptions

**Network Settings** (Cloud API):
- **Timeout**: Request timeout in seconds (default: 30)
- **Retry Count**: Number of retry attempts (default: 3)
- **Connection Pool**: Reuse connections for efficiency

### Security Settings

**Data Encryption**: All sensitive data encrypted at rest.
- **Algorithm**: AES-256-CBC
- **Key Storage**: Windows DPAPI for key protection
- **Backup Encryption**: Settings backups also encrypted

**Privacy Options**:
- **Local Processing**: Keep audio on device when possible
- **Telemetry**: Optional anonymous usage statistics
- **Data Retention**: Automatic cleanup of old recordings

### Enterprise Features

**Group Policy Support**: Windows Group Policy integration.
- **Configuration**: Centralized settings management
- **Lockdown**: Prevent user modifications to critical settings
- **Deployment**: Silent installation with predefined settings

**Roaming Profiles**: Settings synchronization across devices.
- **Cloud Storage**: OneDrive, SharePoint, or custom network location
- **Conflict Resolution**: Merge conflicting settings intelligently
- **Offline Support**: Cached settings work without network

---

## Troubleshooting

### Common Issues

#### Audio Device Problems

**Problem**: No audio input or poor quality
**Solutions**:
1. Check microphone physical connection
2. Verify microphone permissions in Windows Settings
3. Test with different USB port
4. Update audio drivers
5. Try default Windows audio driver

**Problem**: Audio feedback or echo
**Solutions**:
1. Use headphones instead of speakers
2. Lower microphone volume or sensitivity
3. Enable echo cancellation in audio settings
4. Move microphone away from speakers

#### Transcription Issues

**Problem**: Inaccurate transcription
**Solutions**:
1. Improve microphone placement and distance
2. Speak clearly and at consistent pace
3. Reduce background noise
4. Try different transcription model
5. Check API key quota and limits

**Problem**: Slow transcription response
**Solutions**:
1. Check internet connection stability
2. Reduce maximum recording duration
3. Try lower quality audio settings
4. Consider local Whisper model
5. Restart application to clear cache

#### Hotkey Problems

**Problem**: Hotkeys not working
**Solutions**:
1. Run application as Administrator
2. Check for conflicts with other applications
3. Verify hotkey recording was successful
4. Try different key combination
5. Restart Windows hotkey service

**Problem**: Hotkey triggers unintentionally
**Solutions**:
1. Use more complex combinations (3+ keys)
2. Enable accessibility mode if needed
3. Adjust key sensitivity settings
4. Use emergency hotkeys for critical functions

### Diagnostic Tools

**Settings Validation**: Built-in validation checks for common issues.
```
Menu: Help → Diagnostics → Settings Validation
Checks:
- File permissions
- Configuration integrity
- Device compatibility
- API connectivity
- Hotkey conflicts
```

**Log Files**: Detailed logging for troubleshooting.
```
Location: %APPDATA%\WhisperKey\Logs\
Files:
- settings.log - Settings operations
- audio.log - Audio device issues
- transcription.log - Transcription errors
- hotkeys.log - Hotkey registration problems
```

**Reset Options**: Various reset levels for different scenarios.
```
Soft Reset: Reset UI settings only
Medium Reset: Reset audio and hotkey settings
Hard Reset: Reset all settings to defaults
Factory Reset: Complete wipe including encrypted data
```

---

## Migration and Upgrade

### Version Migration

**Automatic Migration**: Settings automatically upgraded between versions.
- **Backup Creation**: Current settings backed up before migration
- **Compatibility Check**: Verify new version compatibility
- **Rollback**: Option to revert if migration fails
- **Validation**: Post-migration settings validation

**Migration Scenarios**:
- **v1.0 to v2.0**: Added hotkey profiles and device testing
- **v2.0 to v2.1**: Enhanced encryption and security features
- **v2.1 to v3.0**: Local Whisper model integration

### Settings Backup

**Manual Backup**: Export complete settings configuration.
```
File Menu: Settings → Backup
Format: JSON with metadata
Content: All settings, encrypted data, profiles
Location: User-specified path
```

**Automatic Backup**: Regular automatic backups.
```
Frequency: Weekly
Retention: 10 backups
Location: %APPDATA%\WhisperKey\Backups\
Naming: settings_backup_YYYYMMDD_HHMMSS.json
```

**Profile Backup**: Individual hotkey profile backup.
```
Context Menu: Right-click profile → Export
Format: JSON with version info
Compatibility: Cross-version import
Metadata: Creation time, version, application
```

### Import and Restore

**Settings Import**: Restore from backup file.
```
Validation: File format and integrity checking
Merge Options: Replace, Merge, or Selective import
Conflict Resolution: User choice for conflicting values
Verification: Post-import validation and testing
```

**Profile Import**: Add hotkey profiles from file.
```
ID Handling: Automatic renaming for conflicts
Version Compatibility: Upgrade old format profiles
Hotkey Validation: Check for conflicts on import
Preview Mode: Review before applying changes
```

---

## Developer API Reference

### SettingsService Interface

```csharp
public interface ISettingsService
{
    // Core settings access
    AppSettings Settings { get; }
    Task SaveAsync();
    Task<T> GetValueAsync<T>(string key);
    Task SetValueAsync<T>(string key, T value);
    
    // Encrypted storage
    Task<string> GetEncryptedValueAsync(string key);
    Task SetEncryptedValueAsync(string key, string value);
    
    // Device management
    Task SetSelectedInputDeviceAsync(string deviceId);
    Task<DeviceSpecificSettings> GetDeviceSettingsAsync(string deviceId);
    Task SetDeviceSettingsAsync(string deviceId, DeviceSpecificSettings settings);
    
    // Hotkey management
    Task<bool> CreateHotkeyProfileAsync(HotkeyProfile profile);
    Task<List<HotkeyProfile>> GetHotkeyProfilesAsync();
    Task<HotkeyValidationResult> ValidateHotkeyAsync(string combination);
    
    // Advanced operations
    Task RefreshDeviceListAsync();
    Task<bool> IsDeviceEnabledAsync(string deviceId);
    Task SetDeviceEnabledAsync(string deviceId, bool enabled);
}
```

### Configuration Classes

```csharp
public class AppSettings
{
    public AudioSettings Audio { get; set; }
    public TranscriptionSettings Transcription { get; set; }
    public HotkeySettings Hotkeys { get; set; }
    public UISettings UI { get; set; }
}

public class AudioSettings
{
    public int SampleRate { get; set; } = 16000;
    public int Channels { get; set; } = 1;
    public string InputDeviceId { get; set; } = "default";
    public string OutputDeviceId { get; set; } = "default";
    public Dictionary<string, DeviceSpecificSettings> DeviceSettings { get; set; }
}

public class TranscriptionSettings
{
    public string Provider { get; set; } = "OpenAI";
    public string Model { get; set; } = "whisper-1";
    public string ApiKey { get; set; }
    public bool AutoPunctuation { get; set; } = true;
    public int ConfidenceThreshold { get; set; } = 80;
    public int MaxDuration { get; set; } = 30;
}
```

### Events and Notifications

```csharp
public class SettingsChangedEventArgs : EventArgs
{
    public string Key { get; set; }
    public object? OldValue { get; set; }
    public object? NewValue { get; set; }
    public string Category { get; set; }
    public bool RequiresRestart { get; set; }
}

// Usage in application
settingsService.SettingsChanged += OnSettingsChanged;

private void OnSettingsChanged(object sender, SettingsChangedEventArgs e)
{
    if (e.RequiresRestart)
    {
        ShowRestartRequiredMessage(e.Category);
    }
    else
    {
        ApplySettingsChange(e.Key, e.NewValue);
    }
}
```

### Validation Framework

```csharp
public class SettingsValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; }
    public List<string> Warnings { get; set; }
    public List<string> Info { get; set; }
}

// Custom validation
public class CustomValidationRule : IValidationRule
{
    public SettingsValidationResult Validate(AppSettings settings)
    {
        var result = new SettingsValidationResult();
        
        if (/* custom condition */)
        {
            result.Errors.Add("Custom validation error");
            result.IsValid = false;
        }
        
        return result;
    }
}
```

### Best Practices

**Performance**:
- Use async methods for all settings operations
- Batch multiple changes before calling SaveAsync()
- Cache frequently accessed settings values
- Avoid property change notifications during bulk updates

**Security**:
- Never store API keys in plaintext
- Use encrypted storage for sensitive data
- Validate all user input before saving
- Implement proper access control for enterprise deployments

**Usability**:
- Provide clear validation error messages
- Offer sensible defaults for all settings
- Implement auto-save for user convenience
- Support undo/redo for complex configuration changes

---

*Settings Guide v2.1*  
*Last updated: January 26, 2026*  
*Compatible with WhisperKey v2.0 and later*