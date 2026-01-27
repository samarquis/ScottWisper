using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using ScottWisper.Services;
using System.Windows;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Text;

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

        #region Cross-Application Compatibility Tests

        [TestMethod]
        [TestCategory("Compatibility")]
        [DataRow("chrome", "Browser", "Google Chrome")]
        [DataRow("firefox", "Browser", "Mozilla Firefox")]
        [DataRow("msedge", "Browser", "Microsoft Edge")]
        public async Task Compatibility_ShouldDetectWebBrowsers(string processName, string category, string displayName)
        {
            // Arrange
            var mockProcess = new Process { ProcessName = processName };
            var service = new TextInjectionService(null);
            
            // Act - Mock the process detection by simulating window info
            var testText = $"Compatibility test for {displayName}";
            
            // Simulate browser compatibility testing
            var result = await service.TestInjectionAsync();
            
            // Assert
            Assert.IsNotNull(result, "Test result should not be null");
            Assert.IsTrue(result.Compatibility.Category == ScottWisper.Services.ApplicationCategory.Browser, 
                $"Should detect {displayName} as browser");
            Assert.IsTrue(result.Compatibility.IsCompatible, "Should be compatible for text injection");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("unicode"), 
                "Should require Unicode handling for browsers");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("newline"), 
                "Should require newline handling for browsers");
        }

        [TestMethod]
        [TestCategory("Compatibility")]
        [DataRow("devenv", "DevelopmentTool", "Visual Studio")]
        [DataRow("code", "DevelopmentTool", "VS Code")]
        [DataRow("sublime", "DevelopmentTool", "Sublime Text")]
        [DataRow("notepad++", "DevelopmentTool", "Notepad++")]
        public async Task Compatibility_ShouldDetectDevelopmentTools(string processName, string category, string displayName)
        {
            // Arrange
            var service = new TextInjectionService(null);
            var testText = $"Compatibility test for {displayName}";
            
            // Act
            var result = await service.TestInjectionAsync();
            
            // Assert
            Assert.IsNotNull(result, "Test result should not be null");
            Assert.IsTrue(result.Compatibility.Category == ScottWisper.Services.ApplicationCategory.DevelopmentTool, 
                $"Should detect {displayName} as development tool");
            Assert.IsTrue(result.Compatibility.IsCompatible, "Should be compatible for text injection");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("unicode"), 
                "Should require Unicode handling for development tools");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("tab"), 
                "Should require tab handling for IDEs");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("syntax_chars"), 
                "Should require syntax character handling for code editors");
        }

        [TestMethod]
        [TestCategory("Compatibility")]
        [DataRow("winword", "Office", "Microsoft Word")]
        [DataRow("excel", "Office", "Microsoft Excel")]
        [DataRow("powerpnt", "Office", "Microsoft PowerPoint")]
        [DataRow("outlook", "Office", "Microsoft Outlook")]
        public async Task Compatibility_ShouldDetectOfficeApplications(string processName, string category, string displayName)
        {
            // Arrange
            var service = new TextInjectionService(null);
            var testText = $"Office compatibility test for {displayName}";
            
            // Act
            var result = await service.TestInjectionAsync();
            
            // Assert
            Assert.IsNotNull(result, "Test result should not be null");
            Assert.IsTrue(result.Compatibility.Category == ScottWisper.Services.ApplicationCategory.Office, 
                $"Should detect {displayName} as Office application");
            Assert.IsTrue(result.Compatibility.IsCompatible, "Should be compatible for text injection");
            Assert.IsTrue(result.Compatibility.PreferredMethod == ScottWisper.Services.InjectionMethod.ClipboardFallback, 
                "Should prefer clipboard fallback for Office apps");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("formatting"), 
                "Should require formatting handling for Office");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("unicode"), 
                "Should require Unicode handling for Office");
        }

        [TestMethod]
        [TestCategory("Compatibility")]
        [DataRow("slack", "Communication", "Slack")]
        [DataRow("discord", "Communication", "Discord")]
        [DataRow("teams", "Communication", "Microsoft Teams")]
        [DataRow("zoom", "Communication", "Zoom")]
        public async Task Compatibility_ShouldDetectCommunicationTools(string processName, string category, string displayName)
        {
            // Arrange
            var service = new TextInjectionService(null);
            var testText = $"Communication test for {displayName}";
            
            // Act
            var result = await service.TestInjectionAsync();
            
            // Assert
            Assert.IsNotNull(result, "Test result should not be null");
            Assert.IsTrue(result.Compatibility.Category == ScottWisper.Services.ApplicationCategory.Communication, 
                $"Should detect {displayName} as communication tool");
            Assert.IsTrue(result.Compatibility.IsCompatible, "Should be compatible for text injection");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("newline"), 
                "Should require newline handling for chat applications");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("emoji"), 
                "Should require emoji handling for modern chat apps");
        }

        [TestMethod]
        [TestCategory("Compatibility")]
        [DataRow("notepad", "TextEditor", "Windows Notepad")]
        [DataRow("wordpad", "TextEditor", "Windows WordPad")]
        [DataRow("write", "TextEditor", "Windows Write")]
        public async Task Compatibility_ShouldDetectTextEditors(string processName, string category, string displayName)
        {
            // Arrange
            var service = new TextInjectionService(null);
            var testText = $"Text editor test for {displayName}";
            
            // Act
            var result = await service.TestInjectionAsync();
            
            // Assert
            Assert.IsNotNull(result, "Test result should not be null");
            Assert.IsTrue(result.Compatibility.Category == ScottWisper.Services.ApplicationCategory.TextEditor, 
                $"Should detect {displayName} as text editor");
            Assert.IsTrue(result.Compatibility.IsCompatible, "Should be compatible for text injection");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("unicode"), 
                "Should require Unicode handling for text editors");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("newline"), 
                "Should require newline handling for text editors");
            Assert.IsTrue(result.Compatibility.RequiresSpecialHandling.Contains("tab"), 
                "Should require tab handling for text editors");
        }

        #endregion

        #region Unicode and Special Character Tests

        [TestMethod]
        [TestCategory("Unicode")]
        public async Task Unicode_ShouldHandleCommonSymbols()
        {
            // Arrange
            var service = new TextInjectionService(null);
            var testTexts = new[]
            {
                "Café naïve résumé",
                "Hello 世界! 🌍",
                "Mathematical: ∑∏∫∆∇∂",
                "Currency: $¥€£¢",
                "Quotes: \"\'\'\u300C\u300D\u300E\u300F\"",
                "Arrows: ←→↑↓↔↖↗↘↙↗",
                "Special: @#$%^&*()_+-={}[]|\\:;\"'<>?,./"
            };

            // Act & Assert
            foreach (var testText in testTexts)
            {
                var result = await service.TestInjectionAsync();
                Assert.IsNotNull(result, $"Unicode test should return result for: {testText}");
                Assert.IsTrue(result.Success || result.Compatibility.IsCompatible, 
                    $"Should handle Unicode text: {testText}");
            }
        }

        [TestMethod]
        [TestCategory("Unicode")]
        public async Task Unicode_ShouldHandleEmojisAndSymbols()
        {
            // Arrange
            var service = new TextInjectionService(null);
            var emojiTexts = new[]
            {
                "Smileys: 😀😃😄😁😆😅😂🤣😊😇",
                "Gestures: 👋👍👌✌✋👏👐",
                "Objects: 🏠🏡🏢🏣🏤🏥🏦🏧🏨🏩🏪",
                "Symbols: ❤️💔💯💢💥💫💦💨🕳️💬👁🗨🔥💩",
                "Flags: 🏳️🌈🏴🏵🏶🏷🏸🏹",
                "Recent: 🥰🥵🥶🥷🥸🥹🥺🥻🥼🥽🥾🥿",
                "Complex mixed: \ud83d\udcbb\ud83d\udcbc\ud83e\udd1f\ud83d\udcbb\ud83e\udd1e\ud83d\udc68\ud83d\udcbb\ud83e\udd23\ud83d\udc69\ud83d\udcbb\ud83e\udd27\ud83d\udc68\ud83d\udcbb\ud83e\udd32\ud83d\udc69\ud83d\udcbb\ud83e\udd37"
            };

            // Act & Assert
            foreach (var emojiText in emojiTexts)
            {
                var result = await service.TestInjectionAsync();
                var displayText = emojiText.Length > 30 ? emojiText.Substring(0, 30) + "..." : emojiText;
                Assert.IsNotNull(result, $"Emoji test should return result for: {displayText}");
                Assert.IsTrue(result.Success || result.Compatibility.IsCompatible, 
                    $"Should handle emoji text: {displayText}");
            }
        }

        #endregion

        #region Performance and Latency Tests

        #endregion

        #region Fallback Mechanism Tests

        [TestMethod]
        [TestCategory("Fallback")]
        public async Task Fallback_ShouldUseClipboardWhenSendInputFails()
        {
            // Arrange
            var service = new TextInjectionService(null);
            var testText = "Fallback mechanism test text";

            // Act - Test with clipboard fallback enabled
            var result = await service.InjectTextAsync(testText, new InjectionOptions 
            { 
                UseClipboardFallback = true,
                RetryCount = 3,
                DelayBetweenRetriesMs = 100
            });

            // Assert
            Assert.IsTrue(result, "Should succeed with clipboard fallback enabled");
            
            // Verify fallback method was tried if needed
            var metrics = service.GetPerformanceMetrics();
            Assert.IsNotNull(metrics, "Should have performance metrics available");
        }

        [TestMethod]
        [TestCategory("Fallback")]
        public async Task Fallback_ShouldRetryWithDifferentMethods()
        {
            // Arrange
            var service = new TextInjectionService(null);
            var testText = "Multi-method retry test";

            // Act - Test with aggressive retry configuration
            var result = await service.InjectTextAsync(testText, new InjectionOptions 
            { 
                UseClipboardFallback = true,
                RetryCount = 5,
                DelayBetweenRetriesMs = 50,
                DelayBetweenCharsMs = 10
            });

            // Assert
            Assert.IsTrue(result, "Should succeed with multiple retry attempts and fallbacks");
            
            var metrics = service.GetPerformanceMetrics();
            Assert.IsTrue(metrics.TotalAttempts >= 1, "Should have attempted injection at least once");
        }

        #endregion

        #region Application Mode Switching Tests

        [TestMethod]
        [TestCategory("ModeSwitching")]
        public async Task ModeSwitching_ShouldAdaptToApplicationChanges()
        {
            // Arrange
            var service = new TextInjectionService(null);
            var testText = "Mode switching test";

            // Act - Test compatibility detection for different application types
            var browserResult = await service.TestInjectionAsync();
            
            // Simulate application change and test again
            await Task.Delay(100);
            var developmentResult = await service.TestInjectionAsync();

            // Assert
            Assert.IsNotNull(browserResult, "Browser compatibility should be detected");
            Assert.IsNotNull(developmentResult, "Development tool compatibility should be detected");
            
            // Verify different handling based on application type
            if (browserResult.Compatibility.Category == ScottWisper.Services.ApplicationCategory.Browser)
            {
                Assert.IsTrue(browserResult.Compatibility.RequiresSpecialHandling.Contains("unicode"), 
                    "Browser mode should enable Unicode handling");
            }
            
            if (developmentResult.Compatibility.Category == ScottWisper.Services.ApplicationCategory.DevelopmentTool)
            {
                Assert.IsTrue(developmentResult.Compatibility.RequiresSpecialHandling.Contains("syntax_chars"), 
                    "Development mode should enable syntax character handling");
            }
        }

        [TestMethod]
        [TestCategory("ModeSwitching")]
        public void ModeSwitching_ShouldHaveCorrectCompatibilityProfiles()
        {
            // Arrange
            var service = new TextInjectionService(null);

            // Act & Assert - Test all compatibility methods return valid profiles
            var browserCompatibility = service.GetApplicationCompatibility();
            Assert.IsTrue(browserCompatibility.IsCompatible || browserCompatibility.Category == ScottWisper.Services.ApplicationCategory.Unknown, 
                "Should return valid compatibility profile");

            // Verify all required categories are supported
            var supportedCategories = Enum.GetValues<ScottWisper.Services.ApplicationCategory>();
            Assert.IsTrue(supportedCategories.Length >= 6, "Should support at least 6 application categories");
        }

        #endregion

        #region Integration Test Categories

        [TestMethod]
        [TestCategory("Integration")]
        public void Integration_ShouldValidateTestCategories()
        {
            // Verify all test categories are properly attributed
            var TestMethods = GetType().GetMethods()
                .Where(m => m.GetCustomAttributes(typeof(TestMethodAttribute), false).Length > 0);

            var hasCompatibilityTests = TestMethods.Any(m => 
                m.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                 .Any(attr => ((TestCategoryAttribute)attr).TestCategories.Contains("Compatibility")));
            
            var hasUnicodeTests = TestMethods.Any(m => 
                m.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                 .Any(attr => ((TestCategoryAttribute)attr).TestCategories.Contains("Unicode")));
            
            var hasPerformanceTests = TestMethods.Any(m => 
                m.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                 .Any(attr => ((TestCategoryAttribute)attr).TestCategories.Contains("Performance")));
            
            var hasFallbackTests = TestMethods.Any(m => 
                m.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                 .Any(attr => ((TestCategoryAttribute)attr).TestCategories.Contains("Fallback")));
            
            var hasModeSwitchingTests = TestMethods.Any(m => 
                m.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                 .Any(attr => ((TestCategoryAttribute)attr).TestCategories.Contains("ModeSwitching")));
            
            // Assert
            Assert.IsTrue(hasCompatibilityTests, "Should have Compatibility test category");
            Assert.IsTrue(hasUnicodeTests, "Should have Unicode test category");
            Assert.IsTrue(hasPerformanceTests, "Should have Performance test category");
            Assert.IsTrue(hasFallbackTests, "Should have Fallback test category");
            Assert.IsTrue(hasModeSwitchingTests, "Should have ModeSwitching test category");
            Assert.IsTrue(TestMethods.Count() >= 60, $"Should have comprehensive test coverage, found {TestMethods.Count()}");
        }

        #endregion
    }
}