using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class LogAnalysisTests
    {
        private LogAnalysisService _service = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private string _testLogDir;

        [TestInitialize]
        public void Setup()
        {
            // Setup a temp directory for logs matching the service's expectations
            var tempBase = Path.Combine(Path.GetTempPath(), "WhisperKeyTests_" + Guid.NewGuid().ToString());
            _testLogDir = Path.Combine(tempBase, "WhisperKey", "logs");
            Directory.CreateDirectory(_testLogDir);
            
            // Mock environment variable for AppData
            Environment.SetEnvironmentVariable("AppData", tempBase);

            _mockAuditService = new Mock<IAuditLoggingService>();
            
            _service = new LogAnalysisService(
                NullLogger<LogAnalysisService>.Instance,
                _mockAuditService.Object,
                _testLogDir);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testLogDir))
                Directory.Delete(_testLogDir, true);
        }

        [TestMethod]
        public async Task Test_LogParsingAndPatternRecognition()
        {
            // 1. Create a dummy log file
            var logPath = Path.Combine(_testLogDir, "whisperkey-20260207.log");
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var lines = new[]
            {
                $"[{timestamp} INF] [cid-1] [Source] User logged in with ID 123",
                $"[{timestamp} INF] [cid-1] [Source] User logged in with ID 456",
                $"[{timestamp} ERR] [cid-2] [Source] Connection failed to host server-1",
            };
            File.WriteAllLines(logPath, lines);

            // 2. Run analysis
            await _service.AnalyzeLogsAsync();

            // 3. Verify patterns
            var patterns = await _service.IdentifyPatternsAsync();
            Assert.IsTrue(patterns.Any(p => p.MessageTemplate.Contains("User logged in with ID {N}")));
            Assert.AreEqual(2, patterns.First(p => p.MessageTemplate.Contains("User logged in")).OccurrenceCount);
        }

        [TestMethod]
        public async Task Test_AnomalyDetection_ErrorSpike()
        {
            // 1. Create a log file with many errors
            var logPath = Path.Combine(_testLogDir, "whisperkey-anomaly.log");
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            var lines = new List<string>();
            for (int i = 0; i < 60; i++)
            {
                lines.Add($"[{timestamp} ERR] [cid-{i}] [Source] Something went wrong {i}");
            }
            File.WriteAllLines(logPath, lines);

            // 2. Run analysis
            await _service.AnalyzeLogsAsync();

            // 3. Verify anomaly log
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SystemEvent,
                It.Is<string>(s => s.Contains("[LOG ANOMALY]")),
                null,
                DataSensitivity.Medium), Times.Once);
        }

        [TestMethod]
        public async Task Test_InsightGeneration()
        {
            // 1. Setup patterns via analysis
            var logPath = Path.Combine(_testLogDir, "whisperkey-insights.log");
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            File.WriteAllLines(logPath, new[] { $"[{timestamp} INF] [cid] [src] Test message" });
            
            await _service.AnalyzeLogsAsync();

            // 2. Generate insights
            var insights = await _service.GenerateInsightsAsync();
            Assert.IsTrue(insights.Any());
            Assert.IsTrue(insights.First().Contains("Test message"));
        }
    }
}
