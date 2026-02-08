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
    public class ErrorReportingTests
    {
        private ErrorReportingService _service = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private Mock<IIntelligentAlertingService> _mockAlertingService = null!;

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _mockAlertingService = new Mock<IIntelligentAlertingService>();
            
            _service = new ErrorReportingService(
                NullLogger<ErrorReportingService>.Instance,
                _mockAuditService.Object,
                _mockAlertingService.Object);
        }

        [TestMethod]
        public async Task Test_ErrorGroupingAndDeduplication()
        {
            var ex = new InvalidOperationException("Test Error");
            
            // Report same error twice
            var hash1 = await _service.ReportExceptionAsync(ex);
            var hash2 = await _service.ReportExceptionAsync(ex);
            
            Assert.AreEqual(hash1, hash2);
            
            var groups = await _service.GetErrorGroupsAsync();
            Assert.AreEqual(1, groups.Count);
            Assert.AreEqual(2, groups[0].OccurrenceCount);
            
            // Audit log should only be called once for the first occurrence (based on IsAlertWarranted logic)
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.Error,
                It.IsAny<string>(),
                It.IsAny<string>(),
                DataSensitivity.Medium), Times.Once);
        }

        [TestMethod]
        public void Test_ErrorClassification()
        {
            var oom = new OutOfMemoryException();
            Assert.AreEqual(ErrorReportSeverity.Critical, _service.ClassifyError(oom));
            
            var io = new System.IO.IOException();
            Assert.AreEqual(ErrorReportSeverity.Medium, _service.ClassifyError(io));
            
            var normal = new Exception();
            Assert.AreEqual(ErrorReportSeverity.Low, _service.ClassifyError(normal));
        }

        [TestMethod]
        public async Task Test_ResolveErrorGroup()
        {
            var ex = new Exception("Error");
            var hash = await _service.ReportExceptionAsync(ex);
            
            await _service.ResolveErrorGroupAsync(hash);
            
            var groups = await _service.GetErrorGroupsAsync();
            Assert.AreEqual(0, groups.Count);
        }
    }
}
