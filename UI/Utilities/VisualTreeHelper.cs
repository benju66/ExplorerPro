// UI/FileTree/Utilities/VisualTreeHelper.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ExplorerPro.UI.FileTree.Utilities
{
    /// <summary>
    /// Provides utility methods for traversing and searching the WPF visual tree
    /// </summary>
    public static class VisualTreeHelperEx
    {
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
        /// </summary>
        /// <param name="container">The container to search in</param>
        /// <param name="item">The data item to find</param>
        /// <returns>The TreeViewItem containing the data, or null if not found</returns>
        public static TreeViewItem FindTreeViewItemForData(ItemsControl container, object item)
        {
            if (container == null || item == null)
                return null;
                
            // Ensure containers are generated
            container.UpdateLayout();
            
            // Force container generation if needed
            if (container.ItemContainerGenerator.Status != GeneratorStatus.ContainersGenerated)
            {
                container.UpdateLayout();
                container.ApplyTemplate();
            }
                
            if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                return tvi;
                
            for (int i = 0; i < container.Items.Count; i++)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childContainer != null)
                {
                    var result = FindTreeViewItemForData(childContainer, item);
                    if (result != null)
                        return result;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Finds a TreeViewItem in the visual tree that contains the specified data item (recursive)
        /// </summary>
        /// <param name="container">The container to search in</param>
        /// <param name="data">The data item to find</param>
        /// <returns>The TreeViewItem containing the data, or null if not found</returns>
        public static TreeViewItem FindTreeViewItem(ItemsControl container, object data)
        {
            if (container == null) return null;
            
            if (container.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                var item = container.ItemContainerGenerator.ContainerFromItem(data) as TreeViewItem;
                if (item != null) return item;
                
                foreach (var childData in container.Items)
                {
                    var childContainer = container.ItemContainerGenerator.ContainerFromItem(childData) as TreeViewItem;
                    if (childContainer != null)
                    {
                        var result = FindTreeViewItem(childContainer, data);
                        if (result != null) return result;
                    }
                }
            }
            return null;
        }
        
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
                itemsControl.UpdateLayout();
                itemsControl.ItemContainerGenerator.GenerateNext();
            }
        }
    }
}