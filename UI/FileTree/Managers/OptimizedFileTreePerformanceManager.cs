using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Enhanced performance manager that uses OptimizedTreeViewIndexer for O(1) lookups
    /// instead of O(n) tree traversal. Maintains backward compatibility with existing code.
    /// </summary>
    public class OptimizedFileTreePerformanceManager : IDisposable
    {
        #region Private Fields

        private readonly TreeView _treeView;
        private readonly ScrollViewer _scrollViewer;
        private readonly OptimizedTreeViewIndexer _indexer;
        
        // Backward compatibility - keep existing cache for hit testing
        private readonly Dictionary<Point, CachedHitTestResult> _hitTestCache = new Dictionary<Point, CachedHitTestResult>();
        private readonly Queue<Point> _cacheKeyQueue = new Queue<Point>();
        private const int HIT_TEST_CACHE_SIZE = 20;
        private const double HIT_TEST_POSITION_TOLERANCE = 3.0;
        
        // Performance metrics
        private volatile int _cacheHitCount = 0;
        private volatile int _cacheMissCount = 0;
        private DateTime _lastCacheUpdate = DateTime.MinValue;
        
        private bool _disposed = false;

        #endregion

        #region Events

        public event EventHandler VisibleItemsCacheUpdated;
        public event EventHandler SelectionUpdateRequested;

        #endregion

        #region Constructor

        public OptimizedFileTreePerformanceManager(TreeView treeView, ScrollViewer explicitScrollViewer = null)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            _scrollViewer = explicitScrollViewer ?? FindScrollViewer(_treeView);
            
            // Create the optimized indexer
            _indexer = new OptimizedTreeViewIndexer(_treeView, _scrollViewer);
            
            // Subscribe to indexer events
            _indexer.VisibilityChanged += OnIndexerVisibilityChanged;
            
            Initialize();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            LogDebug("OptimizedFileTreePerformanceManager initialized");
        }

        private ScrollViewer FindScrollViewer(TreeView treeView)
        {
            return VisualTreeHelperEx.FindScrollViewer(treeView);
        }

        #endregion

        #region Public Methods - Optimized Implementations

        /// <summary>
        /// Gets a TreeViewItem for the given data item using O(1) indexer lookup
        /// </summary>
        public TreeViewItem GetTreeViewItemCached(FileTreeItem dataItem)
        {
            if (dataItem == null || _disposed) return null;
            
            var container = _indexer.GetContainer(dataItem);
            if (container != null)
            {
                Interlocked.Increment(ref _cacheHitCount);
                return container;
            }
            
            Interlocked.Increment(ref _cacheMissCount);
            return null;
        }

        /// <summary>
        /// Gets an item from a point using cached hit testing (unchanged for compatibility)
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
        /// Gets all TreeViewItems efficiently using O(1) indexer lookup
        /// </summary>
        public IEnumerable<TreeViewItem> GetAllTreeViewItemsFast()
        {
            if (_disposed) return Enumerable.Empty<TreeViewItem>();
            
            // Use indexer for O(1) lookup instead of tree traversal
            return _indexer.GetRealizedContainers();
        }

        /// <summary>
        /// Gets all visible TreeViewItems using O(1) indexer lookup
        /// </summary>
        public IEnumerable<TreeViewItem> GetAllVisibleTreeViewItems()
        {
            if (_disposed) return Enumerable.Empty<TreeViewItem>();
            
            return _indexer.GetVisibleContainers();
        }

        /// <summary>
        /// Gets all expanded TreeViewItems using O(1) indexer lookup
        /// </summary>
        public IEnumerable<TreeViewItem> GetExpandedTreeViewItems()
        {
            if (_disposed) return Enumerable.Empty<TreeViewItem>();
            
            return _indexer.GetExpandedContainers();
        }

        /// <summary>
        /// Updates the visible items cache - now a no-op since indexer handles this automatically
        /// </summary>
        public void UpdateVisibleItemsCache()
        {
            if (_disposed) return;
            
            _lastCacheUpdate = DateTime.Now;
            VisibleItemsCacheUpdated?.Invoke(this, EventArgs.Empty);
            
            var visibleCount = _indexer.VisibleCount;
            System.Diagnostics.Debug.WriteLine($"[PERF] Visible items cache updated: {visibleCount} items (via indexer)");
        }

        /// <summary>
        /// Clears all caches
        /// </summary>
        public void ClearAllCaches()
        {
            ClearHitTestCache();
            _indexer?.RebuildIndex();
        }

        /// <summary>
        /// Gets performance statistics including indexer stats
        /// </summary>
        public PerformanceStats GetPerformanceStats()
        {
            var indexerStats = _indexer?.GetStats();
            
            return new PerformanceStats
            {
                CacheHitCount = _cacheHitCount + (indexerStats?.TotalLookups - indexerStats?.CacheMisses ?? 0),
                CacheMissCount = _cacheMissCount + (indexerStats?.CacheMisses ?? 0),
                CacheHitRatio = CalculateCacheHitRatio(),
                VisibleItemsCount = indexerStats?.VisibleCount ?? 0,
                CachedItemsCount = indexerStats?.RealizedCount ?? 0,
                LastCacheUpdate = _lastCacheUpdate,
                IndexerStats = indexerStats
            };
        }

        /// <summary>
        /// Schedules a selection update (for coordinator compatibility)
        /// </summary>
        public void ScheduleSelectionUpdate()
        {
            SelectionUpdateRequested?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Invalidates cache for a specific directory
        /// </summary>
        public void InvalidateDirectory(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath) || _disposed) return;
            
            // Since we don't have direct access to all FileTreeItems by path in the indexer,
            // we'll trigger a rebuild for now. This could be optimized in the future.
            _indexer?.RebuildIndex();
            
            LogDebug($"Invalidated cache for directory: {directoryPath}");
        }

        /// <summary>
        /// Forces an immediate cleanup of dead cache entries
        /// </summary>
        public void ForceCleanup()
        {
            if (_disposed) return;
            
            // Indexer handles its own cleanup automatically
            LogDebug("Manual cleanup requested - indexer handles cleanup automatically");
        }

        #endregion

        #region Indexer Integration

        /// <summary>
        /// Gets the underlying indexer for advanced operations
        /// </summary>
        public OptimizedTreeViewIndexer GetIndexer()
        {
            return _indexer;
        }

        /// <summary>
        /// Temporarily disables indexing for bulk operations
        /// </summary>
        public void DisableIndexing()
        {
            _indexer?.DisableIndexing();
        }

        /// <summary>
        /// Re-enables indexing after bulk operations
        /// </summary>
        public void EnableIndexing()
        {
            _indexer?.EnableIndexing();
        }

        /// <summary>
        /// Checks if a TreeViewItem is currently visible
        /// </summary>
        public bool IsTreeViewItemVisible(TreeViewItem item)
        {
            return _indexer?.IsVisible(item) ?? false;
        }

        /// <summary>
        /// Checks if a TreeViewItem is currently realized
        /// </summary>
        public bool IsTreeViewItemRealized(TreeViewItem item)
        {
            return _indexer?.IsRealized(item) ?? false;
        }

        #endregion

        #region Event Handlers

        private void OnIndexerVisibilityChanged(object sender, OptimizedTreeViewIndexer.VisibilityChangedEventArgs e)
        {
            // Forward visibility changes as cache updates for backward compatibility
            VisibleItemsCacheUpdated?.Invoke(this, EventArgs.Empty);
        }

        #endregion

        #region Private Methods - Hit Test Cache (Unchanged)

        private void ClearHitTestCache()
        {
            _hitTestCache.Clear();
            _cacheKeyQueue.Clear();
        }

        private CachedHitTestResult GetCachedHitTestResult(Point point)
        {
            // Check for exact match first
            if (_hitTestCache.TryGetValue(point, out var exact) && exact.IsValid)
            {
                return exact;
            }
            
            // Check for nearby points within tolerance
            foreach (var kvp in _hitTestCache)
            {
                if (kvp.Value.IsValid)
                {
                    var distance = Math.Sqrt(Math.Pow(kvp.Key.X - point.X, 2) + Math.Pow(kvp.Key.Y - point.Y, 2));
                    if (distance <= HIT_TEST_POSITION_TOLERANCE)
                    {
                        return kvp.Value;
                    }
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

        private double CalculateCacheHitRatio()
        {
            var indexerStats = _indexer?.GetStats();
            var totalLookups = _cacheHitCount + _cacheMissCount + (indexerStats?.TotalLookups ?? 0);
            var totalMisses = _cacheMissCount + (indexerStats?.CacheMisses ?? 0);
            
            return totalLookups > 0 ? 1.0 - ((double)totalMisses / totalLookups) : 1.0;
        }

        private void LogDebug(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[OPTIMIZED-PERF] {DateTime.Now:HH:mm:ss.fff} - {message}");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                LogDebug("Disposing OptimizedFileTreePerformanceManager");
                
                // Dispose indexer
                _indexer?.Dispose();
                
                // Clear hit test cache
                ClearHitTestCache();
                
                // Clear events
                VisibleItemsCacheUpdated = null;
                SelectionUpdateRequested = null;
                
                LogDebug("OptimizedFileTreePerformanceManager disposed");
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
            public OptimizedTreeViewIndexer.IndexerStats IndexerStats { get; set; }
        }

        #endregion
    }
} 