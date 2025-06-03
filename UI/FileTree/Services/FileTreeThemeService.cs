// UI/FileTree/Services/FileTreeThemeService.cs - Performance Optimized Version
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ExplorerPro.Themes;
using ExplorerPro.UI.FileTree.Utilities;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service responsible for managing theme-related functionality for the file tree view
    /// Performance optimized version that only updates visible items
    /// </summary>
    public class FileTreeThemeService : IDisposable
    {
        #region Fields

        private readonly TreeView _treeView;
        private readonly Grid _mainGrid;
        private readonly Dictionary<UIElement, MouseEventHandler> _mouseEnterHandlers;
        private readonly Dictionary<UIElement, MouseEventHandler> _mouseLeaveHandlers;
        private readonly List<WeakReference> _themedElements;
        private bool _disposed;

        // Event handler delegates stored to ensure proper unsubscription
        private EventHandler<AppTheme> _themeChangedHandler;
        private EventHandler<AppTheme> _themeRefreshedHandler;

        // Performance optimization fields
        private readonly DispatcherTimer _deferredUpdateTimer;
        private readonly Queue<TreeViewItem> _pendingThemeUpdates;
        private bool _isProcessingDeferredUpdates;
        private ScrollViewer _treeScrollViewer;
        private double _lastVerticalOffset;
        private const int MAX_VISIBLE_ITEMS_TO_UPDATE = 50; // Limit per frame
        private const int DEFERRED_UPDATE_DELAY_MS = 100;

        // Resource cache for current theme
        private readonly Dictionary<string, object> _currentThemeResourceCache;
        private AppTheme _cachedTheme;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the FileTreeThemeService
        /// </summary>
        /// <param name="treeView">The TreeView control to manage themes for</param>
        /// <param name="mainGrid">The main grid container (optional)</param>
        public FileTreeThemeService(TreeView treeView, Grid mainGrid = null)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            _mainGrid = mainGrid;
            _mouseEnterHandlers = new Dictionary<UIElement, MouseEventHandler>();
            _mouseLeaveHandlers = new Dictionary<UIElement, MouseEventHandler>();
            _themedElements = new List<WeakReference>();
            _pendingThemeUpdates = new Queue<TreeViewItem>();
            _currentThemeResourceCache = new Dictionary<string, object>();

            // Initialize deferred update timer
            _deferredUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(DEFERRED_UPDATE_DELAY_MS)
            };
            _deferredUpdateTimer.Tick += OnDeferredUpdateTimerTick;

            // Find and cache the ScrollViewer
            _treeView.Loaded += (s, e) => CacheScrollViewer();

            // Create and store event handlers
            _themeChangedHandler = OnThemeChanged;
            _themeRefreshedHandler = OnThemeRefreshed;

            // Subscribe to theme change events
            ThemeManager.Instance.ThemeChanged += _themeChangedHandler;
            ThemeManager.Instance.ThemeRefreshed += _themeRefreshedHandler;

            // Cache initial theme resources
            CacheCurrentThemeResources();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Refreshes all theme elements in the file tree - Optimized version
        /// </summary>
        public void RefreshThemeElements()
        {
            if (_disposed)
                return;

            try
            {
                var startTime = DateTime.Now;
                bool isDarkMode = ThemeManager.Instance.IsDarkMode;
                
                // Update main grid background (fast)
                if (_mainGrid != null)
                {
                    _mainGrid.Background = GetCachedResource<SolidColorBrush>("BackgroundColor");
                }
                
                // Update TreeView itself (fast)
                if (_treeView != null)
                {
                    _treeView.Background = GetCachedResource<SolidColorBrush>("TreeViewBackground");
                    _treeView.BorderBrush = GetCachedResource<SolidColorBrush>("TreeViewBorder");
                    _treeView.Foreground = GetCachedResource<SolidColorBrush>("TextColor");
                }
                
                // Update only visible TreeViewItems
                RefreshVisibleTreeViewItems();
                
                // Queue remaining items for deferred update
                QueueNonVisibleItemsForUpdate();
                
                // Clean up dead weak references
                CleanupDeadReferences();
                
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"FileTree theme refresh completed in {elapsed:F1}ms (visible items only)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing file tree theme: {ex.Message}");
            }
        }

        /// <summary>
        /// Applies theme to a specific TreeViewItem - Optimized version
        /// </summary>
        /// <param name="treeViewItem">The TreeViewItem to theme</param>
        public void ApplyThemeToTreeViewItem(TreeViewItem treeViewItem)
        {
            if (_disposed || treeViewItem == null) return;

            // Use cached resources for better performance
            var textColor = GetCachedResource<SolidColorBrush>("TextColor");
            var treeLine = GetCachedResource<SolidColorBrush>("TreeLineColor");

            // Apply theme to the TreeViewItem
            treeViewItem.Foreground = textColor;
            
            // Track the themed element with weak reference
            _themedElements.Add(new WeakReference(treeViewItem));
            
            // Only process visual children if the item is visible and expanded
            if (IsItemVisible(treeViewItem) && treeViewItem.IsExpanded)
            {
                ApplyThemeToVisualChildren(treeViewItem, textColor, treeLine);
            }
        }

        /// <summary>
        /// Forces a theme refresh on all visible items
        /// </summary>
        public void ForceThemeRefresh()
        {
            if (_disposed)
                return;

            // Clear and rebuild resource cache
            CacheCurrentThemeResources();

            // Schedule the refresh on the dispatcher with normal priority for visible items
            _treeView?.Dispatcher.BeginInvoke(DispatcherPriority.Normal, new Action(() =>
            {
                if (!_disposed)
                {
                    RefreshThemeElements();
                }
            }));
        }

        #endregion

        #region Private Methods - Performance Optimized

        /// <summary>
        /// Caches the ScrollViewer reference for performance
        /// </summary>
        private void CacheScrollViewer()
        {
            _treeScrollViewer = VisualTreeHelperEx.FindScrollViewer(_treeView);
            if (_treeScrollViewer != null)
            {
                _treeScrollViewer.ScrollChanged += OnScrollChanged;
            }
        }

        /// <summary>
        /// Handles scroll changes to update newly visible items
        /// </summary>
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            if (_disposed || Math.Abs(e.VerticalOffset - _lastVerticalOffset) < 1)
                return;

            _lastVerticalOffset = e.VerticalOffset;

            // Update newly visible items with theme
            _treeView.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!_disposed)
                {
                    UpdateNewlyVisibleItems();
                }
            }));
        }

        /// <summary>
        /// Caches current theme resources for fast access
        /// </summary>
        private void CacheCurrentThemeResources()
        {
            _cachedTheme = ThemeManager.Instance.CurrentTheme;
            _currentThemeResourceCache.Clear();

            // Cache commonly used resources
            var resourceKeys = new[]
            {
                "TextColor", "TreeLineColor", "TreeLineHighlightColor",
                "BackgroundColor", "TreeViewBackground", "TreeViewBorder",
                "WindowBackground", "BorderColor", "SubtleTextColor"
            };

            foreach (var key in resourceKeys)
            {
                var resource = GetResource<object>(key);
                if (resource != null)
                {
                    _currentThemeResourceCache[key] = resource;
                }
            }
        }

        /// <summary>
        /// Gets a cached resource for better performance
        /// </summary>
        private T GetCachedResource<T>(string key) where T : class
        {
            if (_currentThemeResourceCache.TryGetValue(key, out var cached) && cached is T typedResource)
            {
                return typedResource;
            }
            return GetResource<T>(key);
        }

        /// <summary>
        /// Refreshes only visible TreeViewItems for performance
        /// </summary>
        private void RefreshVisibleTreeViewItems()
        {
            if (_disposed || _treeView == null || _treeScrollViewer == null)
                return;

            try
            {
                var visibleItems = GetVisibleTreeViewItems();
                var updateCount = 0;

                foreach (var item in visibleItems.Take(MAX_VISIBLE_ITEMS_TO_UPDATE))
                {
                    if (_disposed) break;
                    ApplyThemeToTreeViewItemFast(item);
                    updateCount++;
                }

                System.Diagnostics.Debug.WriteLine($"[PERF] Updated {updateCount} visible items immediately");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing visible tree items: {ex.Message}");
            }
        }

        /// <summary>
        /// Fast theme application without deep traversal
        /// </summary>
        private void ApplyThemeToTreeViewItemFast(TreeViewItem item)
        {
            if (item == null || _disposed) return;

            // Use cached resources
            item.Foreground = GetCachedResource<SolidColorBrush>("TextColor");

            // Only update immediate visual children if expanded
            if (item.IsExpanded)
            {
                // Find immediate content presenter only
                var contentPresenter = VisualTreeHelperEx.FindVisualChild<ContentPresenter>(item);
                if (contentPresenter != null)
                {
                    UpdateContentPresenterTheme(contentPresenter);
                }
            }
        }

        /// <summary>
        /// Updates theme for a content presenter
        /// </summary>
        private void UpdateContentPresenterTheme(ContentPresenter presenter)
        {
            if (presenter == null) return;

            // Update any TextBlocks in the content
            var textBlock = VisualTreeHelperEx.FindVisualChild<TextBlock>(presenter);
            if (textBlock != null && textBlock.Foreground == SystemColors.WindowTextBrush)
            {
                textBlock.Foreground = GetCachedResource<SolidColorBrush>("TextColor");
            }
        }

        /// <summary>
        /// Gets currently visible TreeViewItems efficiently
        /// </summary>
        private IEnumerable<TreeViewItem> GetVisibleTreeViewItems()
        {
            if (_treeScrollViewer == null)
                yield break;

            var viewport = new Rect(0, _treeScrollViewer.VerticalOffset,
                                  _treeScrollViewer.ViewportWidth,
                                  _treeScrollViewer.ViewportHeight);

            // Use a more efficient approach - only check expanded items
            foreach (var item in GetExpandedTreeViewItems(_treeView))
            {
                if (IsItemInViewport(item, viewport))
                {
                    yield return item;
                }
            }
        }

        /// <summary>
        /// Gets expanded TreeViewItems without deep recursion
        /// </summary>
        private IEnumerable<TreeViewItem> GetExpandedTreeViewItems(ItemsControl parent)
        {
            if (parent == null || _disposed)
                yield break;

            for (int i = 0; i < parent.Items.Count; i++)
            {
                var container = parent.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (container != null)
                {
                    yield return container;

                    // Only recurse if expanded to avoid unnecessary work
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

        /// <summary>
        /// Checks if an item is in the viewport
        /// </summary>
        private bool IsItemInViewport(TreeViewItem item, Rect viewport)
        {
            try
            {
                var transform = item.TransformToAncestor(_treeView);
                var position = transform.Transform(new Point(0, 0));
                var itemBounds = new Rect(position.X, position.Y, item.ActualWidth, item.ActualHeight);
                return viewport.IntersectsWith(itemBounds);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Checks if an item is visible
        /// </summary>
        private bool IsItemVisible(TreeViewItem item)
        {
            if (_treeScrollViewer == null) return true;
            return IsItemInViewport(item, new Rect(0, _treeScrollViewer.VerticalOffset,
                                                  _treeScrollViewer.ViewportWidth,
                                                  _treeScrollViewer.ViewportHeight));
        }

        /// <summary>
        /// Queues non-visible items for deferred theme update
        /// </summary>
        private void QueueNonVisibleItemsForUpdate()
        {
            if (_disposed) return;

            _pendingThemeUpdates.Clear();

            // Get all items that aren't visible
            var allItems = GetExpandedTreeViewItems(_treeView).ToList();
            var visibleItems = new HashSet<TreeViewItem>(GetVisibleTreeViewItems());

            foreach (var item in allItems)
            {
                if (!visibleItems.Contains(item))
                {
                    _pendingThemeUpdates.Enqueue(item);
                }
            }

            if (_pendingThemeUpdates.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine($"[PERF] Queued {_pendingThemeUpdates.Count} items for deferred update");
                _deferredUpdateTimer.Start();
            }
        }

        /// <summary>
        /// Processes deferred theme updates in batches
        /// </summary>
        private void OnDeferredUpdateTimerTick(object sender, EventArgs e)
        {
            if (_disposed || _isProcessingDeferredUpdates)
                return;

            _isProcessingDeferredUpdates = true;

            try
            {
                var batchSize = Math.Min(20, _pendingThemeUpdates.Count);
                var processedCount = 0;

                while (processedCount < batchSize && _pendingThemeUpdates.Count > 0)
                {
                    var item = _pendingThemeUpdates.Dequeue();
                    if (item != null && !_disposed)
                    {
                        ApplyThemeToTreeViewItemFast(item);
                        processedCount++;
                    }
                }

                if (_pendingThemeUpdates.Count == 0)
                {
                    _deferredUpdateTimer.Stop();
                    System.Diagnostics.Debug.WriteLine("[PERF] Completed deferred theme updates");
                }
            }
            finally
            {
                _isProcessingDeferredUpdates = false;
            }
        }

        /// <summary>
        /// Updates newly visible items after scrolling
        /// </summary>
        private void UpdateNewlyVisibleItems()
        {
            if (_disposed) return;

            var visibleItems = GetVisibleTreeViewItems().Take(10); // Limit to avoid lag
            foreach (var item in visibleItems)
            {
                // Only update if not already themed properly
                if (item.Foreground != GetCachedResource<SolidColorBrush>("TextColor"))
                {
                    ApplyThemeToTreeViewItemFast(item);
                }
            }
        }

        /// <summary>
        /// Applies theme to visual children - Optimized version
        /// </summary>
        private void ApplyThemeToVisualChildren(TreeViewItem treeViewItem, SolidColorBrush textColor, SolidColorBrush treeLine)
        {
            // Limit depth of traversal for performance
            var maxDepth = 2;
            ApplyThemeToChildrenRecursive(treeViewItem, textColor, treeLine, 0, maxDepth);
        }

        /// <summary>
        /// Recursive helper with depth limit
        /// </summary>
        private void ApplyThemeToChildrenRecursive(DependencyObject parent, SolidColorBrush textColor, SolidColorBrush treeLine, int currentDepth, int maxDepth)
        {
            if (currentDepth >= maxDepth || parent == null || _disposed)
                return;

            var childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount && i < 10; i++) // Limit children processed
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is TextBlock textBlock && textBlock.Foreground == SystemColors.WindowTextBrush)
                {
                    textBlock.Foreground = textColor;
                }
                else if (child is System.Windows.Shapes.Line line)
                {
                    line.Stroke = treeLine;
                }
                else if (child is ToggleButton toggle)
                {
                    RefreshTreeViewToggleButton(toggle);
                }

                // Recurse with increased depth
                ApplyThemeToChildrenRecursive(child, textColor, treeLine, currentDepth + 1, maxDepth);
            }
        }

        /// <summary>
        /// Sets up mouse event handlers for tree line hover effects
        /// </summary>
        private void SetupTreeLineMouseHandlers(UIElement parent)
        {
            if (_disposed || parent == null)
                return;

            // Remove existing handlers if any
            RemoveMouseHandlers(parent);

            // Create new handlers
            MouseEventHandler enterHandler = (s, e) => TreeLine_MouseEnter(s, e);
            MouseEventHandler leaveHandler = (s, e) => TreeLine_MouseLeave(s, e);

            // Store and attach handlers
            _mouseEnterHandlers[parent] = enterHandler;
            _mouseLeaveHandlers[parent] = leaveHandler;
            
            parent.MouseEnter += enterHandler;
            parent.MouseLeave += leaveHandler;
        }

        /// <summary>
        /// Removes mouse event handlers from an element
        /// </summary>
        private void RemoveMouseHandlers(UIElement element)
        {
            if (element == null) return;

            if (_mouseEnterHandlers.TryGetValue(element, out var enterHandler))
            {
                element.MouseEnter -= enterHandler;
                _mouseEnterHandlers.Remove(element);
            }
            
            if (_mouseLeaveHandlers.TryGetValue(element, out var leaveHandler))
            {
                element.MouseLeave -= leaveHandler;
                _mouseLeaveHandlers.Remove(element);
            }
        }

        /// <summary>
        /// Refreshes a TreeView toggle button (expander) with theme-appropriate styling
        /// </summary>
        private void RefreshTreeViewToggleButton(ToggleButton toggle)
        {
            if (_disposed || toggle == null)
                return;

            try
            {
                toggle.Background = Brushes.Transparent;
                    
                var pathElement = VisualTreeHelperEx.FindVisualChild<System.Windows.Shapes.Path>(toggle);
                if (pathElement != null)
                {
                    pathElement.Stroke = GetCachedResource<SolidColorBrush>("TextColor");
                    pathElement.Fill = GetCachedResource<SolidColorBrush>("TextColor");
                    
                    RemoveMouseHandlers(toggle);

                    MouseEventHandler enterHandler = (s, e) => ToggleButton_MouseEnter(s, e);
                    MouseEventHandler leaveHandler = (s, e) => ToggleButton_MouseLeave(s, e);

                    _mouseEnterHandlers[toggle] = enterHandler;
                    _mouseLeaveHandlers[toggle] = leaveHandler;
                    
                    toggle.MouseEnter += enterHandler;
                    toggle.MouseLeave += leaveHandler;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing toggle button: {ex.Message}");
            }
        }

        /// <summary>
        /// Event handler for mouse enter on tree lines
        /// </summary>
        private void TreeLine_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_disposed) return;

            try
            {
                foreach (var line in VisualTreeHelperEx.FindVisualChildren<System.Windows.Shapes.Line>(sender as DependencyObject))
                {
                    line.Stroke = GetCachedResource<SolidColorBrush>("TreeLineHighlightColor");
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Event handler for mouse leave on tree lines
        /// </summary>
        private void TreeLine_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_disposed) return;

            try
            {
                foreach (var line in VisualTreeHelperEx.FindVisualChildren<System.Windows.Shapes.Line>(sender as DependencyObject))
                {
                    line.Stroke = GetCachedResource<SolidColorBrush>("TreeLineColor");
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Event handler for mouse enter on toggle buttons
        /// </summary>
        private void ToggleButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (_disposed) return;

            try
            {
                if (sender is ToggleButton toggle)
                {
                    var pathElement = VisualTreeHelperEx.FindVisualChild<System.Windows.Shapes.Path>(toggle);
                    if (pathElement != null)
                    {
                        pathElement.Stroke = GetCachedResource<SolidColorBrush>("TreeLineHighlightColor");
                        pathElement.Fill = GetCachedResource<SolidColorBrush>("TreeLineHighlightColor");
                    }
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Event handler for mouse leave on toggle buttons
        /// </summary>
        private void ToggleButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (_disposed) return;

            try
            {
                if (sender is ToggleButton toggle)
                {
                    var pathElement = VisualTreeHelperEx.FindVisualChild<System.Windows.Shapes.Path>(toggle);
                    if (pathElement != null)
                    {
                        pathElement.Stroke = GetCachedResource<SolidColorBrush>("TextColor");
                        pathElement.Fill = GetCachedResource<SolidColorBrush>("TextColor");
                    }
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Helper method to get a resource from the current theme
        /// </summary>
        private T GetResource<T>(string resourceKey) where T : class
        {
            try
            {
                if (Application.Current?.Resources[resourceKey] is T resource)
                {
                    return resource;
                }
                
                return ThemeManager.Instance.GetResource<T>(resourceKey);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error getting resource '{resourceKey}': {ex.Message}");
            }
            
            // Default values for common types
            bool isDarkMode = ThemeManager.Instance.IsDarkMode;
            
            if (typeof(T) == typeof(SolidColorBrush))
            {
                if (resourceKey.Contains("Background"))
                    return new SolidColorBrush(isDarkMode ? Colors.Black : Colors.White) as T;
                if (resourceKey.Contains("Foreground") || resourceKey.Contains("Text"))
                    return new SolidColorBrush(isDarkMode ? Colors.LightGray : Colors.Black) as T;
                if (resourceKey.Contains("Border"))
                    return new SolidColorBrush(isDarkMode ? Colors.DarkGray : Colors.LightGray) as T;
                if (resourceKey.Contains("Line"))
                    return new SolidColorBrush(isDarkMode ? Colors.DarkGray : Colors.LightGray) as T;
            }
            
            return default;
        }

        /// <summary>
        /// Cleans up dead weak references from the themed elements list
        /// </summary>
        private void CleanupDeadReferences()
        {
            if (_themedElements == null || _themedElements.Count == 0)
                return;

            _themedElements.RemoveAll(wr => !wr.IsAlive);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles theme change events from ThemeManager
        /// </summary>
        private void OnThemeChanged(object sender, AppTheme newTheme)
        {
            if (!_disposed)
            {
                // Clear resource cache when theme changes
                CacheCurrentThemeResources();
                ForceThemeRefresh();
            }
        }

        /// <summary>
        /// Handles theme refresh events from ThemeManager
        /// </summary>
        private void OnThemeRefreshed(object sender, AppTheme currentTheme)
        {
            if (!_disposed)
            {
                // Re-cache resources if theme is the same but resources might have changed
                if (currentTheme == _cachedTheme)
                {
                    CacheCurrentThemeResources();
                }
                ForceThemeRefresh();
            }
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
            if (!_disposed)
            {
                if (disposing)
                {
                    // Stop timers
                    if (_deferredUpdateTimer != null)
                    {
                        _deferredUpdateTimer.Stop();
                        _deferredUpdateTimer.Tick -= OnDeferredUpdateTimerTick;
                    }

                    // Unsubscribe from ScrollViewer events
                    if (_treeScrollViewer != null)
                    {
                        _treeScrollViewer.ScrollChanged -= OnScrollChanged;
                        _treeScrollViewer = null;
                    }

                    // Unsubscribe from ThemeManager events using stored handlers
                    if (_themeChangedHandler != null)
                    {
                        ThemeManager.Instance.ThemeChanged -= _themeChangedHandler;
                        _themeChangedHandler = null;
                    }
                    
                    if (_themeRefreshedHandler != null)
                    {
                        ThemeManager.Instance.ThemeRefreshed -= _themeRefreshedHandler;
                        _themeRefreshedHandler = null;
                    }

                    // Clean up all event handlers
                    var allHandledElements = _mouseEnterHandlers.Keys.ToList();
                    foreach (var element in allHandledElements)
                    {
                        RemoveMouseHandlers(element);
                    }
                    
                    // Clear collections
                    _mouseEnterHandlers?.Clear();
                    _mouseLeaveHandlers?.Clear();
                    _themedElements?.Clear();
                    _pendingThemeUpdates?.Clear();
                    _currentThemeResourceCache?.Clear();
                }
                
                _disposed = true;
            }
        }

        ~FileTreeThemeService()
        {
            Dispose(false);
        }

        #endregion
    }
}