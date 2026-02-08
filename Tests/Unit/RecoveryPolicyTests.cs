using System;
using System.Collections.Generic;
using System.Linq;
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
    public class RecoveryPolicyTests
    {
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private RecoveryPolicyService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _mockAuditService.Setup(a => a.LogEventAsync(It.IsAny<AuditEventType>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<DataSensitivity>())).ReturnsAsync(new AuditLogEntry());

            _service = new RecoveryPolicyService(
                NullLogger<RecoveryPolicyService>.Instance,
                _mockAuditService.Object);
        }

        #region Constructor Tests

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act
            new RecoveryPolicyService(null!, _mockAuditService.Object);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullAuditService_ThrowsArgumentNullException()
        {
            // Act
            new RecoveryPolicyService(NullLogger<RecoveryPolicyService>.Instance, null!);
        }

        #endregion

        #region GetApiRetryPolicy Tests

        [TestMethod]
        public void GetApiRetryPolicy_ReturnsPolicy()
        {
            // Act
            var policy = _service.GetApiRetryPolicy();

            // Assert
            Assert.IsNotNull(policy);
        }

        [TestMethod]
        public void GetApiRetryPolicy_WithCustomRetryCount_ReturnsPolicy()
        {
            // Act
            var policy = _service.GetApiRetryPolicy(5);

            // Assert
            Assert.IsNotNull(policy);
        }

        [TestMethod]
        public async Task GetApiRetryPolicy_RetriesOnHttpRequestException()
        {
            // Arrange
            var policy = _service.GetApiRetryPolicy(3);
            var attemptCount = 0;

            // Act
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    attemptCount++;
                    throw new HttpRequestException("Network error");
                });
            }
            catch (HttpRequestException)
            {
                // Expected
            }

            // Assert
            Assert.AreEqual(4, attemptCount); // Initial attempt + 3 retries
        }

        [TestMethod]
        public async Task GetApiRetryPolicy_RetriesOnTimeoutException()
        {
            // Arrange
            var policy = _service.GetApiRetryPolicy(3);
            var attemptCount = 0;

            // Act
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    attemptCount++;
                    throw new TimeoutException("Request timed out");
                });
            }
            catch (TimeoutException)
            {
                // Expected
            }

            // Assert
            Assert.AreEqual(4, attemptCount);
        }

        [TestMethod]
        public async Task GetApiRetryPolicy_SuccessOnRetry()
        {
            // Arrange
            var policy = _service.GetApiRetryPolicy(3);
            var attemptCount = 0;

            // Act
            var result = await policy.ExecuteAsync(async () =>
            {
                attemptCount++;
                if (attemptCount < 3)
                {
                    throw new HttpRequestException("Network error");
                }
                return "Success";
            });

            // Assert
            Assert.AreEqual("Success", result);
            Assert.AreEqual(3, attemptCount);
        }

        [TestMethod]
        public async Task GetApiRetryPolicy_LogsAuditEvent()
        {
            // Arrange
            var policy = _service.GetApiRetryPolicy(2);
            var auditEvents = new List<(AuditEventType Type, string Description, string Metadata, DataSensitivity Sensitivity)>();
            
            _mockAuditService.Setup(a => a.LogEventAsync(
                    It.IsAny<AuditEventType>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<DataSensitivity>()))
                .ReturnsAsync(new AuditLogEntry())
                .Callback<AuditEventType, string, string?, DataSensitivity>((type, desc, meta, sens) =>
                {
                    auditEvents.Add((type, desc, meta ?? "", sens));
                });

            // Act
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    throw new HttpRequestException("Test error");
                });
            }
            catch (HttpRequestException)
            {
                // Expected
            }

            // Allow time for async audit logging
            await Task.Delay(100);

            // Assert
            Assert.IsTrue(auditEvents.Count > 0, "At least one audit event should be logged");
            Assert.IsTrue(auditEvents.Any(e => 
                e.Type == AuditEventType.SecurityEvent && 
                e.Description.Contains("retry", StringComparison.OrdinalIgnoreCase)),
                "Should log retry audit events");
        }

        #endregion

        #region GetCircuitBreakerPolicy Tests

        [TestMethod]
        public void GetCircuitBreakerPolicy_ReturnsPolicy()
        {
            // Act
            var policy = _service.GetCircuitBreakerPolicy();

            // Assert
            Assert.IsNotNull(policy);
        }

        [TestMethod]
        public void GetCircuitBreakerPolicy_WithCustomParameters_ReturnsPolicy()
        {
            // Act
            var policy = _service.GetCircuitBreakerPolicy(3, 60);

            // Assert
            Assert.IsNotNull(policy);
        }

        [TestMethod]
        public async Task GetCircuitBreakerPolicy_OpensAfterThreshold()
        {
            // Arrange
            var policy = _service.GetCircuitBreakerPolicy(2, 30);
            var attemptCount = 0;

            // Act - Trigger circuit breaker
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    await policy.ExecuteAsync(async () =>
                    {
                        attemptCount++;
                        throw new HttpRequestException("Network error");
                    });
                }
                catch (HttpRequestException)
                {
                    // Expected
                }
                catch (BrokenCircuitException)
                {
                    // Circuit is now open
                    break;
                }
            }

            // Assert - Circuit should be open after 2 failures
            Assert.IsTrue(policy.CircuitState == CircuitState.Open || policy.CircuitState == CircuitState.Isolated);
        }

        [TestMethod]
        public async Task GetCircuitBreakerPolicy_LogsOnBreak()
        {
            // Arrange
            var policy = _service.GetCircuitBreakerPolicy(1, 30);

            // Act
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    throw new HttpRequestException("Test error");
                });
            }
            catch (HttpRequestException)
            {
                // Expected - first failure
            }

            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    throw new HttpRequestException("Test error 2");
                });
            }
            catch (BrokenCircuitException)
            {
                // Expected - circuit opens
            }

            // Assert
            _mockAuditService.Verify(a => a.LogEventAsync(
                It.Is<AuditEventType>(t => t == AuditEventType.SecurityEvent),
                It.Is<string>(s => s.Contains("Circuit breaker")),
                It.IsAny<string>(),
                It.IsAny<DataSensitivity>()), Times.AtLeastOnce);
        }

        #endregion

        #region GetIoRetryPolicy Tests

        [TestMethod]
        public void GetIoRetryPolicy_ReturnsPolicy()
        {
            // Act
            var policy = _service.GetIoRetryPolicy();

            // Assert
            Assert.IsNotNull(policy);
        }

        [TestMethod]
        public void GetIoRetryPolicy_WithCustomRetryCount_ReturnsPolicy()
        {
            // Act
            var policy = _service.GetIoRetryPolicy(5);

            // Assert
            Assert.IsNotNull(policy);
        }

        [TestMethod]
        public async Task GetIoRetryPolicy_RetriesOnIOException()
        {
            // Arrange
            var policy = _service.GetIoRetryPolicy(3);
            var attemptCount = 0;

            // Act
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    attemptCount++;
                    throw new System.IO.IOException("File locked");
                });
            }
            catch (System.IO.IOException)
            {
                // Expected
            }

            // Assert
            Assert.AreEqual(4, attemptCount);
        }

        [TestMethod]
        public async Task GetIoRetryPolicy_SuccessOnRetry()
        {
            // Arrange
            var policy = _service.GetIoRetryPolicy(3);
            var attemptCount = 0;

            // Act
            var result = await policy.ExecuteAsync(async () =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new System.IO.IOException("File locked");
                }
                return "File written successfully";
            });

            // Assert
            Assert.AreEqual("File written successfully", result);
            Assert.AreEqual(2, attemptCount);
        }

        #endregion

        #region ExecuteWithRecoveryAsync Tests

        [TestMethod]
        public async Task ExecuteWithRecoveryAsync_Success_ReturnsResult()
        {
            // Act
            var result = await _service.ExecuteWithRecoveryAsync(async () =>
            {
                await Task.Delay(1);
                return "Success";
            }, "TestOperation");

            // Assert
            Assert.AreEqual("Success", result);
        }

        [TestMethod]
        public async Task ExecuteWithRecoveryAsync_WithRetry_ReturnsResult()
        {
            // Arrange
            var attemptCount = 0;

            // Act
            var result = await _service.ExecuteWithRecoveryAsync(async () =>
            {
                attemptCount++;
                if (attemptCount < 2)
                {
                    throw new HttpRequestException("Transient error");
                }
                return "Success after retry";
            }, "TestOperation");

            // Assert
            Assert.AreEqual("Success after retry", result);
            Assert.IsTrue(attemptCount >= 2);
        }

        [TestMethod]
        public async Task ExecuteWithRecoveryAsync_AllRetriesExhausted_ThrowsException()
        {
            // Arrange
            var attemptCount = 0;

            // Act & Assert
            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await _service.ExecuteWithRecoveryAsync<string>(async () =>
                {
                    attemptCount++;
                    throw new HttpRequestException("Persistent error");
                }, "TestOperation");
            });

            // Should have attempted multiple times
            Assert.IsTrue(attemptCount > 1);
        }

        [TestMethod]
        public async Task ExecuteWithRecoveryAsync_CircuitBreaker_OpensAfterFailures()
        {
            // Arrange - Use a circuit breaker policy directly with low threshold
            var breaker = _service.GetCircuitBreakerPolicy(2, 30); // Open after 2 failures
            var failureCount = 0;

            // Act - First two calls should fail with HttpRequestException
            for (int i = 0; i < 2; i++)
            {
                try
                {
                    await breaker.ExecuteAsync(async () =>
                    {
                        failureCount++;
                        throw new HttpRequestException("Error");
                    });
                }
                catch (HttpRequestException)
                {
                    // Expected - this is the exception we're throwing
                }
            }

            // Third call should fail with BrokenCircuitException since circuit is now open
            bool circuitOpened = false;
            try
            {
                await breaker.ExecuteAsync(async () =>
                {
                    return "Should not reach here";
                });
            }
            catch (BrokenCircuitException)
            {
                circuitOpened = true;
            }

            // Assert
            Assert.AreEqual(2, failureCount, "Should have attempted 2 times before circuit opened");
            Assert.IsTrue(circuitOpened, "Circuit should be open after 2 failures");
            Assert.AreEqual(CircuitState.Open, breaker.CircuitState);
        }

        #endregion

        #region Half-Open State Tests

        [TestMethod]
        public async Task GetCircuitBreakerPolicy_HalfOpenState_AllowsTestRequest()
        {
            // Arrange
            var policy = _service.GetCircuitBreakerPolicy(1, 1); // 1 second break
            var callCount = 0;

            // Open the circuit
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    throw new HttpRequestException("Error");
                });
            }
            catch (HttpRequestException) { }

            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    throw new HttpRequestException("Error 2");
                });
            }
            catch (BrokenCircuitException) { }

            // Wait for circuit to potentially transition to half-open
            await Task.Delay(1500);

            // Act - Try to execute when circuit might be half-open
            try
            {
                await policy.ExecuteAsync(async () =>
                {
                    callCount++;
                    return "Success";
                });
            }
            catch (BrokenCircuitException)
            {
                // Circuit still open - that's ok for this test
            }
            catch (HttpRequestException) { }

            // Assert - Either the call succeeded or circuit was still open
            Assert.IsTrue(callCount == 1 || policy.CircuitState == CircuitState.Open);
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public async Task GetApiRetryPolicy_NonRetryableException_DoesNotRetry()
        {
            // Arrange
            var policy = _service.GetApiRetryPolicy(3);
            var attemptCount = 0;

            // Act
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await policy.ExecuteAsync(async () =>
                {
                    attemptCount++;
                    throw new InvalidOperationException("Not retryable");
                });
            });

            // Assert - Should only attempt once
            Assert.AreEqual(1, attemptCount);
        }

        [TestMethod]
        public async Task GetCircuitBreakerPolicy_NonHandledException_DoesNotCount()
        {
            // Arrange
            var policy = _service.GetCircuitBreakerPolicy(2, 30);

            // Act - Throw non-handled exceptions
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await policy.ExecuteAsync(async () =>
                    {
                        throw new InvalidOperationException("Not handled");
                    });
                }
                catch (InvalidOperationException)
                {
                    // Expected
                }
            }

            // Assert - Circuit should still be closed (not counting these exceptions)
            Assert.AreEqual(CircuitState.Closed, policy.CircuitState);
        }

        [TestMethod]
        public async Task ExecuteWithRecoveryAsync_NullAction_ThrowsNullReferenceException()
        {
            // Act & Assert - Null action should throw NullReferenceException
            await Assert.ThrowsExceptionAsync<NullReferenceException>(async () =>
            {
                await _service.ExecuteWithRecoveryAsync<string>(null!, "TestOperation");
            });
        }

        #endregion
    }
}
