using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls.Interfaces;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Implementation of tab visual management.
    /// Handles styling, themes, and visual states with enterprise customization.
    /// </summary>
    public class TabVisualManager : ITabVisualManager
    {
        #region Private Fields
        
        private readonly ILogger<TabVisualManager> _logger;
        private bool _disposed;
        
        // Theme brushes
        private SolidColorBrush _activeBrush;
        private SolidColorBrush _inactiveBrush;
        private SolidColorBrush _hoverBrush;
        private SolidColorBrush _pinnedBrush;
        
        #endregion

        #region Constructor
        
        public TabVisualManager(ILogger<TabVisualManager> logger = null)
        {
            _logger = logger;
            
            // Initialize with defaults
            CurrentTheme = TabTheme.Light;
            UseSmoothTransitions = true;
            ShowCloseButtons = true;
            ShowTabIcons = true;
            
            InitializeThemeBrushes();
            
            _logger?.LogDebug("TabVisualManager initialized with {Theme} theme", CurrentTheme);
        }
        
        #endregion

        #region ITabVisualManager Implementation
        
        public TabTheme CurrentTheme { get; set; }
        public bool UseSmoothTransitions { get; set; }
        public bool ShowCloseButtons { get; set; }
        public bool ShowTabIcons { get; set; }

        public event EventHandler<TabThemeChangedEventArgs> ThemeChanged;
        public event EventHandler<TabVisualStateChangedEventArgs> VisualStateChanged;

        public void ApplyTabStyling(TabItem tabItem, TabModel tabModel)
        {
            ThrowIfDisposed();
            
            if (tabItem == null || tabModel == null)
                return;
                
            // Apply base styling
            ApplyBaseStyling(tabItem);
            
            // Apply state-specific styling
            UpdateSelectionState(tabItem, tabModel.IsActive);
            UpdatePinnedState(tabItem, tabModel.IsPinned);
            
            // Apply custom color if set
            if (tabModel.HasCustomColor)
                ApplyTabColor(tabItem, tabModel.CustomColor);
                
            // Update close button
            UpdateCloseButtonVisibility(tabItem, tabModel);
            
            // Update icon
            UpdateTabIcon(tabItem, tabModel);
            
            _logger?.LogTrace("Applied styling to tab '{Title}'", tabModel.Title);
        }

        public void UpdateSelectionState(TabItem tabItem, bool isSelected)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            if (isSelected)
            {
                tabItem.Background = _activeBrush;
                tabItem.BorderBrush = GetThemeBrush("ActiveBorder");
                Panel.SetZIndex(tabItem, 10);
                
                // Apply modern active state visuals
                ApplyActiveTabStyling(tabItem);
            }
            else
            {
                tabItem.Background = _inactiveBrush;
                tabItem.BorderBrush = GetThemeBrush("InactiveBorder");
                Panel.SetZIndex(tabItem, 0);
                
                // Apply modern inactive state visuals
                ApplyInactiveTabStyling(tabItem);
            }
            
            FireVisualStateChanged(tabItem, isSelected ? TabVisualState.Selected : TabVisualState.Normal);
        }
        
        /// <summary>
        /// Applies modern active tab styling with enhanced visual hierarchy
        /// </summary>
        private void ApplyActiveTabStyling(TabItem tabItem)
        {
            // Enhanced shadow for elevation
            ApplyDropShadow(tabItem, 0.25, 3);
            
            // Subtle accent highlighting
            if (tabItem.Template?.FindName("AccentBorder", tabItem) is Border accentBorder)
            {
                accentBorder.Opacity = 1.0;
            }
        }
        
        /// <summary>
        /// Applies modern inactive tab styling for clean hierarchy
        /// </summary>
        private void ApplyInactiveTabStyling(TabItem tabItem)
        {
            // Reduced shadow for depth without distraction
            ApplyDropShadow(tabItem, 0.15, 1);
            
            // Hide accent for inactive state
            if (tabItem.Template?.FindName("AccentBorder", tabItem) is Border accentBorder)
            {
                accentBorder.Opacity = 0.0;
            }
        }

        public void UpdateHoverState(TabItem tabItem, bool isHovered)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            if (isHovered && !tabItem.IsSelected)
            {
                tabItem.Background = _hoverBrush;
                ApplyDropShadow(tabItem, 0.3, 2);
            }
            else if (!tabItem.IsSelected)
            {
                tabItem.Background = _inactiveBrush;
                RemoveDropShadow(tabItem);
            }
            
            FireVisualStateChanged(tabItem, isHovered ? TabVisualState.Hovered : TabVisualState.Normal);
        }

        public void UpdateFocusState(TabItem tabItem, bool hasFocus)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            if (hasFocus)
            {
                ApplyGlowEffect(tabItem, GetThemeColor("FocusGlow"), 8);
            }
            else
            {
                RemoveGlowEffect(tabItem);
            }
            
            FireVisualStateChanged(tabItem, hasFocus ? TabVisualState.Focused : TabVisualState.Normal);
        }

        public void UpdateDragState(TabItem tabItem, bool isDragging)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            if (isDragging)
            {
                tabItem.Opacity = 0.7;
                ApplyDropShadow(tabItem, 0.5, 5);
            }
            else
            {
                tabItem.Opacity = 1.0;
                RemoveDropShadow(tabItem);
            }
            
            FireVisualStateChanged(tabItem, isDragging ? TabVisualState.Dragging : TabVisualState.Normal);
        }

        public void ApplyTabColor(TabItem tabItem, Color color)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            var colorBrush = new SolidColorBrush(color);
            
            // Apply color to accent border or background highlight
            if (tabItem.Template?.FindName("ColorAccent", tabItem) is Border colorAccent)
            {
                colorAccent.Background = colorBrush;
                colorAccent.Visibility = Visibility.Visible;
            }
            else
            {
                // Fallback: apply subtle color tint to background
                var currentBrush = tabItem.Background as SolidColorBrush;
                if (currentBrush != null)
                {
                    var blendedColor = BlendColors(currentBrush.Color, color, 0.2);
                    tabItem.Background = new SolidColorBrush(blendedColor);
                }
            }
            
            _logger?.LogTrace("Applied custom color to tab");
        }

        public void ClearTabColor(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            // Hide color accent
            if (tabItem.Template?.FindName("ColorAccent", tabItem) is Border colorAccent)
            {
                colorAccent.Visibility = Visibility.Collapsed;
            }
            
            // Restore default background
            tabItem.Background = tabItem.IsSelected ? _activeBrush : _inactiveBrush;
            
            _logger?.LogTrace("Cleared custom color from tab");
        }

        public Color GetEffectiveTabColor(TabModel tabModel)
        {
            ThrowIfDisposed();
            
            if (tabModel.HasCustomColor)
                return tabModel.CustomColor;
                
            return tabModel.IsActive ? GetThemeColor("ActiveTab") : GetThemeColor("InactiveTab");
        }

        public void ApplyThemeColors(TabControl tabControl)
        {
            ThrowIfDisposed();
            
            if (tabControl == null)
                return;
                
            InitializeThemeBrushes();
            
            // Apply theme to all tabs
            foreach (TabItem tabItem in tabControl.Items)
            {
                var tabModel = GetTabModelFromItem(tabItem);
                if (tabModel != null)
                {
                    ApplyTabStyling(tabItem, tabModel);
                }
            }
            
            _logger?.LogDebug("Applied {Theme} theme colors to {Count} tabs", CurrentTheme, tabControl.Items.Count);
        }

        public void UpdatePinnedState(TabItem tabItem, bool isPinned)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            if (isPinned)
            {
                ApplyPinnedStyling(tabItem);
            }
            else
            {
                ApplyUnpinnedStyling(tabItem);
            }
            
            FireVisualStateChanged(tabItem, isPinned ? TabVisualState.Pinned : TabVisualState.Normal);
        }

        public void ApplyPinnedStyling(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            tabItem.Width = TabDimensions.PinnedWidth;
            tabItem.MinWidth = TabDimensions.PinnedWidth;
            tabItem.MaxWidth = TabDimensions.PinnedWidth;
            
            // Show only icon, hide text
            if (tabItem.Template?.FindName("HeaderPresenter", tabItem) is ContentPresenter headerPresenter)
            {
                headerPresenter.Visibility = Visibility.Collapsed;
            }
            
            if (tabItem.Template?.FindName("TabIcon", tabItem) is FrameworkElement iconElement)
            {
                iconElement.Visibility = Visibility.Visible;
            }
        }

        public void ApplyUnpinnedStyling(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            tabItem.ClearValue(TabItem.WidthProperty);
            tabItem.ClearValue(TabItem.MinWidthProperty);
            tabItem.ClearValue(TabItem.MaxWidthProperty);
            
            // Show text, adjust icon visibility
            if (tabItem.Template?.FindName("HeaderPresenter", tabItem) is ContentPresenter headerPresenter)
            {
                headerPresenter.Visibility = Visibility.Visible;
            }
            
            if (tabItem.Template?.FindName("TabIcon", tabItem) is FrameworkElement iconElement)
            {
                iconElement.Visibility = ShowTabIcons ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void ApplyDropShadow(TabItem tabItem, double opacity, double depth)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            var dropShadow = new DropShadowEffect
            {
                Color = Colors.Black,
                Direction = 270,
                ShadowDepth = depth,
                BlurRadius = depth * 2,
                Opacity = opacity
            };
            
            tabItem.Effect = dropShadow;
        }

        public void RemoveDropShadow(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            tabItem.Effect = null;
        }

        public void ApplyGlowEffect(TabItem tabItem, Color glowColor, double radius)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            var glowEffect = new DropShadowEffect
            {
                Color = glowColor,
                Direction = 0,
                ShadowDepth = 0,
                BlurRadius = radius,
                Opacity = 0.8
            };
            
            tabItem.Effect = glowEffect;
        }

        public void RemoveGlowEffect(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            tabItem.Effect = null;
        }

        public void ApplyHighlight(TabItem tabItem, Color highlightColor)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            tabItem.BorderBrush = new SolidColorBrush(highlightColor);
            tabItem.BorderThickness = new Thickness(2);
        }

        public void RemoveHighlight(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            tabItem.ClearValue(TabItem.BorderBrushProperty);
            tabItem.ClearValue(TabItem.BorderThicknessProperty);
        }

        public void UpdateCloseButtonVisibility(TabItem tabItem, TabModel tabModel)
        {
            ThrowIfDisposed();
            
            if (tabItem == null || tabModel == null)
                return;
                
            var shouldShow = ShowCloseButtons && !tabModel.IsPinned;
            
            if (tabItem.Template?.FindName("CloseButton", tabItem) is Button closeButton)
            {
                closeButton.Visibility = shouldShow ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public void StyleCloseButton(TabItem tabItem, TabModel tabModel)
        {
            ThrowIfDisposed();
            
            if (tabItem == null || tabModel == null)
                return;
                
            if (tabItem.Template?.FindName("CloseButton", tabItem) is Button closeButton)
            {
                closeButton.Style = GetThemeStyle("CloseButtonStyle");
            }
        }

        public void HandleCloseButtonHover(TabItem tabItem, bool isHovered)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            if (tabItem.Template?.FindName("CloseButton", tabItem) is Button closeButton)
            {
                closeButton.Background = isHovered ? 
                    GetThemeBrush("CloseButtonHover") : 
                    Brushes.Transparent;
            }
        }

        public void UpdateTabIcon(TabItem tabItem, TabModel tabModel)
        {
            ThrowIfDisposed();
            
            if (tabItem == null || tabModel == null)
                return;
                
            if (tabItem.Template?.FindName("TabIcon", tabItem) is Image iconImage)
            {
                if (!string.IsNullOrEmpty(tabModel.IconPath))
                {
                    // Load icon from path
                    // Implementation would depend on specific icon loading strategy
                    iconImage.Visibility = Visibility.Visible;
                }
                else
                {
                    iconImage.Visibility = Visibility.Collapsed;
                }
            }
        }

        public void ApplyIconStyling(TabItem tabItem, ImageSource iconSource)
        {
            ThrowIfDisposed();
            
            if (tabItem == null || iconSource == null)
                return;
                
            if (tabItem.Template?.FindName("TabIcon", tabItem) is Image iconImage)
            {
                iconImage.Source = iconSource;
                iconImage.Width = 16;
                iconImage.Height = 16;
                iconImage.Margin = new Thickness(4, 0, 8, 0);
            }
        }

        public void UpdateIconForState(TabItem tabItem, TabState state)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            // Update icon based on tab state (loading spinner, error icon, etc.)
            // Implementation would depend on available icons and requirements
        }

        public void ApplyAccessibilityFeatures(TabItem tabItem, TabModel tabModel)
        {
            ThrowIfDisposed();
            
            if (tabItem == null || tabModel == null)
                return;
                
            UpdateScreenReaderProperties(tabItem, tabModel);
            
            // Ensure proper keyboard navigation
            tabItem.IsTabStop = true;
            tabItem.Focusable = true;
        }

        public void UpdateScreenReaderProperties(TabItem tabItem, TabModel tabModel)
        {
            ThrowIfDisposed();
            
            if (tabItem == null || tabModel == null)
                return;
                
            System.Windows.Automation.AutomationProperties.SetName(tabItem, tabModel.Title);
            System.Windows.Automation.AutomationProperties.SetHelpText(tabItem, 
                $"Tab: {tabModel.Title}" + (tabModel.IsPinned ? " (Pinned)" : ""));
        }

        public void ApplyHighContrastStyling(TabItem tabItem)
        {
            ThrowIfDisposed();
            
            if (tabItem == null)
                return;
                
            // Apply high contrast colors
            tabItem.Background = SystemColors.WindowBrush;
            tabItem.Foreground = SystemColors.WindowTextBrush;
            tabItem.BorderBrush = SystemColors.ActiveBorderBrush;
            tabItem.BorderThickness = new Thickness(2);
        }
        
        #endregion

        #region Private Helper Methods
        
        private void InitializeThemeBrushes()
        {
            switch (CurrentTheme)
            {
                case TabTheme.Light:
                    _activeBrush = new SolidColorBrush(Colors.White);
                    _inactiveBrush = new SolidColorBrush(Color.FromRgb(245, 245, 245));
                    _hoverBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230));
                    _pinnedBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
                    break;
                    
                case TabTheme.Dark:
                    _activeBrush = new SolidColorBrush(Color.FromRgb(60, 60, 60));
                    _inactiveBrush = new SolidColorBrush(Color.FromRgb(45, 45, 45));
                    _hoverBrush = new SolidColorBrush(Color.FromRgb(70, 70, 70));
                    _pinnedBrush = new SolidColorBrush(Color.FromRgb(50, 50, 50));
                    break;
                    
                default:
                    InitializeLightTheme();
                    break;
            }
        }

        private void InitializeLightTheme()
        {
            _activeBrush = new SolidColorBrush(Colors.White);
            _inactiveBrush = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            _hoverBrush = new SolidColorBrush(Color.FromRgb(230, 230, 230));
            _pinnedBrush = new SolidColorBrush(Color.FromRgb(240, 240, 240));
        }

        private void ApplyBaseStyling(TabItem tabItem)
        {
            tabItem.BorderThickness = new Thickness(1, 1, 1, 0);
            tabItem.Margin = new Thickness(2, 3, 2, -1);
            tabItem.Padding = new Thickness(8, 6, 24, 6);
        }

        private SolidColorBrush GetThemeBrush(string key)
        {
            // This would typically lookup brushes from theme resources
            return key switch
            {
                "ActiveBorder" => new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                "InactiveBorder" => new SolidColorBrush(Color.FromRgb(208, 215, 222)),
                "CloseButtonHover" => new SolidColorBrush(Color.FromRgb(232, 17, 35)),
                _ => Brushes.Transparent
            };
        }

        private Color GetThemeColor(string key)
        {
            return key switch
            {
                "ActiveTab" => Colors.White,
                "InactiveTab" => Color.FromRgb(245, 245, 245),
                "FocusGlow" => Color.FromRgb(0, 120, 212),
                _ => Colors.Transparent
            };
        }

        private Style GetThemeStyle(string key)
        {
            // This would typically lookup styles from theme resources
            return null;
        }

        private TabModel GetTabModelFromItem(TabItem tabItem)
        {
            return tabItem?.DataContext as TabModel ?? tabItem?.Tag as TabModel;
        }

        private Color BlendColors(Color color1, Color color2, double ratio)
        {
            var r = (byte)(color1.R * (1 - ratio) + color2.R * ratio);
            var g = (byte)(color1.G * (1 - ratio) + color2.G * ratio);
            var b = (byte)(color1.B * (1 - ratio) + color2.B * ratio);
            var a = (byte)(color1.A * (1 - ratio) + color2.A * ratio);
            
            return Color.FromArgb(a, r, g, b);
        }

        private void FireVisualStateChanged(TabItem tabItem, TabVisualState newState)
        {
            var args = new TabVisualStateChangedEventArgs(tabItem, TabVisualState.Normal, newState);
            VisualStateChanged?.Invoke(this, args);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TabVisualManager));
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
                _logger?.LogDebug("TabVisualManager disposed");
            }
        }
        
        #endregion
    }
} 