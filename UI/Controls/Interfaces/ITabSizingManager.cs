using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.Models;

namespace ExplorerPro.UI.Controls.Interfaces
{
    /// <summary>
    /// Interface for managing tab sizing and layout calculations.
    /// Provides Chrome-style tab sizing with responsive behavior.
    /// </summary>
    public interface ITabSizingManager : IDisposable
    {
        #region Properties
        
        /// <summary>
        /// Minimum tab width
        /// </summary>
        double MinTabWidth { get; set; }
        
        /// <summary>
        /// Maximum tab width
        /// </summary>
        double MaxTabWidth { get; set; }
        
        /// <summary>
        /// Preferred tab width when space allows
        /// </summary>
        double PreferredTabWidth { get; set; }
        
        /// <summary>
        /// Width for pinned tabs
        /// </summary>
        double PinnedTabWidth { get; set; }
        
        /// <summary>
        /// Available width for tabs
        /// </summary>
        double AvailableWidth { get; set; }
        
        /// <summary>
        /// Total number of tabs to size
        /// </summary>
        int TabCount { get; set; }
        
        /// <summary>
        /// Number of pinned tabs
        /// </summary>
        int PinnedTabCount { get; set; }
        
        #endregion

        #region Core Sizing Operations
        
        /// <summary>
        /// Calculates optimal width for a specific tab
        /// </summary>
        double CalculateTabWidth(TabModel tab, int tabIndex, int totalTabs);
        
        /// <summary>
        /// Calculates widths for all tabs
        /// </summary>
        IReadOnlyList<double> CalculateAllTabWidths(IEnumerable<TabModel> tabs);
        
        /// <summary>
        /// Updates tab widths based on current constraints
        /// </summary>
        void UpdateTabWidths(TabControl tabControl);
        
        /// <summary>
        /// Recalculates sizes when container size changes
        /// </summary>
        void HandleContainerSizeChanged(Size newSize);
        
        /// <summary>
        /// Recalculates sizes when tab count changes
        /// </summary>
        void HandleTabCountChanged(int newCount, int pinnedCount);
        
        #endregion

        #region Layout Calculations
        
        /// <summary>
        /// Calculates the position for a tab at the specified index
        /// </summary>
        double CalculateTabPosition(int tabIndex, IEnumerable<TabModel> tabs);
        
        /// <summary>
        /// Calculates total width needed for all tabs
        /// </summary>
        double CalculateTotalTabsWidth(IEnumerable<TabModel> tabs);
        
        /// <summary>
        /// Determines if tabs need to be compressed
        /// </summary>
        bool NeedsCompression(IEnumerable<TabModel> tabs);
        
        /// <summary>
        /// Calculates compression ratio when space is limited
        /// </summary>
        double CalculateCompressionRatio(IEnumerable<TabModel> tabs);
        
        #endregion

        #region Chrome-Style Features
        
        /// <summary>
        /// Applies Chrome-style sizing algorithm
        /// </summary>
        void ApplyChromeStyleSizing(TabControl tabControl);
        
        /// <summary>
        /// Handles tab overflow (scrolling or compression)
        /// </summary>
        TabOverflowStrategy HandleTabOverflow(IEnumerable<TabModel> tabs);
        
        /// <summary>
        /// Calculates fade effect for overflowing tabs
        /// </summary>
        double CalculateTabOpacity(int tabIndex, int visibleTabCount);
        
        #endregion

        #region Events
        
        /// <summary>
        /// Fired when tab sizes are recalculated
        /// </summary>
        event EventHandler<TabSizingChangedEventArgs> SizingChanged;
        
        /// <summary>
        /// Fired when overflow state changes
        /// </summary>
        event EventHandler<TabOverflowEventArgs> OverflowStateChanged;
        
        #endregion
    }

    /// <summary>
    /// Tab overflow handling strategies
    /// </summary>
    public enum TabOverflowStrategy
    {
        /// <summary>
        /// Compress tabs to fit available space
        /// </summary>
        Compress,
        
        /// <summary>
        /// Show scroll buttons for navigation
        /// </summary>
        Scroll,
        
        /// <summary>
        /// Hide least important tabs
        /// </summary>
        Hide,
        
        /// <summary>
        /// Create a dropdown menu for overflow tabs
        /// </summary>
        Dropdown
    }

    /// <summary>
    /// Event arguments for tab sizing changes
    /// </summary>
    public class TabSizingChangedEventArgs : EventArgs
    {
        public IReadOnlyList<double> TabWidths { get; }
        public double TotalWidth { get; }
        public bool IsCompressed { get; }
        public TabOverflowStrategy OverflowStrategy { get; }

        public TabSizingChangedEventArgs(IReadOnlyList<double> tabWidths, double totalWidth, 
            bool isCompressed, TabOverflowStrategy overflowStrategy)
        {
            TabWidths = tabWidths;
            TotalWidth = totalWidth;
            IsCompressed = isCompressed;
            OverflowStrategy = overflowStrategy;
        }
    }

    /// <summary>
    /// Event arguments for tab overflow state changes
    /// </summary>
    public class TabOverflowEventArgs : EventArgs
    {
        public bool HasOverflow { get; }
        public int OverflowCount { get; }
        public TabOverflowStrategy Strategy { get; }

        public TabOverflowEventArgs(bool hasOverflow, int overflowCount, TabOverflowStrategy strategy)
        {
            HasOverflow = hasOverflow;
            OverflowCount = overflowCount;
            Strategy = strategy;
        }
    }

    /// <summary>
    /// Chrome-style sizing constants
    /// </summary>
    public static class ChromeSizingConstants
    {
        public const double MinTabWidth = 40.0;
        public const double MaxTabWidth = 240.0;
        public const double PreferredTabWidth = 180.0;
        public const double PinnedTabWidth = 40.0;
        public const double TabSpacing = 2.0;
        public const double CloseButtonWidth = 20.0;
        public const double TabPadding = 16.0; // 8px on each side
        public const double NewTabButtonWidth = 32.0;
        public const double OverflowButtonWidth = 32.0;
        public const double ScrollButtonWidth = 24.0;
        public const double TabHeaderHeight = 36.0;
    }
} 