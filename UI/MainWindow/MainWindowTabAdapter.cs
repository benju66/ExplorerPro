using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls;

namespace ExplorerPro.UI.MainWindow
{
    /// <summary>
    /// Adapter to bridge MainWindow's existing tab operations to Modern or Chrome control
    /// This allows us to switch implementations via feature flag without changing MainWindow code
    /// </summary>
    public class MainWindowTabAdapter : IDisposable
    {
        private readonly ILogger<MainWindowTabAdapter> _logger;
        private readonly ITabManagerService _tabManagerService;
        private TabControl _tabControl;
        private bool _useModernTabs;
        private bool _disposed;

        public MainWindowTabAdapter(
            ITabManagerService tabManagerService,
            ILogger<MainWindowTabAdapter> logger = null)
        {
            _tabManagerService = tabManagerService ?? throw new ArgumentNullException(nameof(tabManagerService));
            _logger = logger;
        }

        /// <summary>
        /// Initialize adapter with the actual tab control
        /// </summary>
        public void Initialize(TabControl tabControl, bool useModernTabs)
        {
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            _useModernTabs = useModernTabs;

            _logger?.LogInformation($"Tab adapter initialized with {(useModernTabs ? "Modern" : "Chrome")} control");

            // Wire up basic events based on control type
            if (_useModernTabs)
            {
                InitializeModernControl();
            }
            else
            {
                InitializeChromeControl();
            }
        }

        #region Public Methods - MainWindow Interface

        /// <summary>
        /// Creates a new tab with the specified title and content
        /// </summary>
        public async Task<TabModel> CreateNewTabAsync(string title = "New Tab", object content = null)
        {
            try
            {
                var options = new TabCreationOptions
                {
                    Content = content,
                    MakeActive = true
                };

                var tab = await _tabManagerService.CreateTabAsync(title, options: options);

                if (content != null)
                {
                    tab.Content = content;
                }

                _logger?.LogDebug($"Created new tab: {title}");
                return tab;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create new tab");
                throw;
            }
        }

        /// <summary>
        /// Closes the specified tab
        /// </summary>
        public async Task<bool> CloseTabAsync(TabModel tab)
        {
            if (tab == null) return false;

            // Respect pinned tabs
            if (tab.IsPinned)
            {
                _logger?.LogDebug("Cannot close pinned tab");
                return false;
            }

            // Don't close last tab
            if (GetTabCount() <= 1)
            {
                _logger?.LogDebug("Cannot close last tab");
                return false;
            }

            return await _tabManagerService.CloseTabAsync(tab);
        }

        /// <summary>
        /// Gets the currently selected tab
        /// </summary>
        public TabModel GetSelectedTab()
        {
            if (_useModernTabs)
            {
                return _tabManagerService.ActiveTab;
            }
            else
            {
                // Chrome control - get from SelectedItem
                if (_tabControl is ChromeStyleTabControl chrome)
                {
                    return chrome.SelectedTabItem;
                }
            }

            return null;
        }

        /// <summary>
        /// Selects the specified tab
        /// </summary>
        public async Task SelectTabAsync(TabModel tab)
        {
            if (tab == null) return;

            if (_useModernTabs)
            {
                await _tabManagerService.ActivateTabAsync(tab);
            }
            else
            {
                // Chrome control - set SelectedItem
                if (_tabControl is ChromeStyleTabControl chrome)
                {
                    chrome.SelectedTabItem = tab;
                }
            }
        }

        /// <summary>
        /// Gets the total number of tabs
        /// </summary>
        public int GetTabCount()
        {
            return _tabManagerService?.TabCount ?? 0;
        }

        /// <summary>
        /// Handles middle-click close
        /// </summary>
        public async Task<bool> HandleMiddleClickCloseAsync(TabModel tab)
        {
            if (tab == null) return false;

            // Same rules as regular close
            return await CloseTabAsync(tab);
        }

        #endregion

        #region Private Initialization

        private void InitializeModernControl()
        {
            if (_tabControl is ModernTabControl modern)
            {
                // Modern control uses service events
                _tabManagerService.TabCreated += OnServiceTabCreated;
                _tabManagerService.TabClosed += OnServiceTabClosed;
                _tabManagerService.ActiveTabChanged += OnServiceActiveTabChanged;

                // Ensure middle-click works
                EnsureMiddleClickSupport(modern);
            }
        }

        private void InitializeChromeControl()
        {
            if (_tabControl is ChromeStyleTabControl)
            {
                // Chrome control has its own events - adapter will handle translation
                // MainWindow already subscribes to these
            }
        }

        private void EnsureMiddleClickSupport(ModernTabControl modern)
        {
            // Add middle-click handler if not already present
            modern.PreviewMouseDown += async (sender, e) =>
            {
                if (e.ChangedButton == System.Windows.Input.MouseButton.Middle)
                {
                    var source = e.OriginalSource as System.Windows.DependencyObject;
                    var tabItem = FindParent<TabItem>(source);

                    if (tabItem != null)
                    {
                        var tabModel = tabItem.DataContext as TabModel;
                        if (tabModel != null)
                        {
                            _ = HandleMiddleClickCloseAsync(tabModel);
                            e.Handled = true;
                        }
                    }
                }
            };
        }

        private T FindParent<T>(System.Windows.DependencyObject child) where T : System.Windows.DependencyObject
        {
            if (child == null) return null;

            var parent = System.Windows.Media.VisualTreeHelper.GetParent(child);

            if (parent is T typedParent)
                return typedParent;
            else if (parent != null)
                return FindParent<T>(parent);
            else
                return null;
        }

        #endregion

        #region Event Handlers

        private void OnServiceTabCreated(object sender, TabEventArgs e)
        {
            _logger?.LogDebug($"Tab created via service: {e.Tab?.Title}");
        }

        private void OnServiceTabClosed(object sender, TabEventArgs e)
        {
            _logger?.LogDebug($"Tab closed via service: {e.Tab?.Title}");
        }

        private void OnServiceActiveTabChanged(object sender, TabChangedEventArgs e)
        {
            _logger?.LogDebug($"Active tab changed: {e.NewTab?.Title}");
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (_disposed) return;

            // Unhook events
            if (_tabManagerService != null)
            {
                _tabManagerService.TabCreated -= OnServiceTabCreated;
                _tabManagerService.TabClosed -= OnServiceTabClosed;
                _tabManagerService.ActiveTabChanged -= OnServiceActiveTabChanged;
            }

            _disposed = true;
        }

        #endregion
    }
}


