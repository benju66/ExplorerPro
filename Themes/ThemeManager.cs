// Themes/ThemeManager.cs - Performance Optimized Version
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Threading;
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
    /// Performance optimized version with improved caching and batching
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
        
        // Enhanced resource caching
        private readonly Dictionary<string, object> _darkThemeResources = new Dictionary<string, object>();
        private readonly Dictionary<string, object> _lightThemeResources = new Dictionary<string, object>();
        private readonly Dictionary<string, object> _activeThemeResources = new Dictionary<string, object>();
        private bool _resourcesCached = false;
        
        // List of windows to notify of theme changes
        private readonly List<WeakReference<Window>> _registeredWindows = new List<WeakReference<Window>>();
        
        // Performance optimization
        private readonly DispatcherTimer _themeChangeDebouncer;
        private AppTheme? _pendingTheme;
        private bool _isApplyingTheme = false;
        
        // Commonly used resource keys for pre-caching
        private readonly string[] _commonResourceKeys = new[]
        {
            "WindowBackground", "BackgroundColor", "TextColor", "BorderColor",
            "ButtonBackground", "ButtonForeground", "ButtonBorder",
            "TreeViewBackground", "TreeViewBorder", "ListViewBackground",
            "MenuBackground", "MenuItemBackground", "ContextMenuBackground",
            "ToolTipBackground", "ToolTipText", "ToolTipBorder",
            "TreeLineColor", "TreeLineHighlightColor", "SubtleTextColor",
            "ScrollBarTrackBackground", "GridSplitterBackground"
        };
        
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
            
            // Initialize debouncer for rapid theme changes
            _themeChangeDebouncer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _themeChangeDebouncer.Tick += OnThemeChangeDebounce;
                
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
                // Pre-load and cache both themes
                PreloadThemes();
                
                // Apply theme based on settings
                ApplyThemeFast(CurrentTheme);
                
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
        /// Switches to the specified theme with debouncing
        /// </summary>
        /// <param name="theme">Theme to apply</param>
        public void SwitchTheme(AppTheme theme)
        {
            if (theme == CurrentTheme && !_pendingTheme.HasValue)
                return;

            // Debounce rapid theme changes
            _pendingTheme = theme;
            _themeChangeDebouncer.Stop();
            _themeChangeDebouncer.Start();
        }
        
        /// <summary>
        /// Toggles between light and dark themes
        /// </summary>
        public void ToggleTheme()
        {
            SwitchTheme(CurrentTheme == AppTheme.Light ? AppTheme.Dark : AppTheme.Light);
        }
        
        /// <summary>
        /// Gets a color brush from the current theme - Optimized version
        /// </summary>
        /// <param name="resourceKey">Resource key for the brush</param>
        /// <returns>The brush or a default brush if not found</returns>
        public SolidColorBrush GetThemeBrush(string resourceKey)
        {
            // Try cached resources first
            if (_activeThemeResources.TryGetValue(resourceKey, out var cached) && 
                cached is SolidColorBrush cachedBrush)
            {
                return cachedBrush;
            }
            
            try
            {
                if (Application.Current.Resources[resourceKey] is SolidColorBrush brush)
                {
                    // Cache for next time
                    _activeThemeResources[resourceKey] = brush;
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
        /// Gets a resource from the current theme by key - Optimized version
        /// </summary>
        /// <typeparam name="T">Type of resource to get</typeparam>
        /// <param name="resourceKey">Key of the resource</param>
        /// <returns>The resource or default if not found</returns>
        public T GetResource<T>(string resourceKey) where T : class
        {
            // Check active theme cache first
            if (_activeThemeResources.TryGetValue(resourceKey, out var cached) && 
                cached is T typedCached)
            {
                return typedCached;
            }
            
            try
            {
                if (Application.Current.Resources.Contains(resourceKey))
                {
                    var resource = Application.Current.Resources[resourceKey] as T;
                    if (resource != null)
                    {
                        // Cache for next time
                        _activeThemeResources[resourceKey] = resource;
                        return resource;
                    }
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
            if (_isApplyingTheme)
                return;
                
            try
            {
                // Notify before refreshing
                ThemeRefreshing?.Invoke(this, CurrentTheme);
                
                // Re-apply current theme efficiently
                ApplyThemeFast(CurrentTheme);
                
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
            
            // Apply theme immediately but don't force refresh
            ApplyThemeToWindow(window);
            
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
        
        #region Private Methods - Performance Optimized
        
        /// <summary>
        /// Preloads and caches theme resources for faster switching
        /// </summary>
        private void PreloadThemes()
        {
            try
            {
                // Load both theme dictionaries
                var darkDict = GetOrCreateResourceDictionary(
                    "/ExplorerPro;component/Themes/DarkTheme.xaml", "dark");
                var lightDict = GetOrCreateResourceDictionary(
                    "/ExplorerPro;component/Themes/LightTheme.xaml", "light");
                    
                // Pre-cache common resources from both themes
                foreach (var key in _commonResourceKeys)
                {
                    if (darkDict.Contains(key))
                        _darkThemeResources[key] = darkDict[key];
                        
                    if (lightDict.Contains(key))
                        _lightThemeResources[key] = lightDict[key];
                }
                
                _resourcesCached = true;
                Console.WriteLine($"Pre-cached {_darkThemeResources.Count} dark and {_lightThemeResources.Count} light resources");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error preloading themes: {ex.Message}");
            }
        }
        
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
        /// Handles debounced theme changes
        /// </summary>
        private void OnThemeChangeDebounce(object sender, EventArgs e)
        {
            _themeChangeDebouncer.Stop();
            
            if (_pendingTheme.HasValue && _pendingTheme.Value != CurrentTheme)
            {
                var theme = _pendingTheme.Value;
                _pendingTheme = null;
                
                ApplyThemeFast(theme);
                CurrentTheme = theme;
                
                // Update settings
                _settingsManager.UpdateSetting("theme", theme.ToString().ToLower());
                _settingsManager.UpdateSetting("ui_preferences.Enable Dark Mode", theme == AppTheme.Dark);
                
                // Notify listeners of theme change
                ThemeChanged?.Invoke(this, theme);
                
                Console.WriteLine($"Theme switched to: {theme}");
            }
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
        /// Fast theme application with minimal dictionary operations
        /// </summary>
        private void ApplyThemeFast(AppTheme theme)
        {
            if (_isApplyingTheme)
                return;
                
            _isApplyingTheme = true;
            
            try
            {
                var app = Application.Current;
                var startTime = DateTime.Now;
                
                // Find existing theme dictionaries
                ResourceDictionary oldThemeDict = null;
                ResourceDictionary baseDict = null;
                
                foreach (var dict in app.Resources.MergedDictionaries)
                {
                    if (dict.Source != null)
                    {
                        var source = dict.Source.ToString();
                        if (source.Contains("DarkTheme.xaml") || source.Contains("LightTheme.xaml"))
                            oldThemeDict = dict;
                        else if (source.Contains("BaseTheme.xaml"))
                            baseDict = dict;
                    }
                }
                
                // Get new theme dictionary
                var newThemeDict = theme == AppTheme.Dark
                    ? GetOrCreateResourceDictionary("/ExplorerPro;component/Themes/DarkTheme.xaml", "dark")
                    : GetOrCreateResourceDictionary("/ExplorerPro;component/Themes/LightTheme.xaml", "light");
                
                // Ensure base theme is present
                if (baseDict == null)
                {
                    baseDict = GetOrCreateResourceDictionary("/ExplorerPro;component/Themes/BaseTheme.xaml", "base");
                    app.Resources.MergedDictionaries.Insert(0, baseDict);
                }
                
                // Replace theme dictionary efficiently
                if (oldThemeDict != null)
                {
                    var index = app.Resources.MergedDictionaries.IndexOf(oldThemeDict);
                    if (index >= 0)
                    {
                        app.Resources.MergedDictionaries[index] = newThemeDict;
                    }
                    else
                    {
                        app.Resources.MergedDictionaries.Add(newThemeDict);
                    }
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(newThemeDict);
                }
                
                // Update active theme resource cache
                _activeThemeResources.Clear();
                var themeCache = theme == AppTheme.Dark ? _darkThemeResources : _lightThemeResources;
                foreach (var kvp in themeCache)
                {
                    _activeThemeResources[kvp.Key] = kvp.Value;
                }
                
                // Batch notify windows
                NotifyWindowsOfThemeChangeBatched();
                
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                Console.WriteLine($"Theme resources applied in {elapsed:F1}ms: {theme}");
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
            finally
            {
                _isApplyingTheme = false;
            }
        }
        
        /// <summary>
        /// Applies theme to the specified theme
        /// </summary>
        /// <param name="theme">Theme to apply</param>
        private void ApplyTheme(AppTheme theme)
        {
            ApplyThemeFast(theme);
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
        /// Notifies all registered windows of theme changes in batches
        /// </summary>
        private void NotifyWindowsOfThemeChangeBatched()
        {
            // Clean up dead references first
            CleanupDeadWindowReferences();
            
            // Process windows in batches to avoid UI freezing
            var windows = new List<Window>();
            foreach (var weakRef in _registeredWindows)
            {
                if (weakRef.TryGetTarget(out var window))
                {
                    windows.Add(window);
                }
            }
            
            // Update main window first
            var mainWindow = windows.FirstOrDefault(w => w is MainWindow);
            if (mainWindow != null)
            {
                RefreshWindowTheme(mainWindow);
                windows.Remove(mainWindow);
            }
            
            // Then update other windows with lower priority
            if (windows.Count > 0)
            {
                Application.Current.Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    foreach (var window in windows)
                    {
                        RefreshWindowTheme(window);
                    }
                }));
            }
        }
        
        /// <summary>
        /// Applies theme to a specific window without forcing full refresh
        /// </summary>
        private void ApplyThemeToWindow(Window window)
        {
            try
            {
                // Just ensure the window has the current theme applied
                window.Resources.Clear(); // Clear any window-specific overrides
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying theme to window: {ex.Message}");
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
                // Stop any pending theme changes
                _themeChangeDebouncer?.Stop();
                
                // Clear caches
                _themeCache.Clear();
                _darkThemeResources.Clear();
                _lightThemeResources.Clear();
                _activeThemeResources.Clear();
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