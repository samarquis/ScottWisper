using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Services;

namespace WhisperKey.Infrastructure.SmokeTesting.Reporting
{
    /// <summary>
    /// Production smoke test reporting framework
    /// </summary>
    public class SmokeTestReportingService
    {
        private readonly ILogger<SmokeTestReportingService> _logger;
        private readonly SmokeTestConfiguration _configuration;

        public SmokeTestReportingService(ILogger<SmokeTestReportingService> logger, SmokeTestConfiguration configuration)
        {
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// Generate comprehensive production report
        /// </summary>
        public async Task<SmokeTestProductionReport> GenerateProductionReportAsync(SmokeTestSuiteResult testResults)
        {
            var report = new SmokeTestProductionReport
            {
                TestResults = testResults,
                GeneratedAt = DateTime.UtcNow,
                Environment = Environment.GetEnvironmentVariable("ENVIRONMENT") ?? "Unknown",
                BuildVersion = Environment.GetEnvironmentVariable("BUILD_VERSION") ?? "Unknown",
                CommitHash = Environment.GetEnvironmentVariable("COMMIT_HASH") ?? "Unknown"
            };

            // Add system metrics
            report.SystemMetrics = await CollectSystemMetricsAsync();

            // Add deployment validation
            report.DeploymentValidation = await ValidateDeploymentAsync(testResults);

            // Add recommendations
            report.Recommendations = GenerateRecommendations(testResults);

            // Add compliance status
            report.ComplianceStatus = await ValidateComplianceAsync(testResults);

            _logger.LogInformation("Production smoke test report generated: {PassedTests}/{TotalTests} passed, Success Rate: {SuccessRate:F1}%",
                testResults.PassedTests, testResults.TotalTests, testResults.SuccessRate);

            return report;
        }

        /// <summary>
        /// Export report to multiple formats for production monitoring
        /// </summary>
        public async Task ExportProductionReportAsync(SmokeTestProductionReport report, string outputDirectory)
        {
            System.IO.Directory.CreateDirectory(outputDirectory);

            // Export JSON for API consumption
            var jsonPath = System.IO.Path.Combine(outputDirectory, $"smoke-test-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            await ExportJsonReportAsync(report, jsonPath);

            // Export HTML for dashboard viewing
            var htmlPath = System.IO.Path.Combine(outputDirectory, $"smoke-test-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.html");
            await ExportHtmlReportAsync(report, htmlPath);

            // Export Markdown for documentation
            var mdPath = System.IO.Path.Combine(outputDirectory, $"smoke-test-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
            await ExportMarkdownReportAsync(report, mdPath);

            // Export metrics for monitoring systems
            var metricsPath = System.IO.Path.Combine(outputDirectory, $"smoke-test-metrics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.prom");
            await ExportPrometheusMetricsAsync(report, metricsPath);

            _logger.LogInformation("Production smoke test report exported to {OutputDirectory}", outputDirectory);
        }

        private async Task<SystemMetrics> CollectSystemMetricsAsync()
        {
            var metrics = new SystemMetrics
            {
                CollectedAt = DateTime.UtcNow,
                MachineName = Environment.MachineName,
                ProcessId = Environment.ProcessId,
                OsVersion = Environment.OSVersion.ToString(),
                DotNetVersion = Environment.Version.ToString()
            };

            // Collect memory metrics
            using var process = System.Diagnostics.Process.GetCurrentProcess();
            metrics.MemoryUsageMb = process.WorkingSet64 / 1024 / 1024;
            metrics.PeakMemoryUsageMb = process.PeakWorkingSet64 / 1024 / 1024;
            metrics.GcMemoryMb = GC.GetTotalMemory(false) / 1024 / 1024;

            // Collect CPU metrics (simplified)
            metrics.CpuUsagePercent = process.TotalProcessorTime.TotalMilliseconds / Environment.ProcessorCount;

            // Collect disk metrics
            var drive = new System.IO.DriveInfo(System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? "C:");
            metrics.DiskFreeSpaceGb = drive.AvailableFreeSpace / 1024 / 1024 / 1024;
            metrics.DiskTotalSpaceGb = drive.TotalSize / 1024 / 1024 / 1024;

            return await Task.FromResult(metrics);
        }

        private async Task<DeploymentValidation> ValidateDeploymentAsync(SmokeTestSuiteResult testResults)
        {
            var validation = new DeploymentValidation
            {
                IsValid = testResults.AllPassed && !testResults.HasCriticalFailures,
                ValidatedAt = DateTime.UtcNow
            };

            // Check critical test categories
            var criticalCategories = new[] { SmokeTestCategory.Critical, SmokeTestCategory.HealthCheck };
            foreach (var category in criticalCategories)
            {
                if (testResults.ResultsByCategory.ContainsKey(category))
                {
                    var categoryResults = testResults.ResultsByCategory[category];
                    var categoryPassed = categoryResults.All(r => r.Success);
                    validation.CategoryValidation[category.ToString()] = categoryPassed;

                    if (!categoryPassed)
                    {
                        validation.IsValid = false;
                        validation.ValidationErrors.Add($"Critical category {category} has failures");
                    }
                }
            }

            // Check performance thresholds
            if (testResults.ResultsByCategory.ContainsKey(SmokeTestCategory.Performance))
            {
                var performanceResults = testResults.ResultsByCategory[SmokeTestCategory.Performance];
                var performancePassed = performanceResults.All(r => r.Success);
                validation.PerformanceValidation = performancePassed;

                if (!performancePassed)
                {
                    validation.ValidationErrors.Add("Performance thresholds not met");
                }
            }

            // Check security compliance
            if (testResults.ResultsByCategory.ContainsKey(SmokeTestCategory.Security))
            {
                var securityResults = testResults.ResultsByCategory[SmokeTestCategory.Security];
                var securityPassed = securityResults.All(r => r.Success);
                validation.SecurityValidation = securityPassed;

                if (!securityPassed)
                {
                    validation.ValidationErrors.Add("Security compliance validation failed");
                }
            }

            return await Task.FromResult(validation);
        }

        private List<string> GenerateRecommendations(SmokeTestSuiteResult testResults)
        {
            var recommendations = new List<string>();

            if (!testResults.AllPassed)
            {
                recommendations.Add("Address failed tests before proceeding to production deployment");
            }

            if (testResults.HasCriticalFailures)
            {
                recommendations.Add("CRITICAL: Fix critical system failures immediately");
            }

            // Performance recommendations
            if (testResults.ResultsByCategory.ContainsKey(SmokeTestCategory.Performance))
            {
                var performanceResults = testResults.ResultsByCategory[SmokeTestCategory.Performance];
                var failedPerformanceTests = performanceResults.Where(r => !r.Success).ToList();
                if (failedPerformanceTests.Any())
                {
                    recommendations.Add($"Optimize performance for {failedPerformanceTests.Count} failing performance tests");
                }
            }

            // Security recommendations
            if (testResults.ResultsByCategory.ContainsKey(SmokeTestCategory.Security))
            {
                var securityResults = testResults.ResultsByCategory[SmokeTestCategory.Security];
                var failedSecurityTests = securityResults.Where(r => !r.Success).ToList();
                if (failedSecurityTests.Any())
                {
                    recommendations.Add($"Address security compliance issues in {failedSecurityTests.Count} areas");
                }
            }

            // System resource recommendations
            var avgTestDuration = testResults.TestResults.Average(r => r.Duration.TotalSeconds);
            if (avgTestDuration > 30)
            {
                recommendations.Add("Consider optimizing test execution time for faster smoke testing");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("All systems ready for production deployment");
            }

            return recommendations;
        }

        private async Task<ComplianceStatus> ValidateComplianceAsync(SmokeTestSuiteResult testResults)
        {
            var compliance = new ComplianceStatus
            {
                ValidatedAt = DateTime.UtcNow
            };

            // Check SOC2 compliance
            if (testResults.ResultsByCategory.ContainsKey(SmokeTestCategory.Security))
            {
                var securityResults = testResults.ResultsByCategory[SmokeTestCategory.Security];
                var soc2Tests = securityResults.Where(r => r.TestName.Contains("SOC2")).ToList();
                compliance.SOC2Compliant = soc2Tests.All(r => r.Success);
            }

            // Check audit logging compliance
            var auditTests = testResults.TestResults.Where(r => r.TestName.Contains("Audit")).ToList();
            compliance.AuditLoggingCompliant = auditTests.All(r => r.Success);

            // Check security compliance
            var securityTests = testResults.TestResults.Where(r => r.Category == SmokeTestCategory.Security).ToList();
            compliance.SecurityCompliant = securityTests.All(r => r.Success);

            compliance.OverallCompliant = compliance.SOC2Compliant && 
                                       compliance.AuditLoggingCompliant && 
                                       compliance.SecurityCompliant;

            return await Task.FromResult(compliance);
        }

        private async Task ExportJsonReportAsync(SmokeTestProductionReport report, string filePath)
        {
            var json = System.Text.Json.JsonSerializer.Serialize(report, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });
            await File.WriteAllTextAsync(filePath, json);
        }

        private async Task ExportHtmlReportAsync(SmokeTestProductionReport report, string filePath)
        {
            var html = GenerateHtmlReport(report);
            await File.WriteAllTextAsync(filePath, html);
        }

        private async Task ExportMarkdownReportAsync(SmokeTestProductionReport report, string filePath)
        {
            var markdown = GenerateMarkdownReport(report);
            await File.WriteAllTextAsync(filePath, markdown);
        }

        private async Task ExportPrometheusMetricsAsync(SmokeTestProductionReport report, string filePath)
        {
            var metrics = GeneratePrometheusMetrics(report);
            await File.WriteAllTextAsync(filePath, metrics);
        }

        private string GenerateHtmlReport(SmokeTestProductionReport report)
        {
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <title>Smoke Test Report - {report.GeneratedAt:yyyy-MM-dd HH:mm:ss}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; }}
        .header {{ background-color: #f0f0f0; padding: 20px; border-radius: 5px; }}
        .summary {{ margin: 20px 0; }}
        .pass {{ color: green; }}
        .fail {{ color: red; }}
        .critical {{ color: darkred; font-weight: bold; }}
        table {{ border-collapse: collapse; width: 100%; }}
        th, td {{ border: 1px solid #ddd; padding: 8px; text-align: left; }}
        th {{ background-color: #f2f2f2; }}
    </style>
</head>
<body>
    <div class='header'>
        <h1>Production Smoke Test Report</h1>
        <p><strong>Generated:</strong> {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC</p>
        <p><strong>Environment:</strong> {report.Environment}</p>
        <p><strong>Build Version:</strong> {report.BuildVersion}</p>
        <p><strong>Commit Hash:</strong> {report.CommitHash}</p>
    </div>

    <div class='summary'>
        <h2>Test Summary</h2>
        <p><strong>Total Tests:</strong> {report.TestResults.TotalTests}</p>
        <p><strong>Passed:</strong> <span class='pass'>{report.TestResults.PassedTests}</span></p>
        <p><strong>Failed:</strong> <span class='fail'>{report.TestResults.FailedTests}</span></p>
        <p><strong>Success Rate:</strong> {report.TestResults.SuccessRate:F1}%</p>
        <p><strong>Duration:</strong> {report.TestResults.Duration.TotalSeconds:F2} seconds</p>
        <p><strong>Production Ready:</strong> {(report.DeploymentValidation.IsValid ? "<span class='pass'>YES</span>" : "<span class='critical'>NO</span>")}</p>
    </div>

    <h2>Test Results by Category</h2>
    <table>
        <tr><th>Category</th><th>Tests</th><th>Passed</th><th>Failed</th><th>Success Rate</th></tr>";

            foreach (var category in report.TestResults.ResultsByCategory)
            {
                var categoryResults = category.Value;
                var passed = categoryResults.Count(r => r.Success);
                var total = categoryResults.Count;
                var successRate = total > 0 ? (double)passed / total * 100 : 0;
                var cssClass = successRate == 100 ? "pass" : successRate >= 80 ? "" : "fail";

                html += $@"
        <tr>
            <td>{category.Key}</td>
            <td>{total}</td>
            <td class='pass'>{passed}</td>
            <td class='fail'>{total - passed}</td>
            <td class='{cssClass}'>{successRate:F1}%</td>
        </tr>";
            }

            html += @"
    </table>

    <h2>Recommendations</h2>
    <ul>";

            foreach (var recommendation in report.Recommendations)
            {
                html += $"<li>{recommendation}</li>";
            }

            html += @"
    </ul>

    <h2>System Metrics</h2>
    <table>
        <tr><th>Metric</th><th>Value</th></tr>
        <tr><td>Memory Usage</td><td>" + report.SystemMetrics.MemoryUsageMb + @" MB</td></tr>
        <tr><td>Peak Memory</td><td>" + report.SystemMetrics.PeakMemoryUsageMb + @" MB</td></tr>
        <tr><td>Disk Free Space</td><td>" + report.SystemMetrics.DiskFreeSpaceGb + @" GB</td></tr>
    </table>

</body>
</html>";

            return html;
        }

        private string GenerateMarkdownReport(SmokeTestProductionReport report)
        {
            var md = $@"# Production Smoke Test Report

**Generated:** {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC  
**Environment:** {report.Environment}  
**Build Version:** {report.BuildVersion}  
**Commit Hash:** {report.CommitHash}

## Test Summary

- **Total Tests:** {report.TestResults.TotalTests}
- **Passed:** {report.TestResults.PassedTests}
- **Failed:** {report.TestResults.FailedTests}
- **Success Rate:** {report.TestResults.SuccessRate:F1}%
- **Duration:** {report.TestResults.Duration.TotalSeconds:F2} seconds
- **Production Ready:** {(report.DeploymentValidation.IsValid ? "✅ YES" : "❌ NO")}

## Test Results by Category

";

            foreach (var category in report.TestResults.ResultsByCategory)
            {
                var categoryResults = category.Value;
                var passed = categoryResults.Count(r => r.Success);
                var total = categoryResults.Count;
                var successRate = total > 0 ? (double)passed / total * 100 : 0;
                var status = successRate == 100 ? "✅" : successRate >= 80 ? "⚠️" : "❌";

                md += $"### {category.Key} {status}\n";
                md += $"- Tests: {total}\n";
                md += $"- Passed: {passed}\n";
                md += $"- Failed: {total - passed}\n";
                md += $"- Success Rate: {successRate:F1}%\n\n";
            }

            md += "## Recommendations\n\n";
            foreach (var recommendation in report.Recommendations)
            {
                md += $"- {recommendation}\n";
            }

            md += $"\n## System Metrics\n\n";
            md += $"- Memory Usage: {report.SystemMetrics.MemoryUsageMb} MB\n";
            md += $"- Peak Memory: {report.SystemMetrics.PeakMemoryUsageMb} MB\n";
            md += $"- Disk Free Space: {report.SystemMetrics.DiskFreeSpaceGb} GB\n";

            return md;
        }

        private string GeneratePrometheusMetrics(SmokeTestProductionReport report)
        {
            var metrics = $@"# Smoke test metrics for {report.Environment}
# Generated at {report.GeneratedAt:yyyy-MM-dd HH:mm:ss} UTC

smoke_test_total {report.TestResults.TotalTests}
smoke_test_passed {report.TestResults.PassedTests}
smoke_test_failed {report.TestResults.FailedTests}
smoke_test_success_rate {report.TestResults.SuccessRate:F2}
smoke_test_duration_seconds {report.TestResults.Duration.TotalSeconds:F2}
smoke_test_production_ready {(report.DeploymentValidation.IsValid ? 1 : 0)}

# System metrics
system_memory_usage_mb {report.SystemMetrics.MemoryUsageMb}
system_disk_free_gb {report.SystemMetrics.DiskFreeSpaceGb}

# Test results by category
";

            foreach (var category in report.TestResults.ResultsByCategory)
            {
                var categoryResults = category.Value;
                var passed = categoryResults.Count(r => r.Success);
                var total = categoryResults.Count;
                var successRate = total > 0 ? (double)passed / total * 100 : 0;

                metrics += $@"smoke_test_category_total{{category=""{category.Key}""}} {total}
smoke_test_category_passed{{category=""{category.Key}""}} {passed}
smoke_test_category_success_rate{{category=""{category.Key}""}} {successRate:F2}
";
            }

            return metrics;
        }
    }

    /// <summary>
    /// Production smoke test report
    /// </summary>
    public class SmokeTestProductionReport
    {
        public SmokeTestSuiteResult TestResults { get; set; } = new SmokeTestSuiteResult();
        public DateTime GeneratedAt { get; set; }
        public string Environment { get; set; } = string.Empty;
        public string BuildVersion { get; set; } = string.Empty;
        public string CommitHash { get; set; } = string.Empty;
        public SystemMetrics SystemMetrics { get; set; } = new SystemMetrics();
        public DeploymentValidation DeploymentValidation { get; set; } = new DeploymentValidation();
        public List<string> Recommendations { get; set; } = new List<string>();
        public ComplianceStatus ComplianceStatus { get; set; } = new ComplianceStatus();
    }

    /// <summary>
    /// System metrics collection
    /// </summary>
    public class SystemMetrics
    {
        public DateTime CollectedAt { get; set; }
        public string MachineName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string OsVersion { get; set; } = string.Empty;
        public string DotNetVersion { get; set; } = string.Empty;
        public long MemoryUsageMb { get; set; }
        public long PeakMemoryUsageMb { get; set; }
        public long GcMemoryMb { get; set; }
        public double CpuUsagePercent { get; set; }
        public long DiskFreeSpaceGb { get; set; }
        public long DiskTotalSpaceGb { get; set; }
    }

    /// <summary>
    /// Deployment validation results
    /// </summary>
    public class DeploymentValidation
    {
        public bool IsValid { get; set; }
        public DateTime ValidatedAt { get; set; }
        public Dictionary<string, bool> CategoryValidation { get; set; } = new Dictionary<string, bool>();
        public bool PerformanceValidation { get; set; }
        public bool SecurityValidation { get; set; }
        public List<string> ValidationErrors { get; set; } = new List<string>();
    }

    /// <summary>
    /// Compliance status validation
    /// </summary>
    public class ComplianceStatus
    {
        public DateTime ValidatedAt { get; set; }
        public bool SOC2Compliant { get; set; }
        public bool AuditLoggingCompliant { get; set; }
        public bool SecurityCompliant { get; set; }
        public bool OverallCompliant { get; set; }
    }
}
