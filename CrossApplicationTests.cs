using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ScottWisper.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ScottWisper
{
    /// <summary>
    /// Cross-application testing implementation
    /// </summary>
    public class CrossApplicationTests : IntegrationTestFramework
    {
        public CrossApplicationTests(IServiceProvider serviceProvider) : base(serviceProvider)
        {
        }

        public override async Task<TestSuiteResult> RunAllTestsAsync()
        {
            var environmentManager = new TestEnvironmentManager();
            
            if (!await environmentManager.SetupTestEnvironmentAsync())
            {
                return new TestSuiteResult
                {
                    TotalTests = 0,
                    PassedTests = 0,
                    FailedTests = 1,
                    SuccessRate = 0,
                    TestResults = new List<TestResult>
                    {
                        CreateTestResult("Environment Setup", false, "Failed to setup test environment", TimeSpan.Zero)
                    },
                    ReportGeneratedAt = DateTime.Now,
                    Duration = TimeSpan.Zero
                };
            }

            var scenarios = TestDataProvider.GetTestScenarios();
            var results = new List<TestResult>();

            foreach (var scenario in scenarios)
            {
                foreach (var appKey in scenario.Applications)
                {
                    var result = await RunTestAsync($"{scenario.Name}_{appKey}");
                    if (result != null)
                    {
                        results.Add(result);
                    }
                }
            }

            await environmentManager.CleanupTestEnvironmentAsync();

            return new TestResultCollector().GenerateReport();
        }

        public override async Task<TestResult> RunTestAsync(string testName)
        {
            var parts = testName.Split('_');
            if (parts.Length < 2) return null;

            var scenarioName = parts[0];
            var applicationKey = parts[1];

            TestDataProvider.TestApplications.TryGetValue(applicationKey, out var targetApp);
            TestDataProvider.TestScenarios.FirstOrDefault(s => s.Name == scenarioName, out var scenario);

            if (scenario == null || !TestDataProvider.TestTexts.TryGetValue(scenario.TestTexts[0], out var testText))
            {
                return CreateTestResult(testName, false, "Invalid test configuration", TimeSpan.Zero);
            }

            var stopwatch = Stopwatch.StartNew();
            try
            {
                switch (targetApp)
                {
                    case TargetApplication.TextEditor:
                        return await TestTextEditorAsync(testName, testText, targetApp);
                    case TargetApplication.Browser:
                        return await TestBrowserAsync(testName, testText, targetApp);
                    case TargetApplication.Office:
                        return await TestOfficeAsync(testName, TestText, targetApp);
                    case TargetApplication.DevelopmentTool:
                        return await TestDevelopmentToolAsync(testName, testText, targetApp);
                    case TargetApplication.Terminal:
                        return await TestTerminalAsync(testName, testText, targetApp);
                    default:
                        return await TestGenericApplicationAsync(testName, TestText, targetApp);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new TestResult
                {
                    TestName = testName,
                    Category = GetCategoryFromApplication(targetApp),
                    Success = false,
                    Message = $"Exception: {ex.Message}",
                    Duration = stopwatch.Elapsed,
                    Application = targetApp.ToString(),
                    TestText = TestDataProvider.TestTexts[scenario.TestTexts[0]],
                    Metrics = new Dictionary<string, object>
                    {
                        ["ErrorType"] = ex.GetType().Name,
                        ["Exception"] = ex.Message
                    }
                };
            }
            finally
            {
                stopwatch.Stop();
            }
        }

        public override List<TestResult> GetTestResults()
        {
            return _testResults.ToList();
        }

        private async Task<TestResult> TestTextEditorAsync(string testName, string testText, TargetApplication targetApp)
        {
            var appPaths = new Dictionary<string, string>
            {
                ["notepad"] = "notepad.exe",
                ["notepad++"] = "notepad++.exe",
                ["code"] = "code.exe"
            };

            return await TestApplicationInjectionAsync(testName, testText, targetApp, appPaths);
        }

        private async Task<TestResult> TestBrowserAsync(string testName, string testText, TargetApplication targetApp)
        {
            var appPaths = new Dictionary<string, string>
            {
                ["chrome"] = "chrome.exe",
                ["firefox"] = "firefox.exe",
                ["edge"] = "msedge.exe"
            };

            return await TestApplicationInjectionAsync(testName, TestText, targetApp, appPaths);
        }

        private async Task<TestResult> TestOfficeAsync(string testName, string testText, TargetApplication targetApp)
        {
            var appPaths = new Dictionary<string, string>
            {
                ["word"] = "winword.exe",
                ["excel"] = "excel.exe",
                ["outlook"] = "outlook.exe"
            };

            return await TestApplicationInjectionAsync(testName, TestText, targetApp, appPaths);
        }

        private async Task<TestResult> TestDevelopmentToolAsync(string testName, string testText, TargetApplication targetApp)
        {
            var appPaths = new Dictionary<string, string>
            {
                ["visualstudio"] = "devenv.exe"
            };

            return await TestApplicationInjectionAsync(testName, testText, targetApp, appPaths);
        }

        private async Task<TestResult> TestTerminalAsync(string testName, string testText, TargetApplication targetApp)
        {
            var appPaths = new Dictionary<string, string>
            {
                ["cmd"] = "cmd.exe",
                ["powershell"] = "powershell.exe",
                ["terminal"] = "wt.exe"
            };

            return await TestApplicationInjectionAsync(testName, testText, targetApp, appPaths);
        }

        private async Task<TestResult> TestApplicationInjectionAsync(string testName, string testText, TargetApplication targetApp, Dictionary<string, string> appPaths)
        {
            var stopwatch = Stopwatch.StartNew();
            var metrics = new Dictionary<string, object>();

            try
            {
                // Try to find and launch the application
                string applicationPath = null;
                foreach (var kvp in appPaths)
                {
                    if (await LaunchApplicationAsync(kvp.Value))
                    {
                        applicationPath = kvp.Value;
                        break;
                    }
                }

                if (string.IsNullOrEmpty(applicationPath))
                {
                    return new TestResult
                    {
                        TestName = testName,
                        Category = GetCategoryFromApplication(targetApp),
                        Success = false,
                        Message = $"Could not launch any {targetApp} variant",
                        Duration = stopwatch.Elapsed,
                        Application = targetApp.ToString(),
                        TestText = testText
                    };
                }

                // Wait for application to be ready
                await Task.Delay(3000);

                // Perform text injection test
                var injectionResult = await _textInjectionService.InjectTextAsync(testText, new InjectionOptions
                {
                    Method = TextInjectionMethod.SendInput,
                    UseUnicode = true,
                    ValidateUnicode = true,
                    RetryCount = 3
                });

                stopwatch.Stop();

                var success = injectionResult.Success;
                var message = success ? "Text injection successful" : $"Injection failed: {injectionResult.ErrorMessage}";

                metrics["InjectionMethod"] = injectionResult.MethodUsed;
                metrics["InjectionAttempts"] = injectionResult.Attempts;
                metrics["UnicodeHandling"] = injectionResult.UnicodeHandled;
                metrics["TextMatchAccuracy"] = injectionResult.AccuracyPercentage;

                return new TestResult
                {
                    TestName = testName,
                    Category = GetCategoryFromApplication(targetApp),
                    Success = success,
                    Message = message,
                    Duration = stopwatch.Elapsed,
                    Application = applicationPath,
                    TestText = testText,
                    Metrics = metrics
                };
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return new TestResult
                {
                    TestName = testName,
                    Category = GetCategoryFromApplication(targetApp),
                    Success = false,
                    Message = $"Test failed: {ex.Message}",
                    Duration = stopwatch.Elapsed,
                    Application = applicationPath ?? "unknown",
                    TestText = testText,
                    Metrics = new Dictionary<string, object>
                    {
                        ["ErrorType"] = ex.GetType().Name,
                        ["Exception"] = ex.Message
                    }
                };
            }
        }

        private string GetCategoryFromApplication(TargetApplication app)
        {
            return app switch
            {
                TargetApplication.TextEditor => "Text Editor",
                TargetApplication.Browser => "Browser",
                TargetApplication.Office => "Office",
                TargetApplication.DevelopmentTool => "Development Tool",
                TargetApplication.Terminal => "Terminal",
                _ => "Generic"
            };
        }
    }
}