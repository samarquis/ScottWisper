using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Represents an audit log entry for compliance tracking (HIPAA/GDPR)
    /// </summary>
    public class AuditLogEntry
    {
        /// <summary>
        /// Unique identifier for the log entry
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Timestamp when the event occurred
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Type of audit event
        /// </summary>
        public AuditEventType EventType { get; set; }
        
        /// <summary>
        /// User or session identifier (hashed for privacy)
        /// </summary>
        public string UserId { get; set; } = string.Empty;
        
        /// <summary>
        /// Session identifier for grouping related events
        /// </summary>
        public string SessionId { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of the event
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Additional context data (JSON format)
        /// </summary>
        public string? Metadata { get; set; }
        
        /// <summary>
        /// IP address (hashed/anonymized)
        /// </summary>
        public string? IpAddress { get; set; }
        
        /// <summary>
        /// Whether the operation succeeded
        /// </summary>
        public bool Success { get; set; } = true;
        
        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Compliance classification (HIPAA, GDPR, etc.)
        /// </summary>
        public ComplianceType ComplianceType { get; set; } = ComplianceType.General;
        
        /// <summary>
        /// Data sensitivity level
        /// </summary>
        public DataSensitivity Sensitivity { get; set; } = DataSensitivity.Low;
        
        /// <summary>
        /// Retention expiration date
        /// </summary>
        public DateTime RetentionExpiry { get; set; }
        
        /// <summary>
        /// Whether this log has been archived
        /// </summary>
        public bool IsArchived { get; set; } = false;
        
        /// <summary>
        /// Hash for integrity verification
        /// </summary>
        public string? IntegrityHash { get; set; }
    }
    
    /// <summary>
    /// Types of audit events
    /// </summary>
    public enum AuditEventType
    {
        /// <summary>
        /// Transcription session started
        /// </summary>
        TranscriptionStarted,
        
        /// <summary>
        /// Transcription session completed
        /// </summary>
        TranscriptionCompleted,
        
        /// <summary>
        /// Audio data captured
        /// </summary>
        AudioCaptured,
        
        /// <summary>
        /// Text injected into application
        /// </summary>
        TextInjected,
        
        /// <summary>
        /// User logged in
        /// </summary>
        UserLogin,
        
        /// <summary>
        /// User logged out
        /// </summary>
        UserLogout,
        
        /// <summary>
        /// Authentication succeeded
        /// </summary>
        AuthenticationSucceeded,
        
        /// <summary>
        /// Authentication failed
        /// </summary>
        AuthenticationFailed,
        
        /// <summary>
        /// Authorization failed
        /// </summary>
        AuthorizationFailed,
        
        /// <summary>
        /// User role changed
        /// </summary>
        RoleChanged,
        
        /// <summary>
        /// Permission escalation attempted or granted
        /// </summary>
        PermissionEscalation,
        
        /// <summary>
        /// Password changed
        /// </summary>
        PasswordChanged,
        
        /// <summary>
        /// System-level event
        /// </summary>
        SystemEvent,
        
        /// <summary>
        /// Authentication token generated
        /// </summary>
        TokenGenerated,
        
        /// <summary>
        /// Authentication token expired
        /// </summary>
        TokenExpired,
        
        /// <summary>
        /// User account locked
        /// </summary>
        AccountLocked,
        
        /// <summary>
        /// User account unlocked
        /// </summary>
        AccountUnlocked,
        
        /// <summary>
        /// Security configuration changed
        /// </summary>
        SecurityConfigurationChanged,
        
        /// <summary>
        /// Settings changed
        /// </summary>
        SettingsChanged,
        
        /// <summary>
        /// API key accessed or modified
        /// </summary>
        ApiKeyAccessed,
        
        /// <summary>
        /// Data exported
        /// </summary>
        DataExported,
        
        /// <summary>
        /// Data deleted
        /// </summary>
        DataDeleted,
        
        /// <summary>
        /// Error occurred
        /// </summary>
        Error,
        
        /// <summary>
        /// Security event
        /// </summary>
        SecurityEvent,
        
        /// <summary>
        /// Compliance check performed
        /// </summary>
        ComplianceCheck,
        
        /// <summary>
        /// Retention policy applied
        /// </summary>
        RetentionPolicyApplied,
        
        /// <summary>
        /// Log archived
        /// </summary>
        LogArchived,
        
        /// <summary>
        /// Other event
        /// </summary>
        Other
    }
    
    /// <summary>
    /// Compliance framework types
    /// </summary>
    public enum ComplianceType
    {
        /// <summary>
        /// General compliance
        /// </summary>
        General,
        
        /// <summary>
        /// HIPAA (Healthcare)
        /// </summary>
        HIPAA,
        
        /// <summary>
        /// GDPR (EU Data Protection)
        /// </summary>
        GDPR,
        
        /// <summary>
        /// SOX (Sarbanes-Oxley)
        /// </summary>
        SOX,
        
        /// <summary>
        /// PCI DSS (Payment Card Industry)
        /// </summary>
        PCI_DSS,
        
        /// <summary>
        /// FERPA (Education)
        /// </summary>
        FERPA,
        
        /// <summary>
        /// SOC 2
        /// </summary>
        SOC2
    }
    
    /// <summary>
    /// Data sensitivity levels
    /// </summary>
    public enum DataSensitivity
    {
        /// <summary>
        /// Public data
        /// </summary>
        Public,
        
        /// <summary>
        /// Internal use only
        /// </summary>
        Internal,
        
        /// <summary>
        /// Low sensitivity
        /// </summary>
        Low,
        
        /// <summary>
        /// Medium sensitivity
        /// </summary>
        Medium,
        
        /// <summary>
        /// High sensitivity (PII, PHI)
        /// </summary>
        High,
        
        /// <summary>
        /// Critical (secrets, keys)
        /// </summary>
        Critical
    }
    
    /// <summary>
    /// Retention policy configuration
    /// </summary>
    public class RetentionPolicy
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Policy name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Policy description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Retention period in days
        /// </summary>
        public int RetentionDays { get; set; } = 30;
        
        /// <summary>
        /// Event types this policy applies to
        /// </summary>
        public List<AuditEventType> ApplicableEventTypes { get; set; } = new();
        
        /// <summary>
        /// Compliance types this policy applies to
        /// </summary>
        public List<ComplianceType> ApplicableComplianceTypes { get; set; } = new();
        
        /// <summary>
        /// Whether to archive before deletion
        /// </summary>
        public bool ArchiveBeforeDeletion { get; set; } = true;
        
        /// <summary>
        /// Archive retention period in days (0 = keep indefinitely)
        /// </summary>
        public int ArchiveRetentionDays { get; set; } = 365;
        
        /// <summary>
        /// Whether this policy is active
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// When the policy was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the policy was last modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Summary statistics for audit logs
    /// </summary>
    public class AuditLogStatistics
    {
        /// <summary>
        /// Total number of log entries
        /// </summary>
        public int TotalEntries { get; set; }
        
        /// <summary>
        /// Number of entries by event type
        /// </summary>
        public Dictionary<AuditEventType, int> EntriesByType { get; set; } = new();
        
        /// <summary>
        /// Number of entries by compliance type
        /// </summary>
        public Dictionary<ComplianceType, int> EntriesByCompliance { get; set; } = new();
        
        /// <summary>
        /// Number of entries requiring attention
        /// </summary>
        public int EntriesRequiringAttention { get; set; }
        
        /// <summary>
        /// Number of entries past retention period
        /// </summary>
        public int EntriesPastRetention { get; set; }
        
        /// <summary>
        /// Oldest log entry date
        /// </summary>
        public DateTime? OldestEntry { get; set; }
        
        /// <summary>
        /// Newest log entry date
        /// </summary>
        public DateTime? NewestEntry { get; set; }
        
        /// <summary>
        /// Storage size in bytes
        /// </summary>
        public long StorageSizeBytes { get; set; }
        
        /// <summary>
        /// When statistics were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Security alert rule configuration
    /// </summary>
    public class SecurityAlertRule
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Rule name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Rule description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Event type this rule applies to (null = any)
        /// </summary>
        public AuditEventType? EventType { get; set; }
        
        /// <summary>
        /// Alert severity
        /// </summary>
        public SecurityAlertSeverity Severity { get; set; }
        
        /// <summary>
        /// Alert condition type
        /// </summary>
        public SecurityAlertCondition Condition { get; set; }
        
        /// <summary>
        /// Condition parameters
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();
        
        /// <summary>
        /// Cooldown period in minutes before same rule can trigger again
        /// </summary>
        public int CooldownMinutes { get; set; } = 30;
        
        /// <summary>
        /// Whether this rule is active
        /// </summary>
        public bool IsActive { get; set; } = true;
        
        /// <summary>
        /// When rule was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When rule was last modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When rule was last triggered
        /// </summary>
        public DateTime? LastTriggered { get; set; }
    }
    
    /// <summary>
    /// Security alert
    /// </summary>
    public class SecurityAlert
    {
        /// <summary>
        /// Unique identifier
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Rule ID that triggered this alert
        /// </summary>
        public string RuleId { get; set; } = string.Empty;
        
        /// <summary>
        /// Rule name that triggered this alert
        /// </summary>
        public string RuleName { get; set; } = string.Empty;
        
        /// <summary>
        /// Alert type (event type)
        /// </summary>
        public AuditEventType Type { get; set; }
        
        /// <summary>
        /// Alert severity
        /// </summary>
        public SecurityAlertSeverity Severity { get; set; }
        
        /// <summary>
        /// Alert timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Alert description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Associated audit event ID
        /// </summary>
        public string AuditEventId { get; set; } = string.Empty;
        
        /// <summary>
        /// Additional metadata
        /// </summary>
        public string? Metadata { get; set; }
    }
    
    /// <summary>
    /// Security alert severity levels
    /// </summary>
    public enum SecurityAlertSeverity
    {
        /// <summary>
        /// Low severity - informational
        /// </summary>
        Low,
        
        /// <summary>
        /// Medium severity - requires attention
        /// </summary>
        Medium,
        
        /// <summary>
        /// High severity - urgent attention required
        /// </summary>
        High,
        
        /// <summary>
        /// Critical severity - immediate action required
        /// </summary>
        Critical
    }
    
    /// <summary>
    /// Security alert condition types
    /// </summary>
    public enum SecurityAlertCondition
    {
        /// <summary>
        /// Count of events in time window
        /// </summary>
        CountInTimeWindow,
        
        /// <summary>
        /// Event occurs outside allowed hours
        /// </summary>
        OutOfHours,
        
        /// <summary>
        /// Event has specific data sensitivity
        /// </summary>
        DataSensitivity,
        
        /// <summary>
        /// Event description contains specific text
        /// </summary>
        DescriptionContains,
        
        /// <summary>
        /// Event occurs from unusual location
        /// </summary>
        UnusualLocation,
        
        /// <summary>
        /// Predictive analytics detected an anomaly
        /// </summary>
        PredictiveAnomaly,
        
        /// <summary>
        /// Dynamic threshold exceeded based on historical data
        /// </summary>
        DynamicThreshold,
        
        /// <summary>
        /// Custom condition script
        /// </summary>
        CustomCondition
    }
    
    /// <summary>
    /// Security alert statistics
    /// </summary>
    public class SecurityAlertStatistics
    {
        /// <summary>
        /// Total number of alerts
        /// </summary>
        public int TotalAlerts { get; set; }
        
        /// <summary>
        /// Number of alerts in last 24 hours
        /// </summary>
        public int AlertsLast24Hours { get; set; }
        
        /// <summary>
        /// Alerts grouped by severity
        /// </summary>
        public Dictionary<SecurityAlertSeverity, int> AlertsBySeverity { get; set; } = new();
        
        /// <summary>
        /// Alerts grouped by type
        /// </summary>
        public Dictionary<AuditEventType, int> AlertsByType { get; set; } = new();
        
        /// <summary>
        /// Most recent alert timestamp
        /// </summary>
        public DateTime? MostRecentAlert { get; set; }
        
        /// <summary>
        /// Oldest alert timestamp
        /// </summary>
        public DateTime? OldestAlert { get; set; }
        
        /// <summary>
        /// When statistics were generated
        /// </summary>
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
