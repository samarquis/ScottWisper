using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of APM and Distributed Tracing service
    /// </summary>
    public class PerformanceMonitoringService : IPerformanceMonitoringService, IDisposable
    {
        private static readonly ActivitySource _activitySource = new("WhisperKey.APM", "1.0.0");
        private readonly ILogger<PerformanceMonitoringService> _logger;
        private readonly IAuditLoggingService _auditService;
        private readonly ConcurrentDictionary<string, ConcurrentQueue<double>> _metricsHistory = new();
        private readonly ConcurrentDictionary<string, PerformanceBaseline> _baselines = new();
        private readonly ActivityListener _activityListener;
        private const int MAX_SAMPLES = 1000;

        public PerformanceMonitoringService(
            ILogger<PerformanceMonitoringService> logger,
            IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));

            // Initialize activity listener to capture completed spans for baseline updates
            _activityListener = new ActivityListener
            {
                ShouldListenTo = (source) => source.Name == "WhisperKey.APM",
                Sample = (ref ActivityCreationOptions<ActivityContext> options) => ActivitySamplingResult.AllData,
                ActivityStopped = OnActivityStopped
            };
            ActivitySource.AddActivityListener(_activityListener);
        }

        public Activity? StartActivity(string operationName, Activity? parentActivity = null)
        {
            return _activitySource.StartActivity(operationName, ActivityKind.Internal, parentActivity?.Context ?? default);
        }

        public void RecordMetric(string name, double value, string unit = "ms", Dictionary<string, string>? tags = null)
        {
            var queue = _metricsHistory.GetOrAdd(name, _ => new ConcurrentQueue<double>());
            queue.Enqueue(value);
            
            // Trim if too large
            while (queue.Count > MAX_SAMPLES)
            {
                queue.TryDequeue(out _);
            }

            if (tags?.Count > 0)
            {
                _logger.LogTrace("Metric: {Name} = {Value}{Unit} Tags: {@Tags}", name, value, unit, tags);
            }
            else
            {
                _logger.LogTrace("Metric: {Name} = {Value}{Unit}", name, value, unit);
            }
        }

        public Task<List<PerformanceBaseline>> GetBaselinesAsync()
        {
            return Task.FromResult(_baselines.Values.ToList());
        }

        public bool IsAnomaly(string operationName, TimeSpan duration)
        {
            if (!_baselines.TryGetValue(operationName, out var baseline))
                return false;

            // Anomaly if > Avg + 3*StdDev
            var threshold = baseline.AverageDurationMs + (baseline.StandardDeviation * 3);
            return duration.TotalMilliseconds > threshold && baseline.SampleCount > 10;
        }

        public Task<Dictionary<string, List<string>>> GetServiceDependencyMapAsync()
        {
            // Simple map for now - in a real app this would be derived from traces
            var map = new Dictionary<string, List<string>>
            {
                ["TranscriptionWindow"] = new List<string> { "WhisperService", "CostTrackingService" },
                ["WhisperService"] = new List<string> { "LocalInferenceService", "ApiKeyManagementService" },
                ["ApplicationBootstrapper"] = new List<string> { "AudioCaptureService", "TextInjectionService", "SettingsService" }
            };
            return Task.FromResult(map);
        }

        private async void OnActivityStopped(Activity activity)
        {
            try
            {
                var duration = activity.Duration.TotalMilliseconds;
                UpdateBaseline(activity.OperationName, duration);

                if (IsAnomaly(activity.OperationName, activity.Duration))
                {
                    _logger.LogWarning("Performance anomaly detected: {Operation} took {Duration}ms", 
                        activity.OperationName, duration);

                    await _auditService.LogEventAsync(
                        AuditEventType.SystemEvent,
                        $"[PERFORMANCE ANOMALY] {activity.OperationName} took {duration:F1}ms",
                        System.Text.Json.JsonSerializer.Serialize(new
                        {
                            activity.OperationName,
                            DurationMs = duration,
                            activity.TraceId,
                            activity.SpanId,
                            BaselineAvg = _baselines[activity.OperationName].AverageDurationMs
                        }),
                        DataSensitivity.Medium);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing stopped activity");
            }
        }

        private void UpdateBaseline(string operationName, double durationMs)
        {
            _baselines.AddOrUpdate(operationName, 
                _ => new PerformanceBaseline 
                { 
                    OperationName = operationName, 
                    AverageDurationMs = durationMs, 
                    SampleCount = 1,
                    LastUpdated = DateTime.UtcNow 
                },
                (_, existing) =>
                {
                    // Incremental average calculation
                    var newCount = existing.SampleCount + 1;
                    var oldAvg = existing.AverageDurationMs;
                    var newAvg = oldAvg + (durationMs - oldAvg) / newCount;
                    
                    // Simple incremental standard deviation approximation
                    var newStdDev = Math.Abs(durationMs - newAvg) * 0.1 + existing.StandardDeviation * 0.9;

                    existing.AverageDurationMs = newAvg;
                    existing.StandardDeviation = newStdDev;
                    existing.SampleCount = newCount;
                    existing.LastUpdated = DateTime.UtcNow;
                    return existing;
                });
        }

        public void Dispose()
        {
            _activityListener.Dispose();
        }
    }
}
