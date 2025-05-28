// UI/FileTree/FileTreeVirtualizingPanel.cs
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Custom virtualizing panel that efficiently handles column layouts for file tree items
    /// </summary>
    public class FileTreeVirtualizingPanel : VirtualizingStackPanel
    {
        #region Dependency Properties
        
        public static readonly DependencyProperty ColumnWidthsProperty =
            DependencyProperty.Register("ColumnWidths", typeof(double[]), typeof(FileTreeVirtualizingPanel),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
        
        public double[] ColumnWidths
        {
            get => (double[])GetValue(ColumnWidthsProperty);
            set => SetValue(ColumnWidthsProperty, value);
        }
        
        public static readonly DependencyProperty ColumnSpacingProperty =
            DependencyProperty.Register("ColumnSpacing", typeof(double), typeof(FileTreeVirtualizingPanel),
                new FrameworkPropertyMetadata(5.0, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsArrange));
        
        public double ColumnSpacing
        {
            get => (double)GetValue(ColumnSpacingProperty);
            set => SetValue(ColumnSpacingProperty, value);
        }
        
        #endregion
        
        #region Column Management
        
        private static readonly Dictionary<string, double[]> _columnRegistry = new Dictionary<string, double[]>();
        private static readonly object _registryLock = new object();
        
        /// <summary>
        /// Registers column widths for synchronization across all panels
        /// </summary>
        public static void RegisterColumnWidths(string key, double[] widths)
        {
            lock (_registryLock)
            {
                _columnRegistry[key] = widths;
            }
        }
        
        /// <summary>
        /// Gets registered column widths
        /// </summary>
        public static double[] GetRegisteredColumnWidths(string key)
        {
            lock (_registryLock)
            {
                return _columnRegistry.TryGetValue(key, out var widths) ? widths : null;
            }
        }
        
        #endregion
        
        #region Overrides
        
        protected override Size MeasureOverride(Size availableSize)
        {
            // Use base virtualizing measurement
            var baseSize = base.MeasureOverride(availableSize);
            
            // Ensure minimum width based on column configuration
            if (ColumnWidths != null && ColumnWidths.Length > 0)
            {
                double totalWidth = 0;
                foreach (var width in ColumnWidths)
                {
                    totalWidth += width;
                }
                totalWidth += (ColumnWidths.Length - 1) * ColumnSpacing;
                
                return new Size(Math.Max(baseSize.Width, totalWidth), baseSize.Height);
            }
            
            return baseSize;
        }
        
        protected override Size ArrangeOverride(Size finalSize)
        {
            // Let base handle virtualization
            var result = base.ArrangeOverride(finalSize);
            
            // Apply column layout to visible children only
            if (ColumnWidths != null && ColumnWidths.Length > 0)
            {
                foreach (UIElement child in InternalChildren)
                {
                    if (child is ContentPresenter presenter && presenter.Content is FileTreeItem)
                    {
                        ApplyColumnLayoutToChild(presenter);
                    }
                }
            }
            
            return result;
        }
        
        private void ApplyColumnLayoutToChild(ContentPresenter presenter)
        {
            // Find the grid in the content template
            var grid = FindVisualChild<Grid>(presenter);
            if (grid?.ColumnDefinitions.Count > 0 && ColumnWidths != null)
            {
                // Update column widths efficiently
                int columnIndex = 0;
                for (int i = 0; i < grid.ColumnDefinitions.Count && columnIndex < ColumnWidths.Length; i++)
                {
                    if (i % 2 == 0) // Skip spacer columns
                    {
                        grid.ColumnDefinitions[i].Width = new GridLength(ColumnWidths[columnIndex]);
                        columnIndex++;
                    }
                }
            }
        }
        
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T result)
                    return result;
                
                var childResult = FindVisualChild<T>(child);
                if (childResult != null)
                    return childResult;
            }
            
            return null;
        }
        
        #endregion
    }
}