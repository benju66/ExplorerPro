using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Main implementation of tab management service.
    /// Centralizes all tab operations and provides a clean API for tab manipulation.
    /// </summary>
    public class TabManagerService : ITabManagerService
    {
        #region Private Fields
        
        private readonly ILogger<TabManagerService> _logger;
        private readonly ObservableCollection<TabModel> _tabs;
        private TabModel _activeTab;
        private bool _isDisposed;
        private readonly object _lockObject = new object();
        
        #endregion

        #region Constructor
        
        public TabManagerService(ILogger<TabManagerService> logger = null)
        {
            _logger = logger;
            _tabs = new ObservableCollection<TabModel>();
            
            _logger?.LogInformation("TabManagerService initialized");
        }
        
        #endregion

        #region ITabManagerService Properties
        
        public ObservableCollection<TabModel> Tabs => _tabs;
        
        public TabModel ActiveTab
        {
            get => _activeTab;
            set
            {
                if (_activeTab != value)
                {
                    var oldTab = _activeTab;
                    var oldIndex = oldTab != null ? _tabs.IndexOf(oldTab) : -1;
                    var newIndex = value != null ? _tabs.IndexOf(value) : -1;
                    
                    // Deactivate old tab
                    if (oldTab != null)
                    {
                        oldTab.Deactivate();
                    }
                    
                    _activeTab = value;
                    
                    // Activate new tab
                    if (_activeTab != null)
                    {
                        _activeTab.Activate();
                    }
                    
                    ActiveTabChanged?.Invoke(this, new TabChangedEventArgs(oldTab, _activeTab, oldIndex, newIndex));
                    _logger?.LogDebug($"Active tab changed from '{oldTab?.Title}' to '{_activeTab?.Title}'");
                }
            }
        }
        
        public int TabCount => _tabs.Count;
        
        public bool HasTabs => _tabs.Count > 0;
        
        #endregion

        #region Events
        
        public event EventHandler<TabEventArgs> TabCreated;
        public event EventHandler<TabEventArgs> TabClosed;
        public event EventHandler<TabChangedEventArgs> ActiveTabChanged;
        public event EventHandler<TabModifiedEventArgs> TabModified;
        public event EventHandler<TabReorderedEventArgs> TabsReordered;
        public event EventHandler<TabCollectionChangedEventArgs> TabCollectionReordered;
        
        #endregion

        #region Core Tab Operations
        
        public async Task<TabModel> CreateTabAsync(string title, string path = null, TabCreationOptions options = null)
        {
            await Task.Yield(); // Make it properly async
            
            lock (_lockObject)
            {
                if (_isDisposed) throw new ObjectDisposedException(nameof(TabManagerService));
                
                options ??= new TabCreationOptions();
                
                var tab = new TabModel(title, path)
                {
                    IsPinned = options.IsPinned,
                    Content = options.Content
                };
                
                if (options.CustomColor.HasValue)
                {
                    tab.CustomColor = options.CustomColor.Value;
                }
                
                // Determine insertion position
                int insertIndex;
                if (options.InsertAtIndex.HasValue)
                {
                    insertIndex = Math.Max(0, Math.Min(options.InsertAtIndex.Value, _tabs.Count));
                }
                else if (tab.IsPinned)
                {
                    // Insert at end of pinned tabs
                    insertIndex = GetPinnedTabs().Count();
                }
                else
                {
                    // Insert at end
                    insertIndex = _tabs.Count;
                }
                
                _tabs.Insert(insertIndex, tab);
                
                // Wire up property changed events for tab modifications
                tab.PropertyChanged += Tab_PropertyChanged;
                
                if (options.MakeActive)
                {
                    ActiveTab = tab;
                }
                
                TabCreated?.Invoke(this, new TabEventArgs(tab, insertIndex));
                _logger?.LogInformation($"Created tab '{tab.Title}' at index {insertIndex}");
                
                return tab;
            }
        }
        
        public async Task<bool> CloseTabAsync(TabModel tab, bool force = false)
        {
            await Task.Yield();
            
            lock (_lockObject)
            {
                if (_isDisposed) return false;
                if (tab == null || !_tabs.Contains(tab)) return false;
                if (!force && !CanCloseTab(tab)) return false;
                
                var index = _tabs.IndexOf(tab);
                
                // If this is the active tab, activate another tab
                if (_activeTab == tab && _tabs.Count > 1)
                {
                    var nextTab = GetNextTabToActivate(tab);
                    ActiveTab = nextTab;
                }
                else if (_tabs.Count == 1)
                {
                    ActiveTab = null;
                }
                
                // Remove from collection
                _tabs.Remove(tab);
                
                // Unwire events
                tab.PropertyChanged -= Tab_PropertyChanged;
                
                // Dispose tab
                tab.Dispose();
                
                TabClosed?.Invoke(this, new TabEventArgs(tab, index));
                _logger?.LogInformation($"Closed tab '{tab.Title}' from index {index}");
                
                return true;
            }
        }
        
        public async Task<bool> CloseTabAsync(int index, bool force = false)
        {
            var tab = GetTabAt(index);
            return tab != null && await CloseTabAsync(tab, force);
        }
        
        public async Task<TabModel> DuplicateTabAsync(TabModel tab)
        {
            await Task.Yield();
            
            if (tab == null || !_tabs.Contains(tab)) return null;
            
            var clonedTab = tab.Clone();
            clonedTab.Title += " (Copy)";
            
            var options = new TabCreationOptions
            {
                IsPinned = false, // Duplicated tabs are not pinned by default
                CustomColor = tab.CustomColor,
                MakeActive = true,
                InsertAtIndex = _tabs.IndexOf(tab) + 1
            };
            
            return await CreateTabAsync(clonedTab.Title, clonedTab.Path, options);
        }
        
        public async Task ActivateTabAsync(TabModel tab)
        {
            await Task.Yield();
            
            if (tab != null && _tabs.Contains(tab))
            {
                ActiveTab = tab;
            }
        }
        
        public async Task ActivateTabAsync(int index)
        {
            await Task.Yield();
            
            var tab = GetTabAt(index);
            if (tab != null)
            {
                ActiveTab = tab;
            }
        }
        
        #endregion

        #region Tab Customization
        
        public async Task SetTabColorAsync(TabModel tab, Color color)
        {
            await Task.Yield();
            
            if (tab != null && _tabs.Contains(tab))
            {
                var oldColor = tab.CustomColor;
                tab.CustomColor = color;
                _logger?.LogDebug($"Changed tab '{tab.Title}' color from {oldColor} to {color}");
            }
        }
        
        public async Task ClearTabColorAsync(TabModel tab)
        {
            await Task.Yield();
            
            if (tab != null && _tabs.Contains(tab))
            {
                tab.ClearCustomColor();
                _logger?.LogDebug($"Cleared custom color from tab '{tab.Title}'");
            }
        }
        
        public async Task SetTabPinnedAsync(TabModel tab, bool isPinned)
        {
            await Task.Yield();
            
            if (tab != null && _tabs.Contains(tab) && tab.IsPinned != isPinned)
            {
                tab.IsPinned = isPinned;
                
                // Reorganize tabs to maintain pinned-first order
                await ReorganizeTabsAsync();
                
                _logger?.LogDebug($"Tab '{tab.Title}' pinned status changed to {isPinned}");
            }
        }
        
        public async Task RenameTabAsync(TabModel tab, string newTitle)
        {
            await Task.Yield();
            
            if (tab != null && _tabs.Contains(tab) && !string.IsNullOrWhiteSpace(newTitle))
            {
                var oldTitle = tab.Title;
                tab.Title = newTitle.Trim();
                _logger?.LogDebug($"Renamed tab from '{oldTitle}' to '{newTitle}'");
            }
        }
        
        #endregion

        #region Tab Organization
        
        public async Task MoveTabAsync(TabModel tab, int newIndex)
        {
            await Task.Yield();
            
            if (tab == null || !_tabs.Contains(tab)) return;
            
            var oldIndex = _tabs.IndexOf(tab);
            newIndex = Math.Max(0, Math.Min(newIndex, _tabs.Count - 1));
            
            if (oldIndex != newIndex)
            {
                _tabs.RemoveAt(oldIndex);
                _tabs.Insert(newIndex, tab);
                
                TabsReordered?.Invoke(this, new TabReorderedEventArgs(tab, oldIndex, newIndex));
                _logger?.LogDebug($"Moved tab '{tab.Title}' from index {oldIndex} to {newIndex}");
            }
        }
        
        public async Task MoveTabAsync(int fromIndex, int toIndex)
        {
            var tab = GetTabAt(fromIndex);
            if (tab != null)
            {
                await MoveTabAsync(tab, toIndex);
            }
        }
        
        public IEnumerable<TabModel> GetPinnedTabs()
        {
            return _tabs.Where(t => t.IsPinned);
        }
        
        public IEnumerable<TabModel> GetUnpinnedTabs()
        {
            return _tabs.Where(t => !t.IsPinned);
        }
        
        public async Task ReorganizeTabsAsync()
        {
            await Task.Yield();
            
            lock (_lockObject)
            {
                var pinnedTabs = GetPinnedTabs().ToList();
                var unpinnedTabs = GetUnpinnedTabs().ToList();
                
                _tabs.Clear();
                
                // Add pinned tabs first
                foreach (var tab in pinnedTabs)
                {
                    _tabs.Add(tab);
                }
                
                // Add unpinned tabs after
                foreach (var tab in unpinnedTabs)
                {
                    _tabs.Add(tab);
                }
                
                _logger?.LogDebug($"Reorganized tabs: {pinnedTabs.Count} pinned, {unpinnedTabs.Count} unpinned");
                
                // Fire the TabCollectionReordered event to notify UI
                var reorderedTabs = _tabs.ToList();
                TabCollectionReordered?.Invoke(this, new TabCollectionChangedEventArgs(reorderedTabs, "Pin state reorganization"));
            }
        }
        
        #endregion

        #region Navigation
        
        public async Task NavigateToNextTabAsync()
        {
            await Task.Yield();
            
            if (_activeTab == null || _tabs.Count <= 1) return;
            
            var currentIndex = _tabs.IndexOf(_activeTab);
            var nextIndex = (currentIndex + 1) % _tabs.Count;
            ActiveTab = _tabs[nextIndex];
        }
        
        public async Task NavigateToPreviousTabAsync()
        {
            await Task.Yield();
            
            if (_activeTab == null || _tabs.Count <= 1) return;
            
            var currentIndex = _tabs.IndexOf(_activeTab);
            var previousIndex = (currentIndex - 1 + _tabs.Count) % _tabs.Count;
            ActiveTab = _tabs[previousIndex];
        }
        
        public async Task NavigateToTabAsync(int index)
        {
            await ActivateTabAsync(index);
        }
        
        #endregion

        #region Validation
        
        public bool CanCloseTab(TabModel tab)
        {
            if (tab == null) return false;
            
            // Don't allow closing the last tab
            if (_tabs.Count <= 1) return false;
            
            // Check if tab allows closing
            return tab.CanClose;
        }
        
        public bool CanReorderTabs()
        {
            return _tabs.Count > 1;
        }
        
        public TabModel GetTabAt(int index)
        {
            return index >= 0 && index < _tabs.Count ? _tabs[index] : null;
        }
        
        public int GetTabIndex(TabModel tab)
        {
            return tab != null ? _tabs.IndexOf(tab) : -1;
        }
        
        #endregion

        #region Private Methods

        /// <summary>
        /// Throws ObjectDisposedException if the service has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(TabManagerService), "Cannot perform operation on disposed TabManagerService");
            }
        }
        
        private void Tab_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is TabModel tab)
            {
                TabModified?.Invoke(this, new TabModifiedEventArgs(tab, e.PropertyName, null, null));
            }
        }
        
        private TabModel GetNextTabToActivate(TabModel closingTab)
        {
            var index = _tabs.IndexOf(closingTab);
            
            // Try next tab first
            if (index + 1 < _tabs.Count)
            {
                return _tabs[index + 1];
            }
            
            // Try previous tab
            if (index - 1 >= 0)
            {
                return _tabs[index - 1];
            }
            
            // Return any other tab
            return _tabs.FirstOrDefault(t => t != closingTab);
        }
        
        #endregion

        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                _logger?.LogInformation("Disposing TabManagerService - starting cleanup");
                
                lock (_lockObject)
                {
                    // Set disposed flag first to prevent new operations
                    _isDisposed = true;
                    
                    // Unsubscribe all event handlers systematically
                    _logger?.LogDebug("Unsubscribing event handlers");
                    UnsubscribeAllEventHandlers();
                    
                    // Clear all collections
                    _logger?.LogDebug("Clearing collections");
                    ClearAllCollections();
                    
                    // Clear references
                    _logger?.LogDebug("Clearing references");
                    ClearAllReferences();
                    
                    _logger?.LogInformation("TabManagerService disposal completed successfully");
                }
            }
        }
        
        /// <summary>
        /// Unsubscribes all event handlers to prevent memory leaks
        /// </summary>
        private void UnsubscribeAllEventHandlers()
        {
            try
            {
                // Unsubscribe from all tab PropertyChanged events
                foreach (var tab in _tabs.ToList())
                {
                    try
                    {
                        tab.PropertyChanged -= Tab_PropertyChanged;
                        _logger?.LogDebug($"Unsubscribed from PropertyChanged for tab: {tab.Title}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"Error unsubscribing from tab PropertyChanged: {tab.Title}");
                    }
                }
                
                // Clear all public events by setting to null
                TabCreated = null;
                TabClosed = null;
                ActiveTabChanged = null;
                TabModified = null;
                TabsReordered = null;
                
                _logger?.LogDebug("All event handlers unsubscribed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during event handler cleanup");
            }
        }
        
        /// <summary>
        /// Clears all collections and disposes contained items
        /// </summary>
        private void ClearAllCollections()
        {
            try
            {
                // Dispose all tabs individually
                var tabsCopy = _tabs.ToList();
                foreach (var tab in tabsCopy)
                {
                    try
                    {
                        tab.Dispose();
                        _logger?.LogDebug($"Disposed tab: {tab.Title}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"Error disposing tab: {tab.Title}");
                    }
                }
                
                // Clear the observable collection
                _tabs.Clear();
                _logger?.LogDebug($"Cleared {tabsCopy.Count} tabs from collection");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during collection cleanup");
            }
        }
        
        /// <summary>
        /// Clears all object references
        /// </summary>
        private void ClearAllReferences()
        {
            try
            {
                // Clear active tab reference
                _activeTab = null;
                
                _logger?.LogDebug("All references cleared");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during reference cleanup");
            }
        }
        
        #endregion
    }
} 