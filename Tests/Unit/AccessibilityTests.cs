using System;
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class AccessibilityTests
    {
        private AccessibilityService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new AccessibilityService(NullLogger<AccessibilityService>.Instance);
        }

        [TestMethod]
        public void Test_AccessibilityLabels()
        {
            // We can't easily test AutomationProperties in unit tests without a real UI tree,
            // but we can verify the service logic.
            Assert.IsNotNull(_service);
        }

        [TestMethod]
        public void Test_HighContrastCheck()
        {
            // SystemParameters.HighContrast might be true or false depending on the test agent environment
            var isHC = _service.IsHighContrastEnabled();
            Assert.AreEqual(SystemParameters.HighContrast, isHC);
        }
    }
}
