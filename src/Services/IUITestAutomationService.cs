using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for automated UI testing and validation
    /// </summary>
    public interface IUITestAutomationService
    {
        /// <summary>
        /// Executes a full suite of UI tests on the running application
        /// </summary>
        Task<UITestResult> RunFullUISuiteAsync();
        
        /// <summary>
        /// Validates a specific UI workflow
        /// </summary>
        Task<bool> ValidateWorkflowAsync(string workflowName);
        
        /// <summary>
        /// Gets the automation status of all UI elements
        /// </summary>
        Task<Dictionary<string, bool>> GetUIAutomationHealthAsync();
    }

    public class UITestResult
    {
        public bool Success { get; set; }
        public int TotalTests { get; set; }
        public int PassedTests { get; set; }
        public List<UITestFailure> Failures { get; set; } = new();
        public TimeSpan Duration { get; set; }
    }

    public class UITestFailure
    {
        public string TestName { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string? ScreenshotPath { get; set; }
    }
}
