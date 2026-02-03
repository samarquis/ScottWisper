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
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using WhisperKey.Services;
using WhisperKey.Configuration;

namespace WhisperKey
{
    public class WhisperService : IWhisperService
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private string _baseUrl;
        private readonly ISettingsService? _settingsService;
        private readonly ICredentialService? _credentialService;
        private readonly LocalInferenceService? _localInference;
        private readonly IConfiguration? _configuration;
        private readonly ILogger<WhisperService>? _logger;
        private readonly bool _ownsHttpClient;
        private readonly AsyncRetryPolicy<HttpResponseMessage> _retryPolicy;
        private readonly AsyncCircuitBreakerPolicy _circuitBreakerPolicy;
        private readonly TokenBucketRateLimiter? _rateLimiter;
        
        // Configuration key for API endpoint
        private const string ApiEndpointConfigKey = "Transcription:ApiEndpoint";
        
        // Default API endpoint value (loaded from configuration)
        private const string DefaultApiEndpointValue = "https://api.openai.com/v1/audio/transcriptions";
        
        // Maximum audio file size (25MB - OpenAI API limit)
        private const int MaxAudioSizeBytes = 25 * 1024 * 1024;
        
        // API usage tracking
        private int _requestCount = 0;
        private decimal _estimatedCost = 0.0m;
        
        // Whisper API pricing (as of 2024)
        private const decimal CostPerMinute = 0.006m; // $0.006 per minute
        
        // Circuit breaker configuration
        private const int CircuitBreakerThreshold = 5; // Open after 5 failures
        private const int CircuitBreakerDurationSeconds = 30; // Stay open for 30 seconds
        
        public event EventHandler? TranscriptionStarted;
        public event EventHandler<int>? TranscriptionProgress;
        public event EventHandler<string>? TranscriptionCompleted;
        public event EventHandler<Exception>? TranscriptionError;
        public event EventHandler<UsageStats>? UsageUpdated;
        
        public WhisperService()
        {
            _apiKey = GetApiKey();
            _baseUrl = DefaultApiEndpointValue;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _ownsHttpClient = true;
            
            // Initialize policies
            _retryPolicy = CreateRetryPolicy();
            _circuitBreakerPolicy = CreateCircuitBreakerPolicy();
        }
        
        public WhisperService(ISettingsService settingsService, LocalInferenceService? localInference = null)
            : this(settingsService, null, localInference)
        {
        }
        
        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, LocalInferenceService? localInference = null)
            : this(settingsService, httpClientFactory, null, localInference)
        {
        }
        
        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, ICredentialService? credentialService, LocalInferenceService? localInference = null)
            : this(settingsService, httpClientFactory, credentialService, localInference, null)
        {
        }
        
        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, ICredentialService? credentialService, LocalInferenceService? localInference, IConfiguration? configuration)
        {
            _settingsService = settingsService;
            _credentialService = credentialService;
            _localInference = localInference;
            _configuration = configuration;
            _apiKey = GetApiKey(); // Sync version for constructor (env var only)
            
            // Load API endpoint from configuration immediately
            _baseUrl = GetApiEndpointFromConfiguration();
            
            // Initialize rate limiter from configuration
            if (_settingsService?.Settings.Transcription.EnableRateLimiting == true)
            {
                var maxRequests = _settingsService.Settings.Transcription.MaxRequestsPerMinute;
                _rateLimiter = new TokenBucketRateLimiter(maxRequests, 1);
                System.Diagnostics.Debug.WriteLine($"Rate limiting enabled: {maxRequests} requests per minute");
            }
            
            // Use IHttpClientFactory if available to prevent socket exhaustion
            if (httpClientFactory != null)
            {
                _httpClient = httpClientFactory.CreateClient("WhisperApi");
                _ownsHttpClient = false;
            }
            else
            {
                _httpClient = new HttpClient();
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
            _retryPolicy = CreateRetryPolicy();
            _circuitBreakerPolicy = CreateCircuitBreakerPolicy();
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
                        System.Diagnostics.Debug.WriteLine($"Invalid API endpoint URL format: {endpoint}. Using configuration value.");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during async initialization: {ex.Message}");
            }
        }
        
        public async Task<string> TranscribeAudioAsync(byte[] audioData, string? language = null)
        {
            try
            {
                // Check rate limiting if enabled
                if (_rateLimiter != null)
                {
                    var applyToLocal = _settingsService?.Settings.Transcription.ApplyRateLimitToLocal ?? true;
                    var isLocalMode = _settingsService?.Settings.Transcription.Mode == TranscriptionMode.Local;
                    
                    // Apply rate limiting if it's cloud mode, or if configured to apply to local mode too
                    if (!isLocalMode || applyToLocal)
                    {
                        if (!_rateLimiter.TryConsume())
                        {
                            var waitTime = _rateLimiter.GetTimeUntilNextToken();
                            var message = $"Rate limit exceeded. Maximum {_rateLimiter.MaxTokens} requests per {_rateLimiter.PeriodMinutes} minute(s). Please try again in {waitTime.TotalSeconds:F1} seconds.";
                            
                            _logger?.LogWarning("Rate limit exceeded for transcription request. Wait time: {WaitSeconds:F1}s", waitTime.TotalSeconds);
                            System.Diagnostics.Debug.WriteLine(message);
                            
                            // Return 429 Too Many Requests
                            throw new HttpRequestException(message, null, HttpStatusCode.TooManyRequests);
                        }
                    }
                }
                
                // Validate audio data before processing
                ValidateAudioData(audioData);
                
                // Notify transcription started
                TranscriptionStarted?.Invoke(this, EventArgs.Empty);
                
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
                            System.Diagnostics.Debug.WriteLine($"Local transcription failed, falling back to cloud: {ex.Message}");
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
                            System.Diagnostics.Debug.WriteLine($"Local transcription failed, falling back to cloud: {ex.Message}");
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
                            System.Diagnostics.Debug.WriteLine($"Local transcription failed, falling back to cloud: {ex.Message}");
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
                    content.Add(new StringContent("whisper-1"), "model");
                    
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
                        throw new HttpRequestException($"Whisper API request failed: {result.StatusCode} - {errorContent}");
                    }
                    
                    return result;
                }).ConfigureAwait(false);
                }).ConfigureAwait(false);
                
                // Report progress after getting response
                TranscriptionProgress?.Invoke(this, 75);
                
                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var transcriptionResponse = JsonConvert.DeserializeObject<WhisperResponse>(responseContent);
                
                if (transcriptionResponse?.Text == null)
                {
                    throw new InvalidOperationException("Invalid response from Whisper API");
                }
                
                // Update usage statistics
                UpdateUsageStats(audioData.Length);
                
                // Notify subscribers
                TranscriptionCompleted?.Invoke(this, transcriptionResponse.Text);
                
                return transcriptionResponse.Text;
            }
            catch (BrokenCircuitException ex)
            {
                // Circuit breaker is open - API is unavailable
                var circuitBreakerMessage = "Whisper API is temporarily unavailable due to repeated failures. " +
                    $"Circuit breaker will reset in {CircuitBreakerDurationSeconds} seconds. " +
                    "Consider using local transcription mode.";
                System.Diagnostics.Debug.WriteLine(circuitBreakerMessage);
                
                var wrappedException = new HttpRequestException(circuitBreakerMessage, ex);
                TranscriptionError?.Invoke(this, wrappedException);
                throw wrappedException;
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
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
            // Try environment variable first
            var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                return envKey;
            }

            // Note: File I/O for encrypted key is now handled asynchronously via GetApiKeyFromSettingsAsync
            // This sync method only checks environment variables for constructor initialization
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
            
            // Fall back to environment variable
            var envKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (!string.IsNullOrEmpty(envKey))
            {
                return envKey;
            }

            return string.Empty;
        }

        private async Task<string> GetApiKeyFromSettingsAsync()
        {
            // Try credential service first (Windows Credential Manager)
            if (_credentialService != null)
            {
                try
                {
                    var credentialKey = await _credentialService.RetrieveCredentialAsync("OpenAI_ApiKey").ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(credentialKey))
                    {
                        return credentialKey;
                    }
                }
                catch (Exception ex) when (!IsFatalException(ex))
                {
                    // Fall back to settings service
                }
            }
            
            // Fall back to settings service encrypted storage
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
                            System.Diagnostics.Debug.WriteLine($"Invalid API endpoint URL in configuration: {endpoint}. Using default.");
                        }
                    }
                }
                
                // Fall back to default value
                return DefaultApiEndpointValue;
            }
            catch (Exception ex) when (!IsFatalException(ex))
            {
                System.Diagnostics.Debug.WriteLine($"Error loading API endpoint from configuration: {ex.Message}. Using default.");
                return DefaultApiEndpointValue;
            }
        }

        private async Task<string> GetApiEndpointFromSettingsAsync()
        {
            if (_settingsService != null)
            {
                try
                {
                    // Settings access is synchronous (in-memory), but we make this async for consistency
                    await Task.Yield();
                    var endpoint = _settingsService.Settings.Transcription.ApiEndpoint;
                    if (!string.IsNullOrEmpty(endpoint))
                    {
                        return endpoint;
                    }
                }
                catch (Exception ex) when (!IsFatalException(ex))
                {
                    // Log error but don't fall back - let configuration value be used
                    System.Diagnostics.Debug.WriteLine($"Error reading endpoint from settings: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Error handling settings change: {ex.Message}");
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
        /// Validates audio data before API upload.
        /// Checks file size against MAX_AUDIO_SIZE limit.
        /// Verifies WAV format headers.
        /// Throws SecurityException if invalid.
        /// </summary>
        private static void ValidateAudioData(byte[] audioData)
        {
            if (audioData == null)
            {
                throw new SecurityException("Audio data cannot be null");
            }
            
            if (audioData.Length == 0)
            {
                throw new SecurityException("Audio data cannot be empty");
            }
            
            // Check file size against limit
            if (audioData.Length > MaxAudioSizeBytes)
            {
                throw new SecurityException($"Audio file size ({audioData.Length} bytes) exceeds maximum allowed size ({MaxAudioSizeBytes} bytes = 25MB)");
            }
            
            // Verify WAV format headers (minimum 44 bytes for standard WAV header)
            if (audioData.Length < 44)
            {
                throw new SecurityException($"Audio file too small to be a valid WAV file ({audioData.Length} bytes, minimum 44 bytes required)");
            }
            
            // Check RIFF header
            if (audioData[0] != 'R' || audioData[1] != 'I' || audioData[2] != 'F' || audioData[3] != 'F')
            {
                throw new SecurityException("Invalid audio file format: missing RIFF header");
            }
            
            // Check WAVE format
            if (audioData[8] != 'W' || audioData[9] != 'A' || audioData[10] != 'V' || audioData[11] != 'E')
            {
                throw new SecurityException("Invalid audio file format: not a valid WAVE file");
            }
            
            // Check fmt  subchunk
            if (audioData[12] != 'f' || audioData[13] != 'm' || audioData[14] != 't' || audioData[15] != ' ')
            {
                throw new SecurityException("Invalid audio file format: missing fmt  subchunk");
            }
            
            // Check data subchunk marker location (typically at byte 36, but can vary)
            // We look for "data" starting from byte 36
            bool foundDataChunk = false;
            for (int i = 36; i < audioData.Length - 4; i++)
            {
                if (audioData[i] == 'd' && audioData[i + 1] == 'a' && 
                    audioData[i + 2] == 't' && audioData[i + 3] == 'a')
                {
                    foundDataChunk = true;
                    break;
                }
            }
            
            if (!foundDataChunk)
            {
                throw new SecurityException("Invalid audio file format: missing data chunk");
            }
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
                System.Diagnostics.Debug.WriteLine($"API endpoint must use HTTPS. Got: {uri.Scheme}");
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
                .Handle<HttpRequestException>()
                .Or<TimeoutException>()
                .OrResult<HttpResponseMessage>(response => 
                    response.StatusCode == HttpStatusCode.TooManyRequests ||
                    response.StatusCode == HttpStatusCode.ServiceUnavailable ||
                    response.StatusCode == HttpStatusCode.BadGateway ||
                    response.StatusCode == HttpStatusCode.GatewayTimeout)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: retryAttempt => 
                        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential: 2, 4, 8 seconds
                    onRetryAsync: (outcome, timespan, retryCount, context) =>
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"Retry {retryCount} after {timespan.TotalSeconds}s due to: {outcome.Exception?.Message ?? outcome.Result?.StatusCode.ToString()}");
                        return Task.CompletedTask;
                    });
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
                        System.Diagnostics.Debug.WriteLine(
                            $"Circuit breaker OPEN for {duration.TotalSeconds}s due to: {exception.Message}");
                    },
                    onReset: () =>
                    {
                        System.Diagnostics.Debug.WriteLine("Circuit breaker CLOSED - requests allowed");
                    },
                    onHalfOpen: () =>
                    {
                        System.Diagnostics.Debug.WriteLine("Circuit breaker HALF-OPEN - testing request");
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