using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services.Database
{
    /// <summary>
    /// Interface for audit log persistence and retrieval
    /// </summary>
    public interface IAuditRepository
    {
        /// <summary>
        /// Persists a new audit log entry
        /// </summary>
        Task AddAsync(AuditLogEntry entry);
        
        /// <summary>
        /// Retrieves logs matching the given criteria
        /// </summary>
        Task<List<AuditLogEntry>> GetAsync(DateTime? startDate = null, DateTime? endDate = null, AuditEventType? type = null);
        
        /// <summary>
        /// Clears logs older than the specified days
        /// </summary>
        Task ClearOldAsync(int daysToKeep);
    }
}
