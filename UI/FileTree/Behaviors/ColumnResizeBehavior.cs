// UI/FileTree/Behaviors/ColumnResizeBehavior.cs
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ExplorerPro.UI.FileTree.Behaviors
{
    /// <summary>
    /// Provides smooth, real-time column resizing behavior
    /// </summary>
    public static class ColumnResizeBehavior
    {
        #region Attached Properties
        
        public static readonly DependencyProperty EnableSmoothResizeProperty =
            DependencyProperty.RegisterAttached("EnableSmoothResize", typeof(bool), typeof(ColumnResizeBehavior),
                new PropertyMetadata(false, OnEnableSmoothResizeChanged));
        
        public static bool GetEnableSmoothResize(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableSmoothResizeProperty);
        }
        
        public static void SetEnableSmoothResize(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableSmoothResizeProperty, value);
        }
        
        private static void OnEnableSmoothResizeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is GridSplitter splitter)
            {
                if ((bool)e.NewValue)
                {
                    AttachToSplitter(splitter);
                }
                else
                {
                    DetachFromSplitter(splitter);
                }
            }
        }
        
        #endregion
        
        #region Public API
        
        /// <summary>
        /// Sets the resize complete callback for a GridSplitter
        /// </summary>
        public static void SetResizeCompleteCallback(GridSplitter splitter, ResizeCompleteCallback callback)
        {
            if (splitter == null) return;
            
            var state = GetOrCreateResizeState(splitter);
            state.OnComplete = callback;
        }
        
        #endregion
        
        #region Resize State
        
        public delegate void ResizeCompleteCallback(string columnName, double newWidth);
        
        internal class ResizeState
        {
            public Grid HeaderGrid { get; set; }
            public Grid ContentGrid { get; set; }
            public int ColumnIndex { get; set; }
            public double OriginalWidth { get; set; }
            public double MinWidth { get; set; }
            public double MaxWidth { get; set; }
            public bool IsResizing { get; set; }
            public Visual ResizePreview { get; set; }
            public ResizeCompleteCallback OnComplete { get; set; }
        }
        
        public static readonly DependencyProperty ResizeStateProperty =
            DependencyProperty.RegisterAttached("ResizeState", typeof(ResizeState), typeof(ColumnResizeBehavior));
        
        #endregion
        
        #region Event Handlers
        
        private static void AttachToSplitter(GridSplitter splitter)
        {
            splitter.MouseEnter += Splitter_MouseEnter;
            splitter.MouseLeave += Splitter_MouseLeave;
            splitter.DragStarted += Splitter_DragStarted;
            splitter.DragDelta += Splitter_DragDelta;
            splitter.DragCompleted += Splitter_DragCompleted;
            splitter.MouseDoubleClick += Splitter_MouseDoubleClick;
            
            // Set initial properties
            splitter.ShowsPreview = false; // We'll handle preview ourselves
            splitter.DragIncrement = 1;
        }
        
        private static void DetachFromSplitter(GridSplitter splitter)
        {
            splitter.MouseEnter -= Splitter_MouseEnter;
            splitter.MouseLeave -= Splitter_MouseLeave;
            splitter.DragStarted -= Splitter_DragStarted;
            splitter.DragDelta -= Splitter_DragDelta;
            splitter.DragCompleted -= Splitter_DragCompleted;
            splitter.MouseDoubleClick -= Splitter_MouseDoubleClick;
        }
        
        private static void Splitter_MouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is GridSplitter splitter)
            {
                // Animate cursor change
                AnimateSplitterHighlight(splitter, true);
            }
        }
        
        private static void Splitter_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is GridSplitter splitter && !IsResizing(splitter))
            {
                AnimateSplitterHighlight(splitter, false);
            }
        }
        
        private static void Splitter_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (!(sender is GridSplitter splitter)) return;
            
            var state = GetOrCreateResizeState(splitter);
            state.IsResizing = true;
            
            // Find the column being resized
            var grid = splitter.Parent as Grid;
            if (grid == null) return;
            
            int columnIndex = Grid.GetColumn(splitter);
            if (columnIndex > 0)
            {
                state.ColumnIndex = columnIndex - 1; // Previous column is being resized
                state.OriginalWidth = grid.ColumnDefinitions[state.ColumnIndex].ActualWidth;
                
                // Get constraints from column definition
                var colDef = grid.ColumnDefinitions[state.ColumnIndex];
                state.MinWidth = colDef.MinWidth > 0 ? colDef.MinWidth : 50;
                state.MaxWidth = colDef.MaxWidth < double.PositiveInfinity ? colDef.MaxWidth : 1000;
            }
            
            // Create visual feedback
            CreateResizePreview(splitter, state);
            
            // Start real-time updates
            StartRealTimeUpdate(splitter);
        }
        
        private static void Splitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!(sender is GridSplitter splitter)) return;
            
            var state = GetResizeState(splitter);
            if (state == null || !state.IsResizing) return;
            
            // Calculate new width with constraints
            double newWidth = state.OriginalWidth + e.HorizontalChange;
            newWidth = Math.Max(state.MinWidth, Math.Min(state.MaxWidth, newWidth));
            
            // Update all synchronized elements in real-time
            UpdateColumnWidth(splitter, state.ColumnIndex, newWidth);
            
            // Update visual feedback
            UpdateResizePreview(splitter, state, newWidth);
            
            // Show constraint feedback
            if (Math.Abs(newWidth - state.MinWidth) < 1 || Math.Abs(newWidth - state.MaxWidth) < 1)
            {
                ShowConstraintFeedback(splitter);
            }
        }
        
        private static void Splitter_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            if (!(sender is GridSplitter splitter)) return;
            
            var state = GetResizeState(splitter);
            if (state == null) return;
            
            state.IsResizing = false;
            
            // Get final width
            var grid = splitter.Parent as Grid;
            double finalWidth = grid?.ColumnDefinitions[state.ColumnIndex].ActualWidth ?? state.OriginalWidth;
            
            // Animate to final position
            AnimateToFinalWidth(splitter, state, finalWidth);
            
            // Cleanup
            RemoveResizePreview(splitter, state);
            StopRealTimeUpdate(splitter);
            
            // Notify completion
            state.OnComplete?.Invoke(GetColumnName(splitter), finalWidth);
            
            // Animate splitter back to normal
            AnimateSplitterHighlight(splitter, false);
        }
        
        private static void Splitter_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (!(sender is GridSplitter splitter)) return;
            
            // Auto-fit column
            AutoFitColumn(splitter);
            e.Handled = true;
        }
        
        #endregion
        
        #region Real-Time Updates
        
        private static void UpdateColumnWidth(GridSplitter splitter, int columnIndex, double newWidth)
        {
            // Update header grid
            var headerGrid = FindAncestor<Grid>(splitter);
            if (headerGrid?.ColumnDefinitions.Count > columnIndex)
            {
                headerGrid.ColumnDefinitions[columnIndex].Width = new GridLength(newWidth);
            }
            
            // Update all visible content items
            var scrollViewer = FindAncestor<ScrollViewer>(splitter);
            if (scrollViewer != null)
            {
                var treeView = FindDescendant<TreeView>(scrollViewer);
                if (treeView != null)
                {
                    UpdateVisibleItems(treeView, columnIndex, newWidth);
                }
            }
        }
        
        private static void UpdateVisibleItems(TreeView treeView, int columnIndex, double newWidth)
        {
            // Only update items in the viewport for performance
            var scrollViewer = FindDescendant<ScrollViewer>(treeView);
            if (scrollViewer == null) return;
            
            double viewportTop = scrollViewer.VerticalOffset;
            double viewportBottom = viewportTop + scrollViewer.ViewportHeight;
            
            UpdateItemsInViewport(treeView, columnIndex, newWidth, viewportTop, viewportBottom);
        }
        
        private static void UpdateItemsInViewport(ItemsControl itemsControl, int columnIndex, double newWidth, double viewportTop, double viewportBottom)
        {
            var itemsPresenter = FindDescendant<ItemsPresenter>(itemsControl);
            if (itemsPresenter == null) return;
            
            var panel = VisualTreeHelper.GetChild(itemsPresenter, 0) as Panel;
            if (panel == null) return;
            
            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is TreeViewItem item)
                {
                    var position = item.TransformToAncestor(itemsControl).Transform(new Point(0, 0));
                    
                    // Check if item is in viewport
                    if (position.Y + item.ActualHeight >= viewportTop && position.Y <= viewportBottom)
                    {
                        UpdateItemColumns(item, columnIndex, newWidth);
                    }
                    
                    // Recursively update expanded children
                    if (item.IsExpanded)
                    {
                        UpdateItemsInViewport(item, columnIndex, newWidth, viewportTop, viewportBottom);
                    }
                }
            }
        }
        
        private static void UpdateItemColumns(TreeViewItem item, int columnIndex, double newWidth)
        {
            var contentPresenter = FindDescendant<ContentPresenter>(item);
            if (contentPresenter == null) return;
            
            var grid = FindDescendant<Grid>(contentPresenter);
            if (grid?.ColumnDefinitions.Count > columnIndex * 2) // Account for spacer columns
            {
                grid.ColumnDefinitions[columnIndex * 2].Width = new GridLength(newWidth);
            }
        }
        
        #endregion
        
        #region Visual Feedback
        
        private static void AnimateSplitterHighlight(GridSplitter splitter, bool highlight)
        {
            var animation = new DoubleAnimation
            {
                To = highlight ? 0.6 : 0.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            splitter.BeginAnimation(UIElement.OpacityProperty, animation);
        }
        
        private static void CreateResizePreview(GridSplitter splitter, ResizeState state)
        {
            // Create a visual indicator showing the resize operation
            var preview = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 0, 120, 215)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)),
                BorderThickness = new Thickness(1),
                IsHitTestVisible = false
            };
            
            state.ResizePreview = preview;
            
            // Add to adorner layer
            var adornerLayer = AdornerLayer.GetAdornerLayer(splitter);
            if (adornerLayer != null)
            {
                adornerLayer.Add(new ResizeAdorner(splitter, preview));
            }
        }
        
        private static void UpdateResizePreview(GridSplitter splitter, ResizeState state, double newWidth)
        {
            // Update preview position and size
            if (state.ResizePreview is Border preview)
            {
                preview.Width = newWidth;
            }
        }
        
        private static void RemoveResizePreview(GridSplitter splitter, ResizeState state)
        {
            if (state.ResizePreview != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(splitter);
                var adorners = adornerLayer?.GetAdorners(splitter);
                if (adorners != null)
                {
                    foreach (var adorner in adorners)
                    {
                        if (adorner is ResizeAdorner)
                        {
                            adornerLayer.Remove(adorner);
                        }
                    }
                }
                state.ResizePreview = null;
            }
        }
        
        private static void ShowConstraintFeedback(GridSplitter splitter)
        {
            // Provide haptic-like feedback when hitting constraints
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(100)
            };
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.2, KeyTime.FromPercent(0.5)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
            
            var transform = splitter.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
            splitter.RenderTransform = transform;
            splitter.RenderTransformOrigin = new Point(0.5, 0.5);
            
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        }
        
        private static void AnimateToFinalWidth(GridSplitter splitter, ResizeState state, double finalWidth)
        {
            // Smooth animation to final width
            var grid = splitter.Parent as Grid;
            if (grid == null || state.ColumnIndex >= grid.ColumnDefinitions.Count) return;
            
            var column = grid.ColumnDefinitions[state.ColumnIndex];
            var currentWidth = column.ActualWidth;
            
            if (Math.Abs(currentWidth - finalWidth) > 1)
            {
                var animation = new DoubleAnimation
                {
                    From = currentWidth,
                    To = finalWidth,
                    Duration = TimeSpan.FromMilliseconds(150),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                
                animation.Completed += (s, e) => {
                    column.Width = new GridLength(finalWidth);
                };
                
                column.BeginAnimation(ColumnDefinition.WidthProperty, animation);
            }
        }
        
        #endregion
        
        #region Auto-Fit
        
        private static void AutoFitColumn(GridSplitter splitter)
        {
            var state = GetOrCreateResizeState(splitter);
            var grid = splitter.Parent as Grid;
            if (grid == null || state.ColumnIndex >= grid.ColumnDefinitions.Count) return;
            
            // Calculate optimal width based on content
            double optimalWidth = CalculateOptimalWidth(splitter, state.ColumnIndex);
            
            // Animate to optimal width
            var column = grid.ColumnDefinitions[state.ColumnIndex];
            var animation = new DoubleAnimation
            {
                To = optimalWidth,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            column.BeginAnimation(ColumnDefinition.WidthProperty, animation);
            
            // Update all items
            UpdateColumnWidth(splitter, state.ColumnIndex, optimalWidth);
            
            // Notify completion
            state.OnComplete?.Invoke(GetColumnName(splitter), optimalWidth);
        }
        
        private static double CalculateOptimalWidth(GridSplitter splitter, int columnIndex)
        {
            // Find the tree view and measure content
            var scrollViewer = FindAncestor<ScrollViewer>(splitter);
            var treeView = FindDescendant<TreeView>(scrollViewer);
            if (treeView == null) return 150; // Default
            
            double maxWidth = 50; // Minimum
            
            // Measure visible items
            MeasureVisibleItems(treeView, columnIndex, ref maxWidth);
            
            // Add some padding
            maxWidth += 20;
            
            // Apply constraints
            return Math.Max(50, Math.Min(600, maxWidth));
        }
        
        private static void MeasureVisibleItems(ItemsControl itemsControl, int columnIndex, ref double maxWidth)
        {
            var itemsPresenter = FindDescendant<ItemsPresenter>(itemsControl);
            if (itemsPresenter == null) return;
            
            var panel = VisualTreeHelper.GetChild(itemsPresenter, 0) as Panel;
            if (panel == null) return;
            
            for (int i = 0; i < panel.Children.Count; i++)
            {
                if (panel.Children[i] is TreeViewItem item)
                {
                    MeasureItemColumn(item, columnIndex, ref maxWidth);
                    
                    if (item.IsExpanded)
                    {
                        MeasureVisibleItems(item, columnIndex, ref maxWidth);
                    }
                }
            }
        }
        
        private static void MeasureItemColumn(TreeViewItem item, int columnIndex, ref double maxWidth)
        {
            var contentPresenter = FindDescendant<ContentPresenter>(item);
            if (contentPresenter == null) return;
            
            var grid = FindDescendant<Grid>(contentPresenter);
            if (grid == null) return;
            
            // Find the content in the column
            int gridColumnIndex = columnIndex * 2; // Account for spacers
            foreach (UIElement child in grid.Children)
            {
                if (Grid.GetColumn(child) == gridColumnIndex)
                {
                    child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    maxWidth = Math.Max(maxWidth, child.DesiredSize.Width);
                }
            }
        }
        
        #endregion
        
        #region Helper Methods
        
        private static ResizeState GetOrCreateResizeState(GridSplitter splitter)
        {
            var state = splitter.GetValue(ResizeStateProperty) as ResizeState;
            if (state == null)
            {
                state = new ResizeState();
                splitter.SetValue(ResizeStateProperty, state);
            }
            return state;
        }
        
        private static ResizeState GetResizeState(GridSplitter splitter)
        {
            return splitter.GetValue(ResizeStateProperty) as ResizeState;
        }
        
        private static bool IsResizing(GridSplitter splitter)
        {
            var state = GetResizeState(splitter);
            return state?.IsResizing ?? false;
        }
        
        private static string GetColumnName(GridSplitter splitter)
        {
            return splitter.Tag as string ?? "Unknown";
        }
        
        private static void StartRealTimeUpdate(GridSplitter splitter)
        {
            // Enable real-time update mode
            splitter.SetValue(RenderOptions.EdgeModeProperty, EdgeMode.Aliased);
        }
        
        private static void StopRealTimeUpdate(GridSplitter splitter)
        {
            // Restore normal rendering
            splitter.ClearValue(RenderOptions.EdgeModeProperty);
        }
        
        private static T FindAncestor<T>(DependencyObject obj) where T : DependencyObject
        {
            while (obj != null)
            {
                if (obj is T result)
                    return result;
                obj = VisualTreeHelper.GetParent(obj);
            }
            return null;
        }
        
        private static T FindDescendant<T>(DependencyObject obj) where T : DependencyObject
        {
            if (obj == null) return null;
            
            int childCount = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                if (child is T result)
                    return result;
                    
                var descendant = FindDescendant<T>(child);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }
        
        #endregion
        
        #region Adorner for Resize Preview
        
        private class ResizeAdorner : Adorner
        {
            private readonly Visual _visual;
            
            public ResizeAdorner(UIElement adornedElement, Visual visual) : base(adornedElement)
            {
                _visual = visual;
                IsHitTestVisible = false;
            }
            
            protected override Visual GetVisualChild(int index) => _visual;
            protected override int VisualChildrenCount => 1;
            
            protected override Size ArrangeOverride(Size finalSize)
            {
                if (_visual is UIElement element)
                {
                    element.Arrange(new Rect(finalSize));
                }
                return finalSize;
            }
        }
        
        #endregion
    }
}