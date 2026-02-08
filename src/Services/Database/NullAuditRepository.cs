using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services.Database
{
    /// <summary>
    /// Null implementation of audit repository for testing and benchmarks
    /// </summary>
    public class NullAuditRepository : IAuditRepository
    {
        public Task AddAsync(AuditLogEntry entry) => Task.CompletedTask;
        
        public Task<List<AuditLogEntry>> GetAsync(DateTime? startDate = null, DateTime? endDate = null, AuditEventType? type = null)
        {
            return Task.FromResult(new List<AuditLogEntry>());
        }
        
        public Task ClearOldAsync(int daysToKeep) => Task.CompletedTask;
    }
}
