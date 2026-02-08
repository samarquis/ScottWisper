using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Represents a single occurrence of an error
    /// </summary>
    public class ErrorReport
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Message { get; set; } = string.Empty;
        public string? StackTrace { get; set; }
        public string? Source { get; set; }
        public ErrorReportSeverity Severity { get; set; }
        public string? CorrelationId { get; set; }
        public string ErrorHash { get; set; } = string.Empty;
    }

    /// <summary>
    /// Group of similar errors for deduplication
    /// </summary>
    public class ErrorGroup
    {
        public string ErrorHash { get; set; } = string.Empty;
        public string CommonMessage { get; set; } = string.Empty;
        public int OccurrenceCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public bool IsResolved { get; set; }
    }

    public enum ErrorReportSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
}
