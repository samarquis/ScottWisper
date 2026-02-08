using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Represents a performance metric data point
    /// </summary>
    public class PerformanceMetric
    {
        public string Name { get; set; } = string.Empty;
        public double Value { get; set; }
        public string Unit { get; set; } = "ms";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Represents a trace of an operation across service boundaries
    /// </summary>
    public class TraceEntry
    {
        public string TraceId { get; set; } = string.Empty;
        public string SpanId { get; set; } = string.Empty;
        public string ParentSpanId { get; set; } = string.Empty;
        public string OperationName { get; set; } = string.Empty;
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration => EndTime - StartTime;
        public bool IsError { get; set; }
        public Dictionary<string, string> Attributes { get; set; } = new();
    }

    /// <summary>
    /// Performance baseline for a specific operation
    /// </summary>
    public class PerformanceBaseline
    {
        public string OperationName { get; set; } = string.Empty;
        public double AverageDurationMs { get; set; }
        public double P95DurationMs { get; set; }
        public double StandardDeviation { get; set; }
        public int SampleCount { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
