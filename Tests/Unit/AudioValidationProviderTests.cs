using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Validation;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class AudioValidationProviderTests
    {
        private Mock<ILogger<AudioValidationProvider>> _loggerMock = null!;
        private Mock<IAuditLoggingService> _auditServiceMock = null!;
        private AudioValidationProvider _provider = null!;

        [TestInitialize]
        public void Setup()
        {
            _loggerMock = new Mock<ILogger<AudioValidationProvider>>();
            _auditServiceMock = new Mock<IAuditLoggingService>();
            _provider = new AudioValidationProvider(_loggerMock.Object, _auditServiceMock.Object);
        }

        private byte[] CreateValidWavData(int dataSize = 100)
        {
            using var ms = new MemoryStream();
            using var writer = new BinaryWriter(ms);
            
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // Mono
            writer.Write(16000); // Sample rate
            writer.Write(32000); // Byte rate
            writer.Write((short)2); // Block align
            writer.Write((short)16); // Bits per sample
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);
            writer.Write(new byte[dataSize]);
            
            return ms.ToArray();
        }

        [TestMethod]
        public void ValidateAudioData_ValidWav_ReturnsSuccess()
        {
            var data = CreateValidWavData();
            var result = _provider.ValidateAudioData(data);
            Assert.IsTrue(result.IsValid);
            Assert.AreEqual(0, result.Errors.Count);
        }

        [TestMethod]
        public void ValidateAudioData_NullData_ReturnsFailure()
        {
            var result = _provider.ValidateAudioData(null!);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Contains("Audio data cannot be null"));
        }

        [TestMethod]
        public void ValidateAudioData_EmptyData_ReturnsFailure()
        {
            var result = _provider.ValidateAudioData(new byte[0]);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Contains("Audio data cannot be empty"));
        }

        [TestMethod]
        public void ValidateAudioData_ExceedsSize_ReturnsFailure()
        {
            // 26MB
            var data = new byte[26 * 1024 * 1024];
            var result = _provider.ValidateAudioData(data);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("exceeds maximum allowed size")));
        }

        [TestMethod]
        public void ValidateAudioData_InvalidHeader_ReturnsFailure()
        {
            var data = new byte[100];
            var result = _provider.ValidateAudioData(data);
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("missing RIFF header")));
        }

        [TestMethod]
        public async Task ValidateAudioFileAsync_ValidFile_ReturnsSuccess()
        {
            var path = Path.GetTempFileName() + ".wav";
            File.WriteAllBytes(path, CreateValidWavData());
            
            try
            {
                var result = await _provider.ValidateAudioFileAsync(path);
                Assert.IsTrue(result.IsValid);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestMethod]
        public async Task ValidateAudioFileAsync_NonExistentFile_ReturnsFailure()
        {
            var result = await _provider.ValidateAudioFileAsync("non-existent.wav");
            Assert.IsFalse(result.IsValid);
            Assert.IsTrue(result.Errors.Any(e => e.Contains("not found")));
        }
    }
}
