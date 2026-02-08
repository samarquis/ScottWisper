using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for automated log analysis and intelligent insight generation
    /// </summary>
    public interface ILogAnalysisService
    {
        /// <summary>
        /// Analyzes recent logs to detect anomalies and patterns
        /// </summary>
        Task AnalyzeLogsAsync();
        
        /// <summary>
        /// Gets correlated logs for a specific correlation ID
        /// </summary>
        Task<List<LogEntry>> GetCorrelatedLogsAsync(string correlationId);
        
        /// <summary>
        /// Identifies common error patterns in recent logs
        /// </summary>
        Task<List<LogPattern>> IdentifyPatternsAsync();
        
        /// <summary>
        /// Generates automated insights based on log analysis
        /// </summary>
        Task<List<string>> GenerateInsightsAsync();
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string? Exception { get; set; }
        public string? CorrelationId { get; set; }
        public string? SourceContext { get; set; }
    }

    public class LogPattern
    {
        public string MessageTemplate { get; set; } = string.Empty;
        public int OccurrenceCount { get; set; }
        public DateTime FirstSeen { get; set; }
        public DateTime LastSeen { get; set; }
        public string Severity { get; set; } = "Information";
    }
}
