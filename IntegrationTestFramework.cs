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
        protected readonly global::ScottWisper.ITextInjection _textInjectionService;
        protected readonly IAudioDeviceService _audioDeviceService;
        protected readonly ISettingsService _settingsService;
        protected readonly List<TestResult> _testResults = new List<TestResult>();

        protected IntegrationTestFramework(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            _textInjectionService = serviceProvider.GetRequiredService<ITextInjectionService>();
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
            ["long"] = "This is a very long text that exceeds normal typing limits and should test how the system handles extended text input. It contains multiple paragraphs with various punctuation and should be processed correctly by the speech recognition system."
        };

        public static readonly Dictionary<string, TargetApplication> TestApplications = new Dictionary<string, TargetApplication>
        {
            ["notepad"] = TargetApplication.TextEditor,
            ["chrome"] = TargetApplication.Browser,
            ["firefox"] = TargetApplication.Browser,
            ["edge"] = TargetApplication.Browser,
            ["word"] = TargetApplication.Office,
            ["excel"] = TargetApplication.Office,
            ["visualstudio"] = TargetApplication.DevelopmentTool,
            ["cmd"] = TargetApplication.Terminal,
            ["powershell"] = TargetApplication.Terminal,
            ["terminal"] = TargetApplication.Terminal
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