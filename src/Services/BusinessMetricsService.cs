using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;
using WhisperKey.Services.Database;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of business metrics tracking and analytics service
    /// </summary>
    public class BusinessMetricsService : IBusinessMetricsService
    {
        private readonly ILogger<BusinessMetricsService> _logger;
        private readonly IBusinessMetricsRepository _repository;
        private readonly IAuditLoggingService _auditService;

        public BusinessMetricsService(
            ILogger<BusinessMetricsService> logger,
            IBusinessMetricsRepository repository,
            IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public async Task RecordEventAsync(string eventName, Dictionary<string, object>? data = null)
        {
            try
            {
                var snapshot = await GetCurrentKpisAsync();
                
                // Update internal counters based on event
                switch (eventName)
                {
                    case "TranscriptionCompleted":
                        snapshot.TotalTranscriptions++;
                        if (data != null && data.TryGetValue("Cost", out var cost))
                            snapshot.TotalCost += (decimal)cost;
                        break;
                    case "TranscriptionError":
                        snapshot.ErrorCount++;
                        break;
                }

                // Update success rate
                if (snapshot.TotalTranscriptions + snapshot.ErrorCount > 0)
                {
                    snapshot.SuccessRate = (double)snapshot.TotalTranscriptions / (snapshot.TotalTranscriptions + snapshot.ErrorCount);
                }

                snapshot.Timestamp = DateTime.UtcNow;
                await _repository.SaveSnapshotAsync(snapshot);
                
                _logger.LogTrace("Business event recorded: {Event}", eventName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to record business event {Event}", eventName);
            }
        }

        public async Task<BusinessKpiSnapshot> GetCurrentKpisAsync()
        {
            return await _repository.GetLatestSnapshotAsync() ?? new BusinessKpiSnapshot();
        }

        public async Task<MetricTrend> GetTrendAsync(string metricName, TimeSpan duration)
        {
            var cutoff = DateTime.UtcNow.Subtract(duration);
            var snapshots = await _repository.GetSnapshotsAsync(start: cutoff);
            
            var trend = new MetricTrend { MetricName = metricName };
            foreach (var s in snapshots.OrderBy(s => s.Timestamp))
            {
                double value = metricName switch
                {
                    "TotalTranscriptions" => s.TotalTranscriptions,
                    "TotalCost" => (double)s.TotalCost,
                    "SuccessRate" => s.SuccessRate,
                    _ => 0
                };
                trend.Points.Add(new DataPoint { Time = s.Timestamp, Value = value });
            }
            
            return trend;
        }

        public async Task<string> GenerateDailyReportAsync()
        {
            var kpis = await GetCurrentKpisAsync();
            var report = $"Daily Business Report - {DateTime.Now:yyyy-MM-dd}\n" +
                         $"==========================================\n" +
                         $"Total Transcriptions: {kpis.TotalTranscriptions}\n" +
                         $"Estimated Total Cost: ${kpis.TotalCost:F2}\n" +
                         $"Overall Success Rate: {kpis.SuccessRate:P1}\n" +
                         $"Total Errors Encountered: {kpis.ErrorCount}\n";
            
            await _auditService.LogEventAsync(
                AuditEventType.SystemEvent,
                "Daily business report generated.",
                report,
                DataSensitivity.Medium);

            return report;
        }
    }
}
