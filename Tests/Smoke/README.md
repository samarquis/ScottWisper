# WhisperKey Production Smoke Testing Suite

## Overview

This comprehensive smoke testing suite provides production deployment validation for WhisperKey, ensuring system reliability, performance, and security before going live.

## Features

### 🔍 **Phase 1: Infrastructure**
- **SmokeTestFramework**: Base testing framework with correlation IDs and structured logging
- **SmokeTestConfiguration**: Flexible configuration management for different environments
- **SmokeTestReportingService**: Production-grade reporting with multiple export formats

### 🏥 **Phase 2: Health Checks**
- **SystemHealthChecker**: Process availability, memory/CPU usage, disk space, network connectivity
- **DatabaseHealthChecker**: Connection validation, operations testing, data integrity, performance
- **AuthenticationHealthChecker**: Service availability, credential storage, permission system validation
- **ExternalServiceHealthChecker**: Whisper API, audio devices, text injection, network endpoints
- **BasicConnectivityChecker**: Internet connectivity, DNS resolution, SSL validation, basic operations

### 🔄 **Phase 3: Workflow Tests**
- **CoreWorkflowValidator**: End-to-end validation of critical user workflows
  - Audio transcription workflow (device → processing → injection)
  - Hotkey workflow (configuration → registration → conflict detection)
  - Settings workflow (load → modify → persist → restore)
  - Security workflow (authentication → permissions → audit → credentials)
  - Cross-application workflow (injection → compatibility → types)

### ⚡ **Phase 4: Performance & Security**
- **PerformanceBaselineValidator**: REQ-006 benchmark validation
  - Audio processing performance (≤2000ms)
  - Text injection performance (≤500ms)
  - Settings load performance (≤1000ms)
  - Authentication performance (≤1500ms)
  - Memory usage validation (≤512MB)
  - CPU usage validation (≤80%)
- **SecurityFeatureValidator**: REQ-002, REQ-003, REQ-004 compliance
  - SOC2 compliance validation
  - Audit logging system validation
  - Secure credential storage validation
  - Permission system validation
  - API key rotation validation
  - Security alert system validation

### 🚀 **Phase 5: Deployment Integration**
- **DeploymentValidator**: Production deployment validation
  - Deployment health checks
  - Configuration validation
  - Environment-specific validation
  - Rollback procedure validation
  - Service availability validation

## Usage

### Command Line Execution

```bash
# Run all smoke tests
dotnet run --project Tests/Smoke/SmokeTestRunner.csproj

# Run with specific environment
set ENVIRONMENT=production
dotnet run --project Tests/Smoke/SmokeTestRunner.csproj

# Run with custom configuration
set SmokeTest:OutputDirectory=./custom-results
dotnet run --project Tests/Smoke/SmokeTestRunner.csproj
```

### Programmatic Usage

```csharp
// Setup configuration
var configuration = new SmokeTestConfiguration
{
    DefaultTestTimeoutSeconds = 30,
    EnabledCategories = new HashSet<SmokeTestCategory>
    {
        SmokeTestCategory.Critical,
        SmokeTestCategory.HealthCheck,
        SmokeTestCategory.Workflow,
        SmokeTestCategory.Performance,
        SmokeTestCategory.Security
    }
};

// Create orchestrator
var orchestrator = new ProductionSmokeTestOrchestrator(
    serviceProvider,
    logger,
    configuration,
    environmentManager,
    resultCollector,
    reportingService);

// Run smoke tests
var report = await orchestrator.RunProductionSmokeTestsAsync();

// Check production readiness
var isProductionReady = orchestrator.IsProductionReady(report);

// Export results
await orchestrator.ExportSmokeTestResultsAsync(report, "./results");
```

## Configuration

### Environment Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `ENVIRONMENT` | Target environment (production/staging/development) | `production` |
| `LOG_LEVEL` | Logging level (Information/Warning/Error) | `Information` |
| `BUILD_VERSION` | Build version for reporting | `Unknown` |
| `COMMIT_HASH` | Git commit hash for reporting | `Unknown` |

### Configuration File

See `Tests/Smoke/appsettings.json` for complete configuration options:

```json
{
  "SmokeTest": {
    "DefaultTestTimeoutSeconds": 30,
    "HealthCheckTimeoutSeconds": 10,
    "WorkflowTestTimeoutSeconds": 60,
    "MaxRetryAttempts": 2,
    "EnableParallelExecution": true,
    "MaxParallelism": 4,
    "EnabledCategories": "Critical,HealthCheck,Workflow,Performance,Security,ExternalService,Deployment",
    "PerformanceThresholds": {
      "MaxAudioProcessingMs": 2000,
      "MaxTextInjectionMs": 500,
      "MaxSettingsLoadMs": 1000,
      "MaxAuthenticationMs": 1500,
      "MaxMemoryUsageMb": 512,
      "MaxCpuUsagePercent": 80.0
    },
    "SecuritySettings": {
      "RequireSOC2Compliance": true,
      "RequireAuditLogging": true,
      "RequireSecureCredentialStorage": true,
      "RequirePermissionSystem": true,
      "RequireApiKeyRotation": true,
      "RequireSecurityAlerts": true
    }
  }
}
```

## Test Categories

### Critical
Essential system functionality that must work for production deployment.

### HealthCheck
Service and dependency health validation.

### Workflow
Core user workflow end-to-end testing.

### Performance
Performance baseline validation against REQ-006 benchmarks.

### Security
Security feature validation (REQ-002, REQ-003, REQ-004).

### ExternalService
External service integration testing.

### Deployment
Deployment validation and rollback procedures.

## Reports

The smoke testing suite generates comprehensive reports in multiple formats:

### JSON Report
Machine-readable format for API consumption and monitoring systems.

### HTML Report
Interactive dashboard for human viewing with detailed metrics and visualizations.

### Markdown Report
Documentation-friendly format for change logs and incident reports.

### Prometheus Metrics
Monitoring system integration for alerting and dashboarding.

## Production Readiness Criteria

A deployment is considered production-ready when:

1. **All Tests Pass**: 100% test success rate
2. **No Critical Failures**: All critical category tests pass
3. **Deployment Validation**: All deployment checks pass
4. **Compliance Status**: All security compliance checks pass
5. **Performance Thresholds**: All performance benchmarks met
6. **Environment Requirements**: Environment-specific criteria met

### Environment-Specific Requirements

#### Production
- **Strict Mode**: All tests must pass
- **Performance Thresholds**: 1.0x multiplier (no tolerance)
- **Security**: All security tests required
- **Rollback**: Rollback procedures must be validated

#### Staging
- **Relaxed Mode**: 80% test success rate acceptable
- **Performance Thresholds**: 1.5x multiplier tolerance
- **Security**: All security tests required
- **Rollback**: Rollback procedures recommended

#### Development
- **Relaxed Mode**: 70% test success rate acceptable
- **Performance**: Performance tests optional
- **Security**: Security tests required
- **Rollback**: Rollback procedures optional

## Integration with CI/CD

### GitHub Actions Example

```yaml
name: Production Smoke Tests

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  smoke-tests:
    runs-on: windows-latest
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'
    
    - name: Run Smoke Tests
      run: |
        set ENVIRONMENT=production
        set BUILD_VERSION=${{ github.sha }}
        dotnet run --project Tests/Smoke/SmokeTestRunner.csproj
    
    - name: Upload Reports
      uses: actions/upload-artifact@v3
      with:
        name: smoke-test-reports
        path: smoke-test-results/
    
    - name: Check Production Readiness
      run: |
        # Check exit code from smoke test runner
        if ($LASTEXITCODE -ne 0) {
          Write-Host "❌ Production deployment NOT ready"
          exit 1
        } else {
          Write-Host "✅ Production deployment ready"
        }
```

## Troubleshooting

### Common Issues

1. **Network Connectivity Failures**
   - Check internet connection
   - Verify firewall settings
   - Validate external service endpoints

2. **Performance Test Failures**
   - Check system resource availability
   - Verify performance thresholds are realistic
   - Consider environment-specific multipliers

3. **Security Test Failures**
   - Verify credential storage permissions
   - Check audit logging configuration
   - Validate permission system setup

4. **Authentication Issues**
   - Check authentication service availability
   - Verify credential storage
   - Validate permission system

### Debug Mode

Enable debug logging for detailed troubleshooting:

```bash
set LOG_LEVEL=Debug
set SmokeTest:EnableParallelExecution=false
dotnet run --project Tests/Smoke/SmokeTestRunner.csproj
```

## Architecture

The smoke testing suite follows a modular architecture:

```
ProductionSmokeTestOrchestrator
├── HealthCheck Tests
│   ├── SystemHealthChecker
│   ├── DatabaseHealthChecker
│   ├── AuthenticationHealthChecker
│   ├── ExternalServiceHealthChecker
│   └── BasicConnectivityChecker
├── Workflow Tests
│   └── CoreWorkflowValidator
├── Performance Tests
│   └── PerformanceBaselineValidator
├── Security Tests
│   └── SecurityFeatureValidator
├── Deployment Tests
│   └── DeploymentValidator
└── Reporting
    ├── SmokeTestReportingService
    └── Multiple Export Formats
```

## Dependencies

- **.NET 8.0**: Core framework
- **Microsoft.Extensions.**: Dependency injection, logging, configuration
- **Serilog**: Structured logging with correlation IDs
- **MSTest**: Test framework for unit tests
- **Moq**: Mocking framework for testing

## Contributing

When adding new smoke tests:

1. Follow existing patterns in the appropriate category
2. Add comprehensive error handling and logging
3. Include performance metrics where applicable
4. Add corresponding unit tests
5. Update configuration as needed
6. Document new test scenarios

## License

This smoke testing suite is part of the WhisperKey project and follows the same licensing terms.