using System;
using System.IO;
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
    public class DeploymentRollbackTests
    {
        private DeploymentRollbackService _service = null!;
        private Mock<IFileSystemService> _mockFileSystem = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private string _testAppData;

        [TestInitialize]
        public void Setup()
        {
            _testAppData = Path.Combine(Path.GetTempPath(), "WhisperKeyTests_" + Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testAppData);
            
            // Mock environment variable for AppData
            Environment.SetEnvironmentVariable("AppData", _testAppData);

            _mockFileSystem = new Mock<IFileSystemService>();
            _mockAuditService = new Mock<IAuditLoggingService>();
            
            _service = new DeploymentRollbackService(
                NullLogger<DeploymentRollbackService>.Instance,
                _mockFileSystem.Object,
                _mockAuditService.Object);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_testAppData))
                Directory.Delete(_testAppData, true);
        }

        [TestMethod]
        public async Task Test_RecordStartupSuccess()
        {
            await _service.RecordStartupSuccessAsync();
            
            var history = await _service.GetHistoryAsync();
            Assert.AreEqual(0, history.ConsecutiveStartupFailures);
            Assert.IsTrue(history.Targets.Any(t => t.IsStable));
            
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SystemEvent,
                It.Is<string>(s => s.Contains("Startup success")),
                null,
                DataSensitivity.Low), Times.Once);
        }

        [TestMethod]
        public async Task Test_RecordStartupFailure_TriggersRollback()
        {
            // 1. Establish a stable version first
            await _service.RecordStartupSuccessAsync();

            // 2. Simulate 3 failures
            await _service.RecordStartupFailureAsync("Crash 1");
            await _service.RecordStartupFailureAsync("Crash 2");
            
            var historyBefore = await _service.GetHistoryAsync();
            Assert.AreEqual(2, historyBefore.ConsecutiveStartupFailures);
            Assert.IsFalse(_service.IsRollbackRequired());

            // 3. Third failure triggers rollback
            await _service.RecordStartupFailureAsync("Crash 3");

            Assert.IsTrue(_service.IsRollbackRequired());
            
            _mockAuditService.Verify(a => a.LogEventAsync(
                AuditEventType.SecurityEvent,
                It.Is<string>(s => s.Contains("AUTOMATED ROLLBACK INITIATED")),
                null,
                DataSensitivity.High), Times.Once);
        }

        [TestMethod]
        public async Task Test_ConfigurationBackup()
        {
            // Create a dummy appsettings.json in current directory for the test
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            File.WriteAllText(configPath, "{ \"test\": true }");

            try
            {
                await _service.CreateConfigurationBackupAsync();
                
                var history = await _service.GetHistoryAsync();
                var target = history.Targets.FirstOrDefault();
                
                Assert.IsNotNull(target);
                Assert.IsNotNull(target.BackupPath);
                Assert.IsTrue(File.Exists(target.BackupPath));
            }
            finally
            {
                if (File.Exists(configPath))
                    File.Delete(configPath);
            }
        }
    }
}
