using System;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for cost tracking service
    /// </summary>
    public interface ICostTrackingService : IDisposable
    {
        /// <summary>
        /// Record usage from audio data
        /// </summary>
        void RecordUsage(byte[] audioData, TimeSpan duration);
        
        /// <summary>
        /// Get current usage statistics
        /// </summary>
        UsageStats GetCurrentStats();
        
        /// <summary>
        /// Reset usage statistics
        /// </summary>
        void ResetStats();
        
        /// <summary>
        /// Check if free tier has been exceeded
        /// </summary>
        bool IsFreeTierExceeded();
        
        /// <summary>
        /// Check if approaching free tier limit (80% threshold)
        /// </summary>
        bool IsApproachingFreeTierLimit();
        
        /// <summary>
        /// Get remaining free tier amount
        /// </summary>
        decimal GetRemainingFreeTier();
    }
}
