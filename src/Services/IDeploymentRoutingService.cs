using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for blue-green deployment traffic routing and environment management
    /// </summary>
    public interface IDeploymentRoutingService
    {
        /// <summary>
        /// Gets the currently active environment
        /// </summary>
        Task<DeploymentEnvironment> GetActiveEnvironmentAsync();
        
        /// <summary>
        /// Switches traffic to a different environment
        /// </summary>
        /// <param name="environmentName">Name of the environment to make active</param>
        Task<bool> SwitchActiveEnvironmentAsync(string environmentName);
        
        /// <summary>
        /// Performs health checks on all environments
        /// </summary>
        Task PerformHealthChecksAsync();
        
        /// <summary>
        /// Gets the routing configuration
        /// </summary>
        Task<TrafficRoutingConfig> GetRoutingConfigAsync();
        
        /// <summary>
        /// Initiates an instant rollback to the previous environment
        /// </summary>
        Task<bool> InitiateInstantRollbackAsync();
    }
}
