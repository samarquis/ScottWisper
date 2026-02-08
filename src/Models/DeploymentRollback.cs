using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Represents a point in time that the application can roll back to
    /// </summary>
    public class RollbackTarget
    {
        public string Version { get; set; } = string.Empty;
        public DateTime DeployedAt { get; set; }
        public string ConfigurationHash { get; set; } = string.Empty;
        public string BackupPath { get; set; } = string.Empty;
        public bool IsStable { get; set; }
    }

    /// <summary>
    /// History of deployments and their status
    /// </summary>
    public class DeploymentHistory
    {
        public List<RollbackTarget> Targets { get; set; } = new();
        public string CurrentVersion { get; set; } = "1.0.0";
        public string LastKnownStableVersion { get; set; } = "1.0.0";
        public int ConsecutiveStartupFailures { get; set; }
    }

    /// <summary>
    /// Configuration for the rollback system
    /// </summary>
    public class RollbackConfig
    {
        public bool AutoRollbackEnabled { get; set; } = true;
        public int MaxStartupFailuresBeforeRollback { get; set; } = 3;
        public TimeSpan StartupStabilityWindow { get; set; } = TimeSpan.FromMinutes(5);
        public string BackupDirectory { get; set; } = "backups";
    }
}
