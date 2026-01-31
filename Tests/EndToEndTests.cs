using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ScottWisper.Services;

namespace ScottWisper.Tests
{
    [TestClass]
    public class EndToEndTests
    {
        private Mock<ISettingsService> _settingsMock = null!;
        private Mock<IAudioDeviceService> _audioDeviceMock = null!;
        private Mock<ITextInjection> _textInjectionMock = null!;
        private Mock<IFeedbackService> _feedbackMock = null!;
        
        private HotkeyService _hotkeyService = null!;
        private AudioCaptureService _audioCaptureService = null!;
        private WhisperService _whisperService = null!;
        private DictationFlowValidator _validator = null!;

        [TestInitialize]
        public void Setup()
        {
            _settingsMock = new Mock<ISettingsService>();
            _audioDeviceMock = new Mock<IAudioDeviceService>();
            _textInjectionMock = new Mock<ITextInjection>();
            _feedbackMock = new Mock<IFeedbackService>();

            // Setup basic settings
            _settingsMock.Setup(x => x.Settings).Returns(new Configuration.AppSettings());

            // Initialize services (using real classes but mocked dependencies where possible)
            // Note: HotkeyService and AudioCaptureService might be hard to test fully without HW/OS hooks
            // For E2E validation, we focus on the coordination logic.
            
            _hotkeyService = new HotkeyService(_settingsMock.Object);
            _audioCaptureService = new AudioCaptureService(_settingsMock.Object, _audioDeviceMock.Object);
            _whisperService = new WhisperService(_settingsMock.Object);
            
            _validator = new DictationFlowValidator(
                _hotkeyService,
                _audioCaptureService,
                _whisperService,
                _textInjectionMock.Object,
                _feedbackMock.Object
            );
        }

        [TestMethod]
        public async Task Test_ColdStartDictation()
        {
            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .ReturnsAsync(true);

            var result = await _validator.ValidateCompleteDictationFlowAsync("Cold Start");
            
            Assert.IsTrue(result.Success, $"Cold start failed: {result.ErrorMessage}");
            Assert.IsTrue(result.Latency.TotalSeconds < 2, $"Latency too high: {result.Latency.TotalSeconds}s");
        }

        [TestMethod]
        public async Task Test_RapidSuccessiveDictations()
        {
            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .ReturnsAsync(true);

            for (int i = 0; i < 5; i++)
            {
                var result = await _validator.ValidateCompleteDictationFlowAsync($"Rapid {i}");
                Assert.IsTrue(result.Success, $"Rapid dictation {i} failed");
            }
        }

        [TestMethod]
        public async Task Test_ServiceCoordination()
        {
            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .ReturnsAsync(true);

            var result = await _validator.ValidateCompleteDictationFlowAsync("Coordination Test");
            
            // Verify all stages were logged
            Assert.IsTrue(result.StepLog.Any(l => l.Contains("hotkey")), "Hotkey stage missing");
            Assert.IsTrue(result.StepLog.Any(l => l.Contains("capture")), "Capture stage missing");
            Assert.IsTrue(result.StepLog.Any(l => l.Contains("transcription")), "Transcription stage missing");
            Assert.IsTrue(result.StepLog.Any(l => l.Contains("Injecting")), "Injection stage missing");
        }

        [TestMethod]
        public async Task Test_FeedbackMechanisms()
        {
            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .ReturnsAsync(true);

            await _validator.ValidateCompleteDictationFlowAsync("Feedback Test");
            
            _feedbackMock.Verify(x => x.ShowToastNotificationAsync(
                It.IsAny<string>(), 
                It.IsAny<string>(), 
                It.IsAny<IFeedbackService.NotificationType>()), 
                Times.AtLeastOnce());
        }

        [TestMethod]
        public async Task Test_InjectionFailure_ShouldReportError()
        {
            _textInjectionMock.Setup(x => x.InjectTextAsync(It.IsAny<string>(), It.IsAny<InjectionOptions>()))
                .ReturnsAsync(false);

            var result = await _validator.ValidateCompleteDictationFlowAsync("Injection Failure Test");
            
            Assert.IsFalse(result.Success);
            Assert.AreEqual("Text injection failed", result.ErrorMessage);
        }
    }
}
