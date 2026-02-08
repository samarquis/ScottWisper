using System;
using System.Threading.Tasks;
using WhisperKey.Infrastructure.SmokeTesting;
using WhisperKey.Infrastructure.SmokeTesting.Reporting;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for production deployment validation and automated verification
    /// </summary>
    public interface IProductionValidationService
    {
        /// <summary>
        /// Runs full production smoke tests and validates readiness
        /// </summary>
        /// <returns>True if production ready, false otherwise</returns>
        Task<bool> ValidateDeploymentAsync();
        
        /// <summary>
        /// Gets the most recent smoke test report
        /// </summary>
        Task<SmokeTestProductionReport?> GetLastReportAsync();
        
        /// <summary>
        /// Triggers an automated rollback if the deployment is not production ready
        /// </summary>
        Task VerifyAndRollbackIfRequiredAsync();
    }
}
