using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
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
        private readonly LocalInferenceService? _localInference;
        private readonly bool _ownsHttpClient;
        
        private const string DefaultApiEndpoint = "https://api.openai.com/v1/audio/transcriptions";
        
        // API usage tracking
        private int _requestCount = 0;
        private decimal _estimatedCost = 0.0m;
        
        // Whisper API pricing (as of 2024)
        private const decimal CostPerMinute = 0.006m; // $0.006 per minute
        
        public event EventHandler? TranscriptionStarted;
        public event EventHandler<int>? TranscriptionProgress;
        public event EventHandler<string>? TranscriptionCompleted;
        public event EventHandler<Exception>? TranscriptionError;
        public event EventHandler<UsageStats>? UsageUpdated;
        
        public WhisperService()
        {
            _apiKey = GetApiKey();
            _baseUrl = DefaultApiEndpoint;
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            _ownsHttpClient = true;
        }
        
        public WhisperService(ISettingsService settingsService, LocalInferenceService? localInference = null)
            : this(settingsService, null, localInference)
        {
        }
        
        public WhisperService(ISettingsService settingsService, IHttpClientFactory? httpClientFactory, LocalInferenceService? localInference = null)
        {
            _settingsService = settingsService;
            _localInference = localInference;
            _apiKey = GetApiKeyFromSettings();
            _baseUrl = GetApiEndpointFromSettings();
            
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
        }
        
        public async Task<string> TranscribeAudioAsync(byte[] audioData, string? language = null)
        {
            try
            {
                // Notify transcription started
                TranscriptionStarted?.Invoke(this, EventArgs.Empty);
                
                if (audioData == null || audioData.Length == 0)
                {
                    throw new ArgumentException("Audio data cannot be null or empty");
                }

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
                
                // Cloud transcription (OpenAI)
                // Create multipart form content
                using var content = new MultipartFormDataContent();
                
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
                
                // Report some progress before making the request
                TranscriptionProgress?.Invoke(this, 25);
                
                // Make API request
                var response = await _httpClient.PostAsync(_baseUrl, content).ConfigureAwait(false);
                
                // Report progress after getting response
                TranscriptionProgress?.Invoke(this, 75);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new HttpRequestException($"Whisper API request failed: {response.StatusCode} - {errorContent}");
                }
                
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

            // Try to read from encrypted settings file
            try
            {
                var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var keyPath = Path.Combine(appDataPath, "WhisperKey", "api_key.encrypted");
                if (File.Exists(keyPath))
                {
                    var encryptedKey = File.ReadAllText(keyPath);
                    // This would use the same encryption/decryption as SettingsService
                    // For now, return empty to force user to set the key
                }
            }
            catch (IOException)
            {
                // Fall through to return empty
            }
            catch (UnauthorizedAccessException)
            {
                // Fall through to return empty
            }
            catch (SecurityException)
            {
                // Fall through to return empty
            }
            catch (NotSupportedException)
            {
                // Fall through to return empty
            }

            return string.Empty;
        }

        private string GetApiKeyFromSettings()
        {
            if (_settingsService != null)
            {
                try
                {
                    return _settingsService.GetEncryptedValueAsync("OpenAI_ApiKey").Result;
                }
                catch (AggregateException ex) when (ex.InnerException != null && !IsFatalException(ex.InnerException))
                {
                    // Fall back to default method
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
            
            return GetApiKey();
        }

        private string GetApiEndpointFromSettings()
        {
            if (_settingsService != null)
            {
                try
                {
                    var endpoint = _settingsService.Settings.Transcription.ApiEndpoint;
                    if (!string.IsNullOrEmpty(endpoint))
                    {
                        return endpoint;
                    }
                }
                catch (Exception ex) when (!IsFatalException(ex))
                {
                    // Fall back to default endpoint
                }
            }
            
            return DefaultApiEndpoint;
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
            await Task.Run(() => {
                // Handle transcription settings changes
                if (e.Category == "Transcription")
                {
                    // Update API key if it changed
                    if (e.Key.Contains("ApiKey") || e.Key == "ApplyAll" || e.Key == "ReloadSettings")
                    {
                        var newApiKey = GetApiKeyFromSettings();
                        if (!string.IsNullOrEmpty(newApiKey) && newApiKey != _apiKey)
                        {
                            _apiKey = newApiKey;
                            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
                        }
                    }
                    
                    // Update API endpoint if it changed
                    if (e.Key.Contains("ApiEndpoint") || e.Key == "ApplyAll" || e.Key == "ReloadSettings")
                    {
                        var newEndpoint = GetApiEndpointFromSettings();
                        if (!string.IsNullOrEmpty(newEndpoint) && newEndpoint != _baseUrl)
                        {
                            _baseUrl = newEndpoint;
                        }
                    }
                }
            }).ConfigureAwait(false);
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
        
        public void Dispose()
        {
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
        
        // Response model for Whisper API
        private class WhisperResponse
        {
            [JsonProperty("text")]
            public string Text { get; set; } = string.Empty;
        }
    }
    

}