using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using WhisperKey.Configuration;
using WhisperKey.Models;
using Whisper.net;
using WhisperKey.Exceptions;

namespace WhisperKey.Services
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
    /// Implements PRIV-01 for privacy-focused processing using Whisper.net.
    /// </summary>
    public class LocalInferenceService : ILocalInferenceService, ILocalTranscriptionProvider, IDisposable
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
        
        // Whisper.net components
        private WhisperFactory? _whisperFactory;
        private WhisperProcessor? _whisperProcessor;
        
        public bool IsModelLoaded => _isModelLoaded;
        public LocalInferenceStatus Status => _status;
        
        // ILocalTranscriptionProvider implementation
        public string ProviderName => "Whisper";
        public bool IsInitialized => _isModelLoaded;
        
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
            
            _logger?.LogInformation("LocalInferenceService initialized with Whisper.net");
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
                
                // Verify model file exists
                if (!File.Exists(modelPath))
                {
                    _logger?.LogError("Model file does not exist at {Path}", modelPath);
                    _status.LoadingState = ModelLoadingState.Error;
                    _status.ErrorMessage = "Model file not found on disk";
                    return false;
                }
                
                _logger?.LogInformation("Loading local Whisper model from {Path}...", modelPath);
                
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
                
                // Initialize Whisper.net
                try
                {
                    _logger?.LogInformation("Initializing Whisper.net with model: {ModelPath}", modelPath);
                    
                    // Create the factory from the model file
                    _whisperFactory = WhisperFactory.FromPath(modelPath);
                    
                    // Build the processor with default settings
                    // Language will be set per-transcription
                    _whisperProcessor = _whisperFactory.CreateBuilder()
                        .WithLanguage("auto") // Auto-detect language
                        .Build();
                    
                    _logger?.LogInformation("Whisper.net processor created successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to initialize Whisper.net");
                    _status.LoadingState = ModelLoadingState.Error;
                    _status.ErrorMessage = $"Failed to initialize Whisper.net: {ex.Message}";
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
        /// Transcribes audio data locally using Whisper.net.
        /// </summary>
        public async Task<LocalTranscriptionResult> TranscribeAudioAsync(byte[] audioData, string? language = null)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                // Notify start
                TranscriptionStarted?.Invoke(this, EventArgs.Empty);
                
                // Ensure model is loaded
                if (!_isModelLoaded || _whisperProcessor == null)
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
                    throw new TranscriptionFormatException("wav", "Audio data cannot be null or empty for local inference");
                }

                lock (_lock)
                {
                    _status.IsTranscribing = true;
                }

                _logger?.LogInformation("Starting local transcription with model {ModelId}...", _currentModelId);

                // Report initial progress
                TranscriptionProgress?.Invoke(this, 10);

                // Convert audio to WAV format if needed and get duration
                float audioDurationSeconds;
                byte[] wavData;
                
                try
                {
                    // Try to detect format and convert to 16kHz mono PCM if needed
                    wavData = ConvertToWhisperFormat(audioData, out audioDurationSeconds);
                    _logger?.LogInformation("Audio converted successfully. Duration: {Duration:F2}s", audioDurationSeconds);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to convert audio format");
                    throw new InvalidOperationException("Failed to convert audio to compatible format", ex);
                }

                TranscriptionProgress?.Invoke(this, 30);

                // Create memory stream from audio data
                using var memoryStream = new MemoryStream(wavData);
                
                // Collect transcription results
                var fullText = new System.Text.StringBuilder();
                var segments = new List<TranscriptionSegment>();
                var detectedLanguage = language ?? "en";
                var totalConfidence = 0.0f;
                var segmentCount = 0;
                
                // Rebuild processor with specific language if provided
                if (!string.IsNullOrEmpty(language) && language != "auto")
                {
                    _whisperProcessor?.Dispose();
                    _whisperProcessor = _whisperFactory?.CreateBuilder()
                        .WithLanguage(language)
                        .Build();
                }
                
                if (_whisperProcessor == null)
                {
                    throw new InvalidOperationException("Whisper processor is not initialized");
                }

                TranscriptionProgress?.Invoke(this, 50);

                // Process audio through Whisper.net
                var progressStep = 0;
                var totalSteps = 10;
                
                await foreach (var result in _whisperProcessor.ProcessAsync(memoryStream))
                {
                    if (result?.Text != null)
                    {
                        fullText.Append(result.Text);
                        
                        // Create segment
                        var segment = new TranscriptionSegment
                        {
                            Text = result.Text.Trim(),
                            StartTime = (float)result.Start.TotalSeconds,
                            EndTime = (float)result.End.TotalSeconds,
                            Confidence = 0.85f // Whisper doesn't provide per-segment confidence directly
                        };
                        segments.Add(segment);
                        
                        totalConfidence += 0.85f;
                        segmentCount++;
                        
                        _logger?.LogDebug("Segment: {Start:F2}s - {End:F2}s: {Text}", 
                            segment.StartTime, segment.EndTime, segment.Text);
                    }
                    
                    // Update progress (50-90% range)
                    progressStep++;
                    if (progressStep % 2 == 0) // Update every few segments
                    {
                        var progress = 50 + (int)((progressStep / (float)totalSteps) * 40);
                        TranscriptionProgress?.Invoke(this, Math.Min(progress, 90));
                    }
                }

                TranscriptionProgress?.Invoke(this, 90);

                // Calculate average confidence
                var avgConfidence = segmentCount > 0 ? totalConfidence / segmentCount : 0.85f;
                var finalText = fullText.ToString().Trim();
                
                if (string.IsNullOrWhiteSpace(finalText))
                {
                    finalText = "[No speech detected]";
                }

                // Calculate processing metrics
                var processingDuration = DateTime.UtcNow - startTime;
                var audioDuration = TimeSpan.FromSeconds(audioDurationSeconds);
                var realTimeFactor = processingDuration.TotalSeconds / audioDuration.TotalSeconds;

                // Create result
                var transcriptionResult = new LocalTranscriptionResult
                {
                    Text = finalText,
                    Language = detectedLanguage,
                    Confidence = avgConfidence,
                    ModelId = _currentModelId,
                    UsedGpu = false, // CPU-only for now
                    IsCached = false,
                    Segments = segments,
                    ProcessingDuration = processingDuration,
                    RealTimeFactor = realTimeFactor
                };

                // Update statistics
                lock (_lock)
                {
                    _statistics.TotalTranscriptions++;
                    _statistics.TotalAudioDuration += audioDuration;
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
                TranscriptionCompleted?.Invoke(this, transcriptionResult);

                _logger?.LogInformation("Local transcription completed in {Duration:F2}s (RTF: {RTF:F2}). Text length: {Length} chars",
                    processingDuration.TotalSeconds, realTimeFactor, finalText.Length);

                return transcriptionResult;
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
        /// Converts audio data to Whisper-compatible format (16kHz, mono, 16-bit PCM WAV).
        /// </summary>
        private byte[] ConvertToWhisperFormat(byte[] audioData, out float durationSeconds)
        {
            durationSeconds = 0;
            
            try
            {
                using var inputStream = new MemoryStream(audioData);
                
                // Try to read as WAV first
                WaveStream? waveStream = null;
                try
                {
                    waveStream = new WaveFileReader(inputStream);
                }
                catch
                {
                    // Not a valid WAV, try to rewind and use raw PCM assumption
                    inputStream.Position = 0;
                }
                
                if (waveStream == null)
                {
                    // Assume raw PCM 16-bit stereo at 44.1kHz (common microphone format)
                    // This is a fallback - in production, you'd want better format detection
                    waveStream = new RawSourceWaveStream(
                        inputStream, 
                        new WaveFormat(44100, 16, 2));
                }
                
                // Calculate duration
                var totalBytes = waveStream.Length;
                var bytesPerSecond = waveStream.WaveFormat.AverageBytesPerSecond;
                durationSeconds = bytesPerSecond > 0 ? (float)totalBytes / bytesPerSecond : 0;
                
                _logger?.LogInformation("Input audio format: {SampleRate}Hz, {Channels} channels, {BitsPerSample}-bit", 
                    waveStream.WaveFormat.SampleRate,
                    waveStream.WaveFormat.Channels,
                    waveStream.WaveFormat.BitsPerSample);
                
                // Convert to 16kHz mono 16-bit PCM
                var targetFormat = new WaveFormat(16000, 16, 1);
                
                using var finalStream = new MemoryStream();
                
                // If format is already correct, just copy
                if (waveStream.WaveFormat.SampleRate == 16000 && 
                    waveStream.WaveFormat.Channels == 1 &&
                    waveStream.WaveFormat.BitsPerSample == 16)
                {
                    // Already in correct format, just write to WAV
                    using (var writer = new WaveFileWriter(finalStream, targetFormat))
                    {
                        waveStream.CopyTo(writer);
                    }
                }
                else
                {
                    // Resample using MediaFoundationResampler
                    using var resampler = new MediaFoundationResampler(waveStream, targetFormat);
                    resampler.ResamplerQuality = 60; // High quality
                    
                    // Write resampled audio to WAV file writer
                    using (var writer = new WaveFileWriter(finalStream, targetFormat))
                    {
                        // Read from resampler and write to output
                        var buffer = new byte[4096];
                        int bytesRead;
                        while ((bytesRead = resampler.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            writer.Write(buffer, 0, bytesRead);
                        }
                    }
                }
                
                return finalStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Audio format conversion failed");
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
                    
                    // Dispose Whisper.net components
                    _whisperProcessor?.Dispose();
                    _whisperProcessor = null;
                    _whisperFactory?.Dispose();
                    _whisperFactory = null;
                    
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

        /// <summary>
        /// Check if a specific model is available (downloaded)
        /// </summary>
        public async Task<bool> IsModelAvailableAsync(string modelId)
        {
            return await _modelManager.IsModelDownloadedAsync(modelId).ConfigureAwait(false);
        }

        public void Dispose()
        {
            UnloadModel();
        }
    }
}
