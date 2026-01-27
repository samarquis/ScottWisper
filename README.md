# ScottWisper - Professional Voice Dictation

A **privacy-focused professional voice dictation** application with universal text injection and system-level integration for seamless Windows workflow automation.

## Overview

ScottWisper enables secure, accurate voice-to-text conversion that works across **all Windows applications**. Unlike traditional dictation tools, ScottWisper integrates at the system level to provide universal text injection, professional system tray management, and comprehensive audio/visual feedback.

**Core Features:**
- 🎯 **System-wide hotkey activation** - Works from any application
- ⚡ **Real-time transcription** - See text appear as you speak
- 🎤 **Universal text injection** - Works in browsers, IDEs, Office, terminals
- 🏢 **Professional system tray** - Background operation with minimal resources
- 📊 **Comprehensive feedback** - Audio/visual status indicators
- 🔧 **Advanced settings** - Device management, hotkey profiles, customization
- 🎛️ **Audio visualization** - Real-time waveform and level monitoring
- ⌨️ **Smart hotkey management** - Multiple profiles with conflict detection
- 🎚️ **Professional audio control** - Device testing and selection
- 🔒 **Privacy-first design** - Local settings with secure persistence

## Installation

### Prerequisites

- **Windows 10 or Windows 11** (64-bit)
- **.NET 8.0 Runtime** (installed automatically or download from [Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0))
- **Microphone** (built-in or external)
- **OpenAI API key** (required for speech recognition)

### Setup Instructions

1. **Download ScottWisper**
   ```
   Download latest release from GitHub Releases
   Extract to a folder of your choice (e.g., C:\Program Files\ScottWisper)
   ```

2. **Configure OpenAI API Key**
   ```
   Right-click ScottWisper tray icon > Settings > Transcription
   Enter your OpenAI API key in the API Key field
   Click "Test API Key" to verify connection
   ```

3. **Run Application**
   ```
   Double-click ScottWisper.exe
   The application will start in background with a professional system tray icon
   ```

4. **Configure Audio Device** (First time only)
   ```
   Right-click ScottWisper tray icon > Settings > Audio
   Select your preferred microphone from the dropdown
   Click "Test Microphone" to ensure it's working
   Adjust input volume if needed
   ```

## Usage

### Basic Dictation

1. **Activate Dictation** - Press `Ctrl + Win + Shift + V` in any application
2. **See Interface** - Professional status window appears with real-time feedback
3. **Start Speaking** - Visual and audio indicators show recording status
4. **See Results** - Text appears automatically at cursor position in real-time
5. **Stop Dictation** - Press hotkey again or use the stop button

### Windows Integration

**Universal Text Injection:**
- **Browsers** - Works in Chrome, Firefox, Edge, Safari
- **IDEs** - Visual Studio, VS Code, JetBrains IDEs, Notepad++
- **Office** - Word, Excel, PowerPoint, Outlook
- **Terminals** - Command Prompt, PowerShell, Windows Terminal
- **Professional Software** - EHR systems, case management, CRMs

**Text Placement:**
- Text appears at exact cursor position
- Maintains text formatting and styles
- Works with existing keyboard input methods
- Compatible with complex applications

### Understanding Interface

**Status Indicators:**
- 🟢 **Green** - Ready to start recording
- 🔴 **Red** - Currently recording your speech
- 🟡 **Yellow** - Processing speech-to-text conversion
- 🔵 **Blue** - Text injection in progress

**Visual Feedback:**
- **Real-time waveform** - Audio level visualization during recording
- **Progress indicators** - Transcription and injection status
- **Usage tracking** - Live cost and usage statistics
- **Free tier monitor** - Visual warning when approaching limits

**Audio Feedback:**
- **Start sound** - Confirmation when recording begins
- **Stop sound** - Indication when recording ends
- **Success sound** - Text injection completed
- **Error sounds** - Issues with recording or API

### System Tray Integration

**System Tray Features:**
- **Professional icon** - Clear status indication at all times
- **Context menu** - Quick access to common functions
- **Settings access** - Direct link to configuration interface
- **Status monitoring** - Real-time application status
- **Resource optimization** - Minimal CPU and memory usage

**Context Menu Options:**
- **Toggle Recording** - Start/stop dictation
- **Show Status** - Display current transcription window
- **Settings** - Open configuration interface
- **About** - Application information and version
- **Exit** - Graceful application shutdown

### Keyboard Shortcuts and Hotkeys

**Default Hotkeys:**
- `Ctrl + Win + Shift + V` - Toggle dictation on/off
- `Escape` - Stop recording and close window
- `Ctrl + C` - Copy transcribed text to clipboard

**Hotkey Management:**
- **Multiple profiles** - Create different hotkey sets
- **Conflict detection** - Automatic detection of hotkey conflicts
- **Custom combinations** - Support for Ctrl, Alt, Shift, Win modifiers
- **Import/Export** - Backup and share hotkey profiles

## Advanced Configuration

### Settings Management

**Audio Settings:**
- **Device selection** - Choose from available microphones
- **Input volume** - Adjustable microphone gain
- **Sample rate** - 16kHz optimized for speech recognition
- **Channel configuration** - Mono for best accuracy
- **Real-time testing** - Test audio before recording

**Transcription Settings:**
- **API configuration** - OpenAI endpoint and model selection
- **Language settings** - Auto-detect or specify language
- **Quality settings** - Balance between speed and accuracy
- **Custom vocabulary** - Add industry-specific terms
- **Confidence threshold** - Minimum confidence for acceptance

**UI Settings:**
- **Visual feedback** - Customize appearance and animations
- **Window behavior** - Positioning and auto-hide options
- **Theme selection** - Light/dark mode support
- **Accessibility** - High contrast and screen reader support
- **Notification preferences** - Toast and system notifications

**System Integration:**
- **Startup behavior** - Launch with Windows option
- **Resource limits** - CPU and memory usage controls
- **Background operation** - Minimize to system tray
- **Priority settings** - Process priority for performance

### Audio Device Management

**Device Selection:**
- **Automatic detection** - Find all available audio devices
- **Device testing** - Real-time audio level monitoring
- **Fallback handling** - Automatic switch to default device
- **Device profiles** - Save settings for different hardware

**Professional Features:**
- **Level monitoring** - Visual audio level indicators
- **Quality analysis** - Sample rate and bit depth verification
- **Latency measurement** - Real-time latency tracking
- **Device switching** - Change devices without restart

## Monitoring and Costs

### Usage Tracking

ScottWisper includes comprehensive usage tracking to help you stay within free tier limits:

**Real-time Monitoring:**
- **Request counter** - Live transcription request count
- **Duration tracking** - Total audio time processed
- **Cost calculation** - Real-time cost estimation
- **Free tier usage** - Visual progress indicator

**Historical Data:**
- **Session history** - Track usage over time
- **Daily/weekly reports** - Usage patterns and trends
- **Cost projections** - Predict monthly usage
- **Export capabilities** - CSV export for analysis

### Free Tier Management

**Free tier limits:**
- **$5.00 monthly credit** (OpenAI's standard free tier)
- **Warning at 80%** ($4.00) - Visual and audio notifications
- **Blocking at limit** - Pause dictation until next month
- **Reset tracking** - Automatic reset on monthly cycle

**Cost breakdown:**
- **Whisper API**: $0.006 per minute of audio
- **Typical usage**: 2-3 hours daily ≈ $0.36-$0.54 per day
- **Free tier covers**: ~14-17 hours of dictation monthly
- **Professional usage**: 6-8 hours daily ≈ $1.08-$1.44 per day

## System Requirements

### Minimum Requirements

- **OS**: Windows 10 (version 1903) or Windows 11
- **RAM**: 4GB (8GB recommended for smooth operation)
- **Storage**: 100MB free space
- **Internet**: Broadband connection for API calls
- **Microphone**: Any standard microphone (built-in or USB)

### Recommended Setup

- **OS**: Windows 11 with latest updates
- **RAM**: 8GB or more
- **Processor**: Multi-core CPU for better performance
- **Microphone**: USB condenser microphone for best accuracy
- **Network**: Stable broadband with <100ms latency
- **Display**: 1920x1080 or higher for best UI experience

## Troubleshooting

### Common Issues

**Text injection not working:**
- Check if target application has focus
- Try alternative injection methods in settings
- Ensure Windows accessibility features are enabled
- Restart application with administrator privileges

**Hotkey not working:**
- Check for hotkey conflicts in settings
- Try different hotkey combinations
- Ensure Windows Focus Assist isn't blocking
- Restart ScottWisper to re-register hotkeys

**Poor transcription accuracy:**
- Check microphone quality and positioning
- Reduce background noise
- Ensure good internet connection
- Try different audio device settings

**System tray icon missing:**
- Check Windows notification settings
- Ensure ScottWisper is running in Task Manager
- Restart Windows Explorer
- Check for application crashes in Event Viewer

**Performance issues:**
- Close unnecessary applications
- Check CPU and memory usage in settings
- Update .NET runtime to latest version
- Try reducing audio quality settings

### Advanced Troubleshooting

**Audio Device Issues:**
- Use Windows Sound settings to test microphone
- Update audio drivers
- Try different USB ports
- Disable audio enhancements in Windows

**Network Connectivity:**
- Test internet connection with other services
- Check firewall settings for API access
- Try different network connection
- Verify API key validity and billing status

**Application Crashes:**
- Check Windows Event Viewer for error details
- Send crash reports with detailed information
- Try reinstalling the application
- Check for conflicting software

## Privacy and Security

### Data Protection

**What we collect:**
- Audio snippets only during dictation (sent to OpenAI)
- Usage statistics stored locally on your device
- Settings and preferences stored locally
- No personal information or content storage

**What we don't collect:**
- We don't store your transcriptions
- We don't access other applications or files
- We don't track what applications you use
- We don't collect personal data or telemetry

### OpenAI API Security

- **Encryption**: All audio data is encrypted in transit
- **Privacy**: OpenAI doesn't store audio data permanently
- **Compliance**: Complies with GDPR and privacy regulations
- **Control**: You can delete your API key at any time
- **Auditing**: Detailed logs of API usage and costs

### Local Security

- **Settings encryption**: Sensitive data encrypted at rest
- **Secure storage**: Uses Windows secure storage mechanisms
- **Access control**: Application respects Windows user permissions
- **Memory protection**: Sensitive data cleared from memory when not in use

## Development and Architecture

### Technical Architecture

**Three-Layer Design:**
- **Presentation Layer** - WPF UI with modern controls
- **Application Layer** - Business logic and service coordination
- **Integration Layer** - Windows API integration and external services

**Key Technologies:**
- **.NET 8.0 WPF** - Modern Windows desktop framework
- **NAudio** - Professional audio processing and device management
- **Windows API** - System-level integration and hotkey handling
- **OpenAI Whisper API** - Industry-leading speech recognition
- **Dependency Injection** - Modular, testable architecture

### Building from Source

**Prerequisites:**
- Visual Studio 2022 or Visual Studio Code
- .NET 8.0 SDK
- Git for source control

**Build Instructions:**
```bash
git clone https://github.com/your-repo/ScottWisper.git
cd ScottWisper
dotnet restore
dotnet build --configuration Release
dotnet publish --configuration Release --self-contained
```

**Dependencies:**
- .NET 8.0 WPF
- NAudio for audio capture and processing
- OpenAI API client for speech recognition
- Windows API P/Invoke for system integration
- Microsoft.Extensions.DependencyInjection for DI

## Support and Feedback

### Getting Help

- **Documentation**: Check this README and built-in help
- **Settings Help**: Click help buttons in settings interface
- **Issues**: Report bugs on [GitHub Issues](https://github.com/your-repo/ScottWisper/issues)
- **Community**: Join discussions on [GitHub Discussions](https://github.com/your-repo/ScottWisper/discussions)
- **Email Support**: support@scottwisper.com

### Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

**Areas for contribution:**
- Bug fixes and performance improvements
- Additional language support
- New audio device support
- User interface enhancements
- Documentation improvements

### License

ScottWisper is released under MIT License. See [LICENSE](LICENSE) for details.

## Version History

### v2.0.0 - Windows Integration Release (Current)

**Major New Features:**
- ✨ **Universal text injection** - Works across all Windows applications
- 🖥️ **Professional system tray** - Background operation with minimal resources
- 📊 **Comprehensive feedback** - Audio/visual status indicators
- 🎛️ **Advanced settings** - Device management and hotkey profiles
- 🎚️ **Audio visualization** - Real-time waveform monitoring
- ⌨️ **Smart hotkey management** - Multiple profiles with conflict detection

**Improved Features:**
- 🔧 **Enhanced settings interface** - Professional tabbed design
- 🎤 **Professional audio control** - Device testing and selection
- 📈 **Better performance** - Optimized resource usage
- 🛡️ **Improved security** - Enhanced local data protection
- 🎨 **Better UI/UX** - Modern interface with accessibility support

**Technical Improvements:**
- 🏗️ **Refactored architecture** - Three-layer design for maintainability
- 🧪 **Comprehensive testing** - Integration tests for all components
- 📝 **Complete documentation** - User guides and developer docs
- 🔍 **Better debugging** - Enhanced logging and error reporting

### v1.0.0 - Core Technology Release

**Initial Features:**
- Global hotkey activation
- Real-time transcription display
- OpenAI Whisper API integration
- Cost tracking and free tier management
- Basic settings configuration
- Comprehensive validation and testing

### Future Updates

- [ ] Voice commands for punctuation and editing
- [ ] Local/offline speech recognition options
- [ ] Multiple language support
- [ ] Industry-specific vocabulary packs
- [ ] Advanced audio processing
- [ ] Enterprise deployment options
- [ ] Compliance reporting (HIPAA/GDPR)

---

**Thank you for using ScottWisper!** 🎤

If you find ScottWisper helpful, please consider:
- ⭐ Starring the project on GitHub
- 🐛 Reporting any issues you encounter
- 💡 Suggesting features you'd like to see
- 📝 Contributing to documentation or code

For more information, visit [project website](https://github.com/your-repo/ScottWisper) or contact support@scottwisper.com.

**Professional dictation, seamlessly integrated into your Windows workflow.** ✨