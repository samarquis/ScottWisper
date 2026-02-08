using System;
using System.Threading.Tasks;
using WhisperKey.Models;
using System.Collections.Generic;
using System.Linq;

namespace WhisperKey.Services
{
    /// <summary>
    /// Null implementation of audit logging service for backward compatibility
    /// </summary>
        public class NullAuditLoggingService : IAuditLoggingService
        {
            public bool IsEnabled => false;
    
            public event EventHandler<AuditLogEntry>? EventLogged;

            public Task<AuditLogEntry> LogEventAsync(AuditEventType eventType, string description, 
     string? metadata = null, DataSensitivity sensitivity = DataSensitivity.Low)
        {
            return Task.FromResult<AuditLogEntry>(null!);
        }
        
        public Task<AuditLogEntry> LogTranscriptionStartedAsync(string sessionId, string? metadata = null)
        {
            return Task.FromResult<AuditLogEntry>(null!);
        }
        
        public Task<AuditLogEntry> LogTranscriptionCompletedAsync(string sessionId, string? metadata = null)
        {
            return Task.FromResult<AuditLogEntry>(null!);
        }
        
        public Task<AuditLogEntry> LogTextInjectedAsync(string sessionId, string application, DataSensitivity sensitivity = DataSensitivity.Low)
        {
            return Task.FromResult<AuditLogEntry>(null!);
        }
        
        public Task<List<AuditLogEntry>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null, AuditEventType? eventType = null, ComplianceType? complianceType = null)
        {
            return Task.FromResult(new List<AuditLogEntry>());
        }
        
        public Task<AuditLogStatistics> GetStatisticsAsync()
        {
            return Task.FromResult(new AuditLogStatistics());
        }
        
        public Task<int> ApplyRetentionPoliciesAsync()
        {
            return Task.FromResult(0);
        }
        
        public Task<string> ExportLogsAsync(DateTime? startDate = null, DateTime? endDate = null, string? filePath = null)
        {
            return Task.FromResult(string.Empty);
        }
        
        public Task ConfigureRetentionPolicyAsync(RetentionPolicy policy)
        {
            return Task.CompletedTask;
        }
        
        public Task<List<RetentionPolicy>> GetRetentionPoliciesAsync()
        {
            return Task.FromResult(new List<RetentionPolicy>());
        }
        
        public Task<int> ArchiveOldLogsAsync(int daysOld)
        {
            return Task.FromResult(0);
        }
        
        public Task<int> PurgeArchivedLogsAsync(int daysOld)
        {
            return Task.FromResult(0);
        }
        
        public Task<bool> VerifyLogIntegrityAsync(string logId)
        {
            return Task.FromResult(true);
        }
        
        public void SetEnabled(bool enabled)
        {
            // No-op
        }
    }
}
