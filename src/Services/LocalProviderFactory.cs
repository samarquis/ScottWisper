using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using WhisperKey.Configuration;

namespace WhisperKey.Services
{
    /// <summary>
    /// Factory for creating local transcription providers
    /// </summary>
    public class LocalProviderFactory : ILocalProviderFactory
    {
        private readonly ISettingsService _settingsService;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly Dictionary<LocalProviderType, ILocalTranscriptionProvider> _providerCache = new();
        
        public LocalProviderFactory(
            ISettingsService settingsService,
            ILoggerFactory? loggerFactory = null)
        {
            _settingsService = settingsService;
            _loggerFactory = loggerFactory;
        }
        
        public ILocalTranscriptionProvider CreateProvider(LocalProviderType providerType)
        {
            // Check if we have a cached instance
            if (_providerCache.TryGetValue(providerType, out var cachedProvider))
            {
                return cachedProvider;
            }
            
            // Create new instance
            ILocalTranscriptionProvider provider = providerType switch
            {
                LocalProviderType.Whisper => new LocalInferenceService(
                    _settingsService,
                    new ModelManagerService(
                        _loggerFactory?.CreateLogger<ModelManagerService>() ?? 
                        new Microsoft.Extensions.Logging.Abstractions.NullLogger<ModelManagerService>()),
                    _loggerFactory?.CreateLogger<LocalInferenceService>()),
                    
                LocalProviderType.Vosk => new VoskTranscriptionProvider(
                    _settingsService,
                    _loggerFactory?.CreateLogger<VoskTranscriptionProvider>()),
                    
                _ => throw new ArgumentException($"Unknown provider type: {providerType}")
            };
            
            // Cache the provider
            _providerCache[providerType] = provider;
            
            return provider;
        }
        
        public IEnumerable<LocalProviderType> GetSupportedProviders()
        {
            var providers = new List<LocalProviderType>
            {
                LocalProviderType.Whisper
            };
            
            // Check if Vosk is available
            try
            {
                System.Reflection.Assembly.Load("Vosk");
                providers.Add(LocalProviderType.Vosk);
            }
            catch
            {
                // Vosk not available
            }
            
            return providers;
        }
        
        public ProviderMetadata GetProviderMetadata(LocalProviderType providerType)
        {
            return providerType switch
            {
                LocalProviderType.Whisper => new ProviderMetadata
                {
                    Type = LocalProviderType.Whisper,
                    DisplayName = "Whisper",
                    Description = "OpenAI Whisper - High accuracy offline speech recognition with support for 99 languages",
                    SupportsGpu = true,
                    SupportsMultipleLanguages = true,
                    MinRamMb = 512,
                    AccuracyScore = 0.95,
                    SpeedScore = 0.7,
                    IsAvailable = true
                },
                
                LocalProviderType.Vosk => new ProviderMetadata
                {
                    Type = LocalProviderType.Vosk,
                    DisplayName = "Vosk",
                    Description = "Vosk - Lightweight offline speech recognition with streaming support for 20+ languages",
                    SupportsGpu = false,
                    SupportsMultipleLanguages = true,
                    MinRamMb = 256,
                    AccuracyScore = 0.80,
                    SpeedScore = 1.2,
                    IsAvailable = IsVoskAvailable(),
                    InstallationInstructions = "Download Vosk from https://alphacephei.com/vosk/ and models from https://alphacephei.com/vosk/models"
                },
                
                _ => throw new ArgumentException($"Unknown provider type: {providerType}")
            };
        }
        
        private bool IsVoskAvailable()
        {
            try
            {
                // Try to load Vosk assembly
                var assembly = System.Reflection.Assembly.Load("Vosk");
                return assembly != null;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Get the default provider based on availability
        /// </summary>
        public LocalProviderType GetDefaultProvider()
        {
            // Prefer Whisper if available, fall back to Vosk
            if (IsProviderAvailable(LocalProviderType.Whisper))
            {
                return LocalProviderType.Whisper;
            }
            
            if (IsProviderAvailable(LocalProviderType.Vosk))
            {
                return LocalProviderType.Vosk;
            }
            
            // Default to Whisper even if not available (will show error when used)
            return LocalProviderType.Whisper;
        }
        
        /// <summary>
        /// Check if a specific provider is available
        /// </summary>
        public bool IsProviderAvailable(LocalProviderType providerType)
        {
            var metadata = GetProviderMetadata(providerType);
            return metadata.IsAvailable;
        }
        
        /// <summary>
        /// Clear the provider cache and dispose cached instances
        /// </summary>
        public void ClearCache()
        {
            foreach (var provider in _providerCache.Values)
            {
                provider?.Dispose();
            }
            _providerCache.Clear();
        }
    }
}
