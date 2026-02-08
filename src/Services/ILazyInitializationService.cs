using System;
using System.Threading.Tasks;

namespace WhisperKey.Services
{
    /// <summary>
    /// Interface for managing deferred component initialization and resource preloading
    /// </summary>
    public interface ILazyInitializationService
    {
        /// <summary>
        /// Registers a task to be executed after the main application has started
        /// </summary>
        void RegisterDeferredTask(string name, Func<Task> task, DeferredPriority priority = DeferredPriority.Normal);
        
        /// <summary>
        /// Triggers preloading of a heavy resource in the background
        /// </summary>
        Task PreloadResourceAsync(string resourceName, Func<Task> preloadAction);
        
        /// <summary>
        /// Starts the execution of all deferred tasks
        /// </summary>
        void StartDeferredInitialization();
        
        /// <summary>
        /// Checks if a specific component has finished its lazy initialization
        /// </summary>
        bool IsInitialized(string componentName);
    }

    public enum DeferredPriority
    {
        Low,
        Normal,
        High
    }
}
