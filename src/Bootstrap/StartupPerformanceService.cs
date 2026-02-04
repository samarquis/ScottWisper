using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using WhisperKey.Services;

namespace WhisperKey.Bootstrap
{
    /// <summary>
    /// Provides startup performance measurement and analysis capabilities.
    /// Enables monitoring of application initialization timing and dependency injection performance.
    /// </summary>
    public interface IStartupPerformanceService
    {
        /// <summary>
        /// Starts performance measurement for application initialization.
        /// Records start time and initializes measurement infrastructure.
        /// </summary>
        /// <param name="measurementName">The name of the startup phase being measured.</param>
        /// <returns>A disposable that will complete the measurement when disposed.</returns>
        IDisposable StartMeasurement(string measurementName);
        
        /// <summary>
        /// Records a checkpoint time for a specific startup operation.
        /// Used for measuring individual component initialization times.
        /// </summary>
        /// <param name="checkpointName">The name of the checkpoint or operation.</param>
        /// <param name="duration">The duration of the operation up to this checkpoint.</param>
        void RecordCheckpoint(string checkpointName, TimeSpan duration);
        
        /// <summary>
        /// Retrieves the complete startup performance report with analysis.
        /// Provides detailed timing breakdown and performance recommendations.
        /// </summary>
        /// <returns>A task that returns the complete performance analysis.</returns>
        Task<StartupPerformanceReport> GetPerformanceReportAsync();
        
        /// <summary>
        /// Validates that startup performance meets the required SLA of 2 seconds.
        /// Checks if total initialization time is within acceptable limits.
        /// </summary>
        /// <returns>True if startup is under 2 seconds, false otherwise.</returns>
        bool IsStartupPerformanceAcceptable();
    }

    /// <summary>
    /// Represents a single performance measurement checkpoint with timing data.
    /// Used for detailed analysis of startup performance bottlenecks.
    /// </summary>
    public class PerformanceCheckpoint
    {
        /// <summary>
        /// Gets or sets the name of the checkpoint or measurement.
        /// Identifies the specific operation or component being measured.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the elapsed time for this checkpoint.
        /// The duration from the start of the measurement to this checkpoint.
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Gets or sets the timestamp when this checkpoint was recorded.
        /// Used for chronological analysis of startup sequence.
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Gets or sets additional metadata about the checkpoint.
        /// Used for contextual information and analysis.
        /// </summary>
        public Dictionary<string, object> Metadata { get; set; } = new();
    }

    /// <summary>
    /// Represents the complete startup performance analysis with timing breakdown.
    /// Provides comprehensive view of initialization performance and recommendations.
    /// </summary>
    public class StartupPerformanceReport
    {
        /// <summary>
        /// Gets or sets the total startup duration from application start to completion.
        /// The primary metric for evaluating startup performance.
        /// </summary>
        public TimeSpan TotalDuration { get; set; }
        
        /// <summary>
        /// Gets or sets the individual checkpoint measurements.
        /// Detailed timing of each initialization phase.
        /// </summary>
        public List<PerformanceCheckpoint> Checkpoints { get; set; } = new();
        
        /// <summary>
        /// Gets or sets analysis of performance bottlenecks.
        /// Identifies the slowest initialization components.
        /// </summary>
        public List<string> Bottlenecks { get; set; } = new();
        
        /// <summary>
        /// Gets or sets performance improvement recommendations.
        /// Actionable suggestions for optimizing startup time.
        /// </summary>
        public List<string> Recommendations { get; set; } = new();
        
        /// <summary>
        /// Gets or sets whether the startup performance meets the 2-second SLA.
        /// Indicates if performance is acceptable for production use.
        /// </summary>
        public bool MeetsSLA { get; set; }
        
        /// <summary>
        /// Gets or sets the application start timestamp.
        /// Reference point for all duration calculations.
        /// </summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Implementation of startup performance measurement service with comprehensive timing analysis.
    /// Provides real-time measurement of application initialization phases.
    /// </summary>
    public class StartupPerformanceService : IStartupPerformanceService
    {
        private readonly ILogger<StartupPerformanceService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly List<PerformanceCheckpoint> _checkpoints = new();
        private readonly Stopwatch _totalStopwatch = new();
        private bool _measurementStarted = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="StartupPerformanceService"/> class.
        /// Sets up measurement infrastructure and prepares for performance tracking.
        /// </summary>
        /// <param name="logger">The logger for performance tracking and debugging. Must not be null.</param>
        /// <param name="serviceProvider">The service provider for dependency resolution analysis. Must not be null.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        public StartupPerformanceService(ILogger<StartupPerformanceService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// Starts performance measurement for application initialization.
        /// </summary>
        /// <param name="measurementName">The name of the startup phase being measured.</param>
        /// <returns>A disposable that completes the measurement when disposed.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="measurementName"/> is null or empty.</exception>
        public IDisposable StartMeasurement(string measurementName)
        {
            if (string.IsNullOrWhiteSpace(measurementName))
                throw new ArgumentException("Measurement name cannot be null or empty.", nameof(measurementName));

            if (!_measurementStarted)
            {
                _totalStopwatch.Start();
                _measurementStarted = true;
                _logger.LogInformation("Startup performance measurement started: {MeasurementName}", measurementName);
            }

            return new PerformanceMeasurementDisposable(this, measurementName);
        }

        /// <summary>
        /// Records a checkpoint time for a specific startup operation.
        /// </summary>
        /// <param name="checkpointName">The name of the checkpoint or operation.</param>
        /// <param name="duration">The duration of the operation up to this checkpoint.</param>
        public void RecordCheckpoint(string checkpointName, TimeSpan duration)
        {
            var checkpoint = new PerformanceCheckpoint
            {
                Name = checkpointName,
                Duration = duration,
                Timestamp = DateTime.UtcNow,
                Metadata = new Dictionary<string, object>
                {
                    ["ServiceCount"] = GetServiceCount().ToString(),
                    ["MemoryUsage"] = GC.GetTotalMemory(false).ToString()
                }
            };

            _checkpoints.Add(checkpoint);
            _logger.LogDebug("Performance checkpoint recorded: {CheckpointName} - {Duration}ms", 
                checkpointName, duration.TotalMilliseconds);
        }

        /// <summary>
        /// Retrieves the complete startup performance report with analysis.
        /// </summary>
        public async Task<StartupPerformanceReport> GetPerformanceReportAsync()
        {
            return await Task.Run(() =>
            {
                var totalDuration = _totalStopwatch.Elapsed;
                
                // Analyze checkpoints for bottlenecks
                var bottlenecks = _checkpoints
                    .OrderByDescending(c => c.Duration)
                    .Take(3)
                    .Select(c => $"{c.Name}: {c.Duration.TotalMilliseconds:F1}ms")
                    .ToList();

                // Generate recommendations
                var recommendations = GenerateRecommendations();

                var report = new StartupPerformanceReport
                {
                    TotalDuration = totalDuration,
                    Checkpoints = new List<PerformanceCheckpoint>(_checkpoints),
                    Bottlenecks = bottlenecks,
                    Recommendations = recommendations,
                    MeetsSLA = totalDuration.TotalSeconds <= 2.0,
                    StartTime = DateTime.UtcNow.Add(-totalDuration)
                };

                _logger.LogInformation("Startup performance report generated: {TotalDuration}ms, SLA: {MeetsSLA}", 
                    totalDuration.TotalMilliseconds, report.MeetsSLA);

                return report;
            });
        }

        /// <summary>
        /// Validates that startup performance meets the required SLA of 2 seconds.
        /// </summary>
        public bool IsStartupPerformanceAcceptable()
        {
            return _totalStopwatch.Elapsed.TotalSeconds <= 2.0;
        }

        private int GetServiceCount()
        {
            var serviceTypes = new[]
            {
                typeof(ISettingsService),
                typeof(IAudioDeviceService),
                typeof(IWebhookService),
                typeof(ITextInjection),
                typeof(IPermissionService),
                typeof(ICommandProcessingService)
            };

            return serviceTypes.Count(type => _serviceProvider.GetService(type) != null);
        }

        private List<string> GenerateRecommendations()
        {
            var recommendations = new List<string>();
            var slowCheckpoints = _checkpoints.Where(c => c.Duration.TotalMilliseconds > 500).ToList();

            if (!IsStartupPerformanceAcceptable())
            {
                recommendations.Add("Consider lazy loading for heavy services to reduce startup time");
            }

            if (slowCheckpoints.Any())
            {
                var slowest = slowCheckpoints.OrderByDescending(c => c.Duration).First();
                recommendations.Add($"Optimize {slowest.Name} initialization (took {slowest.Duration.TotalMilliseconds:F1}ms)");
            }

            if (_checkpoints.Count > 10)
            {
                recommendations.Add("Consider reducing the number of registered services");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Startup performance is optimal - no major optimizations needed");
            }

            return recommendations;
        }

        private class PerformanceMeasurementDisposable : IDisposable
        {
            private readonly StartupPerformanceService _parent;
            private readonly string _measurementName;
            private readonly Stopwatch _stopwatch;

            public PerformanceMeasurementDisposable(StartupPerformanceService parent, string measurementName)
            {
                _parent = parent;
                _measurementName = measurementName;
                _stopwatch = Stopwatch.StartNew();
            }

            public void Dispose()
            {
                var duration = _stopwatch.Elapsed;
                _stopwatch.Stop();
                _parent.RecordCheckpoint(_measurementName, duration);
            }
        }
    }
}