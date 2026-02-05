using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Polly.CircuitBreaker;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Recovery;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class ErrorRecoveryTests
    {
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private RecoveryPolicyService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _service = new RecoveryPolicyService(NullLogger<RecoveryPolicyService>.Instance, _mockAuditService.Object);
        }

        [TestMethod]
        public async Task ApiRetryPolicy_RetriesOnTransientFailure()
        {
            // Arrange
            var policy = _service.GetApiRetryPolicy(2);
            int calls = 0;

            // Act
            try
            {
                await policy.ExecuteAsync(() =>
                {
                    calls++;
                    throw new HttpRequestException("Transient error", null, HttpStatusCode.ServiceUnavailable);
                });
            }
            catch (HttpRequestException) { }

            // Assert
            Assert.AreEqual(3, calls); // 1 initial + 2 retries
            _mockAuditService.Verify(a => a.LogEventAsync(It.IsAny<AuditEventType>(), It.Is<string>(s => s.Contains("retry")), It.IsAny<string>(), It.IsAny<DataSensitivity>()), Times.Exactly(2));
        }

        [TestMethod]
        public async Task CircuitBreaker_OpensAfterThreshold()
        {
            // Arrange
            var policy = _service.GetCircuitBreakerPolicy(2, 1);
            
            // Act & Assert
            // 1st failure
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => 
                policy.ExecuteAsync(() => throw new HttpRequestException()));
            
            // 2nd failure - should open breaker
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => 
                policy.ExecuteAsync(() => throw new HttpRequestException()));

            // 3rd call - should throw BrokenCircuitException immediately
            await Assert.ThrowsExceptionAsync<BrokenCircuitException>(() => 
                policy.ExecuteAsync(() => Task.FromResult(true)));
            
            _mockAuditService.Verify(a => a.LogEventAsync(It.IsAny<AuditEventType>(), It.Is<string>(s => s.Contains("opened")), It.IsAny<string>(), It.IsAny<DataSensitivity>()), Times.Once);
        }

        [TestMethod]
        public async Task IoRetryPolicy_RetriesOnIoException()
        {
            // Arrange
            var policy = _service.GetIoRetryPolicy(2);
            int calls = 0;

            // Act
            try
            {
                await policy.ExecuteAsync(() =>
                {
                    calls++;
                    throw new System.IO.IOException("File locked");
                });
            }
            catch (System.IO.IOException) { }

            // Assert
            Assert.AreEqual(3, calls);
        }
    }
}
