using System.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Services;
using WhisperKey.Infrastructure.SmokeTesting;

namespace WhisperKey.Infrastructure.SmokeTesting.HealthChecks
{
    /// <summary>
    /// External service health check validator for smoke testing
    /// </summary>
    public class ExternalServiceHealthChecker : SmokeTestFramework
    {
        public ExternalServiceHealthChecker(IServiceProvider serviceProvider, SmokeTestConfiguration configuration) 
            : base(serviceProvider, configuration)
        {
        }

        public override async Task<SmokeTestSuiteResult> RunAllTestsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new List<SmokeTestResult>();

            try
            {
                _logger.LogInformation("Starting external service health checks");

                // External service health checks
                results.Add(await TestWhisperApiHealthAsync());
                results.Add(await TestAudioDeviceHealthAsync());
                results.Add(await TestTextInjectionHealthAsync());
                results.Add(await TestNetworkEndpointsAsync());

                stopwatch.Stop();
                var suiteResult = new SmokeTestSuiteResult
                {
                    SuiteName = "External Service Health Checks",
                    TestResults = results,
                    TotalTests = results.Count,
                    PassedTests = results.Count(r => r.Success),
                    FailedTests = results.Count(r => !r.Success),
                    SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                    Duration = stopwatch.Elapsed,
                    ReportGeneratedAt = DateTime.UtcNow,
                    ResultsByCategory = new Dictionary<SmokeTestCategory, List<SmokeTestResult>>
                    {
                        [SmokeTestCategory.ExternalService] = results
                    }
                };

                _logger.LogInformation("External service health checks completed: {PassedTests}/{TotalTests} passed", 
                    suiteResult.PassedTests, suiteResult.TotalTests);

                return suiteResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "External service health checks failed with exception");
                
                var errorResult = CreateTestResult("External Service Health Checks", SmokeTestCategory.ExternalService, 
                    false, $"External service health check suite failed: {ex.Message}", stopwatch.Elapsed);
                results.Add(errorResult);

                return new SmokeTestSuiteResult
                {
                    SuiteName = "External Service Health Checks",
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
                "whisper" => await TestWhisperApiHealthAsync(),
                "audio" => await TestAudioDeviceHealthAsync(),
                "textinjection" => await TestTextInjectionHealthAsync(),
                "network" => await TestNetworkEndpointsAsync(),
                _ => CreateTestResult(testName, SmokeTestCategory.ExternalService, false, "Unknown external service health check test", TimeSpan.Zero)
            };
        }

        public override List<SmokeTestResult> GetTestResults()
        {
            return new List<SmokeTestResult>(_testResults);
        }

        private async Task<SmokeTestResult> TestWhisperApiHealthAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Test Whisper API connectivity
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_configuration.HealthCheckTimeoutSeconds);

                var apiStart = Stopwatch.StartNew();
                
                // Test OpenAI API endpoint
                var response = await httpClient.GetAsync("https://api.openai.com/v1/engines");
                
                apiStart.Stop();
                var responseTime = apiStart.ElapsedMilliseconds;
                var isHealthy = response.StatusCode == System.Net.HttpStatusCode.OK || 
                               response.StatusCode == System.Net.HttpStatusCode.Unauthorized; // 401 is OK (means API is up)

                var result = CreateTestResult("Whisper API Health", SmokeTestCategory.ExternalService,
                    isHealthy,
                    isHealthy
                        ? $"Whisper API healthy: {response.StatusCode}, {responseTime}ms"
                        : $"Whisper API unhealthy: {response.StatusCode}, {responseTime}ms",
                    stopwatch.Elapsed);

                result.Metrics["ResponseTimeMs"] = responseTime;
                result.Metrics["HttpStatusCode"] = (int)response.StatusCode;
                result.Metrics["ApiEndpoint"] = "https://api.openai.com/v1/engines";

                LogTestResult(result);
                return result;
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                var result = CreateTestResult("Whisper API Health", SmokeTestCategory.ExternalService,
                    false, $"Whisper API connection failed: {ex.Message}", stopwatch.Elapsed);
                result.Metrics["ExceptionType"] = "HttpRequestException";
                LogTestResult(result);
                return result;
            }
            catch (TaskCanceledException ex)
            {
                var result = CreateTestResult("Whisper API Health", SmokeTestCategory.ExternalService,
                    false, $"Whisper API request timed out: {ex.Message}", stopwatch.Elapsed);
                result.Metrics["ExceptionType"] = "TaskCanceledException";
                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Whisper API Health", SmokeTestCategory.ExternalService,
                    false, $"Whisper API health check failed: {ex.Message}", stopwatch.Elapsed);
                result.ExceptionType = ex.GetType().Name;
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestAudioDeviceHealthAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var audioStart = Stopwatch.StartNew();
                
                // Test audio device service
                var devices = await _audioDeviceService.GetInputDevicesAsync();
                var defaultDevice = await _audioDeviceService.GetDefaultInputDeviceAsync();
                
                audioStart.Stop();
                var audioTime = audioStart.ElapsedMilliseconds;

                var hasDevices = devices != null && devices.Count > 0;
                var hasDefaultDevice = defaultDevice != null;
                var isHealthy = hasDevices && hasDefaultDevice;

                var result = CreateTestResult("Audio Device Health", SmokeTestCategory.ExternalService,
                    isHealthy,
                    isHealthy
                        ? $"Audio devices healthy: {devices?.Count ?? 0} devices, {audioTime}ms"
                        : $"Audio device issues: {devices?.Count ?? 0} devices, Default device: {hasDefaultDevice}",
                    stopwatch.Elapsed);

                result.Metrics["DeviceCount"] = devices?.Count ?? 0;
                result.Metrics["HasDefaultDevice"] = hasDefaultDevice;
                result.Metrics["ResponseTimeMs"] = audioTime;

                if (devices != null)
                {
                    for (int i = 0; i < Math.Min(devices.Count, 5); i++) // Log up to 5 devices
                    {
                        var device = devices[i];
                        result.Metrics[$"Device_{i}_Id"] = device.Id;
                        result.Metrics[$"Device_{i}_Name"] = device.Name;
                        result.Metrics[$"Device_{i}_IsDefault"] = device.IsDefault;
                    }
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Audio Device Health", SmokeTestCategory.ExternalService,
                    false, $"Audio device health check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestTextInjectionHealthAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var injectionStart = Stopwatch.StartNew();
                
                // Test text injection service with minimal test
                var testText = "Health Check Test";
                await _textInjectionService.InjectTextAsync(testText);
                
                injectionStart.Stop();
                var injectionTime = injectionStart.ElapsedMilliseconds;
                var isHealthy = true; // If we get here, injection worked

                var result = CreateTestResult("Text Injection Health", SmokeTestCategory.ExternalService,
                    isHealthy,
                    isHealthy
                        ? $"Text injection healthy: {injectionTime}ms"
                        : "Text injection failed",
                    stopwatch.Elapsed);

                result.Metrics["InjectionTimeMs"] = injectionTime;
                result.Metrics["TestTextLength"] = testText.Length;

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Text Injection Health", SmokeTestCategory.ExternalService,
                    false, $"Text injection health check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestNetworkEndpointsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(_configuration.HealthCheckTimeoutSeconds);

                var endpoints = new[]
                {
                    ("Google DNS", "https://8.8.8.8"),
                    ("Cloudflare DNS", "https://1.1.1.1"),
                    ("HTTPBin", "https://httpbin.org/status/200"),
                    ("GitHub API", "https://api.github.com")
                };

                var endpointResults = new List<(string Name, bool Success, long ResponseTimeMs, int? StatusCode)>();

                foreach (var (name, url) in endpoints)
                {
                    try
                    {
                        var endpointStart = Stopwatch.StartNew();
                        var response = await httpClient.GetAsync(url);
                        endpointStart.Stop();

                        endpointResults.Add((name, true, endpointStart.ElapsedMilliseconds, (int)response.StatusCode));
                    }
                    catch
                    {
                        endpointResults.Add((name, false, 0, null));
                    }
                }

                var successfulEndpoints = endpointResults.Count(r => r.Success);
                var totalEndpoints = endpointResults.Count;
                var successRate = (double)successfulEndpoints / totalEndpoints * 100;
                var isHealthy = successfulEndpoints >= totalEndpoints / 2; // At least 50% success rate

                var result = CreateTestResult("Network Endpoints", SmokeTestCategory.ExternalService,
                    isHealthy,
                    isHealthy
                        ? $"Network endpoints healthy: {successfulEndpoints}/{totalEndpoints} ({successRate:F1}%)"
                        : $"Network endpoint issues: {successfulEndpoints}/{totalEndpoints} ({successRate:F1}%)",
                    stopwatch.Elapsed);

                result.Metrics["SuccessfulEndpoints"] = successfulEndpoints;
                result.Metrics["TotalEndpoints"] = totalEndpoints;
                result.Metrics["SuccessRate"] = Math.Round(successRate, 2);

                foreach (var (name, success, responseTime, statusCode) in endpointResults)
                {
                    var sanitizedName = name.Replace(" ", "_").Replace(".", "_");
                    result.Metrics[$"Endpoint_{sanitizedName}_Success"] = success;
                    result.Metrics[$"Endpoint_{sanitizedName}_ResponseTimeMs"] = responseTime;
                    if (statusCode.HasValue)
                    {
                        result.Metrics[$"Endpoint_{sanitizedName}_StatusCode"] = statusCode.Value;
                    }
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Network Endpoints", SmokeTestCategory.ExternalService,
                    false, $"Network endpoints health check failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }
    }

    /// <summary>
    /// Basic connectivity and operation verification
    /// </summary>
    public class BasicConnectivityChecker : SmokeTestFramework
    {
        public BasicConnectivityChecker(IServiceProvider serviceProvider, SmokeTestConfiguration configuration) 
            : base(serviceProvider, configuration)
        {
        }

        public override async Task<SmokeTestSuiteResult> RunAllTestsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new List<SmokeTestResult>();

            try
            {
                _logger.LogInformation("Starting basic connectivity checks");

                // Basic connectivity checks
                results.Add(await TestInternetConnectivityAsync());
                results.Add(await TestDnsResolutionAsync());
                results.Add(await TestSslConnectivityAsync());
                results.Add(await TestBasicOperationsAsync());

                stopwatch.Stop();
                var suiteResult = new SmokeTestSuiteResult
                {
                    SuiteName = "Basic Connectivity Checks",
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

                _logger.LogInformation("Basic connectivity checks completed: {PassedTests}/{TotalTests} passed", 
                    suiteResult.PassedTests, suiteResult.TotalTests);

                return suiteResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Basic connectivity checks failed with exception");
                
                var errorResult = CreateTestResult("Basic Connectivity Checks", SmokeTestCategory.HealthCheck, 
                    false, $"Basic connectivity check suite failed: {ex.Message}", stopwatch.Elapsed);
                results.Add(errorResult);

                return new SmokeTestSuiteResult
                {
                    SuiteName = "Basic Connectivity Checks",
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
                "internet" => await TestInternetConnectivityAsync(),
                "dns" => await TestDnsResolutionAsync(),
                "ssl" => await TestSslConnectivityAsync(),
                "operations" => await TestBasicOperationsAsync(),
                _ => CreateTestResult(testName, SmokeTestCategory.HealthCheck, false, "Unknown basic connectivity test", TimeSpan.Zero)
            };
        }

        public override List<SmokeTestResult> GetTestResults()
        {
            return new List<SmokeTestResult>(_testResults);
        }

        private async Task<SmokeTestResult> TestInternetConnectivityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var connectivityStart = Stopwatch.StartNew();
                var response = await httpClient.GetAsync("https://www.google.com");
                connectivityStart.Stop();

                var isConnected = response.IsSuccessStatusCode;
                var responseTime = connectivityStart.ElapsedMilliseconds;

                var result = CreateTestResult("Internet Connectivity", SmokeTestCategory.HealthCheck,
                    isConnected,
                    isConnected
                        ? $"Internet connectivity OK: {response.StatusCode}, {responseTime}ms"
                        : $"Internet connectivity failed: {response.StatusCode}, {responseTime}ms",
                    stopwatch.Elapsed);

                result.Metrics["IsConnected"] = isConnected;
                result.Metrics["ResponseTimeMs"] = responseTime;
                result.Metrics["HttpStatusCode"] = (int)response.StatusCode;

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Internet Connectivity", SmokeTestCategory.HealthCheck,
                    false, $"Internet connectivity test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestDnsResolutionAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var dnsStart = Stopwatch.StartNew();
                
                // Test DNS resolution for common domains
                var testDomains = new[] { "google.com", "github.com", "api.openai.com" };
                var resolvedDomains = 0;

                foreach (var domain in testDomains)
                {
                    try
                    {
                        var addresses = await System.Net.Dns.GetHostAddressesAsync(domain);
                        if (addresses.Length > 0)
                        {
                            resolvedDomains++;
                        }
                    }
                    catch
                    {
                        // DNS resolution failed for this domain
                    }
                }

                dnsStart.Stop();
                var dnsTime = dnsStart.ElapsedMilliseconds;
                var dnsHealthy = resolvedDomains >= testDomains.Length / 2; // At least 50% success rate

                var result = CreateTestResult("DNS Resolution", SmokeTestCategory.HealthCheck,
                    dnsHealthy,
                    dnsHealthy
                        ? $"DNS resolution healthy: {resolvedDomains}/{testDomains.Length} domains resolved, {dnsTime}ms"
                        : $"DNS resolution issues: {resolvedDomains}/{testDomains.Length} domains resolved, {dnsTime}ms",
                    stopwatch.Elapsed);

                result.Metrics["ResolvedDomains"] = resolvedDomains;
                result.Metrics["TotalDomains"] = testDomains.Length;
                result.Metrics["ResolutionTimeMs"] = dnsTime;

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("DNS Resolution", SmokeTestCategory.HealthCheck,
                    false, $"DNS resolution test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestSslConnectivityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var httpClient = new System.Net.Http.HttpClient();
                httpClient.Timeout = TimeSpan.FromSeconds(10);

                var sslStart = Stopwatch.StartNew();
                
                // Test SSL connectivity to HTTPS endpoints
                var sslEndpoints = new[] { "https://www.google.com", "https://api.github.com", "https://httpbin.org" };
                var successfulSslConnections = 0;

                foreach (var endpoint in sslEndpoints)
                {
                    try
                    {
                        var response = await httpClient.GetAsync(endpoint);
                        if (response.IsSuccessStatusCode)
                        {
                            successfulSslConnections++;
                        }
                    }
                    catch
                    {
                        // SSL connection failed for this endpoint
                    }
                }

                sslStart.Stop();
                var sslTime = sslStart.ElapsedMilliseconds;
                var sslHealthy = successfulSslConnections >= sslEndpoints.Length / 2; // At least 50% success rate

                var result = CreateTestResult("SSL Connectivity", SmokeTestCategory.HealthCheck,
                    sslHealthy,
                    sslHealthy
                        ? $"SSL connectivity healthy: {successfulSslConnections}/{sslEndpoints.Length} connections successful, {sslTime}ms"
                        : $"SSL connectivity issues: {successfulSslConnections}/{sslEndpoints.Length} connections successful, {sslTime}ms",
                    stopwatch.Elapsed);

                result.Metrics["SuccessfulSslConnections"] = successfulSslConnections;
                result.Metrics["TotalSslEndpoints"] = sslEndpoints.Length;
                result.Metrics["SslTimeMs"] = sslTime;

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("SSL Connectivity", SmokeTestCategory.HealthCheck,
                    false, $"SSL connectivity test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestBasicOperationsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var operationsStart = Stopwatch.StartNew();
                
                // Test basic application operations
                var operations = new List<(string Name, bool Success)>();

                // Test settings operation
                try
                {
                    var settings = await _settingsService.LoadSettingsAsync();
                    operations.Add(("SettingsLoad", settings != null));
                }
                catch
                {
                    operations.Add(("SettingsLoad", false));
                }

                // Test audio device operation
                try
                {
                    var devices = await _audioDeviceService.GetInputDevicesAsync();
                    operations.Add(("AudioDevices", devices != null));
                }
                catch
                {
                    operations.Add(("AudioDevices", false));
                }

                // Test authentication operation
                try
                {
                    var isAuthenticated = await _authenticationService.IsAuthenticatedAsync();
                    operations.Add(("Authentication", true)); // Service availability test
                }
                catch
                {
                    operations.Add(("Authentication", false));
                }

                operationsStart.Stop();
                var operationsTime = operationsStart.ElapsedMilliseconds;
                var successfulOperations = operations.Count(o => o.Success);
                var totalOperations = operations.Count;
                var operationsHealthy = successfulOperations >= totalOperations / 2; // At least 50% success rate

                var result = CreateTestResult("Basic Operations", SmokeTestCategory.HealthCheck,
                    operationsHealthy,
                    operationsHealthy
                        ? $"Basic operations healthy: {successfulOperations}/{totalOperations} operations successful, {operationsTime}ms"
                        : $"Basic operations issues: {successfulOperations}/{totalOperations} operations successful, {operationsTime}ms",
                    stopwatch.Elapsed);

                result.Metrics["SuccessfulOperations"] = successfulOperations;
                result.Metrics["TotalOperations"] = totalOperations;
                result.Metrics["OperationsTimeMs"] = operationsTime;

                foreach (var (name, success) in operations)
                {
                    result.Metrics[$"Operation_{name}_Success"] = success;
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Basic Operations", SmokeTestCategory.HealthCheck,
                    false, $"Basic operations test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }
    }
}
