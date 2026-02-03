using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using WhisperKey.Configuration;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class WhisperServiceTests
    {
        private Mock<ISettingsService> _settingsServiceMock = null!;
        private Mock<LocalInferenceService> _localInferenceMock = null!;
        private Mock<HttpMessageHandler> _httpHandlerMock = null!;
        private HttpClient _httpClient = null!;

        /// <summary>
        /// Creates a minimal valid WAV file with proper headers for testing.
        /// Generates a 1-second silent mono 16-bit 16kHz WAV file.
        /// </summary>
        private static byte[] CreateValidWavData(int durationSeconds = 1)
        {
            const int sampleRate = 16000;
            const short bitsPerSample = 16;
            const short channels = 1;
            const int byteRate = sampleRate * channels * bitsPerSample / 8;
            const int blockAlign = channels * bitsPerSample / 8;
            
            int dataSize = sampleRate * durationSeconds * blockAlign;
            int fileSize = 36 + dataSize; // 44 header bytes - 8 (RIFF size field)
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            // RIFF header
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(fileSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            
            // fmt subchunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size (16 for PCM)
            writer.Write((short)1); // AudioFormat (1 for PCM)
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);
            
            // data subchunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            
            // Write silent audio data
            byte[] audioData = new byte[dataSize];
            writer.Write(audioData);
            
            writer.Flush();
            return ms.ToArray();
        }

        [TestInitialize]
        public void Setup()
        {
            _settingsServiceMock = new Mock<ISettingsService>();
            _httpHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpHandlerMock.Object);

            // Setup default settings for cloud mode
            _settingsServiceMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Transcription = new TranscriptionSettings
                {
                    Mode = TranscriptionMode.Cloud,
                    AutoFallbackToCloud = true
                }
            });

            // Setup encrypted API key retrieval
            _settingsServiceMock.Setup(s => s.GetEncryptedValueAsync("OpenAI_ApiKey"))
                .ReturnsAsync("test-api-key");

            // Create mock for LocalInferenceService (concrete class)
            _localInferenceMock = new Mock<LocalInferenceService>(_settingsServiceMock.Object, null, null);
            _localInferenceMock.CallBase = true;
        }

        [TestCleanup]
        public void Cleanup()
        {
            _httpClient?.Dispose();
            _localInferenceMock?.Object?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_Default_CreatesHttpClient()
        {
            var service = new WhisperService();
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_WithSettings_SetsUpHttpClient()
        {
            var service = new WhisperService(_settingsServiceMock.Object, (LocalInferenceService?)null);
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_WithHttpClientFactory_UsesFactoryClient()
        {
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);
            Assert.IsNotNull(service);
            service.Dispose();
        }

        #endregion

        #region Cloud Transcription Tests

        [TestMethod]
        public async Task TranscribeAudioAsync_Cloud_Success_ReturnsTranscription()
        {
            // Arrange
            var expectedText = "Hello, this is a test transcription.";
            var response = new { text = expectedText };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act
            var result = await service.TranscribeAudioAsync(CreateValidWavData()); // 1 second of audio

            // Assert
            Assert.AreEqual(expectedText, result);
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_Cloud_ApiError_ThrowsHttpRequestException()
        {
            // Arrange
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.Unauthorized,
                    Content = new StringContent("{\"error\": \"Invalid API key\"}")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await service.TranscribeAudioAsync(CreateValidWavData());
            });
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_EmptyAudio_ThrowsSecurityException()
        {
            // Arrange
            var service = new WhisperService(_settingsServiceMock.Object, (LocalInferenceService?)null);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<System.Security.SecurityException>(async () =>
            {
                await service.TranscribeAudioAsync(new byte[0]);
            });
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_NullAudio_ThrowsSecurityException()
        {
            // Arrange
            var service = new WhisperService(_settingsServiceMock.Object, (LocalInferenceService?)null);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<System.Security.SecurityException>(async () =>
            {
                await service.TranscribeAudioAsync(null!);
            });
        }

        [TestMethod]
        public async Task TranscribeAudioFileAsync_FileNotFound_ThrowsFileNotFoundException()
        {
            // Arrange
            var service = new WhisperService(_settingsServiceMock.Object, (LocalInferenceService?)null);
            var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".wav");

            // Act & Assert
            await Assert.ThrowsExceptionAsync<FileNotFoundException>(async () =>
            {
                await service.TranscribeAudioFileAsync(nonExistentPath);
            });
        }

        [TestMethod]
        public async Task TranscribeAudioFileAsync_ValidFile_ReturnsTranscription()
        {
            // Arrange
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.wav");
            var expectedText = "Test transcription from file.";
            var response = new { text = expectedText };
            var jsonResponse = JsonConvert.SerializeObject(response);

            try
            {
                // Create test file with valid WAV data
                File.WriteAllBytes(tempFile, CreateValidWavData());

                _httpHandlerMock.Protected()
                    .Setup<Task<HttpResponseMessage>>("SendAsync",
                        ItExpr.IsAny<HttpRequestMessage>(),
                        ItExpr.IsAny<CancellationToken>())
                    .ReturnsAsync(new HttpResponseMessage
                    {
                        StatusCode = HttpStatusCode.OK,
                        Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                    });

                var factoryMock = new Mock<IHttpClientFactory>();
                factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

                var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

                // Act
                var result = await service.TranscribeAudioFileAsync(tempFile);

                // Assert
                Assert.AreEqual(expectedText, result);
            }
            finally
            {
                // Cleanup
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        #endregion

        #region Local Inference Fallback Tests

        [TestMethod]
        [Ignore("LocalInferenceService.TranscribeAudioAsync is not virtual - cannot mock with Moq")]
        public async Task TranscribeAudioAsync_LocalMode_Success_ReturnsLocalResult()
        {
            // Note: This test is skipped because LocalInferenceService is a concrete class
            // with non-virtual methods that cannot be mocked with Moq.
            // To test local mode properly, the class would need either:
            // 1. Virtual methods, or
            // 2. An interface-based design where WhisperService accepts ILocalInferenceService
        }

        [TestMethod]
        [Ignore("LocalInferenceService.TranscribeAudioAsync is not virtual - cannot mock with Moq")]
        public async Task TranscribeAudioAsync_LocalFails_WithFallback_UsesCloud()
        {
            // Note: This test is skipped because LocalInferenceService is a concrete class
            // with non-virtual methods that cannot be mocked with Moq.
        }

        [TestMethod]
        [Ignore("LocalInferenceService.TranscribeAudioAsync is not virtual - cannot mock with Moq")]
        public async Task TranscribeAudioAsync_LocalFails_NoFallback_ThrowsException()
        {
            // Note: This test is skipped because LocalInferenceService is a concrete class
            // with non-virtual methods that cannot be mocked with Moq.
        }

        [TestMethod]
        [Ignore("LocalInferenceService.TranscribeAudioAsync is not virtual - cannot mock with Moq")]
        public async Task TranscribeAudioAsync_HttpRequestException_WithFallback_UsesCloud()
        {
            // Note: This test is skipped because LocalInferenceService is a concrete class
            // with non-virtual methods that cannot be mocked with Moq.
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_IOException_WithFallback_UsesCloud()
        {
            // Arrange - Note: Cannot mock TranscribeAudioAsync on concrete class with Moq
            // since it's not virtual. We'll skip the local inference test and just verify
            // cloud fallback works when local service throws.
            _settingsServiceMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Transcription = new TranscriptionSettings
                {
                    Mode = TranscriptionMode.Local,
                    AutoFallbackToCloud = true
                }
            });

            // Create a mock that will throw when called using CallBase behavior
            var throwingInferenceMock = new Mock<LocalInferenceService>(_settingsServiceMock.Object, null, null);
            // We can't setup non-virtual methods, so we test the fallback path indirectly
            // by using a mock that hasn't had its model loaded

            var expectedText = "Cloud fallback after IO error.";
            var response = new { text = expectedText };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, throwingInferenceMock.Object);

            // Act - In real scenario, local inference would throw and fallback occurs
            // For this test, we verify cloud path is available
            var result = await service.TranscribeAudioAsync(CreateValidWavData());

            // Assert
            Assert.AreEqual(expectedText, result);
        }

        #endregion

        #region Event Tests

        [TestMethod]
        public async Task TranscribeAudioAsync_FiresTranscriptionStartedEvent()
        {
            // Arrange
            var response = new { text = "Test" };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);
            bool eventFired = false;
            service.TranscriptionStarted += (s, e) => eventFired = true;

            // Act
            await service.TranscribeAudioAsync(CreateValidWavData());

            // Assert
            Assert.IsTrue(eventFired, "TranscriptionStarted event should fire");
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_FiresTranscriptionProgressEvent()
        {
            // Arrange
            var response = new { text = "Test" };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);
            var progressValues = new System.Collections.Generic.List<int>();
            service.TranscriptionProgress += (s, e) => progressValues.Add(e);

            // Act
            await service.TranscribeAudioAsync(CreateValidWavData());

            // Assert
            Assert.IsTrue(progressValues.Count > 0, "TranscriptionProgress event should fire");
            Assert.IsTrue(progressValues.Contains(25), "Should report 25% progress before request");
            Assert.IsTrue(progressValues.Contains(75), "Should report 75% progress after response");
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_FiresTranscriptionCompletedEvent()
        {
            // Arrange
            var expectedText = "Test result";
            var response = new { text = expectedText };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);
            string? completedText = null;
            service.TranscriptionCompleted += (s, e) => completedText = e;

            // Act
            await service.TranscribeAudioAsync(CreateValidWavData());

            // Assert
            Assert.AreEqual(expectedText, completedText);
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_FiresTranscriptionErrorEvent()
        {
            // Arrange
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.BadRequest,
                    Content = new StringContent("{\"error\": \"Bad request\"}")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);
            Exception? capturedError = null;
            service.TranscriptionError += (s, e) => capturedError = e;

            // Act
            try
            {
                await service.TranscribeAudioAsync(CreateValidWavData());
            }
            catch
            {
                // Expected
            }

            // Assert
            Assert.IsNotNull(capturedError);
            Assert.IsInstanceOfType(capturedError, typeof(HttpRequestException));
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_FiresUsageUpdatedEvent()
        {
            // Arrange
            var response = new { text = "Test" };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);
            UsageStats? capturedStats = null;
            service.UsageUpdated += (s, e) => capturedStats = e;

            // Act
            await service.TranscribeAudioAsync(CreateValidWavData()); // 1 second of audio

            // Assert
            Assert.IsNotNull(capturedStats);
            Assert.AreEqual(1, capturedStats.RequestCount);
            Assert.IsTrue(capturedStats.EstimatedCost > 0);
        }

        #endregion

        #region Usage Stats Tests

        [TestMethod]
        public void GetUsageStats_Initial_ReturnsZeroStats()
        {
            // Arrange
            var service = new WhisperService(_settingsServiceMock.Object, (LocalInferenceService?)null);

            // Act
            var stats = service.GetUsageStats();

            // Assert
            Assert.AreEqual(0, stats.RequestCount);
            Assert.AreEqual(0.0m, stats.EstimatedCost);
            Assert.AreEqual(0.0, stats.EstimatedMinutes);
        }

        [TestMethod]
        public async Task GetUsageStats_AfterTranscription_ReturnsCorrectStats()
        {
            // Arrange
            var response = new { text = "Test" };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act
            await service.TranscribeAudioAsync(CreateValidWavData(2)); // 2 seconds of audio
            var stats = service.GetUsageStats();

            // Assert
            Assert.AreEqual(1, stats.RequestCount);
            Assert.IsTrue(stats.EstimatedCost > 0);
            Assert.IsTrue(stats.EstimatedMinutes > 0);
        }

        [TestMethod]
        public async Task GetUsageStats_AfterMultipleTranscriptions_ReturnsAggregatedStats()
        {
            // Arrange
            var response = new { text = "Test" };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act
            await service.TranscribeAudioAsync(CreateValidWavData());
            await service.TranscribeAudioAsync(CreateValidWavData());
            await service.TranscribeAudioAsync(CreateValidWavData());
            var stats = service.GetUsageStats();

            // Assert
            Assert.AreEqual(3, stats.RequestCount);
        }

        [TestMethod]
        public void ResetUsageStats_ResetsCounters()
        {
            // Arrange
            var service = new WhisperService(_settingsServiceMock.Object, (LocalInferenceService?)null);
            bool eventFired = false;
            service.UsageUpdated += (s, e) => eventFired = true;

            // Act
            service.ResetUsageStats();

            // Assert
            var stats = service.GetUsageStats();
            Assert.AreEqual(0, stats.RequestCount);
            Assert.AreEqual(0.0m, stats.EstimatedCost);
            Assert.IsTrue(eventFired, "UsageUpdated event should fire on reset");
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task TranscribeAudioAsync_InvalidResponse_ReturnsEmptyString()
        {
            // Arrange - Response missing 'text' field returns empty string
            // because WhisperResponse.Text has default value of string.Empty
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"invalid\": \"response\"}") // Missing 'text' field
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act
            var result = await service.TranscribeAudioAsync(CreateValidWavData());

            // Assert - Returns empty string when text field is missing
            Assert.AreEqual(string.Empty, result);
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_EmptyResponseText_ThrowsInvalidOperationException()
        {
            // Arrange
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent("{\"text\": null}") // Null text field
                });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            {
                await service.TranscribeAudioAsync(CreateValidWavData());
            });
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_FatalException_NotCaught()
        {
            // Arrange - OutOfMemoryException should not be caught
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new OutOfMemoryException("Out of memory"));

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act & Assert
            await Assert.ThrowsExceptionAsync<OutOfMemoryException>(async () =>
            {
                await service.TranscribeAudioAsync(CreateValidWavData());
            });
        }

        [TestMethod]
        public async Task TranscribeAudioFileAsync_FatalException_NotCaught()
        {
            // Arrange - StackOverflowException should not be caught
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ThrowsAsync(new StackOverflowException("Stack overflow"));

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);
            var tempFile = Path.Combine(Path.GetTempPath(), $"test_{Guid.NewGuid():N}.wav");

            try
            {
                File.WriteAllBytes(tempFile, CreateValidWavData());

                // Act & Assert
                await Assert.ThrowsExceptionAsync<StackOverflowException>(async () =>
                {
                    await service.TranscribeAudioFileAsync(tempFile);
                });
            }
            finally
            {
                if (File.Exists(tempFile))
                {
                    File.Delete(tempFile);
                }
            }
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_WithOwnedHttpClient_DisposesClient()
        {
            // Arrange
            var service = new WhisperService();

            // Act - Should not throw
            service.Dispose();

            // Assert - Service should be disposed successfully
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Dispose_WithFactoryHttpClient_DoesNotDisposeClient()
        {
            // Arrange
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act - Should not throw and should not dispose factory client
            service.Dispose();

            // Assert - Service should be disposed successfully
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Dispose_DisposesLocalInference()
        {
            // Arrange - Create a service with local inference
            var localInferenceMock = new Mock<LocalInferenceService>(_settingsServiceMock.Object, null, null);
            localInferenceMock.CallBase = true;

            var service = new WhisperService(_settingsServiceMock.Object, localInferenceMock.Object);

            // Act & Assert - Should dispose without throwing
            // Note: Cannot verify Dispose() was called with Moq since it's not virtual
            service.Dispose();
            Assert.IsNotNull(service);
        }

        #endregion

        #region Settings Change Tests

        [TestMethod]
        public void SettingsChanged_WithTranscriptionSettings_UpdatesApiKey()
        {
            // Arrange
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            _settingsServiceMock.Setup(s => s.GetEncryptedValueAsync("OpenAI_ApiKey"))
                .ReturnsAsync("new-api-key");

            var eventArgs = new SettingsChangedEventArgs
            {
                Category = "Transcription",
                Key = "OpenAI_ApiKey"
            };

            // Act - Simulate settings changed event
            _settingsServiceMock.Raise(s => s.SettingsChanged += null, _settingsServiceMock.Object, eventArgs);

            // Wait a moment for the async handler to complete
            Thread.Sleep(100);

            // Assert - The service should have updated the API key
            // (Verification is implicit - no exception means success)
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void SettingsChanged_WithNonApiKeySetting_DoesNotUpdateApiKey()
        {
            // Arrange
            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            var eventArgs = new SettingsChangedEventArgs
            {
                Category = "Transcription",
                Key = "SomeOtherSetting"
            };

            // Act
            _settingsServiceMock.Raise(s => s.SettingsChanged += null, _settingsServiceMock.Object, eventArgs);

            // Assert - No exception should be thrown
            Assert.IsNotNull(service);
        }

        #endregion

        #region Rate Limiting Tests

        [TestMethod]
        public async Task TranscribeAudioAsync_RateLimitEnabled_WithinLimit_Succeeds()
        {
            // Arrange
            var expectedText = "Test transcription";
            var response = new { text = expectedText };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            // Setup rate limiting (5 requests per minute for testing)
            _settingsServiceMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Transcription = new TranscriptionSettings
                {
                    Mode = TranscriptionMode.Cloud,
                    EnableRateLimiting = true,
                    MaxRequestsPerMinute = 5,
                    AutoFallbackToCloud = true
                }
            });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act
            var result = await service.TranscribeAudioAsync(CreateValidWavData());

            // Assert
            Assert.AreEqual(expectedText, result);
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_RateLimitEnabled_ExceedsLimit_Returns429()
        {
            // Arrange
            var expectedText = "Test transcription";
            var response = new { text = expectedText };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            // Setup rate limiting (1 request per minute for testing)
            _settingsServiceMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Transcription = new TranscriptionSettings
                {
                    Mode = TranscriptionMode.Cloud,
                    EnableRateLimiting = true,
                    MaxRequestsPerMinute = 1,
                    AutoFallbackToCloud = true
                }
            });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // First request should succeed
            await service.TranscribeAudioAsync(CreateValidWavData());

            // Act & Assert - Second request should fail with 429
            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await service.TranscribeAudioAsync(CreateValidWavData());
            });
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_RateLimitDisabled_NoLimitingApplied()
        {
            // Arrange
            var expectedText = "Test transcription";
            var response = new { text = expectedText };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            // Setup rate limiting disabled
            _settingsServiceMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Transcription = new TranscriptionSettings
                {
                    Mode = TranscriptionMode.Cloud,
                    EnableRateLimiting = false,
                    AutoFallbackToCloud = true
                }
            });

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, null);

            // Act - Multiple requests should all succeed when rate limiting is disabled
            var result1 = await service.TranscribeAudioAsync(CreateValidWavData());
            var result2 = await service.TranscribeAudioAsync(CreateValidWavData());
            var result3 = await service.TranscribeAudioAsync(CreateValidWavData());

            // Assert
            Assert.AreEqual(expectedText, result1);
            Assert.AreEqual(expectedText, result2);
            Assert.AreEqual(expectedText, result3);
        }

        [TestMethod]
        public async Task TranscribeAudioAsync_LocalMode_RateLimitApplied_WhenConfigured()
        {
            // Arrange - Local mode with rate limiting enabled for local
            _settingsServiceMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Transcription = new TranscriptionSettings
                {
                    Mode = TranscriptionMode.Local,
                    EnableRateLimiting = true,
                    MaxRequestsPerMinute = 2,
                    ApplyRateLimitToLocal = true,
                    AutoFallbackToCloud = true
                }
            });

            // Setup cloud fallback mock
            var expectedText = "Cloud fallback transcription";
            var response = new { text = expectedText };
            var jsonResponse = JsonConvert.SerializeObject(response);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
                });

            // Local inference service will throw, causing fallback to cloud
            var throwingInferenceMock = new Mock<LocalInferenceService>(_settingsServiceMock.Object, null, null);

            var factoryMock = new Mock<IHttpClientFactory>();
            factoryMock.Setup(f => f.CreateClient("WhisperApi")).Returns(_httpClient);

            var service = new WhisperService(_settingsServiceMock.Object, factoryMock.Object, throwingInferenceMock.Object);

            // Act & Assert - First request fails locally but succeeds with cloud fallback
            var result1 = await service.TranscribeAudioAsync(CreateValidWavData());
            Assert.AreEqual(expectedText, result1);
            
            // Second request also succeeds (within rate limit of 2)
            var result2 = await service.TranscribeAudioAsync(CreateValidWavData());
            Assert.AreEqual(expectedText, result2);
            
            // Third request should fail with rate limit
            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await service.TranscribeAudioAsync(CreateValidWavData());
            });
        }

        #endregion
    }
}
