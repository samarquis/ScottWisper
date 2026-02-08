using System;
using System.Collections.Generic;

namespace WhisperKey.Models
{
    /// <summary>
    /// Information about a downloadable Whisper model
    /// </summary>
    public class WhisperModelInfo
    {
        /// <summary>
        /// Unique identifier for the model
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Model name (e.g., "tiny", "base", "small", "medium", "large")
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Model size category
        /// </summary>
        public ModelSize Size { get; set; }
        
        /// <summary>
        /// Model size in bytes
        /// </summary>
        public long SizeBytes { get; set; }
        
        /// <summary>
        /// Human-readable size (e.g., "75 MB")
        /// </summary>
        public string SizeHuman { get; set; } = string.Empty;
        
        /// <summary>
        /// Download URL
        /// </summary>
        public string DownloadUrl { get; set; } = string.Empty;
        
        /// <summary>
        /// SHA256 hash for integrity verification
        /// </summary>
        public string? Sha256Hash { get; set; }
        
        /// <summary>
        /// Model description
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Languages supported by this model
        /// </summary>
        public List<string> SupportedLanguages { get; set; } = new();
        
        /// <summary>
        /// English-only variant available
        /// </summary>
        public bool HasEnglishVariant { get; set; }
        
        /// <summary>
        /// Relative speed compared to large model (1.0 = large speed)
        /// </summary>
        public double RelativeSpeed { get; set; } = 1.0;
        
        /// <summary>
        /// Relative accuracy compared to large model (1.0 = large accuracy)
        /// </summary>
        public double RelativeAccuracy { get; set; } = 1.0;
        
        /// <summary>
        /// RAM required to run this model (in MB)
        /// </summary>
        public int RequiredRamMb { get; set; }
        
        /// <summary>
        /// Whether this is the recommended model for most users
        /// </summary>
        public bool IsRecommended { get; set; } = false;
        
        /// <summary>
        /// Model version
        /// </summary>
        public string Version { get; set; } = "1.0";
        
        /// <summary>
        /// When the model was released
        /// </summary>
        public DateTime ReleaseDate { get; set; }
    }
    
    /// <summary>
    /// Model size categories
    /// </summary>
    public enum ModelSize
    {
        Tiny,
        Base,
        Small,
        Medium,
        Large,
        LargeV2,
        LargeV3
    }
    
    /// <summary>
    /// Status of a model download
    /// </summary>
    public class ModelDownloadStatus
    {
        /// <summary>
        /// Model being downloaded
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// Download state
        /// </summary>
        public DownloadState State { get; set; } = DownloadState.Pending;
        
        /// <summary>
        /// Total bytes to download
        /// </summary>
        public long TotalBytes { get; set; }
        
        /// <summary>
        /// Bytes downloaded so far
        /// </summary>
        public long DownloadedBytes { get; set; }
        
        /// <summary>
        /// Download progress percentage (0-100)
        /// </summary>
        public double ProgressPercent => TotalBytes > 0 ? (DownloadedBytes / (double)TotalBytes) * 100 : 0;
        
        /// <summary>
        /// Download speed in bytes per second
        /// </summary>
        public double BytesPerSecond { get; set; }
        
        /// <summary>
        /// Estimated time remaining
        /// </summary>
        public TimeSpan? EstimatedTimeRemaining { get; set; }
        
        /// <summary>
        /// Error message if download failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// When download started
        /// </summary>
        public DateTime? StartedAt { get; set; }
        
        /// <summary>
        /// When download completed or failed
        /// </summary>
        public DateTime? CompletedAt { get; set; }
        
        /// <summary>
        /// Local file path where model is saved
        /// </summary>
        public string? LocalPath { get; set; }
    }
    
    /// <summary>
    /// Download states
    /// </summary>
    public enum DownloadState
    {
        Pending,
        Downloading,
        Verifying,
        Completed,
        Failed,
        Cancelled
    }
    
    /// <summary>
    /// Local inference engine status
    /// </summary>
    public class LocalInferenceStatus
    {
        /// <summary>
        /// Whether the inference engine is initialized
        /// </summary>
        public bool IsInitialized { get; set; }
        
        /// <summary>
        /// Currently loaded model
        /// </summary>
        public string? LoadedModelId { get; set; }
        
        /// <summary>
        /// Current model loading state
        /// </summary>
        public ModelLoadingState LoadingState { get; set; }
        
        /// <summary>
        /// Whether a transcription is currently in progress
        /// </summary>
        public bool IsTranscribing { get; set; }
        
        /// <summary>
        /// GPU acceleration available
        /// </summary>
        public bool GpuAccelerationAvailable { get; set; }
        
        /// <summary>
        /// Whether GPU acceleration is enabled
        /// </summary>
        public bool GpuAccelerationEnabled { get; set; }
        
        /// <summary>
        /// Available system RAM (MB)
        /// </summary>
        public long AvailableRamMb { get; set; }
        
        /// <summary>
        /// Error message if initialization failed
        /// </summary>
        public string? ErrorMessage { get; set; }
        
        /// <summary>
        /// Last transcription duration
        /// </summary>
        public TimeSpan? LastTranscriptionDuration { get; set; }
        
        /// <summary>
        /// Total transcriptions performed
        /// </summary>
        public int TotalTranscriptions { get; set; }
    }
    
    /// <summary>
    /// Model loading states
    /// </summary>
    public enum ModelLoadingState
    {
        Idle,
        Loading,
        Loaded,
        Unloading,
        Error
    }
    
    /// <summary>
    /// Local inference settings
    /// </summary>
    public class LocalInferenceSettings
    {
        /// <summary>
        /// Currently selected model ID
        /// </summary>
        public string SelectedModelId { get; set; } = "base";
        
        /// <summary>
        /// Models directory path
        /// </summary>
        public string ModelsDirectory { get; set; } = string.Empty;
        
        /// <summary>
        /// Enable GPU acceleration if available
        /// </summary>
        public bool EnableGpuAcceleration { get; set; } = true;
        
        /// <summary>
        /// Number of threads to use for inference
        /// </summary>
        public int ThreadCount { get; set; } = 4;
        
        /// <summary>
        /// Beam size for decoding
        /// </summary>
        public int BeamSize { get; set; } = 5;
        
        /// <summary>
        /// Best-of sampling parameter
        /// </summary>
        public int BestOf { get; set; } = 5;
        
        /// <summary>
        /// Temperature for sampling
        /// </summary>
        public float Temperature { get; set; } = 0.0f;
        
        /// <summary>
        /// Whether to use temperature fallback
        /// </summary>
        public bool UseTemperatureFallback { get; set; } = true;
        
        /// <summary>
        /// Language to use for transcription (null for auto-detect)
        /// </summary>
        public string? Language { get; set; }
        
        /// <summary>
        /// Whether to suppress non-speech tokens
        /// </summary>
        public bool SuppressNonSpeech { get; set; } = true;
        
        /// <summary>
        /// Whether to use VAD (Voice Activity Detection)
        /// </summary>
        public bool UseVad { get; set; } = true;
        
        /// <summary>
        /// VAD threshold (0.0 to 1.0)
        /// </summary>
        public float VadThreshold { get; set; } = 0.6f;
        
        /// <summary>
        /// Maximum context tokens
        /// </summary>
        public int MaxContextTokens { get; set; } = 224;
        
        /// <summary>
        /// When settings were last modified
        /// </summary>
        public DateTime LastModified { get; set; } = DateTime.UtcNow;
    }
    
    /// <summary>
    /// Result of local transcription
    /// </summary>
    public class LocalTranscriptionResult
    {
        /// <summary>
        /// Transcribed text
        /// </summary>
        public string Text { get; set; } = string.Empty;
        
        /// <summary>
        /// Language detected/used
        /// </summary>
        public string Language { get; set; } = "en";
        
        /// <summary>
        /// Confidence score (0.0 to 1.0)
        /// </summary>
        public float Confidence { get; set; }
        
        /// <summary>
        /// Individual segments with timing
        /// </summary>
        public List<TranscriptionSegment> Segments { get; set; } = new();
        
        /// <summary>
        /// Processing duration
        /// </summary>
        public TimeSpan ProcessingDuration { get; set; }
        
        /// <summary>
        /// Real-time factor (RTF) - duration of audio / processing time
        /// </summary>
        public double RealTimeFactor { get; set; }
        
        /// <summary>
        /// Whether the transcription used GPU acceleration
        /// </summary>
        public bool UsedGpu { get; set; }
        
        /// <summary>
        /// Model used for transcription
        /// </summary>
        public string ModelId { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the result is from cache
        /// </summary>
        public bool IsCached { get; set; }
    }
    
    /// <summary>
    /// Transcription segment with timing
    /// </summary>
    public class TranscriptionSegment
    {
        /// <summary>
        /// Segment text
        /// </summary>
        public string Text { get; set; } = string.Empty;
        
        /// <summary>
        /// Start time in seconds
        /// </summary>
        public float StartTime { get; set; }
        
        /// <summary>
        /// End time in seconds
        /// </summary>
        public float EndTime { get; set; }
        
        /// <summary>
        /// Confidence score
        /// </summary>
        public float Confidence { get; set; }
        
        /// <summary>
        /// Speaker ID (if speaker diarization enabled)
        /// </summary>
        public int? SpeakerId { get; set; }
    }
    
    /// <summary>
    /// Statistics for local inference usage
    /// </summary>
    public class LocalInferenceStatistics
    {
        /// <summary>
        /// Total transcriptions performed
        /// </summary>
        public int TotalTranscriptions { get; set; }
        
        /// <summary>
        /// Total audio duration processed
        /// </summary>
        public TimeSpan TotalAudioDuration { get; set; }
        
        /// <summary>
        /// Total processing time
        /// </summary>
        public TimeSpan TotalProcessingTime { get; set; }
        
        /// <summary>
        /// Average real-time factor (Audio Duration / Processing Time)
        /// </summary>
        public double AverageRealTimeFactor 
        { 
            get
            {
                if (TotalProcessingTime.TotalMilliseconds == 0) return 0;
                return TotalAudioDuration.TotalMilliseconds / TotalProcessingTime.TotalMilliseconds;
            }
            set { } // Keep setter for serialization compatibility
        }
        
        /// <summary>
        /// Number of failures
        /// </summary>
        public int FailureCount { get; set; }
        
        /// <summary>
        /// Cache hit rate (0.0 to 1.0)
        /// </summary>
        public double CacheHitRate { get; set; }
        
        /// <summary>
        /// When statistics were started
        /// </summary>
        public DateTime StartedAt { get; set; } = DateTime.UtcNow;
        
        /// <summary>
        /// Last transcription timestamp
        /// </summary>
        public DateTime? LastTranscriptionAt { get; set; }
    }
}
