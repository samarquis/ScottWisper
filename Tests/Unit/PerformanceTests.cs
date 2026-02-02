using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics.CodeAnalysis;

namespace WhisperKey.Tests.Unit
{
    /// <summary>
    /// Comprehensive performance testing framework for WhisperKey Phase 1 validation
    /// </summary>
    public class PerformanceTests
    {
        private readonly AudioCaptureService _audioCaptureService;
        private readonly WhisperService _whisperService;
        private readonly HotkeyService _hotkeyService;
        private readonly SystemTrayService _systemTrayService;
        private readonly List<PerformanceResult> _results = new();

        public PerformanceTests(AudioCaptureService audioCaptureService, WhisperService whisperService, HotkeyService hotkeyService)
        {
            _audioCaptureService = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
            _whisperService = whisperService ?? throw new ArgumentNullException(nameof(whisperService));
            _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
            _systemTrayService = new SystemTrayService();
        }

        public PerformanceTests(AudioCaptureService audioCaptureService, WhisperService whisperService, HotkeyService hotkeyService, SystemTrayService systemTrayService)
        {
            _audioCaptureService = audioCaptureService ?? throw new ArgumentNullException(nameof(audioCaptureService));
            _whisperService = whisperService ?? throw new ArgumentNullException(nameof(whisperService));
            _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
            _systemTrayService = systemTrayService ?? throw new ArgumentNullException(nameof(systemTrayService));
        }

        /// <summary>
        /// Measures end-to-end latency from hotkey activation to text display
        /// </summary>
        public async Task<LatencyMetrics> MeasureLatency(int iterations = 10)
        {
            var latencyMeasurements = new List<double>();

            for (int i = 0; i < iterations; i++)
            {
                var measurement = await MeasureSingleLatencyIteration();
                latencyMeasurements.Add(measurement.TotalLatency);
                
                // Wait between iterations to avoid API rate limiting
                await Task.Delay(1000);
            }

            var metrics = new LatencyMetrics
            {
                Measurements = latencyMeasurements,
                AverageLatency = latencyMeasurements.Average(),
                MinLatency = latencyMeasurements.Min(),
                MaxLatency = latencyMeasurements.Max(),
                P95Latency = CalculatePercentile(latencyMeasurements, 95),
                P99Latency = CalculatePercentile(latencyMeasurements, 99),
                TotalIterations = iterations,
                SuccessRate = (double)latencyMeasurements.Count(m => m < 100) / iterations * 100 // Under 100ms target
            };

            _results.Add(new PerformanceResult
            {
                TestType = "Latency",
                Timestamp = DateTime.UtcNow,
                Metrics = metrics,
                Success = metrics.AverageLatency < 100
            });

            return metrics;
        }

        private async Task<LatencyMeasurement> MeasureSingleLatencyIteration()
        {
            var stopwatch = Stopwatch.StartNew();
            
            // Step 1: Simulate hotkey activation to audio capture start
            var hotkeyStartTime = stopwatch.ElapsedMilliseconds;
            await _audioCaptureService.StartCaptureAsync();
            var captureStartTime = stopwatch.ElapsedMilliseconds;

            // Step 2: Record audio for a short duration (2 seconds)
            await Task.Delay(2000);
            
            var captureStopTime = stopwatch.ElapsedMilliseconds;
            await _audioCaptureService.StopCaptureAsync();
            
            // Step 3: Send audio data to transcription service (simulated)
            var audioData = _audioCaptureService.GetCapturedAudio();
            if (audioData == null || audioData.Length == 0)
            {
                throw new InvalidOperationException("No audio data captured");
            }

            var apiSubmitTime = stopwatch.ElapsedMilliseconds;
            var transcription = await _whisperService.TranscribeAudioAsync(audioData);
            var apiResponseTime = stopwatch.ElapsedMilliseconds;

            stopwatch.Stop();

            return new LatencyMeasurement
            {
                HotkeyToCapture = captureStartTime - hotkeyStartTime,
                CaptureToAPI = apiSubmitTime - captureStopTime,
                APIToResponse = apiResponseTime - apiSubmitTime,
                TotalLatency = apiResponseTime - hotkeyStartTime,
                TranscriptionText = transcription
            };
        }

        /// <summary>
        /// Validates transcription accuracy using test audio samples
        /// </summary>
        public async Task<AccuracyMetrics> ValidateAccuracy()
        {
            var testCases = GetTestCases();
            var accuracyResults = new List<AccuracyResult>();

            foreach (var testCase in testCases)
            {
                try
                {
                    var result = await TestSingleAccuracyCase(testCase);
                    accuracyResults.Add(result);
                }
                catch (Exception ex)
                {
                    accuracyResults.Add(new AccuracyResult
                    {
                        TestCaseName = testCase.Name,
                        ExpectedText = testCase.ExpectedText,
                        ActualText = "",
                        WordErrorRate = 1.0, // 100% error for failed test
                        Accuracy = 0.0,
                        Error = ex.Message
                    });
                }

                // Rate limiting between API calls
                await Task.Delay(500);
            }

            var metrics = new AccuracyMetrics
            {
                Results = accuracyResults,
                AverageAccuracy = accuracyResults.Average(r => r.Accuracy),
                AverageWER = accuracyResults.Average(r => r.WordErrorRate),
                MinAccuracy = accuracyResults.Min(r => r.Accuracy),
                MaxAccuracy = accuracyResults.Max(r => r.Accuracy),
                SuccessRate = (double)accuracyResults.Count(r => r.Accuracy >= 95) / accuracyResults.Count * 100,
                TotalTests = accuracyResults.Count
            };

            _results.Add(new PerformanceResult
            {
                TestType = "Accuracy",
                Timestamp = DateTime.UtcNow,
                Metrics = metrics,
                Success = metrics.AverageAccuracy >= 95
            });

            return metrics;
        }

        private async Task<AccuracyResult> TestSingleAccuracyCase(TestCase testCase)
        {
            // Use the audio file if it exists, otherwise simulate with generated audio
            byte[] audioData;
            if (File.Exists(testCase.AudioFilePath))
            {
                audioData = await File.ReadAllBytesAsync(testCase.AudioFilePath);
            }
            else
            {
                // Generate a simple test audio file (this would be replaced with actual test audio)
                audioData = GenerateTestAudioData(testCase.DurationSeconds);
            }

            var stopwatch = Stopwatch.StartNew();
            var transcription = await _whisperService.TranscribeAudioAsync(audioData, testCase.Language);
            stopwatch.Stop();

            var wordErrorRate = CalculateWordErrorRate(testCase.ExpectedText, transcription);
            var accuracy = Math.Max(0, 100 - wordErrorRate * 100);

            return new AccuracyResult
            {
                TestCaseName = testCase.Name,
                ExpectedText = testCase.ExpectedText,
                ActualText = transcription,
                WordErrorRate = wordErrorRate,
                Accuracy = accuracy,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                AudioDataSize = audioData.Length
            };
        }

        /// <summary>
        /// Tests application stability over extended periods
        /// </summary>
        public async Task<StabilityMetrics> TestStability(TimeSpan testDuration)
        {
            var startTime = DateTime.UtcNow;
            var memorySnapshots = new List<MemorySnapshot>();
            var operationCount = 0;
            var errors = new List<string>();

            // Get initial memory state
            var initialMemory = GC.GetTotalMemory(false);

            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    var operationStart = DateTime.UtcNow;
                    
                    // Perform a complete dictation operation
                    await _audioCaptureService.StartCaptureAsync();
                    await Task.Delay(1000); // Record for 1 second
                    await _audioCaptureService.StopCaptureAsync();
                    
                    var audioData = _audioCaptureService.GetCapturedAudio();
                    if (audioData != null && audioData.Length > 0)
                    {
                        await _whisperService.TranscribeAudioAsync(audioData);
                    }
                    
                    operationCount++;
                    
                    // Take memory snapshot every 10 operations
                    if (operationCount % 10 == 0)
                    {
                        var currentMemory = GC.GetTotalMemory(false);
                        memorySnapshots.Add(new MemorySnapshot
                        {
                            Timestamp = DateTime.UtcNow,
                            MemoryBytes = currentMemory,
                            OperationCount = operationCount
                        });
                    }

                    // Clear audio buffer
                    _audioCaptureService.ClearCapturedAudio();
                    
                    // Brief pause between operations
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    errors.Add($"Operation {operationCount}: {ex.Message}");
                }
            }

            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            var metrics = new StabilityMetrics
            {
                TestDuration = testDuration,
                TotalOperations = operationCount,
                SuccessfulOperations = operationCount - errors.Count,
                FailedOperations = errors.Count,
                InitialMemoryBytes = initialMemory,
                FinalMemoryBytes = finalMemory,
                MemoryIncreaseBytes = memoryIncrease,
                MemoryIncreaseMB = memoryIncrease / (1024.0 * 1024.0),
                MemorySnapshots = memorySnapshots,
                Errors = errors,
                SuccessRate = operationCount > 0 ? (double)(operationCount - errors.Count) / operationCount * 100 : 0
            };

            _results.Add(new PerformanceResult
            {
                TestType = "Stability",
                Timestamp = DateTime.UtcNow,
                Metrics = metrics,
                Success = metrics.MemoryIncreaseMB < 50 && metrics.SuccessRate > 95 // Less than 50MB memory increase, >95% success rate
            });

            return metrics;
        }

        /// <summary>
        /// Validates cost tracking and free tier sustainability
        /// </summary>
        public async Task<CostValidationMetrics> ValidateCostTracking()
        {
            var testScenarios = GetCostTestScenarios();
            var validationResults = new List<CostValidationResult>();

            foreach (var scenario in testScenarios)
            {
                try
                {
                    var result = await TestCostScenario(scenario);
                    validationResults.Add(result);
                }
                catch (Exception ex)
                {
                    validationResults.Add(new CostValidationResult
                    {
                        ScenarioName = scenario.Name,
                        Success = false,
                        Error = ex.Message
                    });
                }
            }

            var metrics = new CostValidationMetrics
            {
                Results = validationResults,
                SuccessRate = validationResults.Average(r => r.Success ? 1.0 : 0.0) * 100,
                AverageTrackingAccuracy = validationResults.Where(r => r.Success).Average(r => r.TrackingAccuracy),
                TotalScenarios = validationResults.Count
            };

            _results.Add(new PerformanceResult
            {
                TestType = "CostValidation",
                Timestamp = DateTime.UtcNow,
                Metrics = metrics,
                Success = metrics.SuccessRate >= 95
            });

            return metrics;
        }

        private async Task<CostValidationResult> TestCostScenario(CostTestScenario scenario)
        {
            var costTrackingService = new CostTrackingService();
            var expectedCost = 0.0m;

            foreach (var audioSize in scenario.AudioDataSizes)
            {
                expectedCost += CalculateExpectedCost(audioSize);
                costTrackingService.TrackUsage(audioSize, true);
                await Task.Delay(100); // Small delay between operations
            }

            var actualStats = costTrackingService.GetUsageStats();
            var trackingAccuracy = expectedCost > 0 ? Math.Abs((double)(expectedCost - actualStats.EstimatedCost)) / (double)expectedCost : 1.0;
            trackingAccuracy = Math.Max(0, 1 - trackingAccuracy); // Convert to percentage

            return new CostValidationResult
            {
                ScenarioName = scenario.Name,
                ExpectedCost = expectedCost,
                ActualCost = actualStats.EstimatedCost,
                TrackingAccuracy = trackingAccuracy,
                RequestCount = actualStats.RequestCount,
                Success = Math.Abs(expectedCost - actualStats.EstimatedCost) < 0.001m // Within $0.001 tolerance
            };
        }

        /// <summary>
        /// Tests system tray performance under various conditions
        /// </summary>
        public async Task<SystemTrayPerformanceMetrics> TestSystemTrayPerformance()
        {
            if (_systemTrayService == null)
                throw new InvalidOperationException("SystemTrayService not initialized");

            // Initialize system tray
            _systemTrayService.Initialize();
            
            var startTime = DateTime.UtcNow;
            
            // Test 1: Status update performance
            var statusUpdateTimes = new List<long>();
            var statuses = Enum.GetValues<SystemTrayService.TrayStatus>();
            
            for (int i = 0; i < 100; i++)
            {
                var status = statuses[i % statuses.Length];
                var stopwatch = Stopwatch.StartNew();
                _systemTrayService.UpdateStatus(status);
                stopwatch.Stop();
                statusUpdateTimes.Add(stopwatch.ElapsedMilliseconds);
                await Task.Delay(10);
            }

            // Test 2: Notification performance
            var notificationTimes = new List<long>();
            for (int i = 0; i < 50; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                _systemTrayService.ShowNotification($"Test notification {i}", "Performance Test");
                stopwatch.Stop();
                notificationTimes.Add(stopwatch.ElapsedMilliseconds);
                await Task.Delay(50);
            }

            // Test 3: Memory usage during operation
            var memorySnapshots = new List<MemorySnapshot>();
            var initialMemory = GC.GetTotalMemory(false);
            
            for (int i = 0; i < 20; i++)
            {
                // Simulate typical system tray operations
                _systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                _systemTrayService.UpdateDictationStatus(true);
                _systemTrayService.ShowNotification($"Cycle {i}", "Memory test");
                _systemTrayService.UpdateDictationStatus(false);
                
                var currentMemory = GC.GetTotalMemory(false);
                memorySnapshots.Add(new MemorySnapshot
                {
                    Timestamp = DateTime.UtcNow,
                    MemoryBytes = currentMemory,
                    OperationCount = i
                });
                
                await Task.Delay(100);
            }

            // Test 4: High-frequency updates
            var highFreqTimes = new List<long>();
            var highFreqStart = DateTime.UtcNow;
            
            while (DateTime.UtcNow - highFreqStart < TimeSpan.FromSeconds(5))
            {
                var stopwatch = Stopwatch.StartNew();
                _systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Recording);
                _systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Processing);
                _systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                stopwatch.Stop();
                highFreqTimes.Add(stopwatch.ElapsedMilliseconds);
                await Task.Delay(10);
            }

            var finalMemory = GC.GetTotalMemory(false);
            var memoryIncrease = finalMemory - initialMemory;

            var metrics = new SystemTrayPerformanceMetrics
            {
                AverageStatusUpdateTime = statusUpdateTimes.Average(),
                MaxStatusUpdateTime = statusUpdateTimes.Max(),
                MinStatusUpdateTime = statusUpdateTimes.Min(),
                AverageNotificationTime = notificationTimes.Average(),
                MaxNotificationTime = notificationTimes.Max(),
                MinNotificationTime = notificationTimes.Min(),
                MemoryIncreaseBytes = memoryIncrease,
                MemoryIncreaseMB = memoryIncrease / (1024.0 * 1024.0),
                HighFrequencyUpdateCount = highFreqTimes.Count,
                AverageHighFreqUpdateTime = highFreqTimes.Count > 0 ? highFreqTimes.Average() : 0,
                MemorySnapshots = memorySnapshots,
                TotalTestDuration = DateTime.UtcNow - startTime,
                StatusUpdateResponsiveness = (double)statusUpdateTimes.Count(t => t < 100) / statusUpdateTimes.Count * 100,
                NotificationResponsiveness = notificationTimes.Count > 0 ? 
                    (double)notificationTimes.Count(t => t < 50) / notificationTimes.Count * 100 : 0
            };

            _results.Add(new PerformanceResult
            {
                TestType = "SystemTrayPerformance",
                Timestamp = DateTime.UtcNow,
                Metrics = metrics,
                Success = metrics.AverageStatusUpdateTime < 100 && 
                          metrics.AverageNotificationTime < 50 && 
                          metrics.MemoryIncreaseMB < 10
            });

            return metrics;
        }

        /// <summary>
        /// Tests long-term system tray stability and resource management
        /// </summary>
        public async Task<SystemTrayStabilityMetrics> TestLongTermStability()
        {
            if (_systemTrayService == null)
                throw new InvalidOperationException("SystemTrayService not initialized");

            _systemTrayService.Initialize();
            
            var testDuration = TimeSpan.FromMinutes(5); // Reduced for automated testing
            var startTime = DateTime.UtcNow;
            var operationCount = 0;
            var errors = new List<string>();
            var memoryLeakData = new List<long>();
            var responsivenessData = new List<bool>();

            var initialMemory = GC.GetTotalMemory(false);
            var lastCheckTime = startTime;

            while (DateTime.UtcNow - startTime < testDuration)
            {
                try
                {
                    var operationStart = DateTime.UtcNow;
                    
                    // Perform typical system tray operations
                    var random = new Random();
                    var operation = random.Next(0, 4);
                    
                    switch (operation)
                    {
                        case 0:
                            _systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                            break;
                        case 1:
                            _systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Recording);
                            _systemTrayService.UpdateDictationStatus(true);
                            break;
                        case 2:
                            _systemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Processing);
                            _systemTrayService.UpdateDictationStatus(false);
                            break;
                        case 3:
                            _systemTrayService.ShowNotification($"Stability test {operationCount}", "Long-term test");
                            break;
                    }
                    
                    var operationEnd = DateTime.UtcNow;
                    var operationLatency = (operationEnd - operationStart).TotalMilliseconds;
                    responsivenessData.Add(operationLatency < 100);
                    
                    operationCount++;
                    
                    // Check memory every 10 operations
                    if (operationCount % 10 == 0)
                    {
                        var currentMemory = GC.GetTotalMemory(false);
                        memoryLeakData.Add(currentMemory);
                        
                        // Check for potential memory leaks
                        var memoryIncrease = currentMemory - initialMemory;
                        if (memoryIncrease > 50 * 1024 * 1024) // 50MB increase threshold
                        {
                            errors.Add($"Potential memory leak detected at operation {operationCount}: {memoryIncrease / 1024.0 / 1024.0:F2}MB increase");
                        }
                        
                        lastCheckTime = DateTime.UtcNow;
                    }
                    
                    await Task.Delay(200); // 5 operations per second
                }
                catch (Exception ex)
                {
                    errors.Add($"Operation {operationCount}: {ex.Message}");
                }
            }

            var finalMemory = GC.GetTotalMemory(false);
            var totalMemoryIncrease = finalMemory - initialMemory;

            var metrics = new SystemTrayStabilityMetrics
            {
                TestDuration = testDuration,
                TotalOperations = operationCount,
                SuccessfulOperations = operationCount - errors.Count,
                FailedOperations = errors.Count,
                ErrorRate = operationCount > 0 ? (double)errors.Count / operationCount * 100 : 0,
                ResponsivenessRate = responsivenessData.Count > 0 ? 
                    (double)responsivenessData.Count(r => r) / responsivenessData.Count * 100 : 0,
                InitialMemoryBytes = initialMemory,
                FinalMemoryBytes = finalMemory,
                MemoryIncreaseBytes = totalMemoryIncrease,
                MemoryIncreaseMB = totalMemoryIncrease / (1024.0 * 1024.0),
                MemoryLeakData = memoryLeakData,
                Errors = errors,
                SuccessRate = operationCount > 0 ? (double)(operationCount - errors.Count) / operationCount * 100 : 0,
                ResourceCleanupEfficiency = CalculateResourceCleanupEfficiency(memoryLeakData)
            };

            _results.Add(new PerformanceResult
            {
                TestType = "SystemTrayStability",
                Timestamp = DateTime.UtcNow,
                Metrics = metrics,
                Success = metrics.ErrorRate < 5 && 
                          metrics.ResponsivenessRate > 95 && 
                          metrics.MemoryIncreaseMB < 50
            });

            return metrics;
        }

        /// <summary>
        /// Runs all performance tests and returns comprehensive report
        /// </summary>
        public async Task<PerformanceTestReport> RunAllTests()
        {
            var report = new PerformanceTestReport
            {
                StartTime = DateTime.UtcNow,
                TestResults = new List<PerformanceResult>()
            };

            try
            {
                // Run latency tests
                Console.WriteLine("Running latency tests...");
                report.LatencyMetrics = await MeasureLatency();
                report.TestResults.Add(_results.Last());

                // Run accuracy tests
                Console.WriteLine("Running accuracy tests...");
                report.AccuracyMetrics = await ValidateAccuracy();
                report.TestResults.Add(_results.Last());

                // Run stability tests (10 minutes)
                Console.WriteLine("Running stability tests (10 minutes)...");
                report.StabilityMetrics = await TestStability(TimeSpan.FromMinutes(10));
                report.TestResults.Add(_results.Last());

                // Run system tray performance tests
                Console.WriteLine("Running system tray performance tests...");
                report.SystemTrayPerformanceMetrics = await TestSystemTrayPerformance();
                report.TestResults.Add(_results.Last());

                // Run system tray stability tests
                Console.WriteLine("Running system tray stability tests...");
                report.SystemTrayStabilityMetrics = await TestLongTermStability();
                report.TestResults.Add(_results.Last());

                // Run cost validation tests
                Console.WriteLine("Running cost validation tests...");
                report.CostValidationMetrics = await ValidateCostTracking();
                report.TestResults.Add(_results.Last());
            }
            catch (Exception ex)
            {
                report.ErrorMessage = ex.Message;
                report.Success = false;
            }
            finally
            {
                report.EndTime = DateTime.UtcNow;
                report.Duration = report.EndTime - report.StartTime;
                report.Success = report.TestResults.All(r => r.Success) && string.IsNullOrEmpty(report.ErrorMessage);
                
                // Cleanup system tray service
                _systemTrayService?.Dispose();
            }

            return report;
        }

        public List<PerformanceResult> GetResults() => _results.ToList();

        private double CalculatePercentile(List<double> values, int percentile)
        {
            if (values.Count == 0) return 0;
            
            var sorted = values.OrderBy(x => x).ToList();
            var index = (int)Math.Ceiling(sorted.Count * percentile / 100.0) - 1;
            index = Math.Max(0, Math.Min(sorted.Count - 1, index));
            return sorted[index];
        }

        private double CalculateWordErrorRate(string expected, string actual)
        {
            var expectedWords = expected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var actualWords = actual.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            // Simple WER calculation using Levenshtein distance approximation
            var distance = CalculateLevenshteinDistance(expectedWords, actualWords);
            return (double)distance / Math.Max(expectedWords.Length, 1);
        }

        private int CalculateLevenshteinDistance(string[] a, string[] b)
        {
            var matrix = new int[a.Length + 1, b.Length + 1];

            for (int i = 0; i <= a.Length; i++)
                matrix[i, 0] = i;

            for (int j = 0; j <= b.Length; j++)
                matrix[0, j] = j;

            for (int i = 1; i <= a.Length; i++)
            {
                for (int j = 1; j <= b.Length; j++)
                {
                    var cost = a[i - 1].Equals(b[j - 1], StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                    matrix[i, j] = Math.Min(
                        Math.Min(matrix[i - 1, j] + 1, matrix[i, j - 1] + 1),
                        matrix[i - 1, j - 1] + cost);
                }
            }

            return matrix[a.Length, b.Length];
        }

        private List<TestCase> GetTestCases()
        {
            return new List<TestCase>
            {
                new TestCase
                {
                    Name = "Clear English Speech",
                    ExpectedText = "Hello world, this is a test of the speech recognition system.",
                    AudioFilePath = "test_audio_clear_speech.wav",
                    Language = "en",
                    DurationSeconds = 3
                },
                new TestCase
                {
                    Name = "Numbers and Technical Terms",
                    ExpectedText = "The API endpoint returns HTTP 200 status code with JSON response.",
                    AudioFilePath = "test_audio_technical.wav",
                    Language = "en",
                    DurationSeconds = 4
                },
                new TestCase
                {
                    Name = "Short Command",
                    ExpectedText = "Save file now.",
                    AudioFilePath = "test_audio_command.wav",
                    Language = "en",
                    DurationSeconds = 2
                }
            };
        }

        private List<CostTestScenario> GetCostTestScenarios()
        {
            return new List<CostTestScenario>
            {
                new CostTestScenario
                {
                    Name = "Light Usage (30 min/day)",
                    AudioDataSizes = new[] { 48000, 96000, 144000 } // 1.5, 3, 4.5 seconds of audio
                },
                new CostTestScenario
                {
                    Name = "Medium Usage (2 hours/day)",
                    AudioDataSizes = Enumerable.Repeat(96000, 120).ToArray() // 120 x 3-second chunks = 6 minutes
                },
                new CostTestScenario
                {
                    Name = "Heavy Usage (3 hours/day)",
                    AudioDataSizes = Enumerable.Repeat(96000, 180).ToArray() // 180 x 3-second chunks = 9 minutes
                }
            };
        }

        private byte[] GenerateTestAudioData(int durationSeconds)
        {
            // Generate simple sine wave audio data for testing
            // 16kHz, 16-bit, mono
            var sampleRate = 16000;
            var duration = durationSeconds;
            var sampleCount = sampleRate * duration;
            var bytesPerSample = 2;
            var audioData = new byte[sampleCount * bytesPerSample];

            var random = new Random(42); // Fixed seed for reproducible tests
            for (int i = 0; i < sampleCount; i++)
            {
                // Generate simple audio data (would be replaced with actual test audio)
                var sample = (short)(random.NextShort() / 2); // Reduce amplitude
                var bytes = BitConverter.GetBytes(sample);
                Array.Copy(bytes, 0, audioData, i * bytesPerSample, bytesPerSample);
            }

            return audioData;
        }

        private decimal CalculateExpectedCost(int audioDataLength)
        {
            const decimal costPerMinute = 0.006m;
            const int bytesPerSecond = 32000; // 16kHz, 16-bit, mono
            
            var durationMinutes = (double)audioDataLength / bytesPerSecond / 60.0;
            return (decimal)durationMinutes * costPerMinute;
        }

        private double CalculateResourceCleanupEfficiency(List<long> memorySnapshots)
        {
            if (memorySnapshots.Count < 3) return 100.0; // Perfect if insufficient data

            // Calculate memory trend
            var firstHalf = memorySnapshots.Take(memorySnapshots.Count / 2).ToList();
            var secondHalf = memorySnapshots.Skip(memorySnapshots.Count / 2).ToList();

            var firstHalfAvg = firstHalf.Average();
            var secondHalfAvg = secondHalf.Average();

            // Efficiency based on memory growth trend (lower growth = higher efficiency)
            var memoryGrowthRate = (secondHalfAvg - firstHalfAvg) / Math.Max(firstHalfAvg, 1);
            var efficiency = Math.Max(0, 100 - (memoryGrowthRate * 1000)); // Scale growth to efficiency

            return Math.Min(100, efficiency);
        }
    }

    // Data models
    public class PerformanceResult
    {
        public string TestType { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public object Metrics { get; set; } = null!;
        public bool Success { get; set; }
    }

    public class LatencyMetrics
    {
        public List<double> Measurements { get; set; } = new();
        public double AverageLatency { get; set; }
        public double MinLatency { get; set; }
        public double MaxLatency { get; set; }
        public double P95Latency { get; set; }
        public double P99Latency { get; set; }
        public int TotalIterations { get; set; }
        public double SuccessRate { get; set; } // Percentage under 100ms
    }

    public class LatencyMeasurement
    {
        public long HotkeyToCapture { get; set; }
        public long CaptureToAPI { get; set; }
        public long APIToResponse { get; set; }
        public long TotalLatency { get; set; }
        public string TranscriptionText { get; set; } = string.Empty;
    }

    public class AccuracyMetrics
    {
        public List<AccuracyResult> Results { get; set; } = new();
        public double AverageAccuracy { get; set; }
        public double AverageWER { get; set; }
        public double MinAccuracy { get; set; }
        public double MaxAccuracy { get; set; }
        public double SuccessRate { get; set; } // Percentage meeting 95% accuracy
        public int TotalTests { get; set; }
    }

    public class AccuracyResult
    {
        public string TestCaseName { get; set; } = string.Empty;
        public string ExpectedText { get; set; } = string.Empty;
        public string ActualText { get; set; } = string.Empty;
        public double WordErrorRate { get; set; }
        public double Accuracy { get; set; }
        public long ProcessingTimeMs { get; set; }
        public int AudioDataSize { get; set; }
        public string? Error { get; set; }
    }

    public class StabilityMetrics
    {
        public TimeSpan TestDuration { get; set; }
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public long InitialMemoryBytes { get; set; }
        public long FinalMemoryBytes { get; set; }
        public long MemoryIncreaseBytes { get; set; }
        public double MemoryIncreaseMB { get; set; }
        public List<MemorySnapshot> MemorySnapshots { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public double SuccessRate { get; set; }
    }

    public class MemorySnapshot
    {
        public DateTime Timestamp { get; set; }
        public long MemoryBytes { get; set; }
        public int OperationCount { get; set; }
    }

    public class CostValidationMetrics
    {
        public List<CostValidationResult> Results { get; set; } = new();
        public double SuccessRate { get; set; }
        public double AverageTrackingAccuracy { get; set; }
        public int TotalScenarios { get; set; }
    }

    public class CostValidationResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public decimal ExpectedCost { get; set; }
        public decimal ActualCost { get; set; }
        public double TrackingAccuracy { get; set; }
        public int RequestCount { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    public class PerformanceTestReport
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public List<PerformanceResult> TestResults { get; set; } = new();
        public LatencyMetrics? LatencyMetrics { get; set; }
        public AccuracyMetrics? AccuracyMetrics { get; set; }
        public StabilityMetrics? StabilityMetrics { get; set; }
        public CostValidationMetrics? CostValidationMetrics { get; set; }
        public SystemTrayPerformanceMetrics? SystemTrayPerformanceMetrics { get; set; }
        public SystemTrayStabilityMetrics? SystemTrayStabilityMetrics { get; set; }
    }

    public class TestCase
    {
        public string Name { get; set; } = string.Empty;
        public string ExpectedText { get; set; } = string.Empty;
        public string AudioFilePath { get; set; } = string.Empty;
        public string Language { get; set; } = "en";
        public int DurationSeconds { get; set; }
    }

    public class CostTestScenario
    {
        public string Name { get; set; } = string.Empty;
        public int[] AudioDataSizes { get; set; } = Array.Empty<int>();
    }

    // System Tray Performance Metrics Classes
    public class SystemTrayPerformanceMetrics
    {
        public double AverageStatusUpdateTime { get; set; }
        public double MaxStatusUpdateTime { get; set; }
        public double MinStatusUpdateTime { get; set; }
        public double AverageNotificationTime { get; set; }
        public double MaxNotificationTime { get; set; }
        public double MinNotificationTime { get; set; }
        public long MemoryIncreaseBytes { get; set; }
        public double MemoryIncreaseMB { get; set; }
        public int HighFrequencyUpdateCount { get; set; }
        public double AverageHighFreqUpdateTime { get; set; }
        public List<MemorySnapshot> MemorySnapshots { get; set; } = new();
        public TimeSpan TotalTestDuration { get; set; }
        public double StatusUpdateResponsiveness { get; set; } // Percentage of updates < 100ms
        public double NotificationResponsiveness { get; set; } // Percentage of notifications < 50ms
    }

    public class SystemTrayStabilityMetrics
    {
        public TimeSpan TestDuration { get; set; }
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public int FailedOperations { get; set; }
        public double ErrorRate { get; set; } // Percentage of failed operations
        public double ResponsivenessRate { get; set; } // Percentage of operations < 100ms latency
        public long InitialMemoryBytes { get; set; }
        public long FinalMemoryBytes { get; set; }
        public long MemoryIncreaseBytes { get; set; }
        public double MemoryIncreaseMB { get; set; }
        public List<long> MemoryLeakData { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public double SuccessRate { get; set; } // Overall success rate
        public double ResourceCleanupEfficiency { get; set; } // Memory leak prevention score
    }

    // Extension method for random short generation
    public static class RandomExtensions
    {
        public static short NextShort(this Random random)
        {
            return (short)(random.Next(short.MinValue, short.MaxValue + 1));
        }
    }
}