using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WhisperKey.Configuration;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Vosk-based local transcription provider.
    /// Vosk is an offline speech recognition toolkit that provides:
    /// - Lightweight models (50MB - 1.8GB)
    /// - Real-time streaming transcription
    /// - Supports 20+ languages
    /// - Works offline without internet connection
    /// </summary>
    public class VoskTranscriptionProvider : ILocalTranscriptionProvider
    {
        private readonly ILogger<VoskTranscriptionProvider>? _logger;
        private readonly ISettingsService _settingsService;
        private bool _isInitialized = false;
        private string _currentModelPath = string.Empty;
        private string _currentModelId = string.Empty;
        private LocalInferenceStatus _status = new();
        private LocalInferenceStatistics _statistics = new();
        private readonly object _lock = new();
        
        // Vosk model and recognizer (loaded dynamically)
        private dynamic? _model;
        private dynamic? _recognizer;
        private Type? _voskModelType;
        private Type? _voskRecognizerType;
        private bool _voskAvailable = false;
        
        public string ProviderName => "Vosk";
        public bool IsInitialized => _isInitialized;
        public LocalInferenceStatus Status => _status;
        
        public event EventHandler? TranscriptionStarted;
        public event EventHandler<int>? TranscriptionProgress;
        public event EventHandler<LocalTranscriptionResult>? TranscriptionCompleted;
        public event EventHandler<Exception>? TranscriptionError;

        public VoskTranscriptionProvider(
            ISettingsService settingsService,
            ILogger<VoskTranscriptionProvider>? logger = null)
        {
            _settingsService = settingsService;
            _logger = logger;
            
            // Try to load Vosk assembly
            TryLoadVoskAssembly();
            
            _logger?.LogInformation("VoskTranscriptionProvider initialized");
        }
        
        private void TryLoadVoskAssembly()
        {
            try
            {
                // Try to load Vosk from various possible locations
                var assemblyPaths = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Vosk.dll"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhisperKey", "vosk", "Vosk.dll"),
                    "Vosk"
                };
                
                System.Reflection.Assembly? voskAssembly = null;
                
                foreach (var path in assemblyPaths)
                {
                    try
                    {
                        if (path == "Vosk")
                        {
                            voskAssembly = System.Reflection.Assembly.Load("Vosk");
                        }
                        else if (File.Exists(path))
                        {
                            voskAssembly = System.Reflection.Assembly.LoadFrom(path);
                        }
                        
                        if (voskAssembly != null)
                        {
                            _voskModelType = voskAssembly.GetType("Vosk.Model");
                            _voskRecognizerType = voskAssembly.GetType("Vosk.VoskRecognizer");
                            
                            if (_voskModelType != null && _voskRecognizerType != null)
                            {
                                _voskAvailable = true;
                                _logger?.LogInformation("Vosk assembly loaded successfully");
                                return;
                            }
                        }
                    }
                    catch { /* Continue to next path */ }
                }
                
                _logger?.LogWarning("Vosk assembly not found. Vosk provider will be unavailable.");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to load Vosk assembly");
            }
        }

        public async Task<bool> InitializeAsync(string? modelPath = null)
        {
            if (!_voskAvailable)
            {
                _status.ErrorMessage = "Vosk library is not available. Please install Vosk from https://alphacephei.com/vosk/";
                _status.LoadingState = ModelLoadingState.Error;
                _logger?.LogError("Cannot initialize Vosk: library not available");
                return false;
            }
            
            try
            {
                lock (_lock)
                {
                    if (_isInitialized)
                    {
                        UnloadModel();
                    }
                    
                    _status.LoadingState = ModelLoadingState.Loading;
                    _status.ErrorMessage = null;
                }
                
                // Use provided model path or get from settings
                var targetModelPath = modelPath ?? _settingsService.Settings.Transcription.LocalModelPath;
                
                if (string.IsNullOrEmpty(targetModelPath))
                {
                    // Try to find default model in common locations
                    targetModelPath = FindDefaultModelPath();
                }
                
                if (string.IsNullOrEmpty(targetModelPath) || !Directory.Exists(targetModelPath))
                {
                    _logger?.LogError("Vosk model path not found: {Path}", targetModelPath);
                    _status.LoadingState = ModelLoadingState.Error;
                    _status.ErrorMessage = "Vosk model not found. Please download a model from https://alphacephei.com/vosk/models";
                    return false;
                }
                
                _logger?.LogInformation("Initializing Vosk with model: {Path}", targetModelPath);
                
                // Create Vosk model instance using reflection
                try
                {
                    _model = Activator.CreateInstance(_voskModelType!, targetModelPath);
                    _currentModelPath = targetModelPath;
                    _currentModelId = Path.GetFileName(targetModelPath);
                    
                    // Create recognizer with default sample rate (16000)
                    _recognizer = Activator.CreateInstance(_voskRecognizerType!, _model, 16000.0f);
                    
                    _logger?.LogInformation("Vosk model loaded successfully");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Failed to create Vosk model/recognizer instances");
                    _status.LoadingState = ModelLoadingState.Error;
                    _status.ErrorMessage = $"Failed to initialize Vosk: {ex.Message}";
                    return false;
                }
                
                lock (_lock)
                {
                    _isInitialized = true;
                    _status.IsInitialized = true;
                    _status.LoadedModelId = _currentModelId;
                    _status.LoadingState = ModelLoadingState.Loaded;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize Vosk provider");
                lock (_lock)
                {
                    _status.LoadingState = ModelLoadingState.Error;
                    _status.ErrorMessage = ex.Message;
                }
                return false;
            }
        }
        
        private string? FindDefaultModelPath()
        {
            // Common locations for Vosk models
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhisperKey", "models", "vosk"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhisperKey", "models", "vosk"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "vosk"),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vosk-models"),
            };
            
            foreach (var basePath in possiblePaths)
            {
                if (Directory.Exists(basePath))
                {
                    // Look for model directories (they contain files like am/final.mdl, graph/HCLr.fst, etc.)
                    var modelDirs = Directory.GetDirectories(basePath);
                    foreach (var dir in modelDirs)
                    {
                        if (IsValidVoskModel(dir))
                        {
                            return dir;
                        }
                    }
                }
            }
            
            return null;
        }
        
        private bool IsValidVoskModel(string path)
        {
            // Check for Vosk model signature files
            return Directory.Exists(Path.Combine(path, "am")) ||
                   Directory.Exists(Path.Combine(path, "graph")) ||
                   File.Exists(Path.Combine(path, "final.mdl")) ||
                   File.Exists(Path.Combine(path, "model.ckpt"));
        }

        public async Task<LocalTranscriptionResult> TranscribeAudioAsync(byte[] audioData, string? language = null)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                TranscriptionStarted?.Invoke(this, EventArgs.Empty);
                
                if (!_isInitialized || _recognizer == null)
                {
                    var initialized = await InitializeAsync();
                    if (!initialized)
                    {
                        throw new InvalidOperationException("Vosk provider is not initialized and failed to auto-initialize.");
                    }
                }
                
                if (audioData == null || audioData.Length == 0)
                {
                    throw new ArgumentException("Audio data cannot be null or empty");
                }
                
                lock (_lock)
                {
                    _status.IsTranscribing = true;
                }
                
                _logger?.LogInformation("Starting Vosk transcription...");
                TranscriptionProgress?.Invoke(this, 10);
                
                // Convert audio to 16-bit 16kHz mono PCM if needed
                var pcmData = ConvertToPcm(audioData, out var durationSeconds);
                
                TranscriptionProgress?.Invoke(this, 30);
                
                // Process audio through Vosk
                string finalText;
                float confidence = 0.85f;
                
                try
                {
                    // Reset recognizer
                    _recognizer!.Reset();
                    
                    // Process audio in chunks
                    const int chunkSize = 4096;
                    var totalChunks = (pcmData.Length + chunkSize - 1) / chunkSize;
                    var processedChunks = 0;
                    
                    for (int i = 0; i < pcmData.Length; i += chunkSize)
                    {
                        var currentChunkSize = Math.Min(chunkSize, pcmData.Length - i);
                        var chunk = new byte[currentChunkSize];
                        Buffer.BlockCopy(pcmData, i, chunk, 0, currentChunkSize);
                        
                        // Accept waveform
                        _recognizer.AcceptWaveform(chunk);
                        
                        processedChunks++;
                        var progress = 30 + (int)((processedChunks / (float)totalChunks) * 50);
                        TranscriptionProgress?.Invoke(this, Math.Min(progress, 80));
                    }
                    
                    TranscriptionProgress?.Invoke(this, 85);
                    
                    // Get final result
                    var resultJson = _recognizer.FinalResult();
                    var result = ParseVoskResult(resultJson);
                    finalText = result.Text;
                    confidence = result.Confidence;
                    
                    _logger?.LogInformation("Vosk transcription completed. Text length: {Length}", finalText.Length);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Vosk transcription failed during processing");
                    throw new InvalidOperationException("Transcription failed", ex);
                }
                
                TranscriptionProgress?.Invoke(this, 90);
                
                if (string.IsNullOrWhiteSpace(finalText))
                {
                    finalText = "[No speech detected]";
                }
                
                // Calculate metrics
                var processingDuration = DateTime.UtcNow - startTime;
                var audioDuration = TimeSpan.FromSeconds(durationSeconds);
                var realTimeFactor = processingDuration.TotalSeconds / Math.Max(audioDuration.TotalSeconds, 0.001);
                
                // Create result
                var transcriptionResult = new LocalTranscriptionResult
                {
                    Text = finalText,
                    Language = language ?? "en",
                    Confidence = confidence,
                    ModelId = _currentModelId,
                    UsedGpu = false, // Vosk doesn't use GPU
                    IsCached = false,
                    Segments = new List<TranscriptionSegment>(), // Vosk doesn't provide segments by default
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
                
                _status.LastTranscriptionDuration = processingDuration;
                _status.TotalTranscriptions = _statistics.TotalTranscriptions;
                _status.IsTranscribing = false;
                
                TranscriptionProgress?.Invoke(this, 100);
                TranscriptionCompleted?.Invoke(this, transcriptionResult);
                
                _logger?.LogInformation("Vosk transcription completed in {Duration:F2}s (RTF: {RTF:F2})",
                    processingDuration.TotalSeconds, realTimeFactor);
                
                return transcriptionResult;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Vosk transcription failed");
                
                lock (_lock)
                {
                    _statistics.FailureCount++;
                    _status.IsTranscribing = false;
                }
                
                TranscriptionError?.Invoke(this, ex);
                throw;
            }
        }
        
        private (string Text, float Confidence) ParseVoskResult(string json)
        {
            try
            {
                var jObject = JObject.Parse(json);
                var text = jObject["text"]?.ToString() ?? jObject["partial"]?.ToString() ?? string.Empty;
                
                // Try to extract confidence from result
                var confidence = 0.85f;
                if (jObject["result"] is JArray results && results.Count > 0)
                {
                    var confidences = new List<float>();
                    foreach (var item in results)
                    {
                        if (item["conf"] != null)
                        {
                            confidences.Add((float)item["conf"]!);
                        }
                    }
                    
                    if (confidences.Count > 0)
                    {
                        confidence = confidences.Average();
                    }
                }
                
                return (text.Trim(), confidence);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to parse Vosk result JSON");
                return (json, 0.5f);
            }
        }
        
        private byte[] ConvertToPcm(byte[] audioData, out float durationSeconds)
        {
            durationSeconds = 0;
            
            try
            {
                // For now, assume input is already 16-bit PCM at 16kHz mono
                // In a full implementation, this would use NAudio to convert various formats
                
                // Estimate duration based on 16kHz 16-bit mono = 32000 bytes per second
                durationSeconds = audioData.Length / 32000f;
                
                // If the data appears to be WAV, skip the header
                if (audioData.Length > 44 && 
                    audioData[0] == 'R' && audioData[1] == 'I' && 
                    audioData[2] == 'F' && audioData[3] == 'F')
                {
                    // It's a WAV file, extract PCM data
                    // WAV header is 44 bytes for standard PCM
                    var pcmData = new byte[audioData.Length - 44];
                    Buffer.BlockCopy(audioData, 44, pcmData, 0, pcmData.Length);
                    return pcmData;
                }
                
                return audioData;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Audio conversion failed");
                throw;
            }
        }

        public void UnloadModel()
        {
            lock (_lock)
            {
                if (_isInitialized)
                {
                    _logger?.LogInformation("Unloading Vosk model...");
                    
                    _recognizer?.Dispose();
                    _recognizer = null;
                    
                    // Model doesn't have Dispose in older Vosk versions
                    // Just let it be garbage collected
                    _model = null;
                    
                    _isInitialized = false;
                    _currentModelId = string.Empty;
                    _currentModelPath = string.Empty;
                    _status.LoadedModelId = null;
                    _status.LoadingState = ModelLoadingState.Idle;
                    _status.IsInitialized = false;
                    
                    _logger?.LogInformation("Vosk model unloaded");
                }
            }
        }

        public Task<List<WhisperModelInfo>> GetAvailableModelsAsync()
        {
            // Vosk models are typically downloaded manually from https://alphacephei.com/vosk/models
            var models = new List<WhisperModelInfo>
            {
                new WhisperModelInfo
                {
                    Id = "vosk-model-small-en-us-0.15",
                    Name = "English Small (US)",
                    Size = ModelSize.Small,
                    SizeBytes = 40 * 1024 * 1024,
                    SizeHuman = "40 MB",
                    Description = "Lightweight English model for US accent",
                    RequiredRamMb = 512,
                    RelativeSpeed = 2.0,
                    RelativeAccuracy = 0.75
                },
                new WhisperModelInfo
                {
                    Id = "vosk-model-en-us-0.22",
                    Name = "English Full (US)",
                    Size = ModelSize.Medium,
                    SizeBytes = 1_800_000_000,
                    SizeHuman = "1.8 GB",
                    Description = "Full English model for US accent with higher accuracy",
                    RequiredRamMb = 2048,
                    RelativeSpeed = 0.8,
                    RelativeAccuracy = 0.90
                },
                new WhisperModelInfo
                {
                    Id = "vosk-model-small-en-gb-0.15",
                    Name = "English Small (UK)",
                    Size = ModelSize.Small,
                    SizeBytes = 42 * 1024 * 1024,
                    SizeHuman = "42 MB",
                    Description = "Lightweight English model for UK accent",
                    RequiredRamMb = 512,
                    RelativeSpeed = 2.0,
                    RelativeAccuracy = 0.75
                }
            };
            
            return Task.FromResult(models);
        }

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

        public Task<bool> IsModelAvailableAsync(string modelId)
        {
            // Check if the model exists in the models directory
            var modelPath = FindModelPath(modelId);
            return Task.FromResult(!string.IsNullOrEmpty(modelPath) && IsValidVoskModel(modelPath));
        }
        
        private string? FindModelPath(string modelId)
        {
            var possiblePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhisperKey", "models", "vosk", modelId),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WhisperKey", "models", "vosk", modelId),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "models", "vosk", modelId),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vosk-models", modelId),
            };
            
            foreach (var path in possiblePaths)
            {
                if (Directory.Exists(path))
                {
                    return path;
                }
            }
            
            return null;
        }

        public void Dispose()
        {
            UnloadModel();
        }
    }
    
    /// <summary>
    /// Helper class for Vosk result parsing
    /// </summary>
    public class VoskResult
    {
        [JsonProperty("text")]
        public string Text { get; set; } = string.Empty;
        
        [JsonProperty("partial")]
        public string Partial { get; set; } = string.Empty;
        
        [JsonProperty("result")]
        public List<VoskWordResult> Result { get; set; } = new();
    }
    
    public class VoskWordResult
    {
        [JsonProperty("conf")]
        public float Confidence { get; set; }
        
        [JsonProperty("end")]
        public float End { get; set; }
        
        [JsonProperty("start")]
        public float Start { get; set; }
        
        [JsonProperty("word")]
        public string Word { get; set; } = string.Empty;
    }
}
