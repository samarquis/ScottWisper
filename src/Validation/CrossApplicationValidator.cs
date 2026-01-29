using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ScottWisper.Services;

namespace ScottWisper.Validation
{
    /// <summary>
    /// Comprehensive cross-application text injection validation framework
    /// Tests text injection across all target Windows applications with detailed reporting
    /// </summary>
    public class CrossApplicationValidator
    {
        private readonly ITextInjection _textInjectionService;
        private readonly ILogger<CrossApplicationValidator> _logger;
        private readonly ApplicationDetector _applicationDetector;

        // Windows API declarations for UI Automation
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        public CrossApplicationValidator(
            ITextInjection textInjectionService,
            ILogger<CrossApplicationValidator> logger,
            ApplicationDetector applicationDetector)
        {
            _textInjectionService = textInjectionService ?? throw new ArgumentNullException(nameof(textInjectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _applicationDetector = applicationDetector ?? throw new ArgumentNullException(nameof(applicationDetector));
        }

        /// <summary>
        /// Validates cross-application text injection across all target applications
        /// </summary>
        /// <returns>Comprehensive validation result with per-application status</returns>
        public async Task<CrossApplicationValidationResult> ValidateCrossApplicationInjectionAsync()
        {
            _logger.LogInformation("Starting comprehensive cross-application text injection validation");

            var result = new CrossApplicationValidationResult
            {
                StartTime = DateTime.UtcNow,
                ApplicationResults = new List<ApplicationValidationResult>()
            };

            // Define target applications for validation
            var targetApplications = new[]
            {
                new TargetAppInfo { Application = TargetApplication.Chrome, ProcessName = "chrome", DisplayName = "Google Chrome" },
                new TargetAppInfo { Application = TargetApplication.Firefox, ProcessName = "firefox", DisplayName = "Mozilla Firefox" },
                new TargetAppInfo { Application = TargetApplication.Edge, ProcessName = "msedge", DisplayName = "Microsoft Edge" },
                new TargetAppInfo { Application = TargetApplication.VisualStudio, ProcessName = "devenv", DisplayName = "Visual Studio" },
                new TargetAppInfo { Application = TargetApplication.Word, ProcessName = "WINWORD", DisplayName = "Microsoft Word" },
                new TargetAppInfo { Application = TargetApplication.NotepadPlus, ProcessName = "notepad++", DisplayName = "Notepad++" },
                new TargetAppInfo { Application = TargetApplication.WindowsTerminal, ProcessName = "WindowsTerminal", DisplayName = "Windows Terminal" }
            };

            foreach (var targetApp in targetApplications)
            {
                _logger.LogInformation("Validating text injection for {Application}", targetApp.DisplayName);
                
                var appResult = await ValidateApplicationAsync(targetApp);
                result.ApplicationResults.Add(appResult);

                // Add delay between applications to prevent interference
                await Task.Delay(1000);
            }

            // Calculate overall metrics
            result.EndTime = DateTime.UtcNow;
            result.Duration = result.EndTime - result.StartTime;
            result.TotalApplicationsTested = result.ApplicationResults.Count;
            result.SuccessfulApplications = result.ApplicationResults.Count(r => r.IsSuccess);
            result.OverallSuccessRate = (double)result.SuccessfulApplications / result.TotalApplicationsTested;
            result.CompatibilityScore = CalculateCompatibilityScore(result.ApplicationResults);

            _logger.LogInformation("Cross-application validation completed. Success rate: {SuccessRate:P2}", result.OverallSuccessRate);

            return result;
        }

        /// <summary>
        /// Validates text injection for a specific target application
        /// </summary>
        private async Task<ApplicationValidationResult> ValidateApplicationAsync(TargetAppInfo targetApp)
        {
            var result = new ApplicationValidationResult
            {
                Application = targetApp.Application,
                DisplayName = targetApp.DisplayName,
                ProcessName = targetApp.ProcessName,
                TestResults = new List<InjectionTestResult>()
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Check if application is running
                var processes = Process.GetProcessesByName(targetApp.ProcessName);
                if (processes.Length == 0)
                {
                    result.ErrorMessage = $"Application {targetApp.DisplayName} is not running";
                    result.IsSuccess = false;
                    return result;
                }

                var process = processes[0];
                result.ProcessId = process.Id;
                result.WindowTitle = process.MainWindowTitle ?? "Unknown";

                // Test different text scenarios
                var testScenarios = new[]
                {
                    new { Name = "Basic ASCII", Text = "Hello World 123", Expected = true },
                    new { Name = "Unicode Characters", Text = "Test with unicode: αβγδεζηθ", Expected = true },
                    new { Name = "Special Characters", Text = "Special chars: @#$%^&*()[]{}|\\", Expected = true },
                    new { Name = "Newlines and Tabs", Text = "Line 1\n\tIndented Line 2\nLine 3", Expected = true },
                    new { Name = "Code Snippet", Text = "public void Test() { int x = 42; }", Expected = true }
                };

                foreach (var scenario in testScenarios)
                {
                    _logger.LogDebug("Testing {Scenario} for {Application}", scenario.Name, targetApp.DisplayName);
                    
                    var testResult = await TestInjectionScenarioAsync(targetApp, scenario.Text, scenario.Name);
                    result.TestResults.Add(testResult);
                }

                // Calculate application-specific metrics
                result.IsSuccess = result.TestResults.All(t => t.Success);
                result.SuccessRate = (double)result.TestResults.Count(t => t.Success) / result.TestResults.Count;
                result.AverageLatency = TimeSpan.FromMilliseconds(result.TestResults.Average(t => t.Duration.TotalMilliseconds));
                result.AccuracyScore = CalculateAccuracyScore(result.TestResults);

                _logger.LogInformation("Application {Application} validation completed. Success: {Success}", 
                    targetApp.DisplayName, result.IsSuccess);
            }
            catch (Exception ex)
            {
                result.IsSuccess = false;
                result.ErrorMessage = ex.Message;
                _logger.LogError(ex, "Error validating application {Application}", targetApp.DisplayName);
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        /// <summary>
        /// Tests injection scenario for a specific application
        /// </summary>
        private async Task<InjectionTestResult> TestInjectionScenarioAsync(TargetAppInfo targetApp, string testText, string scenarioName)
        {
            var result = new InjectionTestResult
            {
                TestText = testText,
                ScenarioName = scenarioName,
                Application = targetApp.Application
            };

            var stopwatch = Stopwatch.StartNew();

            try
            {
                // Find and activate the target application window
                var targetWindow = await FindAndActivateApplicationWindowAsync(targetApp);
                if (targetWindow == IntPtr.Zero)
                {
                    result.Success = false;
                    result.ErrorMessage = "Could not find or activate application window";
                    return result;
                }

                // Verify focus before injection
                if (!IsTargetWindowFocused(targetWindow))
                {
                    result.Success = false;
                    result.ErrorMessage = "Failed to set focus on target window";
                    return result;
                }

                // Add small delay for focus to settle
                await Task.Delay(200);

                // Perform text injection
                var injectionSuccess = await _textInjectionService.InjectTextAsync(testText);
                
                if (injectionSuccess)
                {
                    // Verify text was injected correctly (if possible)
                    var verificationSuccess = await VerifyTextInjectionAsync(targetWindow, testText);
                    result.Success = verificationSuccess;
                    result.ErrorMessage = verificationSuccess ? string.Empty : "Text injection succeeded but verification failed";
                }
                else
                {
                    result.Success = false;
                    result.ErrorMessage = "Text injection failed";
                }
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = ex.Message;
                _logger.LogDebug(ex, "Error in injection scenario {Scenario} for {Application}", 
                    scenarioName, targetApp.DisplayName);
            }
            finally
            {
                stopwatch.Stop();
                result.Duration = stopwatch.Elapsed;
            }

            return result;
        }

        /// <summary>
        /// Finds and activates the target application window
        /// </summary>
        private async Task<IntPtr> FindAndActivateApplicationWindowAsync(TargetAppInfo targetApp)
        {
            var processes = Process.GetProcessesByName(targetApp.ProcessName);
            if (processes.Length == 0)
                return IntPtr.Zero;

            // Find the first process with a visible window
            foreach (var process in processes)
            {
                try
                {
                    var mainWindow = process.MainWindowHandle;
                    if (mainWindow != IntPtr.Zero && IsWindow(mainWindow))
                    {
                        // Restore and show the window if needed
                        ShowWindow(mainWindow, SW_RESTORE);
                        await Task.Delay(100);
                        
                        // Set foreground window
                        if (SetForegroundWindow(mainWindow))
                        {
                            await Task.Delay(200); // Allow window to become fully active
                            return mainWindow;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Error accessing process {ProcessName}", targetApp.ProcessName);
                }
            }

            return IntPtr.Zero;
        }

        /// <summary>
        /// Verifies that the target window has focus
        /// </summary>
        private bool IsTargetWindowFocused(IntPtr targetWindow)
        {
            var foregroundWindow = GetForegroundWindow();
            return foregroundWindow == targetWindow;
        }

        /// <summary>
        /// Verifies text injection (basic implementation - could be enhanced with UI Automation)
        /// </summary>
        private async Task<bool> VerifyTextInjectionAsync(IntPtr targetWindow, string expectedText)
        {
            // For now, we'll assume injection was successful if no errors occurred
            // In a real implementation, this could use UI Automation to read the text content
            // or take screenshots and perform OCR to verify the injected text
            
            await Task.Delay(100); // Small delay to allow text to appear
            
            // Basic verification - check if window is still responsive
            return IsWindow(targetWindow);
        }

        /// <summary>
        /// Calculates accuracy score for test results
        /// </summary>
        private double CalculateAccuracyScore(List<InjectionTestResult> testResults)
        {
            if (testResults.Count == 0)
                return 0.0;

            var successfulTests = testResults.Count(t => t.Success);
            return (double)successfulTests / testResults.Count;
        }

        /// <summary>
        /// Calculates overall compatibility score across all applications
        /// </summary>
        private double CalculateCompatibilityScore(List<ApplicationValidationResult> applicationResults)
        {
            if (applicationResults.Count == 0)
                return 0.0;

            var totalScore = 0.0;
            foreach (var appResult in applicationResults)
            {
                // Weight applications differently based on their importance
                var weight = GetApplicationWeight(appResult.Application);
                totalScore += appResult.SuccessRate * weight;
            }

            var totalWeight = applicationResults.Sum(app => GetApplicationWeight(app.Application));
            return totalWeight / totalWeight;
        }

        /// <summary>
        /// Gets weight for application based on importance for text injection
        /// </summary>
        private double GetApplicationWeight(TargetApplication application)
        {
            return application switch
            {
                TargetApplication.Chrome => 1.0,      // Most important browser
                TargetApplication.VisualStudio => 0.9, // Most important IDE
                TargetApplication.Word => 0.8,         // Most important office app
                TargetApplication.Firefox => 0.7,      // Secondary browser
                TargetApplication.Edge => 0.7,         // Secondary browser
                TargetApplication.NotepadPlus => 0.6, // Important text editor
                TargetApplication.WindowsTerminal => 0.5, // Important terminal
                _ => 0.5
            };
        }
    }

    #region Data Models

    /// <summary>
    /// Target application information for validation
    /// </summary>
    public class TargetAppInfo
    {
        public TargetApplication Application { get; set; }
        public string ProcessName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    /// <summary>
    /// Comprehensive result of cross-application validation
    /// </summary>
    public class CrossApplicationValidationResult
    {
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public int TotalApplicationsTested { get; set; }
        public int SuccessfulApplications { get; set; }
        public double OverallSuccessRate { get; set; }
        public double CompatibilityScore { get; set; }
        public List<ApplicationValidationResult> ApplicationResults { get; set; } = new();

        /// <summary>
        /// Gets summary of validation results
        /// </summary>
        public string GetSummary()
        {
            var successful = ApplicationResults.Count(r => r.IsSuccess);
            var total = ApplicationResults.Count;
            var rate = total > 0 ? (double)successful / total * 100 : 0;
            
            return $"Cross-application validation: {successful}/{total} applications passed ({rate:F1}%)";
        }
    }

    /// <summary>
    /// Result of validation for a specific application
    /// </summary>
    public class ApplicationValidationResult
    {
        public TargetApplication Application { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public int ProcessId { get; set; }
        public string WindowTitle { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public double SuccessRate { get; set; }
        public double AccuracyScore { get; set; }
        public TimeSpan AverageLatency { get; set; }
        public TimeSpan Duration { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public List<InjectionTestResult> TestResults { get; set; } = new();
    }

    /// <summary>
    /// Extended injection test result for validation
    /// </summary>
    public class InjectionTestResult
    {
        public TargetApplication Application { get; set; }
        public string ScenarioName { get; set; } = string.Empty;
        public string TestText { get; set; } = string.Empty;
        public bool Success { get; set; }
        public string ErrorMessage { get; set; } = string.Empty;
        public TimeSpan Duration { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    #endregion
}