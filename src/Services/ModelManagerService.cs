using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for model management service
    /// </summary>
    public interface IModelManagerService
    {
        /// <summary>
        /// Get available models that can be downloaded
        /// </summary>
        Task<List<WhisperModelInfo>> GetAvailableModelsAsync();
        
        /// <summary>
        /// Get currently downloaded models
        /// </summary>
        Task<List<WhisperModelInfo>> GetDownloadedModelsAsync();
        
        /// <summary>
        /// Download a model
        /// </summary>
        Task<ModelDownloadStatus> DownloadModelAsync(string modelId, IProgress<ModelDownloadStatus>? progress = null, CancellationToken cancellationToken = default);
        
        /// <summary>
        /// Get download status for a model
        /// </summary>
        Task<ModelDownloadStatus?> GetDownloadStatusAsync(string modelId);
        
        /// <summary>
        /// Cancel an ongoing download
        /// </summary>
        Task<bool> CancelDownloadAsync(string modelId);
        
        /// <summary>
        /// Delete a downloaded model
        /// </summary>
        Task<bool> DeleteModelAsync(string modelId);
        
        /// <summary>
        /// Verify model integrity
        /// </summary>
        Task<bool> VerifyModelAsync(string modelId);
        
        /// <summary>
        /// Get the local path for a model
        /// </summary>
        Task<string?> GetModelPathAsync(string modelId);
        
        /// <summary>
        /// Get the recommended model for the system
        /// </summary>
        Task<WhisperModelInfo> GetRecommendedModelAsync();
        
        /// <summary>
        /// Check if a model is downloaded
        /// </summary>
        Task<bool> IsModelDownloadedAsync(string modelId);
        
        /// <summary>
        /// Get total disk space used by models
        /// </summary>
        Task<long> GetTotalDiskSpaceUsedAsync();
    }
    
    /// <summary>
    /// Service for managing Whisper model downloads and storage
    /// </summary>
    public class ModelManagerService : IModelManagerService
    {
        private readonly ILogger<ModelManagerService> _logger;
        private readonly HttpClient _httpClient;
        private readonly string _modelsDirectory;
        private readonly Dictionary<string, WhisperModelInfo> _availableModels;
        private readonly Dictionary<string, ModelDownloadStatus> _activeDownloads;
        private readonly object _lock = new();
        
        public ModelManagerService(ILogger<ModelManagerService> logger, string? modelsDirectory = null)
        {
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromHours(2); // Long timeout for large model downloads
            
            // Default models directory in AppData
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            _modelsDirectory = modelsDirectory ?? Path.Combine(appDataPath, "WhisperKey", "Models");
            
            Directory.CreateDirectory(_modelsDirectory);
            
            _availableModels = InitializeAvailableModels();
            _activeDownloads = new Dictionary<string, ModelDownloadStatus>();
            
            _logger.LogInformation("ModelManagerService initialized. Models directory: {Directory}", _modelsDirectory);
        }
        
        /// <summary>
        /// Initialize the catalog of available models
        /// </summary>
        private Dictionary<string, WhisperModelInfo> InitializeAvailableModels()
        {
            var models = new Dictionary<string, WhisperModelInfo>();
            
            // Tiny model - fastest, lowest accuracy
            models["tiny"] = new WhisperModelInfo
            {
                Id = "tiny",
                Name = "Tiny",
                Size = ModelSize.Tiny,
                SizeBytes = 39_000_000,
                SizeHuman = "39 MB",
                Description = "Fastest model, good for quick tests and resource-constrained devices",
                SupportedLanguages = new List<string> { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su" },
                RelativeSpeed = 32.0,
                RelativeAccuracy = 0.6,
                RequiredRamMb = 512,
                ReleaseDate = new DateTime(2022, 9, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.bin"
            };
            
            // Tiny English-only variant
            models["tiny.en"] = new WhisperModelInfo
            {
                Id = "tiny.en",
                Name = "Tiny (English-only)",
                Size = ModelSize.Tiny,
                SizeBytes = 39_000_000,
                SizeHuman = "39 MB",
                Description = "Tiny model optimized for English transcription",
                SupportedLanguages = new List<string> { "en" },
                HasEnglishVariant = false,
                RelativeSpeed = 32.0,
                RelativeAccuracy = 0.65,
                RequiredRamMb = 512,
                ReleaseDate = new DateTime(2022, 9, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-tiny.en.bin"
            };
            
            // Base model - balanced speed/accuracy
            models["base"] = new WhisperModelInfo
            {
                Id = "base",
                Name = "Base",
                Size = ModelSize.Base,
                SizeBytes = 74_000_000,
                SizeHuman = "74 MB",
                Description = "Good balance of speed and accuracy for general use",
                SupportedLanguages = new List<string> { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su" },
                RelativeSpeed = 16.0,
                RelativeAccuracy = 0.75,
                RequiredRamMb = 1024,
                IsRecommended = true,
                ReleaseDate = new DateTime(2022, 9, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.bin"
            };
            
            // Base English-only variant
            models["base.en"] = new WhisperModelInfo
            {
                Id = "base.en",
                Name = "Base (English-only)",
                Size = ModelSize.Base,
                SizeBytes = 74_000_000,
                SizeHuman = "74 MB",
                Description = "Base model optimized for English transcription",
                SupportedLanguages = new List<string> { "en" },
                HasEnglishVariant = false,
                RelativeSpeed = 16.0,
                RelativeAccuracy = 0.80,
                RequiredRamMb = 1024,
                ReleaseDate = new DateTime(2022, 9, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-base.en.bin"
            };
            
            // Small model - better accuracy
            models["small"] = new WhisperModelInfo
            {
                Id = "small",
                Name = "Small",
                Size = ModelSize.Small,
                SizeBytes = 244_000_000,
                SizeHuman = "244 MB",
                Description = "Better accuracy, suitable for most professional use cases",
                SupportedLanguages = new List<string> { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su" },
                RelativeSpeed = 6.0,
                RelativeAccuracy = 0.85,
                RequiredRamMb = 2048,
                ReleaseDate = new DateTime(2022, 9, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-small.bin"
            };
            
            // Medium model - high accuracy
            models["medium"] = new WhisperModelInfo
            {
                Id = "medium",
                Name = "Medium",
                Size = ModelSize.Medium,
                SizeBytes = 769_000_000,
                SizeHuman = "769 MB",
                Description = "High accuracy model for professional transcription",
                SupportedLanguages = new List<string> { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su" },
                RelativeSpeed = 2.0,
                RelativeAccuracy = 0.90,
                RequiredRamMb = 5120,
                ReleaseDate = new DateTime(2022, 9, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-medium.bin"
            };
            
            // Large model v1 - best accuracy
            models["large"] = new WhisperModelInfo
            {
                Id = "large",
                Name = "Large",
                Size = ModelSize.Large,
                SizeBytes = 1550_000_000,
                SizeHuman = "1.55 GB",
                Description = "Best accuracy, requires significant RAM and processing time",
                SupportedLanguages = new List<string> { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su" },
                RelativeSpeed = 1.0,
                RelativeAccuracy = 1.0,
                RequiredRamMb = 10240,
                ReleaseDate = new DateTime(2022, 9, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large.bin"
            };
            
            // Large model v2
            models["large-v2"] = new WhisperModelInfo
            {
                Id = "large-v2",
                Name = "Large v2",
                Size = ModelSize.LargeV2,
                SizeBytes = 1550_000_000,
                SizeHuman = "1.55 GB",
                Description = "Improved large model with better accuracy",
                SupportedLanguages = new List<string> { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su" },
                RelativeSpeed = 1.0,
                RelativeAccuracy = 1.05,
                RequiredRamMb = 10240,
                ReleaseDate = new DateTime(2022, 12, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v2.bin"
            };
            
            // Large model v3 - latest and best
            models["large-v3"] = new WhisperModelInfo
            {
                Id = "large-v3",
                Name = "Large v3",
                Size = ModelSize.LargeV3,
                SizeBytes = 1550_000_000,
                SizeHuman = "1.55 GB",
                Description = "Latest large model with best overall accuracy",
                SupportedLanguages = new List<string> { "en", "zh", "de", "es", "ru", "ko", "fr", "ja", "pt", "tr", "pl", "ca", "nl", "ar", "sv", "it", "id", "hi", "fi", "vi", "he", "uk", "el", "ms", "cs", "ro", "da", "hu", "ta", "no", "th", "ur", "hr", "bg", "lt", "la", "mi", "ml", "cy", "sk", "te", "fa", "lv", "bn", "sr", "az", "sl", "kn", "et", "mk", "br", "eu", "is", "hy", "ne", "mn", "bs", "kk", "sq", "sw", "gl", "mr", "pa", "si", "km", "sn", "yo", "so", "af", "oc", "ka", "be", "tg", "sd", "gu", "am", "yi", "lo", "uz", "fo", "ht", "ps", "tk", "nn", "mt", "sa", "lb", "my", "bo", "tl", "mg", "as", "tt", "haw", "ln", "ha", "ba", "jw", "su" },
                RelativeSpeed = 1.0,
                RelativeAccuracy = 1.08,
                RequiredRamMb = 10240,
                ReleaseDate = new DateTime(2023, 11, 1),
                DownloadUrl = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/ggml-large-v3.bin"
            };
            
            return models;
        }
        
        /// <summary>
        /// Get all available models
        /// </summary>
        public Task<List<WhisperModelInfo>> GetAvailableModelsAsync()
        {
            return Task.FromResult(_availableModels.Values.ToList());
        }
        
        /// <summary>
        /// Get downloaded models
        /// </summary>
        public async Task<List<WhisperModelInfo>> GetDownloadedModelsAsync()
        {
            var downloaded = new List<WhisperModelInfo>();
            
            foreach (var model in _availableModels.Values)
            {
                if (await IsModelDownloadedAsync(model.Id).ConfigureAwait(false))
                {
                    downloaded.Add(model);
                }
            }
            
            return downloaded;
        }
        
        /// <summary>
        /// Download a model
        /// </summary>
        public async Task<ModelDownloadStatus> DownloadModelAsync(string modelId, IProgress<ModelDownloadStatus>? progress = null, CancellationToken cancellationToken = default)
        {
            if (!_availableModels.TryGetValue(modelId, out var modelInfo))
            {
                return new ModelDownloadStatus
                {
                    ModelId = modelId,
                    State = DownloadState.Failed,
                    ErrorMessage = $"Model '{modelId}' not found in available models catalog."
                };
            }
            
            // Check if already downloaded
            if (await IsModelDownloadedAsync(modelId).ConfigureAwait(false))
            {
                _logger.LogInformation("Model {ModelId} is already downloaded", modelId);
                return new ModelDownloadStatus
                {
                    ModelId = modelId,
                    State = DownloadState.Completed,
                    TotalBytes = modelInfo.SizeBytes,
                    DownloadedBytes = modelInfo.SizeBytes,
                    LocalPath = await GetModelPathAsync(modelId).ConfigureAwait(false)
                };
            }
            
            // Check if download is already in progress
            lock (_lock)
            {
                if (_activeDownloads.TryGetValue(modelId, out var existingStatus))
                {
                    _logger.LogInformation("Download for model {ModelId} is already in progress", modelId);
                    return existingStatus;
                }
            }
            
            var status = new ModelDownloadStatus
            {
                ModelId = modelId,
                State = DownloadState.Downloading,
                TotalBytes = modelInfo.SizeBytes,
                StartedAt = DateTime.UtcNow
            };
            
            lock (_lock)
            {
                _activeDownloads[modelId] = status;
            }
            
            try
            {
                var modelPath = Path.Combine(_modelsDirectory, $"ggml-{modelId}.bin");
                var tempPath = modelPath + ".tmp";
                
                _logger.LogInformation("Starting download of model {ModelId} from {Url}", modelId, modelInfo.DownloadUrl);
                
                // Download with progress tracking
                using var response = await _httpClient.GetAsync(modelInfo.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                
                var totalBytes = response.Content.Headers.ContentLength ?? modelInfo.SizeBytes;
                status.TotalBytes = totalBytes;
                
                using var contentStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);
                
                var buffer = new byte[81920]; // 80KB buffer
                long downloadedBytes = 0;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                while (true)
                {
                    var read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                    if (read == 0) break;
                    
                    await fileStream.WriteAsync(buffer, 0, read, cancellationToken).ConfigureAwait(false);
                    downloadedBytes += read;
                    
                    // Update status
                    status.DownloadedBytes = downloadedBytes;
                    status.BytesPerSecond = downloadedBytes / stopwatch.Elapsed.TotalSeconds;
                    
                    if (status.BytesPerSecond > 0)
                    {
                        var remainingBytes = totalBytes - downloadedBytes;
                        status.EstimatedTimeRemaining = TimeSpan.FromSeconds(remainingBytes / status.BytesPerSecond);
                    }
                    
                    // Report progress
                    progress?.Report(status);
                }
                
                stopwatch.Stop();
                fileStream.Close();
                
                // Verify download
                status.State = DownloadState.Verifying;
                progress?.Report(status);
                
                var actualSize = new FileInfo(tempPath).Length;
                if (actualSize != totalBytes)
                {
                    throw new InvalidOperationException($"Downloaded file size ({actualSize}) doesn't match expected size ({totalBytes})");
                }
                
                // Move temp file to final location
                if (File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                }
                File.Move(tempPath, modelPath);
                
                status.State = DownloadState.Completed;
                status.CompletedAt = DateTime.UtcNow;
                status.LocalPath = modelPath;
                
                _logger.LogInformation("Model {ModelId} downloaded successfully to {Path}", modelId, modelPath);
            }
            catch (OperationCanceledException)
            {
                status.State = DownloadState.Cancelled;
                _logger.LogWarning("Download of model {ModelId} was cancelled", modelId);
            }
            catch (HttpRequestException ex)
            {
                status.State = DownloadState.Failed;
                status.ErrorMessage = $"Network error: {ex.Message}";
                _logger.LogError(ex, "Network error downloading model {ModelId}", modelId);
            }
            catch (IOException ex)
            {
                status.State = DownloadState.Failed;
                status.ErrorMessage = $"File I/O error: {ex.Message}";
                _logger.LogError(ex, "File I/O error downloading model {ModelId}", modelId);
            }
            catch (InvalidOperationException ex)
            {
                status.State = DownloadState.Failed;
                status.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Invalid operation while downloading model {ModelId}", modelId);
            }
            finally
            {
                lock (_lock)
                {
                    _activeDownloads.Remove(modelId);
                }
                
                // Clean up temp file if it exists
                var tempPath = Path.Combine(_modelsDirectory, $"ggml-{modelId}.bin.tmp");
                if (File.Exists(tempPath))
                {
                    try
                    {
                        File.Delete(tempPath);
                    }
                    catch (IOException ex)
                    {
                        _logger.LogWarning(ex, "Failed to delete temporary file: {TempPath}", tempPath);
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        _logger.LogWarning(ex, "Access denied deleting temporary file: {TempPath}", tempPath);
                    }
                }
                
                // Report final status
                progress?.Report(status);
            }
            
            return status;
        }
        
        /// <summary>
        /// Get download status
        /// </summary>
        public Task<ModelDownloadStatus?> GetDownloadStatusAsync(string modelId)
        {
            lock (_lock)
            {
                _activeDownloads.TryGetValue(modelId, out var status);
                return Task.FromResult(status);
            }
        }
        
        /// <summary>
        /// Cancel an ongoing download
        /// </summary>
        public Task<bool> CancelDownloadAsync(string modelId)
        {
            // Note: Actual cancellation is handled via CancellationToken passed to DownloadModelAsync
            // This method is mainly for tracking purposes
            lock (_lock)
            {
                if (_activeDownloads.TryGetValue(modelId, out var status))
                {
                    status.State = DownloadState.Cancelled;
                    return Task.FromResult(true);
                }
            }
            
            return Task.FromResult(false);
        }
        
        /// <summary>
        /// Delete a downloaded model
        /// </summary>
        public async Task<bool> DeleteModelAsync(string modelId)
        {
            try
            {
                var modelPath = await GetModelPathAsync(modelId).ConfigureAwait(false);
                if (modelPath != null && File.Exists(modelPath))
                {
                    File.Delete(modelPath);
                    _logger.LogInformation("Deleted model {ModelId} from {Path}", modelId, modelPath);
                    return true;
                }
                
                return false;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error deleting model {ModelId}", modelId);
                return false;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Access denied deleting model {ModelId}", modelId);
                return false;
            }
        }
        
        /// <summary>
        /// Verify model integrity (basic file size check)
        /// </summary>
        public async Task<bool> VerifyModelAsync(string modelId)
        {
            try
            {
                if (!_availableModels.TryGetValue(modelId, out var modelInfo))
                {
                    return false;
                }
                
                var modelPath = await GetModelPathAsync(modelId).ConfigureAwait(false);
                if (modelPath == null || !File.Exists(modelPath))
                {
                    return false;
                }
                
                var fileInfo = new FileInfo(modelPath);
                
                // Check file size matches expected size (within 1% tolerance)
                var sizeDifference = Math.Abs(fileInfo.Length - modelInfo.SizeBytes);
                var tolerance = modelInfo.SizeBytes * 0.01;
                
                return sizeDifference <= tolerance;
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "I/O error verifying model {ModelId}", modelId);
                return false;
            }
        }
        
        /// <summary>
        /// Get the local path for a model
        /// </summary>
        public Task<string?> GetModelPathAsync(string modelId)
        {
            var modelPath = Path.Combine(_modelsDirectory, $"ggml-{modelId}.bin");
            return Task.FromResult(File.Exists(modelPath) ? modelPath : null);
        }
        
        /// <summary>
        /// Get the recommended model based on available RAM
        /// </summary>
        public Task<WhisperModelInfo> GetRecommendedModelAsync()
        {
            // Get available system RAM
            var availableRamMb = GetAvailableRamMb();
            
            // Find the best model that fits in available RAM
            // Leave some headroom (use only 60% of available RAM)
            var usableRamMb = availableRamMb * 0.6;
            
            var candidates = _availableModels.Values
                .Where(m => m.RequiredRamMb <= usableRamMb)
                .OrderByDescending(m => m.RelativeAccuracy)
                .ToList();
            
            if (candidates.Any())
            {
                return Task.FromResult(candidates.First());
            }
            
            // Fallback to tiny if no model fits
            return Task.FromResult(_availableModels["tiny"]);
        }
        
        /// <summary>
        /// Check if a model is downloaded
        /// </summary>
        public async Task<bool> IsModelDownloadedAsync(string modelId)
        {
            var path = await GetModelPathAsync(modelId).ConfigureAwait(false);
            return path != null;
        }
        
        /// <summary>
        /// Get total disk space used by models
        /// </summary>
        public async Task<long> GetTotalDiskSpaceUsedAsync()
        {
            var downloadedModels = await GetDownloadedModelsAsync().ConfigureAwait(false);
            return downloadedModels.Sum(m => m.SizeBytes);
        }
        
        /// <summary>
        /// Get available system RAM in MB
        /// </summary>
        private long GetAvailableRamMb()
        {
            try
            {
                // Use GC to get total available memory
                var totalMemory = GC.GetTotalMemory(false);
                var totalPhysicalMemory = new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory;
                return (long)(totalPhysicalMemory / (1024 * 1024));
            }
            catch (PlatformNotSupportedException)
            {
                _logger.LogWarning("Platform not supported for memory detection, defaulting to 4GB");
                return 4096;
            }
            catch (InvalidOperationException)
            {
                _logger.LogWarning("Could not determine available memory, defaulting to 4GB");
                return 4096;
            }
        }
    }
}
