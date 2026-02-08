using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for SOC 2 compliance validation service
    /// </summary>
    public interface ISOC2ComplianceService
    {
        /// <summary>
        /// Validate audit log integrity and immutability
        /// </summary>
        Task<SOC2ComplianceResult> ValidateAuditLogIntegrityAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        /// <summary>
        /// Validate audit log completeness (no missing entries)
        /// </summary>
        Task<SOC2ComplianceResult> ValidateAuditLogCompletenessAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        /// <summary>
        /// Validate retention policy compliance
        /// </summary>
        Task<SOC2ComplianceResult> ValidateRetentionPolicyComplianceAsync();
        
        /// <summary>
        /// Generate SOC 2 compliance report
        /// </summary>
        Task<SOC2ComplianceReport> GenerateComplianceReportAsync(DateTime? startDate = null, DateTime? endDate = null);
        
        /// <summary>
        /// Verify hash chain integrity for tamper detection
        /// </summary>
        Task<HashChainValidationResult> VerifyHashChainAsync();
        
        /// <summary>
        /// Check for suspicious patterns in audit logs
        /// </summary>
        Task<List<SuspiciousPattern>> DetectSuspiciousPatternsAsync();
    }

    /// <summary>
    /// SOC 2 compliance validation result
    /// </summary>
    public class SOC2ComplianceResult
    {
        public bool IsCompliant { get; set; }
        public List<ComplianceViolation> Violations { get; set; } = new();
        public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
        public int TotalEntriesValidated { get; set; }
        public int EntriesWithViolations { get; set; }
        public string Summary { get; set; } = string.Empty;
        public ComplianceLevel OverallLevel { get; set; }
    }

    /// <summary>
    /// Compliance violation details
    /// </summary>
    public class ComplianceViolation
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public ViolationType Type { get; set; }
        public SeverityLevel Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? AffectedEntryId { get; set; }
        public DateTime DetectedAt { get; set; } = DateTime.UtcNow;
        public string? Recommendation { get; set; }
        public Dictionary<string, object> Details { get; set; } = new();
        public bool IsCritical { get; set; }
    }

    /// <summary>
    /// SOC 2 compliance report
    /// </summary>
    public class SOC2ComplianceReport
    {
        public string ReportId { get; set; } = Guid.NewGuid().ToString();
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public ComplianceLevel OverallCompliance { get; set; }
        public SOC2ComplianceResult IntegrityValidation { get; set; } = null!;
        public SOC2ComplianceResult CompletenessValidation { get; set; } = null!;
        public SOC2ComplianceResult RetentionPolicyValidation { get; set; } = null!;
        public HashChainValidationResult HashChainValidation { get; set; } = null!;
        public List<SuspiciousPattern> SuspiciousPatterns { get; set; } = new();
        public Dictionary<string, int> EventTypesCount { get; set; } = new();
        public Dictionary<ComplianceType, int> ComplianceTypesCount { get; set; } = new();
        public int TotalEntries { get; set; }
        public int CriticalViolations { get; set; }
        public int HighViolations { get; set; }
        public int MediumViolations { get; set; }
        public int LowViolations { get; set; }
        public string Summary { get; set; } = string.Empty;
        public List<string> Recommendations { get; set; } = new();
    }

    /// <summary>
    /// Hash chain validation result
    /// </summary>
    public class HashChainValidationResult
    {
        public bool IsValid { get; set; }
        public List<HashChainBreak> Breaks { get; set; } = new();
        public int TotalEntries { get; set; }
        public int ValidatedEntries { get; set; }
        public DateTime ValidationTime { get; set; } = DateTime.UtcNow;
        public string Summary { get; set; } = string.Empty;
    }

    /// <summary>
    /// Hash chain break details
    /// </summary>
    public class HashChainBreak
    {
        public string EntryId { get; set; } = string.Empty;
        public DateTime EntryTimestamp { get; set; }
        public string ExpectedHash { get; set; } = string.Empty;
        public string ActualHash { get; set; } = string.Empty;
        public string? PreviousEntryId { get; set; }
        public string Description { get; set; } = string.Empty;
        public SeverityLevel Severity { get; set; }
    }

    /// <summary>
    /// Suspicious pattern detected in audit logs
    /// </summary>
    public class SuspiciousPattern
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public PatternType Type { get; set; }
        public SeverityLevel Severity { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime FirstOccurrence { get; set; }
        public DateTime LastOccurrence { get; set; }
        public int OccurrenceCount { get; set; }
        public List<string> AffectedEntryIds { get; set; } = new();
        public Dictionary<string, object> PatternDetails { get; set; } = new();
        public string? Recommendation { get; set; }
    }

    /// <summary>
    /// Violation types
    /// </summary>
    public enum ViolationType
    {
        HashMismatch,
        MissingEntry,
        TamperedEntry,
        InvalidTimestamp,
        RetentionPolicyViolation,
        MissingSecurityContext,
        UnauthorizedAccess,
        DataIntegrity,
        IncompleteChain,
        SuspiciousPattern
    }

    /// <summary>
    /// Severity levels
    /// </summary>
    public enum SeverityLevel
    {
        Low,
        Medium,
        High,
        Critical
    }

    /// <summary>
    /// Compliance levels
    /// </summary>
    public enum ComplianceLevel
    {
        NonCompliant,
        PartiallyCompliant,
        MostlyCompliant,
        FullyCompliant
    }

    /// <summary>
    /// Suspicious pattern types
    /// </summary>
    public enum PatternType
    {
        BruteForceAttempt,
        UnusualAccessPattern,
        PrivilegeEscalation,
        OffHoursActivity,
        MultipleFailedLogins,
        RapidSuccessionEvents,
        LocationInconsistency,
        AccountAnomaly
    }

    /// <summary>
    /// Implementation of SOC 2 compliance validation service
    /// </summary>
    public class SOC2ComplianceService : ISOC2ComplianceService
    {
        private readonly ILogger<SOC2ComplianceService> _logger;
        private readonly IAuditLoggingService _auditService;
        private readonly string _logDirectory;

        public SOC2ComplianceService(
            ILogger<SOC2ComplianceService> logger,
            IAuditLoggingService auditService,
            string? logDirectory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            
            // Use same directory as audit service
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _logDirectory = logDirectory ?? Path.Combine(appDataPath, "WhisperKey", "AuditLogs");
        }

        /// <summary>
        /// Validate audit log integrity and immutability
        /// </summary>
        public async Task<SOC2ComplianceResult> ValidateAuditLogIntegrityAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = new SOC2ComplianceResult
            {
                ValidationTime = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("Starting SOC 2 integrity validation for period: {Start} to {End}", 
                    startDate?.ToString("yyyy-MM-dd") ?? "Beginning", 
                    endDate?.ToString("yyyy-MM-dd") ?? "Now");

                var logs = await _auditService.GetLogsAsync(startDate, endDate);
                result.TotalEntriesValidated = logs.Count;

                // Validate each entry's hash
                foreach (var entry in logs)
                {
                    var computedHash = ComputeEntryHash(entry);
                    
                    if (entry.IntegrityHash != computedHash)
                    {
                        result.Violations.Add(new ComplianceViolation
                        {
                            Type = ViolationType.HashMismatch,
                            Severity = SeverityLevel.Critical,
                            Description = $"Hash mismatch detected for entry {entry.Id}",
                            AffectedEntryId = entry.Id,
                            IsCritical = true,
                            Details = new Dictionary<string, object>
                            {
                                ["ExpectedHash"] = entry.IntegrityHash ?? "NULL",
                                ["ComputedHash"] = computedHash,
                                ["EntryTimestamp"] = entry.Timestamp,
                                ["EventType"] = entry.EventType.ToString()
                            },
                            Recommendation = "Investigate potential tampering with audit logs"
                        });

                        result.EntriesWithViolations++;
                    }

                    // Validate timestamp
                    if (entry.Timestamp > DateTime.UtcNow.AddMinutes(5) || entry.Timestamp < DateTime.UtcNow.AddDays(-3650))
                    {
                        result.Violations.Add(new ComplianceViolation
                        {
                            Type = ViolationType.InvalidTimestamp,
                            Severity = SeverityLevel.High,
                            Description = $"Invalid timestamp detected for entry {entry.Id}",
                            AffectedEntryId = entry.Id,
                            IsCritical = false,
                            Details = new Dictionary<string, object>
                            {
                                ["Timestamp"] = entry.Timestamp,
                                ["CurrentTime"] = DateTime.UtcNow,
                                ["EventType"] = entry.EventType.ToString()
                            },
                            Recommendation = "Verify system clock synchronization"
                        });

                        result.EntriesWithViolations++;
                    }

                    // Validate security context for security events
                    if (IsSecurityEvent(entry.EventType) && string.IsNullOrEmpty(entry.Metadata))
                    {
                        result.Violations.Add(new ComplianceViolation
                        {
                            Type = ViolationType.MissingSecurityContext,
                            Severity = SeverityLevel.Medium,
                            Description = $"Missing security context for security event {entry.Id}",
                            AffectedEntryId = entry.Id,
                            IsCritical = false,
                            Details = new Dictionary<string, object>
                            {
                                ["EventType"] = entry.EventType.ToString(),
                                ["Timestamp"] = entry.Timestamp
                            },
                            Recommendation = "Ensure security context is captured for all security events"
                        });

                        result.EntriesWithViolations++;
                    }
                }

                // Calculate overall compliance
                result.IsCompliant = result.EntriesWithViolations == 0;
                result.OverallLevel = CalculateComplianceLevel(result);
                result.Summary = GenerateSummary(result);

                _logger.LogInformation("SOC 2 integrity validation completed: {Compliant} ({Violations} violations)", 
                    result.IsCompliant, result.Violations.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SOC 2 integrity validation");
                
                result.Violations.Add(new ComplianceViolation
                {
                    Type = ViolationType.DataIntegrity,
                    Severity = SeverityLevel.Critical,
                    Description = $"System error during validation: {ex.Message}",
                    IsCritical = true,
                    Details = new Dictionary<string, object>
                    {
                        ["Exception"] = ex.Message,
                        ["StackTrace"] = ex.StackTrace ?? ""
                    },
                    Recommendation = "Investigate system errors and retry validation"
                });

                result.IsCompliant = false;
                result.OverallLevel = ComplianceLevel.NonCompliant;
                
                return result;
            }
        }

        /// <summary>
        /// Validate audit log completeness (no missing entries)
        /// </summary>
        public async Task<SOC2ComplianceResult> ValidateAuditLogCompletenessAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var result = new SOC2ComplianceResult
            {
                ValidationTime = DateTime.UtcNow
            };

            try
            {
                var logs = await _auditService.GetLogsAsync(startDate, endDate);
                result.TotalEntriesValidated = logs.Count;

                // Group logs by day to check for gaps
                var logsByDay = logs.GroupBy(l => l.Timestamp.Date).OrderBy(g => g.Key).ToList();

                for (int i = 0; i < logsByDay.Count - 1; i++)
                {
                    var currentDay = logsByDay[i];
                    var nextDay = logsByDay[i + 1];
                    var dayGap = (nextDay.Key - currentDay.Key).Days;

                    // Check for gaps in logging
                    if (dayGap > 1)
                    {
                        result.Violations.Add(new ComplianceViolation
                        {
                            Type = ViolationType.MissingEntry,
                            Severity = SeverityLevel.High,
                            Description = $"Gap in audit logs detected: {dayGap - 1} day(s) missing",
                            IsCritical = false,
                            Details = new Dictionary<string, object>
                            {
                                ["LastLogDate"] = currentDay.Key,
                                ["NextLogDate"] = nextDay.Key,
                                ["GapDays"] = dayGap - 1
                            },
                            Recommendation = "Investigate why logging was interrupted and restore missing data if possible"
                        });

                        result.EntriesWithViolations++;
                    }

                    // Check for suspiciously low activity
                    if (currentDay.Count() < 10 && dayGap == 1)
                    {
                        result.Violations.Add(new ComplianceViolation
                        {
                            Type = ViolationType.SuspiciousPattern,
                            Severity = SeverityLevel.Medium,
                            Description = $"Suspiciously low activity on {currentDay.Key:yyyy-MM-dd}: {currentDay.Count()} entries",
                            IsCritical = false,
                            Details = new Dictionary<string, object>
                            {
                                ["Date"] = currentDay.Key,
                                ["EntryCount"] = currentDay.Count(),
                                ["AverageExpected"] = "50+"
                            },
                            Recommendation = "Verify if logging was functioning correctly on this date"
                        });

                        result.EntriesWithViolations++;
                    }
                }

                // Check for required security events
                var securityEvents = logs.Where(l => IsSecurityEvent(l.EventType)).ToList();
                var requiredSecurityEventTypes = new[]
                {
                    AuditEventType.UserLogin,
                    AuditEventType.UserLogout,
                    AuditEventType.AuthenticationSucceeded,
                    AuditEventType.AuthenticationFailed,
                    AuditEventType.ApiKeyAccessed
                };

                foreach (var requiredType in requiredSecurityEventTypes)
                {
                    if (!securityEvents.Any(l => l.EventType == requiredType))
                    {
                        result.Violations.Add(new ComplianceViolation
                        {
                            Type = ViolationType.MissingEntry,
                            Severity = SeverityLevel.Medium,
                            Description = $"Missing required security event type: {requiredType}",
                            IsCritical = false,
                            Details = new Dictionary<string, object>
                            {
                                ["RequiredEventType"] = requiredType.ToString()
                            },
                            Recommendation = "Ensure all required security event types are being logged"
                        });

                        result.EntriesWithViolations++;
                    }
                }

                result.IsCompliant = result.EntriesWithViolations == 0;
                result.OverallLevel = CalculateComplianceLevel(result);
                result.Summary = GenerateSummary(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during SOC 2 completeness validation");
                
                result.Violations.Add(new ComplianceViolation
                {
                    Type = ViolationType.DataIntegrity,
                    Severity = SeverityLevel.Critical,
                    Description = $"System error during completeness validation: {ex.Message}",
                    IsCritical = true,
                    Recommendation = "Investigate system errors and retry validation"
                });

                result.IsCompliant = false;
                result.OverallLevel = ComplianceLevel.NonCompliant;
                
                return result;
            }
        }

        /// <summary>
        /// Validate retention policy compliance
        /// </summary>
        public async Task<SOC2ComplianceResult> ValidateRetentionPolicyComplianceAsync()
        {
            var result = new SOC2ComplianceResult
            {
                ValidationTime = DateTime.UtcNow
            };

            try
            {
                var stats = await _auditService.GetStatisticsAsync();
                result.TotalEntriesValidated = stats.TotalEntries;

                // Check for entries past retention
                if (stats.EntriesPastRetention > 0)
                {
                    result.Violations.Add(new ComplianceViolation
                    {
                        Type = ViolationType.RetentionPolicyViolation,
                        Severity = SeverityLevel.Medium,
                        Description = $"{stats.EntriesPastRetention} entries past retention period",
                        IsCritical = false,
                        Details = new Dictionary<string, object>
                        {
                            ["PastRetentionCount"] = stats.EntriesPastRetention,
                            ["TotalEntries"] = stats.TotalEntries
                        },
                        Recommendation = "Run retention policy cleanup to remove expired entries"
                    });

                    result.EntriesWithViolations = stats.EntriesPastRetention;
                }

                // Verify SOC 2 logs have sufficient retention (7 years)
                var soc2Logs = await _auditService.GetLogsAsync(
                    startDate: DateTime.UtcNow.AddDays(-2555), // 7 years
                    endDate: DateTime.UtcNow.AddDays(-2545)    // 7 years - 10 days
                );

                if (soc2Logs.Count < 100) // Expecting some activity
                {
                    result.Violations.Add(new ComplianceViolation
                    {
                        Type = ViolationType.RetentionPolicyViolation,
                        Severity = SeverityLevel.High,
                        Description = "Insufficient SOC 2 logs for 7-year retention requirement",
                        IsCritical = false,
                        Details = new Dictionary<string, object>
                        {
                            ["LogCount"] = soc2Logs.Count,
                            ["RequiredRetention"] = "7 years",
                            ["ExpectedMinimum"] = "100+"
                        },
                        Recommendation = "Verify SOC 2 logs are properly retained for 7 years"
                    });

                    result.EntriesWithViolations++;
                }

                result.IsCompliant = result.EntriesWithViolations == 0;
                result.OverallLevel = CalculateComplianceLevel(result);
                result.Summary = GenerateSummary(result);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during retention policy validation");
                
                result.Violations.Add(new ComplianceViolation
                {
                    Type = ViolationType.DataIntegrity,
                    Severity = SeverityLevel.Critical,
                    Description = $"System error during retention validation: {ex.Message}",
                    IsCritical = true,
                    Recommendation = "Investigate system errors and retry validation"
                });

                result.IsCompliant = false;
                result.OverallLevel = ComplianceLevel.NonCompliant;
                
                return result;
            }
        }

        /// <summary>
        /// Generate SOC 2 compliance report
        /// </summary>
        public async Task<SOC2ComplianceReport> GenerateComplianceReportAsync(DateTime? startDate = null, DateTime? endDate = null)
        {
            var report = new SOC2ComplianceReport
            {
                ReportId = Guid.NewGuid().ToString(),
                GeneratedAt = DateTime.UtcNow,
                PeriodStart = startDate,
                PeriodEnd = endDate
            };

            try
            {
                _logger.LogInformation("Generating SOC 2 compliance report for period: {Start} to {End}", 
                    startDate?.ToString("yyyy-MM-dd") ?? "Beginning", 
                    endDate?.ToString("yyyy-MM-dd") ?? "Now");

                // Run all validations
                report.IntegrityValidation = await ValidateAuditLogIntegrityAsync(startDate, endDate);
                report.CompletenessValidation = await ValidateAuditLogCompletenessAsync(startDate, endDate);
                report.RetentionPolicyValidation = await ValidateRetentionPolicyComplianceAsync();
                report.HashChainValidation = await VerifyHashChainAsync();
                report.SuspiciousPatterns = await DetectSuspiciousPatternsAsync();

                // Calculate statistics
                var allLogs = await _auditService.GetLogsAsync(startDate, endDate);
                report.TotalEntries = allLogs.Count;

                report.EventTypesCount = allLogs
                    .GroupBy(l => l.EventType.ToString())
                    .ToDictionary(g => g.Key, g => g.Count());

                report.ComplianceTypesCount = allLogs
                    .GroupBy(l => l.ComplianceType)
                    .ToDictionary(g => g.Key, g => g.Count());

                // Count violations by severity
                var allViolations = new List<ComplianceViolation>();
                allViolations.AddRange(report.IntegrityValidation.Violations);
                allViolations.AddRange(report.CompletenessValidation.Violations);
                allViolations.AddRange(report.RetentionPolicyValidation.Violations);

                report.CriticalViolations = allViolations.Count(v => v.Severity == SeverityLevel.Critical);
                report.HighViolations = allViolations.Count(v => v.Severity == SeverityLevel.High);
                report.MediumViolations = allViolations.Count(v => v.Severity == SeverityLevel.Medium);
                report.LowViolations = allViolations.Count(v => v.Severity == SeverityLevel.Low);

                // Determine overall compliance
                report.OverallCompliance = CalculateOverallCompliance(report);

                // Generate summary and recommendations
                report.Summary = GenerateReportSummary(report);
                report.Recommendations = GenerateRecommendations(report);

                _logger.LogInformation("SOC 2 compliance report generated: {ReportId} [Level: {Level}]", 
                    report.ReportId, report.OverallCompliance);

                return report;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating SOC 2 compliance report");
                
                report.OverallCompliance = ComplianceLevel.NonCompliant;
                report.Summary = $"Error generating report: {ex.Message}";
                report.Recommendations.Add("Investigate system errors and regenerate report");
                
                return report;
            }
        }

        /// <summary>
        /// Verify hash chain integrity for tamper detection
        /// </summary>
        public async Task<HashChainValidationResult> VerifyHashChainAsync()
        {
            var result = new HashChainValidationResult
            {
                ValidationTime = DateTime.UtcNow
            };

            try
            {
                var logs = await _auditService.GetLogsAsync();
                result.TotalEntries = logs.Count;

                if (logs.Count == 0)
                {
                    result.IsValid = true;
                    result.Summary = "No entries to validate";
                    return result;
                }

                // Sort by timestamp
                var sortedLogs = logs.OrderBy(l => l.Timestamp).ToList();

                for (int i = 0; i < sortedLogs.Count; i++)
                {
                    var currentEntry = sortedLogs[i];
                    var computedHash = ComputeEntryHash(currentEntry);

                    if (currentEntry.IntegrityHash != computedHash)
                    {
                        var previousEntryId = i > 0 ? sortedLogs[i - 1].Id : null;

                        result.Breaks.Add(new HashChainBreak
                        {
                            EntryId = currentEntry.Id,
                            EntryTimestamp = currentEntry.Timestamp,
                            ExpectedHash = computedHash,
                            ActualHash = currentEntry.IntegrityHash ?? "NULL",
                            PreviousEntryId = previousEntryId,
                            Description = $"Hash mismatch for entry {currentEntry.Id}",
                            Severity = SeverityLevel.Critical
                        });
                    }

                    // Verify hash chain links
                    if (i > 0)
                    {
                        var previousEntry = sortedLogs[i - 1];
                        var currentMetadata = currentEntry.Metadata;
                        
                        if (!string.IsNullOrEmpty(currentMetadata))
                        {
                            try
                            {
                                var metadataDoc = JsonDocument.Parse(currentMetadata);
                                if (metadataDoc.RootElement.TryGetProperty("previousHash", out var previousHashElement))
                                {
                                    var expectedPreviousHash = previousHashElement.GetString();
                                    if (expectedPreviousHash != previousEntry.IntegrityHash)
                                    {
                                        result.Breaks.Add(new HashChainBreak
                                        {
                                            EntryId = currentEntry.Id,
                                            EntryTimestamp = currentEntry.Timestamp,
                                            ExpectedHash = previousEntry.IntegrityHash,
                                            ActualHash = expectedPreviousHash,
                                            PreviousEntryId = previousEntry.Id,
                                            Description = "Hash chain link broken - previous hash mismatch",
                                            Severity = SeverityLevel.Critical
                                        });
                                    }
                                }
                            }
                            catch
                            {
                                // Invalid JSON metadata - could indicate tampering
                                result.Breaks.Add(new HashChainBreak
                                {
                                    EntryId = currentEntry.Id,
                                    EntryTimestamp = currentEntry.Timestamp,
                                    ExpectedHash = "Valid JSON metadata",
                                    ActualHash = "Invalid JSON metadata",
                                    PreviousEntryId = previousEntry.Id,
                                    Description = "Invalid metadata JSON - possible tampering",
                                    Severity = SeverityLevel.High
                                });
                            }
                        }
                    }

                    result.ValidatedEntries++;
                }

                result.IsValid = result.Breaks.Count == 0;
                result.Summary = result.IsValid 
                    ? "Hash chain is intact - no tampering detected"
                    : $"Hash chain broken - {result.Breaks.Count} integrity violations detected";

                _logger.LogInformation("Hash chain validation completed: {Valid} ({Breaks} breaks)", 
                    result.IsValid, result.Breaks.Count);

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during hash chain validation");
                
                result.IsValid = false;
                result.Summary = $"Validation error: {ex.Message}";
                
                return result;
            }
        }

        /// <summary>
        /// Check for suspicious patterns in audit logs
        /// </summary>
        public async Task<List<SuspiciousPattern>> DetectSuspiciousPatternsAsync()
        {
            var patterns = new List<SuspiciousPattern>();

            try
            {
                var logs = await _auditService.GetLogsAsync();
                var securityLogs = logs.Where(l => IsSecurityEvent(l.EventType)).ToList();

                // Detect brute force attempts
                var failedAuthAttempts = securityLogs
                    .Where(l => l.EventType == AuditEventType.AuthenticationFailed)
                    .GroupBy(l => new { 
                        Hour = l.Timestamp.Date.AddHours(l.Timestamp.Hour),
                        UserId = l.UserId 
                    })
                    .Where(g => g.Count() >= 5) // 5+ failed attempts per hour
                    .ToList();

                foreach (var group in failedAuthAttempts)
                {
                    patterns.Add(new SuspiciousPattern
                    {
                        Type = PatternType.BruteForceAttempt,
                        Severity = SeverityLevel.High,
                        Description = $"Brute force attack detected: {group.Count()} failed authentication attempts for user {group.Key.UserId}",
                        FirstOccurrence = group.Min(l => l.Timestamp),
                        LastOccurrence = group.Max(l => l.Timestamp),
                        OccurrenceCount = group.Count(),
                        AffectedEntryIds = group.Select(l => l.Id).ToList(),
                        PatternDetails = new Dictionary<string, object>
                        {
                            ["UserId"] = group.Key.UserId,
                            ["AttemptsPerHour"] = group.Count(),
                            ["Hour"] = group.Key.Hour
                        },
                        Recommendation = "Implement account lockout and notify user"
                    });
                }

                // Detect unusual access patterns
                var offHoursActivity = securityLogs
                    .Where(l => l.EventType == AuditEventType.UserLogin)
                    .Where(l => l.Timestamp.Hour < 6 || l.Timestamp.Hour > 22) // Outside 6am-10pm
                    .GroupBy(l => l.UserId)
                    .Where(g => g.Count() >= 3) // 3+ off-hours logins
                    .ToList();

                foreach (var group in offHoursActivity)
                {
                    patterns.Add(new SuspiciousPattern
                    {
                        Type = PatternType.OffHoursActivity,
                        Severity = SeverityLevel.Medium,
                        Description = $"Unusual off-hours activity: {group.Count()} logins outside normal hours for user {group.Key}",
                        FirstOccurrence = group.Min(l => l.Timestamp),
                        LastOccurrence = group.Max(l => l.Timestamp),
                        OccurrenceCount = group.Count(),
                        AffectedEntryIds = group.Select(l => l.Id).ToList(),
                        PatternDetails = new Dictionary<string, object>
                        {
                            ["UserId"] = group.Key,
                            ["OffHoursLogins"] = group.Count()
                        },
                        Recommendation = "Review if this activity is legitimate"
                    });
                }

                // Detect rapid succession events
                var rapidEvents = securityLogs
                    .OrderBy(l => l.Timestamp)
                    .SlidingWindow(5, TimeSpan.FromMinutes(1)) // 5+ events in 1 minute
                    .ToList();

                foreach (var window in rapidEvents)
                {
                    patterns.Add(new SuspiciousPattern
                    {
                        Type = PatternType.RapidSuccessionEvents,
                        Severity = SeverityLevel.Medium,
                        Description = $"Rapid succession of {window.Count()} security events within 1 minute",
                        FirstOccurrence = window.First().Timestamp,
                        LastOccurrence = window.Last().Timestamp,
                        OccurrenceCount = window.Count(),
                        AffectedEntryIds = window.Select(l => l.Id).ToList(),
                        PatternDetails = new Dictionary<string, object>
                        {
                            ["EventCount"] = window.Count(),
                            ["TimeSpan"] = "1 minute"
                        },
                        Recommendation = "Investigate potential automated attack"
                    });
                }

                _logger.LogInformation("Suspicious pattern detection completed: {Count} patterns found", patterns.Count);
                return patterns;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during suspicious pattern detection");
                return new List<SuspiciousPattern>();
            }
        }

        #region Private Helper Methods

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
                _ => false
            };
        }

        private string ComputeEntryHash(AuditLogEntry entry)
        {
            var data = $"{entry.Timestamp:O}|{entry.EventType}|{entry.UserId}|{entry.Description}";
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(data);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }

        private ComplianceLevel CalculateComplianceLevel(SOC2ComplianceResult result)
        {
            if (result.Violations.Any(v => v.IsCritical))
                return ComplianceLevel.NonCompliant;

            var criticalCount = result.Violations.Count(v => v.Severity == SeverityLevel.Critical);
            var highCount = result.Violations.Count(v => v.Severity == SeverityLevel.High);
            var mediumCount = result.Violations.Count(v => v.Severity == SeverityLevel.Medium);

            if (criticalCount > 0 || highCount > 5)
                return ComplianceLevel.NonCompliant;

            if (highCount > 0 || mediumCount > 10)
                return ComplianceLevel.PartiallyCompliant;

            if (mediumCount > 0)
                return ComplianceLevel.MostlyCompliant;

            return ComplianceLevel.FullyCompliant;
        }

        private string GenerateSummary(SOC2ComplianceResult result)
        {
            if (result.IsCompliant)
                return "Fully compliant with SOC 2 requirements";

            var critical = result.Violations.Count(v => v.Severity == SeverityLevel.Critical);
            var high = result.Violations.Count(v => v.Severity == SeverityLevel.High);
            var medium = result.Violations.Count(v => v.Severity == SeverityLevel.Medium);
            var low = result.Violations.Count(v => v.Severity == SeverityLevel.Low);

            return $"SOC 2 compliance issues detected: {critical} critical, {high} high, {medium} medium, {low} low severity violations";
        }

        private ComplianceLevel CalculateOverallCompliance(SOC2ComplianceReport report)
        {
            var allResults = new[]
            {
                report.IntegrityValidation,
                report.CompletenessValidation,
                report.RetentionPolicyValidation
            };

            if (allResults.Any(r => !r.IsCompliant && r.Violations.Any(v => v.IsCritical)))
                return ComplianceLevel.NonCompliant;

            var nonCompliantCount = allResults.Count(r => !r.IsCompliant);
            if (nonCompliantCount == 0)
                return ComplianceLevel.FullyCompliant;
            if (nonCompliantCount == 1)
                return ComplianceLevel.MostlyCompliant;
            if (nonCompliantCount == 2)
                return ComplianceLevel.PartiallyCompliant;
            
            return ComplianceLevel.NonCompliant;
        }

        private string GenerateReportSummary(SOC2ComplianceReport report)
        {
            return report.OverallCompliance switch
            {
                ComplianceLevel.FullyCompliant => "System is fully compliant with SOC 2 requirements",
                ComplianceLevel.MostlyCompliant => "System is mostly compliant with minor issues to address",
                ComplianceLevel.PartiallyCompliant => "System has significant compliance issues requiring attention",
                ComplianceLevel.NonCompliant => "System has critical compliance violations requiring immediate action",
                _ => "Compliance status unknown"
            };
        }

        private List<string> GenerateRecommendations(SOC2ComplianceReport report)
        {
            var recommendations = new List<string>();

            if (!report.IntegrityValidation.IsCompliant)
                recommendations.Add("Address audit log integrity violations immediately");
            
            if (!report.CompletenessValidation.IsCompliant)
                recommendations.Add("Investigate missing audit log entries and gaps");
            
            if (!report.RetentionPolicyValidation.IsCompliant)
                recommendations.Add("Update retention policies to meet SOC 2 requirements");
            
            if (!report.HashChainValidation.IsValid)
                recommendations.Add("Investigate potential audit log tampering");

            if (report.SuspiciousPatterns.Any())
                recommendations.Add("Review suspicious security patterns and implement mitigations");

            if (recommendations.Count == 0)
                recommendations.Add("Continue monitoring and maintaining SOC 2 compliance");

            return recommendations;
        }

        #endregion
    }

    /// <summary>
    /// Extension methods for sliding window operations
    /// </summary>
    public static class EnumerableExtensions
    {
        public static IEnumerable<List<T>> SlidingWindow<T>(this IEnumerable<T> source, int windowSize, TimeSpan timeSpan)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            
            var list = source.ToList();
            for (int i = 0; i <= list.Count - windowSize; i++)
            {
                var window = list.Skip(i).Take(windowSize).ToList();
                if (window.Last().Timestamp() - window.First().Timestamp() <= timeSpan)
                {
                    yield return window;
                }
            }
        }

        private static DateTime Timestamp<T>(this T item)
        {
            if (item is AuditLogEntry entry)
                return entry.Timestamp;
            
            return DateTime.MinValue;
        }
    }
}
