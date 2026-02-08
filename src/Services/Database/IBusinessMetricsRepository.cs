using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services.Database
{
    /// <summary>
    /// Interface for business metrics persistence
    /// </summary>
    public interface IBusinessMetricsRepository
    {
        /// <summary>
        /// Saves a KPI snapshot
        /// </summary>
        Task SaveSnapshotAsync(BusinessKpiSnapshot snapshot);
        
        /// <summary>
        /// Gets snapshots within a time range
        /// </summary>
        Task<List<BusinessKpiSnapshot>> GetSnapshotsAsync(DateTime? start = null, DateTime? end = null);
        
        /// <summary>
        /// Gets the most recent snapshot
        /// </summary>
        Task<BusinessKpiSnapshot?> GetLatestSnapshotAsync();
    }
}
