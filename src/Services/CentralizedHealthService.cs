using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Infrastructure.SmokeTesting.HealthChecks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of centralized health monitoring service.
    /// Aggregates signals from system resources, databases, and external APIs.
    /// </summary>
    public class CentralizedHealthService : ICentralizedHealthService
    {
        private readonly ILogger<CentralizedHealthService> _logger;
        private readonly SystemHealthChecker _systemChecker;
        private readonly DatabaseHealthChecker _dbChecker;
        private readonly ExternalServiceHealthChecker _externalChecker;
        private readonly IPerformanceMonitoringService _performanceMonitoring;

        /// <summary>
        /// Event triggered whenever a full health check completes.
        /// </summary>
        public event EventHandler<ApplicationHealthReport>? HealthStatusChanged;

        /// <summary>
        /// Initializes a new instance of the <see cref="CentralizedHealthService"/> class.
        /// </summary>
        public CentralizedHealthService(
            ILogger<CentralizedHealthService> logger,
            SystemHealthChecker systemChecker,
            DatabaseHealthChecker dbChecker,
            ExternalServiceHealthChecker externalChecker,
            IPerformanceMonitoringService performanceMonitoring)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _systemChecker = systemChecker ?? throw new ArgumentNullException(nameof(systemChecker));
            _dbChecker = dbChecker ?? throw new ArgumentNullException(nameof(dbChecker));
            _externalChecker = externalChecker ?? throw new ArgumentNullException(nameof(externalChecker));
            _performanceMonitoring = performanceMonitoring ?? throw new ArgumentNullException(nameof(performanceMonitoring));
        }

        /// <summary>
        /// Executes all registered health checkers in parallel and produces a unified report.
        /// </summary>
        /// <returns>A detailed <see cref="ApplicationHealthReport"/>.</returns>
        public async Task<ApplicationHealthReport> RunFullHealthCheckAsync()
        {
            _logger.LogInformation("Starting full application health check...");
            
            var report = new ApplicationHealthReport { Timestamp = DateTime.UtcNow };

            // 1. System Health
            var systemTask = _systemChecker.RunAllTestsAsync();
            
            // 2. Database Health
            var dbTask = _dbChecker.RunAllTestsAsync();
            
            // 3. External Services Health (OpenAI, Azure, etc.)
            var externalTask = _externalChecker.RunAllTestsAsync();

            await Task.WhenAll(systemTask, dbTask, externalTask);

            // Map results to ComponentHealth (Internal mapping based on checker results)
            report.Components.Add(new ComponentHealth 
            { 
                Name = "System", 
                IsHealthy = systemTask.Result.AllPassed,
                Status = systemTask.Result.AllPassed ? "Healthy" : "Degraded"
            });

            report.Components.Add(new ComponentHealth 
            { 
                Name = "Database", 
                IsHealthy = dbTask.Result.AllPassed,
                Status = dbTask.Result.AllPassed ? "Healthy" : "Degraded"
            });

            report.Components.Add(new ComponentHealth 
            { 
                Name = "ExternalServices", 
                IsHealthy = externalTask.Result.AllPassed,
                Status = externalTask.Result.AllPassed ? "Healthy" : "Degraded"
            });

            report.IsHealthy = report.Components.All(c => c.IsHealthy);
            
            // Get performance score from baselines
            var baselines = await _performanceMonitoring.GetBaselinesAsync();
            report.PerformanceScore = baselines.Any() ? 1.0 : 0.0; // Placeholder score logic

            _logger.LogInformation("Full health check completed. Overall Health: {IsHealthy}", report.IsHealthy);
            
            HealthStatusChanged?.Invoke(this, report);
            return report;
        }

        /// <summary>
        /// Retrieves the health status of a specific named component.
        /// </summary>
        /// <param name="componentName">The name of the component to check.</param>
        /// <returns>The <see cref="ComponentHealth"/> status.</returns>
        public async Task<ComponentHealth> GetComponentHealthAsync(string componentName)
        {
            var fullReport = await RunFullHealthCheckAsync();
            return fullReport.Components.FirstOrDefault(c => c.Name == componentName) 
                   ?? new ComponentHealth { Name = componentName, IsHealthy = false, Status = "Not Found" };
        }
    }
}
