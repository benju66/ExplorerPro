// UI/FileTree/Services/FileTreeThemeService.cs
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
    /// with proper memory leak prevention
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

            // Create and store event handlers
            _themeChangedHandler = OnThemeChanged;
            _themeRefreshedHandler = OnThemeRefreshed;

            // Subscribe to theme change events
            ThemeManager.Instance.ThemeChanged += _themeChangedHandler;
            ThemeManager.Instance.ThemeRefreshed += _themeRefreshedHandler;
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Refreshes all theme elements in the file tree
        /// </summary>
        public void RefreshThemeElements()
        {
            if (_disposed)
                return;

            try
            {
                bool isDarkMode = ThemeManager.Instance.IsDarkMode;
                
                // Update main grid background
                if (_mainGrid != null)
                {
                    _mainGrid.Background = GetResource<SolidColorBrush>("BackgroundColor");
                }
                
                // Update TreeView itself
                if (_treeView != null)
                {
                    _treeView.Background = GetResource<SolidColorBrush>("TreeViewBackground");
                    _treeView.BorderBrush = GetResource<SolidColorBrush>("TreeViewBorder");
                    _treeView.Foreground = GetResource<SolidColorBrush>("TextColor");
                    
                    // Update TreeViewItems
                    RefreshTreeViewItems();
                }
                
                // Refresh dynamic resources in DataTemplates
                RefreshDataTemplateResources();
                
                // Clean up dead weak references
                CleanupDeadReferences();
                
                Console.WriteLine("FileTree theme elements refreshed successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing file tree theme: {ex.Message}");
                // Non-critical error, continue
            }
        }

        /// <summary>
        /// Applies theme to a specific TreeViewItem
        /// </summary>
        /// <param name="treeViewItem">The TreeViewItem to theme</param>
        public void ApplyThemeToTreeViewItem(TreeViewItem treeViewItem)
        {
            if (_disposed || treeViewItem == null) return;

            var textColor = GetResource<SolidColorBrush>("TextColor");
            var treeLine = GetResource<SolidColorBrush>("TreeLineColor");

            // Apply theme to the TreeViewItem
            treeViewItem.Foreground = textColor;
            
            // Track the themed element with weak reference
            _themedElements.Add(new WeakReference(treeViewItem));
            
            // Find and update all TextBlocks within the item
            foreach (var textBlock in VisualTreeHelperEx.FindVisualChildren<TextBlock>(treeViewItem))
            {
                // Don't override custom colors from metadata
                if (textBlock.Foreground == SystemColors.WindowTextBrush)
                {
                    textBlock.Foreground = textColor;
                }
            }
            
            // Update tree lines in the item
            foreach (var line in VisualTreeHelperEx.FindVisualChildren<System.Windows.Shapes.Line>(treeViewItem))
            {
                line.Stroke = treeLine;
                
                // Set up mouse over handling for lines
                if (line.Parent is UIElement parent)
                {
                    SetupTreeLineMouseHandlers(parent);
                }
            }
            
            // Update toggle buttons
            foreach (var toggle in VisualTreeHelperEx.FindVisualChildren<ToggleButton>(treeViewItem))
            {
                RefreshTreeViewToggleButton(toggle);
            }
            
            // Update Images (file/folder icons)
            foreach (var image in VisualTreeHelperEx.FindVisualChildren<Image>(treeViewItem))
            {
                // Keep the image as is - just ensure it's visible
                image.Opacity = 1.0;
            }
        }

        /// <summary>
        /// Forces a theme refresh on all visible items
        /// </summary>
        public void ForceThemeRefresh()
        {
            if (_disposed)
                return;

            // Schedule the refresh on the dispatcher with lower priority
            _treeView?.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                if (!_disposed)
                {
                    RefreshThemeElements();
                }
            }));
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Refreshes TreeViewItems with theme-appropriate styling
        /// </summary>
        private void RefreshTreeViewItems()
        {
            if (_disposed || _treeView == null)
                return;

            try
            {
                // Get theme colors for tree lines and text
                var treeLine = GetResource<SolidColorBrush>("TreeLineColor");
                var treeLineHighlight = GetResource<SolidColorBrush>("TreeLineHighlightColor");
                var textColor = GetResource<SolidColorBrush>("TextColor");
                
                // Update all TreeViewItems
                foreach (var item in VisualTreeHelperEx.FindVisualChildren<TreeViewItem>(_treeView))
                {
                    ApplyThemeToTreeViewItem(item);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing tree view items: {ex.Message}");
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
                // Set background to transparent to let hover effect work
                toggle.Background = Brushes.Transparent;
                    
                // Find the Path element for the expander arrow
                var pathElement = VisualTreeHelperEx.FindVisualChild<System.Windows.Shapes.Path>(toggle);
                if (pathElement != null)
                {
                    pathElement.Stroke = GetResource<SolidColorBrush>("TextColor");
                    pathElement.Fill = GetResource<SolidColorBrush>("TextColor");
                    
                    // Remove existing handlers
                    RemoveMouseHandlers(toggle);

                    // Create new handlers
                    MouseEventHandler enterHandler = (s, e) => ToggleButton_MouseEnter(s, e);
                    MouseEventHandler leaveHandler = (s, e) => ToggleButton_MouseLeave(s, e);

                    // Store and attach handlers
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
                    line.Stroke = GetResource<SolidColorBrush>("TreeLineHighlightColor");
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
                    line.Stroke = GetResource<SolidColorBrush>("TreeLineColor");
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
                        pathElement.Stroke = GetResource<SolidColorBrush>("TreeLineHighlightColor");
                        pathElement.Fill = GetResource<SolidColorBrush>("TreeLineHighlightColor");
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
                        pathElement.Stroke = GetResource<SolidColorBrush>("TextColor");
                        pathElement.Fill = GetResource<SolidColorBrush>("TextColor");
                    }
                }
            }
            catch { /* Ignore errors in UI effects */ }
        }

        /// <summary>
        /// Refreshes resources in data templates
        /// </summary>
        private void RefreshDataTemplateResources()
        {
            if (_disposed || _treeView == null)
                return;

            try
            {
                // Force a refresh of all item containers
                _treeView.UpdateLayout();
                
                // Refresh the items panel (if available)
                var itemsPresenter = VisualTreeHelperEx.FindVisualChild<ItemsPresenter>(_treeView);
                if (itemsPresenter != null)
                {
                    itemsPresenter.UpdateLayout();
                }
                
                // Explicitly update all visible TextBlocks in the tree
                var textBlocks = VisualTreeHelperEx.FindVisualChildren<TextBlock>(_treeView);
                foreach (var textBlock in textBlocks)
                {
                    // Don't override custom foreground colors (from metadata)
                    if (textBlock.Foreground == SystemColors.WindowTextBrush)
                    {
                        textBlock.Foreground = GetResource<SolidColorBrush>("TextColor");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Error refreshing data templates: {ex.Message}");
            }
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
                
                // Try ThemeManager as a fallback for resources
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
                    
                    // Clear dictionaries
                    _mouseEnterHandlers?.Clear();
                    _mouseLeaveHandlers?.Clear();
                    
                    // Clear themed elements list
                    _themedElements?.Clear();
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