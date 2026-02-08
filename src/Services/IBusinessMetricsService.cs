using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for tracking business KPIs and user behavior analytics
    /// </summary>
    public interface IBusinessMetricsService
    {
        /// <summary>
        /// Records a business event (e.g., transcription started, completed, failed)
        /// </summary>
        Task RecordEventAsync(string eventName, Dictionary<string, object>? data = null);
        
        /// <summary>
        /// Gets current KPI snapshot
        /// </summary>
        Task<BusinessKpiSnapshot> GetCurrentKpisAsync();
        
        /// <summary>
        /// Gets trend data for a metric
        /// </summary>
        Task<MetricTrend> GetTrendAsync(string metricName, TimeSpan duration);
        
        /// <summary>
        /// Generates a daily business report
        /// </summary>
        Task<string> GenerateDailyReportAsync();
    }
}
