using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Window information for injection targeting
    /// </summary>
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string WindowTitle { get; set; } = string.Empty;
        public bool HasFocus { get; set; }
        public int ProcessId { get; set; }
    }

    /// <summary>
    /// Universal text injection service interface
    /// </summary>
    public interface ITextInjection
    {
        /// <summary>
        /// Injects text at current cursor position
        /// </summary>
        Task<bool> InjectTextAsync(string text, InjectionOptions? options = null);
        
        /// <summary>
        /// Initializes injection service
        /// </summary>
        Task<bool> InitializeAsync();
        
        /// <summary>
        /// Cleanup resources
        /// </summary>
        void Dispose();
        
        /// <summary>
        /// Test injection functionality
        /// </summary>
        Task<InjectionTestResult> TestInjectionAsync();
        
        /// <summary>
        /// Enable or disable debug mode
        /// </summary>
        void SetDebugMode(bool enabled);
        
        /// <summary>
        /// Get injection performance metrics
        /// </summary>
        InjectionMetrics GetInjectionMetrics();

        /// <summary>
        /// Get injection performance metrics (alias for compatibility)
        /// </summary>
        InjectionMetrics GetPerformanceMetrics() => GetInjectionMetrics();
        
        /// <summary>
        /// Get injection issues report
        /// </summary>
        InjectionIssuesReport GetInjectionIssuesReport();
        
        /// <summary>
        /// Check if current environment is compatible
        /// </summary>
        bool IsInjectionCompatible();
        
        /// <summary>
        /// Get current window information for injection targeting
        /// </summary>
        WindowInfo GetCurrentWindowInfo();
        
        /// <summary>
        /// Detects currently active target application
        /// </summary>
        TargetApplication DetectActiveApplication();
        
        /// <summary>
        /// Validates cross-application text injection compatibility
        /// </summary>
        Task<CrossApplicationValidationResult> ValidateCrossApplicationInjectionAsync();
        
        /// <summary>
        /// Gets application compatibility for a specific process
        /// </summary>
        ApplicationCompatibility GetApplicationCompatibility(string processName);
    }

    // Use existing TargetApplication enum from root namespace to avoid conflicts

    /// <summary>
    /// Basic injection options
    /// </summary>
    public class InjectionOptions
    {
        public bool UseClipboardFallback { get; set; } = false;
        public int RetryCount { get; set; } = 3;
        public int DelayBetweenRetriesMs { get; set; } = 100;
        public int DelayBetweenCharsMs { get; set; } = 5;
        public bool RespectExistingText { get; set; } = true;
        // Additional properties for compatibility
        public string Method { get; set; } = "SendInput";
        public bool UseUnicode { get; set; } = true;
        public bool ValidateUnicode { get; set; } = true;
    }

    /// <summary>
    /// Application compatibility information
    /// </summary>
    public class ApplicationCompatibility
    {
        public ApplicationCategory Category { get; set; }
        public bool IsCompatible { get; set; }
        public InjectionMethod PreferredMethod { get; set; }
        public string[] RequiresSpecialHandling { get; set; } = Array.Empty<string>();
        public Dictionary<string, object> ApplicationSettings { get; set; } = new();
        public bool SupportsUnicode { get; set; } = true;
        public bool SupportsLineBreaks { get; set; } = true;
        public bool SupportsTabs { get; set; } = true;
        public int RecommendedDelayBetweenChars { get; set; } = 5;
        public int MaxTextLength { get; set; } = 1000;
    }

    /// <summary>
    /// Injection strategy enumeration
    /// </summary>
    public enum InjectionStrategy
    {
        SendInput,
        ClipboardFallback,
        Unicode,
        SlowUnicode
    }
}