using System;
using System.Threading;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Provides correlation ID management for tracking operations across service boundaries.
    /// Enables structured logging with request tracing and debugging capabilities.
    /// </summary>
    public interface ICorrelationService
    {
        /// <summary>
        /// Gets the current correlation ID from the execution context.
        /// Returns null if no correlation ID has been set for the current context.
        /// </summary>
        /// <returns>The correlation ID for the current operation, or null if not set.</returns>
        string? GetCurrentCorrelationId();

        /// <summary>
        /// Sets a correlation ID for the current execution context.
        /// If a correlation ID already exists, it will be overwritten.
        /// </summary>
        /// <param name="correlationId">The correlation ID to set. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown when correlationId is null or empty.</exception>
        void SetCorrelationId(string correlationId);

        /// <summary>
        /// Generates a new correlation ID and sets it for the current execution context.
        /// Uses a combination of timestamp and random values to ensure uniqueness.
        /// </summary>
        /// <returns>The newly generated correlation ID.</returns>
        string GenerateAndSetCorrelationId();

        /// <summary>
        /// Executes an action with a correlation ID context.
        /// Automatically generates a correlation ID if one is not provided.
        /// The correlation ID is automatically cleared after execution.
        /// </summary>
        /// <param name="action">The action to execute with correlation context.</param>
        /// <param name="correlationId">Optional correlation ID. If null, a new one will be generated.</param>
        /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
        void ExecuteWithCorrelation(Action action, string? correlationId = null);

        /// <summary>
        /// Executes a function with a correlation ID context and returns the result.
        /// Automatically generates a correlation ID if one is not provided.
        /// The correlation ID is automatically cleared after execution.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The function to execute with correlation context.</param>
        /// <param name="correlationId">Optional correlation ID. If null, a new one will be generated.</param>
        /// <returns>The result of the function execution.</returns>
        /// <exception cref="ArgumentNullException">Thrown when function is null.</exception>
        T ExecuteWithCorrelation<T>(Func<T> function, string? correlationId = null);

        /// <summary>
        /// Executes an asynchronous action with a correlation ID context.
        /// Automatically generates a correlation ID if one is not provided.
        /// The correlation ID is automatically cleared after execution.
        /// </summary>
        /// <param name="action">The async action to execute with correlation context.</param>
        /// <param name="correlationId">Optional correlation ID. If null, a new one will be generated.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
        Task ExecuteWithCorrelationAsync(Func<Task> action, string? correlationId = null);

        /// <summary>
        /// Executes an asynchronous function with a correlation ID context and returns the result.
        /// Automatically generates a correlation ID if one is not provided.
        /// The correlation ID is automatically cleared after execution.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The async function to execute with correlation context.</param>
        /// <param name="correlationId">Optional correlation ID. If null, a new one will be generated.</param>
        /// <returns>A task representing the asynchronous operation with the result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when function is null.</exception>
        Task<T> ExecuteWithCorrelationAsync<T>(Func<Task<T>> function, string? correlationId = null);

        /// <summary>
        /// Clears the correlation ID from the current execution context.
        /// Typically called after operation completion to prevent ID leakage.
        /// </summary>
        void ClearCorrelationId();
    }

    /// <summary>
    /// Implementation of correlation ID management service using AsyncLocal for thread-safe context management.
    /// Ensures correlation IDs flow properly across async/await boundaries.
    /// </summary>
    public class CorrelationService : ICorrelationService
    {
        private readonly AsyncLocal<string?> _correlationId = new();

        /// <summary>
        /// Gets the current correlation ID from the execution context.
        /// </summary>
        /// <returns>The correlation ID for the current operation, or null if not set.</returns>
        public string? GetCurrentCorrelationId()
        {
            return _correlationId.Value;
        }

        /// <summary>
        /// Sets a correlation ID for the current execution context.
        /// </summary>
        /// <param name="correlationId">The correlation ID to set. Must not be null or empty.</param>
        /// <exception cref="ArgumentException">Thrown when correlationId is null or empty.</exception>
        public void SetCorrelationId(string correlationId)
        {
            if (string.IsNullOrWhiteSpace(correlationId))
            {
                throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
            }

            _correlationId.Value = correlationId;
        }

        /// <summary>
        /// Generates a new correlation ID and sets it for the current execution context.
        /// </summary>
        /// <returns>The newly generated correlation ID.</returns>
        public string GenerateAndSetCorrelationId()
        {
            var correlationId = GenerateCorrelationId();
            SetCorrelationId(correlationId);
            return correlationId;
        }

        /// <summary>
        /// Executes an action with a correlation ID context.
        /// </summary>
        /// <param name="action">The action to execute with correlation context.</param>
        /// <param name="correlationId">Optional correlation ID. If null, a new one will be generated.</param>
        /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
        public void ExecuteWithCorrelation(Action action, string? correlationId = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var originalCorrelationId = GetCurrentCorrelationId();
            try
            {
                SetCorrelationId(correlationId ?? GenerateCorrelationId());
                action();
            }
            finally
            {
                SetCorrelationId(originalCorrelationId!);
            }
        }

        /// <summary>
        /// Executes a function with a correlation ID context and returns the result.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The function to execute with correlation context.</param>
        /// <param name="correlationId">Optional correlation ID. If null, a new one will be generated.</param>
        /// <returns>The result of the function execution.</returns>
        /// <exception cref="ArgumentNullException">Thrown when function is null.</exception>
        public T ExecuteWithCorrelation<T>(Func<T> function, string? correlationId = null)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            var originalCorrelationId = GetCurrentCorrelationId();
            try
            {
                SetCorrelationId(correlationId ?? GenerateCorrelationId());
                return function();
            }
            finally
            {
                SetCorrelationId(originalCorrelationId!);
            }
        }

        /// <summary>
        /// Executes an asynchronous action with a correlation ID context.
        /// </summary>
        /// <param name="action">The async action to execute with correlation context.</param>
        /// <param name="correlationId">Optional correlation ID. If null, a new one will be generated.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when action is null.</exception>
        public async Task ExecuteWithCorrelationAsync(Func<Task> action, string? correlationId = null)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var originalCorrelationId = GetCurrentCorrelationId();
            try
            {
                SetCorrelationId(correlationId ?? GenerateCorrelationId());
                await action().ConfigureAwait(false);
            }
            finally
            {
                SetCorrelationId(originalCorrelationId!);
            }
        }

        /// <summary>
        /// Executes an asynchronous function with a correlation ID context and returns the result.
        /// </summary>
        /// <typeparam name="T">The return type of the function.</typeparam>
        /// <param name="function">The async function to execute with correlation context.</param>
        /// <param name="correlationId">Optional correlation ID. If null, a new one will be generated.</param>
        /// <returns>A task representing the asynchronous operation with the result.</returns>
        /// <exception cref="ArgumentNullException">Thrown when function is null.</exception>
        public async Task<T> ExecuteWithCorrelationAsync<T>(Func<Task<T>> function, string? correlationId = null)
        {
            if (function == null)
                throw new ArgumentNullException(nameof(function));

            var originalCorrelationId = GetCurrentCorrelationId();
            try
            {
                SetCorrelationId(correlationId ?? GenerateCorrelationId());
                return await function().ConfigureAwait(false);
            }
            finally
            {
                SetCorrelationId(originalCorrelationId!);
            }
        }

        /// <summary>
        /// Clears the correlation ID from the current execution context.
        /// </summary>
        public void ClearCorrelationId()
        {
            _correlationId.Value = null;
        }

        /// <summary>
        /// Generates a unique correlation ID using timestamp and random components.
        /// Format: {timestamp}-{randomGuid}
        /// </summary>
        /// <returns>A unique correlation ID.</returns>
        private static string GenerateCorrelationId()
        {
            var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmssfff");
            var randomGuid = Guid.NewGuid().ToString("N")[..8];
            return $"{timestamp}-{randomGuid}";
        }
    }
}