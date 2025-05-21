// Themes/ThemeManager.cs
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using ExplorerPro.Models;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.Themes
{
    /// <summary>
    /// Theme options available in the application
    /// </summary>
    public enum AppTheme
    {
        Light,
        Dark
    }
    
    /// <summary>
    /// Manages theme resources and settings application-wide
    /// </summary>
    public class ThemeManager
    {
        #region Singleton Implementation
        
        private static ThemeManager _instance;
        
        /// <summary>
        /// Gets the singleton instance of the ThemeManager
        /// </summary>
        public static ThemeManager Instance => _instance ??= new ThemeManager();
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Event that fires when the theme is changed
        /// </summary>
        public event EventHandler<AppTheme> ThemeChanged;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Gets the currently active theme
        /// </summary>
        public AppTheme CurrentTheme { get; private set; }
        
        /// <summary>
        /// Gets whether dark mode is currently active
        /// </summary>
        public bool IsDarkMode => CurrentTheme == AppTheme.Dark;
        
        #endregion
        
        #region Private Fields
        
        private readonly SettingsManager _settingsManager;
        private bool _isInitialized = false;
        private readonly Dictionary<string, ResourceDictionary> _themeCache = new Dictionary<string, ResourceDictionary>();
        
        #endregion
        
        #region Constructor
        
        /// <summary>
        /// Initializes a new instance of the ThemeManager class
        /// </summary>
        private ThemeManager()
        {
            // Get the application settings manager instance
            _settingsManager = App.Settings ?? new SettingsManager();
            
            // Load the current theme from settings
            string savedTheme = _settingsManager.GetSetting<string>("theme", "light");
            CurrentTheme = savedTheme.Equals("dark", StringComparison.OrdinalIgnoreCase) ? 
                AppTheme.Dark : AppTheme.Light;
                
            Console.WriteLine($"ThemeManager created with initial theme: {CurrentTheme}");
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Initializes theme resources for the application
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized)
                return;
                
            try
            {
                // Apply theme based on settings
                ApplyTheme(CurrentTheme);
                
                // Also sync with UI preferences (legacy setting)
                bool enableDarkMode = _settingsManager.GetSetting<bool>("ui_preferences.Enable Dark Mode", false);
                if ((enableDarkMode && CurrentTheme == AppTheme.Light) || 
                    (!enableDarkMode && CurrentTheme == AppTheme.Dark))
                {
                    // Sync the settings
                    _settingsManager.UpdateSetting("ui_preferences.Enable Dark Mode", IsDarkMode);
                }
                
                _isInitialized = true;
                Console.WriteLine("ThemeManager initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing ThemeManager: {ex.Message}");
                
                // Try to recover with basic theme setup
                try
                {
                    LoadBaseTheme();
                    _isInitialized = true;
                }
                catch
                {
                    // Critical failure in theming
                    Console.WriteLine("Critical failure in theme initialization");
                }
            }
        }
        
        /// <summary>
        /// Switches to the specified theme
        /// </summary>
        /// <param name="theme">Theme to apply</param>
        public void SwitchTheme(AppTheme theme)
        {
            if (theme == CurrentTheme)
                return;

            ApplyTheme(theme);
            CurrentTheme = theme;
            
            // Update settings
            _settingsManager.UpdateSetting("theme", theme.ToString().ToLower());
            _settingsManager.UpdateSetting("ui_preferences.Enable Dark Mode", theme == AppTheme.Dark);
            
            // Notify listeners of theme change
            ThemeChanged?.Invoke(this, theme);
            
            Console.WriteLine($"Theme switched to: {theme}");
        }
        
        /// <summary>
        /// Toggles between light and dark themes
        /// </summary>
        public void ToggleTheme()
        {
            SwitchTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }
        
        /// <summary>
        /// Gets a color brush from the current theme
        /// </summary>
        /// <param name="resourceKey">Resource key for the brush</param>
        /// <returns>The brush or a default brush if not found</returns>
        public SolidColorBrush GetThemeBrush(string resourceKey)
        {
            try
            {
                if (Application.Current.Resources[resourceKey] is SolidColorBrush brush)
                {
                    return brush;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting theme brush '{resourceKey}': {ex.Message}");
            }
            
            // Return a default brush if not found
            return new SolidColorBrush(Colors.Gray);
        }
        
        /// <summary>
        /// Gets a resource from the current theme by key
        /// </summary>
        /// <typeparam name="T">Type of resource to get</typeparam>
        /// <param name="resourceKey">Key of the resource</param>
        /// <returns>The resource or default if not found</returns>
        public T GetResource<T>(string resourceKey) where T : class
        {
            try
            {
                if (Application.Current.Resources.Contains(resourceKey))
                {
                    return Application.Current.Resources[resourceKey] as T;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting theme resource '{resourceKey}': {ex.Message}");
            }
            
            return default;
        }
        
        /// <summary>
        /// Refreshes theme resources without changing the theme
        /// </summary>
        public void RefreshThemeResources()
        {
            ApplyTheme(CurrentTheme);
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Applies theme resources to the application
        /// </summary>
        /// <param name="theme">Theme to apply</param>
        private void ApplyTheme(AppTheme theme)
        {
            try
            {
                var app = Application.Current;
                
                // Clear existing theme dictionaries
                var resourcesToRemove = new List<ResourceDictionary>();
                foreach (var dict in app.Resources.MergedDictionaries)
                {
                    if (dict.Source != null && 
                        (dict.Source.ToString().Contains("/Themes/") || 
                         dict.Source.ToString().Contains("/Styles/")))
                    {
                        resourcesToRemove.Add(dict);
                    }
                }
                
                // Remove the dictionaries (can't modify collection during iteration)
                foreach (var dict in resourcesToRemove)
                {
                    app.Resources.MergedDictionaries.Remove(dict);
                }
                
                // Add base resources that apply to all themes
                app.Resources.MergedDictionaries.Add(GetOrCreateResourceDictionary(
                    "/ExplorerPro;component/Themes/BaseTheme.xaml", "base"));
                
                // Add theme-specific resources
                if (theme == AppTheme.Dark)
                {
                    app.Resources.MergedDictionaries.Add(GetOrCreateResourceDictionary(
                        "/ExplorerPro;component/Themes/DarkTheme.xaml", "dark"));
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(GetOrCreateResourceDictionary(
                        "/ExplorerPro;component/Themes/LightTheme.xaml", "light"));
                }
                
                // Notify all windows to refresh their UI
                foreach (Window window in Application.Current.Windows)
                {
                    RefreshWindowTheme(window);
                }
                
                Console.WriteLine($"Theme resources applied: {theme}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying theme resources: {ex.Message}");
                
                // Try to recover with basic theme
                try
                {
                    LoadBaseTheme();
                }
                catch
                {
                    // Critical error in theming - nothing more we can do
                }
            }
        }
        
        /// <summary>
        /// Gets a cached resource dictionary or creates a new one
        /// </summary>
        private ResourceDictionary GetOrCreateResourceDictionary(string uri, string cacheKey)
        {
            if (_themeCache.TryGetValue(cacheKey, out var cachedDict))
            {
                return cachedDict;
            }
            
            var dict = new ResourceDictionary
            {
                Source = new Uri(uri, UriKind.Relative)
            };
            
            _themeCache[cacheKey] = dict;
            return dict;
        }
        
        /// <summary>
        /// Loads base theme resources as fallback for error recovery
        /// </summary>
        private void LoadBaseTheme()
        {
            var app = Application.Current;
            
            // Clear all theme dictionaries
            var toRemove = app.Resources.MergedDictionaries
                .Where(d => d.Source != null && d.Source.ToString().Contains("/Themes/"))
                .ToList();
                
            foreach (var dict in toRemove)
            {
                app.Resources.MergedDictionaries.Remove(dict);
            }
            
            // Add base resources
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/ExplorerPro;component/Themes/BaseTheme.xaml", UriKind.Relative)
            });
            
            // Add light theme as fallback
            app.Resources.MergedDictionaries.Add(new ResourceDictionary
            {
                Source = new Uri("/ExplorerPro;component/Themes/LightTheme.xaml", UriKind.Relative)
            });
        }
        
        /// <summary>
        /// Refreshes theme-specific elements in a window
        /// </summary>
        private void RefreshWindowTheme(Window window)
        {
            try
            {
                if (window is MainWindow mainWindow)
                {
                    mainWindow.RefreshThemeElements();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing window theme: {ex.Message}");
            }
        }
        
        #endregion
    }
}