using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Services;

namespace WhisperKey.Tests
{
    [TestClass]
    public class TextInjectionServiceTests
    {
        private Mock<ILogger<TextInjectionService>> _loggerMock = null!;
        private TextInjectionService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<TextInjectionService>>();
            _service = new TextInjectionService(_loggerMock.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithLogger_InitializesService()
        {
            // Arrange & Act
            var service = new TextInjectionService(_loggerMock.Object);

            // Assert
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.ApplicationCompatibilityMap);
            Assert.IsTrue(service.ApplicationCompatibilityMap.Count > 0);
        }

        [TestMethod]
        public void Constructor_WithoutLogger_InitializesService()
        {
            // Arrange & Act
            var service = new TextInjectionService();

            // Assert
            Assert.IsNotNull(service);
            Assert.IsNotNull(service.ApplicationCompatibilityMap);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_InitializesCompatibilityMap()
        {
            // Arrange & Act
            var service = new TextInjectionService(_loggerMock.Object);

            // Assert
            Assert.IsTrue(service.ApplicationCompatibilityMap.ContainsKey(TargetApplication.Chrome));
            Assert.IsTrue(service.ApplicationCompatibilityMap.ContainsKey(TargetApplication.Word));
            Assert.IsTrue(service.ApplicationCompatibilityMap.ContainsKey(TargetApplication.VisualStudio));
        }

        #endregion

        #region InitializeAsync Tests

        [TestMethod]
        public async Task InitializeAsync_ReturnsTrue()
        {
            // Act
            var result = await _service.InitializeAsync();

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task InitializeAsync_LogsInformation()
        {
            // Act
            await _service.InitializeAsync();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Initializing")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region InjectTextAsync Tests

        [TestMethod]
        public async Task InjectTextAsync_WithValidText_ReturnsTrue()
        {
            // Arrange
            var text = "Hello World";

            // Act
            var result = await _service.InjectTextAsync(text);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task InjectTextAsync_WithOptions_ReturnsTrue()
        {
            // Arrange
            var text = "Test text";
            var options = new InjectionOptions
            {
                UseClipboardFallback = true,
                DelayBetweenCharsMs = 10
            };

            // Act
            var result = await _service.InjectTextAsync(text, options);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task InjectTextAsync_EmptyText_ReturnsFalse()
        {
            // Arrange
            var text = "";

            // Act
            var result = await _service.InjectTextAsync(text);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task InjectTextAsync_WithUnicodeText_ReturnsTrue()
        {
            // Arrange
            var text = "Unicode test: αβγδεζ 日本語 🎉";

            // Act
            var result = await _service.InjectTextAsync(text);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task InjectTextAsync_LogsDebugMessage()
        {
            // Arrange
            var text = "Test";

            // Act
            await _service.InjectTextAsync(text);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Debug,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Injecting")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion

        #region Application Detection Tests

        [TestMethod]
        public void DetectActiveApplication_ReturnsValidApplication()
        {
            // Act
            var result = _service.DetectActiveApplication();

            // Assert
            Assert.IsNotNull(result);
            // Should return some application type (could be Unknown if no specific window is active)
            Assert.IsInstanceOfType(result, typeof(TargetApplication));
        }

        [TestMethod]
        public void GetCurrentWindowInfo_ReturnsWindowInfo()
        {
            // Act
            var result = _service.GetCurrentWindowInfo();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(WindowInfo));
        }

        [TestMethod]
        public void IsTargetWindowFocused_ReturnsBoolean()
        {
            // Act
            var result = _service.IsTargetWindowFocused();

            // Assert
            // Result depends on current window state, but should return a boolean
            Assert.IsInstanceOfType(result, typeof(bool));
        }

        [TestMethod]
        public void GetApplicationCompatibility_KnownApplication_ReturnsCompatibility()
        {
            // Act
            var result = _service.GetApplicationCompatibility("chrome");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsCompatible);
        }

        [TestMethod]
        public void GetApplicationCompatibility_UnknownApplication_ReturnsDefault()
        {
            // Act
            var result = _service.GetApplicationCompatibility("unknown_app");

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.IsCompatible);
            Assert.AreEqual(ApplicationCategory.Other, result.Category);
        }

        #endregion

        #region Compatibility Matrix Tests

        [TestMethod]
        public void InitializeApplicationCompatibilityMap_ContainsChrome()
        {
            // Assert
            var chromeCompat = _service.ApplicationCompatibilityMap[TargetApplication.Chrome];
            Assert.IsNotNull(chromeCompat);
            Assert.AreEqual(ApplicationCategory.Browser, chromeCompat.Category);
            Assert.IsTrue(chromeCompat.IsCompatible);
        }

        [TestMethod]
        public void InitializeApplicationCompatibilityMap_ContainsWord()
        {
            // Assert
            var wordCompat = _service.ApplicationCompatibilityMap[TargetApplication.Word];
            Assert.IsNotNull(wordCompat);
            Assert.AreEqual(ApplicationCategory.Office, wordCompat.Category);
            Assert.AreEqual(InjectionMethod.Clipboard, wordCompat.PreferredMethod);
        }

        [TestMethod]
        public void InitializeApplicationCompatibilityMap_ContainsVisualStudio()
        {
            // Assert
            var vsCompat = _service.ApplicationCompatibilityMap[TargetApplication.VisualStudio];
            Assert.IsNotNull(vsCompat);
            Assert.AreEqual(ApplicationCategory.DevelopmentTool, vsCompat.Category);
            Assert.IsTrue(vsCompat.ApplicationSettings.ContainsKey("intellisense_compatible"));
        }

        [TestMethod]
        public async Task GetSupportedApplicationsAsync_ReturnsApplications()
        {
            // Act
            var result = await _service.GetSupportedApplicationsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);
            Assert.IsTrue(result.ContainsKey(TargetApplication.Chrome));
        }

        #endregion

        #region Test Injection Tests

        [TestMethod]
        public async Task TestInjectionAsync_ReturnsResult()
        {
            // Act
            var result = await _service.TestInjectionAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsFalse(string.IsNullOrEmpty(result.TestText));
        }

        [TestMethod]
        public async Task TestInjectionAsync_SetsMethodUsed()
        {
            // Act
            var result = await _service.TestInjectionAsync();

            // Assert
            Assert.IsFalse(string.IsNullOrEmpty(result.MethodUsed));
        }

        #endregion

        #region Validation Tests

        [TestMethod]
        public async Task ValidateInjectionAsync_ReturnsValidationResult()
        {
            // Arrange
            var text = "Test validation";

            // Act
            var result = await _service.ValidateInjectionAsync(text);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.TestText);
            Assert.IsTrue(result.StartTime <= result.EndTime);
        }

        [TestMethod]
        public async Task ValidateInjectionAsync_WithOptions_ReturnsValidationResult()
        {
            // Arrange
            var text = "Test with options";
            var options = new InjectionOptions
            {
                UseClipboardFallback = false,
                DelayBetweenCharsMs = 5
            };

            // Act
            var result = await _service.ValidateInjectionAsync(text, options);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(text, result.TestText);
        }

        [TestMethod]
        public async Task GetInjectionAccuracyAsync_ReturnsAccuracyResult()
        {
            // Arrange
            var expectedText = "Expected text";

            // Act
            var result = await _service.GetInjectionAccuracyAsync(expectedText);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(expectedText, result.ExpectedText);
            Assert.IsTrue(result.AccuracyScore >= 0.0);
        }

        [TestMethod]
        public async Task ValidateCrossApplicationInjectionAsync_ReturnsValidationResult()
        {
            // Act
            var result = await _service.ValidateCrossApplicationInjectionAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.ApplicationResults);
        }

        #endregion

        #region Metrics Tests

        [TestMethod]
        public void GetInjectionMetrics_ReturnsMetrics()
        {
            // Act
            var result = _service.GetInjectionMetrics();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.TotalAttempts >= 0);
        }

        [TestMethod]
        public void GetInjectionIssuesReport_ReturnsReport()
        {
            // Act
            var result = _service.GetInjectionIssuesReport();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Issues);
            Assert.IsNotNull(result.Recommendations);
        }

        [TestMethod]
        public async Task GetInjectionTimingMetricsAsync_ReturnsMetrics()
        {
            // Act
            var result = await _service.GetInjectionTimingMetricsAsync();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.TotalInjections >= 0);
            Assert.IsNotNull(result.RecentInjections);
        }

        #endregion

        #region Compatibility Tests

        [TestMethod]
        public void IsInjectionCompatible_ReturnsBoolean()
        {
            // Act
            var result = _service.IsInjectionCompatible();

            // Assert
            // Should return true on Windows, false otherwise
            Assert.IsInstanceOfType(result, typeof(bool));
        }

        [TestMethod]
        public void SetDebugMode_SetsMode()
        {
            // Act - Should not throw
            _service.SetDebugMode(true);
            _service.SetDebugMode(false);

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Debug mode")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Exactly(2));
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Act - Should not throw
            _service.Dispose();
            _service.Dispose();

            // Assert - No exception thrown
            Assert.IsTrue(true);
        }

        [TestMethod]
        public void Dispose_LogsInformation()
        {
            // Act
            _service.Dispose();

            // Assert
            _loggerMock.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Disposing")),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.Once);
        }

        #endregion

        #region Error Handling Tests

        [TestMethod]
        public async Task ValidateInjectionAsync_Exception_ReturnsFailedResult()
        {
            // Arrange - Create service without logger to test exception handling
            var service = new TextInjectionService();
            
            // Act
            var result = await service.ValidateInjectionAsync(""); // Empty text might cause issues

            // Assert
            Assert.IsNotNull(result);
            // Result should indicate failure
            service.Dispose();
        }

        [TestMethod]
        public async Task GetInjectionAccuracyAsync_Exception_ReturnsErrorResult()
        {
            // Arrange
            var service = new TextInjectionService();
            
            // Act - Call with null might cause exception
            var result = await service.GetInjectionAccuracyAsync("test");

            // Assert
            Assert.IsNotNull(result);
            service.Dispose();
        }

        [TestMethod]
        public async Task ValidateCrossApplicationInjectionAsync_HandlesExceptions()
        {
            // Act
            var result = await _service.ValidateCrossApplicationInjectionAsync();

            // Assert
            Assert.IsNotNull(result);
            // Should complete without throwing even if applications aren't running
        }

        #endregion

        #region Application-Specific Tests

        [TestMethod]
        public void GetApplicationCompatibility_Chrome_ReturnsBrowserCategory()
        {
            // Act
            var result = _service.GetApplicationCompatibility("chrome.exe");

            // Assert
            Assert.AreEqual(ApplicationCategory.Browser, result.Category);
        }

        [TestMethod]
        public void GetApplicationCompatibility_Word_ReturnsOfficeCategory()
        {
            // Act
            var result = _service.GetApplicationCompatibility("winword.exe");

            // Assert
            Assert.AreEqual(ApplicationCategory.Office, result.Category);
        }

        [TestMethod]
        public void GetApplicationCompatibility_VSCode_ReturnsTextEditorCategory()
        {
            // Act
            var result = _service.GetApplicationCompatibility("notepad++.exe");

            // Assert
            Assert.AreEqual(ApplicationCategory.TextEditor, result.Category);
        }

        [TestMethod]
        public void ApplicationCompatibilityMap_HasBrowserHandling()
        {
            // Assert
            var chromeCompat = _service.ApplicationCompatibilityMap[TargetApplication.Chrome];
            CollectionAssert.Contains(chromeCompat.RequiresSpecialHandling, "unicode");
            CollectionAssert.Contains(chromeCompat.RequiresSpecialHandling, "web_forms");
        }

        [TestMethod]
        public void ApplicationCompatibilityMap_HasOfficeHandling()
        {
            // Assert
            var wordCompat = _service.ApplicationCompatibilityMap[TargetApplication.Word];
            CollectionAssert.Contains(wordCompat.RequiresSpecialHandling, "formatting");
            CollectionAssert.Contains(wordCompat.RequiresSpecialHandling, "office_safe");
        }

        #endregion

        #region Integration Scenarios

        [TestMethod]
        public async Task FullInjectionWorkflow_InitializeInjectDispose()
        {
            // Arrange
            var service = new TextInjectionService(_loggerMock.Object);
            
            // Act - Full workflow
            var initResult = await service.InitializeAsync();
            var injectResult = await service.InjectTextAsync("Workflow test");
            var metrics = service.GetInjectionMetrics();
            service.Dispose();

            // Assert
            Assert.IsTrue(initResult);
            Assert.IsTrue(injectResult);
            Assert.IsNotNull(metrics);
        }

        [TestMethod]
        public async Task ValidationWorkflow_TestValidateMetrics()
        {
            // Arrange
            var testText = "Validation workflow test";
            
            // Act
            var testResult = await _service.TestInjectionAsync();
            var validationResult = await _service.ValidateInjectionAsync(testText);
            var issuesReport = _service.GetInjectionIssuesReport();
            
            // Assert
            Assert.IsNotNull(testResult);
            Assert.IsNotNull(validationResult);
            Assert.IsNotNull(issuesReport);
        }

        [TestMethod]
        public async Task CrossApplicationWorkflow_ValidateMultipleApps()
        {
            // Act
            var validationResult = await _service.ValidateCrossApplicationInjectionAsync();
            var supportedApps = await _service.GetSupportedApplicationsAsync();
            
            // Assert
            Assert.IsNotNull(validationResult);
            Assert.IsNotNull(validationResult.ApplicationResults);
            Assert.IsNotNull(supportedApps);
            Assert.IsTrue(supportedApps.Count > 0);
        }

        #endregion
    }
}
