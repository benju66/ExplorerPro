using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Coordinates all tab management components
    /// </summary>
    public class TabManager
    {
        private readonly ILogger<TabManager> _logger;
        private readonly TabStateManager _stateManager;
        private readonly TabVirtualizationManager _virtualizationManager;
        private readonly TabSearchManager _searchManager;
        private readonly TabPreviewManager _previewManager;
        private readonly Dictionary<string, Tab> _activeTabs;
        private readonly object _lock = new object();

        public event EventHandler<TabEventArgs>? TabAdded;
        public event EventHandler<TabEventArgs>? TabRemoved;
        public event EventHandler<TabEventArgs>? TabActivated;
        public event EventHandler<TabEventArgs>? TabDeactivated;
        public event EventHandler<TabEventArgs>? TabStateChanged;

        public TabManager(
            ILogger<TabManager> logger,
            TabStateManager stateManager,
            TabVirtualizationManager virtualizationManager,
            TabSearchManager searchManager,
            TabPreviewManager previewManager)
        {
            _logger = logger;
            _stateManager = stateManager;
            _virtualizationManager = virtualizationManager;
            _searchManager = searchManager;
            _previewManager = previewManager;
            _activeTabs = new Dictionary<string, Tab>();
        }

        /// <summary>
        /// Add a new tab
        /// </summary>
        public async Task<Tab> AddTabAsync(string title, string path, bool isPinned = false)
        {
            try
            {
                var tabId = Guid.NewGuid().ToString();
                var tab = new Tab
                {
                    Id = tabId,
                    Title = title,
                    Path = path,
                    IsPinned = isPinned,
                    CreatedAt = DateTime.UtcNow,
                    LastAccessed = DateTime.UtcNow
                };

                // Save initial state
                var state = new TabState
                {
                    Title = title,
                    Path = path,
                    IsPinned = isPinned,
                    LastAccessed = tab.LastAccessed
                };
                _stateManager.SaveTabState(tabId, state);

                // Add to active tabs
                lock (_lock)
                {
                    _activeTabs[tabId] = tab;
                }

                // Register with virtualization manager
                _virtualizationManager.RegisterTabAccess(tabId);

                // Raise event
                TabAdded?.Invoke(this, new TabEventArgs(ConvertToTabModel(tab)));

                return tab;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding tab {Title}", title);
                throw;
            }
        }

        /// <summary>
        /// Remove a tab
        /// </summary>
        public void RemoveTab(string tabId)
        {
            try
            {
                Tab? tab = null;
                lock (_lock)
                {
                    if (_activeTabs.TryGetValue(tabId, out tab))
                    {
                        _activeTabs.Remove(tabId);
                    }
                }

                if (tab != null)
                {
                    // Clear state
                    _stateManager.ClearTabState(tabId);

                    // Clear preview
                    _previewManager.RemoveFromCache(tabId);

                    // Raise event
                    TabRemoved?.Invoke(this, new TabEventArgs(ConvertToTabModel(tab)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing tab {TabId}", tabId);
                throw;
            }
        }

        /// <summary>
        /// Activate a tab
        /// </summary>
        public void ActivateTab(string tabId)
        {
            try
            {
                Tab? tab = null;
                lock (_lock)
                {
                    if (_activeTabs.TryGetValue(tabId, out tab))
                    {
                        tab.LastAccessed = DateTime.UtcNow;
                        _virtualizationManager.RegisterTabAccess(tabId);

                        // Update state
                        var state = _stateManager.GetTabState(tabId);
                        if (state != null)
                        {
                            state.LastAccessed = tab.LastAccessed;
                            _stateManager.SaveTabState(tabId, state);
                        }
                    }
                }

                if (tab != null)
                {
                    TabActivated?.Invoke(this, new TabEventArgs(ConvertToTabModel(tab)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating tab {TabId}", tabId);
                throw;
            }
        }

        /// <summary>
        /// Deactivate a tab
        /// </summary>
        public void DeactivateTab(string tabId)
        {
            try
            {
                Tab? tab = null;
                lock (_lock)
                {
                    _activeTabs.TryGetValue(tabId, out tab);
                }

                if (tab != null)
                {
                    TabDeactivated?.Invoke(this, new TabEventArgs(ConvertToTabModel(tab)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating tab {TabId}", tabId);
                throw;
            }
        }

        /// <summary>
        /// Update tab state
        /// </summary>
        public void UpdateTabState(string tabId, Action<Tab> updateAction)
        {
            try
            {
                Tab? tab = null;
                lock (_lock)
                {
                    if (_activeTabs.TryGetValue(tabId, out tab))
                    {
                        updateAction(tab);

                        // Update state
                        var state = _stateManager.GetTabState(tabId);
                        if (state != null)
                        {
                            state.Title = tab.Title;
                            state.Path = tab.Path;
                            state.IsPinned = tab.IsPinned;
                            state.LastAccessed = tab.LastAccessed;
                            _stateManager.SaveTabState(tabId, state);
                        }
                    }
                }

                if (tab != null)
                {
                    TabStateChanged?.Invoke(this, new TabEventArgs(ConvertToTabModel(tab)));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating tab state {TabId}", tabId);
                throw;
            }
        }

        /// <summary>
        /// Get all active tabs
        /// </summary>
        public List<Tab> GetActiveTabs()
        {
            lock (_lock)
            {
                return _activeTabs.Values.ToList();
            }
        }

        /// <summary>
        /// Get a tab by ID
        /// </summary>
        public Tab? GetTab(string tabId)
        {
            lock (_lock)
            {
                return _activeTabs.TryGetValue(tabId, out var tab) ? tab : null;
            }
        }

        /// <summary>
        /// Search for tabs
        /// </summary>
        public Task<List<TabSearchResult>> SearchTabsAsync(string searchTerm, TabSearchOptions? options = null)
        {
            return _searchManager.SearchTabsAsync(searchTerm, options);
        }

        /// <summary>
        /// Get a tab preview
        /// </summary>
        public Task<TabPreview?> GetTabPreviewAsync(string tabId)
        {
            return _previewManager.GetPreviewAsync(tabId);
        }

        /// <summary>
        /// Get memory statistics
        /// </summary>
        public TabMemoryStats GetMemoryStats()
        {
            return _virtualizationManager.GetMemoryStats();
        }

        /// <summary>
        /// Converts a Tab to TabModel for event args
        /// </summary>
        private TabModel ConvertToTabModel(Tab tab)
        {
            return new TabModel(tab.Title, tab.Path)
            {
                Id = tab.Id,
                IsPinned = tab.IsPinned
            };
        }
    }

    /// <summary>
    /// Represents a tab
    /// </summary>
    public class Tab
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime LastAccessed { get; set; }
    }


} 