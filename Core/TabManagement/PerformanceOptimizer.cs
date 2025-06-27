using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.Core.Monitoring;
using ExplorerPro.UI.Controls;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Main performance optimization engine that coordinates all performance-related subsystems.
    /// Provides intelligent resource management and optimization strategies.
    /// </summary>
    public class PerformanceOptimizer : IDisposable
    {
        #region Private Fields
        
            private readonly ILogger<PerformanceOptimizer> _logger;
    private readonly ResourceMonitor _resourceMonitor;
    private readonly UI.Controls.TabVirtualizationManager _virtualizationManager;
    private readonly TabHibernationManager _hibernationManager;
        private readonly DispatcherTimer _optimizationTimer;
        
        private readonly PerformanceSettings _settings;
        private readonly PerformanceMetrics _metrics;
        private readonly object _optimizationLock = new object();
        
        private bool _disposed;
        private bool _optimizationInProgress;
        
        #endregion

        #region Constructor
        
            public PerformanceOptimizer(
        ILogger<PerformanceOptimizer> logger = null,
        ResourceMonitor resourceMonitor = null,
        UI.Controls.TabVirtualizationManager virtualizationManager = null,
        TabHibernationManager hibernationManager = null,
        PerformanceSettings settings = null)
        {
            _logger = logger;
            _resourceMonitor = resourceMonitor ?? new ResourceMonitor();
            _virtualizationManager = virtualizationManager;
            _hibernationManager = hibernationManager;
            _settings = settings ?? PerformanceSettings.Default;
            _metrics = new PerformanceMetrics();
            
            // Setup optimization timer
            _optimizationTimer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = _settings.OptimizationInterval
            };
            _optimizationTimer.Tick += OnOptimizationTimer;
            _optimizationTimer.Start();
            
            // Wire up events
            WireUpEvents();
            
            _logger?.LogInformation("PerformanceOptimizer initialized with interval: {Interval}", 
                _settings.OptimizationInterval);
        }
        
        #endregion

        #region Events
        
        public event EventHandler<OptimizationCompletedEventArgs> OptimizationCompleted;
        public event EventHandler<PerformanceThresholdEventArgs> ThresholdExceeded;
        
        #endregion

        #region Public Properties
        
        public PerformanceMetrics CurrentMetrics => _metrics;
        public bool IsOptimizationEnabled { get; set; } = true;
        public bool IsOptimizationInProgress => _optimizationInProgress;
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Performs comprehensive performance optimization
        /// </summary>
        public async Task<OptimizationResult> OptimizeAsync(OptimizationOptions options = null)
        {
            if (_disposed || !IsOptimizationEnabled) 
                return OptimizationResult.Skipped;
            
            lock (_optimizationLock)
            {
                if (_optimizationInProgress) return OptimizationResult.AlreadyInProgress;
                _optimizationInProgress = true;
            }
            
            try
            {
                var startTime = DateTime.UtcNow;
                var result = new OptimizationResult { StartTime = startTime };
                
                options = options ?? OptimizationOptions.Default;
                
                _logger?.LogDebug("Starting performance optimization - Options: {Options}", options);
                
                // Step 1: Resource analysis
                var resourceSnapshot = _resourceMonitor.GetCurrentSnapshot();
                result.InitialMemoryUsage = resourceSnapshot.WorkingSetMB;
                
                // Step 2: Tab virtualization optimization
                if (options.OptimizeVirtualization && _virtualizationManager != null)
                {
                    await OptimizeVirtualizationAsync(result);
                }
                
                // Step 3: Hibernation optimization
                if (options.OptimizeHibernation && _hibernationManager != null)
                {
                    await OptimizeHibernationAsync(result);
                }
                
                // Step 4: Memory cleanup
                if (options.ForceGarbageCollection)
                {
                    await ForceMemoryCleanupAsync(result);
                }
                
                // Step 5: Update metrics
                await UpdatePerformanceMetricsAsync(result);
                
                // Finalize result
                result.EndTime = DateTime.UtcNow;
                result.Duration = result.EndTime - result.StartTime;
                result.FinalMemoryUsage = _resourceMonitor.GetCurrentSnapshot().WorkingSetMB;
                result.MemorySaved = Math.Max(0, result.InitialMemoryUsage - result.FinalMemoryUsage);
                result.Status = OptimizationStatus.Completed;
                
                OptimizationCompleted?.Invoke(this, new OptimizationCompletedEventArgs { Result = result });
                
                _logger?.LogInformation("Optimization completed - Duration: {Duration}ms, Memory saved: {Memory}MB", 
                    result.Duration.TotalMilliseconds, result.MemorySaved);
                
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during performance optimization");
                return new OptimizationResult 
                { 
                    Status = OptimizationStatus.Failed, 
                    ErrorMessage = ex.Message 
                };
            }
            finally
            {
                _optimizationInProgress = false;
            }
        }
        
        /// <summary>
        /// Analyzes current performance and returns recommendations
        /// </summary>
        public async Task<PerformanceAnalysis> AnalyzePerformanceAsync()
        {
            var snapshot = _resourceMonitor.GetCurrentSnapshot();
            var analysis = new PerformanceAnalysis
            {
                Timestamp = DateTime.UtcNow,
                MemoryUsageMB = snapshot.WorkingSetMB,
                ThreadCount = snapshot.ThreadCount,
                HandleCount = snapshot.HandleCount,
                Recommendations = new List<PerformanceRecommendation>()
            };
            
            // Memory analysis
            if (snapshot.WorkingSetMB > _settings.MemoryWarningThresholdMB)
            {
                analysis.Recommendations.Add(new PerformanceRecommendation
                {
                    Type = RecommendationType.Memory,
                    Priority = snapshot.WorkingSetMB > _settings.MemoryCriticalThresholdMB ? 
                        RecommendationPriority.High : RecommendationPriority.Medium,
                    Description = $"High memory usage detected: {snapshot.WorkingSetMB}MB",
                    SuggestedActions = GetMemoryOptimizationActions(snapshot.WorkingSetMB)
                });
            }
            
            // Virtualization analysis
            if (_virtualizationManager != null)
            {
                if (_virtualizationManager.TotalTabs > _settings.VirtualizationRecommendationThreshold)
                {
                    analysis.Recommendations.Add(new PerformanceRecommendation
                    {
                        Type = RecommendationType.Virtualization,
                        Priority = RecommendationPriority.Medium,
                        Description = $"Consider enabling virtualization for {_virtualizationManager.TotalTabs} tabs",
                        SuggestedActions = new[] { "Enable tab virtualization", "Increase hibernation aggressiveness" }
                    });
                }
            }
            
            // Threading analysis
            if (snapshot.ThreadCount > _settings.ThreadWarningThreshold)
            {
                analysis.Recommendations.Add(new PerformanceRecommendation
                {
                    Type = RecommendationType.Threading,
                    Priority = RecommendationPriority.Low,
                    Description = $"High thread count: {snapshot.ThreadCount}",
                    SuggestedActions = new[] { "Review background operations", "Optimize async patterns" }
                });
            }
            
            analysis.OverallScore = CalculatePerformanceScore(analysis);
            return analysis;
        }
        
        /// <summary>
        /// Forces aggressive optimization for emergency situations
        /// </summary>
        public async Task<OptimizationResult> EmergencyOptimizeAsync()
        {
            _logger?.LogWarning("Emergency optimization triggered");
            
            var options = new OptimizationOptions
            {
                OptimizeVirtualization = true,
                OptimizeHibernation = true,
                ForceGarbageCollection = true,
                AggressiveMode = true
            };
            
            return await OptimizeAsync(options);
        }
        
        #endregion

        #region Private Optimization Methods
        
        private async Task OptimizeVirtualizationAsync(OptimizationResult result)
        {
            if (_virtualizationManager == null) return;
            
            try
            {
                await _virtualizationManager.OptimizeVisibilityAsync();
                result.VirtualizationOptimized = true;
                
                _logger?.LogDebug("Virtualization optimization completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error optimizing virtualization");
                result.Errors.Add($"Virtualization optimization failed: {ex.Message}");
            }
        }
        
        private async Task OptimizeHibernationAsync(OptimizationResult result)
        {
            if (_hibernationManager == null) return;
            
            try
            {
                var stats = _hibernationManager.GetStatistics();
                result.InitialHibernatedTabs = stats.CurrentlyHibernated;
                
                // Force hibernation of eligible tabs if memory pressure is high
                var snapshot = _resourceMonitor.GetCurrentSnapshot();
                if (snapshot.WorkingSetMB > _settings.MemoryWarningThresholdMB)
                {
                    var hibernatedCount = await _hibernationManager.ForceHibernationAsync(
                        new List<TabModel>(), // Would get from tab manager
                        maxCount: 10);
                    
                    result.TabsHibernated = hibernatedCount;
                }
                
                result.HibernationOptimized = true;
                
                _logger?.LogDebug("Hibernation optimization completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error optimizing hibernation");
                result.Errors.Add($"Hibernation optimization failed: {ex.Message}");
            }
        }
        
        private async Task ForceMemoryCleanupAsync(OptimizationResult result)
        {
            try
            {
                await Task.Run(() =>
                {
                    GC.Collect(2, GCCollectionMode.Forced);
                    GC.WaitForPendingFinalizers();
                    GC.Collect(2, GCCollectionMode.Forced);
                });
                
                result.GarbageCollectionForced = true;
                
                _logger?.LogDebug("Forced garbage collection completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during forced garbage collection");
                result.Errors.Add($"Garbage collection failed: {ex.Message}");
            }
        }
        
        private async Task UpdatePerformanceMetricsAsync(OptimizationResult result)
        {
            await Task.Run(() =>
            {
                _metrics.LastOptimization = DateTime.UtcNow;
                _metrics.TotalOptimizations++;
                _metrics.TotalTimeSaved += result.Duration;
                
                if (result.MemorySaved > 0)
                {
                    _metrics.TotalMemorySaved += result.MemorySaved;
                }
            });
        }
        
        #endregion

        #region Analysis and Scoring
        
        private string[] GetMemoryOptimizationActions(long memoryUsageMB)
        {
            var actions = new List<string>();
            
            if (memoryUsageMB > _settings.MemoryCriticalThresholdMB)
            {
                actions.Add("Force hibernation of inactive tabs");
                actions.Add("Clear cached resources");
                actions.Add("Force garbage collection");
            }
            else if (memoryUsageMB > _settings.MemoryWarningThresholdMB)
            {
                actions.Add("Enable aggressive hibernation");
                actions.Add("Optimize tab virtualization");
                actions.Add("Schedule memory cleanup");
            }
            
            return actions.ToArray();
        }
        
        private int CalculatePerformanceScore(PerformanceAnalysis analysis)
        {
            int score = 100; // Start with perfect score
            
            // Deduct points for issues
            foreach (var recommendation in analysis.Recommendations)
            {
                switch (recommendation.Priority)
                {
                    case RecommendationPriority.High:
                        score -= 30;
                        break;
                    case RecommendationPriority.Medium:
                        score -= 15;
                        break;
                    case RecommendationPriority.Low:
                        score -= 5;
                        break;
                }
            }
            
            return Math.Max(0, score);
        }
        
        #endregion

        #region Event Handlers
        
        private void WireUpEvents()
        {
            if (_resourceMonitor != null)
            {
                _resourceMonitor.HighMemoryPressure += OnHighMemoryPressure;
                _resourceMonitor.ResourceUsageUpdated += OnResourceUsageUpdated;
            }
        }
        
        private void OnOptimizationTimer(object sender, EventArgs e)
        {
            if (!IsOptimizationEnabled || _optimizationInProgress) return;
            
            // Periodic optimization
            Task.Run(async () =>
            {
                var options = new OptimizationOptions
                {
                    OptimizeVirtualization = true,
                    OptimizeHibernation = false, // Less aggressive for periodic optimization
                    ForceGarbageCollection = false
                };
                
                await OptimizeAsync(options);
            });
        }
        
        private void OnHighMemoryPressure(object sender, MemoryPressureEventArgs e)
        {
            _logger?.LogWarning("High memory pressure detected - triggering emergency optimization");
            
            Task.Run(async () => await EmergencyOptimizeAsync());
            
            ThresholdExceeded?.Invoke(this, new PerformanceThresholdEventArgs
            {
                ThresholdType = ThresholdType.Memory,
                CurrentValue = e.CurrentWorkingSetMB,
                ThresholdValue = _settings.MemoryWarningThresholdMB,
                Recommendation = e.Recommendation
            });
        }
        
        private void OnResourceUsageUpdated(object sender, ResourceUsageEventArgs e)
        {
            // Update real-time metrics
            _metrics.CurrentMemoryUsageMB = e.WorkingSetMB;
            _metrics.CurrentThreadCount = e.ThreadCount;
            _metrics.LastUpdate = DateTime.UtcNow;
            
            // Check thresholds
            CheckPerformanceThresholds(e);
        }
        
        private void CheckPerformanceThresholds(ResourceUsageEventArgs usage)
        {
            // Check memory threshold
            if (usage.WorkingSetMB > _settings.MemoryWarningThresholdMB)
            {
                ThresholdExceeded?.Invoke(this, new PerformanceThresholdEventArgs
                {
                    ThresholdType = ThresholdType.Memory,
                    CurrentValue = usage.WorkingSetMB,
                    ThresholdValue = _settings.MemoryWarningThresholdMB,
                    Recommendation = "Consider hibernating inactive tabs or reducing memory usage"
                });
            }
            
            // Check thread threshold
            if (usage.ThreadCount > _settings.ThreadWarningThreshold)
            {
                ThresholdExceeded?.Invoke(this, new PerformanceThresholdEventArgs
                {
                    ThresholdType = ThresholdType.Threads,
                    CurrentValue = usage.ThreadCount,
                    ThresholdValue = _settings.ThreadWarningThreshold,
                    Recommendation = "Review background operations and async patterns"
                });
            }
        }
        
        #endregion

        #region Disposal
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            IsOptimizationEnabled = false;
            
            _optimizationTimer?.Stop();
            
            if (_resourceMonitor != null)
            {
                _resourceMonitor.HighMemoryPressure -= OnHighMemoryPressure;
                _resourceMonitor.ResourceUsageUpdated -= OnResourceUsageUpdated;
            }
            
            _logger?.LogInformation("PerformanceOptimizer disposed - Total optimizations: {Count}, Memory saved: {Memory}MB", 
                _metrics.TotalOptimizations, _metrics.TotalMemorySaved);
        }
        
        #endregion
    }
} 