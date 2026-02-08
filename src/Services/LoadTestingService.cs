using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of load and scalability testing service
    /// </summary>
    public class LoadTestingService : ILoadTestingService
    {
        private readonly ILogger<LoadTestingService> _logger;
        private readonly IWhisperService _whisperService;
        private readonly IPerformanceMonitoringService _performanceMonitoring;
        private readonly IAuditLoggingService _auditService;

        public LoadTestingService(
            ILogger<LoadTestingService> logger,
            IWhisperService whisperService,
            IPerformanceMonitoringService performanceMonitoring,
            IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _whisperService = whisperService ?? throw new ArgumentNullException(nameof(whisperService));
            _performanceMonitoring = performanceMonitoring ?? throw new ArgumentNullException(nameof(performanceMonitoring));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public async Task<LoadTestResult> RunTranscriptionLoadTestAsync(int concurrency, int iterations)
        {
            var result = new LoadTestResult();
            var sw = Stopwatch.StartNew();
            var latencies = new ConcurrentBag<double>();
            int successfulOps = 0;
            int totalOps = 0;
            
            _logger.LogInformation("Starting Transcription Load Test: Concurrency={Concurrency}, Iterations={Iterations}", concurrency, iterations);

            var tasks = Enumerable.Range(0, concurrency).Select(async c =>
            {
                for (int i = 0; i < iterations; i++)
                {
                    var opSw = Stopwatch.StartNew();
                    try
                    {
                        // Use a small dummy audio buffer
                        await _whisperService.TranscribeAudioAsync(new byte[500]);
                        opSw.Stop();
                        latencies.Add(opSw.Elapsed.TotalMilliseconds);
                        Interlocked.Increment(ref successfulOps);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Load test operation failed");
                    }
                    finally
                    {
                        Interlocked.Increment(ref totalOps);
                    }
                }
            });

            await Task.WhenAll(tasks);
            sw.Stop();

            result.SuccessfulOperations = successfulOps;
            result.TotalOperations = totalOps;
            result.AverageLatency = TimeSpan.FromMilliseconds(latencies.Any() ? latencies.Average() : 0);
            result.ThroughputPerSecond = result.TotalOperations / sw.Elapsed.TotalSeconds;
            result.Success = result.SuccessfulOperations > (result.TotalOperations * 0.95); // 95% success threshold

            await _auditService.LogEventAsync(
                AuditEventType.SystemEvent,
                $"Load test completed: {result.TotalOperations} operations at {result.ThroughputPerSecond:F1} ops/sec",
                null,
                DataSensitivity.Low);

            return result;
        }

        public async Task<LoadTestResult> RunEventStressTestAsync(int eventsPerSecond, TimeSpan duration)
        {
            // Simulate high-frequency audit logging
            var result = new LoadTestResult();
            var sw = Stopwatch.StartNew();
            var totalToRun = (int)(eventsPerSecond * duration.TotalSeconds);
            
            _logger.LogInformation("Starting Event Stress Test: {Rate} eps for {Duration}s", eventsPerSecond, duration.TotalSeconds);

            for (int i = 0; i < totalToRun; i++)
            {
                _ = _auditService.LogEventAsync(AuditEventType.SystemEvent, "Stress test event", null, DataSensitivity.Low);
                result.SuccessfulOperations++;
                result.TotalOperations++;
                
                if (i % eventsPerSecond == 0)
                    await Task.Delay(1000 / eventsPerSecond);
            }

            sw.Stop();
            result.Success = true;
            result.ThroughputPerSecond = result.TotalOperations / sw.Elapsed.TotalSeconds;
            
            return result;
        }

        public Task<ScalabilityMetrics> GetScalabilityMetricsAsync()
        {
            ThreadPool.GetAvailableThreads(out int workerThreads, out _);
            ThreadPool.GetMaxThreads(out int maxWorkerThreads, out _);

            var metrics = new ScalabilityMetrics
            {
                ThreadPoolUtilization = 1.0 - ((double)workerThreads / maxWorkerThreads),
                MemoryPressure = (double)GC.GetTotalMemory(false) / (1024 * 1024 * 1024), // GB
                IsScalingHealthy = workerThreads > (maxWorkerThreads * 0.1)
            };

            return Task.FromResult(metrics);
        }
    }
}
