using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WhisperKey.Tests.Smoke.HealthChecks;
using WhisperKey.Tests.Smoke.Workflows;
using WhisperKey.Tests.Smoke.Performance;
using WhisperKey.Tests.Smoke.Security;
using WhisperKey.Tests.Smoke.Deployment;
using WhisperKey.Tests.Smoke.Reporting;

namespace WhisperKey.Tests.Smoke
{
    /// <summary>
    /// Main smoke test orchestrator for production deployment validation
    /// </summary>
    public class ProductionSmokeTestOrchestrator
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProductionSmokeTestOrchestrator> _logger;
        private readonly SmokeTestConfiguration _configuration;
        private readonly SmokeTestEnvironmentManager _environmentManager;
        private readonly SmokeTestResultCollector _resultCollector;
        private readonly SmokeTestReportingService _reportingService;

        public ProductionSmokeTestOrchestrator(
            IServiceProvider serviceProvider,
            ILogger<ProductionSmokeTestOrchestrator> logger,
            SmokeTestConfiguration configuration,
            SmokeTestEnvironmentManager environmentManager,
            SmokeTestResultCollector resultCollector,
            SmokeTestReportingService reportingService)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            _configuration = configuration;
            _environmentManager = environmentManager;
            _resultCollector = resultCollector;
            _reportingService = reportingService;
        }

        /// <summary>
        /// Run comprehensive production smoke tests
        /// </summary>
        public async Task<SmokeTestProductionReport> RunProductionSmokeTestsAsync()
        {
            var overallStopwatch = Stopwatch.StartNew();
            
            try
            {
                _logger.Information("Starting comprehensive production smoke test suite");

                // Setup production environment
                if (!await _environmentManager.SetupProductionEnvironmentAsync())
                {
                    throw new InvalidOperationException("Failed to setup production smoke test environment");
                }

                // Run all smoke test categories
                var testResults = new List<SmokeTestSuiteResult>();

                if (_configuration.EnabledCategories.Contains(SmokeTestCategory.Critical))
                {
                    testResults.Add(await RunCriticalTestsAsync());
                }

                if (_configuration.EnabledCategories.Contains(SmokeTestCategory.HealthCheck))
                {
                    testResults.Add(await RunHealthCheckTestsAsync());
                }

                if (_configuration.EnabledCategories.Contains(SmokeTestCategory.Workflow))
                {
                    testResults.Add(await RunWorkflowTestsAsync());
                }

                if (_configuration.EnabledCategories.Contains(SmokeTestCategory.Performance))
                {
                    testResults.Add(await RunPerformanceTestsAsync());
                }

                if (_configuration.EnabledCategories.Contains(SmokeTestCategory.Security))
                {
                    testResults.Add(await RunSecurityTestsAsync());
                }

                if (_configuration.EnabledCategories.Contains(SmokeTestCategory.ExternalService))
                {
                    testResults.Add(await RunExternalServiceTestsAsync());
                }

                if (_configuration.EnabledCategories.Contains(SmokeTestCategory.Deployment))
                {
                    testResults.Add(await RunDeploymentTestsAsync());
                }

                // Aggregate results
                var aggregatedResult = AggregateTestResults(testResults);
                
                // Generate production report
                var productionReport = await _reportingService.GenerateProductionReportAsync(aggregatedResult);

                overallStopwatch.Stop();
                _logger.Information("Production smoke test suite completed in {Duration}s: {PassedTests}/{TotalTests} passed, Success Rate: {SuccessRate:F1}%",
                    overallStopwatch.Elapsed.TotalSeconds,
                    productionReport.TestResults.PassedTests,
                    productionReport.TestResults.TotalTests,
                    productionReport.TestResults.SuccessRate);

                return productionReport;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Production smoke test suite failed");
                throw;
            }
            finally
            {
                // Cleanup environment
                await _environmentManager.CleanupProductionEnvironmentAsync();
            }
        }

        /// <summary>
        /// Run specific smoke test category
        /// </summary>
        public async Task<SmokeTestSuiteResult> RunSmokeTestCategoryAsync(SmokeTestCategory category)
        {
            return category switch
            {
                SmokeTestCategory.Critical => await RunCriticalTestsAsync(),
                SmokeTestCategory.HealthCheck => await RunHealthCheckTestsAsync(),
                SmokeTestCategory.Workflow => await RunWorkflowTestsAsync(),
                SmokeTestCategory.Performance => await RunPerformanceTestsAsync(),
                SmokeTestCategory.Security => await RunSecurityTestsAsync(),
                SmokeTestCategory.ExternalService => await RunExternalServiceTestsAsync(),
                SmokeTestCategory.Deployment => await RunDeploymentTestsAsync(),
                _ => throw new ArgumentException($"Unknown smoke test category: {category}")
            };
        }

        private async Task<SmokeTestSuiteResult> RunCriticalTestsAsync()
        {
            _logger.Information("Running critical smoke tests");
            
            var systemHealthChecker = new SystemHealthChecker(_serviceProvider, _configuration);
            var result = await systemHealthChecker.RunAllTestsAsync();
            
            // Mark as critical category
            foreach (var testResult in result.TestResults)
            {
                testResult.Category = SmokeTestCategory.Critical;
                _resultCollector.AddResult(testResult);
            }
            
            return result;
        }

        private async Task<SmokeTestSuiteResult> RunHealthCheckTestsAsync()
        {
            _logger.Information("Running health check smoke tests");
            
            var results = new List<SmokeTestResult>();
            
            // System health checks
            var systemHealthChecker = new SystemHealthChecker(_serviceProvider, _configuration);
            var systemResult = await systemHealthChecker.RunAllTestsAsync();
            results.AddRange(systemResult.TestResults);
            
            // Database health checks
            var databaseHealthChecker = new DatabaseHealthChecker(_serviceProvider, _configuration);
            var databaseResult = await databaseHealthChecker.RunAllTestsAsync();
            results.AddRange(databaseResult.TestResults);
            
            // Authentication health checks
            var authHealthChecker = new AuthenticationHealthChecker(_serviceProvider, _configuration);
            var authResult = await authHealthChecker.RunAllTestsAsync();
            results.AddRange(authResult.TestResults);
            
            // External service health checks
            var externalHealthChecker = new ExternalServiceHealthChecker(_serviceProvider, _configuration);
            var externalResult = await externalHealthChecker.RunAllTestsAsync();
            results.AddRange(externalResult.TestResults);
            
            // Basic connectivity checks
            var connectivityChecker = new BasicConnectivityChecker(_serviceProvider, _configuration);
            var connectivityResult = await connectivityChecker.RunAllTestsAsync();
            results.AddRange(connectivityResult.TestResults);
            
            var suiteResult = new SmokeTestSuiteResult
            {
                SuiteName = "Health Check Smoke Tests",
                TestResults = results,
                TotalTests = results.Count,
                PassedTests = results.Count(r => r.Success),
                FailedTests = results.Count(r => !r.Success),
                SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                Duration = TimeSpan.FromMilliseconds(results.Sum(r => r.Duration.TotalMilliseconds)),
                ReportGeneratedAt = DateTime.UtcNow,
                ResultsByCategory = new Dictionary<SmokeTestCategory, List<SmokeTestResult>>
                {
                    [SmokeTestCategory.HealthCheck] = results
                }
            };
            
            foreach (var testResult in results)
            {
                _resultCollector.AddResult(testResult);
            }
            
            return suiteResult;
        }

        private async Task<SmokeTestSuiteResult> RunWorkflowTestsAsync()
        {
            _logger.Information("Running workflow smoke tests");
            
            var coreWorkflowValidator = new CoreWorkflowValidator(_serviceProvider, _configuration);
            var result = await coreWorkflowValidator.RunAllTestsAsync();
            
            foreach (var testResult in result.TestResults)
            {
                _resultCollector.AddResult(testResult);
            }
            
            return result;
        }

        private async Task<SmokeTestSuiteResult> RunPerformanceTestsAsync()
        {
            _logger.Information("Running performance smoke tests");
            
            var performanceValidator = new PerformanceBaselineValidator(_serviceProvider, _configuration);
            var result = await performanceValidator.RunAllTestsAsync();
            
            foreach (var testResult in result.TestResults)
            {
                _resultCollector.AddResult(testResult);
            }
            
            return result;
        }

        private async Task<SmokeTestSuiteResult> RunSecurityTestsAsync()
        {
            _logger.Information("Running security smoke tests");
            
            var securityValidator = new SecurityFeatureValidator(_serviceProvider, _configuration);
            var result = await securityValidator.RunAllTestsAsync();
            
            foreach (var testResult in result.TestResults)
            {
                _resultCollector.AddResult(testResult);
            }
            
            return result;
        }

        private async Task<SmokeTestSuiteResult> RunExternalServiceTestsAsync()
        {
            _logger.Information("Running external service smoke tests");
            
            var externalServiceHealthChecker = new ExternalServiceHealthChecker(_serviceProvider, _configuration);
            var result = await externalServiceHealthChecker.RunAllTestsAsync();
            
            foreach (var testResult in result.TestResults)
            {
                _resultCollector.AddResult(testResult);
            }
            
            return result;
        }

        private async Task<SmokeTestSuiteResult> RunDeploymentTestsAsync()
        {
            _logger.Information("Running deployment smoke tests");
            
            var deploymentValidator = new DeploymentValidator(_serviceProvider, _configuration);
            var result = await deploymentValidator.RunAllTestsAsync();
            
            foreach (var testResult in result.TestResults)
            {
                _resultCollector.AddResult(testResult);
            }
            
            return result;
        }

        private SmokeTestSuiteResult AggregateTestResults(List<SmokeTestSuiteResult> testResults)
        {
            var allResults = testResults.SelectMany(r => r.TestResults).ToList();
            
            var aggregatedResult = new SmokeTestSuiteResult
            {
                SuiteName = "Production Smoke Tests",
                TestResults = allResults,
                TotalTests = allResults.Count,
                PassedTests = allResults.Count(r => r.Success),
                FailedTests = allResults.Count(r => !r.Success),
                SuccessRate = allResults.Count > 0 ? (double)allResults.Count(r => r.Success) / allResults.Count * 100 : 0,
                Duration = TimeSpan.FromMilliseconds(allResults.Sum(r => r.Duration.TotalMilliseconds)),
                ReportGeneratedAt = DateTime.UtcNow,
                ResultsByCategory = allResults.GroupBy(r => r.Category)
                    .ToDictionary(g => g.Key, g => g.ToList()),
                HasCriticalFailures = allResults.Any(r => !r.Success && r.Category == SmokeTestCategory.Critical),
                CriticalFailures = allResults.Where(r => !r.Success && r.Category == SmokeTestCategory.Critical).ToList(),
                Environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Unknown",
                BuildVersion = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? "Unknown"
            };
            
            return aggregatedResult;
        }

        /// <summary>
        /// Export smoke test results to specified directory
        /// </summary>
        public async Task ExportSmokeTestResultsAsync(SmokeTestProductionReport report, string outputDirectory)
        {
            await _reportingService.ExportProductionReportAsync(report, outputDirectory);
        }

        /// <summary>
        /// Validate if deployment is production ready based on smoke test results
        /// </summary>
        public bool IsProductionReady(SmokeTestProductionReport report)
        {
            // Check if all tests passed
            if (!report.TestResults.AllPassed)
            {
                return false;
            }
            
            // Check for critical failures
            if (report.TestResults.HasCriticalFailures)
            {
                return false;
            }
            
            // Check deployment validation
            if (!report.DeploymentValidation.IsValid)
            {
                return false;
            }
            
            // Check compliance status
            if (!report.ComplianceStatus.OverallCompliant)
            {
                return false;
            }
            
            // Check environment-specific requirements
            var environment = report.Environment.ToLower();
            if (_configuration.Environments.ContainsKey(environment))
            {
                var envSettings = _configuration.Environments[environment];
                if (envSettings.StrictMode && !report.TestResults.AllPassed)
                {
                    return false;
                }
                
                if (envSettings.RequireAllTests && report.TestResults.FailedTests > 0)
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}