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
using WhisperKey.Services.Database;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for audit logging service
    /// </summary>
    public interface IAuditLoggingService
    {
        /// <summary>
        /// Event triggered when an audit event is logged
        /// </summary>
        event EventHandler<AuditLogEntry>? EventLogged;

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
    /// Implementation of audit logging service for HIPAA/GDPR/SOC 2 compliance
    /// </summary>
    public class AuditLoggingService : IAuditLoggingService, IDisposable
    {
        private readonly ILogger<AuditLoggingService> _logger;
        private readonly IAuditRepository _repository;
        private readonly string _logDirectory;
        private readonly List<RetentionPolicy> _retentionPolicies;
        private readonly ISecurityContextService _securityContextService;
        private bool _isEnabled;
        private readonly SemaphoreSlim _fileLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Event triggered when an audit event is logged
        /// </summary>
        public event EventHandler<AuditLogEntry>? EventLogged;
        
        public bool IsEnabled => _isEnabled;
        
        public AuditLoggingService(
            ILogger<AuditLoggingService> logger, 
            IAuditRepository repository,
            string? logDirectory = null, 
            ISecurityContextService? securityContextService = null)
        {
            _logger = logger;
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _isEnabled = true;
            _retentionPolicies = new List<RetentionPolicy>();
            _securityContextService = securityContextService ?? new SecurityContextService(
                new LoggerFactory().CreateLogger<SecurityContextService>());
            
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
            
            // SOC 2 compliance logs - 7 years (2555 days)
            _retentionPolicies.Add(new RetentionPolicy
            {
                Name = "SOC 2 Compliance Logs",
                Description = "SOC 2 compliant retention for security audit trails",
                RetentionDays = 2555, // 7 years for SOC 2 compliance
                ApplicableComplianceTypes = new List<ComplianceType> { ComplianceType.SOC2 },
                ArchiveBeforeDeletion = true,
                ArchiveRetentionDays = 3650 // 10 years for archived SOC 2 logs
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
            
            try
            {
                // Get comprehensive security context for SOC 2 compliance
                var securityContext = await _securityContextService.GetSecurityContextAsync();
                
                // Enhance metadata with security context
                var enhancedMetadata = EnhanceMetadataWithSecurityContext(metadata, securityContext, eventType);
                
                var entry = new AuditLogEntry
                {
                    EventType = eventType,
                    UserId = HashValue(Environment.UserName),
                    SessionId = securityContext.SessionId,
                    IpAddress = securityContext.HashedIpAddress,
                    Description = description,
                    Metadata = enhancedMetadata,
                    Sensitivity = sensitivity,
                    ComplianceType = DetermineComplianceType(sensitivity, eventType),
                    RetentionExpiry = CalculateRetentionExpiry(eventType, sensitivity)
                };
                
                // Calculate integrity hash
                entry.IntegrityHash = CalculateHash(entry);
                
                // Save to immutable file storage with hash chaining for SOC 2 compliance
                await SaveLogEntryAsync(entry);
                
                // Save to repository (e.g. database for quick searching)
                await _repository.AddAsync(entry);
                
                // Raise event for real-time alerting
                EventLogged?.Invoke(this, entry);
                
                _logger.LogInformation("Audit event logged: {EventType} - {Description} [Device: {DeviceFingerprint}]", 
                    eventType, description, securityContext.DeviceFingerprint.Substring(0, 8) + "...");
                
                return entry;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error logging audit event: {EventType} - {Description}", eventType, description);
                
                // Fallback logging without security context
                var fallbackEntry = new AuditLogEntry
                {
                    EventType = eventType,
                    UserId = HashValue(Environment.UserName),
                    SessionId = Guid.NewGuid().ToString("N")[..8],
                    Description = description,
                    Metadata = metadata,
                    Sensitivity = sensitivity,
                    ComplianceType = DetermineComplianceType(sensitivity, eventType),
                    RetentionExpiry = CalculateRetentionExpiry(eventType, sensitivity),
                    ErrorMessage = ex.Message
                };
                
                fallbackEntry.IntegrityHash = CalculateHash(fallbackEntry);
                await SaveLogEntryAsync(fallbackEntry);
                
                return fallbackEntry;
            }
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
        /// Verify integrity of a log entry
        /// </summary>
        public async Task<bool> VerifyLogIntegrityAsync(string logId)
        {
            try
            {
                var logs = await GetLogsAsync();
                var targetEntry = logs.FirstOrDefault(l => l.Id == logId);
                
                if (targetEntry == null)
                    return false;
                
                // Extract previous hash from metadata if it exists for chain verification
                string? previousHash = null;
                if (!string.IsNullOrEmpty(targetEntry.Metadata))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(targetEntry.Metadata);
                        if (doc.RootElement.TryGetProperty("previousHash", out var prop))
                        {
                            previousHash = prop.GetString();
                        }
                    }
                    catch
                    {
                        // Ignore parsing errors
                    }
                }

                // Verify individual entry hash, taking into account if it was chained
                var calculatedHash = previousHash != null
                    ? CalculateHashWithChain(targetEntry, previousHash)
                    : CalculateHash(targetEntry);

                if (targetEntry.IntegrityHash != calculatedHash)
                {
                    _logger.LogWarning("Log integrity check failed for {LogId}: hash mismatch. Expected: {Expected}, Actual: {Actual}", 
                        logId, targetEntry.IntegrityHash, calculatedHash);
                    return false;
                }
                
                // Verify hash chain if there are multiple entries
                return await VerifyHashChainAsync(logs, targetEntry);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying log integrity for {LogId}", logId);
                return false;
            }
        }
        
        /// <summary>
        /// Verify hash chain integrity
        /// </summary>
        private async Task<bool> VerifyHashChainAsync(List<AuditLogEntry> logs, AuditLogEntry targetEntry)
        {
            try
            {
                var sortedLogs = logs.OrderBy(l => l.Timestamp).ToList();
                var targetIndex = sortedLogs.IndexOf(targetEntry);
                
                if (targetIndex <= 0)
                    return true; // First entry has no previous hash to verify
                
                // Verify that this entry's hash matches the one in the next entry
                for (int i = 0; i < sortedLogs.Count - 1; i++)
                {
                    var currentEntry = sortedLogs[i];
                    var nextEntry = sortedLogs[i + 1];
                    
                    // Check if next entry contains hash of current entry
                    if (!string.IsNullOrEmpty(nextEntry.Metadata))
                    {
                        try
                        {
                            var metadata = JsonDocument.Parse(nextEntry.Metadata);
                            if (metadata.RootElement.TryGetProperty("previousHash", out var previousHashElement))
                            {
                                var expectedPreviousHash = previousHashElement.GetString();
                                if (expectedPreviousHash != currentEntry.IntegrityHash)
                                {
                                    _logger.LogWarning("Hash chain broken at index {Index}", i);
                                    return false;
                                }
                            }
                        }
                        catch
                        {
                            // Metadata parsing failed, skip chain verification for this entry
                        }
                    }
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error verifying hash chain");
                return false;
            }
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
                
                // Add previous hash to metadata for chain verification
                var lastEntry = entries.LastOrDefault();
                if (lastEntry != null && !string.IsNullOrEmpty(lastEntry.IntegrityHash))
                {
                    var metadata = entry.Metadata ?? "{}";
                    var metadataDoc = JsonDocument.Parse(metadata);
                    var metadataDict = JsonSerializer.Deserialize<Dictionary<string, object>>(metadata) ?? new Dictionary<string, object>();
                    
                    metadataDict["previousHash"] = lastEntry.IntegrityHash;
                    entry.Metadata = JsonSerializer.Serialize(metadataDict);
                    
                    // Recalculate hash with previous hash included
                    entry.IntegrityHash = CalculateHashWithChain(entry, lastEntry.IntegrityHash);
                }
                
                entries.Add(entry);
                
                var newJson = JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true });
                
                // Use retry logic for file writing to handle transient locks
                int retryCount = 0;
                while (retryCount < 3)
                {
                    try
                    {
                        await File.WriteAllTextAsync(filePath, newJson).ConfigureAwait(false);
                        break;
                    }
                    catch (IOException) when (retryCount < 2)
                    {
                        retryCount++;
                        await Task.Delay(100);
                    }
                }
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
            // Security events get highest priority for SOC 2
            if (eventType == AuditEventType.SecurityEvent || 
                eventType == AuditEventType.ApiKeyAccessed ||
                eventType == AuditEventType.UserLogin ||
                eventType == AuditEventType.UserLogout)
            {
                return ComplianceType.SOC2;
            }
            
            // High sensitivity data might be HIPAA
            if (sensitivity == DataSensitivity.High || sensitivity == DataSensitivity.Critical)
            {
                // In a real implementation, this would be configurable
                return ComplianceType.HIPAA;
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
        /// Enhanced hash calculation for SOC 2 compliance with chaining
        /// </summary>
        private string CalculateHashWithChain(AuditLogEntry entry, string? previousHash = null)
        {
            var data = $"{entry.Timestamp:O}|{entry.EventType}|{entry.UserId}|{entry.Description}|{previousHash ?? ""}";
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        /// <summary>
        /// Enhance metadata with comprehensive security context for SOC 2 compliance
        /// </summary>
        private string EnhanceMetadataWithSecurityContext(string? existingMetadata, SecurityContext context, AuditEventType eventType)
        {
            try
            {
                var metadataDict = new Dictionary<string, object>();
                
                // Parse existing metadata if provided
                if (!string.IsNullOrEmpty(existingMetadata))
                {
                    try
                    {
                        var existingDict = JsonSerializer.Deserialize<Dictionary<string, object>>(existingMetadata);
                        if (existingDict != null)
                        {
                            foreach (var kvp in existingDict)
                            {
                                metadataDict[kvp.Key] = kvp.Value;
                            }
                        }
                    }
                    catch
                    {
                        // If parsing fails, add as raw string
                        metadataDict["OriginalMetadata"] = existingMetadata;
                    }
                }
                
                // Add security context for security-related events
                if (IsSecurityEvent(eventType))
                {
                    metadataDict["SecurityContext"] = new
                    {
                        DeviceFingerprint = context.DeviceFingerprint,
                        HashedIpAddress = context.HashedIpAddress,
                        Location = context.Location,
                        UserAgent = context.UserAgent,
                        ProcessId = context.ProcessId,
                        ThreadId = context.ThreadId,
                        HashedMachineName = context.HashedMachineName,
                        CapturedAt = context.CapturedAt
                    };
                    
                    // Add additional security details
                    metadataDict["SecurityDetails"] = new
                    {
                        EventType = eventType.ToString(),
                        IsSecurityCritical = IsSecurityCriticalEvent(eventType),
                        RequiresImmediateAttention = RequiresImmediateAttention(eventType),
                        ComplianceLevel = GetRequiredComplianceLevel(eventType)
                    };
                }
                
                // Add base metadata for all events
                metadataDict["BaseContext"] = new
                {
                    SessionId = context.SessionId,
                    ProcessId = context.ProcessId,
                    CapturedAt = context.CapturedAt
                };
                
                return JsonSerializer.Serialize(metadataDict, new JsonSerializerOptions { WriteIndented = false });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error enhancing metadata with security context");
                return existingMetadata ?? "{}";
            }
        }

        /// <summary>
        /// Check if event type is security-related
        /// </summary>
        private bool IsSecurityEvent(AuditEventType eventType)
        {
            return eventType switch
            {
                AuditEventType.UserLogin => true,
                AuditEventType.UserLogout => true,
                AuditEventType.AuthenticationSucceeded => true,
                AuditEventType.AuthenticationFailed => true,
                AuditEventType.AuthorizationFailed => true,
                AuditEventType.RoleChanged => true,
                AuditEventType.PermissionEscalation => true,
                AuditEventType.PasswordChanged => true,
                AuditEventType.TokenGenerated => true,
                AuditEventType.TokenExpired => true,
                AuditEventType.AccountLocked => true,
                AuditEventType.AccountUnlocked => true,
                AuditEventType.SecurityConfigurationChanged => true,
                AuditEventType.ApiKeyAccessed => true,
                AuditEventType.SecurityEvent => true,
                AuditEventType.DataExported => true,
                AuditEventType.DataDeleted => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if event is security critical for SOC 2
        /// </summary>
        private bool IsSecurityCriticalEvent(AuditEventType eventType)
        {
            return eventType switch
            {
                AuditEventType.AuthenticationFailed => true,
                AuditEventType.AuthorizationFailed => true,
                AuditEventType.PermissionEscalation => true,
                AuditEventType.AccountLocked => true,
                AuditEventType.AccountUnlocked => true,
                AuditEventType.SecurityConfigurationChanged => true,
                AuditEventType.ApiKeyAccessed => true,
                AuditEventType.SecurityEvent => true,
                _ => false
            };
        }

        /// <summary>
        /// Check if event requires immediate attention
        /// </summary>
        private bool RequiresImmediateAttention(AuditEventType eventType)
        {
            return eventType switch
            {
                AuditEventType.AuthenticationFailed => true,
                AuditEventType.AuthorizationFailed => true,
                AuditEventType.PermissionEscalation => true,
                AuditEventType.SecurityEvent => true,
                _ => false
            };
        }

        /// <summary>
        /// Get required compliance level for event type
        /// </summary>
        private string GetRequiredComplianceLevel(AuditEventType eventType)
        {
            return eventType switch
            {
                AuditEventType.AuthenticationFailed => "SOC2-Critical",
                AuditEventType.AuthorizationFailed => "SOC2-Critical",
                AuditEventType.PermissionEscalation => "SOC2-Critical",
                AuditEventType.ApiKeyAccessed => "SOC2-High",
                AuditEventType.PasswordChanged => "SOC2-High",
                AuditEventType.AccountLocked => "SOC2-High",
                AuditEventType.AccountUnlocked => "SOC2-High",
                AuditEventType.SecurityConfigurationChanged => "SOC2-High",
                AuditEventType.UserLogin => "SOC2-Medium",
                AuditEventType.UserLogout => "SOC2-Medium",
                AuditEventType.AuthenticationSucceeded => "SOC2-Medium",
                AuditEventType.TokenGenerated => "SOC2-Medium",
                AuditEventType.TokenExpired => "SOC2-Medium",
                AuditEventType.DataExported => "SOC2-Medium",
                AuditEventType.DataDeleted => "SOC2-Medium",
                _ => "SOC2-Low"
            };
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
