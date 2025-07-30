using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.Disposables;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls;
using ExplorerPro.UI.PaneManagement;

namespace ExplorerPro.Commands
{
    /// <summary>
    /// Manages consistent event handling for all tab operations.
    /// Provides centralized event management for both main window tabs and pane tabs.
    /// </summary>
    public class TabEventManager : IDisposable
    {
        #region Fields

        private readonly CompositeDisposable _eventSubscriptions = new CompositeDisposable();
        private readonly Dictionary<TabItem, IDisposable> _tabSubscriptions = new Dictionary<TabItem, IDisposable>();
        private readonly ILogger<TabEventManager> _logger;
        private bool _disposed;

        #endregion

        #region Constructor

        public TabEventManager(ILogger<TabEventManager> logger = null)
        {
            _logger = logger;
        }

        #endregion

        #region Event Registration

        /// <summary>
        /// Registers all necessary events for a tab item
        /// </summary>
        public void RegisterTabEvents(TabItem tabItem, UnifiedTabCommands.TabCommandContext context)
        {
            if (tabItem == null || _disposed) return;

            try
            {
                // Unregister any existing events for this tab
                UnregisterTabEvents(tabItem);

                var subscriptions = new CompositeDisposable();

                // Register mouse events for tab interaction
                RegisterMouseEvents(tabItem, context, subscriptions);

                // Register model property change events if TabModel exists
                if (tabItem.Tag is TabModel model)
                {
                    RegisterModelEvents(tabItem, model, subscriptions);
                }

                // Store subscriptions for this tab
                _tabSubscriptions[tabItem] = subscriptions;

                _logger?.LogDebug($"Registered events for tab: {tabItem.Header}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error registering events for tab: {tabItem.Header}");
            }
        }

        /// <summary>
        /// Unregisters events for a tab item
        /// </summary>
        public void UnregisterTabEvents(TabItem tabItem)
        {
            if (tabItem == null) return;

            try
            {
                if (_tabSubscriptions.TryGetValue(tabItem, out var subscriptions))
                {
                    subscriptions.Dispose();
                    _tabSubscriptions.Remove(tabItem);
                }

                _logger?.LogDebug($"Unregistered events for tab: {tabItem.Header}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error unregistering events for tab: {tabItem.Header}");
            }
        }

        #endregion

        #region Event Handlers

        private void RegisterMouseEvents(TabItem tabItem, UnifiedTabCommands.TabCommandContext context, CompositeDisposable subscriptions)
        {
            // Double-click to rename
            var doubleClickHandler = new System.Windows.Input.MouseButtonEventHandler((s, e) =>
            {
                if (e.ClickCount == 2 && e.ChangedButton == System.Windows.Input.MouseButton.Left)
                {
                    var renameTask = UnifiedTabCommands.RenameTabAsync(context);
                    // Don't await here to avoid blocking UI
                }
            });

            tabItem.MouseDoubleClick += doubleClickHandler;
            subscriptions.Add(Disposable.Create(() => tabItem.MouseDoubleClick -= doubleClickHandler));

            // Right-click for context menu (handled by existing system, just log)
            var rightClickHandler = new System.Windows.Input.MouseButtonEventHandler((s, e) =>
            {
                if (e.ChangedButton == System.Windows.Input.MouseButton.Right)
                {
                    _logger?.LogDebug($"Right-click on tab: {tabItem.Header}");
                }
            });

            tabItem.MouseRightButtonUp += rightClickHandler;
            subscriptions.Add(Disposable.Create(() => tabItem.MouseRightButtonUp -= rightClickHandler));
        }

        private void RegisterModelEvents(TabItem tabItem, TabModel model, CompositeDisposable subscriptions)
        {
            var propertyChangedHandler = new System.ComponentModel.PropertyChangedEventHandler((s, e) =>
            {
                try
                {
                    switch (e.PropertyName)
                    {
                        case nameof(TabModel.Title):
                            UpdateTabHeader(tabItem, model);
                            break;
                        case nameof(TabModel.IsPinned):
                            UpdateTabPinVisuals(tabItem, model);
                            break;
                        case nameof(TabModel.CustomColor):
                            UpdateTabColorVisuals(tabItem, model);
                            break;
                        case nameof(TabModel.HasUnsavedChanges):
                            UpdateTabUnsavedIndicator(tabItem, model);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error handling property change for tab: {tabItem.Header}");
                }
            });

            model.PropertyChanged += propertyChangedHandler;
            subscriptions.Add(Disposable.Create(() => model.PropertyChanged -= propertyChangedHandler));
        }

        #endregion

        #region Visual Updates

        private void UpdateTabHeader(TabItem tabItem, TabModel model)
        {
            var title = model.Title;
            if (model.HasUnsavedChanges)
            {
                title = "• " + title;  // Add bullet for unsaved changes
            }
            tabItem.Header = title;
        }

        private void UpdateTabPinVisuals(TabItem tabItem, TabModel model)
        {
            if (model.IsPinned)
            {
                // Apply pinned styling
                tabItem.Width = 40;
                tabItem.MinWidth = 40;
                tabItem.MaxWidth = 40;
                
                // TODO: Hide text, show only icon when properly implemented
                tabItem.ToolTip = model.Title;
            }
            else
            {
                // Remove pinned styling
                tabItem.ClearValue(TabItem.WidthProperty);
                tabItem.ClearValue(TabItem.MinWidthProperty);
                tabItem.ClearValue(TabItem.MaxWidthProperty);
                tabItem.ClearValue(TabItem.ToolTipProperty);
            }
        }

        private void UpdateTabColorVisuals(TabItem tabItem, TabModel model)
        {
            if (model.HasCustomColor)
            {
                tabItem.Background = new System.Windows.Media.SolidColorBrush(model.CustomColor);
            }
            else
            {
                tabItem.ClearValue(TabItem.BackgroundProperty);
            }
        }

        private void UpdateTabUnsavedIndicator(TabItem tabItem, TabModel model)
        {
            UpdateTabHeader(tabItem, model);  // This will add/remove the bullet
        }

        #endregion

        #region IDisposable Implementation

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                // Dispose all tab subscriptions
                foreach (var subscription in _tabSubscriptions.Values)
                {
                    subscription.Dispose();
                }
                _tabSubscriptions.Clear();

                // Dispose main event subscriptions
                _eventSubscriptions.Dispose();

                _logger?.LogInformation("TabEventManager disposed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during TabEventManager disposal");
            }
            finally
            {
                _disposed = true;
            }
        }

        #endregion
    }
} 