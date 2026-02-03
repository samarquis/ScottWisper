using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for audit logging service
    /// </summary>
    public interface IAuditLoggingService
    {
        /// <summary>
        /// Log an audit event
        /// </summary>
        Task<AuditLogEntry> LogEventAsync(AuditEventType eventType, string description, 
            string? metadata = null, DataSensitivity sensitivity = DataSensitivity.Low);
        
        /// <summary>
        /// Log a transcription session start
        /// </summary>
        Task<AuditLogEntry> LogTranscriptionStartedAsync(string sessionId, string? metadata = null);
        
        /// <summary>
        /// Log a transcription session completion
        /// </summary>
        Task<AuditLogEntry> LogTranscriptionCompletedAsync(string sessionId, string? metadata = null);
        
        /// <summary>
        /// Log text injection event
        /// </summary>
        Task<AuditLogEntry> LogTextInjectedAsync(string sessionId, string application, 
            DataSensitivity sensitivity = DataSensitivity.Low);
        
        /// <summary>
        /// Get audit logs with filtering
        /// </summary>
        Task<List<AuditLogEntry>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null,
            AuditEventType? eventType = null, ComplianceType? complianceType = null);
        
        /// <summary>
        /// Get statistics for audit logs
        /// </summary>
        Task<AuditLogStatistics> GetStatisticsAsync();
        
        /// <summary>
        /// Apply retention policies
        /// </summary>
        Task<int> ApplyRetentionPoliciesAsync();
        
        /// <summary>
        /// Export logs to file
        /// </summary>
        Task<string> ExportLogsAsync(DateTime? startDate = null, DateTime? endDate = null, 
            string? filePath = null);
        
        /// <summary>
        /// Configure retention policy
        /// </summary>
        Task ConfigureRetentionPolicyAsync(RetentionPolicy policy);
        
        /// <summary>
        /// Get current retention policies
        /// </summary>
        Task<List<RetentionPolicy>> GetRetentionPoliciesAsync();
        
        /// <summary>
        /// Archive old logs
        /// </summary>
        Task<int> ArchiveOldLogsAsync(int daysOld);
        
        /// <summary>
        /// Purge archived logs
        /// </summary>
        Task<int> PurgeArchivedLogsAsync(int daysOld);
        
        /// <summary>
        /// Verify log integrity
        /// </summary>
        Task<bool> VerifyLogIntegrityAsync(string logId);
        
        /// <summary>
        /// Enable or disable audit logging
        /// </summary>
        void SetEnabled(bool enabled);
        
        /// <summary>
        /// Check if audit logging is enabled
        /// </summary>
        bool IsEnabled { get; }
    }
    
    /// <summary>
    /// Implementation of audit logging service for HIPAA/GDPR compliance
    /// </summary>
    public class AuditLoggingService : IAuditLoggingService, IDisposable
    {
        private readonly ILogger<AuditLoggingService> _logger;
        private readonly string _logDirectory;
        private readonly List<RetentionPolicy> _retentionPolicies;
        private bool _isEnabled;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);
        
        public bool IsEnabled => _isEnabled;
        
        public AuditLoggingService(ILogger<AuditLoggingService> logger, string? logDirectory = null)
        {
            _logger = logger;
            _isEnabled = true;
            _retentionPolicies = new List<RetentionPolicy>();
            
            // Default log directory in AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logDirectory = logDirectory ?? Path.Combine(appDataPath, "WhisperKey", "AuditLogs");
            
            // Ensure directory exists
            Directory.CreateDirectory(_logDirectory);
            
            // Initialize default retention policies
            InitializeDefaultPolicies();
            
            _logger.LogInformation("AuditLoggingService initialized. Log directory: {LogDirectory}", _logDirectory);
        }
        
        /// <summary>
        /// Initialize default retention policies
        /// </summary>
        private void InitializeDefaultPolicies()
        {
            // General logs - 30 days
            _retentionPolicies.Add(new RetentionPolicy
            {
                Name = "General Logs",
                Description = "Default retention for general audit logs",
                RetentionDays = 30,
                ApplicableComplianceTypes = new List<ComplianceType> { ComplianceType.General },
                ArchiveBeforeDeletion = true,
                ArchiveRetentionDays = 90
            });
            
            // HIPAA logs - 6 years (HIPAA requirement)
            _retentionPolicies.Add(new RetentionPolicy
            {
                Name = "HIPAA Logs",
                Description = "HIPAA-compliant retention for healthcare data",
                RetentionDays = 2190, // 6 years
                ApplicableComplianceTypes = new List<ComplianceType> { ComplianceType.HIPAA },
                ArchiveBeforeDeletion = true,
                ArchiveRetentionDays = 2555 // 7 years
            });
            
            // GDPR logs - 1 year
            _retentionPolicies.Add(new RetentionPolicy
            {
                Name = "GDPR Logs",
                Description = "GDPR-compliant retention for EU data",
                RetentionDays = 365,
                ApplicableComplianceTypes = new List<ComplianceType> { ComplianceType.GDPR },
                ArchiveBeforeDeletion = true,
                ArchiveRetentionDays = 730 // 2 years
            });
            
            // Security logs - 1 year
            _retentionPolicies.Add(new RetentionPolicy
            {
                Name = "Security Logs",
                Description = "Retention for security-related events",
                RetentionDays = 365,
                ApplicableEventTypes = new List<AuditEventType> 
                { 
                    AuditEventType.SecurityEvent, 
                    AuditEventType.Error,
                    AuditEventType.ApiKeyAccessed 
                },
                ArchiveBeforeDeletion = true,
                ArchiveRetentionDays = 730
            });
        }
        
        /// <summary>
        /// Log an audit event
        /// </summary>
        public async Task<AuditLogEntry> LogEventAsync(AuditEventType eventType, string description,
            string? metadata = null, DataSensitivity sensitivity = DataSensitivity.Low)
        {
            if (!_isEnabled)
            {
                _logger.LogDebug("Audit logging disabled, skipping event: {EventType}", eventType);
                return null!;
            }
            
            var entry = new AuditLogEntry
            {
                EventType = eventType,
                UserId = HashValue(Environment.UserName),
                SessionId = Guid.NewGuid().ToString("N")[..8],
                Description = description,
                Metadata = metadata,
                Sensitivity = sensitivity,
                ComplianceType = DetermineComplianceType(sensitivity, eventType),
                RetentionExpiry = CalculateRetentionExpiry(eventType, sensitivity)
            };
            
            // Calculate integrity hash
            entry.IntegrityHash = CalculateHash(entry);
            
            // Save to file
            await SaveLogEntryAsync(entry);
            
            _logger.LogInformation("Audit event logged: {EventType} - {Description}", eventType, description);
            
            return entry;
        }
        
        /// <summary>
        /// Log transcription session start
        /// </summary>
        public async Task<AuditLogEntry> LogTranscriptionStartedAsync(string sessionId, string? metadata = null)
        {
            return await LogEventAsync(AuditEventType.TranscriptionStarted,
                $"Transcription session started: {sessionId}",
                metadata,
                DataSensitivity.Medium);
        }
        
        /// <summary>
        /// Log transcription session completion
        /// </summary>
        public async Task<AuditLogEntry> LogTranscriptionCompletedAsync(string sessionId, string? metadata = null)
        {
            return await LogEventAsync(AuditEventType.TranscriptionCompleted,
                $"Transcription session completed: {sessionId}",
                metadata,
                DataSensitivity.Medium);
        }
        
        /// <summary>
        /// Log text injection event
        /// </summary>
        public async Task<AuditLogEntry> LogTextInjectedAsync(string sessionId, string application,
            DataSensitivity sensitivity = DataSensitivity.Low)
        {
            var metadata = JsonSerializer.Serialize(new { Application = application });
            return await LogEventAsync(AuditEventType.TextInjected,
                $"Text injected into {application}",
                metadata,
                sensitivity);
        }
        
        /// <summary>
        /// Get audit logs with optional filtering
        /// </summary>
        public async Task<List<AuditLogEntry>> GetLogsAsync(DateTime? startDate = null, DateTime? endDate = null,
            AuditEventType? eventType = null, ComplianceType? complianceType = null)
        {
            var logs = new List<AuditLogEntry>();
            
            var files = Directory.GetFiles(_logDirectory, "audit-*.json");
            
            foreach (var file in files.OrderByDescending(f => f))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    var entries = JsonSerializer.Deserialize<List<AuditLogEntry>>(json) ?? new List<AuditLogEntry>();
                    
                    // Apply filters
                    var filtered = entries.Where(e =>
                    {
                        if (startDate.HasValue && e.Timestamp < startDate.Value)
                            return false;
                        if (endDate.HasValue && e.Timestamp > endDate.Value)
                            return false;
                        if (eventType.HasValue && e.EventType != eventType.Value)
                            return false;
                        if (complianceType.HasValue && e.ComplianceType != complianceType.Value)
                            return false;
                        return true;
                    });
                    
                    logs.AddRange(filtered);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error reading audit log file: {File}", file);
                }
            }
            
            return logs.OrderByDescending(l => l.Timestamp).ToList();
        }
        
        /// <summary>
        /// Get statistics for audit logs
        /// </summary>
        public async Task<AuditLogStatistics> GetStatisticsAsync()
        {
            var stats = new AuditLogStatistics();
            var files = Directory.GetFiles(_logDirectory, "audit-*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    var entries = JsonSerializer.Deserialize<List<AuditLogEntry>>(json) ?? new List<AuditLogEntry>();
                    
                    stats.TotalEntries += entries.Count;
                    
                    foreach (var entry in entries)
                    {
                        // Count by type
                        if (!stats.EntriesByType.ContainsKey(entry.EventType))
                            stats.EntriesByType[entry.EventType] = 0;
                        stats.EntriesByType[entry.EventType]++;
                        
                        // Count by compliance
                        if (!stats.EntriesByCompliance.ContainsKey(entry.ComplianceType))
                            stats.EntriesByCompliance[entry.ComplianceType] = 0;
                        stats.EntriesByCompliance[entry.ComplianceType]++;
                        
                        // Check if past retention
                        if (entry.RetentionExpiry < DateTime.UtcNow)
                            stats.EntriesPastRetention++;
                        
                        // Track dates
                        if (!stats.OldestEntry.HasValue || entry.Timestamp < stats.OldestEntry)
                            stats.OldestEntry = entry.Timestamp;
                        if (!stats.NewestEntry.HasValue || entry.Timestamp > stats.NewestEntry)
                            stats.NewestEntry = entry.Timestamp;
                    }
                    
                    var fileInfo = new FileInfo(file);
                    stats.StorageSizeBytes += fileInfo.Length;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error analyzing audit log file: {File}", file);
                }
            }
            
            return stats;
        }
        
        /// <summary>
        /// Apply retention policies and remove expired logs
        /// </summary>
        public async Task<int> ApplyRetentionPoliciesAsync()
        {
            int deletedCount = 0;
            var files = Directory.GetFiles(_logDirectory, "audit-*.json");
            var now = DateTime.UtcNow;
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    var entries = JsonSerializer.Deserialize<List<AuditLogEntry>>(json) ?? new List<AuditLogEntry>();
                    
                    var expired = entries.Where(e => e.RetentionExpiry < now).ToList();
                    var valid = entries.Where(e => e.RetentionExpiry >= now).ToList();
                    
                    if (expired.Any())
                    {
                        // Archive before deletion if policy requires
                        var toArchive = expired.Where(e =>
                        {
                            var policy = GetPolicyForEntry(e);
                            return policy?.ArchiveBeforeDeletion == true && !e.IsArchived;
                        }).ToList();
                        
                        if (toArchive.Any())
                        {
                            await ArchiveEntriesAsync(toArchive);
                        }
                        
                        deletedCount += expired.Count;
                        
                        // Rewrite file with only valid entries
                        if (valid.Any())
                        {
                            var newJson = JsonSerializer.Serialize(valid, new JsonSerializerOptions { WriteIndented = true });
                            await File.WriteAllTextAsync(file, newJson).ConfigureAwait(false);
                        }
                        else
                        {
                            File.Delete(file);
                        }
                        
                        _logger.LogInformation("Applied retention policy to {File}: {Deleted} entries deleted, {Kept} kept",
                            file, expired.Count, valid.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error applying retention policy to file: {File}", file);
                }
            }
            
            await LogEventAsync(AuditEventType.RetentionPolicyApplied,
                $"Retention policy applied: {deletedCount} entries removed");
            
            return deletedCount;
        }
        
        /// <summary>
        /// Export logs to a file
        /// </summary>
        public async Task<string> ExportLogsAsync(DateTime? startDate = null, DateTime? endDate = null,
            string? filePath = null)
        {
            var logs = await GetLogsAsync(startDate, endDate);
            
            filePath ??= Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"WhisperKey_AuditExport_{DateTime.Now:yyyyMMdd_HHmmss}.json"
            );
            
            var export = new
            {
                ExportedAt = DateTime.UtcNow,
                ExportRange = new { Start = startDate, End = endDate },
                TotalEntries = logs.Count,
                Entries = logs
            };
            
            var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
            
            await LogEventAsync(AuditEventType.DataExported,
                $"Audit logs exported to {filePath}",
                JsonSerializer.Serialize(new { EntryCount = logs.Count }));
            
            _logger.LogInformation("Audit logs exported to {FilePath}: {Count} entries", filePath, logs.Count);
            
            return filePath;
        }
        
        /// <summary>
        /// Configure a retention policy
        /// </summary>
        public Task ConfigureRetentionPolicyAsync(RetentionPolicy policy)
        {
            lock (_fileLock)
            {
                var existing = _retentionPolicies.FirstOrDefault(p => p.Id == policy.Id);
                if (existing != null)
                {
                    _retentionPolicies.Remove(existing);
                }
                
                policy.ModifiedAt = DateTime.UtcNow;
                _retentionPolicies.Add(policy);
            }
            
            _logger.LogInformation("Retention policy configured: {PolicyName}", policy.Name);
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Get all retention policies
        /// </summary>
        public Task<List<RetentionPolicy>> GetRetentionPoliciesAsync()
        {
            return Task.FromResult(_retentionPolicies.ToList());
        }
        
        /// <summary>
        /// Archive old logs
        /// </summary>
        public async Task<int> ArchiveOldLogsAsync(int daysOld)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var archivedCount = 0;
            var files = Directory.GetFiles(_logDirectory, "audit-*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                    var entries = JsonSerializer.Deserialize<List<AuditLogEntry>>(json) ?? new List<AuditLogEntry>();
                    
                    var toArchive = entries.Where(e => e.Timestamp < cutoffDate && !e.IsArchived).ToList();
                    
                    if (toArchive.Any())
                    {
                        await ArchiveEntriesAsync(toArchive);
                        archivedCount += toArchive.Count;
                        
                        // Mark as archived in original file
                        foreach (var entry in entries.Where(e => toArchive.Contains(e)))
                        {
                            entry.IsArchived = true;
                        }
                        
                        var newJson = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
                        await File.WriteAllTextAsync(file, newJson);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error archiving logs from file: {File}", file);
                }
            }
            
            await LogEventAsync(AuditEventType.LogArchived,
                $"Archived {archivedCount} log entries older than {daysOld} days");
            
            return archivedCount;
        }
        
        /// <summary>
        /// Purge archived logs
        /// </summary>
        public async Task<int> PurgeArchivedLogsAsync(int daysOld)
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);
            var archiveDir = Path.Combine(_logDirectory, "Archive");
            var purgedCount = 0;
            
            if (!Directory.Exists(archiveDir))
                return 0;
            
            var files = Directory.GetFiles(archiveDir, "audit-archive-*.json");
            
            foreach (var file in files)
            {
                try
                {
                    var fileDate = File.GetCreationTimeUtc(file);
                    if (fileDate < cutoffDate)
                    {
                    var json = await File.ReadAllTextAsync(file).ConfigureAwait(false);
                        var entries = JsonSerializer.Deserialize<List<AuditLogEntry>>(json) ?? new List<AuditLogEntry>();
                        purgedCount += entries.Count;
                        File.Delete(file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error purging archived logs: {File}", file);
                }
            }
            
            await LogEventAsync(AuditEventType.DataDeleted,
                $"Purged {purgedCount} archived log entries older than {daysOld} days");
            
            return purgedCount;
        }
        
        /// <summary>
        /// Verify the integrity of a log entry
        /// </summary>
        public Task<bool> VerifyLogIntegrityAsync(string logId)
        {
            // Implementation would verify the hash
            // For now, return true
            return Task.FromResult(true);
        }
        
        /// <summary>
        /// Enable or disable audit logging
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _logger.LogInformation("Audit logging {Status}", enabled ? "enabled" : "disabled");
        }
        
        /// <summary>
        /// Save a log entry to file with proper locking to prevent race conditions
        /// </summary>
        private async Task SaveLogEntryAsync(AuditLogEntry entry)
        {
            var fileName = $"audit-{entry.Timestamp:yyyy-MM}.json";
            var filePath = Path.Combine(_logDirectory, fileName);
            
            await _fileLock.WaitAsync();
            try
            {
                List<AuditLogEntry> entries;
                
                if (File.Exists(filePath))
                {
                    var json = await File.ReadAllTextAsync(filePath);
                    entries = JsonSerializer.Deserialize<List<AuditLogEntry>>(json) ?? new List<AuditLogEntry>();
                }
                else
                {
                    entries = new List<AuditLogEntry>();
                }
                
                entries.Add(entry);
                
                var newJson = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(filePath, newJson);
            }
            finally
            {
                _fileLock.Release();
            }
        }
        
        /// <summary>
        /// Archive entries to archive directory
        /// </summary>
        private async Task ArchiveEntriesAsync(List<AuditLogEntry> entries)
        {
            var archiveDir = Path.Combine(_logDirectory, "Archive");
            Directory.CreateDirectory(archiveDir);
            
            var fileName = $"audit-archive-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json";
            var filePath = Path.Combine(archiveDir, fileName);
            
            var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json);
        }
        
        /// <summary>
        /// Calculate retention expiry date for an entry
        /// </summary>
        private DateTime CalculateRetentionExpiry(AuditEventType eventType, DataSensitivity sensitivity)
        {
            var policy = GetPolicyForEntry(eventType, sensitivity);
            return DateTime.UtcNow.AddDays(policy?.RetentionDays ?? 30);
        }
        
        /// <summary>
        /// Get the applicable policy for an entry
        /// </summary>
        private RetentionPolicy? GetPolicyForEntry(AuditLogEntry entry)
        {
            return GetPolicyForEntry(entry.EventType, entry.Sensitivity, entry.ComplianceType);
        }
        
        /// <summary>
        /// Get the applicable policy for event type and sensitivity
        /// </summary>
        private RetentionPolicy? GetPolicyForEntry(AuditEventType eventType, DataSensitivity sensitivity,
            ComplianceType? complianceType = null)
        {
            lock (_fileLock)
            {
                // First, try to find a policy by compliance type
                if (complianceType.HasValue)
                {
                    var compliancePolicy = _retentionPolicies
                        .Where(p => p.IsActive && p.ApplicableComplianceTypes.Contains(complianceType.Value))
                        .OrderByDescending(p => p.ApplicableComplianceTypes.Count)
                        .FirstOrDefault();
                    
                    if (compliancePolicy != null)
                        return compliancePolicy;
                }
                
                // Then, try to find by event type
                var eventPolicy = _retentionPolicies
                    .Where(p => p.IsActive && p.ApplicableEventTypes.Contains(eventType))
                    .FirstOrDefault();
                
                if (eventPolicy != null)
                    return eventPolicy;
                
                // Fall back to general policy
                return _retentionPolicies
                    .Where(p => p.IsActive && p.ApplicableComplianceTypes.Contains(ComplianceType.General))
                    .FirstOrDefault();
            }
        }
        
        /// <summary>
        /// Determine compliance type based on sensitivity and event
        /// </summary>
        private ComplianceType DetermineComplianceType(DataSensitivity sensitivity, AuditEventType eventType)
        {
            // High sensitivity data might be HIPAA
            if (sensitivity == DataSensitivity.High || sensitivity == DataSensitivity.Critical)
            {
                // In a real implementation, this would be configurable
                return ComplianceType.HIPAA;
            }
            
            // Security events
            if (eventType == AuditEventType.SecurityEvent || 
                eventType == AuditEventType.ApiKeyAccessed)
            {
                return ComplianceType.SOC2;
            }
            
            return ComplianceType.General;
        }
        
        /// <summary>
        /// Hash a value for privacy
        /// </summary>
        private string HashValue(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
        
        /// <summary>
        /// Calculate integrity hash for a log entry
        /// </summary>
        private string CalculateHash(AuditLogEntry entry)
        {
            var data = $"{entry.Timestamp:O}|{entry.EventType}|{entry.UserId}|{entry.Description}";
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Dispose of resources
        /// </summary>
        public void Dispose()
        {
            _fileLock?.Dispose();
        }
    }
}
