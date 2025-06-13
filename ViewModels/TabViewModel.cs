using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using ExplorerPro.Core.TabManagement;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.ViewModels
{
    /// <summary>
    /// View model for a tab
    /// </summary>
    public class TabViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<TabViewModel> _logger;
        private readonly TabManager _tabManager;
        private readonly Tab _tab;
        private bool _isActive;
        private TabPreview? _preview;
        private bool _isPreviewVisible;

        public TabViewModel(
            ILogger<TabViewModel> logger,
            TabManager tabManager,
            Tab tab)
        {
            _logger = logger;
            _tabManager = tabManager;
            _tab = tab;

            // Initialize commands
            CloseCommand = new RelayCommand(Close);
            PinCommand = new RelayCommand(Pin);
            ActivateCommand = new RelayCommand(Activate);
            ShowPreviewCommand = new RelayCommand(async () => await ShowPreviewAsync());
            HidePreviewCommand = new RelayCommand(HidePreview);
        }

        public string Id => _tab.Id;
        public string Title => _tab.Title;
        public string Path => _tab.Path;
        public bool IsPinned => _tab.IsPinned;
        public DateTime CreatedAt => _tab.CreatedAt;
        public DateTime LastAccessed => _tab.LastAccessed;

        public bool IsActive
        {
            get => _isActive;
            set => SetProperty(ref _isActive, value);
        }

        public TabPreview? Preview
        {
            get => _preview;
            private set => SetProperty(ref _preview, value);
        }

        public bool IsPreviewVisible
        {
            get => _isPreviewVisible;
            private set => SetProperty(ref _isPreviewVisible, value);
        }

        public ICommand CloseCommand { get; }
        public ICommand PinCommand { get; }
        public ICommand ActivateCommand { get; }
        public ICommand ShowPreviewCommand { get; }
        public ICommand HidePreviewCommand { get; }

        private void Close()
        {
            try
            {
                _tabManager.RemoveTab(Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing tab {TabId}", Id);
            }
        }

        private void Pin()
        {
            try
            {
                _tabManager.UpdateTabState(Id, tab => tab.IsPinned = !tab.IsPinned);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error pinning tab {TabId}", Id);
            }
        }

        private void Activate()
        {
            try
            {
                _tabManager.ActivateTab(Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error activating tab {TabId}", Id);
            }
        }

        private async Task ShowPreviewAsync()
        {
            try
            {
                Preview = await _tabManager.GetTabPreviewAsync(Id);
                IsPreviewVisible = true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error showing preview for tab {TabId}", Id);
            }
        }

        private void HidePreview()
        {
            IsPreviewVisible = false;
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
    /// Simple ICommand implementation
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
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
            return _canExecute?.Invoke() ?? true;
        }

        public void Execute(object? parameter)
        {
            _execute();
        }
    }
} 