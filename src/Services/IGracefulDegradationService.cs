using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for managing graceful degradation and service fallbacks
    /// </summary>
    public interface IGracefulDegradationService
    {
        /// <summary>
        /// Executes an action with a safe fallback if it fails
        /// </summary>
        /// <typeparam name="T">The result type</typeparam>
        /// <param name="action">The action to execute</param>
        /// <param name="fallback">The fallback value to return on failure</param>
        /// <param name="serviceName">Name of the service for tracking</param>
        Task<T> ExecuteWithFallbackAsync<T>(Func<Task<T>> action, T fallback, string serviceName);
        
        /// <summary>
        /// Reports a failure in a specific service
        /// </summary>
        void ReportServiceFailure(string serviceName, Exception ex);
        
        /// <summary>
        /// Checks if the application should run in degraded/minimal mode
        /// </summary>
        bool IsInDegradedMode();
        
        /// <summary>
        /// Gets the health status of all registered services
        /// </summary>
        Task<Dictionary<string, ServiceHealth>> GetServiceHealthAsync();
    }

    public class ServiceHealth
    {
        public string ServiceName { get; set; } = string.Empty;
        public bool IsCritical { get; set; }
        public bool IsHealthy { get; set; }
        public int FailureCount { get; set; }
        public string? LastError { get; set; }
    }
}
