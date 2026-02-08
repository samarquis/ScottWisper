using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of intelligent error reporting and classification service
    /// </summary>
    public class ErrorReportingService : IErrorReportingService
    {
        private readonly ILogger<ErrorReportingService> _logger;
        private readonly IAuditLoggingService _auditService;
        private readonly IIntelligentAlertingService _alertingService;
        private readonly ConcurrentDictionary<string, ErrorGroup> _errorGroups = new();

        public ErrorReportingService(
            ILogger<ErrorReportingService> logger,
            IAuditLoggingService auditService,
            IIntelligentAlertingService alertingService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _alertingService = alertingService ?? throw new ArgumentNullException(nameof(alertingService));
        }

        public async Task<string> ReportExceptionAsync(Exception ex, string? source = null, ErrorReportSeverity severity = ErrorReportSeverity.Medium)
        {
            try
            {
                var hash = CalculateErrorHash(ex);
                var group = _errorGroups.GetOrAdd(hash, _ => new ErrorGroup
                {
                    ErrorHash = hash,
                    CommonMessage = ex.Message,
                    FirstSeen = DateTime.UtcNow
                });

                lock (group)
                {
                    group.OccurrenceCount++;
                    group.LastSeen = DateTime.UtcNow;
                }

                _logger.LogError(ex, "Exception reported from {Source}. Occurrence count: {Count}", 
                    source ?? "Unknown", group.OccurrenceCount);

                // Dedup alerting - only alert on 1st, 10th, 100th occurrence etc.
                if (IsAlertWarranted(group, severity))
                {
                    await _auditService.LogEventAsync(
                        AuditEventType.Error,
                        $"[ERROR GROUP] {ex.Message} (Seen {group.OccurrenceCount} times)",
                        ex.StackTrace,
                        DataSensitivity.Medium);

                    if (severity >= ErrorReportSeverity.High)
                    {
                        // In a real app, we'd map Error to SecurityAlert or a generic Alert
                        // For now, we'll log it as a critical audit event
                        _logger.LogCritical("High severity error grouping triggered escalation.");
                    }
                }

                return hash;
            }
            catch (Exception internalEx)
            {
                _logger.LogError(internalEx, "Failed to process error report for exception: {Msg}", ex.Message);
                return "error-processing-failed";
            }
        }

        public Task<List<ErrorGroup>> GetErrorGroupsAsync()
        {
            return Task.FromResult(_errorGroups.Values.ToList());
        }

        public Task ResolveErrorGroupAsync(string errorHash)
        {
            _errorGroups.TryRemove(errorHash, out _);
            return Task.CompletedTask;
        }

        public ErrorReportSeverity ClassifyError(Exception ex)
        {
            if (ex is OutOfMemoryException || ex is StackOverflowException)
                return ErrorReportSeverity.Critical;
            
            if (ex is System.Security.SecurityException || ex is UnauthorizedAccessException)
                return ErrorReportSeverity.High;

            if (ex is System.IO.IOException || ex is System.Net.Http.HttpRequestException)
                return ErrorReportSeverity.Medium;

            return ErrorReportSeverity.Low;
        }

        private string CalculateErrorHash(Exception ex)
        {
            var sb = new StringBuilder();
            sb.Append(ex.GetType().FullName);
            sb.Append(ex.StackTrace ?? ex.Message);
            
            // For nested exceptions
            if (ex.InnerException != null)
            {
                sb.Append(ex.InnerException.GetType().FullName);
            }

            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
            return Convert.ToBase64String(bytes).Substring(0, 16);
        }

        private bool IsAlertWarranted(ErrorGroup group, ErrorReportSeverity severity)
        {
            // Alert on first occurrence
            if (group.OccurrenceCount == 1) return true;

            // Alert on critical errors every time
            if (severity == ErrorReportSeverity.Critical) return true;

            // Logarithmic alerting for common errors
            if (group.OccurrenceCount == 10 || group.OccurrenceCount == 100 || group.OccurrenceCount % 1000 == 0)
                return true;

            return false;
        }
    }
}
