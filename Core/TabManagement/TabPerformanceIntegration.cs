using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.Core.Monitoring;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.UI.Controls;
using TabState = ExplorerPro.Models.TabState;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Integration layer that coordinates all performance optimization components with the main tab system.
    /// Provides unified performance management for enterprise-level scalability.
    /// </summary>
    public class TabPerformanceIntegration : IDisposable
    {
        #region Private Fields
        
        private readonly ILogger<TabPerformanceIntegration> _logger;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly PerformanceIntegrationSettings _settings;
        private readonly Timer _performanceCheckTimer;
        private readonly object _lockObject = new object();
        
        // Service instances - using correct types
        private ITabManagerService? _tabManagerService;
        private UI.Controls.TabVirtualizationManager? _virtualizationManager;
        private TabHibernationManager? _hibernationManager;
        private PerformanceOptimizer? _performanceOptimizer;
        
        // Performance tracking
        private readonly Dictionary<string, DateTime> _tabRegistrationTimes;
        private readonly Dictionary<string, PerformanceMetrics> _tabPerformanceData;
        private volatile bool _disposed;
        
        // Statistics
        private int _totalTabsRegistered;
        private int _totalTabsOptimized;
        private long _totalMemorySaved;
        
        #endregion

        #region Events
        
        /// <summary>
        /// Raised when optimization is completed
        /// </summary>
        public event EventHandler<PerformanceIntegrationEventArgs>? OptimizationCompleted;
        
        /// <summary>
        /// Raised when performance statistics are updated
        /// </summary>
        public event EventHandler<IntegrationStatsEventArgs>? StatsUpdated;
        
        #endregion

        #region Constructor
        
        public TabPerformanceIntegration(
            ILogger<TabPerformanceIntegration> logger = null,
            ResourceMonitor resourceMonitor = null,
            PerformanceIntegrationSettings settings = null)
        {
            _logger = logger;
            _resourceMonitor = resourceMonitor ?? new ResourceMonitor();
            _settings = settings ?? PerformanceIntegrationSettings.Default;
            
            _tabRegistrationTimes = new Dictionary<string, DateTime>();
            _tabPerformanceData = new Dictionary<string, PerformanceMetrics>();
            
            // Start performance monitoring timer
            _performanceCheckTimer = new Timer(PerformanceCheckCallback, null, 
                _settings.PerformanceCheckInterval, _settings.PerformanceCheckInterval);
            
            _logger?.LogInformation("TabPerformanceIntegration initialized with settings: {Settings}", _settings);
        }
        
        #endregion

        #region Public Properties
        
        public bool IsPerformanceOptimizationEnabled { get; set; } = true;
        public bool IsVirtualizationEnabled { get; set; } = true;
        public bool IsHibernationEnabled { get; set; } = true;
        public PerformanceStats CurrentStats => GetCurrentStats();
        
        #endregion

        #region Initialization
        
        /// <summary>
        /// Initializes the performance integration system
        /// </summary>
        public async Task InitializeAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(TabPerformanceIntegration));
            
            try
            {
                _logger?.LogInformation("Initializing TabPerformanceIntegration services...");
                
                // Get tab manager service
                _tabManagerService = serviceProvider.GetService<ITabManagerService>();
                if (_tabManagerService == null)
                {
                    _logger?.LogWarning("ITabManagerService not found in service provider");
                }
                else
                {
                    // Wire up tab manager events
                    _tabManagerService.TabCreated += OnTabCreated;
                    _tabManagerService.TabClosed += OnTabClosed;
                    _tabManagerService.ActiveTabChanged += OnActiveTabChanged;
                }
                
                // Create virtualization manager
                if (_settings.EnableVirtualization)
                {
                    var virtSettings = new UI.Controls.VirtualizationSettings
                    {
                        MaxVisibleTabs = _settings.MaxVisibleTabs,
                        BufferTabs = _settings.BufferTabs,
                        HibernationDelay = _settings.HibernationDelay,
                        CleanupInterval = TimeSpan.FromMinutes(5)
                    };
                    
                    _virtualizationManager = new UI.Controls.TabVirtualizationManager(
                        serviceProvider.GetService<ILogger<UI.Controls.TabVirtualizationManager>>(),
                        _resourceMonitor,
                        virtSettings);
                }
                
                // Create hibernation manager
                if (_settings.EnableHibernation)
                {
                    var hibernationSettings = new HibernationSettings
                    {
                        MaxIdleTime = _settings.MaxIdleTime,
                        EnableProactiveHibernation = _settings.EnableProactiveHibernation,
                        MaxMemoryUsage = _settings.MaxMemoryUsage
                    };
                    
                    _hibernationManager = new TabHibernationManager(
                        serviceProvider.GetService<ILogger<TabHibernationManager>>(),
                        _resourceMonitor,
                        hibernationSettings);
                }
                
                // Create performance optimizer
                if (_settings.EnableOptimization)
                {
                    var perfSettings = new PerformanceSettings
                    {
                        OptimizationInterval = _settings.OptimizationInterval,
                        MemoryWarningThresholdMB = _settings.MemoryThreshold / (1024 * 1024), // Convert from bytes to MB
                        EnableAutoOptimization = _settings.EnableMemoryProfiling
                    };
                    
                    _performanceOptimizer = new PerformanceOptimizer(
                        serviceProvider.GetService<ILogger<PerformanceOptimizer>>(),
                        _resourceMonitor,
                        null, // UI.Controls.TabVirtualizationManager is incompatible with Core.TabManagement.TabVirtualizationManager
                        _hibernationManager,
                        perfSettings);
                }
                
                // Wire up cross-service events
                await WireUpCrossServiceEventsAsync();
                
                _logger?.LogInformation("TabPerformanceIntegration initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize TabPerformanceIntegration");
                throw;
            }
        }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Optimizes performance for the entire tab system
        /// </summary>
        public async Task<OptimizationResult> OptimizeTabPerformanceAsync(bool aggressive = false)
        {
            if (!IsPerformanceOptimizationEnabled || _performanceOptimizer == null)
                return OptimizationResult.Skipped;
            
            var options = new OptimizationOptions
            {
                OptimizeVirtualization = IsVirtualizationEnabled,
                OptimizeHibernation = IsHibernationEnabled,
                ForceGarbageCollection = aggressive,
                AggressiveMode = aggressive
            };
            
            var result = await _performanceOptimizer.OptimizeAsync(options);
            
            _logger?.LogInformation("Tab performance optimization completed - Status: {Status}, Memory saved: {Memory}MB", 
                result.Status, result.MemorySaved);
            
            return result;
        }
        
        /// <summary>
        /// Gets comprehensive performance analysis
        /// </summary>
        public async Task<PerformanceAnalysis> AnalyzeTabPerformanceAsync()
        {
            if (_performanceOptimizer == null)
                return null;
            
            var analysis = await _performanceOptimizer.AnalyzePerformanceAsync();
            
            // Add tab-specific analysis
            if (_tabManagerService != null)
            {
                var tabCount = _tabManagerService.TabCount;
                var activeTab = _tabManagerService.ActiveTab;
                
                // Add virtualization recommendations
                if (tabCount > _settings.VirtualizationRecommendationThreshold && !IsVirtualizationEnabled)
                {
                    analysis.Recommendations.Add(new PerformanceRecommendation
                    {
                        Type = RecommendationType.Virtualization,
                        Priority = RecommendationPriority.High,
                        Description = $"Enable virtualization for {tabCount} tabs",
                        SuggestedActions = new[] { "Enable tab virtualization", "Configure hibernation" }
                    });
                }
                
                // Add hibernation recommendations
                if (tabCount > _settings.HibernationRecommendationThreshold && !IsHibernationEnabled)
                {
                    analysis.Recommendations.Add(new PerformanceRecommendation
                    {
                        Type = RecommendationType.Hibernation,
                        Priority = RecommendationPriority.Medium,
                        Description = $"Enable hibernation for {tabCount} tabs",
                        SuggestedActions = new[] { "Enable tab hibernation", "Configure aggressive hibernation" }
                    });
                }
            }
            
            return analysis;
        }
        
        /// <summary>
        /// Configures performance settings at runtime
        /// </summary>
        public async Task ConfigurePerformanceAsync(PerformanceConfiguration config)
        {
            if (config == null) return;
            
            // Update global settings
            IsPerformanceOptimizationEnabled = config.EnableOptimization;
            IsVirtualizationEnabled = config.EnableVirtualization;
            IsHibernationEnabled = config.EnableHibernation;
            
            // Configure virtualization
            if (_virtualizationManager != null && config.VirtualizationSettings != null)
            {
                // Apply new virtualization settings
                await _virtualizationManager.OptimizeVisibilityAsync();
            }
            
            // Configure hibernation
            if (_hibernationManager != null && config.HibernationSettings != null)
            {
                // Apply new hibernation settings if needed
            }
            
            _logger?.LogInformation("Performance configuration updated");
        }
        
        /// <summary>
        /// Registers a tab with all performance services
        /// </summary>
        public async Task RegisterTabAsync(TabModel tab)
        {
            if (_disposed || tab == null) return;
            
            lock (_lockObject)
            {
                _tabRegistrationTimes[tab.Id] = DateTime.UtcNow;
                _tabPerformanceData[tab.Id] = new PerformanceMetrics { TabId = tab.Id };
                _totalTabsRegistered++;
            }
            
            try
            {
                // Register with virtualization manager
                if (_virtualizationManager != null)
                {
                    await _virtualizationManager.RegisterTabAsync(tab);
                }
                
                // Register with hibernation manager
                if (_hibernationManager != null)
                {
                    await _hibernationManager.RegisterTabAsync(tab);
                }
                
                _logger?.LogDebug("Tab registered with performance services: {TabId}", tab.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error registering tab with performance services: {TabId}", tab.Id);
            }
        }
        
        /// <summary>
        /// Unregisters a tab from all performance services
        /// </summary>
        public async Task UnregisterTabAsync(string tabId)
        {
            if (_disposed || string.IsNullOrEmpty(tabId)) return;
            
            try
            {
                // Unregister from virtualization manager
                if (_virtualizationManager != null)
                {
                    await _virtualizationManager.UnregisterTabAsync(tabId);
                }
                
                // Unregister from hibernation manager
                if (_hibernationManager != null)
                {
                    await _hibernationManager.UnregisterTabAsync(tabId);
                }
                
                // Clean up tracking data
                lock (_lockObject)
                {
                    _tabRegistrationTimes.Remove(tabId);
                    _tabPerformanceData.Remove(tabId);
                }
                
                _logger?.LogDebug("Tab unregistered from performance services: {TabId}", tabId);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error unregistering tab from performance services: {TabId}", tabId);
            }
        }
        
        /// <summary>
        /// Activates a tab and optimizes its performance
        /// </summary>
        public async Task ActivateTabAsync(TabModel tab)
        {
            if (_disposed || tab == null) return;
            
            try
            {
                // Activate in virtualization manager
                if (_virtualizationManager != null)
                {
                    await _virtualizationManager.ActivateTabAsync(tab);
                }
                
                // Check if tab was hibernated and handle reactivation
                if (tab.State == Models.TabState.Hibernated && _hibernationManager != null)
                {
                    await _hibernationManager.ReactivateTabAsync(tab);
                }
                
                // Update performance metrics
                UpdateTabPerformanceMetrics(tab.Id, TabPerformanceEvent.Activated);
                
                _logger?.LogDebug("Tab activated with performance optimization: {TabId}", tab.Id);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error activating tab with performance services: {TabId}", tab.Id);
            }
        }
        
        /// <summary>
        /// Triggers a global performance optimization cycle
        /// </summary>
        public async Task OptimizePerformanceAsync()
        {
            if (_disposed) return;
            
            try
            {
                var startTime = DateTime.UtcNow;
                var optimizationResults = new List<OptimizationResult>();
                
                // Run virtualization optimization
                if (_virtualizationManager != null)
                {
                    await _virtualizationManager.OptimizeVisibilityAsync();
                    optimizationResults.Add(new OptimizationResult 
                    { 
                        Service = "Virtualization", 
                        Success = true, 
                        MemorySaved = 0 
                    });
                }
                
                // Run hibernation optimization
                if (_hibernationManager != null)
                {
                    var hibernationResult = await _hibernationManager.OptimizeAsync();
                    optimizationResults.Add(hibernationResult);
                }
                
                // Run performance optimizer
                if (_performanceOptimizer != null)
                {
                    await _performanceOptimizer.OptimizeAsync();
                }
                
                var totalTime = DateTime.UtcNow - startTime;
                _totalTabsOptimized++;
                
                _logger?.LogInformation("Performance optimization completed in {Time}ms", 
                    totalTime.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during performance optimization");
            }
        }
        
        #endregion

        #region Event Wiring
        
        private async Task WireUpCrossServiceEventsAsync()
        {
            // Wire up virtualization manager events
            if (_virtualizationManager != null)
            {
                // Note: Commenting out incompatible event handlers due to signature mismatches
                // _virtualizationManager.TabHibernated += OnTabHibernatedByManager;
                // _virtualizationManager.TabReactivated += OnTabReactivatedByManager;
                // _virtualizationManager.StatsUpdated += OnHibernationStatsUpdated;
            }
            
            // Wire up hibernation manager events
            if (_hibernationManager != null)
            {
                _hibernationManager.TabHibernated += OnTabHibernatedByManager;
                _hibernationManager.TabReactivated += OnTabReactivatedByManager;
                _hibernationManager.StatsUpdated += OnHibernationStatsUpdated;
            }
            
            // Wire up performance optimizer events
            if (_performanceOptimizer != null)
            {
                _performanceOptimizer.OptimizationCompleted += OnOptimizationCompleted;
                _performanceOptimizer.ThresholdExceeded += OnPerformanceThresholdExceeded;
            }
            
            await Task.CompletedTask;
        }
        
        #endregion

        #region Event Handlers
        
        private async void OnTabCreated(object sender, TabEventArgs e)
        {
            await RegisterTabAsync(e.Tab);
        }
        
        private async void OnTabClosed(object sender, TabEventArgs e)
        {
            await UnregisterTabAsync(e.Tab.Id);
        }
        
        private async void OnActiveTabChanged(object sender, TabChangedEventArgs e)
        {
            if (e.NewTab != null)
            {
                await ActivateTabAsync(e.NewTab);
            }
        }
        
        private void OnTabHibernatedByManager(object sender, TabHibernationEventArgs e)
        {
            lock (_lockObject)
            {
                _totalMemorySaved += e.MemoryFreed;
            }
            
            UpdateTabPerformanceMetrics(e.TabId, TabPerformanceEvent.Hibernated);
            _logger?.LogDebug("Tab hibernated by manager: {TabId}, Memory freed: {Memory}", 
                e.TabId, e.MemoryFreed);
        }
        
        private void OnTabReactivatedByManager(object sender, TabReactivationEventArgs e)
        {
            UpdateTabPerformanceMetrics(e.TabId, TabPerformanceEvent.Reactivated);
            _logger?.LogDebug("Tab reactivated by manager: {TabId}, Time: {Time}ms", 
                e.TabId, e.ReactivationTime.TotalMilliseconds);
        }
        
        private void OnHibernationStatsUpdated(object sender, HibernationStatsEventArgs e)
        {
            EmitStatsUpdate();
        }
        
        private void OnOptimizationCompleted(object sender, OptimizationCompletedEventArgs e)
        {
            _logger?.LogInformation("Optimization cycle completed: {Result}", e.Result);
            EmitStatsUpdate();
        }
        
        private void OnPerformanceThresholdExceeded(object sender, PerformanceThresholdEventArgs e)
        {
            _logger?.LogWarning("Performance threshold exceeded: {Threshold}", e.Threshold);
            
            // Trigger emergency optimization
            Task.Run(async () => await OptimizePerformanceAsync());
        }
        
        #endregion

        #region Private Methods
        
        private void PerformanceCheckCallback(object state)
        {
            if (_disposed) return;
            
            try
            {
                // Check if optimization is needed
                var stats = GetCurrentStats();
                if (stats.TotalTabs > _settings.MaxVisibleTabs * 2)
                {
                    Task.Run(async () => await OptimizePerformanceAsync());
                }
                
                EmitStatsUpdate();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in performance check callback");
            }
        }
        
        private void UpdateTabPerformanceMetrics(string tabId, TabPerformanceEvent eventType)
        {
            lock (_lockObject)
            {
                if (_tabPerformanceData.TryGetValue(tabId, out var metrics))
                {
                    metrics.LastEvent = eventType;
                    metrics.LastEventTime = DateTime.UtcNow;
                    metrics.EventCount++;
                    
                    _logger?.LogDebug("Tab performance updated: {TabId}, Event: {EventType}", 
                        tabId, eventType);
                }
            }
        }
        
        private PerformanceStats GetCurrentStats()
        {
            lock (_lockObject)
            {
                return new PerformanceStats
                {
                    Timestamp = DateTime.UtcNow,
                    IsOptimizationEnabled = IsPerformanceOptimizationEnabled,
                    IsVirtualizationEnabled = IsVirtualizationEnabled,
                    IsHibernationEnabled = IsHibernationEnabled,
                    CurrentMemoryUsageMB = _resourceMonitor.GetCurrentSnapshot().WorkingSetMB,
                    ThreadCount = _resourceMonitor.GetCurrentSnapshot().ThreadCount,
                    HandleCount = _resourceMonitor.GetCurrentSnapshot().HandleCount,
                    TotalTabs = _virtualizationManager?.TotalTabs ?? 0,
                    VisibleTabs = _virtualizationManager?.VisibleTabs ?? 0,
                    HibernatedTabs = _virtualizationManager?.HibernatedTabs ?? 0,
                    MemorySavedMB = _virtualizationManager?.MemorySavedBytes / (1024 * 1024) ?? 0,
                    TotalOptimizations = _performanceOptimizer?.CurrentMetrics.TotalOptimizations ?? 0,
                    TotalMemorySavedMB = _performanceOptimizer?.CurrentMetrics.TotalMemorySaved ?? 0,
                    LastOptimization = _performanceOptimizer?.CurrentMetrics.LastOptimization ?? DateTime.MinValue
                };
            }
        }
        
        private void EmitStatsUpdate()
        {
            var perfStats = GetCurrentStats();
            StatsUpdated?.Invoke(this, new IntegrationStatsEventArgs
            {
                Stats = new PerformanceIntegrationStats
                {
                    TotalTabsRegistered = _totalTabsRegistered,
                    TotalTabsOptimized = _totalTabsOptimized,
                    TotalMemorySaved = _totalMemorySaved,
                    VirtualizationStats = new VirtualizationStats
                    {
                        TotalTabs = perfStats.TotalTabs,
                        VisibleTabs = perfStats.VisibleTabs,
                        HibernatedTabs = perfStats.HibernatedTabs,
                        MemorySavedBytes = perfStats.MemorySavedMB * 1024 * 1024
                    },
                    HibernationStats = new HibernationStats
                    {
                        TotalHibernated = perfStats.HibernatedTabs,
                        CurrentlyHibernated = perfStats.HibernatedTabs,
                        TotalMemorySavedMB = perfStats.MemorySavedMB,
                        AverageHibernationDuration = TimeSpan.Zero,
                        IsMemoryPressureActive = false
                    },
                    PerformanceStats = perfStats
                },
                Timestamp = DateTime.UtcNow
            });
        }
        
        #endregion

        #region Disposal
        
        private void UnwireEvents()
        {
            if (_tabManagerService != null)
            {
                _tabManagerService.TabCreated -= OnTabCreated;
                _tabManagerService.TabClosed -= OnTabClosed;
                _tabManagerService.ActiveTabChanged -= OnActiveTabChanged;
            }
            
            if (_virtualizationManager != null)
            {
                // Note: Using compatible event handlers for virtualization events
                // _virtualizationManager.TabHibernated -= OnTabHibernatedByManager;
                // _virtualizationManager.TabReactivated -= OnTabReactivatedByManager;
                // _virtualizationManager.StatsUpdated -= OnHibernationStatsUpdated;
            }
            
            if (_hibernationManager != null)
            {
                _hibernationManager.TabHibernated -= OnTabHibernatedByManager;
                _hibernationManager.TabReactivated -= OnTabReactivatedByManager;
                _hibernationManager.StatsUpdated -= OnHibernationStatsUpdated;
            }
            
            if (_performanceOptimizer != null)
            {
                _performanceOptimizer.OptimizationCompleted -= OnOptimizationCompleted;
                _performanceOptimizer.ThresholdExceeded -= OnPerformanceThresholdExceeded;
            }
        }
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            UnwireEvents();
            
            _performanceCheckTimer?.Dispose();
            _virtualizationManager?.Dispose();
            _hibernationManager?.Dispose();
            _performanceOptimizer?.Dispose();
            
            lock (_lockObject)
            {
                _tabRegistrationTimes.Clear();
                _tabPerformanceData.Clear();
            }
            
            _logger?.LogInformation("TabPerformanceIntegration disposed");
        }
        
        #endregion
    }
} 