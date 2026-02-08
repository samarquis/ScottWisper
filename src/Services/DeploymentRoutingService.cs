using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of Blue-Green deployment and traffic routing service
    /// </summary>
    public class DeploymentRoutingService : IDeploymentRoutingService
    {
        private readonly ILogger<DeploymentRoutingService> _logger;
        private readonly ISettingsService _settingsService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuditLoggingService _auditService;
        private TrafficRoutingConfig _config = new();

        public DeploymentRoutingService(
            ILogger<DeploymentRoutingService> logger,
            ISettingsService settingsService,
            IHttpClientFactory httpClientFactory,
            IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            
            InitializeDefaultConfig();
        }

        public async Task<DeploymentEnvironment> GetActiveEnvironmentAsync()
        {
            return await Task.FromResult(_config.Environments.First(e => e.Name == _config.ActiveEnvironment));
        }

        public async Task<bool> SwitchActiveEnvironmentAsync(string environmentName)
        {
            var env = _config.Environments.FirstOrDefault(e => e.Name == environmentName);
            if (env == null) return false;

            if (!env.IsHealthy)
            {
                _logger.LogWarning("Refusing to switch to unhealthy environment: {Env}", environmentName);
                return false;
            }

            var previousActive = _config.ActiveEnvironment;
            _config.ActiveEnvironment = environmentName;
            _config.StandbyEnvironment = previousActive;

            _logger.LogInformation("Traffic switched: {Old} -> {New}", previousActive, environmentName);

            await _auditService.LogEventAsync(
                AuditEventType.SystemEvent,
                $"Blue-Green Switch: {previousActive} -> {environmentName}",
                null,
                DataSensitivity.Medium);

            // Update application settings to use the new endpoint
            if (_settingsService.Settings?.Transcription != null)
            {
                _settingsService.Settings.Transcription.ApiEndpoint = env.ApiEndpoint;
                await _settingsService.SaveAsync();
            }

            return true;
        }

        public async Task PerformHealthChecksAsync()
        {
            var client = _httpClientFactory.CreateClient();
            foreach (var env in _config.Environments)
            {
                try
                {
                    // Simple HEAD request or GET to health endpoint
                    var response = await client.GetAsync(env.ApiEndpoint + "/health");
                    env.IsHealthy = response.IsSuccessStatusCode;
                }
                catch
                {
                    env.IsHealthy = false;
                }
                env.LastHealthCheck = DateTime.UtcNow;
            }

            // Auto-switch if active is unhealthy and standby is healthy
            if (_config.AutomatedSwitchingEnabled)
            {
                var active = _config.Environments.First(e => e.Name == _config.ActiveEnvironment);
                var standby = _config.Environments.First(e => e.Name == _config.StandbyEnvironment);

                if (!active.IsHealthy && standby.IsHealthy)
                {
                    _logger.LogWarning("Active environment {Active} is unhealthy. Auto-switching to {Standby}.", 
                        active.Name, standby.Name);
                    await SwitchActiveEnvironmentAsync(standby.Name);
                }
            }
        }

        public Task<TrafficRoutingConfig> GetRoutingConfigAsync()
        {
            return Task.FromResult(_config);
        }

        public async Task<bool> InitiateInstantRollbackAsync()
        {
            return await SwitchActiveEnvironmentAsync(_config.StandbyEnvironment);
        }

        private void InitializeDefaultConfig()
        {
            _config = new TrafficRoutingConfig
            {
                Environments = new List<DeploymentEnvironment>
                {
                    new DeploymentEnvironment 
                    { 
                        Name = "Blue", 
                        Version = "1.0.0", 
                        ApiEndpoint = "https://api.whisperkey.com/v1",
                        IsHealthy = true 
                    },
                    new DeploymentEnvironment 
                    { 
                        Name = "Green", 
                        Version = "1.1.0-rc1", 
                        ApiEndpoint = "https://staging.whisperkey.com/v1",
                        IsHealthy = true 
                    }
                }
            };
        }
    }
}
