using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WhisperKey.Models;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for comprehensive error reporting, classification and deduplication
    /// </summary>
    public interface IErrorReportingService
    {
        /// <summary>
        /// Reports an exception to the system
        /// </summary>
        Task<string> ReportExceptionAsync(Exception ex, string? source = null, ErrorReportSeverity severity = ErrorReportSeverity.Medium);
        
        /// <summary>
        /// Gets all current error groups
        /// </summary>
        Task<List<ErrorGroup>> GetErrorGroupsAsync();
        
        /// <summary>
        /// Clears/Resolves an error group
        /// </summary>
        Task ResolveErrorGroupAsync(string errorHash);
        
        /// <summary>
        /// Classifies an error based on its type and context
        /// </summary>
        ErrorReportSeverity ClassifyError(Exception ex);
    }
}
