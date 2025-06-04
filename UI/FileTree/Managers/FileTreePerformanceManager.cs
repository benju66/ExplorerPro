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

        #endregion

        #region Events

        public event EventHandler VisibleItemsCacheUpdated;
        public event EventHandler SelectionUpdateRequested;

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
        }

        private void OnTreeViewLoaded(object sender, RoutedEventArgs e)
        {
            _treeView.Loaded -= OnTreeViewLoaded;
            FindScrollViewer();
        }

        private void FindScrollViewer()
        {
            // Find the ScrollViewer within the TreeView's template
            _scrollViewer = VisualTreeHelperEx.FindScrollViewer(_treeView);
            
            if (_scrollViewer != null && !_disposed)
            {
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
            
            // Check cache first
            if (_treeViewItemCache.TryGetValue(dataItem, out WeakReference weakRef) && 
                weakRef.Target is TreeViewItem cachedItem && 
                cachedItem.DataContext == dataItem)
            {
                _cacheHitCount++;
                return cachedItem;
            }
            
            // Not in cache or stale, find it
            _cacheMissCount++;
            var treeViewItem = VisualTreeHelperEx.FindTreeViewItemOptimized(_treeView, dataItem);
            
            // Update cache
            if (treeViewItem != null)
            {
                _treeViewItemCache[dataItem] = new WeakReference(treeViewItem);
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
            var treeViewItem = VisualTreeHelperEx.FindAncestor<TreeViewItem>(hitTestResult?.VisualHit);
            var item = treeViewItem?.DataContext as FileTreeItem;
            
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
            VisibleItemsCacheUpdated?.Invoke(this, EventArgs.Empty);
            
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
        /// Gets performance statistics
        /// </summary>
        public PerformanceStats GetPerformanceStats()
        {
            return new PerformanceStats
            {
                CacheHitCount = _cacheHitCount,
                CacheMissCount = _cacheMissCount,
                CacheHitRatio = _cacheHitCount + _cacheMissCount > 0 ? 
                    (double)_cacheHitCount / (_cacheHitCount + _cacheMissCount) : 0.0,
                VisibleItemsCount = _visibleTreeViewItems.Count,
                CachedItemsCount = _treeViewItemCache.Count,
                LastCacheUpdate = _lastCacheUpdate
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
            if (string.IsNullOrEmpty(directoryPath)) return;
            
            var itemsToRemove = _treeViewItemCache.Keys
                .Where(item => item?.Path?.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
            
            foreach (var item in itemsToRemove)
            {
                _treeViewItemCache.Remove(item);
            }
            
            System.Diagnostics.Debug.WriteLine($"[PERF] Invalidated cache for directory: {directoryPath}");
        }

        #endregion

        #region Private Methods

        public void ClearTreeViewItemCache()
        {
            _treeViewItemCache.Clear();
            _visibleTreeViewItems.Clear();
        }

        private void ClearHitTestCache()
        {
            _hitTestCache.Clear();
            _hitTestCacheQueue.Clear();
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
            while (_hitTestCacheQueue.Count >= HIT_TEST_CACHE_SIZE)
            {
                var oldPoint = _hitTestCacheQueue.Dequeue();
                _hitTestCache.Remove(oldPoint);
            }
            
            // Add new entry
            var result = new CachedHitTestResult
            {
                Item = item,
                CacheTime = DateTime.Now
            };
            
            _hitTestCache[point] = result;
            _hitTestCacheQueue.Enqueue(point);
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
                
                if (_treeView != null)
                {
                    _treeView.Loaded -= OnTreeViewLoaded;
                }
                
                if (_scrollViewer != null)
                {
                    _scrollViewer.ScrollChanged -= OnScrollChanged;
                }
                
                if (_treeView?.ItemContainerGenerator != null)
                {
                    _treeView.ItemContainerGenerator.StatusChanged -= OnContainerGeneratorStatusChanged;
                }
                
                ClearAllCaches();
                VisibleItemsCacheUpdated = null;
                SelectionUpdateRequested = null;
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
        }

        #endregion
    }
}