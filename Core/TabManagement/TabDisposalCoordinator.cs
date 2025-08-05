using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.Telemetry;
using ExplorerPro.Core.Configuration;
using ExplorerPro.Core.Monitoring;
using ExplorerPro.Models;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// PHASE 1 FIX 2: Centralized tab disposal coordination
    /// Prevents race conditions and ensures safe, orderly tab disposal
    /// Features: Circuit breaker pattern, timeout mechanisms, comprehensive telemetry
    /// </summary>
    public class TabDisposalCoordinator : IDisposable
    {
        #region Private Fields
        
        private static TabDisposalCoordinator _instance;
        private static readonly object _instanceLock = new object();
        
        private readonly ILogger<TabDisposalCoordinator> _logger;
        private readonly IExtendedTelemetryService _telemetryService;
        private readonly ResourceMonitor _performanceMonitor;
        
        // Disposal synchronization
        private readonly SemaphoreSlim _disposalSemaphore;
        private readonly ConcurrentDictionary<string, DisposalOperation> _activeDisposals;
        
        // Circuit breaker state
        private readonly CircuitBreaker _circuitBreaker;
        private readonly Timer _healthCheckTimer;
        
        // Telemetry counters
        private int _successfulDisposals = 0;
        private int _failedDisposals = 0;
        private int _timeoutDisposals = 0;
        private int _circuitBreakerTrips = 0;
        
        // Configuration
        private readonly TimeSpan _defaultTimeout;
        private readonly int _maxConcurrentDisposals;
        private readonly TimeSpan _healthCheckInterval;
        
        private bool _disposed = false;
        
        #endregion

        #region Constructor & Singleton
        
        private TabDisposalCoordinator(
            ILogger<TabDisposalCoordinator> logger,
            IExtendedTelemetryService telemetryService,
            ResourceMonitor performanceMonitor)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
            
            // Configuration from settings or defaults
            _defaultTimeout = TimeSpan.FromSeconds(30);
            _maxConcurrentDisposals = Environment.ProcessorCount * 2;
            _healthCheckInterval = TimeSpan.FromMinutes(2);
            
            // Initialize synchronization
            _disposalSemaphore = new SemaphoreSlim(_maxConcurrentDisposals, _maxConcurrentDisposals);
            _activeDisposals = new ConcurrentDictionary<string, DisposalOperation>();
            
            // Initialize circuit breaker
            _circuitBreaker = new CircuitBreaker(
                failureThreshold: 5,
                timeout: TimeSpan.FromMinutes(1),
                onStateChange: OnCircuitBreakerStateChanged
            );
            
            // Start health monitoring
            _healthCheckTimer = new Timer(HealthCheckCallback, null, 
                _healthCheckInterval, _healthCheckInterval);
            
            _logger.LogInformation("TabDisposalCoordinator initialized with max concurrent: {MaxConcurrent}, timeout: {Timeout}",
                _maxConcurrentDisposals, _defaultTimeout);
        }

        public static TabDisposalCoordinator GetInstance(
            ILogger<TabDisposalCoordinator> logger = null,
            IExtendedTelemetryService telemetryService = null,
            ResourceMonitor performanceMonitor = null)
        {
            if (_instance == null)
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        _instance = new TabDisposalCoordinator(logger, telemetryService, performanceMonitor);
                    }
                }
            }
            return _instance;
        }
        
        #endregion

        #region Public API
        
        /// <summary>
        /// Safely dispose a tab with coordination and monitoring
        /// </summary>
        public async Task<DisposalResult> DisposeTabAsync(TabItem tabItem, TimeSpan? timeout = null)
        {
            if (!FeatureFlags.UseTabDisposalCoordinator)
            {
                // Fallback to direct disposal if feature is disabled
                return await DirectDisposalFallback(tabItem);
            }
            
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(TabDisposalCoordinator));
            }
            
            if (tabItem == null)
            {
                _logger.LogWarning("Attempted to dispose null TabItem");
                return DisposalResult.Failed("TabItem is null");
            }
            
            var tabId = GetTabId(tabItem);
            var effectiveTimeout = timeout ?? _defaultTimeout;
            
            _logger.LogDebug("Starting coordinated disposal for tab: {TabId}", tabId);
            
            // Check circuit breaker
            if (_circuitBreaker.State == CircuitBreakerState.Open)
            {
                _logger.LogWarning("Circuit breaker is open, deferring disposal for tab: {TabId}", tabId);
                Interlocked.Increment(ref _circuitBreakerTrips);
                
                _telemetryService.TrackEvent("TabDisposal.CircuitBreakerTrip", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["TabId"] = tabId,
                    ["State"] = _circuitBreaker.State.ToString()
                });
                
                return DisposalResult.Deferred("Circuit breaker is open");
            }
            
            // Wait for disposal slot
            var semaphoreAcquired = false;
            try
            {
                semaphoreAcquired = await _disposalSemaphore.WaitAsync(effectiveTimeout);
                if (!semaphoreAcquired)
                {
                    _logger.LogWarning("Failed to acquire disposal semaphore within timeout for tab: {TabId}", tabId);
                    Interlocked.Increment(ref _timeoutDisposals);
                    
                    _telemetryService.TrackEvent("TabDisposal.SemaphoreTimeout", new System.Collections.Generic.Dictionary<string, object>
                    {
                        ["TabId"] = tabId,
                        ["Timeout"] = effectiveTimeout.TotalMilliseconds
                    });
                    
                    return DisposalResult.Failed("Semaphore acquisition timeout");
                }
                
                // Execute coordinated disposal
                return await ExecuteCoordinatedDisposal(tabItem, tabId, effectiveTimeout);
            }
            finally
            {
                if (semaphoreAcquired)
                {
                    _disposalSemaphore.Release();
                }
            }
        }
        
        /// <summary>
        /// Get current disposal statistics
        /// </summary>
        public DisposalStats GetStats()
        {
            return new DisposalStats
            {
                SuccessfulDisposals = _successfulDisposals,
                FailedDisposals = _failedDisposals,
                TimeoutDisposals = _timeoutDisposals,
                CircuitBreakerTrips = _circuitBreakerTrips,
                ActiveDisposals = _activeDisposals.Count,
                CircuitBreakerState = _circuitBreaker.State,
                SuccessRate = CalculateSuccessRate()
            };
        }
        
        /// <summary>
        /// Force cancel all active disposal operations (emergency shutdown)
        /// </summary>
        public async Task CancelAllDisposalsAsync(TimeSpan timeout)
        {
            _logger.LogWarning("Emergency cancellation of all active disposals requested");
            
            var cancellationSource = new CancellationTokenSource(timeout);
            var tasks = new List<Task>();
            
            foreach (var disposal in _activeDisposals.Values)
            {
                tasks.Add(disposal.CancelAsync(cancellationSource.Token));
            }
            
            try
            {
                await Task.WhenAll(tasks);
                _logger.LogInformation("All disposals cancelled successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during emergency disposal cancellation");
                _telemetryService.TrackException(ex, "TabDisposal.EmergencyCancellation");
            }
        }
        
        #endregion

        #region Private Implementation
        
        private async Task<DisposalResult> ExecuteCoordinatedDisposal(TabItem tabItem, string tabId, TimeSpan timeout)
        {
            var operation = new DisposalOperation(tabId, tabItem, timeout);
            _activeDisposals.TryAdd(tabId, operation);
            
            var startSnapshot = _performanceMonitor?.GetCurrentSnapshot();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            try
            {
                _logger.LogDebug("Executing disposal for tab: {TabId}", tabId);
                
                var result = await _circuitBreaker.ExecuteAsync(async () =>
                {
                    // Phase 1: Pre-disposal preparation
                    await PrepareForDisposal(tabItem, tabId);
                    
                    // Phase 2: Actual disposal
                    await PerformDisposal(tabItem, tabId);
                    
                    // Phase 3: Post-disposal cleanup
                    await CleanupAfterDisposal(tabItem, tabId);
                    
                    return DisposalResult.Success();
                });
                
                stopwatch.Stop();
                Interlocked.Increment(ref _successfulDisposals);
                
                _logger.LogInformation("Successfully disposed tab: {TabId} in {ElapsedMs}ms", 
                    tabId, stopwatch.ElapsedMilliseconds);
                
                // Track performance metrics
                TrackDisposalTelemetry(tabId, "Success", stopwatch.ElapsedMilliseconds, startSnapshot);
                
                return result;
            }
            catch (TimeoutException)
            {
                stopwatch.Stop();
                Interlocked.Increment(ref _timeoutDisposals);
                
                _logger.LogError("Disposal timeout for tab: {TabId} after {ElapsedMs}ms", 
                    tabId, stopwatch.ElapsedMilliseconds);
                
                TrackDisposalTelemetry(tabId, "Timeout", stopwatch.ElapsedMilliseconds, startSnapshot);
                
                return DisposalResult.Failed($"Disposal timeout after {timeout.TotalSeconds}s");
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                Interlocked.Increment(ref _failedDisposals);
                
                _logger.LogError(ex, "Disposal failed for tab: {TabId} after {ElapsedMs}ms", 
                    tabId, stopwatch.ElapsedMilliseconds);
                
                _telemetryService.TrackException(ex, $"TabDisposal.ExecutionFailed.{tabId}");
                TrackDisposalTelemetry(tabId, "Failed", stopwatch.ElapsedMilliseconds, startSnapshot);
                
                return DisposalResult.Failed($"Disposal failed: {ex.Message}");
            }
            finally
            {
                _activeDisposals.TryRemove(tabId, out _);
            }
        }
        
        private async Task<DisposalResult> DirectDisposalFallback(TabItem tabItem)
        {
            try
            {
                _logger.LogDebug("Using direct disposal fallback (TabDisposalCoordinator disabled)");
                
                // Simple direct disposal without coordination
                if (tabItem.DataContext is IDisposable disposableContext)
                {
                    disposableContext.Dispose();
                }
                
                // Clear references
                tabItem.DataContext = null;
                tabItem.Content = null;
                
                return DisposalResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Direct disposal fallback failed");
                return DisposalResult.Failed($"Fallback disposal failed: {ex.Message}");
            }
        }
        
        private async Task PrepareForDisposal(TabItem tabItem, string tabId)
        {
            _logger.LogDebug("Preparing tab for disposal: {TabId}", tabId);
            
            // Validate tab state
            if (tabItem.IsLoaded && tabItem.IsVisible)
            {
                _logger.LogDebug("Tab is active, performing graceful preparation: {TabId}", tabId);
            }
            
            // Allow async preparation operations
            await Task.Yield();
        }
        
        private async Task PerformDisposal(TabItem tabItem, string tabId)
        {
            _logger.LogDebug("Performing disposal for tab: {TabId}", tabId);
            
            // Dispose DataContext if it implements IDisposable
            if (tabItem.DataContext is IDisposable disposableContext)
            {
                _logger.LogDebug("Disposing DataContext for tab: {TabId}", tabId);
                disposableContext.Dispose();
            }
            
            // Clear content
            if (tabItem.Content is IDisposable disposableContent)
            {
                _logger.LogDebug("Disposing Content for tab: {TabId}", tabId);
                disposableContent.Dispose();
            }
            
            // Clear references
            tabItem.DataContext = null;
            tabItem.Content = null;
            tabItem.Tag = null;
            
            // Allow async disposal operations
            await Task.Yield();
        }
        
        private async Task CleanupAfterDisposal(TabItem tabItem, string tabId)
        {
            _logger.LogDebug("Cleaning up after disposal for tab: {TabId}", tabId);
            
            // Perform any additional cleanup
            await Task.Yield();
        }
        
        private void TrackDisposalTelemetry(string tabId, string result, long elapsedMs, ResourceSnapshot startSnapshot)
        {
            var endSnapshot = _performanceMonitor?.GetCurrentSnapshot();
            
            _telemetryService.TrackEvent("TabDisposal.Completed", new System.Collections.Generic.Dictionary<string, object>
            {
                ["TabId"] = tabId,
                ["Result"] = result,
                ["ElapsedMs"] = elapsedMs,
                ["StartMemoryMB"] = startSnapshot?.WorkingSetMB ?? -1,
                ["EndMemoryMB"] = endSnapshot?.WorkingSetMB ?? -1,
                ["ActiveDisposals"] = _activeDisposals.Count
            });
            
            _telemetryService.TrackMetric("TabDisposal.ElapsedMs", elapsedMs);
            _telemetryService.TrackMetric("TabDisposal.ActiveCount", _activeDisposals.Count);
        }
        
        private string GetTabId(TabItem tabItem)
        {
            // Try to get ID from TabModel first
            var tabModel = TabModelResolver.GetTabModel(tabItem);
            if (tabModel != null && !string.IsNullOrEmpty(tabModel.Id))
            {
                return tabModel.Id;
            }
            
            // Fallback to hash code
            return $"Tab_{tabItem.GetHashCode():X8}";
        }
        
        private double CalculateSuccessRate()
        {
            var total = _successfulDisposals + _failedDisposals + _timeoutDisposals;
            return total > 0 ? (double)_successfulDisposals / total * 100.0 : 100.0;
        }
        
        private void OnCircuitBreakerStateChanged(CircuitBreakerState oldState, CircuitBreakerState newState)
        {
            _logger.LogWarning("Circuit breaker state changed: {OldState} -> {NewState}", oldState, newState);
            
            _telemetryService.TrackEvent("TabDisposal.CircuitBreakerStateChange", new System.Collections.Generic.Dictionary<string, object>
            {
                ["OldState"] = oldState.ToString(),
                ["NewState"] = newState.ToString(),
                ["FailureCount"] = _circuitBreaker.FailureCount
            });
        }
        
        private void HealthCheckCallback(object state)
        {
            try
            {
                var stats = GetStats();
                
                _logger.LogInformation("TabDisposal Health Check - Success: {Success}, Failed: {Failed}, Active: {Active}, Circuit: {Circuit}",
                    stats.SuccessfulDisposals, stats.FailedDisposals, stats.ActiveDisposals, stats.CircuitBreakerState);
                
                _telemetryService.TrackEvent("TabDisposal.HealthCheck", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["SuccessfulDisposals"] = stats.SuccessfulDisposals,
                    ["FailedDisposals"] = stats.FailedDisposals,
                    ["TimeoutDisposals"] = stats.TimeoutDisposals,
                    ["ActiveDisposals"] = stats.ActiveDisposals,
                    ["SuccessRate"] = stats.SuccessRate,
                    ["CircuitBreakerState"] = stats.CircuitBreakerState.ToString()
                });
                
                // Check for health issues
                if (stats.SuccessRate < 80.0 && stats.SuccessfulDisposals + stats.FailedDisposals > 10)
                {
                    _logger.LogWarning("Low disposal success rate detected: {SuccessRate:F1}%", stats.SuccessRate);
                }
                
                if (stats.ActiveDisposals > _maxConcurrentDisposals * 0.8)
                {
                    _logger.LogWarning("High number of active disposals: {ActiveDisposals}/{MaxConcurrent}", 
                        stats.ActiveDisposals, _maxConcurrentDisposals);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during disposal health check");
            }
        }
        
        #endregion

        #region IDisposable
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _logger.LogInformation("Disposing TabDisposalCoordinator");
            
            try
            {
                // Cancel all active disposals
                var cancellationTask = CancelAllDisposalsAsync(TimeSpan.FromSeconds(10));
                cancellationTask.Wait(TimeSpan.FromSeconds(15));
                
                // Dispose resources
                _healthCheckTimer?.Dispose();
                _disposalSemaphore?.Dispose();
                _circuitBreaker?.Dispose();
                
                // Final telemetry
                var finalStats = GetStats();
                _telemetryService.TrackEvent("TabDisposal.FinalStats", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["SuccessfulDisposals"] = finalStats.SuccessfulDisposals,
                    ["FailedDisposals"] = finalStats.FailedDisposals,
                    ["TimeoutDisposals"] = finalStats.TimeoutDisposals,
                    ["SuccessRate"] = finalStats.SuccessRate
                });
                
                _logger.LogInformation("TabDisposalCoordinator disposed - Final stats: Success: {Success}, Failed: {Failed}, Rate: {Rate:F1}%",
                    finalStats.SuccessfulDisposals, finalStats.FailedDisposals, finalStats.SuccessRate);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during TabDisposalCoordinator disposal");
            }
            finally
            {
                _disposed = true;
            }
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    public class DisposalResult
    {
        public bool IsSuccess { get; private set; }
        public string Message { get; private set; }
        public DisposalResultType Type { get; private set; }
        
        private DisposalResult(bool isSuccess, string message, DisposalResultType type)
        {
            IsSuccess = isSuccess;
            Message = message;
            Type = type;
        }
        
        public static DisposalResult Success() => new DisposalResult(true, "Disposal completed successfully", DisposalResultType.Success);
        public static DisposalResult Failed(string message) => new DisposalResult(false, message, DisposalResultType.Failed);
        public static DisposalResult Deferred(string message) => new DisposalResult(false, message, DisposalResultType.Deferred);
    }
    
    public enum DisposalResultType
    {
        Success,
        Failed,
        Deferred
    }
    
    public class DisposalStats
    {
        public int SuccessfulDisposals { get; set; }
        public int FailedDisposals { get; set; }
        public int TimeoutDisposals { get; set; }
        public int CircuitBreakerTrips { get; set; }
        public int ActiveDisposals { get; set; }
        public CircuitBreakerState CircuitBreakerState { get; set; }
        public double SuccessRate { get; set; }
    }
    
    internal class DisposalOperation
    {
        public string TabId { get; }
        public TabItem TabItem { get; }
        public DateTime StartTime { get; }
        public TimeSpan Timeout { get; }
        public CancellationTokenSource CancellationSource { get; }
        
        public DisposalOperation(string tabId, TabItem tabItem, TimeSpan timeout)
        {
            TabId = tabId;
            TabItem = tabItem;
            StartTime = DateTime.UtcNow;
            Timeout = timeout;
            CancellationSource = new CancellationTokenSource(timeout);
        }
        
        public async Task CancelAsync(CancellationToken cancellationToken)
        {
            try
            {
                CancellationSource.Cancel();
                await Task.Delay(100, cancellationToken); // Brief delay for cleanup
            }
            catch (OperationCanceledException)
            {
                // Expected during cancellation
            }
        }
    }
    
    #endregion
}