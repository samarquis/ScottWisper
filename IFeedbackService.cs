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
        /// Notification types for toast notifications
        /// </summary>
        enum NotificationType
        {
            Info,
            Warning,
            Error,
            Completion,
            StatusChange
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
        /// Show toast notification with type
        /// </summary>
        /// <param name="title">Notification title</param>
        /// <param name="message">Notification message</param>
        /// <param name="type">Type of notification</param>
        Task ShowToastNotificationAsync(string title, string message, NotificationType type = NotificationType.Info);

        /// <summary>
        /// Clear any active status indicators
        /// </summary>
        Task ClearStatusIndicatorAsync();

        /// <summary>
        /// Initialize the feedback service
        /// </summary>
        Task InitializeAsync();

        /// <summary>
        /// Start progress indicator with title and timeout
        /// </summary>
        /// <param name="title">Progress title</param>
        /// <param name="timeout">Progress timeout</param>
        Task StartProgressAsync(string title, TimeSpan timeout);

        /// <summary>
        /// Update progress with percentage and message
        /// </summary>
        /// <param name="percentage">Progress percentage (0-100)</param>
        /// <param name="message">Progress message</param>
        Task UpdateProgressAsync(int percentage, string message);

        /// <summary>
        /// Complete progress with final message
        /// </summary>
        /// <param name="message">Completion message</param>
        Task CompleteProgressAsync(string message);

        /// <summary>
        /// Cleanup resources
        /// </summary>
        Task DisposeAsync();
    }
}