using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ScottWisper.Testing
{
    /// <summary>
    /// Test execution and reporting framework for ScottWisper integration tests
    /// </summary>
    public class TestRunner
    {
        /// <summary>
        /// Test execution result with detailed metrics
        /// </summary>
        public class TestExecutionResult
        {
            public string TestName { get; set; } = string.Empty;
            public bool Passed { get; set; } = false;
            public TimeSpan ExecutionTime { get; set; } = TimeSpan.Zero;
            public string ErrorMessage { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public int MemoryUsageMB { get; set; } = 0;
            public double CpuUsagePercent { get; set; } = 0.0;
            public List<string> OutputLines { get; set; } = new List<string>();
            public DateTime StartTime { get; set; } = DateTime.Now;
            public DateTime EndTime { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Test suite execution summary
        /// </summary>
        public class TestSuiteSummary
        {
            public int TotalTests { get; set; } = 0;
            public int PassedTests { get; set; } = 0;
            public int FailedTests { get; set; } = 0;
            public TimeSpan TotalExecutionTime { get; set; } = TimeSpan.Zero;
            public double SuccessRate => TotalTests > 0 ? (double)PassedTests / TotalTests * 100 : 0.0;
            public string SuccessRateText => $"{SuccessRate:F1}%";
            public DateTime StartTime { get; set; } = DateTime.Now;
            public DateTime EndTime { get; set; } = DateTime.Now;
            public List<TestExecutionResult> Results { get; set; } = new List<TestExecutionResult>();
            public Dictionary<string, int> CategoryResults { get; set; } = new Dictionary<string, int>();
            public List<string> FailedTestNames { get; set; } = new List<string>();
            public List<string> PerformanceMetrics { get; set; } = new List<string>();
        }

        /// <summary>
        /// Performance metrics collection
        /// </summary>
        public class PerformanceMetrics
        {
            public double AverageLatencyMs { get; set; } = 0.0;
            public double MaxLatencyMs { get; set; } = 0.0;
            public double AverageMemoryMB { get; set; } = 0.0;
            public double MaxMemoryMB { get; set; } = 0.0;
            public double AverageCpuUsage { get; set; } = 0.0;
            public double MaxCpuUsage { get; set; } = 0.0;
            public TimeSpan TotalRunTime { get; set; } = TimeSpan.Zero;
            public int OperationsPerSecond { get; set; } = 0;
        }

        private readonly List<Action<TestExecutionResult>> _testCompletedCallbacks = new List<Action<TestExecutionResult>>();
        private readonly object _lockObject = new object();

        public TestRunner()
        {
            // Subscribe to test completion callbacks
            // In a real implementation, this would connect to test framework events
        }

        /// <summary>
        /// Register a callback to be notified when tests complete
        /// </summary>
        public void RegisterTestCompletedCallback(Action<TestExecutionResult> callback)
        {
            lock (_lockObject)
            {
                _testCompletedCallbacks.Add(callback);
            }
        }

        /// <summary>
        /// Run all tests and generate comprehensive report
        /// </summary>
        public async Task<TestSuiteSummary> RunAllTestsAsync()
        {
            var summary = new TestSuiteSummary { StartTime = DateTime.Now };
            var initialMemory = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024); // MB
            var initialCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

            try
            {
                // Get all test methods
                var testMethods = typeof(IntegrationTests).GetMethods()
                    .Where(m => m.GetCustomAttributes(typeof(TestMethodAttribute), false).Length > 0);

                summary.TotalTests = testMethods.Count();

                // Execute each test
                foreach (var testMethod in testMethods)
                {
                    var result = await ExecuteSingleTestAsync(testMethod, summary);
                    summary.Results.Add(result);

                    if (result.Passed)
                        summary.PassedTests++;
                    else
                    {
                        summary.FailedTests++;
                        summary.FailedTestNames.Add(result.TestName);
                    }

                    // Update category results
                    var categoryAttr = testMethod.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                        .FirstOrDefault() as TestCategoryAttribute;
                    var category = categoryAttr?.TestCategories.FirstOrDefault() ?? "Uncategorized";
                    
                    if (!summary.CategoryResults.ContainsKey(category))
                        summary.CategoryResults[category] = 0;
                    
                    summary.CategoryResults[category] += result.Passed ? 1 : 0;
                }

                summary.EndTime = DateTime.Now;
                summary.TotalExecutionTime = summary.EndTime - summary.StartTime;

                // Calculate performance metrics
                var finalMemory = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024); // MB
                var finalCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
                var cpuUsageMs = (finalCpuTime - initialCpuTime).TotalMilliseconds;

                var passedResults = summary.Results.Where(r => r.Passed).ToList();
                if (passedResults.Count > 0)
                {
                    summary.PerformanceMetrics.AverageLatencyMs = passedResults.Average(r => r.ExecutionTime.TotalMilliseconds);
                    summary.PerformanceMetrics.MaxLatencyMs = passedResults.Max(r => r.ExecutionTime.TotalMilliseconds);
                    summary.PerformanceMetrics.AverageMemoryMB = passedResults.Average(r => r.MemoryUsageMB);
                    summary.PerformanceMetrics.MaxMemoryMB = passedResults.Max(r => r.MemoryUsageMB);
                    summary.PerformanceMetrics.AverageCpuUsage = cpuUsageMs / summary.TotalExecutionTime.TotalMilliseconds * 100;
                    summary.PerformanceMetrics.MaxCpuUsage = summary.PerformanceMetrics.AverageCpuUsage; // Simplified
                    summary.PerformanceMetrics.TotalRunTime = summary.TotalExecutionTime;
                    summary.PerformanceMetrics.OperationsPerSecond = (int)(passedResults.Count / summary.TotalExecutionTime.TotalSeconds);
                }

                // Generate performance insights
                summary.PerformanceMetrics.Add($"Average test execution time: {passedResults.Average(r => r.ExecutionTime.TotalMilliseconds):F2}ms");
                summary.PerformanceMetrics.Add($"Slowest test: {passedResults.OrderByDescending(r => r.ExecutionTime).FirstOrDefault()?.TestName} ({passedResults.Max(r => r.ExecutionTime.TotalMilliseconds:F2}ms)");
                summary.PerformanceMetrics.Add($"Fastest test: {passedResults.OrderBy(r => r.ExecutionTime).FirstOrDefault()?.TestName} ({passedResults.Min(r => r.ExecutionTime.TotalMilliseconds:F2}ms)");
                summary.PerformanceMetrics.Add($"Memory efficiency: Average {(finalMemory - initialMemory) / passedResults.Count:F2}MB per test");
                summary.PerformanceMetrics.Add($"Total CPU usage: {cpuUsageMs:F2}ms ({summary.PerformanceMetrics.AverageCpuUsage:F1}%)");
            }
            catch (Exception ex)
            {
                summary.FailedTests = summary.TotalTests;
                summary.ErrorMessage = ex.Message;
                summary.Results.Add(new TestExecutionResult
                {
                    TestName = "TestRunner",
                    Passed = false,
                    ErrorMessage = ex.Message,
                    ExecutionTime = DateTime.Now - summary.StartTime
                });
            }

            return summary;
        }

        /// <summary>
        /// Execute a single test method and capture metrics
        /// </summary>
        private async Task<TestExecutionResult> ExecuteSingleTestAsync(System.Reflection.MethodInfo testMethod, TestSuiteSummary summary)
        {
            var result = new TestExecutionResult
            {
                TestName = testMethod.Name,
                StartTime = DateTime.Now,
                Category = GetTestCategory(testMethod)
            };

            var initialMemory = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024); // MB
            var initialCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

            try
            {
                // Check if test method takes parameters
                var parameters = testMethod.GetParameters();
                object? testInstance = null;

                // Create test instance if it's an instance method
                if (!testMethod.IsStatic)
                {
                    var testClass = typeof(IntegrationTests);
                    testInstance = Activator.CreateInstance(testClass);
                }

                // Execute the test method
                var task = testMethod.Invoke(testInstance, parameters) as Task<TestExecutionResult>;
                if (task != null)
                {
                    var testResult = await task;
                    result.Passed = testResult.Passed;
                    result.ErrorMessage = testResult.ErrorMessage;
                    result.ExecutionTime = DateTime.Now - result.StartTime;
                }
                else
                {
                    // Synchronous execution for non-async tests
                    var syncResult = testMethod.Invoke(testInstance, parameters) as TestExecutionResult;
                    if (syncResult != null)
                    {
                        result.Passed = syncResult.Passed;
                        result.ErrorMessage = syncResult.ErrorMessage;
                        result.ExecutionTime = DateTime.Now - result.StartTime;
                    }
                    else
                    {
                        result.Passed = false;
                        result.ErrorMessage = "Test method returned null";
                    }
                }

                // Capture any console output
                result.OutputLines.Add($"Test: {result.TestName}");
                result.OutputLines.Add($"Result: {(result.Passed ? "PASS" : "FAIL")}");
                if (!string.IsNullOrEmpty(result.ErrorMessage))
                {
                    result.OutputLines.Add($"Error: {result.ErrorMessage}");
                }
                result.OutputLines.Add($"Execution time: {result.ExecutionTime.TotalMilliseconds}ms");
                result.OutputLines.Add($"Memory: {initialMemory:F2}MB -> {Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024):F2}MB");
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.ErrorMessage = ex.Message;
                result.ExecutionTime = DateTime.Now - result.StartTime;
                result.OutputLines.Add($"Exception: {ex.Message}");
                result.OutputLines.Add($"Stack trace: {ex.StackTrace}");
            }

            result.EndTime = DateTime.Now;

            // Calculate performance metrics
            var finalMemory = Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024); // MB
            result.MemoryUsageMB = Math.Max(0, finalMemory - initialMemory);

            // Notify callbacks
            lock (_lockObject)
            {
                foreach (var callback in _testCompletedCallbacks)
                {
                    try
                    {
                        callback(result);
                    }
                    catch
                    {
                        // Ignore callback exceptions to avoid test failure
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Get test category from method attributes
        /// </summary>
        private string GetTestCategory(System.Reflection.MethodInfo testMethod)
        {
            var categoryAttr = testMethod.GetCustomAttributes(typeof(TestCategoryAttribute), false)
                .FirstOrDefault() as TestCategoryAttribute;
            return categoryAttr?.TestCategories.FirstOrDefault() ?? "Uncategorized";
        }

        /// <summary>
        /// Generate detailed HTML report
        /// </summary>
        public string GenerateHtmlReport(TestSuiteSummary summary)
        {
            var html = new System.Text.StringBuilder();

            html.AppendLine("<!DOCTYPE html>");
            html.AppendLine("<html>");
            html.AppendLine("<head>");
            html.AppendLine("    <title>ScottWisper Integration Test Report</title>");
            html.AppendLine("    <style>");
            html.AppendLine("        body { font-family: Arial, sans-serif; margin: 20px; }");
            html.AppendLine("        .header { background-color: #2c3e50; color: white; padding: 20px; border-radius: 5px; }");
            html.AppendLine("        .summary { background-color: #f4f4f3; padding: 15px; margin: 10px 0; border-radius: 5px; }");
            html.AppendLine("        .test-pass { color: #28a745; }");
            html.AppendLine("        .test-fail { color: #dc3545; }");
            html.AppendLine("        .metric { margin: 5px 0; }");
            html.AppendLine("        .progress-bar { width: 100%; height: 20px; background-color: #ddd; border-radius: 10px; }");
            html.AppendLine("        .progress-fill { height: 100%; background-color: #4caf50; border-radius: 10px; }");
            html.AppendLine("        table { width: 100%; border-collapse: collapse; margin-top: 20px; }");
            html.AppendLine("        th, td { padding: 10px; text-align: left; border-bottom: 1px solid #ddd; }");
            html.AppendLine("        th { background-color: #f2f2f2; }");
            html.AppendLine("    </style>");
            html.AppendLine("</head>");
            html.AppendLine("<body>");

            // Header
            html.AppendLine("    <div class=\"header\">");
            html.AppendLine("        <h1>ScottWisper Integration Test Report</h1>");
            html.AppendLine($"        <p>Generated: {summary.EndTime:yyyy-MM-dd HH:mm:ss}</p>");
            html.AppendLine("    </div>");

            // Summary
            html.AppendLine("    <div class=\"summary\">");
            html.AppendLine("        <h2>Test Suite Summary</h2>");
            html.AppendLine($"        <div class=\"progress-bar\">");
            html.AppendLine($"            <div class=\"progress-fill\" style=\"width: {summary.SuccessRate:F1}%\"></div>");
            html.AppendLine("        </div>");
            html.AppendLine($"        <p>Overall Success Rate: <span class=\"test-pass\">{summary.SuccessRateText}</span></p>");
            html.AppendLine($"        <p>Total Tests: {summary.TotalTests} | Passed: {summary.PassedTests} | Failed: {summary.FailedTests}</p>");
            html.AppendLine($"        <p>Execution Time: {summary.TotalExecutionTime.TotalSeconds:F2} seconds</p>");
            html.AppendLine("    </div>");

            // Results by category
            html.AppendLine("    <div class=\"summary\">");
            html.AppendLine("        <h2>Results by Category</h2>");
            html.AppendLine("        <table>");
            html.AppendLine("            <tr><th>Category</th><th>Total</th><th>Passed</th><th>Success Rate</th></tr>");

            foreach (var category in summary.CategoryResults.OrderByDescending(kv => kv.Value))
            {
                var categoryTotal = summary.Results.Count(r => GetTestCategory(r.GetType().GetMethod(r.TestName)!) == category.Key);
                var categoryPassed = summary.CategoryResults[category.Key];
                var categorySuccessRate = categoryTotal > 0 ? (double)categoryPassed / categoryTotal * 100 : 0.0;

                html.AppendLine($"            <tr><td>{category.Key}</td><td>{categoryTotal}</td><td>{categoryPassed}</td><td>{categorySuccessRate:F1}%</td></tr>");
            }

            html.AppendLine("        </table>");
            html.AppendLine("    </div>");

            // Performance metrics
            if (summary.PerformanceMetrics.Count > 0)
            {
                html.AppendLine("    <div class=\"summary\">");
                html.AppendLine("        <h2>Performance Metrics</h2>");
                foreach (var metric in summary.PerformanceMetrics)
                {
                    html.AppendLine($"        <div class=\"metric\">{metric}</div>");
                }
                html.AppendLine("    </div>");
            }

            // Failed tests
            if (summary.FailedTestNames.Count > 0)
            {
                html.AppendLine("    <div class=\"summary\">");
                html.AppendLine("        <h2>Failed Tests</h2>");
                html.AppendLine("        <ul>");
                foreach (var failedTest in summary.FailedTestNames)
                {
                    html.AppendLine($"            <li class=\"test-fail\">{failedTest}</li>");
                }
                html.AppendLine("        </ul>");
                html.AppendLine("    </div>");
            }

            html.AppendLine("</body>");
            html.AppendLine("</html>");

            return html.ToString();
        }

        /// <summary>
        /// Generate console report
        /// </summary>
        public string GenerateConsoleReport(TestSuiteSummary summary)
        {
            var console = new System.Text.StringBuilder();

            console.AppendLine("=".PadRight(60, '='));
            console.AppendLine("SCOTTWISPER INTEGRATION TEST REPORT");
            console.AppendLine("=".PadRight(60, '='));

            console.AppendLine();
            console.WriteLine($"Generated: {summary.EndTime:yyyy-MM-dd HH:mm:ss}");
            console.WriteLine($"Total Execution Time: {summary.TotalExecutionTime.TotalSeconds:F2} seconds");

            console.WriteLine();
            console.WriteLine("OVERALL RESULTS:");
            console.WriteLine($"  Total Tests: {summary.TotalTests}");
            console.WriteLine($"  Passed: {summary.PassedTests} ({summary.SuccessRateText})");
            console.WriteLine($"  Failed: {summary.FailedTests}");

            console.WriteLine();
            console.WriteLine("RESULTS BY CATEGORY:");
            foreach (var category in summary.CategoryResults.OrderByDescending(kv => kv.Value))
            {
                var categoryTotal = summary.Results.Count(r => GetTestCategory(r.GetType().GetMethod(r.TestName)!) == category.Key);
                var categoryPassed = summary.CategoryResults[category.Key];
                var categorySuccessRate = categoryTotal > 0 ? (double)categoryPassed / categoryTotal * 100 : 0.0;
                console.WriteLine($"  {category.Key}: {categoryPassed}/{categoryTotal} ({categorySuccessRate:F1}%)");
            }

            if (summary.FailedTestNames.Count > 0)
            {
                console.WriteLine();
                console.WriteLine("FAILED TESTS:");
                foreach (var failedTest in summary.FailedTestNames)
                {
                    console.WriteLine($"  - {failedTest}");
                }
            }

            if (summary.PerformanceMetrics.Count > 0)
            {
                console.WriteLine();
                console.WriteLine("PERFORMANCE METRICS:");
                foreach (var metric in summary.PerformanceMetrics)
                {
                    console.WriteLine($"  {metric}");
                }
            }

            if (!string.IsNullOrEmpty(summary.ErrorMessage))
            {
                console.WriteLine();
                console.WriteLine($"ERROR: {summary.ErrorMessage}");
            }

            return console.ToString();
        }

        /// <summary>
        /// Save reports to files
        /// </summary>
        public async Task SaveReportsAsync(TestSuiteSummary summary)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var reportsDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ScottWisper-TestReports");
            
            if (!Directory.Exists(reportsDir))
            {
                Directory.CreateDirectory(reportsDir);
            }

            // Save HTML report
            var htmlReport = Path.Combine(reportsDir, $"TestReport_{timestamp}.html");
            await File.WriteAllTextAsync(htmlReport, GenerateHtmlReport(summary));

            // Save console report
            var consoleReport = Path.Combine(reportsDir, $"TestReport_{timestamp}.txt");
            await File.WriteAllTextAsync(consoleReport, GenerateConsoleReport(summary));

            // Save JSON summary for programmatic access
            var jsonReport = Path.Combine(reportsDir, $"TestSummary_{timestamp}.json");
            var jsonSummary = System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(jsonReport, jsonSummary);

            return new string[] { htmlReport, consoleReport, jsonReport };
        }
    }
}