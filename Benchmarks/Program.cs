using BenchmarkDotNet.Running;
using WhisperKey.Benchmarks;

namespace WhisperKey.Benchmarks
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var summary = BenchmarkRunner.Run<AudioBenchmarks>();
        }
    }
}
