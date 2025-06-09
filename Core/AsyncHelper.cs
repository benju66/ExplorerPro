using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Centralized async utilities for proper async/await patterns throughout the application.
    /// Prevents UI freezes, deadlocks, and provides safe fire-and-forget execution.
    /// </summary>
    public static class AsyncHelper
    {
        private static readonly ILogger _logger = CreateLogger();
        
        private static ILogger CreateLogger()
        {
            try
            {
                // Try to get logger from the shared factory if available
                return ExplorerPro.UI.MainWindow.MainWindow.SharedLoggerFactory?.CreateLogger(nameof(AsyncHelper)) 
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            }
            catch
            {
                // Fallback to null logger if creation fails
                return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            }
        }

        /// <summary>
        /// Safely executes a fire-and-forget async operation with proper error handling.
        /// Prevents async void and unhandled exceptions in background tasks.
        /// </summary>
        /// <param name="task">The async task to execute</param>
        /// <param name="onException">Optional exception handler</param>
        /// <param name="callerName">Automatically captured caller name for logging</param>
        public static async Task SafeFireAndForgetAsync(
            Task task, 
            Action<Exception> onException = null,
            [CallerMemberName] string callerName = "")
        {
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    onException?.Invoke(ex);
                }
                catch (Exception handlerEx)
                {
                    // Log exception handler failures
                    LogError($"Exception handler failed in {callerName}", handlerEx);
                }
                
                LogError($"Fire-and-forget task failed in {callerName}", ex);
            }
        }

        /// <summary>
        /// Safely executes a fire-and-forget async operation with proper error handling.
        /// Overload for Func&lt;Task&gt; to enable lazy evaluation.
        /// </summary>
        public static async Task SafeFireAndForgetAsync(
            Func<Task> taskFactory, 
            Action<Exception> onException = null,
            [CallerMemberName] string callerName = "")
        {
            try
            {
                await taskFactory().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                try
                {
                    onException?.Invoke(ex);
                }
                catch (Exception handlerEx)
                {
                    LogError($"Exception handler failed in {callerName}", handlerEx);
                }
                
                LogError($"Fire-and-forget task failed in {callerName}", ex);
            }
        }
        
        /// <summary>
        /// Executes an async operation with a timeout.
        /// Prevents hanging operations from blocking indefinitely.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="task">The task to execute</param>
        /// <param name="timeout">Maximum time to wait</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>The task result</returns>
        /// <exception cref="TimeoutException">Thrown when the operation times out</exception>
        public static async Task<T> WithTimeout<T>(
            Task<T> task, 
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            try
            {
                return await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Operation timed out after {timeout}");
            }
        }

        /// <summary>
        /// Executes an async operation with a timeout (no return value).
        /// </summary>
        public static async Task WithTimeout(
            Task task, 
            TimeSpan timeout,
            CancellationToken cancellationToken = default)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeout);
            
            try
            {
                await task.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException($"Operation timed out after {timeout}");
            }
        }

        /// <summary>
        /// Safely executes an async operation on the UI thread.
        /// Prevents deadlocks when marshaling back to the UI thread.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="asyncAction">The async action to execute on the UI thread</param>
        /// <param name="priority">Dispatcher priority</param>
        /// <returns>The result of the operation</returns>
        public static Task<T> ExecuteOnUIThreadAsync<T>(
            Func<Task<T>> asyncAction,
            DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                throw new InvalidOperationException("No UI dispatcher available");

            if (dispatcher.CheckAccess())
                return asyncAction();
                
            var tcs = new TaskCompletionSource<T>();
            
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    var result = await asyncAction().ConfigureAwait(true);
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, priority);
            
            return tcs.Task;
        }

        /// <summary>
        /// Safely executes an async operation on the UI thread (no return value).
        /// </summary>
        public static Task ExecuteOnUIThreadAsync(
            Func<Task> asyncAction,
            DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                throw new InvalidOperationException("No UI dispatcher available");

            if (dispatcher.CheckAccess())
                return asyncAction();
                
            var tcs = new TaskCompletionSource<object>();
            
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await asyncAction().ConfigureAwait(true);
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, priority);
            
            return tcs.Task;
        }

        /// <summary>
        /// Safely executes a synchronous operation on the UI thread.
        /// </summary>
        /// <typeparam name="T">The return type</typeparam>
        /// <param name="action">The action to execute on the UI thread</param>
        /// <param name="priority">Dispatcher priority</param>
        /// <returns>The result of the operation</returns>
        public static Task<T> ExecuteOnUIThreadAsync<T>(
            Func<T> action,
            DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                throw new InvalidOperationException("No UI dispatcher available");

            if (dispatcher.CheckAccess())
                return Task.FromResult(action());
                
            var tcs = new TaskCompletionSource<T>();
            
            dispatcher.InvokeAsync(() =>
            {
                try
                {
                    var result = action();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, priority);
            
            return tcs.Task;
        }

        /// <summary>
        /// Safely executes a synchronous operation on the UI thread (no return value).
        /// </summary>
        public static Task ExecuteOnUIThreadAsync(
            Action action,
            DispatcherPriority priority = DispatcherPriority.Normal)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
                throw new InvalidOperationException("No UI dispatcher available");

            if (dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }
                
            var tcs = new TaskCompletionSource<object>();
            
            dispatcher.InvokeAsync(() =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, priority);
            
            return tcs.Task;
        }

        /// <summary>
        /// Creates a task that can be safely used for fire-and-forget operations.
        /// Ensures exceptions are logged and don't crash the application.
        /// </summary>
        /// <param name="taskFactory">Factory to create the task</param>
        /// <param name="onException">Optional exception handler</param>
        /// <param name="callerName">Automatically captured caller name</param>
        /// <returns>A task that can be safely ignored</returns>
        public static Task CreateSafeTask(
            Func<Task> taskFactory,
            Action<Exception> onException = null,
            [CallerMemberName] string callerName = "")
        {
            return Task.Run(async () =>
            {
                await SafeFireAndForgetAsync(taskFactory, onException, callerName);
            });
        }

        /// <summary>
        /// Logs errors safely without throwing exceptions.
        /// </summary>
        private static void LogError(string message, Exception exception)
        {
            try
            {
                _logger.LogError(exception, message);
            }
            catch
            {
                // Fallback to debug output if logging fails
                System.Diagnostics.Debug.WriteLine($"[ERROR] {message}: {exception}");
            }
        }

        /// <summary>
        /// Extension method to make fire-and-forget safer.
        /// Usage: someTask.SafeFireAndForget(ex => HandleError(ex));
        /// </summary>
        public static void SafeFireAndForget(
            this Task task,
            Action<Exception> onException = null,
            [CallerMemberName] string callerName = "")
        {
            // Don't await this - it's fire and forget
            _ = SafeFireAndForgetAsync(task, onException, callerName);
        }
    }
} 