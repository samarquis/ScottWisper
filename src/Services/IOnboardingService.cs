using System;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for managing user onboarding and interactive guides
    /// </summary>
    public interface IOnboardingService
    {
        /// <summary>
        /// Gets the current onboarding state for the user
        /// </summary>
        Task<OnboardingState> GetStateAsync();
        
        /// <summary>
        /// Marks a specific onboarding module as completed
        /// </summary>
        Task CompleteModuleAsync(string moduleName);
        
        /// <summary>
        /// Starts the welcome walkthrough if not already completed
        /// </summary>
        Task StartWelcomeAsync();
        
        /// <summary>
        /// Checks if any onboarding is required
        /// </summary>
        bool IsOnboardingRequired();
    }
}
