using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services.Database
{
    /// <summary>
    /// JSON-based implementation of business metrics repository
    /// </summary>
    public class JsonBusinessMetricsRepository : IBusinessMetricsRepository
    {
        private readonly JsonDatabaseService _db;
        private const string COLLECTION_NAME = "business_metrics";

        public JsonBusinessMetricsRepository(JsonDatabaseService db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task SaveSnapshotAsync(BusinessKpiSnapshot snapshot)
        {
            await _db.UpsertAsync(COLLECTION_NAME, snapshot, s => s.Id == snapshot.Id);
        }

        public async Task<List<BusinessKpiSnapshot>> GetSnapshotsAsync(DateTime? start = null, DateTime? end = null)
        {
            return await _db.QueryListAsync<BusinessKpiSnapshot>(COLLECTION_NAME, s =>
            {
                if (start.HasValue && s.Timestamp < start.Value) return false;
                if (end.HasValue && s.Timestamp > end.Value) return false;
                return true;
            });
        }

        public async Task<BusinessKpiSnapshot?> GetLatestSnapshotAsync()
        {
            var results = await GetSnapshotsAsync();
            return results.OrderByDescending(s => s.Timestamp).FirstOrDefault();
        }
    }
}
