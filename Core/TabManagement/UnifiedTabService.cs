using System;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Unified tab service that bridges TabModel (modern) and TabModel (legacy) 
    /// for compatibility with ChromeStyleTabControl while maintaining modern architecture.
    /// 
    /// This service acts as an adapter layer that:
    /// - Exposes ObservableCollection&lt;TabModel&gt; for ChromeStyleTabControl
    /// - Maintains internal ObservableCollection&lt;TabModel&gt; for modern services
    /// - Automatically synchronizes between the two collections
    /// - Handles proper disposal and memory management
    /// </summary>
    public class UnifiedTabService : IDisposable, INotifyPropertyChanged
    {
        #region Private Fields
        
        private readonly ITabManagerService _modernTabManager;
        private readonly ILogger<UnifiedTabService> _logger;
        private readonly ObservableCollection<TabModel> _legacyTabItems;
        private readonly ConcurrentDictionary<string, TabModelAdapter> _adapters;
        private bool _isDisposed;
        private bool _isSynchronizing; // Prevents circular updates
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new unified tab service
        /// </summary>
        /// <param name="modernTabManager">The modern tab manager service</param>
        /// <param name="logger">Logger for diagnostics</param>
        public UnifiedTabService(ITabManagerService modernTabManager, ILogger<UnifiedTabService> logger = null)
        {
            _modernTabManager = modernTabManager ?? throw new ArgumentNullException(nameof(modernTabManager));
            _logger = logger;
            _legacyTabItems = new ObservableCollection<TabModel>();
            _adapters = new ConcurrentDictionary<string, TabModelAdapter>();
            
            // Subscribe to modern tab manager events
            // No references to ModernTabManagerService directly; works with any ITabManagerService (TabManagerService primary)
            _modernTabManager.TabCreated += OnModernTabCreated;
            _modernTabManager.TabClosed += OnModernTabClosed;
            _modernTabManager.ActiveTabChanged += OnModernActiveTabChanged;
            _modernTabManager.TabModified += OnModernTabModified;
            _modernTabManager.TabsReordered += OnModernTabsReordered;
            
            // Subscribe to modern tabs collection changes
            if (_modernTabManager.Tabs != null)
            {
                _modernTabManager.Tabs.CollectionChanged += OnModernTabsCollectionChanged;
                
                // Initialize adapters for existing tabs
                InitializeAdaptersForExistingTabs();
            }
            
            _logger?.LogInformation("UnifiedTabService initialized");
        }
        
        #endregion

        #region Public Properties
        
        /// <summary>
        /// Legacy tab items collection for ChromeStyleTabControl binding
        /// </summary>
        public ObservableCollection<TabModel> LegacyTabItems => _legacyTabItems;
        
        /// <summary>
        /// Modern tab collection (read-only access)
        /// </summary>
        public ObservableCollection<TabModel> ModernTabs => _modernTabManager.Tabs;
        
        /// <summary>
        /// Currently active tab (modern)
        /// </summary>
        public TabModel ActiveTab => _modernTabManager.ActiveTab;
        
        /// <summary>
        /// Currently active tab adapter (legacy)
        /// </summary>
        public TabModel ActiveLegacyTab 
        {
            get
            {
                var activeTab = _modernTabManager.ActiveTab;
                if (activeTab != null && _adapters.TryGetValue(activeTab.Id, out var adapter))
                {
                    return adapter;
                }
                return null;
            }
        }
        
        /// <summary>
        /// Number of tabs
        /// </summary>
        public int TabCount => _modernTabManager.TabCount;
        
        /// <summary>
        /// Whether there are any tabs
        /// </summary>
        public bool HasTabs => _modernTabManager.HasTabs;
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Creates a new tab and returns both modern and legacy representations
        /// </summary>
        public async Task<(TabModel modern, TabModel legacy)> CreateTabAsync(string title, string path = null, TabCreationOptions options = null)
        {
            ThrowIfDisposed();
            
            try
            {
                var modernTab = await _modernTabManager.CreateTabAsync(title, path, options);
                var legacyTab = GetLegacyTabById(modernTab.Id);
                
                _logger?.LogDebug($"Created unified tab: {title} (ID: {modernTab.Id})");
                return (modernTab, legacyTab);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creating unified tab: {title}");
                throw;
            }
        }
        
        /// <summary>
        /// Closes a tab by modern TabModel
        /// </summary>
        public async Task<bool> CloseTabAsync(TabModel tab, bool force = false)
        {
            ThrowIfDisposed();
            return await _modernTabManager.CloseTabAsync(tab, force);
        }

        
        /// <summary>
        /// Gets the modern TabModel for a legacy TabModel ID
        /// </summary>
        public TabModel GetModernTabById(string tabId)
        {
            ThrowIfDisposed();
            return _modernTabManager.Tabs?.FirstOrDefault(t => t.Id == tabId);
        }
        
        /// <summary>
        /// Gets the legacy TabModel for a modern TabModel ID
        /// </summary>
        public TabModel GetLegacyTabById(string tabId)
        {
            ThrowIfDisposed();
            
            if (_adapters.TryGetValue(tabId, out var adapter))
            {
                return adapter;
            }
            
            return null;
        }
        
        /// <summary>
        /// Activates a tab by modern TabModel
        /// </summary>
        public async Task ActivateTabAsync(TabModel tab)
        {
            ThrowIfDisposed();
            await _modernTabManager.ActivateTabAsync(tab);
        }

        
        /// <summary>
        /// Reorders tabs
        /// </summary>
        public async Task MoveTabAsync(TabModel tab, int newIndex)
        {
            ThrowIfDisposed();
            await _modernTabManager.MoveTabAsync(tab, newIndex);
        }

        
        #endregion

        #region Private Methods
        
        /// <summary>
        /// Initializes adapters for tabs that already exist in the modern manager
        /// </summary>
        private void InitializeAdaptersForExistingTabs()
        {
            if (_modernTabManager.Tabs == null) return;
            
            foreach (var modernTab in _modernTabManager.Tabs)
            {
                CreateAdapterForModernTab(modernTab);
            }
        }
        
        /// <summary>
        /// Creates an adapter for a modern tab and adds it to collections
        /// </summary>
        private TabModelAdapter CreateAdapterForModernTab(TabModel modernTab)
        {
            if (_adapters.ContainsKey(modernTab.Id))
            {
                return _adapters[modernTab.Id];
            }
            
            var adapter = new TabModelAdapter(modernTab);
            _adapters[modernTab.Id] = adapter;
            
            // Add to legacy collection (this will trigger UI updates)
            if (!_legacyTabItems.Contains(adapter))
            {
                _legacyTabItems.Add(adapter);
            }
            
            return adapter;
        }
        
        /// <summary>
        /// Removes an adapter and cleans up
        /// </summary>
        private void RemoveAdapter(string tabId)
        {
            if (_adapters.TryRemove(tabId, out var adapter))
            {
                _legacyTabItems.Remove(adapter);
                adapter.Dispose();
            }
        }
        
        /// <summary>
        /// Synchronizes the legacy collection order with modern collection
        /// </summary>
        private void SynchronizeCollectionOrder()
        {
            if (_isSynchronizing) return;
            
            _isSynchronizing = true;
            try
            {
                // Reorder legacy collection to match modern collection
                var modernOrder = _modernTabManager.Tabs.ToList();
                for (int i = 0; i < modernOrder.Count; i++)
                {
                    var modernTab = modernOrder[i];
                    if (_adapters.TryGetValue(modernTab.Id, out var adapter))
                    {
                        var currentIndex = _legacyTabItems.IndexOf(adapter);
                        if (currentIndex != i && currentIndex >= 0)
                        {
                            _legacyTabItems.Move(currentIndex, i);
                        }
                    }
                }
            }
            finally
            {
                _isSynchronizing = false;
            }
        }
        
        /// <summary>
        /// Throws if the service is disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(UnifiedTabService));
            }
        }
        
        #endregion

        #region Event Handlers
        
        /// <summary>
        /// Handles modern tab creation
        /// </summary>
        private void OnModernTabCreated(object sender, TabEventArgs e)
        {
            if (_isSynchronizing) return;
            
            CreateAdapterForModernTab(e.Tab);
            OnPropertyChanged(nameof(TabCount));
            OnPropertyChanged(nameof(HasTabs));
        }
        
        /// <summary>
        /// Handles modern tab closure
        /// </summary>
        private void OnModernTabClosed(object sender, TabEventArgs e)
        {
            if (_isSynchronizing) return;
            
            RemoveAdapter(e.Tab.Id);
            OnPropertyChanged(nameof(TabCount));
            OnPropertyChanged(nameof(HasTabs));
        }
        
        /// <summary>
        /// Handles modern active tab changes
        /// </summary>
        private void OnModernActiveTabChanged(object sender, TabChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ActiveTab));
            OnPropertyChanged(nameof(ActiveLegacyTab));
        }
        
        /// <summary>
        /// Handles modern tab modifications
        /// </summary>
        private void OnModernTabModified(object sender, TabModifiedEventArgs e)
        {
            // Adapter will automatically sync the changes
        }
        
        /// <summary>
        /// Handles modern tab reordering
        /// </summary>
        private void OnModernTabsReordered(object sender, TabReorderedEventArgs e)
        {
            SynchronizeCollectionOrder();
        }
        
        /// <summary>
        /// Handles changes to the modern tabs collection
        /// </summary>
        private void OnModernTabsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            if (_isSynchronizing) return;
            
            switch (e.Action)
            {
                case NotifyCollectionChangedAction.Add:
                    foreach (TabModel newTab in e.NewItems)
                    {
                        CreateAdapterForModernTab(newTab);
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Remove:
                    foreach (TabModel oldTab in e.OldItems)
                    {
                        RemoveAdapter(oldTab.Id);
                    }
                    break;
                    
                case NotifyCollectionChangedAction.Move:
                    SynchronizeCollectionOrder();
                    break;
                    
                case NotifyCollectionChangedAction.Reset:
                    // Clear all adapters and recreate
                    foreach (var adapter in _adapters.Values)
                    {
                        adapter.Dispose();
                    }
                    _adapters.Clear();
                    _legacyTabItems.Clear();
                    InitializeAdaptersForExistingTabs();
                    break;
            }
        }
        
        #endregion

        #region INotifyPropertyChanged Implementation
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion

        #region IDisposable Implementation
        
        /// <summary>
        /// Disposes the unified tab service
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            
            try
            {
                // Unsubscribe from modern tab manager events
                if (_modernTabManager != null)
                {
                    _modernTabManager.TabCreated -= OnModernTabCreated;
                    _modernTabManager.TabClosed -= OnModernTabClosed;
                    _modernTabManager.ActiveTabChanged -= OnModernActiveTabChanged;
                    _modernTabManager.TabModified -= OnModernTabModified;
                    _modernTabManager.TabsReordered -= OnModernTabsReordered;
                    
                    if (_modernTabManager.Tabs != null)
                    {
                        _modernTabManager.Tabs.CollectionChanged -= OnModernTabsCollectionChanged;
                    }
                }
                
                // Dispose all adapters
                foreach (var adapter in _adapters.Values)
                {
                    adapter.Dispose();
                }
                _adapters.Clear();
                
                // Clear legacy collection
                _legacyTabItems.Clear();
                
                _logger?.LogInformation("UnifiedTabService disposed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error disposing UnifiedTabService");
            }
            
            GC.SuppressFinalize(this);
        }
        
        #endregion
    }
} 
