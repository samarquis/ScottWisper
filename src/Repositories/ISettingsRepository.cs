using System.Threading.Tasks;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Repositories
{
    /// <summary>
    /// Repository interface for settings persistence and retrieval
    /// </summary>
    public interface ISettingsRepository
    {
        /// <summary>
        /// Load settings from storage
        /// </summary>
        /// <returns>The loaded settings, or default settings if none exist</returns>
        Task<AppSettings> LoadAsync();

        /// <summary>
        /// Save settings to storage
        /// </summary>
        /// <param name="settings">The settings to save</param>
        /// <returns>Task representing the save operation</returns>
        Task SaveAsync(AppSettings settings);

        /// <summary>
        /// Check if settings file exists
        /// </summary>
        /// <returns>True if settings file exists, false otherwise</returns>
        Task<bool> ExistsAsync();

        /// <summary>
        /// Create a backup of current settings
        /// </summary>
        /// <param name="settings">The settings to backup</param>
        /// <param name="description">Description for the backup</param>
        /// <returns>Task representing the backup operation</returns>
        Task BackupAsync(AppSettings settings, string description);

        /// <summary>
        /// Restore settings from a backup
        /// </summary>
        /// <param name="backupId">The backup ID to restore</param>
        /// <returns>The restored settings</returns>
        Task<AppSettings> RestoreFromBackupAsync(string backupId);

        /// <summary>
        /// Get list of available backups
        /// </summary>
        /// <returns>List of available backups</returns>
        Task<SettingsBackup[]> GetBackupsAsync();

        /// <summary>
        /// Delete a backup
        /// </summary>
        /// <param name="backupId">The backup ID to delete</param>
        /// <returns>Task representing the delete operation</returns>
        Task DeleteBackupAsync(string backupId);

        /// <summary>
        /// Validate settings file integrity
        /// </summary>
        /// <returns>True if settings file is valid, false otherwise</returns>
        Task<bool> ValidateAsync();
    }
}