using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ExplorerPro.Core.Threading;
using ExplorerPro.Commands;
using ExplorerPro.ViewModels;
using ExplorerPro.UI.Controls;
using ExplorerPro.Core.Services;
using ExplorerPro.Core.Events;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Service integration manager that orchestrates all modern tab services and ensures proper communication.
    /// Provides enterprise-level service lifecycle management and dependency coordination.
    /// </summary>
    public class ServiceIntegrationManager : IDisposable
    {
        #region Private Fields
        
        private readonly ILogger<ServiceIntegrationManager> _logger;
        private readonly IServiceProvider _serviceProvider;
        private bool _disposed;
        private bool _isInitialized;
        
        // Core services
        private ITabManagerService _tabManagerService;
        private ThreadSafeTabOperations _threadSafeOperations;
        private MainWindowTabsViewModel _tabsViewModel;
        private ModernTabControl _tabControl;
        
        // Service communication
        private WeakEventManager _eventManager;
        private ServiceHealthMonitor _healthMonitor;
        private CancellationTokenSource _cancellationTokenSource;
        
        #endregion

        #region Constructor
        
        public ServiceIntegrationManager(
            IServiceProvider serviceProvider,
            ILogger<ServiceIntegrationManager> logger = null)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger;
            _cancellationTokenSource = new CancellationTokenSource();
            
            _logger?.LogDebug("ServiceIntegrationManager created");
        }
        
        #endregion

        #region Public Properties
        
        /// <summary>
        /// Whether the service integration is initialized and ready
        /// </summary>
        public bool IsInitialized => _isInitialized;
        
        /// <summary>
        /// The tab manager service instance
        /// </summary>
        public ITabManagerService TabManagerService => _tabManagerService;
        
        /// <summary>
        /// The thread-safe operations manager
        /// </summary>
        public ThreadSafeTabOperations ThreadSafeOperations => _threadSafeOperations;
        
        /// <summary>
        /// The tabs view model
        /// </summary>
        public MainWindowTabsViewModel TabsViewModel => _tabsViewModel;
        
        /// <summary>
        /// The modern tab control
        /// </summary>
        public ModernTabControl TabControl => _tabControl;
        
        /// <summary>
        /// The service health monitor
        /// </summary>
        public ServiceHealthMonitor ServiceHealthMonitor => _healthMonitor;
        
        #endregion

        #region Initialization
        
        /// <summary>
        /// Initializes all services and establishes proper communication channels
        /// </summary>
        public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (_isInitialized)
            {
                _logger?.LogWarning("ServiceIntegrationManager is already initialized");
                return true;
            }
            
            try
            {
                _logger?.LogInformation("Initializing service integration...");
                
                // Step 1: Initialize core services
                await InitializeCoreServicesAsync(cancellationToken);
                
                // Step 2: Initialize service communication
                await InitializeServiceCommunicationAsync(cancellationToken);
                
                // Step 3: Wire up service dependencies
                await WireUpServiceDependenciesAsync(cancellationToken);
                
                // Step 4: Initialize health monitoring
                await InitializeHealthMonitoringAsync(cancellationToken);
                
                // Step 5: Validate integration
                await ValidateIntegrationAsync(cancellationToken);
                
                _isInitialized = true;
                _logger?.LogInformation("Service integration initialized successfully");
                
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize service integration");
                await CleanupPartialInitializationAsync();
                throw;
            }
        }

        /// <summary>
        /// Initializes all core tab services
        /// </summary>
        private async Task InitializeCoreServicesAsync(CancellationToken cancellationToken)
        {
            _logger?.LogDebug("Initializing core services...");
            
            // Get or create tab manager service
            _tabManagerService = _serviceProvider.GetService<ITabManagerService>() ??
                _serviceProvider.GetService<ModernTabManagerService>() ??
                new ModernTabManagerService();
            
            // Create thread-safe operations manager
            _threadSafeOperations = new ThreadSafeTabOperations(_tabManagerService);
            
            // Get or create tabs view model
            _tabsViewModel = _serviceProvider.GetService<MainWindowTabsViewModel>() ??
                new MainWindowTabsViewModel(_tabManagerService);
            
            // Get or create tab control
            _tabControl = _serviceProvider.GetService<ModernTabControl>() ??
                new ModernTabControl();
            
            await Task.CompletedTask;
            
            _logger?.LogDebug("Core services initialized successfully");
        }

        /// <summary>
        /// Initializes service communication infrastructure
        /// </summary>
        private async Task InitializeServiceCommunicationAsync(CancellationToken cancellationToken)
        {
            _logger?.LogDebug("Initializing service communication...");
            
            // Create weak event manager for loose coupling
            _eventManager = new WeakEventManager();
            
            // Wire up service events
            WireUpServiceEvents();
            
            await Task.CompletedTask;
            
            _logger?.LogDebug("Service communication initialized successfully");
        }

        /// <summary>
        /// Wires up dependencies between services
        /// </summary>
        private async Task WireUpServiceDependenciesAsync(CancellationToken cancellationToken)
        {
            _logger?.LogDebug("Wiring up service dependencies...");
            
            // Connect tab control to services
            if (_tabControl != null)
            {
                // Set up service references in tab control
                var tabControlType = _tabControl.GetType();
                var tabManagerProperty = tabControlType.GetProperty("TabManagerService");
                tabManagerProperty?.SetValue(_tabControl, _tabManagerService);
                
                var viewModelProperty = tabControlType.GetProperty("ViewModel");
                viewModelProperty?.SetValue(_tabControl, _tabsViewModel);
            }
            
            // Connect view model to thread-safe operations if supported
            if (_tabsViewModel is IThreadSafeOperationsConsumer threadSafeConsumer)
            {
                threadSafeConsumer.SetThreadSafeOperations(_threadSafeOperations);
            }
            
            await Task.CompletedTask;
            
            _logger?.LogDebug("Service dependencies wired up successfully");
        }

        /// <summary>
        /// Initializes health monitoring for all services
        /// </summary>
        private async Task InitializeHealthMonitoringAsync(CancellationToken cancellationToken)
        {
            _logger?.LogDebug("Initializing health monitoring...");
            
            _healthMonitor = new ServiceHealthMonitor();
            
            // Register services for monitoring
            _healthMonitor.RegisterService("TabManagerService", _tabManagerService);
            _healthMonitor.RegisterService("ThreadSafeOperations", _threadSafeOperations);
            _healthMonitor.RegisterService("TabsViewModel", _tabsViewModel);
            _healthMonitor.RegisterService("TabControl", _tabControl);
            
            // Start monitoring
            await _healthMonitor.StartMonitoringAsync(cancellationToken);
            
            _logger?.LogDebug("Health monitoring initialized successfully");
        }

        /// <summary>
        /// Validates that all services are properly integrated
        /// </summary>
        private async Task ValidateIntegrationAsync(CancellationToken cancellationToken)
        {
            _logger?.LogDebug("Validating service integration...");
            
            // Test basic service connectivity
            if (_tabManagerService == null)
                throw new InvalidOperationException("Tab manager service not initialized");
                
            if (_threadSafeOperations == null)
                throw new InvalidOperationException("Thread-safe operations not initialized");
                
            if (_tabsViewModel == null)
                throw new InvalidOperationException("Tabs view model not initialized");
                
            if (_tabControl == null)
                throw new InvalidOperationException("Tab control not initialized");
            
            // Test service communication
            var testPassed = await TestServiceCommunicationAsync(cancellationToken);
            if (!testPassed)
                throw new InvalidOperationException("Service communication test failed");
            
            await Task.CompletedTask;
            
            _logger?.LogDebug("Service integration validated successfully");
        }
        
        #endregion

        #region Service Communication
        
        /// <summary>
        /// Wires up events between services using weak references
        /// </summary>
        private void WireUpServiceEvents()
        {
            // Tab manager service events
            _eventManager.Subscribe<TabEventArgs>(_tabManagerService, nameof(ITabManagerService.TabCreated), OnTabCreated);
            _eventManager.Subscribe<TabEventArgs>(_tabManagerService, nameof(ITabManagerService.TabClosed), OnTabClosed);
            _eventManager.Subscribe<TabChangedEventArgs>(_tabManagerService, nameof(ITabManagerService.ActiveTabChanged), OnActiveTabChanged);
            
            _logger?.LogDebug("Service events wired up");
        }

        /// <summary>
        /// Tests service communication by performing a simple operation
        /// </summary>
        private async Task<bool> TestServiceCommunicationAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger?.LogDebug("Testing service communication...");
                
                // Test basic service methods
                var tabCount = _tabManagerService.TabCount;
                var hasTabsResult = _tabManagerService.HasTabs;
                
                // Test thread-safe operations
                var pendingCount = _threadSafeOperations.GetPendingOperationCount();
                
                // Test view model properties
                var viewModelTabCount = _tabsViewModel.TabCount;
                var viewModelHasTabs = _tabsViewModel.HasTabs;
                
                _logger?.LogDebug("Service communication test passed");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Service communication test failed");
                return false;
            }
        }
        
        #endregion

        #region Event Handlers
        
        private void OnTabCreated(object sender, TabEventArgs e)
        {
            _logger?.LogTrace("Tab created event received: {Title}", e.Tab.Title);
        }

        private void OnTabClosed(object sender, TabEventArgs e)
        {
            _logger?.LogTrace("Tab closed event received: {Title}", e.Tab.Title);
        }

        private void OnActiveTabChanged(object sender, TabChangedEventArgs e)
        {
            _logger?.LogTrace("Active tab changed: {OldTitle} -> {NewTitle}", 
                e.OldTab?.Title, e.NewTab?.Title);
        }
        
        #endregion

        #region Service Factory Methods
        
        /// <summary>
        /// Creates a properly configured service integration manager
        /// </summary>
        public static async Task<ServiceIntegrationManager> CreateAsync(
            IServiceProvider serviceProvider,
            ILogger<ServiceIntegrationManager> logger = null,
            CancellationToken cancellationToken = default)
        {
            var manager = new ServiceIntegrationManager(serviceProvider, logger);
            await manager.InitializeAsync(cancellationToken);
            return manager;
        }

        /// <summary>
        /// Creates service integration manager with default services
        /// </summary>
        public static async Task<ServiceIntegrationManager> CreateWithDefaultServicesAsync(
            ILogger<ServiceIntegrationManager> logger = null,
            CancellationToken cancellationToken = default)
        {
            var services = new ServiceCollection();
            
            // Register default services
            services.AddScoped<ITabManagerService, ModernTabManagerService>();
            services.AddScoped<ThreadSafeTabOperations>();
            services.AddScoped<MainWindowTabsViewModel>();
            services.AddScoped<ModernTabControl>();
            
            // Add logging if provided
            if (logger != null)
            {
                services.AddLogging(builder => builder.AddConsole());
            }
            
            var serviceProvider = services.BuildServiceProvider();
            
            return await CreateAsync(serviceProvider, logger, cancellationToken);
        }
        
        #endregion

        #region Cleanup
        
        /// <summary>
        /// Cleans up partial initialization in case of failure
        /// </summary>
        private async Task CleanupPartialInitializationAsync()
        {
            try
            {
                _healthMonitor?.Dispose();
                _eventManager?.Dispose();
                _threadSafeOperations?.Dispose();
                _tabControl?.Dispose();
                
                await Task.CompletedTask;
                
                _logger?.LogDebug("Partial initialization cleaned up");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during partial initialization cleanup");
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ServiceIntegrationManager));
        }
        
        #endregion

        #region IDisposable Implementation
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _cancellationTokenSource?.Cancel();
                
                try
                {
                    _threadSafeOperations?.WaitForPendingOperationsAsync(TimeSpan.FromSeconds(5)).Wait();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error waiting for operations during disposal");
                }
                
                _healthMonitor?.Dispose();
                _eventManager?.Dispose();
                _threadSafeOperations?.Dispose();
                _tabControl?.Dispose();
                _cancellationTokenSource?.Dispose();
                
                _disposed = true;
                _logger?.LogDebug("ServiceIntegrationManager disposed");
            }
        }
        
        #endregion
    }

    /// <summary>
    /// Interface for services that consume thread-safe operations
    /// </summary>
    public interface IThreadSafeOperationsConsumer
    {
        void SetThreadSafeOperations(ThreadSafeTabOperations threadSafeOperations);
    }
}

// Extension methods for logger creation
namespace Microsoft.Extensions.Logging
{
    public static class LoggerExtensions
    {
        public static ILogger<T> CreateChildLogger<T>(this ILogger logger)
        {
            // This would typically use a proper logger factory
            // For now, we'll create a wrapper that forwards to the parent logger
            return new ChildLogger<T>(logger);
        }
    }

    internal class ChildLogger<T> : ILogger<T>
    {
        private readonly ILogger _parentLogger;

        public ChildLogger(ILogger parentLogger)
        {
            _parentLogger = parentLogger;
        }

        public IDisposable BeginScope<TState>(TState state) => _parentLogger.BeginScope(state);
        public bool IsEnabled(LogLevel logLevel) => _parentLogger.IsEnabled(logLevel);
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => _parentLogger.Log(logLevel, eventId, state, exception, formatter);
    }
} 