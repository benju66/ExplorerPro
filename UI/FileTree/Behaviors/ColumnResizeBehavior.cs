// UI/FileTree/Behaviors/ColumnResizeBehavior.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ExplorerPro.UI.FileTree.Behaviors
{
    /// <summary>
    /// Provides smooth, real-time column resizing behavior with visual feedback
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
        
        #region Resize State
        
        private class ResizeState
        {
            public Grid HeaderGrid { get; set; }
            public ScrollViewer TreeScrollViewer { get; set; }
            public TreeView TreeView { get; set; }
            public int ColumnIndex { get; set; }
            public double OriginalWidth { get; set; }
            public double MinWidth { get; set; }
            public double MaxWidth { get; set; }
            public bool IsResizing { get; set; }
            public ResizePreviewAdorner PreviewAdorner { get; set; }
            public ResizeCompleteCallback OnComplete { get; set; }
            public DispatcherTimer UpdateTimer { get; set; }
            public double LastUpdateWidth { get; set; }
            public string ColumnName { get; set; }
        }
        
        public delegate void ResizeCompleteCallback(string columnName, double newWidth);
        
        private static readonly DependencyProperty ResizeStateProperty =
            DependencyProperty.RegisterAttached("ResizeState", typeof(ResizeState), typeof(ColumnResizeBehavior));
        
        #endregion
        
        #region Event Handlers
        
        private static void AttachToSplitter(GridSplitter splitter)
        {
            splitter.PreviewMouseEnter += Splitter_PreviewMouseEnter;
            splitter.PreviewMouseLeave += Splitter_PreviewMouseLeave;
            splitter.DragStarted += Splitter_DragStarted;
            splitter.DragDelta += Splitter_DragDelta;
            splitter.DragCompleted += Splitter_DragCompleted;
            splitter.MouseDoubleClick += Splitter_MouseDoubleClick;
            
            // Set initial properties
            splitter.ShowsPreview = false; // We'll handle preview ourselves
            splitter.DragIncrement = 1;
            
            // Create resize state
            var state = new ResizeState
            {
                ColumnName = splitter.Tag as string ?? "Unknown",
                UpdateTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(16) // ~60fps
                }
            };
            state.UpdateTimer.Tick += (s, e) => UpdateColumnWidthsThrottled(splitter, state);
            splitter.SetValue(ResizeStateProperty, state);
        }
        
        private static void DetachFromSplitter(GridSplitter splitter)
        {
            splitter.PreviewMouseEnter -= Splitter_PreviewMouseEnter;
            splitter.PreviewMouseLeave -= Splitter_PreviewMouseLeave;
            splitter.DragStarted -= Splitter_DragStarted;
            splitter.DragDelta -= Splitter_DragDelta;
            splitter.DragCompleted -= Splitter_DragCompleted;
            splitter.MouseDoubleClick -= Splitter_MouseDoubleClick;
            
            var state = GetResizeState(splitter);
            state?.UpdateTimer?.Stop();
        }
        
        private static void Splitter_PreviewMouseEnter(object sender, MouseEventArgs e)
        {
            if (sender is GridSplitter splitter)
            {
                AnimateSplitterHighlight(splitter, true);
            }
        }
        
        private static void Splitter_PreviewMouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is GridSplitter splitter && !IsResizing(splitter))
            {
                AnimateSplitterHighlight(splitter, false);
            }
        }
        
        private static void Splitter_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (!(sender is GridSplitter splitter)) return;
            
            var state = GetResizeState(splitter);
            if (state == null) return;
            
            state.IsResizing = true;
            
            // Find the header grid and tree view
            state.HeaderGrid = FindAncestor<Grid>(splitter);
            var userControl = FindAncestor<UserControl>(splitter);
            state.TreeScrollViewer = FindDescendant<ScrollViewer>(userControl, "TreeScrollViewer");
            state.TreeView = FindDescendant<TreeView>(userControl, "fileTreeView");
            
            if (state.HeaderGrid == null) return;
            
            // Determine which column is being resized
            int columnIndex = Grid.GetColumn(splitter);
            if (columnIndex > 0)
            {
                state.ColumnIndex = columnIndex - 1; // Previous column is being resized
                state.OriginalWidth = state.HeaderGrid.ColumnDefinitions[state.ColumnIndex].ActualWidth;
                
                // Get constraints
                var colDef = state.HeaderGrid.ColumnDefinitions[state.ColumnIndex];
                state.MinWidth = colDef.MinWidth > 0 ? colDef.MinWidth : 50;
                state.MaxWidth = colDef.MaxWidth < double.PositiveInfinity ? colDef.MaxWidth : 1000;
                
                state.LastUpdateWidth = state.OriginalWidth;
            }
            
            // Create visual preview
            CreateResizePreview(splitter, state);
            
            // Start real-time updates
            state.UpdateTimer.Start();
        }
        
        private static void Splitter_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (!(sender is GridSplitter splitter)) return;
            
            var state = GetResizeState(splitter);
            if (state == null || !state.IsResizing) return;
            
            // Calculate new width with constraints
            double newWidth = state.OriginalWidth + e.HorizontalChange;
            newWidth = Math.Max(state.MinWidth, Math.Min(state.MaxWidth, newWidth));
            
            // Update header immediately for responsive feel
            if (state.HeaderGrid != null && state.ColumnIndex < state.HeaderGrid.ColumnDefinitions.Count)
            {
                state.HeaderGrid.ColumnDefinitions[state.ColumnIndex].Width = new GridLength(newWidth);
            }
            
            // Store for throttled update
            state.LastUpdateWidth = newWidth;
            
            // Update preview
            UpdateResizePreview(state, newWidth);
            
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
            state.UpdateTimer.Stop();
            
            // Final update
            double finalWidth = state.LastUpdateWidth;
            
            // Ensure all items are updated with final width
            UpdateAllTreeViewItems(state, finalWidth);
            
            // Remove preview
            RemoveResizePreview(state);
            
            // Notify completion
            state.OnComplete?.Invoke(state.ColumnName, finalWidth);
            
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
        
        #region Column Updates
        
        private static void UpdateColumnWidthsThrottled(GridSplitter splitter, ResizeState state)
        {
            if (!state.IsResizing || state.TreeView == null) return;
            
            double newWidth = state.LastUpdateWidth;
            
            // Update visible items only
            UpdateVisibleTreeViewItems(state, newWidth);
        }
        
        private static void UpdateVisibleTreeViewItems(ResizeState state, double newWidth)
        {
            if (state.TreeView == null || state.TreeScrollViewer == null) return;
            
            var scrollViewer = state.TreeScrollViewer;
            double viewportTop = scrollViewer.VerticalOffset;
            double viewportBottom = viewportTop + scrollViewer.ViewportHeight;
            
            // Update items in viewport
            UpdateItemsInViewport(state.TreeView, state.ColumnIndex, newWidth, viewportTop, viewportBottom);
        }
        
        private static void UpdateItemsInViewport(ItemsControl itemsControl, int columnIndex, double newWidth, double viewportTop, double viewportBottom)
        {
            var itemsPresenter = FindDescendant<ItemsPresenter>(itemsControl);
            if (itemsPresenter == null) return;
            
            var panel = VisualTreeHelper.GetChildrenCount(itemsPresenter) > 0 ? 
                       VisualTreeHelper.GetChild(itemsPresenter, 0) as Panel : null;
            if (panel == null) return;
            
            foreach (var child in panel.Children.OfType<TreeViewItem>())
            {
                var position = child.TransformToAncestor(itemsControl).Transform(new Point(0, 0));
                
                // Check if item is in viewport
                if (position.Y + child.ActualHeight >= viewportTop && position.Y <= viewportBottom)
                {
                    UpdateItemGrid(child, columnIndex, newWidth);
                }
                
                // Update expanded children
                if (child.IsExpanded)
                {
                    UpdateItemsInViewport(child, columnIndex, newWidth, viewportTop, viewportBottom);
                }
            }
        }
        
        private static void UpdateItemGrid(TreeViewItem item, int columnIndex, double newWidth)
        {
            var contentPresenter = FindDescendant<ContentPresenter>(item);
            if (contentPresenter == null) return;
            
            var grid = FindDescendant<Grid>(contentPresenter, "ItemGrid");
            if (grid == null) return;
            
            // Map header column index to item grid column index
            int itemColumnIndex = GetItemColumnIndex(columnIndex);
            if (itemColumnIndex >= 0 && itemColumnIndex < grid.ColumnDefinitions.Count)
            {
                grid.ColumnDefinitions[itemColumnIndex].Width = new GridLength(newWidth);
            }
        }
        
        private static void UpdateAllTreeViewItems(ResizeState state, double newWidth)
        {
            if (state.TreeView == null) return;
            
            // Update all items recursively
            UpdateAllItemsRecursive(state.TreeView, state.ColumnIndex, newWidth);
        }
        
        private static void UpdateAllItemsRecursive(ItemsControl itemsControl, int columnIndex, double newWidth)
        {
            foreach (var item in itemsControl.Items)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null)
                {
                    UpdateItemGrid(container, columnIndex, newWidth);
                    
                    if (container.IsExpanded)
                    {
                        UpdateAllItemsRecursive(container, columnIndex, newWidth);
                    }
                }
            }
        }
        
        private static int GetItemColumnIndex(int headerColumnIndex)
        {
            // Map from header grid column index to item grid column index
            // Header: [checkbox][name][splitter][size][splitter][type][splitter][date]
            // Item:   [checkbox][name][spacer][size][spacer][type][spacer][date]
            switch (headerColumnIndex)
            {
                case 1: return 1; // Name column
                case 3: return 3; // Size column
                case 5: return 5; // Type column
                case 7: return 7; // Date column
                default: return -1;
            }
        }
        
        #endregion
        
        #region Visual Feedback
        
        private static void AnimateSplitterHighlight(GridSplitter splitter, bool highlight)
        {
            // Create or get the highlight border
            var adornerLayer = AdornerLayer.GetAdornerLayer(splitter);
            if (adornerLayer == null) return;
            
            if (highlight)
            {
                var highlightAdorner = new SplitterHighlightAdorner(splitter);
                adornerLayer.Add(highlightAdorner);
            }
            else
            {
                var adorners = adornerLayer.GetAdorners(splitter);
                if (adorners != null)
                {
                    foreach (var adorner in adorners.OfType<SplitterHighlightAdorner>())
                    {
                        adornerLayer.Remove(adorner);
                    }
                }
            }
        }
        
        private static void CreateResizePreview(GridSplitter splitter, ResizeState state)
        {
            var adornerLayer = AdornerLayer.GetAdornerLayer(splitter);
            if (adornerLayer != null)
            {
                state.PreviewAdorner = new ResizePreviewAdorner(splitter, state.MinWidth, state.MaxWidth);
                adornerLayer.Add(state.PreviewAdorner);
            }
        }
        
        private static void UpdateResizePreview(ResizeState state, double newWidth)
        {
            state.PreviewAdorner?.UpdateWidth(newWidth);
        }
        
        private static void RemoveResizePreview(ResizeState state)
        {
            if (state.PreviewAdorner != null)
            {
                var adornerLayer = AdornerLayer.GetAdornerLayer(state.PreviewAdorner.AdornedElement);
                adornerLayer?.Remove(state.PreviewAdorner);
                state.PreviewAdorner = null;
            }
        }
        
        private static void ShowConstraintFeedback(GridSplitter splitter)
        {
            // Visual pulse effect when hitting constraints
            var animation = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(200)
            };
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.5, KeyTime.FromPercent(0.5)));
            animation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromPercent(1.0)));
            
            var transform = splitter.RenderTransform as ScaleTransform ?? new ScaleTransform(1, 1);
            splitter.RenderTransform = transform;
            splitter.RenderTransformOrigin = new Point(0.5, 0.5);
            
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
        }
        
        #endregion
        
        #region Auto-Fit
        
        private static void AutoFitColumn(GridSplitter splitter)
        {
            var state = GetResizeState(splitter);
            if (state == null) return;
            
            // Find the header grid and tree view
            state.HeaderGrid = FindAncestor<Grid>(splitter);
            var userControl = FindAncestor<UserControl>(splitter);
            state.TreeView = FindDescendant<TreeView>(userControl, "fileTreeView");
            
            if (state.HeaderGrid == null || state.TreeView == null) return;
            
            // Determine column
            int columnIndex = Grid.GetColumn(splitter);
            if (columnIndex > 0)
            {
                state.ColumnIndex = columnIndex - 1;
            }
            
            // Calculate optimal width
            double optimalWidth = CalculateOptimalWidth(state);
            
            // Animate to optimal width
            AnimateColumnToWidth(state, optimalWidth);
            
            // Notify completion
            state.OnComplete?.Invoke(state.ColumnName, optimalWidth);
        }
        
        private static double CalculateOptimalWidth(ResizeState state)
        {
            double maxWidth = 50; // Minimum
            
            // Measure header content
            if (state.HeaderGrid.Children.Count > state.ColumnIndex)
            {
                var headerElement = state.HeaderGrid.Children[state.ColumnIndex] as FrameworkElement;
                if (headerElement != null)
                {
                    headerElement.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                    maxWidth = Math.Max(maxWidth, headerElement.DesiredSize.Width + 20);
                }
            }
            
            // Measure visible content
            MeasureVisibleContent(state.TreeView, state.ColumnIndex, ref maxWidth);
            
            // Apply constraints
            return Math.Max(state.MinWidth, Math.Min(state.MaxWidth, maxWidth));
        }
        
        private static void MeasureVisibleContent(ItemsControl itemsControl, int columnIndex, ref double maxWidth)
        {
            foreach (var item in itemsControl.Items)
            {
                var container = itemsControl.ItemContainerGenerator.ContainerFromItem(item) as TreeViewItem;
                if (container != null && container.IsVisible)
                {
                    MeasureItemColumn(container, columnIndex, ref maxWidth);
                    
                    if (container.IsExpanded)
                    {
                        MeasureVisibleContent(container, columnIndex, ref maxWidth);
                    }
                }
            }
        }
        
        private static void MeasureItemColumn(TreeViewItem item, int columnIndex, ref double maxWidth)
        {
            var contentPresenter = FindDescendant<ContentPresenter>(item);
            if (contentPresenter == null) return;
            
            var grid = FindDescendant<Grid>(contentPresenter, "ItemGrid");
            if (grid == null) return;
            
            int itemColumnIndex = GetItemColumnIndex(columnIndex);
            if (itemColumnIndex >= 0 && itemColumnIndex < grid.Children.Count)
            {
                foreach (UIElement child in grid.Children)
                {
                    if (Grid.GetColumn(child) == itemColumnIndex)
                    {
                        child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                        maxWidth = Math.Max(maxWidth, child.DesiredSize.Width + 10);
                    }
                }
            }
        }
        
        private static void AnimateColumnToWidth(ResizeState state, double targetWidth)
        {
            if (state.HeaderGrid == null || state.ColumnIndex >= state.HeaderGrid.ColumnDefinitions.Count) return;
            
            var column = state.HeaderGrid.ColumnDefinitions[state.ColumnIndex];
            double currentWidth = column.ActualWidth;
            
            // Create smooth animation
            var storyboard = new Storyboard();
            var animation = new DoubleAnimation
            {
                From = currentWidth,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(300),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            
            // Animate header column
            Storyboard.SetTarget(animation, column);
            Storyboard.SetTargetProperty(animation, new PropertyPath("Width.Value"));
            storyboard.Children.Add(animation);
            
            // Update items during animation
            var updateTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            updateTimer.Tick += (s, e) =>
            {
                UpdateVisibleTreeViewItems(state, column.Width.Value);
            };
            
            storyboard.Completed += (s, e) =>
            {
                updateTimer.Stop();
                UpdateAllTreeViewItems(state, targetWidth);
            };
            
            updateTimer.Start();
            storyboard.Begin();
        }
        
        #endregion
        
        #region Helper Methods
        
        private static ResizeState GetResizeState(GridSplitter splitter)
        {
            return splitter.GetValue(ResizeStateProperty) as ResizeState;
        }
        
        private static bool IsResizing(GridSplitter splitter)
        {
            var state = GetResizeState(splitter);
            return state?.IsResizing ?? false;
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
        
        private static T FindDescendant<T>(DependencyObject obj, string name = null) where T : DependencyObject
        {
            if (obj == null) return null;
            
            int childCount = VisualTreeHelper.GetChildrenCount(obj);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(obj, i);
                
                if (child is T result)
                {
                    if (name == null || (child is FrameworkElement fe && fe.Name == name))
                        return result;
                }
                
                var descendant = FindDescendant<T>(child, name);
                if (descendant != null)
                    return descendant;
            }
            return null;
        }
        
        #endregion
        
        #region Adorners
        
        private class SplitterHighlightAdorner : Adorner
        {
            private readonly Brush _highlightBrush;
            
            public SplitterHighlightAdorner(UIElement adornedElement) : base(adornedElement)
            {
                _highlightBrush = new SolidColorBrush(Color.FromArgb(50, 0, 120, 215));
                IsHitTestVisible = false;
                
                // Animate in
                var animation = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
                BeginAnimation(OpacityProperty, animation);
            }
            
            protected override void OnRender(DrawingContext drawingContext)
            {
                var rect = new Rect(AdornedElement.RenderSize);
                drawingContext.DrawRectangle(_highlightBrush, null, rect);
            }
        }
        
        private class ResizePreviewAdorner : Adorner
        {
            private double _currentWidth;
            private readonly double _minWidth;
            private readonly double _maxWidth;
            private readonly Pen _previewPen;
            private readonly Pen _constraintPen;
            
            public ResizePreviewAdorner(UIElement adornedElement, double minWidth, double maxWidth) : base(adornedElement)
            {
                _minWidth = minWidth;
                _maxWidth = maxWidth;
                _previewPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)), 2);
                _constraintPen = new Pen(new SolidColorBrush(Color.FromArgb(100, 255, 0, 0)), 2);
                IsHitTestVisible = false;
            }
            
            public void UpdateWidth(double width)
            {
                _currentWidth = width;
                InvalidateVisual();
            }
            
            protected override void OnRender(DrawingContext drawingContext)
            {
                // Draw current position indicator
                var isAtConstraint = Math.Abs(_currentWidth - _minWidth) < 1 || Math.Abs(_currentWidth - _maxWidth) < 1;
                var pen = isAtConstraint ? _constraintPen : _previewPen;
                
                var x = AdornedElement.RenderSize.Width / 2;
                drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, AdornedElement.RenderSize.Height));
            }
        }
        
        #endregion
    }
}