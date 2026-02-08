using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class AudioCaptureRateLimitingTests
    {
        private Mock<ISettingsService> _settingsServiceMock = null!;
        private Mock<IAudioDeviceService> _audioDeviceServiceMock = null!;
        private Mock<IWaveIn> _waveInMock = null!;
        private AppSettings _settings = null!;

        [TestInitialize]
        public void Setup()
        {
            _settingsServiceMock = new Mock<ISettingsService>();
            _audioDeviceServiceMock = new Mock<IAudioDeviceService>();
            _waveInMock = new Mock<IWaveIn>();
            _settings = new AppSettings();
            
            _settingsServiceMock.Setup(s => s.Settings).Returns(_settings);
            
            // Setup default behavior for StartCapture dependencies
            _audioDeviceServiceMock.Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
        }

        [TestMethod]
        public async Task StartCaptureAsync_WhenRateLimitExceeded_ReturnsFalse()
        {
            // Arrange
            _settings.Audio.EnableCaptureRateLimiting = true;
            _settings.Audio.MaxCapturesPerMinute = 1;
            
            var service = new AudioCaptureService(
                _settingsServiceMock.Object, 
                _audioDeviceServiceMock.Object, 
                _waveInMock.Object,
                null,
                NullLogger<AudioCaptureService>.Instance);

            // Act - First attempt should succeed
            var result1 = await service.StartCaptureAsync();
            await service.StopCaptureAsync();

            // Act - Second attempt within the same minute should fail
            var result2 = await service.StartCaptureAsync();

            // Assert
            Assert.IsTrue(result1, "First capture attempt should succeed");
            Assert.IsFalse(result2, "Second capture attempt should fail due to rate limit");
        }

        [TestMethod]
        public async Task StartCaptureAsync_WhenRateLimitDisabled_SucceedsMultipleTimes()
        {
            // Arrange
            _settings.Audio.EnableCaptureRateLimiting = false;
            _settings.Audio.MaxCapturesPerMinute = 1;
            
            var service = new AudioCaptureService(
                _settingsServiceMock.Object, 
                _audioDeviceServiceMock.Object, 
                _waveInMock.Object,
                null,
                NullLogger<AudioCaptureService>.Instance);

            // Act
            var result1 = await service.StartCaptureAsync();
            await service.StopCaptureAsync();
            var result2 = await service.StartCaptureAsync();
            await service.StopCaptureAsync();

            // Assert
            Assert.IsTrue(result1);
            Assert.IsTrue(result2);
        }
    }
}
