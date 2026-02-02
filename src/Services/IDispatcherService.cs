using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace WhisperKey.Services
{
    /// <summary>
    /// Represents the priority for dispatcher operations.
    /// </summary>
    public enum DispatcherPriorityLevel
    {
        /// <summary>
        /// Operations are processed when the system is idle.
        /// </summary>
        Idle,

        /// <summary>
        /// Operations are processed at background priority.
        /// </summary>
        Background,

        /// <summary>
        /// Operations are processed at normal priority.
        /// </summary>
        Normal,

        /// <summary>
        /// Operations are processed at render priority.
        /// </summary>
        Render,

        /// <summary>
        /// Operations are processed at data bind priority.
        /// </summary>
        DataBind,

        /// <summary>
        /// Operations are processed at send priority (immediately).
        /// </summary>
        Send
    }

    /// <summary>
    /// Interface for WPF Dispatcher operations to enable testing without UI thread dependencies.
    /// This abstraction allows unit tests to execute dispatcher operations synchronously or with mocks.
    /// </summary>
    public interface IDispatcherService
    {
        /// <summary>
        /// Gets whether the current thread is the dispatcher thread.
        /// </summary>
        bool CheckAccess();

        /// <summary>
        /// Verifies that the current thread is the dispatcher thread.
        /// </summary>
        void VerifyAccess();

        /// <summary>
        /// Executes an action on the dispatcher thread.
        /// </summary>
        void Invoke(Action action, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal);

        /// <summary>
        /// Executes a function on the dispatcher thread and returns the result.
        /// </summary>
        T Invoke<T>(Func<T> func, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal);

        /// <summary>
        /// Executes an action on the dispatcher thread asynchronously.
        /// </summary>
        Task InvokeAsync(Action action, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal);

        /// <summary>
        /// Executes a function on the dispatcher thread asynchronously and returns the result.
        /// </summary>
        Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal);

        /// <summary>
        /// Executes an action on the dispatcher thread after a delay.
        /// </summary>
        void DelayInvoke(Action action, TimeSpan delay);

        /// <summary>
        /// Starts a timer that executes an action at the specified interval.
        /// </summary>
        IDisposable StartTimer(Action callback, TimeSpan interval);
    }

    /// <summary>
    /// Real implementation of IDispatcherService that uses the WPF Dispatcher.
    /// This is the production implementation that runs on the actual UI thread.
    /// </summary>
    public class DispatcherService : IDispatcherService
    {
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// Creates a new DispatcherService using the current application's dispatcher.
        /// </summary>
        public DispatcherService() : this(Application.Current.Dispatcher)
        {
        }

        /// <summary>
        /// Creates a new DispatcherService with the specified dispatcher.
        /// </summary>
        public DispatcherService(Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        public bool CheckAccess()
        {
            return _dispatcher.CheckAccess();
        }

        public void VerifyAccess()
        {
            _dispatcher.VerifyAccess();
        }

        public void Invoke(Action action, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var wpfPriority = ConvertPriority(priority);
            _dispatcher.Invoke(action, wpfPriority);
        }

        public T Invoke<T>(Func<T> func, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            var wpfPriority = ConvertPriority(priority);
            return _dispatcher.Invoke(func, wpfPriority);
        }

        public async Task InvokeAsync(Action action, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var wpfPriority = ConvertPriority(priority);
            await _dispatcher.InvokeAsync(action, wpfPriority);
        }

        public async Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            var wpfPriority = ConvertPriority(priority);
            return await _dispatcher.InvokeAsync(func, wpfPriority);
        }

        public void DelayInvoke(Action action, TimeSpan delay)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            var timer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
            {
                Interval = delay
            };

            timer.Tick += (s, e) =>
            {
                timer.Stop();
                action();
            };

            timer.Start();
        }

        public IDisposable StartTimer(Action callback, TimeSpan interval)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            var timer = new DispatcherTimer(DispatcherPriority.Background, _dispatcher)
            {
                Interval = interval
            };

            timer.Tick += (s, e) => callback();
            timer.Start();

            return new DisposableTimer(timer);
        }

        private static DispatcherPriority ConvertPriority(DispatcherPriorityLevel priority)
        {
            return priority switch
            {
                DispatcherPriorityLevel.Idle => DispatcherPriority.ApplicationIdle,
                DispatcherPriorityLevel.Background => DispatcherPriority.Background,
                DispatcherPriorityLevel.Normal => DispatcherPriority.Normal,
                DispatcherPriorityLevel.Render => DispatcherPriority.Render,
                DispatcherPriorityLevel.DataBind => DispatcherPriority.DataBind,
                DispatcherPriorityLevel.Send => DispatcherPriority.Send,
                _ => DispatcherPriority.Normal
            };
        }

        /// <summary>
        /// Disposable wrapper for dispatcher timers.
        /// </summary>
        private class DisposableTimer : IDisposable
        {
            private readonly DispatcherTimer _timer;
            private bool _disposed;

            public DisposableTimer(DispatcherTimer timer)
            {
                _timer = timer ?? throw new ArgumentNullException(nameof(timer));
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    _timer.Stop();
                    _disposed = true;
                }
            }
        }
    }

    /// <summary>
    /// Synchronous implementation of IDispatcherService for unit testing.
    /// This implementation executes actions immediately on the current thread,
    /// making it suitable for tests that don't require actual UI thread marshaling.
    /// </summary>
    public class SynchronousDispatcherService : IDispatcherService
    {
        public bool CheckAccess()
        {
            return true;
        }

        public void VerifyAccess()
        {
            // Always succeeds in synchronous mode
        }

        public void Invoke(Action action, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            action();
        }

        public T Invoke<T>(Func<T> func, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            return func();
        }

        public Task InvokeAsync(Action action, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            action();
            return Task.CompletedTask;
        }

        public Task<T> InvokeAsync<T>(Func<T> func, DispatcherPriorityLevel priority = DispatcherPriorityLevel.Normal)
        {
            if (func == null)
                throw new ArgumentNullException(nameof(func));
            return Task.FromResult(func());
        }

        public void DelayInvoke(Action action, TimeSpan delay)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));
            // In synchronous mode, we execute immediately or could use Task.Delay
            Task.Run(async () =>
            {
                await Task.Delay(delay);
                action();
            });
        }

        public IDisposable StartTimer(Action callback, TimeSpan interval)
        {
            if (callback == null)
                throw new ArgumentNullException(nameof(callback));
            // In synchronous mode, return a no-op timer
            return new NoOpDisposable();
        }

        private class NoOpDisposable : IDisposable
        {
            public void Dispose()
            {
                // No-op
            }
        }
    }
}
