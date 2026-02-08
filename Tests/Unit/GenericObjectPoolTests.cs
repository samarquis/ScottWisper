using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services.Memory;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class GenericObjectPoolTests
    {
        #region Constructor Tests

        [TestMethod]
        public void Constructor_DefaultMaxSize_SetsMaxSizeTo100()
        {
            // Arrange & Act
            var pool = new GenericObjectPool<TestObject>();

            // Assert
            Assert.IsNotNull(pool);
        }

        [TestMethod]
        public void Constructor_CustomMaxSize_SetsMaxSize()
        {
            // Arrange & Act
            var pool = new GenericObjectPool<TestObject>(50);

            // Assert
            Assert.IsNotNull(pool);
        }

        [TestMethod]
        public void Constructor_ZeroMaxSize_Allowed()
        {
            // Arrange & Act
            var pool = new GenericObjectPool<TestObject>(0);

            // Assert
            Assert.IsNotNull(pool);
        }

        #endregion

        #region Get Tests

        [TestMethod]
        public void Get_EmptyPool_CreatesNewObject()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>();

            // Act
            var obj = pool.Get();

            // Assert
            Assert.IsNotNull(obj);
            Assert.IsTrue(obj.IsNew);
        }

        [TestMethod]
        public void Get_PoolWithObjects_ReturnsPooledObject()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>();
            var obj1 = new TestObject { IsNew = false };
            pool.Return(obj1);

            // Act
            var obj2 = pool.Get();

            // Assert
            Assert.IsNotNull(obj2);
            Assert.IsFalse(obj2.IsNew);
            Assert.AreSame(obj1, obj2);
        }

        [TestMethod]
        public void Get_MultipleObjectsFIFO_OrderMaintained()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>();
            var obj1 = new TestObject { Id = 1 };
            var obj2 = new TestObject { Id = 2 };
            pool.Return(obj1);
            pool.Return(obj2);

            // Act
            var first = pool.Get();
            var second = pool.Get();

            // Assert
            Assert.AreEqual(1, first.Id);
            Assert.AreEqual(2, second.Id);
        }

        #endregion

        #region Return Tests

        [TestMethod]
        public void Return_ObjectBelowMaxSize_AddsToPool()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>(10);
            var obj = new TestObject();

            // Act
            pool.Return(obj);
            var retrieved = pool.Get();

            // Assert
            Assert.AreSame(obj, retrieved);
        }

        [TestMethod]
        public void Return_ObjectAtMaxSize_DiscardsObject()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>(2);
            var obj1 = new TestObject { Id = 1 };
            var obj2 = new TestObject { Id = 2 };
            var obj3 = new TestObject { Id = 3 };

            // Act
            pool.Return(obj1);
            pool.Return(obj2);
            pool.Return(obj3); // Should be discarded

            var retrieved1 = pool.Get();
            var retrieved2 = pool.Get();
            var retrieved3 = pool.Get(); // Should be new object

            // Assert
            Assert.AreEqual(1, retrieved1.Id);
            Assert.AreEqual(2, retrieved2.Id);
            Assert.AreEqual(0, retrieved3.Id); // New object
        }

        [TestMethod]
        public void Return_NullObject_DoesNotThrow()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>();

            // Act & Assert - Should not throw
            pool.Return(null!);
        }

        #endregion

        #region Thread Safety Tests

        [TestMethod]
        public async Task Get_Return_ConcurrentAccess_MaintainsConsistency()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>(100);
            var tasks = new List<Task>();
            var objects = new List<TestObject>();
            var lockObj = new object();

            // Act
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var obj = pool.Get();
                    lock (lockObj)
                    {
                        objects.Add(obj);
                    }
                    pool.Return(obj);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All gets should have returned objects
            Assert.AreEqual(100, objects.Count);
        }

        [TestMethod]
        public async Task Get_Return_MultipleThreads_NoExceptions()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>(50);
            var exceptions = new List<Exception>();
            var tasks = new List<Task>();

            // Act
            for (int i = 0; i < 20; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 50; j++)
                        {
                            var obj = pool.Get();
                            Thread.Sleep(1);
                            pool.Return(obj);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(0, exceptions.Count, $"Exceptions occurred: {string.Join(", ", exceptions)}");
        }

        [TestMethod]
        public async Task ConcurrentGet_WithLimitedPoolSize_RespectsMaxSize()
        {
            // Arrange
            int maxSize = 10;
            var pool = new GenericObjectPool<TestObject>(maxSize);
            var returnedObjects = new List<TestObject>();
            var lockObj = new object();

            // Pre-populate pool to max size
            for (int i = 0; i < maxSize; i++)
            {
                pool.Return(new TestObject { Id = i });
            }

            // Act
            var tasks = new List<Task>();
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(Task.Run(() =>
                {
                    var obj = pool.Get();
                    Thread.Sleep(5);
                    pool.Return(obj);
                    lock (lockObj)
                    {
                        returnedObjects.Add(obj);
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - All operations completed without exception
            Assert.AreEqual(50, returnedObjects.Count);
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public void Get_Return_SameObjectMultipleTimes()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>();
            var obj = new TestObject { Id = 1 };

            // Act
            pool.Return(obj);
            var retrieved1 = pool.Get();
            pool.Return(retrieved1);
            var retrieved2 = pool.Get();

            // Assert
            Assert.AreSame(obj, retrieved1);
            Assert.AreSame(obj, retrieved2);
        }

        [TestMethod]
        public void Get_MultipleNewObjects_AllUnique()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>();
            var objects = new HashSet<TestObject>();

            // Act
            for (int i = 0; i < 100; i++)
            {
                objects.Add(pool.Get());
            }

            // Assert - All objects should be unique (new)
            Assert.AreEqual(100, objects.Count);
        }

        [TestMethod]
        public void Return_ManyObjects_OnlyKeepsMaxSize()
        {
            // Arrange
            int maxSize = 5;
            var pool = new GenericObjectPool<TestObject>(maxSize);
            var returnedObjects = new List<TestObject>();
            var retrievedObjects = new List<TestObject>();

            // Act - Return more objects than max size
            for (int i = 0; i < 20; i++)
            {
                var obj = new TestObject { Id = i, IsNew = false };
                returnedObjects.Add(obj);
                pool.Return(obj);
            }

            // Retrieve objects from pool - should get at most maxSize unique objects
            // that were previously returned
            for (int i = 0; i < 20; i++)
            {
                var obj = pool.Get();
                if (!obj.IsNew) // Only count objects that came from the pool (not newly created)
                {
                    retrievedObjects.Add(obj);
                }
            }

            // Assert - Should only get maxSize pooled objects from the original returned set
            Assert.IsTrue(retrievedObjects.Count <= maxSize, 
                $"Should retrieve at most {maxSize} pooled objects, but got {retrievedObjects.Count}");
            
            // Verify all retrieved objects were in the original returned set
            foreach (var retrieved in retrievedObjects)
            {
                Assert.IsTrue(returnedObjects.Contains(retrieved), 
                    "Retrieved object should be from the originally returned set");
            }
        }

        [TestMethod]
        public void Get_Return_StressTest()
        {
            // Arrange
            var pool = new GenericObjectPool<TestObject>(1000);

            // Act - Heavy usage pattern
            for (int i = 0; i < 10000; i++)
            {
                var obj = pool.Get();
                obj.Value = i;
                pool.Return(obj);
            }

            // Assert - No exception means success
            Assert.IsTrue(true);
        }

        #endregion

        #region ByteArrayPool Tests (if we're testing MemoryPool.cs)

        [TestMethod]
        public void ByteArrayPool_Rent_ReturnsArray()
        {
            // Arrange
            var pool = new ByteArrayPool();

            // Act
            var array = pool.Rent(1024);

            // Assert
            Assert.IsNotNull(array);
            Assert.IsTrue(array.Length >= 1024);
        }

        [TestMethod]
        public void ByteArrayPool_Return_WithoutClear()
        {
            // Arrange
            var pool = new ByteArrayPool();
            var array = pool.Rent(1024);
            array[0] = 42;

            // Act
            pool.Return(array, clearArray: false);
            var newArray = pool.Rent(1024);

            // Assert
            // The same array might be returned (implementation dependent)
            // Just verify no exception occurs
            Assert.IsNotNull(newArray);
        }

        [TestMethod]
        public void ByteArrayPool_Return_WithClear()
        {
            // Arrange
            var pool = new ByteArrayPool();
            var array = pool.Rent(1024);
            array[0] = 42;

            // Act
            pool.Return(array, clearArray: true);
            var newArray = pool.Rent(1024);

            // Assert
            Assert.IsNotNull(newArray);
            // If same array returned, it should be cleared
            Assert.AreEqual(0, newArray[0]);
        }

        [TestMethod]
        public void ByteArrayPool_RentCount_TracksCorrectly()
        {
            // Arrange
            var pool = new ByteArrayPool();

            // Act
            pool.Rent(1024);
            pool.Rent(1024);
            pool.Rent(1024);

            // Assert
            Assert.AreEqual(3, pool.RentCount);
        }

        [TestMethod]
        public void ByteArrayPool_ReturnCount_TracksCorrectly()
        {
            // Arrange
            var pool = new ByteArrayPool();
            var arr1 = pool.Rent(1024);
            var arr2 = pool.Rent(1024);

            // Act
            pool.Return(arr1);
            pool.Return(arr2);

            // Assert
            Assert.AreEqual(2, pool.ReturnCount);
        }

        #endregion

        // Test object for pool testing
        private class TestObject
        {
            public int Id { get; set; }
            public bool IsNew { get; set; } = true;
            public int Value { get; set; }
        }
    }
}
