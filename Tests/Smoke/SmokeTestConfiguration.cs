using System;
using System.Collections.Generic;

namespace WhisperKey.Tests.Smoke
{
    /// <summary>
    /// Configuration for smoke testing suite
    /// </summary>
    public class SmokeTestConfiguration
    {
        /// <summary>
        /// Default timeout for individual tests (seconds)
        /// </summary>
        public int DefaultTestTimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Timeout for health checks (seconds)
        /// </summary>
        public int HealthCheckTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Timeout for workflow tests (seconds)
        /// </summary>
        public int WorkflowTestTimeoutSeconds { get; set; } = 60;

        /// <summary>
        /// Maximum number of retry attempts for failed tests
        /// </summary>
        public int MaxRetryAttempts { get; set; } = 2;

        /// <summary>
        /// Delay between retry attempts (milliseconds)
        /// </summary>
        public int RetryDelayMs { get; set; } = 1000;

        /// <summary>
        /// Enable parallel test execution
        /// </summary>
        public bool EnableParallelExecution { get; set; } = true;

        /// <summary>
        /// Maximum degree of parallelism
        /// </summary>
        public int MaxParallelism { get; set; } = 4;

        /// <summary>
        /// Performance baseline thresholds (ms)
        /// </summary>
        public PerformanceThresholds PerformanceThresholds { get; set; } = new PerformanceThresholds();

        /// <summary>
        /// Security validation settings
        /// </summary>
        public SecurityValidationSettings SecuritySettings { get; set; } = new SecurityValidationSettings();

        /// <summary>
        /// External service endpoints for health checks
        /// </summary>
        public Dictionary<string, string> ServiceEndpoints { get; set; } = new Dictionary<string, string>
        {
            ["whisper_api"] = "https://api.openai.com/v1/engines",
            ["authentication"] = "https://auth.example.com/health",
            ["database"] = "https://db.example.com/health"
        };

        /// <summary>
        /// Test categories to run
        /// </summary>
        public HashSet<SmokeTestCategory> EnabledCategories { get; set; } = new HashSet<SmokeTestCategory>
        {
            SmokeTestCategory.Critical,
            SmokeTestCategory.HealthCheck,
            SmokeTestCategory.Workflow,
            SmokeTestCategory.Performance,
            SmokeTestCategory.Security
        };

        /// <summary>
        /// Environment-specific settings
        /// </summary>
        public Dictionary<string, EnvironmentSettings> Environments { get; set; } = new Dictionary<string, EnvironmentSettings>
        {
            ["production"] = new EnvironmentSettings
            {
                StrictMode = true,
                PerformanceThresholdMultiplier = 1.0,
                RequireAllTests = true
            },
            ["staging"] = new EnvironmentSettings
            {
                StrictMode = false,
                PerformanceThresholdMultiplier = 1.5,
                RequireAllTests = false
            }
        };
    }

    /// <summary>
    /// Performance baseline thresholds
    /// </summary>
    public class PerformanceThresholds
    {
        /// <summary>
        /// Maximum audio processing time (ms)
        /// </summary>
        public int MaxAudioProcessingMs { get; set; } = 2000;

        /// <summary>
        /// Maximum text injection time (ms)
        /// </summary>
        public int MaxTextInjectionMs { get; set; } = 500;

        /// <summary>
        /// Maximum settings load time (ms)
        /// </summary>
        public int MaxSettingsLoadMs { get; set; } = 1000;

        /// <summary>
        /// Maximum authentication time (ms)
        /// </summary>
        public int MaxAuthenticationMs { get; set; } = 1500;

        /// <summary>
        /// Maximum memory usage (MB)
        /// </summary>
        public int MaxMemoryUsageMb { get; set; } = 512;

        /// <summary>
        /// Maximum CPU usage percentage
        /// </summary>
        public double MaxCpuUsagePercent { get; set; } = 80.0;
    }

    /// <summary>
    /// Security validation settings
    /// </summary>
    public class SecurityValidationSettings
    {
        /// <summary>
        /// Require SOC2 compliance validation
        /// </summary>
        public bool RequireSOC2Compliance { get; set; } = true;

        /// <summary>
        /// Require audit logging validation
        /// </summary>
        public bool RequireAuditLogging { get; set; } = true;

        /// <summary>
        /// Require secure credential storage validation
        /// </summary>
        public bool RequireSecureCredentialStorage { get; set; } = true;

        /// <summary>
        /// Require permission system validation
        /// </summary>
        public bool RequirePermissionSystem { get; set; } = true;

        /// <summary>
        /// Require API key rotation validation
        /// </summary>
        public bool RequireApiKeyRotation { get; set; } = true;

        /// <summary>
        /// Require security alert system validation
        /// </summary>
        public bool RequireSecurityAlerts { get; set; } = true;
    }

    /// <summary>
    /// Environment-specific settings
    /// </summary>
    public class EnvironmentSettings
    {
        /// <summary>
        /// Enable strict mode (all tests must pass)
        /// </summary>
        public bool StrictMode { get; set; } = true;

        /// <summary>
        /// Performance threshold multiplier
        /// </summary>
        public double PerformanceThresholdMultiplier { get; set; } = 1.0;

        /// <summary>
        /// Require all tests to pass
        /// </summary>
        public bool RequireAllTests { get; set; } = true;

        /// <summary>
        /// Skip performance tests
        /// </summary>
        public bool SkipPerformanceTests { get; set; } = false;

        /// <summary>
        /// Skip security tests
        /// </summary>
        public bool SkipSecurityTests { get; set; } = false;
    }

    /// <summary>
    /// Smoke test categories
    /// </summary>
    public enum SmokeTestCategory
    {
        /// <summary>
        /// Critical system functionality tests
        /// </summary>
        Critical,

        /// <summary>
        /// Health check tests for services and dependencies
        /// </summary>
        HealthCheck,

        /// <summary>
        /// Core workflow validation tests
        /// </summary>
        Workflow,

        /// <summary>
        /// Performance baseline validation tests
        /// </summary>
        Performance,

        /// <summary>
        /// Security feature validation tests
        /// </summary>
        Security,

        /// <summary>
        /// External service integration tests
        /// </summary>
        ExternalService,

        /// <summary>
        /// Deployment validation tests
        /// </summary>
        Deployment
    }

    /// <summary>
    /// Smoke test result
    /// </summary>
    public class SmokeTestResult
    {
        public string TestName { get; set; } = string.Empty;
        public SmokeTestCategory Category { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; }
        public string CorrelationId { get; set; } = string.Empty;
        public int RetryCount { get; set; }
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
        public List<string> Warnings { get; set; } = new List<string>();
        public string? ExceptionType { get; set; }
        public string? StackTrace { get; set; }
    }

    /// <summary>
    /// Smoke test suite result
    /// </summary>
    public class SmokeTestSuiteResult
    {
        public string SuiteName { get; set; } = string.Empty;
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public double SuccessRate { get; set; }
        public List<SmokeTestResult> TestResults { get; set; } = new List<SmokeTestResult>();
        public Dictionary<SmokeTestCategory, List<SmokeTestResult>> ResultsByCategory { get; set; } = new Dictionary<SmokeTestCategory, List<SmokeTestResult>>();
        public DateTime ReportGeneratedAt { get; set; }
        public TimeSpan Duration { get; set; }
        public bool HasCriticalFailures { get; set; }
        public List<SmokeTestResult> CriticalFailures { get; set; } = new List<SmokeTestResult>();
        public string Environment { get; set; } = string.Empty;
        public string BuildVersion { get; set; } = string.Empty;
        public Dictionary<string, object> SystemMetrics { get; set; } = new Dictionary<string, object>();
        public bool AllPassed => PassedTests == TotalTests && TotalTests > 0;
        public bool IsProductionReady => AllPassed && !HasCriticalFailures;
    }
}