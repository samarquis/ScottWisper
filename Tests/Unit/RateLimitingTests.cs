using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class RateLimitingTests
    {
        private RateLimitingService _service = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _service = new RateLimitingService(
                NullLogger<RateLimitingService>.Instance,
                _mockAuditService.Object);
        }

        [TestMethod]
        public void Test_ConsumeQuota_Success()
        {
            // Initial consumption should succeed
            bool allowed = _service.TryConsume("Transcription");
            Assert.IsTrue(allowed);
        }

        [TestMethod]
        public void Test_ConsumeQuota_Throttling()
        {
            // Transcription has baseline of 10
            for (int i = 0; i < 10; i++)
            {
                _service.TryConsume("Transcription");
            }

            // 11th should fail
            bool allowed = _service.TryConsume("Transcription");
            Assert.IsFalse(allowed);
            
            var waitTime = _service.GetWaitTime("Transcription");
            Assert.IsTrue(waitTime > TimeSpan.Zero);

            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SecurityEvent,
                It.Is<string>(s => s.Contains("[RATE LIMIT]")),
                null,
                DataSensitivity.Low), Times.Once);
        }

        [TestMethod]
        public void Test_AdaptiveAdjustments()
        {
            // Reduce limits by 50%
            _service.AdjustLimits(0.5);
            
            // Transcription now has limit of 5
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(_service.TryConsume("Transcription"));
            }

            Assert.IsFalse(_service.TryConsume("Transcription"));
        }
    }
}
