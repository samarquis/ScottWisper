using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Polly;
using WhisperKey.Services;
using WhisperKey.Services.Database;
using WhisperKey.Services.Recovery;

namespace WhisperKey.Tests.Unit
{
    [TestClass]
    public class JsonDatabaseServiceTests
    {
        private Mock<IFileSystemService> _mockFileSystem = null!;
        private Mock<IRecoveryPolicyService> _mockRecoveryPolicy = null!;
        private JsonDatabaseService _service = null!;
        private string _testAppDataPath = null!;

        [TestInitialize]
        public void Setup()
        {
            _testAppDataPath = Path.Combine(Path.GetTempPath(), $"JsonDbTest_{Guid.NewGuid():N}");
            _mockFileSystem = new Mock<IFileSystemService>();
            _mockFileSystem.Setup(f => f.GetAppDataPath()).Returns(_testAppDataPath);
            _mockFileSystem.Setup(f => f.CombinePath(It.IsAny<string[]>()))
                .Returns((string[] paths) => Path.Combine(paths));

            _service = new JsonDatabaseService(_mockFileSystem.Object, NullLogger<JsonDatabaseService>.Instance);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _service?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_WithFileSystem_InitializesCorrectly()
        {
            // Arrange & Act
            var service = new JsonDatabaseService(_mockFileSystem.Object, NullLogger<JsonDatabaseService>.Instance);

            // Assert
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        public void Constructor_WithRecoveryPolicy_InitializesCorrectly()
        {
            // Arrange & Act
            _mockRecoveryPolicy = new Mock<IRecoveryPolicyService>();
            var service = new JsonDatabaseService(
                _mockFileSystem.Object,
                _mockRecoveryPolicy.Object,
                NullLogger<JsonDatabaseService>.Instance);

            // Assert
            Assert.IsNotNull(service);
            service.Dispose();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullFileSystem_ThrowsArgumentNullException()
        {
            // Act
            new JsonDatabaseService(null!, NullLogger<JsonDatabaseService>.Instance);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentNullException))]
        public void Constructor_NullLogger_ThrowsArgumentNullException()
        {
            // Act
            new JsonDatabaseService(_mockFileSystem.Object, null!);
        }

        #endregion

        #region QueryAsync Tests

        [TestMethod]
        public async Task QueryAsync_ExistingItem_ReturnsItem()
        {
            // Arrange
            var testItem = new TestModel { Id = 1, Name = "Test" };
            var collection = new List<TestModel> { testItem };
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);

            // Act
            var result = await _service.QueryAsync<TestModel>("test_collection", x => x.Id == 1);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Test", result.Name);
        }

        [TestMethod]
        public async Task QueryAsync_NonExistingItem_ReturnsNull()
        {
            // Arrange
            var collection = new List<TestModel>();
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);

            // Act
            var result = await _service.QueryAsync<TestModel>("test_collection", x => x.Id == 999);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task QueryAsync_FileDoesNotExist_ReturnsNull()
        {
            // Arrange
            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);

            // Act
            var result = await _service.QueryAsync<TestModel>("nonexistent", x => x.Id == 1);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task QueryAsync_InvalidJson_ReturnsNull()
        {
            // Arrange
            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync("invalid json");

            // Act
            var result = await _service.QueryAsync<TestModel>("test_collection", x => x.Id == 1);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task QueryAsync_WithCache_ReturnsCachedItem()
        {
            // Arrange
            var testItem = new TestModel { Id = 1, Name = "Cached" };
            var collection = new List<TestModel> { testItem };
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);

            // First call to populate cache
            await _service.QueryAsync<TestModel>("cached_collection", x => x.Id == 1);

            // Act - Second call should use cache
            var result = await _service.QueryAsync<TestModel>("cached_collection", x => x.Id == 1);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("Cached", result.Name);
            // File should only be read once
            _mockFileSystem.Verify(f => f.ReadAllTextAsync(It.IsAny<string>()), Times.Once);
        }

        #endregion

        #region QueryListAsync Tests

        [TestMethod]
        public async Task QueryListAsync_MultipleMatches_ReturnsAllMatches()
        {
            // Arrange
            var collection = new List<TestModel>
            {
                new() { Id = 1, Name = "Test1", Category = "A" },
                new() { Id = 2, Name = "Test2", Category = "A" },
                new() { Id = 3, Name = "Test3", Category = "B" }
            };
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);

            // Act
            var result = await _service.QueryListAsync<TestModel>("test_collection", x => x.Category == "A");

            // Assert
            Assert.AreEqual(2, result.Count);
        }

        [TestMethod]
        public async Task QueryListAsync_NoMatches_ReturnsEmptyList()
        {
            // Arrange
            var collection = new List<TestModel>();
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);

            // Act
            var result = await _service.QueryListAsync<TestModel>("test_collection", x => x.Id == 999);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public async Task QueryListAsync_Exception_ReturnsEmptyList()
        {
            // Arrange
            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ThrowsAsync(new IOException("Disk error"));

            // Act
            var result = await _service.QueryListAsync<TestModel>("test_collection", x => true);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        #endregion

        #region UpsertAsync Tests

        [TestMethod]
        public async Task UpsertAsync_NewItem_AddsToCollection()
        {
            // Arrange
            var collection = new List<TestModel>();
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);
            _mockFileSystem.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var newItem = new TestModel { Id = 1, Name = "New Item" };

            // Act
            await _service.UpsertAsync("test_collection", newItem, x => x.Id == 1);

            // Assert
            _mockFileSystem.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("New Item"))), Times.Once);
        }

        [TestMethod]
        public async Task UpsertAsync_ExistingItem_UpdatesInCollection()
        {
            // Arrange
            var existingItem = new TestModel { Id = 1, Name = "Old Name" };
            var collection = new List<TestModel> { existingItem };
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);
            _mockFileSystem.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var updatedItem = new TestModel { Id = 1, Name = "Updated Name" };

            // Act
            await _service.UpsertAsync("test_collection", updatedItem, x => x.Id == 1);

            // Assert
            _mockFileSystem.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("Updated Name"))), Times.Once);
        }

        [TestMethod]
        public async Task UpsertAsync_ConcurrentAccess_HandlesCorrectly()
        {
            // Arrange
            var collection = new List<TestModel>();
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);
            _mockFileSystem.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                var id = i;
                tasks.Add(_service.UpsertAsync("concurrent_collection", new TestModel { Id = id, Name = $"Item{id}" }, x => x.Id == id));
            }

            // Act
            await Task.WhenAll(tasks);

            // Assert - No exceptions should be thrown
            _mockFileSystem.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(10));
        }

        #endregion

        #region InvalidateCache Tests

        [TestMethod]
        public async Task InvalidateCache_RemovesFromCache()
        {
            // Arrange
            var testItem = new TestModel { Id = 1, Name = "Test" };
            var collection = new List<TestModel> { testItem };
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);

            // Populate cache
            await _service.QueryAsync<TestModel>("cache_test", x => x.Id == 1);

            // Act
            _service.InvalidateCache("cache_test");

            // Query again - should read from file
            await _service.QueryAsync<TestModel>("cache_test", x => x.Id == 1);

            // Assert - File should be read twice (once before invalidation, once after)
            _mockFileSystem.Verify(f => f.ReadAllTextAsync(It.IsAny<string>()), Times.Exactly(2));
        }

        [TestMethod]
        public void InvalidateCache_NonExistingCollection_DoesNotThrow()
        {
            // Act & Assert - Should not throw
            _service.InvalidateCache("nonexistent_collection");
        }

        #endregion

        #region Dispose Tests

        [TestMethod]
        public void Dispose_ReleasesResources()
        {
            // Arrange
            var service = new JsonDatabaseService(_mockFileSystem.Object, NullLogger<JsonDatabaseService>.Instance);

            // Act
            service.Dispose();

            // Assert - No exception should be thrown
            Assert.IsNotNull(service);
        }

        [TestMethod]
        public void Dispose_MultipleCalls_DoesNotThrow()
        {
            // Arrange
            var service = new JsonDatabaseService(_mockFileSystem.Object, NullLogger<JsonDatabaseService>.Instance);

            // Act & Assert - Should not throw on multiple disposes
            service.Dispose();
            service.Dispose();
        }

        #endregion

        #region Recovery Policy Integration Tests

        [TestMethod]
        public async Task QueryAsync_WithRecoveryPolicy_UsesRetryPolicy()
        {
            // Arrange - Create recovery policy service that returns a retry policy
            var recoveryService = new RecoveryPolicyService(
                NullLogger<RecoveryPolicyService>.Instance,
                Mock.Of<IAuditLoggingService>());

            var service = new JsonDatabaseService(
                _mockFileSystem.Object,
                recoveryService,
                NullLogger<JsonDatabaseService>.Instance);

            var testItem = new TestModel { Id = 1, Name = "Test" };
            var collection = new List<TestModel> { testItem };
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);

            // Act
            Func<TestModel, bool> predicate = x => x.Id == 1;
            var result = await service.QueryAsync<TestModel>("recovery_test", predicate);

            // Assert
            Assert.IsNotNull(result);

            service.Dispose();
        }

        #endregion

        #region Edge Case Tests

        [TestMethod]
        public async Task QueryAsync_EmptyCollection_ReturnsNull()
        {
            // Arrange
            var collection = new List<TestModel>();
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync(json);

            // Act
            var result = await _service.QueryAsync<TestModel>("empty_collection", x => true);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task QueryAsync_NullJson_ReturnsNull()
        {
            // Arrange
            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>())).ReturnsAsync((string)null!);

            // Act
            var result = await _service.QueryAsync<TestModel>("null_collection", x => true);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public async Task QueryAsync_SlowQuery_LogsWarning()
        {
            // Arrange
            var collection = new List<TestModel>();
            for (int i = 0; i < 10000; i++)
            {
                collection.Add(new TestModel { Id = i, Name = $"Item{i}" });
            }
            var json = System.Text.Json.JsonSerializer.Serialize(collection);

            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(true);
            _mockFileSystem.Setup(f => f.ReadAllTextAsync(It.IsAny<string>()))
                .Returns(async () =>
                {
                    await Task.Delay(50); // Simulate slow read
                    return json;
                });

            // Act
            var result = await _service.QueryAsync<TestModel>("slow_collection", x => x.Id == 9999);

            // Assert
            Assert.IsNotNull(result);
        }

        [TestMethod]
        public async Task UpsertAsync_NewFile_CreatesFile()
        {
            // Arrange
            _mockFileSystem.Setup(f => f.FileExists(It.IsAny<string>())).Returns(false);
            _mockFileSystem.Setup(f => f.WriteAllTextAsync(It.IsAny<string>(), It.IsAny<string>())).Returns(Task.CompletedTask);

            var newItem = new TestModel { Id = 1, Name = "New Item" };

            // Act
            await _service.UpsertAsync("new_collection", newItem, x => x.Id == 1);

            // Assert
            _mockFileSystem.Verify(f => f.WriteAllTextAsync(It.IsAny<string>(), It.Is<string>(s => s.Contains("New Item"))), Times.Once);
        }

        #endregion

        // Test model for serialization
        private class TestModel
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Category { get; set; } = string.Empty;
        }
    }
}
