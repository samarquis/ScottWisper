using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Exceptions;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class ExceptionHierarchyTests
    {
        [TestMethod]
        public void Test_TranscriptionException_Chaining()
        {
            var inner = new Exception("Root cause");
            var ex = new TranscriptionException("Failed", "TRANS_001", inner);
            
            Assert.AreEqual("Failed", ex.Message);
            Assert.AreEqual("TRANS_001", ex.ErrorCode);
            Assert.AreSame(inner, ex.InnerException);
        }

        [TestMethod]
        public void Test_AudioCaptureException_Properties()
        {
            var ex = new AudioCaptureException("No mic", "dev-123", "DEVICE_ERROR");
            
            Assert.AreEqual("dev-123", ex.DeviceId);
            Assert.AreEqual("DEVICE_ERROR", ex.ErrorCode);
        }

        [TestMethod]
        public void Test_ConfigurationDriftException()
        {
            var ex = new ConfigurationDriftException(5, "Drift detected");
            
            Assert.AreEqual(5, ex.DriftCount);
            Assert.AreEqual("CONFIG_DRIFT", ex.ErrorCode);
        }

        [TestMethod]
        public void Test_WindowNotFoundException()
        {
            var ex = new WindowNotFoundException("Notepad");
            
            Assert.IsTrue(ex.Message.Contains("Notepad"));
            Assert.AreEqual("INJECTION_ERROR", ex.ErrorCode);
        }
    }
}
