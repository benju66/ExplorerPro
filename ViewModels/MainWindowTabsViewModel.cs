using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.Core.Threading;
using ExplorerPro.Models;
using ExplorerPro.Commands;
using ExplorerPro.Core.Commands;

namespace ExplorerPro.ViewModels
{
    /// <summary>
    /// ViewModel for the main window tab system using proper MVVM architecture.
    /// This replaces the scattered tab logic in MainWindow.xaml.cs
    /// </summary>
    public class MainWindowTabsViewModel : INotifyPropertyChanged, IDisposable, IThreadSafeOperationsConsumer
    {
        #region Private Fields
        
        private readonly ITabManagerService _tabManager;
        private readonly ILogger<MainWindowTabsViewModel> _logger;
        private ThreadSafeTabOperations _threadSafeOperations;
        private bool _isDisposed;
        
        #endregion

        #region Constructor
        
        public MainWindowTabsViewModel(ITabManagerService tabManager, ILogger<MainWindowTabsViewModel> logger = null)
        {
            _tabManager = tabManager ?? throw new ArgumentNullException(nameof(tabManager));
            _logger = logger;
            
            // Wire up service events
            _tabManager.TabCreated += OnTabCreated;
            _tabManager.TabClosed += OnTabClosed;
            _tabManager.ActiveTabChanged += OnActiveTabChanged;
            _tabManager.TabModified += OnTabModified;
            _tabManager.TabsReordered += OnTabsReordered;
            
            // Initialize commands
            InitializeCommands();
            
            _logger?.LogInformation("MainWindowTabsViewModel initialized");
        }
        
        #endregion

        #region Public Properties
        
        /// <summary>
        /// Collection of all tabs
        /// </summary>
        public ObservableCollection<TabModel> Tabs
        {
            get
            {
                ThrowIfDisposed();
                return _tabManager.Tabs;
            }
        }
        
        /// <summary>
        /// Currently active tab
        /// </summary>
        public TabModel ActiveTab
        {
            get
            {
                ThrowIfDisposed();
                return _tabManager.ActiveTab;
            }
            set
            {
                ThrowIfDisposed();
                if (_tabManager.ActiveTab != value)
                {
                    _tabManager.ActiveTab = value;
                    OnPropertyChanged();
                }
            }
        }
        
        /// <summary>
        /// Number of tabs
        /// </summary>
        public int TabCount => _tabManager.TabCount;
        
        /// <summary>
        /// Whether there are any tabs
        /// </summary>
        public bool HasTabs => _tabManager.HasTabs;
        
        /// <summary>
        /// Whether multiple tabs exist (for UI visibility)
        /// </summary>
        public bool HasMultipleTabs => _tabManager.TabCount > 1;
        
        /// <summary>
        /// Whether tabs can be closed (always true except for last tab)
        /// </summary>
        public bool CanCloseTabs => _tabManager.TabCount > 1;
        
        /// <summary>
        /// Whether tabs can be reordered
        /// </summary>
        public bool CanReorderTabs => _tabManager.CanReorderTabs();
        
        #endregion

        #region Commands
        
        /// <summary>
        /// Modern async command to create a new tab
        /// </summary>
        public IAsyncCommand NewTabCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to close a tab
        /// </summary>
        public IAsyncCommand<TabModel> CloseTabCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to duplicate a tab
        /// </summary>
        public IAsyncCommand<TabModel> DuplicateTabCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to rename a tab
        /// </summary>
        public IAsyncCommand<TabModel> RenameTabCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to change tab color
        /// </summary>
        public IAsyncCommand<TabModel> ChangeTabColorCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to clear tab color
        /// </summary>
        public IAsyncCommand<TabModel> ClearTabColorCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to pin/unpin a tab
        /// </summary>
        public IAsyncCommand<TabModel> TogglePinTabCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to navigate to next tab
        /// </summary>
        public IAsyncCommand NextTabCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to navigate to previous tab
        /// </summary>
        public IAsyncCommand PreviousTabCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to move tab
        /// </summary>
        public IAsyncCommand<(TabModel tab, int newIndex)> MoveTabCommand { get; private set; }
        
        /// <summary>
        /// Modern async command to activate a specific tab
        /// </summary>
        public IAsyncCommand<TabModel> ActivateTabCommand { get; private set; }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Creates a new tab with the specified parameters
        /// </summary>
        public async Task<TabModel> CreateTabAsync(string title, string path = null, TabCreationOptions options = null)
        {
            ThrowIfDisposed();
            
            try
            {
                var tab = await _tabManager.CreateTabAsync(title, path, options);
                _logger?.LogDebug($"Created tab via ViewModel: {title}");
                return tab;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creating tab: {title}");
                throw;
            }
        }
        
        /// <summary>
        /// Closes the specified tab
        /// </summary>
        public async Task<bool> CloseTabAsync(TabModel tab, bool force = false)
        {
            ThrowIfDisposed();
            
            try
            {
                var result = await _tabManager.CloseTabAsync(tab, force);
                _logger?.LogDebug($"Closed tab via ViewModel: {tab?.Title}");
                return result;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error closing tab: {tab?.Title}");
                throw;
            }
        }
        
        /// <summary>
        /// Duplicates the specified tab
        /// </summary>
        public async Task<TabModel> DuplicateTabAsync(TabModel tab)
        {
            ThrowIfDisposed();
            
            try
            {
                var newTab = await _tabManager.DuplicateTabAsync(tab);
                _logger?.LogDebug($"Duplicated tab via ViewModel: {tab?.Title}");
                return newTab;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error duplicating tab: {tab?.Title}");
                throw;
            }
        }
        
        /// <summary>
        /// Sets the color of the specified tab
        /// </summary>
        public async Task SetTabColorAsync(TabModel tab, Color color)
        {
            ThrowIfDisposed();
            
            try
            {
                await _tabManager.SetTabColorAsync(tab, color);
                _logger?.LogDebug($"Set tab color via ViewModel: {tab?.Title}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error setting tab color: {tab?.Title}");
                throw;
            }
        }
        
        /// <summary>
        /// Clears the color of the specified tab
        /// </summary>
        public async Task ClearTabColorAsync(TabModel tab)
        {
            ThrowIfDisposed();
            
            try
            {
                await _tabManager.ClearTabColorAsync(tab);
                _logger?.LogDebug($"Cleared tab color via ViewModel: {tab?.Title}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error clearing tab color: {tab?.Title}");
                throw;
            }
        }
        
        /// <summary>
        /// Toggles the pinned state of the specified tab
        /// </summary>
        public async Task ToggleTabPinnedAsync(TabModel tab)
        {
            ThrowIfDisposed();
            
            try
            {
                await _tabManager.SetTabPinnedAsync(tab, !tab.IsPinned);
                _logger?.LogDebug($"Toggled tab pinned via ViewModel: {tab?.Title}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error toggling tab pinned: {tab?.Title}");
                throw;
            }
        }
        
        /// <summary>
        /// Renames the specified tab
        /// </summary>
        public async Task RenameTabAsync(TabModel tab, string newTitle)
        {
            ThrowIfDisposed();
            
            try
            {
                await _tabManager.RenameTabAsync(tab, newTitle);
                _logger?.LogDebug($"Renamed tab via ViewModel: {tab?.Title} -> {newTitle}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error renaming tab: {tab?.Title}");
                throw;
            }
        }
        
        /// <summary>
        /// Moves the specified tab to a new index
        /// </summary>
        public async Task MoveTabAsync(TabModel tab, int newIndex)
        {
            ThrowIfDisposed();
            
            try
            {
                await _tabManager.MoveTabAsync(tab, newIndex);
                _logger?.LogDebug($"Moved tab via ViewModel: {tab?.Title} to index {newIndex}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error moving tab: {tab?.Title}");
                throw;
            }
        }
        
        /// <summary>
        /// Navigates to the next tab
        /// </summary>
        public async Task NavigateToNextTabAsync()
        {
            ThrowIfDisposed();
            
            try
            {
                await _tabManager.NavigateToNextTabAsync();
                _logger?.LogDebug("Navigated to next tab via ViewModel");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating to next tab");
                throw;
            }
        }
        
        /// <summary>
        /// Navigates to the previous tab
        /// </summary>
        public async Task NavigateToPreviousTabAsync()
        {
            ThrowIfDisposed();
            
            try
            {
                await _tabManager.NavigateToPreviousTabAsync();
                _logger?.LogDebug("Navigated to previous tab via ViewModel");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error navigating to previous tab");
                throw;
            }
        }
        
        #endregion

        #region Private Methods

        /// <summary>
        /// Throws ObjectDisposedException if the view model has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
            {
                throw new ObjectDisposedException(nameof(MainWindowTabsViewModel), "Cannot perform operation on disposed MainWindowTabsViewModel");
            }
        }
        
        /// <summary>
        /// Initializes all commands
        /// </summary>
        private void InitializeCommands()
        {
            // Use modern command system with proper async support
            NewTabCommand = ModernTabCommandSystem.CreateNewTabCommand(_tabManager, _logger);
            
            CloseTabCommand = ModernTabCommandSystem.CreateCloseTabCommand(_tabManager, _logger);
            
            DuplicateTabCommand = ModernTabCommandSystem.CreateDuplicateTabCommand(_tabManager, _logger);
            
            RenameTabCommand = ModernTabCommandSystem.CreateRenameTabCommand(_tabManager, _logger);
            
            ChangeTabColorCommand = ModernTabCommandSystem.CreateChangeColorCommand(_tabManager, _logger);
            
            ClearTabColorCommand = ModernTabCommandSystem.CreateClearColorCommand(_tabManager, _logger);
            
            TogglePinTabCommand = ModernTabCommandSystem.CreateTogglePinCommand(_tabManager, _logger);
            
            NextTabCommand = ModernTabCommandSystem.CreateNextTabCommand(_tabManager, _logger);
            
            PreviousTabCommand = ModernTabCommandSystem.CreatePreviousTabCommand(_tabManager, _logger);
            
            MoveTabCommand = ModernTabCommandSystem.CreateMoveTabCommand(_tabManager, _logger);
            
            ActivateTabCommand = new AsyncRelayCommand<TabModel>(
                async tab => await _tabManager.ActivateTabAsync(tab),
                tab => tab != null && Tabs.Contains(tab),
                _logger
            );
        }

        #endregion

        #region Event Handlers
        
        /// <summary>
        /// Handles tab created events from the service
        /// </summary>
        private void OnTabCreated(object? sender, TabEventArgs e)
        {
            OnPropertyChanged(nameof(TabCount));
            OnPropertyChanged(nameof(HasTabs));
            OnPropertyChanged(nameof(HasMultipleTabs));
            OnPropertyChanged(nameof(CanCloseTabs));
            OnPropertyChanged(nameof(CanReorderTabs));
        }
        
        /// <summary>
        /// Handles tab closed events from the service
        /// </summary>
        private void OnTabClosed(object? sender, TabEventArgs e)
        {
            OnPropertyChanged(nameof(TabCount));
            OnPropertyChanged(nameof(HasTabs));
            OnPropertyChanged(nameof(HasMultipleTabs));
            OnPropertyChanged(nameof(CanCloseTabs));
            OnPropertyChanged(nameof(CanReorderTabs));
        }
        
        /// <summary>
        /// Handles active tab changed events from the service
        /// </summary>
        private void OnActiveTabChanged(object? sender, TabChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ActiveTab));
        }
        
        /// <summary>
        /// Handles tab modified events from the service
        /// </summary>
        private void OnTabModified(object? sender, TabModifiedEventArgs e)
        {
            // Properties on the tab model itself will raise their own change notifications
        }
        
        /// <summary>
        /// Handles tab reordered events from the service
        /// </summary>
        private void OnTabsReordered(object? sender, TabReorderedEventArgs e)
        {
            OnPropertyChanged(nameof(CanReorderTabs));
        }
        
        #endregion

        #region INotifyPropertyChanged Implementation
        
        public event PropertyChangedEventHandler? PropertyChanged;
        
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion

        #region IThreadSafeOperationsConsumer Implementation
        
        /// <summary>
        /// Sets the thread-safe operations manager
        /// </summary>
        public void SetThreadSafeOperations(ThreadSafeTabOperations threadSafeOperations)
        {
            _threadSafeOperations = threadSafeOperations;
            _logger?.LogDebug("Thread-safe operations manager set in MainWindowTabsViewModel");
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
                _logger?.LogInformation("Disposing MainWindowTabsViewModel - starting cleanup");
                
                // Set disposed flag first to prevent operations during cleanup
                _isDisposed = true;
                
                // Unsubscribe from TabManagerService events
                _logger?.LogDebug("Unsubscribing from TabManagerService events");
                UnsubscribeFromTabManagerEvents();
                
                // Clear command references
                _logger?.LogDebug("Clearing command references");
                ClearCommandReferences();
                
                // Set all properties to null
                _logger?.LogDebug("Clearing property references");
                ClearPropertyReferences();
                
                _logger?.LogInformation("MainWindowTabsViewModel disposal completed successfully");
            }
        }
        
        /// <summary>
        /// Unsubscribes from all TabManagerService events
        /// </summary>
        private void UnsubscribeFromTabManagerEvents()
        {
            try
            {
                if (_tabManager != null)
                {
                    _tabManager.TabCreated -= OnTabCreated;
                    _logger?.LogDebug("Unsubscribed from TabCreated event");
                    
                    _tabManager.TabClosed -= OnTabClosed;
                    _logger?.LogDebug("Unsubscribed from TabClosed event");
                    
                    _tabManager.ActiveTabChanged -= OnActiveTabChanged;
                    _logger?.LogDebug("Unsubscribed from ActiveTabChanged event");
                    
                    _tabManager.TabModified -= OnTabModified;
                    _logger?.LogDebug("Unsubscribed from TabModified event");
                    
                    _tabManager.TabsReordered -= OnTabsReordered;
                    _logger?.LogDebug("Unsubscribed from TabsReordered event");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error unsubscribing from TabManagerService events");
            }
        }
        
        /// <summary>
        /// Clears all command references to prevent memory leaks
        /// </summary>
        private void ClearCommandReferences()
        {
            try
            {
                NewTabCommand = null;
                CloseTabCommand = null;
                DuplicateTabCommand = null;
                RenameTabCommand = null;
                ChangeTabColorCommand = null;
                ClearTabColorCommand = null;
                TogglePinTabCommand = null;
                NextTabCommand = null;
                PreviousTabCommand = null;
                MoveTabCommand = null;
                ActivateTabCommand = null;
                
                _logger?.LogDebug("All command references cleared");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing command references");
            }
        }
        
        /// <summary>
        /// Clears all property references
        /// </summary>
        private void ClearPropertyReferences()
        {
            try
            {
                // Clear PropertyChanged event subscribers
                PropertyChanged = null;
                
                _logger?.LogDebug("All property references cleared");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error clearing property references");
            }
        }
        
        #endregion
    }
} 