using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ScottWisper.Services
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
        /// Detect currently active target application
        /// </summary>
        TargetApplication DetectActiveApplication();
    }

    /// <summary>
    /// Target applications for text injection (extended from ApplicationDetector)
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
        Notepad = 10
    }

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