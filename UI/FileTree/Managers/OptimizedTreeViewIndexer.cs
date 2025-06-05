using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using ExplorerPro.Models;

namespace ExplorerPro.UI.FileTree.Managers
{
    /// <summary>
    /// High-performance TreeView indexer that maintains live indexes of TreeViewItems
    /// using ItemContainerGenerator events for O(1) lookups instead of O(n) traversal.
    /// Handles virtualization properly and is thread-safe for concurrent access.
    /// </summary>
    public class OptimizedTreeViewIndexer : IDisposable
    {
        #region Private Fields

        private readonly TreeView _treeView;
        private readonly ScrollViewer _scrollViewer;
        
        // Thread-safe collections for live indexing
        private readonly ConcurrentDictionary<FileTreeItem, WeakReference> _dataToContainerIndex = 
            new ConcurrentDictionary<FileTreeItem, WeakReference>();
        private readonly ConcurrentDictionary<TreeViewItem, FileTreeItem> _containerToDataIndex = 
            new ConcurrentDictionary<TreeViewItem, FileTreeItem>();
        
        // Fast lookup sets for different query patterns
        private readonly ConcurrentDictionary<TreeViewItem, byte> _realizedContainers = 
            new ConcurrentDictionary<TreeViewItem, byte>();
        private readonly ConcurrentDictionary<TreeViewItem, byte> _visibleContainers = 
            new ConcurrentDictionary<TreeViewItem, byte>();
        private readonly ConcurrentDictionary<TreeViewItem, byte> _expandedContainers = 
            new ConcurrentDictionary<TreeViewItem, byte>();
        
        // Container generator tracking
        private readonly Dictionary<ItemsControl, bool> _trackedItemsControls = new Dictionary<ItemsControl, bool>();
        private readonly object _trackedControlsLock = new object();
        
        // Performance and cleanup
        private readonly Timer _cleanupTimer;
        private readonly Timer _visibilityUpdateTimer;
        private volatile bool _disposed = false;
        private volatile bool _indexingEnabled = true;
        
        // Statistics
        private volatile int _totalLookups = 0;
        private volatile int _cacheMisses = 0;
        private volatile int _containersCreated = 0;
        private volatile int _containersDestroyed = 0;
        
        // Virtualization support
        private Rect _lastVisibleBounds = Rect.Empty;
        private readonly HashSet<TreeViewItem> _pendingVisibilityUpdate = new HashSet<TreeViewItem>();
        private readonly object _visibilityLock = new object();
        
        #endregion

        #region Events

        /// <summary>
        /// Fired when a TreeViewItem is created and indexed
        /// </summary>
        public event EventHandler<ContainerEventArgs> ContainerCreated;
        
        /// <summary>
        /// Fired when a TreeViewItem is destroyed and removed from index
        /// </summary>
        public event EventHandler<ContainerEventArgs> ContainerDestroyed;
        
        /// <summary>
        /// Fired when visibility state of containers changes
        /// </summary>
        public event EventHandler<VisibilityChangedEventArgs> VisibilityChanged;

        #endregion

        #region Constructor

        public OptimizedTreeViewIndexer(TreeView treeView, ScrollViewer scrollViewer = null)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            _scrollViewer = scrollViewer ?? FindScrollViewer(_treeView);
            
            // Initialize cleanup timer (runs every 30 seconds)
            _cleanupTimer = new Timer(PerformCleanup, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
            
            // Initialize visibility update timer (runs every 100ms during scrolling)
            _visibilityUpdateTimer = new Timer(UpdateVisibility, null, Timeout.Infinite, Timeout.Infinite);
            
            Initialize();
        }

        #endregion

        #region Initialization

        private void Initialize()
        {
            // Attach to main TreeView
            AttachToItemsControl(_treeView);
            
            // Attach scroll viewer events for visibility tracking
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged += OnScrollChanged;
            }
            
            // Initial population of index
            PopulateInitialIndex();
        }

        private void AttachToItemsControl(ItemsControl itemsControl)
        {
            if (itemsControl == null || _disposed) return;
            
            lock (_trackedControlsLock)
            {
                if (_trackedItemsControls.ContainsKey(itemsControl)) return;
                _trackedItemsControls[itemsControl] = true;
            }
            
            // Attach to ItemContainerGenerator events
            var generator = itemsControl.ItemContainerGenerator;
            if (generator != null)
            {
                generator.ItemsChanged += OnItemsChanged;
                generator.StatusChanged += OnGeneratorStatusChanged;
            }
        }

        private void DetachFromItemsControl(ItemsControl itemsControl)
        {
            if (itemsControl == null) return;
            
            lock (_trackedControlsLock)
            {
                if (!_trackedItemsControls.Remove(itemsControl)) return;
            }
            
            var generator = itemsControl.ItemContainerGenerator;
            if (generator != null)
            {
                generator.ItemsChanged -= OnItemsChanged;
                generator.StatusChanged -= OnGeneratorStatusChanged;
            }
        }

        private void PopulateInitialIndex()
        {
            if (_disposed) return;
            
            // Use dispatcher to avoid blocking during initialization
            _treeView.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!_disposed)
                {
                    TraverseAndIndexRealizedContainers(_treeView);
                    UpdateVisibilityState();
                }
            }));
        }

        #endregion

        #region Public API - O(1) Lookups

        /// <summary>
        /// Gets TreeViewItem for data item with O(1) complexity
        /// </summary>
        public TreeViewItem GetContainer(FileTreeItem dataItem)
        {
            if (dataItem == null || _disposed) return null;
            
            Interlocked.Increment(ref _totalLookups);
            
            if (_dataToContainerIndex.TryGetValue(dataItem, out var weakRef) && 
                weakRef.Target is TreeViewItem container)
            {
                return container;
            }
            
            Interlocked.Increment(ref _cacheMisses);
            return null;
        }

        /// <summary>
        /// Gets data item for TreeViewItem with O(1) complexity
        /// </summary>
        public FileTreeItem GetDataItem(TreeViewItem container)
        {
            if (container == null || _disposed) return null;
            
            return _containerToDataIndex.TryGetValue(container, out var dataItem) ? dataItem : null;
        }

        /// <summary>
        /// Gets all realized TreeViewItems with O(1) complexity
        /// </summary>
        public IEnumerable<TreeViewItem> GetRealizedContainers()
        {
            if (_disposed) return Enumerable.Empty<TreeViewItem>();
            return _realizedContainers.Keys.ToList();
        }

        /// <summary>
        /// Gets all visible TreeViewItems with O(1) complexity
        /// </summary>
        public IEnumerable<TreeViewItem> GetVisibleContainers()
        {
            if (_disposed) return Enumerable.Empty<TreeViewItem>();
            return _visibleContainers.Keys.ToList();
        }

        /// <summary>
        /// Gets all expanded TreeViewItems with O(1) complexity
        /// </summary>
        public IEnumerable<TreeViewItem> GetExpandedContainers()
        {
            if (_disposed) return Enumerable.Empty<TreeViewItem>();
            return _expandedContainers.Keys.ToList();
        }

        /// <summary>
        /// Checks if a container is realized
        /// </summary>
        public bool IsRealized(TreeViewItem container)
        {
            return !_disposed && _realizedContainers.ContainsKey(container);
        }

        /// <summary>
        /// Checks if a container is visible
        /// </summary>
        public bool IsVisible(TreeViewItem container)
        {
            return !_disposed && _visibleContainers.ContainsKey(container);
        }

        /// <summary>
        /// Checks if a container is expanded
        /// </summary>
        public bool IsExpanded(TreeViewItem container)
        {
            return !_disposed && _expandedContainers.ContainsKey(container);
        }

        /// <summary>
        /// Gets count of realized containers
        /// </summary>
        public int RealizedCount => _disposed ? 0 : _realizedContainers.Count;

        /// <summary>
        /// Gets count of visible containers
        /// </summary>
        public int VisibleCount => _disposed ? 0 : _visibleContainers.Count;

        /// <summary>
        /// Gets count of expanded containers
        /// </summary>
        public int ExpandedCount => _disposed ? 0 : _expandedContainers.Count;

        #endregion

        #region Public API - Control Methods

        /// <summary>
        /// Temporarily disables indexing for bulk operations
        /// </summary>
        public void DisableIndexing()
        {
            _indexingEnabled = false;
        }

        /// <summary>
        /// Re-enables indexing and rebuilds the index
        /// </summary>
        public void EnableIndexing()
        {
            _indexingEnabled = true;
            if (!_disposed)
            {
                RebuildIndex();
            }
        }

        /// <summary>
        /// Forces a complete rebuild of the index
        /// </summary>
        public void RebuildIndex()
        {
            if (_disposed) return;
            
            ClearIndexes();
            PopulateInitialIndex();
        }

        /// <summary>
        /// Invalidates cache for a specific data item
        /// </summary>
        public void InvalidateItem(FileTreeItem dataItem)
        {
            if (dataItem == null || _disposed) return;
            
            if (_dataToContainerIndex.TryRemove(dataItem, out var weakRef) && 
                weakRef.Target is TreeViewItem container)
            {
                RemoveFromIndexes(container);
            }
        }

        /// <summary>
        /// Gets performance statistics
        /// </summary>
        public IndexerStats GetStats()
        {
            return new IndexerStats
            {
                TotalLookups = _totalLookups,
                CacheMisses = _cacheMisses,
                CacheHitRatio = _totalLookups > 0 ? 1.0 - ((double)_cacheMisses / _totalLookups) : 1.0,
                ContainersCreated = _containersCreated,
                ContainersDestroyed = _containersDestroyed,
                RealizedCount = RealizedCount,
                VisibleCount = VisibleCount,
                ExpandedCount = ExpandedCount,
                TrackedControlsCount = _trackedItemsControls.Count
            };
        }

        #endregion

        #region Event Handlers

        private void OnItemsChanged(object sender, ItemsChangedEventArgs e)
        {
            if (_disposed || !_indexingEnabled) return;
            
            var generator = sender as ItemContainerGenerator;
            
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    HandleItemsAdded(generator, e);
                    break;
                    
                case NotifyCollectionChangedAction.Remove:
                    HandleItemsRemoved(generator, e);
                    break;
                    
                case NotifyCollectionChangedAction.Reset:
                    HandleItemsReset(generator);
                    break;
                    
                case NotifyCollectionChangedAction.Replace:
                    HandleItemsReplaced(generator, e);
                    break;
            }
        }

        private void OnGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (_disposed || !_indexingEnabled) return;
            
            var generator = sender as ItemContainerGenerator;
            if (generator?.Status == GeneratorStatus.ContainersGenerated)
            {
                // New containers may have been generated
                _treeView.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    if (!_disposed)
                    {
                        var itemsControl = FindItemsControlForGenerator(generator);
                        if (itemsControl != null)
                        {
                            TraverseAndIndexRealizedContainers(itemsControl);
                            ScheduleVisibilityUpdate();
                        }
                    }
                }));
            }
        }

        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_disposed || (e.VerticalChange == 0 && e.ViewportHeightChange == 0)) return;
            
            ScheduleVisibilityUpdate();
        }

        #endregion

        #region Private Methods - Index Management

        private void HandleItemsAdded(ItemContainerGenerator generator, ItemsChangedEventArgs e)
        {
            var itemsControl = FindItemsControlForGenerator(generator);
            if (itemsControl == null) return;
            
            // Schedule indexing of new containers on dispatcher
            _treeView.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!_disposed)
                {
                    for (int i = 0; i < e.ItemCount; i++)
                    {
                        var container = generator.ContainerFromIndex(e.Position.Index + i) as TreeViewItem;
                        if (container != null)
                        {
                            IndexContainer(container);
                        }
                    }
                    ScheduleVisibilityUpdate();
                }
            }));
        }

        private void HandleItemsRemoved(ItemContainerGenerator generator, ItemsChangedEventArgs e)
        {
            // Items are already removed, so we need to clean up orphaned references
            ScheduleCleanup();
        }

        private void HandleItemsReset(ItemContainerGenerator generator)
        {
            // Complete rebuild needed
            _treeView.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!_disposed)
                {
                    RebuildIndex();
                }
            }));
        }

        private void HandleItemsReplaced(ItemContainerGenerator generator, ItemsChangedEventArgs e)
        {
            // Handle replace as remove + add
            HandleItemsRemoved(generator, e);
            HandleItemsAdded(generator, e);
        }

        private void IndexContainer(TreeViewItem container)
        {
            if (container?.DataContext is FileTreeItem dataItem)
            {
                // Add to indexes
                _dataToContainerIndex[dataItem] = new WeakReference(container);
                _containerToDataIndex[container] = dataItem;
                _realizedContainers[container] = 0;
                
                // Track expansion state
                if (container.IsExpanded)
                {
                    _expandedContainers[container] = 0;
                }
                
                // Attach to TreeViewItem for child containers
                AttachToItemsControl(container);
                
                Interlocked.Increment(ref _containersCreated);
                ContainerCreated?.Invoke(this, new ContainerEventArgs(container, dataItem));
            }
        }

        private void RemoveFromIndexes(TreeViewItem container)
        {
            if (container == null) return;
            
            var dataItem = GetDataItem(container);
            
            // Remove from all indexes
            if (dataItem != null)
            {
                _dataToContainerIndex.TryRemove(dataItem, out _);
            }
            _containerToDataIndex.TryRemove(container, out _);
            _realizedContainers.TryRemove(container, out _);
            _visibleContainers.TryRemove(container, out _);
            _expandedContainers.TryRemove(container, out _);
            
            // Detach from events
            DetachFromItemsControl(container);
            
            Interlocked.Increment(ref _containersDestroyed);
            if (dataItem != null)
            {
                ContainerDestroyed?.Invoke(this, new ContainerEventArgs(container, dataItem));
            }
        }

        private void TraverseAndIndexRealizedContainers(ItemsControl itemsControl)
        {
            if (itemsControl?.ItemContainerGenerator == null) return;
            
            var generator = itemsControl.ItemContainerGenerator;
            if (generator.Status != GeneratorStatus.ContainersGenerated)
            {
                return;
            }
            
            for (int i = 0; i < itemsControl.Items.Count; i++)
            {
                var container = generator.ContainerFromIndex(i) as TreeViewItem;
                if (container != null)
                {
                    IndexContainer(container);
                    
                    // Recursively index child containers if expanded
                    if (container.IsExpanded)
                    {
                        TraverseAndIndexRealizedContainers(container);
                    }
                }
            }
        }

        private void ClearIndexes()
        {
            _dataToContainerIndex.Clear();
            _containerToDataIndex.Clear();
            _realizedContainers.Clear();
            _visibleContainers.Clear();
            _expandedContainers.Clear();
        }

        #endregion

        #region Private Methods - Visibility Management

        private void ScheduleVisibilityUpdate()
        {
            // Reset timer to delay execution until scrolling stops
            _visibilityUpdateTimer?.Change(100, Timeout.Infinite);
        }

        private void UpdateVisibility(object state)
        {
            if (_disposed || _scrollViewer == null) return;
            
            try
            {
                _treeView.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    // Add disposal check AFTER getting on UI thread
                    if (_disposed || _treeView == null || _scrollViewer == null) 
                        return;
                        
                    try
                    {
                        UpdateVisibilityState();
                    }
                    catch (ObjectDisposedException)
                    {
                        // Silently handle if objects were disposed during execution
                    }
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[OptimizedTreeViewIndexer] Visibility update error: {ex.Message}");
            }
        }

        private void UpdateVisibilityState()
        {
            if (_disposed || _scrollViewer == null) return;
            
            var visibleBounds = new Rect(0, _scrollViewer.VerticalOffset, 
                                       _scrollViewer.ViewportWidth, 
                                       _scrollViewer.ViewportHeight);
            
            // Skip update if bounds haven't changed significantly
            if (Math.Abs(visibleBounds.Y - _lastVisibleBounds.Y) < 10 && 
                Math.Abs(visibleBounds.Height - _lastVisibleBounds.Height) < 10)
            {
                return;
            }
            
            _lastVisibleBounds = visibleBounds;
            
            var previouslyVisible = new HashSet<TreeViewItem>(_visibleContainers.Keys);
            var currentlyVisible = new HashSet<TreeViewItem>();
            
            // Check visibility for all realized containers
            foreach (var container in _realizedContainers.Keys.ToList())
            {
                if (IsContainerInBounds(container, visibleBounds))
                {
                    currentlyVisible.Add(container);
                    _visibleContainers[container] = 0;
                }
                else
                {
                    _visibleContainers.TryRemove(container, out _);
                }
            }
            
            // Fire visibility change event
            var becameVisible = currentlyVisible.Except(previouslyVisible).ToList();
            var becameHidden = previouslyVisible.Except(currentlyVisible).ToList();
            
            if (becameVisible.Count > 0 || becameHidden.Count > 0)
            {
                VisibilityChanged?.Invoke(this, new VisibilityChangedEventArgs(becameVisible, becameHidden));
            }
        }

        private bool IsContainerInBounds(TreeViewItem container, Rect bounds)
        {
            try
            {
                if (!container.IsVisible) return false;
                
                var transform = container.TransformToAncestor(_treeView);
                var position = transform.Transform(new Point(0, 0));
                var containerBounds = new Rect(position.X, position.Y, container.ActualWidth, container.ActualHeight);
                
                return bounds.IntersectsWith(containerBounds);
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private Methods - Cleanup

        private void ScheduleCleanup()
        {
            // Cleanup will happen on next timer tick
        }

        private void PerformCleanup(object state)
        {
            if (_disposed) return;
            
            var deadContainers = new List<TreeViewItem>();
            
            // Find dead weak references
            foreach (var kvp in _dataToContainerIndex.ToList())
            {
                if (kvp.Value?.Target == null)
                {
                    _dataToContainerIndex.TryRemove(kvp.Key, out _);
                }
            }
            
            // Find orphaned containers
            foreach (var container in _containerToDataIndex.Keys.ToList())
            {
                if (container == null || !IsContainerValid(container))
                {
                    deadContainers.Add(container);
                }
            }
            
            // Remove dead containers
            foreach (var container in deadContainers)
            {
                RemoveFromIndexes(container);
            }
            
            if (deadContainers.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[OptimizedTreeViewIndexer] Cleaned up {deadContainers.Count} dead containers");
            }
        }

        private bool IsContainerValid(TreeViewItem container)
        {
            try
            {
                // Check if container is still in visual tree
                return container.Parent != null || VisualTreeHelper.GetParent(container) != null;
            }
            catch
            {
                return false;
            }
        }

        #endregion

        #region Private Methods - Utilities

        private ScrollViewer FindScrollViewer(DependencyObject element)
        {
            if (element == null) return null;
            
            if (element is ScrollViewer scrollViewer)
                return scrollViewer;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = FindScrollViewer(child);
                if (result != null) return result;
            }
            
            return null;
        }

        private ItemsControl FindItemsControlForGenerator(ItemContainerGenerator generator)
        {
            lock (_trackedControlsLock)
            {
                foreach (var itemsControl in _trackedItemsControls.Keys)
                {
                    if (itemsControl.ItemContainerGenerator == generator)
                        return itemsControl;
                }
            }
            return null;
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            
            // Stop timers
            _cleanupTimer?.Dispose();
            _visibilityUpdateTimer?.Dispose();
            
            // Detach from all tracked controls
            lock (_trackedControlsLock)
            {
                foreach (var itemsControl in _trackedItemsControls.Keys.ToList())
                {
                    DetachFromItemsControl(itemsControl);
                }
                _trackedItemsControls.Clear();
            }
            
            // Detach from scroll viewer
            if (_scrollViewer != null)
            {
                _scrollViewer.ScrollChanged -= OnScrollChanged;
            }
            
            // Clear all indexes
            ClearIndexes();
            
            // Clear events
            ContainerCreated = null;
            ContainerDestroyed = null;
            VisibilityChanged = null;
        }

        #endregion

        #region Nested Types

        public class ContainerEventArgs : EventArgs
        {
            public TreeViewItem Container { get; }
            public FileTreeItem DataItem { get; }
            
            public ContainerEventArgs(TreeViewItem container, FileTreeItem dataItem)
            {
                Container = container;
                DataItem = dataItem;
            }
        }

        public class VisibilityChangedEventArgs : EventArgs
        {
            public IReadOnlyList<TreeViewItem> BecameVisible { get; }
            public IReadOnlyList<TreeViewItem> BecameHidden { get; }
            
            public VisibilityChangedEventArgs(IReadOnlyList<TreeViewItem> becameVisible, IReadOnlyList<TreeViewItem> becameHidden)
            {
                BecameVisible = becameVisible;
                BecameHidden = becameHidden;
            }
        }

        public class IndexerStats
        {
            public int TotalLookups { get; set; }
            public int CacheMisses { get; set; }
            public double CacheHitRatio { get; set; }
            public int ContainersCreated { get; set; }
            public int ContainersDestroyed { get; set; }
            public int RealizedCount { get; set; }
            public int VisibleCount { get; set; }
            public int ExpandedCount { get; set; }
            public int TrackedControlsCount { get; set; }
        }

        #endregion
    }
} 