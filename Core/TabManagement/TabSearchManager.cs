using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Manages tab search and organization features
    /// </summary>
    public class TabSearchManager
    {
        private readonly ILogger<TabSearchManager> _logger;
        private readonly TabStateManager _stateManager;
        private readonly Dictionary<string, TabGroup> _tabGroups;
        private readonly object _lock = new object();

        public TabSearchManager(
            ILogger<TabSearchManager> logger,
            TabStateManager stateManager)
        {
            _logger = logger;
            _stateManager = stateManager;
            _tabGroups = new Dictionary<string, TabGroup>();
        }

        /// <summary>
        /// Search for tabs matching the given criteria
        /// </summary>
        public async Task<List<TabSearchResult>> SearchTabsAsync(
            string searchTerm,
            TabSearchOptions? options = null)
        {
            try
            {
                options ??= new TabSearchOptions();
                var results = new List<TabSearchResult>();

                // Search in tab states
                var states = await GetAllTabStatesAsync();
                foreach (var state in states)
                {
                    if (MatchesSearchCriteria(state, searchTerm, options))
                    {
                        results.Add(new TabSearchResult
                        {
                            TabId = state.Key,
                            Title = state.Value.Title,
                            Path = state.Value.Path,
                            LastAccessed = state.Value.LastAccessed,
                            Group = GetTabGroup(state.Key)
                        });
                    }
                }

                return results.OrderByDescending(x => x.LastAccessed).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tabs");
                return new List<TabSearchResult>();
            }
        }

        /// <summary>
        /// Create a new tab group
        /// </summary>
        public void CreateTabGroup(string groupName, string? color = null)
        {
            try
            {
                lock (_lock)
                {
                    if (!_tabGroups.ContainsKey(groupName))
                    {
                        _tabGroups[groupName] = new TabGroup
                        {
                            Name = groupName,
                            Color = color,
                            CreatedAt = DateTime.UtcNow
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating tab group {GroupName}", groupName);
            }
        }

        /// <summary>
        /// Add a tab to a group
        /// </summary>
        public void AddTabToGroup(string tabId, string groupName)
        {
            try
            {
                var state = _stateManager.GetTabState(tabId);
                if (state != null)
                {
                    state.CustomProperties["Group"] = groupName;
                    _stateManager.SaveTabState(tabId, state);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tab {TabId} to group {GroupName}", tabId, groupName);
            }
        }

        /// <summary>
        /// Remove a tab from its group
        /// </summary>
        public void RemoveTabFromGroup(string tabId)
        {
            try
            {
                var state = _stateManager.GetTabState(tabId);
                if (state != null)
                {
                    state.CustomProperties.Remove("Group");
                    _stateManager.SaveTabState(tabId, state);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing tab {TabId} from group", tabId);
            }
        }

        /// <summary>
        /// Get all tab groups
        /// </summary>
        public List<TabGroup> GetTabGroups()
        {
            lock (_lock)
            {
                return _tabGroups.Values.OrderBy(x => x.Name).ToList();
            }
        }

        /// <summary>
        /// Get the group a tab belongs to
        /// </summary>
        private string? GetTabGroup(string tabId)
        {
            var state = _stateManager.GetTabState(tabId);
            return state?.CustomProperties.TryGetValue("Group", out var group) == true
                ? group.ToString()
                : null;
        }

        /// <summary>
        /// Get all tab states
        /// </summary>
        private async Task<Dictionary<string, TabState>> GetAllTabStatesAsync()
        {
            // This is a placeholder - in a real implementation, you would need to
            // implement a way to get all tab states from your tab management system
            return new Dictionary<string, TabState>();
        }

        /// <summary>
        /// Check if a tab state matches search criteria
        /// </summary>
        private bool MatchesSearchCriteria(
            KeyValuePair<string, TabState> tabState,
            string searchTerm,
            TabSearchOptions options)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return true;
            }

            var state = tabState.Value;
            searchTerm = searchTerm.ToLowerInvariant();

            if (options.SearchInTitle && state.Title.ToLowerInvariant().Contains(searchTerm))
            {
                return true;
            }

            if (options.SearchInPath && state.Path.ToLowerInvariant().Contains(searchTerm))
            {
                return true;
            }

            if (options.SearchInGroups && GetTabGroup(tabState.Key)?.ToLowerInvariant().Contains(searchTerm) == true)
            {
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Options for tab search
    /// </summary>
    public class TabSearchOptions
    {
        public bool SearchInTitle { get; set; } = true;
        public bool SearchInPath { get; set; } = true;
        public bool SearchInGroups { get; set; } = true;
    }

    /// <summary>
    /// Result of a tab search
    /// </summary>
    public class TabSearchResult
    {
        public string TabId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public DateTime LastAccessed { get; set; }
        public string? Group { get; set; }
    }

    /// <summary>
    /// Represents a group of tabs
    /// </summary>
    public class TabGroup
    {
        public string Name { get; set; } = string.Empty;
        public string? Color { get; set; }
        public DateTime CreatedAt { get; set; }
    }
} 