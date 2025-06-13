using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Manages tab states and provides recovery functionality
    /// </summary>
    public class TabStateManager
    {
        private readonly ILogger<TabStateManager> _logger;
        private readonly string _stateFilePath;
        private readonly Dictionary<string, TabState> _tabStates;
        private readonly object _stateLock = new object();

        public TabStateManager(ILogger<TabStateManager> logger)
        {
            _logger = logger;
            _stateFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ExplorerPro",
                "tab_states.json"
            );
            _tabStates = new Dictionary<string, TabState>();
            
            // Ensure directory exists
            Directory.CreateDirectory(Path.GetDirectoryName(_stateFilePath)!);
        }

        /// <summary>
        /// Save the current state of a tab
        /// </summary>
        public void SaveTabState(string tabId, TabState state)
        {
            try
            {
                lock (_stateLock)
                {
                    _tabStates[tabId] = state;
                    PersistStates();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving tab state for tab {TabId}", tabId);
            }
        }

        /// <summary>
        /// Get the saved state for a tab
        /// </summary>
        public TabState? GetTabState(string tabId)
        {
            lock (_stateLock)
            {
                return _tabStates.TryGetValue(tabId, out var state) ? state : null;
            }
        }

        /// <summary>
        /// Remove a tab's saved state
        /// </summary>
        public void RemoveTabState(string tabId)
        {
            try
            {
                lock (_stateLock)
                {
                    if (_tabStates.Remove(tabId))
                    {
                        PersistStates();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing tab state for tab {TabId}", tabId);
            }
        }

        /// <summary>
        /// Load all saved tab states
        /// </summary>
        public async Task LoadSavedStatesAsync()
        {
            try
            {
                if (!File.Exists(_stateFilePath))
                {
                    return;
                }

                var json = await File.ReadAllTextAsync(_stateFilePath);
                var states = JsonSerializer.Deserialize<Dictionary<string, TabState>>(json);

                if (states != null)
                {
                    lock (_stateLock)
                    {
                        _tabStates.Clear();
                        foreach (var state in states)
                        {
                            _tabStates[state.Key] = state.Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading saved tab states");
            }
        }

        /// <summary>
        /// Persist current tab states to disk
        /// </summary>
        private void PersistStates()
        {
            try
            {
                var json = JsonSerializer.Serialize(_tabStates, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_stateFilePath, json);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error persisting tab states");
            }
        }

        /// <summary>
        /// Clear all saved tab states
        /// </summary>
        public void ClearAllStates()
        {
            try
            {
                lock (_stateLock)
                {
                    _tabStates.Clear();
                    if (File.Exists(_stateFilePath))
                    {
                        File.Delete(_stateFilePath);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing tab states");
            }
        }

        /// <summary>
        /// Clear the state for a tab
        /// </summary>
        public void ClearTabState(string tabId)
        {
            try
            {
                lock (_stateLock)
                {
                    _tabStates.Remove(tabId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error clearing tab state {TabId}", tabId);
            }
        }
    }

    /// <summary>
    /// Represents the state of a tab
    /// </summary>
    public class TabState
    {
        public string Title { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public bool IsHibernated { get; set; }
        public DateTime LastAccessed { get; set; }
        public Dictionary<string, object> CustomProperties { get; set; } = new Dictionary<string, object>();
    }
} 