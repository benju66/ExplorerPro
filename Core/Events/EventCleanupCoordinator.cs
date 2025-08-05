using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.Telemetry;
using ExplorerPro.Core.Configuration;
using ExplorerPro.Core.Monitoring;

namespace ExplorerPro.Core.Events
{
    /// <summary>
    /// PHASE 1 FIX 3: Global coordinator for all EventCleanupManager instances
    /// Provides centralized management, health monitoring, and bulk operations
    /// Features: Performance tracking, memory leak detection, automatic cleanup
    /// </summary>
    public class EventCleanupCoordinator : IDisposable
    {
        #region Private Fields
        
        private static EventCleanupCoordinator _instance;
        private static readonly object _instanceLock = new object();
        
        private readonly ILogger<EventCleanupCoordinator> _logger;
        private readonly IExtendedTelemetryService _telemetryService;
        private readonly ResourceMonitor _resourceMonitor;
        
        private readonly ConcurrentDictionary<string, EventCleanupManager> _managers;
        private readonly Timer _healthCheckTimer;
        private readonly Timer _memoryCheckTimer;
        
        // Global statistics
        private long _totalRegistrations = 0;
        private long _totalCleanups = 0;
        private long _totalMemoryFreed = 0;
        private int _totalFailures = 0;
        
        // Configuration
        private readonly TimeSpan _healthCheckInterval = TimeSpan.FromMinutes(2);
        private readonly TimeSpan _memoryCheckInterval = TimeSpan.FromMinutes(1);
        
        private bool _disposed = false;
        
        #endregion

        #region Constructor & Singleton
        
        private EventCleanupCoordinator(
            ILogger<EventCleanupCoordinator> logger = null,
            IExtendedTelemetryService telemetryService = null,
            ResourceMonitor resourceMonitor = null)
        {
            _logger = logger;
            _telemetryService = telemetryService;
            _resourceMonitor = resourceMonitor;
            
            _managers = new ConcurrentDictionary<string, EventCleanupManager>();
            
            // Start health monitoring
            _healthCheckTimer = new Timer(HealthCheckCallback, null, _healthCheckInterval, _healthCheckInterval);
            _memoryCheckTimer = new Timer(MemoryCheckCallback, null, _memoryCheckInterval, _memoryCheckInterval);
            
            _logger?.LogInformation("EventCleanupCoordinator initialized");
        }
        
        public static EventCleanupCoordinator GetInstance(
            ILogger<EventCleanupCoordinator> logger = null,
            IExtendedTelemetryService telemetryService = null,
            ResourceMonitor resourceMonitor = null)
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = new EventCleanupCoordinator(logger, telemetryService, resourceMonitor);
                    }
                }
            }
            return _instance;
        }
        
        #endregion

        #region Public API
        
        /// <summary>
        /// Creates a new EventCleanupManager for a component
        /// </summary>
        public EventCleanupManager CreateManager(string componentName)
        {
            if (!FeatureFlags.UseEventCleanupManager)
            {
                // Return a no-op manager when disabled
                return new NoOpEventCleanupManager(componentName);
            }
            
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EventCleanupCoordinator));
            }
            
            if (string.IsNullOrEmpty(componentName))
            {
                throw new ArgumentException("Component name cannot be null or empty", nameof(componentName));
            }
            
            var logger = App.LoggerFactory?.CreateLogger<EventCleanupManager>();
            var manager = new EventCleanupManager(componentName, logger, _telemetryService);
            
            if (_managers.TryAdd(componentName, manager))
            {
                _logger?.LogDebug("Created EventCleanupManager for component: {ComponentName}", componentName);
                
                _telemetryService?.TrackEvent("EventCleanup.ManagerCreated", new Dictionary<string, object>
                {
                    ["ComponentName"] = componentName,
                    ["TotalManagers"] = _managers.Count
                });
            }
            else
            {
                _logger?.LogWarning("EventCleanupManager already exists for component: {ComponentName}", componentName);
                manager.Dispose(); // Dispose the duplicate
                manager = _managers[componentName]; // Return existing
            }
            
            return manager;
        }
        
        /// <summary>
        /// Gets an existing manager by component name
        /// </summary>
        public EventCleanupManager GetManager(string componentName)
        {
            if (!FeatureFlags.UseEventCleanupManager)
            {
                return new NoOpEventCleanupManager(componentName);
            }
            
            return _managers.TryGetValue(componentName, out var manager) ? manager : null;
        }
        
        /// <summary>
        /// Removes and disposes a manager
        /// </summary>
        public void RemoveManager(string componentName)
        {
            if (_managers.TryRemove(componentName, out var manager))
            {
                try
                {
                    manager.Dispose();
                    _logger?.LogDebug("Removed and disposed EventCleanupManager: {ComponentName}", componentName);
                    
                    _telemetryService?.TrackEvent("EventCleanup.ManagerRemoved", new Dictionary<string, object>
                    {
                        ["ComponentName"] = componentName,
                        ["RemainingManagers"] = _managers.Count
                    });
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error disposing EventCleanupManager: {ComponentName}", componentName);
                    _telemetryService?.TrackException(ex, $"EventCleanupCoordinator.RemoveManager.{componentName}");
                }
            }
        }
        
        /// <summary>
        /// Performs cleanup on all managers
        /// </summary>
        public async Task CleanupAllAsync()
        {
            if (_disposed) return;
            
            _logger?.LogInformation("Starting global event cleanup for {ManagerCount} managers", _managers.Count);
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var startMemory = GC.GetTotalMemory(false);
            
            var cleanupTasks = new List<Task>();
            var managersCopy = _managers.Values.ToArray();
            
            foreach (var manager in managersCopy)
            {
                cleanupTasks.Add(Task.Run(() =>
                {
                    try
                    {
                        manager.CleanupAll();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error during cleanup for manager");
                        Interlocked.Increment(ref _totalFailures);
                    }
                }));
            }
            
            await Task.WhenAll(cleanupTasks);
            
            // Force garbage collection and measure results
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            stopwatch.Stop();
            var endMemory = GC.GetTotalMemory(false);
            var memoryFreed = startMemory - endMemory;
            
            Interlocked.Add(ref _totalMemoryFreed, memoryFreed);
            
            _logger?.LogInformation("Global event cleanup completed: {ManagerCount} managers, {ElapsedMs}ms, {MemoryFreedMB:F2}MB freed",
                _managers.Count, stopwatch.ElapsedMilliseconds, memoryFreed / (1024.0 * 1024.0));
            
            _telemetryService?.TrackEvent("EventCleanup.GlobalCleanup", new Dictionary<string, object>
            {
                ["ManagerCount"] = _managers.Count,
                ["ElapsedMs"] = stopwatch.ElapsedMilliseconds,
                ["MemoryFreedBytes"] = memoryFreed,
                ["TotalMemoryFreedBytes"] = _totalMemoryFreed
            });
        }
        
        /// <summary>
        /// Gets global statistics across all managers
        /// </summary>
        public GlobalEventCleanupStats GetGlobalStats()
        {
            var managerStats = _managers.Values.Select(m => m.GetStats()).ToArray();
            
            return new GlobalEventCleanupStats
            {
                TotalManagers = _managers.Count,
                TotalRegistrations = managerStats.Sum(s => s.RegistrationCount),
                TotalCleanups = managerStats.Sum(s => s.CleanupCount),
                TotalActiveSubscriptions = managerStats.Sum(s => s.ActiveEventSubscriptions + s.ActiveRoutedEventSubscriptions + s.ActiveCustomCleanups),
                TotalMemoryFreedBytes = managerStats.Sum(s => s.MemoryFreedBytes),
                TotalFailures = managerStats.Sum(s => s.CleanupFailures),
                AverageCleanupTimeMs = managerStats.Where(s => s.CleanupCount > 0).Average(s => s.AverageCleanupTimeMs),
                ManagerDetails = managerStats.ToList()
            };
        }
        
        /// <summary>
        /// Checks for potential memory leaks across all managers
        /// </summary>
        public List<MemoryLeakWarning> CheckForMemoryLeaks()
        {
            var warnings = new List<MemoryLeakWarning>();
            var globalStats = GetGlobalStats();
            
            // Check for high memory usage
            if (globalStats.TotalActiveSubscriptions > 10000)
            {
                warnings.Add(new MemoryLeakWarning
                {
                    ComponentName = "Global",
                    WarningType = "HighSubscriptionCount",
                    Description = $"Very high number of active subscriptions: {globalStats.TotalActiveSubscriptions}",
                    Severity = MemoryLeakSeverity.High
                });
            }
            
            // Check individual managers
            foreach (var manager in _managers.Values)
            {
                var stats = manager.GetStats();
                var activeCount = stats.ActiveEventSubscriptions + stats.ActiveRoutedEventSubscriptions + stats.ActiveCustomCleanups;
                
                if (activeCount > 1000)
                {
                    warnings.Add(new MemoryLeakWarning
                    {
                        ComponentName = stats.ComponentName,
                        WarningType = "HighSubscriptionCount",
                        Description = $"High number of active subscriptions: {activeCount}",
                        Severity = MemoryLeakSeverity.Medium
                    });
                }
                
                if (stats.CleanupFailures > 0 && stats.CleanupCount > 0)
                {
                    var failureRate = (double)stats.CleanupFailures / stats.CleanupCount * 100.0;
                    if (failureRate > 5.0)
                    {
                        warnings.Add(new MemoryLeakWarning
                        {
                            ComponentName = stats.ComponentName,
                            WarningType = "HighFailureRate",
                            Description = $"High cleanup failure rate: {failureRate:F1}%",
                            Severity = MemoryLeakSeverity.Medium
                        });
                    }
                }
                
                if (stats.AverageCleanupTimeMs > 100.0)
                {
                    warnings.Add(new MemoryLeakWarning
                    {
                        ComponentName = stats.ComponentName,
                        WarningType = "SlowCleanup",
                        Description = $"Slow cleanup performance: {stats.AverageCleanupTimeMs:F1}ms average",
                        Severity = MemoryLeakSeverity.Low
                    });
                }
            }
            
            return warnings;
        }
        
        #endregion

        #region Private Implementation
        
        private void HealthCheckCallback(object state)
        {
            try
            {
                if (_disposed) return;
                
                var globalStats = GetGlobalStats();
                var warnings = CheckForMemoryLeaks();
                
                _logger?.LogInformation("Event cleanup health check: {Managers} managers, {ActiveSubscriptions} active subscriptions, {Warnings} warnings",
                    globalStats.TotalManagers, globalStats.TotalActiveSubscriptions, warnings.Count);
                
                _telemetryService?.TrackEvent("EventCleanup.HealthCheck", new Dictionary<string, object>
                {
                    ["TotalManagers"] = globalStats.TotalManagers,
                    ["TotalActiveSubscriptions"] = globalStats.TotalActiveSubscriptions,
                    ["TotalRegistrations"] = globalStats.TotalRegistrations,
                    ["TotalCleanups"] = globalStats.TotalCleanups,
                    ["TotalFailures"] = globalStats.TotalFailures,
                    ["WarningCount"] = warnings.Count,
                    ["HighSeverityWarnings"] = warnings.Count(w => w.Severity == MemoryLeakSeverity.High),
                    ["AverageCleanupTimeMs"] = globalStats.AverageCleanupTimeMs
                });
                
                // Log high-severity warnings
                foreach (var warning in warnings.Where(w => w.Severity == MemoryLeakSeverity.High))
                {
                    _logger?.LogWarning("Memory leak warning for {ComponentName}: {Description}", 
                        warning.ComponentName, warning.Description);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during event cleanup health check");
                _telemetryService?.TrackException(ex, "EventCleanupCoordinator.HealthCheck");
            }
        }
        
        private void MemoryCheckCallback(object state)
        {
            try
            {
                if (_disposed) return;
                
                var currentMemory = GC.GetTotalMemory(false);
                var resourceSnapshot = _resourceMonitor?.GetCurrentSnapshot();
                
                _telemetryService?.TrackEvent("EventCleanup.MemoryCheck", new Dictionary<string, object>
                {
                    ["TotalMemoryBytes"] = currentMemory,
                    ["WorkingSetMB"] = resourceSnapshot?.WorkingSetMB ?? -1,
                    ["PrivateMemoryMB"] = resourceSnapshot?.PrivateMemoryMB ?? -1,
                    ["ManagedMemoryMB"] = currentMemory / (1024.0 * 1024.0),
                    ["TotalMemoryFreedBytes"] = _totalMemoryFreed
                });
                
                // Check for concerning memory growth
                if (resourceSnapshot?.WorkingSetMB > 1000) // Alert if > 1GB
                {
                    _logger?.LogWarning("High memory usage detected: {WorkingSetMB:F1}MB working set", resourceSnapshot.WorkingSetMB);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during memory check");
            }
        }
        
        #endregion

        #region IDisposable
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _logger?.LogInformation("Disposing EventCleanupCoordinator with {ManagerCount} managers", _managers.Count);
            
            try
            {
                // Stop timers
                _healthCheckTimer?.Dispose();
                _memoryCheckTimer?.Dispose();
                
                // Cleanup all managers
                var disposalTasks = new List<Task>();
                foreach (var manager in _managers.Values)
                {
                    disposalTasks.Add(Task.Run(() =>
                    {
                        try
                        {
                            manager.Dispose();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error disposing manager during coordinator disposal");
                        }
                    }));
                }
                
                Task.WaitAll(disposalTasks.ToArray(), TimeSpan.FromSeconds(30));
                
                _managers.Clear();
                
                // Final telemetry
                var globalStats = GetGlobalStats();
                _telemetryService?.TrackEvent("EventCleanup.CoordinatorDisposed", new Dictionary<string, object>
                {
                    ["TotalRegistrations"] = _totalRegistrations,
                    ["TotalCleanups"] = _totalCleanups,
                    ["TotalMemoryFreedBytes"] = _totalMemoryFreed,
                    ["TotalFailures"] = _totalFailures
                });
                
                _disposed = true;
                
                _logger?.LogInformation("EventCleanupCoordinator disposed - Final stats: {Registrations} registrations, {Cleanups} cleanups, {MemoryFreedMB:F2}MB freed",
                    _totalRegistrations, _totalCleanups, _totalMemoryFreed / (1024.0 * 1024.0));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during EventCleanupCoordinator disposal");
                _telemetryService?.TrackException(ex, "EventCleanupCoordinator.Dispose");
            }
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    /// <summary>
    /// No-op implementation used when EventCleanupManager is disabled
    /// </summary>
    internal class NoOpEventCleanupManager : EventCleanupManager
    {
        public NoOpEventCleanupManager(string componentName) : base(componentName)
        {
        }
        
        // All methods are inherited but do nothing when feature is disabled
    }
    
    public class GlobalEventCleanupStats
    {
        public int TotalManagers { get; set; }
        public long TotalRegistrations { get; set; }
        public long TotalCleanups { get; set; }
        public int TotalActiveSubscriptions { get; set; }
        public long TotalMemoryFreedBytes { get; set; }
        public int TotalFailures { get; set; }
        public double AverageCleanupTimeMs { get; set; }
        public List<EventCleanupStats> ManagerDetails { get; set; } = new List<EventCleanupStats>();
    }
    
    public class MemoryLeakWarning
    {
        public string ComponentName { get; set; }
        public string WarningType { get; set; }
        public string Description { get; set; }
        public MemoryLeakSeverity Severity { get; set; }
    }
    
    public enum MemoryLeakSeverity
    {
        Low,
        Medium,
        High
    }
    
    #endregion
}