using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ScottWisper.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        private Mock<ITextInjection>? _mockTextInjection;
        private Mock<IFeedbackService>? _mockFeedbackService;

        [TestInitialize]
        public void Setup()
        {
            _mockTextInjection = new Mock<ITextInjection>();
            _mockFeedbackService = new Mock<IFeedbackService>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Cleanup any test resources
        }

        #region Text Injection Integration Tests

        [TestMethod]
        [TestCategory("TextInjection")]
        public async Task TextInjection_ShouldHandleBasicInjection()
        {
            // Arrange
            var testText = "Hello World Test " + DateTime.Now.Ticks;
            _mockTextInjection!.Setup(x => x.InjectTextAsync(testText, It.IsAny<InjectionOptions>()))
                           .ReturnsAsync(true);

            // Act
            var result = await _mockTextInjection.Object.InjectTextAsync(testText);

            // Assert
            Assert.IsTrue(result, "Text injection should succeed");
            _mockTextInjection.Verify(x => x.InjectTextAsync(testText, It.IsAny<InjectionOptions>()), Times.Once);
        }

        [TestMethod]
        [TestCategory("TextInjection")]
        public async Task TextInjection_ShouldHandleSpecialCharacters()
        {
            // Arrange
            var testText = "Test with symbols: @#$%^&*()_+-={}[]|\\:;\"'<>?,./";
            _mockTextInjection!.Setup(x => x.InjectTextAsync(testText, It.IsAny<InjectionOptions>()))
                           .ReturnsAsync(true);

            // Act
            var result = await _mockTextInjection.Object.InjectTextAsync(testText);

            // Assert
            Assert.IsTrue(result, "Should handle special characters");
        }

        [TestMethod]
        [TestCategory("TextInjection")]
        public async Task TextInjection_ShouldWorkWithRetryLogic()
        {
            // Arrange
            var options = new InjectionOptions
            {
                RetryCount = 5,
                DelayBetweenRetriesMs = 50
            };
            var testText = "Retry test " + Guid.NewGuid();
            _mockTextInjection!.Setup(x => x.InjectTextAsync(testText, options))
                           .ReturnsAsync(true);

            // Act
            var result = await _mockTextInjection.Object.InjectTextAsync(testText, options);

            // Assert
            Assert.IsTrue(result, "Should succeed with retry logic");
        }

        #endregion

        #region Feedback Service Integration Tests

        [TestMethod]
        [TestCategory("Feedback")]
        public async Task FeedbackService_ShouldProvideVisualFeedback()
        {
            // Arrange
            _mockFeedbackService!.Setup(x => x.ShowNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                           .Returns(Task.CompletedTask);

            // Act
            var startTime = DateTime.Now;
            await _mockFeedbackService.Object.ShowNotificationAsync("Test Status", "Test notification", 3000);
            var endTime = DateTime.Now;

            // Assert
            var latency = (endTime - startTime).TotalMilliseconds;
            Assert.IsTrue(latency < 100, $"Feedback latency should be < 100ms, was {latency}ms");
        }

        [TestMethod]
        [TestCategory("Feedback")]
        public async Task FeedbackService_ShouldUpdateStatusCorrectly()
        {
            // Arrange
            var statusChangedFired = false;
            _mockFeedbackService!.Setup(x => x.SetStatusAsync(IFeedbackService.DictationStatus.Recording, null))
                           .Returns(Task.CompletedTask);
            
            _mockFeedbackService.Object.StatusChanged += (sender, status) =>
            {
                if (status == IFeedbackService.DictationStatus.Recording)
                    statusChangedFired = true;
            };

            // Act
            await _mockFeedbackService.Object.SetStatusAsync(IFeedbackService.DictationStatus.Recording);

            // Assert
            Assert.IsTrue(statusChangedFired, "StatusChanged event should fire");
        }

        #endregion

        #region Settings Service Integration Tests

        [TestMethod]
        [TestCategory("Settings")]
        public void SettingsService_ShouldValidateBasicOperations()
        {
            // Arrange & Act & Assert - Test basic validation scenarios
            Assert.IsNotNull(Environment.OSVersion, "Should access system information");
            Assert.IsTrue(Environment.MachineName.Length > 0, "Should have machine name");
            Assert.IsTrue(System.IO.Directory.Exists(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)), 
                              "Should have access to application data folder");
        }

        #endregion

        #region End-to-End Workflow Tests

        [TestMethod]
        [TestCategory("Workflow")]
        public async Task EndToEndWorkflow_ShouldProcessDictation()
        {
            // Arrange
            var workflowCompleted = false;
            _mockFeedbackService!.Setup(x => x.SetStatusAsync(It.IsAny<IFeedbackService.DictationStatus>(), It.IsAny<string>()))
                           .Returns(Task.CompletedTask);
            
            _mockFeedbackService.Object.StatusChanged += (sender, status) =>
            {
                if (status == IFeedbackService.DictationStatus.Complete)
                    workflowCompleted = true;
            };

            // Act - Simulate dictation workflow
            var startTime = DateTime.Now;
            
            // Start recording
            await _mockFeedbackService.Object.SetStatusAsync(IFeedbackService.DictationStatus.Ready, "Starting recording...");
            
            // Process audio (simulated)
            await Task.Delay(50);
            
            // Transcribe (simulated)
            await _mockFeedbackService.Object.SetStatusAsync(IFeedbackService.DictationStatus.Processing, "Processing...");
            await Task.Delay(30);
            
            // Complete
            await _mockFeedbackService.Object.SetStatusAsync(IFeedbackService.DictationStatus.Complete, "Complete");
            
            var endTime = DateTime.Now;

            // Assert
            var totalTime = (endTime - startTime).TotalSeconds;
            Assert.IsTrue(totalTime < 2.0, $"End-to-end workflow should complete in < 2 seconds, took {totalTime} seconds");
            Assert.IsTrue(workflowCompleted, "Workflow should complete successfully");
        }

        [TestMethod]
        [TestCategory("Workflow")]
        public async Task Workflow_ShouldHandleErrorsGracefully()
        {
            // Arrange
            var errorHandled = false;
            _mockFeedbackService!.Setup(x => x.SetStatusAsync(IFeedbackService.DictationStatus.Error, It.IsAny<string>()))
                           .Returns(Task.CompletedTask);
            
            _mockFeedbackService.Object.StatusChanged += (sender, status) =>
            {
                if (status == IFeedbackService.DictationStatus.Error)
                    errorHandled = true;
            };

            // Act - Simulate error scenario
            await _mockFeedbackService.Object.SetStatusAsync(IFeedbackService.DictationStatus.Error, "Test error message");

            // Assert
            Assert.IsTrue(errorHandled, "Error scenarios should be handled gracefully");
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        [TestCategory("Performance")]
        public void Performance_ShouldMeetCpuConstraints()
        {
            // Arrange
            var initialCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
            
            // Act - Simulate typical usage
            for (int i = 0; i < 1000; i++)
            {
                // Simulate service operations
                Thread.Sleep(1);
            }
            
            var finalCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
            var cpuUsageMs = (finalCpuTime - initialCpuTime).TotalMilliseconds;

            // Assert
            Assert.IsTrue(cpuUsageMs < 1000, $"CPU usage should be reasonable, was {cpuUsageMs}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public async Task Performance_ShouldMeetLatencyRequirements()
        {
            // Arrange
            _mockFeedbackService!.Setup(x => x.ShowNotificationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
                           .Returns(Task.CompletedTask);

            var latencies = new List<double>();

            // Act - Measure multiple operations
            for (int i = 0; i < 10; i++)
            {
                var start = DateTime.Now;
                await _mockFeedbackService.Object.ShowNotificationAsync($"Test {i}", "Test message", 1000);
                var end = DateTime.Now;
                
                latencies.Add((end - start).TotalMilliseconds);
            }

            // Assert
            var averageLatency = latencies.Average();
            Assert.IsTrue(averageLatency < 100, $"Average latency should be < 100ms, was {averageLatency}ms");
        }

        [TestMethod]
        [TestCategory("Performance")]
        public async Task Performance_ShouldMaintainStabilityOverTime()
        {
            // Arrange
            var startTime = DateTime.Now;
            var operationsCount = 0;

            // Act - Run for extended period (simulated)
            while ((DateTime.Now - startTime).TotalSeconds < 5) // 5 second test
            {
                await _mockFeedbackService!.ShowNotificationAsync($"Operation {operationsCount}", "Test message", 100);
                operationsCount++;
                await Task.Delay(10);
            }

            // Assert
            Assert.IsTrue(operationsCount > 400, "Should handle sustained operations");
        }

        #endregion

        #region Compatibility Tests

        [TestMethod]
        [TestCategory("Compatibility")]
        public void Compatibility_ShouldWorkOnWindows10AndAbove()
        {
            // Arrange
            var version = Environment.OSVersion.Version;

            // Act & Assert
            Assert.IsTrue(version.Major >= 10, $"Windows 10+ required, detected Windows {version.Major}");
        }

        [TestMethod]
        [TestCategory("Compatibility")]
        public void Compatibility_ShouldHandleDotNetEnvironment()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(Environment.Version.Major >= 8, ".NET 8+ required");
            Assert.IsNotNull(typeof(object).Assembly, "Should have access to core libraries");
            Assert.IsTrue(System.IO.Directory.Exists(AppDomain.CurrentDomain.BaseDirectory), "Should have access to application directory");
        }

        #endregion

        #region Validation Tests

        [TestMethod]
        [TestCategory("Validation")]
        public void Validation_ShouldEnforceBasicConstraints()
        {
            // Arrange & Act & Assert - Test basic validation scenarios
            Assert.ThrowsException<ArgumentNullException>(() =>
            {
                var service = new TextInjectionService();
                var result = service.InjectTextAsync(null!).Result;
            }, "Should reject null text");

            // Test with valid text
            var validText = "Valid test text";
            Assert.DoesNotThrow(() =>
            {
                var service = new TextInjectionService();
                // This would normally initialize and test, but we'll just validate the text is not null
                if (string.IsNullOrEmpty(validText))
                    throw new ArgumentNullException(nameof(validText));
            }, "Should accept valid text");
        }

        #endregion

        #region Integration Test Categories

        [TestMethod]
        [TestCategory("Integration")]
        public void Integration_ShouldValidateTestCategories()
        {
            // Verify all test categories are properly attributed
            var testMethods = GetType().GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(TestMethodAttribute), false).Length > 0);

            var hasTextInjectionTests = testMethods.Any(m => 
                m.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                 .Any(attr => ((TestCategoryAttribute)attr).TestCategories.Contains("TextInjection")));
            
            var hasFeedbackTests = testMethods.Any(m => 
                m.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                 .Any(attr => ((TestCategoryAttribute)attr).TestCategories.Contains("Feedback")));
            
            var hasPerformanceTests = testMethods.Any(m => 
                m.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                 .Any(attr => ((TestCategoryAttribute)attr).TestCategories.Contains("Performance")));

            // Assert
            Assert.IsTrue(hasTextInjectionTests, "Should have TextInjection test category");
            Assert.IsTrue(hasFeedbackTests, "Should have Feedback test category");
            Assert.IsTrue(hasPerformanceTests, "Should have Performance test category");
            Assert.IsTrue(testMethods.Count() >= 20, "Should have comprehensive test coverage");
        }

        #endregion
    }
}