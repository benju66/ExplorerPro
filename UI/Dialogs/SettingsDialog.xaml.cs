using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for SettingsDialog.xaml
    /// </summary>
    public partial class SettingsDialog : Window
    {
        private Models.SettingsManager _settingsManager;
        private Window? _parentWindow;
        
        // Dictionary to keep track of panel toggles
        private Dictionary<string, CheckBox> _panelToggles = new Dictionary<string, CheckBox>();
        
        /// <summary>
        /// Initialize the settings dialog
        /// </summary>
        /// <param name="settingsManager">The application's settings manager</param>
        /// <param name="parent">The parent window</param>
        public SettingsDialog(Models.SettingsManager settingsManager, Window? parent = null)
        {
            InitializeComponent();
            
            _settingsManager = settingsManager;
            _parentWindow = parent;
            
            // Initialize panel toggles dictionary
            InitializePanelToggles();
            
            // Load current settings
            LoadSettings();
        }
        
        /// <summary>
        /// Map panel names to their respective checkboxes
        /// </summary>
        private void InitializePanelToggles()
        {
            _panelToggles = new Dictionary<string, CheckBox>
            {
                { "pinned_panel", PinnedPanelToggle },
                { "details_panel", DetailsPanelToggle },
                { "bookmarks_panel", BookmarksPanelToggle },
                { "procore_panel", ProcorePanelToggle },
                { "to_do_panel", ToDoPanelToggle }
            };
        }
        
        /// <summary>
        /// Load settings from the settings manager
        /// </summary>
        private void LoadSettings()
        {
            // Load theme settings
            bool darkModeEnabled = _settingsManager.GetSetting("ui_preferences.Enable Dark Mode", false);
            DarkModeToggle.IsChecked = darkModeEnabled;
            
            // Load panel visibility settings
            var panelSettings = _settingsManager.GetSetting("dockable_panels", new Dictionary<string, bool>());
            
            foreach (var kvp in _panelToggles)
            {
                string panelKey = kvp.Key;
                CheckBox checkbox = kvp.Value;
                
                // Use the default value of true if not specified
                bool isVisible = true;
                
                if (panelSettings.ContainsKey(panelKey))
                {
                    isVisible = panelSettings[panelKey];
                }
                
                checkbox.IsChecked = isVisible;
            }
            
            // Load advanced settings
            LoadAdvancedSettings();
        }
        
        /// <summary>
        /// Load advanced AI and automation settings
        /// </summary>
        private void LoadAdvancedSettings()
        {
            // AI-Powered Search
            EnableAiSearchToggle.IsChecked = _settingsManager.GetSetting("ai_settings.enable_ai_search", false);
            SearchInsideFilesToggle.IsChecked = _settingsManager.GetSetting("ai_settings.search_inside_files", false);
            EnableAiAutocompleteToggle.IsChecked = _settingsManager.GetSetting("ai_settings.enable_ai_autocomplete", false);
            
            // AI-Powered File Organization
            AutoOrganizeFilesToggle.IsChecked = _settingsManager.GetSetting("ai_settings.auto_organize_files", false);
            AiFileTaggingToggle.IsChecked = _settingsManager.GetSetting("ai_settings.ai_file_tagging", false);
            AiDuplicateDetectionToggle.IsChecked = _settingsManager.GetSetting("ai_settings.ai_duplicate_detection", false);
            
            // Cloud & OneDrive AI Integration
            AiKeepFoldersLocalToggle.IsChecked = _settingsManager.GetSetting("ai_settings.ai_keep_folders_local", false);
            AiCloudFileSearchToggle.IsChecked = _settingsManager.GetSetting("ai_settings.ai_cloud_file_search", false);
            
            // AI Summarization & Metadata Extraction
            AiFileSummarizationToggle.IsChecked = _settingsManager.GetSetting("ai_settings.ai_file_summarization", false);
            AiMetadataExtractionToggle.IsChecked = _settingsManager.GetSetting("ai_settings.ai_metadata_extraction", false);
        }
        
        /// <summary>
        /// Event handler for the Dark Mode checkbox
        /// </summary>
        private void DarkModeToggle_Checked(object sender, RoutedEventArgs e)
        {
            ToggleSetting("ui_preferences.Enable Dark Mode", true);
            
            // Apply theme change immediately if parent window is available
            if (_parentWindow != null && _parentWindow is MainWindow.MainWindow mainWindow)
            {
                mainWindow.ApplyTheme("dark");
            }
        }
        
        /// <summary>
        /// Event handler for the Dark Mode checkbox
        /// </summary>
        private void DarkModeToggle_Unchecked(object sender, RoutedEventArgs e)
        {
            ToggleSetting("ui_preferences.Enable Dark Mode", false);
            
            // Apply theme change immediately if parent window is available
            if (_parentWindow != null && _parentWindow is MainWindow.MainWindow mainWindow)
            {
                mainWindow.ApplyTheme("light");
            }
        }
        
        /// <summary>
        /// Event handler for panel visibility checkboxes
        /// </summary>
        private void PanelVisibility_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox)
            {
                string? panelKey = GetPanelKeyFromCheckBox(checkbox);
                if (!string.IsNullOrEmpty(panelKey))
                {
                    TogglePanel(panelKey, true);
                }
            }
        }
        
        /// <summary>
        /// Event handler for panel visibility checkboxes
        /// </summary>
        private void PanelVisibility_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox)
            {
                string? panelKey = GetPanelKeyFromCheckBox(checkbox);
                if (!string.IsNullOrEmpty(panelKey))
                {
                    TogglePanel(panelKey, false);
                }
            }
        }
        
        /// <summary>
        /// Get the panel key corresponding to a checkbox
        /// </summary>
        private string? GetPanelKeyFromCheckBox(CheckBox checkbox)
        {
            foreach (var kvp in _panelToggles)
            {
                if (kvp.Value == checkbox)
                {
                    return kvp.Key;
                }
            }
            return null;
        }
        
        /// <summary>
        /// Toggle a panel's visibility in settings
        /// </summary>
        private void TogglePanel(string panelKey, bool isVisible)
        {
            _settingsManager.UpdateSetting($"dockable_panels.{panelKey}", isVisible);
            
            // Update UI if parent window is available
            if (_parentWindow != null && _parentWindow is MainWindow.MainWindow mainWindow)
            {
                // Just save the setting - MainWindow will read from settings when needed
                // Using reflection to try to update UI if possible
                try 
                {
                    var method = mainWindow.GetType().GetMethod("UpdatePanelVisibility");
                    if (method != null) 
                    {
                        method.Invoke(mainWindow, new object[] { panelKey, isVisible });
                    }
                } 
                catch 
                {
                    // Silently ignore if method doesn't exist
                }
            }
        }
        
        /// <summary>
        /// Event handler for advanced settings checkboxes
        /// </summary>
        private void AdvancedSetting_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox)
            {
                string? settingKey = GetAdvancedSettingKey(checkbox);
                if (!string.IsNullOrEmpty(settingKey))
                {
                    ToggleSetting(settingKey, true);
                }
            }
        }
        
        /// <summary>
        /// Event handler for advanced settings checkboxes
        /// </summary>
        private void AdvancedSetting_Unchecked(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkbox)
            {
                string? settingKey = GetAdvancedSettingKey(checkbox);
                if (!string.IsNullOrEmpty(settingKey))
                {
                    ToggleSetting(settingKey, false);
                }
            }
        }
        
        /// <summary>
        /// Get the settings key for an advanced setting checkbox
        /// </summary>
        private string? GetAdvancedSettingKey(CheckBox checkbox)
        {
            if (checkbox == EnableAiSearchToggle) return "ai_settings.enable_ai_search";
            if (checkbox == SearchInsideFilesToggle) return "ai_settings.search_inside_files";
            if (checkbox == EnableAiAutocompleteToggle) return "ai_settings.enable_ai_autocomplete";
            if (checkbox == AutoOrganizeFilesToggle) return "ai_settings.auto_organize_files";
            if (checkbox == AiFileTaggingToggle) return "ai_settings.ai_file_tagging";
            if (checkbox == AiDuplicateDetectionToggle) return "ai_settings.ai_duplicate_detection";
            if (checkbox == AiKeepFoldersLocalToggle) return "ai_settings.ai_keep_folders_local";
            if (checkbox == AiCloudFileSearchToggle) return "ai_settings.ai_cloud_file_search";
            if (checkbox == AiFileSummarizationToggle) return "ai_settings.ai_file_summarization";
            if (checkbox == AiMetadataExtractionToggle) return "ai_settings.ai_metadata_extraction";
            
            return null;
        }
        
        /// <summary>
        /// Update a setting in the settings manager
        /// </summary>
        private void ToggleSetting(string settingKey, bool isEnabled)
        {
            _settingsManager.UpdateSetting(settingKey, isEnabled);
        }
        
        /// <summary>
        /// Event handler for the Save button
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            _settingsManager.SaveSettings();
            
            // Apply settings to main window
            if (_parentWindow != null && _parentWindow is MainWindow.MainWindow mainWindow)
            {
                mainWindow.ApplySavedSettings();
            }
            
            DialogResult = true;
            Close();
        }
        
        /// <summary>
        /// Event handler for the Cancel button
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        /// <summary>
        /// Set the current tab to the Advanced tab
        /// </summary>
        public void OpenAdvancedSettings()
        {
            Tabs.SelectedIndex = 1;  // Advanced tab
        }
    }
}