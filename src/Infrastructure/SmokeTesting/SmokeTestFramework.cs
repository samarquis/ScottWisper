using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WhisperKey.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace WhisperKey.Infrastructure.SmokeTesting
{
    /// <summary>
    /// Base class for smoke testing framework
    /// </summary>
    public abstract class SmokeTestFramework
    {
        protected readonly IServiceProvider _serviceProvider;
        protected readonly ILogger<SmokeTestFramework> _logger;
        protected readonly ISettingsService _settingsService;
        protected readonly IAuthenticationService _authenticationService;
        protected readonly IAudioDeviceService _audioDeviceService;
        protected readonly ITextInjection _textInjectionService;
        protected readonly List<SmokeTestResult> _testResults = new List<SmokeTestResult>();
        protected readonly SmokeTestConfiguration _configuration;

        protected SmokeTestFramework(IServiceProvider serviceProvider, SmokeTestConfiguration configuration)
        {
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _logger = serviceProvider.GetRequiredService<ILogger<SmokeTestFramework>>();
            _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
            _authenticationService = serviceProvider.GetRequiredService<IAuthenticationService>();
            _audioDeviceService = serviceProvider.GetRequiredService<IAudioDeviceService>();
            _textInjectionService = serviceProvider.GetRequiredService<ITextInjection>();
        }

        public abstract Task<SmokeTestSuiteResult> RunAllTestsAsync();
        public abstract Task<SmokeTestResult> RunTestAsync(string testName);
        public abstract List<SmokeTestResult> GetTestResults();

        protected SmokeTestResult CreateTestResult(string testName, SmokeTestCategory category, bool success, string message, TimeSpan duration)
        {
            return new SmokeTestResult
            {
                TestName = testName,
                Category = category,
                Success = success,
                Message = message,
                Duration = duration,
                Timestamp = DateTime.UtcNow,
                CorrelationId = Guid.NewGuid().ToString()
            };
        }

        protected async Task<bool> WaitForServiceAsync(string serviceName, TimeSpan timeout)
        {
            var startTime = DateTime.UtcNow;
            while (DateTime.UtcNow - startTime < timeout)
            {
                try
                {
                    // Service-specific health check logic would go here
                    // For now, simulate service availability
                    await Task.Delay(100);
                    return true;
                }
                catch
                {
                    await Task.Delay(1000);
                }
            }
            return false;
        }

        protected void LogTestResult(SmokeTestResult result)
        {
            _testResults.Add(result);
            
            var logLevel = result.Success ? LogLevel.Information : LogLevel.Error;
            _logger.Log(logLevel, 
                "Smoke Test: {TestName} [{Category}] - {Status} - {Message} (Duration: {Duration}ms, Correlation: {CorrelationId})",
                result.TestName,
                result.Category,
                result.Success ? "PASS" : "FAIL",
                result.Message,
                result.Duration.TotalMilliseconds,
                result.CorrelationId);
        }

        protected async Task<T> ExecuteWithTimeoutAsync<T>(Func<Task<T>> operation, TimeSpan timeout, string operationName)
        {
            using var cts = new System.Threading.CancellationTokenSource(timeout);
            try
            {
                return await operation();
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Operation {OperationName} timed out after {Timeout}ms", operationName, timeout.TotalMilliseconds);
                throw;
            }
        }
    }

    /// <summary>
    /// Smoke test environment manager for production deployment validation
    /// </summary>
    public class SmokeTestEnvironmentManager
    {
        private readonly ILogger<SmokeTestEnvironmentManager> _logger;
        private readonly SmokeTestConfiguration _configuration;
        private readonly List<IDisposable> _disposables = new List<IDisposable>();

        public SmokeTestEnvironmentManager(ILogger<SmokeTestEnvironmentManager> logger, SmokeTestConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        public async Task<bool> SetupProductionEnvironmentAsync()
        {
            try
            {
                _logger.LogInformation("Setting up smoke test environment for production validation");

                // Validate environment configuration
                if (!await ValidateEnvironmentConfigurationAsync())
                {
                    _logger.LogError("Environment configuration validation failed");
                    return false;
                }

                // Initialize logging with correlation IDs
                await InitializeStructuredLoggingAsync();

                // Setup monitoring and alerting
                await SetupMonitoringAsync();

                _logger.LogInformation("Smoke test environment setup completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to setup smoke test environment");
                return false;
            }
        }

        public async Task CleanupProductionEnvironmentAsync()
        {
            try
            {
                _logger.LogInformation("Cleaning up smoke test environment");

                foreach (var disposable in _disposables)
                {
                    try
                    {
                        disposable?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error disposing resource during cleanup");
                    }
                }
                _disposables.Clear();

                await Task.Delay(1000); // Allow for cleanup completion
                _logger.LogInformation("Smoke test environment cleanup completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during environment cleanup");
            }
        }

        private async Task<bool> ValidateEnvironmentConfigurationAsync()
        {
            // Validate required environment variables
            var requiredVars = new[] { "ENVIRONMENT", "LOG_LEVEL", "API_BASE_URL" };
            foreach (var varName in requiredVars)
            {
                var value = Environment.GetEnvironmentVariable(varName);
                if (string.IsNullOrEmpty(value))
                {
                    _logger.LogError("Required environment variable {VarName} is not set", varName);
                    return false;
                }
            }

            // Validate network connectivity
            if (!await ValidateNetworkConnectivityAsync())
            {
                _logger.LogError("Network connectivity validation failed");
                return false;
            }

            return true;
        }

        private async Task<bool> ValidateNetworkConnectivityAsync()
        {
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                var response = await httpClient.GetAsync("https://www.google.com");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task InitializeStructuredLoggingAsync()
        {
            // Serilog is already configured, just ensure correlation ID logging
            await Task.CompletedTask;
        }

        private async Task SetupMonitoringAsync()
        {
            // Setup would include health check endpoints, metrics collection, etc.
            await Task.CompletedTask;
        }
    }

    /// <summary>
    /// Smoke test result collector for production reporting
    /// </summary>
    public class SmokeTestResultCollector
    {
        private readonly ILogger<SmokeTestResultCollector> _logger;
        private readonly List<SmokeTestResult> _results = new List<SmokeTestResult>();
        private readonly Dictionary<SmokeTestCategory, List<SmokeTestResult>> _resultsByCategory = new Dictionary<SmokeTestCategory, List<SmokeTestResult>>();

        public SmokeTestResultCollector(ILogger<SmokeTestResultCollector> logger)
        {
            _logger = logger;
        }

        public void AddResult(SmokeTestResult result)
        {
            _results.Add(result);
            
            if (!_resultsByCategory.ContainsKey(result.Category))
            {
                _resultsByCategory[result.Category] = new List<SmokeTestResult>();
            }
            _resultsByCategory[result.Category].Add(result);

            // Log to structured logging for production monitoring
            _logger.LogInformation("Smoke test result: {TestName} - {Success} - {Category} - {Duration}ms",
                result.TestName, result.Success, result.Category, result.Duration.TotalMilliseconds);
        }

        public SmokeTestSuiteResult GenerateProductionReport()
        {
            var totalTests = _results.Count;
            var passedTests = _results.Count(r => r.Success);
            var failedTests = totalTests - passedTests;
            var successRate = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0;

            // Check for critical failures
            var criticalFailures = _results.Where(r => !r.Success && r.Category == SmokeTestCategory.Critical).ToList();
            var hasCriticalFailures = criticalFailures.Any();

            return new SmokeTestSuiteResult
            {
                SuiteName = "Production Smoke Tests",
                TotalTests = totalTests,
                PassedTests = passedTests,
                FailedTests = failedTests,
                SuccessRate = successRate,
                TestResults = new List<SmokeTestResult>(_results),
                ResultsByCategory = new Dictionary<SmokeTestCategory, List<SmokeTestResult>>(_resultsByCategory),
                ReportGeneratedAt = DateTime.UtcNow,
                Duration = CalculateTotalDuration(),
                HasCriticalFailures = hasCriticalFailures,
                CriticalFailures = criticalFailures,
                Environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Unknown",
                BuildVersion = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? "Unknown"
            };
        }

        private TimeSpan CalculateTotalDuration()
        {
            var totalTicks = _results.Sum(r => r.Duration.Ticks);
            return TimeSpan.FromTicks(totalTicks);
        }

        public async Task ExportProductionReportAsync(string filePath)
        {
            var report = await GenerateJsonReportAsync();
            await File.WriteAllTextAsync(filePath, report);
        }

        private async Task<string> GenerateJsonReportAsync()
        {
            var report = GenerateProductionReport();
            return System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
        }
    }
}
