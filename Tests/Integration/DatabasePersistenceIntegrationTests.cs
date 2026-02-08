using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Models;
using WhisperKey.Services;
using WhisperKey.Services.Database;
using WhisperKey.Services.Validation;

namespace WhisperKey.Tests.Integration
{
    [TestClass]
    public class DatabasePersistenceIntegrationTests
    {
        private IFileSystemService _fileSystemService = null!;
        private JsonDatabaseService _databaseService = null!;
        private string _testDataDirectory = null!;
        private ILogger<JsonDatabaseService> _logger = null!;

        [TestInitialize]
        public void Setup()
        {
            _testDataDirectory = Path.Combine(Path.GetTempPath(), $"DbIntegrationTest_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_testDataDirectory);
            
            _fileSystemService = new FileSystemService();
            _logger = new Microsoft.Extensions.Logging.Abstractions.NullLogger<JsonDatabaseService>();
            _databaseService = new JsonDatabaseService(_fileSystemService, _logger);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _databaseService?.Dispose();
            
            if (Directory.Exists(_testDataDirectory))
            {
                try
                {
                    Directory.Delete(_testDataDirectory, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        #region Basic Persistence Tests

        [TestMethod]
        public async Task Database_BasicPersistence_ShouldMaintainDataAcrossServiceInstances()
        {
            // Arrange
            var testData = new TestModel
            {
                Id = 1,
                Name = "Test Persistence",
                Description = "This should persist across service instances",
                CreatedAt = DateTime.UtcNow
            };

            // Act - Save with first instance
            await _databaseService.UpsertAsync("test_collection", testData, x => x.Id == 1);
            
            // Create new service instance (simulating app restart)
            using var newService = new JsonDatabaseService(_fileSystemService, _logger);
            
            // Retrieve with new instance
            var retrieved = await newService.QueryAsync<TestModel>("test_collection", x => x.Id == 1);

            // Assert
            Assert.IsNotNull(retrieved);
            Assert.AreEqual(testData.Id, retrieved.Id);
            Assert.AreEqual(testData.Name, retrieved.Name);
            Assert.AreEqual(testData.Description, retrieved.Description);
        }

        [TestMethod]
        public async Task Database_ConcurrentUpdates_ShouldMaintainConsistency()
        {
            // Arrange
            var baseModel = new TestModel { Id = 1, Name = "Base", CreatedAt = DateTime.UtcNow };
            await _databaseService.UpsertAsync("concurrent_test", baseModel, x => x.Id == 1);

            var tasks = new List<Task>();
            var updatedNames = new HashSet<string>();

            // Act - Concurrent updates from multiple threads
            for (int i = 0; i < 10; i++)
            {
                var threadId = i;
                tasks.Add(Task.Run(async () =>
                {
                    var updateModel = new TestModel 
                    { 
                        Id = 1, 
                        Name = $"Updated_Thread_{threadId}",
                        Description = $"Update from thread {threadId}",
                        CreatedAt = DateTime.UtcNow
                    };
                    
                    await _databaseService.UpsertAsync("concurrent_test", updateModel, x => x.Id == 1);
                    
                    // Read back to verify
                    var retrieved = await _databaseService.QueryAsync<TestModel>("concurrent_test", x => x.Id == 1);
                    if (retrieved != null)
                    {
                        lock (updatedNames)
                        {
                            updatedNames.Add(retrieved.Name);
                        }
                    }
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Should have final consistent state
            var final = await _databaseService.QueryAsync<TestModel>("concurrent_test", x => x.Id == 1);
            Assert.IsNotNull(final);
            Assert.IsTrue(final.Name.StartsWith("Updated_Thread_"));
        }

        [TestMethod]
        public async Task Database_LargeDataset_ShouldMaintainPerformance()
        {
            // Arrange
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var testData = new List<TestModel>();

            // Act - Create 1000 records
            for (int i = 0; i < 1000; i++)
            {
                var model = new TestModel
                {
                    Id = i,
                    Name = $"Item_{i:D4}",
                    Description = $"Description for item {i}",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i)
                };
                testData.Add(model);
                await _databaseService.UpsertAsync("large_dataset", model, x => x.Id == i);
            }

            stopwatch.Stop();
            var writeTime = stopwatch.ElapsedMilliseconds;

            // Query performance test
            stopwatch.Restart();
            var retrieved = await _databaseService.QueryListAsync<TestModel>("large_dataset", x => x.Id >= 500 && x.Id < 600);
            stopwatch.Stop();
            var readTime = stopwatch.ElapsedMilliseconds;

            // Assert
            Assert.AreEqual(1000, testData.Count);
            Assert.AreEqual(100, retrieved.Count);
            Assert.IsTrue(writeTime < 10000, $"Write performance too slow: {writeTime}ms for 1000 records");
            Assert.IsTrue(readTime < 1000, $"Read performance too slow: {readTime}ms for range query");
        }

        #endregion

        #region Schema Evolution Tests

        [TestMethod]
        public async Task Database_SchemaEvolution_ShouldHandleNewFields()
        {
            // Arrange - Create record with v1 schema
            var v1Model = new TestModelV1
            {
                Id = 1,
                Name = "Version 1",
                CreatedAt = DateTime.UtcNow
            };
            await _databaseService.UpsertAsync("schema_test", v1Model, x => x.Id == 1);

            // Act - Retrieve as v2 model (new field should be default)
            var v2Model = await _databaseService.QueryAsync<TestModelV2>("schema_test", x => x.Id == 1);

            // Assert - Should handle missing field gracefully
            Assert.IsNotNull(v2Model);
            Assert.AreEqual(v1Model.Id, v2Model.Id);
            Assert.AreEqual(v1Model.Name, v2Model.Name);
            Assert.AreEqual(default, v2Model.NewField); // Should be default for missing field
        }

        [TestMethod]
        public async Task Database_SchemaEvolution_ShouldPreserveExistingFields()
        {
            // Arrange - Create record with v2 schema
            var v2Model = new TestModelV2
            {
                Id = 1,
                Name = "Version 2",
                NewField = "New Value",
                CreatedAt = DateTime.UtcNow
            };
            await _databaseService.UpsertAsync("schema_test_v2", v2Model, x => x.Id == 1);

            // Act - Retrieve as v1 model (extra field should be ignored)
            var v1Model = await _databaseService.QueryAsync<TestModelV1>("schema_test_v2", x => x.Id == 1);

            // Assert - Should preserve existing fields
            Assert.IsNotNull(v1Model);
            Assert.AreEqual(v2Model.Id, v1Model.Id);
            Assert.AreEqual(v2Model.Name, v1Model.Name);
        }

        #endregion

        #region Data Integrity Tests

        [TestMethod]
        public async Task Database_CorruptionRecovery_ShouldHandleGracefully()
        {
            // Arrange - Create valid data
            var validModel = new TestModel
            {
                Id = 1,
                Name = "Valid Data",
                CreatedAt = DateTime.UtcNow
            };
            await _databaseService.UpsertAsync("corruption_test", validModel, x => x.Id == 1);

            // Simulate file corruption
            var collectionPath = Path.Combine(_testDataDirectory, "corruption_test.json");
            if (File.Exists(collectionPath))
            {
                await File.WriteAllTextAsync(collectionPath, "{ invalid json content");
            }

            // Act - Try to read corrupted data
            var corrupted = await _databaseService.QueryAsync<TestModel>("corruption_test", x => x.Id == 1);

            // Assert - Should handle corruption gracefully
            Assert.IsNull(corrupted);

            // Should be able to write new data after corruption
            var recoveryModel = new TestModel { Id = 2, Name = "Recovery", CreatedAt = DateTime.UtcNow };
            await _databaseService.UpsertAsync("corruption_test", recoveryModel, x => x.Id == 2);
            
            var recovered = await _databaseService.QueryAsync<TestModel>("corruption_test", x => x.Id == 2);
            Assert.IsNotNull(recovered);
            Assert.AreEqual(recoveryModel.Name, recovered.Name);
        }

        [TestMethod]
        public async Task Database_ConcurrentAccess_ShouldMaintainDataIntegrity()
        {
            // Arrange - Pre-populate with data
            for (int i = 0; i < 50; i++)
            {
                await _databaseService.UpsertAsync("integrity_test", 
                    new TestModel { Id = i, Name = $"Initial_{i}", CreatedAt = DateTime.UtcNow }, 
                    x => x.Id == i);
            }

            var writeTasks = new List<Task>();
            var readTasks = new List<Task<List<TestModel>>>();
            var exceptions = new List<Exception>();

            // Act - Perform concurrent reads and writes
            for (int i = 50; i < 100; i++)
            {
                var id = i;
                writeTasks.Add(Task.Run(async () =>
                {
                    try
                    {
                        await _databaseService.UpsertAsync("integrity_test",
                            new TestModel { Id = id, Name = $"Concurrent_{id}", CreatedAt = DateTime.UtcNow },
                            x => x.Id == id);
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions) { exceptions.Add(ex); }
                    }
                }));
            }

            for (int i = 0; i < 20; i++)
            {
                readTasks.Add(_databaseService.QueryListAsync<TestModel>("integrity_test", x => true));
            }

            await Task.WhenAll(writeTasks);
            var readResults = await Task.WhenAll(readTasks);

            // Assert - No data corruption should occur
            Assert.AreEqual(0, exceptions.Count, $"Exceptions occurred: {string.Join(", ", exceptions.Select(e => e.Message))}");
            
            foreach (var readResult in readResults)
            {
                Assert.IsTrue(readResult.Count >= 50, "Should read at least initial 50 records");
                Assert.IsTrue(readResult.All(r => r.Id >= 0 && r.Id < 100), "All IDs should be valid");
                Assert.IsTrue(readResult.All(r => !string.IsNullOrEmpty(r.Name)), "All names should be populated");
            }

            var finalRecords = await _databaseService.QueryListAsync<TestModel>("integrity_test", x => true);
            Assert.AreEqual(100, finalRecords.Count, "Should have all 100 records");
        }

        #endregion

        #region Backup and Recovery Tests

        [TestMethod]
        public async Task Database_BackupCreation_ShouldMaintainDataConsistency()
        {
            // Arrange - Create test data
            var originalData = new List<TestModel>();
            for (int i = 0; i < 10; i++)
            {
                var model = new TestModel
                {
                    Id = i,
                    Name = $"Backup_Test_{i}",
                    CreatedAt = DateTime.UtcNow.AddMinutes(-i)
                };
                originalData.Add(model);
                await _databaseService.UpsertAsync("backup_test", model, x => x.Id == i);
            }

            // Act - Create backup
            var backupPath = Path.Combine(_testDataDirectory, "backup_test_backup.json");
            var allRecords = await _databaseService.QueryListAsync<TestModel>("backup_test", x => true);
            await File.WriteAllTextAsync(backupPath, System.Text.Json.JsonSerializer.Serialize(allRecords));

            // Simulate data loss and restore from backup
            var collectionPath = Path.Combine(_testDataDirectory, "backup_test.json");
            File.Delete(collectionPath);
            await File.WriteAllTextAsync(collectionPath, await File.ReadAllTextAsync(backupPath));

            // Create new service instance to test recovery
            using var recoveryService = new JsonDatabaseService(_fileSystemService, _logger);
            var recoveredData = await recoveryService.QueryListAsync<TestModel>("backup_test", x => true);

            // Assert - Data should be identical
            Assert.AreEqual(originalData.Count, recoveredData.Count);
            foreach (var original in originalData)
            {
                var recovered = recoveredData.FirstOrDefault(x => x.Id == original.Id);
                Assert.IsNotNull(recovered);
                Assert.AreEqual(original.Name, recovered.Name);
            }
        }

        #endregion

        #region Performance Under Load Tests

        [TestMethod]
        public async Task Database_HighVolumeOperations_ShouldMaintainPerformance()
        {
            // Arrange
            var operationTimes = new List<long>();
            var batchSize = 100;
            var batchCount = 10;

            // Act - Process batches and measure performance
            for (int batch = 0; batch < batchCount; batch++)
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var batchTasks = new List<Task>();

                for (int i = 0; i < batchSize; i++)
                {
                    var id = batch * batchSize + i;
                    batchTasks.Add(_databaseService.UpsertAsync("performance_test",
                        new TestModel
                        {
                            Id = id,
                            Name = $"Perf_Test_{id:D5}",
                            Description = $"Performance test record {id}",
                            CreatedAt = DateTime.UtcNow.AddMilliseconds(id)
                        },
                        x => x.Id == id));
                }

                await Task.WhenAll(batchTasks);
                stopwatch.Stop();
                operationTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            // Assert - Performance should remain consistent
            var avgTime = operationTimes.Average();
            var maxTime = operationTimes.Max();
            var minTime = operationTimes.Min();

            Assert.IsTrue(avgTime < 5000, $"Average batch time too high: {avgTime}ms");
            Assert.IsTrue(maxTime < avgTime * 2, $"Performance degradation detected: min={minTime}ms, max={maxTime}ms, avg={avgTime}ms");

            // Verify all data was written correctly
            var allRecords = await _databaseService.QueryListAsync<TestModel>("performance_test", x => true);
            Assert.AreEqual(batchSize * batchCount, allRecords.Count);
        }

        #endregion

        #region Test Models for Schema Evolution

        private class TestModelV1
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
        }

        private class TestModelV2
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string NewField { get; set; } = string.Empty; // New field in v2
            public DateTime CreatedAt { get; set; }
        }

        #endregion
    }
}
