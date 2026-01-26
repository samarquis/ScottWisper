# ScottWisper - Professional Voice Dictation

A **privacy-focused professional voice dictation** application with offline-first architecture and enterprise compliance capabilities.

## Overview

ScottWisper enables secure, accurate voice-to-text conversion for professionals who require privacy, compliance, and offline capability. Unlike cloud-only solutions, ScottWisper works without internet connection and provides industry-specific accuracy for medical, legal, and enterprise environments.

**Core Features:**
- 🎯 **System-wide hotkey activation** - Works from any application
- ⚡ **Real-time transcription** - See text appear as you speak
- 🎤 **Industry-leading accuracy** - Local and cloud processing options
- 🏥 **Professional vocabulary** - Medical, legal, and technical terminology
- 🔒 **Privacy-first design** - Offline processing, no data collection
- 🏢 **Enterprise ready** - HIPAA/GDPR compliance and deployment options

## Installation

### Prerequisites

- **Windows 10 or Windows 11** (64-bit)
- **.NET 8.0 Runtime** (installed automatically or download from [Microsoft](https://dotnet.microsoft.com/download/dotnet/8.0))
- **Microphone** (built-in or external)
- **OpenAI API key** (required for speech recognition)

### Setup Instructions

1. **Download ScottWisper**
   ```
   Download the latest release from GitHub Releases
   Extract to a folder of your choice (e.g., C:\Program Files\ScottWisper)
   ```

2. **Configure OpenAI API Key**
   ```
   Open Windows Settings (Win+I)
   Go to System > About > Advanced system settings > Environment Variables
   Add new System Variable:
     Variable name: OPENAI_API_KEY
     Variable value: sk-your-api-key-here
   
   Alternatively, create a .env file in the ScottWisper folder:
     OPENAI_API_KEY=sk-your-api-key-here
   ```

3. **Run the Application**
   ```
   Double-click ScottWisper.exe
   The application will start in the background with a system tray icon
   ```

4. **Configure Microphone** (First time only)
   ```
   Right-click ScottWisper tray icon > Settings
   Select your preferred microphone device
   Test the microphone to ensure it's working
   ```

## Usage

### Basic Dictation

1. **Activate Dictation** - Press `Ctrl + Win + Shift + V` in any application
2. **See the Interface** - A semi-transparent window appears near your cursor
3. **Start Speaking** - The window shows recording status when active
4. **See Results** - Text appears in real-time as you speak
5. **Stop Dictation** - Press Escape or the hotkey again to finish

### Understanding the Interface

**Status Indicators:**
- 🟢 **Green dot** - Ready to start recording
- 🔴 **Red dot** - Currently recording your speech
- 🟡 **Yellow dot** - Processing speech-to-text conversion

**Information Display:**
- **Transcription text** - Your converted speech appears here
- **Usage counter** - Shows requests and cost for current session
- **Free tier indicator** - Visual feedback on remaining monthly usage

### Keyboard Shortcuts

- `Ctrl + Win + Shift + V` - Toggle dictation on/off
- `Escape` - Close transcription window and stop dictation
- `Ctrl + C` - Copy transcribed text to clipboard

## Monitoring and Costs

### Usage Tracking

ScottWisper includes comprehensive usage tracking to help you stay within free tier limits:

**What's tracked:**
- Number of speech recognition requests
- Total audio duration processed
- Estimated API costs based on usage

**Where it's stored:**
- Local file: `%APPDATA%\ScottWisper\usage.json`
- Automatic backup every minute

### Free Tier Management

**Free tier limits:**
- **$5.00 monthly credit** (OpenAI's standard free tier)
- **Warning at 80%** ($4.00) - You'll see a notification
- **Blocking at limit** - Dictation pauses until next month

**Cost breakdown:**
- **Whisper API**: $0.006 per minute of audio
- **Typical usage**: 2-3 hours daily ≈ $0.36-$0.54 per day
- **Free tier covers**: ~14-17 hours of dictation monthly

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
- **Microphone**: USB condenser microphone for best accuracy
- **Network**: Stable broadband with <100ms latency

## Troubleshooting

### Common Issues

**Hotkey not working:**
- Check if another application uses the same hotkey
- Try restarting ScottWisper
- Ensure Windows Focus Assist isn't blocking notifications

**Poor transcription accuracy:**
- Speak clearly and at moderate pace
- Reduce background noise
- Use a quality microphone
- Ensure good internet connection

**API key errors:**
- Verify your OpenAI API key is valid and active
- Check environment variable spelling (OPENAI_API_KEY)
- Ensure API key has billing enabled (even for free tier)

**Microphone issues:**
- Check Windows Sound settings to ensure microphone is enabled
- Test microphone with Windows Voice Recorder
- Try a different USB port or microphone

### Performance Optimization

**For best latency (<100ms):**
- Use wired internet connection
- Close unnecessary applications
- Ensure .NET runtime is up to date
- Restart ScottWisper if it's been running for days

**For best accuracy:**
- Use a quiet environment
- Position microphone 6-12 inches from mouth
- Speak naturally but clearly
- Avoid rapid speech patterns

## Privacy and Security

### Data Protection

**What we collect:**
- Audio snippets only during dictation (sent to OpenAI)
- Usage statistics stored locally on your device
- No personal information or content storage

**What we don't collect:**
- We don't store your transcriptions
- We don't access other applications or files
- We don't track what applications you use

### OpenAI API Security

- **Encryption**: All audio data is encrypted in transit
- **Privacy**: OpenAI doesn't store audio data permanently
- **Compliance**: Complies with GDPR and privacy regulations
- **Control**: You can delete your API key at any time

## Advanced Configuration

### Customization Options

**Hotkey customization:**
```
Edit ScottWisper.config.json (in %APPDATA%\ScottWisper\)
{
  "hotkey": "Ctrl+Win+Shift+V"
}
```

**API settings:**
```
{
  "api_endpoint": "https://api.openai.com/v1/audio/transcriptions",
  "model": "whisper-1",
  "language": "en"
}
```

### Development

**Building from source:**
```bash
git clone https://github.com/your-repo/ScottWisper.git
cd ScottWisper
dotnet build --configuration Release
dotnet publish --configuration Release --self-contained
```

**Dependencies:**
- .NET 8.0 WPF
- NAudio for audio capture
- OpenAI API client
- Windows API P/Invoke for hotkey handling

## Support and Feedback

### Getting Help

- **Documentation**: Check this README and built-in help
- **Issues**: Report bugs on [GitHub Issues](https://github.com/your-repo/ScottWisper/issues)
- **Community**: Join discussions on [GitHub Discussions](https://github.com/your-repo/ScottWisper/discussions)

### Contributing

We welcome contributions! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

### License

ScottWisper is released under the MIT License. See [LICENSE](LICENSE) for details.

## Version History

### v1.0.0 (Current)
- Initial release with core functionality
- Global hotkey activation
- Real-time transcription display
- Cost tracking and free tier management
- Comprehensive validation and testing

### Future Updates
- [ ] Text injection into active applications
- [ ] Voice commands for punctuation
- [ ] Offline speech recognition options
- [ ] Multiple language support
- [ ] Advanced audio processing

---

**Thank you for using ScottWisper!** 🎤

If you find ScottWisper helpful, please consider:
- ⭐ Starring the project on GitHub
- 🐛 Reporting any issues you encounter
- 💡 Suggesting features you'd like to see

For more information, visit [project website](https://github.com/your-repo/ScottWisper) or contact support@scottwisper.com.