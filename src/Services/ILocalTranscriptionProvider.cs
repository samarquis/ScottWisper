using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Configuration;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for local transcription providers (Whisper, Vosk, etc.)
    /// </summary>
    public interface ILocalTranscriptionProvider : IDisposable
    {
        /// <summary>
        /// Provider name (e.g., "Whisper", "Vosk")
        /// </summary>
        string ProviderName { get; }
        
        /// <summary>
        /// Whether the provider is initialized and ready
        /// </summary>
        bool IsInitialized { get; }
        
        /// <summary>
        /// Current provider status
        /// </summary>
        LocalInferenceStatus Status { get; }
        
        /// <summary>
        /// Initialize the provider with a specific model
        /// </summary>
        Task<bool> InitializeAsync(string? modelPath = null);
        
        /// <summary>
        /// Transcribe audio data locally
        /// </summary>
        Task<LocalTranscriptionResult> TranscribeAudioAsync(byte[] audioData, string? language = null);
        
        /// <summary>
        /// Unload the current model and release resources
        /// </summary>
        void UnloadModel();
        
        /// <summary>
        /// Get available models for this provider
        /// </summary>
        Task<List<WhisperModelInfo>> GetAvailableModelsAsync();
        
        /// <summary>
        /// Get inference statistics
        /// </summary>
        Task<LocalInferenceStatistics> GetStatisticsAsync();
        
        /// <summary>
        /// Check if a specific model is available
        /// </summary>
        Task<bool> IsModelAvailableAsync(string modelId);
        
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
    /// Factory for creating local transcription providers
    /// </summary>
    public interface ILocalProviderFactory
    {
        /// <summary>
        /// Create a provider instance based on type
        /// </summary>
        ILocalTranscriptionProvider CreateProvider(LocalProviderType providerType);
        
        /// <summary>
        /// Get all supported provider types
        /// </summary>
        IEnumerable<LocalProviderType> GetSupportedProviders();
        
        /// <summary>
        /// Get provider metadata
        /// </summary>
        ProviderMetadata GetProviderMetadata(LocalProviderType providerType);
    }
    
    /// <summary>
    /// Metadata for a local transcription provider
    /// </summary>
    public class ProviderMetadata
    {
        /// <summary>
        /// Provider type
        /// </summary>
        public LocalProviderType Type { get; set; }
        
        /// <summary>
        /// Display name
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;
        
        /// <summary>
        /// Description of the provider
        /// </summary>
        public string Description { get; set; } = string.Empty;
        
        /// <summary>
        /// Whether the provider supports GPU acceleration
        /// </summary>
        public bool SupportsGpu { get; set; }
        
        /// <summary>
        /// Whether the provider supports multiple languages
        /// </summary>
        public bool SupportsMultipleLanguages { get; set; }
        
        /// <summary>
        /// Minimum RAM required in MB
        /// </summary>
        public int MinRamMb { get; set; }
        
        /// <summary>
        /// Relative accuracy score (0-1)
        /// </summary>
        public double AccuracyScore { get; set; }
        
        /// <summary>
        /// Relative speed score (0-1)
        /// </summary>
        public double SpeedScore { get; set; }
        
        /// <summary>
        /// Whether the provider is installed/available
        /// </summary>
        public bool IsAvailable { get; set; }
        
        /// <summary>
        /// Installation instructions if not available
        /// </summary>
        public string? InstallationInstructions { get; set; }
    }
}
