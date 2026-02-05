using System;
using System.Buffers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using WhisperKey.Services.Memory;

namespace WhisperKey.Benchmarks
{
    [MemoryDiagnoser]
    public class AudioBenchmarks
    {
        private ByteArrayPool _customPool = null!;
        private byte[] _data = null!;
        private const int BufferSize = 4096;

        [GlobalSetup]
        public void Setup()
        {
            _customPool = new ByteArrayPool();
            _data = new byte[BufferSize];
            new Random(42).NextBytes(_data);
        }

        [Benchmark(Baseline = true)]
        public byte[] Allocation_NoPooling()
        {
            var buffer = new byte[BufferSize];
            Array.Copy(_data, buffer, BufferSize);
            return buffer;
        }

        [Benchmark]
        public byte[] Custom_ByteArrayPool()
        {
            var buffer = _customPool.Rent(BufferSize);
            Array.Copy(_data, buffer, BufferSize);
            _customPool.Return(buffer);
            return buffer;
        }

        [Benchmark]
        public byte[] System_ArrayPool()
        {
            var buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
            Array.Copy(_data, buffer, BufferSize);
            ArrayPool<byte>.Shared.Return(buffer);
            return buffer;
        }
    }
}
