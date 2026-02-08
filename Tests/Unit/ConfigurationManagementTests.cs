using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class ConfigurationManagementTests
    {
        private ConfigurationManagementService _service = null!;
        private Mock<IFileSystemService> _mockFileSystem = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private Mock<ISettingsService> _mockSettingsService = null!;
        private AppSettings _settings = new();

        [TestInitialize]
        public void Setup()
        {
            _mockFileSystem = new Mock<IFileSystemService>();
            _mockAuditService = new Mock<IAuditLoggingService>();
            _mockSettingsService = new Mock<ISettingsService>();
            _settings = new AppSettings();

            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);
            
            _service = new ConfigurationManagementService(
                NullLogger<ConfigurationManagementService>.Instance,
                _mockFileSystem.Object,
                _mockAuditService.Object,
                _mockSettingsService.Object);
        }

        [TestMethod]
        public async Task Test_CaptureSnapshot()
        {
            _settings.Transcription.Model = "test-model";
            
            var snapshot = await _service.CaptureSnapshotAsync();
            
            Assert.IsNotNull(snapshot);
            Assert.IsTrue(snapshot.Settings.ContainsKey("Transcription.Model"));
            Assert.AreEqual("test-model", snapshot.Settings["Transcription.Model"]);
            Assert.IsFalse(string.IsNullOrEmpty(snapshot.ConfigHash));
        }

        [TestMethod]
        public async Task Test_DriftDetection()
        {
            // 1. Set baseline
            _settings.Transcription.Model = "baseline-model";
            await _service.SetBaselineAsync();

            // 2. Modify settings (create drift)
            _settings.Transcription.Model = "drifted-model";

            // 3. Validate parity
            var report = await _service.ValidateParityAsync();

            Assert.IsTrue(report.HasDrift);
            Assert.AreEqual(1, report.Differences.Count);
            Assert.AreEqual("Transcription.Model", report.Differences[0].SettingKey);
            Assert.AreEqual("baseline-model", report.Differences[0].ExpectedValue);
            Assert.AreEqual("drifted-model", report.Differences[0].ActualValue);

            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SystemEvent,
                It.Is<string>(s => s.Contains("[CONFIG DRIFT]")),
                It.IsAny<string>(),
                DataSensitivity.Medium), Times.Once);
        }

        [TestMethod]
        public async Task Test_TrackChange()
        {
            await _service.TrackChangeAsync("Key", "Old", "New");
            
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SettingsChanged,
                It.Is<string>(s => s.Contains("Key") && s.Contains("Old") && s.Contains("New")),
                null,
                DataSensitivity.Low), Times.Once);
        }
    }
}
