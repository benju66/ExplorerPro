using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using ExplorerPro.Models;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Interface for settings management with abstraction, validation, and error handling.
    /// IMPLEMENTS FIX 7: Settings Management Coupling - Provides testable settings abstraction
    /// </summary>
    public interface ISettingsService
    {
        /// <summary>
        /// Gets a setting value with type safety and error handling.
        /// </summary>
        /// <typeparam name="T">The type to convert the setting value to</typeparam>
        /// <param name="key">The setting key, using dot notation for nested settings</param>
        /// <param name="defaultValue">Default value to return if the setting is not found or invalid</param>
        /// <returns>The setting value converted to type T, or defaultValue if not found/invalid</returns>
        T GetSetting<T>(string key, T defaultValue = default);

        /// <summary>
        /// Sets a setting value asynchronously with validation and error handling.
        /// </summary>
        /// <typeparam name="T">The type of the setting value</typeparam>
        /// <param name="key">The setting key</param>
        /// <param name="value">The value to set</param>
        /// <returns>True if the setting was successfully saved, false otherwise</returns>
        Task<bool> SetSettingAsync<T>(string key, T value);

        /// <summary>
        /// Updates a setting value synchronously (for compatibility with existing code).
        /// </summary>
        /// <param name="key">The setting key</param>
        /// <param name="value">The value to set</param>
        /// <returns>True if the setting was successfully updated, false otherwise</returns>
        bool UpdateSetting(string key, object value);

        /// <summary>
        /// Validates all settings and corrects any issues found.
        /// </summary>
        /// <returns>True if settings are valid or were successfully corrected, false if corruption is severe</returns>
        bool ValidateSettings();

        /// <summary>
        /// Resets all settings to their default values.
        /// </summary>
        void ResetToDefaults();

        /// <summary>
        /// Saves all pending changes to persistent storage.
        /// </summary>
        /// <returns>True if settings were successfully saved, false otherwise</returns>
        Task<bool> SaveSettingsAsync();

        // Window-specific settings
        /// <summary>
        /// Gets window settings for a specific window instance.
        /// </summary>
        /// <param name="windowId">Unique identifier for the window</param>
        /// <returns>Window settings object, or null if not found</returns>
        WindowSettings GetWindowSettings(string windowId);

        /// <summary>
        /// Saves window settings for a specific window instance.
        /// </summary>
        /// <param name="windowId">Unique identifier for the window</param>
        /// <param name="settings">The window settings to save</param>
        /// <returns>True if settings were successfully saved, false otherwise</returns>
        Task<bool> SaveWindowSettingsAsync(string windowId, WindowSettings settings);

        // Theme settings
        /// <summary>
        /// Gets the current theme setting.
        /// </summary>
        /// <returns>The current theme name</returns>
        string GetTheme();

        /// <summary>
        /// Sets the current theme setting.
        /// </summary>
        /// <param name="theme">The theme name to set</param>
        /// <returns>True if the theme was successfully set, false otherwise</returns>
        Task<bool> SetThemeAsync(string theme);

        /// <summary>
        /// Gets whether dark mode is enabled.
        /// </summary>
        /// <returns>True if dark mode is enabled, false otherwise</returns>
        bool IsDarkModeEnabled();

        // Panel settings
        /// <summary>
        /// Gets panel visibility settings.
        /// </summary>
        /// <returns>Dictionary of panel names and their visibility states</returns>
        Dictionary<string, bool> GetPanelSettings();

        /// <summary>
        /// Sets the visibility of a specific panel.
        /// </summary>
        /// <param name="panelName">Name of the panel</param>
        /// <param name="isVisible">Whether the panel should be visible</param>
        /// <returns>True if the setting was successfully updated, false otherwise</returns>
        Task<bool> SetPanelVisibilityAsync(string panelName, bool isVisible);

        // Events
        /// <summary>
        /// Raised when a setting value changes.
        /// </summary>
        event EventHandler<SettingChangedEventArgs> SettingChanged;

        /// <summary>
        /// Raised when settings validation fails and defaults are applied.
        /// </summary>
        event EventHandler<SettingsValidationEventArgs> ValidationFailed;
    }

    /// <summary>
    /// Event arguments for setting change notifications.
    /// </summary>
    public class SettingChangedEventArgs : EventArgs
    {
        public string Key { get; }
        public object Value { get; }
        public object PreviousValue { get; }

        public SettingChangedEventArgs(string key, object value, object previousValue = null)
        {
            Key = key;
            Value = value;
            PreviousValue = previousValue;
        }
    }

    /// <summary>
    /// Event arguments for settings validation failures.
    /// </summary>
    public class SettingsValidationEventArgs : EventArgs
    {
        public string[] FailedKeys { get; }
        public Exception Exception { get; }
        public bool WasReset { get; }

        public SettingsValidationEventArgs(string[] failedKeys, Exception exception = null, bool wasReset = false)
        {
            FailedKeys = failedKeys;
            Exception = exception;
            WasReset = wasReset;
        }
    }
} 