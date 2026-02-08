using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;
using WhisperKey.Infrastructure.SmokeTesting;
using WhisperKey.Infrastructure.SmokeTesting.Reporting;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of production deployment validation service
    /// </summary>
    public class ProductionValidationService : IProductionValidationService
    {
        private readonly ILogger<ProductionValidationService> _logger;
        private readonly ProductionSmokeTestOrchestrator _orchestrator;
        private readonly IDeploymentRollbackService _rollbackService;
        private readonly IAuditLoggingService _auditService;
        private SmokeTestProductionReport? _lastReport;

        public ProductionValidationService(
            ILogger<ProductionValidationService> logger,
            ProductionSmokeTestOrchestrator orchestrator,
            IDeploymentRollbackService rollbackService,
            IAuditLoggingService auditService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
            _rollbackService = rollbackService ?? throw new ArgumentNullException(nameof(rollbackService));
            _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
        }

        public async Task<bool> ValidateDeploymentAsync()
        {
            try
            {
                _logger.LogInformation("Initiating production smoke tests for validation...");
                
                _lastReport = await _orchestrator.RunProductionSmokeTestsAsync();
                
                var isReady = _orchestrator.IsProductionReady(_lastReport);
                
                if (isReady)
                {
                    _logger.LogInformation("Production validation PASSED");
                    await _auditService.LogEventAsync(
                        AuditEventType.SystemEvent,
                        "Production smoke tests passed. Deployment validated.",
                        null,
                        DataSensitivity.Low);
                }
                else
                {
                    _logger.LogWarning("Production validation FAILED");
                    await _auditService.LogEventAsync(
                        AuditEventType.SystemEvent,
                        "Production smoke tests failed. Deployment validation unsuccessful.",
                        null,
                        DataSensitivity.High);
                }
                
                return isReady;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during production validation");
                return false;
            }
        }

        public Task<SmokeTestProductionReport?> GetLastReportAsync()
        {
            return Task.FromResult(_lastReport);
        }

        public async Task VerifyAndRollbackIfRequiredAsync()
        {
            var isReady = await ValidateDeploymentAsync();
            
            if (!isReady)
            {
                _logger.LogCritical("Deployment failed production validation. Triggering automated rollback.");
                await _rollbackService.InitiateRollbackAsync();
            }
        }
    }
}
