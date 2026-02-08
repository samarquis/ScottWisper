using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Represents a snapshot of the application configuration
    /// </summary>
    public class ConfigurationSnapshot
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string ConfigHash { get; set; } = string.Empty;
        public Dictionary<string, string> Settings { get; set; } = new();
        public string Environment { get; set; } = "Production";
    }

    /// <summary>
    /// Report of detected configuration drift
    /// </summary>
    public class DriftReport
    {
        public bool HasDrift { get; set; }
        public DateTime DetectedAt { get; set; }
        public List<DriftItem> Differences { get; set; } = new();
    }

    public class DriftItem
    {
        public string SettingKey { get; set; } = string.Empty;
        public string ExpectedValue { get; set; } = string.Empty;
        public string ActualValue { get; set; } = string.Empty;
        public DriftSeverity Severity { get; set; }
    }

    public enum DriftSeverity
    {
        Information,
        Warning,
        Critical
    }
}
