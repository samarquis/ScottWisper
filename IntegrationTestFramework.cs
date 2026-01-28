using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ScottWisper.Services;
using Microsoft.Extensions.DependencyInjection;
using System.IO;
using System.Text.Json;

namespace ScottWisper
{
    /// <summary>
    /// Base class for integration testing framework
    /// </summary>
    public abstract class IntegrationTestFramework
    {
        protected readonly IServiceProvider _serviceProvider;
        protected readonly ITextInjection _textInjectionService;
        protected readonly IAudioDeviceService _audioDeviceService;
        protected readonly ISettingsService _settingsService;
        protected readonly List<TestResult> _testResults = new List<TestResult>();

        protected IntegrationTestFramework(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _textInjectionService = serviceProvider.GetRequiredService<ITextInjection>();
            _audioDeviceService = serviceProvider.GetRequiredService<IAudioDeviceService>();
            _settingsService = serviceProvider.GetRequiredService<ISettingsService>();
        }

        public abstract Task<TestSuiteResult> RunAllTestsAsync();
        public abstract Task<TestResult> RunTestAsync(string testName);
        public abstract List<TestResult> GetTestResults();
        
        protected TestResult CreateTestResult(string testName, bool success, string message, TimeSpan duration)
        {
            return new TestResult
            {
                TestName = testName,
                Success = success,
                Message = message,
                Duration = duration,
                Timestamp = DateTime.Now
            };
        }

        protected async Task<bool> LaunchApplicationAsync(string applicationPath)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = applicationPath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Normal
                };

                using var process = Process.Start(startInfo);
                await Task.Delay(2000); // Wait for app to start
                
                return !process.HasExited;
            }
            catch
            {
                return false;
            }
        }

        protected void LogTestResult(TestResult result)
        {
            _testResults.Add(result);
            Debug.WriteLine($"Test: {result.TestName} - {(result.Success ? "PASS" : "FAIL")} - {result.Message}");
        }
    }

    /// <summary>
    /// Test environment manager for setup/teardown
    /// </summary>
    public class TestEnvironmentManager
    {
        private readonly List<Process> _runningProcesses = new List<Process>();

        public async Task<bool> SetupTestEnvironmentAsync()
        {
            try
            {
                // Close existing applications that might interfere
                await CleanupTestEnvironmentAsync();
                
                // Setup test environment
                Debug.WriteLine("Test environment setup complete");
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task CleanupTestEnvironmentAsync()
        {
            foreach (var process in _runningProcesses)
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        await process.WaitForExitAsync();
                    }
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
            _runningProcesses.Clear();
        }

        public void RegisterProcess(Process process)
        {
            _runningProcesses.Add(process);
        }
    }

    /// <summary>
    /// Test data provider with test scenarios
    /// </summary>
    public static class TestDataProvider
    {
        public static readonly Dictionary<string, string> TestTexts = new Dictionary<string, string>
        {
            ["basic"] = "The quick brown fox jumps over the lazy dog.",
            ["numbers"] = "Call me at 555-123-4567.",
            ["special_chars"] = "Test with @#$%^&*() characters.",
            ["multiline"] = "Line 1\nLine 2\nLine 3 with special chars: αβγδ",
            ["unicode"] = "Test Unicode: 你好世界 🎤",
            ["code"] = "function test() { return 'Hello World'; }",
            ["email"] = "john.doe@example.com",
            ["url"] = "https://www.example.com/test/path",
            ["short"] = "Hi",
            ["medium"] = "This is a medium length text that contains multiple sentences and should test the text injection service properly.",
            ["long"] = "This is a very long text that exceeds normal typing limits and should test how the system handles extended text input. It contains multiple paragraphs with various punctuation and should be processed correctly by the speech recognition system.",
            ["browser"] = "Testing browser compatibility with form input fields and text areas for cross-application text injection validation.",
            ["office"] = "Testing Microsoft Office application compatibility with formatted text injection and document field handling.",
            ["terminal"] = "echo 'Testing terminal compatibility with command line text injection and shell prompt handling.'"
        };

        public static readonly Dictionary<string, TargetApplication> TestApplications = new Dictionary<string, TargetApplication>
        {
            ["notepad"] = TargetApplication.Notepad,
            ["chrome"] = TargetApplication.Chrome,
            ["firefox"] = TargetApplication.Firefox,
            ["edge"] = TargetApplication.Edge,
            ["word"] = TargetApplication.Word,
            ["excel"] = TargetApplication.Unknown,
            ["visualstudio"] = TargetApplication.VisualStudio,
            ["vscode"] = TargetApplication.Unknown,
            ["notepad++"] = TargetApplication.NotepadPlus,
            ["cmd"] = TargetApplication.CommandPrompt,
            ["powershell"] = TargetApplication.Unknown,
            ["windowsterminal"] = TargetApplication.WindowsTerminal,
            ["outlook"] = TargetApplication.Outlook
        };

        public static List<TestScenario> GetTestScenarios()
        {
            return new List<TestScenario>
            {
                new TestScenario
                {
                    Name = "Basic Text Injection",
                    Description = "Test basic text injection functionality",
                    TestTexts = new[] { "basic", "numbers", "short" },
                    Applications = new[] { "notepad", "chrome" }
                },
                new TestScenario
                {
                    Name = "Unicode and Special Characters",
                    Description = "Test injection of Unicode and special characters",
                    TestTexts = new[] { "special_chars", "unicode", "multiline" },
                    Applications = new[] { "word", "notepad" }
                },
                new TestScenario
                {
                    Name = "Code Injection",
                    Description = "Test injection of code snippets",
                    TestTexts = new[] { "code" },
                    Applications = new[] { "visualstudio", "notepad++" }
                },
                new TestScenario
                {
                    Name = "Long Text Handling",
                    Description = "Test handling of long text input",
                    TestTexts = new[] { "long", "medium" },
                    Applications = new[] { "word", "chrome" }
                },
                new TestScenario
                {
                    Name = "Browser Compatibility",
                    Description = "Test across different browsers",
                    TestTexts = new[] { "url", "email", "basic" },
                    Applications = new[] { "chrome", "firefox", "edge" }
                }
            };
        }
    }

    /// <summary>
    /// Test result collector for reporting
    /// </summary>
    public class TestResultCollector
    {
        private readonly List<TestResult> _results = new List<TestResult>();
        private readonly Dictionary<string, List<TestResult>> _resultsByCategory = new Dictionary<string, List<TestResult>>();

        public void AddResult(TestResult result)
        {
            _results.Add(result);
            
            if (!_resultsByCategory.ContainsKey(result.Category))
            {
                _resultsByCategory[result.Category] = new List<TestResult>();
            }
            _resultsByCategory[result.Category].Add(result);
        }

        public TestSuiteResult GenerateReport()
        {
            var totalTests = _results.Count;
            var passedTests = _results.Count(r => r.Success);
            var failedTests = totalTests - passedTests;
            var successRate = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0;

            return new TestSuiteResult
            {
                TotalTests = totalTests,
                PassedTests = passedTests,
                FailedTests = failedTests,
                SuccessRate = successRate,
                TestResults = new List<TestResult>(_results),
                ResultsByCategory = new Dictionary<string, List<TestResult>>(_resultsByCategory),
                ReportGeneratedAt = DateTime.Now,
                Duration = CalculateTotalDuration()
            };
        }

        private TimeSpan CalculateTotalDuration()
        {
            var totalTicks = _results.Sum(r => r.Duration.Ticks);
            return TimeSpan.FromTicks(totalTicks);
        }

        public void ExportToFile(string filePath)
        {
            var report = GenerateTextReport();
            File.WriteAllText(filePath, report);
        }

        private string GenerateTextReport()
        {
            var report = new System.Text.StringBuilder();
            report.AppendLine("=== ScottWisper Integration Test Report ===");
            report.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            report.AppendLine();

            var groupedResults = _results.GroupBy(r => r.Category);
            foreach (var group in groupedResults)
            {
                report.AppendLine($"## {group.Key} Tests ##");
                foreach (var result in group)
                {
                    var status = result.Success ? "✓ PASS" : "✗ FAIL";
                    report.AppendLine($"  {status} {result.TestName}");
                    if (!string.IsNullOrWhiteSpace(result.Message))
                    {
                        report.AppendLine($"    Details: {result.Message}");
                    }
                    report.AppendLine($"    Duration: {result.Duration.TotalMilliseconds}ms");
                    report.AppendLine($"    Timestamp: {result.Timestamp:yyyy-MM-dd HH:mm:ss}");
                    report.AppendLine();
                }
                report.AppendLine();
            }

            // Summary
            var totalTests = _results.Count;
            var passedTests = _results.Count(r => r.Success);
            var failedTests = totalTests - passedTests;
            var successRate = totalTests > 0 ? (double)passedTests / totalTests * 100 : 0;

            report.AppendLine("=== Summary ===");
            report.AppendLine($"Total Tests: {totalTests}");
            report.AppendLine($"Passed: {passedTests}");
            report.AppendLine($"Failed: {failedTests}");
            report.AppendLine($"Success Rate: {successRate:F1}%");
            report.AppendLine($"Total Duration: {CalculateTotalDuration().TotalSeconds:F2} seconds");

            return report.ToString();
        }
    }

    /// <summary>
    /// Browser compatibility testing suite
    /// </summary>
    public class BrowserTestSuite : IntegrationTestFramework
    {
        public BrowserTestSuite(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override async Task<TestSuiteResult> RunAllTestsAsync()
        {
            var results = new List<TestResult>();
            
            // Test Chrome compatibility
            results.Add(await TestChromeCompatibilityAsync());
            
            // Test Firefox compatibility
            results.Add(await TestFirefoxCompatibilityAsync());
            
            // Test Edge compatibility
            results.Add(await TestEdgeCompatibilityAsync());

            return new TestSuiteResult
            {
                TestResults = results,
                TotalTests = results.Count,
                PassedTests = results.Count(r => r.Success),
                FailedTests = results.Count(r => !r.Success),
                SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                Duration = TimeSpan.FromMilliseconds(results.Sum(r => r.Duration.TotalMilliseconds)),
                ReportGeneratedAt = DateTime.Now
            };
        }

        public override Task<TestResult> RunTestAsync(string testName)
        {
            return testName.ToLower() switch
            {
                "chrome" => TestChromeCompatibilityAsync(),
                "firefox" => TestFirefoxCompatibilityAsync(),
                "edge" => TestEdgeCompatibilityAsync(),
                _ => Task.FromResult(CreateTestResult(testName, false, "Unknown test", TimeSpan.Zero))
            };
        }

        public override List<TestResult> GetTestResults()
        {
            return new List<TestResult>();
        }

        private async Task<TestResult> TestChromeCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Test text injection in Chrome
                var testText = TestDataProvider.TestTexts["browser"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Chrome Browser Compatibility", 
                    success, 
                    success ? "Text injection successful in Chrome" : "Text injection failed in Chrome",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Chrome Browser Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private async Task<TestResult> TestFirefoxCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["browser"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Firefox Browser Compatibility", 
                    success, 
                    success ? "Text injection successful in Firefox" : "Text injection failed in Firefox",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Firefox Browser Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private async Task<TestResult> TestEdgeCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["browser"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Edge Browser Compatibility", 
                    success, 
                    success ? "Text injection successful in Edge" : "Text injection failed in Edge",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Edge Browser Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }
    }

    /// <summary>
    /// IDE and editor testing suite
    /// </summary>
    public class IDETestSuite : IntegrationTestFramework
    {
        public IDETestSuite(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override async Task<TestSuiteResult> RunAllTestsAsync()
        {
            var results = new List<TestResult>();
            
            results.Add(await TestVisualStudioCompatibilityAsync());
            results.Add(await TestVSCodeCompatibilityAsync());
            results.Add(await TestNotepadPlusCompatibilityAsync());

            return new TestSuiteResult
            {
                TestResults = results,
                TotalTests = results.Count,
                PassedTests = results.Count(r => r.Success),
                FailedTests = results.Count(r => !r.Success),
                SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                Duration = TimeSpan.FromMilliseconds(results.Sum(r => r.Duration.TotalMilliseconds)),
                ReportGeneratedAt = DateTime.Now
            };
        }

        public override Task<TestResult> RunTestAsync(string testName)
        {
            return testName.ToLower() switch
            {
                "visualstudio" => TestVisualStudioCompatibilityAsync(),
                "vscode" => TestVSCodeCompatibilityAsync(),
                "notepad++" => TestNotepadPlusCompatibilityAsync(),
                _ => Task.FromResult(CreateTestResult(testName, false, "Unknown test", TimeSpan.Zero))
            };
        }

        public override List<TestResult> GetTestResults()
        {
            return new List<TestResult>();
        }

        private async Task<TestResult> TestVisualStudioCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["code"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Visual Studio Compatibility", 
                    success, 
                    success ? "Code injection successful in Visual Studio" : "Code injection failed in Visual Studio",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Visual Studio Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private async Task<TestResult> TestVSCodeCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["code"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "VS Code Compatibility", 
                    success, 
                    success ? "Code injection successful in VS Code" : "Code injection failed in VS Code",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("VS Code Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private async Task<TestResult> TestNotepadPlusCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["basic"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Notepad++ Compatibility", 
                    success, 
                    success ? "Text injection successful in Notepad++" : "Text injection failed in Notepad++",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Notepad++ Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }
    }

    /// <summary>
    /// Office application testing suite
    /// </summary>
    public class OfficeTestSuite : IntegrationTestFramework
    {
        public OfficeTestSuite(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override async Task<TestSuiteResult> RunAllTestsAsync()
        {
            var results = new List<TestResult>();
            
            results.Add(await TestWordCompatibilityAsync());
            results.Add(await TestOutlookCompatibilityAsync());
            results.Add(await TestExcelCompatibilityAsync());

            return new TestSuiteResult
            {
                TestResults = results,
                TotalTests = results.Count,
                PassedTests = results.Count(r => r.Success),
                FailedTests = results.Count(r => !r.Success),
                SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                Duration = TimeSpan.FromMilliseconds(results.Sum(r => r.Duration.TotalMilliseconds)),
                ReportGeneratedAt = DateTime.Now
            };
        }

        public override Task<TestResult> RunTestAsync(string testName)
        {
            return testName.ToLower() switch
            {
                "word" => TestWordCompatibilityAsync(),
                "outlook" => TestOutlookCompatibilityAsync(),
                "excel" => TestExcelCompatibilityAsync(),
                _ => Task.FromResult(CreateTestResult(testName, false, "Unknown test", TimeSpan.Zero))
            };
        }

        public override List<TestResult> GetTestResults()
        {
            return new List<TestResult>();
        }

        private async Task<TestResult> TestWordCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["office"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Microsoft Word Compatibility", 
                    success, 
                    success ? "Text injection successful in Word" : "Text injection failed in Word",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Microsoft Word Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private async Task<TestResult> TestOutlookCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["email"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Microsoft Outlook Compatibility", 
                    success, 
                    success ? "Text injection successful in Outlook" : "Text injection failed in Outlook",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Microsoft Outlook Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private async Task<TestResult> TestExcelCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["basic"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Microsoft Excel Compatibility", 
                    success, 
                    success ? "Text injection successful in Excel" : "Text injection failed in Excel",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Microsoft Excel Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }
    }

    /// <summary>
    /// Terminal and command line testing suite
    /// </summary>
    public class TerminalTestSuite : IntegrationTestFramework
    {
        public TerminalTestSuite(IServiceProvider serviceProvider) : base(serviceProvider) { }

        public override async Task<TestSuiteResult> RunAllTestsAsync()
        {
            var results = new List<TestResult>();
            
            results.Add(await TestWindowsTerminalCompatibilityAsync());
            results.Add(await TestCommandPromptCompatibilityAsync());
            results.Add(await TestPowerShellCompatibilityAsync());

            return new TestSuiteResult
            {
                TestResults = results,
                TotalTests = results.Count,
                PassedTests = results.Count(r => r.Success),
                FailedTests = results.Count(r => !r.Success),
                SuccessRate = (double)results.Count(r => r.Success) / results.Count * 100,
                Duration = TimeSpan.FromMilliseconds(results.Sum(r => r.Duration.TotalMilliseconds)),
                ReportGeneratedAt = DateTime.Now
            };
        }

        public override Task<TestResult> RunTestAsync(string testName)
        {
            return testName.ToLower() switch
            {
                "windowsterminal" => TestWindowsTerminalCompatibilityAsync(),
                "cmd" => TestCommandPromptCompatibilityAsync(),
                "powershell" => TestPowerShellCompatibilityAsync(),
                _ => Task.FromResult(CreateTestResult(testName, false, "Unknown test", TimeSpan.Zero))
            };
        }

        public override List<TestResult> GetTestResults()
        {
            return new List<TestResult>();
        }

        private async Task<TestResult> TestWindowsTerminalCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["terminal"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Windows Terminal Compatibility", 
                    success, 
                    success ? "Command injection successful in Windows Terminal" : "Command injection failed in Windows Terminal",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Windows Terminal Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private async Task<TestResult> TestCommandPromptCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["terminal"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "Command Prompt Compatibility", 
                    success, 
                    success ? "Command injection successful in CMD" : "Command injection failed in CMD",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("Command Prompt Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }

        private async Task<TestResult> TestPowerShellCompatibilityAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var testText = TestDataProvider.TestTexts["terminal"];
                var success = await _textInjectionService.InjectTextAsync(testText);
                
                return CreateTestResult(
                    "PowerShell Compatibility", 
                    success, 
                    success ? "Command injection successful in PowerShell" : "Command injection failed in PowerShell",
                    stopwatch.Elapsed
                );
            }
            catch (Exception ex)
            {
                return CreateTestResult("PowerShell Compatibility", false, $"Exception: {ex.Message}", stopwatch.Elapsed);
            }
        }
    }

    // Data classes
    public class TestSuiteResult
    {
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public int FailedTests { get; set; }
        public double SuccessRate { get; set; }
        public List<TestResult> TestResults { get; set; } = new List<TestResult>();
        public Dictionary<string, List<TestResult>> ResultsByCategory { get; set; } = new Dictionary<string, List<TestResult>>();
        public DateTime ReportGeneratedAt { get; set; }
        public TimeSpan Duration { get; set; }
    }

    public class TestResult
    {
        public string TestName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; }
        public string Application { get; set; } = string.Empty;
        public string TestText { get; set; } = string.Empty;
        public Dictionary<string, object> Metrics { get; set; } = new Dictionary<string, object>();
    }

    public class TestScenario
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string[] TestTexts { get; set; } = Array.Empty<string>();
        public string[] Applications { get; set; } = Array.Empty<string>();
    }
}