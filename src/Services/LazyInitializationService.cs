using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace WhisperKey.Services
{
    /// <summary>
    /// Implementation of lazy initialization and deferred task service
    /// </summary>
    public class LazyInitializationService : ILazyInitializationService
    {
        private readonly ILogger<LazyInitializationService> _logger;
        private readonly ConcurrentDictionary<string, DeferredTaskInfo> _tasks = new();
        private readonly ConcurrentDictionary<string, bool> _initializedComponents = new();
        private bool _initStarted = false;

        public LazyInitializationService(ILogger<LazyInitializationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void RegisterDeferredTask(string name, Func<Task> task, DeferredPriority priority = DeferredPriority.Normal)
        {
            _tasks[name] = new DeferredTaskInfo { Name = name, Task = task, Priority = priority };
            _logger.LogDebug("Registered deferred task: {Name} ({Priority})", name, priority);
        }

        public async Task PreloadResourceAsync(string resourceName, Func<Task> preloadAction)
        {
            _logger.LogInformation("Background preloading started for: {Resource}", resourceName);
            try
            {
                await preloadAction();
                _initializedComponents[resourceName] = true;
                _logger.LogInformation("Preloading completed for: {Resource}", resourceName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to preload resource: {Resource}", resourceName);
            }
        }

        public void StartDeferredInitialization()
        {
            if (_initStarted) return;
            _initStarted = true;

            _logger.LogInformation("Starting deferred initialization of non-critical components...");

            // Process tasks by priority
            _ = Task.Run(async () =>
            {
                var orderedTasks = _tasks.Values
                    .OrderByDescending(t => t.Priority)
                    .ToList();

                foreach (var info in orderedTasks)
                {
                    try
                    {
                        _logger.LogDebug("Executing deferred task: {Name}", info.Name);
                        await info.Task();
                        _initializedComponents[info.Name] = true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Deferred task {Name} failed", info.Name);
                    }
                }

                _logger.LogInformation("All deferred initialization tasks completed.");
            });
        }

        public bool IsInitialized(string componentName)
        {
            return _initializedComponents.GetValueOrDefault(componentName, false);
        }

        private class DeferredTaskInfo
        {
            public string Name { get; set; } = string.Empty;
            public Func<Task> Task { get; set; } = null!;
            public DeferredPriority Priority { get; set; }
        }
    }
}
