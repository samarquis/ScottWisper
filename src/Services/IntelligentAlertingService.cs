using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Advanced intelligent alerting service implementation
    /// </summary>
    public class IntelligentAlertingService : IIntelligentAlertingService, IDisposable
    {
        private readonly ILogger<IntelligentAlertingService> _logger;
        private readonly IAuditLoggingService _auditService;
        private readonly IWebhookService _webhookService;
        private readonly ConcurrentDictionary<string, List<double>> _historicalMetrics = new();
        private const int MAX_HISTORY_POINTS = 100;
        private Timer? _analysisTimer;
        private readonly TimeSpan _analysisInterval = TimeSpan.FromMinutes(15);

        public IntelligentAlertingService(
            ILogger<IntelligentAlertingService> logger,
            IAuditLoggingService auditService,
            IWebhookService webhookService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _webhookService = webhookService ?? throw new ArgumentNullException(nameof(webhookService));
        }

        public void Start()
        {
            _logger.LogInformation("IntelligentAlertingService starting. Interval: {Interval}", _analysisInterval);
            _analysisTimer = new Timer(async _ => await AnalyzeSystemHealthAsync(), null, TimeSpan.Zero, _analysisInterval);
        }

        public void Stop()
        {
            _analysisTimer?.Change(Timeout.Infinite, 0);
            _logger.LogInformation("IntelligentAlertingService stopped.");
        }

        public void Dispose()
        {
            _analysisTimer?.Dispose();
        }

        /// <summary>
        /// Analyzes system health using predictive analytics
        /// </summary>
        public async Task AnalyzeSystemHealthAsync()
        {
            try
            {
                _logger.LogInformation("Starting predictive health analysis...");
                
                // Get logs from the last hour
                var cutoff = DateTime.UtcNow.AddHours(-1);
                var recentLogs = await _auditService.GetLogsAsync(startDate: cutoff);
                
                // Group by event type
                var countsByType = recentLogs
                    .GroupBy(l => l.EventType)
                    .ToDictionary(g => g.Key.ToString(), g => (double)g.Count());
                
                foreach (var (type, currentCount) in countsByType)
                {
                    var history = _historicalMetrics.GetOrAdd(type, _ => new List<double>());
                    
                    if (history.Count >= 10) // Need some data to be predictive
                    {
                        var average = history.Average();
                        var stdDev = CalculateStandardDeviation(history);
                        
                        // Detect anomaly (current count > avg + 2*stdDev)
                        if (currentCount > average + (stdDev * 2) && currentCount > 5)
                        {
                            await TriggerIntelligentAlert(
                                $"Anomaly detected for {type}",
                                $"Current count ({currentCount}) is significantly higher than historical average ({average:F1}).",
                                SecurityAlertSeverity.High,
                                AuditEventType.SecurityEvent);
                        }
                    }
                    
                    // Update history
                    lock (history)
                    {
                        history.Add(currentCount);
                        if (history.Count > MAX_HISTORY_POINTS)
                            history.RemoveAt(0);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during predictive health analysis");
            }
        }

        /// <summary>
        /// Performs automated root cause analysis
        /// </summary>
        public async Task<RootCauseAnalysisResult> PerformRootCauseAnalysisAsync(SecurityAlert alert)
        {
            var result = new RootCauseAnalysisResult
            {
                AlertId = alert.Id,
                AnalyzedAt = DateTime.UtcNow
            };

            try
            {
                // Look for contributing events in the same session or preceding 15 minutes
                var cutoff = alert.Timestamp.AddMinutes(-15);
                var relatedLogs = await _auditService.GetLogsAsync(startDate: cutoff, endDate: alert.Timestamp);
                
                // If there's an associated audit event, look for other events in that session
                if (!string.IsNullOrEmpty(alert.AuditEventId))
                {
                    var triggerEvent = relatedLogs.FirstOrDefault(l => l.Id == alert.AuditEventId);
                    if (triggerEvent != null && !string.IsNullOrEmpty(triggerEvent.SessionId))
                    {
                        result.ContributingEvents = relatedLogs
                            .Where(l => l.SessionId == triggerEvent.SessionId)
                            .OrderByDescending(l => l.Timestamp)
                            .Take(5)
                            .ToList();
                    }
                }
                
                if (!result.ContributingEvents.Any())
                {
                    result.ContributingEvents = relatedLogs
                        .OrderByDescending(l => l.Timestamp)
                        .Take(5)
                        .ToList();
                }

                // Analyze probable cause
                if (result.ContributingEvents.Any(e => e.EventType == AuditEventType.AuthenticationFailed))
                {
                    result.ProbableCause = "Multiple authentication failures suggesting a potential brute-force attempt.";
                    result.RecommendedAction = "Consider temporary account lockout or IP throttling.";
                    result.ConfidenceScore = 0.85;
                }
                else if (result.ContributingEvents.Any(e => e.EventType == AuditEventType.AuthorizationFailed))
                {
                    result.ProbableCause = "Repeated authorization failures suggesting unauthorized access attempts to restricted features.";
                    result.RecommendedAction = "Review user permissions and audit recent role changes.";
                    result.ConfidenceScore = 0.75;
                }
                else
                {
                    result.ProbableCause = "Unusual pattern of system events without a clear singular cause.";
                    result.RecommendedAction = "Manual investigation of recent audit logs required.";
                    result.ConfidenceScore = 0.40;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during root cause analysis for alert {AlertId}", alert.Id);
                result.ProbableCause = "Analysis failed due to internal error.";
            }

            return result;
        }

        /// <summary>
        /// Calculates dynamic thresholds based on historical data
        /// </summary>
        public Task<Dictionary<string, object>> CalculateDynamicThresholdAsync(string ruleId)
        {
            var history = _historicalMetrics.GetValueOrDefault(ruleId, new List<double>());
            
            var parameters = new Dictionary<string, object>();
            if (history.Count > 0)
            {
                var average = history.Average();
                var stdDev = CalculateStandardDeviation(history);
                
                parameters["dynamicCount"] = (int)Math.Ceiling(average + (stdDev * 3));
                parameters["isDynamic"] = true;
                parameters["calculatedAt"] = DateTime.UtcNow;
            }
            
            return Task.FromResult(parameters);
        }

        /// <summary>
        /// Escalates alert to configured webhooks
        /// </summary>
        public async Task EscalateAlertAsync(SecurityAlert alert)
        {
            try
            {
                var escalationData = new Dictionary<string, object>
                {
                    ["alertId"] = alert.Id,
                    ["ruleName"] = alert.RuleName,
                    ["severity"] = alert.Severity.ToString(),
                    ["description"] = alert.Description,
                    ["timestamp"] = alert.Timestamp.ToString("O"),
                    ["isEscalation"] = true
                };

                // Perform RCA before escalating for high/critical alerts
                if (alert.Severity >= SecurityAlertSeverity.High)
                {
                    var rca = await PerformRootCauseAnalysisAsync(alert);
                    escalationData["probableCause"] = rca.ProbableCause;
                    escalationData["recommendedAction"] = rca.RecommendedAction;
                    escalationData["rcaConfidence"] = rca.ConfidenceScore;
                }

                await _webhookService.SendWebhookAsync(WebhookEventType.SecurityEvent, escalationData);
                _logger.LogInformation("Alert {AlertId} escalated via WebhookService", alert.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to escalate alert {AlertId}", alert.Id);
            }
        }

        private async Task TriggerIntelligentAlert(string name, string description, SecurityAlertSeverity severity, AuditEventType type)
        {
            var alert = new SecurityAlert
            {
                Id = Guid.NewGuid().ToString(),
                RuleName = name,
                Description = description,
                Severity = severity,
                Type = type,
                Timestamp = DateTime.UtcNow
            };

            _logger.LogWarning("Intelligent alert triggered: {Name} - {Description}", name, description);
            
            // Log to audit service
            await _auditService.LogEventAsync(
                type,
                $"[INTELLIGENT ALERT] {description}",
                JsonSerializer.Serialize(alert),
                DataSensitivity.High);

            // Auto-escalate high severity alerts
            if (severity >= SecurityAlertSeverity.High)
            {
                await EscalateAlertAsync(alert);
            }
        }

        private static double CalculateStandardDeviation(IEnumerable<double> values)
        {
            var avg = values.Average();
            var sum = values.Sum(v => Math.Pow(v - avg, 2));
            return Math.Sqrt(sum / values.Count());
        }
    }
}
