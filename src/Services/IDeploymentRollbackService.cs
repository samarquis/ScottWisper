using System;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for managing application deployment rollbacks and version history
    /// </summary>
    public interface IDeploymentRollbackService
    {
        /// <summary>
        /// Records that the application started successfully
        /// </summary>
        Task RecordStartupSuccessAsync();
        
        /// <summary>
        /// Records a startup failure
        /// </summary>
        Task RecordStartupFailureAsync(string error);
        
        /// <summary>
        /// Creates a backup of the current configuration
        /// </summary>
        Task CreateConfigurationBackupAsync();
        
        /// <summary>
        /// Rolls back the application to the last known stable version
        /// </summary>
        Task<DeploymentResult> InitiateRollbackAsync();
        
        /// <summary>
        /// Gets the current deployment history
        /// </summary>
        Task<DeploymentHistory> GetHistoryAsync();
        
        /// <summary>
        /// Checks if a rollback is needed based on recent failures
        /// </summary>
        bool IsRollbackRequired();
    }
}
