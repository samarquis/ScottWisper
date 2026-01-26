using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace ScottWisper
{
    public class WhisperService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;
        private readonly string _baseUrl = "https://api.openai.com/v1/audio/transcriptions";
        
        // API usage tracking
        private int _requestCount = 0;
        private decimal _estimatedCost = 0.0m;
        
        // Whisper API pricing (as of 2024)
        private const decimal CostPerMinute = 0.006m; // $0.006 per minute
        
        public event EventHandler<string>? TranscriptionCompleted;
        public event EventHandler<Exception>? TranscriptionError;
        public event EventHandler<UsageStats>? UsageUpdated;
        
        public WhisperService()
        {
            _apiKey = GetApiKey();
            _httpClient = new HttpClient();
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        }
        
        public async Task<string> TranscribeAudioAsync(byte[] audioData, string? language = null)
        {
            try
            {
                if (audioData == null || audioData.Length == 0)
                {
                    throw new ArgumentException("Audio data cannot be null or empty");
                }
                
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
                
                // Make API request
                var response = await _httpClient.PostAsync(_baseUrl, content);
                
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
            var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set. Please set it to use the Whisper API.");
            }
            
            return apiKey;
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
        }
        
        // Response model for Whisper API
        private class WhisperResponse
        {
            [JsonProperty("text")]
            public string Text { get; set; } = string.Empty;
        }
    }
    

}