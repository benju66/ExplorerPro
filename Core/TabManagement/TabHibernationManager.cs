using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.Core.Monitoring;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Specialized manager for tab hibernation and memory optimization.
    /// Provides intelligent hibernation strategies and state preservation.
    /// </summary>
    public class TabHibernationManager : IDisposable
    {
        #region Private Fields
        
        private readonly ILogger<TabHibernationManager> _logger;
        private readonly ResourceMonitor _resourceMonitor;
        private readonly Timer _hibernationTimer;
        private readonly Timer _cleanupTimer;
        
        private readonly ConcurrentDictionary<string, HibernatedTabData> _hibernatedTabs;
        private readonly ConcurrentDictionary<string, TabMemoryProfile> _memoryProfiles;
        private readonly ConcurrentQueue<HibernationCandidate> _hibernationQueue;
        private readonly ConcurrentDictionary<string, TabModel> _activeTabs;
        private readonly object _lock = new object();
        
        private readonly HibernationSettings _settings;
        private long _totalMemorySaved;
        private int _totalHibernated;
        private bool _disposed;
        
        #endregion

        #region Constructor
        
        public TabHibernationManager(
            ILogger<TabHibernationManager> logger = null,
            ResourceMonitor resourceMonitor = null,
            HibernationSettings settings = null)
        {
            _logger = logger;
            _resourceMonitor = resourceMonitor;
            _settings = settings ?? HibernationSettings.Default;
            
            _hibernatedTabs = new ConcurrentDictionary<string, HibernatedTabData>();
            _memoryProfiles = new ConcurrentDictionary<string, TabMemoryProfile>();
            _hibernationQueue = new ConcurrentQueue<HibernationCandidate>();
            _activeTabs = new ConcurrentDictionary<string, TabModel>();
            
            // Setup timers
            _hibernationTimer = new Timer(ProcessHibernationQueue, null, 
                _settings.HibernationInterval, _settings.HibernationInterval);
            
            _cleanupTimer = new Timer(CleanupExpiredData, null,
                _settings.CleanupInterval, _settings.CleanupInterval);
            
            // Wire up resource monitoring
            if (_resourceMonitor != null)
            {
                _resourceMonitor.HighMemoryPressure += OnHighMemoryPressure;
            }
            
            _logger?.LogInformation("TabHibernationManager initialized with aggressive hibernation: {Aggressive}", 
                _settings.AggressiveHibernation);
        }
        
        #endregion

        #region Events
        
        public event EventHandler<TabHibernationEventArgs> TabHibernated;
        public event EventHandler<TabReactivationEventArgs> TabReactivated;
        public event EventHandler<HibernationStatsEventArgs> StatsUpdated;
        
        #endregion

        #region Public Properties
        
        public int HibernatedCount => _hibernatedTabs.Count;
        public long TotalMemorySaved => _totalMemorySaved;
        public bool IsMemoryPressureActive { get; private set; }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Analyzes a tab for hibernation potential
        /// </summary>
        public async Task<HibernationAnalysis> AnalyzeTabAsync(TabModel tab)
        {
            if (tab == null) throw new ArgumentNullException(nameof(tab));
            
            var analysis = new HibernationAnalysis
            {
                TabId = tab.Id,
                CanHibernate = CanTabBeHibernated(tab),
                RecommendedAction = GetRecommendedAction(tab),
                EstimatedMemorySavings = await EstimateMemorySavingsAsync(tab),
                Priority = CalculateHibernationPriority(tab),
                TimeSinceLastAccess = tab.TimeSinceLastActivation
            };
            
            return analysis;
        }
        
        /// <summary>
        /// Queues a tab for hibernation
        /// </summary>
        public async Task QueueForHibernationAsync(TabModel tab, HibernationReason reason = HibernationReason.Automatic)
        {
            if (tab == null || !CanTabBeHibernated(tab)) return;
            
            var candidate = new HibernationCandidate
            {
                TabId = tab.Id,
                Tab = tab,
                QueuedAt = DateTime.UtcNow,
                Reason = reason,
                Priority = CalculateHibernationPriority(tab)
            };
            
            _hibernationQueue.Enqueue(candidate);
            
            _logger?.LogDebug("Tab queued for hibernation: {TabId} - Reason: {Reason}", 
                tab.Id, reason);
        }
        
        /// <summary>
        /// Immediately hibernates a tab
        /// </summary>
        public async Task<bool> HibernateTabAsync(TabModel tab, HibernationReason reason = HibernationReason.Manual)
        {
            if (tab == null || !CanTabBeHibernated(tab)) return false;
            
            try
            {
                var startTime = DateTime.UtcNow;
                
                // Create memory profile before hibernation
                var memoryProfile = await CreateMemoryProfileAsync(tab);
                _memoryProfiles[tab.Id] = memoryProfile;
                
                // Create hibernation data
                var hibernationData = new HibernatedTabData
                {
                    TabId = tab.Id,
                    OriginalTitle = tab.Title,
                    OriginalPath = tab.Path,
                    CustomColor = tab.CustomColor,
                    IsPinned = tab.IsPinned,
                    Metadata = tab.Metadata,
                    HibernatedAt = DateTime.UtcNow,
                    Reason = reason,
                    MemoryProfile = memoryProfile,
                    PreservationLevel = DeterminePreservationLevel(tab)
                };
                
                // Preserve essential state based on preservation level
                if (hibernationData.PreservationLevel >= PreservationLevel.Extended)
                {
                    hibernationData.ExtendedState = await CaptureExtendedStateAsync(tab);
                }
                
                // Clear tab content to free memory
                await ClearTabContentAsync(tab);
                
                // Update tab state
                tab.State = Models.TabState.Hibernated;
                
                // Store hibernation data
                _hibernatedTabs[tab.Id] = hibernationData;
                
                // Update statistics
                var memoryFreed = memoryProfile.EstimatedSize;
                Interlocked.Add(ref _totalMemorySaved, memoryFreed);
                Interlocked.Increment(ref _totalHibernated);
                
                var hibernationTime = DateTime.UtcNow - startTime;
                
                TabHibernated?.Invoke(this, new TabHibernationEventArgs
                {
                    TabId = tab.Id,
                    MemoryFreed = memoryFreed,
                    HibernatedAt = hibernationData.HibernatedAt,
                    Reason = reason,
                    HibernationTime = hibernationTime
                });
                
                _logger?.LogInformation("Tab hibernated: {TabId} - Memory freed: {Memory} KB - Time: {Time}ms", 
                    tab.Id, memoryFreed / 1024, hibernationTime.TotalMilliseconds);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error hibernating tab: {TabId}", tab.Id);
                return false;
            }
        }
        
        /// <summary>
        /// Reactivates a hibernated tab
        /// </summary>
        public async Task<bool> ReactivateTabAsync(TabModel tab)
        {
            if (tab == null || !_hibernatedTabs.ContainsKey(tab.Id)) return false;
            
            try
            {
                var startTime = DateTime.UtcNow;
                
                if (_hibernatedTabs.TryRemove(tab.Id, out var hibernationData))
                {
                    // Restore tab state
                    await RestoreTabStateAsync(tab, hibernationData);
                    
                    // Update tab state
                    tab.State = Models.TabState.Normal;
                    
                    // Update statistics
                    var memoryRestored = hibernationData.MemoryProfile.EstimatedSize;
                    Interlocked.Add(ref _totalMemorySaved, -memoryRestored);
                    
                    var reactivationTime = DateTime.UtcNow - startTime;
                    
                    TabReactivated?.Invoke(this, new TabReactivationEventArgs
                    {
                        TabId = tab.Id,
                        ReactivationTime = reactivationTime,
                        MemoryRestored = memoryRestored,
                        WasHibernatedFor = DateTime.UtcNow - hibernationData.HibernatedAt
                    });
                    
                    _logger?.LogInformation("Tab reactivated: {TabId} - Memory restored: {Memory} KB - Time: {Time}ms", 
                        tab.Id, memoryRestored / 1024, reactivationTime.TotalMilliseconds);
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reactivating tab: {TabId}", tab.Id);
                                                        tab.State = Models.TabState.Error;
            }
            
            return false;
        }
        
        /// <summary>
        /// Forces hibernation of multiple tabs based on priority
        /// </summary>
        public async Task<int> ForceHibernationAsync(IEnumerable<TabModel> tabs, int maxCount = 10)
        {
            var hibernatedCount = 0;
            var candidates = tabs
                .Where(CanTabBeHibernated)
                .OrderBy(CalculateHibernationPriority)
                .Take(maxCount);
            
            foreach (var tab in candidates)
            {
                if (await HibernateTabAsync(tab, HibernationReason.MemoryPressure))
                {
                    hibernatedCount++;
                }
            }
            
            _logger?.LogInformation("Force hibernation completed: {Count} tabs hibernated", hibernatedCount);
            return hibernatedCount;
        }
        
        /// <summary>
        /// Gets hibernation statistics
        /// </summary>
        public HibernationStats GetStatistics()
        {
            return new HibernationStats
            {
                TotalHibernated = _totalHibernated,
                CurrentlyHibernated = _hibernatedTabs.Count,
                TotalMemorySavedMB = _totalMemorySaved / (1024 * 1024),
                AverageHibernationDuration = CalculateAverageHibernationDuration(),
                IsMemoryPressureActive = IsMemoryPressureActive
            };
        }
        
        /// <summary>
        /// Registers a tab for hibernation monitoring
        /// </summary>
        public async Task RegisterTabAsync(TabModel tab)
        {
            if (tab == null) return;

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    if (!_activeTabs.ContainsKey(tab.Id))
                    {
                        _activeTabs[tab.Id] = tab;
                    }
                }
            });
        }

        /// <summary>
        /// Unregisters a tab from hibernation monitoring
        /// </summary>
        public async Task UnregisterTabAsync(string tabId)
        {
            if (string.IsNullOrEmpty(tabId)) return;

            await Task.Run(() =>
            {
                lock (_lock)
                {
                    _activeTabs.TryRemove(tabId, out _);
                    _hibernatedTabs.TryRemove(tabId, out _);
                }
            });
        }

        /// <summary>
        /// Optimizes hibernation based on current conditions
        /// </summary>
        public async Task<OptimizationResult> OptimizeAsync()
        {
            try
            {
                var startTime = DateTime.UtcNow;
                var initialMemory = GC.GetTotalMemory(false);
                
                var candidates = await GetHibernationCandidatesAsync();
                var hibernated = 0;
                
                foreach (var candidate in candidates.Take(5)) // Limit to 5 per optimization cycle
                {
                    if (await HibernateTabAsync(candidate.Tab))
                    {
                        hibernated++;
                    }
                }
                
                var finalMemory = GC.GetTotalMemory(false);
                var memorySaved = initialMemory - finalMemory;
                
                return new OptimizationResult
                {
                    Status = OptimizationStatus.Completed,
                    Service = "HibernationManager",
                    Success = true,
                    StartTime = startTime,
                    EndTime = DateTime.UtcNow,
                    Duration = DateTime.UtcNow - startTime,
                    MemorySaved = memorySaved,
                    TabsHibernated = hibernated
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to optimize hibernation");
                return new OptimizationResult
                {
                    Status = OptimizationStatus.Failed,
                    Service = "HibernationManager",
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }
        
        #endregion

        #region Private Methods
        
        private bool CanTabBeHibernated(TabModel tab)
        {
            if (tab == null) return false;
            if (tab.IsActive) return false;
            if (tab.IsPinned && !_settings.AllowPinnedHibernation) return false;
            if (tab.HasUnsavedChanges && !_settings.AllowUnsavedHibernation) return false;
            if (tab.State == Models.TabState.Hibernated) return false;
            if (tab.State == Models.TabState.Loading) return false;
            if (tab.State == Models.TabState.Error) return false;
            
            return true;
        }
        
        private HibernationAction GetRecommendedAction(TabModel tab)
        {
            if (!CanTabBeHibernated(tab)) return HibernationAction.None;
            
            var timeSinceAccess = tab.TimeSinceLastActivation;
            var priority = CalculateHibernationPriority(tab);
            
            if (IsMemoryPressureActive && priority <= 2) return HibernationAction.Immediate;
            if (timeSinceAccess > _settings.AggressiveHibernationThreshold) return HibernationAction.Immediate;
            if (timeSinceAccess > _settings.StandardHibernationThreshold) return HibernationAction.Scheduled;
            
            return HibernationAction.Monitor;
        }
        
        private int CalculateHibernationPriority(TabModel tab)
        {
            int priority = 5; // Default medium priority
            
            // Lower number = higher hibernation priority (hibernate sooner)
            if (tab.IsPinned) priority += 10;
            if (tab.HasUnsavedChanges) priority += 5;
            if (tab.TimeSinceLastActivation < TimeSpan.FromMinutes(5)) priority += 8;
            if (tab.TimeSinceLastActivation < TimeSpan.FromMinutes(30)) priority += 3;
            if (tab.TimeSinceLastActivation > TimeSpan.FromHours(2)) priority -= 3;
            if (tab.TimeSinceLastActivation > TimeSpan.FromHours(24)) priority -= 5;
            
            return Math.Max(0, priority);
        }
        
        private async Task<TabMemoryProfile> CreateMemoryProfileAsync(TabModel tab)
        {
            return await Task.FromResult(new TabMemoryProfile
            {
                TabId = tab.Id,
                EstimatedSize = EstimateTabMemoryUsage(tab),
                ContentType = DetermineContentType(tab),
                HasLargeContent = HasLargeContent(tab),
                ProfiledAt = DateTime.UtcNow
            });
        }
        
        private long EstimateTabMemoryUsage(TabModel tab)
        {
            // Base memory for tab structure
            long estimate = 50 * 1024; // 50KB base
            
            // Add content-based estimates
            if (tab.Content != null) estimate += 500 * 1024; // 500KB for content
            if (tab.Metadata != null) estimate += 100 * 1024; // 100KB for metadata
            
            // Adjust based on tab characteristics
            if (HasLargeContent(tab)) estimate *= 3;
            
            return estimate;
        }
        
        private async Task<long> EstimateMemorySavingsAsync(TabModel tab)
        {
            var profile = await CreateMemoryProfileAsync(tab);
            
            // Consider preservation overhead
            var preservationOverhead = profile.EstimatedSize * 0.1; // 10% overhead
            return (long)(profile.EstimatedSize - preservationOverhead);
        }
        
        private ContentType DetermineContentType(TabModel tab)
        {
            // Simple content type determination
            if (string.IsNullOrEmpty(tab.Path)) return ContentType.Unknown;
            if (tab.Path.Contains("image")) return ContentType.Image;
            if (tab.Path.Contains("document")) return ContentType.Document;
            return ContentType.FileSystem;
        }
        
        private bool HasLargeContent(TabModel tab)
        {
            // Heuristic for large content detection
            return tab.Content != null && tab.Metadata != null;
        }
        
        private PreservationLevel DeterminePreservationLevel(TabModel tab)
        {
            if (tab.HasUnsavedChanges) return PreservationLevel.Full;
            if (tab.IsPinned) return PreservationLevel.Extended;
            if (tab.TimeSinceLastActivation < TimeSpan.FromHours(1)) return PreservationLevel.Extended;
            return PreservationLevel.Basic;
        }
        
        private async Task<object> CaptureExtendedStateAsync(TabModel tab)
        {
            // Capture additional state for extended preservation
            return await Task.FromResult(new
            {
                ViewState = "captured",
                ScrollPosition = 0,
                SelectionState = "preserved"
            });
        }
        
        private async Task<List<HibernationCandidate>> GetHibernationCandidatesAsync()
        {
            return await Task.Run(() =>
            {
                var candidates = new List<HibernationCandidate>();
                
                lock (_lock)
                {
                    foreach (var tab in _activeTabs.Values)
                    {
                        if (CanTabBeHibernated(tab))
                        {
                            candidates.Add(new HibernationCandidate
                            {
                                TabId = tab.Id,
                                Tab = tab,
                                QueuedAt = DateTime.UtcNow,
                                Reason = HibernationReason.Automatic,
                                Priority = CalculateHibernationPriority(tab)
                            });
                        }
                    }
                }
                
                return candidates.OrderBy(c => c.Priority).ToList();
            });
        }
        
        private async Task ClearTabContentAsync(TabModel tab)
        {
            await Task.Run(() =>
            {
                if (tab.Content is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                tab.Content = null;
            });
        }
        
        private async Task RestoreTabStateAsync(TabModel tab, HibernatedTabData hibernationData)
        {
            await Task.Run(() =>
            {
                // Restore basic properties
                tab.Title = hibernationData.OriginalTitle;
                tab.Path = hibernationData.OriginalPath;
                tab.CustomColor = hibernationData.CustomColor;
                tab.IsPinned = hibernationData.IsPinned;
                tab.Metadata = hibernationData.Metadata as Dictionary<string, object> ?? new Dictionary<string, object>();
                
                // Restore extended state if available
                if (hibernationData.ExtendedState != null && 
                    hibernationData.PreservationLevel >= PreservationLevel.Extended)
                {
                    // Restore extended state
                }
            });
        }
        
        private void ProcessHibernationQueue(object state)
        {
            if (_disposed) return;
            
            var processedCount = 0;
            var maxProcessPerCycle = _settings.MaxHibernationsPerCycle;
            
            while (processedCount < maxProcessPerCycle && _hibernationQueue.TryDequeue(out var candidate))
            {
                if (CanTabBeHibernated(candidate.Tab) && 
                    DateTime.UtcNow - candidate.QueuedAt >= _settings.HibernationDelay)
                {
                    Task.Run(async () => await HibernateTabAsync(candidate.Tab, candidate.Reason));
                }
                
                processedCount++;
            }
            
            // Emit statistics periodically
            if (processedCount > 0)
            {
                EmitStatistics();
            }
        }
        
        private void CleanupExpiredData(object state)
        {
            if (_disposed) return;
            
            var cutoff = DateTime.UtcNow - _settings.DataRetentionPeriod;
            var toRemove = _memoryProfiles
                .Where(kvp => kvp.Value.ProfiledAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();
            
            foreach (var key in toRemove)
            {
                _memoryProfiles.TryRemove(key, out _);
            }
            
            _logger?.LogDebug("Cleaned up {Count} expired memory profiles", toRemove.Count);
        }
        
        private void OnHighMemoryPressure(object sender, MemoryPressureEventArgs e)
        {
            IsMemoryPressureActive = true;
            _logger?.LogWarning("High memory pressure detected - enabling aggressive hibernation");
            
            // Trigger immediate hibernation of eligible tabs
            Task.Run(async () =>
            {
                // Process hibernation queue more aggressively
                await Task.Delay(100); // Brief delay to avoid overwhelming
                ProcessHibernationQueue(null);
            });
        }
        
        private TimeSpan CalculateAverageHibernationDuration()
        {
            var durations = _hibernatedTabs.Values
                .Select(h => DateTime.UtcNow - h.HibernatedAt)
                .ToList();
            
            if (!durations.Any()) return TimeSpan.Zero;
            
            var averageTicks = (long)durations.Average(d => d.Ticks);
            return new TimeSpan(averageTicks);
        }
        
        private void EmitStatistics()
        {
            StatsUpdated?.Invoke(this, new HibernationStatsEventArgs
            {
                Stats = GetStatistics()
            });
        }
        
        #endregion

        #region Disposal
        
        public void Dispose()
        {
            if (_disposed) return;
            
            _disposed = true;
            
            _hibernationTimer?.Dispose();
            _cleanupTimer?.Dispose();
            
            if (_resourceMonitor != null)
            {
                _resourceMonitor.HighMemoryPressure -= OnHighMemoryPressure;
            }
            
            _hibernatedTabs.Clear();
            _memoryProfiles.Clear();
            
            _logger?.LogInformation("TabHibernationManager disposed - Total hibernated: {Count}, Memory saved: {Memory}MB", 
                _totalHibernated, _totalMemorySaved / (1024 * 1024));
        }
        
        #endregion
    }
} 