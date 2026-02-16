# Testing Patterns

**Analysis Date:** 2026-02-15

## Test Framework

**Runner:**
- **MSTest** (Microsoft.VisualStudio.TestTools) v3.1.1
- Config: `Tests/WhisperKey.Tests.csproj`
- Target: .NET 8.0-windows

**Assertion Library:**
- MSTest built-in assertions (`Assert.AreEqual`, `Assert.IsNotNull`, `Assert.ThrowsExceptionAsync`)

**Run Commands:**
```bash
# Run all tests
dotnet test Tests/WhisperKey.Tests.csproj

# Run with coverage
dotnet test --collect:"XPlat Code Coverage" --results-directory ./CoverageResults

# Run with custom settings
dotnet test --settings coverlet.runsettings --logger "trx;LogFileName=test_results.trx"

# Generate HTML report (requires ReportGenerator)
reportgenerator -reports:"./CoverageResults/*.xml" -targetdir:"./CoverageReports" -reporttypes:Html
```

## Test File Organization

**Location:**
- Unit tests: `Tests/Unit/*.cs`
- Integration tests: `Tests/Integration/*.cs` (currently excluded from compilation)
- Smoke tests: `Tests/Smoke/*.cs`
- E2E tests: `Tests/E2E/*.cs`
- Performance tests: `Tests/Performance/*.cs`
- Common utilities: `Tests/Common/*.cs`

**Naming:**
- `[ServiceName]Tests.cs` for unit tests
- `[Feature]IntegrationTests.cs` for integration tests
- Test methods: `[MethodName]_[Scenario]_[ExpectedResult]`
  - Example: `TranscribeAudioAsync_Cloud_Success_ReturnsTranscription`
  - Example: `StartCaptureAsync_WithPermissionGranted_StartsRecording`

**Structure:**
```
Tests/
├── Unit/
│   ├── WhisperServiceTests.cs
│   ├── AudioCaptureServiceTests.cs
│   ├── HotkeyServiceTests.cs
│   └── ... (60+ test files)
├── Integration/
│   ├── IntegrationTests.cs
│   ├── CrossApplicationTests.cs
│   └── ... (currently excluded from compilation)
├── Smoke/
│   ├── SmokeTestRunner.cs
│   └── SmokeTestFrameworkTests.cs
├── E2E/
│   └── EndToEndTests.cs
├── Performance/
│   └── PerformanceRegressionTests.cs
└── Common/
    ├── ValidationTestRunner.cs
    └── IntegrationTestFramework.cs
```

## Test Structure

**Suite Organization:**
```csharp
[TestClass]
public class WhisperServiceTests
{
    private Mock<ISettingsService> _settingsServiceMock = null!;
    private Mock<IAudioValidationProvider> _audioValidatorMock = null!;

    [TestInitialize]
    public void Setup()
    {
        _settingsServiceMock = new Mock<ISettingsService>();
        // Setup default mocks
    }

    [TestCleanup]
    public void Cleanup()
    {
        _service?.Dispose();
    }

    #region Constructor Tests
    [TestMethod]
    public void Constructor_Default_CreatesHttpClient()
    {
        // Arrange
        // Act
        // Assert
    }
    #endregion
}
```

**Patterns:**
- **Setup/Teardown:** Use `[TestInitialize]` and `[TestCleanup]` attributes
- **Null-forgiving:** Initialize mocks with `null!` to satisfy nullable checks
- **Regions:** Group related tests with `#region` blocks
- **Async:** All async tests return `Task` and use `async/await`

## Mocking

**Framework:** Moq v4.20.69

**Patterns:**
```csharp
// Basic mock creation
_settingsServiceMock = new Mock<ISettingsService>();

// Setup return values
_settingsServiceMock.Setup(s => s.Settings).Returns(new AppSettings { ... });

// Setup async methods
_settingsServiceMock.Setup(s => s.GetEncryptedValueAsync("OpenAI_ApiKey"))
    .ReturnsAsync("test-api-key");

// Verify calls
_audioDeviceServiceMock.Verify(a => a.RequestMicrophonePermissionAsync(), Times.Once);

// Raise events
_settingsServiceMock.Raise(s => s.SettingsChanged += null, sender, eventArgs);

// Mock HttpClient (common pattern)
_httpHandlerMock = new Mock<HttpMessageHandler>();
_httpHandlerMock.Protected()
    .Setup<Task<HttpResponseMessage>>("SendAsync",
        ItExpr.IsAny<HttpRequestMessage>(),
        ItExpr.IsAny<CancellationToken>())
    .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });
```

**What to Mock:**
- External services (HTTP clients, API calls)
- I/O operations (file system, audio devices)
- Configuration services
- Dependencies with side effects

**What NOT to Mock:**
- Value objects and DTOs
- Static utility methods (use them directly)
- The class under test
- Concrete classes with non-virtual methods (limitation noted in codebase)

**Custom Mocks:**
- Create mock implementations for complex interfaces
- Example: `MockWaveIn` class for `IWaveIn` testing
  ```csharp
  public class MockWaveIn : IWaveIn
  {
      public void SimulateDataAvailable(byte[] buffer, int bytesRecorded)
      {
          DataAvailable?.Invoke(this, new WaveInEventArgs(buffer, bytesRecorded));
      }
  }
  ```

## Fixtures and Factories

**Test Data:**
- Inline test data for simple cases
- Helper methods for complex test data generation
  ```csharp
  private static byte[] CreateValidWavData(int durationSeconds = 1)
  {
      // Generate valid WAV file bytes for testing
  }
  ```

**Location:**
- Test data generators in same test class as private methods
- Shared fixtures in `Tests/Common/`

## Coverage

**Requirements:**
- Target: 80% line, branch, and method coverage
- Tool: Coverlet (coverlet.collector, coverlet.msbuild)

**View Coverage:**
```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage" --results-directory ./CoverageResults

# Convert to HTML
reportgenerator -reports:"./CoverageResults/coverage.cobertura.xml" -targetdir:"./CoverageReports" -reporttypes:Html
```

**Coverage Areas:**
- **High Priority:** `AudioDeviceService`, `WhisperService`, `AudioCaptureService`, `SettingsService`, `HotkeyService`
- **Exceptions:** All custom exception classes

## Test Types

**Unit Tests:**
- Test individual classes in isolation
- Mock all dependencies
- Fast execution (<100ms per test)
- Example location: `Tests/Unit/WhisperServiceTests.cs`

**Integration Tests:**
- Test service interactions
- Partial mocking allowed
- Currently disabled in compilation:
  ```xml
  <ItemGroup>
    <Compile Remove="Integration\**" />
  </ItemGroup>
  ```

**E2E Tests:**
- Full workflow testing
- Minimal mocking
- Example: `Tests/E2E/EndToEndTests.cs`

**Smoke Tests:**
- Basic functionality verification
- Quick health checks
- Example: `Tests/Smoke/SmokeTestRunner.cs`

**Performance Tests:**
- Benchmark and regression detection
- Example: `Tests/Performance/PerformanceRegressionTests.cs`
- Also see: `Benchmarks/` directory

## Common Patterns

**Async Testing:**
```csharp
[TestMethod]
public async Task TranscribeAudioAsync_Cloud_Success_ReturnsTranscription()
{
    // Arrange
    _httpHandlerMock.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ...)
        .ReturnsAsync(new HttpResponseMessage { ... });

    // Act
    var result = await service.TranscribeAudioAsync(CreateValidWavData());

    // Assert
    Assert.AreEqual(expectedText, result);
}
```

**Error Testing:**
```csharp
[TestMethod]
public async Task TranscribeAudioAsync_ApiError_ThrowsHttpRequestException()
{
    // Arrange
    _httpHandlerMock.Protected()
        .Setup<Task<HttpResponseMessage>>("SendAsync", ...)
        .ReturnsAsync(new HttpResponseMessage 
        { 
            StatusCode = HttpStatusCode.Unauthorized 
        });

    // Act & Assert
    await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
    {
        await service.TranscribeAudioAsync(CreateValidWavData());
    });
}
```

**Event Testing:**
```csharp
[TestMethod]
public async Task TranscribeAudioAsync_FiresTranscriptionStartedEvent()
{
    // Arrange
    bool eventFired = false;
    service.TranscriptionStarted += (s, e) => eventFired = true;

    // Act
    await service.TranscribeAudioAsync(CreateValidWavData());

    // Assert
    Assert.IsTrue(eventFired, "TranscriptionStarted event should fire");
}
```

**File System Testing:**
```csharp
[TestMethod]
public async Task TranscribeAudioFileAsync_ValidFile_ReturnsTranscription()
{
    var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.wav");
    try
    {
        File.WriteAllBytes(tempFile, CreateValidWavData());
        // Test logic
    }
    finally
    {
        if (File.Exists(tempFile))
            File.Delete(tempFile);
    }
}
```

**Ignored Tests:**
```csharp
[TestMethod]
[Ignore("LocalInferenceService.TranscribeAudioAsync is not virtual - cannot mock with Moq")]
public async Task TranscribeAudioAsync_LocalMode_Success_ReturnsLocalResult()
{
    // Test implementation
}
```

## Test Configuration

**Test Project Settings:**
```xml
<PropertyGroup>
    <IsTestProject>true</IsTestProject>
    <PreserveCompilationContext>true</PreserveCompilationContext>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
</PropertyGroup>
```

**Test Exclusions:**
- Integration tests currently excluded from compilation
- Quick test file excluded: `QuickTest.cs`

**Dependencies:**
- `Microsoft.NET.Test.Sdk` v17.8.0
- `MSTest.TestFramework` v3.1.1
- `MSTest.TestAdapter` v3.1.1
- `Moq` v4.20.69
- `coverlet.collector` v6.0.0
- `coverlet.msbuild` v6.0.0

---

*Testing analysis: 2026-02-15*
