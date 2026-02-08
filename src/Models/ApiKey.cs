using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Status of an API key in the rotation lifecycle.
    /// </summary>
    public enum ApiKeyStatus
    {
        /// <summary>Key is active and in use.</summary>
        Active,
        /// <summary>Key is deprecated but still valid for zero-downtime transition.</summary>
        Deprecated,
        /// <summary>Key has expired and is no longer valid.</summary>
        Expired,
        /// <summary>Key has been revoked for security reasons.</summary>
        Revoked
    }

    /// <summary>
    /// Permission scopes for API keys.
    /// </summary>
    [Flags]
    public enum ApiKeyScope
    {
        None = 0,
        Transcription = 1,
        Management = 2,
        Analytics = 4,
        Full = Transcription | Management | Analytics
    }

    /// <summary>
    /// Metadata for an API key version.
    /// </summary>
    public class ApiKeyVersion
    {
        /// <summary>Unique identifier for this version.</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();
        
        /// <summary>Version number (incremental).</summary>
        public int Version { get; set; }
        
        /// <summary>When this version was created.</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>When this version will expire.</summary>
        public DateTime? ExpiresAt { get; set; }
        
        /// <summary>When this version was last successfully used.</summary>
        public DateTime? LastUsedAt { get; set; }
        
        /// <summary>Current status of this version.</summary>
        public ApiKeyStatus Status { get; set; } = ApiKeyStatus.Active;
        
        /// <summary>Target name in credential manager.</summary>
        public string CredentialName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Management metadata for an API key.
    /// </summary>
    public class ApiKeyMetadata
    {
        /// <summary>The provider this key is for (e.g., OpenAI, Azure).</summary>
        public string Provider { get; set; } = string.Empty;
        
        /// <summary>Friendly name for this key.</summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>Current active version identifier.</summary>
        public string ActiveVersionId { get; set; } = string.Empty;
        
        /// <summary>History of all versions for this key.</summary>
        public List<ApiKeyVersion> Versions { get; set; } = new List<ApiKeyVersion>();
        
        /// <summary>Rotation schedule in days (0 = manual only).</summary>
        public int RotationDays { get; set; }
        
        /// <summary>Notification lead time in days.</summary>
        public int NotificationDays { get; set; } = 7;
        
        /// <summary>Allowed scopes for this key.</summary>
        public ApiKeyScope Scopes { get; set; } = ApiKeyScope.Transcription;
        
        /// <summary>Usage statistics for the key.</summary>
        public ApiKeyUsageStats Usage { get; set; } = new ApiKeyUsageStats();
    }

    /// <summary>
    /// Usage statistics for an API key.
    /// </summary>
    public class ApiKeyUsageStats
    {
        public int TotalRequests { get; set; }
        public int FailedRequests { get; set; }
        public long TotalTokens { get; set; }
        public decimal TotalCost { get; set; }
        public DateTime? LastErrorAt { get; set; }
        public string? LastErrorMessage { get; set; }
    }
}
