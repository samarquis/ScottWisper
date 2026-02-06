using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Services;
using WhisperKey.Tests.Smoke;

namespace WhisperKey.Tests.Smoke.HealthChecks
{
    /// <summary>
    /// System health check validator for smoke testing
    /// </summary>
    public class SystemHealthChecker : SmokeTestFramework
    {
        public SystemHealthChecker(IServiceProvider serviceProvider, SmokeTestConfiguration configuration) 
            : base(serviceProvider, configuration)
        {
        }

        public override async Task<SmokeTestSuiteResult> RunAllTestsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new List<SmokeTestResult>();

            try
            {
                _logger.Information("Starting system health checks");

                // Core system health checks
                results.Add(await TestProcessAvailabilityAsync());
                results.Add(await TestMemoryUsageAsync());
                results.Add(await TestCpuUsageAsync());
                results.Add(await TestDiskSpaceAsync());
                results.Add(await TestNetworkConnectivityAsync());
                results.Add(await TestServiceDependenciesAsync());

                stopwatch.Stop();
                var suiteResult = new SmokeTestSuiteResult
                {
                    SuiteName = "System Health Checks",
                    TestResults = results,
                    TotalTests = results.Count,
                    PassedTests = results.Count(r => r.Success),
                    FailedTests = results.Count(r => !r.Success),
                    SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                    Duration = stopwatch.Elapsed,
                    ReportGeneratedAt = DateTime.UtcNow,
                    ResultsByCategory = new Dictionary<SmokeTestCategory, List<SmokeTestResult>>
                    {
                        [SmokeTestCategory.HealthCheck] = results
                    }
                };

                _logger.Information("System health checks completed: {PassedTests}/{TotalTests} passed", 
                    suiteResult.PassedTests, suiteResult.TotalTests);

                return suiteResult;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "System health checks failed with exception");
                
                var errorResult = CreateTestResult("System Health Checks", SmokeTestCategory.HealthCheck, 
                    false, $"Health check suite failed: {ex.Message}", stopwatch.Elapsed);
                results.Add(errorResult);

                return new SmokeTestSuiteResult
                {
                    SuiteName = "System Health Checks",
                    TestResults = results,
                    TotalTests = results.Count,
                    PassedTests = results.Count(r => r.Success),
                    FailedTests = results.Count(r => !r.Success),
                    SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                    Duration = stopwatch.Elapsed,
                    ReportGeneratedAt = DateTime.UtcNow
                };
            }
        }

        public override async Task<SmokeTestResult> RunTestAsync(string testName)
        {
            return testName.ToLower() switch
            {
                "process" => await TestProcessAvailabilityAsync(),
                "memory" => await TestMemoryUsageAsync(),
                "cpu" => await TestCpuUsageAsync(),
                "disk" => await TestDiskSpaceAsync(),
                "network" => await TestNetworkConnectivityAsync(),
                "dependencies" => await TestServiceDependenciesAsync(),
                _ => CreateTestResult(testName, SmokeTestCategory.HealthCheck, false, "Unknown health check test", TimeSpan.Zero)
            };
        }

        public override List<SmokeTestResult> GetTestResults()
        {
            return new List<SmokeTestResult>(_testResults);
        }

        private async Task<SmokeTestResult> TestProcessAvailabilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                var isResponding = !currentProcess.HasExited && currentProcess.Responding;

                var result = CreateTestResult("Process Availability", SmokeTestCategory.HealthCheck,
                    isResponding, 
                    isResponding ? "Main process is running and responsive" : "Main process is not responding",
                    stopwatch.Elapsed);

                result.Metrics["ProcessId"] = currentProcess.Id;
                result.Metrics["MainThreadId"] = currentProcess.MainWindowHandle.ToInt32();
                result.Metrics["StartTime"] = currentProcess.StartTime;

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Process Availability", SmokeTestCategory.HealthCheck,
                    false, $"Process availability check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestMemoryUsageAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                var memoryUsageMb = currentProcess.WorkingSet64 / 1024 / 1024;
                var peakMemoryMb = currentProcess.PeakWorkingSet64 / 1024 / 1024;
                var gcMemoryMb = GC.GetTotalMemory(false) / 1024 / 1024;

                var threshold = _configuration.PerformanceThresholds.MaxMemoryUsageMb;
                var withinThreshold = memoryUsageMb <= threshold;

                var result = CreateTestResult("Memory Usage", SmokeTestCategory.HealthCheck,
                    withinThreshold,
                    withinThreshold 
                        ? $"Memory usage within threshold: {memoryUsageMb}MB <= {threshold}MB"
                        : $"Memory usage exceeds threshold: {memoryUsageMb}MB > {threshold}MB",
                    stopwatch.Elapsed);

                result.Metrics["CurrentMemoryMb"] = memoryUsageMb;
                result.Metrics["PeakMemoryMb"] = peakMemoryMb;
                result.Metrics["GcMemoryMb"] = gcMemoryMb;
                result.Metrics["ThresholdMb"] = threshold;

                if (!withinThreshold)
                {
                    result.Warnings.Add("High memory usage detected - consider optimization");
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Memory Usage", SmokeTestCategory.HealthCheck,
                    false, $"Memory usage check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestCpuUsageAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                
                // Get CPU usage (simplified approach)
                var startTime = DateTime.UtcNow;
                var startCpuTime = currentProcess.TotalProcessorTime;
                
                await Task.Delay(1000); // Wait 1 second for measurement
                
                var endTime = DateTime.UtcNow;
                var endCpuTime = currentProcess.TotalProcessorTime;
                
                var cpuUsedMs = (endCpuTime - startCpuTime).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsagePercent = cpuUsedMs / totalMsPassed / Environment.ProcessorCount * 100;

                var threshold = _configuration.PerformanceThresholds.MaxCpuUsagePercent;
                var withinThreshold = cpuUsagePercent <= threshold;

                var result = CreateTestResult("CPU Usage", SmokeTestCategory.HealthCheck,
                    withinThreshold,
                    withinThreshold
                        ? $"CPU usage within threshold: {cpuUsagePercent:F1}% <= {threshold}%"
                        : $"CPU usage exceeds threshold: {cpuUsagePercent:F1}% > {threshold}%",
                    stopwatch.Elapsed);

                result.Metrics["CpuUsagePercent"] = Math.Round(cpuUsagePercent, 2);
                result.Metrics["ThresholdPercent"] = threshold;
                result.Metrics["ProcessorCount"] = Environment.ProcessorCount;

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("CPU Usage", SmokeTestCategory.HealthCheck,
                    false, $"CPU usage check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestDiskSpaceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var systemDrive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? "C:");
                var freeSpaceGb = systemDrive.AvailableFreeSpace / 1024 / 1024 / 1024;
                var totalSpaceGb = systemDrive.TotalSize / 1024 / 1024 / 1024;
                var usedPercent = ((double)(totalSpaceGb - freeSpaceGb) / totalSpaceGb) * 100;

                var minFreeSpaceGb = 1; // Require at least 1GB free space
                var hasEnoughSpace = freeSpaceGb >= minFreeSpaceGb;

                var result = CreateTestResult("Disk Space", SmokeTestCategory.HealthCheck,
                    hasEnoughSpace,
                    hasEnoughSpace
                        ? $"Sufficient disk space: {freeSpaceGb}GB free"
                        : $"Insufficient disk space: {freeSpaceGb}GB free (minimum {minFreeSpaceGb}GB required)",
                    stopwatch.Elapsed);

                result.Metrics["FreeSpaceGb"] = freeSpaceGb;
                result.Metrics["TotalSpaceGb"] = totalSpaceGb;
                result.Metrics["UsedPercent"] = Math.Round(usedPercent, 2);
                result.Metrics["MinFreeSpaceGb"] = minFreeSpaceGb;

                if (freeSpaceGb < 5)
                {
                    result.Warnings.Add("Low disk space - consider cleanup");
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Disk Space", SmokeTestCategory.HealthCheck,
                    false, $"Disk space check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestNetworkConnectivityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_configuration.HealthCheckTimeoutSeconds);

                var testUrls = new[]
                {
                    "https://www.google.com",
                    "https://httpbin.org/status/200",
                    "https://api.openai.com/v1/engines"
                };

                var successfulConnections = 0;
                var connectionTimes = new List<long>();

                foreach (var url in testUrls)
                {
                    try
                    {
                        var connectionStart = Stopwatch.StartNew();
                        var response = await httpClient.GetAsync(url);
                        connectionStart.Stop();

                        if (response.IsSuccessStatusCode)
                        {
                            successfulConnections++;
                            connectionTimes.Add(connectionStart.ElapsedMilliseconds);
                        }
                    }
                    catch
                    {
                        // Connection failed for this URL
                    }
                }

                var successRate = (double)successfulConnections / testUrls.Length * 100;
                var avgConnectionTime = connectionTimes.Any() ? connectionTimes.Average() : 0;
                var isHealthy = successfulConnections >= testUrls.Length / 2; // At least 50% success rate

                var result = CreateTestResult("Network Connectivity", SmokeTestCategory.HealthCheck,
                    isHealthy,
                    isHealthy
                        ? $"Network connectivity healthy: {successfulConnections}/{testUrls.Length} connections successful"
                        : $"Network connectivity issues: {successfulConnections}/{testUrls.Length} connections successful",
                    stopwatch.Elapsed);

                result.Metrics["SuccessfulConnections"] = successfulConnections;
                result.Metrics["TotalConnections"] = testUrls.Length;
                result.Metrics["SuccessRate"] = Math.Round(successRate, 2);
                result.Metrics["AvgConnectionTimeMs"] = Math.Round(avgConnectionTime, 2);

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Network Connectivity", SmokeTestCategory.HealthCheck,
                    false, $"Network connectivity check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestServiceDependenciesAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var dependencyChecks = new List<(string Name, Task<bool> Check)>();

                // Check core service dependencies
                dependencyChecks.Add(("Settings Service", TestSettingsServiceAsync()));
                dependencyChecks.Add(("Authentication Service", TestAuthenticationServiceAsync()));
                dependencyChecks.Add(("Audio Device Service", TestAudioDeviceServiceAsync()));
                dependencyChecks.Add(("Text Injection Service", TestTextInjectionServiceAsync()));

                var results = new List<(string Name, bool Success, long DurationMs)>();

                foreach (var (name, check) in dependencyChecks)
                {
                    var checkStopwatch = Stopwatch.StartNew();
                    try
                    {
                        var success = await check;
                        checkStopwatch.Stop();
                        results.Add((name, success, checkStopwatch.ElapsedMilliseconds));
                    }
                    catch
                    {
                        checkStopwatch.Stop();
                        results.Add((name, false, checkStopwatch.ElapsedMilliseconds));
                    }
                }

                var successfulDependencies = results.Count(r => r.Success);
                var totalDependencies = results.Count;
                var allHealthy = successfulDependencies == totalDependencies;

                var result = CreateTestResult("Service Dependencies", SmokeTestCategory.HealthCheck,
                    allHealthy,
                    allHealthy
                        ? $"All service dependencies healthy: {successfulDependencies}/{totalDependencies}"
                        : $"Service dependency issues: {successfulDependencies}/{totalDependencies} healthy",
                    stopwatch.Elapsed);

                result.Metrics["SuccessfulDependencies"] = successfulDependencies;
                result.Metrics["TotalDependencies"] = totalDependencies;

                foreach (var (name, success, duration) in results)
                {
                    result.Metrics[$"Dependency_{name.Replace(" ", "_")}"] = success;
                    result.Metrics[$"Dependency_{name.Replace(" ", "_")}_DurationMs"] = duration;
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Service Dependencies", SmokeTestCategory.HealthCheck,
                    false, $"Service dependencies check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<bool> TestSettingsServiceAsync()
        {
            try
            {
                await _settingsService.LoadSettingsAsync();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestAuthenticationServiceAsync()
        {
            try
            {
                // Test basic authentication service availability
                var isAuthenticated = await _authenticationService.IsAuthenticatedAsync();
                return true; // Service is available even if not authenticated
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestAudioDeviceServiceAsync()
        {
            try
            {
                var devices = await _audioDeviceService.GetAvailableDevicesAsync();
                return devices != null;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestTextInjectionServiceAsync()
        {
            try
            {
                // Test text injection service availability
                var testText = "Health Check";
                await _textInjectionService.InjectTextAsync(testText);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}