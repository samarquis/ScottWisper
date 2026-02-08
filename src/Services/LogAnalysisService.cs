using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of automated log analysis and anomaly detection
    /// </summary>
    public class LogAnalysisService : ILogAnalysisService
    {
        private readonly ILogger<LogAnalysisService> _logger;
        private readonly IAuditLoggingService _auditService;
        private readonly string _logDirectory;
        private readonly ConcurrentDictionary<string, LogPattern> _knownPatterns = new();
        
        // Regex for parsing the Serilog template: [{Timestamp} {Level}] [{CorrelationId}] [{SourceContext}] {Message}{Exception}
        private static readonly Regex _logRegex = new Regex(
            @"^\[(?<ts>.*?)\s+(?<lvl>[A-Z]+)\]\s+\[(?<cid>.*?)\]\s+\[(?<src>.*?)\]\s+(?<msg>.*)$",
            RegexOptions.Compiled);

        public LogAnalysisService(
            ILogger<LogAnalysisService> logger,
            IAuditLoggingService auditService,
            string? logDirectory = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            
            _logDirectory = logDirectory ?? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WhisperKey",
                "logs");
        }

        public async Task AnalyzeLogsAsync()
        {
            try
            {
                var logs = await ReadRecentLogsAsync(TimeSpan.FromHours(1));
                if (!logs.Any()) return;

                // 1. Pattern Recognition
                foreach (var log in logs)
                {
                    var template = SimplifyMessage(log.Message);
                    var pattern = _knownPatterns.GetOrAdd(template, _ => new LogPattern
                    {
                        MessageTemplate = template,
                        FirstSeen = log.Timestamp,
                        Severity = log.Level
                    });

                    lock (pattern)
                    {
                        pattern.OccurrenceCount++;
                        pattern.LastSeen = log.Timestamp;
                    }
                }

                // 2. Anomaly Detection (Sudden spike in Error logs)
                var errorCount = logs.Count(l => l.Level.Contains("ERR") || l.Level.Contains("FTL"));
                if (errorCount > 50) // Arbitrary threshold for anomaly
                {
                    await _auditService.LogEventAsync(
                        AuditEventType.SystemEvent,
                        $"[LOG ANOMALY] High error rate detected: {errorCount} errors in the last hour.",
                        null,
                        DataSensitivity.Medium);
                }

                _logger.LogInformation("Log analysis completed. Identified {Count} unique patterns.", _knownPatterns.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during log analysis");
            }
        }

        public async Task<List<LogEntry>> GetCorrelatedLogsAsync(string correlationId)
        {
            if (string.IsNullOrEmpty(correlationId)) return new List<LogEntry>();
            
            var logs = await ReadRecentLogsAsync(TimeSpan.FromHours(24));
            return logs.Where(l => l.CorrelationId == correlationId).ToList();
        }

        public Task<List<LogPattern>> IdentifyPatternsAsync()
        {
            return Task.FromResult(_knownPatterns.Values.OrderByDescending(p => p.OccurrenceCount).ToList());
        }

        public Task<List<string>> GenerateInsightsAsync()
        {
            var insights = new List<string>();
            var patterns = _knownPatterns.Values.ToList();

            // Insight: Noisiest source
            var topPattern = patterns.OrderByDescending(p => p.OccurrenceCount).FirstOrDefault();
            if (topPattern != null)
            {
                insights.Add($"Most frequent log pattern: '{topPattern.MessageTemplate}' ({topPattern.OccurrenceCount} times)");
            }

            // Insight: New errors
            var recentErrors = patterns.Where(p => p.Severity.Contains("ERR") && p.FirstSeen > DateTime.UtcNow.AddHours(-1)).ToList();
            if (recentErrors.Any())
            {
                insights.Add($"{recentErrors.Count} new error patterns appeared in the last hour.");
            }

            return Task.FromResult(insights);
        }

        private async Task<List<LogEntry>> ReadRecentLogsAsync(TimeSpan window)
        {
            var results = new List<LogEntry>();
            if (!Directory.Exists(_logDirectory)) return results;

            var cutoff = DateTime.UtcNow.Subtract(window);
            var logFiles = Directory.GetFiles(_logDirectory, "whisperkey-*.log")
                .Select(f => new FileInfo(f))
                .Where(f => f.LastWriteTimeUtc >= cutoff)
                .OrderByDescending(f => f.LastWriteTimeUtc);

            foreach (var file in logFiles)
            {
                try
                {
                    // Use FileStream with shared read to avoid locking
                    using var fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(fs);
                    
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        var match = _logRegex.Match(line);
                        if (match.Success)
                        {
                            var entry = new LogEntry
                            {
                                Timestamp = DateTime.Parse(match.Groups["ts"].Value),
                                Level = match.Groups["lvl"].Value,
                                CorrelationId = match.Groups["cid"].Value,
                                SourceContext = match.Groups["src"].Value,
                                Message = match.Groups["msg"].Value
                            };
                            
                            if (entry.Timestamp >= cutoff)
                                results.Add(entry);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Failed to read log file {File}", file.Name);
                }
            }

            return results;
        }

        private string SimplifyMessage(string message)
        {
            // Remove GUIDs, numbers, and dates to find the template
            var simplified = Regex.Replace(message, @"[0-9a-fA-F]{8}[-][0-9a-fA-F]{4}[-][0-9a-fA-F]{4}[-][0-9a-fA-F]{4}[-][0-9a-fA-F]{12}", "{GUID}");
            simplified = Regex.Replace(simplified, @"\d+", "{N}");
            return simplified.Trim();
        }
    }
}
