using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Service for managing API key lifecycle, rotation, and usage tracking.
    /// Uses Windows Credential Manager for secure storage and local JSON for metadata.
    /// </summary>
    public class ApiKeyManagementService : IApiKeyManagementService
    {
        private readonly ICredentialService _credentialService;
        private readonly IAuditLoggingService _auditService;
        private readonly IFileSystemService _fileSystem;
        private readonly ILogger<ApiKeyManagementService> _logger;
        private readonly string _metadataPath;
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);
        private Dictionary<string, ApiKeyMetadata>? _cache;

        private const string MetadataFileName = "apikeys.json";
        private const string CredentialKeyPrefix = "ApiKey_";

        public ApiKeyManagementService(
            ICredentialService credentialService,
            IAuditLoggingService auditService,
            IFileSystemService fileSystem,
            ILogger<ApiKeyManagementService> logger)
        {
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _metadataPath = _fileSystem.CombinePath(_fileSystem.GetAppDataPath(), MetadataFileName);
        }

        public async Task<bool> RegisterKeyAsync(string provider, string name, string keyValue, int rotationDays = 0, ApiKeyScope scopes = ApiKeyScope.Transcription)
        {
            await _lock.WaitAsync();
            try
            {
                var metadataMap = await LoadMetadataAsync();
                
                if (metadataMap.ContainsKey(provider))
                {
                    _logger.LogWarning("API key for provider {Provider} already exists. Use rotation instead.", provider);
                    return false;
                }

                var version = new ApiKeyVersion
                {
                    Version = 1,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = rotationDays > 0 ? DateTime.UtcNow.AddDays(rotationDays) : null,
                    Status = ApiKeyStatus.Active,
                    CredentialName = $"{CredentialKeyPrefix}{provider}_v1"
                };

                var metadata = new ApiKeyMetadata
                {
                    Provider = provider,
                    Name = name,
                    ActiveVersionId = version.Id,
                    RotationDays = rotationDays,
                    Scopes = scopes,
                    Versions = new List<ApiKeyVersion> { version }
                };

                // Store actual key in credential manager
                var stored = await _credentialService.StoreCredentialAsync(version.CredentialName, keyValue);
                if (!stored)
                {
                    _logger.LogError("Failed to store API key in credential manager for {Provider}", provider);
                    return false;
                }

                metadataMap[provider] = metadata;
                await SaveMetadataAsync(metadataMap);

                await _auditService.LogEventAsync(
                    AuditEventType.ApiKeyAccessed,
                    $"API key registered for {provider}",
                    JsonSerializer.Serialize(new { Provider = provider, Name = name, Version = 1 }),
                    DataSensitivity.High);

                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<bool> RotateKeyAsync(string provider, string newKeyValue)
        {
            await _lock.WaitAsync();
            try
            {
                var metadataMap = await LoadMetadataAsync();
                if (!metadataMap.TryGetValue(provider, out var metadata))
                {
                    _logger.LogError("Cannot rotate key: Provider {Provider} not found", provider);
                    return false;
                }

                var currentVersion = metadata.Versions.FirstOrDefault(v => v.Id == metadata.ActiveVersionId);
                var newVersionNumber = (currentVersion?.Version ?? 0) + 1;

                var newVersion = new ApiKeyVersion
                {
                    Version = newVersionNumber,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = metadata.RotationDays > 0 ? DateTime.UtcNow.AddDays(metadata.RotationDays) : null,
                    Status = ApiKeyStatus.Active,
                    CredentialName = $"{CredentialKeyPrefix}{provider}_v{newVersionNumber}"
                };

                // Store new key
                var stored = await _credentialService.StoreCredentialAsync(newVersion.CredentialName, newKeyValue);
                if (!stored)
                {
                    _logger.LogError("Failed to store new API key version for {Provider}", provider);
                    return false;
                }

                // Deprecate old versions
                foreach (var v in metadata.Versions.Where(v => v.Status == ApiKeyStatus.Active))
                {
                    v.Status = ApiKeyStatus.Deprecated;
                    // In a real zero-downtime scenario, we'd keep it for a few hours/days
                    v.ExpiresAt = DateTime.UtcNow.AddHours(24); 
                }

                metadata.Versions.Add(newVersion);
                metadata.ActiveVersionId = newVersion.Id;

                await SaveMetadataAsync(metadataMap);

                await _auditService.LogEventAsync(
                    AuditEventType.ApiKeyAccessed,
                    $"API key rotated for {provider}",
                    JsonSerializer.Serialize(new { Provider = provider, NewVersion = newVersionNumber }),
                    DataSensitivity.High);

                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<string?> GetActiveKeyAsync(string provider)
        {
            var metadata = await GetMetadataAsync(provider);
            if (metadata == null) return null;

            var activeVersion = metadata.Versions.FirstOrDefault(v => v.Id == metadata.ActiveVersionId);
            if (activeVersion == null || activeVersion.Status != ApiKeyStatus.Active)
            {
                _logger.LogWarning("No active version found for provider {Provider}", provider);
                return null;
            }

            return await _credentialService.RetrieveCredentialAsync(activeVersion.CredentialName);
        }

        public async Task<bool> RevokeKeyAsync(string provider)
        {
            await _lock.WaitAsync();
            try
            {
                var metadataMap = await LoadMetadataAsync();
                if (!metadataMap.TryGetValue(provider, out var metadata)) return false;

                foreach (var version in metadata.Versions)
                {
                    version.Status = ApiKeyStatus.Revoked;
                    await _credentialService.DeleteCredentialAsync(version.CredentialName);
                }

                await SaveMetadataAsync(metadataMap);

                await _auditService.LogEventAsync(
                    AuditEventType.ApiKeyAccessed,
                    $"API key revoked for {provider}",
                    JsonSerializer.Serialize(new { Provider = provider }),
                    DataSensitivity.Critical);

                return true;
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<ApiKeyMetadata?> GetMetadataAsync(string provider)
        {
            var metadataMap = await LoadMetadataAsync();
            return metadataMap.TryGetValue(provider, out var metadata) ? metadata : null;
        }

        public async Task<List<ApiKeyMetadata>> ListKeysAsync()
        {
            var metadataMap = await LoadMetadataAsync();
            return metadataMap.Values.ToList();
        }

        public async Task RecordUsageAsync(string provider, int tokens = 0, decimal cost = 0, bool success = true, string? errorMessage = null)
        {
            await _lock.WaitAsync();
            try
            {
                var metadataMap = await LoadMetadataAsync();
                if (!metadataMap.TryGetValue(provider, out var metadata)) return;

                metadata.Usage.TotalRequests++;
                if (!success)
                {
                    metadata.Usage.FailedRequests++;
                    metadata.Usage.LastErrorAt = DateTime.UtcNow;
                    metadata.Usage.LastErrorMessage = errorMessage;
                }
                else
                {
                    metadata.Usage.TotalTokens += tokens;
                    metadata.Usage.TotalCost += cost;
                }

                var activeVersion = metadata.Versions.FirstOrDefault(v => v.Id == metadata.ActiveVersionId);
                if (activeVersion != null)
                {
                    activeVersion.LastUsedAt = DateTime.UtcNow;
                }

                await SaveMetadataAsync(metadataMap);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task<List<string>> CheckExpirationsAsync()
        {
            var metadataMap = await LoadMetadataAsync();
            var providersToNotify = new List<string>();

            foreach (var metadata in metadataMap.Values)
            {
                var activeVersion = metadata.Versions.FirstOrDefault(v => v.Id == metadata.ActiveVersionId);
                if (activeVersion?.ExpiresAt != null)
                {
                    var daysRemaining = (activeVersion.ExpiresAt.Value - DateTime.UtcNow).TotalDays;
                    if (daysRemaining <= metadata.NotificationDays && daysRemaining > 0)
                    {
                        providersToNotify.Add(metadata.Provider);
                    }
                }
            }

            return providersToNotify;
        }

        public async Task ProcessAutomaticRotationsAsync()
        {
            var metadataMap = await LoadMetadataAsync();
            
            foreach (var metadata in metadataMap.Values.Where(m => m.RotationDays > 0))
            {
                var activeVersion = metadata.Versions.FirstOrDefault(v => v.Id == metadata.ActiveVersionId);
                if (activeVersion?.ExpiresAt != null && DateTime.UtcNow >= activeVersion.ExpiresAt)
                {
                    _logger.LogInformation("Automatic rotation triggered for {Provider}", metadata.Provider);
                    
                    // We need the key value to rotate, but we can't automatically generate a NEW external key (e.g. OpenAI key)
                    // Automatic rotation in this context likely means we need the user to provide a new one, 
                    // or if it's an internal key we generate it.
                    // For now, we'll just mark it as expired if we can't automatically rotate.
                    
                    if (activeVersion.Status == ApiKeyStatus.Active)
                    {
                        activeVersion.Status = ApiKeyStatus.Expired;
                        await _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            $"API key expired for {metadata.Provider}",
                            JsonSerializer.Serialize(new { Provider = metadata.Provider, Version = activeVersion.Version }),
                            DataSensitivity.Medium);
                    }
                }
            }
            
            await SaveMetadataAsync(metadataMap);
        }

        private async Task<Dictionary<string, ApiKeyMetadata>> LoadMetadataAsync()
        {
            if (_cache != null) return _cache;

            if (!_fileSystem.FileExists(_metadataPath))
            {
                _cache = new Dictionary<string, ApiKeyMetadata>();
                return _cache;
            }

            try
            {
                var json = await _fileSystem.ReadAllTextAsync(_metadataPath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, ApiKeyMetadata>>(json) ?? new Dictionary<string, ApiKeyMetadata>();
                return _cache;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load API key metadata from {Path}", _metadataPath);
                return new Dictionary<string, ApiKeyMetadata>();
            }
        }

        private async Task SaveMetadataAsync(Dictionary<string, ApiKeyMetadata> metadata)
        {
            _cache = metadata;
            try
            {
                var json = JsonSerializer.Serialize(metadata, new JsonSerializerOptions { WriteIndented = true });
                await _fileSystem.WriteAllTextAsync(_metadataPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save API key metadata to {Path}", _metadataPath);
            }
        }
    }
}
