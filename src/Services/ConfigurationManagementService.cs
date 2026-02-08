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
    /// Implementation of configuration management and drift detection service
    /// </summary>
    public class ConfigurationManagementService : IConfigurationManagementService
    {
        private readonly ILogger<ConfigurationManagementService> _logger;
        private readonly IFileSystemService _fileSystem;
        private readonly IAuditLoggingService _auditService;
        private readonly ISettingsService _settingsService;
        private readonly string _baselinePath;
        private ConfigurationSnapshot? _baseline;

        public ConfigurationManagementService(
            ILogger<ConfigurationManagementService> logger,
            IFileSystemService fileSystem,
            IAuditLoggingService auditService,
            ISettingsService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _baselinePath = Path.Combine(appData, "WhisperKey", "config_baseline.json");
            
            LoadBaseline();
        }

        public async Task<ConfigurationSnapshot> CaptureSnapshotAsync()
        {
            var settings = _settingsService.Settings;
            var flatSettings = FlattenSettings(settings);
            
            var snapshot = new ConfigurationSnapshot
            {
                Timestamp = DateTime.UtcNow,
                Settings = flatSettings,
                ConfigHash = CalculateHash(flatSettings)
            };

            return await Task.FromResult(snapshot);
        }

        public async Task<DriftReport> ValidateParityAsync()
        {
            var current = await CaptureSnapshotAsync();
            var report = new DriftReport { DetectedAt = DateTime.UtcNow };

            if (_baseline == null)
            {
                report.HasDrift = false; // No baseline to compare against
                return report;
            }

            if (current.ConfigHash == _baseline.ConfigHash)
            {
                report.HasDrift = false;
                return report;
            }

            // Detect specific differences
            foreach (var key in _baseline.Settings.Keys.Union(current.Settings.Keys))
            {
                _baseline.Settings.TryGetValue(key, out var expected);
                current.Settings.TryGetValue(key, out var actual);

                if (expected != actual)
                {
                    report.Differences.Add(new DriftItem
                    {
                        SettingKey = key,
                        ExpectedValue = expected ?? "[MISSING]",
                        ActualValue = actual ?? "[MISSING]",
                        Severity = DriftSeverity.Warning
                    });
                }
            }

            report.HasDrift = report.Differences.Any();
            
            if (report.HasDrift)
            {
                _logger.LogWarning("Configuration drift detected! {Count} settings differ from baseline.", report.Differences.Count);
                await _auditService.LogEventAsync(
                    AuditEventType.SystemEvent,
                    $"[CONFIG DRIFT] {report.Differences.Count} settings deviated from baseline.",
                    JsonSerializer.Serialize(report),
                    DataSensitivity.Medium);
            }

            return report;
        }

        public async Task TrackChangeAsync(string key, string oldValue, string newValue)
        {
            _logger.LogInformation("Config change tracked: {Key} = {Old} -> {New}", key, oldValue, newValue);
            
            await _auditService.LogEventAsync(
                AuditEventType.SettingsChanged,
                $"Setting '{key}' changed from '{oldValue}' to '{newValue}'",
                null,
                DataSensitivity.Low);
        }

        public Task<List<ConfigurationSnapshot>> GetHistoryAsync()
        {
            // In a real app, we'd load this from a history table/file
            return Task.FromResult(new List<ConfigurationSnapshot>());
        }

        public async Task SetBaselineAsync()
        {
            _baseline = await CaptureSnapshotAsync();
            SaveBaseline();
            _logger.LogInformation("New configuration baseline established. Hash: {Hash}", _baseline.ConfigHash);
        }

        private void LoadBaseline()
        {
            try
            {
                if (File.Exists(_baselinePath))
                {
                    var json = File.ReadAllText(_baselinePath);
                    _baseline = JsonSerializer.Deserialize<ConfigurationSnapshot>(json);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load configuration baseline");
            }
        }

        private void SaveBaseline()
        {
            try
            {
                var dir = Path.GetDirectoryName(_baselinePath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_baseline, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_baselinePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save configuration baseline");
            }
        }

        private string CalculateHash(Dictionary<string, string> settings)
        {
            var sortedContent = string.Join(";", settings.OrderBy(kv => kv.Key).Select(kv => $"{kv.Key}={kv.Value}"));
            using var sha256 = SHA256.Create();
            var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sortedContent));
            return Convert.ToBase64String(bytes);
        }

        private Dictionary<string, string> FlattenSettings(object obj, string prefix = "")
        {
            var result = new Dictionary<string, string>();
            if (obj == null) return result;

            var properties = obj.GetType().GetProperties();
            foreach (var prop in properties)
            {
                var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                var value = prop.GetValue(obj);

                if (value == null)
                {
                    result[key] = "null";
                }
                else if (prop.PropertyType.IsPrimitive || prop.PropertyType == typeof(string) || prop.PropertyType == typeof(decimal) || prop.PropertyType == typeof(DateTime) || prop.PropertyType.IsEnum)
                {
                    result[key] = value.ToString() ?? "";
                }
                else if (typeof(System.Collections.IEnumerable).IsAssignableFrom(prop.PropertyType))
                {
                    // Skip collections for now to keep it simple, or serialize to JSON
                    result[key] = "[COLLECTION]";
                }
                else
                {
                    // Recursive for nested objects
                    var nested = FlattenSettings(value, key);
                    foreach (var kv in nested) result[kv.Key] = kv.Value;
                }
            }

            return result;
        }
    }
}
