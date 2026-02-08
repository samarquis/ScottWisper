using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using WhisperKey.Configuration;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Tests.Common;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class ExternalApiIntegrationTests
    {
        private Mock<ISettingsService> _settingsServiceMock = null!;
        private Mock<HttpMessageHandler> _httpHandlerMock = null!;
        private HttpClient _httpClient = null!;
        private WhisperService _whisperService = null!;
        private Mock<IAuditLoggingService> _auditServiceMock = null!;

        [TestInitialize]
        public void Setup()
        {
            _settingsServiceMock = new Mock<ISettingsService>();
            _httpHandlerMock = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_httpHandlerMock.Object);
            _auditServiceMock = new Mock<IAuditLoggingService>();

            SetupDefaultSettings();
            
            _whisperService = new WhisperService(
                _settingsServiceMock.Object,
                _auditServiceMock.Object,
                _httpClient);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _httpClient?.Dispose();
        }

        #region OpenAI API Integration Tests

        [TestMethod]
        public async Task OpenAI_Api_Authentication_ShouldUseValidApiKey()
        {
            // Arrange
            var apiKey = "sk-test123456789";
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:ApiKey"))
                .ReturnsAsync(apiKey);
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    // Verify Authorization header
                    Assert.IsTrue(request.Headers.Authorization != null, "Authorization header should be present");
                    Assert.AreEqual($"Bearer {apiKey}", request.Headers.Authorization.Parameter);
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"text\":\"Test transcription\"}")
                });

            // Act
            var audioData = CreateValidWavData();
            var result = await _whisperService.TranscribeAudioAsync(audioData);

            // Assert
            Assert.AreEqual("Test transcription", result);
            _httpHandlerMock.Protected().Verify(
                "SendAsync", Times.Once(),
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>());
        }

        [TestMethod]
        public async Task OpenAI_Api_RateLimiting_ShouldHandle429Responses()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            var callCount = 0;
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() =>
                {
                    callCount++;
                    if (callCount <= 2)
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests)
                        {
                            Content = new StringContent("{\"error\":\"Rate limit exceeded\"}")
                        });
                    else
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"text\":\"Success after retry\"}")
                        });
                });

            var audioData = CreateValidWavData();

            // Act & Assert - First two calls should fail with rate limit
            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await _whisperService.TranscribeAudioAsync(audioData);
            });

            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await _whisperService.TranscribeAudioAsync(audioData);
            });

            // Third call should succeed (after backoff)
            var result = await _whisperService.TranscribeAudioAsync(audioData);
            Assert.AreEqual("Success after retry", result);
        }

        [TestMethod]
        public async Task OpenAI_Api_LargeFileUpload_ShouldHandleChunkedUpload()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            var uploadedContent = new List<byte[]>();
            var totalUploadSize = 0L;

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    if (request.Content is MultipartContent multipartContent)
                    {
                        foreach (var content in multipartContent)
                        {
                            if (content is ByteArrayContent byteArrayContent)
                            {
                                var bytes = byteArrayContent.ReadAsByteArrayAsync().Result;
                                uploadedContent.Add(bytes);
                                totalUploadSize += bytes.Length;
                            }
                        }
                    }
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"text\":\"Large file transcribed\"}")
                });

            // Act - Upload large audio file (25MB)
            var largeAudioData = CreateLargeWavData(25);
            var result = await _whisperService.TranscribeAudioAsync(largeAudioData);

            // Assert
            Assert.AreEqual("Large file transcribed", result);
            Assert.IsTrue(totalUploadSize > 25 * 1024 * 1024, "Should upload at least 25MB");
            Assert.IsTrue(uploadedContent.Count > 0, "Should have uploaded content");
        }

        [TestMethod]
        public async Task OpenAI_Api_ServiceInterruption_ShouldImplementRetryPolicy()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            var attemptCount = 0;
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() =>
                {
                    attemptCount++;
                    if (attemptCount <= 3)
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                    else
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"text\":\"Success after retries\"}")
                        });
                });

            var audioData = CreateValidWavData();

            // Act
            var result = await _whisperService.TranscribeAudioAsync(audioData);

            // Assert - Should retry and eventually succeed
            Assert.AreEqual("Success after retries", result);
            Assert.AreEqual(4, attemptCount, "Should have made 4 attempts (3 failures + 1 success)");
        }

        #endregion

        #region Azure Speech API Integration Tests

        [TestMethod]
        public async Task AzureSpeech_Api_Authentication_ShouldUseValidToken()
        {
            // Arrange
            var endpoint = "https://eastus.stt.speech.microsoft.com/";
            var apiKey = "azure-test-key";
            
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("Azure");
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Endpoint"))
                .ReturnsAsync(endpoint);
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:ApiKey"))
                .ReturnsAsync(apiKey);

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    // Verify Azure-specific headers
                    Assert.IsTrue(request.Headers.Contains("Ocp-Apim-Subscription-Key"));
                    Assert.AreEqual(apiKey, request.Headers.GetValues("Ocp-Apim-Subscription-Key").First());
                    Assert.IsTrue(request.RequestUri!.ToString().StartsWith(endpoint));
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"DisplayText\":\"Azure transcription\"}")
                });

            // Act
            var audioData = CreateValidWavData();
            var result = await _whisperService.TranscribeAudioAsync(audioData);

            // Assert
            Assert.AreEqual("Azure transcription", result);
        }

        [TestMethod]
        public async Task AzureSpeech_Api_RealTimeTranscription_ShouldHandleStreaming()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("Azure");

            var streamingResponses = new[]
            {
                "{\"DisplayText\":\"Partial\",\"Offset\":100}",
                "{\"DisplayText\":\"Partial result\",\"Offset\":200}",
                "{\"DisplayText\":\"Final transcription result\",\"Offset\":300}"
            };

            var responseIndex = 0;
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StreamingHttpContent(streamingResponses[responseIndex++])
                    };
                    return response;
                });

            // Act
            var audioData = CreateValidWavData();
            var result = await _whisperService.TranscribeAudioAsync(audioData);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Contains("Final transcription result"), "Should return final transcription");
        }

        #endregion

        #region Error Handling and Resilience Tests

        [TestMethod]
        public async Task ExternalApi_NetworkTimeout_ShouldImplementTimeoutHandling()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    // Simulate delay longer than timeout
                    Task.Delay(30000, ct).Wait();
                })
                .ThrowsAsync(new TaskCanceledException("Request timeout"));

            // Act & Assert
            var audioData = CreateValidWavData();
            
            await Assert.ThrowsExceptionAsync<TaskCanceledException>(async () =>
            {
                await _whisperService.TranscribeAudioAsync(audioData);
            });
        }

        [TestMethod]
        public async Task ExternalApi_InvalidResponse_ShouldHandleGracefully()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{ invalid json response }")
                });

            // Act & Assert
            var audioData = CreateValidWavData();
            
            await Assert.ThrowsExceptionAsync<JsonException>(async () =>
            {
                await _whisperService.TranscribeAudioAsync(audioData);
            });
        }

        [TestMethod]
        public async Task ExternalApi_ServerErrors_ShouldLogAuditEvents()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("{\"error\":\"Internal server error\"}")
                });

            var audioData = CreateValidWavData();

            // Act
            try
            {
                await _whisperService.TranscribeAudioAsync(audioData);
            }
            catch
            {
                // Expected to throw
            }

            // Assert - Should audit the error
            _auditServiceMock.Verify(a => a.LogEventAsync(
                It.Is<AuditEventType>(et => et == AuditEventType.TranscriptionFailed),
                It.Is<string>(msg => msg.Contains("Internal server error")),
                It.IsAny<string>(),
                It.IsAny<DataSensitivity>()),
                Times.Once);
        }

        #endregion

        #region Multi-Provider Integration Tests

        [TestMethod]
        public async Task MultiProvider_ProviderFailover_ShouldSwitchProviders()
        {
            // Arrange
            _settingsServiceMock.SetupSequence(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI")      // First attempt
                .ReturnsAsync("Azure")        // Failover to Azure
                .ReturnsAsync("OpenAI");     // Switch back

            // Configure OpenAI to fail
            var attemptCount = 0;
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Returns(() =>
                {
                    attemptCount++;
                    var provider = attemptCount <= 2 ? "OpenAI" : "Azure";
                    
                    if (provider == "OpenAI")
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                    else
                        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                        {
                            Content = new StringContent("{\"DisplayText\":\"Azure failover success\"}")
                        });
                });

            var audioData = CreateValidWavData();

            // Act - First attempt (OpenAI) should fail
            await Assert.ThrowsExceptionAsync<HttpRequestException>(async () =>
            {
                await _whisperService.TranscribeAudioAsync(audioData);
            });

            // Second attempt (Azure) should succeed
            var result = await _whisperService.TranscribeAudioAsync(audioData);

            // Assert
            Assert.AreEqual("Azure failover success", result);
        }

        #endregion

        #region Performance and Load Tests

        [TestMethod]
        public async Task ExternalApi_ConcurrentRequests_ShouldHandleLoad()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"text\":\"Concurrent success\"}")
                });

            // Act - Make concurrent requests
            var tasks = new List<Task<string>>();
            var audioData = CreateValidWavData();

            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_whisperService.TranscribeAudioAsync(audioData));
            }

            var results = await Task.WhenAll(tasks);

            // Assert - All requests should succeed
            Assert.AreEqual(10, results.Length);
            Assert.IsTrue(results.All(r => r == "Concurrent success"));
        }

        [TestMethod]
        public async Task ExternalApi_ResponseTime_ShouldMeetSLA()
        {
            // Arrange
            _settingsServiceMock.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            var responseTimes = new List<long>();
            _httpHandlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .Callback<HttpRequestMessage, CancellationToken>((request, ct) =>
                {
                    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                    Task.Delay(500, ct).Wait(); // Simulate 500ms response time
                    stopwatch.Stop();
                    responseTimes.Add(stopwatch.ElapsedMilliseconds);
                })
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"text\":\"SLA test\"}")
                });

            var audioData = CreateValidWavData();

            // Act
            await _whisperService.TranscribeAudioAsync(audioData);

            // Assert
            Assert.IsTrue(responseTimes.Count > 0, "Should have measured response time");
            var avgResponseTime = responseTimes.Average();
            Assert.IsTrue(avgResponseTime < 2000, $"Average response time {avgResponseTime}ms exceeds SLA of 2000ms");
        }

        #endregion

        #region Helper Methods

        private byte[] CreateValidWavData()
        {
            // Use existing helper from tests
            return IntegrationTestHelpers.CreateValidWavData();
        }

        private byte[] CreateLargeWavData(int durationSeconds)
        {
            // Generate large WAV file
            const int sampleRate = 16000;
            const short bitsPerSample = 16;
            const short channels = 1;
            const int byteRate = sampleRate * channels * bitsPerSample / 8;
            const int blockAlign = channels * bitsPerSample / 8;
            
            int dataSize = sampleRate * durationSeconds * blockAlign;
            int fileSize = 36 + dataSize;
            
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            // RIFF header
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(fileSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            
            // fmt subchunk
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16); // Subchunk size
            writer.Write((short)1); // Audio format (PCM)
            writer.Write(channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)blockAlign);
            writer.Write(bitsPerSample);
            
            // Data subchunk
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            
            // Write audio data
            for (int i = 0; i < dataSize / 2; i++)
            {
                writer.Write((short)(Math.Sin(i * 0.1) * short.MaxValue));
            }
            
            return ms.ToArray();
        }

        #endregion

        #region Test Helper Classes

        private class StreamingHttpContent : HttpContent
        {
            private readonly string _response;

            public StreamingHttpContent(string response)
            {
                _response = response;
            }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                using var writer = new StreamWriter(stream);
                return writer.WriteAsync(_response);
            }

            protected override bool TryComputeLength(out long length)
            {
                length = _response.Length;
                return true;
            }
        }

        #endregion
    }
}
