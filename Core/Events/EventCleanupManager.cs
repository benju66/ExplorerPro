using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.Telemetry;
using ExplorerPro.Core.Configuration;
using ExplorerPro.Core.Monitoring;

namespace ExplorerPro.Core.Events
{
    /// <summary>
    /// PHASE 1 FIX 3: Event cleanup manager for preventing memory leaks
    /// Replaces flawed weak event patterns with proper explicit cleanup management
    /// Features: Memory leak detection, performance monitoring, thread-safe operations
    /// </summary>
    public class EventCleanupManager : IDisposable
    {
        #region Private Fields
        
        private readonly ILogger<EventCleanupManager> _logger;
        private readonly IExtendedTelemetryService _telemetryService;
        private readonly string _componentName;
        
        private readonly List<EventSubscription> _eventSubscriptions;
        private readonly List<RoutedEventSubscription> _routedEventSubscriptions;
        private readonly List<CustomCleanupAction> _customCleanupActions;
        private readonly object _lock = new object();
        
        // Memory monitoring
        private long _memoryBeforeRegistration;
        private long _memoryAfterCleanup;
        private int _registrationCount;
        private int _cleanupCount;
        
        // Performance tracking
        private long _totalCleanupTimeMs;
        private int _cleanupFailures;
        
        private bool _disposed = false;
        
        #endregion

        #region Constructor
        
        public EventCleanupManager(
            string componentName,
            ILogger<EventCleanupManager> logger = null,
            IExtendedTelemetryService telemetryService = null)
        {
            _componentName = componentName ?? throw new ArgumentNullException(nameof(componentName));
            _logger = logger;
            _telemetryService = telemetryService;
            
            _eventSubscriptions = new List<EventSubscription>();
            _routedEventSubscriptions = new List<RoutedEventSubscription>();
            _customCleanupActions = new List<CustomCleanupAction>();
            
            // Capture initial memory baseline
            _memoryBeforeRegistration = GC.GetTotalMemory(false);
            
            _logger?.LogDebug("EventCleanupManager created for component: {ComponentName}", _componentName);
        }
        
        #endregion

        #region Public API
        
        /// <summary>
        /// Registers a regular event handler for cleanup
        /// </summary>
        public void RegisterEventHandler<TEventArgs>(
            object source,
            string eventName,
            EventHandler<TEventArgs> handler) where TEventArgs : EventArgs
        {
            if (!FeatureFlags.UseEventCleanupManager)
            {
                // Fallback to direct subscription when disabled
                RegisterEventHandlerDirect(source, eventName, handler);
                return;
            }
            
            if (_disposed)
            {
                _logger?.LogWarning("Attempted to register event handler on disposed EventCleanupManager: {ComponentName}", _componentName);
                return;
            }
            
            if (source == null || string.IsNullOrEmpty(eventName) || handler == null)
            {
                _logger?.LogWarning("Invalid parameters for event registration: source={Source}, eventName={EventName}, handler={Handler}",
                    source?.GetType().Name, eventName, handler?.Method.Name);
                return;
            }
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            lock (_lock)
            {
                try
                {
                    // Get event info
                    var eventInfo = source.GetType().GetEvent(eventName);
                    if (eventInfo == null)
                    {
                        _logger?.LogError("Event '{EventName}' not found on type {TypeName}", eventName, source.GetType().Name);
                        return;
                    }
                    
                    // Subscribe to the event
                    eventInfo.AddEventHandler(source, handler);
                    
                    // Create subscription record
                    var subscription = new EventSubscription
                    {
                        Source = new WeakReference(source),
                        EventName = eventName,
                        Handler = handler,
                        EventInfo = eventInfo,
                        RegisteredAt = DateTime.UtcNow,
                        SubscriptionId = Guid.NewGuid().ToString("N")[..8]
                    };
                    
                    _eventSubscriptions.Add(subscription);
                    _registrationCount++;
                    
                    stopwatch.Stop();
                    
                    _logger?.LogDebug("Registered event handler: {ComponentName}.{EventName} [{SubscriptionId}] in {ElapsedMs}ms",
                        _componentName, eventName, subscription.SubscriptionId, stopwatch.ElapsedMilliseconds);
                    
                    // Track telemetry and check for potential issues
                    TrackRegistrationTelemetry(eventName, stopwatch.ElapsedMilliseconds);
                    CheckMemoryHealth();
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger?.LogError(ex, "Failed to register event handler: {ComponentName}.{EventName}", _componentName, eventName);
                    _telemetryService?.TrackException(ex, $"EventCleanupManager.RegisterEventHandler.{_componentName}");
                }
            }
        }
        
        /// <summary>
        /// Registers a routed event handler for cleanup
        /// </summary>
        public void RegisterRoutedEventHandler(
            System.Windows.UIElement element,
            System.Windows.RoutedEvent routedEvent,
            System.Windows.RoutedEventHandler handler)
        {
            if (!FeatureFlags.UseEventCleanupManager)
            {
                // Fallback to direct subscription when disabled
                element?.AddHandler(routedEvent, handler);
                return;
            }
            
            if (_disposed)
            {
                _logger?.LogWarning("Attempted to register routed event handler on disposed EventCleanupManager: {ComponentName}", _componentName);
                return;
            }
            
            if (element == null || routedEvent == null || handler == null)
            {
                _logger?.LogWarning("Invalid parameters for routed event registration");
                return;
            }
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            lock (_lock)
            {
                try
                {
                    // Subscribe to the routed event
                    element.AddHandler(routedEvent, handler);
                    
                    // Create subscription record
                    var subscription = new RoutedEventSubscription
                    {
                        Element = new WeakReference(element),
                        RoutedEvent = routedEvent,
                        Handler = handler,
                        RegisteredAt = DateTime.UtcNow,
                        SubscriptionId = Guid.NewGuid().ToString("N")[..8]
                    };
                    
                    _routedEventSubscriptions.Add(subscription);
                    _registrationCount++;
                    
                    stopwatch.Stop();
                    
                    _logger?.LogDebug("Registered routed event handler: {ComponentName}.{EventName} [{SubscriptionId}] in {ElapsedMs}ms",
                        _componentName, routedEvent.Name, subscription.SubscriptionId, stopwatch.ElapsedMilliseconds);
                    
                    TrackRegistrationTelemetry(routedEvent.Name, stopwatch.ElapsedMilliseconds);
                    CheckMemoryHealth();
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger?.LogError(ex, "Failed to register routed event handler: {ComponentName}.{EventName}", _componentName, routedEvent?.Name);
                    _telemetryService?.TrackException(ex, $"EventCleanupManager.RegisterRoutedEventHandler.{_componentName}");
                }
            }
        }
        
        /// <summary>
        /// Registers a custom cleanup action
        /// </summary>
        public void RegisterCustomCleanup(string description, Action cleanupAction)
        {
            if (!FeatureFlags.UseEventCleanupManager)
            {
                // No-op when disabled
                return;
            }
            
            if (_disposed)
            {
                _logger?.LogWarning("Attempted to register custom cleanup on disposed EventCleanupManager: {ComponentName}", _componentName);
                return;
            }
            
            if (string.IsNullOrEmpty(description) || cleanupAction == null)
            {
                _logger?.LogWarning("Invalid parameters for custom cleanup registration");
                return;
            }
            
            lock (_lock)
            {
                var cleanup = new CustomCleanupAction
                {
                    Description = description,
                    Action = cleanupAction,
                    RegisteredAt = DateTime.UtcNow,
                    CleanupId = Guid.NewGuid().ToString("N")[..8]
                };
                
                _customCleanupActions.Add(cleanup);
                _registrationCount++;
                
                _logger?.LogDebug("Registered custom cleanup: {ComponentName}.{Description} [{CleanupId}]",
                    _componentName, description, cleanup.CleanupId);
            }
        }
        
        /// <summary>
        /// Gets current statistics for monitoring
        /// </summary>
        public EventCleanupStats GetStats()
        {
            lock (_lock)
            {
                return new EventCleanupStats
                {
                    ComponentName = _componentName,
                    RegistrationCount = _registrationCount,
                    CleanupCount = _cleanupCount,
                    ActiveEventSubscriptions = _eventSubscriptions.Count,
                    ActiveRoutedEventSubscriptions = _routedEventSubscriptions.Count,
                    ActiveCustomCleanups = _customCleanupActions.Count,
                    CleanupFailures = _cleanupFailures,
                    AverageCleanupTimeMs = _cleanupCount > 0 ? (double)_totalCleanupTimeMs / _cleanupCount : 0,
                    MemoryBeforeRegistrationBytes = _memoryBeforeRegistration,
                    MemoryAfterCleanupBytes = _memoryAfterCleanup,
                    MemoryFreedBytes = _memoryAfterCleanup > 0 ? _memoryBeforeRegistration - _memoryAfterCleanup : 0
                };
            }
        }
        
        /// <summary>
        /// Performs cleanup of all registered handlers
        /// </summary>
        public void CleanupAll()
        {
            if (_disposed) return;
            
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var cleanupStartMemory = GC.GetTotalMemory(false);
            
            _logger?.LogInformation("Starting cleanup for component: {ComponentName}", _componentName);
            
            lock (_lock)
            {
                var initialSubscriptions = _eventSubscriptions.Count + _routedEventSubscriptions.Count + _customCleanupActions.Count;
                var cleanupFailures = 0;
                
                try
                {
                    // Cleanup regular event subscriptions
                    for (int i = _eventSubscriptions.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            CleanupEventSubscription(_eventSubscriptions[i]);
                            _eventSubscriptions.RemoveAt(i);
                            _cleanupCount++;
                        }
                        catch (Exception ex)
                        {
                            cleanupFailures++;
                            _cleanupFailures++;
                            _logger?.LogError(ex, "Failed to cleanup event subscription {Index} for {ComponentName}", i, _componentName);
                        }
                    }
                    
                    // Cleanup routed event subscriptions
                    for (int i = _routedEventSubscriptions.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            CleanupRoutedEventSubscription(_routedEventSubscriptions[i]);
                            _routedEventSubscriptions.RemoveAt(i);
                            _cleanupCount++;
                        }
                        catch (Exception ex)
                        {
                            cleanupFailures++;
                            _cleanupFailures++;
                            _logger?.LogError(ex, "Failed to cleanup routed event subscription {Index} for {ComponentName}", i, _componentName);
                        }
                    }
                    
                    // Execute custom cleanup actions
                    for (int i = _customCleanupActions.Count - 1; i >= 0; i--)
                    {
                        try
                        {
                            ExecuteCustomCleanup(_customCleanupActions[i]);
                            _customCleanupActions.RemoveAt(i);
                            _cleanupCount++;
                        }
                        catch (Exception ex)
                        {
                            cleanupFailures++;
                            _cleanupFailures++;
                            _logger?.LogError(ex, "Failed to execute custom cleanup {Index} for {ComponentName}", i, _componentName);
                        }
                    }
                    
                    stopwatch.Stop();
                    _totalCleanupTimeMs += stopwatch.ElapsedMilliseconds;
                    
                    // Force garbage collection and measure memory
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    
                    _memoryAfterCleanup = GC.GetTotalMemory(false);
                    var memoryFreed = cleanupStartMemory - _memoryAfterCleanup;
                    
                    _logger?.LogInformation("Cleanup completed for {ComponentName}: {InitialSubscriptions} items, {CleanupFailures} failures, {ElapsedMs}ms, {MemoryFreed} bytes freed",
                        _componentName, initialSubscriptions, cleanupFailures, stopwatch.ElapsedMilliseconds, memoryFreed);
                    
                    // Track cleanup telemetry
                    TrackCleanupTelemetry(initialSubscriptions, cleanupFailures, stopwatch.ElapsedMilliseconds, memoryFreed);
                }
                catch (Exception ex)
                {
                    stopwatch.Stop();
                    _logger?.LogError(ex, "Critical error during cleanup for {ComponentName}", _componentName);
                    _telemetryService?.TrackException(ex, $"EventCleanupManager.CleanupAll.{_componentName}");
                }
            }
        }
        
        #endregion

        #region Private Implementation
        
        private void RegisterEventHandlerDirect<TEventArgs>(object source, string eventName, EventHandler<TEventArgs> handler) where TEventArgs : EventArgs
        {
            try
            {
                var eventInfo = source.GetType().GetEvent(eventName);
                eventInfo?.AddEventHandler(source, handler);
                _logger?.LogDebug("Direct event registration (EventCleanupManager disabled): {ComponentName}.{EventName}", _componentName, eventName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed direct event registration: {ComponentName}.{EventName}", _componentName, eventName);
            }
        }
        
        private void CleanupEventSubscription(EventSubscription subscription)
        {
            var source = subscription.Source.Target;
            if (source != null && subscription.EventInfo != null)
            {
                // Ensure we're on the correct thread for UI operations
                if (source is System.Windows.Threading.DispatcherObject dispatcherObject && !dispatcherObject.CheckAccess())
                {
                    dispatcherObject.Dispatcher.Invoke(() =>
                    {
                        subscription.EventInfo.RemoveEventHandler(source, subscription.Handler);
                    });
                }
                else
                {
                    subscription.EventInfo.RemoveEventHandler(source, subscription.Handler);
                }
                
                _logger?.LogDebug("Cleaned up event subscription: {ComponentName}.{EventName} [{SubscriptionId}]",
                    _componentName, subscription.EventName, subscription.SubscriptionId);
            }
            else
            {
                _logger?.LogDebug("Event subscription source was garbage collected: {ComponentName}.{EventName} [{SubscriptionId}]",
                    _componentName, subscription.EventName, subscription.SubscriptionId);
            }
        }
        
        private void CleanupRoutedEventSubscription(RoutedEventSubscription subscription)
        {
            var element = subscription.Element.Target as System.Windows.UIElement;
            if (element != null)
            {
                // Ensure we're on the UI thread
                if (!element.CheckAccess())
                {
                    element.Dispatcher.Invoke(() =>
                    {
                        element.RemoveHandler(subscription.RoutedEvent, subscription.Handler);
                    });
                }
                else
                {
                    element.RemoveHandler(subscription.RoutedEvent, subscription.Handler);
                }
                
                _logger?.LogDebug("Cleaned up routed event subscription: {ComponentName}.{EventName} [{SubscriptionId}]",
                    _componentName, subscription.RoutedEvent.Name, subscription.SubscriptionId);
            }
            else
            {
                _logger?.LogDebug("Routed event subscription element was garbage collected: {ComponentName}.{EventName} [{SubscriptionId}]",
                    _componentName, subscription.RoutedEvent.Name, subscription.SubscriptionId);
            }
        }
        
        private void ExecuteCustomCleanup(CustomCleanupAction cleanup)
        {
            try
            {
                cleanup.Action();
                _logger?.LogDebug("Executed custom cleanup: {ComponentName}.{Description} [{CleanupId}]",
                    _componentName, cleanup.Description, cleanup.CleanupId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Custom cleanup action failed: {ComponentName}.{Description} [{CleanupId}]",
                    _componentName, cleanup.Description, cleanup.CleanupId);
                throw;
            }
        }
        
        private void TrackRegistrationTelemetry(string eventName, long elapsedMs)
        {
            _telemetryService?.TrackEvent("EventCleanup.Registration", new Dictionary<string, object>
            {
                ["ComponentName"] = _componentName,
                ["EventName"] = eventName,
                ["ElapsedMs"] = elapsedMs,
                ["TotalRegistrations"] = _registrationCount
            });
            
            _telemetryService?.TrackMetric("EventCleanup.RegistrationTime", elapsedMs);
            
            // Report periodic statistics
            if (_registrationCount % 50 == 0)
            {
                ReportPeriodicStats();
            }
        }
        
        private void TrackCleanupTelemetry(int itemsCleanedUp, int failures, long elapsedMs, long memoryFreed)
        {
            _telemetryService?.TrackEvent("EventCleanup.Cleanup", new Dictionary<string, object>
            {
                ["ComponentName"] = _componentName,
                ["ItemsCleanedUp"] = itemsCleanedUp,
                ["Failures"] = failures,
                ["ElapsedMs"] = elapsedMs,
                ["MemoryFreedBytes"] = memoryFreed,
                ["SuccessRate"] = itemsCleanedUp > 0 ? (double)(itemsCleanedUp - failures) / itemsCleanedUp * 100.0 : 100.0
            });
            
            _telemetryService?.TrackMetric("EventCleanup.CleanupTime", elapsedMs);
            _telemetryService?.TrackMetric("EventCleanup.MemoryFreed", memoryFreed);
        }
        
        private void CheckMemoryHealth()
        {
            if (_registrationCount % 100 == 0) // Check every 100 registrations
            {
                var currentMemory = GC.GetTotalMemory(false);
                var memoryGrowth = currentMemory - _memoryBeforeRegistration;
                
                if (memoryGrowth > 10 * 1024 * 1024) // Alert if > 10MB growth
                {
                    _logger?.LogWarning("High memory growth detected in {ComponentName}: {MemoryGrowthMB:F2} MB after {Registrations} registrations",
                        _componentName, memoryGrowth / (1024.0 * 1024.0), _registrationCount);
                    
                    _telemetryService?.TrackEvent("EventCleanup.MemoryLeakRisk", new Dictionary<string, object>
                    {
                        ["ComponentName"] = _componentName,
                        ["MemoryGrowthBytes"] = memoryGrowth,
                        ["RegistrationCount"] = _registrationCount
                    });
                }
            }
        }
        
        private void ReportPeriodicStats()
        {
            var stats = GetStats();
            
            _logger?.LogInformation("Event cleanup stats for {ComponentName}: {Registrations} registrations, {Active} active, {AvgCleanupMs:F1}ms avg cleanup",
                _componentName, stats.RegistrationCount, stats.ActiveEventSubscriptions + stats.ActiveRoutedEventSubscriptions + stats.ActiveCustomCleanups, stats.AverageCleanupTimeMs);
            
            _telemetryService?.TrackEvent("EventCleanup.PeriodicStats", new Dictionary<string, object>
            {
                ["ComponentName"] = _componentName,
                ["RegistrationCount"] = stats.RegistrationCount,
                ["ActiveSubscriptions"] = stats.ActiveEventSubscriptions + stats.ActiveRoutedEventSubscriptions + stats.ActiveCustomCleanups,
                ["CleanupFailures"] = stats.CleanupFailures,
                ["AverageCleanupTimeMs"] = stats.AverageCleanupTimeMs
            });
        }
        
        #endregion

        #region IDisposable
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _logger?.LogDebug("Disposing EventCleanupManager for component: {ComponentName}", _componentName);
            
            try
            {
                CleanupAll();
                _disposed = true;
                
                _logger?.LogInformation("EventCleanupManager disposed for component: {ComponentName}", _componentName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during EventCleanupManager disposal for component: {ComponentName}", _componentName);
                _telemetryService?.TrackException(ex, $"EventCleanupManager.Dispose.{_componentName}");
            }
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    internal class EventSubscription
    {
        public WeakReference Source { get; set; }
        public string EventName { get; set; }
        public Delegate Handler { get; set; }
        public System.Reflection.EventInfo EventInfo { get; set; }
        public DateTime RegisteredAt { get; set; }
        public string SubscriptionId { get; set; }
    }
    
    internal class RoutedEventSubscription
    {
        public WeakReference Element { get; set; }
        public System.Windows.RoutedEvent RoutedEvent { get; set; }
        public System.Windows.RoutedEventHandler Handler { get; set; }
        public DateTime RegisteredAt { get; set; }
        public string SubscriptionId { get; set; }
    }
    
    internal class CustomCleanupAction
    {
        public string Description { get; set; }
        public Action Action { get; set; }
        public DateTime RegisteredAt { get; set; }
        public string CleanupId { get; set; }
    }
    
    public class EventCleanupStats
    {
        public string ComponentName { get; set; }
        public int RegistrationCount { get; set; }
        public int CleanupCount { get; set; }
        public int ActiveEventSubscriptions { get; set; }
        public int ActiveRoutedEventSubscriptions { get; set; }
        public int ActiveCustomCleanups { get; set; }
        public int CleanupFailures { get; set; }
        public double AverageCleanupTimeMs { get; set; }
        public long MemoryBeforeRegistrationBytes { get; set; }
        public long MemoryAfterCleanupBytes { get; set; }
        public long MemoryFreedBytes { get; set; }
    }
    
    #endregion
}