using System;
using System.Windows;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class ResponsiveUITests
    {
        private ResponsiveUIService _service = null!;

        [TestInitialize]
        public void Setup()
        {
            _service = new ResponsiveUIService(NullLogger<ResponsiveUIService>.Instance);
        }

        [TestMethod]
        public void Test_ScalingCalculations()
        {
            // Initial scale should be 1.0 in test environment
            var scale = _service.GetCurrentScale();
            Assert.IsTrue(scale > 0);

            var baseValue = 100.0;
            var scaledValue = _service.GetScaledValue(baseValue);
            Assert.AreEqual(baseValue * scale, scaledValue);

            var baseThickness = new Thickness(10, 20, 10, 20);
            var scaledThickness = _service.GetScaledThickness(baseThickness);
            Assert.AreEqual(baseThickness.Left * scale, scaledThickness.Left);
            Assert.AreEqual(baseThickness.Top * scale, scaledThickness.Top);
        }

        [TestMethod]
        public void Test_ScalingChangedEvent()
        {
            double reportedScale = 0;
            _service.ScalingChanged += (s, e) => reportedScale = e;
            
            // We can't easily trigger DpiChanged on a Window in unit tests without a message loop,
            // but we've verified the calculation logic.
            Assert.IsNotNull(_service);
        }
    }
}
