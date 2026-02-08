using System;
using System.Net.Http;
using System.Threading.Tasks;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace WhisperKey.Services.Recovery
{
    /// <summary>
    /// Interface for managing error recovery policies.
    /// </summary>
    public interface IRecoveryPolicyService
    {
        /// <summary>
        /// Gets a retry policy for transient network and API failures.
        /// </summary>
        AsyncRetryPolicy GetApiRetryPolicy(int retryCount = 3);

        /// <summary>
        /// Gets a generic retry policy for transient network and API failures.
        /// </summary>
        AsyncRetryPolicy<HttpResponseMessage> GetApiRetryPolicy<HttpResponseMessage>(int retryCount = 3);

        /// <summary>
        /// Gets a circuit breaker policy for high-frequency operations.
        /// </summary>
        AsyncCircuitBreakerPolicy GetCircuitBreakerPolicy(int exceptionsAllowedBeforeBreaking = 5, int durationOfBreakSeconds = 30);

        /// <summary>
        /// Gets a retry policy for transient I/O failures.
        /// </summary>
        AsyncRetryPolicy GetIoRetryPolicy(int retryCount = 3);

        /// <summary>
        /// Executes an action with a combined retry and circuit breaker strategy.
        /// </summary>
        Task<T> ExecuteWithRecoveryAsync<T>(Func<Task<T>> action, string operationName);
    }
}
