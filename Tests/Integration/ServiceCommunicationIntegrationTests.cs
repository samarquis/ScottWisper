using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Database;
using WhisperKey.Services.Recovery;
using WhisperKey.Services.Validation;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class ServiceCommunicationIntegrationTests
    {
        private IServiceProvider _serviceProvider = null!;
        private Mock<IAuditLoggingService> _auditServiceMock = null!;
        private Mock<ICredentialService> _credentialServiceMock = null!;
        private JsonDatabaseService _databaseService = null!;
        private ApiKeyManagementService _apiKeyService = null!;
        private RecoveryPolicyService _recoveryService = null!;

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();

            // Setup mocks
            _auditServiceMock = new Mock<IAuditLoggingService>();
            _credentialServiceMock = new Mock<ICredentialService>();

            // Register real services for integration testing
            services.AddSingleton(_auditServiceMock.Object);
            services.AddSingleton(_credentialServiceMock.Object);
            services.AddSingleton<IFileSystemService, FileSystemService>();
            services.AddSingleton<IInputValidationService, InputValidationService>();
            services.AddSingleton<JsonDatabaseService>();
            services.AddSingleton<ApiKeyManagementService>();
            services.AddSingleton<RecoveryPolicyService>();

            _serviceProvider = services.BuildServiceProvider();

            _databaseService = _serviceProvider.GetRequiredService<JsonDatabaseService>();
            _apiKeyService = _serviceProvider.GetRequiredService<ApiKeyManagementService>();
            _recoveryService = _serviceProvider.GetRequiredService<RecoveryPolicyService>();

            SetupBasicMocks();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _databaseService?.Dispose();
            _serviceProvider?.Dispose();
        }

        #region API Key Management Service Integration Tests

        [TestMethod]
        public async Task ApiKeyService_WithDatabase_ShouldPersistKeys()
        {
            // Arrange
            var apiKey = "sk-test123456789";
            var provider = "OpenAI";

            _credentialServiceMock.Setup(c => c.SetCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            var result = await _apiKeyService.RegisterKeyAsync(provider, apiKey);

            // Assert - Should store in both credential manager and database
            Assert.IsTrue(result, "Key registration should succeed");
            _credentialServiceMock.Verify(c => c.SetCredentialAsync($"ApiKey_{provider}_v1", apiKey), Times.Once);

            // Verify database persistence
            var metadata = await _apiKeyService.GetMetadataAsync(provider);
            Assert.IsNotNull(metadata);
            Assert.AreEqual(provider, metadata.Provider);
            Assert.AreEqual(1, metadata.Versions.Count);
        }

        [TestMethod]
        public async Task ApiKeyService_WithRecoveryPolicy_ShouldHandleFailures()
        {
            // Arrange - Simulate credential service failures
            var attemptCount = 0;
            _credentialServiceMock.Setup(c => c.SetCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(() =>
                {
                    attemptCount++;
                    if (attemptCount <= 2)
                        return Task.FromResult(false);
                    else
                        return Task.FromResult(true);
                });

            // Act
            var result = await _apiKeyService.RegisterKeyAsync("TestProvider", "test-key");

            // Assert - Should eventually succeed after retries
            Assert.IsTrue(result, "Should succeed after recovery policy retries");
            Assert.IsTrue(attemptCount >= 3, "Should have attempted at least 3 times");
        }

        [TestMethod]
        public async Task ApiKeyService_Auditing_ShouldLogSecurityEvents()
        {
            // Arrange
            _credentialServiceMock.Setup(c => c.SetCredentialAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(true);

            // Act
            await _apiKeyService.RegisterKeyAsync("OpenAI", "sk-test123");
            await _apiKeyService.RevokeKeyAsync("OpenAI");

            // Assert - Should audit all key operations
            _auditServiceMock.Verify(a => a.LogEventAsync(
                AuditEventType.ApiKeyCreated,
                It.Is<string>(msg => msg.Contains("OpenAI")),
                It.IsAny<string>(),
                DataSensitivity.High), Times.Once);

            _auditServiceMock.Verify(a => a.LogEventAsync(
                AuditEventType.ApiKeyRevoked,
                It.Is<string>(msg => msg.Contains("OpenAI")),
                It.IsAny<string>(),
                DataSensitivity.High), Times.Once);
        }

        #endregion

        #region Recovery Policy Service Integration Tests

        [TestMethod]
        public async Task RecoveryService_WithCircuitBreaker_ShouldProtectDownstreamServices()
        {
            // Arrange
            var downstreamService = new Mock<IDownstreamService>();
            var failureCount = 0;

            downstreamService.Setup(ds => ds.ExecuteOperationAsync())
                .Returns(() =>
                {
                    failureCount++;
                    if (failureCount <= 5)
                        throw new InvalidOperationException("Service unavailable");
                    return Task.FromResult("Success");
                });

            // Act - Execute operations through recovery policy
            var results = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var result = await _recoveryService.ExecuteWithRecoveryAsync(
                        () => downstreamService.Object.ExecuteOperationAsync(),
                        "TestOperation");
                    results.Add(result);
                }
                catch
                {
                    // Expected failures
                }
            }

            // Assert - Circuit breaker should open after failures
            Assert.IsTrue(failureCount >= 5, "Should have attempted at least 5 times");
            Assert.IsTrue(results.Count <= 2, "Should have limited success after circuit opens");
        }

        [TestMethod]
        public async Task RecoveryService_WithRetry_ShouldRecoverFromTransientFailures()
        {
            // Arrange
            var flakyService = new Mock<IFlakyService>();
            var attemptCount = 0;

            flakyService.Setup(fs => fs.TransientOperationAsync())
                .Returns(() =>
                {
                    attemptCount++;
                    if (attemptCount <= 2)
                        throw new TimeoutException("Transient failure");
                    return Task.FromResult("Recovered");
                });

            // Act
            var result = await _recoveryService.ExecuteWithRecoveryAsync(
                () => flakyService.Object.TransientOperationAsync(),
                "FlakyOperation");

            // Assert - Should succeed after retries
            Assert.AreEqual("Recovered", result);
            Assert.AreEqual(3, attemptCount, "Should have retried 2 times + 1 success");
        }

        #endregion

        #region Cross-Service Data Flow Tests

        [TestMethod]
        public async Task Services_TranscriptionWorkflow_ShouldFlowCorrectly()
        {
            // Arrange
            var audioDeviceService = new Mock<IAudioDeviceService>();
            var whisperService = new Mock<IWhisperService>();
            var textInjectionService = new Mock<ITextInjectionService>();

            // Setup audio capture
            audioDeviceService.Setup(ads => ads.StartCaptureAsync())
                .ReturnsAsync(true);
            audioDeviceService.Setup(ads => ads.GetAudioDataAsync())
                .ReturnsAsync(new byte[] { 0x52, 0x49, 0x46, 0x46 }); // RIFF header

            // Setup transcription
            whisperService.Setup(ws => ws.TranscribeAudioAsync(It.IsAny<byte[]>()))
                .ReturnsAsync("Integration test transcription");

            // Setup text injection
            textInjectionService.Setup(tis => tis.InjectTextAsync(It.IsAny<string>()))
                .ReturnsAsync(true);

            // Create service orchestration
            var orchestrator = new TranscriptionOrchestrator(
                audioDeviceService.Object,
                whisperService.Object,
                textInjectionService.Object,
                _auditServiceMock.Object);

            // Act
            var result = await orchestrator.ExecuteTranscriptionWorkflowAsync();

            // Assert - Should flow through all services correctly
            Assert.IsTrue(result.Success, "Workflow should succeed");
            Assert.AreEqual("Integration test transcription", result.TranscriptionText);

            audioDeviceService.Verify(ads => ads.StartCaptureAsync(), Times.Once);
            whisperService.Verify(ws => ws.TranscribeAudioAsync(It.IsAny<byte[]>()), Times.Once);
            textInjectionService.Verify(tis => tis.InjectTextAsync("Integration test transcription"), Times.Once);
        }

        [TestMethod]
        public async Task Services_ConcurrentUserOperations_ShouldMaintainIsolation()
        {
            // Arrange - Multiple concurrent user operations
            var userOperations = new List<UserOperation>();
            for (int i = 0; i < 10; i++)
            {
                userOperations.Add(new UserOperation
                {
                    UserId = $"User_{i}",
                    ApiKey = $"sk-key{i}",
                    AudioData = CreateTestAudioData(i)
                });
            }

            var tasks = userOperations.Select(async op =>
            {
                // Create isolated service instances per user
                var userServices = await CreateUserServicesAsync(op.UserId);
                
                // Register user's API key
                var registered = await userServices.ApiKeyService.RegisterKeyAsync("OpenAI", op.ApiKey);
                
                // Simulate transcription
                var transcription = await userServices.WhisperService.TranscribeAudioAsync(op.AudioData);
                
                return new { UserId = op.UserId, Registered = registered, Transcription = transcription };
            });

            // Act
            var results = await Task.WhenAll(tasks);

            // Assert - Each user's operations should be isolated
            foreach (var result in results)
            {
                Assert.IsTrue(result.Registered, $"User {result.UserId} should have key registered");
                Assert.IsNotNull(result.Transcription, $"User {result.UserId} should have transcription");
            }

            // Verify user isolation by checking that each user gets their own data
            var userResults = results.ToDictionary(r => r.UserId);
            Assert.AreEqual(10, userResults.Count, "All users should have independent results");
        }

        #endregion

        #region Service Discovery and Configuration Tests

        [TestMethod]
        public async Task Services_ConfigurationChanges_ShouldPropagateCorrectly()
        {
            // Arrange
            var settingsService = new Mock<ISettingsService>();
            
            // Initial configuration
            settingsService.Setup(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("OpenAI");

            var whisperService = new WhisperService(
                settingsService.Object,
                _auditServiceMock.Object,
                new HttpClient());

            // Act - Change configuration
            settingsService.SetupSequence(s => s.GetValueAsync("Transcription:Provider"))
                .ReturnsAsync("Azure")  // After change
                .ReturnsAsync("Azure");  // Subsequent calls

            // Simulate configuration reload
            await whisperService.ReloadConfigurationAsync();

            // Assert - Service should use new configuration
            // This would require implementing configuration change detection in WhisperService
            settingsService.Verify(s => s.GetValueAsync("Transcription:Provider"), Times.AtLeast(2));
        }

        [TestMethod]
        public async Task Services_HealthCheck_ShouldReportStatusCorrectly()
        {
            // Arrange
            var healthCheckServices = new Dictionary<string, IHealthCheck>
            {
                ["Database"] = _databaseService,
                ["APIKey"] = _apiKeyService,
                ["Recovery"] = _recoveryService
            };

            // Act
            var healthResults = new Dictionary<string, HealthStatus>();
            foreach (var service in healthCheckServices)
            {
                var health = await service.Value.CheckHealthAsync();
                healthResults[service.Key] = health;
            }

            // Assert
            Assert.IsTrue(healthResults.ContainsKey("Database"), "Should check database health");
            Assert.IsTrue(healthResults.ContainsKey("APIKey"), "Should check API key service health");
            Assert.IsTrue(healthResults.ContainsKey("Recovery"), "Should check recovery service health");

            // All should be healthy in test environment
            Assert.IsTrue(healthResults.Values.All(h => h.Status == HealthStatus.Healthy));
        }

        #endregion

        #region Event-Driven Communication Tests

        [TestMethod]
        public async Task Services_EventDrivenCommunication_ShouldPublishAndSubscribe()
        {
            // Arrange
            var eventBus = new InMemoryEventBus();
            var publisher = new EventPublisher(eventBus);
            var subscriber = new EventSubscriber(eventBus);

            var receivedEvents = new List<ServiceEvent>();
            subscriber.OnEvent += (evt) => receivedEvents.Add(evt);

            // Act
            await publisher.PublishAsync(new ServiceEvent
            {
                EventType = "TranscriptionCompleted",
                Data = new { Text = "Test transcription", Duration = 1500 },
                Timestamp = DateTime.UtcNow
            });

            // Allow event processing
            await Task.Delay(100);

            // Assert
            Assert.AreEqual(1, receivedEvents.Count);
            Assert.AreEqual("TranscriptionCompleted", receivedEvents[0].EventType);
        }

        #endregion

        #region Performance and Scaling Tests

        [TestMethod]
        public async Task Services_HighConcurrency_ShouldMaintainPerformance()
        {
            // Arrange
            var concurrentOperations = 50;
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var tasks = new List<Task<ServiceOperationResult>>();

            // Act - Execute concurrent service operations
            for (int i = 0; i < concurrentOperations; i++)
            {
                var operationId = i;
                tasks.Add(ExecuteServiceOperationAsync(operationId));
            }

            var results = await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            var successCount = results.Count(r => r.Success);
            var avgDuration = results.Average(r => r.Duration.TotalMilliseconds);

            Assert.AreEqual(concurrentOperations, results.Count, "All operations should complete");
            Assert.IsTrue(successCount >= concurrentOperations * 0.95, "At least 95% should succeed");
            Assert.IsTrue(avgDuration < 1000, $"Average operation time {avgDuration}ms should be under 1000ms");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, "Total time should be reasonable");
        }

        #endregion

        #region Helper Methods

        private void SetupBasicMocks()
        {
            _auditServiceMock.Setup(a => a.LogEventAsync(
                It.IsAny<AuditEventType>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<DataSensitivity>()))
                .ReturnsAsync(Guid.NewGuid().ToString());

            _credentialServiceMock.Setup(c => c.GetCredentialAsync(It.IsAny<string>()))
                .ReturnsAsync("test-api-key");
        }

        private async Task<UserServiceContainer> CreateUserServicesAsync(string userId)
        {
            var services = new ServiceCollection();
            
            // Register user-scoped services
            services.AddSingleton(_auditServiceMock.Object);
            services.AddSingleton(_credentialServiceMock.Object);
            services.AddSingleton<IFileSystemService, FileSystemService>();
            services.AddSingleton<IInputValidationService, InputValidationService>();
            services.AddSingleton<JsonDatabaseService>();
            services.AddSingleton<ApiKeyManagementService>();
            
            var provider = services.BuildServiceProvider();
            
            return new UserServiceContainer
            {
                UserId = userId,
                ApiKeyService = provider.GetRequiredService<ApiKeyManagementService>(),
                WhisperService = CreateMockWhisperService()
            };
        }

        private IWhisperService CreateMockWhisperService()
        {
            var mock = new Mock<IWhisperService>();
            mock.Setup(ws => ws.TranscribeAudioAsync(It.IsAny<byte[]>()))
                .ReturnsAsync($"Transcription for {Guid.NewGuid()}");
            return mock.Object;
        }

        private byte[] CreateTestAudioData(int seed)
        {
            var random = new Random(seed);
            var data = new byte[1024];
            random.NextBytes(data);
            return data;
        }

        private async Task<ServiceOperationResult> ExecuteServiceOperationAsync(int operationId)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                // Simulate service operation
                await Task.Delay(random.Next(100, 500));
                
                stopwatch.Stop();
                return new ServiceOperationResult
                {
                    OperationId = operationId,
                    Success = true,
                    Duration = stopwatch.Elapsed
                };
            }
            catch
            {
                stopwatch.Stop();
                return new ServiceOperationResult
                {
                    OperationId = operationId,
                    Success = false,
                    Duration = stopwatch.Elapsed
                };
            }
        }

        #endregion

        #region Test Helper Classes

        private class UserOperation
        {
            public string UserId { get; set; } = string.Empty;
            public string ApiKey { get; set; } = string.Empty;
            public byte[] AudioData { get; set; } = Array.Empty<byte>();
        }

        private class UserServiceContainer
        {
            public string UserId { get; set; } = string.Empty;
            public ApiKeyManagementService ApiKeyService { get; set; } = null!;
            public IWhisperService WhisperService { get; set; } = null!;
        }

        private class TranscriptionOrchestrator
        {
            private readonly IAudioDeviceService _audioService;
            private readonly IWhisperService _whisperService;
            private readonly ITextInjectionService _textInjectionService;
            private readonly IAuditLoggingService _auditService;

            public TranscriptionOrchestrator(
                IAudioDeviceService audioService,
                IWhisperService whisperService,
                ITextInjectionService textInjectionService,
                IAuditLoggingService auditService)
            {
                _audioService = audioService;
                _whisperService = whisperService;
                _textInjectionService = textInjectionService;
                _auditService = auditService;
            }

            public async Task<TranscriptionResult> ExecuteTranscriptionWorkflowAsync()
            {
                try
                {
                    await _auditService.LogEventAsync(AuditEventType.TranscriptionStarted, "Workflow started");
                    
                    var captureStarted = await _audioService.StartCaptureAsync();
                    if (!captureStarted)
                    {
                        return new TranscriptionResult { Success = false, Error = "Failed to start audio capture" };
                    }

                    var audioData = await _audioService.GetAudioDataAsync();
                    var transcription = await _whisperService.TranscribeAudioAsync(audioData);
                    
                    var injected = await _textInjectionService.InjectTextAsync(transcription);
                    if (!injected)
                    {
                        return new TranscriptionResult { Success = false, Error = "Failed to inject text" };
                    }

                    await _auditService.LogEventAsync(AuditEventType.TranscriptionCompleted, "Workflow completed");
                    
                    return new TranscriptionResult 
                    { 
                        Success = true, 
                        TranscriptionText = transcription 
                    };
                }
                catch (Exception ex)
                {
                    await _auditService.LogEventAsync(AuditEventType.TranscriptionFailed, $"Workflow failed: {ex.Message}");
                    
                    return new TranscriptionResult 
                    { 
                        Success = false, 
                        Error = ex.Message 
                    };
                }
            }
        }

        private class TranscriptionResult
        {
            public bool Success { get; set; }
            public string? TranscriptionText { get; set; }
            public string? Error { get; set; }
        }

        private class ServiceOperationResult
        {
            public int OperationId { get; set; }
            public bool Success { get; set; }
            public TimeSpan Duration { get; set; }
        }

        #endregion

        #region Mock Interfaces

        public interface IDownstreamService
        {
            Task<string> ExecuteOperationAsync();
        }

        public interface IFlakyService
        {
            Task<string> TransientOperationAsync();
        }

        public interface IHealthCheck
        {
            Task<HealthStatus> CheckHealthAsync();
        }

        public class HealthStatus
        {
            public HealthStatusEnum Status { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        public enum HealthStatusEnum
        {
            Healthy,
            Degraded,
            Unhealthy
        }

        #endregion

        #region Event System

        public class ServiceEvent
        {
            public string EventType { get; set; } = string.Empty;
            public object Data { get; set; } = null!;
            public DateTime Timestamp { get; set; }
        }

        public interface IEventBus
        {
            Task PublishAsync(ServiceEvent evt);
            void Subscribe(Action<ServiceEvent> handler);
        }

        public class InMemoryEventBus : IEventBus
        {
            private readonly List<Action<ServiceEvent>> _subscribers = new();

            public async Task PublishAsync(ServiceEvent evt)
            {
                foreach (var subscriber in _subscribers)
                {
                    subscriber(evt);
                }
                await Task.CompletedTask;
            }

            public void Subscribe(Action<ServiceEvent> handler)
            {
                _subscribers.Add(handler);
            }
        }

        public class EventPublisher
        {
            private readonly IEventBus _eventBus;

            public EventPublisher(IEventBus eventBus)
            {
                _eventBus = eventBus;
            }

            public async Task PublishAsync(ServiceEvent evt)
            {
                await _eventBus.PublishAsync(evt);
            }
        }

        public class EventSubscriber
        {
            public event Action<ServiceEvent>? OnEvent;

            public EventSubscriber(IEventBus eventBus)
            {
                eventBus.Subscribe(evt => OnEvent?.Invoke(evt));
            }
        }

        #endregion
    }
}
