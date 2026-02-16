# Technology Stack

**Analysis Date:** 2026-02-15

## Languages

**Primary:**
- C# 12 - All application logic, WPF UI, services, and business logic
- XAML - WPF UI definitions (Windows, controls, resources)

**Secondary:**
- PowerShell - Build scripts and automation (`build-msi.ps1`, `Run-FullReview.ps1`)
- WiX XML - MSI installer definitions (`Files.wxs`, `ScottWisperSetup.wxs`)

## Runtime

**Environment:**
- .NET 8.0 (net8.0-windows)
- Windows-specific build (WinExe output type)
- WPF (Windows Presentation Foundation) application
- RollForward: LatestMinor enabled

**Package Manager:**
- NuGet (PackageReference format)
- Lockfile: Not present (relies on explicit version pins)

## Frameworks

**Core:**
- **WPF** (.NET 8.0) - Desktop UI framework
- **Microsoft.Extensions.DependencyInjection** 8.0.0 - IoC container
- **Microsoft.Extensions.Configuration** 8.0.0 - Configuration management
- **Microsoft.Extensions.Logging** 8.0.0 - Logging abstractions
- **Microsoft.Extensions.Http** 8.0.0 - HTTP client factory

**Audio Processing:**
- **NAudio** 2.2.1 - Audio capture, playback, format conversion
- **Whisper.net** 1.8.1 - Local Whisper model inference
- **Whisper.net.Runtime** 1.8.1 - Native runtime for Whisper

**Transcription:**
- **OpenAI Whisper API** - Cloud transcription service (via HTTP)
- **Vosk** (optional) - Alternative local provider

**Testing:**
- **MSTest** 3.1.1 - Unit testing framework
- **Moq** 4.20.69 - Mocking library
- **coverlet** 6.0.0 - Code coverage
- **BenchmarkDotNet** 0.13.12 - Performance benchmarking

**Resilience:**
- **Polly** 8.6.5 - Retry policies, circuit breakers, resilience patterns

**Logging:**
- **Serilog** 4.2.0 - Structured logging
- **Serilog.Sinks.Console** 6.0.0 - Console output
- **Serilog.Sinks.File** 6.0.0 - File logging
- **Serilog.Sinks.Seq** 8.0.0 - Seq log aggregation (optional, localhost:5341)
- **Serilog.Enrichers.CorrelationId** 3.0.1 - Request correlation
- **Serilog.Enrichers.Environment** 2.3.0 - Environment enrichment
- **Serilog.Enrichers.Process** 2.0.2 - Process info enrichment
- **Serilog.Extensions.Hosting** 8.0.0 - Host integration

**UI/UX:**
- **H.NotifyIcon** 2.4.1 - System tray icon
- **H.NotifyIcon.Wpf** 2.4.1 - WPF integration for tray icon
- **H.InputSimulator** 1.4.0 - Text injection simulation

**Data/Serialization:**
- **Newtonsoft.Json** 13.0.3 - JSON serialization

**System Integration:**
- **System.Management** 8.0.0 - Windows Management Instrumentation
- **System.ServiceProcess.ServiceController** 10.0.2 - Service control
- **System.IO.FileSystem.AccessControl** 5.0.0 - File ACLs
- **System.IO.Abstractions** 21.0.2 - File system abstraction for testing
- **System.Security.Cryptography.ProtectedData** 8.0.0 - DPAPI encryption

## Key Dependencies

**Critical for Core Functionality:**
- `Whisper.net` + `Whisper.net.Runtime` - Local AI inference engine
- `NAudio` - Audio subsystem foundation
- `Microsoft.Extensions.*` - Dependency injection and configuration
- `Polly` - API resilience and fault tolerance
- `H.InputSimulator` - Text injection into other applications

**Infrastructure:**
- `Serilog` ecosystem - Observability and diagnostics
- `Newtonsoft.Json` - Data exchange format

## Configuration

**Environment:**
- Configuration via JSON files:
  - `appsettings.json` - Base configuration (bundled with app)
  - `%APPDATA%/WhisperKey/usersettings.json` - User-specific overrides
- Environment variables supported via `AddEnvironmentVariables()`
- No `.env` files (Windows Credential Manager used for secrets)

**Build:**
- `Directory.Build.props` - MSBuild properties (versioning)
- `version.props` - Semantic version components
- `app.manifest` - Windows application manifest
- `coverlet.runsettings` - Code coverage configuration

**Key Configuration Sections:**
```json
{
  "Audio": { "InputDeviceId", "SampleRate", "Channels" },
  "Transcription": { "Provider", "Model", "ApiKey", "ApiEndpoint" },
  "Hotkeys": { "ToggleRecording", "ShowSettings" },
  "UI": { "ShowVisualFeedback", "MinimizeToTray" }
}
```

## Platform Requirements

**Development:**
- Windows 10/11
- .NET 8.0 SDK
- Visual Studio 2022 or VS Code with C# Dev Kit
- WiX Toolset (for MSI builds)

**Production:**
- Windows 10 version 1809 or later
- .NET 8.0 Runtime (bundled in MSI)
- Microphone access permissions
- ~500MB RAM minimum (varies by model)
- Internet connection (optional - supports offline mode)

**Deployment:**
- MSI installer generated via WiX
- Single-file deployment support
- Self-contained capable
- Code signing recommended for production

---

*Stack analysis: 2026-02-15*
