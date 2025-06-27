using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ExplorerPro.Models;

namespace ExplorerPro.UI.Controls.Interfaces
{
    /// <summary>
    /// Interface for managing tab visual appearance and styling.
    /// Provides modern, theme-aware visual management with enterprise customization.
    /// </summary>
    public interface ITabVisualManager : IDisposable
    {
        #region Properties
        
        /// <summary>
        /// Current theme being used
        /// </summary>
        TabTheme CurrentTheme { get; set; }
        
        /// <summary>
        /// Whether to use smooth visual transitions
        /// </summary>
        bool UseSmoothTransitions { get; set; }
        
        /// <summary>
        /// Whether to show close buttons on tabs
        /// </summary>
        bool ShowCloseButtons { get; set; }
        
        /// <summary>
        /// Whether to show icons on tabs
        /// </summary>
        bool ShowTabIcons { get; set; }
        
        #endregion

        #region Core Visual Operations
        
        /// <summary>
        /// Applies visual styling to a tab item
        /// </summary>
        void ApplyTabStyling(TabItem tabItem, TabModel tabModel);
        
        /// <summary>
        /// Updates visual state for tab selection
        /// </summary>
        void UpdateSelectionState(TabItem tabItem, bool isSelected);
        
        /// <summary>
        /// Updates visual state for tab hover
        /// </summary>
        void UpdateHoverState(TabItem tabItem, bool isHovered);
        
        /// <summary>
        /// Updates visual state for tab focus
        /// </summary>
        void UpdateFocusState(TabItem tabItem, bool hasFocus);
        
        /// <summary>
        /// Updates visual state for tab drag
        /// </summary>
        void UpdateDragState(TabItem tabItem, bool isDragging);
        
        #endregion

        #region Color Management
        
        /// <summary>
        /// Applies custom color to a tab
        /// </summary>
        void ApplyTabColor(TabItem tabItem, Color color);
        
        /// <summary>
        /// Clears custom color from a tab
        /// </summary>
        void ClearTabColor(TabItem tabItem);
        
        /// <summary>
        /// Gets the effective color for a tab
        /// </summary>
        Color GetEffectiveTabColor(TabModel tabModel);
        
        /// <summary>
        /// Applies theme colors to all tabs
        /// </summary>
        void ApplyThemeColors(TabControl tabControl);
        
        #endregion

        #region Pin State Management
        
        /// <summary>
        /// Updates visual appearance for pinned state
        /// </summary>
        void UpdatePinnedState(TabItem tabItem, bool isPinned);
        
        /// <summary>
        /// Applies pinned tab styling
        /// </summary>
        void ApplyPinnedStyling(TabItem tabItem);
        
        /// <summary>
        /// Applies unpinned tab styling
        /// </summary>
        void ApplyUnpinnedStyling(TabItem tabItem);
        
        #endregion

        #region Visual Effects
        
        /// <summary>
        /// Applies drop shadow effect
        /// </summary>
        void ApplyDropShadow(TabItem tabItem, double opacity, double depth);
        
        /// <summary>
        /// Removes drop shadow effect
        /// </summary>
        void RemoveDropShadow(TabItem tabItem);
        
        /// <summary>
        /// Applies glow effect
        /// </summary>
        void ApplyGlowEffect(TabItem tabItem, Color glowColor, double radius);
        
        /// <summary>
        /// Removes glow effect
        /// </summary>
        void RemoveGlowEffect(TabItem tabItem);
        
        /// <summary>
        /// Applies highlight effect
        /// </summary>
        void ApplyHighlight(TabItem tabItem, Color highlightColor);
        
        /// <summary>
        /// Removes highlight effect
        /// </summary>
        void RemoveHighlight(TabItem tabItem);
        
        #endregion

        #region Close Button Management
        
        /// <summary>
        /// Updates close button visibility
        /// </summary>
        void UpdateCloseButtonVisibility(TabItem tabItem, TabModel tabModel);
        
        /// <summary>
        /// Styles the close button for a tab
        /// </summary>
        void StyleCloseButton(TabItem tabItem, TabModel tabModel);
        
        /// <summary>
        /// Handles close button hover effects
        /// </summary>
        void HandleCloseButtonHover(TabItem tabItem, bool isHovered);
        
        #endregion

        #region Icon Management
        
        /// <summary>
        /// Updates tab icon
        /// </summary>
        void UpdateTabIcon(TabItem tabItem, TabModel tabModel);
        
        /// <summary>
        /// Applies icon styling
        /// </summary>
        void ApplyIconStyling(TabItem tabItem, ImageSource iconSource);
        
        /// <summary>
        /// Updates icon for tab state (loading, error, etc.)
        /// </summary>
        void UpdateIconForState(TabItem tabItem, TabState state);
        
        #endregion

        #region Accessibility
        
        /// <summary>
        /// Applies accessibility features to a tab
        /// </summary>
        void ApplyAccessibilityFeatures(TabItem tabItem, TabModel tabModel);
        
        /// <summary>
        /// Updates screen reader properties
        /// </summary>
        void UpdateScreenReaderProperties(TabItem tabItem, TabModel tabModel);
        
        /// <summary>
        /// Applies high contrast mode styling
        /// </summary>
        void ApplyHighContrastStyling(TabItem tabItem);
        
        #endregion

        #region Events
        
        /// <summary>
        /// Fired when theme changes
        /// </summary>
        event EventHandler<TabThemeChangedEventArgs> ThemeChanged;
        
        /// <summary>
        /// Fired when visual state changes
        /// </summary>
        event EventHandler<TabVisualStateChangedEventArgs> VisualStateChanged;
        
        #endregion
    }

    /// <summary>
    /// Tab visual themes
    /// </summary>
    public enum TabTheme
    {
        Light,
        Dark,
        HighContrast,
        Custom
    }

    /// <summary>
    /// Tab visual states
    /// </summary>
    public enum TabVisualState
    {
        Normal,
        Hovered,
        Selected,
        Focused,
        Dragging,
        Pinned,
        Loading,
        Error
    }

    /// <summary>
    /// Event arguments for theme changes
    /// </summary>
    public class TabThemeChangedEventArgs : EventArgs
    {
        public TabTheme OldTheme { get; }
        public TabTheme NewTheme { get; }

        public TabThemeChangedEventArgs(TabTheme oldTheme, TabTheme newTheme)
        {
            OldTheme = oldTheme;
            NewTheme = newTheme;
        }
    }

    /// <summary>
    /// Event arguments for visual state changes
    /// </summary>
    public class TabVisualStateChangedEventArgs : EventArgs
    {
        public TabItem TabItem { get; }
        public TabVisualState OldState { get; }
        public TabVisualState NewState { get; }

        public TabVisualStateChangedEventArgs(TabItem tabItem, TabVisualState oldState, TabVisualState newState)
        {
            TabItem = tabItem;
            OldState = oldState;
            NewState = newState;
        }
    }
} 