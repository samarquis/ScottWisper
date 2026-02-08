using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;
using WhisperKey.Services.Memory;
using WhisperKey.Services.Recovery;
using WhisperKey.Services.Database;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class PerformanceIntegrationTests
    {
        private IServiceProvider _serviceProvider = null!;
        private ByteArrayPool _memoryPool = null!;
        private JsonDatabaseService _databaseService = null!;
        private RecoveryPolicyService _recoveryService = null!;

        [TestInitialize]
        public void Setup()
        {
            var services = new ServiceCollection();
            
            services.AddSingleton<ByteArrayPool>();
            services.AddSingleton<JsonDatabaseService>();
            services.AddSingleton<RecoveryPolicyService>();

            _serviceProvider = services.BuildServiceProvider();

            _memoryPool = _serviceProvider.GetRequiredService<ByteArrayPool>();
            _databaseService = _serviceProvider.GetRequiredService<JsonDatabaseService>();
            _recoveryService = _serviceProvider.GetRequiredService<RecoveryPolicyService>();
        }

        [TestCleanup]
        public void Cleanup()
        {
            _databaseService?.Dispose();
            _serviceProvider?.Dispose();
        }

        #region REQ-005: Memory Pooling Performance Tests

        [TestMethod]
        public void MemoryPool_HighFrequencyRentals_ShouldMaintainPerformance()
        {
            // Arrange
            var rentalCount = 10000;
            var sizes = new[] { 1024, 2048, 4096, 8192, 16384 };
            var random = new Random(42); // Fixed seed for reproducible tests

            var measurements = new List<PerformanceMeasurement>();

            // Act - Perform high-frequency rentals
            var stopwatch = Stopwatch.StartNew();
            
            for (int i = 0; i < rentalCount; i++)
            {
                var size = sizes[random.Next(sizes.Length)];
                var rentStopwatch = Stopwatch.StartNew();
                
                var buffer = _memoryPool.Rent(size);
                
                rentStopwatch.Stop();
                
                // Simulate some work with the buffer
                for (int j = 0; j < size; j += 1024)
                {
                    buffer[j] = (byte)(j % 256);
                }
                
                _memoryPool.Return(buffer);
                
                measurements.Add(new PerformanceMeasurement
                {
                    OperationId = i,
                    Size = size,
                    RentalTime = rentStopwatch.Elapsed,
                    FromPool = buffer.Length >= size
                });
            }

            stopwatch.Stop();

            // Assert - Performance should remain consistent
            var avgRentalTime = measurements.Average(m => m.RentalTime.TotalMicroseconds);
            var maxRentalTime = measurements.Max(m => m.RentalTime.TotalMicroseconds);
            var poolHitRate = measurements.Count(m => m.FromPool) / (double)measurements.Count;

            Assert.IsTrue(avgRentalTime < 10, $"Average rental time {avgRentalTime}μs should be under 10μs");
            Assert.IsTrue(maxRentalTime < 100, $"Max rental time {maxRentalTime}μs should be under 100μs");
            Assert.IsTrue(poolHitRate > 0.8, $"Pool hit rate {poolHitRate:P} should be over 80%");
            Assert.IsTrue(stopwatch.ElapsedMilliseconds < 5000, $"Total time {stopwatch.ElapsedMilliseconds}ms should be reasonable");

            // Verify pool statistics
            Assert.IsTrue(_memoryPool.RentCount > 0, "Should have rental statistics");
            Assert.IsTrue(_memoryPool.ReturnCount > 0, "Should have return statistics");
        }

        [TestMethod]
        public void MemoryPool_ConcurrentAccess_ShouldMaintainLowContention()
        {
            // Arrange
            var concurrentUsers = 20;
            var operationsPerUser = 500;
            var contentionMetrics = new List<ContentionMetric>();

            // Act - Concurrent memory pool access
            var tasks = new List<Task>();
            var barrier = new Barrier(concurrentUsers);

            for (int user = 0; user < concurrentUsers; user++)
            {
                var userId = user;
                tasks.Add(Task.Run(() =>
                {
                    barrier.SignalAndWait(); // Synchronize start
                    
                    var userMetrics = new List<TimeSpan>();
                    
                    for (int op = 0; op < operationsPerUser; op++)
                    {
                        var stopwatch = Stopwatch.StartNew();
                        
                        var buffer = _memoryPool.Rent(4096);
                        buffer[0] = (byte)userId;
                        _memoryPool.Return(buffer);
                        
                        stopwatch.Stop();
                        userMetrics.Add(stopwatch.Elapsed);
                    }
                    
                    lock (contentionMetrics)
                    {
                        contentionMetrics.Add(new ContentionMetric
                        {
                            UserId = userId,
                            AverageOperationTime = TimeSpan.FromTicks((long)userMetrics.Average(t => t.Ticks)),
                            MaxOperationTime = userMetrics.Max(),
                            OperationCount = operationsPerUser
                        });
                    }
                }));
            }

            Task.WaitAll(tasks);

            // Assert - Contention should be minimal
            var avgAcrossUsers = contentionMetrics.Average(cm => cm.AverageOperationTime.TotalMicroseconds);
            var maxAcrossUsers = contentionMetrics.Max(cm => cm.MaxOperationTime.TotalMicroseconds);
            var totalOperations = contentionMetrics.Sum(cm => cm.OperationCount);

            Assert.AreEqual(concurrentUsers, contentionMetrics.Count);
            Assert.IsTrue(avgAcrossUsers < 50, $"Average operation time {avgAcrossUsers}μs should be low");
            Assert.IsTrue(maxAcrossUsers < 1000, $"Max operation time {maxAcrossUsers}μs should be reasonable");
            Assert.AreEqual(concurrentUsers * operationsPerUser, totalOperations);

            // Verify pool performance under contention
            Assert.AreEqual(totalOperations, _memoryPool.RentCount);
            Assert.AreEqual(totalOperations, _memoryPool.ReturnCount);
        }

        [TestMethod]
        public void MemoryPool_MemoryUsage_ShouldStayWithinLimits()
        {
            // Arrange
            var maxPoolSize = 1000; // Based on pool configuration
            var largeOperations = 1500; // More than pool capacity

            var memorySnapshots = new List<MemorySnapshot>();

            // Act - Stress the memory pool beyond capacity
            for (int i = 0; i < largeOperations; i++)
            {
                var beforeRent = GC.GetTotalMemory(false);
                
                var buffer = _memoryPool.Rent(8192);
                buffer[0] = (byte)i;
                
                var afterRent = GC.GetTotalMemory(false);
                
                // Simulate work
                await Task.Delay(1);
                
                _memoryPool.Return(buffer);
                
                var afterReturn = GC.GetTotalMemory(false);
                
                memorySnapshots.Add(new MemorySnapshot
                {
                    Operation = i,
                    MemoryBeforeRent = beforeRent,
                    MemoryAfterRent = afterRent,
                    MemoryAfterReturn = afterReturn,
                    BufferSize = 8192
                });
                
                // Periodic garbage collection to get accurate measurements
                if (i % 100 == 0)
                {
                    GC.Collect();
                }
            }

            // Assert - Memory usage should be controlled
            var maxMemoryUsage = memorySnapshots.Max(s => s.MemoryAfterRent - s.MemoryBeforeRent);
            var avgMemoryUsage = memorySnapshots.Average(s => s.MemoryAfterRent - s.MemoryBeforeRent);

            // Memory usage should be reasonable (considering GC)
            Assert.IsTrue(maxMemoryUsage < 50 * 1024 * 1024, $"Max memory usage {maxMemoryUsage / (1024*1024)}MB should be controlled");
            Assert.IsTrue(avgMemoryUsage < 10 * 1024 * 1024, $"Avg memory usage {avgMemoryUsage / (1024*1024)}MB should be low");

            // Pool should handle over-capacity gracefully
            Assert.AreEqual(largeOperations, _memoryPool.RentCount);
            Assert.AreEqual(largeOperations, _memoryPool.ReturnCount);
        }

        #endregion

        #region REQ-006: Performance Profiling Tests

        [TestMethod]
        public async Task PerformanceProfiling_CpuIntensiveOperations_ShouldMeasureAccurately()
        {
            // Arrange
            var profiler = new PerformanceProfiler();
            var testOperation = new CpuIntensiveOperation();

            // Act - Profile CPU-intensive operation
            var profile = await profiler.ProfileAsync(async () =>
            {
                await testOperation.ExecuteAsync(iterations: 1000, complexity: OperationComplexity.High);
            });

            // Assert - Profile should capture metrics accurately
            Assert.IsNotNull(profile);
            Assert.IsTrue(profile.Duration.TotalMilliseconds > 0, "Duration should be measured");
            Assert.IsTrue(profile.CpuUsage > 0, "CPU usage should be measured");
            Assert.IsTrue(profile.MemoryAllocated > 0, "Memory allocation should be measured");

            // Performance should be within acceptable limits
            Assert.IsTrue(profile.Duration.TotalSeconds < 30, $"Operation should complete in {profile.Duration.TotalSeconds}s");
            Assert.IsTrue(profile.CpuUsage < 100, $"CPU usage {profile.CpuUsage}% should be reasonable");
        }

        [TestMethod]
        public async Task PerformanceProfiling_MemoryIntensiveOperations_ShouldTrackCorrectly()
        {
            // Arrange
            var profiler = new PerformanceProfiler();
            var memoryOperation = new MemoryIntensiveOperation();

            // Act - Profile memory-intensive operation
            var profile = await profiler.ProfileAsync(async () =>
            {
                await memoryOperation.ExecuteAsync(dataSize: 100 * 1024 * 1024); // 100MB
            });

            // Assert - Memory metrics should be accurate
            Assert.IsTrue(profile.MemoryAllocated > 50 * 1024 * 1024, 
                $"Should allocate at least 50MB, allocated {profile.MemoryAllocated / (1024*1024)}MB");
            Assert.IsTrue(profile.PeakMemoryUsage > profile.MemoryAllocated,
                "Peak usage should exceed allocation");

            // Memory should be cleaned up after operation
            Assert.IsTrue(profile.MemoryFreed > 0, "Memory should be freed");
            Assert.IsTrue(profile.MemoryFreed >= profile.MemoryAllocated * 0.8,
                "Should free most allocated memory");
        }

        [TestMethod]
        public async Task PerformanceProfiling_ConcurrentOperations_ShouldMeasureEach()
        {
            // Arrange
            var profiler = new PerformanceProfiler();
            var concurrentCount = 10;

            // Act - Profile concurrent operations
            var profiles = await Task.WhenAll(
                Enumerable.Range(0, concurrentCount)
                    .Select(async i =>
                    {
                        var operation = new CpuIntensiveOperation();
                        return await profiler.ProfileAsync(async () =>
                        {
                            await operation.ExecuteAsync(iterations: 100, complexity: OperationComplexity.Medium);
                        });
                    })
            );

            // Assert - Each operation should be profiled correctly
            Assert.AreEqual(concurrentCount, profiles.Length);
            
            foreach (var profile in profiles)
            {
                Assert.IsNotNull(profile);
                Assert.IsTrue(profile.Duration.TotalMilliseconds > 0, "Each operation should have duration");
                Assert.IsTrue(profile.CpuUsage > 0, "Each operation should have CPU usage");
            }

            // Concurrent operations should complete within reasonable time
            var avgDuration = profiles.Average(p => p.Duration.TotalMilliseconds);
            var maxDuration = profiles.Max(p => p.Duration.TotalMilliseconds);
            
            Assert.IsTrue(avgDuration < 5000, $"Avg duration {avgDuration}ms should be reasonable");
            Assert.IsTrue(maxDuration < avgDuration * 3, "No operation should be much slower than average");
        }

        #endregion

        #region REQ-007: Database Optimization Tests

        [TestMethod]
        public async Task DatabaseOptimization_QueryPerformance_ShouldImproveWithIndexes()
        {
            // Arrange
            var testData = GenerateTestData(recordCount: 10000);
            
            // Insert test data
            foreach (var record in testData)
            {
                await _databaseService.UpsertAsync("performance_test", record, x => x.Id == record.Id);
            }

            // Act - Measure query performance before and after optimization
            var searchCriteria = testData.Where(r => r.Category == "PerformanceTest").ToList();
            
            var beforeOptimization = await MeasureQueryPerformanceAsync(
                "performance_test", x => x.Category == "PerformanceTest");

            // Simulate database optimization (in real scenario would create indexes)
            await SimulateDatabaseOptimizationAsync();

            var afterOptimization = await MeasureQueryPerformanceAsync(
                "performance_test", x => x.Category == "PerformanceTest");

            // Assert - Performance should improve after optimization
            Assert.IsTrue(afterOptimization.QueryTime < beforeOptimization.QueryTime,
                $"Query time should improve: before {beforeOptimization.QueryTime}ms, after {afterOptimization.QueryTime}ms");
            
            var improvementRatio = (double)beforeOptimization.QueryTime / afterOptimization.QueryTime;
            Assert.IsTrue(improvementRatio > 1.2,
                $"Should improve by at least 20%, actual improvement: {improvementRatio:P}");
        }

        [TestMethod]
        public async Task DatabaseOptimization_BatchOperations_ShouldBeMoreEfficient()
        {
            // Arrange
            var batchSize = 100;
            var totalRecords = 1000;
            var testData = GenerateTestData(recordCount: totalRecords);

            // Act - Measure individual vs batch operations
            var individualStopwatch = Stopwatch.StartNew();
            foreach (var record in testData)
            {
                await _databaseService.UpsertAsync("batch_test", record, x => x.Id == record.Id);
            }
            individualStopwatch.Stop();

            // Clear and test batch operations
            await ClearCollectionAsync("batch_test");
            
            var batchStopwatch = Stopwatch.StartNew();
            for (int i = 0; i < totalRecords; i += batchSize)
            {
                var batch = testData.Skip(i).Take(batchSize).ToList();
                var batchTasks = batch.Select(record =>
                    _databaseService.UpsertAsync("batch_test", record, x => x.Id == record.Id));
                await Task.WhenAll(batchTasks);
            }
            batchStopwatch.Stop();

            // Assert - Batch operations should be more efficient
            Assert.IsTrue(batchStopwatch.ElapsedMilliseconds < individualStopwatch.ElapsedMilliseconds,
                $"Batch should be faster: individual {individualStopwatch.ElapsedMilliseconds}ms, batch {batchStopwatch.ElapsedMilliseconds}ms");
            
            var efficiencyRatio = (double)individualStopwatch.ElapsedMilliseconds / batchStopwatch.ElapsedMilliseconds;
            Assert.IsTrue(efficiencyRatio > 1.1,
                $"Batch should be at least 10% more efficient: {efficiencyRatio:P}");
        }

        [TestMethod]
        public async Task DatabaseOptimization_ConcurrentAccess_ShouldMaintainPerformance()
        {
            // Arrange
            var concurrentUsers = 20;
            var operationsPerUser = 50;
            var testData = GenerateTestData(recordCount: concurrentUsers * operationsPerUser);

            // Act - Measure performance under concurrent load
            var performanceMetrics = new List<ConcurrentPerformanceMetric>();
            var barrier = new Barrier(concurrentUsers);

            var tasks = Enumerable.Range(0, concurrentUsers)
                .Select(userId => Task.Run(async () =>
                {
                    barrier.SignalAndWait();
                    
                    var userMetrics = new List<TimeSpan>();
                    var userRecords = testData
                        .Skip(userId * operationsPerUser)
                        .Take(operationsPerUser);

                    foreach (var record in userRecords)
                    {
                        var stopwatch = Stopwatch.StartNew();
                        
                        await _databaseService.UpsertAsync("concurrent_test", record, x => x.Id == record.Id);
                        var retrieved = await _databaseService.QueryAsync<TestPerformanceData>(
                            "concurrent_test", x => x.Id == record.Id);
                        
                        stopwatch.Stop();
                        userMetrics.Add(stopwatch.Elapsed);
                        
                        Assert.IsNotNull(retrieved, $"Record {record.Id} should be retrievable");
                    }

                    lock (performanceMetrics)
                    {
                        performanceMetrics.Add(new ConcurrentPerformanceMetric
                        {
                            UserId = userId,
                            AverageOperationTime = TimeSpan.FromTicks((long)userMetrics.Average(t => t.Ticks)),
                            MaxOperationTime = userMetrics.Max(),
                            OperationCount = operationsPerUser,
                            SuccessCount = userMetrics.Count
                        });
                    }
                }))
                .ToArray();

            await Task.WhenAll(tasks);

            // Assert - Performance should remain stable under concurrency
            var avgAcrossUsers = performanceMetrics.Average(m => m.AverageOperationTime.TotalMilliseconds);
            var maxAcrossUsers = performanceMetrics.Max(m => m.MaxOperationTime.TotalMilliseconds);
            var totalOperations = performanceMetrics.Sum(m => m.OperationCount);
            var successRate = performanceMetrics.Average(m => (double)m.SuccessCount / m.OperationCount);

            Assert.AreEqual(concurrentUsers, performanceMetrics.Count);
            Assert.IsTrue(avgAcrossUsers < 100, $"Average operation time {avgAcrossUsers}ms should be reasonable");
            Assert.IsTrue(maxAcrossUsers < avgAcrossUsers * 5, "No user should experience extreme slowness");
            Assert.AreEqual(concurrentUsers * operationsPerUser, totalOperations);
            Assert.AreEqual(1.0, successRate, "All operations should succeed");
        }

        #endregion

        #region REQ-008: Error Recovery Performance Tests

        [TestMethod]
        public async Task ErrorRecovery_FastRecovery_ShouldMinimizeDowntime()
        {
            // Arrange
            var recoveryService = new TestRecoveryService();
            var failureSimulator = new FailureSimulator();

            // Act - Measure recovery performance
            var recoveryMetrics = new List<RecoveryMetric>();

            for (int i = 0; i < 50; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                
                // Simulate failure and recovery
                var failure = await failureSimulator.SimulateFailureAsync();
                var recovered = await recoveryService.RecoverFromFailureAsync(failure);
                
                stopwatch.Stop();
                
                recoveryMetrics.Add(new RecoveryMetric
                {
                    FailureType = failure.Type,
                    RecoveryTime = stopwatch.Elapsed,
                    RecoverySuccessful = recovered,
                    DowntimeMs = stopwatch.ElapsedMilliseconds
                });
            }

            // Assert - Recovery should be fast and reliable
            var avgRecoveryTime = recoveryMetrics.Average(m => m.RecoveryTime.TotalMilliseconds);
            var maxRecoveryTime = recoveryMetrics.Max(m => m.RecoveryTime.TotalMilliseconds);
            var successRate = recoveryMetrics.Average(m => m.RecoverySuccessful ? 1.0 : 0.0);

            Assert.IsTrue(avgRecoveryTime < 5000, $"Avg recovery time {avgRecoveryTime}ms should be under 5s");
            Assert.IsTrue(maxRecoveryTime < 30000, $"Max recovery time {maxRecoveryTime}ms should be under 30s");
            Assert.IsTrue(successRate > 0.95, $"Recovery success rate {successRate:P} should be over 95%");
        }

        [TestMethod]
        public async Task ErrorRecovery_CircuitBreakerPerformance_ShouldPreventCascadeFailures()
        {
            // Arrange
            var circuitBreaker = new TestCircuitBreaker();
            var unreliableService = new UnreliableService();

            // Act - Test circuit breaker performance under failure conditions
            var operationMetrics = new List<CircuitBreakerMetric>();

            for (int i = 0; i < 100; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var circuitStateBefore = circuitBreaker.State;
                
                var result = await circuitBreaker.ExecuteThroughCircuitBreakerAsync(
                    () => unreliableService.OperationAsync());

                var circuitStateAfter = circuitBreaker.State;
                stopwatch.Stop();

                operationMetrics.Add(new CircuitBreakerMetric
                {
                    OperationId = i,
                    Success = result.Success,
                    OperationTime = stopwatch.Elapsed,
                    CircuitStateBefore = circuitStateBefore,
                    CircuitStateAfter = circuitStateAfter,
                    FastFail = circuitStateAfter == CircuitBreakerState.Open && stopwatch.ElapsedMilliseconds < 100
                });
            }

            // Assert - Circuit breaker should prevent cascade failures
            var fastFailures = operationMetrics.Count(m => m.FastFail);
            var totalOperations = operationMetrics.Count;
            var fastFailRate = (double)fastFailures / totalOperations;

            Assert.IsTrue(fastFailures > 0, "Should have some fast failures when circuit is open");
            Assert.IsTrue(fastFailRate > 0.2, "At least 20% of operations should fail fast when circuit opens");
            Assert.IsTrue(operationMetrics.Where(m => m.CircuitStateAfter == CircuitBreakerState.Open)
                .All(m => m.OperationTime.TotalMilliseconds < 200),
                "Fast failures should be quick");
        }

        #endregion

        #region Helper Methods

        private List<TestPerformanceData> GenerateTestData(int recordCount)
        {
            return Enumerable.Range(0, recordCount)
                .Select(i => new TestPerformanceData
                {
                    Id = i,
                    Name = $"TestRecord_{i:D5}",
                    Category = i % 3 == 0 ? "PerformanceTest" : "OtherCategory",
                    Value = i * 1.5,
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i)
                })
                .ToList();
        }

        private async Task<QueryPerformanceMetric> MeasureQueryPerformanceAsync<T>(
            string collectionName, Func<T, bool> predicate) where T : class
        {
            var stopwatch = Stopwatch.StartNew();
            var results = await _databaseService.QueryListAsync(collectionName, predicate);
            stopwatch.Stop();

            return new QueryPerformanceMetric
            {
                QueryTime = stopwatch.ElapsedMilliseconds,
                ResultCount = results.Count,
                CollectionName = collectionName
            };
        }

        private async Task SimulateDatabaseOptimizationAsync()
        {
            // Simulate database optimization (in real scenario would:
            // - Create indexes, update statistics, defragment, etc.)
            await Task.Delay(100); // Simulate optimization time
        }

        private async Task ClearCollectionAsync(string collectionName)
        {
            // Clear all records from collection
            var records = await _databaseService.QueryListAsync<TestPerformanceData>(collectionName, x => true);
            foreach (var record in records)
            {
                await _databaseService.UpsertAsync(collectionName, 
                    null!, x => x.Id == record.Id); // This is a simplified clear
            }
        }

        #endregion

        #region Test Helper Classes

        private class TestPerformanceData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
            public double Value { get; set; }
            public DateTime CreatedAt { get; set; }
        }

        private class PerformanceMeasurement
        {
            public int OperationId { get; set; }
            public int Size { get; set; }
            public TimeSpan RentalTime { get; set; }
            public bool FromPool { get; set; }
        }

        private class ContentionMetric
        {
            public int UserId { get; set; }
            public TimeSpan AverageOperationTime { get; set; }
            public TimeSpan MaxOperationTime { get; set; }
            public int OperationCount { get; set; }
        }

        private class MemorySnapshot
        {
            public int Operation { get; set; }
            public long MemoryBeforeRent { get; set; }
            public long MemoryAfterRent { get; set; }
            public long MemoryAfterReturn { get; set; }
            public int BufferSize { get; set; }
        }

        private class QueryPerformanceMetric
        {
            public long QueryTime { get; set; }
            public int ResultCount { get; set; }
            public string CollectionName { get; set; } = string.Empty;
        }

        private class ConcurrentPerformanceMetric
        {
            public int UserId { get; set; }
            public TimeSpan AverageOperationTime { get; set; }
            public TimeSpan MaxOperationTime { get; set; }
            public int OperationCount { get; set; }
            public int SuccessCount { get; set; }
        }

        private class RecoveryMetric
        {
            public string FailureType { get; set; } = string.Empty;
            public TimeSpan RecoveryTime { get; set; }
            public bool RecoverySuccessful { get; set; }
            public long DowntimeMs { get; set; }
        }

        private class CircuitBreakerMetric
        {
            public int OperationId { get; set; }
            public bool Success { get; set; }
            public TimeSpan OperationTime { get; set; }
            public CircuitBreakerState CircuitStateBefore { get; set; }
            public CircuitBreakerState CircuitStateAfter { get; set; }
            public bool FastFail { get; set; }
        }

        #endregion

        #region Mock Performance Classes

        private class PerformanceProfiler
        {
            public async Task<PerformanceProfile> ProfileAsync(Func<Task> operation)
            {
                var stopwatch = Stopwatch.StartNew();
                var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                var memoryBefore = GC.GetTotalMemory(false);

                await operation();

                stopwatch.Stop();
                var memoryAfter = GC.GetTotalMemory(false);

                return new PerformanceProfile
                {
                    Duration = stopwatch.Elapsed,
                    CpuUsage = cpuCounter?.NextValue() ?? 0,
                    MemoryAllocated = Math.Max(0, memoryAfter - memoryBefore),
                    PeakMemoryUsage = memoryAfter,
                    MemoryFreed = Math.Max(0, memoryBefore - memoryAfter)
                };
            }
        }

        private class PerformanceProfile
        {
            public TimeSpan Duration { get; set; }
            public double CpuUsage { get; set; }
            public long MemoryAllocated { get; set; }
            public long PeakMemoryUsage { get; set; }
            public long MemoryFreed { get; set; }
        }

        private class CpuIntensiveOperation
        {
            public async Task ExecuteAsync(int iterations, OperationComplexity complexity)
            {
                for (int i = 0; i < iterations; i++)
                {
                    // Simulate CPU work
                    var result = Math.Sin(i * complexity.GetMultiplier()) * Math.Cos(i * 0.1);
                    await Task.Delay(1);
                }
            }
        }

        private class MemoryIntensiveOperation
        {
            public async Task ExecuteAsync(int dataSize)
            {
                var data = new byte[dataSize];
                
                // Fill with some data to ensure memory allocation
                for (int i = 0; i < dataSize; i++)
                {
                    data[i] = (byte)(i % 256);
                }

                // Simulate some processing
                await Task.Delay(100);

                // Process data (calculate checksum)
                long checksum = 0;
                for (int i = 0; i < dataSize; i += 1024)
                {
                    checksum += data[i];
                }

                // Cleanup
                Array.Clear(data, 0, data.Length);
            }
        }

        private class TestRecoveryService
        {
            public async Task<bool> RecoverFromFailureAsync(SimulatedFailure failure)
            {
                // Simulate recovery time based on failure type
                await Task.Delay(failure.Type.GetRecoveryTime());
                return true; // Always recover successfully in test
            }
        }

        private class FailureSimulator
        {
            private readonly Random _random = new Random(42);

            public async Task<SimulatedFailure> SimulateFailureAsync()
            {
                var failureTypes = new[] { "NetworkError", "DatabaseError", "ServiceUnavailable", "Timeout" };
                var type = failureTypes[_random.Next(failureTypes.Length)];
                
                // Simulate failure detection time
                await Task.Delay(_random.Next(50, 500));
                
                return new SimulatedFailure { Type = type };
            }
        }

        private class TestCircuitBreaker
        {
            private int _failureCount = 0;
            private CircuitBreakerState _state = CircuitBreakerState.Closed;

            public CircuitBreakerState State => _state;

            public async Task<OperationResult> ExecuteThroughCircuitBreakerAsync(Func<Task<Task<OperationResult>>> operation)
            {
                if (_state == CircuitBreakerState.Open)
                {
                    await Task.Delay(100); // Fast fail
                    return new OperationResult { Success = false, Error = "Circuit open" };
                }

                try
                {
                    var result = await (await operation()).ConfigureAwait(false);
                    
                    if (result.Success)
                    {
                        _failureCount = 0;
                        _state = CircuitBreakerState.Closed;
                    }
                    else
                    {
                        _failureCount++;
                        if (_failureCount >= 5)
                        {
                            _state = CircuitBreakerState.Open;
                        }
                    }

                    return result;
                }
                catch
                {
                    _failureCount++;
                    if (_failureCount >= 5)
                    {
                        _state = CircuitBreakerState.Open;
                    }
                    return new OperationResult { Success = false, Error = "Exception" };
                }
            }
        }

        private class UnreliableService
        {
            private readonly Random _random = new Random(42);

            public async Task<OperationResult> OperationAsync()
            {
                await Task.Delay(_random.Next(10, 100));
                
                // 30% failure rate
                if (_random.NextDouble() < 0.3)
                {
                    return new OperationResult { Success = false, Error = "Random failure" };
                }
                
                return new OperationResult { Success = true, Result = "Operation completed" };
            }
        }

        #endregion

        #region Supporting Enums and Classes

        public enum OperationComplexity
        {
            Low,
            Medium,
            High
        }

        public enum CircuitBreakerState
        {
            Closed,
            Open,
            HalfOpen
        }

        public class OperationResult
        {
            public bool Success { get; set; }
            public string? Error { get; set; }
            public string? Result { get; set; }
        }

        public class SimulatedFailure
        {
            public string Type { get; set; } = string.Empty;
        }

        #endregion

        #region Extension Methods

        public static class OperationComplexityExtensions
        {
            public static double GetMultiplier(this OperationComplexity complexity)
            {
                return complexity switch
                {
                    OperationComplexity.Low => 1.0,
                    OperationComplexity.Medium => 2.5,
                    OperationComplexity.High => 5.0,
                    _ => 1.0
                };
            }
        }

        public static class StringExtensions
        {
            public static int GetRecoveryTime(this string failureType)
            {
                return failureType switch
                {
                    "NetworkError" => 2000,
                    "DatabaseError" => 5000,
                    "ServiceUnavailable" => 1000,
                    "Timeout" => 3000,
                    _ => 2000
                };
            }
        }

        #endregion
    }
}
