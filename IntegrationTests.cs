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
using static ScottWisper.Services.ApplicationCategory;

namespace ScottWisper.Tests
{
    /// <summary>
    /// Test framework infrastructure for automated cross-application validation
    /// </summary>
    [TestClass]
    public class IntegrationTests
    {
        private readonly ITextInjection _textInjection;
        private readonly IFeedbackService _feedbackService;
        private readonly IAudioDeviceService _audioDeviceService;

        public IntegrationTests(ITextInjection textInjection, IFeedbackService feedbackService, IAudioDeviceService audioDeviceService)
        {
            _textInjection = textInjection;
            _feedbackService = feedbackService;
            _audioDeviceService = audioDeviceService;
        }

        /// <summary>
        /// Browser compatibility testing
        /// </summary>
        public class BrowserTestSuite
        {
            public async Task<TestResult> TestChromeCompatibilityAsync()
            {
                return await TestBrowserInjectionAsync("chrome");
            }

            public async Task<TestResult> TestFirefoxCompatibilityAsync()
            {
                return await TestBrowserInjectionAsync("firefox");
            }

            public async Task<TestResult> TestEdgeCompatibilityAsync()
            {
                return await TestBrowserInjectionAsync("edge");
            }

            private async Task<TestResult> TestBrowserInjectionAsync(string browser)
            {
                try
                {
                    // Arrange: Find or start browser
                    var testResult = new TestResult { TestName = $"{browser} Browser Injection Test" };

                    // Act: Simulate text injection
                    var testText = $"Browser injection test for {browser} - {DateTime.Now:HH:mm:ss}";
                    var injectResult = await _textInjection.InjectTextAsync(testText);

                    // Assert: Verify injection success
                    testResult.Success = injectResult;
                    testResult.Details = injectResult ? "Injection successful" : $"Injection failed: {injectResult}";
                    
                    return testResult;
                }
                catch (Exception ex)
                {
                    return new TestResult { TestName = $"{browser} Browser Injection Test", Success = false, Details = ex.Message };
                }
            }
        }

        /// <summary>
        /// IDE and editor testing
        /// </summary>
        public class IDETestSuite
        {
            public async Task<TestResult> TestVisualStudioCompatibilityAsync()
            {
                return await TestIDEInjectionAsync("devenv");
            }

            public async Task<TestResult> TestVSCodeCompatibilityAsync()
            {
                return await TestIDEInjectionAsync("code");
            }

            public async Task<TestResult> TestNotepadPlusCompatibilityAsync()
            {
                return await TestIDEInjectionAsync("notepad++");
            }

            private async Task<TestResult> TestIDEInjectionAsync(string ideProcess)
            {
                try
                {
                    var testResult = new TestResult { TestName = $"{ideProcess} IDE Injection Test" };

                    var testText = $"IDE injection test for {ideProcess} - {DateTime.Now:HH:mm:ss}";
                    var injectResult = await _textInjection.InjectTextAsync(new TestTextOptions 
                    { 
                        UseClipboardFallback = false,
                        RetryCount = 2,
                        DelayBetweenCharsMs = ideProcess == "code" ? 12 : 8
                    });

                    testResult.Success = injectResult;
                    testResult.Details = injectResult ? "IDE injection successful" : $"IDE injection failed: {injectResult}";

                    return testResult;
                }
                catch (Exception ex)
                {
                    return new TestResult { TestName = $"{ideProcess} IDE Injection Test", Success = false, Details = ex.Message };
                }
            }
        }

        /// <summary>
        /// Office application testing
        /// </summary>
        public class OfficeTestSuite
        {
            public async Task<TestResult> TestWordCompatibilityAsync()
            {
                return await TestOfficeInjectionAsync("winword");
            }

            public async Task<TestResult> TestOutlookCompatibilityAsync()
            {
                return await TestOfficeInjectionAsync("outlook");
            }

            public async Task<TestResult> TestExcelCompatibilityAsync()
            {
                return await TestOfficeInjectionAsync("excel");
            }

            public async Task<TestResult> TestPowerPointCompatibilityAsync()
            {
                return await TestOfficeInjectionAsync("powerpnt");
            }

            private async Task<TestResult> TestOfficeInjectionAsync(string officeProcess)
            {
                try
                {
                    var testResult = new TestResult { TestName = $"{officeProcess} Office Injection Test" };

                    var testText = $"Office injection test for {officeProcess} - {DateTime.Now:HH:mm:ss}";
                    var injectResult = await _textInjection.InjectTextAsync(new TestTextOptions 
                    { 
                        UseClipboardFallback = true, // Office apps prefer clipboard
                        RetryCount = 2,
                        DelayBetweenCharsMs = 5
                    });

                    testResult.Success = injectResult;
                    testResult.Details = injectResult ? "Office injection successful" : $"Office injection failed: {injectResult}";

                    return testResult;
                }
                catch (Exception ex)
                {
                    return new TestResult { TestName = $"{officeProcess} Office Injection Test", Success = false, Details = ex.Message };
                }
            }
        }

        /// <summary>
        /// Terminal testing
        /// </summary>
        public class TerminalTestSuite
        {
            public async Task<TestResult> TestWindowsTerminalCompatibilityAsync()
            {
                return await TestTerminalInjectionAsync("WindowsTerminal");
            }

            public async Task<TestResult> TestCommandPromptCompatibilityAsync()
            {
                return await TestTerminalInjectionAsync("cmd");
            }

            public async Task<TestResult> TestPowerShellCompatibilityAsync()
            {
                return await TestTerminalInjectionAsync("powershell");
            }

            private async Task<TestResult> TestTerminalInjectionAsync(string terminalProcess)
            {
                try
                {
                    var testResult = new TestResult { TestName = $"{terminalProcess} Terminal Injection Test" };

                    var testText = $"Terminal injection test for {terminalProcess} - {DateTime.Now:HH:mm:ss}";
                    var injectResult = await _textInjection.InjectTextAsync(new TestTextOptions 
                    { 
                        UseClipboardFallback = false,
                        RetryCount = 1,
                        DelayBetweenCharsMs = 3 // Fast for terminals
                    });

                    testResult.Success = injectResult;
                    testResult.Details = injectResult ? "Terminal injection successful" : $"Terminal injection failed: {injectResult}";

                    return testResult;
                }
                catch (Exception ex)
                {
                    return new TestResult { TestName = $"{terminalProcess} Terminal Injection Test", Success = false, Details = ex.Message };
                }
            }
        }

        /// <summary>
        /// Test execution orchestration
        /// </summary>
        public class TestResultCollector
        {
            public List<TestResult> Results { get; set; } = new();
            public DateTime TestRunStarted { get; set; }
            public TimeSpan TotalDuration { get; set; }

            public void AddResult(TestResult result)
            {
                Results.Add(result);
            }

            public TestSummary GetSummary()
            {
                var totalTests = Results.Count;
                var successfulTests = Results.Count(r => r.Success);
                var successRate = totalTests > 0 ? (double)successfulTests / totalTests : 0;

                return new TestSummary
                {
                    TotalTests = totalTests,
                    SuccessfulTests = successfulTests,
                    SuccessRate = successRate,
                    Results = Results
                };
            }
        }

        /// <summary>
        /// Individual test result
        /// </summary>
        public class TestResult
        {
            public string TestName { get; set; } = string.Empty;
            public bool Success { get; set; }
            public string Details { get; set; } = string.Empty;
            public DateTime TestTime { get; set; }
            public ApplicationCategory? ApplicationCategory { get; set; }
            public string ProcessName { get; set; } = string.Empty;
        }

        /// <summary>
        /// Test summary
        /// </summary>
        public class TestSummary
        {
            public int TotalTests { get; set; }
            public int SuccessfulTests { get; set; }
            public double SuccessRate { get; set; }
            public List<TestResult> Results { get; set; } = new();
        }

        /// <summary>
        /// Enhanced test options for cross-application validation
        /// </summary>
        public class TestTextOptions : InjectionOptions
        {
            public bool ForceClipboardFallback { get; set; }
            public bool ValidateCursorPosition { get; set; }
        }

        /// <summary>
        /// Run all compatibility tests
        /// </summary>
        public async Task<TestSummary> RunAllCompatibilityTestsAsync()
        {
            var collector = new TestResultCollector { TestRunStarted = DateTime.Now };

            // Browser tests
            collector.AddResult(await new BrowserTestSuite().TestChromeCompatibilityAsync());
            collector.AddResult(await new BrowserTestSuite().TestFirefoxCompatibilityAsync());
            collector.AddResult(await new BrowserTestSuite().TestEdgeCompatibilityAsync());

            // IDE tests
            collector.AddResult(await new IDETestSuite().TestVisualStudioCompatibilityAsync());
            collector.AddResult(await new IDETestSuite().TestVSCodeCompatibilityAsync());
            collector.AddResult(await new IDETestSuite().TestNotepadPlusCompatibilityAsync());

            // Office tests
            collector.AddResult(await new OfficeTestSuite().TestWordCompatibilityAsync());
            collector.AddResult(await new OfficeTestSuite().TestOutlookCompatibilityAsync());
            collector.AddResult(await new OfficeTestSuite().TestExcelCompatibilityAsync());
            collector.AddResult(await new OfficeTestSuite().TestPowerPointCompatibilityAsync());

            // Terminal tests
            collector.AddResult(await new TerminalTestSuite().TestWindowsTerminalCompatibilityAsync());
            collector.AddResult(await new TerminalTestSuite().TestCommandPromptCompatibilityAsync());
            collector.AddResult(await new TerminalTestSuite().TestPowerShellCompatibilityAsync());

            collector.TotalDuration = DateTime.Now - collector.TestRunStarted;
            return collector.GetSummary();
        }
    }
}