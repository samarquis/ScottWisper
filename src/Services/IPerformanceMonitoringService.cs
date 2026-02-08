using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for comprehensive application performance monitoring and distributed tracing
    /// </summary>
    public interface IPerformanceMonitoringService
    {
        /// <summary>
        /// Starts a new activity/span for tracing
        /// </summary>
        /// <param name="operationName">Name of the operation</param>
        /// <param name="parentActivity">Optional parent activity for nesting</param>
        /// <returns>The started activity</returns>
        Activity? StartActivity(string operationName, Activity? parentActivity = null);
        
        /// <summary>
        /// Records a single performance metric
        /// </summary>
        void RecordMetric(string name, double value, string unit = "ms", Dictionary<string, string>? tags = null);
        
        /// <summary>
        /// Gets the current performance baselines
        /// </summary>
        Task<List<PerformanceBaseline>> GetBaselinesAsync();
        
        /// <summary>
        /// Detects if an operation duration is an anomaly compared to baselines
        /// </summary>
        bool IsAnomaly(string operationName, TimeSpan duration);
        
        /// <summary>
        /// Generates a dependency map of services based on recent traces
        /// </summary>
        Task<Dictionary<string, List<string>>> GetServiceDependencyMapAsync();
    }
}
