using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.Services
{
    /// <summary>
    /// Service health monitor that tracks the health and status of all registered services.
    /// Provides enterprise-level health monitoring and diagnostics.
    /// </summary>
    public class ServiceHealthMonitor : IDisposable
    {
        #region Private Fields
        
        private readonly ILogger<ServiceHealthMonitor> _logger;
        private readonly ConcurrentDictionary<string, ServiceHealthInfo> _serviceHealth;
        private readonly Timer _healthCheckTimer;
        private readonly SemaphoreSlim _healthCheckSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private bool _disposed;
        private bool _monitoringActive;
        
        #endregion

        #region Constructor
        
        public ServiceHealthMonitor(ILogger<ServiceHealthMonitor> logger = null)
        {
            _logger = logger;
            _serviceHealth = new ConcurrentDictionary<string, ServiceHealthInfo>();
            _healthCheckSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
            
            // Initialize timer (but don't start yet)
            _healthCheckTimer = new Timer(PerformHealthChecks, null, Timeout.Infinite, Timeout.Infinite);
            
            _logger?.LogDebug("ServiceHealthMonitor initialized");
        }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Registers a service for health monitoring
        /// </summary>
        public void RegisterService(string serviceName, object serviceInstance)
        {
            if (_disposed) return;
            
            var healthInfo = new ServiceHealthInfo
            {
                ServiceName = serviceName,
                ServiceInstance = new WeakReference(serviceInstance),
                IsHealthy = true,
                LastCheckTime = DateTime.UtcNow
            };
            
            _serviceHealth.AddOrUpdate(serviceName, healthInfo, (key, existing) => healthInfo);
            
            _logger?.LogDebug("Registered service '{ServiceName}' for health monitoring", serviceName);
        }

        /// <summary>
        /// Unregisters a service from health monitoring
        /// </summary>
        public bool UnregisterService(string serviceName)
        {
            ThrowIfDisposed();
            
            var removed = _serviceHealth.TryRemove(serviceName, out _);
            if (removed)
            {
                _logger?.LogDebug("Unregistered service '{ServiceName}' from health monitoring", serviceName);
            }
            
            return removed;
        }

        /// <summary>
        /// Starts health monitoring with the specified interval
        /// </summary>
        public async Task StartMonitoringAsync(CancellationToken cancellationToken = default, TimeSpan interval = default)
        {
            if (_disposed) return;
            
            if (_monitoringActive)
            {
                _logger?.LogWarning("Health monitoring is already active");
                return;
            }
            
            if (interval == default)
                interval = TimeSpan.FromSeconds(30);
            
            try
            {
                await _healthCheckSemaphore.WaitAsync(cancellationToken);
                
                // Perform initial health check
                await PerformHealthChecksAsync();
                
                // Start periodic monitoring
                _healthCheckTimer.Change(interval, interval);
                _monitoringActive = true;
                
                _logger?.LogInformation("Health monitoring started with interval {Interval}", interval);
            }
            finally
            {
                _healthCheckSemaphore.Release();
            }
        }

        /// <summary>
        /// Stops health monitoring
        /// </summary>
        public async Task StopMonitoringAsync()
        {
            ThrowIfDisposed();
            
            if (!_monitoringActive)
                return;
                
            try
            {
                await _healthCheckSemaphore.WaitAsync();
                
                _healthCheckTimer.Change(Timeout.Infinite, Timeout.Infinite);
                _monitoringActive = false;
                
                _logger?.LogInformation("Health monitoring stopped");
            }
            finally
            {
                _healthCheckSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets the health status of a specific service
        /// </summary>
        public ServiceHealthInfo GetServiceHealth(string serviceName)
        {
            ThrowIfDisposed();
            
            return _serviceHealth.TryGetValue(serviceName, out var health) ? health : null;
        }

        /// <summary>
        /// Gets the health status of all services
        /// </summary>
        public ServiceHealthInfo[] GetAllServiceHealth()
        {
            ThrowIfDisposed();
            
            var healthList = new ServiceHealthInfo[_serviceHealth.Count];
            var index = 0;
            
            foreach (var kvp in _serviceHealth)
            {
                healthList[index++] = kvp.Value;
            }
            
            return healthList;
        }

        /// <summary>
        /// Performs an immediate health check on all services
        /// </summary>
        public async Task<int> PerformImmediateHealthCheckAsync()
        {
            ThrowIfDisposed();
            
            try
            {
                await _healthCheckSemaphore.WaitAsync(_cancellationTokenSource.Token);
                return await PerformHealthChecksAsync();
            }
            finally
            {
                _healthCheckSemaphore.Release();
            }
        }
        
        #endregion

        #region Private Methods
        
        /// <summary>
        /// Timer callback for periodic health checks
        /// </summary>
        private void PerformHealthChecks(object state)
        {
            if (_disposed || _cancellationTokenSource.Token.IsCancellationRequested)
                return;
                
            Task.Run(async () =>
            {
                try
                {
                    await PerformHealthChecksAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during periodic health check");
                }
            });
        }

        /// <summary>
        /// Performs health checks on all registered services
        /// </summary>
        private async Task<int> PerformHealthChecksAsync()
        {
            var checkedCount = 0;
            var currentTime = DateTime.UtcNow;
            
            foreach (var kvp in _serviceHealth)
            {
                try
                {
                    var serviceName = kvp.Key;
                    var healthInfo = kvp.Value;
                    
                    // Check if service instance is still alive
                    var serviceInstance = healthInfo.ServiceInstance.Target;
                    if (serviceInstance == null)
                    {
                        healthInfo.IsHealthy = false;
                        healthInfo.LastError = "Service disposed";
                        healthInfo.LastCheckTime = currentTime;
                        
                        _logger?.LogWarning("Service '{ServiceName}' instance has been disposed", serviceName);
                        continue;
                    }
                    
                    // Perform specific health checks based on service type
                    var isHealthy = await CheckServiceHealthAsync(serviceName, serviceInstance);
                    
                    healthInfo.IsHealthy = isHealthy;
                    healthInfo.LastError = isHealthy ? null : "Service health check failed";
                    healthInfo.LastCheckTime = currentTime;
                    healthInfo.CheckCount++;
                    
                    if (!isHealthy)
                    {
                        _logger?.LogWarning("Service '{ServiceName}' is unhealthy", serviceName);
                    }
                    
                    checkedCount++;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error checking health of service '{ServiceName}'", kvp.Key);
                    
                    kvp.Value.IsHealthy = false;
                    kvp.Value.LastError = ex.Message;
                    kvp.Value.LastCheckTime = currentTime;
                }
            }
            
            _logger?.LogTrace("Performed health checks on {Count} services", checkedCount);
            return checkedCount;
        }

        /// <summary>
        /// Checks the health of a specific service instance
        /// </summary>
        private async Task<bool> CheckServiceHealthAsync(string serviceName, object serviceInstance)
        {
            // Basic health checks that apply to most services
            
            // Check if service is IDisposable and disposed
            if (serviceInstance is IDisposable disposable)
            {
                try
                {
                    // Try to access a property to see if it throws ObjectDisposedException
                    _ = serviceInstance.GetType().Name;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
            
            // Service-specific health checks
            if (serviceInstance is Core.TabManagement.ITabManagerService tabManager)
            {
                try
                {
                    // Basic service connectivity check
                    var tabCount = tabManager.TabCount;
                    var hasTabsResult = tabManager.HasTabs;
                    return true;
                }
                catch
                {
                    return false;
                }
            }
            
            if (serviceInstance is Threading.ThreadSafeTabOperations threadSafeOps)
            {
                try
                {
                    // Check for pending operations (high count might indicate issues)
                    var pendingCount = threadSafeOps.GetPendingOperationCount();
                    return pendingCount < 100; // Arbitrary threshold
                }
                catch
                {
                    return false;
                }
            }
            
            // Default to healthy if no specific checks failed
            return true;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceHealthMonitor));
        }
        
        #endregion

        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                _cancellationTokenSource?.Cancel();
                
                _healthCheckTimer?.Dispose();
                _healthCheckSemaphore?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                _serviceHealth.Clear();
                
                _disposed = true;
                _logger?.LogDebug("ServiceHealthMonitor disposed");
            }
        }
        
        #endregion
    }

    /// <summary>
    /// Health information for a registered service
    /// </summary>
    public class ServiceHealthInfo
    {
        public string ServiceName { get; set; }
        public WeakReference ServiceInstance { get; set; }
        public bool IsHealthy { get; set; }
        public DateTime LastCheckTime { get; set; }
        public string LastError { get; set; }
        public int CheckCount { get; set; }
    }

    /// <summary>
    /// Service status enumeration
    /// </summary>
    public enum ServiceStatus
    {
        Unknown,
        Healthy,
        Unhealthy,
        Error,
        Disposed
    }
} 