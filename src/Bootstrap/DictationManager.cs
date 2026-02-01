using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ScottWisper.Services;

namespace ScottWisper.Bootstrap
{
    /// <summary>
    /// Manages dictation state and operations
    /// </summary>
    public class DictationManager
    {
        private readonly ApplicationBootstrapper _bootstrapper;
        private bool _isDictating = false;
        private readonly object _dictationLock = new object();
        
        public bool IsDictating => _isDictating;
        
        public DictationManager(ApplicationBootstrapper bootstrapper)
        {
            _bootstrapper = bootstrapper ?? throw new ArgumentNullException(nameof(bootstrapper));
        }
        
        /// <summary>
        /// Toggles dictation state (start if stopped, stop if running)
        /// </summary>
        public async Task ToggleAsync()
        {
            Task dictationTask;
            lock (_dictationLock)
            {
                if (_isDictating)
                {
                    dictationTask = StopAsync();
                }
                else
                {
                    dictationTask = StartAsync();
                }
            }
            await dictationTask;
        }
        
        /// <summary>
        /// Starts dictation
        /// </summary>
        public async Task StartAsync()
        {
            if (_isDictating)
                return;
                
            try
            {
                var feedbackService = _bootstrapper.FeedbackService;
                
                // Update enhanced feedback to recording state
                if (feedbackService != null)
                {
                    await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Recording, "Recording started - Speak clearly");
                }
                else if (_bootstrapper.SystemTrayService != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _bootstrapper.SystemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Recording);
                    });
                }
                
                // Show transcription window
                if (_bootstrapper.TranscriptionWindow != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _bootstrapper.TranscriptionWindow.ShowForDictation();
                    });
                }
                
                // Start audio capture with progress feedback
                if (feedbackService != null)
                {
                    await feedbackService.StartProgressAsync("Recording", TimeSpan.FromMinutes(30));
                }
                
                // Start audio capture
                if (_bootstrapper.AudioCaptureService != null)
                {
                    await _bootstrapper.AudioCaptureService.StartRecordingAsync();
                }
                
                lock (_dictationLock)
                {
                    _isDictating = true;
                }
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("Failed to start dictation", ex);
            }
        }
        
        /// <summary>
        /// Stops dictation
        /// </summary>
        public async Task StopAsync()
        {
            if (!_isDictating)
                return;
                
            try
            {
                var feedbackService = _bootstrapper.FeedbackService;
                
                // Stop progress
                if (feedbackService != null)
                {
                    await feedbackService.StopProgressAsync();
                }
                
                // Stop audio capture
                if (_bootstrapper.AudioCaptureService != null)
                {
                    await _bootstrapper.AudioCaptureService.StopRecordingAsync();
                }
                
                // Update feedback status
                if (feedbackService != null)
                {
                    await feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Idle, "Ready");
                }
                else if (_bootstrapper.SystemTrayService != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _bootstrapper.SystemTrayService.UpdateStatus(SystemTrayService.TrayStatus.Ready);
                    });
                }
                
                // Hide transcription window
                if (_bootstrapper.TranscriptionWindow != null)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        _bootstrapper.TranscriptionWindow.Hide();
                    });
                }
                
                lock (_dictationLock)
                {
                    _isDictating = false;
                }
            }
            catch (Exception ex)
            {
                await HandleErrorAsync("Failed to stop dictation", ex);
            }
        }
        
        private async Task HandleErrorAsync(string message, Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"{message}: {ex.Message}");
            
            if (_bootstrapper.FeedbackService != null)
            {
                await _bootstrapper.FeedbackService.ShowToastNotificationAsync(
                    "Dictation Error",
                    $"{message}: {ex.Message}",
                    IFeedbackService.NotificationType.Error
                );
            }
            else
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    MessageBox.Show($"{message}: {ex.Message}", "ScottWisper Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                });
            }
        }
    }
}
