using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of graceful degradation and service health monitoring
    /// </summary>
    public class GracefulDegradationService : IGracefulDegradationService
    {
        private readonly ILogger<GracefulDegradationService> _logger;
        private readonly IAuditLoggingService _auditService;
        private readonly ConcurrentDictionary<string, ServiceHealth> _serviceRegistry = new();
        private bool _degradedMode = false;

        public GracefulDegradationService(
            ILogger<GracefulDegradationService> logger,
            IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            
            InitializeRegistry();
        }

        public async Task<T> ExecuteWithFallbackAsync<T>(Func<Task<T>> action, T fallback, string serviceName)
        {
            try
            {
                return await action();
            }
            catch (Exception ex)
            {
                ReportServiceFailure(serviceName, ex);
                _logger.LogWarning("Action for service {Service} failed. Using fallback.", serviceName);
                return fallback;
            }
        }

        public void ReportServiceFailure(string serviceName, Exception ex)
        {
            var health = _serviceRegistry.GetOrAdd(serviceName, _ => new ServiceHealth { ServiceName = serviceName });
            
            lock (health)
            {
                health.FailureCount++;
                health.IsHealthy = false;
                health.LastError = ex.Message;
            }

            if (health.IsCritical)
            {
                _logger.LogCritical("CRITICAL service failure: {Service}", serviceName);
            }
            else if (health.FailureCount > 5)
            {
                _degradedMode = true;
                _logger.LogWarning("System entering DEGRADED mode due to repeated failures in {Service}", serviceName);
            }

            // Fire and forget audit log
            _ = _auditService.LogEventAsync(
                AuditEventType.Error,
                $"Service Failure: {serviceName}",
                ex.ToString(),
                DataSensitivity.Medium);
        }

        public bool IsInDegradedMode() => _degradedMode;

        public Task<Dictionary<string, ServiceHealth>> GetServiceHealthAsync()
        {
            return Task.FromResult(_serviceRegistry.ToDictionary(k => k.Key, v => v.Value));
        }

        private void InitializeRegistry()
        {
            var criticalServices = new[] { "WhisperService", "AudioCaptureService", "TextInjectionService" };
            var nonCriticalServices = new[] { "CostTrackingService", "PerformanceMonitoringService", "SystemTrayService", "WebhookService" };

            foreach (var s in criticalServices)
                _serviceRegistry[s] = new ServiceHealth { ServiceName = s, IsCritical = true, IsHealthy = true };

            foreach (var s in nonCriticalServices)
                _serviceRegistry[s] = new ServiceHealth { ServiceName = s, IsCritical = false, IsHealthy = true };
        }
    }
}
