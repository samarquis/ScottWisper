using System;
using System.Threading.Tasks;

namespace ScottWisper
{
    /// <summary>
    /// Defines feedback service for providing audio and visual status notifications
    /// </summary>
    public interface IFeedbackService
    {
        /// <summary>
        /// Dictation status states
        /// </summary>
        enum DictationStatus
        {
            Idle,
            Ready,
            Recording,
            Processing,
            Complete,
            Error
        }

        /// <summary>
        /// Current dictation status
        /// </summary>
        DictationStatus CurrentStatus { get; }

        /// <summary>
        /// Event triggered when status changes
        /// </summary>
        event EventHandler<DictationStatus>? StatusChanged;

        /// <summary>
        /// Update dictation status with optional message
        /// </summary>
        /// <param name="status">New status</param>
        /// <param name="message">Optional status message</param>
        Task SetStatusAsync(DictationStatus status, string? message = null);

        /// <summary>
        /// Show visual notification to user
        /// </summary>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        /// <param name="duration">Display duration in milliseconds</param>
        Task ShowNotificationAsync(string title, string message, int duration = 3000);

        /// <summary>
        /// Play audio feedback for status change
        /// </summary>
        /// <param name="status">Status to play sound for</param>
        Task PlayAudioFeedbackAsync(DictationStatus status);

        /// <summary>
        /// Show brief status indicator
        /// </summary>
        /// <param name="status">Status to indicate</param>
        /// <param name="duration">How long to show indicator</param>
        Task ShowStatusIndicatorAsync(DictationStatus status, int duration = 2000);

        /// <summary>
        /// Clear any active status indicators
        /// </summary>
        Task ClearStatusIndicatorAsync();

        /// <summary>
        /// Initialize the feedback service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Cleanup resources
        /// </summary>
        Task DisposeAsync();
    }
}