using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;
using WhisperKey.Services.Validation;
using WhisperKey.Services.Database;

namespace WhisperKey.Services
{
    /// <summary>
    /// Service for managing API key lifecycle, rotation, and usage tracking.
    /// Uses Windows Credential Manager for secure storage and JsonDatabaseService for metadata.
    /// </summary>
    public class ApiKeyManagementService : IApiKeyManagementService
    {
        private readonly ICredentialService _credentialService;
        private readonly IAuditLoggingService _auditService;
        private readonly JsonDatabaseService _db;
        private readonly IInputValidationService _validationService;
        private readonly ILogger<ApiKeyManagementService> _logger;

        private const string CollectionName = "apikeys_v2";
        private const string CredentialKeyPrefix = "ApiKey_";

        public ApiKeyManagementService(
            ICredentialService credentialService,
            IAuditLoggingService auditService,
            JsonDatabaseService db,
            IInputValidationService validationService,
            ILogger<ApiKeyManagementService> logger)
        {
            _credentialService = credentialService ?? throw new ArgumentNullException(nameof(credentialService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<bool> RegisterKeyAsync(string provider, string name, string keyValue, int rotationDays = 0, ApiKeyScope scopes = ApiKeyScope.Transcription)
        {
            // Validate inputs
            var providerResult = _validationService.Validate(provider, new ValidationRuleSet { Required = true, MaxLength = 50 });
            var nameResult = _validationService.Validate(name, new ValidationRuleSet { Required = true, MaxLength = 100 });
            var keyResult = _validationService.Validate(keyValue, new ValidationRuleSet { Required = true, MinLength = 10 });

            if (!providerResult.IsValid || !nameResult.IsValid || !keyResult.IsValid)
            {
                _logger.LogWarning("Invalid input for API key registration: {Errors}", 
                    string.Join(", ", providerResult.Errors.Concat(nameResult.Errors).Concat(keyResult.Errors)));
                return false;
            }

            var existing = await _db.QueryAsync<ApiKeyMetadata>(CollectionName, m => m.Provider == provider);
            if (existing != null)
            {
                _logger.LogWarning("API key for provider {Provider} already exists.", provider);
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

            await _db.UpsertAsync(CollectionName, metadata, m => m.Provider == provider);

            await _auditService.LogEventAsync(
                AuditEventType.ApiKeyAccessed,
                $"API key registered for {provider}",
                JsonSerializer.Serialize(new { Provider = provider, Name = name, Version = 1 }),
                DataSensitivity.High);

            return true;
        }

        public async Task<bool> RotateKeyAsync(string provider, string newKeyValue)
        {
            // Validate inputs
            var keyResult = _validationService.Validate(newKeyValue, new ValidationRuleSet { Required = true, MinLength = 10 });
            if (!keyResult.IsValid)
            {
                _logger.LogWarning("Invalid input for API key rotation: {Errors}", string.Join(", ", keyResult.Errors));
                return false;
            }

            var metadata = await _db.QueryAsync<ApiKeyMetadata>(CollectionName, m => m.Provider == provider);
            if (metadata == null)
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
                v.ExpiresAt = DateTime.UtcNow.AddHours(24); 
            }

            metadata.Versions.Add(newVersion);
            metadata.ActiveVersionId = newVersion.Id;

            await _db.UpsertAsync(CollectionName, metadata, m => m.Provider == provider);

            await _auditService.LogEventAsync(
                AuditEventType.ApiKeyAccessed,
                $"API key rotated for {provider}",
                JsonSerializer.Serialize(new { Provider = provider, NewVersion = newVersionNumber }),
                DataSensitivity.High);

            return true;
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
            var metadata = await _db.QueryAsync<ApiKeyMetadata>(CollectionName, m => m.Provider == provider);
            if (metadata == null) return false;

            foreach (var version in metadata.Versions)
            {
                version.Status = ApiKeyStatus.Revoked;
                await _credentialService.DeleteCredentialAsync(version.CredentialName);
            }

            await _db.UpsertAsync(CollectionName, metadata, m => m.Provider == provider);

            await _auditService.LogEventAsync(
                AuditEventType.ApiKeyAccessed,
                $"API key revoked for {provider}",
                JsonSerializer.Serialize(new { Provider = provider }),
                DataSensitivity.Critical);

            return true;
        }

        public async Task<ApiKeyMetadata?> GetMetadataAsync(string provider)
        {
            return await _db.QueryAsync<ApiKeyMetadata>(CollectionName, m => m.Provider == provider);
        }

        public async Task<List<ApiKeyMetadata>> ListKeysAsync()
        {
            return await _db.QueryListAsync<ApiKeyMetadata>(CollectionName, _ => true);
        }

        public async Task RecordUsageAsync(string provider, int tokens = 0, decimal cost = 0, bool success = true, string? errorMessage = null)
        {
            var metadata = await _db.QueryAsync<ApiKeyMetadata>(CollectionName, m => m.Provider == provider);
            if (metadata == null) return;

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

            await _db.UpsertAsync(CollectionName, metadata, m => m.Provider == provider);
        }

        public async Task<List<string>> CheckExpirationsAsync()
        {
            var allKeys = await ListKeysAsync();
            var providersToNotify = new List<string>();

            foreach (var metadata in allKeys)
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
            var allKeys = await ListKeysAsync();
            
            foreach (var metadata in allKeys.Where(m => m.RotationDays > 0))
            {
                var activeVersion = metadata.Versions.FirstOrDefault(v => v.Id == metadata.ActiveVersionId);
                if (activeVersion?.ExpiresAt != null && DateTime.UtcNow >= activeVersion.ExpiresAt)
                {
                    if (activeVersion.Status == ApiKeyStatus.Active)
                    {
                        activeVersion.Status = ApiKeyStatus.Expired;
                        await _db.UpsertAsync(CollectionName, metadata, m => m.Provider == metadata.Provider);
                        
                        await _auditService.LogEventAsync(
                            AuditEventType.SecurityEvent,
                            $"API key expired for {metadata.Provider}",
                            JsonSerializer.Serialize(new { Provider = metadata.Provider, Version = activeVersion.Version }),
                            DataSensitivity.Medium);
                    }
                }
            }
        }
    }
}
