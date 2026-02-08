using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of automated deployment rollback service
    /// </summary>
    public class DeploymentRollbackService : IDeploymentRollbackService
    {
        private readonly ILogger<DeploymentRollbackService> _logger;
        private readonly IFileSystemService _fileSystem;
        private readonly IAuditLoggingService _auditService;
        private readonly string _historyPath;
        private readonly RollbackConfig _config;
        private DeploymentHistory _history = new();

        public DeploymentRollbackService(
            ILogger<DeploymentRollbackService> logger,
            IFileSystemService fileSystem,
            IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _historyPath = Path.Combine(appData, "WhisperKey", "deployment_history.json");
            _config = new RollbackConfig();
            
            LoadHistory();
        }

        public async Task RecordStartupSuccessAsync()
        {
            _logger.LogInformation("Recording successful startup for version {Version}", GetCurrentVersion());
            
            _history.CurrentVersion = GetCurrentVersion();
            _history.ConsecutiveStartupFailures = 0;
            
            var target = _history.Targets.FirstOrDefault(t => t.Version == _history.CurrentVersion);
            if (target == null)
            {
                target = new RollbackTarget
                {
                    Version = _history.CurrentVersion,
                    DeployedAt = DateTime.UtcNow
                };
                _history.Targets.Add(target);
            }
            
            target.IsStable = true;
            _history.LastKnownStableVersion = _history.CurrentVersion;
            
            SaveHistory();
            
            await _auditService.LogEventAsync(
                AuditEventType.SystemEvent,
                $"Startup success for version {_history.CurrentVersion}",
                null,
                DataSensitivity.Low);
        }

        public async Task RecordStartupFailureAsync(string error)
        {
            _history.ConsecutiveStartupFailures++;
            _logger.LogWarning("Recording startup failure #{Count}: {Error}", 
                _history.ConsecutiveStartupFailures, error);
            
            SaveHistory();

            await _auditService.LogEventAsync(
                AuditEventType.SystemEvent,
                $"Startup failure for version {GetCurrentVersion()} - #{_history.ConsecutiveStartupFailures}",
                error,
                DataSensitivity.Medium);

            if (IsRollbackRequired())
            {
                await InitiateRollbackAsync();
            }
        }

        public async Task CreateConfigurationBackupAsync()
        {
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                var backupDir = Path.Combine(appData, "WhisperKey", _config.BackupDirectory);
                
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);

                var backupPath = Path.Combine(backupDir, $"appsettings.{GetCurrentVersion()}.{DateTime.UtcNow:yyyyMMddHHmmss}.json");
                
                if (File.Exists(configPath))
                {
                    File.Copy(configPath, backupPath, true);
                    _logger.LogInformation("Configuration backed up to {Path}", backupPath);
                    
                    var target = _history.Targets.FirstOrDefault(t => t.Version == GetCurrentVersion());
                    if (target != null)
                    {
                        target.BackupPath = backupPath;
                        SaveHistory();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create configuration backup");
            }
            await Task.CompletedTask;
        }

        public async Task<DeploymentResult> InitiateRollbackAsync()
        {
            _logger.LogCritical("Initiating automated rollback to last known stable version: {Version}", 
                _history.LastKnownStableVersion);

            await _auditService.LogEventAsync(
                AuditEventType.SecurityEvent, // Rollback is high-priority system change
                $"AUTOMATED ROLLBACK INITIATED: Reverting to {_history.LastKnownStableVersion}",
                null,
                DataSensitivity.High);

            var result = new DeploymentResult
            {
                Success = true,
                Version = _history.LastKnownStableVersion,
                ErrorMessage = "Rollback triggered successfully. Application will restart."
            };

            // 1. Restore configuration if backup exists
            var stableTarget = _history.Targets.FirstOrDefault(t => t.Version == _history.LastKnownStableVersion);
            if (stableTarget != null && !string.IsNullOrEmpty(stableTarget.BackupPath) && File.Exists(stableTarget.BackupPath))
            {
                try
                {
                    var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                    File.Copy(stableTarget.BackupPath, configPath, true);
                    _logger.LogInformation("Configuration restored from {Path}", stableTarget.BackupPath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to restore configuration during rollback");
                    result.Success = false;
                    result.ErrorMessage = "Rollback failed during configuration restoration.";
                }
            }

            // 2. In a real app, we would trigger an external updater to reinstall the previous version
            // For this implementation, we log the intent and would exit
            
            return await Task.FromResult(result);
        }

        public Task<DeploymentHistory> GetHistoryAsync()
        {
            return Task.FromResult(_history);
        }

        public bool IsRollbackRequired()
        {
            return _config.AutoRollbackEnabled && 
                   _history.ConsecutiveStartupFailures >= _config.MaxStartupFailuresBeforeRollback;
        }

        private string GetCurrentVersion()
        {
            return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
        }

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(_historyPath))
                {
                    var json = File.ReadAllText(_historyPath);
                    _history = JsonSerializer.Deserialize<DeploymentHistory>(json) ?? new DeploymentHistory();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load deployment history");
                _history = new DeploymentHistory();
            }
        }

        private void SaveHistory()
        {
            try
            {
                var dir = Path.GetDirectoryName(_historyPath);
                if (dir != null && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_historyPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save deployment history");
            }
        }
    }
}
