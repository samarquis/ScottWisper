using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Service for managing API key lifecycle, rotation, and usage tracking.
    /// </summary>
    public interface IApiKeyManagementService
    {
        /// <summary>
        /// Registers a new API key with initial metadata.
        /// </summary>
        Task<bool> RegisterKeyAsync(string provider, string name, string keyValue, int rotationDays = 0, ApiKeyScope scopes = ApiKeyScope.Transcription);

        /// <summary>
        /// Rotates an existing API key, creating a new version.
        /// Provides zero-downtime transition by keeping the old key active for a grace period.
        /// </summary>
        Task<bool> RotateKeyAsync(string provider, string newKeyValue);

        /// <summary>
        /// Retrieves the current active API key value for a provider.
        /// </summary>
        Task<string?> GetActiveKeyAsync(string provider);

        /// <summary>
        /// Revokes all versions of an API key for a provider.
        /// </summary>
        Task<bool> RevokeKeyAsync(string provider);

        /// <summary>
        /// Gets metadata for an API key.
        /// </summary>
        Task<ApiKeyMetadata?> GetMetadataAsync(string provider);

        /// <summary>
        /// Lists all managed API keys.
        /// </summary>
        Task<List<ApiKeyMetadata>> ListKeysAsync();

        /// <summary>
        /// Records usage statistics for an API key version.
        /// </summary>
        Task RecordUsageAsync(string provider, int tokens = 0, decimal cost = 0, bool success = true, string? errorMessage = null);

        /// <summary>
        /// Checks for keys nearing expiration and returns providers that need notification.
        /// </summary>
        Task<List<string>> CheckExpirationsAsync();

        /// <summary>
        /// Performs automatic rotation for keys on a schedule.
        /// </summary>
        Task ProcessAutomaticRotationsAsync();
    }
}
