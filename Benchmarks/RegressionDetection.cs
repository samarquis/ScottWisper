using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace WhisperKey.Benchmarks
{
    public class PerformanceBaseline
    {
        public string BenchmarkName { get; set; } = string.Empty;
        public double MeanTimeNs { get; set; }
        public long AllocatedBytes { get; set; }
        public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    }

    public class RegressionDetector
    {
        private const double ThresholdMultiplier = 1.15; // 10% threshold

        public static bool CheckForRegression(PerformanceBaseline current, PerformanceBaseline baseline)
        {
            if (current.MeanTimeNs > baseline.MeanTimeNs * ThresholdMultiplier)
            {
                Console.WriteLine($"REGRESSION DETECTED: {current.BenchmarkName}");
                Console.WriteLine($"Baseline: {baseline.MeanTimeNs:F2}ns, Current: {current.MeanTimeNs:F2}ns");
                return true;
            }

            if (current.AllocatedBytes > baseline.AllocatedBytes * ThresholdMultiplier)
            {
                Console.WriteLine($"ALLOCATION REGRESSION: {current.BenchmarkName}");
                Console.WriteLine($"Baseline: {baseline.AllocatedBytes} bytes, Current: {current.AllocatedBytes} bytes");
                return true;
            }

            return false;
        }
    }
}
