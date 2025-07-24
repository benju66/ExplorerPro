using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using ExplorerPro.Models;
using ExplorerPro.UI.MainWindow;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.ViewModels
{
    /// <summary>
    /// Main window view model supporting Chrome-style tab system
    /// </summary>
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        #region Fields

        private readonly ILogger<MainWindowViewModel> _logger;
        private readonly ObservableCollection<TabModel> _tabItems;
        private TabModel _selectedTabItem;
        private string _windowTitle;
        private bool _isInitialized;
        private ExplorerPro.Core.TabManagement.ITabManagerService _tabManager;
        private ExplorerPro.Core.TabManagement.TabOperationsManager _tabOperationsManager;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of MainWindowViewModel
        /// </summary>
        public MainWindowViewModel(ILogger<MainWindowViewModel> logger = null)
        {
            _logger = logger;
            _tabItems = new ObservableCollection<TabModel>();
            _windowTitle = "ExplorerPro";
            _isInitialized = false;

            InitializeCommands();
            InitializeDefaultTab();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Collection of tab items
        /// </summary>
        public ObservableCollection<TabModel> TabItems => _tabItems;

        /// <summary>
        /// Currently selected tab item
        /// </summary>
        public TabModel SelectedTabItem
        {
            get => _selectedTabItem;
            set
            {
                if (SetProperty(ref _selectedTabItem, value))
                {
                    OnSelectedTabChanged();
                }
            }
        }

        /// <summary>
        /// Window title
        /// </summary>
        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        /// <summary>
        /// Whether the view model is initialized
        /// </summary>
        public bool IsInitialized
        {
            get => _isInitialized;
            private set => SetProperty(ref _isInitialized, value);
        }

        /// <summary>
        /// Gets or sets the tab operations manager
        /// </summary>
        public ExplorerPro.Core.TabManagement.TabOperationsManager TabOperationsManager
        {
            get => _tabOperationsManager;
            set => SetProperty(ref _tabOperationsManager, value);
        }

        #endregion

        #region Commands

        /// <summary>
        /// Command to add a new tab
        /// </summary>
        public ICommand AddNewTabCommand { get; private set; }

        /// <summary>
        /// Command to close a tab
        /// </summary>
        public ICommand CloseTabCommand { get; private set; }

        /// <summary>
        /// Command to close current tab
        /// </summary>
        public ICommand CloseCurrentTabCommand { get; private set; }

        /// <summary>
        /// Command to duplicate current tab
        /// </summary>
        public ICommand DuplicateTabCommand { get; private set; }

        /// <summary>
        /// Command to pin/unpin a tab
        /// </summary>
        public ICommand TogglePinTabCommand { get; private set; }

        /// <summary>
        /// Command to rename a tab
        /// </summary>
        public ICommand RenameTabCommand { get; private set; }

        /// <summary>
        /// Command to change tab color
        /// </summary>
        public ICommand ChangeColorCommand { get; private set; }

        /// <summary>
        /// Command to toggle pin state
        /// </summary>
        public ICommand TogglePinCommand { get; private set; }

        #endregion

        #region Public Methods

        /// <summary>
        /// Initializes the view model
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

            try
            {
                _logger?.LogInformation("Initializing MainWindowViewModel");

                // Ensure we have at least one tab
                if (_tabItems.Count == 0)
                {
                    InitializeDefaultTab();
                }

                IsInitialized = true;
                _logger?.LogInformation("MainWindowViewModel initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize MainWindowViewModel");
                throw;
            }
        }

        /// <summary>
        /// Adds a new tab with the specified title and content
        /// </summary>
        /// <param name="title">Tab title</param>
        /// <param name="content">Tab content</param>
        /// <returns>The created tab item</returns>
        public TabModel AddNewTab(string title = null, object content = null)
        {
            try
            {
                title = title ?? $"Tab {_tabItems.Count + 1}";
                
                var newTab = new TabModel(title, "");
                newTab.Content = content;

                _tabItems.Add(newTab);
                SelectedTabItem = newTab;

                UpdateWindowTitle();
                
                _logger?.LogDebug($"Added new tab: {title}");
                return newTab;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to add new tab: {title}");
                return null;
            }
        }

        /// <summary>
        /// Closes the specified tab
        /// </summary>
        /// <param name="tabItem">Tab to close</param>
        /// <returns>True if the tab was closed</returns>
        public bool CloseTab(TabModel tabItem)
        {
            try
            {
                if (tabItem == null) return false;

                // Don't close pinned tabs with unsaved changes
                if (tabItem.IsPinned && tabItem.HasUnsavedChanges)
                {
                    _logger?.LogWarning($"Cannot close pinned tab with unsaved changes: {tabItem.Title}");
                    return false;
                }

                // Don't close the last tab
                if (_tabItems.Count <= 1)
                {
                    _logger?.LogWarning("Cannot close the last tab");
                    return false;
                }

                var wasSelected = SelectedTabItem == tabItem;
                var index = _tabItems.IndexOf(tabItem);
                
                _tabItems.Remove(tabItem);

                // Select another tab if this was the selected one
                if (wasSelected && _tabItems.Count > 0)
                {
                    var nextIndex = Math.Min(index, _tabItems.Count - 1);
                    SelectedTabItem = _tabItems[nextIndex];
                }

                _logger?.LogDebug($"Closed tab: {tabItem.Title}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error closing tab: {tabItem?.Title}");
                return false;
            }
        }

        /// <summary>
        /// Closes the currently selected tab
        /// </summary>
        /// <returns>True if the tab was closed</returns>
        public bool CloseCurrentTab()
        {
            return CloseTab(SelectedTabItem);
        }

        /// <summary>
        /// Duplicates the specified tab
        /// </summary>
        /// <param name="tabItem">Tab to duplicate</param>
        /// <returns>The duplicated tab</returns>
        public TabModel DuplicateTab(TabModel tabItem)
        {
            if (tabItem == null) return null;

            try
            {
                var duplicatedTab = tabItem.Clone();
                duplicatedTab.Title = $"{tabItem.Title} - Copy";

                _tabItems.Add(duplicatedTab);
                SelectedTabItem = duplicatedTab;

                _logger?.LogDebug($"Duplicated tab: {tabItem.Title}");
                return duplicatedTab;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to duplicate tab: {tabItem?.Title}");
                return null;
            }
        }

        /// <summary>
        /// Toggles the pin state of the specified tab
        /// </summary>
        /// <param name="tabItem">Tab to toggle pin state</param>
        public async void TogglePinTab(TabModel tabItem)
        {
            if (tabItem == null) return;

            try
            {
                // Update the model
                tabItem.IsPinned = !tabItem.IsPinned;
                
                // Sync with TabManagerService if available
                if (_tabManager != null)
                {
                    // Find corresponding TabModel in the service and sync pin state
                    var correspondingTabModel = _tabManager.Tabs?.FirstOrDefault(t => t.Title == tabItem.Title);
                    if (correspondingTabModel != null)
                    {
                        await _tabManager.SetTabPinnedAsync(correspondingTabModel, tabItem.IsPinned);
                    }
                }
                
                // Force UI update
                OnPropertyChanged(nameof(TabItems));
                
                _logger?.LogDebug($"Toggled pin state for tab: {tabItem.Title}, Pinned: {tabItem.IsPinned}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to toggle pin state for tab: {tabItem?.Title}");
            }
        }

        /// <summary>
        /// Finds a tab by its ID
        /// </summary>
        /// <param name="tabId">Tab ID to find</param>
        /// <returns>The tab item or null if not found</returns>
        public TabModel FindTabById(string tabId)
        {
            return _tabItems.FirstOrDefault(t => t.Id == tabId);
        }

        /// <summary>
        /// Updates the content of a specific tab
        /// </summary>
        /// <param name="tabId">Tab ID</param>
        /// <param name="content">New content</param>
        public void UpdateTabContent(string tabId, object content)
        {
            var tab = FindTabById(tabId);
            if (tab != null)
            {
                tab.Content = content;
                _logger?.LogDebug($"Updated content for tab: {tab.Title}");
            }
        }

        /// <summary>
        /// Updates the title of a specific tab
        /// </summary>
        /// <param name="tabId">Tab ID</param>
        /// <param name="title">New title</param>
        public void UpdateTabTitle(string tabId, string title)
        {
            var tab = FindTabById(tabId);
            if (tab != null)
            {
                tab.Title = title;
                if (tab == SelectedTabItem)
                {
                    UpdateWindowTitle();
                }
                _logger?.LogDebug($"Updated title for tab: {tab.Id} to {title}");
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Initializes the commands
        /// </summary>
        private void InitializeCommands()
        {
            AddNewTabCommand = new RelayCommand(() => AddNewTab());
            CloseTabCommand = new RelayCommand<TabModel>(tab => CloseTab(tab));
            CloseCurrentTabCommand = new RelayCommand(() => CloseCurrentTab());
            DuplicateTabCommand = new RelayCommand<TabModel>(tab => DuplicateTab(tab));
            TogglePinTabCommand = new RelayCommand<TabModel>(TogglePinTab);
            
            // Phase 2 Commands
            RenameTabCommand = Commands.TabCommands.CreateRenameTabCommand(_logger);
            ChangeColorCommand = Commands.TabCommands.CreateChangeColorCommand(_logger);
            TogglePinCommand = Commands.TabCommands.CreateTogglePinCommand(_logger);
        }

        /// <summary>
        /// Initializes the default tab
        /// </summary>
        private void InitializeDefaultTab()
        {
            if (_tabItems.Count == 0)
            {
                var defaultTab = new TabModel("Home", "");
                _tabItems.Add(defaultTab);
                SelectedTabItem = defaultTab;
            }
        }

        /// <summary>
        /// Handles when the selected tab changes
        /// </summary>
        private void OnSelectedTabChanged()
        {
            try
            {
                if (_selectedTabItem != null)
                {
                    _selectedTabItem.Activate();
                    UpdateWindowTitle();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling selected tab changed");
            }
        }

        /// <summary>
        /// Updates the window title based on the selected tab
        /// </summary>
        private void UpdateWindowTitle()
        {
            if (SelectedTabItem != null)
            {
                WindowTitle = $"{SelectedTabItem.Title} - ExplorerPro";
            }
            else
            {
                WindowTitle = "ExplorerPro";
            }
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        /// <summary>
        /// Event fired when a property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Sets a property value and raises PropertyChanged if the value changed
        /// </summary>
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="field">Reference to the backing field</param>
        /// <param name="value">New value</param>
        /// <param name="propertyName">Name of the property (automatically filled)</param>
        /// <returns>True if the property value changed, false otherwise</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
} 