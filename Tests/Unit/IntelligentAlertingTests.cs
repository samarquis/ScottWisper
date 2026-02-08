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
    public class IntelligentAlertingTests
    {
        private IntelligentAlertingService _service = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private Mock<IWebhookService> _mockWebhookService = null!;
        private List<AuditLogEntry> _auditLogs = new();

        [TestInitialize]
        public void Setup()
        {
            _mockAuditService = new Mock<IAuditLoggingService>();
            _mockWebhookService = new Mock<IWebhookService>();
            _auditLogs = new List<AuditLogEntry>();

            _mockAuditService.Setup(a => a.GetLogsAsync(It.IsAny<DateTime?>(), It.IsAny<DateTime?>(), It.IsAny<AuditEventType?>(), It.IsAny<ComplianceType?>()))
                .ReturnsAsync((DateTime? start, DateTime? end, AuditEventType? type, ComplianceType? compliance) =>
                {
                    var query = _auditLogs.AsQueryable();
                    if (start.HasValue) query = query.Where(l => l.Timestamp >= start.Value);
                    if (end.HasValue) query = query.Where(l => l.Timestamp <= end.Value);
                    if (type.HasValue) query = query.Where(l => l.EventType == type.Value);
                    return query.ToList();
                });

            _mockAuditService.Setup(a => a.LogEventAsync(It.IsAny<AuditEventType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DataSensitivity>()))
                .ReturnsAsync((AuditEventType type, string desc, string? meta, DataSensitivity sens) =>
                {
                    var entry = new AuditLogEntry { EventType = type, Description = desc, Metadata = meta, Sensitivity = sens, Timestamp = DateTime.UtcNow };
                    _auditLogs.Add(entry);
                    return entry;
                });

            _service = new IntelligentAlertingService(
                NullLogger<IntelligentAlertingService>.Instance,
                _mockAuditService.Object,
                _mockWebhookService.Object);
        }

        [TestMethod]
        public async Task Test_PredictiveAnomalyDetection()
        {
            // 1. Establish history (10 points with low values)
            for (int i = 0; i < 10; i++)
            {
                _auditLogs.Clear();
                // Simulate 2 events per hour in history (use -30 mins to be safe within the 1h window)
                for (int j = 0; j < 2; j++)
                {
                    _auditLogs.Add(new AuditLogEntry { EventType = AuditEventType.SecurityEvent, Timestamp = DateTime.UtcNow.AddMinutes(-30) });
                }
                await _service.AnalyzeSystemHealthAsync();
            }

            // 2. Simulate anomaly (20 events in current hour)
            _auditLogs.Clear();
            for (int i = 0; i < 20; i++)
            {
                _auditLogs.Add(new AuditLogEntry { EventType = AuditEventType.SecurityEvent, Timestamp = DateTime.UtcNow.AddMinutes(-10) });
            }

            // 3. Run analysis
            await _service.AnalyzeSystemHealthAsync();

            // 4. Verify alert was logged
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SecurityEvent,
                It.Is<string>(s => s.Contains("significantly higher than historical average")),
                It.IsAny<string>(),
                DataSensitivity.High), Times.Once);
            
            // 5. Verify escalation via webhook for high severity anomaly
            _mockWebhookService.Verify(w => w.SendWebhookAsync(
                WebhookEventType.SecurityEvent,
                It.Is<Dictionary<string, object>>(d => d.ContainsKey("isEscalation")),
                It.IsAny<string>()), Times.Once);
        }

        [TestMethod]
        public async Task Test_RootCauseAnalysis_AuthFailure()
        {
            var sessionId = "test-session-123";
            var baseTime = DateTime.UtcNow;

            // Setup contributing events
            _auditLogs.Add(new AuditLogEntry { EventType = AuditEventType.AuthenticationFailed, SessionId = sessionId, Timestamp = baseTime.AddMinutes(-5) });
            _auditLogs.Add(new AuditLogEntry { EventType = AuditEventType.AuthenticationFailed, SessionId = sessionId, Timestamp = baseTime.AddMinutes(-4) });
            
            var triggerEvent = new AuditLogEntry { Id = "trigger-1", EventType = AuditEventType.SecurityEvent, SessionId = sessionId, Timestamp = baseTime };
            _auditLogs.Add(triggerEvent);

            var alert = new SecurityAlert
            {
                Id = "alert-1",
                AuditEventId = triggerEvent.Id,
                Timestamp = baseTime
            };

            // Run RCA
            var result = await _service.PerformRootCauseAnalysisAsync(alert);

            Assert.IsNotNull(result);
            Assert.IsTrue(result.ProbableCause.Contains("authentication failures"));
            Assert.IsTrue(result.ContributingEvents.Any(e => e.EventType == AuditEventType.AuthenticationFailed));
            Assert.IsTrue(result.ConfidenceScore > 0.8);
        }

        [TestMethod]
        public async Task Test_DynamicThresholdCalculation()
        {
            // 1. Establish history (10 points: 10, 11, 9, 10, 12, 8, 11, 9, 10, 10)
            // Avg = 10, StdDev approx 1.15
            var historyValues = new double[] { 10, 11, 9, 10, 12, 8, 11, 9, 10, 10 };
            
            foreach (var val in historyValues)
            {
                _auditLogs.Clear();
                for (int i = 0; i < val; i++)
                {
                    _auditLogs.Add(new AuditLogEntry { EventType = AuditEventType.SecurityEvent, Timestamp = DateTime.UtcNow.AddMinutes(-30) });
                }
                await _service.AnalyzeSystemHealthAsync();
            }

            // 2. Calculate dynamic threshold
            var parameters = await _service.CalculateDynamicThresholdAsync(AuditEventType.SecurityEvent.ToString());

            Assert.IsTrue(parameters.ContainsKey("dynamicCount"));
            int dynamicCount = (int)parameters["dynamicCount"];
            
            // Average 10 + 3*StdDev (approx 3.45) = 13.45 -> 14
            Assert.IsTrue(dynamicCount >= 13 && dynamicCount <= 15);
            Assert.AreEqual(true, parameters["isDynamic"]);
        }

        [TestMethod]
        public async Task Test_Escalation_HighSeverity()
        {
            var alert = new SecurityAlert
            {
                Id = "alert-high",
                RuleName = "Test Rule",
                Severity = SecurityAlertSeverity.High,
                Description = "High severity test alert",
                Timestamp = DateTime.UtcNow
            };

            await _service.EscalateAlertAsync(alert);

            _mockWebhookService.Verify(w => w.SendWebhookAsync(
                WebhookEventType.SecurityEvent,
                It.Is<Dictionary<string, object>>(d => 
                    d["severity"].ToString() == "High" && 
                    (bool)d["isEscalation"] == true),
                It.IsAny<string>()), Times.Once);
        }
    }
}
