using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Provides structured logging capabilities with correlation ID management and service boundary tracking.
    /// Enables comprehensive observability for debugging and monitoring service operations.
    /// </summary>
    public interface IStructuredLoggingService
    {
        /// <summary>
        /// Logs the start of a service operation with correlation ID and performance tracking.
        /// Automatically includes operation name, correlation ID, timestamp, and caller information.
        /// </summary>
        /// <param name="operationName">Name of the operation being performed.</param>
        /// <param name="parameters">Optional parameters to include in the log entry.</param>
        /// <param name="correlationId">Optional correlation ID. If not provided, uses current context.</param>
        /// <returns>A disposable that will log operation completion when disposed.</returns>
        IDisposable LogOperationStart(string operationName, IDictionary<string, object>? parameters = null, string? correlationId = null);

        /// <summary>
        /// Logs the successful completion of a service operation with duration and result information.
        /// </summary>
        /// <param name="operationName">Name of the operation that completed.</param>
        /// <param name="duration">Duration of the operation.</param>
        /// <param name="result">Optional result information to include in the log.</param>
        /// <param name="correlationId">Optional correlation ID.</param>
        void LogOperationSuccess(string operationName, TimeSpan duration, object? result = null, string? correlationId = null);

        /// <summary>
        /// Logs an error that occurred during a service operation with exception details.
        /// </summary>
        /// <param name="operationName">Name of the operation where the error occurred.</param>
        /// <param name="exception">The exception that was thrown.</param>
        /// <param name="duration">Duration of the operation before the error occurred.</param>
        /// <param name="correlationId">Optional correlation ID.</param>
        void LogOperationError(string operationName, Exception exception, TimeSpan? duration = null, string? correlationId = null);

        /// <summary>
        /// Logs a service boundary event (entry/exit) for monitoring service interactions.
        /// </summary>
        /// <param name="serviceName">Name of the service being called.</param>
        /// <param name="methodName">Name of the method being called.</param>
        /// <param name="parameters">Optional parameters to include.</param>
        /// <param name="boundaryType">Type of boundary (Entry/Exit).</param>
        /// <param name="correlationId">Optional correlation ID.</param>
        void LogServiceBoundary(string serviceName, string methodName, IDictionary<string, object>? parameters = null, 
            ServiceBoundaryType boundaryType = ServiceBoundaryType.Entry, string? correlationId = null);

        /// <summary>
        /// Logs structured performance metrics for monitoring and optimization.
        /// </summary>
        /// <param name="serviceName">Name of the service.</param>
        /// <param name="operationName">Name of the operation.</param>
        /// <param name="metrics">Performance metrics to log.</param>
        /// <param name="correlationId">Optional correlation ID.</param>
        void LogPerformanceMetrics(string serviceName, string operationName, PerformanceMetrics metrics, string? correlationId = null);

        /// <summary>
        /// Logs security-related events for audit trail and compliance.
        /// </summary>
        /// <param name="eventType">Type of security event.</param>
        /// <param name="description">Description of the security event.</param>
        /// <param name="userId">Optional user identifier.</param>
        /// <param name="resource">Optional resource being accessed.</param>
        /// <param name="success">Whether the operation was successful.</param>
        /// <param name="correlationId">Optional correlation ID.</param>
        void LogSecurityEvent(SecurityEventType eventType, string description, string? userId = null, 
            string? resource = null, bool success = true, string? correlationId = null);

        /// <summary>
        /// Logs business-level events for domain-specific tracking and analytics.
        /// </summary>
        /// <param name="eventType">Type of business event.</param>
        /// <param name="description">Description of the business event.</param>
        /// <param name="data">Optional event data.</param>
        /// <param name="correlationId">Optional correlation ID.</param>
        void LogBusinessEvent(string eventType, string description, IDictionary<string, object>? data = null, string? correlationId = null);

        /// <summary>
        /// Executes an operation with comprehensive logging including timing and exception handling.
        /// </summary>
        /// <typeparam name="T">Return type of the operation.</typeparam>
        /// <param name="operationName">Name of the operation.</param>
        /// <param name="operation">The operation to execute.</param>
        /// <param name="parameters">Optional parameters to log.</param>
        /// <param name="correlationId">Optional correlation ID.</param>
        /// <returns>The result of the operation.</returns>
        Task<T> ExecuteWithLoggingAsync<T>(string operationName, Func<Task<T>> operation, 
            IDictionary<string, object>? parameters = null, string? correlationId = null);
    }

    /// <summary>
    /// Types of service boundary events for logging
    /// </summary>
    public enum ServiceBoundaryType
    {
        /// <summary>Service method entry</summary>
        Entry,
        /// <summary>Service method exit</summary>
        Exit
    }

    /// <summary>
    /// Types of security events for audit logging
    /// </summary>
    public enum SecurityEventType
    {
        /// <summary>User authentication event</summary>
        Authentication,
        /// <summary>Authorization check</summary>
        Authorization,
        /// <summary>Data access</summary>
        DataAccess,
        /// <summary>Configuration change</summary>
        ConfigurationChange,
        /// <summary>Permission change</summary>
        PermissionChange,
        /// <summary>Security exception</summary>
        SecurityException
    }

    /// <summary>
    /// Performance metrics for operation monitoring
    /// </summary>
    public class PerformanceMetrics
    {
        /// <summary>Duration of the operation</summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>Memory usage before operation (bytes)</summary>
        public long MemoryBefore { get; set; }
        
        /// <summary>Memory usage after operation (bytes)</summary>
        public long MemoryAfter { get; set; }
        
        /// <summary>CPU time used (milliseconds)</summary>
        public long CpuTime { get; set; }
        
        /// <summary>Number of database operations performed</summary>
        public int DatabaseOperations { get; set; }
        
        /// <summary>Number of external API calls made</summary>
        public int ExternalApiCalls { get; set; }
        
        /// <summary>Additional custom metrics</summary>
        public IDictionary<string, object>? CustomMetrics { get; set; }
    }

    /// <summary>
    /// Operation timer for tracking service operation performance
    /// </summary>
    public class OperationTimer : IDisposable
    {
        private readonly IStructuredLoggingService _loggingService;
        private readonly ICorrelationService _correlationService;
        private readonly string _operationName;
        private readonly string? _correlationId;
        private readonly Stopwatch _stopwatch;
        private readonly long _memoryBefore;
        private bool _disposed = false;

        public OperationTimer(IStructuredLoggingService loggingService, ICorrelationService correlationService,
            string operationName, string? correlationId = null)
        {
            _loggingService = loggingService ?? throw new ArgumentNullException(nameof(loggingService));
            _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
            _operationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            _correlationId = correlationId;
            
            _stopwatch = Stopwatch.StartNew();
            _memoryBefore = GC.GetTotalMemory(false);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(false);
                
                var metrics = new PerformanceMetrics
                {
                    Duration = _stopwatch.Elapsed,
                    MemoryBefore = _memoryBefore,
                    MemoryAfter = memoryAfter,
                    CpuTime = Environment.TickCount64 // Simplified CPU timing
                };
                
                _loggingService.LogPerformanceMetrics("Operation", _operationName, metrics, _correlationId);
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Implementation of structured logging service with correlation ID integration
    /// </summary>
    public class StructuredLoggingService : IStructuredLoggingService
    {
        private readonly ILogger<StructuredLoggingService> _logger;
        private readonly ICorrelationService _correlationService;

        public StructuredLoggingService(ILogger<StructuredLoggingService> logger, ICorrelationService correlationService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
        }

        /// <summary>
        /// Logs the start of a service operation with correlation ID and performance tracking.
        /// </summary>
        public IDisposable LogOperationStart(string operationName, IDictionary<string, object>? parameters = null, string? correlationId = null)
        {
            var currentCorrelationId = correlationId ?? _correlationService.GetCurrentCorrelationId();
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["OperationName"] = operationName,
                ["CorrelationId"] = currentCorrelationId ?? "unknown",
                ["StartTime"] = DateTime.UtcNow,
                ["Parameters"] = parameters ?? new Dictionary<string, object>()
            });

            _logger.LogInformation("Starting operation: {OperationName}", operationName);
            
            return new OperationTimer(this, _correlationService, operationName, currentCorrelationId);
        }

        /// <summary>
        /// Logs the successful completion of a service operation.
        /// </summary>
        public void LogOperationSuccess(string operationName, TimeSpan duration, object? result = null, string? correlationId = null)
        {
            var currentCorrelationId = correlationId ?? _correlationService.GetCurrentCorrelationId();
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["OperationName"] = operationName,
                ["CorrelationId"] = currentCorrelationId ?? "unknown",
                ["Duration"] = duration,
                ["Result"] = result?.ToString() ?? "void"
            });

            _logger.LogInformation("Operation completed successfully: {OperationName} in {Duration}ms", 
                operationName, duration.TotalMilliseconds);
        }

        /// <summary>
        /// Logs an error that occurred during a service operation.
        /// </summary>
        public void LogOperationError(string operationName, Exception exception, TimeSpan? duration = null, string? correlationId = null)
        {
            var currentCorrelationId = correlationId ?? _correlationService.GetCurrentCorrelationId();
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["OperationName"] = operationName,
                ["CorrelationId"] = currentCorrelationId ?? "unknown",
                ["Duration"] = duration?.TotalMilliseconds,
                ["ExceptionType"] = exception.GetType().Name,
                ["ExceptionMessage"] = exception.Message
            });

            _logger.LogError(exception, "Operation failed: {OperationName}", operationName);
        }

        /// <summary>
        /// Logs a service boundary event.
        /// </summary>
        public void LogServiceBoundary(string serviceName, string methodName, IDictionary<string, object>? parameters = null, 
            ServiceBoundaryType boundaryType = ServiceBoundaryType.Entry, string? correlationId = null)
        {
            var currentCorrelationId = correlationId ?? _correlationService.GetCurrentCorrelationId();
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["ServiceName"] = serviceName,
                ["MethodName"] = methodName,
                ["BoundaryType"] = boundaryType.ToString(),
                ["CorrelationId"] = currentCorrelationId ?? "unknown",
                ["Parameters"] = parameters ?? new Dictionary<string, object>()
            });

            var logMessage = boundaryType == ServiceBoundaryType.Entry 
                ? "Entering service boundary: {ServiceName}.{MethodName}"
                : "Exiting service boundary: {ServiceName}.{MethodName}";
                
            _logger.LogDebug(logMessage, serviceName, methodName);
        }

        /// <summary>
        /// Logs structured performance metrics.
        /// </summary>
        public void LogPerformanceMetrics(string serviceName, string operationName, PerformanceMetrics metrics, string? correlationId = null)
        {
            var currentCorrelationId = correlationId ?? _correlationService.GetCurrentCorrelationId();
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["ServiceName"] = serviceName,
                ["OperationName"] = operationName,
                ["CorrelationId"] = currentCorrelationId ?? "unknown",
                ["DurationMs"] = metrics.Duration.TotalMilliseconds,
                ["MemoryBefore"] = metrics.MemoryBefore,
                ["MemoryAfter"] = metrics.MemoryAfter,
                ["MemoryDelta"] = metrics.MemoryAfter - metrics.MemoryBefore,
                ["CpuTime"] = metrics.CpuTime,
                ["DatabaseOperations"] = metrics.DatabaseOperations,
                ["ExternalApiCalls"] = metrics.ExternalApiCalls
            });

            if (metrics.CustomMetrics != null)
            {
                foreach (var metric in metrics.CustomMetrics)
                {
                    scope.Add(metric.Key, metric.Value);
                }
            }

            _logger.LogInformation("Performance metrics for {ServiceName}.{OperationName}: {Duration}ms, Memory: {MemoryDelta} bytes", 
                serviceName, operationName, metrics.Duration.TotalMilliseconds, metrics.MemoryAfter - metrics.MemoryBefore);
        }

        /// <summary>
        /// Logs security-related events for audit trail.
        /// </summary>
        public void LogSecurityEvent(SecurityEventType eventType, string description, string? userId = null, 
            string? resource = null, bool success = true, string? correlationId = null)
        {
            var currentCorrelationId = correlationId ?? _correlationService.GetCurrentCorrelationId();
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventType"] = eventType.ToString(),
                ["Description"] = description,
                ["UserId"] = userId ?? "anonymous",
                ["Resource"] = resource ?? "unknown",
                ["Success"] = success,
                ["CorrelationId"] = currentCorrelationId ?? "unknown",
                ["Timestamp"] = DateTime.UtcNow
            });

            var logLevel = success ? LogLevel.Information : LogLevel.Warning;
            _logger.Log(logLevel, "Security event: {EventType} - {Description}", eventType, description);
        }

        /// <summary>
        /// Logs business-level events.
        /// </summary>
        public void LogBusinessEvent(string eventType, string description, IDictionary<string, object>? data = null, string? correlationId = null)
        {
            var currentCorrelationId = correlationId ?? _correlationService.GetCurrentCorrelationId();
            
            using var scope = _logger.BeginScope(new Dictionary<string, object>
            {
                ["EventType"] = eventType,
                ["Description"] = description,
                ["CorrelationId"] = currentCorrelationId ?? "unknown",
                ["Data"] = data ?? new Dictionary<string, object>()
            });

            _logger.LogInformation("Business event: {EventType} - {Description}", eventType, description);
        }

        /// <summary>
        /// Executes an operation with comprehensive logging.
        /// </summary>
        public async Task<T> ExecuteWithLoggingAsync<T>(string operationName, Func<Task<T>> operation, 
            IDictionary<string, object>? parameters = null, string? correlationId = null)
        {
            using var operationTimer = LogOperationStart(operationName, parameters, correlationId);
            var currentCorrelationId = correlationId ?? _correlationService.GetCurrentCorrelationId();
            
            try
            {
                var result = await operation().ConfigureAwait(false);
                LogOperationSuccess(operationName, operationTimer._stopwatch.Elapsed, result, currentCorrelationId);
                return result;
            }
            catch (Exception ex)
            {
                LogOperationError(operationName, ex, operationTimer._stopwatch.Elapsed, currentCorrelationId);
                throw;
            }
        }
    }
}