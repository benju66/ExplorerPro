using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Centralized shutdown manager to ensure proper application termination
    /// Handles timers, background threads, and resource cleanup
    /// </summary>
    public static class ShutdownManager
    {
        private static readonly ILogger _logger = CreateLogger();
        private static readonly List<IDisposable> _disposables = new List<IDisposable>();
        private static readonly List<DispatcherTimer> _dispatcherTimers = new List<DispatcherTimer>();
        private static readonly List<Timer> _systemTimers = new List<Timer>();
        private static readonly List<Task> _backgroundTasks = new List<Task>();
        private static readonly object _lock = new object();
        private static volatile bool _shutdownInProgress = false;
        private static volatile bool _emergencyShutdown = false;

        private static ILogger CreateLogger()
        {
            try
            {
                return ExplorerPro.UI.MainWindow.MainWindow.SharedLoggerFactory?.CreateLogger(nameof(ShutdownManager)) 
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            }
            catch
            {
                return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            }
        }

        /// <summary>
        /// Registers a disposable resource for cleanup during shutdown
        /// </summary>
        public static void RegisterDisposable(IDisposable disposable)
        {
            if (disposable == null) return;
            
            lock (_lock)
            {
                if (!_shutdownInProgress)
                {
                    _disposables.Add(disposable);
                }
            }
        }

        /// <summary>
        /// Registers a DispatcherTimer for shutdown cleanup
        /// </summary>
        public static void RegisterDispatcherTimer(DispatcherTimer timer)
        {
            if (timer == null) return;
            
            lock (_lock)
            {
                if (!_shutdownInProgress)
                {
                    _dispatcherTimers.Add(timer);
                }
            }
        }

        /// <summary>
        /// Registers a System.Threading.Timer for shutdown cleanup
        /// </summary>
        public static void RegisterSystemTimer(Timer timer)
        {
            if (timer == null) return;
            
            lock (_lock)
            {
                if (!_shutdownInProgress)
                {
                    _systemTimers.Add(timer);
                }
            }
        }

        /// <summary>
        /// Registers a background task for shutdown monitoring
        /// </summary>
        public static void RegisterBackgroundTask(Task task)
        {
            if (task == null) return;
            
            lock (_lock)
            {
                if (!_shutdownInProgress && !task.IsCompleted)
                {
                    _backgroundTasks.Add(task);
                }
            }
        }

        /// <summary>
        /// Initiates graceful shutdown of all registered resources
        /// </summary>
        public static async Task InitiateShutdownAsync()
        {
            if (_shutdownInProgress) return;
            
            lock (_lock)
            {
                _shutdownInProgress = true;
            }

            try
            {
                _logger.LogInformation("Initiating graceful shutdown...");

                // Stop all timers first
                await StopAllTimersAsync();

                // Wait for background tasks with timeout
                await WaitForBackgroundTasksAsync(TimeSpan.FromSeconds(5));

                // Dispose all registered resources
                await DisposeAllResourcesAsync();

                _logger.LogInformation("Graceful shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during graceful shutdown");
                await EmergencyShutdownAsync();
            }
        }

        /// <summary>
        /// Emergency shutdown - forces termination of all resources
        /// </summary>
        public static async Task EmergencyShutdownAsync()
        {
            if (_emergencyShutdown) return;
            
            _emergencyShutdown = true;
            _logger.LogWarning("Initiating emergency shutdown...");

            try
            {
                // Force stop all timers without waiting
                ForceStopAllTimers();

                // Cancel all background tasks
                CancelAllBackgroundTasks();

                // Force dispose all resources
                ForceDisposeAllResources();

                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                _logger.LogWarning("Emergency shutdown completed");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Critical error during emergency shutdown");
            }
        }

        /// <summary>
        /// Forces immediate application termination
        /// </summary>
        public static void ForceExit(int exitCode = 0)
        {
            try
            {
                _logger.LogCritical($"Force exit called with code {exitCode}");
                
                // Try graceful WPF shutdown first
                if (Application.Current != null)
                {
                    Application.Current.Shutdown(exitCode);
                }
            }
            catch
            {
                // If WPF shutdown fails, force process termination
                Environment.Exit(exitCode);
            }
        }

        private static async Task StopAllTimersAsync()
        {
            var tasks = new List<Task>();

            // Stop DispatcherTimers on UI thread
            if (_dispatcherTimers.Count > 0)
            {
                tasks.Add(Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    foreach (var timer in _dispatcherTimers.ToList())
                    {
                        try
                        {
                            timer.Stop();
                            _logger.LogDebug("Stopped DispatcherTimer");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Error stopping DispatcherTimer");
                        }
                    }
                }).Task ?? Task.CompletedTask);
            }

            // Stop system timers
            tasks.Add(Task.Run(() =>
            {
                foreach (var timer in _systemTimers.ToList())
                {
                    try
                    {
                        timer.Change(Timeout.Infinite, Timeout.Infinite);
                        _logger.LogDebug("Stopped System Timer");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error stopping System Timer");
                    }
                }
            }));

            await Task.WhenAll(tasks);
        }

        private static async Task WaitForBackgroundTasksAsync(TimeSpan timeout)
        {
            if (_backgroundTasks.Count == 0) return;

            try
            {
                var completedTasks = await Task.WhenAny(
                    Task.WhenAll(_backgroundTasks.Where(t => !t.IsCompleted)),
                    Task.Delay(timeout)
                );

                _logger.LogInformation($"Background task wait completed: {_backgroundTasks.Count(t => t.IsCompleted)} of {_backgroundTasks.Count} completed");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error waiting for background tasks");
            }
        }

        private static async Task DisposeAllResourcesAsync()
        {
            var disposeTasks = _disposables.Select(async disposable =>
            {
                try
                {
                    if (disposable is IAsyncDisposable asyncDisposable)
                    {
                        await asyncDisposable.DisposeAsync();
                    }
                    else
                    {
                        disposable.Dispose();
                    }
                    _logger.LogDebug($"Disposed resource: {disposable.GetType().Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, $"Error disposing resource: {disposable.GetType().Name}");
                }
            });

            await Task.WhenAll(disposeTasks);
        }

        private static void ForceStopAllTimers()
        {
            // Force stop DispatcherTimers
            foreach (var timer in _dispatcherTimers.ToList())
            {
                try
                {
                    timer.Stop();
                }
                catch { /* Ignore errors during force stop */ }
            }

            // Force stop system timers
            foreach (var timer in _systemTimers.ToList())
            {
                try
                {
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
                    timer.Dispose();
                }
                catch { /* Ignore errors during force stop */ }
            }
        }

        private static void CancelAllBackgroundTasks()
        {
            foreach (var task in _backgroundTasks.ToList())
            {
                try
                {
                    if (!task.IsCompleted && !task.IsCanceled)
                    {
                        // We can't cancel tasks that don't support cancellation
                        // Just wait a brief moment for them to complete
                        task.Wait(100);
                    }
                }
                catch { /* Ignore errors during cancellation */ }
            }
        }

        private static void ForceDisposeAllResources()
        {
            foreach (var disposable in _disposables.ToList())
            {
                try
                {
                    disposable.Dispose();
                }
                catch { /* Ignore errors during force dispose */ }
            }
        }

        /// <summary>
        /// Gets shutdown statistics for debugging
        /// </summary>
        public static ShutdownStatistics GetStatistics()
        {
            lock (_lock)
            {
                return new ShutdownStatistics
                {
                    RegisteredDisposables = _disposables.Count,
                    RegisteredDispatcherTimers = _dispatcherTimers.Count,
                    RegisteredSystemTimers = _systemTimers.Count,
                    RegisteredBackgroundTasks = _backgroundTasks.Count,
                    ShutdownInProgress = _shutdownInProgress,
                    EmergencyShutdown = _emergencyShutdown
                };
            }
        }

        /// <summary>
        /// Clears all registered resources (for testing)
        /// </summary>
        public static void ClearAll()
        {
            lock (_lock)
            {
                _disposables.Clear();
                _dispatcherTimers.Clear();
                _systemTimers.Clear();
                _backgroundTasks.Clear();
                _shutdownInProgress = false;
                _emergencyShutdown = false;
            }
        }
    }

    /// <summary>
    /// Statistics about the shutdown manager state
    /// </summary>
    public class ShutdownStatistics
    {
        public int RegisteredDisposables { get; set; }
        public int RegisteredDispatcherTimers { get; set; }
        public int RegisteredSystemTimers { get; set; }
        public int RegisteredBackgroundTasks { get; set; }
        public bool ShutdownInProgress { get; set; }
        public bool EmergencyShutdown { get; set; }

        public override string ToString()
        {
            return $"Disposables: {RegisteredDisposables}, DispatcherTimers: {RegisteredDispatcherTimers}, " +
                   $"SystemTimers: {RegisteredSystemTimers}, BackgroundTasks: {RegisteredBackgroundTasks}, " +
                   $"ShutdownInProgress: {ShutdownInProgress}, EmergencyShutdown: {EmergencyShutdown}";
        }
    }
} 