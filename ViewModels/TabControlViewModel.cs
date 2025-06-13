using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ExplorerPro.Core.TabManagement;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.ViewModels
{
    /// <summary>
    /// View model for the tab control
    /// </summary>
    public class TabControlViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<TabControlViewModel> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly TabManager _tabManager;
        private readonly ObservableCollection<TabViewModel> _tabs;
        private TabViewModel? _selectedTab;
        private string _searchText = string.Empty;
        private bool _isSearchVisible;

        public TabControlViewModel(
            ILogger<TabControlViewModel> logger,
            ILoggerFactory loggerFactory,
            TabManager tabManager)
        {
            _logger = logger;
            _loggerFactory = loggerFactory;
            _tabManager = tabManager;
            _tabs = new ObservableCollection<TabViewModel>();

            // Initialize commands
            NewTabCommand = new RelayCommand(NewTab);
            CloseTabCommand = new RelayCommand<TabViewModel>(CloseTab);
            PinTabCommand = new RelayCommand<TabViewModel>(PinTab);
            ActivateTabCommand = new RelayCommand<TabViewModel>(ActivateTab);
            SearchCommand = new RelayCommand(Search);
            ClearSearchCommand = new RelayCommand(ClearSearch);

            // Subscribe to tab manager events
            _tabManager.TabAdded += OnTabAdded;
            _tabManager.TabRemoved += OnTabRemoved;
            _tabManager.TabActivated += OnTabActivated;
            _tabManager.TabDeactivated += OnTabDeactivated;
            _tabManager.TabStateChanged += OnTabStateChanged;
        }

        public ObservableCollection<TabViewModel> Tabs => _tabs;

        public TabViewModel? SelectedTab
        {
            get => _selectedTab;
            set => SetProperty(ref _selectedTab, value);
        }

        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }

        public bool IsSearchVisible
        {
            get => _isSearchVisible;
            set => SetProperty(ref _isSearchVisible, value);
        }

        public ICommand NewTabCommand { get; }
        public ICommand CloseTabCommand { get; }
        public ICommand PinTabCommand { get; }
        public ICommand ActivateTabCommand { get; }
        public ICommand SearchCommand { get; }
        public ICommand ClearSearchCommand { get; }

        private void NewTab()
        {
            try
            {
                _tabManager.AddTabAsync("New Tab", string.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating new tab");
            }
        }

        private void CloseTab(TabViewModel? tab)
        {
            if (tab != null)
            {
                try
                {
                    _tabManager.RemoveTab(tab.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error closing tab {TabId}", tab.Id);
                }
            }
        }

        private void PinTab(TabViewModel? tab)
        {
            if (tab != null)
            {
                try
                {
                    _tabManager.UpdateTabState(tab.Id, t => t.IsPinned = !t.IsPinned);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error pinning tab {TabId}", tab.Id);
                }
            }
        }

        private void ActivateTab(TabViewModel? tab)
        {
            if (tab != null)
            {
                try
                {
                    _tabManager.ActivateTab(tab.Id);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error activating tab {TabId}", tab.Id);
                }
            }
        }

        private async void Search()
        {
            try
            {
                if (string.IsNullOrWhiteSpace(SearchText))
                {
                    return;
                }

                var results = await _tabManager.SearchTabsAsync(SearchText);
                if (results.Any())
                {
                    // Activate the first matching tab
                    var firstResult = results.First();
                    var tab = _tabs.FirstOrDefault(t => t.Id == firstResult.TabId);
                    if (tab != null)
                    {
                        ActivateTab(tab);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching tabs");
            }
        }

        private void ClearSearch()
        {
            SearchText = string.Empty;
            IsSearchVisible = false;
        }

        private void OnTabAdded(object? sender, TabEventArgs e)
        {
            try
            {
                // Create a logger specifically for TabViewModel
                var tabLogger = _loggerFactory.CreateLogger<TabViewModel>();
                var tabViewModel = new TabViewModel(tabLogger, _tabManager, e.Tab);
                _tabs.Add(tabViewModel);
                SelectedTab = tabViewModel;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab added event");
            }
        }

        private void OnTabRemoved(object? sender, TabEventArgs e)
        {
            try
            {
                var tab = _tabs.FirstOrDefault(t => t.Id == e.Tab.Id);
                if (tab != null)
                {
                    _tabs.Remove(tab);
                    if (SelectedTab == tab)
                    {
                        SelectedTab = _tabs.FirstOrDefault();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab removed event");
            }
        }

        private void OnTabActivated(object? sender, TabEventArgs e)
        {
            try
            {
                var tab = _tabs.FirstOrDefault(t => t.Id == e.Tab.Id);
                if (tab != null)
                {
                    SelectedTab = tab;
                    tab.IsActive = true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab activated event");
            }
        }

        private void OnTabDeactivated(object? sender, TabEventArgs e)
        {
            try
            {
                var tab = _tabs.FirstOrDefault(t => t.Id == e.Tab.Id);
                if (tab != null)
                {
                    tab.IsActive = false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab deactivated event");
            }
        }

        private void OnTabStateChanged(object? sender, TabEventArgs e)
        {
            try
            {
                var tab = _tabs.FirstOrDefault(t => t.Id == e.Tab.Id);
                if (tab != null)
                {
                    // Force UI update by raising property changed
                    OnPropertyChanged(nameof(Tabs));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling tab state changed event");
            }
        }

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Generic RelayCommand implementation for commands with parameters
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<T?, bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<T?, bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute?.Invoke((T?)parameter) ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute((T?)parameter);
        }
    }
} 