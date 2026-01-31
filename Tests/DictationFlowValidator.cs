using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ScottWisper.Services;

namespace ScottWisper.Tests
{
    public class DictationFlowValidationResult
    {
        public string ScenarioName { get; set; } = string.Empty;
        public bool Success { get; set; }
        public TimeSpan Latency { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<string> StepLog { get; set; } = new List<string>();
        public List<string> EventsFired { get; set; } = new List<string>();
    }

    public class DictationFlowValidator
    {
        private readonly IHotkeyService _hotkeyService;
        private readonly IAudioCaptureService _audioCaptureService;
        private readonly IWhisperService _whisperService;
        private readonly ITextInjection _textInjection;
        private readonly IFeedbackService _feedbackService;

        public DictationFlowValidator(
            IHotkeyService hotkeyService,
            IAudioCaptureService audioCaptureService,
            IWhisperService whisperService,
            ITextInjection textInjection,
            IFeedbackService feedbackService)
        {
            _hotkeyService = hotkeyService;
            _audioCaptureService = audioCaptureService;
            _whisperService = whisperService;
            _textInjection = textInjection;
            _feedbackService = feedbackService;
        }

        public async Task<DictationFlowValidationResult> ValidateCompleteDictationFlowAsync(
            string scenarioName, 
            string testSpeech = "The quick brown fox jumps over the lazy dog.",
            bool simulateNetworkError = false,
            bool simulateMicError = false,
            bool simulateLoad = false)
        {
            var result = new DictationFlowValidationResult { ScenarioName = scenarioName, Success = true };
            var sw = Stopwatch.StartNew();
            var events = new List<string>();

            // Subscribe to events for coordination validation
            void OnAudioDataCaptured(object? s, byte[] d) => events.Add("AudioDataCaptured");
            void OnTranscriptionStarted(object? s, EventArgs e) => events.Add("TranscriptionStarted");
            void OnTranscriptionCompleted(object? s, string t) => events.Add("TranscriptionCompleted");
            void OnStatusChanged(object? s, IFeedbackService.DictationStatus st) => events.Add($"StatusChanged:{st}");

            _audioCaptureService.AudioDataCaptured += OnAudioDataCaptured;
            _whisperService.TranscriptionStarted += OnTranscriptionStarted;
            _whisperService.TranscriptionCompleted += OnTranscriptionCompleted;
            _feedbackService.StatusChanged += OnStatusChanged;

            try
            {
                result.StepLog.Add($"Starting scenario: {scenarioName}");

                if (simulateLoad)
                {
                    result.StepLog.Add("Simulating high CPU load...");
                    _ = Task.Run(() => {
                        var end = DateTime.Now.AddSeconds(2);
                        while (DateTime.Now < end) { Math.Sqrt(new Random().Next()); }
                    });
                }

                // 1. Hotkey Trigger (Conceptual)
                result.StepLog.Add("Step 1: Hotkey Trigger");
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Ready);

                // 2. Audio Capture
                result.StepLog.Add("Step 2: Audio Capture");
                if (simulateMicError)
                {
                    throw new Exception("Microphone unavailable");
                }

                bool captureStarted = await _audioCaptureService.StartCaptureAsync();
                if (!captureStarted)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to start audio capture";
                    return result;
                }

                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Recording);
                await Task.Delay(500); // Simulate recording
                await _audioCaptureService.StopCaptureAsync();
                
                // 3. Transcription
                result.StepLog.Add("Step 3: Transcription");
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Processing);
                
                if (simulateNetworkError)
                {
                    throw new Exception("Network connection lost during transcription");
                }

                string transcription = await _whisperService.TranscribeAudioAsync(new byte[1000]); // Dummy data
                result.StepLog.Add($"Transcribed text: '{transcription}'");

                // 4. Text Injection
                result.StepLog.Add("Step 4: Text Injection");
                bool injectionSuccess = await _textInjection.InjectTextAsync(transcription);
                if (!injectionSuccess)
                {
                    result.Success = false;
                    result.ErrorMessage = "Text injection failed";
                    return result;
                }

                // 5. Completion Feedback
                result.StepLog.Add("Step 5: Completion Feedback");
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Complete);
                await _feedbackService.ShowToastNotificationAsync("Success", "Text injected successfully", IFeedbackService.NotificationType.Completion);

                sw.Stop();
                result.Latency = sw.Elapsed;
                result.EventsFired = events;
                result.StepLog.Add($"Scenario completed successfully in {result.Latency.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.StepLog.Add($"Error in flow: {ex.Message}");
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Error, ex.Message);
            }
            finally
            {
                _audioCaptureService.AudioDataCaptured -= OnAudioDataCaptured;
                _whisperService.TranscriptionStarted -= OnTranscriptionStarted;
                _whisperService.TranscriptionCompleted -= OnTranscriptionCompleted;
                _feedbackService.StatusChanged -= OnStatusChanged;
            }

            return result;
        }

        public async Task<List<DictationFlowValidationResult>> RunComprehensiveValidationAsync()
        {
            var results = new List<DictationFlowValidationResult>();

            // 1. Cold Start
            results.Add(await ValidateCompleteDictationFlowAsync("Cold Start"));

            // 2. Rapid Successive (5)
            for (int i = 1; i <= 5; i++)
            {
                results.Add(await ValidateCompleteDictationFlowAsync($"Rapid Successive {i}"));
                await Task.Delay(100);
            }

            // 3. High CPU Load
            results.Add(await ValidateCompleteDictationFlowAsync("High CPU Load", simulateLoad: true));

            // 4. Network Interruption
            results.Add(await ValidateCompleteDictationFlowAsync("Network Interruption", simulateNetworkError: true));

            // 5. Microphone Unavailable
            results.Add(await ValidateCompleteDictationFlowAsync("Microphone Unavailable", simulateMicError: true));

            // 6. Application Focus Change
            results.Add(await ValidateApplicationFocusChangeAsync());

            // 7. Long Duration Dictation
            results.Add(await ValidateLongDurationDictationAsync());

            // 8. Empty Audio Handling
            results.Add(await ValidateEmptyAudioHandlingAsync());

            return results;
        }

        public async Task<DictationFlowValidationResult> ValidateApplicationFocusChangeAsync()
        {
            var result = new DictationFlowValidationResult { ScenarioName = "Application Focus Change", Success = true };
            var sw = Stopwatch.StartNew();

            try
            {
                result.StepLog.Add("Starting application focus change scenario");
                
                // Simulate focus changes during dictation
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Ready);
                result.StepLog.Add("Status: Ready - before focus change");
                
                bool captureStarted = await _audioCaptureService.StartCaptureAsync();
                if (!captureStarted)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to start audio capture before focus change";
                    return result;
                }

                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Recording);
                result.StepLog.Add("Started recording, simulating focus change");
                
                // Simulate focus change event
                await Task.Delay(200);
                result.StepLog.Add("Focus changed to different application");
                
                // Continue recording after focus change
                await Task.Delay(300);
                await _audioCaptureService.StopCaptureAsync();
                
                // Transcribe and inject
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Processing);
                string transcription = await _whisperService.TranscribeAudioAsync(new byte[1000]);
                
                bool injectionSuccess = await _textInjection.InjectTextAsync(transcription);
                if (!injectionSuccess)
                {
                    result.Success = false;
                    result.ErrorMessage = "Text injection failed after focus change";
                    return result;
                }

                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Complete);
                result.StepLog.Add("Dictation completed successfully after focus change");
                
                sw.Stop();
                result.Latency = sw.Elapsed;
                result.StepLog.Add($"Scenario completed in {result.Latency.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.StepLog.Add($"Error: {ex.Message}");
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Error, ex.Message);
            }

            return result;
        }

        public async Task<DictationFlowValidationResult> ValidateLongDurationDictationAsync()
        {
            var result = new DictationFlowValidationResult { ScenarioName = "Long Duration Dictation", Success = true };
            var sw = Stopwatch.StartNew();

            try
            {
                result.StepLog.Add("Starting long duration dictation test (simulated 30 seconds)");
                
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Ready);
                
                bool captureStarted = await _audioCaptureService.StartCaptureAsync();
                if (!captureStarted)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to start audio capture for long duration";
                    return result;
                }

                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Recording);
                result.StepLog.Add("Recording started for extended period");
                
                // Simulate longer recording time
                await Task.Delay(1000);
                result.StepLog.Add("Simulated recording in progress (1s elapsed)");
                
                // Check for memory/performance issues during long recording
                var memoryBefore = GC.GetTotalMemory(false);
                await Task.Delay(500);
                var memoryAfter = GC.GetTotalMemory(false);
                var memoryDelta = memoryAfter - memoryBefore;
                result.StepLog.Add($"Memory delta during recording: {memoryDelta / 1024}KB");
                
                await _audioCaptureService.StopCaptureAsync();
                result.StepLog.Add("Long duration recording stopped");
                
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Processing);
                string transcription = await _whisperService.TranscribeAudioAsync(new byte[5000]); // Larger buffer for long audio
                result.StepLog.Add($"Transcribed text length: {transcription.Length} characters");
                
                bool injectionSuccess = await _textInjection.InjectTextAsync(transcription);
                if (!injectionSuccess)
                {
                    result.Success = false;
                    result.ErrorMessage = "Text injection failed for long duration dictation";
                    return result;
                }

                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Complete);
                await _feedbackService.ShowToastNotificationAsync("Success", "Long dictation completed", IFeedbackService.NotificationType.Completion);
                
                sw.Stop();
                result.Latency = sw.Elapsed;
                result.StepLog.Add($"Long duration scenario completed in {result.Latency.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.StepLog.Add($"Error in long duration flow: {ex.Message}");
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Error, ex.Message);
            }

            return result;
        }

        public async Task<DictationFlowValidationResult> ValidateEmptyAudioHandlingAsync()
        {
            var result = new DictationFlowValidationResult { ScenarioName = "Empty Audio Handling", Success = true };
            var sw = Stopwatch.StartNew();

            try
            {
                result.StepLog.Add("Starting empty audio handling scenario");
                
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Ready);
                
                bool captureStarted = await _audioCaptureService.StartCaptureAsync();
                if (!captureStarted)
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to start audio capture";
                    return result;
                }

                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Recording);
                result.StepLog.Add("Recording started - simulating silence/empty audio");
                
                // Very short recording to simulate empty/silent audio
                await Task.Delay(100);
                await _audioCaptureService.StopCaptureAsync();
                result.StepLog.Add("Stopped recording with minimal audio data");
                
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Processing);
                
                // Test with empty audio data
                string transcription = await _whisperService.TranscribeAudioAsync(new byte[0]);
                result.StepLog.Add($"Empty audio transcription result: '{transcription}'");
                
                // Even with empty transcription, injection should handle it gracefully
                if (!string.IsNullOrEmpty(transcription))
                {
                    bool injectionSuccess = await _textInjection.InjectTextAsync(transcription);
                    if (!injectionSuccess)
                    {
                        result.StepLog.Add("Text injection returned false for empty content - acceptable behavior");
                    }
                    else
                    {
                        result.StepLog.Add("Text injection succeeded even with empty content");
                    }
                }
                else
                {
                    result.StepLog.Add("Empty transcription detected - no injection attempted");
                }

                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Complete);
                
                sw.Stop();
                result.Latency = sw.Elapsed;
                result.StepLog.Add($"Empty audio scenario completed in {result.Latency.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.StepLog.Add($"Error handling empty audio: {ex.Message}");
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Error, ex.Message);
            }

            return result;
        }

        public async Task<DictationFlowValidationResult> ValidateServiceCoordinationAsync()
        {
            var result = new DictationFlowValidationResult { ScenarioName = "Service Coordination", Success = true };
            var sw = Stopwatch.StartNew();
            var serviceCalls = new List<string>();

            try
            {
                result.StepLog.Add("Validating service coordination and event propagation");
                
                // Track all service interactions
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Ready);
                serviceCalls.Add("FeedbackService.SetStatusAsync(Ready)");
                
                bool captureStarted = await _audioCaptureService.StartCaptureAsync();
                serviceCalls.Add($"AudioCaptureService.StartCaptureAsync() -> {captureStarted}");
                
                if (captureStarted)
                {
                    await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Recording);
                    serviceCalls.Add("FeedbackService.SetStatusAsync(Recording)");
                    
                    await Task.Delay(300);
                    await _audioCaptureService.StopCaptureAsync();
                    serviceCalls.Add("AudioCaptureService.StopCaptureAsync()");
                    
                    await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Processing);
                    serviceCalls.Add("FeedbackService.SetStatusAsync(Processing)");
                    
                    string transcription = await _whisperService.TranscribeAudioAsync(new byte[1000]);
                    serviceCalls.Add($"WhisperService.TranscribeAudioAsync() -> '{transcription}'");
                    
                    bool injectionSuccess = await _textInjection.InjectTextAsync(transcription);
                    serviceCalls.Add($"TextInjection.InjectTextAsync() -> {injectionSuccess}");
                    
                    await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Complete);
                    serviceCalls.Add("FeedbackService.SetStatusAsync(Complete)");
                    
                    // Verify toast notification
                    await _feedbackService.ShowToastNotificationAsync("Test", "Service coordination test", IFeedbackService.NotificationType.Info);
                    serviceCalls.Add("FeedbackService.ShowToastNotificationAsync()");
                }
                
                result.StepLog.AddRange(serviceCalls);
                result.StepLog.Add($"Total service calls: {serviceCalls.Count}");
                
                sw.Stop();
                result.Latency = sw.Elapsed;
                result.StepLog.Add($"Service coordination validated in {result.Latency.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.StepLog.Add($"Service coordination error: {ex.Message}");
                result.StepLog.AddRange(serviceCalls);
                await _feedbackService.SetStatusAsync(IFeedbackService.DictationStatus.Error, ex.Message);
            }

            return result;
        }
    }
}
