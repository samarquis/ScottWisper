using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of centralized adaptive rate limiting service
    /// </summary>
    public class RateLimitingService : IRateLimitingService
    {
        private readonly ILogger<RateLimitingService> _logger;
        private readonly IAuditLoggingService _auditService;
        private readonly ConcurrentDictionary<string, TokenBucketRateLimiter> _limiters = new();
        private readonly ConcurrentDictionary<string, (int max, int period)> _baselines = new();

        public RateLimitingService(
            ILogger<RateLimitingService> logger,
            IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
            
            InitializeLimiters();
        }

        public bool TryConsume(string resourceName)
        {
            if (!_limiters.TryGetValue(resourceName, out var limiter))
                return true; // No limiter, allow by default

            bool allowed = limiter.TryConsume();
            
            if (!allowed)
            {
                _logger.LogWarning("Rate limit exceeded for resource: {Resource}", resourceName);
                
                // Fire and forget audit log for potential abuse
                _ = _auditService.LogEventAsync(
                    AuditEventType.SecurityEvent,
                    $"[RATE LIMIT] Resource '{resourceName}' throttled.",
                    null,
                    DataSensitivity.Low);
            }

            return allowed;
        }

        public TimeSpan GetWaitTime(string resourceName)
        {
            if (_limiters.TryGetValue(resourceName, out var limiter))
                return limiter.GetTimeUntilNextToken();
            
            return TimeSpan.Zero;
        }

        public void AdjustLimits(double scalingFactor)
        {
            _logger.LogInformation("Adjusting rate limits with factor {Factor}", scalingFactor);
            
            foreach (var name in _limiters.Keys)
            {
                var baseline = _baselines[name];
                var newMax = (int)Math.Max(1, baseline.max * scalingFactor);
                _limiters[name] = new TokenBucketRateLimiter(newMax, baseline.period);
            }
        }

        public void ResetAll()
        {
            foreach (var name in _baselines.Keys)
            {
                var baseline = _baselines[name];
                _limiters[name] = new TokenBucketRateLimiter(baseline.max, baseline.period);
            }
        }

        private void InitializeLimiters()
        {
            // Transcription: 10 per minute
            AddLimiter("Transcription", 10, 1);
            
            // Text Injection: 60 per minute
            AddLimiter("Injection", 60, 1);
            
            // Webhooks: 30 per minute
            AddLimiter("Webhooks", 30, 1);
        }

        private void AddLimiter(string name, int max, int periodMinutes)
        {
            _baselines[name] = (max, periodMinutes);
            _limiters[name] = new TokenBucketRateLimiter(max, periodMinutes);
        }
    }
}
