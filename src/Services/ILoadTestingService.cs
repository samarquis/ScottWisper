using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for automated load testing and scalability validation
    /// </summary>
    public interface ILoadTestingService
    {
        /// <summary>
        /// Executes a load test by running many concurrent transcription operations
        /// </summary>
        /// <param name="concurrency">Number of concurrent operations</param>
        /// <param name="iterations">Number of iterations per operation</param>
        Task<LoadTestResult> RunTranscriptionLoadTestAsync(int concurrency, int iterations);
        
        /// <summary>
        /// Executes a stress test by rapidly triggering system events
        /// </summary>
        Task<LoadTestResult> RunEventStressTestAsync(int eventsPerSecond, TimeSpan duration);
        
        /// <summary>
        /// Gets the current system scalability metrics
        /// </summary>
        Task<ScalabilityMetrics> GetScalabilityMetricsAsync();
    }

    public class LoadTestResult
    {
        public bool Success { get; set; }
        public int TotalOperations { get; set; }
        public int SuccessfulOperations { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan P95Latency { get; set; }
        public double ThroughputPerSecond { get; set; }
        public string BottlenecksFound { get; set; } = string.Empty;
    }

    public class ScalabilityMetrics
    {
        public double MaxConcurrentTranscriptions { get; set; }
        public double ThreadPoolUtilization { get; set; }
        public double MemoryPressure { get; set; }
        public bool IsScalingHealthy { get; set; }
    }
}
