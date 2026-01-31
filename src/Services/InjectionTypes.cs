using System;
using System.Collections.Generic;
using System.Linq;

namespace ScottWisper
{
    /// <summary>
    /// Target applications for text injection (reference from ApplicationDetector)
    /// </summary>
    public enum TargetApplication
    {
        Unknown = 0,
        Chrome = 1,
        Firefox = 2,
        Edge = 3,
        VisualStudio = 4,
        Word = 5,
        Outlook = 6,
        NotepadPlus = 7,
        WindowsTerminal = 8,
        CommandPrompt = 9,
        Notepad = 10,
        Excel = 11,
        PowerShell = 12,
        TextEditor = 13,
        Browser = 14,
        Office = 15,
        DevelopmentTool = 16,
        Terminal = 17
    }
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
        // Additional properties for compatibility
        public string ErrorMessage { get; set; } = string.Empty;
        public string ScenarioName { get; set; } = string.Empty;
        public TargetApplication Application { get; set; } = TargetApplication.Unknown;
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
        // Add ApplicationInfo property for compatibility
        public Services.WindowInfo ApplicationInfo { get; set; } = new();
        public string Method { get; set; } = string.Empty; // This property is already present, no change needed.
    }

    /// <summary>
    /// Cross-application validation result data
    /// </summary>
    public class CrossApplicationValidationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int TotalApplicationsTested { get; set; }
        public int SuccessfulApplications { get; set; }
        public double OverallSuccessRate { get; set; }
        public double CompatibilityScore { get; set; }
        public List<ApplicationValidationResult> ApplicationResults { get; set; } = new();
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets summary of validation results
        /// </summary>
        public string GetSummary()
        {
            var successful = ApplicationResults.Count(r => r.IsSuccess);
            var total = ApplicationResults.Count;
            var rate = total > 0 ? (double)successful / total * 100 : 0;
            
            return $"Cross-application validation: {successful}/{total} applications passed ({rate:F1}%)";
        }
    }

    /// <summary>
    /// Result of validation for a specific application
    /// </summary>
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
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<InjectionTestResult> TestResults { get; set; } = new();
        // Additional property for compatibility
        public string ApplicationName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Extended injection test result for validation
    /// </summary>
    public class InjectionTestResultExtended : InjectionTestResult
    {
        public new string ScenarioName { get; set; } = string.Empty;
        public new TargetApplication Application { get; set; }
    }

    /// <summary>
    /// Supported text injection methods
    /// </summary>
    public enum InjectionMethod
    {
        SendInput,
        Clipboard
    }

    /// <summary>
    /// Category of application for compatibility handling
    /// </summary>
    public enum ApplicationCategory
    {
        Unknown,
        WebBrowser,
        Browser,
        TextEditor,
        IDE,
        DevelopmentTool,
        Office,
        Terminal,
        Communication,
        Other
    }

    /// <summary>
    /// Compatibility mode for application profiles
    /// </summary>
    public enum CompatibilityMode
    {
        Standard,
        Browser,
        IDE,
        Office,
        Terminal,
        Communication
    }

    /// <summary>
    /// Level of special character support
    /// </summary>
    public enum SpecialCharacterSupport
    {
        Standard,
        Basic,
        Full
    }
}