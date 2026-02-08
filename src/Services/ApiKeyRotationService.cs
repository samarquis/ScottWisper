using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Background service for managing API key rotation schedules and expiration notifications.
    /// </summary>
    public class ApiKeyRotationService : IDisposable
    {
        private readonly IApiKeyManagementService _keyManagement;
        private readonly IFeedbackService _feedbackService;
        private readonly IAuditLoggingService _auditService;
        private readonly ILogger<ApiKeyRotationService> _logger;
        private Timer? _checkTimer;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(12);

        public ApiKeyRotationService(
            IApiKeyManagementService keyManagement,
            IFeedbackService feedbackService,
            IAuditLoggingService auditService,
            ILogger<ApiKeyRotationService> logger)
        {
            _keyManagement = keyManagement ?? throw new ArgumentNullException(nameof(keyManagement));
            _feedbackService = feedbackService ?? throw new ArgumentNullException(nameof(feedbackService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Start()
        {
            _logger.LogInformation("ApiKeyRotationService starting. Check interval: {Interval}", _checkInterval);
            _checkTimer = new Timer(async _ => await PerformCheckAsync(), null, TimeSpan.Zero, _checkInterval);
        }

        public void Stop()
        {
            _checkTimer?.Change(Timeout.Infinite, 0);
            _logger.LogInformation("ApiKeyRotationService stopped.");
        }

        private async Task PerformCheckAsync()
        {
            _logger.LogDebug("Performing API key lifecycle check...");
            
            try
            {
                // 1. Process automatic rotations (mark expired if new key needed)
                await _keyManagement.ProcessAutomaticRotationsAsync();

                // 2. Check for upcoming expirations
                var expiringProviders = await _keyManagement.CheckExpirationsAsync();
                
                foreach (var provider in expiringProviders)
                {
                    var metadata = await _keyManagement.GetMetadataAsync(provider);
                    if (metadata != null)
                    {
                        await NotifyExpirationAsync(metadata);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during API key lifecycle check");
            }
        }

        private async Task NotifyExpirationAsync(ApiKeyMetadata metadata)
        {
            var activeVersion = metadata.Versions.FirstOrDefault(v => v.Id == metadata.ActiveVersionId);
            if (activeVersion?.ExpiresAt == null) return;

            var daysRemaining = (int)(activeVersion.ExpiresAt.Value - DateTime.UtcNow).TotalDays;
            var message = $"API key for {metadata.Provider} will expire in {daysRemaining} days. Please rotate your key.";
            
            if (daysRemaining <= 0)
            {
                message = $"API key for {metadata.Provider} has expired. Services using this key may fail.";
            }

            _logger.LogWarning("API Key Expiration: {Message}", message);

            // Notify via Toast
            await _feedbackService.ShowToastNotificationAsync(
                "Security Alert", 
                message, 
                IFeedbackService.NotificationType.Warning);

            // Audit the notification
            await _auditService.LogEventAsync(
                AuditEventType.SecurityEvent,
                $"Expiration notification sent for {metadata.Provider}",
                System.Text.Json.JsonSerializer.Serialize(new { Provider = metadata.Provider, DaysRemaining = daysRemaining }),
                DataSensitivity.Medium);
        }

        public void Dispose()
        {
            _checkTimer?.Dispose();
        }
    }
}
