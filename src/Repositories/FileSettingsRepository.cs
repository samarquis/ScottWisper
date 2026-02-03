using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Repositories
{
    /// <summary>
    /// File-based implementation of settings repository
    /// </summary>
    public class FileSettingsRepository : ISettingsRepository
    {
        private readonly string _settingsPath;
        private readonly string _backupPath;
        private readonly ILogger<FileSettingsRepository> _logger;

        public FileSettingsRepository(ILogger<FileSettingsRepository> logger)
        {
            _logger = logger;
            
            // Initialize paths
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "WhisperKey");
            Directory.CreateDirectory(appFolder);
            
            _settingsPath = Path.Combine(appFolder, "usersettings.json");
            _backupPath = Path.Combine(appFolder, "backups");
            Directory.CreateDirectory(_backupPath);
        }

        public async Task<AppSettings> LoadAsync()
        {
            try
            {
                if (!await ExistsAsync())
                {
                    _logger.LogInformation("Settings file not found, using default settings");
                    return new AppSettings();
                }

                var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                if (settings == null)
                {
                    _logger.LogWarning("Failed to deserialize settings, using defaults");
                    return new AppSettings();
                }

                _logger.LogInformation("Settings loaded successfully");
                return settings;
            }
            catch (FileNotFoundException)
            {
                _logger.LogInformation("Settings file not found, using default settings");
                return new AppSettings();
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error loading settings, using defaults");
                return new AppSettings();
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied loading settings, using defaults");
                return new AppSettings();
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Invalid JSON in settings file, using defaults");
                return new AppSettings();
            }
        }

        public async Task SaveAsync(AppSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            try
            {
                // Create backup before saving
                await CreateAutoBackupAsync(settings).ConfigureAwait(false);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                // Write to temporary file first, then move to avoid corruption
                var tempPath = _settingsPath + ".tmp";
                await File.WriteAllTextAsync(tempPath, json).ConfigureAwait(false);
                
                // Validate the written file
                var writtenJson = await File.ReadAllTextAsync(tempPath).ConfigureAwait(false);
                var validationSettings = JsonSerializer.Deserialize<AppSettings>(writtenJson);
                if (validationSettings == null)
                {
                    throw new InvalidOperationException("Failed to validate written settings");
                }

                // Replace the original file
                File.Move(tempPath, _settingsPath, true);

                _logger.LogInformation("Settings saved successfully");
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "IO error saving settings");
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied saving settings");
                throw new InvalidOperationException($"Failed to save settings: {ex.Message}", ex);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "JSON serialization error saving settings");
                throw new InvalidOperationException($"Failed to serialize settings: {ex.Message}", ex);
            }
        }

        public async Task<bool> ExistsAsync()
        {
            try
            {
                return await Task.FromResult(File.Exists(_settingsPath)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if settings file exists");
                return false;
            }
        }

        public async Task BackupAsync(AppSettings settings, string description)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            try
            {
                var backup = new SettingsBackup
                {
                    Id = Guid.NewGuid().ToString(),
                    CreatedAt = DateTime.Now,
                    Description = description ?? "Manual backup",
                    Settings = settings,
                    Version = "1.0",
                    Application = "WhisperKey"
                };

                var backupFileName = $"backup_{backup.CreatedAt:yyyyMMdd_HHmmss}_{backup.Id.Substring(0, 8)}.json";
                var backupFilePath = Path.Combine(_backupPath, backupFileName);

                var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(backupFilePath, json).ConfigureAwait(false);

                _logger.LogInformation("Settings backup created: {BackupId}", backup.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating settings backup");
                throw new InvalidOperationException($"Failed to create backup: {ex.Message}", ex);
            }
        }

        public async Task<AppSettings> RestoreFromBackupAsync(string backupId)
        {
            if (string.IsNullOrWhiteSpace(backupId))
                throw new ArgumentException("Backup ID cannot be empty", nameof(backupId));

            try
            {
                var backupFiles = await GetBackupFilesAsync().ConfigureAwait(false);
                var backupFile = backupFiles.FirstOrDefault(f => f.Contains(backupId));

                if (backupFile == null)
                {
                    throw new FileNotFoundException($"Backup with ID {backupId} not found");
                }

                var json = await File.ReadAllTextAsync(backupFile).ConfigureAwait(false);
                var backup = JsonSerializer.Deserialize<SettingsBackup>(json);

                if (backup?.Settings == null)
                {
                    throw new InvalidOperationException("Invalid backup file format");
                }

                _logger.LogInformation("Settings restored from backup: {BackupId}", backupId);
                return backup.Settings;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring settings from backup");
                throw;
            }
        }

        public async Task<SettingsBackup[]> GetBackupsAsync()
        {
            try
            {
                var backupFiles = await GetBackupFilesAsync().ConfigureAwait(false);
                var backups = new List<SettingsBackup>();

                foreach (var backupFile in backupFiles)
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(backupFile).ConfigureAwait(false);
                        var backup = JsonSerializer.Deserialize<SettingsBackup>(json);
                        if (backup != null)
                        {
                            backups.Add(backup);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error reading backup file: {BackupFile}", backupFile);
                        // Continue processing other backup files
                    }
                }

                return backups.OrderByDescending(b => b.CreatedAt).ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backups list");
                return Array.Empty<SettingsBackup>();
            }
        }

        public async Task DeleteBackupAsync(string backupId)
        {
            if (string.IsNullOrWhiteSpace(backupId))
                throw new ArgumentException("Backup ID cannot be empty", nameof(backupId));

            try
            {
                var backupFiles = await GetBackupFilesAsync().ConfigureAwait(false);
                var backupFile = backupFiles.FirstOrDefault(f => f.Contains(backupId));

                if (backupFile != null)
                {
                    File.Delete(backupFile);
                    _logger.LogInformation("Backup deleted: {BackupId}", backupId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting backup: {BackupId}", backupId);
                throw;
            }
        }

        public async Task<bool> ValidateAsync()
        {
            try
            {
                if (!await ExistsAsync())
                    return true; // No file to validate is fine

                var json = await File.ReadAllTextAsync(_settingsPath).ConfigureAwait(false);
                var settings = JsonSerializer.Deserialize<AppSettings>(json);

                return settings != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Settings file validation failed");
                return false;
            }
        }

        private async Task CreateAutoBackupAsync(AppSettings settings)
        {
            try
            {
                if (!await ExistsAsync())
                    return; // No existing file to backup

                // Only create backup if file was modified recently (within last hour)
                var fileInfo = new FileInfo(_settingsPath);
                if (DateTime.Now - fileInfo.LastWriteTime > TimeSpan.FromHours(1))
                {
                    var backup = new SettingsBackup
                    {
                        Id = Guid.NewGuid().ToString(),
                        CreatedAt = DateTime.Now,
                        Description = "Auto backup before save",
                        Settings = await LoadAsync().ConfigureAwait(false),
                        Version = "1.0",
                        Application = "WhisperKey"
                    };

                    var backupFileName = $"auto_backup_{backup.CreatedAt:yyyyMMdd_HHmmss}.json";
                    var backupFilePath = Path.Combine(_backupPath, backupFileName);

                    var json = JsonSerializer.Serialize(backup, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    await File.WriteAllTextAsync(backupFilePath, json).ConfigureAwait(false);

                    // Keep only last 5 auto backups
                    await CleanupOldAutoBackupsAsync().ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error creating auto backup");
                // Don't fail the save operation if backup fails
            }
        }

        private async Task CleanupOldAutoBackupsAsync()
        {
            try
            {
                var backupFiles = await GetBackupFilesAsync().ConfigureAwait(false);
                var autoBackups = backupFiles
                    .Where(f => Path.GetFileName(f).StartsWith("auto_backup_"))
                    .OrderByDescending(f => f)
                    .Skip(5); // Keep only 5 most recent

                foreach (var oldBackup in autoBackups)
                {
                    try
                    {
                        File.Delete(oldBackup);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error deleting old auto backup: {BackupFile}", oldBackup);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error cleaning up old auto backups");
            }
        }

        private async Task<string[]> GetBackupFilesAsync()
        {
            try
            {
                return await Task.FromResult(Directory.GetFiles(_backupPath, "*.json")).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting backup files");
                return Array.Empty<string>();
            }
        }
    }
}