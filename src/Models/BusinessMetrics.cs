using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Represents a snapshot of business KPIs
    /// </summary>
    public class BusinessKpiSnapshot
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public int TotalTranscriptions { get; set; }
        public decimal TotalCost { get; set; }
        public double SuccessRate { get; set; }
        public int ActiveUsers { get; set; }
        public double AverageLatencyMs { get; set; }
        public int ErrorCount { get; set; }
    }

    /// <summary>
    /// Represents trend data for a specific metric over time
    /// </summary>
    public class MetricTrend
    {
        public string MetricName { get; set; } = string.Empty;
        public List<DataPoint> Points { get; set; } = new();
    }

    public class DataPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }
    }
}
