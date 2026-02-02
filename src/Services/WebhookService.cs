using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for webhook service
    /// </summary>
    public interface IWebhookService
    {
        /// <summary>
        /// Configure webhook
        /// </summary>
        Task ConfigureAsync(WebhookConfig config);
        
        /// <summary>
        /// Send webhook for an event
        /// </summary>
        Task<WebhookResult> SendWebhookAsync(WebhookEventType eventType, Dictionary<string, object> data, string? sessionId = null);
        
        /// <summary>
        /// Send transcription completed webhook
        /// </summary>
        Task<WebhookResult> SendTranscriptionCompletedAsync(string transcription, string? application, TimeSpan duration);
        
        /// <summary>
        /// Send text injected webhook
        /// </summary>
        Task<WebhookResult> SendTextInjectedAsync(string application, string text);
        
        /// <summary>
        /// Send settings changed webhook
        /// </summary>
        Task<WebhookResult> SendSettingsChangedAsync(string settingName, object oldValue, object newValue);
        
        /// <summary>
        /// Send error webhook
        /// </summary>
        Task<WebhookResult> SendErrorAsync(string error, string? stackTrace = null);
        
        /// <summary>
        /// Get webhook configuration
        /// </summary>
        WebhookConfig GetConfig();
        
        /// <summary>
        /// Enable or disable webhooks
        /// </summary>
        void SetEnabled(bool enabled);
        
        /// <summary>
        /// Check if webhooks are enabled
        /// </summary>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Get webhook statistics
        /// </summary>
        Task<WebhookStatistics> GetStatisticsAsync();
        
        /// <summary>
        /// Test webhook connection
        /// </summary>
        Task<WebhookTestResult> TestConnectionAsync();
    }
    
    /// <summary>
    /// Webhook service for external integrations
    /// </summary>
    public class WebhookService : IWebhookService
    {
        private readonly ILogger<WebhookService> _logger;
        private readonly HttpClient _httpClient;
        private WebhookConfig _config = new();
        private bool _isEnabled = false;
        private readonly List<WebhookLogEntry> _log = new();
        private readonly object _lock = new();
        private AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
        
        public bool IsEnabled => _isEnabled && _config.Enabled;
        
        public WebhookService(ILogger<WebhookService> logger, HttpClient? httpClient = null)
        {
            _logger = logger;
            _httpClient = httpClient ?? new HttpClient();
        }
        
        /// <summary>
        /// Configure webhook
        /// </summary>
        public Task ConfigureAsync(WebhookConfig config)
        {
            _config = config;
            _isEnabled = config.Enabled;
            
            // Configure HttpClient timeout
            _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
            
            _logger.LogInformation("Webhook configured for endpoint: {Endpoint}", config.EndpointUrl);
            
            InitializeResiliencePolicies();
            
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Send webhook for an event
        /// </summary>
        public async Task<WebhookResult> SendWebhookAsync(WebhookEventType eventType, Dictionary<string, object> data, string? sessionId = null)
        {
            if (!IsEnabled)
            {
                return new WebhookResult { Success = true, Skipped = true };
            }
            
            if (!_config.TriggerEvents.Contains(eventType))
            {
                return new WebhookResult { Success = true, Skipped = true };
            }
            
            var payload = CreatePayload(eventType, data, sessionId);
            return await SendPayloadAsync(payload);
        }
        
        /// <summary>
        /// Send transcription completed webhook
        /// </summary>
        public async Task<WebhookResult> SendTranscriptionCompletedAsync(string transcription, string? application, TimeSpan duration)
        {
            var data = new Dictionary<string, object>
            {
                ["transcription"] = transcription,
                ["application"] = application ?? "unknown",
                ["duration_ms"] = duration.TotalMilliseconds,
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            };
            
            return await SendWebhookAsync(WebhookEventType.TranscriptionCompleted, data);
        }
        
        /// <summary>
        /// Send text injected webhook
        /// </summary>
        public async Task<WebhookResult> SendTextInjectedAsync(string application, string text)
        {
            var data = new Dictionary<string, object>
            {
                ["application"] = application,
                ["text_length"] = text.Length,
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            };
            
            return await SendWebhookAsync(WebhookEventType.TextInjected, data);
        }
        
        /// <summary>
        /// Send settings changed webhook
        /// </summary>
        public async Task<WebhookResult> SendSettingsChangedAsync(string settingName, object oldValue, object newValue)
        {
            var data = new Dictionary<string, object>
            {
                ["setting_name"] = settingName,
                ["old_value"] = oldValue?.ToString() ?? "null",
                ["new_value"] = newValue?.ToString() ?? "null",
                ["timestamp"] = DateTime.UtcNow.ToString("O")
            };
            
            return await SendWebhookAsync(WebhookEventType.SettingsChanged, data);
        }
        
        /// <summary>
        /// Send error webhook
        /// </summary>
        public async Task<WebhookResult> SendErrorAsync(string error, string? stackTrace = null)
        {
            var data = new Dictionary<string, object>
            {
                ["error"] = SanitizeErrorMessage(error),
                ["timestamp"] = DateTime.UtcNow.ToString("O"),
                ["user_id"] = HashValue(Environment.UserName)
            };
            
            // Only include stack trace in debug builds or if explicitly configured
            if (!string.IsNullOrEmpty(stackTrace) && _config.IncludeStackTraces)
            {
                data["stack_trace"] = SanitizeStackTrace(stackTrace);
            }
            
            return await SendWebhookAsync(WebhookEventType.Error, data);
        }
        
        /// <summary>
        /// Get webhook configuration
        /// </summary>
        public WebhookConfig GetConfig()
        {
            return _config;
        }
        
        /// <summary>
        /// Enable or disable webhooks
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _isEnabled = enabled;
            _logger.LogInformation("Webhooks {Status}", enabled ? "enabled" : "disabled");
        }
        
        /// <summary>
        /// Get webhook statistics
        /// </summary>
        public Task<WebhookStatistics> GetStatisticsAsync()
        {
            lock (_lock)
            {
                var stats = new WebhookStatistics
                {
                    TotalSent = _log.Count,
                    Successful = _log.Count(l => l.Success),
                    Failed = _log.Count(l => !l.Success),
                    LastSent = _log.LastOrDefault()?.Timestamp
                };
                
                return Task.FromResult(stats);
            }
        }
        
        /// <summary>
        /// Test webhook connection
        /// </summary>
        public async Task<WebhookTestResult> TestConnectionAsync()
        {
            if (!IsEnabled || string.IsNullOrEmpty(_config.EndpointUrl))
            {
                return new WebhookTestResult
                {
                    Success = false,
                    Error = "Webhooks not configured"
                };
            }
            
            try
            {
                var testPayload = CreatePayload(WebhookEventType.TranscriptionCompleted, new Dictionary<string, object>
                {
                    ["test"] = true,
                    ["message"] = "Webhook connection test"
                });
                
                var json = JsonSerializer.Serialize(testPayload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                
                // Add headers
                AddHeaders(content, testPayload);
                
                var response = await _httpClient.PostAsync(_config.EndpointUrl, content);
                
                var result = new WebhookTestResult
                {
                    Success = response.IsSuccessStatusCode,
                    StatusCode = (int)response.StatusCode,
                    Response = await response.Content.ReadAsStringAsync()
                };
                
                if (!response.IsSuccessStatusCode)
                {
                    result.Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                }
                
                return result;
            }
            catch (Exception ex)
            {
                return new WebhookTestResult
                {
                    Success = false,
                    Error = ex.Message
                };
            }
        }
        
        /// <summary>
        /// Initialize Polly resilience policies
        /// </summary>
        private void InitializeResiliencePolicies()
        {
            _retryPolicy = Policy<HttpResponseMessage>
                .Handle<HttpRequestException>()
                .OrResult(r => r.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                    || r.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                    || r.StatusCode == System.Net.HttpStatusCode.BadGateway
                    || r.StatusCode == System.Net.HttpStatusCode.GatewayTimeout)
                .WaitAndRetryAsync(3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogWarning("Webhook retry {RetryCount} after {Delay}s due to: {Error}",
                            retryCount, timeSpan.TotalSeconds, exception.Exception?.Message ?? "transient failure");
                    });

            _circuitBreakerPolicy = Policy
                .Handle<Exception>()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30),
                    (ex, duration) =>
                    {
                        _logger.LogError("Circuit breaker opened for {Duration}s due to: {Error}",
                            duration.TotalSeconds, ex.Message);
                    },
                    () =>
                    {
                        _logger.LogInformation("Circuit breaker closed, webhooks resuming");
                    });
        }

        /// <summary>
        /// Create webhook payload
        /// </summary>
        private WebhookPayload CreatePayload(WebhookEventType eventType, Dictionary<string, object> data, string? sessionId = null)
        {
            return new WebhookPayload
            {
                EventType = eventType,
                UserId = HashValue(Environment.UserName),
                SessionId = sessionId ?? Guid.NewGuid().ToString("N")[..8],
                Data = data,
                OrganizationId = _config.CustomHeaders.ContainsKey("X-Organization-ID") 
                    ? _config.CustomHeaders["X-Organization-ID"] 
                    : null
            };
        }
        
        /// <summary>
        /// Send payload to webhook endpoint
        /// </summary>
        private async Task<WebhookResult> SendPayloadAsync(WebhookPayload payload)
        {
            var startTime = DateTime.UtcNow;
            var result = new WebhookResult { PayloadId = payload.Id };
            
            if (_circuitBreakerPolicy == null)
            {
                _logger.LogWarning("Resilience policies not initialized, webhook aborted");
                result.Success = false;
                result.Error = "Resilience policies not initialized";
                return result;
            }
            
            // Check if circuit breaker is open
            if (_circuitBreakerPolicy.CircuitState == CircuitState.Open)
            {
                _logger.LogWarning("Circuit breaker is open, webhook blocked");
                result.Success = false;
                result.Error = "Circuit breaker is open";
                return result;
            }
            
            try
            {
                var response = await _circuitBreakerPolicy.ExecuteAsync(async () =>
                {
                    return await _retryPolicy.ExecuteAsync(async () =>
                    {
                        var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
                        {
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                        });
                        
                        var content = new StringContent(json, Encoding.UTF8, "application/json");
                        
                        // Add headers
                        AddHeaders(content, payload);
                        
                        return await _httpClient.PostAsync(_config.EndpointUrl, content);
                    });
                });
                
                result.StatusCode = (int)response.StatusCode;
                result.Response = await response.Content.ReadAsStringAsync();
                result.Success = response.IsSuccessStatusCode;
                result.Duration = DateTime.UtcNow - startTime;
                
                LogEntry(payload, result, result.Success, 0);
                
                return result;
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = SanitizeErrorMessage(ex.Message);
                result.Duration = DateTime.UtcNow - startTime;
                
                _logger.LogError("Webhook failed: {Error}", result.Error);
                LogEntry(payload, result, false, 0);
                
                return result;
            }
        }
        
        /// <summary>
        /// Add headers to request
        /// </summary>
        private void AddHeaders(StringContent content, WebhookPayload payload)
        {
            var headers = content.Headers;
            
            // Add signature if secret is configured
            if (!string.IsNullOrEmpty(_config.Secret))
            {
                var signature = payload.GenerateSignature(_config.Secret);
                headers.Add("X-Webhook-Signature", signature);
            }
            
            // Add auth token if configured
            if (!string.IsNullOrEmpty(_config.AuthToken))
            {
                headers.Add("Authorization", $"Bearer {_config.AuthToken}");
            }
            
            // Add custom headers
            foreach (var header in _config.CustomHeaders)
            {
                headers.Add(header.Key, header.Value);
            }
            
            // Add standard headers
            headers.Add("X-Webhook-Id", payload.Id);
            headers.Add("X-Webhook-Timestamp", payload.Timestamp.ToString("O"));
            headers.Add("X-Webhook-Event", payload.EventType.ToString());
        }
        
        /// <summary>
        /// Log webhook entry
        /// </summary>
        private void LogEntry(WebhookPayload payload, WebhookResult result, bool success, int attempts)
        {
            lock (_lock)
            {
                _log.Add(new WebhookLogEntry
                {
                    Timestamp = DateTime.UtcNow,
                    PayloadId = payload.Id,
                    EventType = payload.EventType,
                    Success = success,
                    StatusCode = result.StatusCode,
                    Attempts = attempts + 1,
                    Duration = result.Duration
                });
            }
        }
        
        /// <summary>
        /// Hash a value for privacy
        /// </summary>
        private string HashValue(string value)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
        
        /// <summary>
        /// Sanitize error message to remove sensitive information
        /// </summary>
        private string SanitizeErrorMessage(string error)
        {
            if (string.IsNullOrEmpty(error))
                return error;
            
            // Remove common sensitive patterns
            var sanitized = error;
            
            // Remove file paths (Windows and Unix)
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[A-Za-z]:\\[^\s]*|/[^\s]*", "[PATH_REDACTED]");
            
            // Remove potential connection strings, API keys, tokens
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"(password|pwd|secret|token|key|apikey)=[^&\s]*", "$1=[REDACTED]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove email addresses
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[EMAIL_REDACTED]");
            
            return sanitized;
        }
        
        /// <summary>
        /// Sanitize stack trace to remove sensitive file paths
        /// </summary>
        private string SanitizeStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace))
                return stackTrace;
            
            // Remove full file paths, keep only filename
            var sanitized = System.Text.RegularExpressions.Regex.Replace(stackTrace, @"[A-Za-z]:\\[^\s]*\\([^\s]+\.cs|[^\s]+\.dll)", "$1");
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"/[^\s]*/([^/\s]+\.cs|[^/\s]+\.dll)", "$1");
            
            return sanitized;
        }
    }
    
    /// <summary>
    /// Webhook send result
    /// </summary>
    public class WebhookResult
    {
        /// <summary>
        /// Success status
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Whether the webhook was skipped (not configured for this event)
        /// </summary>
        public bool Skipped { get; set; }
        
        /// <summary>
        /// HTTP status code
        /// </summary>
        public int StatusCode { get; set; }
        
        /// <summary>
        /// Response body
        /// </summary>
        public string? Response { get; set; }
        
        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? Error { get; set; }
        
        /// <summary>
        /// Request duration
        /// </summary>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Payload ID
        /// </summary>
        public string? PayloadId { get; set; }
    }
    
    /// <summary>
    /// Webhook test result
    /// </summary>
    public class WebhookTestResult
    {
        /// <summary>
        /// Success status
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// HTTP status code
        /// </summary>
        public int StatusCode { get; set; }
        
        /// <summary>
        /// Response body
        /// </summary>
        public string? Response { get; set; }
        
        /// <summary>
        /// Error message if failed
        /// </summary>
        public string? Error { get; set; }
    }
    
    /// <summary>
    /// Webhook statistics
    /// </summary>
    public class WebhookStatistics
    {
        /// <summary>
        /// Total webhooks sent
        /// </summary>
        public int TotalSent { get; set; }
        
        /// <summary>
        /// Successful deliveries
        /// </summary>
        public int Successful { get; set; }
        
        /// <summary>
        /// Failed deliveries
        /// </summary>
        public int Failed { get; set; }
        
        /// <summary>
        /// Last webhook sent timestamp
        /// </summary>
        public DateTime? LastSent { get; set; }
        
        /// <summary>
        /// Success rate percentage
        /// </summary>
        public double SuccessRate => TotalSent > 0 ? (double)Successful / TotalSent * 100 : 0;
    }
    
    /// <summary>
    /// Webhook log entry
    /// </summary>
    public class WebhookLogEntry
    {
        /// <summary>
        /// Timestamp
        /// </summary>
        public DateTime Timestamp { get; set; }
        
        /// <summary>
        /// Payload ID
        /// </summary>
        public string? PayloadId { get; set; }
        
        /// <summary>
        /// Event type
        /// </summary>
        public WebhookEventType EventType { get; set; }
        
        /// <summary>
        /// Success status
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// HTTP status code
        /// </summary>
        public int StatusCode { get; set; }
        
        /// <summary>
        /// Number of attempts
        /// </summary>
        public int Attempts { get; set; }
        
        /// <summary>
        /// Duration
        /// </summary>
        public TimeSpan Duration { get; set; }
    }
}
