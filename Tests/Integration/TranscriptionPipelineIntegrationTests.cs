using System;
using System.Threading.Tasks;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Services;
using WhisperKey.Configuration;
using Microsoft.Extensions.Logging;
using WhisperKey.Tests.Common;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class TranscriptionPipelineIntegrationTests
    {
        private readonly IWhisperService _whisperService;
        private readonly IAudioDeviceService _audioDeviceService;
        private readonly ISettingsService _settingsService;
        private readonly IFeedbackService _feedbackService;
        private readonly ILogger<TranscriptionPipelineIntegrationTests> _logger;

        public TranscriptionPipelineIntegrationTests(
            IWhisperService whisperService, 
            IAudioDeviceService audioDeviceService, 
            ISettingsService settingsService,
            IFeedbackService feedbackService)
        {
            _whisperService = whisperService;
            _audioDeviceService = audioDeviceService;
            _settingsService = settingsService;
            _feedbackService = feedbackService;
            _logger = new NullLogger<TranscriptionPipelineIntegrationTests>();
        }

        /// <summary>
        /// End-to-end transcription pipeline integration tests
        /// </summary>
        [TestInitialize]
        public void Setup()
        {
            // Setup default test configuration
            _settingsService.SetValueAsync("Transcription:Provider", "OpenAI").Wait();
            _settingsService.SetValueAsync("Transcription:Model", "whisper-1").Wait();
            _settingsService.SetValueAsync("Audio:SampleRate", 16000).Wait();
        }

        [TestMethod]
        public async Task TranscriptionProvider_OpenAI_ShouldProcessAudioAndReturnText()
        {
            // Arrange
            var testAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // 1 second of silence + test audio
            
            // Mock the audio capture service to provide test audio
            _audioDeviceService.BeginCapture();
            
            // Act
            var result = await _whisperService.TranscribeAudioAsync(testAudio);
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Transcription);
            Assert.IsFalse(string.IsNullOrEmpty(result.Transcription));
            
            _logger.LogInformation($"Test completed: {(result.IsSuccess ? "PASS" : "FAIL")} - Duration: {result.Duration.TotalMilliseconds}ms");
            
            return result;
        }

        [TestMethod]
        public async Task TranscriptionProvider_Azure_ShouldProcessAudioAndReturnText()
        {
            // Arrange
            var testAudio = new byte[] { 0x05, 0x06, 0x07, 0x08 }; // Different test audio
            
            // Switch to Azure provider
            await _settingsService.SetValueAsync("Transcription:Provider", "Azure");
            await _settingsService.SetValueAsync("Transcription:Endpoint", "https://eastus.stt.speech.microsoft.com/speech/recognition/conversationtranscribes?api-version=2024-05-15-preview");
            
            _audioDeviceService.BeginCapture();
            
            // Act
            var result = await _whisperService.TranscribeAudioAsync(testAudio);
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Transcription);
            
            _logger.LogInformation($"Azure transcription test completed: {(result.IsSuccess ? "PASS" : "FAIL")}");
            
            return result;
        }

        [TestMethod]
        public async Task TranscriptionProvider_OpenAI_ShouldHandleEmptyAudio()
        {
            // Arrange
            var emptyAudio = Array.Empty<byte>();
            
            _audioDeviceService.BeginCapture();
            
            // Act
            var result = await _whisperService.TranscribeAudioAsync(emptyAudio);
            
            // Assert
            Assert.IsFalse(result.IsSuccess);
            Assert.IsTrue(result.HasError);
            Assert.IsNotNull(result.ErrorMessage);
            
            return result;
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldValidateAndSanitizeInput()
        {
            // Arrange
            var maliciousAudio = new byte[] { 0xFF, 0xFE, 0xBA, 0xAD }; // Invalid header
            var normalAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            
            // Act & Assert - Should reject malicious audio
            _audioDeviceService.BeginCapture();
            var maliciousResult = await _whisperService.TranscribeAudioAsync(maliciousAudio);
            Assert.IsFalse(maliciousResult.IsSuccess);
            
            // Act & Assert - Should accept valid audio
            _audioDeviceService.BeginCapture();
            var validResult = await _whisperService.TranscribeAudioAsync(normalAudio);
            Assert.IsTrue(validResult.IsSuccess);
            
            _logger.LogInformation($"Input validation test completed: Malicious={maliciousResult.IsSuccess}, Valid={validResult.IsSuccess}");
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldTrackProcessingMetrics()
        {
            // Arrange
            var testAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // 1 second of test audio
            
            // Mock metrics tracking
            TimeSpan? processingTime = null;
            int audioLength = 0;
            
            Mock.GetOrCreate<IAudioDeviceService>(mock => 
            {
                mock.Setup(x => x.BeginCapture())
                   .Callback(() =>
                   {
                       audioLength = 1024; // Simulate 1 second of audio
                   });
                mock.Setup(x => x.StopCapture())
                   .Callback(() =>
                   {
                       processingTime = TimeSpan.FromMilliseconds(2000); // 2 second processing time
                   });
            });

            _audioDeviceService = Mock.GetOrCreate<IAudioDeviceService>(MockBehavior.Strict);
            Mock.GetOrCreate<IAudioDeviceService>(MockBehavior.Strict)
                .Setup(x => x.BeginCapture())
                   .Callback(() => audioLength = 1024)
                   .Setup(x => x.StopCapture())
                   .Callback(() => processingTime = TimeSpan.FromMilliseconds(2000));
            
            _audioDeviceService.BeginCapture();
            
            // Act
            var result = await _whisperService.TranscribeAudioAsync(testAudio);
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, result.AudioLength.TotalSeconds);
            Assert.AreEqual(2.0, processingTime?.TotalSeconds); // 2 seconds for 1 second of audio
            Assert.AreEqual(240, result.ProcessingTime.TotalMilliseconds); // 240ms for 1024 bytes at 16000Hz
            
            _logger.LogInformation($"Metrics test completed: AudioLength={result.AudioLength.TotalSeconds}s, ProcessingTime={processingTime?.TotalSeconds}s");
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldHandleProviderSwitch()
        {
            // Arrange
            var testAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            
            // Start with OpenAI
            await _settingsService.SetValueAsync("Transcription:Provider", "OpenAI");
            _audioDeviceService.BeginCapture();
            var openaiResult = await _whisperService.TranscribeAudioAsync(testAudio);
            
            // Switch to Azure mid-processing (simulating provider change)
            // This should be handled gracefully by the service architecture
            
            // Act - Continue transcription (service should handle provider switch internally)
            var finalResult = await _whisperService.TranscribeAudioAsync(new byte[] { 0x05, 0x06, 0x07, 0x08 });
            
            // Assert
            Assert.IsTrue(openaiResult.IsSuccess);
            Assert.IsTrue(finalResult.IsSuccess);
            Assert.AreNotEqual(openaiResult.Transcription, finalResult.Transcription); // Different processing
            
            _logger.LogInformation($"Provider switch test completed: Both transcriptions successful");
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldIntegrateWithFeedbackService()
        {
            // Arrange
            var testAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            
            _audioDeviceService.BeginCapture();
            
            // Act
            var result = await _whisperService.TranscribeAudioAsync(testAudio);
            
            // Verify feedback was called (would be called in real implementation)
            _feedbackService.Received<UserFeedback>(Arg.Any<UserFeedback>());
            
            // Assert
            Assert.IsTrue(result.IsSuccess);
            Assert.IsNotNull(result.Transcription);
            Assert.IsTrue(result.Duration.TotalMilliseconds > 0);
            
            _logger.LogInformation($"Feedback integration test completed: Duration={result.Duration.TotalMilliseconds}ms");
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldHandleConcurrentTranscriptions()
        {
            // Arrange
            var testAudio1 = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            var testAudio2 = new byte[] { 0x05, 0x06, 0x07, 0x08 };
            
            // Act
            var tasks = new[]
            {
                _whisperService.TranscribeAudioAsync(testAudio1),
                _whisperService.TranscribeAudioAsync(testAudio2)
            };
            
            var results = await Task.WhenAll(tasks);
            
            // Assert
            Assert.IsTrue(results.All(r => r.IsSuccess));
            Assert.IsTrue(results.All(r => !string.IsNullOrEmpty(r.Transcription)));
            
            _logger.LogInformation($"Concurrent transcription test completed: {results.Count} parallel transcriptions processed");
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldRespectConfigurationSettings()
        {
            // Arrange
            var testAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // 1 second at 16kHz
            
            // Test different configurations
            var configurations = new[]
            {
                new { SampleRate = 8000, Language = "en" },
                new { SampleRate = 16000, Language = "es" },
                new { SampleRate = 22050, Language = "fr" }
            };
            
            foreach (var config in configurations)
            {
                // Apply configuration
                await _settingsService.SetValueAsync("Transcription:Language", config.Language);
                await _settingsService.SetValueAsync("Audio:SampleRate", config.SampleRate);
                
                _audioDeviceService.BeginCapture();
                var result = await _whisperService.TranscribeAudioAsync(testAudio);
                
                // Assert
                Assert.IsTrue(result.IsSuccess);
                Assert.IsNotNull(result.Transcription);
                
                _logger.LogInformation($"Configuration test completed: Language={config.Language}, SampleRate={config.SampleRate}, Success={result.IsSuccess}");
                
                _audioDeviceService.StopCapture();
            }
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldHandleErrorsAndRecovery()
        {
            // Arrange
            var testAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 };
            
            // Mock failure scenarios
            var scenarios = new[]
            {
                new { NetworkError = true, Description = "Simulated network failure" },
                new { ApiKeyInvalid = true, Description = "Simulated API key error" },
                new { QuotaExceeded = true, Description = "Simulated quota exceeded" },
                new { AudioTooLarge = true, Description = "Simulated audio too large" }
            };
            
            foreach (var scenario in scenarios)
            {
                // Mock the failure condition
                // In real implementation, this would be handled by service mocks
                
                _audioDeviceService.BeginCapture();
                var result = await _whisperService.TranscribeAudioAsync(testAudio);
                
                // Assert
                if (scenario.NetworkError || scenario.ApiKeyInvalid)
                {
                    Assert.IsFalse(result.IsSuccess);
                    Assert.IsNotNull(result.ErrorMessage);
                }
                else
                {
                    // For other scenarios, expect success (in this test setup)
                    Assert.IsTrue(result.IsSuccess);
                }
                
                _audioDeviceService.StopCapture();
            }
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldMaintainContext()
        {
            // Arrange
            var testAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // 1 second
            
            // Mock context preservation
            string? originalContext = null;
            Mock.GetOrCreate<IWhisperService>(MockBehavior.Strict)
                .Setup(x => x.TranscribeAudioAsync(It.IsAny<byte[]>()))
                .ReturnsAsync((byte[] audio) =>
                {
                    if (originalContext == null)
                    {
                        originalContext = "Initial transcription context";
                    }
                    return new TranscriptionResult
                    {
                        IsSuccess = true,
                        Transcription = $"Audio processed with context: {originalContext ?? "new"}",
                        Duration = TimeSpan.FromMilliseconds(1500)
                    };
                });
            
            // Act
            var result1 = await _whisperService.TranscribeAudioAsync(testAudio);
            var result2 = await _whisperService.TranscribeAudioAsync(testAudio); // Should maintain context
            
            // Assert
            Assert.IsTrue(result1.IsSuccess);
            Assert.IsTrue(result2.IsSuccess);
            Assert.IsTrue(result2.Transcription.Contains("Initial transcription context")); // Context maintained
            Assert.AreEqual(result1.Duration, result2.Duration); // Consistent processing
            
            _logger.LogInformation($"Context maintenance test completed: Context preserved across multiple transcriptions");
        }

        [TestMethod]
        public async Task TranscriptionPipeline_ShouldValidateAudioFormat()
        {
            // Arrange
            var validAudio = new byte[] { 0x01, 0x02, 0x03, 0x04 }; // Valid 16-bit PCM
            var invalidAudio = new byte[] { 0x01, 0x02 }; // Too short
            var wrongFormatAudio = new byte[] { 0x00, 0x00, 0x00, 0x01 }; // Invalid header
            
            // Act & Assert
            _audioDeviceService.BeginCapture();
            
            // Valid audio should succeed
            var validResult = await _whisperService.TranscribeAudioAsync(validAudio);
            Assert.IsTrue(validResult.IsSuccess);
            
            // Invalid audio should fail gracefully
            var invalidResult = await _whisperService.TranscribeAudioAsync(invalidAudio);
            Assert.IsFalse(invalidResult.IsSuccess);
            Assert.IsNotNull(invalidResult.ErrorMessage);
            
            var wrongFormatResult = await _whisperService.TranscribeAudioAsync(wrongFormatAudio);
            Assert.IsFalse(wrongFormatResult.IsSuccess);
            Assert.IsNotNull(wrongFormatResult.ErrorMessage);
            
            _audioDeviceService.StopCapture();
            
            _logger.LogInformation($"Audio format validation completed: Valid={validResult.IsSuccess}, Invalid={invalidResult.IsSuccess}, WrongFormat={wrongFormatResult.IsSuccess}");
        }
    }
}
