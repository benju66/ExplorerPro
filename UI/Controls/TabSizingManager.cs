using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls.Interfaces;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Implementation of Chrome-style tab sizing management.
    /// Handles responsive tab sizing with proper compression and overflow handling.
    /// </summary>
    public class TabSizingManager : ITabSizingManager
    {
        #region Private Fields
        
        private readonly ILogger<TabSizingManager> _logger;
        private bool _disposed;
        
        #endregion

        #region Constructor
        
        public TabSizingManager(ILogger<TabSizingManager> logger = null)
        {
            _logger = logger;
            
            // Initialize with Chrome-style defaults
            MinTabWidth = TabDimensions.MinRegularWidth;
            MaxTabWidth = TabDimensions.MaxRegularWidth;
            PreferredTabWidth = TabDimensions.PreferredTabWidth;
            PinnedTabWidth = TabDimensions.PinnedWidth;
            
            _logger?.LogDebug("TabSizingManager initialized with Chrome-style defaults");
        }
        
        #endregion

        #region ITabSizingManager Implementation
        
        public double MinTabWidth { get; set; }
        public double MaxTabWidth { get; set; }
        public double PreferredTabWidth { get; set; }
        public double PinnedTabWidth { get; set; }
        public double AvailableWidth { get; set; }
        public int TabCount { get; set; }
        public int PinnedTabCount { get; set; }

        public event EventHandler<TabSizingChangedEventArgs> SizingChanged;
        public event EventHandler<TabOverflowEventArgs> OverflowStateChanged;

        public double CalculateTabWidth(TabModel tab, int tabIndex, int totalTabs)
        {
            ThrowIfDisposed();
            
            // Pinned tabs always use fixed width
            if (tab.IsPinned)
                return PinnedTabWidth;
                
            var unpinnedTabs = totalTabs - PinnedTabCount;
            if (unpinnedTabs <= 0)
                return PreferredTabWidth;
                
            // Calculate available space for unpinned tabs
            var reservedForPinned = PinnedTabCount * PinnedTabWidth;
            var reservedForSpacing = Math.Max(0, (totalTabs - 1)) * TabDimensions.TabSpacing;
            var reservedForNewTabButton = TabDimensions.NewTabButtonWidth;
            var reservedForOverflow = TabDimensions.OverflowButtonWidth;
            
            var availableForUnpinned = AvailableWidth - reservedForPinned - reservedForSpacing - reservedForNewTabButton;
            
            // Reserve space for overflow button if needed
            var totalRequiredWidth = reservedForPinned + (unpinnedTabs * MinTabWidth) + reservedForSpacing + reservedForNewTabButton;
            if (totalRequiredWidth > AvailableWidth)
            {
                availableForUnpinned -= reservedForOverflow;
            }
            
            if (availableForUnpinned <= 0)
                return MinTabWidth;
                
            var idealWidth = availableForUnpinned / unpinnedTabs;
            
            // Apply Chrome-style progressive compression algorithm
            if (idealWidth >= PreferredTabWidth)
            {
                // Plenty of space - use preferred width
                return PreferredTabWidth;
            }
            else if (idealWidth >= MinTabWidth * 1.5)
            {
                // Moderate compression - linear scaling
                return Math.Max(idealWidth, MinTabWidth);
            }
            else
            {
                // Heavy compression - ensure minimum width
                return MinTabWidth;
            }
        }

        public IReadOnlyList<double> CalculateAllTabWidths(IEnumerable<TabModel> tabs)
        {
            ThrowIfDisposed();
            
            var tabList = tabs.ToList();
            var widths = new List<double>();
            
            var totalTabs = tabList.Count;
            var pinnedCount = tabList.Count(t => t.IsPinned);
            
            // Update internal state
            TabCount = totalTabs;
            PinnedTabCount = pinnedCount;
            
            for (int i = 0; i < tabList.Count; i++)
            {
                var width = CalculateTabWidth(tabList[i], i, totalTabs);
                widths.Add(width);
            }
            
            // Check if we need compression
            var totalWidth = widths.Sum() + (TabDimensions.TabSpacing * (totalTabs - 1));
            var isCompressed = totalWidth > AvailableWidth;
            var overflowStrategy = DetermineOverflowStrategy(tabList);
            
            // Fire events
            var sizingArgs = new TabSizingChangedEventArgs(widths, totalWidth, isCompressed, overflowStrategy);
            SizingChanged?.Invoke(this, sizingArgs);
            
            var hasOverflow = isCompressed && overflowStrategy != TabOverflowStrategy.Compress;
            var overflowCount = hasOverflow ? CalculateOverflowCount(tabList) : 0;
            var overflowArgs = new TabOverflowEventArgs(hasOverflow, overflowCount, overflowStrategy);
            OverflowStateChanged?.Invoke(this, overflowArgs);
            
            _logger?.LogDebug("Calculated tab widths: Total={TotalWidth}, Available={AvailableWidth}, Compressed={IsCompressed}", 
                totalWidth, AvailableWidth, isCompressed);
            
            return widths;
        }

        public void UpdateTabWidths(TabControl tabControl)
        {
            ThrowIfDisposed();
            
            if (tabControl == null)
                return;
                
            var tabs = GetTabModelsFromControl(tabControl);
            var widths = CalculateAllTabWidths(tabs);
            
            // Apply widths to actual tab items with Chrome-style responsive behavior
            for (int i = 0; i < Math.Min(tabControl.Items.Count, widths.Count); i++)
            {
                if (tabControl.Items[i] is TabItem tabItem)
                {
                    var currentWidth = tabItem.Width;
                    var newWidth = widths[i];
                    
                    // Only animate if there's a significant change
                    if (Math.Abs(currentWidth - newWidth) > 2.0 && !double.IsNaN(currentWidth))
                    {
                        // This would integrate with animation manager if available
                        // For now, apply directly for immediate feedback
                        tabItem.Width = newWidth;
                        tabItem.MinWidth = newWidth;
                        tabItem.MaxWidth = newWidth;
                    }
                    else
                    {
                        // Small change or initial setup - apply directly
                        tabItem.Width = newWidth;
                        tabItem.MinWidth = newWidth;
                        tabItem.MaxWidth = newWidth;
                    }
                }
            }
        }

        public void HandleContainerSizeChanged(Size newSize)
        {
            ThrowIfDisposed();
            
            var oldAvailableWidth = AvailableWidth;
            AvailableWidth = newSize.Width;
            
            if (Math.Abs(oldAvailableWidth - AvailableWidth) > 1.0) // Only update if significant change
            {
                _logger?.LogDebug("Container size changed: {OldWidth} -> {NewWidth}", oldAvailableWidth, AvailableWidth);
                // Sizing will be recalculated on next UpdateTabWidths call
            }
        }

        public void HandleTabCountChanged(int newCount, int pinnedCount)
        {
            ThrowIfDisposed();
            
            var oldTabCount = TabCount;
            var oldPinnedCount = PinnedTabCount;
            
            TabCount = newCount;
            PinnedTabCount = pinnedCount;
            
            if (oldTabCount != newCount || oldPinnedCount != pinnedCount)
            {
                _logger?.LogDebug("Tab count changed: {OldCount} -> {NewCount} (Pinned: {OldPinned} -> {NewPinned})", 
                    oldTabCount, newCount, oldPinnedCount, pinnedCount);
                // Sizing will be recalculated on next UpdateTabWidths call
            }
        }

        public double CalculateTabPosition(int tabIndex, IEnumerable<TabModel> tabs)
        {
            ThrowIfDisposed();
            
            var tabList = tabs.ToList();
            var widths = CalculateAllTabWidths(tabList);
            
            double position = 0;
            for (int i = 0; i < tabIndex && i < widths.Count; i++)
            {
                position += widths[i] + TabDimensions.TabSpacing;
            }
            
            return position;
        }

        public double CalculateTotalTabsWidth(IEnumerable<TabModel> tabs)
        {
            ThrowIfDisposed();
            
            var widths = CalculateAllTabWidths(tabs);
            var totalWidth = widths.Sum();
            var spacing = TabDimensions.TabSpacing * (widths.Count - 1);
            
            return totalWidth + spacing;
        }

        public bool NeedsCompression(IEnumerable<TabModel> tabs)
        {
            ThrowIfDisposed();
            
            var totalWidth = CalculateTotalTabsWidth(tabs);
            return totalWidth > AvailableWidth;
        }

        public double CalculateCompressionRatio(IEnumerable<TabModel> tabs)
        {
            ThrowIfDisposed();
            
            if (!NeedsCompression(tabs))
                return 1.0;
                
            var totalWidth = CalculateTotalTabsWidth(tabs);
            return AvailableWidth / totalWidth;
        }

        public void ApplyChromeStyleSizing(TabControl tabControl)
        {
            ThrowIfDisposed();
            
            if (tabControl == null)
                return;
                
            // Update available width from actual control
            AvailableWidth = tabControl.ActualWidth;
            
            // Apply sizing
            UpdateTabWidths(tabControl);
            
            _logger?.LogDebug("Applied Chrome-style sizing to TabControl with {TabCount} tabs", tabControl.Items.Count);
        }

        public TabOverflowStrategy HandleTabOverflow(IEnumerable<TabModel> tabs)
        {
            ThrowIfDisposed();
            
            var strategy = DetermineOverflowStrategy(tabs);
            
            switch (strategy)
            {
                case TabOverflowStrategy.Compress:
                    // Already handled in width calculation
                    break;
                case TabOverflowStrategy.Scroll:
                    // TODO: Implement scroll buttons
                    break;
                case TabOverflowStrategy.Hide:
                    // TODO: Implement tab hiding
                    break;
                case TabOverflowStrategy.Dropdown:
                    // TODO: Implement overflow dropdown
                    break;
            }
            
            return strategy;
        }

        public double CalculateTabOpacity(int tabIndex, int visibleTabCount)
        {
            ThrowIfDisposed();
            
            if (tabIndex < visibleTabCount)
                return 1.0;
                
            // Fade out overflowing tabs
            var fadeStart = visibleTabCount;
            var fadeRange = 3; // Fade over 3 tabs
            var fadePosition = tabIndex - fadeStart;
            
            if (fadePosition >= fadeRange)
                return 0.0;
                
            return 1.0 - (fadePosition / (double)fadeRange);
        }
        
        #endregion

        #region Private Helper Methods
        
        private IEnumerable<TabModel> GetTabModelsFromControl(TabControl tabControl)
        {
            var models = new List<TabModel>();
            
            foreach (var item in tabControl.Items)
            {
                TabModel model = null;
                
                if (item is TabItem tabItem)
                    model = tabItem.DataContext as TabModel ?? tabItem.Tag as TabModel;
                else if (item is TabModel directModel)
                    model = directModel;
                    
                if (model != null)
                    models.Add(model);
            }
            
            return models;
        }

        private TabOverflowStrategy DetermineOverflowStrategy(IEnumerable<TabModel> tabs)
        {
            var tabCount = tabs.Count();
            
            if (tabCount <= 10)
                return TabOverflowStrategy.Compress;
            else if (tabCount <= 20)
                return TabOverflowStrategy.Scroll;
            else
                return TabOverflowStrategy.Dropdown;
        }

        private int CalculateOverflowCount(IEnumerable<TabModel> tabs)
        {
            var tabList = tabs.ToList();
            var widths = CalculateAllTabWidths(tabList);
            
            double currentWidth = 0;
            int visibleCount = 0;
            
            for (int i = 0; i < widths.Count; i++)
            {
                currentWidth += widths[i];
                if (i > 0)
                    currentWidth += TabDimensions.TabSpacing;
                    
                if (currentWidth <= AvailableWidth)
                    visibleCount++;
                else
                    break;
            }
            
            return Math.Max(0, tabList.Count - visibleCount);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TabSizingManager));
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
            if (!_disposed && disposing)
            {
                _disposed = true;
                _logger?.LogDebug("TabSizingManager disposed");
            }
        }
        
        #endregion
    }
} 