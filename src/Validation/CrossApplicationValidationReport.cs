using System;
using System.Collections.Generic;
using ScottWisper.Services;

namespace ScottWisper.Validation
{
    public class CrossApplicationValidationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public List<ApplicationValidationResult> ApplicationResults { get; set; } = new();
        public int TotalApplicationsTested { get; set; }
        public int SuccessfulApplications { get; set; }
        public double OverallSuccessRate { get; set; }
    }

    public class ApplicationValidationResult
    {
        public TargetApplication Application { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public double SuccessRate { get; set; }
        public double AccuracyScore { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public string? ErrorMessage { get; set; }
        public List<InjectionTestResult> TestResults { get; set; } = new();
    }
}
