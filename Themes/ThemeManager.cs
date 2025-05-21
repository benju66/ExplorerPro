// Themes/ThemeManager.cs - Enhanced with improved theme handling
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
        
        /// <summary>
        /// Event that fires before theme resources are refreshed
        /// </summary>
        public event EventHandler<AppTheme> ThemeRefreshing;
        
        /// <summary>
        /// Event that fires after theme resources are refreshed
        /// </summary>
        public event EventHandler<AppTheme> ThemeRefreshed;
        
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
        
        /// <summary>
        /// Gets a dictionary of default fallback resources for when resources aren't found
        /// </summary>
        public Dictionary<string, object> DefaultResources { get; private set; }
        
        #endregion
        
        #region Private Fields
        
        private readonly SettingsManager _settingsManager;
        private bool _isInitialized = false;
        private readonly Dictionary<string, ResourceDictionary> _themeCache = new Dictionary<string, ResourceDictionary>();
        
        // Theme-specific resource cache
        private Dictionary<string, object> _darkThemeResources = new Dictionary<string, object>();
        private Dictionary<string, object> _lightThemeResources = new Dictionary<string, object>();
        
        // List of windows to notify of theme changes
        private readonly List<WeakReference<Window>> _registeredWindows = new List<WeakReference<Window>>();
        
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
            
            // Initialize default resources
            InitializeDefaultResources();
                
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
                
                // Register for application exit to clean up resources
                Application.Current.Exit += (s, e) => Cleanup();
                
                // Monitor application windows
                Application.Current.Activated += Application_Activated;
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
            
            // Try to get default brush from our default resources
            if (DefaultResources.TryGetValue(resourceKey, out var defaultBrush) && 
                defaultBrush is SolidColorBrush defaultSolidBrush)
            {
                return defaultSolidBrush;
            }
            
            // Return a theme-appropriate default brush if not found
            return new SolidColorBrush(IsDarkMode ? Colors.Gray : Colors.DarkGray);
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
                
                // Try to get from cached theme resources
                var themeCache = IsDarkMode ? _darkThemeResources : _lightThemeResources;
                if (themeCache.TryGetValue(resourceKey, out var cachedResource) && 
                    cachedResource is T typedResource)
                {
                    return typedResource;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting theme resource '{resourceKey}': {ex.Message}");
            }
            
            // Look in default resources
            if (DefaultResources.TryGetValue(resourceKey, out var defaultResource) && 
                defaultResource is T typedDefault)
            {
                return typedDefault;
            }
            
            return default;
        }
        
        /// <summary>
        /// Gets a specifically themed resource (from light or dark theme regardless of current setting)
        /// </summary>
        /// <typeparam name="T">Type of resource to get</typeparam>
        /// <param name="resourceKey">Key of the resource</param>
        /// <param name="theme">Theme to use</param>
        /// <returns>The resource or default if not found</returns>
        public T GetThemedResource<T>(string resourceKey, AppTheme theme) where T : class
        {
            try
            {
                // Try to get from cached theme resources for the specific theme
                var themeCache = theme == AppTheme.Dark ? _darkThemeResources : _lightThemeResources;
                if (themeCache.TryGetValue(resourceKey, out var cachedResource) && 
                    cachedResource is T typedResource)
                {
                    return typedResource;
                }
                
                // Try to load the resource from the theme dictionary if not cached
                if (themeCache.Count == 0)
                {
                    // No resources cached yet, load the theme dictionary
                    var themeDictionary = LoadThemeDictionary(theme);
                    if (themeDictionary != null && themeDictionary.Contains(resourceKey))
                    {
                        return themeDictionary[resourceKey] as T;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting themed resource '{resourceKey}': {ex.Message}");
            }
            
            // Look in default resources
            if (DefaultResources.TryGetValue(resourceKey, out var defaultResource) && 
                defaultResource is T typedDefault)
            {
                return typedDefault;
            }
            
            return default;
        }
        
        /// <summary>
        /// Refreshes theme resources without changing the theme
        /// </summary>
        public void RefreshThemeResources()
        {
            try
            {
                // Notify before refreshing
                ThemeRefreshing?.Invoke(this, CurrentTheme);
                
                // Re-apply current theme
                ApplyTheme(CurrentTheme);
                
                // Notify after refreshing
                ThemeRefreshed?.Invoke(this, CurrentTheme);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing theme resources: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Registers a window to be notified of theme changes
        /// </summary>
        /// <param name="window">Window to register</param>
        public void RegisterWindow(Window window)
        {
            if (window == null)
                return;
                
            // Check if window is already registered
            foreach (var weakRef in _registeredWindows)
            {
                if (weakRef.TryGetTarget(out var existingWindow) && existingWindow == window)
                {
                    return; // Already registered
                }
            }
            
            // Add window to registration list
            _registeredWindows.Add(new WeakReference<Window>(window));
            
            // Force window to refresh its theme immediately
            RefreshWindowTheme(window);
            
            Console.WriteLine($"Window registered for theme updates: {window.GetType().Name}");
        }
        
        /// <summary>
        /// Creates or gets a fallback resource with appropriate theme values
        /// </summary>
        /// <param name="resourceKey">The resource key</param>
        /// <returns>A theme-appropriate resource</returns>
        public object GetFallbackResource(string resourceKey)
        {
            // Return existing fallback if already created
            if (DefaultResources.TryGetValue(resourceKey, out var existingResource))
            {
                return existingResource;
            }
            
            // Create appropriate fallback based on resource key pattern
            if (resourceKey.Contains("Background"))
            {
                var brush = new SolidColorBrush(IsDarkMode ? Colors.Black : Colors.White);
                DefaultResources[resourceKey] = brush;
                return brush;
            }
            else if (resourceKey.Contains("Foreground") || resourceKey.Contains("Text"))
            {
                var brush = new SolidColorBrush(IsDarkMode ? Colors.LightGray : Colors.Black);
                DefaultResources[resourceKey] = brush;
                return brush;
            }
            else if (resourceKey.Contains("Border"))
            {
                var brush = new SolidColorBrush(IsDarkMode ? Colors.DarkGray : Colors.LightGray);
                DefaultResources[resourceKey] = brush;
                return brush;
            }
            
            // Return null for unknown resource types
            return null;
        }
        
        #endregion
        
        #region Private Methods
        
        /// <summary>
        /// Initializes default fallback resources
        /// </summary>
        private void InitializeDefaultResources()
        {
            DefaultResources = new Dictionary<string, object>();
            
            // Add standard fallbacks for the dark theme
            DefaultResources["TextColor_Dark"] = new SolidColorBrush(Colors.LightGray);
            DefaultResources["WindowBackground_Dark"] = new SolidColorBrush(Colors.Black);
            DefaultResources["BackgroundColor_Dark"] = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            DefaultResources["BorderColor_Dark"] = new SolidColorBrush(Colors.DarkGray);
            
            // Add standard fallbacks for the light theme
            DefaultResources["TextColor_Light"] = new SolidColorBrush(Colors.Black);
            DefaultResources["WindowBackground_Light"] = new SolidColorBrush(Colors.White);
            DefaultResources["BackgroundColor_Light"] = new SolidColorBrush(Color.FromRgb(245, 245, 245));
            DefaultResources["BorderColor_Light"] = new SolidColorBrush(Colors.LightGray);
        }
        
        /// <summary>
        /// Handles Application.Activated event to track new windows
        /// </summary>
        private void Application_Activated(object sender, EventArgs e)
        {
            try
            {
                // Look for new windows to register
                foreach (Window window in Application.Current.Windows)
                {
                    bool isRegistered = false;
                    
                    // Check if already registered
                    foreach (var weakRef in _registeredWindows)
                    {
                        if (weakRef.TryGetTarget(out var registeredWindow) && registeredWindow == window)
                        {
                            isRegistered = true;
                            break;
                        }
                    }
                    
                    // Register if not already registered
                    if (!isRegistered)
                    {
                        RegisterWindow(window);
                    }
                }
                
                // Clean up dead references
                CleanupDeadWindowReferences();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in Application_Activated: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Removes references to closed windows
        /// </summary>
        private void CleanupDeadWindowReferences()
        {
            _registeredWindows.RemoveAll(weakRef => !weakRef.TryGetTarget(out _));
        }
        
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
                        
                    // Cache dark theme resources
                    CacheThemeResources(_darkThemeResources, AppTheme.Dark);
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(GetOrCreateResourceDictionary(
                        "/ExplorerPro;component/Themes/LightTheme.xaml", "light"));
                        
                    // Cache light theme resources
                    CacheThemeResources(_lightThemeResources, AppTheme.Light);
                }
                
                // Notify all registered windows to refresh their UI
                NotifyWindowsOfThemeChange();
                
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
        /// Caches resources from a theme for faster lookup
        /// </summary>
        private void CacheThemeResources(Dictionary<string, object> cache, AppTheme theme)
        {
            try
            {
                // Clear existing cache
                cache.Clear();
                
                // Get the theme dictionary
                var themeDictionary = LoadThemeDictionary(theme);
                if (themeDictionary == null)
                    return;
                    
                // Cache all resources
                foreach (var key in themeDictionary.Keys)
                {
                    if (key is string stringKey)
                    {
                        cache[stringKey] = themeDictionary[key];
                    }
                }
                
                Console.WriteLine($"Cached {cache.Count} resources for {theme} theme");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error caching theme resources: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads a theme's resource dictionary
        /// </summary>
        private ResourceDictionary LoadThemeDictionary(AppTheme theme)
        {
            string uri = theme == AppTheme.Dark 
                ? "/ExplorerPro;component/Themes/DarkTheme.xaml" 
                : "/ExplorerPro;component/Themes/LightTheme.xaml";
                
            string cacheKey = theme == AppTheme.Dark ? "dark" : "light";
            
            return GetOrCreateResourceDictionary(uri, cacheKey);
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
            
            Console.WriteLine("Loaded base theme as fallback");
        }
        
        /// <summary>
        /// Notifies all registered windows of theme changes
        /// </summary>
        private void NotifyWindowsOfThemeChange()
        {
            // Clean up dead references first
            CleanupDeadWindowReferences();
            
            // Refresh each registered window
            foreach (var weakRef in _registeredWindows)
            {
                if (weakRef.TryGetTarget(out var window))
                {
                    RefreshWindowTheme(window);
                }
            }
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
                else
                {
                    // Try to find and invoke RefreshThemeElements on other window types
                    var refreshMethod = window.GetType().GetMethod("RefreshThemeElements");
                    refreshMethod?.Invoke(window, null);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing window theme: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Cleans up resources when the application exits
        /// </summary>
        private void Cleanup()
        {
            try
            {
                // Clear caches
                _themeCache.Clear();
                _darkThemeResources.Clear();
                _lightThemeResources.Clear();
                DefaultResources.Clear();
                _registeredWindows.Clear();
                
                Console.WriteLine("ThemeManager resources cleaned up");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during ThemeManager cleanup: {ex.Message}");
            }
        }
        
        #endregion
    }
}