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
        private readonly ObservableCollection<TabItemModel> _tabItems;
        private TabItemModel _selectedTabItem;
        private string _windowTitle;
        private bool _isInitialized;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of MainWindowViewModel
        /// </summary>
        public MainWindowViewModel(ILogger<MainWindowViewModel> logger = null)
        {
            _logger = logger;
            _tabItems = new ObservableCollection<TabItemModel>();
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
        public ObservableCollection<TabItemModel> TabItems => _tabItems;

        /// <summary>
        /// Currently selected tab item
        /// </summary>
        public TabItemModel SelectedTabItem
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
        public TabItemModel AddNewTab(string title = null, object content = null)
        {
            try
            {
                title ??= $"Tab {_tabItems.Count + 1}";
                
                var newTab = new TabItemModel(Guid.NewGuid().ToString(), title, content);
                
                // If content is not provided, create a MainWindowContainer
                if (content == null)
                {
                    // This will be handled by the MainWindow when it receives the NewTabRequested event
                    newTab.Content = null; // Will be set later
                }

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
        public bool CloseTab(TabItemModel tabItem)
        {
            if (tabItem == null || !tabItem.IsClosable)
                return false;

            // Don't close the last tab
            if (_tabItems.Count <= 1)
                return false;

            try
            {
                var wasSelected = SelectedTabItem == tabItem;
                var index = _tabItems.IndexOf(tabItem);
                
                _tabItems.Remove(tabItem);

                // Select another tab if this was the selected one
                if (wasSelected && _tabItems.Count > 0)
                {
                    // Select the tab at the same index, or the last tab if index is out of bounds
                    var newIndex = Math.Min(index, _tabItems.Count - 1);
                    SelectedTabItem = _tabItems[newIndex];
                }

                UpdateWindowTitle();
                
                _logger?.LogDebug($"Closed tab: {tabItem.Title}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to close tab: {tabItem?.Title}");
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
        public TabItemModel DuplicateTab(TabItemModel tabItem)
        {
            if (tabItem == null) return null;

            try
            {
                var duplicatedTab = tabItem.Clone();
                duplicatedTab.Id = Guid.NewGuid().ToString();
                duplicatedTab.Title = $"{tabItem.Title} - Copy";
                duplicatedTab.CreatedAt = DateTime.Now;
                duplicatedTab.LastAccessed = DateTime.Now;

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
        public void TogglePinTab(TabItemModel tabItem)
        {
            if (tabItem == null) return;

            try
            {
                tabItem.IsPinned = !tabItem.IsPinned;
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
        public TabItemModel FindTabById(string tabId)
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
            CloseTabCommand = new RelayCommand<TabItemModel>(tab => CloseTab(tab));
            CloseCurrentTabCommand = new RelayCommand(() => CloseCurrentTab());
            DuplicateTabCommand = new RelayCommand<TabItemModel>(tab => DuplicateTab(tab));
            TogglePinTabCommand = new RelayCommand<TabItemModel>(TogglePinTab);
            
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
                var defaultTab = new TabItemModel(Guid.NewGuid().ToString(), "Home", null);
                _tabItems.Add(defaultTab);
                SelectedTabItem = defaultTab;
            }
        }

        /// <summary>
        /// Handles when the selected tab changes
        /// </summary>
        private void OnSelectedTabChanged()
        {
            if (SelectedTabItem != null)
            {
                SelectedTabItem.UpdateLastAccessed();
                UpdateWindowTitle();
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