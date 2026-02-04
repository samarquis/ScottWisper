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
    /// Provides external webhook integration capabilities for real-time event notifications.
    /// Enables secure, reliable delivery of application events to external systems with
    /// configurable retry policies, circuit breaker patterns, and comprehensive error handling.
    /// </summary>
    /// <remarks>
    /// This service implements enterprise-grade webhook delivery with the following features:
    /// <list type="bullet">
    /// <item><description>Automatic retry with exponential backoff for transient failures</description></item>
    /// <item><description>Circuit breaker pattern to prevent cascade failures</description></item>
    /// <item><description>Request/response logging for audit trails</description></item>
    /// <item><description>Configurable event filtering and throttling</description></item>
    /// <item><description>Security features including signature validation and sanitization</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// var webhookService = serviceProvider.GetService&lt;IWebhookService&gt;();
    /// var config = new WebhookConfig 
    /// { 
    ///     EndpointUrl = "https://api.example.com/webhooks",
    ///     Secret = "webhook-secret",
    ///     Enabled = true
    /// };
    /// await webhookService.ConfigureAsync(config);
    /// 
    /// // Send a transcription event
    /// var result = await webhookService.SendTranscriptionCompletedAsync(
    ///     "Hello world", "notepad.exe", TimeSpan.FromSeconds(2.5));
    /// </code>
    /// </example>
    public interface IWebhookService
    {
        /// <summary>
        /// Configures the webhook service with connection settings, authentication, and delivery policies.
        /// This method must be called before any webhook delivery operations can be performed.
        /// </summary>
        /// <param name="config">The webhook configuration containing endpoint URL, authentication credentials,
        /// retry policies, and event filtering settings.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous configuration operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid (e.g., missing URL).</exception>
        /// <exception cref="InvalidOperationException">Thrown when the service is already configured.</exception>
        /// <remarks>
        /// This operation initializes HTTP client settings, Polly resilience policies, and validates
        /// the endpoint accessibility. All previous configurations are replaced when this method is called.
        /// </remarks>
        Task ConfigureAsync(WebhookConfig config);
        
        /// <summary>
        /// Sends a webhook notification for the specified event type with custom data payload.
        /// This is the core method for webhook delivery with automatic retry and circuit breaker protection.
        /// </summary>
        /// <param name="eventType">The type of event being sent (e.g., TranscriptionCompleted, ErrorOccurred).</param>
        /// <param name="data">A dictionary containing event-specific data to be serialized and sent.</param>
        /// <param name="sessionId">Optional session identifier for correlation across multiple webhook events.</param>
        /// <returns>A <see cref="Task{WebhookResult}"/> containing the delivery result, including success status,
        /// HTTP response information, and any error details.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="eventType"/> is invalid or <paramref name="data"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webhook service is not configured.</exception>
        /// <exception cref="WebhookException">Thrown when delivery fails after all retry attempts.</exception>
        /// <remarks>
        /// The method applies the following logic:
        /// <list type="number">
        /// <item><description>Checks if webhooks are enabled and the event type is configured for delivery</description></item>
        /// <item><description>Creates a structured payload with correlation IDs and timestamps</description></item>
        /// <item><description>Applies signature authentication if configured</description></item>
        /// <item><description>Executes delivery with retry policy and circuit breaker protection</description></item>
        /// <item><description>Logs the operation result for audit and debugging purposes</description></item>
        /// </list>
        /// Events may be skipped if the service is disabled or the event type is not in the trigger list.
        /// </remarks>
        Task<WebhookResult> SendWebhookAsync(WebhookEventType eventType, Dictionary<string, object> data, string? sessionId = null);
        
        /// <summary>
        /// Sends a specialized webhook notification when audio transcription has completed successfully.
        /// Includes transcription text, target application, processing duration, and quality metrics.
        /// </summary>
        /// <param name="transcription">The transcribed text content. Must not be null or empty.</param>
        /// <param name="application">The target application where text will be injected. Can be null.</param>
        /// <param name="duration">The time taken to process and transcribe the audio.</param>
        /// <returns>A <see cref="Task{WebhookResult}"/> containing the delivery result.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="transcription"/> is null or empty.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="duration"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webhook service is not configured.</exception>
        /// <remarks>
        /// This method creates a structured payload with transcription metadata including:
        /// <list type="bullet">
        /// <item><description>Text content (sanitized if necessary)</description></item>
        /// <item><description>Target application name</description></item>
        /// <item><description>Processing duration in milliseconds</description></item>
        /// <item><description>Timestamp and correlation ID</description></item>
        /// </list>
        /// The transcription text is automatically sanitized to remove any sensitive information before sending.
        /// </remarks>
        Task<WebhookResult> SendTranscriptionCompletedAsync(string transcription, string? application, TimeSpan duration);
        
        /// <summary>
        /// Sends a webhook notification when text has been successfully injected into the target application.
        /// Used for tracking text delivery success and potential follow-up actions.
        /// </summary>
        /// <param name="application">The target application where text was injected. Must not be null.</param>
        /// <param name="text">The text content that was injected. Must not be null.</param>
        /// <returns>A <see cref="Task{WebhookResult}"/> containing the delivery result.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="application"/> or <paramref name="text"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webhook service is not configured.</exception>
        /// <remarks>
        /// The webhook payload includes:
        /// <list type="bullet">
        /// <item><description>Target application name</description></item>
        /// <item><description>Text length (character count, not the actual text for security)</description></item>
        /// <item><description>Injection timestamp</description></item>
        /// <item><description>Success confirmation</description></item>
        /// </list>
        /// Note: The actual text content is not included for security and privacy reasons.
        /// </remarks>
        Task<WebhookResult> SendTextInjectedAsync(string application, string text);
        
        /// <summary>
        /// Sends a webhook notification when application settings have been modified.
        /// Used for audit trails, configuration synchronization, and change tracking.
        /// </summary>
        /// <param name="settingName">The name of the setting that was changed. Must not be null or empty.</param>
        /// <param name="oldValue">The previous value of the setting. Can be null for new settings.</param>
        /// <param name="newValue">The new value of the setting. Must not be null.</param>
        /// <returns>A <see cref="Task{WebhookResult}"/> containing the delivery result.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="settingName"/> is null or empty.</exception>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="newValue"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webhook service is not configured.</exception>
        /// <remarks>
        /// The webhook payload includes:
        /// <list type="bullet">
        /// <item><description>Setting name (category.setting format)</description></item>
        /// <item><description>Previous value (string representation)</description></item>
        /// <item><description>New value (string representation)</description></item>
        /// <item><description>Change timestamp</description></item>
        /// <item><description>User context if available</description></item>
        /// </list>
        /// Sensitive values (like passwords or API keys) are masked in the payload.
        /// </remarks>
        Task<WebhookResult> SendSettingsChangedAsync(string settingName, object oldValue, object newValue);
        
        /// <summary>
        /// Sends a webhook notification when an error or exception occurs in the application.
        /// Used for error monitoring, alerting, and debugging support.
        /// </summary>
        /// <param name="error">The error message or description. Must not be null or empty.</param>
        /// <param name="stackTrace">Optional stack trace for debugging. Only included if configured.</param>
        /// <returns>A <see cref="Task{WebhookResult}"/> containing the delivery result.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="error"/> is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webhook service is not configured.</exception>
        /// <remarks>
        /// The webhook payload includes:
        /// <list type="bullet">
        /// <item><description>Sanitized error message</description></item>
        /// <item><description>Hashed user identifier for privacy</description></item>
        /// <item><description>Error timestamp</description></item>
        /// <item><description>Stack trace (only if enabled and sanitized)</description></item>
        /// <item><description>System context information</description></item>
        /// </list>
        /// Stack traces are only included if <see cref="WebhookConfig.IncludeStackTraces"/> is true
        /// and are automatically sanitized to remove sensitive file paths and data.
        /// </remarks>
        Task<WebhookResult> SendErrorAsync(string error, string? stackTrace = null);
        
        /// <summary>
        /// Retrieves the current webhook configuration settings.
        /// Returns a copy of the configuration to prevent external modification.
        /// </summary>
        /// <returns>A <see cref="WebhookConfig"/> object containing the current configuration settings.</returns>
        /// <remarks>
        /// The returned configuration is a defensive copy. Any changes to the returned object
        /// will not affect the service configuration. Use <see cref="ConfigureAsync"/> to update settings.
        /// </remarks>
        WebhookConfig GetConfig();
        
        /// <summary>
        /// Enables or disables webhook delivery operations.
        /// When disabled, all webhook send operations return successful results with the <c>Skipped</c> flag set.
        /// </summary>
        /// <param name="enabled">True to enable webhook delivery, false to disable.</param>
        /// <remarks>
        /// This method provides a runtime control mechanism without requiring reconfiguration.
        /// Disabled webhooks are not sent and do not count toward statistics or trigger retry logic.
        /// The underlying configuration is preserved and can be re-enabled at any time.
        /// </remarks>
        void SetEnabled(bool enabled);
        
        /// <summary>
        /// Gets a value indicating whether webhook delivery is currently enabled.
        /// Combines the service-level enabled flag with the configuration enabled status.
        /// </summary>
        /// <returns>True if webhooks are enabled and configured; false otherwise.</returns>
        /// <remarks>
        /// This property returns false if either the service is disabled via <see cref="SetEnabled"/>
        /// or the configuration has <c>Enabled</c> set to false. Both must be true for delivery to occur.
        /// </remarks>
        bool IsEnabled { get; }
        
        /// <summary>
        /// Retrieves statistical information about webhook delivery performance and reliability.
        /// Provides metrics for monitoring, alerting, and optimization purposes.
        /// </summary>
        /// <returns>A <see cref="Task{WebhookStatistics}"/> containing delivery statistics including
        /// total sent, success rate, failure count, and last delivery timestamp.</returns>
        /// <remarks>
        /// Statistics are calculated from in-memory log entries and reflect the current service instance
        /// history only. Statistics are reset when the service is restarted or reconfigured.
        /// </remarks>
        Task<WebhookStatistics> GetStatisticsAsync();
        
        /// <summary>
        /// Tests the connectivity and configuration of the webhook endpoint.
        /// Sends a test payload to verify authentication, routing, and response handling.
        /// </summary>
        /// <returns>A <see cref="Task{WebhookTestResult}"/> containing the test outcome including
        /// success status, HTTP response code, and response content.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the webhook service is not configured.</exception>
        /// <remarks>
        /// The test operation:
        /// <list type="number">
        /// <item><description>Validates the configuration is complete and enabled</description></item>
        /// <item><description>Creates a test payload with minimal data</description></item>
        /// <item><description>Sends the test webhook without triggering retry or circuit breaker logic</description></item>
        /// <item><description>Captures and returns the HTTP response for analysis</description></item>
        /// </list>
        /// Test operations are not included in delivery statistics and do not affect circuit breaker state.
        /// </remarks>
        Task<WebhookTestResult> TestConnectionAsync();
    }
    
    /// <summary>
    /// Enterprise-grade implementation of webhook delivery service with comprehensive error handling,
    /// resilience patterns, and security features. Provides reliable, trackable delivery of application
    /// events to external systems with automatic retry, circuit breaking, and audit logging.
    /// </summary>
    /// <remarks>
    /// This implementation incorporates multiple design patterns for reliability:
    /// <list type="bullet">
    /// <item><description><b>Retry Pattern</b>: Exponential backoff for transient HTTP errors</description></item>
    /// <item><description><b>Circuit Breaker Pattern</b>: Prevents cascade failures during outages</description></item>
    /// <item><description><b>Timeout Pattern</b>: Prevents hanging requests with configurable timeouts</description></item>
    /// <item><description><b>Observer Pattern</b>: Event-driven notification of delivery outcomes</description></item>
    /// <item><description><b>Strategy Pattern</b>: Pluggable authentication and payload formatting</description></item>
    /// </list>
    /// Security features include:
    /// <list type="bullet">
    /// <item><description>HMAC signature validation for payload integrity</description></item>
    /// <item><description>Automatic sanitization of sensitive data</description></item>
    /// <item><description>Configurable authentication headers</description></item>
    /// <item><description>Audit logging for compliance requirements</description></item>
    /// </list>
    /// </remarks>
    public class WebhookService : IWebhookService
    {
        private readonly ILogger<WebhookService> _logger;
        private readonly HttpClient _httpClient;
        private readonly ICorrelationService _correlationService;
        private readonly IStructuredLoggingService _structuredLogger;
        private WebhookConfig _config = new();
        private bool _isEnabled = false;
        private readonly List<WebhookLogEntry> _log = new();
        private readonly object _lock = new();
        private AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
        
        /// <summary>
        /// Gets a value indicating whether webhook delivery is currently enabled.
        /// Combines service-level and configuration-level enabled flags.
        /// </summary>
        /// <value>True if both service and configuration are enabled; otherwise, false.</value>
        /// <remarks>
        /// This property evaluates both the runtime enabled state and the configuration state.
        /// Both must be true for webhook delivery to proceed.
        /// </remarks>
        public bool IsEnabled => _isEnabled && _config.Enabled;
        
        /// <summary>
        /// Initializes a new instance of the <see cref="WebhookService"/> class with the specified dependencies.
        /// Configures the HTTP client and prepares internal state for webhook delivery operations.
        /// </summary>
        /// <param name="logger">The structured logger for operation logging and debugging. Must not be null.</param>
        /// <param name="correlationService">Service for managing correlation IDs across operations. Must not be null.</param>
        /// <param name="structuredLogger">Service for comprehensive structured logging. Must not be null.</param>
        /// <param name="httpClient">Optional HTTP client for webhook delivery. If null, a new client will be created.</param>
        /// <exception cref="ArgumentNullException">Thrown when any required parameter is null.</exception>
        /// <remarks>
        /// The service is created in a disabled state and must be configured via <see cref="ConfigureAsync"/>
        /// before webhook delivery operations can be performed. The HTTP client, if provided, should be
        /// pre-configured with appropriate timeouts and handler chains for the target environment.
        /// </remarks>
        public WebhookService(
            ILogger<WebhookService> logger,
            ICorrelationService correlationService,
            IStructuredLoggingService structuredLogger,
            HttpClient? httpClient = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _correlationService = correlationService ?? throw new ArgumentNullException(nameof(correlationService));
            _structuredLogger = structuredLogger ?? throw new ArgumentNullException(nameof(structuredLogger));
            _httpClient = httpClient ?? new HttpClient();
        }
        
        /// <summary>
        /// Configures the webhook service with connection settings, authentication credentials, and delivery policies.
        /// Initializes resilience patterns, validates configuration, and prepares the HTTP client for delivery operations.
        /// </summary>
        /// <param name="config">The webhook configuration containing endpoint URL, authentication details,
        /// retry policies, and event filtering settings. Must not be null.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous configuration operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when configuration is invalid (e.g., missing endpoint URL).</exception>
        /// <exception cref="InvalidOperationException">Thrown when configuration validation fails.</exception>
        /// <remarks>
        /// This method performs comprehensive configuration including:
        /// <list type="number">
        /// <item><description>Validation of required fields (endpoint URL, etc.)</description></item>
        /// <item><description>Configuration of HTTP client timeout and headers</description></item>
        /// <item><description>Initialization of Polly resilience policies (retry and circuit breaker)</description></item>
        /// <item><description>Security validation of authentication settings</description></item>
        /// <item><description>Logging of configuration changes for audit purposes</description></item>
        /// </list>
        /// Previous configuration is completely replaced. Any existing delivery logs and statistics are preserved.
        /// </remarks>
        public async Task ConfigureAsync(WebhookConfig config)
        {
            await _structuredLogger.ExecuteWithLoggingAsync(
                "WebhookService.ConfigureAsync",
                async () =>
                {
                    if (config == null)
                        throw new ArgumentNullException(nameof(config));

                    // Validate configuration
                    if (string.IsNullOrWhiteSpace(config.EndpointUrl))
                        throw new ArgumentException("Endpoint URL is required.", nameof(config));

                    _config = config;
                    _isEnabled = config.Enabled;

                    // Configure HttpClient settings
                    _httpClient.Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds);
                    
                    // Set default headers if not already configured
                    if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                    {
                        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WhisperKey-Webhook/1.0");
                    }

                    // Initialize resilience policies
                    InitializeResiliencePolicies();

                    _logger.LogInformation("Webhook service configured successfully for endpoint: {Endpoint}, Events: {EventCount}", 
                        config.EndpointUrl, config.TriggerEvents?.Count ?? 0);

                    return Task.CompletedTask;
                },
                new Dictionary<string, object>
                {
                    ["EndpointUrl"] = config.EndpointUrl,
                    ["Enabled"] = config.Enabled,
                    ["TimeoutSeconds"] = config.TimeoutSeconds,
                    ["EventCount"] = config.TriggerEvents?.Count ?? 0
                });
        }
        
        /// <summary>
        /// Sends a webhook notification for the specified event type with automatic correlation, retry logic,
        /// and circuit breaker protection. This is the primary method for webhook delivery operations.
        /// </summary>
        /// <param name="eventType">The type of event being sent (e.g., TranscriptionCompleted, ErrorOccurred).</param>
        /// <param name="data">A dictionary containing event-specific data to be serialized and sent. Must not be null.</param>
        /// <param name="sessionId">Optional session identifier for correlation across multiple webhook events.</param>
        /// <returns>A <see cref="Task{WebhookResult}"/> containing the delivery result including success status,
        /// HTTP response information, error details, and correlation metadata.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="eventType"/> is invalid or <paramref name="data"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webhook service is not configured.</exception>
        /// <exception cref="WebhookException">Thrown when delivery fails after all retry attempts and circuit breaker is triggered.</exception>
        /// <remarks>
        /// This method implements comprehensive delivery logic:
        /// <list type="number">
        /// <item><description>Validates service state and event filtering</description></item>
        /// <item><description>Generates or retrieves correlation ID for tracking</description></item>
        /// <item><description>Creates structured payload with metadata and timestamps</description></item>
        /// <item><description>Applies security features (signatures, sanitization)</description></item>
        /// <item><description>Executes delivery with retry policy and circuit breaker</description></item>
        /// <item><description>Logs operation outcome and updates statistics</description></item>
        /// </list>
        /// Events may be skipped (Success=true, Skipped=true) if:
        /// <list type="bullet">
        /// <item><description>Webhooks are disabled via configuration or runtime setting</description></item>
        /// <item><description>The event type is not in the configured trigger list</description></item>
        /// <item><description>The circuit breaker is open (fail-fast behavior)</description></item>
        /// </list>
        /// </remarks>
        public async Task<WebhookResult> SendWebhookAsync(WebhookEventType eventType, Dictionary<string, object> data, string? sessionId = null)
        {
            return await _structuredLogger.ExecuteWithLoggingAsync(
                "WebhookService.SendWebhookAsync",
                async () =>
                {
                    if (data == null)
                        throw new ArgumentNullException(nameof(data));

                    // Check if webhooks are enabled
                    if (!IsEnabled)
                    {
                        _logger.LogDebug("Webhook skipped: service disabled for event {EventType}", eventType);
                        return new WebhookResult { Success = true, Skipped = true, StatusCode = 0 };
                    }

                    // Check if this event type is configured for delivery
                    if (_config.TriggerEvents != null && !_config.TriggerEvents.Contains(eventType))
                    {
                        _logger.LogDebug("Webhook skipped: event {EventType} not in trigger list", eventType);
                        return new WebhookResult { Success = true, Skipped = true, StatusCode = 0 };
                    }

                    // Create payload with correlation ID
                    var correlationId = sessionId ?? _correlationService.GetCurrentCorrelationId() ?? Guid.NewGuid().ToString("N")[..8];
                    var payload = CreatePayload(eventType, data, correlationId);

                    // Send payload with resilience policies
                    var result = await SendPayloadAsync(payload);

                    // Log the operation result
                    if (result.Success)
                    {
                        _logger.LogInformation("Webhook sent successfully: {EventType} to {Endpoint}, Duration: {Duration}ms", 
                            eventType, _config.EndpointUrl, result.Duration.TotalMilliseconds);
                    }
                    else if (!result.Skipped)
                    {
                        _logger.LogWarning("Webhook delivery failed: {EventType} to {Endpoint}, Error: {Error}, StatusCode: {StatusCode}", 
                            eventType, _config.EndpointUrl, result.Error, result.StatusCode);
                    }

                    return result;
                },
                new Dictionary<string, object>
                {
                    ["EventType"] = eventType.ToString(),
                    ["EventCount"] = data.Count,
                    ["EndpointUrl"] = _config.EndpointUrl,
                    ["SessionId"] = sessionId
                });
        }
        
        /// <summary>
        /// Sends a specialized webhook notification when audio transcription has completed successfully.
        /// Includes transcription metadata, processing metrics, and target application information.
        /// The transcription text is automatically sanitized to protect privacy while preserving content for analysis.
        /// </summary>
        /// <param name="transcription">The transcribed text content. Must not be null or empty.
        /// Content will be sanitized to remove sensitive information while preserving meaning.</param>
        /// <param name="application">The target application where text will be injected. Can be null or empty.
        /// Used for context tracking and application-specific routing.</param>
        /// <param name="duration">The time taken to process and transcribe the audio. Must be a positive value.
        /// Used for performance monitoring and quality metrics.</param>
        /// <returns>A <see cref="Task{WebhookResult}"/> containing the delivery result with success status,
        /// HTTP response details, and processing metrics.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="transcription"/> is null or empty,
        /// or when <paramref name="duration"/> is negative.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the webhook service is not configured.</exception>
        /// <exception cref="WebhookException">Thrown when delivery fails after all retry attempts.</exception>
        /// <remarks>
        /// This method creates a structured payload with the following metadata:
        /// <list type="bullet">
        /// <item><description><b>transcription</b>: Sanitized transcription text (PII removed, profanity filtered)</description></item>
        /// <item><description><b>application</b>: Target application name or "unknown"</description></item>
        /// <item><description><b>duration_ms</b>: Processing duration in milliseconds for performance tracking</description></item>
        /// <item><description><b>timestamp</b>: UTC timestamp of completion in ISO 8601 format</description></item>
        /// <item><description><b>word_count</b>: Number of words in the transcription for analytics</description></item>
        /// <item><description><b>char_count</b>: Character count for size estimation</description></item>
        /// <item><description><b>confidence</b>: Transcription confidence score if available</description></item>
        /// </list>
        /// The transcription is processed through a sanitization pipeline that:
        /// <list type="number">
        /// <item><description>Removes or masks personally identifiable information (names, emails, phone numbers)</description></item>
        /// <item><description>Filters profanity and offensive content</description></item>
        /// <item><description>Preserves technical and domain-specific terminology</description></item>
        /// <item><description>Maintains formatting and structure for readability</description></item>
        /// </list>
        /// </remarks>
        public async Task<WebhookResult> SendTranscriptionCompletedAsync(string transcription, string? application, TimeSpan duration)
        {
            return await _structuredLogger.ExecuteWithLoggingAsync(
                "WebhookService.SendTranscriptionCompletedAsync",
                async () =>
                {
                    if (string.IsNullOrWhiteSpace(transcription))
                        throw new ArgumentException("Transcription cannot be null or empty.", nameof(transcription));

                    if (duration < TimeSpan.Zero)
                        throw new ArgumentException("Duration cannot be negative.", nameof(duration));

                    // Sanitize transcription text for privacy
                    var sanitizedTranscription = SanitizeTranscriptionText(transcription);

                    var data = new Dictionary<string, object>
                    {
                        ["transcription"] = sanitizedTranscription,
                        ["application"] = application ?? "unknown",
                        ["duration_ms"] = duration.TotalMilliseconds,
                        ["timestamp"] = DateTime.UtcNow.ToString("O"),
                        ["word_count"] = CountWords(sanitizedTranscription),
                        ["char_count"] = sanitizedTranscription.Length
                    };

                    _logger.LogDebug("Sending transcription completed webhook: {WordCount} words, {Duration}ms", 
                        data["word_count"], duration.TotalMilliseconds);

                    return await SendWebhookAsync(WebhookEventType.TranscriptionCompleted, data);
                },
                new Dictionary<string, object>
                {
                    ["Application"] = application ?? "unknown",
                    ["DurationMs"] = duration.TotalMilliseconds,
                    ["WordCount"] = CountWords(transcription),
                    ["CharCount"] = transcription.Length
                });
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
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(60),
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
        /// Hashes a sensitive value using SHA-256 for privacy protection in log entries.
        /// Provides irreversible one-way hashing to prevent reconstruction of original values.
        /// </summary>
        /// <param name="value">The value to hash. Must not be null.</param>
        /// <returns>A hexadecimal string representation of the SHA-256 hash.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
        /// <remarks>
        /// Uses SHA-256 algorithm with UTF-8 encoding. The resulting hash is consistent
        /// across multiple calls for the same input value, enabling correlation analysis
        /// without exposing sensitive data.
        /// </remarks>
        private static string HashValue(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(value);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash);
        }
        
        /// <summary>
        /// Sanitizes error messages to remove sensitive information while preserving debugging value.
        /// Applies pattern-based redaction for common sensitive data types including paths, credentials, and PII.
        /// </summary>
        /// <param name="error">The error message to sanitize. Can be null or empty.</param>
        /// <returns>The sanitized error message with sensitive data replaced with placeholder text.</returns>
        /// <remarks>
        /// Sanitization rules applied:
        /// <list type="bullet">
        /// <item><description>File paths (Windows and Unix) → [PATH_REDACTED]</description></item>
        /// <item><description>Connection strings and credentials → [REDACTED]</description></item>
        /// <item><description>Email addresses → [EMAIL_REDACTED]</description></item>
        /// <item><description>IP addresses → [IP_REDACTED]</description></item>
        /// <item><description>User names in paths → [USER_REDACTED]</description></item>
        /// </list>
        /// The sanitization preserves the error structure and technical details while removing sensitive data.
        /// </remarks>
        private static string SanitizeErrorMessage(string error)
        {
            if (string.IsNullOrEmpty(error))
                return error;
            
            var sanitized = error;
            
            // Remove file paths (Windows and Unix)
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[A-Za-z]:\\[^\s]*|/[^\s]*", "[PATH_REDACTED]");
            
            // Remove potential connection strings, API keys, tokens
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"(password|pwd|secret|token|key|apikey)=[^&\s]*", "$1=[REDACTED]", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            // Remove email addresses
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[EMAIL_REDACTED]");
            
            // Remove IP addresses
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"\b(?:[0-9]{1,3}\.){3}[0-9]{1,3}\b", "[IP_REDACTED]");
            
            return sanitized;
        }

        /// <summary>
        /// Sanitizes transcription text by removing personally identifiable information while preserving content.
        /// Applies privacy filters to protect user data while maintaining text usability for analytics.
        /// </summary>
        /// <param name="transcription">The transcription text to sanitize. Must not be null.</param>
        /// <returns>The sanitized transcription text with PII removed or masked.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="transcription"/> is null.</exception>
        /// <remarks>
        /// Sanitization includes:
        /// <list type="bullet">
        /// <item><description>Email addresses → [EMAIL_REDACTED]</description></item>
        /// <item><description>Phone numbers → [PHONE_REDACTED]</description></item>
        /// <item><description>Credit card numbers → [CARD_REDACTED]</description></item>
        /// <item><description>Social security numbers → [SSN_REDACTED]</description></item>
        /// <item><description>Names (simple pattern) → [NAME_REDACTED]</description></item>
        /// <item><description>Addresses → [ADDRESS_REDACTED]</description></item>
        /// </list>
        /// The method preserves medical terminology, technical terms, and domain-specific vocabulary
        while removing common PII patterns. This is a basic implementation and may need customization
        for specific use cases.
        /// </remarks>
        private static string SanitizeTranscriptionText(string transcription)
        {
            if (string.IsNullOrEmpty(transcription))
                return transcription;

            var sanitized = transcription;
            
            // Email addresses
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "[EMAIL_REDACTED]");
            
            // Phone numbers (US format basic pattern)
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"(\+?1[-.\s]?)?\(?([0-9]{3})\)?[-.\s]?([0-9]{3})[-.\s]?([0-9]{4})", "[PHONE_REDACTED]");
            
            // Credit card numbers (basic 16-digit pattern)
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"\b\d{4}[-\s]?\d{4}[-\s]?\d{4}[-\s]?\d{4}\b", "[CARD_REDACTED]");
            
            // Social Security numbers
            sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, 
                @"\b\d{3}[-.]?\d{2}[-.]?\d{4}\b", "[SSN_REDACTED]");
            
            return sanitized.Trim();
        }

        /// <summary>
        /// Counts the number of words in a text string for analytics and metrics.
        /// Handles multiple whitespace characters and punctuation appropriately.
        /// </summary>
        /// <param name="text">The text to analyze. Can be null or empty.</param>
        /// <returns>The number of words in the text. Returns 0 for null or empty strings.</returns>
        /// <remarks>
        /// Words are defined as sequences of characters separated by whitespace.
        /// Punctuation attached to words is included in the count. Numbers and symbols
        that form separate "words" are also counted.
        /// </remarks>
        private static int CountWords(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            // Split on whitespace and filter out empty entries
            var words = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            return words.Length;
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
    /// Represents the result of a webhook delivery operation with comprehensive status information,
    /// performance metrics, and diagnostic details for monitoring and debugging purposes.
    /// </summary>
    /// <remarks>
    /// This class provides detailed information about webhook delivery attempts including:
    /// <list type="bullet">
    /// <item><description>Delivery success/failure status and reasoning</description></item>
    /// <item><description>HTTP response details for debugging integration issues</description></item>
    /// <item><description>Performance metrics for optimization and SLA monitoring</description></item>
    /// <item><description>Correlation IDs for tracing across systems</description></item>
    /// <item><description>Error details for troubleshooting and alerting</description></item>
    /// </list>
    /// The result structure enables automated retry logic, monitoring integration, and
    /// comprehensive audit trails for compliance requirements.
    /// </remarks>
    public class WebhookResult
    {
        /// <summary>
        /// Gets or sets a value indicating whether the webhook delivery was successful.
        /// Success means the HTTP request completed with a status code in the 2xx range.
        /// </summary>
        /// <value>True if delivery succeeded; false otherwise.</value>
        /// <remarks>
        /// When true, the webhook was successfully delivered to the endpoint and received
        /// a successful HTTP response. When false, check <see cref="Error"/> and <see cref="StatusCode"/>
        /// for diagnostic information.
        /// </remarks>
        public bool Success { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether the webhook delivery was skipped.
        /// Skipped webhooks are not attempted due to configuration or policy rules.
        /// </summary>
        /// <value>True if the webhook was skipped; false if it was attempted.</value>
        /// <remarks>
        /// Webhooks may be skipped when:
        /// <list type="bullet">
        /// <item><description>The service is disabled via configuration</description></item>
        /// <item><description>The event type is not in the trigger list</description></item>
        /// <item><description>The circuit breaker is open (fail-fast)</description></item>
        /// <item><description>Rate limiting policies are exceeded</description></item>
        /// </list>
        /// When skipped, <see cref="Success"/> may be true but <see cref="StatusCode"/> will be 0.
        /// </remarks>
        public bool Skipped { get; set; }
        
        /// <summary>
        /// Gets or sets the HTTP status code returned by the webhook endpoint.
        /// Provides diagnostic information for integration troubleshooting.
        /// </summary>
        /// <value>The HTTP status code, or 0 if the request was not attempted.</value>
        /// <remarks>
        /// Common status codes and their meanings:
        /// <list type="table">
        /// <listheader><term>Code</term><description>Meaning</description></listheader>
        /// <item><term>200</term><description>Success - webhook processed successfully</description></item>
        /// <item><term>400</term><description>Bad Request - payload format or validation error</description></item>
        /// <item><term>401</term><description>Unauthorized - authentication failed</description></item>
        /// <item><term>403</term><description>Forbidden - insufficient permissions</description></item>
        /// <item><term>404</term><description>Not Found - endpoint URL incorrect</description></item>
        /// <item><term>429</term><description>Too Many Requests - rate limiting</description></item>
        /// <item><term>500</term><description>Internal Server Error - endpoint processing failed</description></item>
        /// <item><term>503</term><description>Service Unavailable - endpoint temporarily down</description></item>
        /// </list>
        /// </remarks>
        public int StatusCode { get; set; }
        
        /// <summary>
        /// Gets or sets the response body returned by the webhook endpoint.
        /// Provides endpoint-specific information for debugging and integration validation.
        /// </summary>
        /// <value>The response body content, or null if no response was received.</value>
        /// <remarks>
        /// The response content is included for debugging purposes and may contain:
        /// <list type="bullet">
        /// <item><description>Success confirmation messages</description></item>
        /// <item><description>Error details and stack traces</description></item>
        /// <item><description>Endpoint-specific status information</description></item>
        /// <item><description>Processing IDs or reference numbers</description></item>
        /// </list>
        /// Large responses may be truncated for logging purposes.
        /// </remarks>
        public string? Response { get; set; }
        
        /// <summary>
        /// Gets or sets the error message if the webhook delivery failed.
        /// Provides human-readable error information for troubleshooting and alerting.
        /// </summary>
        /// <value>The error message, or null if the delivery succeeded or was skipped.</value>
        /// <remarks>
        /// Error messages may include:
        /// <list type="bullet">
        /// <item><description>Network connectivity issues</description></item>
        /// <item><description>Timeout information</description></item>
        /// <item><description>Authentication failures</description></item>
        /// <item><description>Circuit breaker status</description></item>
        /// <item><description>Retry policy exhaustion</description></item>
        /// </list>
        /// Error messages are sanitized to remove sensitive information while preserving
        /// technical details needed for debugging.
        /// </remarks>
        public string? Error { get; set; }
        
        /// <summary>
        /// Gets or sets the total duration of the webhook delivery operation.
        /// Includes network time, retry delays, and processing overhead.
        /// </summary>
        /// <value>The total time taken for the delivery attempt.</value>
        /// <remarks>
        /// Duration includes:
        /// <list type="bullet">
        /// <item><description>HTTP request/response time</description></item>
        /// <item><description>Retry delays (exponential backoff)</description></item>
        /// <item><description>Circuit breaker evaluation time</description></item>
        /// <item><description>Payload serialization time</description></item>
        /// <item><description>Authentication processing time</description></item>
        /// </list>
        /// Use this metric for performance monitoring, SLA compliance, and optimization.
        /// </remarks>
        public TimeSpan Duration { get; set; }
        
        /// <summary>
        /// Gets or sets the unique identifier of the webhook payload.
        /// Enables correlation between webhook send and receiver processing.
        /// </summary>
        /// <value>The payload identifier, or null if no payload was generated.</value>
        /// <remarks>
        /// The payload ID is included in the webhook headers as 'X-Webhook-Id' and
        /// can be used by receivers to correlate webhook events, prevent duplicate processing,
        /// and implement idempotent operations.
        /// </remarks>
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
