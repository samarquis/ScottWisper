using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using ScottWisper.Services;
using ScottWisper.Configuration;

namespace ScottWisper
{
    public class WhisperService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private string _apiKey;
        private readonly string _baseUrl = "https://api.openai.com/v1/audio/transcriptions";
        private readonly ISettingsService? _settingsService;
        private readonly LocalInferenceService? _localInference;
        
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
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        
        public WhisperService(ISettingsService settingsService, LocalInferenceService? localInference = null)
        {
            _settingsService = settingsService;
            _localInference = localInference;
            _apiKey = GetApiKeyFromSettings();
            _httpClient = new HttpClient();
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
                        var result = await _localInference.TranscribeAudioAsync(audioData, language);
                        TranscriptionProgress?.Invoke(this, 100);
                        TranscriptionCompleted?.Invoke(this, result);
                        return result;
                    }
                    catch (Exception ex)
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
                var response = await _httpClient.PostAsync(_baseUrl, content);
                
                // Report progress after getting response
                TranscriptionProgress?.Invoke(this, 75);
                
                if (!response.IsSuccessStatusCode)
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    throw new HttpRequestException($"Whisper API request failed: {response.StatusCode} - {errorContent}");
                }
                
                // Parse response
                var responseContent = await response.Content.ReadAsStringAsync();
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
            catch (Exception ex)
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
                
                var audioData = await File.ReadAllBytesAsync(filePath);
                return await TranscribeAudioAsync(audioData, language);
            }
            catch (Exception ex)
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
                var keyPath = Path.Combine(appDataPath, "ScottWisper", "api_key.encrypted");
                if (File.Exists(keyPath))
                {
                    var encryptedKey = File.ReadAllText(keyPath);
                    // This would use the same encryption/decryption as SettingsService
                    // For now, return empty to force user to set the key
                }
            }
            catch
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
                catch
                {
                    // Fall back to default method
                }
            }
            
            return GetApiKey();
        }

        private async void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
        {
            // Handle transcription settings changes
            if (e.Category == "Transcription")
            {
                // Update API key if it changed
                if (e.Key.Contains("Api") || e.Key == "ApplyAll" || e.Key == "ReloadSettings")
                {
                    var newApiKey = GetApiKeyFromSettings();
                    if (!string.IsNullOrEmpty(newApiKey) && newApiKey != _apiKey)
                    {
                        _apiKey = newApiKey;
                        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
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
        
        public void Dispose()
        {
            _httpClient?.Dispose();
            _localInference?.Dispose();
        }
        
        // Response model for Whisper API
        private class WhisperResponse
        {
            [JsonProperty("text")]
            public string Text { get; set; } = string.Empty;
        }
    }
    

}