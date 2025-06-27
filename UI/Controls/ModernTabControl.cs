using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ExplorerPro.Models;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.ViewModels;
using ExplorerPro.UI.Controls.Interfaces;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Modern tab control with clean architecture and separation of concerns.
    /// Replaces the monolithic ChromeStyleTabControl with focused, maintainable components.
    /// </summary>
    public class ModernTabControl : TabControl, IDisposable
    {
        #region Private Fields
        
        private readonly ILogger<ModernTabControl> _logger;
        private readonly IServiceProvider _serviceProvider;
        
        // Specialized managers
        private ITabDragDropManager _dragDropManager;
        private ITabAnimationManager _animationManager;
        private ITabSizingManager _sizingManager;
        private ITabVisualManager _visualManager;
        
        // Services
        private ITabManagerService _tabManagerService;
        private MainWindowTabsViewModel _viewModel;
        
        private bool _disposed;
        private bool _isInitialized;
        
        #endregion

        #region Dependency Properties
        
        public static readonly DependencyProperty TabManagerServiceProperty =
            DependencyProperty.Register(
                nameof(TabManagerService),
                typeof(ITabManagerService),
                typeof(ModernTabControl),
                new PropertyMetadata(null, OnTabManagerServiceChanged));

        public static readonly DependencyProperty ViewModelProperty =
            DependencyProperty.Register(
                nameof(ViewModel),
                typeof(MainWindowTabsViewModel),
                typeof(ModernTabControl),
                new PropertyMetadata(null, OnViewModelChanged));

        public static readonly DependencyProperty AllowTabReorderingProperty =
            DependencyProperty.Register(
                nameof(AllowTabReordering),
                typeof(bool),
                typeof(ModernTabControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty AllowTabDetachmentProperty =
            DependencyProperty.Register(
                nameof(AllowTabDetachment),
                typeof(bool),
                typeof(ModernTabControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty AnimationsEnabledProperty =
            DependencyProperty.Register(
                nameof(AnimationsEnabled),
                typeof(bool),
                typeof(ModernTabControl),
                new PropertyMetadata(true, OnAnimationsEnabledChanged));

        public static readonly DependencyProperty CurrentThemeProperty =
            DependencyProperty.Register(
                nameof(CurrentTheme),
                typeof(TabTheme),
                typeof(ModernTabControl),
                new PropertyMetadata(TabTheme.Light, OnCurrentThemeChanged));
        
        #endregion

        #region Public Properties
        
        /// <summary>
        /// Tab manager service for tab operations
        /// </summary>
        public ITabManagerService TabManagerService
        {
            get => (ITabManagerService)GetValue(TabManagerServiceProperty);
            set => SetValue(TabManagerServiceProperty, value);
        }

        /// <summary>
        /// View model for MVVM binding
        /// </summary>
        public MainWindowTabsViewModel ViewModel
        {
            get => (MainWindowTabsViewModel)GetValue(ViewModelProperty);
            set => SetValue(ViewModelProperty, value);
        }

        /// <summary>
        /// Whether tab reordering is allowed
        /// </summary>
        public bool AllowTabReordering
        {
            get => (bool)GetValue(AllowTabReorderingProperty);
            set => SetValue(AllowTabReorderingProperty, value);
        }

        /// <summary>
        /// Whether tab detachment is allowed
        /// </summary>
        public bool AllowTabDetachment
        {
            get => (bool)GetValue(AllowTabDetachmentProperty);
            set => SetValue(AllowTabDetachmentProperty, value);
        }

        /// <summary>
        /// Whether animations are enabled
        /// </summary>
        public bool AnimationsEnabled
        {
            get => (bool)GetValue(AnimationsEnabledProperty);
            set => SetValue(AnimationsEnabledProperty, value);
        }

        /// <summary>
        /// Current visual theme
        /// </summary>
        public TabTheme CurrentTheme
        {
            get => (TabTheme)GetValue(CurrentThemeProperty);
            set => SetValue(CurrentThemeProperty, value);
        }
        
        #endregion

        #region Constructor
        
        public ModernTabControl(IServiceProvider serviceProvider = null, ILogger<ModernTabControl> logger = null)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
            
            // Initialize with defaults
            DefaultStyleKey = typeof(ModernTabControl);
            
            // Wire up events
            Loaded += OnLoaded;
            SizeChanged += OnSizeChanged;
            
            _logger?.LogDebug("ModernTabControl created");
        }
        
        #endregion

        #region Public Methods
        
        /// <summary>
        /// Initializes the tab control with dependency injection
        /// </summary>
        public void Initialize(IServiceProvider serviceProvider)
        {
            ThrowIfDisposed();
            
            if (_isInitialized)
                return;
                
            try
            {
                // Get or create managers
                _dragDropManager = serviceProvider?.GetService<ITabDragDropManager>() ?? 
                    new TabDragDropManager(_logger?.CreateChildLogger<TabDragDropManager>());
                    
                _animationManager = serviceProvider?.GetService<ITabAnimationManager>() ?? 
                    new TabAnimationManager(_logger?.CreateChildLogger<TabAnimationManager>());
                    
                _sizingManager = serviceProvider?.GetService<ITabSizingManager>() ?? 
                    new TabSizingManager(_logger?.CreateChildLogger<TabSizingManager>());
                    
                _visualManager = serviceProvider?.GetService<ITabVisualManager>() ?? 
                    new TabVisualManager(_logger?.CreateChildLogger<TabVisualManager>());
                
                // Initialize managers
                _dragDropManager.Initialize(this);
                
                // Wire up manager events
                WireUpManagerEvents();
                
                // Configure managers
                ConfigureManagers();
                
                _isInitialized = true;
                _logger?.LogInformation("ModernTabControl initialized successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize ModernTabControl");
                throw;
            }
        }

        /// <summary>
        /// Manually refreshes all tab styling and layout
        /// </summary>
        public void RefreshTabs()
        {
            ThrowIfDisposed();
            
            if (!_isInitialized)
                return;
                
            try
            {
                // Update sizing
                _sizingManager?.UpdateTabWidths(this);
                
                // Refresh visual styling
                RefreshTabVisuals();
                
                _logger?.LogDebug("Refreshed all tabs");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error refreshing tabs");
            }
        }

        /// <summary>
        /// Updates the theme and refreshes all styling
        /// </summary>
        public void UpdateTheme(TabTheme newTheme)
        {
            ThrowIfDisposed();
            
            CurrentTheme = newTheme;
            
            if (_visualManager != null)
            {
                _visualManager.CurrentTheme = newTheme;
                _visualManager.ApplyThemeColors(this);
            }
        }
        
        #endregion

        #region Event Handlers
        
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized)
            {
                Initialize(_serviceProvider);
            }
            
            // Perform initial layout
            RefreshTabs();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isInitialized && _sizingManager != null)
            {
                _sizingManager.HandleContainerSizeChanged(e.NewSize);
                
                // Trigger layout update
                Dispatcher.BeginInvoke(new Action(() => _sizingManager.UpdateTabWidths(this)));
            }
        }

        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);
            
            if (!_isInitialized)
                return;
                
            // Animate selection change
            if (_animationManager?.AnimationsEnabled == true)
            {
                var newTab = e.AddedItems.Count > 0 ? e.AddedItems[0] as TabItem : null;
                var oldTab = e.RemovedItems.Count > 0 ? e.RemovedItems[0] as TabItem : null;
                
                if (newTab != null)
                {
                    _ = _animationManager.AnimateTabActivationAsync(newTab, oldTab);
                }
            }
            
            // Update visual states
            UpdateTabVisualStates();
        }

        protected override void OnItemsChanged(NotifyCollectionChangedEventArgs e)
        {
            base.OnItemsChanged(e);
            
            if (!_isInitialized)
                return;
                
            // Handle tab count changes
            if (_sizingManager != null)
            {
                var pinnedCount = CountPinnedTabs();
                _sizingManager.HandleTabCountChanged(Items.Count, pinnedCount);
            }
            
            // Animate new tabs
            if (e.Action == NotifyCollectionChangedAction.Add && _animationManager != null)
            {
                foreach (TabItem newTab in e.NewItems)
                {
                    _ = _animationManager.AnimateTabCreationAsync(newTab);
                    
                    // Apply initial styling
                    var tabModel = GetTabModelFromItem(newTab);
                    if (tabModel != null)
                    {
                        _visualManager?.ApplyTabStyling(newTab, tabModel);
                    }
                }
            }
            
            // Update layout
            Dispatcher.BeginInvoke(new Action(RefreshTabs));
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            
            if (_dragDropManager?.IsDragging == true)
            {
                _dragDropManager.UpdateDrag(e.GetPosition(this));
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            
            if (_dragDropManager?.IsDragging == true)
            {
                _dragDropManager.CompleteDrag(e.GetPosition(this));
            }
        }
        
        #endregion

        #region Manager Event Handlers
        
        private void OnDragDropReorderRequested(object sender, TabReorderRequestedEventArgs e)
        {
            if (!AllowTabReordering || _tabManagerService == null)
            {
                e.Cancel = true;
                return;
            }
            
            try
            {
                _ = _tabManagerService.MoveTabAsync(e.FromIndex, e.ToIndex);
                _logger?.LogDebug("Tab reordered from {FromIndex} to {ToIndex}", e.FromIndex, e.ToIndex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to reorder tab");
                e.Cancel = true;
            }
        }

        private void OnDragDropDetachRequested(object sender, TabDetachRequestedEventArgs e)
        {
            if (!AllowTabDetachment)
            {
                e.Cancel = true;
                return;
            }
            
            try
            {
                // Handle tab detachment (would require window management implementation)
                _logger?.LogDebug("Tab detachment requested for '{Title}'", e.Tab.Title);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to detach tab");
                e.Cancel = true;
            }
        }

        private void OnSizingChanged(object sender, TabSizingChangedEventArgs e)
        {
            _logger?.LogTrace("Tab sizing changed: TotalWidth={TotalWidth}, Compressed={IsCompressed}", 
                e.TotalWidth, e.IsCompressed);
        }

        private void OnVisualStateChanged(object sender, TabVisualStateChangedEventArgs e)
        {
            _logger?.LogTrace("Tab visual state changed: {OldState} -> {NewState}", e.OldState, e.NewState);
        }
        
        #endregion

        #region Property Change Handlers
        
        private static void OnTabManagerServiceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernTabControl control)
            {
                control.OnTabManagerServiceChanged(e.OldValue as ITabManagerService, e.NewValue as ITabManagerService);
            }
        }

        private static void OnViewModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernTabControl control)
            {
                control.OnViewModelChanged(e.OldValue as MainWindowTabsViewModel, e.NewValue as MainWindowTabsViewModel);
            }
        }

        private static void OnAnimationsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernTabControl control && control._animationManager != null)
            {
                control._animationManager.AnimationsEnabled = (bool)e.NewValue;
            }
        }

        private static void OnCurrentThemeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ModernTabControl control)
            {
                control.UpdateTheme((TabTheme)e.NewValue);
            }
        }

        private void OnTabManagerServiceChanged(ITabManagerService oldService, ITabManagerService newService)
        {
            if (oldService != null)
            {
                // Unwire old service events
                oldService.TabCreated -= OnTabCreated;
                oldService.TabClosed -= OnTabClosed;
                oldService.ActiveTabChanged -= OnActiveTabChanged;
            }
            
            _tabManagerService = newService;
            
            if (newService != null)
            {
                // Wire up new service events
                newService.TabCreated += OnTabCreated;
                newService.TabClosed += OnTabClosed;
                newService.ActiveTabChanged += OnActiveTabChanged;
                
                // Update data context
                DataContext = newService.Tabs;
            }
        }

        private void OnViewModelChanged(MainWindowTabsViewModel oldViewModel, MainWindowTabsViewModel newViewModel)
        {
            _viewModel = newViewModel;
            
            if (newViewModel != null)
            {
                DataContext = newViewModel;
            }
        }

        private void OnTabCreated(object sender, TabEventArgs e)
        {
            _logger?.LogDebug("Tab created: {Title}", e.Tab.Title);
            
            // The tab will be added to the UI automatically through data binding
            // Additional styling will be applied in OnItemsChanged
        }

        private void OnTabClosed(object sender, TabEventArgs e)
        {
            _logger?.LogDebug("Tab closed: {Title}", e.Tab.Title);
            
            // Animate tab closure if found
            var tabItem = FindTabItemFromModel(e.Tab);
            if (tabItem != null && _animationManager != null)
            {
                _ = _animationManager.AnimateTabClosingAsync(tabItem);
            }
        }

        private void OnActiveTabChanged(object sender, TabChangedEventArgs e)
        {
            _logger?.LogDebug("Active tab changed: {OldTitle} -> {NewTitle}", 
                e.OldTab?.Title, e.NewTab?.Title);
                
            // Update selection in UI
            var newTabItem = FindTabItemFromModel(e.NewTab);
            if (newTabItem != null)
            {
                SelectedItem = newTabItem;
            }
        }
        
        #endregion

        #region Private Helper Methods
        
        private void WireUpManagerEvents()
        {
            if (_dragDropManager != null)
            {
                _dragDropManager.ReorderRequested += OnDragDropReorderRequested;
                _dragDropManager.DetachRequested += OnDragDropDetachRequested;
            }
            
            if (_sizingManager != null)
            {
                _sizingManager.SizingChanged += OnSizingChanged;
            }
            
            if (_visualManager != null)
            {
                _visualManager.VisualStateChanged += OnVisualStateChanged;
            }
        }

        private void ConfigureManagers()
        {
            if (_animationManager != null)
            {
                _animationManager.AnimationsEnabled = AnimationsEnabled;
            }
            
            if (_visualManager != null)
            {
                _visualManager.CurrentTheme = CurrentTheme;
            }
            
            if (_sizingManager != null)
            {
                _sizingManager.AvailableWidth = ActualWidth;
            }
        }

        private void RefreshTabVisuals()
        {
            if (_visualManager == null)
                return;
                
            foreach (TabItem tabItem in Items)
            {
                var tabModel = GetTabModelFromItem(tabItem);
                if (tabModel != null)
                {
                    _visualManager.ApplyTabStyling(tabItem, tabModel);
                }
            }
        }

        private void UpdateTabVisualStates()
        {
            if (_visualManager == null)
                return;
                
            foreach (TabItem tabItem in Items)
            {
                var tabModel = GetTabModelFromItem(tabItem);
                if (tabModel != null)
                {
                    _visualManager.UpdateSelectionState(tabItem, tabItem.IsSelected);
                }
            }
        }

        private TabItem FindTabItemFromModel(TabModel model)
        {
            if (model == null)
                return null;
                
            foreach (TabItem item in Items)
            {
                var itemModel = GetTabModelFromItem(item);
                if (itemModel?.Id == model.Id)
                    return item;
            }
            
            return null;
        }

        private TabModel GetTabModelFromItem(TabItem tabItem)
        {
            return tabItem?.DataContext as TabModel ?? tabItem?.Tag as TabModel;
        }

        private int CountPinnedTabs()
        {
            int count = 0;
            foreach (TabItem item in Items)
            {
                var model = GetTabModelFromItem(item);
                if (model?.IsPinned == true)
                    count++;
            }
            return count;
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ModernTabControl));
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
            if (!_disposed && disposing)
            {
                try
                {
                    // Dispose managers
                    _dragDropManager?.Dispose();
                    _animationManager?.Dispose();
                    _sizingManager?.Dispose();
                    _visualManager?.Dispose();
                    
                    // Unwire events
                    if (_tabManagerService != null)
                    {
                        _tabManagerService.TabCreated -= OnTabCreated;
                        _tabManagerService.TabClosed -= OnTabClosed;
                        _tabManagerService.ActiveTabChanged -= OnActiveTabChanged;
                    }
                    
                    _disposed = true;
                    _logger?.LogDebug("ModernTabControl disposed");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during ModernTabControl disposal");
                }
            }
        }
        
        #endregion
    }
}

/// <summary>
/// Extension methods for ILogger to create child loggers
/// </summary>
internal static class LoggerExtensions
{
    public static ILogger<T> CreateChildLogger<T>(this ILogger logger)
    {
        // This would typically use a proper logger factory
        // For now, return null to avoid compilation errors
        return null;
    }
} 