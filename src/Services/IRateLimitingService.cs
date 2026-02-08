using System;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for managing adaptive rate limiting and resource protection
    /// </summary>
    public interface IRateLimitingService
    {
        /// <summary>
        /// Attempts to consume a quota for a specific resource
        /// </summary>
        /// <param name="resourceName">Name of the protected resource</param>
        /// <returns>True if allowed, false if throttled</returns>
        bool TryConsume(string resourceName);
        
        /// <summary>
        /// Gets the time until the next quota becomes available
        /// </summary>
        TimeSpan GetWaitTime(string resourceName);
        
        /// <summary>
        /// Dynamically adjusts limits based on system load or external signals
        /// </summary>
        void AdjustLimits(double scalingFactor);
        
        /// <summary>
        /// Resets all limits to their baseline values
        /// </summary>
        void ResetAll();
    }
}
