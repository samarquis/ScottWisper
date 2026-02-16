# Architecture

**Analysis Date:** 2026-02-15

## Pattern Overview

**Overall:** Service-Oriented Architecture (SOA) with MVVM presentation layer

**Key Characteristics:**
- Dependency Injection container for service lifecycle management
- Layered architecture with clear separation between UI, services, and data
- Event-driven communication between components via EventCoordinator
- Async/await patterns throughout for non-blocking operations
- Provider pattern for transcription backends (Whisper, Vosk, Cloud)

## Layers

**Presentation Layer (UI):**
- Purpose: User interface components and interactions
- Location: Root directory (`MainWindow.xaml`, `SettingsWindow.xaml`) and `src/UI/`
- Contains: XAML views, code-behind, window definitions
- Depends on: ViewModels, Services (via DI)
- Used by: User interactions, System events

**ViewModel Layer:**
- Purpose: MVVM pattern implementation for data binding
- Location: `src/ViewModels/`
- Contains: `MainViewModel.cs`, `SettingsViewModel.cs`, `TranscriptionViewModel.cs`
- Depends on: Services interfaces
- Used by: XAML views via data binding

**Application/Bootstrap Layer:**
- Purpose: Application startup, service configuration, event coordination
- Location: `src/Bootstrap/`
- Contains: `ApplicationBootstrapper.cs`, `ServiceConfiguration.cs`, `EventCoordinator.cs`, `DictationManager.cs`
- Depends on: All service implementations
- Used by: `App.xaml.cs` (entry point)

**Service Layer:**
- Purpose: Business logic and external integrations
- Location: `src/Services/`
- Contains: 99 service files (interfaces and implementations)
- Depends on: Models, Repositories, External libraries (NAudio, Whisper.net, Polly)
- Used by: Bootstrap layer, ViewModels, other Services

**Repository Layer:**
- Purpose: Data persistence abstraction
- Location: `src/Repositories/`
- Contains: `ISettingsRepository.cs`, `FileSettingsRepository.cs`
- Depends on: Configuration models
- Used by: Services requiring persistence

**Model Layer:**
- Purpose: Data structures and domain models
- Location: `src/Models/`
- Contains: 15 model classes (Settings, AuditLog, BusinessMetrics, etc.)
- Depends on: None (pure data)
- Used by: All layers

**Configuration Layer:**
- Purpose: Settings classes and configuration management
- Location: `Configuration/`
- Contains: `AppSettings.cs` (comprehensive settings hierarchy)
- Depends on: None
- Used by: Services, DI container

**Infrastructure Layer:**
- Purpose: Platform-specific implementations, exceptions, smoke testing
- Location: `src/Infrastructure/`, `src/Exceptions/`
- Contains: Exception classes, smoke testing framework
- Depends on: System libraries
- Used by: Services, Application layer

**Integration Layer:**
- Purpose: External service integrations
- Location: `src/Integration/`
- Contains: Empty (placeholder for future integrations)
- Used by: Services layer

## Data Flow

**Dictation Flow:**

1. **Activation:** Hotkey press detected by `HotkeyService` → `EventCoordinator.OnHotkeyPressed`
2. **State Management:** `DictationManager.ToggleAsync()` manages recording state
3. **Audio Capture:** `AudioCaptureService.StartCaptureAsync()` begins recording via NAudio/WASAPI
4. **Audio Processing:** `AudioCaptureService.AudioDataCaptured` event streams audio data
5. **Transcription:** `WhisperService.TranscribeAudioAsync()` processes audio via Whisper.net
6. **Text Review (Optional):** `TranscriptionReviewWindow.ShowReview()` for user verification
7. **Text Injection:** `TextInjectionService.InjectTextAsync()` simulates keystrokes via Win32 SendInput
8. **Feedback:** `FeedbackService` provides visual/audio status updates

**Settings Flow:**

1. **Load:** `FileSettingsRepository.LoadAsync()` → JSON deserialization
2. **Access:** Services request settings via `ISettingsService.Settings`
3. **Modify:** UI binds to `SettingsViewModel` properties
4. **Persist:** `SettingsService.SaveAsync()` → Repository → JSON file
5. **Notify:** `SettingsService.SettingsChanged` event triggers service reconfiguration

**State Management:**

- **Application State:** Managed by `ApplicationBootstrapper` (singleton services)
- **Dictation State:** Managed by `DictationManager` (recording/stopped)
- **UI State:** Managed by ViewModels with `INotifyPropertyChanged`
- **Configuration State:** Immutable settings objects, replaced on save

## Key Abstractions

**Service Interfaces:**
- Purpose: Define contracts for all business logic services
- Examples: `IWhisperService`, `IAudioCaptureService`, `IHotkeyService`
- Pattern: Interface in `src/Services/I*.cs`, Implementation in `src/Services/*Service.cs`

**Repository Pattern:**
- Purpose: Abstract data persistence
- Examples: `ISettingsRepository` → `FileSettingsRepository`
- Pattern: Async CRUD operations with backup/restore support

**Provider Pattern:**
- Purpose: Pluggable transcription backends
- Examples: `ILocalTranscriptionProvider` → `WhisperService`, `VoskTranscriptionProvider`
- Pattern: Factory-based instantiation in `WhisperProcessorFactory`

**Event Coordinator:**
- Purpose: Centralize event handling to prevent async void antipattern
- Location: `src/Bootstrap/EventCoordinator.cs`
- Pattern: Subscribes to service events, delegates to async handlers

**Lazy Initialization:**
- Purpose: Defer heavy service construction to improve startup time
- Implementation: `ServiceConfiguration.AddLazy<TInterface, TImplementation>()`
- Examples: `IAudioDeviceService`, `HotkeyService`, `WebhookService`

## Entry Points

**Application Entry:**
- Location: `App.xaml.cs`
- Triggers: Windows application startup
- Responsibilities: 
  - Global exception handlers registration
  - DI container initialization via `ServiceConfiguration.ConfigureServices()`
  - Bootstrapper initialization
  - Main window creation and display

**Dictation Activation:**
- Location: `HotkeyService` (global hotkey) / `SystemTrayService` (tray menu)
- Triggers: Alt+Space hotkey, Tray icon click, MainWindow button
- Responsibilities: Route to `DictationManager.ToggleAsync()`

**Settings UI:**
- Location: `SettingsWindow.xaml`
- Triggers: Tray menu → Settings, MainWindow → Settings button
- Responsibilities: Display/edit settings via `SettingsViewModel`

## Error Handling

**Strategy:** Layered exception handling with user-friendly messages

**Patterns:**
1. **Global Exception Handlers:** Registered in `App.xaml.cs` (AppDomain, TaskScheduler, Dispatcher)
2. **Structured Logging:** Serilog with correlation IDs and contextual enrichment
3. **Service-Level Handling:** Try-catch blocks in EventCoordinator event handlers
4. **User Notifications:** Toast notifications via `FeedbackService` for non-fatal errors
5. **Fatal Exception Filtering:** OutOfMemoryException, StackOverflowException terminate application
6. **Exception Hierarchy:** Custom exceptions in `src/Exceptions/` (AudioCaptureException, TranscriptionException, etc.)

**Error Reporting:**
- `IErrorReportingService` classifies errors by severity
- Automatic error reporting with correlation tracking
- Fallback to console logging if service unavailable

## Cross-Cutting Concerns

**Logging:**
- Framework: Serilog with Microsoft.Extensions.Logging integration
- Sinks: Console, File (rolling), Seq (structured logging server)
- Enrichers: CorrelationId, Environment, Process, ThreadId
- Location: `ServiceConfiguration.ConfigureSerilogLogger()`

**Validation:**
- Approach: Input validation at service boundaries
- Implementation: `ValidationService`, `InputValidationService`
- Patterns: Fluent validation-style methods, guard clauses

**Authentication/Security:**
- API Key Management: `ApiKeyManagementService` with Windows Credential Store
- Encryption: `WindowsCredentialService` for sensitive data
- Audit Logging: `AuditLoggingService` for security events
- Compliance: `SOC2ComplianceService` for audit trails

**Correlation Tracking:**
- Service: `CorrelationService` provides request-scoped IDs
- Usage: All async operations tagged with correlation ID
- Benefit: End-to-end request tracing across service calls

**Performance Monitoring:**
- Service: `PerformanceMonitoringService`
- Metrics: Operation timing, memory usage, throughput
- Alerting: `IntelligentAlertingService` for anomaly detection

**Resilience:**
- Library: Polly for circuit breaker, retry, timeout patterns
- Implementation: `RecoveryPolicyService`, `GracefulDegradationService`

---

*Architecture analysis: 2026-02-15*
