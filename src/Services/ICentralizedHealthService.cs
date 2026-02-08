using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for centralized health monitoring across all application domains
    /// </summary>
    public interface ICentralizedHealthService
    {
        /// <summary>
        /// Executes all registered health checks and returns a comprehensive report
        /// </summary>
        Task<ApplicationHealthReport> RunFullHealthCheckAsync();
        
        /// <summary>
        /// Gets the current health status of a specific component
        /// </summary>
        Task<ComponentHealth> GetComponentHealthAsync(string componentName);
        
        /// <summary>
        /// Notifies when any critical health status changes
        /// </summary>
        event EventHandler<ApplicationHealthReport>? HealthStatusChanged;
    }

    public class ApplicationHealthReport
    {
        public bool IsHealthy { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<ComponentHealth> Components { get; set; } = new();
        public double PerformanceScore { get; set; }
    }

    public class ComponentHealth
    {
        public string Name { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = "Unknown";
        public string? Details { get; set; }
        public TimeSpan Latency { get; set; }
    }
}
