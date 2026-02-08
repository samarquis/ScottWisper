using System;
using System.Collections.Generic;
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
    public class LoadTestingTests
    {
        private LoadTestingService _service = null!;
        private Mock<IWhisperService> _mockWhisper = null!;
        private Mock<IPerformanceMonitoringService> _mockPerformance = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockWhisper = new Mock<IWhisperService>();
            _mockPerformance = new Mock<IPerformanceMonitoringService>();
            _mockAuditService = new Mock<IAuditLoggingService>();

            _mockWhisper.Setup(w => w.TranscribeAudioAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync("Transcription");

            _service = new LoadTestingService(
                NullLogger<LoadTestingService>.Instance,
                _mockWhisper.Object,
                _mockPerformance.Object,
                _mockAuditService.Object);
        }

        [TestMethod]
        public async Task Test_TranscriptionLoadTest()
        {
            var result = await _service.RunTranscriptionLoadTestAsync(concurrency: 5, iterations: 2);
            
            Assert.IsTrue(result.Success);
            Assert.AreEqual(10, result.TotalOperations);
            Assert.AreEqual(10, result.SuccessfulOperations);
            Assert.IsTrue(result.ThroughputPerSecond > 0);
            
            _mockWhisper.Verify(w => w.TranscribeAudioAsync(It.IsAny<byte[]>(), null), Times.Exactly(10));
        }

        [TestMethod]
        public async Task Test_EventStressTest()
        {
            var result = await _service.RunEventStressTestAsync(eventsPerSecond: 10, duration: TimeSpan.FromSeconds(1));
            
            Assert.IsTrue(result.Success);
            Assert.IsTrue(result.TotalOperations >= 10);
        }

        [TestMethod]
        public async Task Test_ScalabilityMetrics()
        {
            var metrics = await _service.GetScalabilityMetricsAsync();
            
            Assert.IsNotNull(metrics);
            Assert.IsTrue(metrics.ThreadPoolUtilization >= 0);
            Assert.IsTrue(metrics.IsScalingHealthy);
        }
    }
}
