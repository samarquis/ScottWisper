# Coding Conventions

**Analysis Date:** 2026-02-15

## Naming Patterns

**Files:**
- **Service classes:** `[ServiceName]Service.cs` (e.g., `WhisperService.cs`, `AudioCaptureService.cs`)
- **Interface files:** `I[InterfaceName].cs` (e.g., `IAudioServices.cs`, `IWhisperService.cs`)
- **Exception files:** `[Name]Exception.cs` (e.g., `WhisperKeyException.cs`, `TranscriptionException.cs`)
- **ViewModel files:** `[Name]ViewModel.cs` (e.g., `MainViewModel.cs`, `TranscriptionViewModel.cs`)
- **Test files:** `[Name]Tests.cs` (e.g., `WhisperServiceTests.cs`, `AudioCaptureServiceTests.cs`)
- **Window files:** `[Name]Window.xaml.cs` (e.g., `TranscriptionWindow.xaml.cs`, `MainWindow.xaml.cs`)

**Functions:**
- **PascalCase** for all public methods (C# standard)
  - Example: `StartCaptureAsync()`, `ValidateAudioData()`, `GetUsageStats()`
- **camelCase** for private/protected methods
  - Example: `onSettingsChanged()`, `handleSettingsChangedAsync()`
- **Async suffix** for all async methods
  - Example: `TranscribeAudioAsync()`, `StartCaptureAsync()`

**Variables:**
- **Private fields:** `_camelCase` with underscore prefix
  - Example: `_settingsService`, `_apiKey`, `_logger`
- **Public properties:** PascalCase
  - Example: `StatusText`, `CurrentStatus`, `IsCapturing`
- **Local variables:** camelCase
  - Example: `result`, `audioData`, `response`
- **Constants:** PascalCase or ALL_CAPS
  - Example: `ApiEndpointConfigKey`, `CircuitBreakerThreshold`

**Types:**
- **Classes:** PascalCase (e.g., `WhisperService`, `MainViewModel`)
- **Interfaces:** IPascalCase (e.g., `ISettingsService`, `IWhisperService`)
- **Enums:** PascalCase, members PascalCase
  - Example: `VoiceCommandType` with `Punctuation`, `Delete`, `NewLine`
- **Structs/Records:** PascalCase

**Generic Types:**
- Use single uppercase letter for type parameters
  - Example: `SetProperty<T>(ref T field, T value)`

## Code Style

**Formatting:**
- **Indentation:** 4 spaces
- **Braces:** Opening brace on new line (Allman style)
  ```csharp
  public void Method()
  {
      // implementation
  }
  ```
- **Line endings:** CRLF (Windows style)
- **Max line length:** No strict limit, but aim for 120 characters
- **File encoding:** UTF-8 with BOM

**Null Safety:**
- Project uses C# nullable reference types (`<Nullable>enable</Nullable>`)
- Use null-forgiving operator `!` sparingly
- Mark nullable parameters/returns with `?`
  ```csharp
  public string? GetApiKey()
  public async Task HandleSettingsChangedAsync(SettingsChangedEventArgs? e)
  ```
- Use null-conditional operator for safe navigation: `_logger?.LogError(...)`

**Documentation Comments:**
- XML documentation on all public APIs
- `<summary>` tags for all public members
- `<param>` and `<returns>` for methods with parameters
- `<exception>` for methods that throw
  ```csharp
  /// <summary>
  /// Creates a retry policy for OpenAI API calls with exponential backoff.
  /// Handles transient failures like network blips, rate limits, and temporary service unavailability.
  /// </summary>
  private static AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy()
  ```

**Region Organization:**
- Use `#region` to group related functionality
- Common regions: Constructor Tests, Event Tests, Dispose Tests
  ```csharp
  #region Constructor Tests
  // Constructor test methods here
  #endregion
  ```

## Import Organization

**Order:**
1. System namespaces
2. Third-party library namespaces
3. Internal project namespaces

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Services;
```

**Path Aliases:**
- No custom path aliases detected
- Standard project references via `using` statements

## Error Handling

**Exception Hierarchy:**
- Base exception: `WhisperKeyException` with `ErrorCode` property
- Domain exceptions inherit from base:
  - `TranscriptionException`
  - `AudioCaptureException`
  - `HotkeyRegistrationException`
  - `TextInjectionException`
  - `SettingsValidationException`

**Fatal Exception Filtering:**
- Never catch `OutOfMemoryException`, `StackOverflowException`, `AccessViolationException`
- Pattern used:
  ```csharp
  catch (Exception ex) when (!IsFatalException(ex))
  {
      _logger?.LogError(ex, "Operation failed");
  }
  ```

**Error Codes:**
- Include error codes in exceptions for programmatic handling
- Example: `"INVALID_AUDIO"`, `"RATE_LIMIT_EXCEEDED"`

## Logging

**Framework:** Serilog with Microsoft.Extensions.Logging

**Patterns:**
- Use structured logging with named parameters
- Always null-check logger before use: `_logger?.LogError(...)`
- Use appropriate log levels:
  - `LogError` for exceptions and failures
  - `LogWarning` for non-fatal issues
  - `LogInformation` for significant events
  - `LogDebug` for diagnostic information

**Examples:**
```csharp
_logger?.LogError(ex, "SSL Certificate error: {Errors}", errors);
_logger?.LogWarning("Rate limit exceeded for transcription request.");
_logger?.LogInformation("SystemTrayService initialized successfully");
```

## Comments

**When to Comment:**
- Complex business logic or algorithms
- Security-related code (marked with SEC-XXX)
- Deprecated methods with migration guidance
- TODO/FIXME markers for known issues

**Security Comments:**
- Use `SEC-XXX` format for security controls
- Example: `// SEC-004: Implement server certificate validation`

**Deprecation Comments:**
```csharp
// DEPRECATED: This constructor creates HttpClient directly and should not be used
// Use IHttpClientFactory-based constructor instead to prevent socket exhaustion
[Obsolete("Use constructor with IHttpClientFactory to prevent socket exhaustion")]
public WhisperService()
```

## Function Design

**Size:**
- Keep methods focused and under 100 lines when possible
- Extract complex logic into private helper methods

**Parameters:**
- Use constructor injection for dependencies
- Group related parameters into configuration objects
- Use optional parameters for backward compatibility

**Return Values:**
- Async methods return `Task` or `Task<T>`
- Use `ValueTask<T>` for performance-critical hot paths
- Return result objects for complex operations

## Module Design

**Namespace Organization:**
- Root namespace: `WhisperKey`
- Sub-namespaces mirror folder structure:
  - `WhisperKey.Services`
  - `WhisperKey.Models`
  - `WhisperKey.ViewModels`
  - `WhisperKey.Exceptions`
  - `WhisperKey.Configuration`

**Dependency Injection:**
- Constructor injection is the primary pattern
- Use `IServiceProvider` for service location in complex scenarios
- Register services in `ServiceConfiguration.cs`

**Events:**
- Use `EventHandler<T>` pattern with custom EventArgs
- Always null-check before invoking: `TranscriptionCompleted?.Invoke(this, result)`

## Configuration

**Settings Pattern:**
- Strongly-typed configuration classes
- Use `IOptions<T>` or `ISettingsService` for settings access
- Encrypted values for sensitive data

**Example:**
```csharp
public class AppSettings
{
    public TranscriptionSettings Transcription { get; set; } = new();
    public AudioSettings Audio { get; set; } = new();
    public UISettings UI { get; set; } = new();
}
```

---

*Convention analysis: 2026-02-15*
