using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services.Recovery
{
    /// <summary>
    /// Centralized service for managing error recovery policies (Retry, Circuit Breaker, Fallback).
    /// </summary>
    public class RecoveryPolicyService : IRecoveryPolicyService
    {
        private readonly ILogger<RecoveryPolicyService> _logger;
        private readonly IAuditLoggingService _auditService;

        public RecoveryPolicyService(ILogger<RecoveryPolicyService> logger, IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        /// <summary>
        /// Creates a retry policy for transient network and API failures.
        /// </summary>
        public AsyncRetryPolicy GetApiRetryPolicy(int retryCount = 3)
        {
            return Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .WaitAndRetryAsync(
                    retryCount,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogWarning(exception, "Retry {Attempt} after {Delay}ms due to {Message}", 
                            retryAttempt, timeSpan.TotalMilliseconds, exception.Message);
                        
                        _ = _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            $"Transient failure retry: Attempt {retryAttempt}",
                            exception.Message,
                            DataSensitivity.Low);
                    });
        }

        /// <summary>
        /// Creates a circuit breaker policy for high-frequency operations.
        /// </summary>
        public AsyncCircuitBreakerPolicy GetCircuitBreakerPolicy(int exceptionsAllowedBeforeBreaking = 5, int durationOfBreakSeconds = 30)
        {
            return Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking,
                    TimeSpan.FromSeconds(durationOfBreakSeconds),
                    onBreak: (exception, duration) =>
                    {
                        _logger.LogCritical("Circuit breaker OPEN for {Duration}s due to {Message}", duration.TotalSeconds, exception.Message);
                        _ = _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            "Circuit breaker opened",
                            $"Duration: {duration.TotalSeconds}s, Reason: {exception.Message}",
                            DataSensitivity.Medium);
                    },
                    onReset: () =>
                    {
                        _logger.LogInformation("Circuit breaker CLOSED");
                        _ = _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            "Circuit breaker closed",
                            null,
                            DataSensitivity.Low);
                    });
        }

        /// <summary>
        /// Creates a retry policy for transient I/O failures (file locks, etc).
        /// </summary>
        public AsyncRetryPolicy GetIoRetryPolicy(int retryCount = 3)
        {
            return Policy
                .Handle<System.IO.IOException>()
                .WaitAndRetryAsync(
                    retryCount,
                    retryAttempt => TimeSpan.FromMilliseconds(100 * retryAttempt),
                    onRetry: (exception, timeSpan, retryAttempt, context) =>
                    {
                        _logger.LogDebug("I/O retry {Attempt} due to {Message}", retryAttempt, exception.Message);
                    });
        }

        /// <summary>
        /// Executes an action with a combined retry and circuit breaker strategy.
        /// </summary>
        public async Task<T> ExecuteWithRecoveryAsync<T>(Func<Task<T>> action, string operationName)
        {
            var retry = GetApiRetryPolicy();
            var breaker = GetCircuitBreakerPolicy();

            return await Policy.WrapAsync(retry, breaker).ExecuteAsync(action);
        }
    }
}
