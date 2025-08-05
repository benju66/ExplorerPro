using System;
using System.Threading;
using ExplorerPro.Models;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.Monitoring;
using System.Collections.Generic;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// PHASE 1 FIX 3: Centralized TabModel resolution logic
    /// Provides consistent access to TabModel regardless of storage location (DataContext vs Tag)
    /// with telemetry tracking and automatic migration support.
    /// </summary>
    public static class TabModelResolver
    {
        #region Private Fields
        
        private static ILogger _logger;
        private static ITelemetryService _telemetryService;
        private static ResourceMonitor _performanceMonitor;
        private static ISettingsService _settingsService;
        
        // Telemetry counters - thread-safe
        private static int _dataContextHitCount = 0;
        private static int _tagFallbackCount = 0;
        private static int _migrationCount = 0;
        private static int _notFoundCount = 0;
        
        // Feature flag
        private static bool? _isEnabled;
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Initializes the TabModelResolver with required services
        /// </summary>
        public static void Initialize(
            ILogger logger, 
            ITelemetryService telemetryService, 
            ResourceMonitor performanceMonitor,
            ISettingsService settingsService)
        {
            _logger = logger;
            _telemetryService = telemetryService;
            _performanceMonitor = performanceMonitor;
            _settingsService = settingsService;
            
            _logger?.LogInformation("TabModelResolver initialized with telemetry and performance monitoring");
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Gets the TabModel from a TabItem, checking DataContext first, then Tag.
        /// Automatically migrates from Tag to DataContext if needed.
        /// </summary>
        /// <param name="tabItem">The TabItem to get the model from</param>
        /// <param name="migrate">Whether to automatically migrate from Tag to DataContext</param>
        /// <returns>The TabModel or null if not found</returns>
        public static TabModel GetTabModel(TabItem tabItem, bool migrate = true)
        {
            if (!IsEnabled())
            {
                // Fall back to legacy behavior if feature is disabled
                return GetTabModelLegacy(tabItem);
            }
            
            if (tabItem == null) 
            {
                Interlocked.Increment(ref _notFoundCount);
                return null;
            }
            
            using var operation = _performanceMonitor?.StartOperation("TabModel.Resolution");
            
            try
            {
                // Priority 1: Check DataContext (preferred pattern)
                if (tabItem.DataContext is TabModel dataContextModel)
                {
                    Interlocked.Increment(ref _dataContextHitCount);
                    LogTelemetryIfNeeded("DataContext");
                    return dataContextModel;
                }
                
                // Priority 2: Check Tag for backward compatibility
                if (tabItem.Tag is TabModel tagModel)
                {
                    Interlocked.Increment(ref _tagFallbackCount);
                    
                    _logger?.LogDebug("TabModel found in Tag property for tab '{Title}'", 
                        tagModel?.Title ?? "Unknown");
                    
                    _telemetryService?.TrackEvent("TabModel.TagFallback", new Dictionary<string, object>
                    {
                        ["TabTitle"] = tagModel?.Title ?? "Unknown",
                        ["FallbackCount"] = _tagFallbackCount
                    });
                    
                    // Migrate to DataContext if enabled
                    if (migrate && tagModel != null)
                    {
                        MigrateToDataContext(tabItem, tagModel);
                    }
                    
                    LogTelemetryIfNeeded("Tag");
                    return tagModel;
                }
                
                Interlocked.Increment(ref _notFoundCount);
                _logger?.LogWarning("TabModel not found in DataContext or Tag for TabItem");
                LogTelemetryIfNeeded("NotFound");
                
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resolving TabModel from TabItem");
                _telemetryService?.TrackException(ex, "TabModelResolver.GetTabModel");
                return null;
            }
        }
        
        /// <summary>
        /// Sets the TabModel on a TabItem using the preferred DataContext pattern
        /// </summary>
        /// <param name="tabItem">The TabItem to set the model on</param>
        /// <param name="model">The TabModel to set</param>
        public static void SetTabModel(TabItem tabItem, TabModel model)
        {
            if (tabItem == null) return;
            
            try
            {
                tabItem.DataContext = model;
                
                // Clear Tag to avoid confusion
                if (tabItem.Tag is TabModel)
                {
                    tabItem.Tag = null;
                    _logger?.LogDebug("Cleared Tag property after setting DataContext");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting TabModel on TabItem");
            }
        }
        
        /// <summary>
        /// Gets current telemetry statistics
        /// </summary>
        public static TabResolutionStats GetStats()
        {
            return new TabResolutionStats
            {
                DataContextHits = _dataContextHitCount,
                TagFallbacks = _tagFallbackCount,
                Migrations = _migrationCount,
                NotFound = _notFoundCount,
                TagFallbackRate = CalculateFallbackRate()
            };
        }
        
        /// <summary>
        /// Resets telemetry counters (useful for testing)
        /// </summary>
        public static void ResetStats()
        {
            _dataContextHitCount = 0;
            _tagFallbackCount = 0;
            _migrationCount = 0;
            _notFoundCount = 0;
        }
        
        #endregion
        
        #region Private Methods
        
        private static bool IsEnabled()
        {
            if (!_isEnabled.HasValue)
            {
                // Check feature flag via environment variable first
                var envValue = Environment.GetEnvironmentVariable("FF_USE_TAB_MODEL_RESOLVER");
                if (bool.TryParse(envValue, out var envFlag))
                {
                    _isEnabled = envFlag;
                }
                else if (_settingsService != null)
                {
                    // Check settings service
                    _isEnabled = _settingsService.GetSetting("FeatureFlags.UseTabModelResolver", true);
                }
                else
                {
                    // Default to enabled
                    _isEnabled = true;
                }
                
                _logger?.LogInformation("TabModelResolver feature flag: {IsEnabled}", _isEnabled);
            }
            
            return _isEnabled.Value;
        }
        
        private static TabModel GetTabModelLegacy(TabItem tabItem)
        {
            // Legacy behavior - check DataContext first, then Tag
            if (tabItem?.DataContext is TabModel dataContextModel)
                return dataContextModel;
                
            if (tabItem?.Tag is TabModel tagModel)
                return tagModel;
                
            return null;
        }
        
        private static void MigrateToDataContext(TabItem tabItem, TabModel model)
        {
            try
            {
                tabItem.DataContext = model;
                tabItem.Tag = null; // Clear Tag after migration
                
                Interlocked.Increment(ref _migrationCount);
                
                _logger?.LogInformation("Migrated TabModel from Tag to DataContext for tab '{Title}'", 
                    model?.Title ?? "Unknown");
                    
                _telemetryService?.TrackEvent("TabModel.Migration", new Dictionary<string, object>
                {
                    ["TabTitle"] = model?.Title ?? "Unknown",
                    ["MigrationCount"] = _migrationCount
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error migrating TabModel to DataContext");
            }
        }
        
        private static double CalculateFallbackRate()
        {
            var total = _dataContextHitCount + _tagFallbackCount;
            return total > 0 ? (double)_tagFallbackCount / total * 100 : 0;
        }
        
        private static void LogTelemetryIfNeeded(string source)
        {
            // Log aggregate stats every 100 resolutions
            var total = _dataContextHitCount + _tagFallbackCount + _notFoundCount;
            if (total % 100 == 0 && total > 0)
            {
                var stats = GetStats();
                _logger?.LogInformation(
                    "TabModel Resolution Stats - DataContext: {DataContextHits}, Tag: {TagFallbacks}, " +
                    "NotFound: {NotFound}, Migrations: {Migrations}, FallbackRate: {FallbackRate:F2}%",
                    stats.DataContextHits, stats.TagFallbacks, stats.NotFound, 
                    stats.Migrations, stats.TagFallbackRate);
                    
                _telemetryService?.TrackEvent("TabModel.ResolutionStats", new Dictionary<string, object>
                {
                    ["DataContextHits"] = stats.DataContextHits,
                    ["TagFallbacks"] = stats.TagFallbacks,
                    ["NotFound"] = stats.NotFound,
                    ["Migrations"] = stats.Migrations,
                    ["FallbackRate"] = stats.TagFallbackRate
                });
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Statistics for TabModel resolution tracking
    /// </summary>
    public class TabResolutionStats
    {
        public int DataContextHits { get; set; }
        public int TagFallbacks { get; set; }
        public int Migrations { get; set; }
        public int NotFound { get; set; }
        public double TagFallbackRate { get; set; }
    }
}