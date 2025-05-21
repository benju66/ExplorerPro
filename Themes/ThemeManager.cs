// Themes/ThemeManager.cs
using System;
using System.Windows;
using System.Windows.Media;
using ExplorerPro.Models;

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
                var resourcesToRemove = new ResourceDictionary[app.Resources.MergedDictionaries.Count];
                app.Resources.MergedDictionaries.CopyTo(resourcesToRemove, 0);
                
                foreach (var dict in resourcesToRemove)
                {
                    if (dict.Source != null && 
                        (dict.Source.ToString().Contains("/Themes/") || 
                         dict.Source.ToString().Contains("/Styles/")))
                    {
                        app.Resources.MergedDictionaries.Remove(dict);
                    }
                }
                
                // Add base resources that apply to all themes
                app.Resources.MergedDictionaries.Add(new ResourceDictionary
                {
                    Source = new Uri("/ExplorerPro;component/Themes/BaseTheme.xaml", UriKind.Relative)
                });
                
                // Add theme-specific resources
                if (theme == AppTheme.Dark)
                {
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("/ExplorerPro;component/Themes/DarkTheme.xaml", UriKind.Relative)
                    });
                }
                else
                {
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("/ExplorerPro;component/Themes/LightTheme.xaml", UriKind.Relative)
                    });
                }
                
                Console.WriteLine($"Theme resources applied: {theme}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying theme resources: {ex.Message}");
            }
        }
        
        #endregion
    }
}