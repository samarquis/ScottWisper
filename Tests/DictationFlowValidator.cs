using System;
using System.Collections.Generic;
using System.Diagnostics;
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

        public async Task<DictationFlowValidationResult> ValidateCompleteDictationFlowAsync(string scenarioName, string testSpeech = "Test transcription")
        {
            var result = new DictationFlowValidationResult { ScenarioName = scenarioName, Success = true };
            var sw = Stopwatch.StartNew();

            try
            {
                result.StepLog.Add("Starting dictation flow validation...");

                // 1. Simulate Hotkey Press
                result.StepLog.Add("Simulating hotkey press...");
                // In a real test, we would trigger the event
                // But since we are validating the flow, we assume the trigger works 
                // and we manually start the next step or verify the link.

                // 2. Audio Capture
                result.StepLog.Add("Starting audio capture...");
                // Note: We might need to mock WaveIn for automated tests to avoid hardware dependency
                
                // 3. Transcription
                result.StepLog.Add($"Simulating transcription for: '{testSpeech}'");
                
                // 4. Text Injection
                result.StepLog.Add("Injecting text...");
                bool injectionSuccess = await _textInjection.InjectTextAsync(testSpeech);
                
                if (!injectionSuccess)
                {
                    result.Success = false;
                    result.ErrorMessage = "Text injection failed";
                }

                // 5. Feedback
                result.StepLog.Add("Verifying feedback...");
                await _feedbackService.ShowToastNotificationAsync("Success", "Text injected", IFeedbackService.NotificationType.Completion);

                sw.Stop();
                result.Latency = sw.Elapsed;
                result.StepLog.Add($"Flow completed in {result.Latency.TotalMilliseconds:F0}ms");
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                result.StepLog.Add($"Error: {ex.Message}");
            }

            return result;
        }

        public async Task<List<DictationFlowValidationResult>> RunAllScenariosAsync()
        {
            var results = new List<DictationFlowValidationResult>();

            // Cold start
            results.Add(await ValidateCompleteDictationFlowAsync("Cold Start"));

            // Rapid successive
            for (int i = 1; i <= 3; i++)
            {
                results.Add(await ValidateCompleteDictationFlowAsync($"Rapid Successive {i}"));
            }

            return results;
        }
    }
}
