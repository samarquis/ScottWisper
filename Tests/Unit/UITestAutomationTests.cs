using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class UITestAutomationTests
    {
        private UITestAutomationService _service = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _service = new UITestAutomationService(
                NullLogger<UITestAutomationService>.Instance,
                _mockAuditService.Object);
        }

        [TestMethod]
        public async Task Test_UIAutomation_HealthReport()
        {
            var health = await _service.GetUIAutomationHealthAsync();
            
            Assert.IsNotNull(health);
            Assert.IsTrue(health.ContainsKey("MainWindow"));
            Assert.IsTrue(health["MainWindow"]);
        }

        [TestMethod]
        public async Task Test_UIAutomation_WorkflowValidation()
        {
            var success = await _service.ValidateWorkflowAsync("TestWorkflow");
            Assert.IsTrue(success);
        }
    }
}
