using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services.Database
{
    /// <summary>
    /// File-based implementation of the audit repository
    /// </summary>
    public class FileAuditRepository : IAuditRepository
    {
        private readonly ILogger<FileAuditRepository> _logger;
        private readonly IFileSystemService _fileSystem;
        private readonly string _logDir;

        public FileAuditRepository(
            ILogger<FileAuditRepository> logger,
            IFileSystemService fileSystem)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logDir = Path.Combine(_fileSystem.GetAppDataPath(), "audit");
            
            if (!Directory.Exists(_logDir))
                Directory.CreateDirectory(_logDir);
        }

        public async Task AddAsync(AuditLogEntry entry)
        {
            var fileName = $"audit_{entry.Timestamp:yyyyMMdd}.jsonl";
            var path = Path.Combine(_logDir, fileName);
            var json = JsonSerializer.Serialize(entry) + Environment.NewLine;
            
            await File.AppendAllTextAsync(path, json);
        }

        public async Task<List<AuditLogEntry>> GetAsync(DateTime? startDate = null, DateTime? endDate = null, AuditEventType? type = null)
        {
            var results = new List<AuditLogEntry>();
            var files = Directory.GetFiles(_logDir, "audit_*.jsonl");

            foreach (var file in files)
            {
                var lines = await File.ReadAllLinesAsync(file);
                foreach (var line in lines)
                {
                    try
                    {
                        var entry = JsonSerializer.Deserialize<AuditLogEntry>(line);
                        if (entry == null) continue;

                        if (startDate.HasValue && entry.Timestamp < startDate.Value) continue;
                        if (endDate.HasValue && entry.Timestamp > endDate.Value) continue;
                        if (type.HasValue && entry.EventType != type.Value) continue;

                        results.Add(entry);
                    }
                    catch { /* Skip corrupted lines */ }
                }
            }

            return results.OrderByDescending(r => r.Timestamp).ToList();
        }

        public async Task ClearOldAsync(int daysToKeep)
        {
            var cutoff = DateTime.UtcNow.AddDays(-daysToKeep);
            var files = Directory.GetFiles(_logDir, "audit_*.jsonl");

            foreach (var file in files)
            {
                var fi = new FileInfo(file);
                if (fi.LastWriteTimeUtc < cutoff)
                {
                    _logger.LogInformation("Deleting old audit file: {File}", fi.Name);
                    File.Delete(file);
                }
            }
            await Task.CompletedTask;
        }
    }
}
