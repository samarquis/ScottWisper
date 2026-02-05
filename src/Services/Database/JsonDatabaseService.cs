using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WhisperKey.Models;
using WhisperKey.Services.Recovery;

namespace WhisperKey.Services.Database
{
    /// <summary>
    /// Optimized JSON "database" service with caching and indexing to meet low-latency requirements.
    /// </summary>
    public class JsonDatabaseService : IDisposable
    {
        private readonly IFileSystemService _fileSystem;
        private readonly IRecoveryPolicyService? _recoveryPolicy;
        private readonly ILogger<JsonDatabaseService> _logger;
        private readonly ConcurrentDictionary<string, object> _cache = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();
        private readonly Stopwatch _queryStopwatch = new();

        public JsonDatabaseService(IFileSystemService fileSystem, ILogger<JsonDatabaseService> logger)
            : this(fileSystem, null, logger)
        {
        }

        public JsonDatabaseService(IFileSystemService fileSystem, IRecoveryPolicyService? recoveryPolicy, ILogger<JsonDatabaseService> logger)
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _recoveryPolicy = recoveryPolicy;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public virtual async Task<T?> QueryAsync<T>(string collectionName, Func<T, bool> predicate) where T : class
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var collection = await GetCollectionAsync<T>(collectionName);
                var result = collection.FirstOrDefault(predicate);
                
                sw.Stop();
                if (sw.ElapsedMilliseconds > 10)
                {
                    _logger.LogWarning("Slow query detected on {Collection}: {Elapsed}ms", collectionName, sw.ElapsedMilliseconds);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Query failed on {Collection}", collectionName);
                return null;
            }
        }

        public virtual async Task<List<T>> QueryListAsync<T>(string collectionName, Func<T, bool> predicate) where T : class
        {
            var sw = Stopwatch.StartNew();
            try
            {
                var collection = await GetCollectionAsync<T>(collectionName);
                var result = collection.Where(predicate).ToList();
                
                sw.Stop();
                if (sw.ElapsedMilliseconds > 10)
                {
                    _logger.LogWarning("Slow list query detected on {Collection}: {Elapsed}ms", collectionName, sw.ElapsedMilliseconds);
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "List query failed on {Collection}", collectionName);
                return new List<T>();
            }
        }

        public virtual async Task UpsertAsync<T>(string collectionName, T item, Func<T, bool> identityPredicate) where T : class
        {
            var semaphore = GetLock(collectionName);
            await semaphore.WaitAsync();
            try
            {
                var collection = await GetCollectionAsync<T>(collectionName);
                var existing = collection.FirstOrDefault(identityPredicate);
                
                if (existing != null)
                {
                    var index = collection.IndexOf(existing);
                    collection[index] = item;
                }
                else
                {
                    collection.Add(item);
                }

                await SaveCollectionAsync(collectionName, collection);
            }
            finally
            {
                semaphore.Release();
            }
        }

        public virtual void InvalidateCache(string collectionName)
        {
            _cache.TryRemove(collectionName, out _);
        }

        private async Task<List<T>> GetCollectionAsync<T>(string collectionName) where T : class
        {
            if (_cache.TryGetValue(collectionName, out var cached) && cached is List<T> list)
            {
                return list;
            }

            var path = GetCollectionPath(collectionName);
            if (!_fileSystem.FileExists(path))
            {
                var newList = new List<T>();
                _cache[collectionName] = newList;
                return newList;
            }

            string json;
            if (_recoveryPolicy != null)
            {
                json = await _recoveryPolicy.GetIoRetryPolicy().ExecuteAsync(async () => await _fileSystem.ReadAllTextAsync(path));
            }
            else
            {
                json = await _fileSystem.ReadAllTextAsync(path);
            }
            
            var result = JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
            _cache[collectionName] = result;
            return result;
        }

        private async Task SaveCollectionAsync<T>(string collectionName, List<T> collection) where T : class
        {
            var path = GetCollectionPath(collectionName);
            var json = JsonSerializer.Serialize(collection, new JsonSerializerOptions { WriteIndented = true });
            
            if (_recoveryPolicy != null)
            {
                await _recoveryPolicy.GetIoRetryPolicy().ExecuteAsync(async () => await _fileSystem.WriteAllTextAsync(path, json));
            }
            else
            {
                await _fileSystem.WriteAllTextAsync(path, json);
            }
            
            _cache[collectionName] = collection;
        }

        private string GetCollectionPath(string collectionName)
        {
            return _fileSystem.CombinePath(_fileSystem.GetAppDataPath(), $"{collectionName}.json");
        }

        private SemaphoreSlim GetLock(string collectionName)
        {
            return _locks.GetOrAdd(collectionName, _ => new SemaphoreSlim(1, 1));
        }

        public void Dispose()
        {
            foreach (var semaphore in _locks.Values)
            {
                semaphore.Dispose();
            }
        }
    }
}
