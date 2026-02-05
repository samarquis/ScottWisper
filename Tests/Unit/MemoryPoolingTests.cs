using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WhisperKey.Services.Memory;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class MemoryPoolingTests
    {
        private ByteArrayPool _pool = null!;

        [TestInitialize]
        public void Setup()
        {
            _pool = new ByteArrayPool();
        }

        [TestMethod]
        public void Rent_ReturnsArrayOfAtLeastMinimumSize()
        {
            var size = 1024;
            var array = _pool.Rent(size);
            Assert.IsNotNull(array);
            Assert.IsTrue(array.Length >= size);
            _pool.Return(array);
        }

        [TestMethod]
        public void Return_AllowsReuseOfArrays()
        {
            var array1 = _pool.Rent(1024);
            _pool.Return(array1);
            
            var array2 = _pool.Rent(1024);
            // ArrayPool might return the same instance if it was just returned
            // but it's not guaranteed. We can at least check RentCount and ReturnCount.
            Assert.AreEqual(2, _pool.RentCount);
            Assert.AreEqual(1, _pool.ReturnCount);
            _pool.Return(array2);
        }

        [TestMethod]
        public void Benchmark_AllocationsWithAndWithoutPooling()
        {
            var iterations = 1000;
            var size = 4096;

            // Measure without pooling
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var allocatedBefore = GC.GetTotalAllocatedBytes(true);
            
            for (int i = 0; i < iterations; i++)
            {
                var array = new byte[size];
                // Use array to prevent optimization
                array[0] = (byte)(i % 256);
            }
            
            var allocatedWithoutPooling = GC.GetTotalAllocatedBytes(true) - allocatedBefore;

            // Measure with pooling
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            allocatedBefore = GC.GetTotalAllocatedBytes(true);
            
            for (int i = 0; i < iterations; i++)
            {
                var array = _pool.Rent(size);
                array[0] = (byte)(i % 256);
                _pool.Return(array);
            }
            
            var allocatedWithPooling = GC.GetTotalAllocatedBytes(true) - allocatedBefore;

            Console.WriteLine($"Allocated without pooling: {allocatedWithoutPooling} bytes");
            Console.WriteLine($"Allocated with pooling: {allocatedWithPooling} bytes");

            // Target is 50% reduction. In this simple loop, it should be much more.
            Assert.IsTrue(allocatedWithPooling < allocatedWithoutPooling / 2, 
                $"Pooling should reduce allocations by at least 50%. Without: {allocatedWithoutPooling}, With: {allocatedWithPooling}");
        }
    }
}
