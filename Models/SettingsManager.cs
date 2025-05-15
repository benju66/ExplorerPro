using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Manages application settings, including loading, saving, and accessing configuration options.
    /// </summary>
    public class SettingsManager
    {
        #region Fields

        private readonly string _settingsFilePath;
        private readonly ILogger<SettingsManager>? _logger;
        private readonly object? _parentWindow;
        private JObject _settings;
        private readonly JObject _defaultSettings;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the SettingsManager class.
        /// </summary>
        /// <param name="settingsFilePath">Path to the settings JSON file</param>
        /// <param name="parentWindow">Optional reference to the parent window for UI updates</param>
        /// <param name="logger">Optional logger for operation tracking</param>
        public SettingsManager(string settingsFilePath = "data/settings.json", object? parentWindow = null, ILogger<SettingsManager>? logger = null)
        {
            _settingsFilePath = settingsFilePath;
            _parentWindow = parentWindow;
            _logger = logger;

            // Updated default settings to include one_note_panel
            _defaultSettings = JObject.Parse(@"{
                ""theme"": ""light"",
                ""last_opened_directory"": ""C:\\Users"",
                ""ui_preferences"": {
                    ""Enable Dark Mode"": false,
                    ""Show Address Bar"": true
                },
                ""dockable_panels"": {
                    ""pinned_panel"": true,
                    ""recent_items_panel"": false,
                    ""preview_panel"": false,
                    ""details_panel"": true,
                    ""procore_panel"": false,
                    ""bookmarks_panel"": false,
                    ""to_do_panel"": true,
                    ""one_note_panel"": false
                },
                ""window_geometry_b64"": null,
                ""window_state_b64"": null
            }");

            _settings = LoadSettings();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Loads settings from the JSON file and ensures missing settings are added.
        /// </summary>
        /// <returns>The loaded settings</returns>
        public JObject LoadSettings()
        {
            if (File.Exists(_settingsFilePath))
            {
                try
                {
                    string settingsJson = File.ReadAllText(_settingsFilePath);
                    JObject loadedSettings = JObject.Parse(settingsJson);

                    // Ensure missing keys are filled with defaults by merging
                    loadedSettings.Merge(_defaultSettings, new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Union,
                        MergeNullValueHandling = MergeNullValueHandling.Merge
                    });

                    _settings = loadedSettings;
                    return _settings;
                }
                catch (JsonException)
                {
                    _logger?.LogError("Error decoding settings file. Using default settings.");
                    ResetToDefaults(true);
                }
                catch (IOException ex)
                {
                    _logger?.LogError(ex, "Error reading settings file. Using default settings.");
                    ResetToDefaults(true);
                }
            }
            else
            {
                ResetToDefaults(true);
            }

            return _settings;
        }

        /// <summary>
        /// Applies default settings and ensures all required settings are present
        /// </summary>
        public void ApplyDefaultSettings()
        {
            // Reset to defaults but don't save immediately
            ResetToDefaults(false);
            
            // You can customize any specific default settings here
            
            // Save the default settings
            SaveSettings();
        }

        /// <summary>
        /// Saves the current settings to the JSON file.
        /// </summary>
        /// <param name="settings">Optional settings to save; if null, saves the current settings</param>
        /// <returns>True if successful, false otherwise</returns>
        public bool SaveSettings(JObject? settings = null)
        {
            if (settings != null)
            {
                _settings = settings;
            }

            try
            {
                string? directoryName = Path.GetDirectoryName(_settingsFilePath);
                if (!string.IsNullOrEmpty(directoryName))
                {
                    Directory.CreateDirectory(directoryName);
                }
                File.WriteAllText(_settingsFilePath, _settings.ToString(Formatting.Indented));
                return true;
            }
            catch (IOException ex)
            {
                _logger?.LogError(ex, "Error saving settings");
                return false;
            }
        }

        /// <summary>
        /// Resets settings to default values and ensures all keys exist.
        /// </summary>
        /// <param name="save">Whether to save the reset settings to disk</param>
        public void ResetToDefaults(bool save = false)
        {
            _settings = JObject.Parse(_defaultSettings.ToString());
            if (save)
            {
                SaveSettings();
            }
        }

        /// <summary>
        /// Retrieves a specific setting, ensuring nested defaults exist.
        /// </summary>
        /// <typeparam name="T">The type to convert the setting value to</typeparam>
        /// <param name="key">The setting key, using dot notation for nested settings</param>
        /// <param name="defaultValue">Default value to return if the setting is not found</param>
        /// <returns>The setting value converted to type T, or defaultValue if not found</returns>
        public T GetSetting<T>(string key, T defaultValue = default)
        {
            string[] keys = key.Split('.');
            JToken? value = _settings;

            foreach (string k in keys)
            {
                if (value is JObject obj)
                {
                    value = obj[k];
                    if (value == null)
                    {
                        return defaultValue;
                    }
                }
                else
                {
                    return defaultValue;
                }
            }

            try
            {
                return value.ToObject<T>() ?? defaultValue;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Updates a setting while preserving existing data.
        /// </summary>
        /// <param name="key">The setting key, using dot notation for nested settings</param>
        /// <param name="value">The new value to set</param>
        public void UpdateSetting(string key, object value)
        {
            string[] keys = key.Split('.');
            JToken current = _settings;

            // Navigate to the right location in the JSON structure
            for (int i = 0; i < keys.Length - 1; i++)
            {
                string k = keys[i];
                if (current[k] == null)
                {
                    current[k] = new JObject();
                }
                current = current[k] ?? new JObject();
            }

            // Set the value
            if (current is JObject objCurrent)
            {
                objCurrent[keys[keys.Length - 1]] = JToken.FromObject(value);
                SaveSettings();
            }
        }

        /// <summary>
        /// Updates visibility of a specific panel and applies changes to the UI.
        /// </summary>
        /// <param name="panelName">Name of the panel</param>
        /// <param name="isVisible">Whether the panel should be visible</param>
        public void SetPanelVisibility(string panelName, bool isVisible)
        {
            if (_settings["dockable_panels"] == null)
            {
                _settings["dockable_panels"] = new JObject();
            }

            if (_settings["dockable_panels"] is JObject panels)
            {
                panels[panelName] = isVisible;
                SaveSettings();

                // If there's a parent window, update the UI
                if (_parentWindow != null && _parentWindow is IWindowWithDockPanels windowWithPanels)
                {
                    windowWithPanels.UpdatePanelVisibility(panelName, isVisible);
                }
            }
        }

        /// <summary>
        /// Saves the main window geometry and state as base64-encoded strings in settings.
        /// </summary>
        /// <param name="geometryBytes">Window geometry bytes</param>
        /// <param name="stateBytes">Window state bytes</param>
        public void StoreMainWindowLayout(byte[]? geometryBytes, byte[]? stateBytes)
        {
            if (geometryBytes != null)
            {
                _settings["window_geometry_b64"] = Convert.ToBase64String(geometryBytes);
            }

            if (stateBytes != null)
            {
                _settings["window_state_b64"] = Convert.ToBase64String(stateBytes);
            }

            SaveSettings();
        }

        /// <summary>
        /// Returns the previously saved geometry and state as raw bytes, or null if not set.
        /// </summary>
        /// <returns>A tuple containing the geometry bytes and state bytes</returns>
        public (byte[]? GeometryBytes, byte[]? StateBytes) RetrieveMainWindowLayout()
        {
            byte[]? geometryBytes = null;
            byte[]? stateBytes = null;

            string? geometryB64 = _settings["window_geometry_b64"]?.ToString();
            string? stateB64 = _settings["window_state_b64"]?.ToString();

            if (!string.IsNullOrEmpty(geometryB64))
            {
                try
                {
                    geometryBytes = Convert.FromBase64String(geometryB64);
                }
                catch (FormatException ex)
                {
                    _logger?.LogError(ex, "Error decoding window geometry");
                }
            }

            if (!string.IsNullOrEmpty(stateB64))
            {
                try
                {
                    stateBytes = Convert.FromBase64String(stateB64);
                }
                catch (FormatException ex)
                {
                    _logger?.LogError(ex, "Error decoding window state");
                }
            }

            return (geometryBytes, stateBytes);
        }

        #endregion
    }

    /// <summary>
    /// Interface for a window that contains dock panels that can be updated programmatically.
    /// </summary>
    public interface IWindowWithDockPanels
    {
        /// <summary>
        /// Updates the visibility of a named dock panel.
        /// </summary>
        /// <param name="panelName">Name of the panel</param>
        /// <param name="isVisible">Whether the panel should be visible</param>
        void UpdatePanelVisibility(string panelName, bool isVisible);
    }
}