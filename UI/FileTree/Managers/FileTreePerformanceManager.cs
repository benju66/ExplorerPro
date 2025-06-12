// UI/FileTree/Managers/FileTreePerformanceManager.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using System.Threading;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Utilities;

namespace ExplorerPro.UI.FileTree.Managers
{
    /// <summary>
    /// Handles all performance optimizations for the FileTreeListView including caching, 
    /// visual tree management, and hit testing.
    /// </summary>
    public class FileTreePerformanceManager : IDisposable
    {
        #region Private Fields

        private readonly TreeView _treeView;
        private ScrollViewer _scrollViewer;
        
        // Cache for TreeViewItem lookups to avoid repeated visual tree traversal
        private readonly Dictionary<FileTreeItem, WeakReference> _treeViewItemCache = new Dictionary<FileTreeItem, WeakReference>();
        private readonly ReaderWriterLockSlim _cacheLock = new ReaderWriterLockSlim();
        
        // Cleanup timer and statistics
        private readonly DispatcherTimer _cleanupTimer;
        private readonly object _cleanupStatsLock = new object();
        private CleanupStatistics _cleanupStats = new CleanupStatistics();
        
        // Track currently visible TreeViewItems for efficient updates
        private readonly HashSet<TreeViewItem> _visibleTreeViewItems = new HashSet<TreeViewItem>();
        
        // Hit test cache for drag & drop performance
        private readonly Dictionary<Point, CachedHitTestResult> _hitTestCache = new Dictionary<Point, CachedHitTestResult>();
        private readonly Queue<Point> _cacheKeyQueue = new Queue<Point>();
        private const int HIT_TEST_CACHE_SIZE = 20;
        private const double HIT_TEST_POSITION_TOLERANCE = 3.0;
        
        // Performance metrics
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        private int _cacheHitCount = 0;
        private int _cacheMissCount = 0;
        
        private bool _disposed = false;

        // Cleanup configuration
        private const int CLEANUP_INTERVAL_SECONDS = 45; // 45 seconds - middle of requested range
        private const int INITIAL_CLEANUP_DELAY_SECONDS = 60; // Wait 1 minute before first cleanup

        #endregion

        #region Events

        public event EventHandler? VisibleItemsCacheUpdated;
        public event EventHandler? SelectionUpdateRequested;
        public event EventHandler<CleanupCompletedEventArgs>? CleanupCompleted;

        #endregion

        #region Constructor

        public FileTreePerformanceManager(TreeView treeView, ScrollViewer explicitScrollViewer = null)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            
            // If explicit ScrollViewer provided, use it; otherwise find it within TreeView
            if (explicitScrollViewer != null)
            {
                _scrollViewer = explicitScrollViewer;
            }
            else
            {
                // Defer finding the ScrollViewer until the template is applied
                _treeView.Loaded += OnTreeViewLoaded;
                if (_treeView.IsLoaded)
                {
                    FindScrollViewer();
                }
            }
            
            // Initialize cleanup timer
            _cleanupTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(CLEANUP_INTERVAL_SECONDS)
            };
            _cleanupTimer.Tick += OnCleanupTimer;
            
            Initialize();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }
            
            if (_treeView.ItemContainerGenerator != null)
            {
                _treeView.ItemContainerGenerator.StatusChanged += OnContainerGeneratorStatusChanged;
            }
            
            // Start cleanup timer with initial delay
            ScheduleFirstCleanup();
            
            LogDebug("FileTreePerformanceManager initialized with cleanup timer");
        }

        private void ScheduleFirstCleanup()
        {
            // Use a one-time timer for the initial delay
            var initialTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(INITIAL_CLEANUP_DELAY_SECONDS)
            };
            
            initialTimer.Tick += (sender, e) =>
            {
                initialTimer.Stop();
                if (!_disposed)
                {
                    _cleanupTimer.Start();
                    LogDebug("Cleanup timer started after initial delay");
                }
            };
            
            initialTimer.Start();
        }

        private void OnTreeViewLoaded(object sender, RoutedEventArgs e)
        {
            _treeView.Loaded -= OnTreeViewLoaded;
            FindScrollViewer();
        }

        private void FindScrollViewer()
        {
            // Find the ScrollViewer within the TreeView's template - but don't attach events if it's working properly
            _scrollViewer = VisualTreeHelperEx.FindScrollViewer(_treeView);
            
            // Only attach if we found it and it's not disposed, but let it handle its own scrolling behavior
            if (_scrollViewer != null && !_disposed)
            {
                // Only attach scroll changed event for performance tracking, not manipulation
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets a TreeViewItem for the given data item using caching for performance
        /// </summary>
        public TreeViewItem GetTreeViewItemCached(FileTreeItem dataItem)
        {
            if (dataItem == null) return null;
            
            // Thread-safe cache access
            _cacheLock.EnterReadLock();
            try
            {
                // Check cache first
                if (_treeViewItemCache.TryGetValue(dataItem, out WeakReference weakRef) && 
                    weakRef.Target is TreeViewItem cachedItem && 
                    cachedItem.DataContext == dataItem)
                {
                    Interlocked.Increment(ref _cacheHitCount);
                    return cachedItem;
                }
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
            
            // Not in cache or stale, find it
            Interlocked.Increment(ref _cacheMissCount);
            var treeViewItem = VisualTreeHelperEx.FindTreeViewItemOptimized(_treeView, dataItem);
            
            // Update cache with thread safety
            if (treeViewItem != null)
            {
                _cacheLock.EnterWriteLock();
                try
                {
                    _treeViewItemCache[dataItem] = new WeakReference(treeViewItem);
                }
                finally
                {
                    _cacheLock.ExitWriteLock();
                }
            }
            
            return treeViewItem;
        }

        /// <summary>
        /// Gets an item from a point using cached hit testing
        /// </summary>
        public FileTreeItem GetItemFromPoint(Point point)
        {
            // Check cache first
            var cachedResult = GetCachedHitTestResult(point);
            if (cachedResult != null && cachedResult.IsValid)
            {
                return cachedResult.Item;
            }
            
            // Perform hit test
            var hitTestResult = VisualTreeHelper.HitTest(_treeView, point);
            var treeViewItem = hitTestResult != null ? VisualTreeHelperEx.FindAncestor<TreeViewItem>(hitTestResult.VisualHit) : null;
            var item = treeViewItem != null ? treeViewItem.DataContext as FileTreeItem : null;
            
            // Cache the result
            CacheHitTestResult(point, item);
            
            return item;
        }

        /// <summary>
        /// Gets all TreeViewItems efficiently using cache when possible
        /// </summary>
        public IEnumerable<TreeViewItem> GetAllTreeViewItemsFast()
        {
            if (_visibleTreeViewItems.Count > 0 && _visibleTreeViewItems.Count < 100)
            {
                return _visibleTreeViewItems.ToList();
            }
            
            return GetExpandedTreeViewItems(_treeView);
        }

        /// <summary>
        /// Gets all visible TreeViewItems
        /// </summary>
        public IEnumerable<TreeViewItem> GetAllVisibleTreeViewItems()
        {
            return VisualTreeHelperEx.FindVisualChildren<TreeViewItem>(_treeView)
                .Where(item => item.IsVisible);
        }

        /// <summary>
        /// Updates the visible items cache for performance optimization
        /// </summary>
        public void UpdateVisibleItemsCache()
        {
            _visibleTreeViewItems.Clear();
            
            if (_scrollViewer == null)
            {
                // Try to find ScrollViewer again if not found yet
                FindScrollViewer();
                if (_scrollViewer == null) return;
            }
            
            // Get visible bounds
            var visibleBounds = new Rect(0, _scrollViewer.VerticalOffset, 
                                       _scrollViewer.ViewportWidth, 
                                       _scrollViewer.ViewportHeight);
            
            // Find visible TreeViewItems
            foreach (var item in GetAllTreeViewItemsFast())
            {
                var bounds = VisualTreeHelperEx.GetBounds(item, _treeView);
                if (visibleBounds.IntersectsWith(bounds))
                {
                    _visibleTreeViewItems.Add(item);
                }
            }
            
            _lastCacheUpdate = DateTime.Now;
            if (VisibleItemsCacheUpdated != null)
            {
                VisibleItemsCacheUpdated.Invoke(this, EventArgs.Empty);
            }
            
            System.Diagnostics.Debug.WriteLine($"[PERF] Visible items cache updated: {_visibleTreeViewItems.Count} items");
        }

        /// <summary>
        /// Clears all caches
        /// </summary>
        public void ClearAllCaches()
        {
            ClearTreeViewItemCache();
            ClearHitTestCache();
        }

        /// <summary>
        /// Gets performance statistics including cleanup stats
        /// </summary>
        public PerformanceStats GetPerformanceStats()
        {
            CleanupStatistics cleanupStatsCopy;
            lock (_cleanupStatsLock)
            {
                cleanupStatsCopy = _cleanupStats.Clone();
            }
            
            int cacheCount;
            _cacheLock.EnterReadLock();
            try
            {
                cacheCount = _treeViewItemCache.Count;
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }
            
            return new PerformanceStats
            {
                CacheHitCount = _cacheHitCount,
                CacheMissCount = _cacheMissCount,
                CacheHitRatio = _cacheHitCount + _cacheMissCount > 0 ? 
                    (double)_cacheHitCount / (_cacheHitCount + _cacheMissCount) : 0.0,
                VisibleItemsCount = _visibleTreeViewItems.Count,
                CachedItemsCount = cacheCount,
                LastCacheUpdate = _lastCacheUpdate,
                CleanupStats = cleanupStatsCopy
            };
        }

        /// <summary>
        /// Gets current cleanup statistics
        /// </summary>
        public CleanupStatistics GetCleanupStatistics()
        {
            lock (_cleanupStatsLock)
            {
                return _cleanupStats.Clone();
            }
        }

        /// <summary>
        /// Forces an immediate cleanup of dead cache entries
        /// </summary>
        public void ForceCleanup()
        {
            if (_disposed) return;
            
            LogDebug("Manual cleanup requested");
            PerformCacheCleanup();
        }

        /// <summary>
        /// Schedules a selection update (for coordinator compatibility)
        /// </summary>
        public void ScheduleSelectionUpdate()
        {
            if (SelectionUpdateRequested != null)
            {
                SelectionUpdateRequested.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Invalidates cache for a specific directory
        /// </summary>
        public void InvalidateDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath)) return;
            
            _cacheLock.EnterWriteLock();
            try
            {
                var itemsToRemove = _treeViewItemCache.Keys
                    .Where(item => item != null && item.Path != null && item.Path.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                foreach (var item in itemsToRemove)
                {
                    _treeViewItemCache.Remove(item);
                }
                
                LogDebug($"Invalidated cache for directory: {directoryPath} ({itemsToRemove.Count} items removed)");
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        #endregion

        #region Cache Cleanup Implementation

        private void OnCleanupTimer(object sender, EventArgs e)
        {
            if (_disposed) return;
            
            // Perform cleanup on a background thread to avoid blocking UI
            ThreadPool.QueueUserWorkItem(_ => PerformCacheCleanup());
        }

        private void PerformCacheCleanup()
        {
            if (_disposed) return;
            
            var startTime = DateTime.Now;
            var initialCount = 0;
            var deadKeysRemoved = 0;
            var liveCacheUpdated = 0;
            
            try
            {
                var deadKeys = new List<FileTreeItem>();
                var liveEntriesToUpdate = new List<KeyValuePair<FileTreeItem, WeakReference>>();
                
                // First pass: collect information under read lock
                _cacheLock.EnterReadLock();
                try
                {
                    initialCount = _treeViewItemCache.Count;
                    
                    foreach (var kvp in _treeViewItemCache)
                    {
                        if (kvp.Value == null || !kvp.Value.IsAlive)
                        {
                            deadKeys.Add(kvp.Key);
                        }
                        else if (kvp.Value.Target is TreeViewItem tvi && tvi.DataContext != kvp.Key)
                        {
                            // TreeViewItem exists but DataContext doesn't match - stale entry
                            deadKeys.Add(kvp.Key);
                        }
                        else
                        {
                            // This is a live entry, keep it
                            liveEntriesToUpdate.Add(kvp);
                        }
                    }
                }
                finally
                {
                    _cacheLock.ExitReadLock();
                }
                
                // Second pass: remove dead entries under write lock
                if (deadKeys.Count > 0)
                {
                    _cacheLock.EnterWriteLock();
                    try
                    {
                        foreach (var deadKey in deadKeys)
                        {
                            if (_treeViewItemCache.Remove(deadKey))
                            {
                                deadKeysRemoved++;
                            }
                        }
                    }
                    finally
                    {
                        _cacheLock.ExitWriteLock();
                    }
                }
                
                var duration = DateTime.Now - startTime;
                
                // Update statistics
                lock (_cleanupStatsLock)
                {
                    _cleanupStats.TotalCleanupsPerformed++;
                    _cleanupStats.TotalDeadEntriesRemoved += deadKeysRemoved;
                    _cleanupStats.LastCleanupTime = startTime;
                    _cleanupStats.LastCleanupDuration = duration;
                    _cleanupStats.AverageCleanupDuration = TimeSpan.FromMilliseconds(
                        (_cleanupStats.AverageCleanupDuration.TotalMilliseconds * (_cleanupStats.TotalCleanupsPerformed - 1) + 
                         duration.TotalMilliseconds) / _cleanupStats.TotalCleanupsPerformed);
                    
                    if (deadKeysRemoved > _cleanupStats.MaxEntriesRemovedInSingleCleanup)
                    {
                        _cleanupStats.MaxEntriesRemovedInSingleCleanup = deadKeysRemoved;
                    }
                }
                
                LogDebug($"Cache cleanup completed: {deadKeysRemoved}/{initialCount} dead entries removed in {duration.TotalMilliseconds:F1}ms");
                
                // Fire cleanup completed event on UI thread
                if (CleanupCompleted != null)
                {
                    if (Application.Current != null)
                    {
                        Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                        {
                            if (!_disposed)
                            {
                                if (CleanupCompleted != null)
                                {
                                    CleanupCompleted.Invoke(this, new CleanupCompletedEventArgs
                                    {
                                        DeadEntriesRemoved = deadKeysRemoved,
                                        InitialCacheSize = initialCount,
                                        CleanupDuration = duration,
                                        TotalCleanupsPerformed = GetCleanupStatistics().TotalCleanupsPerformed
                                    });
                                }
                            }
                        }));
                    }
                }
            }
            catch (Exception ex)
            {
                // Log error but don't crash the application
                LogDebug($"Error during cache cleanup: {ex.Message}");
                
                lock (_cleanupStatsLock)
                {
                    _cleanupStats.CleanupErrors++;
                }
            }
        }

        #endregion

        #region Private Methods

        public void ClearTreeViewItemCache()
        {
            _cacheLock.EnterWriteLock();
            try
            {
                var count = _treeViewItemCache.Count;
                _treeViewItemCache.Clear();
                _visibleTreeViewItems.Clear();
                
                LogDebug($"TreeViewItem cache cleared: {count} items removed");
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        private void ClearHitTestCache()
        {
            _hitTestCache.Clear();
            _cacheKeyQueue.Clear();
        }

        private CachedHitTestResult GetCachedHitTestResult(Point point)
        {
            // Check for exact match first
            if (_hitTestCache.TryGetValue(point, out CachedHitTestResult result))
            {
                return result;
            }
            
            // Check for nearby points within tolerance
            foreach (var kvp in _hitTestCache)
            {
                var cachedPoint = kvp.Key;
                var distance = Math.Sqrt(Math.Pow(point.X - cachedPoint.X, 2) + 
                                       Math.Pow(point.Y - cachedPoint.Y, 2));
                
                if (distance <= HIT_TEST_POSITION_TOLERANCE && kvp.Value.IsValid)
                {
                    return kvp.Value;
                }
            }
            
            return null;
        }

        private void CacheHitTestResult(Point point, FileTreeItem item)
        {
            // Remove old entries if cache is full
            while (_cacheKeyQueue.Count >= HIT_TEST_CACHE_SIZE)
            {
                var oldPoint = _cacheKeyQueue.Dequeue();
                _hitTestCache.Remove(oldPoint);
            }
            
            // Add new entry
            var result = new CachedHitTestResult
            {
                Item = item,
                CacheTime = DateTime.Now
            };
            
            _hitTestCache[point] = result;
            _cacheKeyQueue.Enqueue(point);
        }

        private IEnumerable<TreeViewItem> GetExpandedTreeViewItems(ItemsControl parent)
        {
            if (parent == null || _disposed) yield break;
            
            parent.UpdateLayout();
            
            if (parent.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                parent.UpdateLayout();
                parent.ApplyTemplate();
            }
            
            for (int i = 0; i < parent.Items.Count; i++)
            {
                var container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (container != null)
                {
                    yield return container;
                    
                    if (container.IsExpanded)
                    {
                        foreach (var child in GetExpandedTreeViewItems(container))
                        {
                            yield return child;
                        }
                    }
                }
            }
        }

        private void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[PERF-CACHE] {DateTime.Now:HH:mm:ss.fff} - {message}");
        }

        #endregion

        #region Event Handlers

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (e.VerticalChange != 0 || e.ViewportHeightChange != 0)
            {
                // Update cache on next dispatcher cycle for better performance
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (!_disposed)
                    {
                        UpdateVisibleItemsCache();
                    }
                }));
            }
        }

        private void OnContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (_treeView.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                // Clear caches when containers are regenerated
                ClearAllCaches();
                
                // Update visible items
                UpdateVisibleItemsCache();
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                LogDebug("Disposing FileTreePerformanceManager");
                
                // Stop and dispose cleanup timer
                if (_cleanupTimer != null)
                {
                    _cleanupTimer.Stop();
                    _cleanupTimer.Tick -= OnCleanupTimer;
                }
                
                if (_treeView != null)
                {
                    _treeView.Loaded -= OnTreeViewLoaded;
                }
                
                if (_scrollViewer != null)
                {
                    _scrollViewer.ScrollChanged -= OnScrollChanged;
                }
                
                if (_treeView != null && _treeView.ItemContainerGenerator != null)
                {
                    _treeView.ItemContainerGenerator.StatusChanged -= OnContainerGeneratorStatusChanged;
                }
                
                ClearAllCaches();
                
                // Dispose the read-write lock
                if (_cacheLock != null)
                {
                    _cacheLock.Dispose();
                }
                
                VisibleItemsCacheUpdated = null;
                SelectionUpdateRequested = null;
                CleanupCompleted = null;
                
                LogDebug("FileTreePerformanceManager disposed");
            }
        }

        #endregion

        #region Nested Types

        private class CachedHitTestResult
        {
            public FileTreeItem Item { get; set; }
            public DateTime CacheTime { get; set; }
            public bool IsValid => (DateTime.Now - CacheTime).TotalMilliseconds < 300; // Cache for 300ms
        }

        public class PerformanceStats
        {
            public int CacheHitCount { get; set; }
            public int CacheMissCount { get; set; }
            public double CacheHitRatio { get; set; }
            public int VisibleItemsCount { get; set; }
            public int CachedItemsCount { get; set; }
            public DateTime LastCacheUpdate { get; set; }
            public CleanupStatistics CleanupStats { get; set; }
        }

        public class CleanupStatistics
        {
            public int TotalCleanupsPerformed { get; set; }
            public int TotalDeadEntriesRemoved { get; set; }
            public DateTime LastCleanupTime { get; set; }
            public TimeSpan LastCleanupDuration { get; set; }
            public TimeSpan AverageCleanupDuration { get; set; }
            public int MaxEntriesRemovedInSingleCleanup { get; set; }
            public int CleanupErrors { get; set; }
            
            public CleanupStatistics Clone()
            {
                return new CleanupStatistics
                {
                    TotalCleanupsPerformed = this.TotalCleanupsPerformed,
                    TotalDeadEntriesRemoved = this.TotalDeadEntriesRemoved,
                    LastCleanupTime = this.LastCleanupTime,
                    LastCleanupDuration = this.LastCleanupDuration,
                    AverageCleanupDuration = this.AverageCleanupDuration,
                    MaxEntriesRemovedInSingleCleanup = this.MaxEntriesRemovedInSingleCleanup,
                    CleanupErrors = this.CleanupErrors
                };
            }
        }

        public class CleanupCompletedEventArgs : EventArgs
        {
            public int DeadEntriesRemoved { get; set; }
            public int InitialCacheSize { get; set; }
            public TimeSpan CleanupDuration { get; set; }
            public int TotalCleanupsPerformed { get; set; }
        }

        #endregion
    }
}