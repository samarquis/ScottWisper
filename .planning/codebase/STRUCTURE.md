# Codebase Structure

**Analysis Date:** 2026-02-15

## Directory Layout

```
[project-root]/
├── .planning/               # GSD planning documents
│   ├── codebase/           # Architecture and structure docs
│   ├── phases/             # Implementation phase plans
│   ├── research/           # Research documents
│   └── milestones/         # Release milestones
├── .github/workflows/      # CI/CD pipelines
├── src/                    # Source code (organized by layer)
│   ├── Bootstrap/         # Application bootstrap and DI
│   ├── Exceptions/        # Custom exception hierarchy
│   ├── Infrastructure/    # Smoke testing infrastructure
│   ├── Integration/       # External integrations (empty)
│   ├── Interfaces/        # Key interfaces
│   ├── Models/            # Domain models
│   ├── Repositories/      # Data persistence
│   ├── Services/          # Business logic (99 files)
│   ├── UI/                # Reusable UI components
│   ├── Validation/        # Cross-application validation
│   └── ViewModels/        # MVVM view models
├── Configuration/         # Settings classes
├── Converters/            # WPF value converters
├── Resources/             # Icons and images
├── Tests/                 # Unit tests
│   └── Unit/              # 50+ test classes
├── Benchmarks/            # Performance benchmarks
├── Installer/             # WiX installer project
├── Assets/                # Documentation assets
├── docs/                  # Documentation
│   └── adr/               # Architecture Decision Records
├── do-work/               # Issue tracking
│   ├── user-requests/     # Active user requests
│   └── archive/           # Completed work
├── *.xaml                 # Main window definitions (root level)
├── *.xaml.cs              # Window code-behind
├── *.cs                   # Standalone service files
├── WhisperKey.csproj      # Main project file
├── appsettings.json       # Application configuration
└── version.props          # Version tracking
```

## Directory Purposes

**Root Directory:**
- Purpose: Main application entry points and primary windows
- Contains: `App.xaml/cs`, `MainWindow.xaml/cs`, `SettingsWindow.xaml/cs`, etc.
- Key files: `App.xaml.cs` (application entry), `WhisperKey.csproj` (project config)
- Note: Legacy structure - services migrated to `src/` but XAML files remain at root

**`src/Bootstrap/`:**
- Purpose: Application initialization and service configuration
- Contains: 5 files - `ServiceConfiguration.cs`, `ApplicationBootstrapper.cs`, `EventCoordinator.cs`, `DictationManager.cs`, `StartupPerformanceService.cs`
- Key file: `ServiceConfiguration.cs` (DI container setup)

**`src/Services/`:**
- Purpose: All business logic services
- Contains: 99 files (50+ services with interfaces)
- Organization: Flat structure - interfaces alongside implementations
- Subdirectories: `Database/`, `Memory/`, `Recovery/`, `Validation/`
- Largest files: `AudioDeviceService.cs` (2941 lines), `SettingsService.cs` (1576 lines)

**`src/Models/`:**
- Purpose: Data transfer objects and domain models
- Contains: 15 model classes
- Examples: `AuditLog.cs`, `BusinessMetrics.cs`, `VocabularyContext.cs`

**`src/ViewModels/`:**
- Purpose: MVVM pattern implementation
- Contains: 3 view models - `MainViewModel.cs`, `SettingsViewModel.cs`, `TranscriptionViewModel.cs`

**`src/UI/`:**
- Purpose: Reusable UI components and dialogs
- Contains: `FirstTimeSetupWizard`, `PermissionDialog`, `WindowFactory`

**`src/Repositories/`:**
- Purpose: Data persistence abstraction
- Contains: `ISettingsRepository.cs`, `FileSettingsRepository.cs`

**`src/Exceptions/`:**
- Purpose: Custom exception hierarchy
- Contains: 8 exception classes (`AudioCaptureException`, `TranscriptionException`, etc.)

**`Configuration/`:**
- Purpose: Settings class definitions
- Contains: `AppSettings.cs` (comprehensive settings hierarchy)

**`Tests/Unit/`:**
- Purpose: Unit test coverage
- Contains: 50+ test classes matching service structure
- Organization: Mirrors `src/Services/` naming

**`Benchmarks/`:**
- Purpose: Performance benchmarking
- Contains: `Program.cs`, `SecurityBenchmarks.cs`, `AudioBenchmarks.cs`

**`.planning/`:**
- Purpose: GSD methodology artifacts
- Contains: Phase plans, research, architecture docs
- Generated: No (maintained manually)
- Committed: Yes

## Key File Locations

**Entry Points:**
- `App.xaml` / `App.xaml.cs`: Application entry point
- `WhisperKey.csproj`: Project configuration (.NET 8 WPF)
- `appsettings.json`: Static application configuration

**Configuration:**
- `Configuration/AppSettings.cs`: All settings classes
- `src/Repositories/FileSettingsRepository.cs`: Settings persistence
- `%APPDATA%/WhisperKey/usersettings.json`: User settings storage

**Core Logic:**
- `src/Services/WhisperService.cs`: Transcription engine
- `src/Services/AudioCaptureService.cs`: Audio recording
- `src/Services/TextInjectionService.cs`: Text input simulation
- `src/Services/HotkeyService.cs`: Global hotkey management
- `src/Bootstrap/DictationManager.cs`: Dictation orchestration

**UI Layer:**
- `MainWindow.xaml`: Main application window
- `SettingsWindow.xaml`: Configuration UI (7-tab interface)
- `TranscriptionWindow.xaml`: Floating transcription display
- `ListeningIndicator.xaml`: Visual recording indicator
- `src/UI/FirstTimeSetupWizard.xaml`: Onboarding wizard

**Testing:**
- `Tests/Unit/*Tests.cs`: Unit tests (50+ files)
- `Tests/WhisperKey.Tests.csproj`: Test project
- `coverlet.runsettings`: Code coverage configuration

## Naming Conventions

**Files:**
- Services: `{ServiceName}Service.cs` with `I{ServiceName}Service.cs` interface
- Models: `{ModelName}.cs` (PascalCase)
- ViewModels: `{ViewName}ViewModel.cs`
- Windows: `{WindowName}Window.xaml` + `.xaml.cs`
- Exceptions: `{ExceptionType}Exception.cs`
- Tests: `{ClassUnderTest}Tests.cs`

**Directories:**
- PascalCase for all directories (`Bootstrap`, `Services`, `ViewModels`)
- Singular naming (`Service` not `Services` in some legacy paths)

**Namespaces:**
- Root: `WhisperKey`
- Services: `WhisperKey.Services` (with sub-namespaces for nested folders)
- Models: `WhisperKey.Models`
- Bootstrap: `WhisperKey.Bootstrap`
- ViewModels: `WhisperKey.ViewModels`

## Where to Add New Code

**New Service:**
- Implementation: `src/Services/{ServiceName}Service.cs`
- Interface: `src/Services/I{ServiceName}Service.cs`
- Registration: `src/Bootstrap/ServiceConfiguration.cs` in `RegisterApplicationServices()`
- Tests: `Tests/Unit/{ServiceName}ServiceTests.cs`

**New Model:**
- Location: `src/Models/{ModelName}.cs`
- Usage: Reference from Services or Configuration

**New Window/Dialog:**
- Location: Root directory (follow existing pattern) OR `src/UI/` for reusable components
- Files: `{Name}.xaml` + `{Name}.xaml.cs`
- Registration: Wire up in `ApplicationBootstrapper` or `EventCoordinator`

**New ViewModel:**
- Location: `src/ViewModels/{Name}ViewModel.cs`
- Registration: Add to DI in `ServiceConfiguration.cs`

**New Exception:**
- Location: `src/Exceptions/{Name}Exception.cs`
- Pattern: Inherit from appropriate base (WhisperKeyException, SecurityException, etc.)

**New Repository:**
- Location: `src/Repositories/`
- Pattern: `I{Name}Repository.cs` interface + `{Name}Repository.cs` implementation

**New Configuration Section:**
- Location: Add class to `Configuration/AppSettings.cs`
- Usage: Bind in `ServiceConfiguration.ConfigureConfiguration()`

**New Test:**
- Location: `Tests/Unit/{ClassUnderTest}Tests.cs`
- Framework: xUnit (inferred from test file structure)

## Special Directories

**`src/Exceptions/`:**
- Purpose: Application-specific exception hierarchy
- Contains: Custom exceptions with structured error information
- Usage: Throw from services, catch in EventCoordinator

**`src/Infrastructure/SmokeTesting/`:**
- Purpose: Production smoke testing framework
- Contains: Health checkers, test orchestrators, result collectors
- Usage: Automated production validation

**`src/Services/Memory/`:**
- Purpose: Object pooling and memory management
- Contains: `ByteArrayPool`, `GenericObjectPool`
- Usage: Reduce GC pressure in audio processing

**`src/Services/Database/`:**
- Purpose: JSON-based database services
- Contains: `JsonDatabaseService`, repositories

**`src/Services/Recovery/`:**
- Purpose: Error recovery and resilience policies
- Contains: `RecoveryPolicyService`

**`Converters/`:**
- Purpose: WPF value converters
- Contains: `DpiScaleConverter.cs`
- Usage: XAML data binding transformations

**`Resources/`:**
- Purpose: Static assets (icons, images)
- Contains: Multi-resolution icons for system tray and window
- Format: PNG files at 1x, 1.5x, 2x, 3x scales

**`.planning/`:**
- Purpose: GSD methodology planning documents
- Structure: `phases/`, `research/`, `codebase/`, `milestones/`
- Integration: Referenced by `/gsd-plan-phase` and `/gsd-execute-phase`

---

*Structure analysis: 2026-02-15*
