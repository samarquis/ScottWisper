# Phase 02: Windows Integration & User Experience - Research

## Overview

This document outlines the research findings for implementing Phase 02 of the WPF voice dictation application, focusing on system-level integration, universal text injection, and enhanced user experience.

## Key Implementation Areas

### 1. Text Injection at Cursor Position

#### Research Findings

**Input Injection APIs:**
- Windows UI Input.Preview.Injection APIs provide the most robust solution for universal text injection
- Supports simulating keyboard input across all Windows applications
- Can target applications running with Administrator privileges
- Requires Windows 10/11 UWP APIs but accessible from WPF via WinRT interop

**Alternative Solutions:**
- **InputSimulator Libraries:**
  - `H.InputSimulator` (NuGet) - Modern, actively maintained
  - `Windows Input Simulator` by michaelnoonan - Classic, well-established
  - Both support SendInput wrapper for global keyboard/mouse simulation
  - Limitations: May not work in all applications due to security restrictions

**Recommended Approach:**
- Primary: Windows UI Input.Preview.Injection APIs for maximum compatibility
- Fallback: H.InputSimulator for broader Windows version support
- Implementation should include clipboard-based fallback for problematic applications

### 2. System Tray Background Process

#### Research Findings

**WPF NotifyIcon Solutions:**
- **H.NotifyIcon.Wpf** (NuGet v2.4.1) - Recommended
  - .NET 8 compatible
  - Modern implementation with full WPF integration
  - Supports balloons, context menus, and custom tooltips
  - MIT License, actively maintained

- **hardcodet/wpf-notifyicon** (GitHub) - Classic option
  - 958 stars, widely used
  - Stable but less actively maintained
  - Good fallback option

**Background Process Architecture:**
- WPF application with `ShowInTaskbar="false"` and `WindowStyle="None"`
- NotifyIcon as primary user interface
- Minimize to tray on startup
- Global hotkeys for activation (see section 5)

### 3. Audio/Visual Feedback

#### Research Findings

**Visual Feedback:**
- **Real-time Audio Visualization:**
  - Syncfusion WPF Charts for audio visualization
  - Custom WPF animations using `MediaElement` and `Storyboard`
  - Waveform visualization during recording
  - Progress indicators for transcription status

**Audio Feedback:**
- **Windows.Media.SpeechSynthesizer** for audio confirmation
- Custom sound files for start/stop/complete events
- Volume control and device selection integration

**Status Indicators:**
- System tray icon states (idle, recording, processing, error)
- Toast notifications for status updates
- Optional overlay window for visual feedback

### 4. Settings Management

#### Research Findings

**Configuration Architecture:**
- **.NET 8 Configuration System:**
  - `appsettings.json` for user preferences
  - Options pattern for strongly-typed settings
  - Environment-specific configuration support
  - JSON serialization for persistence

**Key Settings Categories:**
```json
{
  "Audio": {
    "InputDeviceId": "default",
    "OutputDeviceId": "default",
    "SampleRate": 16000,
    "Channels": 1
  },
  "Transcription": {
    "Provider": "OpenAI",
    "Model": "whisper-1",
    "Language": "auto",
    "ApiKey": ""
  },
  "Hotkeys": {
    "ToggleRecording": "Ctrl+Alt+V",
    "ShowSettings": "Ctrl+Alt+S"
  },
  "UI": {
    "ShowVisualFeedback": true,
    "MinimizeToTray": true,
    "StartWithWindows": false
  }
}
```

**Persistence Strategy:**
- User-specific settings in `%APPDATA%/VoiceDictation/`
- Machine-specific settings in registry fallback
- Settings window with real-time preview

### 5. Global Hotkeys

#### Research Findings

**Global Hotkey Registration:**
- **Windows API Approach:**
  - `RegisterHotKey` and `UnregisterHotKey` Win32 APIs
  - `WM_HOTKEY` message handling
  - Requires P/Invoke declarations

- **Library Solutions:**
  - `GlobalHotKeys` (GitHub) - Lightweight, MIT License
  - `Hotkeys` by mrousavy - More feature-rich
  - Custom P/Invoke implementation for full control

**Implementation Requirements:**
- Hotkey registration on application startup
- Unregistration on application shutdown
- Conflict detection and resolution
- Support for modifier keys (Ctrl, Alt, Shift, Win)

### 6. Audio Device Selection

#### Research Findings

**NAudio Integration:**
- **NAudio v2.2** - Recommended audio library
  - .NET 8 compatible
  - Comprehensive device enumeration
  - WASAPI, DirectSound, and ASIO support
  - MIT License, actively maintained

**Device Enumeration:**
```csharp
// Input devices
var enumerator = new MMDeviceEnumerator();
var inputDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

// Output devices  
var outputDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
```

**Device Selection Strategy:**
- Store device IDs for persistence
- Fallback to default device if selected device unavailable
- Real-time device change detection
- Audio device testing capabilities

## Implementation Architecture

### Recommended Technology Stack

1. **Core Framework:** WPF + .NET 8
2. **Input Injection:** Windows UI Input.Preview.Injection + H.InputSimulator fallback
3. **System Tray:** H.NotifyIcon.Wpf
4. **Audio Processing:** NAudio
5. **Configuration:** .NET 8 Configuration + Options pattern
6. **Global Hotkeys:** Custom P/Invoke implementation
7. **Dependency Injection:** Microsoft.Extensions.DependencyInjection

### Application Structure

```
VoiceDictation/
├── Core/
│   ├── Audio/           # NAudio integration
│   ├── Input/           # Text injection
│   ├── Settings/        # Configuration management
│   └── Hotkeys/         # Global hotkey handling
├── UI/
│   ├── MainWindow.xaml
│   ├── SettingsWindow.xaml
│   └── NotifyIcon/      # System tray integration
└── Services/
    ├── TranscriptionService.cs
    ├── AudioService.cs
    └── SettingsService.cs
```

## Security Considerations

1. **Input Injection:**
   - Requires user consent for accessibility features
   - UAC elevation may be needed for some applications
   - Implement proper security checks

2. **Audio Recording:**
   - Microphone permission requests
   - Visual indicator when recording
   - Secure storage of API keys

3. **Settings Persistence:**
   - Encrypt sensitive data (API keys)
   - User data protection compliance
   - Secure configuration file locations

## Development Timeline Estimate

1. **Week 1-2:** Core infrastructure (audio, settings, basic UI)
2. **Week 3-4:** Text injection implementation and testing
3. **Week 5:** System tray and background process
4. **Week 6:** Global hotkeys and audio device selection
5. **Week 7-8:** Visual feedback, testing, and refinement

## Next Steps

1. Create detailed technical specifications for each component
2. Set up development environment with recommended NuGet packages
3. Implement proof-of-concept for text injection across different applications
4. Design user interface mockups for settings and feedback systems
5. Establish testing strategy for various Windows versions and applications

## References

- Windows Input Injection API Documentation
- H.NotifyIcon.Wpf NuGet Package
- NAudio Documentation and Examples
- .NET 8 Configuration Best Practices
- Windows Global Hotkey Implementation Guidelines