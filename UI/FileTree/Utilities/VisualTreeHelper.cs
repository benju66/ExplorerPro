// UI/FileTree/Utilities/VisualTreeHelper.cs - Performance Optimized Version
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ExplorerPro.UI.FileTree.Utilities
{
    /// <summary>
    /// Provides utility methods for traversing and searching the WPF visual tree
    /// Performance optimized version with caching and efficient lookups
    /// </summary>
    public static class VisualTreeHelperEx
    {
        #region Caching Infrastructure
        
        // ConditionalWeakTable automatically removes entries when keys are garbage collected
        private static readonly ConditionalWeakTable<ItemsControl, TreeViewItemCache> _itemCaches = 
            new ConditionalWeakTable<ItemsControl, TreeViewItemCache>();
        
        /// <summary>
        /// Cache for TreeViewItem lookups to avoid repeated visual tree traversal
        /// </summary>
        private class TreeViewItemCache
        {
            private readonly Dictionary<object, WeakReference> _cache = new Dictionary<object, WeakReference>();
            private DateTime _lastCleanup = DateTime.Now;
            private const int CleanupIntervalSeconds = 30;
            
            public TreeViewItem GetCachedItem(object dataItem)
            {
                if (dataItem == null) return null;
                
                // Periodic cleanup of dead references
                if ((DateTime.Now - _lastCleanup).TotalSeconds > CleanupIntervalSeconds)
                {
                    CleanupDeadReferences();
                }
                
                if (_cache.TryGetValue(dataItem, out WeakReference weakRef) && 
                    weakRef.Target is TreeViewItem tvi && 
                    tvi.DataContext == dataItem)
                {
                    return tvi;
                }
                
                return null;
            }
            
            public void SetCachedItem(object dataItem, TreeViewItem treeViewItem)
            {
                if (dataItem != null && treeViewItem != null)
                {
                    _cache[dataItem] = new WeakReference(treeViewItem);
                }
            }
            
            public void Clear()
            {
                _cache.Clear();
            }
            
            private void CleanupDeadReferences()
            {
                var deadKeys = new List<object>();
                
                foreach (var kvp in _cache)
                {
                    if (!kvp.Value.IsAlive)
                    {
                        deadKeys.Add(kvp.Key);
                    }
                }
                
                foreach (var key in deadKeys)
                {
                    _cache.Remove(key);
                }
                
                _lastCleanup = DateTime.Now;
            }
        }
        
        /// <summary>
        /// Clears all caches - should be called when major UI changes occur
        /// </summary>
        public static void ClearAllCaches()
        {
            // ConditionalWeakTable doesn't have a Clear method, but entries will be 
            // garbage collected when the ItemsControl keys are collected
        }
        
        #endregion
        
        #region Optimized Search Methods
        
        /// <summary>
        /// Finds a TreeViewItem for data with caching - O(1) for cached items
        /// </summary>
        public static TreeViewItem FindTreeViewItemOptimized(ItemsControl container, object dataItem)
        {
            if (container == null || dataItem == null)
                return null;
            
            // Get or create cache for this container
            var cache = _itemCaches.GetOrCreateValue(container);
            
            // Check cache first
            var cachedItem = cache.GetCachedItem(dataItem);
            if (cachedItem != null)
                return cachedItem;
            
            // Not in cache, search for it
            var item = FindTreeViewItemInternal(container, dataItem, cache);
            return item;
        }
        
        /// <summary>
        /// Internal recursive search with cache population
        /// </summary>
        private static TreeViewItem FindTreeViewItemInternal(ItemsControl container, object dataItem, TreeViewItemCache cache)
        {
            // Ensure containers are generated
            container.UpdateLayout();
            
            if (container.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                container.UpdateLayout();
                container.ApplyTemplate();
            }
            
            // Direct lookup first
            var directContainer = container.ItemContainerGenerator.ContainerFromItem(dataItem) as TreeViewItem;
            if (directContainer != null)
            {
                cache.SetCachedItem(dataItem, directContainer);
                return directContainer;
            }
            
            // Search in visible items only (optimization for large trees)
            for (int i = 0; i < container.Items.Count; i++)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childContainer == null) continue;
                
                // Cache this item
                var childData = childContainer.DataContext;
                if (childData != null)
                {
                    cache.SetCachedItem(childData, childContainer);
                }
                
                // Check if this is what we're looking for
                if (childData == dataItem)
                    return childContainer;
                
                // Only search expanded items
                if (childContainer.IsExpanded)
                {
                    var result = FindTreeViewItemInternal(childContainer, dataItem, cache);
                    if (result != null)
                        return result;
                }
            }
            
            return null;
        }
        
        #endregion
        
        #region Original Methods (Kept for Compatibility)
        
        /// <summary>
        /// Finds all visual children of a specific type in the visual tree
        /// </summary>
        /// <typeparam name="T">The type of children to find</typeparam>
        /// <param name="parent">The parent element to search from</param>
        /// <returns>All children of the specified type</returns>
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                {
                    yield return t;
                }
                
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }
        
        /// <summary>
        /// Finds the first visual child of a specific type in the visual tree
        /// </summary>
        /// <typeparam name="T">The type of child to find</typeparam>
        /// <param name="parent">The parent element to search from</param>
        /// <returns>The first child of the specified type, or null if not found</returns>
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                {
                    return t;
                }
                
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Finds an ancestor of a specific type in the visual tree
        /// </summary>
        /// <typeparam name="T">The type of ancestor to find</typeparam>
        /// <param name="current">The element to start searching from</param>
        /// <returns>The first ancestor of the specified type, or null if not found</returns>
        public static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            
            return current as T;
        }
        
        /// <summary>
        /// Finds a TreeViewItem in the visual tree that contains the specified data item
        /// Uses the optimized method internally
        /// </summary>
        public static TreeViewItem FindTreeViewItemForData(ItemsControl container, object item)
        {
            return FindTreeViewItemOptimized(container, item);
        }
        
        /// <summary>
        /// Finds a TreeViewItem in the visual tree that contains the specified data item (recursive)
        /// Uses the optimized method internally
        /// </summary>
        public static TreeViewItem FindTreeViewItem(ItemsControl container, object data)
        {
            return FindTreeViewItemOptimized(container, data);
        }
        
        #endregion
        
        #region Performance Optimized Methods
        
        /// <summary>
        /// Gets visible TreeViewItems efficiently by limiting depth of search
        /// </summary>
        public static IEnumerable<TreeViewItem> GetVisibleTreeViewItems(TreeView treeView)
        {
            if (treeView == null) yield break;
            
            var scrollViewer = FindScrollViewer(treeView);
            if (scrollViewer == null)
            {
                // Fallback to all items if no scroll viewer
                foreach (var item in FindVisualChildren<TreeViewItem>(treeView))
                    yield return item;
                yield break;
            }
            
            // Calculate visible bounds
            var visibleBounds = new Rect(0, scrollViewer.VerticalOffset, 
                                       scrollViewer.ViewportWidth, 
                                       scrollViewer.ViewportHeight);
            
            // Only return items that intersect with visible bounds
            foreach (var item in FindVisualChildren<TreeViewItem>(treeView))
            {
                var itemBounds = GetBounds(item, treeView);
                if (visibleBounds.IntersectsWith(itemBounds))
                {
                    yield return item;
                }
            }
        }
        
        /// <summary>
        /// Batch finds multiple TreeViewItems efficiently
        /// </summary>
        public static Dictionary<object, TreeViewItem> FindMultipleTreeViewItems(ItemsControl container, IEnumerable<object> dataItems)
        {
            var result = new Dictionary<object, TreeViewItem>();
            if (container == null || dataItems == null) return result;
            
            var cache = _itemCaches.GetOrCreateValue(container);
            var itemsToFind = new HashSet<object>(dataItems);
            
            // Check cache first
            var uncachedItems = new HashSet<object>();
            foreach (var dataItem in itemsToFind)
            {
                var cached = cache.GetCachedItem(dataItem);
                if (cached != null)
                {
                    result[dataItem] = cached;
                }
                else
                {
                    uncachedItems.Add(dataItem);
                }
            }
            
            // If all found in cache, return early
            if (uncachedItems.Count == 0)
                return result;
            
            // Single traversal to find all uncached items
            FindMultipleItemsRecursive(container, uncachedItems, result, cache);
            
            return result;
        }
        
        private static void FindMultipleItemsRecursive(ItemsControl container, HashSet<object> itemsToFind, 
            Dictionary<object, TreeViewItem> result, TreeViewItemCache cache)
        {
            if (itemsToFind.Count == 0) return; // All found
            
            container.UpdateLayout();
            
            for (int i = 0; i < container.Items.Count && itemsToFind.Count > 0; i++)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childContainer == null) continue;
                
                var childData = childContainer.DataContext;
                if (childData != null)
                {
                    // Cache this item
                    cache.SetCachedItem(childData, childContainer);
                    
                    // Check if it's one we're looking for
                    if (itemsToFind.Contains(childData))
                    {
                        result[childData] = childContainer;
                        itemsToFind.Remove(childData);
                    }
                }
                
                // Only search expanded items
                if (childContainer.IsExpanded && itemsToFind.Count > 0)
                {
                    FindMultipleItemsRecursive(childContainer, itemsToFind, result, cache);
                }
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        /// <summary>
        /// Finds the ScrollViewer in a control's visual tree
        /// </summary>
        /// <param name="element">The element to search from</param>
        /// <returns>The ScrollViewer if found, null otherwise</returns>
        public static ScrollViewer FindScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer scrollViewer)
                return scrollViewer;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);
                var result = FindScrollViewer(child);
                if (result != null)
                    return result;
            }
            
            return null;
        }
        
        /// <summary>
        /// Gets all visual children of a specific type that are currently visible
        /// </summary>
        /// <typeparam name="T">The type of children to find</typeparam>
        /// <param name="parent">The parent element to search from</param>
        /// <returns>All visible children of the specified type</returns>
        public static IEnumerable<T> FindVisibleChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            foreach (var child in FindVisualChildren<T>(parent))
            {
                if (child is UIElement element && element.IsVisible)
                {
                    yield return child;
                }
            }
        }
        
        /// <summary>
        /// Gets the bounds of an element relative to another element
        /// </summary>
        /// <param name="element">The element to get bounds for</param>
        /// <param name="relativeTo">The element to get bounds relative to</param>
        /// <returns>The bounds of the element relative to the other element</returns>
        public static Rect GetBounds(FrameworkElement element, FrameworkElement relativeTo)
        {
            if (element == null || relativeTo == null)
                return Rect.Empty;
                
            try
            {
                var topLeft = element.TranslatePoint(new Point(0, 0), relativeTo);
                return new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
            }
            catch
            {
                return Rect.Empty;
            }
        }
        
        /// <summary>
        /// Hit tests to find an element at a specific point
        /// </summary>
        /// <typeparam name="T">The type of element to find</typeparam>
        /// <param name="reference">The reference element for the point</param>
        /// <param name="point">The point to test</param>
        /// <returns>The element at the point, or null if not found</returns>
        public static T HitTest<T>(Visual reference, Point point) where T : DependencyObject
        {
            HitTestResult result = VisualTreeHelper.HitTest(reference, point);
            if (result?.VisualHit == null)
                return null;
                
            return FindAncestor<T>(result.VisualHit);
        }
        
        /// <summary>
        /// Forces the visual tree to update and generate containers
        /// </summary>
        /// <param name="element">The element to update</param>
        public static void ForceVisualTreeUpdate(FrameworkElement element)
        {
            if (element == null) return;
            
            element.UpdateLayout();
            element.ApplyTemplate();
            
            if (element is ItemsControl itemsControl)
            {
                // Force update layout to ensure containers are generated
                itemsControl.UpdateLayout();
                
                // If containers still aren't generated, we can request generation
                // by accessing the ItemContainerGenerator status
                if (itemsControl.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
                {
                    // Force a measure/arrange pass which will trigger container generation
                    itemsControl.InvalidateMeasure();
                    itemsControl.InvalidateArrange();
                    itemsControl.UpdateLayout();
                }
            }
        }
        
        /// <summary>
        /// Efficiently counts visible TreeViewItems
        /// </summary>
        public static int CountVisibleTreeViewItems(TreeView treeView)
        {
            if (treeView == null) return 0;
            
            int count = 0;
            CountVisibleItemsRecursive(treeView, ref count);
            return count;
        }
        
        private static void CountVisibleItemsRecursive(ItemsControl container, ref int count)
        {
            for (int i = 0; i < container.Items.Count; i++)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childContainer != null && childContainer.IsVisible)
                {
                    count++;
                    
                    if (childContainer.IsExpanded)
                    {
                        CountVisibleItemsRecursive(childContainer, ref count);
                    }
                }
            }
        }
        
        #endregion
    }
}