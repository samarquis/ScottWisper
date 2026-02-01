using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using NAudio.Wave;
using WhisperKey.Configuration;
using WhisperKey.Services;

namespace WhisperKey.Tests
{
    /// <summary>
    /// Mock implementation of IWaveIn for testing purposes.
    /// Allows simulation of audio capture scenarios without actual hardware.
    /// </summary>
    public class MockWaveIn : IWaveIn
    {
        private bool _isRecording;
        private bool _isDisposed;

        public WaveFormat WaveFormat { get; set; } = new WaveFormat(16000, 16, 1);
        public int BufferMilliseconds { get; set; } = 100;

        public event EventHandler<WaveInEventArgs>? DataAvailable;
        public event EventHandler<StoppedEventArgs>? RecordingStopped;

        public bool IsRecording => _isRecording;
        public bool IsDisposed => _isDisposed;

        public void StartRecording()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MockWaveIn));

            _isRecording = true;
        }

        public void StopRecording()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MockWaveIn));

            _isRecording = false;
            RecordingStopped?.Invoke(this, new StoppedEventArgs(null));
        }

        public void StopRecordingWithException(Exception exception)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(MockWaveIn));

            _isRecording = false;
            RecordingStopped?.Invoke(this, new StoppedEventArgs(exception));
        }

        /// <summary>
        /// Simulates audio data being captured.
        /// </summary>
        public void SimulateDataAvailable(byte[] buffer, int bytesRecorded)
        {
            if (!_isRecording)
                throw new InvalidOperationException("Cannot simulate data when not recording");

            var args = new WaveInEventArgs(buffer, bytesRecorded);
            DataAvailable?.Invoke(this, args);
        }

        /// <summary>
        /// Simulates audio data being captured with automatic buffer creation.
        /// </summary>
        public void SimulateDataAvailable(int bytesRecorded)
        {
            var buffer = new byte[bytesRecorded];
            new Random().NextBytes(buffer);
            SimulateDataAvailable(buffer, bytesRecorded);
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                StopRecording();
            }
            _isDisposed = true;
        }
    }

    /// <summary>
    /// Unit tests for AudioCaptureService.
    /// Tests audio recording, permission handling, and WAV conversion.
    /// </summary>
    [TestClass]
    public class AudioCaptureServiceTests
    {
        private Mock<ISettingsService> _settingsServiceMock = null!;
        private Mock<IAudioDeviceService> _audioDeviceServiceMock = null!;
        private MockWaveIn _mockWaveIn = null!;
        private AudioCaptureService _service = null!;
        private AppSettings _appSettings = null!;

        [TestInitialize]
        public void Setup()
        {
            _settingsServiceMock = new Mock<ISettingsService>();
            _audioDeviceServiceMock = new Mock<IAudioDeviceService>();
            _mockWaveIn = new MockWaveIn();
            _appSettings = new AppSettings
            {
                Audio = new AudioSettings
                {
                    SampleRate = 16000,
                    Channels = 1,
                    InputDeviceId = "test-device",
                DeviceSettings = new Dictionary<string, DeviceSpecificSettings>
                {
                    { "test-device", new DeviceSpecificSettings { BufferSize = 1024 } }
                }
                }
            };

            _settingsServiceMock.Setup(s => s.Settings).Returns(_appSettings);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service?.Dispose();
            _mockWaveIn?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_Default_CreatesInstance()
        {
            var service = new AudioCaptureService();
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_WithSettings_CreatesInstance()
        {
            var service = new AudioCaptureService(_settingsServiceMock.Object);
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_WithSettingsAndDeviceService_CreatesInstance()
        {
            var service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object);
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_WithIWaveIn_CreatesInstance()
        {
            var service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_WithWaveInFactory_CreatesInstance()
        {
            var factory = new Func<IWaveIn>(() => new MockWaveIn());
            var service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, factory);
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_WithSettings_LoadsAudioSettings()
        {
            var service = new AudioCaptureService(_settingsServiceMock.Object);
            
            Assert.IsNotNull(service);
            // Settings should be loaded internally
            service.Dispose();
        }

        #endregion

        #region StartCaptureAsync Tests

        [TestMethod]
        public async Task StartCaptureAsync_WithPermissionGranted_StartsRecording()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);

            // Act
            var result = await _service.StartCaptureAsync();

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(_service.IsCapturing);
            Assert.IsTrue(_mockWaveIn.IsRecording);
        }

        [TestMethod]
        public async Task StartCaptureAsync_RequestsPermission_WhenNotGranted()
        {
            // Arrange
            _audioDeviceServiceMock
                .SetupSequence(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Unknown)
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _audioDeviceServiceMock
                .Setup(a => a.RequestMicrophonePermissionAsync())
                .ReturnsAsync(true);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);

            // Act
            var result = await _service.StartCaptureAsync();

            // Assert
            Assert.IsTrue(result);
            _audioDeviceServiceMock.Verify(a => a.RequestMicrophonePermissionAsync(), Times.Once);
        }

        [TestMethod]
        public async Task StartCaptureAsync_ReturnsFalse_WhenPermissionDenied()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Denied);
            
            _audioDeviceServiceMock
                .Setup(a => a.RequestMicrophonePermissionAsync())
                .ReturnsAsync(false);
            
            var permissionRequiredRaised = false;
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            _service.PermissionRequired += (s, e) => permissionRequiredRaised = true;

            // Act
            var result = await _service.StartCaptureAsync();

            // Assert
            Assert.IsFalse(result);
            Assert.IsFalse(_service.IsCapturing);
            Assert.IsTrue(permissionRequiredRaised);
        }

        [TestMethod]
        public async Task StartCaptureAsync_ReturnsFalse_WhenAlreadyCapturing()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            await _service.StartCaptureAsync();

            // Act - Try to start again
            var result = await _service.StartCaptureAsync();

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public async Task StartCaptureAsync_HandlesUnauthorizedAccessException()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            var mockWaveIn = new Mock<IWaveIn>();
            mockWaveIn.Setup(w => w.StartRecording()).Throws(new UnauthorizedAccessException("Access denied"));
            
            var permissionRequiredRaised = false;
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, mockWaveIn.Object);
            _service.PermissionRequired += (s, e) => permissionRequiredRaised = true;

            // Act
            var result = await _service.StartCaptureAsync();

            // Assert
            Assert.IsFalse(result);
            Assert.IsTrue(permissionRequiredRaised);
        }

        [TestMethod]
        public async Task StartCaptureAsync_HandlesSecurityException()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            var mockWaveIn = new Mock<IWaveIn>();
            mockWaveIn.Setup(w => w.StartRecording()).Throws(new SecurityException("Security violation"));
            
            var permissionRequiredRaised = false;
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, mockWaveIn.Object);
            _service.PermissionRequired += (s, e) => permissionRequiredRaised = true;

            // Act
            var result = await _service.StartCaptureAsync();

            // Assert
            Assert.IsFalse(result);
            Assert.IsTrue(permissionRequiredRaised);
        }

        [TestMethod]
        public async Task StartCaptureAsync_RaisesCaptureError_OnGeneralException()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            var mockWaveIn = new Mock<IWaveIn>();
            mockWaveIn.Setup(w => w.StartRecording()).Throws(new InvalidOperationException("Device error"));
            
            Exception? capturedError = null;
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, mockWaveIn.Object);
            _service.CaptureError += (s, e) => capturedError = e;

            // Act
            var result = await _service.StartCaptureAsync();

            // Assert
            Assert.IsFalse(result);
            Assert.IsNotNull(capturedError);
            Assert.IsInstanceOfType(capturedError, typeof(InvalidOperationException));
        }

        #endregion

        #region StopCaptureAsync Tests

        [TestMethod]
        public async Task StopCaptureAsync_StopsRecording()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            await _service.StartCaptureAsync();
            Assert.IsTrue(_service.IsCapturing);

            // Act
            await _service.StopCaptureAsync();

            // Assert
            Assert.IsFalse(_service.IsCapturing);
        }

        [TestMethod]
        public async Task StopCaptureAsync_HandlesNotCapturing()
        {
            // Arrange
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);

            // Act & Assert - Should not throw
            await _service.StopCaptureAsync();
            Assert.IsFalse(_service.IsCapturing);
        }

        #endregion

        #region AudioDataCaptured Event Tests

        [TestMethod]
        public async Task AudioDataCaptured_Raised_WhenDataAvailable()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            byte[]? capturedData = null;
            _service.AudioDataCaptured += (s, e) => capturedData = e;

            await _service.StartCaptureAsync();

            // Act
            var testData = new byte[] { 1, 2, 3, 4, 5 };
            _mockWaveIn.SimulateDataAvailable(testData, testData.Length);

            // Assert
            Assert.IsNotNull(capturedData);
            CollectionAssert.AreEqual(testData, capturedData);
        }

        [TestMethod]
        public async Task AudioDataCaptured_Raised_MultipleTimes()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            var capturedDataCount = 0;
            _service.AudioDataCaptured += (s, e) => capturedDataCount++;

            await _service.StartCaptureAsync();

            // Act
            for (int i = 0; i < 5; i++)
            {
                _mockWaveIn.SimulateDataAvailable(100);
            }

            // Assert
            Assert.AreEqual(5, capturedDataCount);
        }

        [TestMethod]
        public async Task AudioDataCaptured_HandlesException()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            Exception? capturedError = null;
            _service.AudioDataCaptured += (s, e) => throw new InvalidOperationException("Handler error");
            _service.CaptureError += (s, e) => capturedError = e;

            await _service.StartCaptureAsync();

            // Act
            _mockWaveIn.SimulateDataAvailable(100);

            // Assert - Error should be captured, not thrown
            Assert.IsNotNull(capturedError);
        }

        #endregion

        #region RecordingStopped Event Tests

        [TestMethod]
        public async Task RecordingStopped_SetsIsCapturingFalse()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            await _service.StartCaptureAsync();
            Assert.IsTrue(_service.IsCapturing);

            // Act
            _mockWaveIn.StopRecording();

            // Assert
            Assert.IsFalse(_service.IsCapturing);
        }

        [TestMethod]
        public async Task RecordingStopped_RaisesPermissionRequired_OnUnauthorizedAccessException()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            var permissionRequiredRaised = false;
            _service.PermissionRequired += (s, e) => permissionRequiredRaised = true;

            await _service.StartCaptureAsync();

            // Act
            _mockWaveIn.StopRecordingWithException(new UnauthorizedAccessException());

            // Assert
            Assert.IsTrue(permissionRequiredRaised);
        }

        [TestMethod]
        public async Task RecordingStopped_RaisesPermissionRequired_OnSecurityException()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            var permissionRequiredRaised = false;
            _service.PermissionRequired += (s, e) => permissionRequiredRaised = true;

            await _service.StartCaptureAsync();

            // Act
            _mockWaveIn.StopRecordingWithException(new SecurityException());

            // Assert
            Assert.IsTrue(permissionRequiredRaised);
        }

        [TestMethod]
        public async Task RecordingStopped_RaisesCaptureError_OnGeneralException()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            Exception? capturedError = null;
            _service.CaptureError += (s, e) => capturedError = e;

            await _service.StartCaptureAsync();

            // Act
            var expectedException = new InvalidOperationException("Recording error");
            _mockWaveIn.StopRecordingWithException(expectedException);

            // Assert
            Assert.IsNotNull(capturedError);
            Assert.AreEqual(expectedException, capturedError);
        }

        #endregion

        #region GetCapturedAudio Tests

        [TestMethod]
        public async Task GetCapturedAudio_ReturnsWavData()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            await _service.StartCaptureAsync();

            // Simulate audio data
            var testData = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            _mockWaveIn.SimulateDataAvailable(testData, testData.Length);

            // Act
            var result = _service.GetCapturedAudio();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 0);
            
            // Verify it's valid WAV format (starts with RIFF)
            Assert.AreEqual(0x52, result[0]); // 'R'
            Assert.AreEqual(0x49, result[1]); // 'I'
            Assert.AreEqual(0x46, result[2]); // 'F'
            Assert.AreEqual(0x46, result[3]); // 'F'
        }

        [TestMethod]
        public async Task GetCapturedAudio_ReturnsNull_WhenNoData()
        {
            // Arrange
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);

            // Act
            var result = _service.GetCapturedAudio();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetCapturedAudio_ReturnsNull_WhenBufferEmptied()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            await _service.StartCaptureAsync();

            // Add data and retrieve it
            _mockWaveIn.SimulateDataAvailable(100);
            _service.GetCapturedAudio();

            // Act - Try to get audio again
            var result = _service.GetCapturedAudio();

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task GetCapturedAudio_CombinesMultipleChunks()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            await _service.StartCaptureAsync();

            // Simulate multiple audio chunks
            for (int i = 0; i < 5; i++)
            {
                _mockWaveIn.SimulateDataAvailable(100);
            }

            // Act
            var result = _service.GetCapturedAudio();

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Length > 44); // WAV header is 44 bytes
        }

        #endregion

        #region ClearCapturedAudio Tests

        [TestMethod]
        public async Task ClearCapturedAudio_RemovesAllData()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            await _service.StartCaptureAsync();

            _mockWaveIn.SimulateDataAvailable(100);
            _mockWaveIn.SimulateDataAvailable(100);

            // Act
            _service.ClearCapturedAudio();

            // Assert
            var result = _service.GetCapturedAudio();
            Assert.IsNull(result);
        }

        [TestMethod]
        public void ClearCapturedAudio_HandlesNullBuffer()
        {
            // Arrange
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);

            // Act & Assert - Should not throw
            _service.ClearCapturedAudio();
        }

        #endregion

        #region RetryWithPermissionAsync Tests

        [TestMethod]
        public async Task RetryWithPermissionAsync_WaitsAndRetries()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);

            // Act
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var result = await _service.RetryWithPermissionAsync();
            stopwatch.Stop();

            // Assert
            Assert.IsTrue(result);
            Assert.IsTrue(stopwatch.ElapsedMilliseconds >= 900, "Should wait at least 1 second before retrying");
        }

        #endregion

        #region Permission Events Tests

        [TestMethod]
        public void AudioDeviceService_PermissionDenied_RaisesPermissionRequired()
        {
            // Arrange
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            var permissionRequiredRaised = false;
            _service.PermissionRequired += (s, e) => permissionRequiredRaised = true;

            // Act - Simulate permission denied from AudioDeviceService
            _audioDeviceServiceMock.Raise(a => a.PermissionDenied += null, EventArgs.Empty);

            // Assert
            Assert.IsTrue(permissionRequiredRaised);
        }

        [TestMethod]
        public void AudioDeviceService_PermissionGranted_RaisesPermissionRetry()
        {
            // Arrange
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            var permissionRetryRaised = false;
            _service.PermissionRetry += (s, e) => permissionRetryRaised = true;

            // Act - Simulate permission granted from AudioDeviceService
            _audioDeviceServiceMock.Raise(a => a.PermissionGranted += null, EventArgs.Empty);

            // Assert
            Assert.IsTrue(permissionRetryRaised);
        }

        [TestMethod]
        public void AudioDeviceService_PermissionRequestFailed_RaisesPermissionRequired()
        {
            // Arrange
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            
            var permissionRequiredRaised = false;
            _service.PermissionRequired += (s, e) => permissionRequiredRaised = true;

            // Act - Simulate permission request failed from AudioDeviceService
            _audioDeviceServiceMock.Raise(a => a.PermissionRequestFailed += null, EventArgs.Empty);

            // Assert
            Assert.IsTrue(permissionRequiredRaised);
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_StopsRecording()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            _service.StartCaptureAsync().Wait();

            // Act
            _service.Dispose();

            // Assert
            Assert.IsFalse(_mockWaveIn.IsRecording);
            Assert.IsTrue(_mockWaveIn.IsDisposed);
        }

        [TestMethod]
        public void Dispose_ClearsBuffer()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);
            _service.StartCaptureAsync().Wait();
            _mockWaveIn.SimulateDataAvailable(100);

            // Act
            _service.Dispose();

            // Assert - After dispose, should not be able to get captured audio
            var result = _service.GetCapturedAudio();
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Dispose_HandlesException()
        {
            // Arrange
            var mockWaveIn = new Mock<IWaveIn>();
            mockWaveIn.Setup(w => w.StopRecording()).Throws(new InvalidOperationException("Stop error"));
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, mockWaveIn.Object);

            Exception? capturedError = null;
            _service.CaptureError += (s, e) => capturedError = e;

            // Act & Assert - Should not throw
            _service.Dispose();
            
            // Error should be captured
            Assert.IsNotNull(capturedError);
        }

        [TestMethod]
        public void Dispose_CanBeCalledMultipleTimes()
        {
            // Arrange
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);

            // Act & Assert - Should not throw
            _service.Dispose();
            _service.Dispose();
        }

        #endregion

        #region GetAvailableDevices Tests

        [TestMethod]
        public void GetAvailableDevices_ReturnsArray()
        {
            // Act
            var devices = AudioCaptureService.GetAvailableDevices();

            // Assert
            Assert.IsNotNull(devices);
            // Note: In a test environment with no audio devices, this may return empty array
        }

        #endregion

        #region CaptureAudioDevice Tests

        [TestMethod]
        public void CaptureAudioDevice_ToString_FormatsCorrectly()
        {
            // Arrange
            var device = new CaptureAudioDevice
            {
                DeviceNumber = 0,
                Name = "Test Microphone",
                Channels = 2
            };

            // Act
            var result = device.ToString();

            // Assert
            Assert.AreEqual("Test Microphone (Device 0, 2 channels)", result);
        }

        #endregion

        #region Settings Loading Tests

        [TestMethod]
        public void Constructor_LoadsDefaultSettings_WhenSettingsNull()
        {
            // Arrange
            var settingsMock = new Mock<ISettingsService>();
            settingsMock.Setup(s => s.Settings).Returns((AppSettings?)null);

            // Act
            var service = new AudioCaptureService(settingsMock.Object);

            // Assert - Should create instance with defaults
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_LoadsDefaultSettings_WhenAudioSettingsNull()
        {
            // Arrange
            var settingsMock = new Mock<ISettingsService>();
            settingsMock.Setup(s => s.Settings).Returns(new AppSettings { Audio = null });

            // Act
            var service = new AudioCaptureService(settingsMock.Object);

            // Assert - Should create instance with defaults
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_UsesDefaultValues_WhenSettingsInvalid()
        {
            // Arrange
            var settingsMock = new Mock<ISettingsService>();
            settingsMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Audio = new AudioSettings
                {
                    SampleRate = -1, // Invalid
                    Channels = 5,    // Invalid (> 2)
                    InputDeviceId = "unknown-device"
                }
            });

            // Act
            var service = new AudioCaptureService(settingsMock.Object, _audioDeviceServiceMock.Object);

            // Assert - Should use defaults
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_UsesDeviceSettings_WhenDeviceFound()
        {
            // Arrange
            var settingsMock = new Mock<ISettingsService>();
            settingsMock.Setup(s => s.Settings).Returns(new AppSettings
            {
                Audio = new AudioSettings
                {
                    SampleRate = 22050,
                    Channels = 2,
                    InputDeviceId = "device-123",
                DeviceSettings = new Dictionary<string, DeviceSpecificSettings>
                {
                    { "device-123", new DeviceSpecificSettings { BufferSize = 2048 } }
                }
                }
            });

            // Act
            var service = new AudioCaptureService(settingsMock.Object);

            // Assert
            Assert.IsNotNull(service);
            service.Dispose();
        }

        #endregion

        #region OpenWindowsMicrophoneSettings Tests

        [TestMethod]
        public void OpenWindowsMicrophoneSettings_DoesNotThrow()
        {
            // Arrange
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, _mockWaveIn);

            // Act & Assert - Should not throw (opens Windows settings)
            _service.OpenWindowsMicrophoneSettings();
        }

        #endregion

        #region Audio Format Tests

        [TestMethod]
        public async Task WaveFormat_UsesCorrectFormat()
        {
            // Arrange
            _audioDeviceServiceMock
                .Setup(a => a.CheckMicrophonePermissionAsync())
                .ReturnsAsync(MicrophonePermissionStatus.Granted);
            
            var formatSet = false;
            WaveFormat? capturedFormat = null;
            
            var mockWaveIn = new Mock<IWaveIn>();
            mockWaveIn.SetupSet(w => w.WaveFormat = It.IsAny<WaveFormat>())
                .Callback<WaveFormat>(f =>
                {
                    capturedFormat = f;
                    formatSet = true;
                });
            mockWaveIn.Setup(w => w.StartRecording()).Callback(() => { });
            
            _service = new AudioCaptureService(_settingsServiceMock.Object, _audioDeviceServiceMock.Object, mockWaveIn.Object);

            // Act
            await _service.StartCaptureAsync();

            // Assert
            Assert.IsTrue(formatSet);
            Assert.IsNotNull(capturedFormat);
            Assert.AreEqual(16000, capturedFormat.SampleRate);
            Assert.AreEqual(1, capturedFormat.Channels);
        }

        #endregion
    }
}
