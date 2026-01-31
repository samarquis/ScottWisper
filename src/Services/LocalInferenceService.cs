using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScottWisper.Configuration;
using ScottWisper.Models;

namespace ScottWisper.Services
{
    /// <summary>
    /// Interface for local inference service
    /// </summary>
    public interface ILocalInferenceService
    {
        /// <summary>
        /// Whether a model is currently loaded
        /// </summary>
        bool IsModelLoaded { get; }
        
        /// <summary>
        /// Current inference status
        /// </summary>
        LocalInferenceStatus Status { get; }
        
        /// <summary>
        /// Initialize the service with a specific model
        /// </summary>
        Task<bool> InitializeAsync(string? modelId = null);
        
        /// <summary>
        /// Transcribe audio data locally
        /// </summary>
        Task<LocalTranscriptionResult> TranscribeAudioAsync(byte[] audioData, string? language = null);
        
        /// <summary>
        /// Unload the current model
        /// </summary>
        void UnloadModel();
        
        /// <summary>
        /// Get available models
        /// </summary>
        Task<List<WhisperModelInfo>> GetAvailableModelsAsync();
        
        /// <summary>
        /// Get inference statistics
        /// </summary>
        Task<LocalInferenceStatistics> GetStatisticsAsync();
        
        /// <summary>
        /// Event raised when transcription starts
        /// </summary>
        event EventHandler? TranscriptionStarted;
        
        /// <summary>
        /// Event raised when transcription progresses
        /// </summary>
        event EventHandler<int>? TranscriptionProgress;
        
        /// <summary>
        /// Event raised when transcription completes
        /// </summary>
        event EventHandler<LocalTranscriptionResult>? TranscriptionCompleted;
        
        /// <summary>
        /// Event raised when transcription fails
        /// </summary>
        event EventHandler<Exception>? TranscriptionError;
    }
    
    /// <summary>
    /// Enhanced service for local offline transcription using Whisper models.
    /// Implements PRIV-01 for privacy-focused processing.
    /// </summary>
    public class LocalInferenceService : ILocalInferenceService, IDisposable
    {
        private readonly ILogger<LocalInferenceService>? _logger;
        private readonly ISettingsService _settingsService;
        private readonly IModelManagerService _modelManager;
        private bool _isModelLoaded = false;
        private string _currentModelId = string.Empty;
        private string _currentModelPath = string.Empty;
        private LocalInferenceStatus _status = new();
        private LocalInferenceStatistics _statistics = new();
        private LocalInferenceSettings _settings = new();
        private readonly object _lock = new();
        
        public bool IsModelLoaded => _isModelLoaded;
        public LocalInferenceStatus Status => _status;
        
        public event EventHandler? TranscriptionStarted;
        public event EventHandler<int>? TranscriptionProgress;
        public event EventHandler<LocalTranscriptionResult>? TranscriptionCompleted;
        public event EventHandler<Exception>? TranscriptionError;

        public LocalInferenceService(
            ISettingsService settingsService, 
            IModelManagerService modelManager,
            ILogger<LocalInferenceService>? logger = null)
        {
            _settingsService = settingsService;
            _modelManager = modelManager;
            _logger = logger;
            
            // Load settings
            _settings.SelectedModelId = _settingsService.Settings.Transcription.LocalModelPath;
            _settings.EnableGpuAcceleration = false; // Default to CPU for compatibility
            
            _logger?.LogInformation("LocalInferenceService initialized");
        }

        /// <summary>
        /// Initializes the local inference engine and loads the specified model.
        /// </summary>
        public async Task<bool> InitializeAsync(string? modelId = null)
        {
            try
            {
                lock (_lock)
                {
                    if (_isModelLoaded)
                    {
                        UnloadModel();
                    }
                    
                    _status.LoadingState = ModelLoadingState.Loading;
                    _status.ErrorMessage = null;
                }
                
                // Use provided model ID or get from settings
                var targetModelId = modelId ?? _settings.SelectedModelId ?? "base";
                
                _logger?.LogInformation("Initializing local inference with model: {ModelId}", targetModelId);
                
                // Check if model is downloaded
                if (!await _modelManager.IsModelDownloadedAsync(targetModelId))
                {
                    _logger?.LogWarning("Model {ModelId} is not downloaded. Attempting to download...", targetModelId);
                    
                    var downloadStatus = await _modelManager.DownloadModelAsync(targetModelId);
                    if (downloadStatus.State != DownloadState.Completed)
                    {
                        _logger?.LogError("Failed to download model {ModelId}: {Error}", 
                            targetModelId, downloadStatus.ErrorMessage);
                        _status.LoadingState = ModelLoadingState.Error;
                        _status.ErrorMessage = $"Failed to download model: {downloadStatus.ErrorMessage}";
                        return false;
                    }
                }
                
                // Get model path
                var modelPath = await _modelManager.GetModelPathAsync(targetModelId);
                if (string.IsNullOrEmpty(modelPath))
                {
                    _logger?.LogError("Model path not found for {ModelId}", targetModelId);
                    _status.LoadingState = ModelLoadingState.Error;
                    _status.ErrorMessage = "Model file not found";
                    return false;
                }
                
                _logger?.LogInformation("Loading local Whisper model from {Path}...", modelPath);
                
                // Simulate model loading
                // In real implementation, this would initialize whisper.cpp or Whisper.net
                await Task.Delay(500);
                
                // Check available RAM
                var availableRam = GetAvailableRamMb();
                var availableModels = await _modelManager.GetAvailableModelsAsync();
                var modelInfo = availableModels.FirstOrDefault(m => m.Id == targetModelId);
                
                if (modelInfo != null && modelInfo.RequiredRamMb > availableRam)
                {
                    _logger?.LogWarning("Insufficient RAM for model {ModelId}. Required: {Required}MB, Available: {Available}MB",
                        targetModelId, modelInfo.RequiredRamMb, availableRam);
                    _status.LoadingState = ModelLoadingState.Error;
                    _status.ErrorMessage = $"Insufficient RAM. Model requires {modelInfo.RequiredRamMb}MB but only {availableRam}MB is available.";
                    return false;
                }
                
                lock (_lock)
                {
                    _currentModelId = targetModelId;
                    _currentModelPath = modelPath;
                    _isModelLoaded = true;
                    _status.IsInitialized = true;
                    _status.LoadedModelId = targetModelId;
                    _status.LoadingState = ModelLoadingState.Loaded;
                    _status.AvailableRamMb = availableRam;
                }
                
                _logger?.LogInformation("Local Whisper model loaded successfully. Model: {ModelId}, Path: {Path}", 
                    targetModelId, modelPath);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize local inference engine");
                lock (_lock)
                {
                    _status.LoadingState = ModelLoadingState.Error;
                    _status.ErrorMessage = ex.Message;
                }
                return false;
            }
        }

        /// <summary>
        /// Transcribes audio data locally.
        /// </summary>
        public async Task<LocalTranscriptionResult> TranscribeAudioAsync(byte[] audioData, string? language = null)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                // Notify start
                TranscriptionStarted?.Invoke(this, EventArgs.Empty);
                
                // Ensure model is loaded
                if (!_isModelLoaded)
                {
                    var initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        throw new InvalidOperationException("Local inference engine is not initialized and failed to auto-initialize.");
                    }
                }

                // Validate audio data
                if (audioData == null || audioData.Length == 0)
                {
                    throw new ArgumentException("Audio data cannot be null or empty");
                }

                lock (_lock)
                {
                    _status.IsTranscribing = true;
                }

                _logger?.LogInformation("Starting local transcription with model {ModelId}...", _currentModelId);

                // Report initial progress
                TranscriptionProgress?.Invoke(this, 10);

                // Simulate transcription processing
                // In real implementation:
                // 1. Convert audio to format expected by whisper (16kHz, mono, 16-bit PCM)
                // 2. Run inference using whisper.cpp / Whisper.net
                // 3. Process results into segments
                
                await Task.Delay(100); // Initial processing
                TranscriptionProgress?.Invoke(this, 30);
                
                await Task.Delay(200); // Main inference (would be actual model inference)
                TranscriptionProgress?.Invoke(this, 70);
                
                await Task.Delay(100); // Post-processing
                TranscriptionProgress?.Invoke(this, 90);

                // Generate simulated result
                // In real implementation, this would come from the actual model
                var result = new LocalTranscriptionResult
                {
                    Text = $"[Local Whisper - {_currentModelId}] This is simulated local transcription output. " +
                           $"In production, this would contain the actual transcribed text from the audio.",
                    Language = language ?? "en",
                    Confidence = 0.85f,
                    ModelId = _currentModelId,
                    UsedGpu = _settings.EnableGpuAcceleration && _status.GpuAccelerationAvailable,
                    IsCached = false,
                    Segments = new List<TranscriptionSegment>
                    {
                        new TranscriptionSegment
                        {
                            Text = "Simulated transcription segment 1",
                            StartTime = 0.0f,
                            EndTime = 3.5f,
                            Confidence = 0.90f
                        },
                        new TranscriptionSegment
                        {
                            Text = "Simulated transcription segment 2",
                            StartTime = 3.5f,
                            EndTime = 7.0f,
                            Confidence = 0.80f
                        }
                    }
                };

                // Calculate processing metrics
                var processingDuration = DateTime.UtcNow - startTime;
                result.ProcessingDuration = processingDuration;
                
                // Estimate real-time factor (assume 10 seconds of audio)
                var assumedAudioDuration = TimeSpan.FromSeconds(10);
                result.RealTimeFactor = assumedAudioDuration.TotalSeconds / processingDuration.TotalSeconds;

                // Update statistics
                lock (_lock)
                {
                    _statistics.TotalTranscriptions++;
                    _statistics.TotalAudioDuration += assumedAudioDuration;
                    _statistics.TotalProcessingTime += processingDuration;
                    _statistics.LastTranscriptionAt = DateTime.UtcNow;
                    _statistics.AverageRealTimeFactor = _statistics.TotalTranscriptions > 0 
                        ? _statistics.TotalAudioDuration.TotalSeconds / _statistics.TotalProcessingTime.TotalSeconds 
                        : 1.0;
                }

                // Update status
                _status.LastTranscriptionDuration = processingDuration;
                _status.TotalTranscriptions = _statistics.TotalTranscriptions;
                _status.IsTranscribing = false;

                // Report completion
                TranscriptionProgress?.Invoke(this, 100);
                TranscriptionCompleted?.Invoke(this, result);

                _logger?.LogInformation("Local transcription completed in {Duration:F2}s with RTF {RTF:F2}",
                    processingDuration.TotalSeconds, result.RealTimeFactor);

                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Local transcription failed");
                
                lock (_lock)
                {
                    _statistics.FailureCount++;
                    _status.IsTranscribing = false;
                }
                
                TranscriptionError?.Invoke(this, ex);
                throw;
            }
        }

        /// <summary>
        /// Unloads the current model and releases resources.
        /// </summary>
        public void UnloadModel()
        {
            lock (_lock)
            {
                if (_isModelLoaded)
                {
                    _logger?.LogInformation("Unloading local model {ModelId}...", _currentModelId);
                    
                    // Real cleanup would happen here
                    // For example: _whisperProcessor?.Dispose();
                    
                    _isModelLoaded = false;
                    _currentModelId = string.Empty;
                    _currentModelPath = string.Empty;
                    _status.LoadedModelId = null;
                    _status.LoadingState = ModelLoadingState.Idle;
                    _status.IsInitialized = false;
                    
                    _logger?.LogInformation("Local model unloaded");
                }
            }
        }

        /// <summary>
        /// Get available models
        /// </summary>
        public Task<List<WhisperModelInfo>> GetAvailableModelsAsync()
        {
            return _modelManager.GetAvailableModelsAsync();
        }

        /// <summary>
        /// Get inference statistics
        /// </summary>
        public Task<LocalInferenceStatistics> GetStatisticsAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(new LocalInferenceStatistics
                {
                    TotalTranscriptions = _statistics.TotalTranscriptions,
                    TotalAudioDuration = _statistics.TotalAudioDuration,
                    TotalProcessingTime = _statistics.TotalProcessingTime,
                    AverageRealTimeFactor = _statistics.AverageRealTimeFactor,
                    FailureCount = _statistics.FailureCount,
                    CacheHitRate = _statistics.CacheHitRate,
                    StartedAt = _statistics.StartedAt,
                    LastTranscriptionAt = _statistics.LastTranscriptionAt
                });
            }
        }

        /// <summary>
        /// Get available system RAM in MB
        /// </summary>
        private long GetAvailableRamMb()
        {
            try
            {
                // Use GC to estimate memory pressure
                var totalMemory = GC.GetTotalMemory(false);
                
                // Try to get total physical memory (Windows-specific)
                if (OperatingSystem.IsWindows())
                {
                    var computerInfoType = Type.GetType("Microsoft.VisualBasic.Devices.ComputerInfo, Microsoft.VisualBasic");
                    if (computerInfoType != null)
                    {
                        var computerInfo = Activator.CreateInstance(computerInfoType);
                        var totalPhysicalMemoryProperty = computerInfoType.GetProperty("TotalPhysicalMemory");
                        if (totalPhysicalMemoryProperty != null)
                        {
                            var totalPhysicalMemory = (ulong)totalPhysicalMemoryProperty.GetValue(computerInfo)!;
                            return (long)(totalPhysicalMemory / (1024 * 1024));
                        }
                    }
                }
                
                // Fallback: estimate based on GC info
                return 4096; // Assume 4GB
            }
            catch
            {
                return 4096; // Default to 4GB
            }
        }

        public void Dispose()
        {
            UnloadModel();
        }
    }
}
