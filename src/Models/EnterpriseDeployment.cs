using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Enterprise deployment configuration for MSI/GPO deployment
    /// </summary>
    public class EnterpriseDeploymentConfig
    {
        /// <summary>
        /// Unique identifier for this deployment configuration
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Organization name
        /// </summary>
        public string OrganizationName { get; set; } = string.Empty;
        
        /// <summary>
        /// Deployment type (MSI, GPO, SCCM, etc.)
        /// </summary>
        public DeploymentType Type { get; set; } = DeploymentType.MSI;
        
        /// <summary>
        /// Installation scope (per-user or per-machine)
        /// </summary>
        public InstallationScope Scope { get; set; } = InstallationScope.PerMachine;
        
        /// <summary>
        /// Installation directory (null for default)
        /// </summary>
        public string? InstallDirectory { get; set; }
        
        /// <summary>
        /// Whether to install for all users
        /// </summary>
        public bool AllUsers { get; set; } = true;
        
        /// <summary>
        /// Whether to create desktop shortcuts
        /// </summary>
        public bool CreateDesktopShortcut { get; set; } = false;
        
        /// <summary>
        /// Whether to create Start Menu shortcuts
        /// </summary>
        public bool CreateStartMenuShortcut { get; set; } = true;
        
        /// <summary>
        /// Whether to register file associations
        /// </summary>
        public bool RegisterFileAssociations { get; set; } = false;
        
        /// <summary>
        /// Whether to add to PATH environment variable
        /// </summary>
        public bool AddToPath { get; set; } = false;
        
        /// <summary>
        /// Silent installation parameters
        /// </summary>
        public SilentInstallOptions SilentOptions { get; set; } = new();
        
        /// <summary>
        /// Pre-configured settings for deployment
        /// </summary>
        public PreconfiguredSettings Settings { get; set; } = new();
        
        /// <summary>
        /// Webhook configuration for integrations
        /// </summary>
        public WebhookConfig Webhook { get; set; } = new();
        
        /// <summary>
        /// License key for enterprise deployment
        /// </summary>
        public string? LicenseKey { get; set; }
        
        /// <summary>
        /// Whether this is a trial deployment
        /// </summary>
        public bool IsTrial { get; set; } = false;
        
        /// <summary>
        /// Trial expiration date
        /// </summary>
        public DateTime? TrialExpiration { get; set; }
        
        /// <summary>
        /// When the configuration was created
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// When the configuration was last modified
        /// </summary>
        public DateTime ModifiedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Configuration version
        /// </summary>
        public string Version { get; set; } = "1.0";
    }
    
    /// <summary>
    /// Deployment types
    /// </summary>
    public enum DeploymentType
    {
        /// <summary>
        /// MSI installer
        /// </summary>
        MSI,
        
        /// <summary>
        /// Group Policy Object
        /// </summary>
        GPO,
        
        /// <summary>
        /// Microsoft Endpoint Configuration Manager (SCCM)
        /// </summary>
        SCCM,
        
        /// <summary>
        /// Microsoft Intune
        /// </summary>
        Intune,
        
        /// <summary>
        /// Custom deployment
        /// </summary>
        Custom
    }
    
    /// <summary>
    /// Installation scope
    /// </summary>
    public enum InstallationScope
    {
        /// <summary>
        /// Per-user installation
        /// </summary>
        PerUser,
        
        /// <summary>
        /// Per-machine (all users) installation
        /// </summary>
        PerMachine
    }
    
    /// <summary>
    /// Silent installation options
    /// </summary>
    public class SilentInstallOptions
    {
        /// <summary>
        /// Enable silent installation
        /// </summary>
        public bool Enabled { get; set; } = true;
        
        /// <summary>
        /// Suppress all UI
        /// </summary>
        public bool SuppressUI { get; set; } = true;
        
        /// <summary>
        /// Suppress reboot
        /// </summary>
        public bool SuppressReboot { get; set; } = true;
        
        /// <summary>
        /// Log file path
        /// </summary>
        public string? LogFile { get; set; }
        
        /// <summary>
        /// MSI properties for silent install
        /// </summary>
        public Dictionary<string, string> MsiProperties { get; set; } = new()
        {
            ["INSTALLDIR"] = "[ProgramFiles64Folder]WhisperKey",
            ["ALLUSERS"] = "1",
            ["MSIINSTALLPERUSER"] = "0"
        };
    }
    
    /// <summary>
    /// Pre-configured settings for enterprise deployment
    /// </summary>
    public class PreconfiguredSettings
    {
        /// <summary>
        /// Pre-configured API key
        /// </summary>
        public string? ApiKey { get; set; }
        
        /// <summary>
        /// Pre-configured API endpoint
        /// </summary>
        public string? ApiEndpoint { get; set; }
        
        /// <summary>
        /// Default language
        /// </summary>
        public string DefaultLanguage { get; set; } = "en-US";
        
        /// <summary>
        /// Enable auto-punctuation by default
        /// </summary>
        public bool EnableAutoPunctuation { get; set; } = true;
        
        /// <summary>
        /// Enable voice commands by default
        /// </summary>
        public bool EnableVoiceCommands { get; set; } = true;
        
        /// <summary>
        /// Enable audit logging by default
        /// </summary>
        public bool EnableAuditLogging { get; set; } = true;
        
        /// <summary>
        /// Compliance framework to use
        /// </summary>
        public string ComplianceFramework { get; set; } = "General";
        
        /// <summary>
        /// Retention policy days
        /// </summary>
        public int RetentionDays { get; set; } = 30;
        
        /// <summary>
        /// Enabled vocabulary packs
        /// </summary>
        public List<string> EnabledVocabularyPacks { get; set; } = new();
        
        /// <summary>
        /// Custom terms to pre-populate
        /// </summary>
        public List<string> CustomTerms { get; set; } = new();
        
        /// <summary>
        /// Whether to lock settings (prevent user changes)
        /// </summary>
        public bool LockSettings { get; set; } = false;
    }
    
    /// <summary>
    /// Webhook configuration for external integrations
    /// </summary>
    public class WebhookConfig
    {
        /// <summary>
        /// Enable webhook functionality
        /// </summary>
        public bool Enabled { get; set; } = false;
        
        /// <summary>
        /// Webhook endpoint URL
        /// </summary>
        public string? EndpointUrl { get; set; }
        
        /// <summary>
        /// Authentication token
        /// </summary>
        public string? AuthToken { get; set; }
        
        /// <summary>
        /// Webhook secret for HMAC verification
        /// </summary>
        public string? Secret { get; set; }
        
        /// <summary>
        /// Events to trigger webhooks
        /// </summary>
        public List<WebhookEventType> TriggerEvents { get; set; } = new()
        {
            WebhookEventType.TranscriptionCompleted
        };
        
        /// <summary>
        /// Request timeout in seconds
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;
        
        /// <summary>
        /// Retry count on failure
        /// </summary>
        public int RetryCount { get; set; } = 3;
        
        /// <summary>
        /// Custom headers to include
        /// </summary>
        public Dictionary<string, string> CustomHeaders { get; set; } = new();
        
        /// <summary>
        /// Include stack traces in error webhooks (security consideration)
        /// </summary>
        public bool IncludeStackTraces { get; set; } = false;
    }
    
    /// <summary>
    /// Webhook event types
    /// </summary>
    public enum WebhookEventType
    {
        /// <summary>
        /// Transcription completed
        /// </summary>
        TranscriptionCompleted,
        
        /// <summary>
        /// Transcription started
        /// </summary>
        TranscriptionStarted,
        
        /// <summary>
        /// Text injected
        /// </summary>
        TextInjected,
        
        /// <summary>
        /// Security event triggered
        /// </summary>
        SecurityEvent,
        
        /// <summary>
        /// Settings changed
        /// </summary>
        SettingsChanged,
        
        /// <summary>
        /// Error occurred
        /// </summary>
        Error,
        
        /// <summary>
        /// User logged in
        /// </summary>
        UserLogin,
        
        /// <summary>
        /// User logged out
        /// </summary>
        UserLogout,
        
        /// <summary>
        /// Audit event logged
        /// </summary>
        AuditEvent,
        
        /// <summary>
        /// Voice command executed
        /// </summary>
        VoiceCommandExecuted
    }
    
    /// <summary>
    /// Webhook payload for external integrations
    /// </summary>
    public class WebhookPayload
    {
        /// <summary>
        /// Event ID
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>
        /// Event type
        /// </summary>
        public WebhookEventType EventType { get; set; }
        
        /// <summary>
        /// Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// User ID (hashed)
        /// </summary>
        public string? UserId { get; set; }
        
        /// <summary>
        /// Session ID
        /// </summary>
        public string? SessionId { get; set; }
        
        /// <summary>
        /// Event data
        /// </summary>
        public Dictionary<string, object> Data { get; set; } = new();
        
        /// <summary>
        /// Application version
        /// </summary>
        public string AppVersion { get; set; } = "1.0.0";
        
        /// <summary>
        /// Organization ID
        /// </summary>
        public string? OrganizationId { get; set; }
        
        /// <summary>
        /// Generate HMAC signature for verification
        /// </summary>
        public string GenerateSignature(string secret)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(
                System.Text.Encoding.UTF8.GetBytes(secret));
            var data = System.Text.Encoding.UTF8.GetBytes(Id + Timestamp.ToString("O"));
            var hash = hmac.ComputeHash(data);
            return Convert.ToBase64String(hash);
        }
    }
    
    /// <summary>
    /// Deployment result
    /// </summary>
    public class DeploymentResult
    {
        /// <summary>
        /// Success status
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Exit code
        /// </summary>
        public int ExitCode { get; set; }
        
        /// <summary>
        /// Installation path
        /// </summary>
        public string? InstallPath { get; set; }
        
        /// <summary>
        /// Log file path
        /// </summary>
        public string? LogFile { get; set; }
        
        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Deployment duration
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Installed version
        /// </summary>
        public string? Version { get; set; }
    }
    
    /// <summary>
    /// GPO deployment configuration
    /// </summary>
    public class GpoDeploymentConfig
    {
        /// <summary>
        /// Group Policy Object name
        /// </summary>
        public string GpoName { get; set; } = string.Empty;
        
        /// <summary>
        /// Organizational Unit (OU) path
        /// </summary>
        public string? OrganizationalUnit { get; set; }
        
        /// <summary>
        /// Target computers group
        /// </summary>
        public string? TargetComputers { get; set; }
        
        /// <summary>
        /// Target users group
        /// </summary>
        public string? TargetUsers { get; set; }
        
        /// <summary>
        /// Force reinstall
        /// </summary>
        public bool ForceReinstall { get; set; } = false;
        
        /// <summary>
        /// Uninstall existing versions first
        /// </summary>
        public bool UninstallExisting { get; set; } = false;
        
        /// <summary>
        /// Assignment type (Computer or User)
        /// </summary>
        public GpoAssignmentType AssignmentType { get; set; } = GpoAssignmentType.Computer;
    }
    
    /// <summary>
    /// GPO assignment types
    /// </summary>
    public enum GpoAssignmentType
    {
        /// <summary>
        /// Assigned to computers
        /// </summary>
        Computer,
        
        /// <summary>
        /// Assigned to users
        /// </summary>
        User
    }
    
    /// <summary>
    /// Installation detection result
    /// </summary>
    public class InstallationInfo
    {
        /// <summary>
        /// Whether the application is installed
        /// </summary>
        public bool IsInstalled { get; set; }
        
        /// <summary>
        /// Installation path
        /// </summary>
        public string? InstallPath { get; set; }
        
        /// <summary>
        /// Installed version
        /// </summary>
        public string? Version { get; set; }
        
        /// <summary>
        /// Installation date
        /// </summary>
        public DateTime? InstallDate { get; set; }
        
        /// <summary>
        /// MSI product code
        /// </summary>
        public string? ProductCode { get; set; }
        
        /// <summary>
        /// Installation scope
        /// </summary>
        public InstallationScope Scope { get; set; }
    }
}
