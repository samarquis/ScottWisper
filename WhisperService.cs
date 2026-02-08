using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using WhisperKey.Exceptions;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using WhisperKey.Services;
using WhisperKey.Services.Validation;
using WhisperKey.Configuration;
using WhisperKey.Services.Recovery;

namespace WhisperKey
{
    public class WhisperService : IWhisperService
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private string _baseUrl;
        private readonly ISettingsService? _settingsService;
        private readonly ICredentialService? _credentialService;
        private readonly IApiKeyManagementService? _apiKeyManagement;
        private readonly IAudioValidationProvider? _audioValidator;
        private readonly IRecoveryPolicyService? _recoveryPolicyService;
        private readonly IPerformanceMonitoringService? _performanceMonitoring;
        private readonly LocalInferenceService? _localInference;
        private readonly IConfiguration? _configuration;
        private readonly ILogger<WhisperService>? _logger;
        private readonly bool _ownsHttpClient;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
        private readonly IRateLimitingService? _rateLimiting;
        
        // Configuration key for API endpoint
        private const string ApiEndpointConfigKey = "Transcription:ApiEndpoint";
        
        // Default API endpoint value (loaded from configuration)
        private const string DefaultApiEndpointValue = "https://api.openai.com/v1/audio/transcriptions";
        
        // API usage tracking
        private int _requestCount = 0;
        private decimal _estimatedCost = 0.0m;
        
        // Whisper API pricing (as of 2024)
        private const decimal CostPerMinute = 0.006m; // $0.006 per minute
        
        // Circuit breaker configuration
        private const int CircuitBreakerThreshold = 5; // Open after 5 failures
        private const int CircuitBreakerDurationSeconds = 60; // Stay open for 1 minute
        
        public event EventHandler? TranscriptionStarted;
        public event EventHandler<int>? TranscriptionProgress;
        public event EventHandler<string>? TranscriptionCompleted;
        public event EventHandler<Exception>? TranscriptionError;
        public event EventHandler<UsageStats>? UsageUpdated;
        
        // DEPRECATED: This constructor creates HttpClient directly and should not be used
        // Use IHttpClientFactory-based constructor instead to prevent socket exhaustion
        [Obsolete("Use constructor with IHttpClientFactory to prevent socket exhaustion")]
        public WhisperService()
        {
            _apiKey = GetApiKey();
            _baseUrl = DefaultApiEndpointValue;
            var handler = new HttpClientHandler
            {
                // SEC-004: Implement server certificate validation
                ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                {
                    if (errors == System.Net.Security.SslPolicyErrors.None) return true;
                    _logger?.LogError("SSL Certificate error: {Errors}", errors);
                    return false;
                }
            };
            _httpClient = new HttpClient(handler);
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _ownsHttpClient = true;
            
            // Fallback initialization if no service provider
            _retryPolicy = CreateRetryPolicy();
            _circuitBreakerPolicy = CreateCircuitBreakerPolicy();
            
            _logger?.LogWarning("WhisperService created with deprecated constructor - use IHttpClientFactory to prevent socket exhaustion");
        }
        
        public WhisperService(ISettingsService settingsService, LocalInferenceService? localInference = null)
            : this(settingsService, null, null, null, null, localInference)
        {
        }
        
        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, LocalInferenceService? localInference = null)
            : this(settingsService, httpClientFactory, null, null, null, localInference)
        {
        }
        
        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, ICredentialService? credentialService, LocalInferenceService? localInference = null)
            : this(settingsService, httpClientFactory, credentialService, null, null, localInference, null)
        {
        }

        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, ICredentialService? credentialService, IApiKeyManagementService? apiKeyManagement, LocalInferenceService? localInference = null)
            : this(settingsService, httpClientFactory, credentialService, apiKeyManagement, null, localInference, null)
        {
        }

        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, ICredentialService? credentialService, IApiKeyManagementService? apiKeyManagement, IRecoveryPolicyService? recoveryPolicy, LocalInferenceService? localInference = null)
            : this(settingsService, httpClientFactory, credentialService, apiKeyManagement, recoveryPolicy, localInference, null)
        {
        }
        
        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, ICredentialService? credentialService, IApiKeyManagementService? apiKeyManagement, IRecoveryPolicyService? recoveryPolicy, LocalInferenceService? localInference, IConfiguration? configuration, IAudioValidationProvider? audioValidator = null, IPerformanceMonitoringService? performanceMonitoring = null, IRateLimitingService? rateLimiting = null)
        {
            _settingsService = settingsService;
            _credentialService = credentialService;
            _apiKeyManagement = apiKeyManagement;
            _recoveryPolicyService = recoveryPolicy;
            _localInference = localInference;
            _configuration = configuration;
            _audioValidator = audioValidator;
            _performanceMonitoring = performanceMonitoring;
            _rateLimiting = rateLimiting;
            _apiKey = GetApiKey(); // Sync version for constructor (env var only)
            
            // Load API endpoint from configuration immediately
            _baseUrl = GetApiEndpointFromConfiguration();
            
            // Use IRateLimitingService instead of local limiter if available
            if (_rateLimiting == null && _settingsService?.Settings.Transcription.EnableRateLimiting == true)
            {
                var maxRequests = _settingsService.Settings.Transcription.MaxRequestsPerMinute;
                // Fallback to local limiter for backward compatibility if service not provided
                // But preferred is using the centralized service
            }
            
            // Use IHttpClientFactory if available to prevent socket exhaustion
            if (httpClientFactory != null)
            {
                _httpClient = httpClientFactory.CreateClient("WhisperApi");
                _ownsHttpClient = false;
            }
            else
            {
                _logger?.LogError("WhisperService created without IHttpClientFactory - this will cause socket exhaustion under load");
                var handler = new HttpClientHandler
                {
                    // SEC-004: Implement server certificate validation
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) =>
                    {
                        if (errors == System.Net.Security.SslPolicyErrors.None) return true;
                        _logger?.LogError("SSL Certificate error: {Errors}", errors);
                        return false;
                    }
                };
                _httpClient = new HttpClient(handler);
                _ownsHttpClient = true;
            }
            
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            
            // Subscribe to settings changes
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged += OnSettingsChanged;
            }
            
            // Async initialization for API key and other settings
            _ = InitializeAsync();
            
            // Initialize policies for API calls
            if (_recoveryPolicyService != null)
            {
                // Use recovery service for policies
                _retryPolicy = _recoveryPolicyService.GetApiRetryPolicy<HttpResponseMessage>(5);
                _circuitBreakerPolicy = _recoveryPolicyService.GetCircuitBreakerPolicy(CircuitBreakerThreshold, CircuitBreakerDurationSeconds);
            }
            else
            {
                _retryPolicy = CreateRetryPolicy();
                _circuitBreakerPolicy = CreateCircuitBreakerPolicy();
            }
        }
        
        private async Task InitializeAsync()
        {
            try
            {
                // Load API key asynchronously (may read from file)
                var apiKey = await GetApiKeyFromSettingsAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(apiKey) && apiKey != _apiKey)
                {
                    _apiKey = apiKey;
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                }
                
                // Load endpoint asynchronously from settings (may override configuration value)
                var endpoint = await GetApiEndpointFromSettingsAsync().ConfigureAwait(false);
                if (!string.IsNullOrEmpty(endpoint) && endpoint != _baseUrl)
                {
                    // Validate endpoint URL format before using
                    if (ValidateApiEndpoint(endpoint))
                    {
                        _baseUrl = endpoint;
                    }
                    else
                    {
                        _logger?.LogWarning("Invalid API endpoint URL format: {Endpoint}. Using configuration value.", endpoint);
                    }
                }
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                _logger?.LogError(ex, "Error during async initialization");
            }
        }
        
        public async Task<string> TranscribeAudioAsync(byte[] audioData, string? language = null)
        {
            using var activity = _performanceMonitoring?.StartActivity("WhisperService.TranscribeAudio");
            try
            {
                // Check rate limiting if enabled
                if (_rateLimiting != null)
                {
                    if (!_rateLimiting.TryConsume("Transcription"))
                    {
                        var waitTime = _rateLimiting.GetWaitTime("Transcription");
                        var message = $"Rate limit exceeded for transcription. Please try again in {waitTime.TotalSeconds:F1} seconds.";
                        _logger?.LogWarning("Rate limit exceeded for transcription request.");
                        throw new HttpRequestException(message, null, HttpStatusCode.TooManyRequests);
                    }
                }
                
                // Validate audio data before processing using the specialized provider
                if (_audioValidator != null)
                {
                    var validationResult = _audioValidator.ValidateAudioData(audioData);
                    if (!validationResult.IsValid)
                    {
                        var errorMessage = $"Audio validation failed: {string.Join(", ", validationResult.Errors)}";
                        _logger?.LogWarning(errorMessage);
                        throw new WhisperKey.Exceptions.TranscriptionException(errorMessage, "INVALID_AUDIO");
                    }
                }
                
                // Notify transcription started
                TranscriptionStarted?.Invoke(this, EventArgs.Empty);
                
                _performanceMonitoring?.RecordMetric("transcription.audio_length_bytes", audioData.Length, "bytes");
                
                // Check if we should use local inference
                if (_settingsService?.Settings.Transcription.Mode == TranscriptionMode.Local && _localInference != null)
                {
                    try
                    {
                        TranscriptionProgress?.Invoke(this, 10);
                        var result = await _localInference.TranscribeAudioAsync(audioData, language).ConfigureAwait(false);
                        TranscriptionProgress?.Invoke(this, 100);
                        TranscriptionCompleted?.Invoke(this, result.Text);
                        return result.Text;
                    }
                    catch (InvalidOperationException ex)
                    {
                        if (_settingsService.Settings.Transcription.AutoFallbackToCloud)
                        {
                            // Log warning and fallback
                            _logger?.LogWarning(ex, "Local transcription failed, falling back to cloud");
                        }
                        else
                        {
                            throw;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        if (_settingsService.Settings.Transcription.AutoFallbackToCloud)
                        {
                            // Log warning and fallback
                            _logger?.LogWarning(ex, "Local transcription failed due to network error, falling back to cloud");
                        }
                        else
                        {
                            throw;
                        }
                    }
                    catch (IOException ex)
                    {
                        if (_settingsService.Settings.Transcription.AutoFallbackToCloud)
                        {
                            // Log warning and fallback
                            _logger?.LogWarning(ex, "Local transcription failed due to IO error, falling back to cloud");
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                
                // Cloud transcription (OpenAI) with retry logic
                // Report some progress before making the request
                TranscriptionProgress?.Invoke(this, 25);
                
                                                // Execute API request with retry policy, wrapped in circuit breaker
                
                                                HttpResponseMessage response = await _circuitBreakerPolicy.ExecuteAsync(async () =>
                
                                                {
                
                                                    return await _retryPolicy.ExecuteAsync(async () =>
                
                                                    {
                
                                                    // Create multipart form content (must be recreated for each retry)
                
                                                    var content = new MultipartFormDataContent();
                
                                                    
                
                                                    // Add audio file
                
                                                    var audioContent = new ByteArrayContent(audioData);
                
                                                    audioContent.Headers.ContentType = MediaTypeHeaderValue.Parse("audio/wav");
                
                                                    content.Add(audioContent, "file", "audio.wav");
                
                                                    
                
                                                    // Add model parameter
                
                                                    content.Add(new StringContent(_settingsService?.Settings.Transcription.Model ?? "whisper-1"), "model");
                
                                                    
                
                                                    // Add language parameter if specified
                
                                                    if (!string.IsNullOrEmpty(language))
                
                                                    {
                
                                                        content.Add(new StringContent(language), "language");
                
                                                    }
                
                                                    
                
                                                    // Add response format
                
                                                    content.Add(new StringContent("json"), "response_format");
                
                                                    
                
                                                    // Add temperature for consistent results
                
                                                    content.Add(new StringContent("0.0"), "temperature");
                
                                                    
                
                                                    var result = await _httpClient.PostAsync(_baseUrl, content).ConfigureAwait(false);
                
                                                    
                
                                                    // Dispose content after request
                
                                                    content.Dispose();
                
                                                    
                
                                                                        // Check for success - retry policy will handle transient failures
                
                                                    
                
                                                                        if (!result.IsSuccessStatusCode && 
                
                                                    
                
                                                                            result.StatusCode != HttpStatusCode.TooManyRequests && 
                
                                                    
                
                                                                            result.StatusCode != HttpStatusCode.ServiceUnavailable && 
                
                                                    
                
                                                                            result.StatusCode != HttpStatusCode.BadGateway && 
                
                                                    
                
                                                                            result.StatusCode != HttpStatusCode.GatewayTimeout)
                
                                                    
                
                                                                        {
                
                                                    
                
                                                                            var errorContent = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                
                                                    
                
                                                                            throw new HttpRequestException($"Whisper API request failed: {(int)result.StatusCode} ({result.StatusCode}) - {errorContent}");
                
                                                    
                
                                                                        }
                
                                                    
                
                                                    
                
                                                    
                
                                                    return result;
                
                                                }).ConfigureAwait(false);
                
                                                }).ConfigureAwait(false);
                
                                
                                // Ensure successful response
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new HttpRequestException($"Whisper API request failed after retries: {response.StatusCode} - {errorContent}", null, response.StatusCode);
                }
                
                // Report progress after getting response
                TranscriptionProgress?.Invoke(this, 75);
                
                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var transcriptionResponse = JsonConvert.DeserializeObject<WhisperResponse>(responseContent);
                
                if (transcriptionResponse?.Text == null)
                {
                    throw new TranscriptionModelException("Whisper API", "Invalid response from Whisper API");
                }
                
                // Update usage statistics
                UpdateUsageStats(audioData.Length);
                
                // Record usage in management service
                if (_apiKeyManagement != null)
                {
                    _ = _apiKeyManagement.RecordUsageAsync("OpenAI", success: true);
                }

                // Notify subscribers
                TranscriptionCompleted?.Invoke(this, transcriptionResponse.Text);
                
                return transcriptionResponse.Text;
            }
            catch (BrokenCircuitException ex)
            {
                // Wrap BrokenCircuitException in HttpRequestException to match test expectations (SEC-007)
                var message = "Whisper API is temporarily unavailable due to repeated failures. " +
                    $"Circuit breaker is OPEN. Please try again in {CircuitBreakerDurationSeconds} seconds.";
                _logger?.LogWarning(ex, "Circuit breaker is open");
                throw new HttpRequestException(message, ex, HttpStatusCode.ServiceUnavailable);
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                if (_apiKeyManagement != null)
                {
                    _ = _apiKeyManagement.RecordUsageAsync("OpenAI", success: false, errorMessage: ex.Message);
                }
                TranscriptionError?.Invoke(this, ex);
                throw;
            }
        }
                        public async Task<string> TranscribeAudioFileAsync(string filePath, string? language = null)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException($"Audio file not found: {filePath}");
                }
                
                if (_audioValidator != null)
                {
                    var validationResult = await _audioValidator.ValidateAudioFileAsync(filePath);
                    if (!validationResult.IsValid)
                    {
                        throw new SecurityException($"Audio file validation failed: {string.Join(", ", validationResult.Errors)}");
                    }
                }
                
                var audioData = await File.ReadAllBytesAsync(filePath).ConfigureAwait(false);
                return await TranscribeAudioAsync(audioData, language).ConfigureAwait(false);
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                TranscriptionError?.Invoke(this, ex);
                throw;
            }
        }
        
        public UsageStats GetUsageStats()
        {
            return new UsageStats
            {
                RequestCount = _requestCount,
                EstimatedCost = _estimatedCost,
                EstimatedMinutes = (double)(_estimatedCost / CostPerMinute)
            };
        }
        
        public void ResetUsageStats()
        {
            _requestCount = 0;
            _estimatedCost = 0.0m;
            UsageUpdated?.Invoke(this, GetUsageStats());
        }
        
        private string GetApiKey()
        {
            // Environment variables are no longer supported for secrets (IA-5 compliance)
            // Secrets must be retrieved through the Credential Service or Settings Service
            return string.Empty;
        }

        private async Task<string> GetApiKeyAsync()
        {
            // Try credential service first (Windows Credential Manager)
            if (_credentialService != null)
            {
                var credentialKey = await _credentialService.RetrieveCredentialAsync("OpenAI_ApiKey").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(credentialKey))
                {
                    return credentialKey;
                }
            }
            
            return string.Empty;
        }

        private async Task<string> GetApiKeyFromSettingsAsync()
        {
            // Try management service first
            if (_apiKeyManagement != null)
            {
                try
                {
                    var managedKey = await _apiKeyManagement.GetActiveKeyAsync("OpenAI").ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(managedKey))
                    {
                        return managedKey;
                    }
                }
                catch (Exception ex) when (!IsFatalException(ex))
                {
                    // Fall back
                }
            }

            // Try credential service next (Windows Credential Manager)
            if (_settingsService != null)
            {
                try
                {
                    return await _settingsService.GetEncryptedValueAsync("OpenAI_ApiKey").ConfigureAwait(false);
                }
                catch (InvalidOperationException)
                {
                    // Fall back to default method
                }
                catch (Exception ex) when (!IsFatalException(ex))
                {
                    // Fall back to default method
                }
            }
            
            return await GetApiKeyAsync().ConfigureAwait(false);
        }

        /// <summary>
        /// Loads the API endpoint from IConfiguration (appsettings.json).
        /// Validates the URL format on startup and falls back to default if invalid.
        /// </summary>
        private string GetApiEndpointFromConfiguration()
        {
            try
            {
                // Try to get from IConfiguration first
                if (_configuration != null)
                {
                    var endpoint = _configuration[ApiEndpointConfigKey];
                    if (!string.IsNullOrEmpty(endpoint))
                    {
                        // Validate URL format before using
                        if (ValidateApiEndpoint(endpoint))
                        {
                            return endpoint;
                        }
                        else
                        {
                            _logger?.LogWarning("Invalid API endpoint URL in configuration: {Endpoint}. Using default.", endpoint);
                        }
                    }
                }
                
                // Fall back to default value
                return DefaultApiEndpointValue;
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                _logger?.LogError(ex, "Error loading API endpoint from configuration. Using default.");
                return DefaultApiEndpointValue;
            }
        }

        private async Task<string> GetApiEndpointFromSettingsAsync()
        {
            if (_settingsService != null)
            {
                try
                {
                    // Try to get from encrypted storage first (SEC-006)
                    var encryptedEndpoint = await _settingsService.GetEncryptedValueAsync("Transcription_ApiEndpoint").ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(encryptedEndpoint))
                    {
                        return encryptedEndpoint;
                    }

                    // Fallback to legacy plaintext setting for backward compatibility
                    var endpoint = _settingsService.Settings.Transcription.ApiEndpoint;
                    if (!string.IsNullOrEmpty(endpoint))
                    {
                        // Migration path: save to encrypted storage if found in plaintext
                        await _settingsService.SetEncryptedValueAsync("Transcription_ApiEndpoint", endpoint).ConfigureAwait(false);
                        return endpoint;
                    }
                }
                catch (Exception ex) when (!IsFatalException(ex))
                {
                    _logger?.LogWarning(ex, "Error reading API endpoint from settings");
                }
            }
            
            // Return empty string - configuration value is the source of truth
            return string.Empty;
        }

        private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            try
            {
                await HandleSettingsChangedAsync(e).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling settings change");
                TranscriptionError?.Invoke(this, ex);
            }
        }
        
        private async Task HandleSettingsChangedAsync(SettingsChangedEventArgs e)
        {
            // Handle transcription settings changes
            if (e.Category == "Transcription")
            {
                // Update API key if it changed
                if (e.Key.Contains("ApiKey") || e.Key == "ApplyAll" || e.Key == "ReloadSettings")
                {
                    var newApiKey = await GetApiKeyFromSettingsAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(newApiKey) && newApiKey != _apiKey)
                    {
                        _apiKey = newApiKey;
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                    }
                }
                
                // Update API endpoint if it changed
                if (e.Key.Contains("ApiEndpoint") || e.Key == "ApplyAll" || e.Key == "ReloadSettings")
                {
                    var newEndpoint = await GetApiEndpointFromSettingsAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(newEndpoint) && newEndpoint != _baseUrl)
                    {
                        _baseUrl = newEndpoint;
                    }
                }
            }
        }
        
        private void UpdateUsageStats(int audioDataLength)
        {
            // Estimate audio duration (16kHz, 16-bit, mono = 32,000 bytes per second)
            var estimatedDurationSeconds = (double)audioDataLength / 32000;
            var estimatedDurationMinutes = estimatedDurationSeconds / 60.0;
            
            _requestCount++;
            _estimatedCost += (decimal)estimatedDurationMinutes * CostPerMinute;
            
            UsageUpdated?.Invoke(this, GetUsageStats());
        }
        
        /// <summary>
        /// Validates the API endpoint URL format.
        /// Ensures the URL is a valid absolute HTTPS URL.
        /// </summary>
        private static bool ValidateApiEndpoint(string endpoint)
        {
            if (string.IsNullOrWhiteSpace(endpoint))
            {
                return false;
            }
            
            if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            {
                return false;
            }
            
            // Require HTTPS for security
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                return false;
            }
            
            return true;
        }
        
        public void Dispose()
        {
            // Unsubscribe from settings changes to prevent memory leak
            if (_settingsService != null)
            {
                _settingsService.SettingsChanged -= OnSettingsChanged;
            }
            
            // Only dispose HttpClient if we created it (not from factory)
            if (_ownsHttpClient)
            {
                _httpClient?.Dispose();
            }
            _localInference?.Dispose();
        }
        
        /// <summary>
        /// Determines if an exception is fatal and should not be caught.
        /// </summary>
        private static bool IsFatalException(Exception ex)
        {
            return ex is OutOfMemoryException ||
                   ex is StackOverflowException ||
                   ex is AccessViolationException;
        }
        
        /// <summary>
        /// Creates a retry policy for OpenAI API calls with exponential backoff.
        /// Handles transient failures like network blips, rate limits, and temporary service unavailability.
        /// </summary>
        private static AsyncRetryPolicy<HttpResponseMessage> CreateRetryPolicy()
        {
            return Policy
                .Handle<HttpRequestException>(ex => 
                    ex.StatusCode == null || // Transient network errors often don't have a status code
                    ex.StatusCode == HttpStatusCode.TooManyRequests ||
                    ex.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    ex.StatusCode == HttpStatusCode.BadGateway ||
                    ex.StatusCode == HttpStatusCode.GatewayTimeout ||
                    ex.StatusCode == HttpStatusCode.InternalServerError)
                .Or<TimeoutException>()
                .OrResult<HttpResponseMessage>(response => 
                    response.StatusCode == HttpStatusCode.TooManyRequests ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    response.StatusCode == HttpStatusCode.BadGateway ||
                    response.StatusCode == HttpStatusCode.GatewayTimeout ||
                    response.StatusCode == HttpStatusCode.InternalServerError)
                .WaitAndRetryAsync(
                    retryCount: 5,
                    sleepDurationProvider: retryAttempt => 
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)) + GetJitter(), // Exponential: 2, 4, 8 seconds
                    onRetryAsync: (outcome, timespan, retryCount, context) =>
                    {
                        // Use static logger if available or just skip if not (since this is a static method)
                        // In a real refactor, we might want to pass the logger or use a factory
                        return Task.CompletedTask;
                    });
        }
        
        /// <summary>
        /// Gets random jitter value to prevent thundering herd problem
        /// Adds small random delay to retry intervals to avoid synchronized retries
        /// </summary>
        private static TimeSpan GetJitter()
        {
            // Random value between 0 and 1000ms
            return TimeSpan.FromMilliseconds(new Random().Next(0, 1000));
        }
        
        /// <summary>
        /// Creates a circuit breaker policy for OpenAI API calls.
        /// Prevents cascading failures by stopping requests after consecutive failures.
        /// Provides graceful degradation when the API is unavailable.
        /// </summary>
        private static AsyncCircuitBreakerPolicy CreateCircuitBreakerPolicy()
        {
            return Policy
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .Or<InvalidOperationException>()
                .CircuitBreakerAsync(
                    exceptionsAllowedBeforeBreaking: CircuitBreakerThreshold,
                    durationOfBreak: TimeSpan.FromSeconds(CircuitBreakerDurationSeconds),
                    onBreak: (exception, duration) =>
                    {
                        // Circuit breaker opened
                    },
                    onReset: () =>
                    {
                        // Circuit breaker closed
                    },
                    onHalfOpen: () =>
                    {
                        // Circuit breaker half-open
                    });
        }
        
        // Response model for Whisper API
        private class WhisperResponse
        {
            [JsonProperty("text")]
            public string Text { get; set; } = string.Empty;
        }
    }
}
