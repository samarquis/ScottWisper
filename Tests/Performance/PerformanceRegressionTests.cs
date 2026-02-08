using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Benchmarks;

namespace WhisperKey.Tests.Performance
{
    [TestClass]
    public class PerformanceRegressionTests
    {
        [TestMethod]
        [TestCategory("Performance")]
        public void Verify_RegressionDetector_Logic()
        {
            var baseline = new PerformanceBaseline
            {
                BenchmarkName = "Test",
                MeanTimeNs = 100,
                AllocatedBytes = 1024
            };

            var currentOk = new PerformanceBaseline
            {
                BenchmarkName = "Test",
                MeanTimeNs = 105, // 5% increase (within 10% threshold)
                AllocatedBytes = 1024
            };

            var currentRegressed = new PerformanceBaseline
            {
                BenchmarkName = "Test",
                MeanTimeNs = 120, // 20% increase (exceeds threshold)
                AllocatedBytes = 1024
            };

            Assert.IsFalse(RegressionDetector.CheckForRegression(currentOk, baseline), "Should not detect regression within threshold");
            Assert.IsTrue(RegressionDetector.CheckForRegression(currentRegressed, baseline), "Should detect regression exceeding threshold");
        }
    }
}
