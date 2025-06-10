using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using ExplorerPro.Models;

namespace ExplorerPro.Services
{
    /// <summary>
    /// Centralized navigation service that coordinates navigation across tabs
    /// and handles persistence of navigation history.
    /// </summary>
    public class NavigationService : INotifyPropertyChanged
    {
        private readonly Dictionary<string, TabNavigationHistory> _tabHistories = new();
        private readonly SettingsManager _settingsManager;
        private string _activeTabId;
        
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<NavigationEventArgs> NavigationRequested;
        
        public NavigationService(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            LoadPersistedHistories();
        }
        
        /// <summary>
        /// Gets the currently active tab ID
        /// </summary>
        public string ActiveTabId 
        { 
            get => _activeTabId;
            private set
            {
                if (_activeTabId != value)
                {
                    _activeTabId = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CanGoBack));
                    OnPropertyChanged(nameof(CanGoForward));
                }
            }
        }
        
        /// <summary>
        /// Indicates if the current tab can navigate backward
        /// </summary>
        public bool CanGoBack => GetCurrentHistory()?.CanGoBack ?? false;
        
        /// <summary>
        /// Indicates if the current tab can navigate forward
        /// </summary>
        public bool CanGoForward => GetCurrentHistory()?.CanGoForward ?? false;
        
        /// <summary>
        /// Registers a new tab for navigation tracking
        /// </summary>
        public void RegisterTab(string tabId)
        {
            if (string.IsNullOrEmpty(tabId))
                return;
                
            if (!_tabHistories.ContainsKey(tabId))
            {
                var history = new TabNavigationHistory();
                history.PropertyChanged += (s, e) => 
                {
                    if (tabId == ActiveTabId)
                    {
                        // Propagate property changes from active tab
                        if (e.PropertyName == nameof(TabNavigationHistory.CanGoBack))
                            OnPropertyChanged(nameof(CanGoBack));
                        if (e.PropertyName == nameof(TabNavigationHistory.CanGoForward))
                            OnPropertyChanged(nameof(CanGoForward));
                    }
                };
                
                _tabHistories[tabId] = history;
            }
        }
        
        /// <summary>
        /// Unregisters a tab when it's closed
        /// </summary>
        public void UnregisterTab(string tabId)
        {
            if (_tabHistories.ContainsKey(tabId))
            {
                _tabHistories.Remove(tabId);
                
                if (ActiveTabId == tabId)
                {
                    ActiveTabId = null;
                }
            }
        }
        
        /// <summary>
        /// Sets the active tab for navigation commands
        /// </summary>
        public void SetActiveTab(string tabId)
        {
            if (_tabHistories.ContainsKey(tabId))
            {
                ActiveTabId = tabId;
            }
        }
        
        /// <summary>
        /// Navigates to a path in the specified tab
        /// </summary>
        public void NavigateTo(string tabId, string path, string title = null)
        {
            if (string.IsNullOrEmpty(tabId) || string.IsNullOrEmpty(path))
                return;
                
            RegisterTab(tabId); // Ensure tab is registered
            
            var history = _tabHistories[tabId];
            history.AddEntry(path, title);
            
            // Fire navigation event
            NavigationRequested?.Invoke(this, new NavigationEventArgs(tabId, path, NavigationType.Navigate));
        }
        
        /// <summary>
        /// Navigates backward in the active tab
        /// </summary>
        public void GoBack()
        {
            var history = GetCurrentHistory();
            if (history != null)
            {
                var item = history.GoBack();
                if (item != null)
                {
                    NavigationRequested?.Invoke(this, new NavigationEventArgs(ActiveTabId, item.Path, NavigationType.Back));
                }
            }
        }
        
        /// <summary>
        /// Navigates forward in the active tab
        /// </summary>
        public void GoForward()
        {
            var history = GetCurrentHistory();
            if (history != null)
            {
                var item = history.GoForward();
                if (item != null)
                {
                    NavigationRequested?.Invoke(this, new NavigationEventArgs(ActiveTabId, item.Path, NavigationType.Forward));
                }
            }
        }
        
        /// <summary>
        /// Gets navigation history for a specific tab
        /// </summary>
        public TabNavigationHistory GetTabHistory(string tabId)
        {
            _tabHistories.TryGetValue(tabId, out var history);
            return history;
        }
        
        /// <summary>
        /// Gets all active tab IDs
        /// </summary>
        public IEnumerable<string> GetActiveTabIds()
        {
            return _tabHistories.Keys.ToList();
        }
        
        /// <summary>
        /// Clears history for a specific tab
        /// </summary>
        public void ClearTabHistory(string tabId)
        {
            if (_tabHistories.TryGetValue(tabId, out var history))
            {
                history.Clear();
            }
        }
        
        /// <summary>
        /// Saves all navigation histories to settings
        /// </summary>
        public void SaveHistories()
        {
            try
            {
                var historiesData = new Dictionary<string, List<NavigationHistoryItem>>();
                
                foreach (var kvp in _tabHistories)
                {
                    var serialized = kvp.Value.Serialize();
                    if (serialized.Any())
                    {
                        historiesData[kvp.Key] = serialized;
                    }
                }
                
                _settingsManager.UpdateSetting("navigation_histories", historiesData);
                _settingsManager.SaveSettings();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving navigation histories: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Loads persisted navigation histories from settings
        /// </summary>
        private void LoadPersistedHistories()
        {
            try
            {
                var historiesData = _settingsManager.GetSetting<Dictionary<string, List<NavigationHistoryItem>>>("navigation_histories");
                
                if (historiesData != null)
                {
                    foreach (var kvp in historiesData)
                    {
                        RegisterTab(kvp.Key);
                        _tabHistories[kvp.Key].Restore(kvp.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading navigation histories: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Gets the current tab's navigation history
        /// </summary>
        private TabNavigationHistory GetCurrentHistory()
        {
            return !string.IsNullOrEmpty(ActiveTabId) && _tabHistories.TryGetValue(ActiveTabId, out var history) ? history : null;
        }
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    /// <summary>
    /// Event arguments for navigation requests
    /// </summary>
    public class NavigationEventArgs : EventArgs
    {
        public string TabId { get; }
        public string Path { get; }
        public NavigationType Type { get; }
        
        public NavigationEventArgs(string tabId, string path, NavigationType type)
        {
            TabId = tabId;
            Path = path;
            Type = type;
        }
    }
    
    /// <summary>
    /// Types of navigation operations
    /// </summary>
    public enum NavigationType
    {
        Navigate,
        Back,
        Forward
    }
} 