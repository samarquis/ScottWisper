using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Represents a deployment environment (e.g., Blue, Green, Staging, Production)
    /// </summary>
    public class DeploymentEnvironment
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string ApiEndpoint { get; set; } = string.Empty;
        public bool IsHealthy { get; set; }
        public DateTime LastHealthCheck { get; set; }
        public Dictionary<string, string> Tags { get; set; } = new();
    }

    /// <summary>
    /// Configuration for traffic routing between environments
    /// </summary>
    public class TrafficRoutingConfig
    {
        public string ActiveEnvironment { get; set; } = "Blue";
        public string StandbyEnvironment { get; set; } = "Green";
        public double TrafficWeight { get; set; } = 100.0; // % to Active
        public bool AutomatedSwitchingEnabled { get; set; } = true;
        public List<DeploymentEnvironment> Environments { get; set; } = new();
    }
}
