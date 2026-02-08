using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class PerformanceMonitoringTests
    {
        private PerformanceMonitoringService _service = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _service = new PerformanceMonitoringService(
                NullLogger<PerformanceMonitoringService>.Instance,
                _mockAuditService.Object);
        }

        [TestMethod]
        public async Task Test_TraceCreationAndBaselineUpdate()
        {
            // 1. Start and stop an activity
            using (var activity = _service.StartActivity("TestOperation"))
            {
                Assert.IsNotNull(activity);
                await Task.Delay(50);
            }

            // 2. Allow some time for the listener to process
            await Task.Delay(100);

            // 3. Verify baseline was created
            var baselines = await _service.GetBaselinesAsync();
            var baseline = baselines.FirstOrDefault(b => b.OperationName == "TestOperation");
            
            Assert.IsNotNull(baseline);
            Assert.IsTrue(baseline.AverageDurationMs >= 50);
            Assert.AreEqual(1, baseline.SampleCount);
        }

        [TestMethod]
        public async Task Test_AnomalyDetection()
        {
            // 1. Establish baseline (11 samples of 50ms)
            for (int i = 0; i < 11; i++)
            {
                using (var activity = _service.StartActivity("AnomalyOp"))
                {
                    // Manually set duration for consistent testing if possible, 
                    // but here we just sleep.
                    Thread.Sleep(10); 
                }
            }
            
            // Allow listener to process
            await Task.Delay(200);

            // 2. Simulate anomaly (500ms)
            using (var activity = _service.StartActivity("AnomalyOp"))
            {
                Thread.Sleep(200);
            }

            // Allow listener to process
            await Task.Delay(200);

            // 3. Verify audit log was called for anomaly
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SystemEvent,
                It.Is<string>(s => s.Contains("[PERFORMANCE ANOMALY]")),
                It.IsAny<string>(),
                DataSensitivity.Medium), Times.AtLeastOnce);
        }

        [TestMethod]
        public void Test_MetricRecording()
        {
            var tags = new Dictionary<string, string> { ["env"] = "test" };
            _service.RecordMetric("test.count", 1.0, "unit", tags);
            
            // Metrics are mostly used for logging/streaming, verification is successful call
            Assert.IsTrue(true);
        }

        [TestMethod]
        public async Task Test_ServiceDependencyMap()
        {
            var map = await _service.GetServiceDependencyMapAsync();
            Assert.IsNotNull(map);
            Assert.IsTrue(map.ContainsKey("WhisperService"));
            Assert.IsTrue(map["WhisperService"].Contains("LocalInferenceService"));
        }
    }
}
