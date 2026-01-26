# Stack Research

**Domain:** Windows Voice Dictation Applications  
**Researched:** January 26, 2026  
**Confidence:** HIGH

## Recommended Stack

### Core Technologies

| Technology | Version | Purpose | Why Recommended |
|------------|---------|---------|-----------------|
| **WinUI 3** | 1.8.250907003 | Modern Windows UI framework | Microsoft's future-proof framework with native Windows integration, supports latest Windows 11 features like Mica and Fluent Design |
| **Windows App SDK** | 1.8.250907003 | Core Windows APIs | Provides access to modern Windows features including AI APIs, notifications, and system integration |
| **.NET 8** | 8.0.11 | Runtime environment | Latest LTS release with excellent performance, native AOT compilation, and cross-platform support |
| **OpenAI Whisper API** | gpt-4o-transcribe | Speech-to-text engine | Highest accuracy transcription, supports real-time streaming, handles technical vocabulary well |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| **Windows.Media.SpeechRecognition** | Built-in | Local speech recognition | For offline fallback scenarios when internet unavailable |
| **NAudio** | 2.2.1 | Audio capture and processing | When implementing custom audio pipelines or local processing |
| **WindowsInput** | 1.0.4 | Global hotkey handling | For system-wide hotkey registration outside app focus |
| **Hardcodet.NotifyIcon.Wpf** | 1.1.0 | System tray integration | For background operation with tray icon (WinUI 3 compatible) |
| **OpenAI SDK** | 2.1.0 | Whisper API integration | Official OpenAI client with streaming support |

### Development Tools

| Tool | Purpose | Notes |
|------|---------|-------|
| **Visual Studio 2022** | Primary IDE | Use 17.11+ for best WinUI 3 support |
| **Windows App SDK C# Templates** | Project scaffolding | Provides modern project structure |
| **Windows AI Dev Gallery** | API testing | Test Windows AI features before integration |

## Installation

```bash
# Core
dotnet add package Microsoft.WindowsAppSDK
dotnet add package Microsoft.Windows.SDK.BuildTools

# Supporting
dotnet add package NAudio
dotnet add package WindowsInput
dotnet add package Hardcodet.NotifyIcon.Wpf
dotnet add package OpenAI

# Dev dependencies
dotnet add package Microsoft.Windows.CsWinRT
```

## Alternatives Considered

| Recommended | Alternative | When to Use Alternative |
|-------------|-------------|-------------------------|
| **WinUI 3 + .NET 8** | WPF + .NET Framework 4.8 | Legacy systems requiring extensive existing WPF codebases |
| **OpenAI Whisper API** | Azure Speech Services | Enterprise environments with Azure commitments and compliance requirements |
| **Online API** | Local Vosk processing | Air-gapped environments requiring 100% offline operation |
| **Windows App SDK** | Electron + Tauri | Teams with only web development skills needing rapid prototyping |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| **System.Speech** | Deprecated, poor accuracy compared to modern engines | Windows.Media.SpeechRecognition or cloud APIs |
| **WinForms** | Legacy framework, no modern Windows integration | WinUI 3 or WPF |
| **.NET Framework 4.8** | End-of-life approaching, missing modern features | .NET 8 with Windows App SDK |
| **Local Whisper models** | Large model sizes (1GB+), complex setup, requires GPU for good performance | OpenAI Whisper API or Vosk for lightweight offline use |
| **Electron** | High memory usage, poor Windows integration, large app size | WinUI 3 for native experience or Tauri for cross-platform |

## Stack Patterns by Variant

**If cloud connectivity is reliable:**
- Use OpenAI Whisper API as primary engine
- Windows.Media.SpeechRecognition as offline fallback
- Implement streaming for real-time feedback

**If 100% offline is required:**
- Use Vosk API with local models
- NAudio for audio capture
- Consider smaller models for better performance

**If cross-platform is needed:**
- Use Tauri with Rust backend
- Whisper bindings (whisper-rs) for local processing
- Web-based UI for rapid development

**If enterprise compliance required:**
- Use Azure Speech Services
- On-prem deployment options
- Integration with Azure Active Directory

## Version Compatibility

| Package A | Compatible With | Notes |
|-----------|-----------------|-------|
| Windows App SDK 1.8.250907003 | .NET 6+ | Best performance with .NET 8+ |
| NAudio 2.2.1 | .NET Standard 2.0 | Works across all .NET versions |
| WindowsInput 1.0.4 | .NET Framework 4.6.1+ | P/Invoke based, works everywhere |
| OpenAI SDK 2.1.0 | .NET Standard 2.0+ | Modern async/await patterns |

## Sources

- [Windows AI APIs with WinUI sample app] — Microsoft Learn — HIGH confidence
- [OpenAI Whisper API documentation] — Official OpenAI docs — HIGH confidence  
- [Windows App SDK 1.8.250907003 release notes] — Microsoft Learn — HIGH confidence
- [Don't Start a New C# Desktop App Until You Read This: WPF vs WinUI 3 in 2025] — Medium — MEDIUM confidence
- [Vosk Speech Recognition: The Ultimate 2025 Guide] — VideoSDK — MEDIUM confidence
- [Real-Time Speech to Text in C# – Convert Mic Input to Text!] — YouTube tutorial — LOW confidence

---
*Stack research for: Windows Voice Dictation Applications*  
*Researched: January 26, 2026*