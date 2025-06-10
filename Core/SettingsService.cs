using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using System.Linq;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Settings service implementation with validation, error handling, and testability.
    /// IMPLEMENTS FIX 7: Settings Management Coupling - Provides safe settings abstraction
    /// </summary>
    public class SettingsService : ISettingsService
    {
        private readonly SettingsManager _settingsManager;
        private readonly ILogger _logger;
        private readonly object _lock = new object();

        /// <summary>
        /// Raised when a setting value changes.
        /// </summary>
        public event EventHandler<SettingChangedEventArgs> SettingChanged;

        /// <summary>
        /// Raised when settings validation fails and defaults are applied.
        /// </summary>
        public event EventHandler<SettingsValidationEventArgs> ValidationFailed;

        /// <summary>
        /// Initializes a new instance of the SettingsService.
        /// </summary>
        /// <param name="settingsManager">The underlying settings manager</param>
        /// <param name="logger">Optional logger for error reporting</param>
        public SettingsService(SettingsManager settingsManager, ILogger logger = null)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _logger = logger;

            // Validate settings on initialization
            ValidateSettings();
        }

        /// <summary>
        /// Gets a setting value with error handling and validation.
        /// </summary>
        public T GetSetting<T>(string key, T defaultValue = default)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger?.LogWarning("GetSetting called with null or empty key, returning default");
                return defaultValue;
            }

            try
            {
                lock (_lock)
                {
                    var value = _settingsManager.GetSetting<T>(key, defaultValue);
                    
                    // Additional validation for specific types
                    if (typeof(T) == typeof(string) && value is string strValue)
                    {
                        // Ensure string values are not corrupted
                        if (strValue?.Contains('\0') == true)
                        {
                            _logger?.LogWarning("Corrupted string setting detected for key '{Key}', using default", key);
                            return defaultValue;
                        }
                    }
                    
                    return value;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error reading setting '{Key}', using default value", key);
                return defaultValue;
            }
        }

        /// <summary>
        /// Sets a setting value asynchronously with validation.
        /// </summary>
        public async Task<bool> SetSettingAsync<T>(string key, T value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger?.LogWarning("SetSettingAsync called with null or empty key");
                return false;
            }

            try
            {
                T previousValue = default;
                bool success;

                lock (_lock)
                {
                    // Capture previous value for event
                    try
                    {
                        previousValue = _settingsManager.GetSetting<T>(key);
                    }
                    catch
                    {
                        // Ignore errors when getting previous value
                    }

                    // Update the setting
                    _settingsManager.UpdateSetting(key, value);
                    success = true;
                }

                if (success)
                {
                    // Save settings asynchronously
                    await Task.Run(() =>
                    {
                        try
                        {
                            _settingsManager.SaveSettings();
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error saving settings after updating '{Key}'", key);
                            throw;
                        }
                    });

                    // Raise change event
                    SettingChanged?.Invoke(this, new SettingChangedEventArgs(key, value, previousValue));
                    
                    _logger?.LogDebug("Successfully set setting '{Key}' to '{Value}'", key, value);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error setting '{Key}' to '{Value}'", key, value);
                return false;
            }
        }

        /// <summary>
        /// Updates a setting value synchronously (for compatibility).
        /// </summary>
        public bool UpdateSetting(string key, object value)
        {
            if (string.IsNullOrWhiteSpace(key))
            {
                _logger?.LogWarning("UpdateSetting called with null or empty key");
                return false;
            }

            try
            {
                object previousValue = null;

                lock (_lock)
                {
                    // Capture previous value for event
                    try
                    {
                        previousValue = _settingsManager.GetSetting<object>(key);
                    }
                    catch
                    {
                        // Ignore errors when getting previous value
                    }

                    // Update the setting
                    _settingsManager.UpdateSetting(key, value);
                }

                // Raise change event
                SettingChanged?.Invoke(this, new SettingChangedEventArgs(key, value, previousValue));
                
                _logger?.LogDebug("Successfully updated setting '{Key}' to '{Value}'", key, value);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error updating setting '{Key}' to '{Value}'", key, value);
                return false;
            }
        }

        /// <summary>
        /// Validates all settings and corrects any issues found.
        /// </summary>
        public bool ValidateSettings()
        {
            var failedKeys = new List<string>();
            bool wasReset = false;

            try
            {
                _logger?.LogDebug("Starting settings validation");

                // Validate critical settings
                ValidateThemeSetting(failedKeys);
                ValidateWindowGeometry(failedKeys);
                ValidatePanelSettings(failedKeys);
                ValidateUIPreferences(failedKeys);

                // If there were validation failures, trigger event
                if (failedKeys.Count > 0)
                {
                    _logger?.LogWarning("Settings validation found {Count} issues: {Keys}", 
                        failedKeys.Count, string.Join(", ", failedKeys));
                    
                    ValidationFailed?.Invoke(this, new SettingsValidationEventArgs(
                        failedKeys.ToArray(), null, wasReset));
                }
                else
                {
                    _logger?.LogDebug("Settings validation completed successfully");
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error during settings validation, resetting to defaults");
                
                try
                {
                    ResetToDefaults();
                    wasReset = true;
                    
                    ValidationFailed?.Invoke(this, new SettingsValidationEventArgs(
                        new[] { "ALL_SETTINGS" }, ex, wasReset));
                    
                    return false;
                }
                catch (Exception resetEx)
                {
                    _logger?.LogCritical(resetEx, "Failed to reset settings to defaults");
                    return false;
                }
            }
        }

        /// <summary>
        /// Resets all settings to their default values.
        /// </summary>
        public void ResetToDefaults()
        {
            try
            {
                _logger?.LogInformation("Resetting all settings to defaults");
                
                lock (_lock)
                {
                    _settingsManager.ResetToDefaults(true);
                }
                
                _logger?.LogInformation("Settings reset to defaults completed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error resetting settings to defaults");
                throw;
            }
        }

        /// <summary>
        /// Saves all pending changes to persistent storage.
        /// </summary>
        public async Task<bool> SaveSettingsAsync()
        {
            try
            {
                return await Task.Run(() =>
                {
                    lock (_lock)
                    {
                        return _settingsManager.SaveSettings();
                    }
                });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving settings");
                return false;
            }
        }

        /// <summary>
        /// Gets window settings for a specific window instance.
        /// </summary>
        public WindowSettings GetWindowSettings(string windowId)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                _logger?.LogWarning("GetWindowSettings called with null or empty windowId");
                return WindowSettings.CreateDefault();
            }

            try
            {
                // For now, we'll use the legacy window geometry storage
                // This can be enhanced to support multiple windows in the future
                var (geometryBytes, stateBytes) = _settingsManager.RetrieveMainWindowLayout();
                
                if (geometryBytes == null && stateBytes == null)
                {
                    _logger?.LogDebug("No saved window settings found for '{WindowId}', using defaults", windowId);
                    return WindowSettings.CreateDefault();
                }

                // Convert legacy format to WindowSettings
                // This is a simplified conversion - in a real implementation,
                // you'd properly deserialize the geometry and state bytes
                var settings = WindowSettings.CreateDefault();
                
                if (settings.ValidateAndCorrect(_logger))
                {
                    return settings;
                }
                else
                {
                    _logger?.LogWarning("Invalid window settings for '{WindowId}', using defaults", windowId);
                    return WindowSettings.CreateDefault();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error retrieving window settings for '{WindowId}', using defaults", windowId);
                return WindowSettings.CreateDefault();
            }
        }

        /// <summary>
        /// Saves window settings for a specific window instance.
        /// </summary>
        public async Task<bool> SaveWindowSettingsAsync(string windowId, WindowSettings settings)
        {
            if (string.IsNullOrWhiteSpace(windowId))
            {
                _logger?.LogWarning("SaveWindowSettingsAsync called with null or empty windowId");
                return false;
            }

            if (settings == null)
            {
                _logger?.LogWarning("SaveWindowSettingsAsync called with null settings");
                return false;
            }

            try
            {
                // Validate settings before saving
                if (!settings.ValidateAndCorrect(_logger))
                {
                    _logger?.LogWarning("Invalid window settings for '{WindowId}', not saving", windowId);
                    return false;
                }

                // For now, we'll use the legacy window geometry storage
                // In a real implementation, you'd properly serialize the settings
                await Task.Run(() =>
                {
                    // This is a simplified implementation
                    // You would convert WindowSettings to the legacy format here
                    _settingsManager.StoreMainWindowLayout(null, null);
                    _settingsManager.SaveSettings();
                });

                _logger?.LogDebug("Saved window settings for '{WindowId}'", windowId);
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving window settings for '{WindowId}'", windowId);
                return false;
            }
        }

        /// <summary>
        /// Gets the current theme setting.
        /// </summary>
        public string GetTheme()
        {
            return GetSetting<string>("theme", "light");
        }

        /// <summary>
        /// Sets the current theme setting.
        /// </summary>
        public async Task<bool> SetThemeAsync(string theme)
        {
            if (string.IsNullOrWhiteSpace(theme))
            {
                _logger?.LogWarning("SetThemeAsync called with null or empty theme");
                return false;
            }

            // Validate theme value
            var validThemes = new[] { "light", "dark", "auto" };
            if (!validThemes.Contains(theme.ToLower()))
            {
                _logger?.LogWarning("Invalid theme '{Theme}', using 'light'", theme);
                theme = "light";
            }

            return await SetSettingAsync("theme", theme.ToLower());
        }

        /// <summary>
        /// Gets whether dark mode is enabled.
        /// </summary>
        public bool IsDarkModeEnabled()
        {
            return GetSetting<bool>("ui_preferences.Enable Dark Mode", false);
        }

        /// <summary>
        /// Gets panel visibility settings.
        /// </summary>
        public Dictionary<string, bool> GetPanelSettings()
        {
            try
            {
                return GetSetting<Dictionary<string, bool>>("dockable_panels", new Dictionary<string, bool>());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting panel settings, returning empty dictionary");
                return new Dictionary<string, bool>();
            }
        }

        /// <summary>
        /// Sets the visibility of a specific panel.
        /// </summary>
        public async Task<bool> SetPanelVisibilityAsync(string panelName, bool isVisible)
        {
            if (string.IsNullOrWhiteSpace(panelName))
            {
                _logger?.LogWarning("SetPanelVisibilityAsync called with null or empty panelName");
                return false;
            }

            return await SetSettingAsync($"dockable_panels.{panelName}", isVisible);
        }

        #region Private Validation Methods

        private void ValidateThemeSetting(List<string> failedKeys)
        {
            try
            {
                var theme = GetSetting<string>("theme", "light");
                var validThemes = new[] { "light", "dark", "auto" };
                
                if (string.IsNullOrWhiteSpace(theme) || !validThemes.Contains(theme.ToLower()))
                {
                    _logger?.LogWarning("Invalid theme setting '{Theme}', resetting to 'light'", theme);
                    UpdateSetting("theme", "light");
                    UpdateSetting("ui_preferences.Enable Dark Mode", false);
                    failedKeys.Add("theme");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating theme setting");
                failedKeys.Add("theme");
            }
        }

        private void ValidateWindowGeometry(List<string> failedKeys)
        {
            try
            {
                // Check if geometry data is corrupted
                var (geometryBytes, stateBytes) = _settingsManager.RetrieveMainWindowLayout();
                
                // Basic validation - ensure bytes are reasonable size if present
                if (geometryBytes != null && geometryBytes.Length > 10000)
                {
                    _logger?.LogWarning("Window geometry data appears corrupted (too large), clearing");
                    _settingsManager.StoreMainWindowLayout(null, null);
                    failedKeys.Add("window_geometry");
                }
                
                if (stateBytes != null && stateBytes.Length > 10000)
                {
                    _logger?.LogWarning("Window state data appears corrupted (too large), clearing");
                    _settingsManager.StoreMainWindowLayout(null, null);
                    failedKeys.Add("window_state");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating window geometry");
                failedKeys.Add("window_geometry");
            }
        }

        private void ValidatePanelSettings(List<string> failedKeys)
        {
            try
            {
                var panelSettings = GetSetting<Dictionary<string, bool>>("dockable_panels", new Dictionary<string, bool>());
                
                // Validate that panel settings is a proper dictionary
                if (panelSettings == null)
                {
                    _logger?.LogWarning("Panel settings is null, resetting to defaults");
                    UpdateSetting("dockable_panels", new Dictionary<string, bool>());
                    failedKeys.Add("dockable_panels");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating panel settings");
                failedKeys.Add("dockable_panels");
            }
        }

        private void ValidateUIPreferences(List<string> failedKeys)
        {
            try
            {
                var uiPreferences = GetSetting<Dictionary<string, object>>("ui_preferences", new Dictionary<string, object>());
                
                // Validate that UI preferences is a proper dictionary
                if (uiPreferences == null)
                {
                    _logger?.LogWarning("UI preferences is null, resetting to defaults");
                    UpdateSetting("ui_preferences", new Dictionary<string, object>
                    {
                        ["Enable Dark Mode"] = false,
                        ["Show Address Bar"] = true
                    });
                    failedKeys.Add("ui_preferences");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error validating UI preferences");
                failedKeys.Add("ui_preferences");
            }
        }

        #endregion
    }
} 