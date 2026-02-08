using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;
using System.Text.Json;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for security alerting service
    /// </summary>
    public interface ISecurityAlertService
    {
        /// <summary>
        /// Check an audit event against alert rules
        /// </summary>
        Task CheckEventAsync(AuditLogEntry auditEvent);
        
        /// <summary>
        /// Add or update an alert rule
        /// </summary>
        Task ConfigureAlertRuleAsync(SecurityAlertRule rule);
        
        /// <summary>
        /// Get all alert rules
        /// </summary>
        Task<List<SecurityAlertRule>> GetAlertRulesAsync();
        
        /// <summary>
        /// Get recent security alerts
        /// </summary>
        Task<List<SecurityAlert>> GetRecentAlertsAsync(int hours = 24);
        
        /// <summary>
        /// Get alert statistics
        /// </summary>
        Task<SecurityAlertStatistics> GetAlertStatisticsAsync();
        
        /// <summary>
        /// Clear old alerts
        /// </summary>
        Task<int> ClearOldAlertsAsync(int daysOld);
    }

    /// <summary>
    /// Security alerting service for real-time threat detection
    /// </summary>
    public class SecurityAlertService : ISecurityAlertService
    {
        private readonly ILogger<SecurityAlertService> _logger;
        private readonly IAuditLoggingService _auditService;
        private readonly List<SecurityAlertRule> _alertRules;
        private readonly ConcurrentQueue<SecurityAlert> _alerts;
        private readonly Timer _alertCleanupTimer;
        
        public SecurityAlertService(ILogger<SecurityAlertService> logger, IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _alertRules = new List<SecurityAlertRule>();
            _alerts = new ConcurrentQueue<SecurityAlert>();
            
            // Initialize default alert rules
            InitializeDefaultAlertRules();
            
            // Subscribe to audit events for real-time alerting
            _auditService.EventLogged += OnAuditLogged;
            
            // Start cleanup timer (runs every hour)
            _alertCleanupTimer = new Timer(CleanupOldAlerts, null, TimeSpan.FromHours(1), TimeSpan.FromHours(1));
            
            _logger.LogInformation("SecurityAlertService initialized with {RuleCount} default rules", _alertRules.Count);
        }

        private static readonly AsyncLocal<bool> _isProcessingLocal = new AsyncLocal<bool>();

        /// <summary>
        /// Event handler for audit log entries
        /// </summary>
        private async void OnAuditLogged(object? sender, AuditLogEntry entry)
        {
            if (_isProcessingLocal.Value) return;
            
            try
            {
                _isProcessingLocal.Value = true;
                await CheckEventAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing audit event for alerting");
            }
            finally
            {
                _isProcessingLocal.Value = false;
            }
        }
        
        /// <summary>
        /// Check an audit event against all alert rules
        /// </summary>
        public async Task CheckEventAsync(AuditLogEntry auditEvent)
        {
            if (auditEvent == null) return;
            
            try
            {
                foreach (var rule in _alertRules.Where(r => r.IsActive))
                {
                    if (await ShouldTriggerAlert(rule, auditEvent))
                    {
                        await TriggerAlert(rule, auditEvent);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking audit event against alert rules");
            }
        }
        
        /// <summary>
        /// Configure an alert rule
        /// </summary>
        public async Task ConfigureAlertRuleAsync(SecurityAlertRule rule)
        {
            if (rule == null) throw new ArgumentNullException(nameof(rule));
            
            lock (_alertRules)
            {
                var existing = _alertRules.FirstOrDefault(r => r.Id == rule.Id);
                if (existing != null)
                {
                    _alertRules.Remove(existing);
                }
                
                rule.ModifiedAt = DateTime.UtcNow;
                _alertRules.Add(rule);
            }
            
            await _auditService.LogEventAsync(
                AuditEventType.SecurityEvent,
                $"Alert rule configured: {rule.Name}",
                JsonSerializer.Serialize(new { RuleId = rule.Id, RuleName = rule.Name }),
                DataSensitivity.Medium);
            
            _logger.LogInformation("Alert rule configured: {RuleName}", rule.Name);
        }
        
        /// <summary>
        /// Get all alert rules
        /// </summary>
        public async Task<List<SecurityAlertRule>> GetAlertRulesAsync()
        {
            return await Task.FromResult(_alertRules.ToList());
        }
        
        /// <summary>
        /// Get recent security alerts
        /// </summary>
        public async Task<List<SecurityAlert>> GetRecentAlertsAsync(int hours = 24)
        {
            var cutoff = DateTime.UtcNow.AddHours(-hours);
            return await Task.FromResult(_alerts.Where(a => a.Timestamp >= cutoff).ToList());
        }
        
        /// <summary>
        /// Get alert statistics
        /// </summary>
        public async Task<SecurityAlertStatistics> GetAlertStatisticsAsync()
        {
            var stats = new SecurityAlertStatistics
            {
                GeneratedAt = DateTime.UtcNow
            };
            
            var allAlerts = _alerts.ToList();
            stats.TotalAlerts = allAlerts.Count;
            
            var cutoff24h = DateTime.UtcNow.AddHours(-24);
            stats.AlertsLast24Hours = allAlerts.Count(a => a.Timestamp >= cutoff24h);
            
            stats.AlertsBySeverity = allAlerts
                .GroupBy(a => a.Severity)
                .ToDictionary(g => g.Key, g => g.Count());
                
            stats.AlertsByType = allAlerts
                .GroupBy(a => a.Type)
                .ToDictionary(g => g.Key, g => g.Count());
            
            if (allAlerts.Any())
            {
                stats.MostRecentAlert = allAlerts.OrderByDescending(a => a.Timestamp).First().Timestamp;
                stats.OldestAlert = allAlerts.OrderBy(a => a.Timestamp).First().Timestamp;
            }
            
            return stats;
        }
        
        /// <summary>
        /// Clear old alerts
        /// </summary>
        public async Task<int> ClearOldAlertsAsync(int daysOld)
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysOld);
            var oldAlerts = _alerts.Where(a => a.Timestamp < cutoff).ToList();
            
            // Remove old alerts from queue
            var newQueue = new ConcurrentQueue<SecurityAlert>();
            var remainingAlerts = _alerts.Where(a => a.Timestamp >= cutoff);
            
            foreach (var alert in remainingAlerts)
            {
                newQueue.Enqueue(alert);
            }
            
            // Replace the queue (atomic operation)
            while (_alerts.TryDequeue(out _)) { } // Clear old queue
            foreach (var alert in newQueue)
            {
                _alerts.Enqueue(alert);
            }
            
            var clearedCount = oldAlerts.Count;
            if (clearedCount > 0)
            {
                await _auditService.LogEventAsync(
                    AuditEventType.SecurityEvent,
                    $"Cleared {clearedCount} old security alerts",
                    JsonSerializer.Serialize(new { DaysOld = daysOld, ClearedCount = clearedCount }),
                    DataSensitivity.Low);
            }
            
            return clearedCount;
        }
        
        /// <summary>
        /// Initialize default security alert rules
        /// </summary>
        private void InitializeDefaultAlertRules()
        {
            // Multiple failed permission attempts
            _alertRules.Add(new SecurityAlertRule
            {
                Id = "failed-permissions-5min",
                Name = "Multiple Failed Permission Attempts",
                Description = "Alert on 3+ failed permission attempts within 5 minutes",
                EventType = AuditEventType.SecurityEvent,
                Severity = SecurityAlertSeverity.Medium,
                Condition = SecurityAlertCondition.CountInTimeWindow,
                Parameters = new Dictionary<string, object>
                {
                    ["count"] = 3,
                    ["timeWindowMinutes"] = 5,
                    ["descriptionFilter"] = "failed"
                },
                CooldownMinutes = 15,
                IsActive = true
            });
            
            // API key access from unusual context
            _alertRules.Add(new SecurityAlertRule
            {
                Id = "api-key-unusual-access",
                Name = "Unusual API Key Access",
                Description = "Alert on API key access outside normal hours",
                EventType = AuditEventType.ApiKeyAccessed,
                Severity = SecurityAlertSeverity.High,
                Condition = SecurityAlertCondition.OutOfHours,
                Parameters = new Dictionary<string, object>
                {
                    ["startHour"] = 9, // 9 AM
                    ["endHour"] = 17    // 5 PM
                },
                CooldownMinutes = 30,
                IsActive = true
            });
            
            // Security event burst
            _alertRules.Add(new SecurityAlertRule
            {
                Id = "security-event-burst",
                Name = "Security Event Burst",
                Description = "Alert on 10+ security events within 1 minute",
                EventType = AuditEventType.SecurityEvent,
                Severity = SecurityAlertSeverity.High,
                Condition = SecurityAlertCondition.CountInTimeWindow,
                Parameters = new Dictionary<string, object>
                {
                    ["count"] = 10,
                    ["timeWindowMinutes"] = 1
                },
                CooldownMinutes = 10,
                IsActive = true
            });
            
            // Critical sensitivity data access
            _alertRules.Add(new SecurityAlertRule
            {
                Id = "critical-data-access",
                Name = "Critical Sensitivity Data Access",
                Description = "Alert on any event with critical sensitivity",
                EventType = null, // Any event type
                Severity = SecurityAlertSeverity.Critical,
                Condition = SecurityAlertCondition.DataSensitivity,
                Parameters = new Dictionary<string, object>
                {
                    ["sensitivity"] = DataSensitivity.Critical
                },
                CooldownMinutes = 5,
                IsActive = true
            });
        }
        
        /// <summary>
        /// Check if an alert should be triggered for a rule
        /// </summary>
        private async Task<bool> ShouldTriggerAlert(SecurityAlertRule rule, AuditLogEntry auditEvent)
        {
            // Check if rule applies to this event type
            if (rule.EventType.HasValue && rule.EventType.Value != auditEvent.EventType)
                return false;
            
            // Check cooldown period
            if (rule.LastTriggered.HasValue && 
                DateTime.UtcNow < rule.LastTriggered.Value.AddMinutes(rule.CooldownMinutes))
                return false;
            
            return rule.Condition switch
            {
                SecurityAlertCondition.CountInTimeWindow => await CheckCountInTimeWindow(rule, auditEvent),
                SecurityAlertCondition.OutOfHours => CheckOutOfHours(rule, auditEvent),
                SecurityAlertCondition.DataSensitivity => CheckDataSensitivity(rule, auditEvent),
                _ => false
            };
        }
        
        /// <summary>
        /// Check count in time window condition
        /// </summary>
        private async Task<bool> CheckCountInTimeWindow(SecurityAlertRule rule, AuditLogEntry auditEvent)
        {
            var timeWindowMinutes = rule.Parameters.GetValueOrDefault("timeWindowMinutes", 5) as int? ?? 5;
            var requiredCount = rule.Parameters.GetValueOrDefault("count", 3) as int? ?? 3;
            var descriptionFilter = rule.Parameters.GetValueOrDefault("descriptionFilter") as string;
            
            var cutoff = DateTime.UtcNow.AddMinutes(-timeWindowMinutes);
            var recentEvents = await _auditService.GetLogsAsync(
                startDate: cutoff,
                eventType: rule.EventType);
            
            var filteredEvents = recentEvents;
            if (!string.IsNullOrEmpty(descriptionFilter))
            {
                filteredEvents = recentEvents.Where(e => 
                    e.Description.Contains(descriptionFilter, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            
            return filteredEvents.Count >= requiredCount;
        }
        
        /// <summary>
        /// Check out of hours condition
        /// </summary>
        private bool CheckOutOfHours(SecurityAlertRule rule, AuditLogEntry auditEvent)
        {
            var startHour = rule.Parameters.GetValueOrDefault("startHour", 9) as int? ?? 9;
            var endHour = rule.Parameters.GetValueOrDefault("endHour", 17) as int? ?? 17;
            
            var eventHour = auditEvent.Timestamp.Hour;
            return eventHour < startHour || eventHour > endHour;
        }
        
        /// <summary>
        /// Check data sensitivity condition
        /// </summary>
        private bool CheckDataSensitivity(SecurityAlertRule rule, AuditLogEntry auditEvent)
        {
            var requiredSensitivity = rule.Parameters.GetValueOrDefault("sensitivity") as DataSensitivity?;
            return requiredSensitivity.HasValue && auditEvent.Sensitivity == requiredSensitivity.Value;
        }
        
        /// <summary>
        /// Trigger a security alert
        /// </summary>
        private async Task TriggerAlert(SecurityAlertRule rule, AuditLogEntry auditEvent)
        {
            var alert = new SecurityAlert
            {
                Id = Guid.NewGuid().ToString(),
                RuleId = rule.Id,
                RuleName = rule.Name,
                Type = rule.EventType ?? auditEvent.EventType,
                Severity = rule.Severity,
                Timestamp = DateTime.UtcNow,
                Description = $"{rule.Name}: {auditEvent.Description}",
                AuditEventId = auditEvent.Id,
                Metadata = auditEvent.Metadata
            };
            
            _alerts.Enqueue(alert);
            
            // Log the alert itself
            await _auditService.LogEventAsync(
                AuditEventType.SecurityEvent,
                alert.Description,
                JsonSerializer.Serialize(new
                {
                    AlertId = alert.Id,
                    RuleId = rule.Id,
                    Severity = alert.Severity.ToString(),
                    AuditEventId = auditEvent.Id
                }),
                DataSensitivity.Medium);
            
            // Update rule last triggered
            rule.LastTriggered = DateTime.UtcNow;
            
            // Log to Windows Event Log for enterprise monitoring
            await LogToWindowsEventLog(alert);
            
            _logger.LogWarning("Security alert triggered: {AlertName} - {Description}", 
                rule.Name, alert.Description);
        }
        
        /// <summary>
        /// Log alert to Windows Event Log for enterprise monitoring
        /// </summary>
        private async Task LogToWindowsEventLog(SecurityAlert alert)
        {
            try
            {
                // This would integrate with Windows Event Log APIs
                // For now, log to audit service as a placeholder
                await _auditService.LogEventAsync(
                    AuditEventType.SecurityEvent,
                    $"Windows Event Log: {alert.Description}",
                    JsonSerializer.Serialize(new
                    {
                        EventLogSource = "WhisperKey Security",
                        EventId = int.Parse(alert.Id.Substring(0, 8), System.Globalization.NumberStyles.HexNumber),
                        EventType = "Warning",
                        AlertSeverity = alert.Severity.ToString()
                    }),
                    DataSensitivity.Low);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to log alert to Windows Event Log");
            }
        }
        
        /// <summary>
        /// Timer callback for cleanup
        /// </summary>
        private async void CleanupOldAlerts(object? state)
        {
            try
            {
                await ClearOldAlertsAsync(7); // Clear alerts older than 7 days
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during alert cleanup");
            }
        }
        
        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            if (_auditService != null)
            {
                _auditService.EventLogged -= OnAuditLogged;
            }
            _alertCleanupTimer?.Dispose();
            _logger.LogInformation("SecurityAlertService disposed");
        }
    }
}
