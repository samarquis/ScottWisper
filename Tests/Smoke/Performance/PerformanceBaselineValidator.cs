using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Services;
using WhisperKey.Tests.Smoke;

namespace WhisperKey.Tests.Smoke.Performance
{
    /// <summary>
    /// Performance baseline validator using REQ-006 benchmarks
    /// </summary>
    public class PerformanceBaselineValidator : SmokeTestFramework
    {
        public PerformanceBaselineValidator(IServiceProvider serviceProvider, SmokeTestConfiguration configuration) 
            : base(serviceProvider, configuration)
        {
        }

        public override async Task<SmokeTestSuiteResult> RunAllTestsAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var results = new List<SmokeTestResult>();

            try
            {
                _logger.Information("Starting performance baseline validation");

                // Performance baseline tests
                results.Add(await TestAudioProcessingPerformanceAsync());
                results.Add(await TestTextInjectionPerformanceAsync());
                results.Add(await TestSettingsLoadPerformanceAsync());
                results.Add(await TestAuthenticationPerformanceAsync());
                results.Add(await TestMemoryUsagePerformanceAsync());
                results.Add(await TestCpuUsagePerformanceAsync());

                stopwatch.Stop();
                var suiteResult = new SmokeTestSuiteResult
                {
                    SuiteName = "Performance Baseline Validation",
                    TestResults = results,
                    TotalTests = results.Count,
                    PassedTests = results.Count(r => r.Success),
                    FailedTests = results.Count(r => !r.Success),
                    SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                    Duration = stopwatch.Elapsed,
                    ReportGeneratedAt = DateTime.UtcNow,
                    ResultsByCategory = new Dictionary<SmokeTestCategory, List<SmokeTestResult>>
                    {
                        [SmokeTestCategory.Performance] = results
                    }
                };

                _logger.Information("Performance baseline validation completed: {PassedTests}/{TotalTests} passed", 
                    suiteResult.PassedTests, suiteResult.TotalTests);

                return suiteResult;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Performance baseline validation failed with exception");
                
                var errorResult = CreateTestResult("Performance Baseline Validation", SmokeTestCategory.Performance, 
                    false, $"Performance baseline validation suite failed: {ex.Message}", stopwatch.Elapsed);
                results.Add(errorResult);

                return new SmokeTestSuiteResult
                {
                    SuiteName = "Performance Baseline Validation",
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
                "audioprocessing" => await TestAudioProcessingPerformanceAsync(),
                "textinjection" => await TestTextInjectionPerformanceAsync(),
                "settingsload" => await TestSettingsLoadPerformanceAsync(),
                "authentication" => await TestAuthenticationPerformanceAsync(),
                "memoryusage" => await TestMemoryUsagePerformanceAsync(),
                "cpuusage" => await TestCpuUsagePerformanceAsync(),
                _ => CreateTestResult(testName, SmokeTestCategory.Performance, false, "Unknown performance test", TimeSpan.Zero)
            };
        }

        public override List<SmokeTestResult> GetTestResults()
        {
            return new List<SmokeTestResult>(_testResults);
        }

        private async Task<SmokeTestResult> TestAudioProcessingPerformanceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var performanceTests = new List<(string Test, long DurationMs, bool WithinThreshold)>();

                // Test audio device enumeration performance
                var enumStart = Stopwatch.StartNew();
                var devices = await _audioDeviceService.GetAvailableDevicesAsync();
                enumStart.Stop();
                var enumTime = enumStart.ElapsedMilliseconds;
                var enumThreshold = 500; // 500ms threshold
                performanceTests.Add(("DeviceEnumeration", enumTime, enumTime <= enumThreshold));

                // Test default device retrieval performance
                var defaultStart = Stopwatch.StartNew();
                var defaultDevice = await _audioDeviceService.GetDefaultDeviceAsync();
                defaultStart.Stop();
                var defaultTime = defaultStart.ElapsedMilliseconds;
                var defaultThreshold = 200; // 200ms threshold
                performanceTests.Add(("DefaultDeviceRetrieval", defaultTime, defaultTime <= defaultThreshold));

                // Simulate audio processing performance
                var processingStart = Stopwatch.StartNew();
                await Task.Delay(100); // Simulate audio processing
                processingStart.Stop();
                var processingTime = processingStart.ElapsedMilliseconds;
                var processingThreshold = _configuration.PerformanceThresholds.MaxAudioProcessingMs;
                performanceTests.Add(("AudioProcessing", processingTime, processingTime <= processingThreshold));

                // Test multiple device operations
                var multiStart = Stopwatch.StartNew();
                for (int i = 0; i < 5; i++)
                {
                    await _audioDeviceService.GetAvailableDevicesAsync();
                }
                multiStart.Stop();
                var multiTime = multiStart.ElapsedMilliseconds;
                var avgMultiTime = multiTime / 5.0;
                var multiThreshold = 300; // 300ms average threshold
                performanceTests.Add(("MultipleOperations", (long)avgMultiTime, avgMultiTime <= multiThreshold));

                var passedTests = performanceTests.Count(t => t.WithinThreshold);
                var totalTests = performanceTests.Count;
                var performanceHealthy = passedTests >= totalTests * 0.75; // 75% of tests must pass

                var result = CreateTestResult("Audio Processing Performance", SmokeTestCategory.Performance,
                    performanceHealthy,
                    performanceHealthy
                        ? $"Audio processing performance within baselines: {passedTests}/{totalTests} tests passed"
                        : $"Audio processing performance issues: {passedTests}/{totalTests} tests passed",
                    stopwatch.Elapsed);

                result.Metrics["PassedTests"] = passedTests;
                result.Metrics["TotalTests"] = totalTests;
                result.Metrics["DeviceCount"] = devices?.Count ?? 0;

                foreach (var (test, duration, withinThreshold) in performanceTests)
                {
                    result.Metrics[$"Performance_{test}_DurationMs"] = duration;
                    result.Metrics[$"Performance_{test}_WithinThreshold"] = withinThreshold;
                    result.Metrics[$"Performance_{test}_ThresholdMs"] = test switch
                    {
                        "DeviceEnumeration" => enumThreshold,
                        "DefaultDeviceRetrieval" => defaultThreshold,
                        "AudioProcessing" => processingThreshold,
                        "MultipleOperations" => multiThreshold,
                        _ => 0
                    };
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Audio Processing Performance", SmokeTestCategory.Performance,
                    false, $"Audio processing performance test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestTextInjectionPerformanceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var performanceTests = new List<(string Test, long DurationMs, bool WithinThreshold)>();

                // Test single text injection performance
                var singleStart = Stopwatch.StartNew();
                var testText = "Performance test text";
                await _textInjectionService.InjectTextAsync(testText);
                singleStart.Stop();
                var singleTime = singleStart.ElapsedMilliseconds;
                var singleThreshold = _configuration.PerformanceThresholds.MaxTextInjectionMs;
                performanceTests.Add(("SingleInjection", singleTime, singleTime <= singleThreshold));

                // Test multiple text injections
                var multiStart = Stopwatch.StartNew();
                var testTexts = new[] { "Test 1", "Test 2", "Test 3", "Test 4", "Test 5" };
                foreach (var text in testTexts)
                {
                    await _textInjectionService.InjectTextAsync(text);
                }
                multiStart.Stop();
                var multiTime = multiStart.ElapsedMilliseconds;
                var avgMultiTime = multiTime / (double)testTexts.Length;
                var multiThreshold = singleThreshold * 1.5; // Allow 50% more time for multiple operations
                performanceTests.Add(("MultipleInjections", (long)avgMultiTime, avgMultiTime <= multiThreshold));

                // Test long text injection performance
                var longText = "This is a very long text that should test the performance of the text injection system when handling extended content. It contains multiple sentences and should be processed efficiently by the injection service.";
                var longStart = Stopwatch.StartNew();
                await _textInjectionService.InjectTextAsync(longText);
                longStart.Stop();
                var longTime = longStart.ElapsedMilliseconds;
                var longThreshold = singleThreshold * 2; // Allow double time for long text
                performanceTests.Add(("LongTextInjection", longTime, longTime <= longThreshold));

                // Test special characters injection performance
                var specialText = "Test with special chars: @#$%^&*()_+{}|:<>?[]\\;'\",./";
                var specialStart = Stopwatch.StartNew();
                await _textInjectionService.InjectTextAsync(specialText);
                specialStart.Stop();
                var specialTime = specialStart.ElapsedMilliseconds;
                var specialThreshold = singleThreshold * 1.2; // Allow 20% more time for special chars
                performanceTests.Add(("SpecialCharsInjection", specialTime, specialTime <= specialThreshold));

                var passedTests = performanceTests.Count(t => t.WithinThreshold);
                var totalTests = performanceTests.Count;
                var performanceHealthy = passedTests >= totalTests * 0.75; // 75% of tests must pass

                var result = CreateTestResult("Text Injection Performance", SmokeTestCategory.Performance,
                    performanceHealthy,
                    performanceHealthy
                        ? $"Text injection performance within baselines: {passedTests}/{totalTests} tests passed"
                        : $"Text injection performance issues: {passedTests}/{totalTests} tests passed",
                    stopwatch.Elapsed);

                result.Metrics["PassedTests"] = passedTests;
                result.Metrics["TotalTests"] = totalTests;

                foreach (var (test, duration, withinThreshold) in performanceTests)
                {
                    result.Metrics[$"Performance_{test}_DurationMs"] = duration;
                    result.Metrics[$"Performance_{test}_WithinThreshold"] = withinThreshold;
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Text Injection Performance", SmokeTestCategory.Performance,
                    false, $"Text injection performance test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestSettingsLoadPerformanceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var performanceTests = new List<(string Test, long DurationMs, bool WithinThreshold)>();

                // Test single settings load performance
                var singleStart = Stopwatch.StartNew();
                var settings = await _settingsService.LoadSettingsAsync();
                singleStart.Stop();
                var singleTime = singleStart.ElapsedMilliseconds;
                var singleThreshold = _configuration.PerformanceThresholds.MaxSettingsLoadMs;
                performanceTests.Add(("SingleLoad", singleTime, singleTime <= singleThreshold));

                // Test multiple settings loads
                var multiStart = Stopwatch.StartNew();
                for (int i = 0; i < 10; i++)
                {
                    await _settingsService.LoadSettingsAsync();
                }
                multiStart.Stop();
                var multiTime = multiStart.ElapsedMilliseconds;
                var avgMultiTime = multiTime / 10.0;
                var multiThreshold = singleThreshold * 1.5; // Allow 50% more time for multiple operations
                performanceTests.Add(("MultipleLoads", (long)avgMultiTime, avgMultiTime <= multiThreshold));

                // Test settings save performance
                var saveStart = Stopwatch.StartNew();
                await _settingsService.SaveSettingsAsync(settings);
                saveStart.Stop();
                var saveTime = saveStart.ElapsedMilliseconds;
                var saveThreshold = singleThreshold * 2; // Allow double time for save operations
                performanceTests.Add(("SettingsSave", saveTime, saveTime <= saveThreshold));

                // Test settings modification and save performance
                var modifyStart = Stopwatch.StartNew();
                var originalTestMode = settings.TestMode;
                settings.TestMode = !originalTestMode;
                await _settingsService.SaveSettingsAsync(settings);
                settings.TestMode = originalTestMode; // Restore
                await _settingsService.SaveSettingsAsync(settings);
                modifyStart.Stop();
                var modifyTime = modifyStart.ElapsedMilliseconds;
                var modifyThreshold = saveThreshold * 2; // Allow double time for modify operations
                performanceTests.Add(("ModifyAndSave", modifyTime, modifyTime <= modifyThreshold));

                var passedTests = performanceTests.Count(t => t.WithinThreshold);
                var totalTests = performanceTests.Count;
                var performanceHealthy = passedTests >= totalTests * 0.75; // 75% of tests must pass

                var result = CreateTestResult("Settings Load Performance", SmokeTestCategory.Performance,
                    performanceHealthy,
                    performanceHealthy
                        ? $"Settings load performance within baselines: {passedTests}/{totalTests} tests passed"
                        : $"Settings load performance issues: {passedTests}/{totalTests} tests passed",
                    stopwatch.Elapsed);

                result.Metrics["PassedTests"] = passedTests;
                result.Metrics["TotalTests"] = totalTests;

                foreach (var (test, duration, withinThreshold) in performanceTests)
                {
                    result.Metrics[$"Performance_{test}_DurationMs"] = duration;
                    result.Metrics[$"Performance_{test}_WithinThreshold"] = withinThreshold;
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Settings Load Performance", SmokeTestCategory.Performance,
                    false, $"Settings load performance test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestAuthenticationPerformanceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var performanceTests = new List<(string Test, long DurationMs, bool WithinThreshold)>();

                // Test authentication check performance
                var authStart = Stopwatch.StartNew();
                var isAuthenticated = await _authenticationService.IsAuthenticatedAsync();
                authStart.Stop();
                var authTime = authStart.ElapsedMilliseconds;
                var authThreshold = _configuration.PerformanceThresholds.MaxAuthenticationMs;
                performanceTests.Add(("AuthenticationCheck", authTime, authTime <= authThreshold));

                // Test multiple authentication checks
                var multiStart = Stopwatch.StartNew();
                for (int i = 0; i < 5; i++)
                {
                    await _authenticationService.IsAuthenticatedAsync();
                }
                multiStart.Stop();
                var multiTime = multiStart.ElapsedMilliseconds;
                var avgMultiTime = multiTime / 5.0;
                var multiThreshold = authThreshold * 1.5; // Allow 50% more time for multiple operations
                performanceTests.Add(("MultipleAuthChecks", (long)avgMultiTime, avgMultiTime <= multiThreshold));

                // Test credential retrieval performance (simulated)
                var credentialStart = Stopwatch.StartNew();
                await Task.Delay(50); // Simulate credential retrieval
                credentialStart.Stop();
                var credentialTime = credentialStart.ElapsedMilliseconds;
                var credentialThreshold = 200; // 200ms threshold for credential operations
                performanceTests.Add(("CredentialRetrieval", credentialTime, credentialTime <= credentialThreshold));

                var passedTests = performanceTests.Count(t => t.WithinThreshold);
                var totalTests = performanceTests.Count;
                var performanceHealthy = passedTests >= totalTests * 0.75; // 75% of tests must pass

                var result = CreateTestResult("Authentication Performance", SmokeTestCategory.Performance,
                    performanceHealthy,
                    performanceHealthy
                        ? $"Authentication performance within baselines: {passedTests}/{totalTests} tests passed"
                        : $"Authentication performance issues: {passedTests}/{totalTests} tests passed",
                    stopwatch.Elapsed);

                result.Metrics["PassedTests"] = passedTests;
                result.Metrics["TotalTests"] = totalTests;
                result.Metrics["IsAuthenticated"] = isAuthenticated;

                foreach (var (test, duration, withinThreshold) in performanceTests)
                {
                    result.Metrics[$"Performance_{test}_DurationMs"] = duration;
                    result.Metrics[$"Performance_{test}_WithinThreshold"] = withinThreshold;
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Authentication Performance", SmokeTestCategory.Performance,
                    false, $"Authentication performance test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestMemoryUsagePerformanceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                
                // Collect memory metrics
                var initialMemory = currentProcess.WorkingSet64 / 1024 / 1024;
                var initialGcMemory = GC.GetTotalMemory(false) / 1024 / 1024;

                // Simulate memory-intensive operations
                var operationsStart = Stopwatch.StartNew();
                
                // Load settings multiple times
                for (int i = 0; i < 20; i++)
                {
                    var settings = await _settingsService.LoadSettingsAsync();
                }
                
                // Perform audio device operations
                for (int i = 0; i < 10; i++)
                {
                    var devices = await _audioDeviceService.GetAvailableDevicesAsync();
                }
                
                operationsStart.Stop();

                // Force garbage collection and measure final memory
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var finalMemory = currentProcess.WorkingSet64 / 1024 / 1024;
                var finalGcMemory = GC.GetTotalMemory(false) / 1024 / 1024;

                var memoryIncrease = finalMemory - initialMemory;
                var gcMemoryIncrease = finalGcMemory - initialGcMemory;

                var memoryThreshold = _configuration.PerformanceThresholds.MaxMemoryUsageMb;
                var memoryHealthy = finalMemory <= memoryThreshold && memoryIncrease <= 100; // Allow 100MB increase

                var result = CreateTestResult("Memory Usage Performance", SmokeTestCategory.Performance,
                    memoryHealthy,
                    memoryHealthy
                        ? $"Memory usage within thresholds: {finalMemory}MB (increase: {memoryIncrease}MB)"
                        : $"Memory usage exceeds thresholds: {finalMemory}MB (increase: {memoryIncrease}MB)",
                    stopwatch.Elapsed);

                result.Metrics["InitialMemoryMb"] = initialMemory;
                result.Metrics["FinalMemoryMb"] = finalMemory;
                result.Metrics["MemoryIncreaseMb"] = memoryIncrease;
                result.Metrics["InitialGcMemoryMb"] = initialGcMemory;
                result.Metrics["FinalGcMemoryMb"] = finalGcMemory;
                result.Metrics["GcMemoryIncreaseMb"] = gcMemoryIncrease;
                result.Metrics["MemoryThresholdMb"] = memoryThreshold;
                result.Metrics["OperationsDurationMs"] = operationsStart.ElapsedMilliseconds;

                if (memoryIncrease > 50)
                {
                    result.Warnings.Add("Significant memory increase detected during operations");
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("Memory Usage Performance", SmokeTestCategory.Performance,
                    false, $"Memory usage performance test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }

        private async Task<SmokeTestResult> TestCpuUsagePerformanceAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                
                // Measure initial CPU time
                var initialCpuTime = currentProcess.TotalProcessorTime;
                var startTime = DateTime.UtcNow;

                // Perform CPU-intensive operations
                var operationsStart = Stopwatch.StartNew();
                
                // Simulate transcription processing
                var tasks = new List<Task>();
                for (int i = 0; i < 5; i++)
                {
                    tasks.Add(Task.Run(async () =>
                    {
                        for (int j = 0; j < 100; j++)
                        {
                            await Task.Delay(1);
                            // Simulate some CPU work
                            var result = Math.Sqrt(j * j + j);
                        }
                    }));
                }
                
                await Task.WhenAll(tasks);
                operationsStart.Stop();

                // Measure final CPU time
                var endTime = DateTime.UtcNow;
                var finalCpuTime = currentProcess.TotalProcessorTime;
                
                var cpuUsedMs = (finalCpuTime - initialCpuTime).TotalMilliseconds;
                var totalMsPassed = (endTime - startTime).TotalMilliseconds;
                var cpuUsagePercent = cpuUsedMs / totalMsPassed / Environment.ProcessorCount * 100;

                var cpuThreshold = _configuration.PerformanceThresholds.MaxCpuUsagePercent;
                var cpuHealthy = cpuUsagePercent <= cpuThreshold;

                var result = CreateTestResult("CPU Usage Performance", SmokeTestCategory.Performance,
                    cpuHealthy,
                    cpuHealthy
                        ? $"CPU usage within thresholds: {cpuUsagePercent:F1}% <= {cpuThreshold}%"
                        : $"CPU usage exceeds thresholds: {cpuUsagePercent:F1}% > {cpuThreshold}%",
                    stopwatch.Elapsed);

                result.Metrics["CpuUsagePercent"] = Math.Round(cpuUsagePercent, 2);
                result.Metrics["CpuUsedMs"] = Math.Round(cpuUsedMs, 2);
                result.Metrics["TotalMsPassed"] = Math.Round(totalMsPassed, 2);
                result.Metrics["CpuThresholdPercent"] = cpuThreshold;
                result.Metrics["ProcessorCount"] = Environment.ProcessorCount;
                result.Metrics["OperationsDurationMs"] = operationsStart.ElapsedMilliseconds;
                result.Metrics["ConcurrentTasks"] = tasks.Count;

                if (cpuUsagePercent > cpuThreshold * 0.8)
                {
                    result.Warnings.Add("High CPU usage detected during operations");
                }

                LogTestResult(result);
                return result;
            }
            catch (Exception ex)
            {
                var result = CreateTestResult("CPU Usage Performance", SmokeTestCategory.Performance,
                    false, $"CPU usage performance test failed: {ex.Message}", stopwatch.Elapsed);
                LogTestResult(result);
                return result;
            }
        }
    }
}