using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WhisperKey.Services;
using WhisperKey.Services.Database;

namespace WhisperKey.Tests.Integration
{
    /// <summary>
    /// Integration testing environment setup and test isolation utilities.
    /// Provides clean, isolated environments for integration tests.
    /// </summary>
    [TestClass]
    public class IntegrationTestEnvironment
    {
        private static readonly List<TestEnvironment> _activeEnvironments = new();
        private static readonly object _environmentLock = new object();

        #region Environment Management

        [TestInitialize]
        public async Task SetupTestEnvironment()
        {
            var environment = await CreateIsolatedEnvironmentAsync();
            
            lock (_environmentLock)
            {
                _activeEnvironments.Add(environment);
            }

            // Configure environment for testing
            await ConfigureEnvironmentAsync(environment);
        }

        [TestCleanup]
        public async Task CleanupTestEnvironment()
        {
            TestEnvironment? environmentToRemove = null;

            lock (_environmentLock)
            {
                environmentToRemove = _activeEnvironments.LastOrDefault();
                if (environmentToRemove != null)
                {
                    _activeEnvironments.Remove(environmentToRemove);
                }
            }

            if (environmentToRemove != null)
            {
                await CleanupEnvironmentAsync(environmentToRemove);
            }
        }

        private async Task<TestEnvironment> CreateIsolatedEnvironmentAsync()
        {
            var environmentId = Guid.NewGuid().ToString("N")[..8];
            var environmentPath = Path.Combine(
                Path.GetTempPath(),
                $"WhisperKey_IntegrationTest_{environmentId}");

            Directory.CreateDirectory(environmentPath);

            return new TestEnvironment
            {
                Id = environmentId,
                RootPath = environmentPath,
                DataPath = Path.Combine(environmentPath, "Data"),
                LogsPath = Path.Combine(environmentPath, "Logs"),
                ConfigPath = Path.Combine(environmentPath, "Config"),
                ServiceProvider = CreateServiceProvider(environmentId, environmentPath)
            };
        }

        private async Task ConfigureEnvironmentAsync(TestEnvironment environment)
        {
            // Create subdirectories
            Directory.CreateDirectory(environment.DataPath);
            Directory.CreateDirectory(environment.LogsPath);
            Directory.CreateDirectory(environment.ConfigPath);

            // Setup test configuration
            await CreateTestConfigurationAsync(environment);

            // Initialize test database
            await InitializeTestDatabaseAsync(environment);

            // Setup logging
            SetupTestLogging(environment);
        }

        private async Task CleanupEnvironmentAsync(TestEnvironment environment)
        {
            try
            {
                // Dispose service provider
                environment.ServiceProvider?.Dispose();

                // Clean up test files (with retries for locked files)
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try
                    {
                        if (Directory.Exists(environment.RootPath))
                        {
                            Directory.Delete(environment.RootPath, true);
                        }
                        break;
                    }
                    catch (IOException) when (attempt < 2)
                    {
                        await Task.Delay(1000); // Wait and retry
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to clean up test environment {environment.Id}: {ex.Message}");
            }
        }

        #endregion

        #region Service Provider Configuration

        private IServiceProvider CreateServiceProvider(string environmentId, string rootPath)
        {
            var services = new ServiceCollection();

            // Configure logging for test environment
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.AddFile(Path.Combine(rootPath, "Logs", $"test_{environmentId}.log"));
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Register file system service with isolated paths
            services.AddSingleton<IFileSystemService>(new IsolatedFileSystemService(rootPath));

            // Register core services
            services.AddSingleton<JsonDatabaseService>();
            services.AddSingleton<ApiKeyManagementService>();
            services.AddSingleton<WhisperKey.Services.Validation.InputValidationService>();
            services.AddSingleton<WhisperKey.Services.Validation.SanitizationService>();
            services.AddSingleton<RecoveryPolicyService>();
            services.AddSingleton<SecurityAlertService>();

            // Register test-specific services
            services.AddSingleton<TestEnvironmentDataService>();
            services.AddSingleton<TestMockService>();

            return services.BuildServiceProvider();
        }

        private async Task CreateTestConfigurationAsync(TestEnvironment environment)
        {
            var configPath = Path.Combine(environment.ConfigPath, "test-settings.json");
            
            var testConfig = new
            {
                Environment = "IntegrationTest",
                EnvironmentId = environment.Id,
                TestMode = true,
                ExternalServices = new
                {
                    OpenAI = new
                    {
                        Endpoint = "https://api.openai-test.com/v1/audio/transcriptions",
                        UseMock = true
                    },
                    Azure = new
                    {
                        Endpoint = "https://eastus.stt.speech.microsoft.com/speech/recognition/conversationtranscribes",
                        UseMock = true
                    }
                },
                Database = new
                {
                    Provider = "JsonFile",
                    Path = environment.DataPath,
                    EnableCaching = true
                },
                Logging = new
                {
                    Level = "Debug",
                    IncludeSensitiveData = false
                }
            };

            var configJson = System.Text.Json.JsonSerializer.Serialize(testConfig, new System.Text.Json.JsonSerializerOptions 
            { 
                WriteIndented = true 
            });

            await File.WriteAllTextAsync(configPath, configJson);
        }

        private async Task InitializeTestDatabaseAsync(TestEnvironment environment)
        {
            var databaseService = environment.ServiceProvider.GetRequiredService<JsonDatabaseService>();
            
            // Create test data collections
            var testData = new List<TestIntegrationData>
            {
                new() { Id = 1, Name = "Test User 1", Email = "user1@test.com", Active = true },
                new() { Id = 2, Name = "Test User 2", Email = "user2@test.com", Active = true },
                new() { Id = 3, Name = "Test User 3", Email = "user3@test.com", Active = false }
            };

            foreach (var data in testData)
            {
                await databaseService.UpsertAsync("test_users", data, x => x.Id == data.Id);
            }
        }

        private void SetupTestLogging(TestEnvironment environment)
        {
            var loggerFactory = environment.ServiceProvider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger<IntegrationTestEnvironment>();

            logger.LogInformation("Integration test environment {EnvironmentId} initialized at {RootPath}", 
                environment.Id, environment.RootPath);
        }

        #endregion

        #region Test Utilities

        [TestMethod]
        public async Task TestEnvironment_Isolation_ShouldPreventCrossContamination()
        {
            // Arrange - Create multiple isolated environments
            var environments = new List<TestEnvironment>();
            
            for (int i = 0; i < 3; i++)
            {
                var env = await CreateIsolatedEnvironmentAsync();
                await ConfigureEnvironmentAsync(env);
                environments.Add(env);
            }

            try
            {
                // Act - Modify data in each environment
                foreach (var (index, env) in environments.Select((e, i) => (i, e)))
                {
                    var databaseService = env.ServiceProvider.GetRequiredService<JsonDatabaseService>();
                    
                    var uniqueData = new TestIntegrationData
                    {
                        Id = 100 + index,
                        Name = $"Environment {env.Id} Data",
                        Email = $"env{index}@{env.Id}.test.com",
                        Active = true
                    };

                    await databaseService.UpsertAsync("isolation_test", uniqueData, x => x.Id == uniqueData.Id);
                }

                // Assert - Each environment should only contain its own data
                for (int i = 0; i < environments.Count; i++)
                {
                    var env = environments[i];
                    var databaseService = env.ServiceProvider.GetRequiredService<JsonDatabaseService>();
                    
                    var allData = await databaseService.QueryListAsync<TestIntegrationData>("isolation_test", x => true);
                    
                    // Should only have one record (the one created in this environment)
                    Assert.AreEqual(1, allData.Count, 
                        $"Environment {env.Id} should only contain its own data");
                    
                    var data = allData.First();
                    Assert.AreEqual(env.Id, data.Email.Substring(data.Email.LastIndexOf('@') + 1),
                        $"Data should belong to environment {env.Id}");
                }
            }
            finally
            {
                // Cleanup
                foreach (var env in environments)
                {
                    await CleanupEnvironmentAsync(env);
                }
            }
        }

        [TestMethod]
        public async Task TestEnvironment_ResourceCleanup_ShouldFreeAllResources()
        {
            // Arrange
            var environment = await CreateIsolatedEnvironmentAsync();
            await ConfigureEnvironmentAsync(environment);

            // Act - Use services and create files
            var databaseService = environment.ServiceProvider.GetRequiredService<JsonDatabaseService>();
            
            await databaseService.UpsertAsync("cleanup_test", 
                new TestIntegrationData { Id = 1, Name = "Cleanup Test", Email = "cleanup@test.com" }, 
                x => x.Id == 1);

            var tempFile = Path.Combine(environment.RootPath, "temp_file.txt");
            await File.WriteAllTextAsync(tempFile, "test content");

            // Verify resources exist
            var dataExists = await databaseService.QueryAsync<TestIntegrationData>("cleanup_test", x => x.Id == 1) != null;
            var fileExists = File.Exists(tempFile);

            Assert.IsTrue(dataExists, "Test data should exist");
            Assert.IsTrue(fileExists, "Temp file should exist");

            // Cleanup
            await CleanupEnvironmentAsync(environment);

            // Assert - Resources should be cleaned up
            Assert.IsFalse(Directory.Exists(environment.RootPath), "Environment directory should be deleted");
        }

        [TestMethod]
        public async Task TestEnvironment_ConcurrentEnvironments_ShouldNotInterfere()
        {
            // Arrange
            var concurrentEnvironments = new List<TestEnvironment>();
            var tasks = new List<Task>();

            // Act - Create multiple environments concurrently
            for (int i = 0; i < 5; i++)
            {
                var index = i;
                tasks.Add(Task.Run(async () =>
                {
                    var env = await CreateIsolatedEnvironmentAsync();
                    await ConfigureEnvironmentAsync(env);
                    
                    lock (concurrentEnvironments)
                    {
                        concurrentEnvironments.Add(env);
                    }

                    // Create unique data in each environment
                    var databaseService = env.ServiceProvider.GetRequiredService<JsonDatabaseService>();
                    await databaseService.UpsertAsync("concurrent_test", 
                        new TestIntegrationData 
                        { 
                            Id = index, 
                            Name = $"Concurrent Test {index}",
                            Email = $"concurrent{index}@env{index}.test.com",
                            Active = true 
                        }, 
                        x => x.Id == index);
                }));
            }

            await Task.WhenAll(tasks);

            // Assert - Each environment should have its own data
            Assert.AreEqual(5, concurrentEnvironments.Count);
            
            foreach (var env in concurrentEnvironments)
            {
                var databaseService = env.ServiceProvider.GetRequiredService<JsonDatabaseService>();
                var data = await databaseService.QueryAsync<TestIntegrationData>("concurrent_test", x => true);
                
                Assert.IsNotNull(data, $"Environment {env.Id} should have data");
                
                // Verify data isolation
                Assert.IsTrue(data.Email.Contains($"env{concurrentEnvironments.IndexOf(env)}"),
                    $"Data should match environment index {concurrentEnvironments.IndexOf(env)}");
            }

            // Cleanup
            foreach (var env in concurrentEnvironments)
            {
                await CleanupEnvironmentAsync(env);
            }
        }

        #endregion

        #region Environment Validation

        [TestMethod]
        public async Task TestEnvironment_Validation_ShouldVerifyIntegrity()
        {
            // Arrange
            var environment = await CreateIsolatedEnvironmentAsync();
            
            try
            {
                // Act
                var validationResult = await ValidateEnvironmentIntegrityAsync(environment);

                // Assert - Environment should be valid
                Assert.IsTrue(validationResult.IsValid, 
                    $"Environment should be valid: {string.Join(", ", validationResult.Errors)}");

                Assert.IsTrue(validationResult.DatabaseAccessible, "Database should be accessible");
                Assert.IsTrue(validationResult.FileSystemWritable, "File system should be writable");
                Assert.IsTrue(validationResult.LoggingWorking, "Logging should be working");
                Assert.IsTrue(validationResult.ServicesConfigured, "Services should be configured");
            }
            finally
            {
                await CleanupEnvironmentAsync(environment);
            }
        }

        private async Task<EnvironmentValidationResult> ValidateEnvironmentIntegrityAsync(TestEnvironment environment)
        {
            var result = new EnvironmentValidationResult();

            try
            {
                // Test database access
                var databaseService = environment.ServiceProvider.GetRequiredService<JsonDatabaseService>();
                await databaseService.QueryListAsync<TestIntegrationData>("test_users", x => x.Id == 1);
                result.DatabaseAccessible = true;
            }
            catch
            {
                result.DatabaseAccessible = false;
                result.Errors.Add("Database not accessible");
            }

            // Test file system write
            try
            {
                var testFile = Path.Combine(environment.RootPath, "integrity_test.txt");
                await File.WriteAllTextAsync(testFile, "test");
                File.Delete(testFile);
                result.FileSystemWritable = true;
            }
            catch
            {
                result.FileSystemWritable = false;
                result.Errors.Add("File system not writable");
            }

            // Test logging
            try
            {
                var loggerFactory = environment.ServiceProvider.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger<IntegrationTestEnvironment>();
                logger.LogDebug("Environment integrity test logging");
                result.LoggingWorking = true;
            }
            catch
            {
                result.LoggingWorking = false;
                result.Errors.Add("Logging not working");
            }

            // Test service configuration
            try
            {
                var services = new[]
                {
                    typeof(JsonDatabaseService),
                    typeof(ApiKeyManagementService),
                    typeof(WhisperKey.Services.Validation.InputValidationService),
                    typeof(RecoveryPolicyService)
                };

                foreach (var serviceType in services)
                {
                    var service = environment.ServiceProvider.GetService(serviceType);
                    if (service == null)
                    {
                        result.ServicesConfigured = false;
                        result.Errors.Add($"Service {serviceType.Name} not configured");
                        return result;
                    }
                }

                result.ServicesConfigured = true;
            }
            catch
            {
                result.ServicesConfigured = false;
                result.Errors.Add("Services not configured");
            }

            result.IsValid = result.Errors.Count == 0;
            return result;
        }

        #endregion

        #region Test Environment Manager

        public static class TestEnvironmentManager
        {
            private static readonly Dictionary<string, TestEnvironment> _namedEnvironments = new();

            public static async Task<TestEnvironment> GetOrCreateNamedEnvironmentAsync(string name)
            {
                if (_namedEnvironments.TryGetValue(name, out var existing))
                {
                    return existing;
                }

                var environment = await CreateNamedEnvironmentAsync(name);
                _namedEnvironments[name] = environment;
                return environment;
            }

            private static async Task<TestEnvironment> CreateNamedEnvironmentAsync(string name)
            {
                var environmentId = $"{name}_{Guid.NewGuid():N}[..8]";
                var basePath = Path.Combine(Path.GetTempPath(), $"WhisperKey_NamedTest_{name}");
                
                Directory.CreateDirectory(basePath);

                var environment = new TestEnvironment
                {
                    Id = environmentId,
                    Name = name,
                    RootPath = basePath,
                    DataPath = Path.Combine(basePath, "Data"),
                    LogsPath = Path.Combine(basePath, "Logs"),
                    ConfigPath = Path.Combine(basePath, "Config"),
                    ServiceProvider = CreateServiceProviderForNamedEnvironment(environmentId, basePath, name)
                };

                await ConfigureNamedEnvironmentAsync(environment);
                return environment;
            }

            private static IServiceProvider CreateServiceProviderForNamedEnvironment(string environmentId, string basePath, string name)
            {
                var services = new ServiceCollection();

                services.AddLogging(builder =>
                {
                    builder.AddConsole();
                    builder.AddFile(Path.Combine(basePath, "Logs", $"named_{name}_{environmentId}.log"));
                });

                services.AddSingleton<IFileSystemService>(new IsolatedFileSystemService(basePath));
                services.AddSingleton<JsonDatabaseService>();
                services.AddSingleton<TestEnvironmentDataService>();

                return services.BuildServiceProvider();
            }

            private static async Task ConfigureNamedEnvironmentAsync(TestEnvironment environment)
            {
                Directory.CreateDirectory(environment.DataPath);
                Directory.CreateDirectory(environment.LogsPath);
                Directory.CreateDirectory(environment.ConfigPath);

                var config = new
                {
                    Name = environment.Name,
                    Type = "NamedTestEnvironment",
                    CreatedAt = DateTime.UtcNow
                };

                var configPath = Path.Combine(environment.ConfigPath, "named_config.json");
                var configJson = System.Text.Json.JsonSerializer.Serialize(config, new System.Text.Json.JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                await File.WriteAllTextAsync(configPath, configJson);
            }

            public static async Task CleanupNamedEnvironmentAsync(string name)
            {
                if (_namedEnvironments.TryGetValue(name, out var environment))
                {
                    await CleanupEnvironmentAsync(environment);
                    _namedEnvironments.Remove(name);
                }
            }

            private static async Task CleanupEnvironmentAsync(TestEnvironment environment)
            {
                environment.ServiceProvider?.Dispose();
                
                try
                {
                    if (Directory.Exists(environment.RootPath))
                    {
                        Directory.Delete(environment.RootPath, true);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Warning: Failed to clean up named environment {environment.Id}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Helper Classes

        private class TestEnvironment
        {
            public string Id { get; set; } = string.Empty;
            public string? Name { get; set; }
            public string RootPath { get; set; } = string.Empty;
            public string DataPath { get; set; } = string.Empty;
            public string LogsPath { get; set; } = string.Empty;
            public string ConfigPath { get; set; } = string.Empty;
            public IServiceProvider? ServiceProvider { get; set; }
        }

        private class TestIntegrationData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public bool Active { get; set; }
        }

        private class EnvironmentValidationResult
        {
            public bool IsValid { get; set; }
            public bool DatabaseAccessible { get; set; }
            public bool FileSystemWritable { get; set; }
            public bool LoggingWorking { get; set; }
            public bool ServicesConfigured { get; set; }
            public List<string> Errors { get; set; } = new();
        }

        private class IsolatedFileSystemService : IFileSystemService
        {
            private readonly string _basePath;

            public IsolatedFileSystemService(string basePath)
            {
                _basePath = basePath;
            }

            public string GetAppDataPath() => _basePath;
            public string CombinePath(params string[] paths) => Path.Combine(paths);

            public Task<bool> FileExistsAsync(string path) => Task.FromResult(File.Exists(Path.Combine(_basePath, path)));
            public Task<string> ReadAllTextAsync(string path) => File.ReadAllTextAsync(Path.Combine(_basePath, path));
            public Task WriteAllTextAsync(string path, string contents) => File.WriteAllTextAsync(Path.Combine(_basePath, path), contents);
            public Task WriteAllBytesAsync(string path, byte[] bytes) => File.WriteAllBytesAsync(Path.Combine(_basePath, path), bytes);

            // Other IFileSystemService members would need to be implemented similarly
            public void CreateDirectory(string path) => Directory.CreateDirectory(Path.Combine(_basePath, path));
            public void DeleteFile(string path) => File.Delete(Path.Combine(_basePath, path));
            public Task<bool> DirectoryExistsAsync(string path) => Task.FromResult(Directory.Exists(Path.Combine(_basePath, path)));
        }

        private class TestEnvironmentDataService
        {
            public async Task<bool> ValidateEnvironmentAsync()
            {
                // Custom validation logic for test environment
                await Task.Delay(50); // Simulate validation work
                return true;
            }
        }

        private class TestMockService
        {
            public async Task<bool> IsHealthyAsync()
            {
                await Task.Delay(10);
                return true;
            }
        }

        #endregion
    }
}
