using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.Models;
using ExplorerPro.Commands;

namespace ExplorerPro.ViewModels
{
    /// <summary>
    /// ViewModel for the main window tab system using proper MVVM architecture.
    /// This replaces the scattered tab logic in MainWindow.xaml.cs
    /// </summary>
    public class MainWindowTabsViewModel : INotifyPropertyChanged, IDisposable
    {
        #region Private Fields
        
        private readonly ITabManagerService _tabManager;
        private readonly ILogger<MainWindowTabsViewModel> _logger;
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
        public ObservableCollection<TabModel> Tabs => _tabManager.Tabs;
        
        /// <summary>
        /// Currently active tab
        /// </summary>
        public TabModel ActiveTab
        {
            get => _tabManager.ActiveTab;
            set
            {
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
        /// Command to create a new tab
        /// </summary>
        public ICommand NewTabCommand { get; private set; }
        
        /// <summary>
        /// Command to close a tab
        /// </summary>
        public ICommand CloseTabCommand { get; private set; }
        
        /// <summary>
        /// Command to duplicate a tab
        /// </summary>
        public ICommand DuplicateTabCommand { get; private set; }
        
        /// <summary>
        /// Command to rename a tab
        /// </summary>
        public ICommand RenameTabCommand { get; private set; }
        
        /// <summary>
        /// Command to change tab color
        /// </summary>
        public ICommand ChangeTabColorCommand { get; private set; }
        
        /// <summary>
        /// Command to clear tab color
        /// </summary>
        public ICommand ClearTabColorCommand { get; private set; }
        
        /// <summary>
        /// Command to pin/unpin a tab
        /// </summary>
        public ICommand TogglePinTabCommand { get; private set; }
        
        /// <summary>
        /// Command to navigate to next tab
        /// </summary>
        public ICommand NextTabCommand { get; private set; }
        
        /// <summary>
        /// Command to navigate to previous tab
        /// </summary>
        public ICommand PreviousTabCommand { get; private set; }
        
        /// <summary>
        /// Command to move tab
        /// </summary>
        public ICommand MoveTabCommand { get; private set; }
        
        /// <summary>
        /// Command to activate a specific tab
        /// </summary>
        public ICommand ActivateTabCommand { get; private set; }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Creates a new tab with the specified parameters
        /// </summary>
        public async Task<TabModel> CreateTabAsync(string title, string path = null, TabCreationOptions options = null)
        {
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
        /// Initializes all commands
        /// </summary>
        private void InitializeCommands()
        {
            NewTabCommand = new RelayCommand(async () => await CreateTabAsync("New Tab"));
            
            CloseTabCommand = new RelayCommand<TabModel>(async tab => 
            {
                if (tab != null) await CloseTabAsync(tab);
            });
            
            DuplicateTabCommand = new RelayCommand<TabModel>(async tab => 
            {
                if (tab != null) await DuplicateTabAsync(tab);
            });
            
            RenameTabCommand = new RelayCommand<TabModel>(async tab => 
            {
                if (tab != null)
                {
                    // This would typically open a dialog
                    // For now, just add " (renamed)" to demonstrate
                    await RenameTabAsync(tab, tab.Title + " (renamed)");
                }
            });
            
            ChangeTabColorCommand = new RelayCommand<TabModel>(async tab => 
            {
                if (tab != null)
                {
                    // This would typically open a color picker
                    // For now, just set to a default color
                    await SetTabColorAsync(tab, Colors.LightBlue);
                }
            });
            
            ClearTabColorCommand = new RelayCommand<TabModel>(async tab => 
            {
                if (tab != null) await ClearTabColorAsync(tab);
            });
            
            TogglePinTabCommand = new RelayCommand<TabModel>(async tab => 
            {
                if (tab != null) await ToggleTabPinnedAsync(tab);
            });
            
            NextTabCommand = new RelayCommand(async () => await NavigateToNextTabAsync());
            
            PreviousTabCommand = new RelayCommand(async () => await NavigateToPreviousTabAsync());
            
            MoveTabCommand = new RelayCommand<TabModel>(async tab => 
            {
                if (tab != null)
                {
                    // This would typically be called with specific index
                    // For demo, move to end
                    await MoveTabAsync(tab, TabCount - 1);
                }
            });
            
            ActivateTabCommand = new RelayCommand<TabModel>(tab => 
            {
                if (tab != null) ActiveTab = tab;
            });
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
                // Unwire service events
                if (_tabManager != null)
                {
                    _tabManager.TabCreated -= OnTabCreated;
                    _tabManager.TabClosed -= OnTabClosed;
                    _tabManager.ActiveTabChanged -= OnActiveTabChanged;
                    _tabManager.TabModified -= OnTabModified;
                    _tabManager.TabsReordered -= OnTabsReordered;
                }
                
                _isDisposed = true;
                _logger?.LogInformation("MainWindowTabsViewModel disposed");
            }
        }
        
        #endregion
    }
} 