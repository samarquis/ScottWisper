using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Database;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class BusinessMetricsTests
    {
        private BusinessMetricsService _service = null!;
        private Mock<IBusinessMetricsRepository> _mockRepo = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private List<BusinessKpiSnapshot> _snapshots = new();

        [TestInitialize]
        public void Setup()
        {
            _mockRepo = new Mock<IBusinessMetricsRepository>();
            _mockAuditService = new Mock<IAuditLoggingService>();
            _snapshots = new List<BusinessKpiSnapshot>();

            _mockRepo.Setup(r => r.GetSnapshotsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>()))
                .ReturnsAsync((DateTime? start, DateTime? end) => 
                {
                    var query = _snapshots.AsQueryable();
                    if (start.HasValue) query = query.Where(s => s.Timestamp >= start.Value);
                    if (end.HasValue) query = query.Where(s => s.Timestamp <= end.Value);
                    return query.ToList();
                });

            _mockRepo.Setup(r => r.GetLatestSnapshotAsync())
                .ReturnsAsync(() => _snapshots.OrderByDescending(s => s.Timestamp).FirstOrDefault());

            _mockRepo.Setup(r => r.SaveSnapshotAsync(It.IsAny<BusinessKpiSnapshot>()))
                .Callback<BusinessKpiSnapshot>(s => 
                {
                    var existing = _snapshots.FirstOrDefault(x => x.Id == s.Id);
                    if (existing != null) _snapshots.Remove(existing);
                    _snapshots.Add(s);
                })
                .Returns(Task.CompletedTask);

            _service = new BusinessMetricsService(
                NullLogger<BusinessMetricsService>.Instance,
                _mockRepo.Object,
                _mockAuditService.Object);
        }

        [TestMethod]
        public async Task Test_RecordEvent_TranscriptionCompleted()
        {
            await _service.RecordEventAsync("TranscriptionCompleted", new Dictionary<string, object> { ["Cost"] = 0.05m });
            
            var kpis = await _service.GetCurrentKpisAsync();
            Assert.AreEqual(1, kpis.TotalTranscriptions);
            Assert.AreEqual(0.05m, kpis.TotalCost);
            Assert.AreEqual(1.0, kpis.SuccessRate);
        }

        [TestMethod]
        public async Task Test_RecordEvent_TranscriptionError()
        {
            await _service.RecordEventAsync("TranscriptionCompleted");
            await _service.RecordEventAsync("TranscriptionError");
            
            var kpis = await _service.GetCurrentKpisAsync();
            Assert.AreEqual(1, kpis.TotalTranscriptions);
            Assert.AreEqual(1, kpis.ErrorCount);
            Assert.AreEqual(0.5, kpis.SuccessRate);
        }

        [TestMethod]
        public async Task Test_TrendAnalysis()
        {
            // Setup history
            var now = DateTime.UtcNow;
            _snapshots.Add(new BusinessKpiSnapshot { Timestamp = now.AddDays(-2), TotalTranscriptions = 10 });
            _snapshots.Add(new BusinessKpiSnapshot { Timestamp = now.AddDays(-1), TotalTranscriptions = 20 });
            _snapshots.Add(new BusinessKpiSnapshot { Timestamp = now, TotalTranscriptions = 35 });

            var trend = await _service.GetTrendAsync("TotalTranscriptions", TimeSpan.FromDays(7));
            
            Assert.AreEqual(3, trend.Points.Count);
            Assert.AreEqual(10, trend.Points[0].Value);
            Assert.AreEqual(35, trend.Points[2].Value);
        }

        [TestMethod]
        public async Task Test_ReportGeneration()
        {
            await _service.RecordEventAsync("TranscriptionCompleted");
            var report = await _service.GenerateDailyReportAsync();
            
            Assert.IsTrue(report.Contains("Total Transcriptions: 1"));
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SystemEvent,
                It.IsAny<string>(),
                It.IsAny<string>(),
                DataSensitivity.Medium), Times.Once);
        }
    }
}
