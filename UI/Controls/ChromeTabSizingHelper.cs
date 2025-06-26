using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.Models;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Helper class that implements Chrome-style tab sizing algorithm.
    /// Ensures consistent tab widths and proper distribution regardless of tab count.
    /// </summary>
    public static class ChromeTabSizingHelper
    {
        #region Constants
        
        /// <summary>
        /// Minimum width for a tab (matches Chrome behavior)
        /// </summary>
        public const double MIN_TAB_WIDTH = 40.0;
        
        /// <summary>
        /// Maximum width for a tab (matches Chrome behavior)
        /// </summary>
        public const double MAX_TAB_WIDTH = 240.0;
        
        /// <summary>
        /// Preferred tab width when there's plenty of space
        /// </summary>
        public const double PREFERRED_TAB_WIDTH = 180.0;
        
        /// <summary>
        /// Width for pinned tabs
        /// </summary>
        public const double PINNED_TAB_WIDTH = 40.0;
        
        /// <summary>
        /// Margin between tabs
        /// </summary>
        public const double TAB_MARGIN = 2.0;
        
        /// <summary>
        /// Space reserved for the add tab button
        /// </summary>
        public const double ADD_BUTTON_SPACE = 45.0;
        
        /// <summary>
        /// Minimum space to maintain on the right side
        /// </summary>
        public const double RIGHT_MARGIN = 20.0;
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Calculates optimal tab widths for Chrome-style distribution
        /// </summary>
        /// <param name="tabItems">Collection of tab items</param>
        /// <param name="availableWidth">Available width for tabs</param>
        /// <param name="includeAddButton">Whether to reserve space for add button</param>
        /// <returns>Dictionary mapping tab IDs to their calculated widths</returns>
        public static Dictionary<string, double> CalculateTabWidths(
            IEnumerable<TabItemModel> tabItems, 
            double availableWidth, 
            bool includeAddButton = true)
        {
            if (tabItems == null || !tabItems.Any())
                return new Dictionary<string, double>();

            var tabs = tabItems.ToList();
            var result = new Dictionary<string, double>();
            
            // Calculate available space
            var reservedSpace = (includeAddButton ? ADD_BUTTON_SPACE : 0) + RIGHT_MARGIN;
            var usableWidth = Math.Max(0, availableWidth - reservedSpace);
            
            // Separate pinned and unpinned tabs
            var pinnedTabs = tabs.Where(t => t.IsPinned).ToList();
            var unpinnedTabs = tabs.Where(t => !t.IsPinned).ToList();
            
            // Calculate space used by pinned tabs
            var pinnedSpace = pinnedTabs.Count * (PINNED_TAB_WIDTH + TAB_MARGIN);
            var remainingSpace = Math.Max(0, usableWidth - pinnedSpace);
            
            // Set pinned tab widths
            foreach (var pinnedTab in pinnedTabs)
            {
                result[pinnedTab.Id] = PINNED_TAB_WIDTH;
            }
            
            // Calculate unpinned tab widths
            if (unpinnedTabs.Any())
            {
                var unpinnedTabWidth = CalculateUnpinnedTabWidth(unpinnedTabs.Count, remainingSpace);
                foreach (var unpinnedTab in unpinnedTabs)
                {
                    result[unpinnedTab.Id] = unpinnedTabWidth;
                }
            }
            
            return result;
        }
        
        /// <summary>
        /// Applies calculated widths to actual TabItem controls
        /// </summary>
        /// <param name="tabControl">The tab control containing the tabs</param>
        /// <param name="widthCalculations">Pre-calculated widths</param>
        public static void ApplyTabWidths(TabControl tabControl, Dictionary<string, double> widthCalculations)
        {
            if (tabControl?.Items == null || widthCalculations == null)
                return;
                
            foreach (TabItem tabItem in tabControl.Items)
            {
                var tabModel = GetTabModel(tabItem);
                if (tabModel != null && widthCalculations.TryGetValue(tabModel.Id, out var width))
                {
                    // Apply width with smooth transition
                    ApplyWidthWithTransition(tabItem, width);
                }
            }
        }
        
        /// <summary>
        /// Updates tab widths for a specific TabControl based on current layout
        /// </summary>
        /// <param name="tabControl">The tab control to update</param>
        /// <param name="includeAddButton">Whether to account for add button space</param>
        public static void UpdateTabWidths(TabControl tabControl, bool includeAddButton = true)
        {
            if (tabControl?.Items == null)
                return;
                
            var availableWidth = tabControl.ActualWidth;
            if (availableWidth <= 0)
                availableWidth = tabControl.Width;
                
            if (availableWidth <= 0 || double.IsNaN(availableWidth))
                return;
            
            var tabModels = GetTabModels(tabControl);
            var widthCalculations = CalculateTabWidths(tabModels, availableWidth, includeAddButton);
            ApplyTabWidths(tabControl, widthCalculations);
        }
        
        /// <summary>
        /// Gets the optimal tab panel width based on tab count and desired tab size
        /// </summary>
        /// <param name="tabCount">Number of tabs</param>
        /// <param name="pinnedCount">Number of pinned tabs</param>
        /// <param name="includeAddButton">Whether to include add button space</param>
        /// <returns>Calculated optimal panel width</returns>
        public static double CalculateOptimalPanelWidth(int tabCount, int pinnedCount = 0, bool includeAddButton = true)
        {
            var unpinnedCount = tabCount - pinnedCount;
            var pinnedSpace = pinnedCount * (PINNED_TAB_WIDTH + TAB_MARGIN);
            var unpinnedSpace = unpinnedCount * (PREFERRED_TAB_WIDTH + TAB_MARGIN);
            var buttonSpace = includeAddButton ? ADD_BUTTON_SPACE : 0;
            
            return pinnedSpace + unpinnedSpace + buttonSpace + RIGHT_MARGIN;
        }
        
        #endregion

        #region Private Methods
        
        /// <summary>
        /// Calculates the width for unpinned tabs based on available space
        /// </summary>
        private static double CalculateUnpinnedTabWidth(int tabCount, double availableSpace)
        {
            if (tabCount <= 0)
                return PREFERRED_TAB_WIDTH;
            
            // Account for margins between tabs
            var marginsSpace = (tabCount - 1) * TAB_MARGIN;
            var spaceForTabs = Math.Max(0, availableSpace - marginsSpace);
            
            // Calculate width per tab
            var widthPerTab = spaceForTabs / tabCount;
            
            // Apply Chrome-style constraints
            if (widthPerTab >= PREFERRED_TAB_WIDTH)
            {
                // Use preferred width if there's plenty of space
                return PREFERRED_TAB_WIDTH;
            }
            else if (widthPerTab >= MIN_TAB_WIDTH)
            {
                // Use calculated width if it's within acceptable range
                return Math.Min(widthPerTab, MAX_TAB_WIDTH);
            }
            else
            {
                // Use minimum width if space is very constrained
                return MIN_TAB_WIDTH;
            }
        }
        
        /// <summary>
        /// Applies width to a TabItem with smooth transition animation
        /// </summary>
        private static void ApplyWidthWithTransition(TabItem tabItem, double targetWidth)
        {
            if (tabItem == null || double.IsNaN(targetWidth) || targetWidth <= 0)
                return;
            
            // Set width properties for consistent behavior
            tabItem.Width = targetWidth;
            tabItem.MinWidth = targetWidth;
            tabItem.MaxWidth = targetWidth;
            
            // Apply to the TabItemModel if available
            var tabModel = GetTabModel(tabItem);
            if (tabModel != null)
            {
                // Store the calculated width for future reference
                SetCalculatedWidth(tabItem, targetWidth);
            }
        }
        
        /// <summary>
        /// Gets the TabItemModel from a TabItem (works with both direct Tag and adapter patterns)
        /// </summary>
        private static TabItemModel GetTabModel(TabItem tabItem)
        {
            if (tabItem?.Tag is TabItemModel model)
                return model;
                
            // Handle adapter pattern
            if (tabItem?.Tag is TabModelAdapter adapter)
                return adapter;
                
            return null;
        }
        
        /// <summary>
        /// Gets all TabItemModels from a TabControl
        /// </summary>
        private static IEnumerable<TabItemModel> GetTabModels(TabControl tabControl)
        {
            if (tabControl?.Items == null)
                return Enumerable.Empty<TabItemModel>();
                
            return tabControl.Items
                .OfType<TabItem>()
                .Select(GetTabModel)
                .Where(model => model != null);
        }
        
        /// <summary>
        /// Stores the calculated width as attached property for reference
        /// </summary>
        private static void SetCalculatedWidth(TabItem tabItem, double width)
        {
            tabItem.SetValue(CalculatedWidthProperty, width);
        }
        
        /// <summary>
        /// Gets the stored calculated width
        /// </summary>
        private static double GetCalculatedWidth(TabItem tabItem)
        {
            var value = tabItem.GetValue(CalculatedWidthProperty);
            return value is double d ? d : PREFERRED_TAB_WIDTH;
        }
        
        #endregion

        #region Attached Properties
        
        /// <summary>
        /// Attached property to store calculated width for reference
        /// </summary>
        public static readonly DependencyProperty CalculatedWidthProperty =
            DependencyProperty.RegisterAttached(
                "CalculatedWidth",
                typeof(double),
                typeof(ChromeTabSizingHelper),
                new PropertyMetadata(PREFERRED_TAB_WIDTH));
        
        /// <summary>
        /// Gets the calculated width attached property
        /// </summary>
        public static double GetCalculatedWidth(DependencyObject obj)
        {
            return (double)obj.GetValue(CalculatedWidthProperty);
        }
        
        /// <summary>
        /// Sets the calculated width attached property
        /// </summary>
        public static void SetCalculatedWidth(DependencyObject obj, double value)
        {
            obj.SetValue(CalculatedWidthProperty, value);
        }
        
        #endregion
    }
} 