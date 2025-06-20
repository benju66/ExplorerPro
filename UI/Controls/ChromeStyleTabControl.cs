using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.Models;
using ExplorerPro.UI.MainWindow;
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
        /// Event fired when a tab drag operation starts
        /// </summary>
        public event EventHandler<TabDragEventArgs> TabDragStarted;

        /// <summary>
        /// Event fired during tab dragging
        /// </summary>
        public event EventHandler<TabDragEventArgs> TabDragging;

        /// <summary>
        /// Event fired when a tab drag operation completes
        /// </summary>
        public event EventHandler<TabDragEventArgs> TabDragCompleted;

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

        #region Drag and Drop Support

        private Point? _dragStartPoint;
        private TabItem _draggedTab;
        private bool _isDragging;
        private TabOperationsManager _tabOperationsManager;
        private ITabDragDropService _dragDropService;
        private DragOperation _currentDragOperation;

        // Thresholds for drag operations
        private const double DRAG_THRESHOLD = 5.0;
        private const double TEAR_OFF_THRESHOLD = 40.0;

        /// <summary>
        /// Gets or sets the tab operations manager
        /// </summary>
        public TabOperationsManager TabOperationsManager
        {
            get => _tabOperationsManager;
            set => _tabOperationsManager = value;
        }

        /// <summary>
        /// Gets or sets the drag drop service
        /// </summary>
        public ITabDragDropService DragDropService
        {
            get => _dragDropService;
            set => _dragDropService = value;
        }

        /// <summary>
        /// Handles mouse down on tab headers for drag initiation
        /// </summary>
        protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonDown(e);

            // Reset previous drag state
            _dragStartPoint = null;
            _draggedTab = null;

            // Find if we clicked on a tab header
            var tabItem = FindTabItemFromPoint(e.GetPosition(this));
            if (tabItem != null && !IsAddNewTabButton(e.OriginalSource))
            {
                _dragStartPoint = e.GetPosition(this);
                _draggedTab = tabItem;
                _isDragging = false;
                
                // Capture mouse for drag detection
                CaptureMouse();
            }
        }

        /// <summary>
        /// Handles mouse movement for drag operations
        /// </summary>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_dragStartPoint.HasValue && e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                var currentPoint = e.GetPosition(this);
                var dragDistance = currentPoint - _dragStartPoint.Value;

                // Check if we've moved enough to start dragging
                if (Math.Abs(dragDistance.X) > DRAG_THRESHOLD ||
                    Math.Abs(dragDistance.Y) > DRAG_THRESHOLD)
                {
                    StartDragOperation();
                }
            }
            else if (_isDragging)
            {
                UpdateDragOperation(e.GetPosition(null));
            }
        }

        /// <summary>
        /// Handles mouse up to complete drag operations
        /// </summary>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            if (_isDragging)
            {
                CompleteDragOperation(e.GetPosition(null));
            }
            else if (_draggedTab != null)
            {
                // Just a click, not a drag
                SelectedItem = _draggedTab;
            }

            // Reset drag state
            ResetDragState();
            ReleaseMouseCapture();
        }

        /// <summary>
        /// Handles escape key to cancel drag
        /// </summary>
        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            if (_isDragging && e.Key == Key.Escape)
            {
                CancelDragOperation();
                e.Handled = true;
            }
        }

        /// <summary>
        /// Starts the drag operation with visual feedback
        /// </summary>
        private void StartDragOperation()
        {
            if (_draggedTab?.Tag is TabItemModel tabModel)
            {
                _isDragging = true;
                var window = Window.GetWindow(this);
                
                // Create drag operation state
                _currentDragOperation = new DragOperation
                {
                    Tab = tabModel,
                    SourceWindow = window,
                    SourceTabControl = this,
                    DraggedTabItem = _draggedTab,
                    StartPoint = _dragStartPoint.Value,
                    OriginalIndex = Items.IndexOf(_draggedTab),
                    IsActive = true
                };

                // Start visual feedback
                StartDragVisualFeedback();

                // Use drag service if available
                if (_dragDropService != null)
                {
                    _dragDropService.StartDrag(tabModel, _dragStartPoint.Value, window);
                }
                else
                {
                    // Local drag handling
                    tabModel.IsDragging = true;
                }

                // Raise drag started event
                TabDragStarted?.Invoke(this, new TabDragEventArgs(
                    tabModel, 
                    _dragStartPoint.Value, 
                    Mouse.GetPosition(null)));

                // Set cursor
                Mouse.OverrideCursor = Cursors.Hand;
            }
        }

        /// <summary>
        /// Updates drag operation based on current position
        /// </summary>
        private void UpdateDragOperation(Point screenPoint)
        {
            if (_currentDragOperation == null || !_isDragging) return;

            _currentDragOperation.CurrentPoint = screenPoint;
            
            // Determine operation type based on position
            var operationType = DetermineOperationType(screenPoint);
            
            if (operationType != _currentDragOperation.CurrentOperationType)
            {
                _currentDragOperation.CurrentOperationType = operationType;
                UpdateDragVisualFeedback(operationType);
            }

            // Update via service if available
            if (_dragDropService != null)
            {
                _dragDropService.UpdateDrag(screenPoint);
            }

            // Raise dragging event
            if (_draggedTab?.Tag is TabItemModel tabModel)
            {
                TabDragging?.Invoke(this, new TabDragEventArgs(
                    tabModel,
                    _dragStartPoint.Value,
                    screenPoint));
            }
        }

        /// <summary>
        /// Completes the drag operation
        /// </summary>
        private void CompleteDragOperation(Point screenPoint)
        {
            if (_currentDragOperation == null || !_isDragging) return;

            try
            {
                var targetWindow = WindowLocator.FindWindowUnderPoint(screenPoint);
                bool success = false;

                // Try service first
                if (_dragDropService != null)
                {
                    success = _dragDropService.CompleteDrag(targetWindow, screenPoint);
                }
                else
                {
                    // Fallback to local handling
                    var operationType = _currentDragOperation.CurrentOperationType;
                    
                    switch (operationType)
                    {
                        case DragOperationType.Reorder:
                            success = HandleReorderDrop(screenPoint);
                            break;
                            
                        case DragOperationType.Detach:
                            success = HandleDetachDrop(screenPoint);
                            break;
                            
                        case DragOperationType.Transfer:
                            success = HandleTransferDrop(screenPoint);
                            break;
                    }
                }

                // Raise completed event
                if (_draggedTab?.Tag is TabItemModel tabModel)
                {
                    TabDragCompleted?.Invoke(this, new TabDragEventArgs(
                        tabModel,
                        _dragStartPoint.Value,
                        screenPoint));
                }

                if (success)
                {
                    _logger?.LogInformation($"Drag operation completed successfully");
                }
            }
            finally
            {
                EndDragVisualFeedback();
                Mouse.OverrideCursor = null;
                ResetDragState();
            }
        }

        /// <summary>
        /// Cancels the current drag operation
        /// </summary>
        private void CancelDragOperation()
        {
            if (!_isDragging) return;

            // Cancel via service
            _dragDropService?.CancelDrag();

            // Reset visual state
            EndDragVisualFeedback();
            
            // Reset tab to original position if needed
            if (_currentDragOperation != null && _draggedTab != null)
            {
                var currentIndex = Items.IndexOf(_draggedTab);
                if (currentIndex != _currentDragOperation.OriginalIndex)
                {
                    _tabOperationsManager?.ReorderTab(this, 
                        _draggedTab.Tag as TabItemModel, 
                        _currentDragOperation.OriginalIndex);
                }
            }

            ResetDragState();
        }

        /// <summary>
        /// Determines the type of drag operation based on position
        /// </summary>
        private DragOperationType DetermineOperationType(Point screenPoint)
        {
            // Check if we're over this tab control
            var localPoint = PointFromScreen(screenPoint);
            var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
            
            if (bounds.Contains(localPoint))
            {
                // Still within the same tab control - reorder
                return DragOperationType.Reorder;
            }

            // Check distance from original position
            var distance = (screenPoint - _currentDragOperation.StartPoint).Length;
            if (distance > TEAR_OFF_THRESHOLD)
            {
                // Check if over another window
                var targetWindow = FindWindowUnderPoint(screenPoint);
                if (targetWindow != null && targetWindow != _currentDragOperation.SourceWindow)
                {
                    return DragOperationType.Transfer;
                }
                
                // Far from any window - detach
                return DragOperationType.Detach;
            }

            return DragOperationType.None;
        }

        /// <summary>
        /// Handles reordering within the same tab control
        /// </summary>
        private bool HandleReorderDrop(Point screenPoint)
        {
            if (_tabOperationsManager == null || _draggedTab == null) return false;

            var dropIndex = _tabOperationsManager.CalculateDropIndex(this, screenPoint);
            var tabModel = _draggedTab.Tag as TabItemModel;
            
            return _tabOperationsManager.ReorderTab(this, tabModel, dropIndex);
        }

        /// <summary>
        /// Handles detaching to a new window
        /// </summary>
        private bool HandleDetachDrop(Point screenPoint)
        {
            if (_draggedTab == null) return false;

            // Use existing detach method from Phase 1
            var mainWindow = Window.GetWindow(this) as ExplorerPro.UI.MainWindow.MainWindow;
            var tabs = mainWindow?.FindName("MainTabs") as ChromeStyleTabControl;
            
            if (tabs != null)
            {
                var detachedWindow = mainWindow.DetachTabToNewWindow(_draggedTab);
                if (detachedWindow != null)
                {
                    // Position at drop point
                    detachedWindow.Left = screenPoint.X - 100;
                    detachedWindow.Top = screenPoint.Y - 20;
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// Handles transferring to another window
        /// </summary>
        private bool HandleTransferDrop(Point screenPoint)
        {
            var targetWindow = FindWindowUnderPoint(screenPoint);
            if (targetWindow == null || targetWindow == Window.GetWindow(this))
                return false;

            var targetTabControl = FindTabControlInWindow(targetWindow);
            if (targetTabControl == null || _tabOperationsManager == null)
                return false;

            var dropIndex = _tabOperationsManager.CalculateDropIndex(targetTabControl, screenPoint);
            var tabModel = _draggedTab.Tag as TabItemModel;
            
            return _tabOperationsManager.TransferTab(this, targetTabControl, tabModel, dropIndex);
        }

        #region Visual Feedback Methods

        private void StartDragVisualFeedback()
        {
            if (_draggedTab != null)
            {
                _draggedTab.Opacity = 0.6;
                _draggedTab.RenderTransform = new TranslateTransform();
            }
        }

        private void UpdateDragVisualFeedback(DragOperationType operationType)
        {
            if (_draggedTab == null) return;

            switch (operationType)
            {
                case DragOperationType.Reorder:
                    _draggedTab.Opacity = 0.8;
                    ShowReorderIndicator();
                    break;
                    
                case DragOperationType.Detach:
                    _draggedTab.Opacity = 0.4;
                    ShowDetachIndicator();
                    break;
                    
                case DragOperationType.Transfer:
                    _draggedTab.Opacity = 0.6;
                    ShowTransferIndicator();
                    break;
            }
        }

        private void EndDragVisualFeedback()
        {
            if (_draggedTab != null)
            {
                _draggedTab.Opacity = 1.0;
                _draggedTab.RenderTransform = null;
            }
            
            HideAllIndicators();
        }

        private void ShowReorderIndicator()
        {
            // Implementation for reorder visual indicator
        }

        private void ShowDetachIndicator()
        {
            // Implementation for detach visual indicator
        }

        private void ShowTransferIndicator()
        {
            // Implementation for transfer visual indicator
        }

        private void HideAllIndicators()
        {
            // Hide all visual indicators
        }

        #endregion

        #region Helper Methods

        private void ResetDragState()
        {
            _dragStartPoint = null;
            _draggedTab = null;
            _isDragging = false;
            _currentDragOperation = null;
        }

        private TabItem FindTabItemFromPoint(Point point)
        {
            var hitTest = VisualTreeHelper.HitTest(this, point);
            if (hitTest != null)
            {
                var element = hitTest.VisualHit;
                while (element != null && !(element is TabItem))
                {
                    element = VisualTreeHelper.GetParent(element);
                }
                return element as TabItem;
            }
            return null;
        }

        private bool IsAddNewTabButton(object source)
        {
            var element = source as DependencyObject;
            while (element != null)
            {
                if (element is Button button && button.Name == "AddNewTabButton")
                    return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        private Window FindWindowUnderPoint(Point screenPoint)
        {
            // This will be implemented properly in Phase 6
            // For now, return null
            return null;
        }

        private ChromeStyleTabControl FindTabControlInWindow(Window window)
        {
            if (window is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
            {
                return mainWindow.MainTabs as ChromeStyleTabControl;
            }
            return null;
        }

        #endregion

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the Loaded event
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Initialize services from App static properties
            if (_tabOperationsManager == null)
            {
                _tabOperationsManager = ExplorerPro.App.TabOperationsManager;
            }
            
            if (_dragDropService == null)
            {
                _dragDropService = ExplorerPro.App.DragDropService;
            }
            
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

        #region Animation Support

        /// <summary>
        /// Animates tab reordering with smooth transitions
        /// </summary>
        private void AnimateTabReorder(TabItem tab, int fromIndex, int toIndex)
        {
            if (Math.Abs(fromIndex - toIndex) == 0) return;

            // Calculate positions
            double tabWidth = tab.ActualWidth;
            double fromX = fromIndex * tabWidth;
            double toX = toIndex * tabWidth;

            // Create transform if needed
            if (!(tab.RenderTransform is TranslateTransform transform))
            {
                transform = new TranslateTransform();
                tab.RenderTransform = transform;
            }

            // Animate the movement
            var animation = new DoubleAnimation
            {
                From = fromX - toX,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(200),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };

            animation.Completed += (s, e) =>
            {
                tab.RenderTransform = null;
            };

            transform.BeginAnimation(TranslateTransform.XProperty, animation);
        }

        /// <summary>
        /// Shows a visual glow effect at the drop point
        /// </summary>
        private void ShowDropGlow(Point dropPoint)
        {
            // Create glow effect at drop point
            var glow = new Border
            {
                Width = 100,
                Height = 40,
                Background = new RadialGradientBrush(
                    Color.FromArgb(100, 0, 120, 212),
                    Colors.Transparent),
                IsHitTestVisible = false
            };

            // Position and animate
            Canvas.SetLeft(glow, dropPoint.X - 50);
            Canvas.SetTop(glow, dropPoint.Y - 20);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150));
            var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
            fadeOut.BeginTime = TimeSpan.FromMilliseconds(150);

            glow.BeginAnimation(OpacityProperty, fadeIn);
            glow.BeginAnimation(OpacityProperty, fadeOut);
        }

        #endregion

        #region Accessibility

        /// <summary>
        /// Announces drag operations to screen readers
        /// </summary>
        private void AnnounceOperation(string message)
        {
            if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
            {
                var peer = UIElementAutomationPeer.FromElement(this);
                peer?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
            }
        }

        /// <summary>
        /// Keyboard navigation for drag operations
        /// </summary>
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (SelectedItem is TabItem selectedTab && selectedTab.Tag is TabItemModel tabModel)
            {
                bool handled = false;

                // Alt+Arrow keys for reordering
                if (Keyboard.Modifiers == ModifierKeys.Alt)
                {
                    switch (e.Key)
                    {
                        case Key.Left:
                            handled = MoveTabLeft(tabModel);
                            break;
                        case Key.Right:
                            handled = MoveTabRight(tabModel);
                            break;
                    }
                }
                // Ctrl+Shift+N for detach
                else if (e.Key == Key.N && 
                         Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift))
                {
                    handled = DetachSelectedTab();
                }

                if (handled)
                {
                    e.Handled = true;
                    AnnounceOperation($"Tab {tabModel.Title} moved");
                }
            }
        }

        /// <summary>
        /// Moves the selected tab to the left
        /// </summary>
        private bool MoveTabLeft(TabItemModel tab)
        {
            var currentIndex = GetTabIndex(tab);
            if (currentIndex > 0)
            {
                return _tabOperationsManager?.ReorderTab(this, tab, currentIndex - 1) ?? false;
            }
            return false;
        }

        /// <summary>
        /// Moves the selected tab to the right
        /// </summary>
        private bool MoveTabRight(TabItemModel tab)
        {
            var currentIndex = GetTabIndex(tab);
            if (currentIndex < Items.Count - 1)
            {
                return _tabOperationsManager?.ReorderTab(this, tab, currentIndex + 1) ?? false;
            }
            return false;
        }

        /// <summary>
        /// Detaches the selected tab to a new window
        /// </summary>
        private bool DetachSelectedTab()
        {
            if (SelectedItem is TabItem tabItem && Items.Count > 1)
            {
                var window = Window.GetWindow(this);
                var mainWindow = window as MainWindow.MainWindow;
                return mainWindow?.DetachTabToNewWindow(tabItem) != null;
            }
            return false;
        }

        /// <summary>
        /// Gets the index of a tab by its model
        /// </summary>
        private int GetTabIndex(TabItemModel tab)
        {
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is TabItem tabItem && tabItem.Tag == tab)
                {
                    return i;
                }
            }
            return -1;
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