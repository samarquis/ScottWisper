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
    public class GracefulDegradationTests
    {
        private GracefulDegradationService _service = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _service = new GracefulDegradationService(
                NullLogger<GracefulDegradationService>.Instance,
                _mockAuditService.Object);
        }

        [TestMethod]
        public async Task Test_ExecuteWithFallback_Success()
        {
            var result = await _service.ExecuteWithFallbackAsync(
                () => Task.FromResult("Success"),
                "Fallback",
                "TestService");
            
            Assert.AreEqual("Success", result);
            Assert.IsFalse(_service.IsInDegradedMode());
        }

        [TestMethod]
        public async Task Test_ExecuteWithFallback_Failure()
        {
            var result = await _service.ExecuteWithFallbackAsync<string>(
                () => throw new Exception("Operation failed"),
                "FallbackValue",
                "NonCriticalService");
            
            Assert.AreEqual("FallbackValue", result);
            
            var health = await _service.GetServiceHealthAsync();
            Assert.IsFalse(health["NonCriticalService"].IsHealthy);
            Assert.AreEqual(1, health["NonCriticalService"].FailureCount);
        }

        [TestMethod]
        public void Test_CriticalServiceFailure_Logging()
        {
            _service.ReportServiceFailure("WhisperService", new Exception("Critical error"));
            
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.Error,
                It.Is<string>(s => s.Contains("WhisperService")),
                It.IsAny<string>(),
                DataSensitivity.Medium), Times.Once);
        }

        [TestMethod]
        public void Test_TransitionToDegradedMode()
        {
            // Fail non-critical service 6 times
            for (int i = 0; i < 6; i++)
            {
                _service.ReportServiceFailure("SystemTrayService", new Exception("Minor error"));
            }

            Assert.IsTrue(_service.IsInDegradedMode());
        }
    }
}
