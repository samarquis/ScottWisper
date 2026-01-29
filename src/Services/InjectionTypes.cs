using System;
using System.Collections.Generic;

namespace ScottWisper
{
    /// <summary>
    /// Performance metrics for injection operations
    /// </summary>
    public class InjectionMetrics
    {
        public TimeSpan AverageLatency { get; set; }
        public double SuccessRate { get; set; }
        public int TotalAttempts { get; set; }
        public List<InjectionAttempt> RecentFailures { get; set; } = new();
    }

    /// <summary>
    /// User feedback report for injection issues
    /// </summary>
    public class InjectionIssuesReport
    {
        public int IssueCount { get; set; }
        public string OverallHealth { get; set; }
        public List<string> Recommendations { get; set; } = new List<string>();
        public List<string> Issues { get; set; } = new List<string>();
    }

    /// <summary>
    /// Result of injection test
    /// </summary>
    public class InjectionTestResult
    {
        public bool Success { get; set; }
        public string TestText { get; set; } = string.Empty;
        public string MethodUsed { get; set; } = string.Empty;
        public string[] Issues { get; set; } = Array.Empty<string>();
        public TimeSpan Duration { get; set; }
        public Services.WindowInfo ApplicationInfo { get; set; } = new();
        public Services.ApplicationCompatibility Compatibility { get; set; } = new();
    }

    /// <summary>
    /// Information about injection attempts for metrics tracking
    /// </summary>
    public class InjectionAttempt
    {
        public DateTime Timestamp { get; set; }
        public string AttemptedText { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string FailureReason { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public string TargetApplication { get; set; } = string.Empty;
    }
}