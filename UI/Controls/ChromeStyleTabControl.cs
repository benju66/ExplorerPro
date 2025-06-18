using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExplorerPro.Models;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Chrome-style tab control with advanced features
    /// Supports custom rendering, add/delete operations, drag-drop, and metadata storage
    /// </summary>
    public class ChromeStyleTabControl : TabControl
    {
        #region Fields
        
        /// <summary>
        /// Logger for this control
        /// </summary>
        private readonly ILogger<ChromeStyleTabControl>? _logger;
        
        #endregion

        #region Dependency Properties

        /// <summary>
        /// Whether to allow adding new tabs
        /// </summary>
        public static readonly DependencyProperty AllowAddNewProperty =
            DependencyProperty.Register(
                nameof(AllowAddNew),
                typeof(bool),
                typeof(ChromeStyleTabControl),
                new PropertyMetadata(true));

        /// <summary>
        /// Whether to allow closing tabs
        /// </summary>
        public static readonly DependencyProperty AllowDeleteProperty =
            DependencyProperty.Register(
                nameof(AllowDelete),
                typeof(bool),
                typeof(ChromeStyleTabControl),
                new PropertyMetadata(true));

        /// <summary>
        /// Collection of tab items
        /// </summary>
        public static readonly DependencyProperty TabItemsProperty =
            DependencyProperty.Register(
                nameof(TabItems),
                typeof(ObservableCollection<TabItemModel>),
                typeof(ChromeStyleTabControl),
                new PropertyMetadata(null, OnTabItemsChanged));

        /// <summary>
        /// Currently selected tab item model
        /// </summary>
        public static readonly DependencyProperty SelectedTabItemProperty =
            DependencyProperty.Register(
                nameof(SelectedTabItem),
                typeof(TabItemModel),
                typeof(ChromeStyleTabControl),
                new PropertyMetadata(null, OnSelectedTabItemChanged));

        /// <summary>
        /// Maximum number of tabs allowed
        /// </summary>
        public static readonly DependencyProperty MaxTabCountProperty =
            DependencyProperty.Register(
                nameof(MaxTabCount),
                typeof(int),
                typeof(ChromeStyleTabControl),
                new PropertyMetadata(20));

        /// <summary>
        /// Whether to show add tab button
        /// </summary>
        public static readonly DependencyProperty ShowAddTabButtonProperty =
            DependencyProperty.Register(
                nameof(ShowAddTabButton),
                typeof(bool),
                typeof(ChromeStyleTabControl),
                new PropertyMetadata(true));

        #endregion

        #region Public Properties

        /// <summary>
        /// Gets or sets whether to allow adding new tabs
        /// </summary>
        public bool AllowAddNew
        {
            get => (bool)GetValue(AllowAddNewProperty);
            set => SetValue(AllowAddNewProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to allow closing tabs
        /// </summary>
        public bool AllowDelete
        {
            get => (bool)GetValue(AllowDeleteProperty);
            set => SetValue(AllowDeleteProperty, value);
        }

        /// <summary>
        /// Gets or sets the collection of tab items
        /// </summary>
        public ObservableCollection<TabItemModel> TabItems
        {
            get => (ObservableCollection<TabItemModel>)GetValue(TabItemsProperty);
            set => SetValue(TabItemsProperty, value);
        }

        /// <summary>
        /// Gets or sets the currently selected tab item model
        /// </summary>
        public TabItemModel SelectedTabItem
        {
            get => (TabItemModel)GetValue(SelectedTabItemProperty);
            set => SetValue(SelectedTabItemProperty, value);
        }

        /// <summary>
        /// Gets or sets the maximum number of tabs allowed
        /// </summary>
        public int MaxTabCount
        {
            get => (int)GetValue(MaxTabCountProperty);
            set => SetValue(MaxTabCountProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the add tab button
        /// </summary>
        public bool ShowAddTabButton
        {
            get => (bool)GetValue(ShowAddTabButtonProperty);
            set => SetValue(ShowAddTabButtonProperty, value);
        }

        #endregion

        #region Events

        /// <summary>
        /// Event fired when a new tab is requested
        /// </summary>
        public event EventHandler<NewTabRequestedEventArgs> NewTabRequested;

        /// <summary>
        /// Event fired when a tab close is requested
        /// </summary>
        public event EventHandler<TabCloseRequestedEventArgs> TabCloseRequested;

        /// <summary>
        /// Event fired when a tab is dragged
        /// </summary>
        public event EventHandler<TabDragEventArgs> TabDragged;

        /// <summary>
        /// Event fired when a tab's metadata changes
        /// </summary>
        public event EventHandler<TabMetadataChangedEventArgs> TabMetadataChanged;

        #endregion

        #region Constructor

        static ChromeStyleTabControl()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ChromeStyleTabControl),
                new FrameworkPropertyMetadata(typeof(ChromeStyleTabControl)));
        }

        /// <summary>
        /// Initializes a new instance of ChromeStyleTabControl
        /// </summary>
        public ChromeStyleTabControl()
        {
            // Initialize logger with a simple fallback
            try
            {
                var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
                _logger = loggerFactory.CreateLogger<ChromeStyleTabControl>();
            }
            catch
            {
                // Fallback if logging setup fails
                _logger = null;
            }

            // Initialize tab items collection if not set
            if (TabItems == null)
            {
                TabItems = new ObservableCollection<TabItemModel>();
            }

            // Wire up events
            Loaded += OnLoaded;
            KeyDown += OnKeyDown;
            MouseDoubleClick += OnMouseDoubleClick;
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the Loaded event
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Ensure we have at least one tab if none exist
            if (TabItems?.Count == 0 && AllowAddNew)
            {
                AddNewTab();
            }
        }

        /// <summary>
        /// Handles keyboard shortcuts
        /// </summary>
        private void OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.T && Keyboard.Modifiers == ModifierKeys.Control && AllowAddNew)
            {
                // Ctrl+T: New Tab
                AddNewTab();
                e.Handled = true;
            }
            else if (e.Key == Key.W && Keyboard.Modifiers == ModifierKeys.Control && AllowDelete)
            {
                // Ctrl+W: Close Tab
                CloseCurrentTab();
                e.Handled = true;
            }
            else if (e.Key >= Key.D1 && e.Key <= Key.D9 && Keyboard.Modifiers == ModifierKeys.Control)
            {
                // Ctrl+1-9: Switch to tab by number
                var tabIndex = e.Key - Key.D1;
                if (tabIndex < TabItems?.Count)
                {
                    SelectedTabItem = TabItems[tabIndex];
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Handles double-click on tab area to add new tab
        /// </summary>
        private void OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Check if double-click was on empty area, not on a tab
            if (e.OriginalSource == this && AllowAddNew)
            {
                AddNewTab();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles tab close requests
        /// </summary>
        private void OnTabCloseRequested(object sender, TabCloseRequestedEventArgs e)
        {
            try
            {
                if (e.TabItem != null && Items.Count > 1)
                {
                    var tabIndex = Items.IndexOf(e.TabItem);
                    if (tabIndex >= 0)
                    {
                        // Fire the close requested event for parent handling
                        TabCloseRequested?.Invoke(this, e);
                        
                        // If not cancelled, remove the tab
                        if (!e.Cancel)
                        {
                            Items.RemoveAt(tabIndex);
                            
                            // Update selection if needed
                            if (SelectedIndex >= Items.Count)
                            {
                                SelectedIndex = Items.Count - 1;
                            }
                        }
                    }
                }
                else
                {
                    e.Cancel = true; // Don't close the last tab
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling tab close request");
                e.Cancel = true;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Adds a new tab with default properties
        /// </summary>
        /// <returns>The created tab item model</returns>
        public TabItemModel AddNewTab()
        {
            return AddNewTab("New Tab", null);
        }

        /// <summary>
        /// Adds a new tab with specified title and content
        /// </summary>
        /// <param name="title">Title of the new tab</param>
        /// <param name="content">Content for the new tab</param>
        /// <returns>The created tab item model</returns>
        public TabItemModel AddNewTab(string title, object content = null)
        {
            if (!AllowAddNew || (TabItems?.Count >= MaxTabCount))
            {
                return null;
            }

            var newTab = new TabItemModel(Guid.NewGuid().ToString(), title, content);

            // Fire event to allow customization before adding
            var eventArgs = new NewTabRequestedEventArgs(newTab);
            NewTabRequested?.Invoke(this, eventArgs);

            if (eventArgs.Cancel)
            {
                return null;
            }

            // Use the possibly modified tab from the event
            newTab = eventArgs.TabItem;

            // Add to collection
            TabItems ??= new ObservableCollection<TabItemModel>();
            TabItems.Add(newTab);

            // Select the new tab
            SelectedTabItem = newTab;

            return newTab;
        }

        /// <summary>
        /// Closes the specified tab
        /// </summary>
        /// <param name="tabItem">Tab to close</param>
        /// <returns>True if the tab was closed, false otherwise</returns>
        public bool CloseTab(TabItemModel tabItem)
        {
            if (!AllowDelete || tabItem == null || !tabItem.IsClosable)
            {
                return false;
            }

            // Don't close the last tab unless explicitly allowed
            if (TabItems?.Count <= 1)
            {
                return false;
            }

            // Fire event to allow cancellation
            var eventArgs = new TabCloseRequestedEventArgs(tabItem);
            TabCloseRequested?.Invoke(this, eventArgs);

            if (eventArgs.Cancel)
            {
                return false;
            }

            // Remove from collection
            var wasSelected = SelectedTabItem == tabItem;
            TabItems?.Remove(tabItem);

            // Select another tab if this was the selected one
            if (wasSelected && TabItems?.Count > 0)
            {
                SelectedTabItem = TabItems.FirstOrDefault();
            }

            return true;
        }

        /// <summary>
        /// Closes the currently selected tab
        /// </summary>
        /// <returns>True if the tab was closed, false otherwise</returns>
        public bool CloseCurrentTab()
        {
            return CloseTab(SelectedTabItem);
        }

        /// <summary>
        /// Finds a tab by its ID
        /// </summary>
        /// <param name="tabId">ID of the tab to find</param>
        /// <returns>The tab item model or null if not found</returns>
        public TabItemModel FindTabById(string tabId)
        {
            return TabItems?.FirstOrDefault(t => t.Id == tabId);
        }

        /// <summary>
        /// Moves a tab from one position to another
        /// </summary>
        /// <param name="fromIndex">Source index</param>
        /// <param name="toIndex">Target index</param>
        /// <returns>True if the move was successful</returns>
        public bool MoveTab(int fromIndex, int toIndex)
        {
            if (TabItems == null || fromIndex < 0 || toIndex < 0 ||
                fromIndex >= TabItems.Count || toIndex >= TabItems.Count ||
                fromIndex == toIndex)
            {
                return false;
            }

            var tab = TabItems[fromIndex];
            TabItems.RemoveAt(fromIndex);
            TabItems.Insert(toIndex, tab);

            return true;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Handles changes to the TabItems collection
        /// </summary>
        private static void OnTabItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChromeStyleTabControl control)
            {
                // Unsubscribe from old collection
                if (e.OldValue is ObservableCollection<TabItemModel> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnTabItemsCollectionChanged;
                }

                // Subscribe to new collection
                if (e.NewValue is ObservableCollection<TabItemModel> newCollection)
                {
                    newCollection.CollectionChanged += control.OnTabItemsCollectionChanged;
                    control.RefreshTabItems();
                }
            }
        }

        /// <summary>
        /// Handles changes to the SelectedTabItem property
        /// </summary>
        private static void OnSelectedTabItemChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChromeStyleTabControl control)
            {
                // Update the selected tab in the underlying TabControl
                control.UpdateSelection();
            }
        }

        /// <summary>
        /// Handles collection change events for TabItems
        /// </summary>
        private void OnTabItemsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            RefreshTabItems();
        }

        /// <summary>
        /// Refreshes the actual TabControl items based on the TabItemModels
        /// </summary>
        private void RefreshTabItems()
        {
            // Clear existing items
            Items.Clear();

            if (TabItems == null) return;

            // Add TabItems based on TabItemModels
            foreach (var tabModel in TabItems)
            {
                var tabItem = CreateTabItemFromModel(tabModel);
                Items.Add(tabItem);
            }
        }

        /// <summary>
        /// Creates a WPF TabItem from a TabItemModel
        /// </summary>
        private TabItem CreateTabItemFromModel(TabItemModel model)
        {
            var tabItem = new TabItem
            {
                Header = model.Title,
                Content = model.Content,
                Tag = model,
                ToolTip = string.IsNullOrEmpty(model.Tooltip) ? model.Title : model.Tooltip
            };

            // Apply styling based on model properties
            if (model.IsPinned)
            {
                // Apply pinned styling
                tabItem.FontWeight = FontWeights.Bold;
            }

            if (model.HasUnsavedChanges)
            {
                // Add unsaved changes indicator to header
                tabItem.Header = $"• {model.Title}";
            }

            // Wire up property change notifications
            model.PropertyChanged += (s, e) => UpdateTabItemFromModel(tabItem, model);

            return tabItem;
        }

        /// <summary>
        /// Updates a TabItem when its model changes
        /// </summary>
        private void UpdateTabItemFromModel(TabItem tabItem, TabItemModel model)
        {
            if (tabItem == null || model == null) return;

            tabItem.Header = model.HasUnsavedChanges ? $"• {model.Title}" : model.Title;
            tabItem.Content = model.Content;
            tabItem.ToolTip = string.IsNullOrEmpty(model.Tooltip) ? model.Title : model.Tooltip;
            tabItem.FontWeight = model.IsPinned ? FontWeights.Bold : FontWeights.Normal;
        }

        /// <summary>
        /// Updates the selection to match the SelectedTabItem property
        /// </summary>
        private void UpdateSelection()
        {
            if (SelectedTabItem == null) return;

            // Find the corresponding TabItem
            var tabItem = Items.Cast<TabItem>().FirstOrDefault(t => t.Tag == SelectedTabItem);
            if (tabItem != null)
            {
                SelectedItem = tabItem;
                SelectedTabItem.UpdateLastAccessed();
            }
        }

        #endregion

        #region Override Methods

        /// <summary>
        /// Handles selection changed events
        /// </summary>
        /// <param name="e">Selection changed event args</param>
        protected override void OnSelectionChanged(SelectionChangedEventArgs e)
        {
            base.OnSelectionChanged(e);

            // Update SelectedTabItem based on the actual selection
            if (SelectedItem is TabItem selectedTabItem && selectedTabItem.Tag is TabItemModel model)
            {
                SelectedTabItem = model;
                model.UpdateLastAccessed();
            }
        }

        #endregion
    }

    #region Event Args Classes

    /// <summary>
    /// Event arguments for new tab requested event
    /// </summary>
    public class NewTabRequestedEventArgs : EventArgs
    {
        public TabItemModel TabItem { get; set; }
        public bool Cancel { get; set; }

        public NewTabRequestedEventArgs(TabItemModel tabItem)
        {
            TabItem = tabItem;
            Cancel = false;
        }
    }

    /// <summary>
    /// Event arguments for tab close requested event
    /// </summary>
    public class TabCloseRequestedEventArgs : EventArgs
    {
        public TabItemModel TabItem { get; }
        public bool Cancel { get; set; }

        public TabCloseRequestedEventArgs(TabItemModel tabItem)
        {
            TabItem = tabItem;
            Cancel = false;
        }
    }

    /// <summary>
    /// Event arguments for tab drag event
    /// </summary>
    public class TabDragEventArgs : EventArgs
    {
        public TabItemModel TabItem { get; }
        public Point StartPosition { get; }
        public Point CurrentPosition { get; }

        public TabDragEventArgs(TabItemModel tabItem, Point startPosition, Point currentPosition)
        {
            TabItem = tabItem;
            StartPosition = startPosition;
            CurrentPosition = currentPosition;
        }
    }

    /// <summary>
    /// Event arguments for tab metadata changed event
    /// </summary>
    public class TabMetadataChangedEventArgs : EventArgs
    {
        public TabItemModel TabItem { get; }
        public string PropertyName { get; }
        public object OldValue { get; }
        public object NewValue { get; }

        public TabMetadataChangedEventArgs(TabItemModel tabItem, string propertyName, object oldValue, object newValue)
        {
            TabItem = tabItem;
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    #endregion
} 