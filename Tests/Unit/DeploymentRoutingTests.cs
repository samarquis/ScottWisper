using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Moq.Protected;
using WhisperKey.Configuration;
using WhisperKey.Models;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class DeploymentRoutingTests
    {
        private DeploymentRoutingService _service = null!;
        private Mock<ISettingsService> _mockSettingsService = null!;
        private Mock<IHttpClientFactory> _mockHttpFactory = null!;
        private Mock<IAuditLoggingService> _mockAuditService = null!;
        private AppSettings _settings = new();

        [TestInitialize]
        public void Setup()
        {
            _mockSettingsService = new Mock<ISettingsService>();
            _mockHttpFactory = new Mock<IHttpClientFactory>();
            _mockAuditService = new Mock<IAuditLoggingService>();
            _settings = new AppSettings();

            _mockSettingsService.Setup(s => s.Settings).Returns(_settings);

            _service = new DeploymentRoutingService(
                NullLogger<DeploymentRoutingService>.Instance,
                _mockSettingsService.Object,
                _mockHttpFactory.Object,
                _mockAuditService.Object);
        }

        [TestMethod]
        public async Task Test_SwitchEnvironment()
        {
            var success = await _service.SwitchActiveEnvironmentAsync("Green");
            
            Assert.IsTrue(success);
            var active = await _service.GetActiveEnvironmentAsync();
            Assert.AreEqual("Green", active.Name);
            Assert.AreEqual("https://staging.whisperkey.com/v1", _settings.Transcription.ApiEndpoint);
            
            _mockSettingsService.Verify(s => s.SaveAsync(), Times.Once);
        }

        [TestMethod]
        public async Task Test_InstantRollback()
        {
            await _service.SwitchActiveEnvironmentAsync("Green");
            var success = await _service.InitiateInstantRollbackAsync();
            
            Assert.IsTrue(success);
            var active = await _service.GetActiveEnvironmentAsync();
            Assert.AreEqual("Blue", active.Name);
        }

        [TestMethod]
        public async Task Test_HealthCheck_AutoSwitch()
        {
            // Setup mock HttpClient to fail for Blue and succeed for Green
            var handlerMock = new Mock<HttpMessageHandler>();
            
            // Blue health check (fails)
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("api.whisperkey.com")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.InternalServerError));

            // Green health check (succeeds)
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", 
                    ItExpr.Is<HttpRequestMessage>(r => r.RequestUri!.ToString().Contains("staging.whisperkey.com")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK));

            _mockHttpFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(handlerMock.Object));

            // Run health checks
            await _service.PerformHealthChecksAsync();

            // Should have auto-switched to Green
            var active = await _service.GetActiveEnvironmentAsync();
            Assert.AreEqual("Green", active.Name);
        }
    }
}
