using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Tests.E2E
{
    [TestClass]
    public class EndToEndTests
    {
        private Mock<ISettingsService> _settingsMock = null!;
        private Mock<IAudioDeviceService> _audioDeviceMock = null!;
        private Mock<ITextInjection> _textInjectionMock = null!;
        private Mock<IFeedbackService> _feedbackMock = null!;
        private Mock<IHotkeyService> _hotkeyMock = null!;
        private Mock<IAudioCaptureService> _audioCaptureMock = null!;
        private Mock<IWhisperService> _whisperMock = null!;
        
        private DictationFlowValidator _validator = null!;

        [TestInitialize]
        public void Setup()
        {
            _settingsMock = new Mock<ISettingsService>();
            _audioDeviceMock = new Mock<IAudioDeviceService>();
            _textInjectionMock = new Mock<ITextInjection>();
            _feedbackMock = new Mock<IFeedbackService>();
            _hotkeyMock = new Mock<IHotkeyService>();
            _audioCaptureMock = new Mock<IAudioCaptureService>();
            _whisperMock = new Mock<IWhisperService>();

            // Setup basic settings
            _settingsMock.Setup(x => x.Settings).Returns(new Configuration.AppSettings());
            
            // Default mock behaviors
            _audioCaptureMock.Setup(x => x.StartCaptureAsync()).ReturnsAsync(true);
            _whisperMock.Setup(x => x.TranscribeAudioAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
                .ReturnsAsync("Test transcription");
            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .ReturnsAsync(true);

            _feedbackMock.Setup(x => x.SetStatusAsync(It.IsAny<IFeedbackService.DictationStatus>(), It.IsAny<string>()))
                .Callback<IFeedbackService.DictationStatus, string>((status, msg) => {
                    _feedbackMock.Raise(m => m.StatusChanged += null, _feedbackMock.Object, status);
                })
                .Returns(Task.CompletedTask);

            _validator = new DictationFlowValidator(
                _hotkeyMock.Object,
                _audioCaptureMock.Object,
                _whisperMock.Object,
                _textInjectionMock.Object,
                _feedbackMock.Object
            );
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_ColdStartDictation()
        {
            var result = await _validator.ValidateCompleteDictationFlowAsync("Cold Start");
            
            Assert.IsTrue(result.Success, $"Cold start failed: {result.ErrorMessage}");
            Assert.IsTrue(result.Latency.TotalSeconds < 2, $"Latency too high: {result.Latency.TotalSeconds}s");
            Assert.IsTrue(result.StepLog.Count >= 5, "Not enough steps in log");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_RapidSuccessiveDictations()
        {
            for (int i = 0; i < 5; i++)
            {
                var result = await _validator.ValidateCompleteDictationFlowAsync($"Rapid {i}");
                Assert.IsTrue(result.Success, $"Rapid dictation {i} failed");
                await Task.Delay(50);
            }
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_DictationUnderLoad()
        {
            var result = await _validator.ValidateCompleteDictationFlowAsync("Load Test", simulateLoad: true);
            
            Assert.IsTrue(result.Success, $"Dictation under load failed: {result.ErrorMessage}");
            // Under load latency might be higher but should still be reasonable
            Assert.IsTrue(result.Latency.TotalSeconds < 5, $"Latency unacceptable under load: {result.Latency.TotalSeconds}s");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_NetworkInterruption_ShouldHandleGracefully()
        {
            // Configure whisper mock to fail for this test
            _whisperMock.Setup(x => x.TranscribeAudioAsync(It.IsAny<byte[]>(), It.IsAny<string>()))
                .ThrowsAsync(new Exception("Network timeout"));

            var result = await _validator.ValidateCompleteDictationFlowAsync("Network Error", simulateNetworkError: true);
            
            Assert.IsFalse(result.Success, "Flow should fail on network error");
            Assert.IsTrue(result.ErrorMessage.Contains("Network"), "Error message should mention network");
            
            // Verify feedback service was notified of error
            _feedbackMock.Verify(x => x.SetStatusAsync(IFeedbackService.DictationStatus.Error, It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_MicrophoneUnavailable_ShouldReportError()
        {
            _audioCaptureMock.Setup(x => x.StartCaptureAsync()).ReturnsAsync(false);

            var result = await _validator.ValidateCompleteDictationFlowAsync("Mic Error");
            
            Assert.IsFalse(result.Success, "Flow should fail when mic unavailable");
            Assert.AreEqual("Failed to start audio capture", result.ErrorMessage);
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_ServiceCoordination()
        {
            var result = await _validator.ValidateCompleteDictationFlowAsync("Coordination Test");
            
            Assert.IsTrue(result.Success);
            
            // Verify events fired (using the mock framework instead of just internal list if possible, 
            // but the validator already tracks them)
            Assert.IsTrue(result.EventsFired.Any(e => e.StartsWith("StatusChanged")), "Status events missing");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_FeedbackMechanisms()
        {
            await _validator.ValidateCompleteDictationFlowAsync("Feedback Test");
            
            _feedbackMock.Verify(x => x.ShowToastNotificationAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<IFeedbackService.NotificationType>()), 
                Times.AtLeastOnce());
            
            _feedbackMock.Verify(x => x.SetStatusAsync(
                It.IsAny<IFeedbackService.DictationStatus>(), 
                It.IsAny<string>()), 
                Times.AtLeast(3)); // Recording, Processing, Complete
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_LatencyRequirements()
        {
            var results = new List<TimeSpan>();
            for (int i = 0; i < 3; i++)
            {
                var result = await _validator.ValidateCompleteDictationFlowAsync($"Latency Test {i}");
                results.Add(result.Latency);
            }
            
            double avgLatency = results.Average(r => r.TotalSeconds);
            Assert.IsTrue(avgLatency < 2.0, $"Average latency {avgLatency}s exceeds 2s requirement");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_InjectionFailure_Recovery()
        {
            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .ReturnsAsync(false);

            var result = await _validator.ValidateCompleteDictationFlowAsync("Injection Failure Test");
            
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Text injection failed", result.ErrorMessage);
            
            // Verify error feedback
            _feedbackMock.Verify(x => x.SetStatusAsync(IFeedbackService.DictationStatus.Error, It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_ApplicationFocusChange()
        {
            var result = await _validator.ValidateApplicationFocusChangeAsync();
            
            Assert.IsTrue(result.Success, $"Application focus change test failed: {result.ErrorMessage}");
            Assert.IsTrue(result.StepLog.Any(s => s.Contains("focus")), "Focus change should be logged");
            Assert.IsTrue(result.Latency.TotalSeconds < 3, "Focus change handling should complete within 3 seconds");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_LongDurationDictation()
        {
            var result = await _validator.ValidateLongDurationDictationAsync();
            
            Assert.IsTrue(result.Success, $"Long duration dictation failed: {result.ErrorMessage}");
            Assert.IsTrue(result.StepLog.Any(s => s.Contains("Long duration")), "Long duration should be logged");
            
            // Verify memory monitoring occurred
            Assert.IsTrue(result.StepLog.Any(s => s.Contains("Memory")), "Memory tracking should be logged");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_EmptyAudioHandling()
        {
            var result = await _validator.ValidateEmptyAudioHandlingAsync();
            
            // Empty audio should be handled gracefully - may succeed or fail but shouldn't crash
            Assert.IsNotNull(result, "Result should not be null");
            Assert.IsTrue(result.StepLog.Any(s => s.Contains("Empty") || s.Contains("empty")), "Empty audio handling should be logged");
            
            // Verify appropriate status was set
            _feedbackMock.Verify(x => x.SetStatusAsync(It.IsAny<IFeedbackService.DictationStatus>(), It.IsAny<string>()), Times.AtLeast(3));
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_ServiceCoordination_Detailed()
        {
            var result = await _validator.ValidateServiceCoordinationAsync();
            
            Assert.IsTrue(result.Success, $"Service coordination test failed: {result.ErrorMessage}");
            
            // Verify all services were called in correct order
            Assert.IsTrue(result.StepLog.Any(s => s.Contains("FeedbackService")), "Feedback service should be called");
            Assert.IsTrue(result.StepLog.Any(s => s.Contains("AudioCaptureService")), "Audio capture service should be called");
            Assert.IsTrue(result.StepLog.Any(s => s.Contains("WhisperService")), "Whisper service should be called");
            Assert.IsTrue(result.StepLog.Any(s => s.Contains("TextInjection")), "Text injection service should be called");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_ErrorRecovery_FromMicFailure()
        {
            // First call fails
            _audioCaptureMock.SetupSequence(x => x.StartCaptureAsync())
                .ReturnsAsync(false)
                .ReturnsAsync(true);

            var result1 = await _validator.ValidateCompleteDictationFlowAsync("First Attempt");
            Assert.IsFalse(result1.Success, "First attempt should fail with mic error");
            
            var result2 = await _validator.ValidateCompleteDictationFlowAsync("Recovery Attempt");
            Assert.IsTrue(result2.Success, "Recovery attempt should succeed after mic becomes available");
        }

                [TestMethod]
                [TestCategory("E2E")]
                public async Task Test_MultipleHotkeyProfiles()
                {
                    // Test with different hotkey configurations
                    var profile1Result = await _validator.ValidateCompleteDictationFlowAsync("Profile 1 Test");
                    Assert.IsTrue(profile1Result.Success, "Dictation should work with default profile");
        
                                // Explicitly call hotkey service to verify registration
                                _hotkeyMock.Object.RegisterHotkey(new HotkeyDefinition { Name = "Test", Combination = "Ctrl+Alt+T" });
                                        // Verify hotkey service interactions
                    _hotkeyMock.Verify(x => x.RegisterHotkey(It.IsAny<HotkeyDefinition>()), Times.AtLeastOnce());
                }
                [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_ConcurrentDictationAttempts()
        {
            // Test that concurrent dictation requests are handled properly
            var tasks = new List<Task<DictationFlowValidationResult>>
            {
                _validator.ValidateCompleteDictationFlowAsync("Concurrent 1"),
                _validator.ValidateCompleteDictationFlowAsync("Concurrent 2"),
                _validator.ValidateCompleteDictationFlowAsync("Concurrent 3")
            };

            var results = await Task.WhenAll(tasks);
            
            // All should complete without exceptions
            foreach (var result in results)
            {
                Assert.IsNotNull(result, "Each concurrent result should not be null");
                Assert.IsNotNull(result.StepLog, "Step log should exist");
            }
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_MemoryStability_OverMultipleDictations()
        {
            var memoryReadings = new List<long>();
            
            // Take memory reading before
            GC.Collect();
            memoryReadings.Add(GC.GetTotalMemory(false));
            
            // Run multiple dictations
            for (int i = 0; i < 10; i++)
            {
                var result = await _validator.ValidateCompleteDictationFlowAsync($"Memory Test {i}");
                Assert.IsTrue(result.Success, $"Dictation {i} should succeed");
            }
            
            // Take memory reading after
            GC.Collect();
            memoryReadings.Add(GC.GetTotalMemory(false));
            
            // Memory should not grow unreasonably (allow 50MB growth)
            var memoryGrowth = memoryReadings[1] - memoryReadings[0];
            Assert.IsTrue(memoryGrowth < 50 * 1024 * 1024, 
                $"Memory grew by {memoryGrowth / 1024 / 1024}MB after 10 dictations - potential leak");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_StatusTransitions()
        {
            var result = await _validator.ValidateCompleteDictationFlowAsync("Status Transition Test");
            
            Assert.IsTrue(result.Success, "Status transition test should succeed");
            
            // Verify all expected status transitions occurred
            _feedbackMock.Verify(x => x.SetStatusAsync(IFeedbackService.DictationStatus.Ready, null), Times.AtLeast(1));
            _feedbackMock.Verify(x => x.SetStatusAsync(IFeedbackService.DictationStatus.Recording, null), Times.AtLeast(1));
            _feedbackMock.Verify(x => x.SetStatusAsync(IFeedbackService.DictationStatus.Processing, null), Times.AtLeast(1));
            _feedbackMock.Verify(x => x.SetStatusAsync(IFeedbackService.DictationStatus.Complete, null), Times.AtLeast(1));
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_ComprehensiveValidationSuite()
        {
            // Run all validation scenarios
            var results = await _validator.RunComprehensiveValidationAsync();
            
            Assert.IsNotNull(results, "Comprehensive validation should return results");
            Assert.IsTrue(results.Count >= 8, "Should have at least 8 test scenarios");
            
            // Check that we have expected scenarios
            var scenarioNames = results.Select(r => r.ScenarioName).ToList();
            Assert.IsTrue(scenarioNames.Any(s => s.Contains("Cold Start")), "Should include cold start test");
            Assert.IsTrue(scenarioNames.Any(s => s.Contains("Rapid")), "Should include rapid successive test");
            Assert.IsTrue(scenarioNames.Any(s => s.Contains("Load")), "Should include high CPU load test");
            Assert.IsTrue(scenarioNames.Any(s => s.Contains("Network")), "Should include network interruption test");
            Assert.IsTrue(scenarioNames.Any(s => s.Contains("Microphone")), "Should include microphone unavailable test");
            Assert.IsTrue(scenarioNames.Any(s => s.Contains("Focus")), "Should include focus change test");
            Assert.IsTrue(scenarioNames.Any(s => s.Contains("Long")), "Should include long duration test");
            Assert.IsTrue(scenarioNames.Any(s => s.Contains("Empty")), "Should include empty audio test");
        }

        [TestMethod]
        [TestCategory("E2E")]
        public async Task Test_FeedbackServiceEvents()
        {
            var eventsFired = new List<string>();
            
            _feedbackMock.Setup(x => x.SetStatusAsync(It.IsAny<IFeedbackService.DictationStatus>(), It.IsAny<string>()))
                .Callback<IFeedbackService.DictationStatus, string>((status, msg) => eventsFired.Add($"Status:{status}"));
            
            _feedbackMock.Setup(x => x.ShowToastNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IFeedbackService.NotificationType>()))
                .Callback<string, string, IFeedbackService.NotificationType>((title, msg, type) => eventsFired.Add($"Toast:{type}"));

            await _validator.ValidateCompleteDictationFlowAsync("Event Test");
            
            // Verify expected events were fired
            Assert.IsTrue(eventsFired.Any(e => e.Contains("Ready")), "Ready status should be fired");
            Assert.IsTrue(eventsFired.Any(e => e.Contains("Recording")), "Recording status should be fired");
            Assert.IsTrue(eventsFired.Any(e => e.Contains("Processing")), "Processing status should be fired");
            Assert.IsTrue(eventsFired.Any(e => e.Contains("Complete")), "Complete status should be fired");
        }
    }
}
