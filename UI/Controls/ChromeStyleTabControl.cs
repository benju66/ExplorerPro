using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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
    public class ChromeStyleTabControl : TabControl, IDisposable
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
        
        // Visual indicator fields
        private TabDropInsertionIndicator _insertionIndicator;
        private Window _detachPreviewWindow;
        private Window _dragVisualWindow;
        private Rectangle _insertionLine;

        // Thresholds for drag operations
        private const double DRAG_THRESHOLD = 5.0;
        private const double TEAR_OFF_THRESHOLD = 40.0;
        private const double TAB_STRIP_HEIGHT = 35.0;

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
            
            // Validate that we have a valid tab item and not clicking on special buttons
            if (tabItem != null && 
                !IsAddNewTabButton(e.OriginalSource) && 
                !IsCloseButton(e.OriginalSource) &&
                IsValidTabForDrag(tabItem))
            {
                _dragStartPoint = e.GetPosition(this);
                _draggedTab = tabItem;
                _isDragging = false;
                
                // Only capture mouse if we successfully identified a draggable tab
                if (_draggedTab != null)
                {
                    CaptureMouse();
                }
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
                    // Release mouse capture before starting drag operation
                    if (IsMouseCaptured)
                    {
                        ReleaseMouseCapture();
                    }

                    try
                    {
                        StartDragOperation();
                    }
                    finally
                    {
                        // Ensure cleanup happens even if drag operation fails
                        if (_draggedTab != null)
                        {
                            _draggedTab.Opacity = 1.0;
                        }
                        
                        if (IsMouseCaptured)
                        {
                            ReleaseMouseCapture();
                        }
                        
                        // Reset drag state
                        _dragStartPoint = null;
                        _draggedTab = null;
                        _isDragging = false;
                    }
                }
            }
            else if (_isDragging)
            {
                var screenPoint = e.GetPosition(null);
                UpdateDragOperation(screenPoint);
                
                // Update drag visual position to follow cursor
                if (_dragVisualWindow != null && _dragVisualWindow.IsVisible)
                {
                    UpdateDragVisualPosition(screenPoint);
                }
                
                // Update insertion indicator based on drop validity
                UpdateInsertionIndicatorVisibility(screenPoint);
            }
        }

        /// <summary>
        /// Handles mouse left button up to properly release mouse capture and reset drag state
        /// </summary>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);

            try
            {
                // Complete any ongoing drag operation
                if (_isDragging)
                {
                    CompleteDragOperation(e.GetPosition(null));
                }
                else if (_draggedTab != null)
                {
                    // Just a click, not a drag - select the tab
                    SelectedItem = _draggedTab;
                }
            }
            finally
            {
                // ALWAYS ensure mouse capture is released and state is reset
                ResetDragState();
            }
        }

        /// <summary>
        /// Handles mouse capture lost to reset drag state when capture is lost unexpectedly
        /// </summary>
        protected override void OnLostMouseCapture(MouseEventArgs e)
        {
            base.OnLostMouseCapture(e);

            // Reset all drag state when mouse capture is lost unexpectedly
            _dragStartPoint = null;
            _draggedTab = null;
            _isDragging = false;

            // Reset visual state if needed
            if (_draggedTab != null)
            {
                _draggedTab.Opacity = 1.0;
                _draggedTab.RenderTransform = null;
            }

            // Hide any visual indicators
            HideAllIndicators();
        }

        /// <summary>
        /// Handles mouse up to complete drag operations
        /// </summary>
        protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseLeftButtonUp(e);

            try
            {
                if (_isDragging)
                {
                    CompleteDragOperation(e.GetPosition(null));
                }
                else if (_draggedTab != null)
                {
                    // Just a click, not a drag
                    SelectedItem = _draggedTab;
                }
            }
            finally
            {
                // ALWAYS reset state, even if operation fails
                ResetDragState();
            }
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
                
                // Create and show enhanced drag visual
                _dragVisualWindow = CreateDragVisual(_draggedTab);
                if (_dragVisualWindow != null)
                {
                    // Position at current cursor location
                    var cursorPos = Mouse.GetPosition(null);
                    _dragVisualWindow.Left = cursorPos.X - (_dragVisualWindow.Width / 2);
                    _dragVisualWindow.Top = cursorPos.Y - 20;
                    _dragVisualWindow.Show();
                }

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
            if (!_isDragging || _currentDragOperation == null)
                return;

            // Store previous operation type to detect changes
            var previousType = _currentDragOperation.CurrentOperationType;
            var operationType = DetermineOperationType(screenPoint);
            
            // Update operation state
            _currentDragOperation.CurrentOperationType = operationType;
            _currentDragOperation.CurrentPoint = screenPoint;
            _currentDragOperation.CurrentScreenPoint = screenPoint;
            
            // Only update visuals if operation type changed
            if (previousType != operationType)
            {
                UpdateDragVisualFeedback(operationType);
                
                // Log operation type change for debugging
                _logger?.LogDebug($"Drag operation changed from {previousType} to {operationType}");
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

            try
            {
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
            }
            finally
            {
                // ALWAYS ensure cleanup happens
                ResetDragState();
            }
        }

        /// <summary>
        /// Determines the type of drag operation based on position
        /// </summary>
        private DragOperationType DetermineOperationType(Point screenPoint)
        {
            // CRITICAL: Get the actual tab strip bounds, not the entire control
            var tabStripHeight = TAB_STRIP_HEIGHT; // Height of tab headers
            
            // Convert to local coordinates relative to this control
            Point localPoint;
            try 
            {
                localPoint = PointFromScreen(screenPoint);
            }
            catch
            {
                // If coordinate conversion fails, default to no operation
                return DragOperationType.None;
            }
            
            // Define the bounds for tab reordering (just the tab strip area)
            var tabStripBounds = new Rect(0, 0, ActualWidth, tabStripHeight);
            
            if (tabStripBounds.Contains(localPoint))
            {
                // Cursor is within tab strip - this is a reorder operation
                return DragOperationType.Reorder;
            }
            
            // Check vertical distance for detach
            var verticalDistance = Math.Abs(localPoint.Y - tabStripHeight / 2);
            if (verticalDistance > TEAR_OFF_THRESHOLD)
            {
                // Check if over another window
                var targetWindow = FindWindowUnderPoint(screenPoint);
                if (targetWindow != null && targetWindow != Window.GetWindow(this))
                {
                    // Over another window - transfer operation
                    var targetTabControl = FindTabControlInWindow(targetWindow);
                    if (targetTabControl != null)
                    {
                        return DragOperationType.Transfer;
                    }
                }
                
                // Not over another valid window - detach operation
                return DragOperationType.Detach;
            }
            
            // Close to tab strip but not quite inside - no operation yet
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
            
            // Hide and cleanup drag visual window
            if (_dragVisualWindow != null)
            {
                _dragVisualWindow.Hide();
                _dragVisualWindow.Close();
                _dragVisualWindow = null;
            }
            
            // Hide enhanced insertion indicators
            HideEnhancedInsertionIndicator();
            
            HideAllIndicators();
        }

        private void ShowReorderIndicator()
        {
            if (_currentDragOperation == null || _draggedTab == null)
                return;
                
            // Calculate drop index based on current mouse position
            var dropIndex = _tabOperationsManager?.CalculateDropIndex(
                this, 
                _currentDragOperation.CurrentScreenPoint) ?? -1;
                
            if (dropIndex < 0)
                return;
                
            // Create or update insertion indicator
            if (_insertionIndicator == null)
            {
                _insertionIndicator = new TabDropInsertionIndicator(this);
            }
            
            // Calculate position for indicator
            double indicatorX = 0;
            for (int i = 0; i < dropIndex && i < Items.Count; i++)
            {
                if (Items[i] is TabItem tab)
                {
                    indicatorX += tab.ActualWidth;
                }
            }
            
            // Show indicator at calculated position
            _insertionIndicator.UpdatePosition(indicatorX, TAB_STRIP_HEIGHT);
            _insertionIndicator.ShowIndicator();
        }

        private void ShowDetachIndicator()
        {
            if (_draggedTab == null || _currentDragOperation == null)
                return;
                
            // Create a semi-transparent preview window at cursor position
            if (_detachPreviewWindow == null)
            {
                _detachPreviewWindow = new Window
                {
                    WindowStyle = WindowStyle.None,
                    AllowsTransparency = true,
                    Background = new SolidColorBrush(Color.FromArgb(100, 0, 120, 215)),
                    Width = 300,
                    Height = 200,
                    ShowInTaskbar = false,
                    Topmost = true,
                    IsHitTestVisible = false
                };
                
                // Add visual content
                var border = new Border
                {
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                    BorderThickness = new Thickness(2),
                    CornerRadius = new CornerRadius(4),
                    Margin = new Thickness(10)
                };
                
                var textBlock = new TextBlock
                {
                    Text = _draggedTab.Header?.ToString() ?? "Tab",
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = Brushes.White,
                    FontSize = 16
                };
                
                border.Child = textBlock;
                _detachPreviewWindow.Content = border;
            }
            
            // Position preview at cursor
            var cursorPos = _currentDragOperation.CurrentScreenPoint;
            _detachPreviewWindow.Left = cursorPos.X - 150;
            _detachPreviewWindow.Top = cursorPos.Y - 20;
            
            if (!_detachPreviewWindow.IsVisible)
            {
                _detachPreviewWindow.Show();
            }
        }

        private void ShowTransferIndicator()
        {
            if (_currentDragOperation == null)
                return;
                
            // Find target window under cursor
            var targetWindow = FindWindowUnderPoint(_currentDragOperation.CurrentScreenPoint);
            if (targetWindow == null || targetWindow == Window.GetWindow(this))
                return;
                
            // Find tab control in target window
            var targetTabControl = FindTabControlInWindow(targetWindow);
            if (targetTabControl == null)
                return;
                
            // Highlight the target window border
            HighlightWindow(targetWindow);
            
            // Show insertion indicator in target tab control if it has one
            if (targetTabControl._insertionIndicator == null)
            {
                targetTabControl._insertionIndicator = new TabDropInsertionIndicator(targetTabControl);
            }
            
            var dropIndex = _tabOperationsManager?.CalculateDropIndex(
                targetTabControl, 
                _currentDragOperation.CurrentScreenPoint) ?? -1;
                
            if (dropIndex >= 0)
            {
                // Calculate position for indicator in target control
                double indicatorX = 0;
                for (int i = 0; i < dropIndex && i < targetTabControl.Items.Count; i++)
                {
                    if (targetTabControl.Items[i] is TabItem tab)
                    {
                        indicatorX += tab.ActualWidth;
                    }
                }
                
                targetTabControl._insertionIndicator.UpdatePosition(indicatorX, TAB_STRIP_HEIGHT);
                targetTabControl._insertionIndicator.CurrentState = TabDropInsertionIndicator.DropIndicatorState.ValidDrop;
                targetTabControl._insertionIndicator.ShowIndicator();
            }
        }

        private void HideAllIndicators()
        {
            // Hide insertion indicator
            _insertionIndicator?.HideIndicator();
            
            // Hide detach preview
            if (_detachPreviewWindow != null)
            {
                _detachPreviewWindow.Hide();
            }
            
            // Hide transfer highlights
            RemoveAllWindowHighlights();
            
            // Reset cursor
            Mouse.OverrideCursor = null;
        }
        
        private void HighlightWindow(Window targetWindow)
        {
            if (targetWindow == null) return;
            
            // Add a subtle glow effect to the target window
            try
            {
                var originalEffect = targetWindow.Effect;
                var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Color.FromRgb(0, 120, 215),
                    BlurRadius = 15,
                    ShadowDepth = 0,
                    Opacity = 0.6
                };
                
                targetWindow.Effect = glowEffect;
                
                // Store the original effect to restore later
                targetWindow.Tag = originalEffect;
            }
            catch
            {
                // Ignore errors with window highlighting
            }
        }
        
        private void RemoveAllWindowHighlights()
        {
            // Find all windows and remove highlights
            try
            {
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.Tag is System.Windows.Media.Effects.Effect originalEffect)
                    {
                        window.Effect = originalEffect;
                        window.Tag = null;
                    }
                    else if (window.Effect is System.Windows.Media.Effects.DropShadowEffect shadowEffect &&
                             shadowEffect.Color == Color.FromRgb(0, 120, 215))
                    {
                        window.Effect = null;
                    }
                }
            }
            catch
            {
                // Ignore errors with window highlighting cleanup
            }
        }

        #region Phase 5 Enhanced Visual Indicators

        /// <summary>
        /// Creates a floating window with tab preview that follows the cursor
        /// </summary>
        private Window CreateDragVisual(TabItem tabItem)
        {
            if (tabItem?.Tag is not TabItemModel tabModel)
                return null;

            // Create floating window with no chrome
            var dragWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                IsHitTestVisible = false,
                Width = Math.Max(tabItem.ActualWidth, 150),
                Height = 40,
                Opacity = 0.8
            };

            // Create visual content - tab preview
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 240, 248, 255)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4, 4, 0, 0),
                Margin = new Thickness(2)
            };

            // Add drop shadow effect
            border.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Colors.Black,
                BlurRadius = 8,
                ShadowDepth = 3,
                Opacity = 0.3
            };

            // Create content grid
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Add tab icon placeholder (folder icon for now)
            var iconPath = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse("M10,4H4C2.89,4 2,4.89 2,6V18A2,2 0 0,0 4,20H20A2,2 0 0,0 22,18V8C22,6.89 21.1,6 20,6H12L10,4Z"),
                Fill = new SolidColorBrush(Color.FromRgb(100, 149, 237)),
                Width = 14,
                Height = 14,
                Stretch = Stretch.Uniform,
                Margin = new Thickness(8, 0, 4, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(iconPath, 0);
            grid.Children.Add(iconPath);

            // Add tab title
            var titleText = new TextBlock
            {
                Text = tabModel.Title ?? "Tab",
                FontSize = 12,
                FontWeight = FontWeights.Normal,
                Foreground = new SolidColorBrush(Color.FromRgb(68, 68, 68)),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 8, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(titleText, 1);
            grid.Children.Add(titleText);

            border.Child = grid;
            dragWindow.Content = border;

            return dragWindow;
        }

        /// <summary>
        /// Creates or updates a vertical line indicator between tabs
        /// </summary>
        private void CreateInsertionIndicator()
        {
            if (_insertionLine != null)
                return;

            _insertionLine = new Rectangle
            {
                Width = 2,
                Height = TAB_STRIP_HEIGHT - 4,
                Fill = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                Opacity = 0,
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0)
            };

            // Add glow effect
            _insertionLine.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                Color = Color.FromRgb(0, 120, 215),
                BlurRadius = 6,
                ShadowDepth = 0,
                Opacity = 0.8
            };

            // Add to panel if available
            var panel = FindChildOfType<Panel>(this);
            panel?.Children.Add(_insertionLine);
        }

        /// <summary>
        /// Updates drag visual position to follow cursor
        /// </summary>
        private void UpdateDragVisualPosition(Point screenPoint)
        {
            if (_dragVisualWindow == null)
                return;

            // Position window slightly offset from cursor
            _dragVisualWindow.Left = screenPoint.X - (_dragVisualWindow.Width / 2);
            _dragVisualWindow.Top = screenPoint.Y - 20;

            // Keep window on screen
            var screen = SystemParameters.PrimaryScreenWidth;
            var screenHeight = SystemParameters.PrimaryScreenHeight;

            if (_dragVisualWindow.Left < 0)
                _dragVisualWindow.Left = 0;
            if (_dragVisualWindow.Left + _dragVisualWindow.Width > screen)
                _dragVisualWindow.Left = screen - _dragVisualWindow.Width;
            if (_dragVisualWindow.Top < 0)
                _dragVisualWindow.Top = 0;
            if (_dragVisualWindow.Top + _dragVisualWindow.Height > screenHeight)
                _dragVisualWindow.Top = screenPoint.Y + 20;
        }

        /// <summary>
        /// Updates insertion indicator visibility based on drop validity
        /// </summary>
        private void UpdateInsertionIndicatorVisibility(Point screenPoint)
        {
            if (_currentDragOperation == null)
                return;

            var operationType = DetermineOperationType(screenPoint);
            
            switch (operationType)
            {
                case DragOperationType.Reorder:
                    ShowEnhancedInsertionIndicator(screenPoint);
                    break;
                    
                case DragOperationType.Transfer:
                    // Show indicator in target tab control
                    var targetWindow = FindWindowUnderPoint(screenPoint);
                    var targetTabControl = targetWindow != null ? FindTabControlInWindow(targetWindow) : null;
                    if (targetTabControl != null)
                    {
                        targetTabControl.ShowEnhancedInsertionIndicator(screenPoint);
                    }
                    break;
                    
                default:
                    HideEnhancedInsertionIndicator();
                    break;
            }
        }

        /// <summary>
        /// Shows enhanced insertion indicator with smooth animation
        /// </summary>
        private void ShowEnhancedInsertionIndicator(Point screenPoint)
        {
            CreateInsertionIndicator();
            
            if (_insertionLine == null)
                return;

            // Calculate insertion position
            var localPoint = this.PointFromScreen(screenPoint);
            var insertionIndex = CalculateInsertionIndex(localPoint);
            var insertionX = CalculateInsertionPosition(insertionIndex);

            // Position the indicator
            Canvas.SetLeft(_insertionLine, insertionX);
            
            // Animate indicator appearance
            var fadeIn = new DoubleAnimation
            {
                From = _insertionLine.Opacity,
                To = 1.0,
                Duration = TimeSpan.FromMilliseconds(150),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };

            _insertionLine.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }

        /// <summary>
        /// Hides enhanced insertion indicator with smooth animation
        /// </summary>
        private void HideEnhancedInsertionIndicator()
        {
            if (_insertionLine == null)
                return;

            var fadeOut = new DoubleAnimation
            {
                From = _insertionLine.Opacity,
                To = 0.0,
                Duration = TimeSpan.FromMilliseconds(100),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
            };

            _insertionLine.BeginAnimation(UIElement.OpacityProperty, fadeOut);
        }

        /// <summary>
        /// Calculates insertion index based on mouse position
        /// </summary>
        private int CalculateInsertionIndex(Point localPoint)
        {
            var insertionIndex = 0;
            var accumulatedWidth = 0.0;

            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i] is TabItem tab && tab != _draggedTab)
                {
                    var tabCenter = accumulatedWidth + (tab.ActualWidth / 2);
                    if (localPoint.X <= tabCenter)
                        break;
                    insertionIndex = i + 1;
                }
                
                if (Items[i] is TabItem tabItem)
                    accumulatedWidth += tabItem.ActualWidth;
            }

            return Math.Min(insertionIndex, Items.Count);
        }

        /// <summary>
        /// Calculates insertion position in pixels
        /// </summary>
        private double CalculateInsertionPosition(int insertionIndex)
        {
            var position = 0.0;
            
            for (int i = 0; i < insertionIndex && i < Items.Count; i++)
            {
                if (Items[i] is TabItem tab)
                    position += tab.ActualWidth;
            }

            return position;
        }

        #endregion

        #endregion

        #region Helper Methods

        private void ResetDragState()
        {
            // Reset visual states before nulling references
            if (_draggedTab != null)
            {
                _draggedTab.Opacity = 1.0;
                _draggedTab.RenderTransform = null;
            }
            
            // Hide and cleanup drag visual window
            if (_dragVisualWindow != null)
            {
                _dragVisualWindow.Hide();
                _dragVisualWindow.Close();
                _dragVisualWindow = null;
            }
            
            // Hide enhanced insertion indicators
            HideEnhancedInsertionIndicator();
            
            // Hide all visual indicators
            HideAllIndicators();
            
            // CRITICAL: Release mouse capture to allow future drags
            if (IsMouseCaptured)
            {
                ReleaseMouseCapture();
            }
            
            // Clear state variables
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

        private bool IsCloseButton(object source)
        {
            var element = source as DependencyObject;
            while (element != null)
            {
                if (element is Button button && 
                    (button.Name == "CloseTabButton" || 
                     button.Name == "PART_CloseButton" ||
                     button.GetType().Name.Contains("CloseButton")))
                    return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        private bool IsValidTabForDrag(TabItem tabItem)
        {
            if (tabItem == null) return false;

            // Check if the tab is associated with a valid TabItemModel
            if (tabItem.Tag is TabItemModel tabModel)
            {
                // Don't allow dragging of pinned tabs in some scenarios
                // For now, allow all tabs to be dragged
                return true;
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
            
            // Wire up event handlers for dynamically created buttons
            WireUpTabControlEvents();
            
            // Ensure we have at least one tab if none exist
            if (TabItems?.Count == 0 && AllowAddNew)
            {
                AddNewTab();
            }
        }

        /// <summary>
        /// Wires up event handlers for tab control buttons
        /// </summary>
        private void WireUpTabControlEvents()
        {
            try
            {
                // Find and wire up the AddTabButton in the template
                var addTabButton = FindNameInTemplate("AddTabButton") as Button;
                if (addTabButton != null)
                {
                    addTabButton.Click -= OnAddTabButtonClick; // Remove any existing handler
                    addTabButton.Click += OnAddTabButtonClick;
                }

                // Wire up close button clicks for existing tabs
                foreach (TabItem tabItem in Items)
                {
                    WireUpTabItemEvents(tabItem);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to wire up tab control events");
            }
        }

        /// <summary>
        /// Wires up events for a specific tab item
        /// </summary>
        private void WireUpTabItemEvents(TabItem tabItem)
        {
            try
            {
                // Find close button in the tab item template
                var closeButton = FindChildOfType<Button>(tabItem, "CloseButton");
                if (closeButton != null)
                {
                    closeButton.Click -= OnTabCloseButtonClick; // Remove any existing handler
                    closeButton.Click += OnTabCloseButtonClick;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to wire up tab item events");
            }
        }

        /// <summary>
        /// Finds a child control of specific type and name in the visual tree
        /// </summary>
        private T FindChildOfType<T>(DependencyObject parent, string name = null) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild && (name == null || (child as FrameworkElement)?.Name == name))
                {
                    return typedChild;
                }

                var result = FindChildOfType<T>(child, name);
                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

        /// <summary>
        /// Finds a named element in the control template
        /// </summary>
        private object FindNameInTemplate(string name)
        {
            try
            {
                var template = this.Template;
                if (template != null)
                {
                    return template.FindName(name, this);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to find '{name}' in template");
            }

            return null;
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
        /// Handles tab close button clicks
        /// </summary>
        private void OnTabCloseButtonClick(object sender, RoutedEventArgs e)
        {
            var closeButton = sender as Button;
            if (closeButton != null)
            {
                // Find parent TabItem
                var tabItem = FindParent<TabItem>(closeButton);
                if (tabItem?.Tag is TabItemModel tabModel)
                {
                    CloseTab(tabModel);
                }
            }
        }

        /// <summary>
        /// Handles add tab button clicks
        /// </summary>
        private void OnAddTabButtonClick(object sender, RoutedEventArgs e)
        {
            if (AllowAddNew)
            {
                AddNewTab();
            }
        }

        /// <summary>
        /// Handles right-click context menu on tabs
        /// </summary>
        protected override void OnPreviewMouseRightButtonDown(MouseButtonEventArgs e)
        {
            base.OnPreviewMouseRightButtonDown(e);

            // Find which tab was right-clicked
            var tabItem = FindTabItemFromPoint(e.GetPosition(this));
            if (tabItem?.Tag is TabItemModel tabModel)
            {
                // Select the tab that was right-clicked
                SelectedItem = tabItem;

                // Show context menu
                ShowTabContextMenu(tabItem, tabModel, e.GetPosition(this));
                e.Handled = true;
            }
        }

        /// <summary>
        /// Shows the context menu for a tab
        /// </summary>
        private void ShowTabContextMenu(TabItem tabItem, TabItemModel tabModel, Point position)
        {
            var contextMenu = new ContextMenu();

            // New Tab
            var newTabItem = new MenuItem 
            { 
                Header = "New Tab", 
                Icon = new TextBlock { Text = "＋", FontSize = 12 }
            };
            newTabItem.Click += (s, e) => AddNewTab();
            contextMenu.Items.Add(newTabItem);

            contextMenu.Items.Add(new Separator());

            // Duplicate Tab
            var duplicateItem = new MenuItem 
            { 
                Header = "Duplicate Tab",
                Icon = new TextBlock { Text = "⧉", FontSize = 12 }
            };
            duplicateItem.Click += (s, e) => DuplicateTab(tabModel);
            contextMenu.Items.Add(duplicateItem);

            contextMenu.Items.Add(new Separator());

            // Pin/Unpin Tab
            var pinItem = new MenuItem 
            { 
                Header = tabModel.IsPinned ? "Unpin Tab" : "Pin Tab",
                Icon = new TextBlock { Text = tabModel.IsPinned ? "📌" : "📍", FontSize = 12 }
            };
            pinItem.Click += (s, e) => ToggleTabPin(tabModel);
            contextMenu.Items.Add(pinItem);

            // Change Color (if not pinned)
            if (!tabModel.IsPinned)
            {
                var colorItem = new MenuItem 
                { 
                    Header = "Change Color",
                    Icon = new TextBlock { Text = "🎨", FontSize = 12 }
                };
                colorItem.Click += (s, e) => ChangeTabColor(tabModel);
                contextMenu.Items.Add(colorItem);
            }

            contextMenu.Items.Add(new Separator());

            // Detach to New Window
            if (Items.Count > 1)
            {
                var detachItem = new MenuItem 
                { 
                    Header = "Move to New Window",
                    Icon = new TextBlock { Text = "🗗", FontSize = 12 }
                };
                detachItem.Click += (s, e) => DetachTab(tabModel);
                contextMenu.Items.Add(detachItem);

                contextMenu.Items.Add(new Separator());
            }

            // Close Tab
            if (tabModel.IsClosable && Items.Count > 1)
            {
                var closeItem = new MenuItem 
                { 
                    Header = "Close Tab",
                    Icon = new TextBlock { Text = "✕", FontSize = 12 }
                };
                closeItem.Click += (s, e) => CloseTab(tabModel);
                contextMenu.Items.Add(closeItem);

                // Close Other Tabs
                if (Items.Count > 2)
                {
                    var closeOthersItem = new MenuItem 
                    { 
                        Header = "Close Other Tabs",
                        Icon = new TextBlock { Text = "⧈", FontSize = 12 }
                    };
                    closeOthersItem.Click += (s, e) => CloseOtherTabs(tabModel);
                    contextMenu.Items.Add(closeOthersItem);
                }

                // Close Tabs to the Right
                var tabIndex = GetTabIndex(tabModel);
                if (tabIndex < Items.Count - 1)
                {
                    var closeRightItem = new MenuItem 
                    { 
                        Header = "Close Tabs to the Right",
                        Icon = new TextBlock { Text = "→✕", FontSize = 12 }
                    };
                    closeRightItem.Click += (s, e) => CloseTabsToTheRight(tabModel);
                    contextMenu.Items.Add(closeRightItem);
                }
            }

            // Show the context menu
            contextMenu.PlacementTarget = tabItem;
            contextMenu.Placement = PlacementMode.Bottom;
            contextMenu.IsOpen = true;
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
                
                // Wire up events for the new tab item
                WireUpTabItemEvents(tabItem);
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

        #region Tab Context Menu Methods

        /// <summary>
        /// Finds a parent of specified type in the visual tree
        /// </summary>
        private T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null) return null;

            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        /// <summary>
        /// Duplicates a tab
        /// </summary>
        private void DuplicateTab(TabItemModel tabModel)
        {
            try
            {
                // Create a new tab with the same content type
                var newTab = new TabItemModel(Guid.NewGuid().ToString(), $"{tabModel.Title} - Copy", tabModel.Content);
                newTab.Tooltip = tabModel.Tooltip;
                
                TabItems ??= new ObservableCollection<TabItemModel>();
                TabItems.Add(newTab);
                SelectedTabItem = newTab;

                _logger?.LogInformation($"Duplicated tab '{tabModel.Title}'");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to duplicate tab '{tabModel.Title}'");
            }
        }

        /// <summary>
        /// Toggles the pin state of a tab
        /// </summary>
        private void ToggleTabPin(TabItemModel tabModel)
        {
            try
            {
                tabModel.IsPinned = !tabModel.IsPinned;
                
                // Update visual representation
                var tabItem = Items.Cast<TabItem>().FirstOrDefault(t => t.Tag == tabModel);
                if (tabItem != null)
                {
                    UpdateTabItemFromModel(tabItem, tabModel);
                }

                _logger?.LogInformation($"Tab '{tabModel.Title}' {(tabModel.IsPinned ? "pinned" : "unpinned")}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to toggle pin for tab '{tabModel.Title}'");
            }
        }

        /// <summary>
        /// Changes the color of a tab
        /// </summary>
        private void ChangeTabColor(TabItemModel tabModel)
        {
            try
            {
                // Show color picker dialog
                var colorDialog = new System.Windows.Forms.ColorDialog();
                if (colorDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    // For now, just update the background of the tab item directly
                    var tabItem = Items.Cast<TabItem>().FirstOrDefault(t => t.Tag == tabModel);
                    if (tabItem != null)
                    {
                        var color = Color.FromArgb(colorDialog.Color.A, colorDialog.Color.R, colorDialog.Color.G, colorDialog.Color.B);
                        tabItem.Background = new SolidColorBrush(color);
                    }

                    _logger?.LogInformation($"Changed color for tab '{tabModel.Title}'");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to change color for tab '{tabModel.Title}'");
            }
        }

        /// <summary>
        /// Detaches a tab to a new window
        /// </summary>
        private void DetachTab(TabItemModel tabModel)
        {
            try
            {
                var mainWindow = Window.GetWindow(this) as ExplorerPro.UI.MainWindow.MainWindow;
                if (mainWindow != null)
                {
                    var tabItem = Items.Cast<TabItem>().FirstOrDefault(t => t.Tag == tabModel);
                    if (tabItem != null)
                    {
                        var detachedWindow = mainWindow.DetachTabToNewWindow(tabItem);
                        if (detachedWindow != null)
                        {
                            _logger?.LogInformation($"Detached tab '{tabModel.Title}' to new window");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to detach tab '{tabModel.Title}'");
            }
        }

        /// <summary>
        /// Closes all tabs except the specified one
        /// </summary>
        private void CloseOtherTabs(TabItemModel keepTab)
        {
            try
            {
                var tabsToClose = TabItems?.Where(t => t != keepTab && t.IsClosable).ToList();
                if (tabsToClose != null)
                {
                    foreach (var tab in tabsToClose)
                    {
                        CloseTab(tab);
                    }
                }

                _logger?.LogInformation($"Closed other tabs, kept '{keepTab.Title}'");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to close other tabs");
            }
        }

        /// <summary>
        /// Closes all tabs to the right of the specified tab
        /// </summary>
        private void CloseTabsToTheRight(TabItemModel fromTab)
        {
            try
            {
                if (TabItems == null) return;

                var fromIndex = TabItems.IndexOf(fromTab);
                if (fromIndex >= 0)
                {
                    var tabsToClose = TabItems.Skip(fromIndex + 1).Where(t => t.IsClosable).ToList();
                    foreach (var tab in tabsToClose)
                    {
                        CloseTab(tab);
                    }
                }

                _logger?.LogInformation($"Closed tabs to the right of '{fromTab.Title}'");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to close tabs to the right");
            }
        }

        #endregion

        #region IDisposable Implementation

        private bool _disposed = false;

        /// <summary>
        /// Disposes the control and its resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected disposal method
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    _insertionIndicator?.Dispose();
                    _insertionIndicator = null;
                    
                    if (_detachPreviewWindow != null)
                    {
                        _detachPreviewWindow.Close();
                        _detachPreviewWindow = null;
                    }
                    
                    // Clean up window highlights
                    RemoveAllWindowHighlights();
                }

                _disposed = true;
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