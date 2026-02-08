using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for managing application configuration, drift detection, and environment parity
    /// </summary>
    public interface IConfigurationManagementService
    {
        /// <summary>
        /// Captures a snapshot of the current configuration
        /// </summary>
        Task<ConfigurationSnapshot> CaptureSnapshotAsync();
        
        /// <summary>
        /// Validates current configuration against a baseline
        /// </summary>
        Task<DriftReport> ValidateParityAsync();
        
        /// <summary>
        /// Tracks a specific configuration change
        /// </summary>
        Task TrackChangeAsync(string key, string oldValue, string newValue);
        
        /// <summary>
        /// Gets the configuration change history
        /// </summary>
        Task<List<ConfigurationSnapshot>> GetHistoryAsync();
        
        /// <summary>
        /// Establishes the current configuration as the new baseline
        /// </summary>
        Task SetBaselineAsync();
    }
}
