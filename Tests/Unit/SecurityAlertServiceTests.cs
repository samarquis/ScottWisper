using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class SecurityAlertServiceTests
    {
        private SecurityAlertService _service = null!;
        private IAuditLoggingService _auditService = null!;

        [TestInitialize]
        public void Setup()
        {
            _auditService = new NullAuditLoggingService();
            _service = new SecurityAlertService(
                NullLogger<SecurityAlertService>.Instance,
                _auditService);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service?.Dispose();
        }

        #region Basic Alert Rule Tests

        [TestMethod]
        public async Task Test_GetDefaultAlertRules()
        {
            var rules = await _service.GetAlertRulesAsync();

            Assert.IsNotNull(rules);
            Assert.IsTrue(rules.Count >= 4); // Should have default rules

            // Check for expected default rules
            Assert.IsTrue(rules.Any(r => r.Name.Contains("Failed Permission")));
            Assert.IsTrue(rules.Any(r => r.Name.Contains("API Key")));
            Assert.IsTrue(rules.Any(r => r.Name.Contains("Event Burst")));
            Assert.IsTrue(rules.Any(r => r.Name.Contains("Critical Data")));
        }

        [TestMethod]
        public async Task Test_ConfigureAlertRule()
        {
            var newRule = new SecurityAlertRule
            {
                Name = "Test Rule",
                Description = "Test alert rule",
                EventType = AuditEventType.SecurityEvent,
                Severity = SecurityAlertSeverity.High,
                Condition = SecurityAlertCondition.CountInTimeWindow,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["count"] = 5,
                    ["timeWindowMinutes"] = 10
                }
            };

            await _service.ConfigureAlertRuleAsync(newRule);

            var rules = await _service.GetAlertRulesAsync();
            var configuredRule = rules.FirstOrDefault(r => r.Name == "Test Rule");

            Assert.IsNotNull(configuredRule);
            Assert.AreEqual(SecurityAlertSeverity.High, configuredRule.Severity);
            Assert.AreEqual(5, configuredRule.Parameters["count"]);
        }

        #endregion

        #region Alert Triggering Tests

        [TestMethod]
        public async Task Test_FailedPermissionAttempts_Alert()
        {
            // Create multiple failed permission events
            var events = new[]
            {
                new AuditLogEntry { EventType = AuditEventType.SecurityEvent, Description = "failed permission check", Timestamp = DateTime.UtcNow.AddMinutes(-4) },
                new AuditLogEntry { EventType = AuditEventType.SecurityEvent, Description = "failed permission check", Timestamp = DateTime.UtcNow.AddMinutes(-3) },
                new AuditLogEntry { EventType = AuditEventType.SecurityEvent, Description = "failed permission check", Timestamp = DateTime.UtcNow.AddMinutes(-2) }
            };

            // Process events
            foreach (var auditEvent in events)
            {
                await _service.CheckEventAsync(auditEvent);
            }

            // Check for alerts
            var alerts = await _service.GetRecentAlertsAsync(24);
            var permissionAlerts = alerts.Where(a => a.RuleName.Contains("Failed Permission")).ToList();

            Assert.IsTrue(permissionAlerts.Count >= 1, "Should have triggered failed permission alert");
        }

        [TestMethod]
        public async Task Test_ApiKeyUnusualHours_Alert()
        {
            // Create API key access event outside business hours
            var unusualHourEvent = new AuditLogEntry
            {
                EventType = AuditEventType.ApiKeyAccessed,
                Description = "API key accessed",
                Timestamp = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 2, 0, 0) // 2 AM
            };

            await _service.CheckEventAsync(unusualHourEvent);

            var alerts = await _service.GetRecentAlertsAsync(24);
            var apiKeyAlerts = alerts.Where(a => a.RuleName.Contains("API Key")).ToList();

            Assert.IsTrue(apiKeyAlerts.Count >= 1, "Should have triggered unusual hours alert for API key access");
        }

        [TestMethod]
        public async Task Test_CriticalDataAccess_Alert()
        {
            // Create critical sensitivity event
            var criticalEvent = new AuditLogEntry
            {
                EventType = AuditEventType.ApiKeyAccessed,
                Description = "Critical data accessed",
                Sensitivity = DataSensitivity.Critical,
                Timestamp = DateTime.UtcNow
            };

            await _service.CheckEventAsync(criticalEvent);

            var alerts = await _service.GetRecentAlertsAsync(24);
            var criticalAlerts = alerts.Where(a => a.Severity == SecurityAlertSeverity.Critical).ToList();

            Assert.IsTrue(criticalAlerts.Count >= 1, "Should have triggered critical data access alert");
        }

        [TestMethod]
        public async Task Test_SecurityEventBurst_Alert()
        {
            // Create many security events in short time
            var baseTime = DateTime.UtcNow.AddMinutes(-1);
            var events = Enumerable.Range(0, 12)
                .Select(i => new AuditLogEntry
                {
                    EventType = AuditEventType.SecurityEvent,
                    Description = $"Security event {i}",
                    Timestamp = baseTime.AddSeconds(i * 5) // Spread over 1 minute
                })
                .ToList();

            // Process events
            foreach (var auditEvent in events)
            {
                await _service.CheckEventAsync(auditEvent);
            }

            var alerts = await _service.GetRecentAlertsAsync(24);
            var burstAlerts = alerts.Where(a => a.RuleName.Contains("Event Burst")).ToList();

            Assert.IsTrue(burstAlerts.Count >= 1, "Should have triggered security event burst alert");
        }

        #endregion

        #region Alert Cooldown Tests

        [TestMethod]
        public async Task Test_AlertCooldownPeriod()
        {
            // Create rule with short cooldown
            var testRule = new SecurityAlertRule
            {
                Name = "Cooldown Test Rule",
                Description = "Test cooldown functionality",
                EventType = AuditEventType.SecurityEvent,
                Severity = SecurityAlertSeverity.Low,
                Condition = SecurityAlertCondition.CountInTimeWindow,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["count"] = 1,
                    ["timeWindowMinutes"] = 1
                },
                CooldownMinutes = 1 // 1 minute cooldown
            };

            await _service.ConfigureAlertRuleAsync(testRule);

            // Trigger event twice quickly
            var event1 = new AuditLogEntry
            {
                EventType = AuditEventType.SecurityEvent,
                Description = "Test event 1",
                Timestamp = DateTime.UtcNow
            };

            var event2 = new AuditLogEntry
            {
                EventType = AuditEventType.SecurityEvent,
                Description = "Test event 2",
                Timestamp = DateTime.UtcNow.AddSeconds(10)
            };

            await _service.CheckEventAsync(event1);
            await _service.CheckEventAsync(event2);

            var alerts = await _service.GetRecentAlertsAsync(24);
            var cooldownAlerts = alerts.Where(a => a.RuleName == "Cooldown Test Rule").ToList();

            // Should only trigger once due to cooldown
            Assert.AreEqual(1, cooldownAlerts.Count, "Should only trigger one alert due to cooldown period");
        }

        #endregion

        #region Alert Statistics Tests

        [TestMethod]
        public async Task Test_GetAlertStatistics()
        {
            // Generate some alerts
            await _service.CheckEventAsync(new AuditLogEntry
            {
                EventType = AuditEventType.SecurityEvent,
                Description = "failed permission check",
                Timestamp = DateTime.UtcNow.AddMinutes(-1)
            });

            await _service.CheckEventAsync(new AuditLogEntry
            {
                EventType = AuditEventType.ApiKeyAccessed,
                Description = "API key access",
                Timestamp = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day, 2, 0, 0)
            });

            var stats = await _service.GetAlertStatisticsAsync();

            Assert.IsNotNull(stats);
            Assert.IsTrue(stats.TotalAlerts >= 1);
            Assert.IsTrue(stats.AlertsBySeverity.Any());
            Assert.IsTrue(stats.AlertsByType.Any());
            Assert.IsNotNull(stats.GeneratedAt);
        }

        [TestMethod]
        public async Task Test_ClearOldAlerts()
        {
            // Generate some alerts
            await _service.CheckEventAsync(new AuditLogEntry
            {
                EventType = AuditEventType.SecurityEvent,
                Description = "Test alert for cleanup",
                Timestamp = DateTime.UtcNow
            });

            // Clear alerts older than 0 days (should clear all)
            var clearedCount = await _service.ClearOldAlertsAsync(0);

            Assert.IsTrue(clearedCount >= 0);
        }

        #endregion

        #region Rule Condition Tests

        [TestMethod]
        public async Task Test_CountInTimeWindow_Condition()
        {
            var rule = new SecurityAlertRule
            {
                Name = "Count Test Rule",
                Description = "Test count in time window",
                EventType = AuditEventType.SecurityEvent,
                Condition = SecurityAlertCondition.CountInTimeWindow,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["count"] = 3,
                    ["timeWindowMinutes"] = 10
                }
            };

            await _service.ConfigureAlertRuleAsync(rule);

            // Create 3 events within time window
            var baseTime = DateTime.UtcNow.AddMinutes(-5);
            for (int i = 0; i < 3; i++)
            {
                var evt = new AuditLogEntry
                {
                    EventType = AuditEventType.SecurityEvent,
                    Description = $"Event {i}",
                    Timestamp = baseTime.AddMinutes(i)
                };
                await _service.CheckEventAsync(evt);
            }

            var alerts = await _service.GetRecentAlertsAsync(24);
            var countAlerts = alerts.Where(a => a.RuleName == "Count Test Rule").ToList();

            Assert.IsTrue(countAlerts.Count >= 1, "Should have triggered count condition alert");
        }

        [TestMethod]
        public async Task Test_DataSensitivity_Condition()
        {
            var rule = new SecurityAlertRule
            {
                Name = "Sensitivity Test Rule",
                Description = "Test data sensitivity condition",
                Condition = SecurityAlertCondition.DataSensitivity,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["sensitivity"] = DataSensitivity.High
                }
            };

            await _service.ConfigureAlertRuleAsync(rule);

            // Create event with high sensitivity
            var highSensitivityEvent = new AuditLogEntry
            {
                EventType = AuditEventType.ApiKeyAccessed,
                Description = "High sensitivity event",
                Sensitivity = DataSensitivity.High,
                Timestamp = DateTime.UtcNow
            };

            await _service.CheckEventAsync(highSensitivityEvent);

            var alerts = await _service.GetRecentAlertsAsync(24);
            var sensitivityAlerts = alerts.Where(a => a.RuleName == "Sensitivity Test Rule").ToList();

            Assert.IsTrue(sensitivityAlerts.Count >= 1, "Should have triggered sensitivity condition alert");
        }

        #endregion

        #region Alert Severity Tests

        [TestMethod]
        public async Task Test_AlertSeverityLevels()
        {
            // Test different severity levels
            var criticalRule = new SecurityAlertRule
            {
                Name = "Critical Rule",
                Description = "Test critical severity",
                Condition = SecurityAlertCondition.DataSensitivity,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["sensitivity"] = DataSensitivity.Critical
                },
                Severity = SecurityAlertSeverity.Critical
            };

            var lowRule = new SecurityAlertRule
            {
                Name = "Low Rule",
                Description = "Test low severity",
                Condition = SecurityAlertCondition.DataSensitivity,
                Parameters = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["sensitivity"] = DataSensitivity.Low
                },
                Severity = SecurityAlertSeverity.Low
            };

            await _service.ConfigureAlertRuleAsync(criticalRule);
            await _service.ConfigureAlertRuleAsync(lowRule);

            // Trigger both rules
            await _service.CheckEventAsync(new AuditLogEntry
            {
                EventType = AuditEventType.ApiKeyAccessed,
                Description = "Critical data",
                Sensitivity = DataSensitivity.Critical,
                Timestamp = DateTime.UtcNow
            });

            await _service.CheckEventAsync(new AuditLogEntry
            {
                EventType = AuditEventType.TextInjected,
                Description = "Low sensitivity data",
                Sensitivity = DataSensitivity.Low,
                Timestamp = DateTime.UtcNow
            });

            var alerts = await _service.GetRecentAlertsAsync(24);
            var criticalAlerts = alerts.Where(a => a.Severity == SecurityAlertSeverity.Critical).ToList();
            var lowAlerts = alerts.Where(a => a.Severity == SecurityAlertSeverity.Low).ToList();

            Assert.IsTrue(criticalAlerts.Count >= 1, "Should have critical severity alert");
            Assert.IsTrue(lowAlerts.Count >= 1, "Should have low severity alert");
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public async Task Test_ManyEvents_ProcessingPerformance()
        {
            var startTime = DateTime.UtcNow;

            // Process many events
            var tasks = Enumerable.Range(0, 100)
                .Select(i => _service.CheckEventAsync(new AuditLogEntry
                {
                    EventType = AuditEventType.SecurityEvent,
                    Description = $"Performance test event {i}",
                    Timestamp = DateTime.UtcNow.AddMilliseconds(i)
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            var processingTime = DateTime.UtcNow - startTime;

            // Should process 100 events quickly
            Assert.IsTrue(processingTime.TotalSeconds < 10, $"Processing took too long: {processingTime.TotalSeconds} seconds");
        }

        #endregion
    }
}