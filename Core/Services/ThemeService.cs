using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ExplorerPro.Themes;
using ExplorerPro.Core;

namespace ExplorerPro.Core.Services
{
    /// <summary>
    /// Service interface for theme management operations in ExplorerPro.
    /// Extracted from MainWindow.xaml.cs to improve separation of concerns and testability.
    /// </summary>
    public interface IThemeService
    {
        /// <summary>
        /// Event fired when theme changes
        /// </summary>
        event EventHandler<ThemeChangedEventArgs> ThemeChanged;
        
        /// <summary>
        /// Gets the current theme
        /// </summary>
        AppTheme CurrentTheme { get; }
        
        /// <summary>
        /// Gets whether dark mode is active
        /// </summary>
        bool IsDarkMode { get; }
        
        /// <summary>
        /// Applies the specified theme
        /// </summary>
        void ApplyTheme(string theme);
        
        /// <summary>
        /// Toggles between light and dark theme
        /// </summary>
        void ToggleTheme();
        
        /// <summary>
        /// Gets a theme resource by key
        /// </summary>
        T GetResource<T>(string resourceKey) where T : class;
        
        /// <summary>
        /// Updates UI element with theme-appropriate styling
        /// </summary>
        void ApplyThemeToElement(FrameworkElement element, string elementType);
        
        /// <summary>
        /// Updates button content based on current theme
        /// </summary>
        void UpdateThemeToggleButton(Button toggleButton);
        
        /// <summary>
        /// Initializes theme event handling
        /// </summary>
        void InitializeThemeHandling();
        
        /// <summary>
        /// Cleans up theme resources
        /// </summary>
        void Cleanup();
    }

    /// <summary>
    /// Event arguments for theme changes
    /// </summary>
    public class ThemeChangedEventArgs : EventArgs
    {
        public AppTheme OldTheme { get; set; }
        public AppTheme NewTheme { get; set; }
        public bool IsDarkMode { get; set; }
    }

    /// <summary>
    /// Implementation of IThemeService for managing application themes
    /// </summary>
    public class ThemeService : IThemeService
    {
        private readonly ILogger<ThemeService> _logger;
        private readonly ISettingsService _settingsService;
        private AppTheme _currentTheme;
        private bool _isInitialized;

        public ThemeService(ILogger<ThemeService> logger, ISettingsService settingsService)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
            _currentTheme = AppTheme.Light; // Default
        }

        /// <summary>
        /// Event fired when theme changes
        /// </summary>
        public event EventHandler<ThemeChangedEventArgs> ThemeChanged;

        /// <summary>
        /// Gets the current theme
        /// </summary>
        public AppTheme CurrentTheme => _currentTheme;

        /// <summary>
        /// Gets whether dark mode is active
        /// </summary>
        public bool IsDarkMode => _currentTheme == AppTheme.Dark;

        /// <summary>
        /// Applies the specified theme
        /// </summary>
        public void ApplyTheme(string theme)
        {
            try
            {
                var oldTheme = _currentTheme;
                
                // Convert string to enum
                var themeEnum = theme?.ToLower() == "dark" ? 
                    AppTheme.Dark : AppTheme.Light;
                    
                if (themeEnum == _currentTheme)
                {
                    _logger.LogDebug($"Theme {theme} is already active");
                    return;
                }
                
                _currentTheme = themeEnum;
                
                // Apply theme through ThemeManager
                ThemeManager.Instance.SwitchTheme(themeEnum);
                
                // Save theme to settings
                _settingsService.UpdateSetting("theme", theme.ToLower());
                _settingsService.UpdateSetting("ui_preferences.Enable Dark Mode", IsDarkMode);
                
                // Fire theme changed event
                var eventArgs = new ThemeChangedEventArgs
                {
                    OldTheme = oldTheme,
                    NewTheme = _currentTheme,
                    IsDarkMode = IsDarkMode
                };
                ThemeChanged?.Invoke(this, eventArgs);
                
                _logger.LogInformation($"Applied theme: {theme}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error applying theme: {theme}");
                throw;
            }
        }
        
        /// <summary>
        /// Toggles between light and dark theme
        /// </summary>
        public void ToggleTheme()
        {
            var newTheme = IsDarkMode ? "light" : "dark";
            ApplyTheme(newTheme);
        }
        
        /// <summary>
        /// Gets a theme resource by key
        /// </summary>
        public T GetResource<T>(string resourceKey) where T : class
        {
            try
            {
                return ThemeManager.Instance.GetResource<T>(resourceKey);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Failed to get theme resource: {resourceKey}");
                return null;
            }
        }
        
        /// <summary>
        /// Updates UI element with theme-appropriate styling
        /// </summary>
        public void ApplyThemeToElement(FrameworkElement element, string elementType)
        {
            if (element == null) return;
            
            try
            {
                switch (elementType.ToLower())
                {
                    case "window":
                        ApplyWindowTheme(element);
                        break;
                    case "textblock":
                    case "label":
                        ApplyTextElementTheme(element);
                        break;
                    case "tabcontrol":
                        ApplyTabControlTheme(element);
                        break;
                    case "tabitem":
                        ApplyTabItemTheme(element);
                        break;
                    default:
                        ApplyGenericElementTheme(element);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error applying theme to element type: {elementType}");
            }
        }
        
        /// <summary>
        /// Updates theme toggle button content based on current theme
        /// </summary>
        public void UpdateThemeToggleButton(Button toggleButton)
        {
            if (toggleButton == null) return;
            
            try
            {
                toggleButton.ToolTip = IsDarkMode ? 
                    "Switch to Light Theme" : "Switch to Dark Theme";
                    
                // Update button appearance if needed
                ApplyThemeToElement(toggleButton, "button");
                
                _logger.LogDebug("Updated theme toggle button");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating theme toggle button");
            }
        }
        
        /// <summary>
        /// Initializes theme event handling
        /// </summary>
        public void InitializeThemeHandling()
        {
            if (_isInitialized) return;
            
            try
            {
                // Get current theme from settings
                var savedTheme = _settingsService.GetTheme();
                if (!string.IsNullOrEmpty(savedTheme))
                {
                    _currentTheme = savedTheme.ToLower() == "dark" ? AppTheme.Dark : AppTheme.Light;
                }
                
                // Subscribe to ThemeManager events if available
                // Note: ThemeManager event subscription could be added here if needed
                
                _isInitialized = true;
                _logger.LogInformation("Theme service initialized successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing theme handling");
                throw;
            }
        }
        
        /// <summary>
        /// Cleans up theme resources
        /// </summary>
        public void Cleanup()
        {
            try
            {
                // Unsubscribe from events
                ThemeChanged = null;
                
                _isInitialized = false;
                _logger.LogDebug("Theme service cleaned up");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during theme service cleanup");
            }
        }
        
        #region Private Theme Application Methods
        
        private void ApplyWindowTheme(FrameworkElement element)
        {
            if (element is Window window)
            {
                window.Background = GetResource<SolidColorBrush>("WindowBackground");
                window.Foreground = GetResource<SolidColorBrush>("TextColor");
            }
        }
        
        private void ApplyTextElementTheme(FrameworkElement element)
        {
            if (element is Control control)
            {
                control.Foreground = GetResource<SolidColorBrush>("TextColor");
            }
        }
        
        private void ApplyTabControlTheme(FrameworkElement element)
        {
            if (element is TabControl tabControl)
            {
                tabControl.Background = GetResource<SolidColorBrush>("TabControlBackground");
            }
        }
        
        private void ApplyTabItemTheme(FrameworkElement element)
        {
            if (element is TabItem tabItem)
            {
                tabItem.Background = GetResource<SolidColorBrush>("TabBackground");
                tabItem.Foreground = GetResource<SolidColorBrush>("TextColor");
                
                // Apply selected style if selected
                if (tabItem.IsSelected)
                {
                    tabItem.Background = GetResource<SolidColorBrush>("TabSelectedBackground");
                    tabItem.Foreground = GetResource<SolidColorBrush>("TabSelectedForeground");
                }
            }
        }
        
        private void ApplyGenericElementTheme(FrameworkElement element)
        {
            if (element is Control control)
            {
                // Apply general theme colors
                var background = GetResource<SolidColorBrush>("ControlBackground");
                var foreground = GetResource<SolidColorBrush>("TextColor");
                
                if (background != null) control.Background = background;
                if (foreground != null) control.Foreground = foreground;
            }
        }
        
        #endregion
    }
} 