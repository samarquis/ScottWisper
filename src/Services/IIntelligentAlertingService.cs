using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for advanced intelligent alerting service with predictive capabilities
    /// </summary>
    public interface IIntelligentAlertingService
    {
        /// <summary>
        /// Starts the background analysis and monitoring
        /// </summary>
        void Start();
        
        /// <summary>
        /// Stops the background analysis
        /// </summary>
        void Stop();

        /// <summary>
        /// Analyzes current system state and historical data to detect anomalies
        /// </summary>
        Task AnalyzeSystemHealthAsync();
        
        /// <summary>
        /// Performs automated root cause analysis for a given alert
        /// </summary>
        /// <param name="alert">The alert to analyze</param>
        /// <returns>A detailed analysis of the likely root cause</returns>
        Task<RootCauseAnalysisResult> PerformRootCauseAnalysisAsync(SecurityAlert alert);
        
        /// <summary>
        /// Calculates dynamic thresholds for alert rules based on historical averages
        /// </summary>
        /// <param name="ruleId">The rule identifier</param>
        /// <returns>Updated threshold parameters</returns>
        Task<Dictionary<string, object>> CalculateDynamicThresholdAsync(string ruleId);
        
        /// <summary>
        /// Escalates an alert to multiple channels (Slack, Email, etc.)
        /// </summary>
        /// <param name="alert">The alert to escalate</param>
        Task EscalateAlertAsync(SecurityAlert alert);
    }

    /// <summary>
    /// Result of an automated root cause analysis
    /// </summary>
    public class RootCauseAnalysisResult
    {
        public string AlertId { get; set; } = string.Empty;
        public string ProbableCause { get; set; } = string.Empty;
        public List<AuditLogEntry> ContributingEvents { get; set; } = new();
        public double ConfidenceScore { get; set; }
        public string RecommendedAction { get; set; } = string.Empty;
        public DateTime AnalyzedAt { get; set; } = DateTime.UtcNow;
    }
}
