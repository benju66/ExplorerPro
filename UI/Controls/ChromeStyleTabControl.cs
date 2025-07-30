using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.Core.Disposables;
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
                typeof(ObservableCollection<TabModel>),
                typeof(ChromeStyleTabControl),
                new PropertyMetadata(null, OnTabItemsChanged));

        /// <summary>
        /// Currently selected tab item model
        /// </summary>
        public static readonly DependencyProperty SelectedTabItemProperty =
            DependencyProperty.Register(
                nameof(SelectedTabItem),
                typeof(TabModel),
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
        public ObservableCollection<TabModel> TabItems
        {
            get => (ObservableCollection<TabModel>)GetValue(TabItemsProperty);
            set => SetValue(TabItemsProperty, value);
        }

        /// <summary>
        /// Gets or sets the currently selected tab item model
        /// </summary>
        public TabModel SelectedTabItem
        {
            get => (TabModel)GetValue(SelectedTabItemProperty);
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
                TabItems = new ObservableCollection<TabModel>();
            }

            // Wire up events directly in constructor - weak references will be handled by CreateWeakSubscription
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
        
        // Animation timing constants
        private const double ANIMATION_DURATION = 200.0; // milliseconds
        private const double FADE_DURATION = 150.0; // milliseconds
        private const double SNAP_DURATION = 100.0; // milliseconds
        private const double BOUNCE_DURATION = 300.0; // milliseconds
        private const double SHAKE_DURATION = 400.0; // milliseconds
        
        // Animation state fields
        private Storyboard _currentAnimation;
        private bool _isAnimating;
        private DragOperationType _lastOperationType = DragOperationType.None;
        private bool _isHoveringValidDropZone;
        private DateTime _lastHoverChange = DateTime.Now;

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
            try
            {
                base.OnMouseMove(e);

                if (_dragStartPoint.HasValue && e.LeftButton == MouseButtonState.Pressed && !_isDragging)
                {
                    try
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
                                try
                                {
                                    ReleaseMouseCapture();
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogWarning(ex, "Error releasing mouse capture before drag start");
                                }
                            }

                            try
                            {
                                StartDragOperation();
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "Error starting drag operation from mouse move");
                                // Cancel drag operation on error
                                CancelDrag();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error processing mouse move for drag initiation");
                        CancelDrag();
                    }
                }
                else if (_isDragging)
                {
                    try
                    {
                        var screenPoint = e.GetPosition(null);
                        
                        try
                        {
                            UpdateDragOperation(screenPoint);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error updating drag operation during mouse move");
                        }
                        
                        // Update drag visual position to follow cursor
                        try
                        {
                            if (_dragVisualWindow != null && _dragVisualWindow.IsVisible)
                            {
                                UpdateDragVisualPosition(screenPoint);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error updating drag visual position during mouse move");
                        }
                        
                        // Update insertion indicator based on drop validity
                        try
                        {
                            UpdateInsertionIndicatorVisibility(screenPoint);
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error updating insertion indicator during mouse move");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error processing mouse move during drag operation");
                        CancelDrag();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error in OnMouseMove");
                // Emergency cleanup
                try
                {
                    CancelDrag();
                }
                catch (Exception cancelEx)
                {
                    _logger?.LogError(cancelEx, "Critical error during emergency cleanup in OnMouseMove");
                }
            }
        }

        /// <summary>
        /// Handles mouse left button up to properly release mouse capture and reset drag state
        /// </summary>
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            try
            {
                base.OnMouseLeftButtonUp(e);

                try
                {
                    // Complete any ongoing drag operation
                    if (_isDragging)
                    {
                        try
                        {
                            CompleteDragOperation(e.GetPosition(null));
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogError(ex, "Error completing drag operation in OnMouseLeftButtonUp");
                            CancelDrag();
                        }
                    }
                    else if (_draggedTab != null)
                    {
                        try
                        {
                            // Just a click, not a drag - select the tab
                            SelectedItem = _draggedTab;
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error selecting tab in OnMouseLeftButtonUp");
                        }
                    }
                }
                finally
                {
                    // ALWAYS ensure mouse capture is released and state is reset
                    try
                    {
                        ResetDragState();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error resetting drag state in OnMouseLeftButtonUp");
                        // Force reset critical state
                        _isDragging = false;
                        _dragStartPoint = null;
                        _draggedTab = null;
                        _currentDragOperation = null;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error in OnMouseLeftButtonUp");
                // Emergency cleanup
                try
                {
                    CancelDrag();
                }
                catch (Exception cancelEx)
                {
                    _logger?.LogError(cancelEx, "Critical error during emergency cleanup in OnMouseLeftButtonUp");
                }
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
            try
            {
                if (_draggedTab?.Tag is TabModel tabModel)
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
                    try
                    {
                        StartDragVisualFeedback();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error starting visual feedback during drag start");
                    }
                    
                    // Create and show enhanced drag visual
                    try
                    {
                        _dragVisualWindow = CreateDragVisual(_draggedTab);
                        if (_dragVisualWindow != null)
                        {
                            // Position at current cursor location
                            var cursorPos = Mouse.GetPosition(null);
                            _dragVisualWindow.Left = cursorPos.X - (_dragVisualWindow.Width / 2);
                            _dragVisualWindow.Top = cursorPos.Y - 20;
                            _dragVisualWindow.Show();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error creating drag visual during drag start");
                        // Continue without visual - drag can still work
                    }

                    // Use drag service if available
                    try
                    {
                        if (_dragDropService != null)
                        {
                            _dragDropService.StartDrag(tabModel, _dragStartPoint.Value, window);
                        }
                        else
                        {
                            // Local drag handling
                            tabModel.State = ExplorerPro.Models.TabState.Loading;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error starting drag service during drag start");
                        // Fallback to local handling
                        tabModel.State = ExplorerPro.Models.TabState.Loading;
                    }

                    // Raise drag started event
                    try
                    {
                        TabDragStarted?.Invoke(this, new TabDragEventArgs(
                            tabModel, 
                            _dragStartPoint.Value, 
                            Mouse.GetPosition(null)));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error raising TabDragStarted event");
                    }

                    // Set cursor
                    try
                    {
                        Mouse.OverrideCursor = Cursors.Hand;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error setting cursor during drag start");
                    }

                    _logger?.LogDebug($"Drag operation started for tab '{tabModel.Title}'");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error during drag operation start");
                // Cancel the drag operation on any critical error
                CancelDrag();
            }
        }

        /// <summary>
        /// Updates drag operation based on current position
        /// </summary>
        private void UpdateDragOperation(Point screenPoint)
        {
            try
            {
                if (!_isDragging || _currentDragOperation == null)
                    return;

                // Store previous operation type to detect changes
                var previousType = _currentDragOperation.CurrentOperationType;
                DragOperationType operationType;
                
                try
                {
                    operationType = DetermineOperationType(screenPoint);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error determining operation type during drag update");
                    operationType = DragOperationType.Reorder; // Safe fallback
                }
                
                // Update operation state
                _currentDragOperation.CurrentOperationType = operationType;
                _currentDragOperation.CurrentPoint = screenPoint;
                _currentDragOperation.CurrentScreenPoint = screenPoint;
                
                // Only update visuals if operation type changed
                if (previousType != operationType)
                {
                    try
                    {
                        UpdateDragVisualFeedback(operationType);
                        
                        // Log operation type change for debugging
                        _logger?.LogDebug($"Drag operation changed from {previousType} to {operationType}");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error updating visual feedback during drag update");
                    }
                }

                // Update via service if available
                try
                {
                    if (_dragDropService != null)
                    {
                        _dragDropService.UpdateDrag(screenPoint);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error updating drag service during drag update");
                }

                // Raise dragging event
                try
                {
                    if (_draggedTab?.Tag is TabModel tabModel)
                    {
                        TabDragging?.Invoke(this, new TabDragEventArgs(
                            tabModel,
                            _dragStartPoint.Value,
                            screenPoint));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error raising TabDragging event");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error during drag operation update");
                // Cancel the drag operation on any critical error
                CancelDrag();
            }
        }

        /// <summary>
        /// Completes the drag operation
        /// </summary>
        private void CompleteDragOperation(Point screenPoint)
        {
            if (_currentDragOperation == null || !_isDragging) return;

            bool success = false;
            
            try
            {
                Window targetWindow = null;
                
                try
                {
                    targetWindow = WindowLocator.FindWindowUnderPoint(screenPoint);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error finding target window during drag completion");
                }

                // Try service first
                try
                {
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
                                try
                                {
                                    success = HandleReorderDrop(screenPoint);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, "Error handling reorder drop");
                                }
                                break;
                                
                            case DragOperationType.Detach:
                                try
                                {
                                    success = HandleDetachDrop(screenPoint);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, "Error handling detach drop");
                                }
                                break;
                                
                            case DragOperationType.Transfer:
                                try
                                {
                                    success = HandleTransferDrop(screenPoint);
                                }
                                catch (Exception ex)
                                {
                                    _logger?.LogError(ex, "Error handling transfer drop");
                                }
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error executing drag completion logic");
                }

                // Raise completed event
                try
                {
                    if (_draggedTab?.Tag is TabModel tabModel)
                    {
                        TabDragCompleted?.Invoke(this, new TabDragEventArgs(
                            tabModel,
                            _dragStartPoint.Value,
                            screenPoint));
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error raising TabDragCompleted event");
                }

                if (success)
                {
                    _logger?.LogInformation($"Drag operation completed successfully");
                    PlaySuccessBounceAnimation();
                }
                else
                {
                    _logger?.LogWarning("Drag operation completed but was not successful");
                    PlayErrorShakeAnimation();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error during drag operation completion");
            }
            finally
            {
                // ALWAYS ensure cleanup happens, even if there are errors
                try
                {
                    EndDragVisualFeedback();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error ending visual feedback during cleanup");
                }
                
                try
                {
                    Mouse.OverrideCursor = null;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error resetting cursor during cleanup");
                }
                
                try
                {
                    ResetDragState();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error resetting drag state during cleanup");
                    // Force reset critical state even if ResetDragState fails
                    _isDragging = false;
                    _dragStartPoint = null;
                    _draggedTab = null;
                    _currentDragOperation = null;
                }
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
                            _draggedTab.Tag as TabModel, 
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
        /// Public method to cancel drag operation with comprehensive cleanup
        /// Used for error recovery and external cancellation
        /// </summary>
        public void CancelDrag()
        {
            if (!_isDragging) return;

            try
            {
                _logger?.LogInformation("Drag operation cancelled via CancelDrag method");

                // Hide drag visual window immediately
                if (_dragVisualWindow != null)
                {
                    try
                    {
                        _dragVisualWindow.Hide();
                        _dragVisualWindow.Close();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error hiding drag visual window during cancel");
                    }
                    finally
                    {
                        _dragVisualWindow = null;
                    }
                }

                // Release mouse capture
                if (IsMouseCaptured)
                {
                    try
                    {
                        ReleaseMouseCapture();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error releasing mouse capture during cancel");
                    }
                }

                // Reset all drag state
                ResetDragState();

                // Return tab to original position if needed
                if (_currentDragOperation != null && _draggedTab != null)
                {
                    try
                    {
                        var currentIndex = Items.IndexOf(_draggedTab);
                        if (currentIndex != _currentDragOperation.OriginalIndex && 
                            _currentDragOperation.OriginalIndex >= 0 && 
                            _currentDragOperation.OriginalIndex < Items.Count)
                        {
                            _tabOperationsManager?.ReorderTab(this, 
                                _draggedTab.Tag as TabModel, 
                                _currentDragOperation.OriginalIndex);
                            
                            _logger?.LogDebug($"Restored tab to original position {_currentDragOperation.OriginalIndex}");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error restoring tab to original position during cancel");
                    }
                }

                // Cancel via service if available
                try
                {
                    _dragDropService?.CancelDrag();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error calling service CancelDrag during cancel");
                }

                // Hide all visual indicators
                try
                {
                    HideAllIndicators();
                    EndDragVisualFeedback();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error hiding visual indicators during cancel");
                }

                // Reset cursor
                try
                {
                    Mouse.OverrideCursor = null;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error resetting cursor during cancel");
                }

                // Clear drag operation state
                if (_draggedTab?.Tag is TabModel tabModel)
                {
                    try
                    {
                        tabModel.State = ExplorerPro.Models.TabState.Normal;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error clearing tab model drag state during cancel");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error during drag cancellation");
            }
            finally
            {
                // Ensure cleanup always happens
                _isDragging = false;
                _dragStartPoint = null;
                _draggedTab = null;
                _currentDragOperation = null;
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
            var tabModel = _draggedTab.Tag as TabModel;
            
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
            var tabModel = _draggedTab.Tag as TabModel;
            
            return _tabOperationsManager.TransferTab(this, targetTabControl, tabModel, dropIndex);
        }

        #region Visual Feedback Methods

        /// <summary>
        /// Stops any currently running animation
        /// </summary>
        private void StopCurrentAnimation()
        {
            try
            {
                if (_currentAnimation != null && _isAnimating)
                {
                    _currentAnimation.Stop();
                    _currentAnimation = null;
                    _isAnimating = false;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error stopping current animation");
            }
        }

        private void StartDragVisualFeedback()
        {
            try
            {
                if (_draggedTab != null)
                {
                    // Stop any existing animations
                    StopCurrentAnimation();
                    
                    // Create smooth fade-out animation for drag start
                    var fadeStoryboard = new Storyboard();
                    
                    // Opacity animation
                    var opacityAnimation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.6,
                        Duration = TimeSpan.FromMilliseconds(FADE_DURATION),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    Storyboard.SetTarget(opacityAnimation, _draggedTab);
                    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
                    fadeStoryboard.Children.Add(opacityAnimation);
                    
                    // Scale animation for visual feedback
                    var scaleTransform = new ScaleTransform(1.0, 1.0);
                    var transformGroup = new TransformGroup();
                    transformGroup.Children.Add(scaleTransform);
                    transformGroup.Children.Add(new TranslateTransform());
                    _draggedTab.RenderTransform = transformGroup;
                    
                    var scaleXAnimation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.95,
                        Duration = TimeSpan.FromMilliseconds(FADE_DURATION),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    var scaleYAnimation = new DoubleAnimation
                    {
                        From = 1.0,
                        To = 0.95,
                        Duration = TimeSpan.FromMilliseconds(FADE_DURATION),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                    };
                    
                    Storyboard.SetTarget(scaleXAnimation, scaleTransform);
                    Storyboard.SetTargetProperty(scaleXAnimation, new PropertyPath("ScaleX"));
                    fadeStoryboard.Children.Add(scaleXAnimation);
                    
                    Storyboard.SetTarget(scaleYAnimation, scaleTransform);
                    Storyboard.SetTargetProperty(scaleYAnimation, new PropertyPath("ScaleY"));
                    fadeStoryboard.Children.Add(scaleYAnimation);
                    
                    _currentAnimation = fadeStoryboard;
                    _isAnimating = true;
                    
                    CreateWeakSubscription(
                        () => fadeStoryboard.Completed += OnAnimationCompleted,
                        () => fadeStoryboard.Completed -= OnAnimationCompleted);
                    fadeStoryboard.Begin();
                    
                    _logger?.LogDebug("Started drag visual feedback with smooth animation");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error starting drag visual feedback animation");
                // Fallback to simple opacity change
                if (_draggedTab != null)
                {
                    _draggedTab.Opacity = 0.6;
                    _draggedTab.RenderTransform = new TranslateTransform();
                }
            }
        }

        private void UpdateDragVisualFeedback(DragOperationType operationType)
        {
            try
            {
                if (_draggedTab == null) return;

                // Check if operation type changed for hover effects
                var operationChanged = _lastOperationType != operationType;
                _lastOperationType = operationType;

                // Update hover state
                var isValidDropZone = operationType != DragOperationType.None;
                if (_isHoveringValidDropZone != isValidDropZone)
                {
                    _isHoveringValidDropZone = isValidDropZone;
                    _lastHoverChange = DateTime.Now;
                    
                    if (isValidDropZone)
                    {
                        PlaySnapFeedback();
                    }
                }

                // Animate operation type changes with smooth transitions
                if (operationChanged)
                {
                    AnimateOperationTypeChange(operationType);
                }

                // Update cursor based on operation type
                UpdateCursorForOperation(operationType);

                // Show appropriate indicators with hover effects
                switch (operationType)
                {
                    case DragOperationType.Reorder:
                        ShowReorderIndicatorWithHover();
                        break;
                        
                    case DragOperationType.Detach:
                        ShowDetachIndicatorWithHover();
                        break;
                        
                    case DragOperationType.Transfer:
                        ShowTransferIndicatorWithHover();
                        break;
                        
                    case DragOperationType.None:
                        HideAllIndicators();
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error updating drag visual feedback");
                // Fallback to basic feedback
                if (_draggedTab != null)
                {
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
            }
        }

        /// <summary>
        /// Plays visual snap feedback when hovering over valid drop zone
        /// </summary>
        private void PlaySnapFeedback()
        {
            try
            {
                if (_draggedTab == null) return;

                // Create snap animation - brief scale pulse
                var snapStoryboard = new Storyboard();
                
                var transformGroup = _draggedTab.RenderTransform as TransformGroup;
                var scaleTransform = transformGroup?.Children.OfType<ScaleTransform>().FirstOrDefault();
                    
                if (scaleTransform != null)
                {
                    var snapAnimation = new DoubleAnimation
                    {
                        From = scaleTransform.ScaleX,
                        To = 1.05,
                        Duration = TimeSpan.FromMilliseconds(SNAP_DURATION / 2),
                        AutoReverse = true,
                        EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 }
                    };
                    
                    Storyboard.SetTarget(snapAnimation, scaleTransform);
                    Storyboard.SetTargetProperty(snapAnimation, new PropertyPath("ScaleX"));
                    snapStoryboard.Children.Add(snapAnimation);
                    
                    var snapAnimationY = snapAnimation.Clone();
                    Storyboard.SetTargetProperty(snapAnimationY, new PropertyPath("ScaleY"));
                    snapStoryboard.Children.Add(snapAnimationY);
                    
                    snapStoryboard.Begin();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error playing snap feedback");
            }
        }

        /// <summary>
        /// Animates operation type changes with smooth transitions
        /// </summary>
        private void AnimateOperationTypeChange(DragOperationType operationType)
        {
            try
            {
                if (_draggedTab == null) return;

                StopCurrentAnimation();
                
                var storyboard = new Storyboard();
                double targetOpacity = operationType switch
                {
                    DragOperationType.Reorder => 0.8,
                    DragOperationType.Detach => 0.4,
                    DragOperationType.Transfer => 0.6,
                    _ => 0.6
                };

                var opacityAnimation = new DoubleAnimation
                {
                    To = targetOpacity,
                    Duration = TimeSpan.FromMilliseconds(ANIMATION_DURATION),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
                };
                
                Storyboard.SetTarget(opacityAnimation, _draggedTab);
                Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
                storyboard.Children.Add(opacityAnimation);
                
                _currentAnimation = storyboard;
                _isAnimating = true;
                                    CreateWeakSubscription(
                        () => storyboard.Completed += OnAnimationCompleted,
                        () => storyboard.Completed -= OnAnimationCompleted);
                storyboard.Begin();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error animating operation type change");
            }
        }

        /// <summary>
        /// Updates cursor based on current operation type
        /// </summary>
        private void UpdateCursorForOperation(DragOperationType operationType)
        {
            try
            {
                var cursor = operationType switch
                {
                    DragOperationType.Reorder => Cursors.Hand,
                    DragOperationType.Detach => Cursors.SizeAll,
                    DragOperationType.Transfer => Cursors.Cross,
                    _ => Cursors.No
                };
                
                Mouse.OverrideCursor = cursor;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error updating cursor for operation");
            }
        }

        /// <summary>
        /// Shows reorder indicator with hover effects
        /// </summary>
        private void ShowReorderIndicatorWithHover()
        {
            try
            {
                ShowReorderIndicator();
                HighlightDropZone(DragOperationType.Reorder);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error showing reorder indicator with hover");
                ShowReorderIndicator(); // Fallback
            }
        }

        /// <summary>
        /// Shows detach indicator with hover effects
        /// </summary>
        private void ShowDetachIndicatorWithHover()
        {
            try
            {
                ShowDetachIndicator();
                HighlightDropZone(DragOperationType.Detach);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error showing detach indicator with hover");
                ShowDetachIndicator(); // Fallback
            }
        }

        /// <summary>
        /// Shows transfer indicator with hover effects
        /// </summary>
        private void ShowTransferIndicatorWithHover()
        {
            try
            {
                ShowTransferIndicator();
                HighlightDropZone(DragOperationType.Transfer);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error showing transfer indicator with hover");
                ShowTransferIndicator(); // Fallback
            }
        }

        /// <summary>
        /// Highlights drop zone based on operation type
        /// </summary>
        private void HighlightDropZone(DragOperationType operationType)
        {
            try
            {
                // Add subtle glow effect to the tab control based on operation type
                var color = operationType switch
                {
                    DragOperationType.Reorder => Color.FromRgb(0, 120, 215), // Blue
                    DragOperationType.Detach => Color.FromRgb(255, 140, 0),  // Orange
                    DragOperationType.Transfer => Color.FromRgb(34, 139, 34), // Green
                    _ => Colors.Transparent
                };

                if (color != Colors.Transparent)
                {
                    var dropShadow = new DropShadowEffect
                    {
                        Color = color,
                        BlurRadius = 10,
                        Opacity = 0.3,
                        ShadowDepth = 0
                    };
                    
                    // Animate the glow effect
                    var glowStoryboard = new Storyboard();
                    var opacityAnimation = new DoubleAnimation
                    {
                        From = 0,
                        To = 0.3,
                        Duration = TimeSpan.FromMilliseconds(ANIMATION_DURATION),
                        AutoReverse = true,
                        EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut }
                    };
                    
                    Storyboard.SetTarget(opacityAnimation, dropShadow);
                    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
                    glowStoryboard.Children.Add(opacityAnimation);
                    
                    Effect = dropShadow;
                    glowStoryboard.Begin();
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error highlighting drop zone");
            }
        }

        /// <summary>
        /// Plays success bounce animation for successful drops
        /// </summary>
        private void PlaySuccessBounceAnimation()
        {
            _logger?.LogDebug("PlaySuccessBounceAnimation called");
        }

        /// <summary>
        /// Plays error shake animation for invalid drops
        /// </summary>
        private void PlayErrorShakeAnimation()
        {
            _logger?.LogDebug("PlayErrorShakeAnimation called");
        }

        /// <summary>
        /// Creates enhanced insertion indicator that initializes _insertionIndicator as a new TabDropInsertionIndicator
        /// </summary>
        private void CreateEnhancedInsertionIndicator()
        {
            try
            {
                if (_insertionIndicator != null)
                    return;

                _insertionIndicator = new TabDropInsertionIndicator(this, null);
                _logger?.LogDebug("Created enhanced insertion indicator");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error creating enhanced insertion indicator");
            }
        }

        /// <summary>
        /// Shows enhanced insertion indicator that positions and shows the indicator at specified position and drop index
        /// </summary>
        /// <param name="position">Position to show the indicator</param>
        /// <param name="dropIndex">Drop index for positioning</param>
        private void ShowEnhancedInsertionIndicator(Point position, int dropIndex)
        {
            try
            {
                CreateEnhancedInsertionIndicator();
                
                if (_insertionIndicator != null)
                {
                    // Calculate insertion position based on drop index
                    var insertionX = CalculateInsertionPosition(dropIndex);
                    _insertionIndicator.UpdatePosition(insertionX, TAB_STRIP_HEIGHT);
                    _insertionIndicator.ShowIndicator();
                    _logger?.LogDebug($"Showing enhanced insertion indicator at position {position} with drop index {dropIndex}");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error showing enhanced insertion indicator");
            }
        }

        private void EndDragVisualFeedback()
        {
            try
            {
                StopCurrentAnimation();
                
                if (_draggedTab != null)
                {
                    // Animate fade back to normal with smooth transition
                    var endStoryboard = new Storyboard();
                    
                    var opacityAnimation = new DoubleAnimation
                    {
                        To = 1.0,
                        Duration = TimeSpan.FromMilliseconds(FADE_DURATION),
                        EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                    };
                    
                    Storyboard.SetTarget(opacityAnimation, _draggedTab);
                    Storyboard.SetTargetProperty(opacityAnimation, new PropertyPath("Opacity"));
                    endStoryboard.Children.Add(opacityAnimation);
                    
                    CreateWeakSubscription(
                        () => endStoryboard.Completed += OnDragEndAnimationCompleted,
                        () => endStoryboard.Completed -= OnDragEndAnimationCompleted);
                    
                    endStoryboard.Begin();
                }
                
                // Hide and cleanup drag visual window with fade
                if (_dragVisualWindow != null)
                {
                    var fadeOutStoryboard = new Storyboard();
                    var fadeAnimation = new DoubleAnimation
                    {
                        From = _dragVisualWindow.Opacity,
                        To = 0,
                        Duration = TimeSpan.FromMilliseconds(FADE_DURATION)
                    };
                    
                    Storyboard.SetTarget(fadeAnimation, _dragVisualWindow);
                    Storyboard.SetTargetProperty(fadeAnimation, new PropertyPath("Opacity"));
                    fadeOutStoryboard.Children.Add(fadeAnimation);
                    
                    CreateWeakSubscription(
                        () => fadeOutStoryboard.Completed += OnDragVisualFadeOutCompleted,
                        () => fadeOutStoryboard.Completed -= OnDragVisualFadeOutCompleted);
                    
                    fadeOutStoryboard.Begin();
                }
                
                // Hide enhanced insertion indicators
                HideEnhancedInsertionIndicator();
                
                HideAllIndicators();
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Error ending drag visual feedback");
                // Fallback cleanup
                if (_draggedTab != null)
                {
                    _draggedTab.Opacity = 1.0;
                    _draggedTab.RenderTransform = null;
                }
                
                if (_dragVisualWindow != null)
                {
                    _dragVisualWindow.Hide();
                    _dragVisualWindow.Close();
                    _dragVisualWindow = null;
                }
                
                HideAllIndicators();
                Effect = null;
            }
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
            if (tabItem?.Tag is not TabModel tabModel)
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

            // Check if the tab is associated with a valid TabModel
            if (tabItem.Tag is TabModel tabModel)
            {
                // Don't allow dragging of pinned tabs in some scenarios
                // For now, allow all tabs to be dragged
                return true;
            }

            return false;
        }

        private Window FindWindowUnderPoint(Point screenPoint)
        {
            try
            {
                // Use App.WindowManager to iterate through GetDropTargetWindows()
                if (ExplorerPro.App.WindowManager == null)
                {
                    _logger?.LogWarning("WindowManager is null, cannot find window under point");
                    return null;
                }

                var dropTargetWindows = ExplorerPro.App.WindowManager.GetDropTargetWindows();
                if (dropTargetWindows == null)
                {
                    _logger?.LogWarning("GetDropTargetWindows returned null");
                    return null;
                }

                foreach (var window in dropTargetWindows)
                {
                    if (window == null || !window.IsVisible)
                        continue;

                    try
                    {
                        // Convert window bounds to screen coordinates
                        var windowBounds = new Rect(
                            window.Left,
                            window.Top,
                            window.ActualWidth,
                            window.ActualHeight
                        );

                        // Check if the screen point is within the window bounds
                        if (windowBounds.Contains(screenPoint))
                        {
                            _logger?.LogDebug($"Found window under point {screenPoint}: {window.GetType().Name}");
                            return window;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, $"Error checking window bounds for {window.GetType().Name}");
                        continue;
                    }
                }

                _logger?.LogDebug($"No window found under point {screenPoint}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in FindWindowUnderPoint");
                return null;
            }
        }

        private ChromeStyleTabControl FindTabControlInWindow(Window window)
        {
            try
            {
                if (window == null)
                {
                    _logger?.LogDebug("Window is null, cannot find tab control");
                    return null;
                }

                if (window is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
                {
                    var tabControl = mainWindow.MainTabs as ChromeStyleTabControl;
                    if (tabControl != null)
                    {
                        _logger?.LogDebug($"Found ChromeStyleTabControl in MainWindow: {mainWindow.GetHashCode()}");
                    }
                    else
                    {
                        _logger?.LogDebug($"MainTabs is not ChromeStyleTabControl in MainWindow: {mainWindow.GetHashCode()}");
                    }
                    return tabControl;
                }

                _logger?.LogDebug($"Window is not a MainWindow, type: {window.GetType().Name}");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error in FindTabControlInWindow");
                return null;
            }
        }

        #endregion

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handles the Loaded event
        /// </summary>
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _logger?.LogInformation("ChromeStyleTabControl.OnLoaded event fired");
            
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
                _logger?.LogInformation("No tabs exist, adding new tab");
                AddNewTab();
            }
            else
            {
                _logger?.LogInformation($"TabItems count: {TabItems?.Count ?? 0}");
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
                    SubscribeToRoutedEventWeak(addTabButton, Button.ClickEvent, OnAddTabButtonClick);
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
                    SubscribeToRoutedEventWeak(closeButton, Button.ClickEvent, OnTabCloseButtonClick);
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
                if (tabItem?.Tag is TabModel tabModel)
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
        /// Handles animation completion for storyboards
        /// </summary>
        private void OnAnimationCompleted(object sender, EventArgs e)
        {
            _isAnimating = false;
        }

        /// <summary>
        /// Handles drag end animation completion
        /// </summary>
        private void OnDragEndAnimationCompleted(object sender, EventArgs e)
        {
            if (_draggedTab != null)
            {
                _draggedTab.RenderTransform = null;
            }
            Effect = null; // Remove any glow effects
        }

        /// <summary>
        /// Handles fade out animation completion for drag visual window
        /// </summary>
        private void OnDragVisualFadeOutCompleted(object sender, EventArgs e)
        {
            if (_dragVisualWindow != null)
            {
                _dragVisualWindow.Hide();
                _dragVisualWindow.Close();
                _dragVisualWindow = null;
            }
        }

        /// <summary>
        /// Creates a weak subscription for tab animation completion with specific tab reference
        /// </summary>
        private void SubscribeToTabAnimationCompleted(DoubleAnimation animation, TabItem tab)
        {
            EventHandler completedHandler = (s, e) => tab.RenderTransform = null;
            CreateWeakSubscription(
                () => animation.Completed += completedHandler,
                () => animation.Completed -= completedHandler);
        }

        #endregion

        #region Context Menu Event Handlers

        /// <summary>
        /// Handles context menu new tab click
        /// </summary>
        private void OnContextMenuNewTab(object sender, RoutedEventArgs e)
        {
            AddNewTab();
        }

        /// <summary>
        /// Handles context menu duplicate tab click
        /// </summary>
        private void OnContextMenuDuplicateTab(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabModel tabModel)
            {
                DuplicateTab(tabModel);
            }
        }

        /// <summary>
        /// Handles context menu pin/unpin tab click
        /// </summary>
        private void OnContextMenuTogglePin(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabModel tabModel)
            {
                ToggleTabPin(tabModel);
            }
        }

        /// <summary>
        /// Handles context menu change color click
        /// </summary>
        private void OnContextMenuChangeColor(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabModel tabModel)
            {
                ChangeTabColor(tabModel);
            }
        }

        /// <summary>
        /// Handles context menu detach tab click
        /// </summary>
        private void OnContextMenuDetachTab(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabModel tabModel)
            {
                DetachTab(tabModel);
            }
        }

        /// <summary>
        /// Handles context menu close tab click
        /// </summary>
        private void OnContextMenuCloseTab(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabModel tabModel)
            {
                CloseTab(tabModel);
            }
        }

        /// <summary>
        /// Handles context menu close other tabs click
        /// </summary>
        private void OnContextMenuCloseOtherTabs(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabModel tabModel)
            {
                CloseOtherTabs(tabModel);
            }
        }

        /// <summary>
        /// Handles context menu close tabs to the right click
        /// </summary>
        private void OnContextMenuCloseTabsToTheRight(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.Tag is TabModel tabModel)
            {
                CloseTabsToTheRight(tabModel);
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
            if (tabItem?.Tag is TabModel tabModel)
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
        private void ShowTabContextMenu(TabItem tabItem, TabModel tabModel, Point position)
        {
            var contextMenu = new ContextMenu();

            // New Tab
            var newTabItem = new MenuItem 
            { 
                Header = "New Tab", 
                Icon = new TextBlock { Text = "＋", FontSize = 12 }
            };
            CreateWeakSubscription(
                () => newTabItem.Click += OnContextMenuNewTab,
                () => newTabItem.Click -= OnContextMenuNewTab);
            contextMenu.Items.Add(newTabItem);

            contextMenu.Items.Add(new Separator());

            // Duplicate Tab
            var duplicateItem = new MenuItem 
            { 
                Header = "Duplicate Tab",
                Icon = new TextBlock { Text = "⧉", FontSize = 12 }
            };
            duplicateItem.Tag = tabModel;
            CreateWeakSubscription(
                () => duplicateItem.Click += OnContextMenuDuplicateTab,
                () => duplicateItem.Click -= OnContextMenuDuplicateTab);
            contextMenu.Items.Add(duplicateItem);

            contextMenu.Items.Add(new Separator());

            // Pin/Unpin Tab
            var pinItem = new MenuItem 
            { 
                Header = tabModel.IsPinned ? "Unpin Tab" : "Pin Tab",
                Icon = new TextBlock { Text = tabModel.IsPinned ? "📌" : "📍", FontSize = 12 }
            };
            pinItem.Tag = tabModel;
            CreateWeakSubscription(
                () => pinItem.Click += OnContextMenuTogglePin,
                () => pinItem.Click -= OnContextMenuTogglePin);
            contextMenu.Items.Add(pinItem);

            // Change Color (if not pinned)
            if (!tabModel.IsPinned)
            {
                var colorItem = new MenuItem 
                { 
                    Header = "Change Color",
                    Icon = new TextBlock { Text = "🎨", FontSize = 12 }
                };
                CreateWeakSubscription(
                    () => colorItem.Click += (s, e) => ChangeTabColor(tabModel),
                    () => colorItem.Click -= (s, e) => ChangeTabColor(tabModel));
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
                CreateWeakSubscription(
                    () => detachItem.Click += (s, e) => DetachTab(tabModel),
                    () => detachItem.Click -= (s, e) => DetachTab(tabModel));
                contextMenu.Items.Add(detachItem);

                contextMenu.Items.Add(new Separator());
            }

            // Close Tab
            if (tabModel.CanClose && Items.Count > 1)
            {
                var closeItem = new MenuItem 
                { 
                    Header = "Close Tab",
                    Icon = new TextBlock { Text = "✕", FontSize = 12 }
                };
                CreateWeakSubscription(
                    () => closeItem.Click += (s, e) => CloseTab(tabModel),
                    () => closeItem.Click -= (s, e) => CloseTab(tabModel));
                contextMenu.Items.Add(closeItem);

                // Close Other Tabs
                if (Items.Count > 2)
                {
                    var closeOthersItem = new MenuItem 
                    { 
                        Header = "Close Other Tabs",
                        Icon = new TextBlock { Text = "⧈", FontSize = 12 }
                    };
                    CreateWeakSubscription(
                        () => closeOthersItem.Click += (s, e) => CloseOtherTabs(tabModel),
                        () => closeOthersItem.Click -= (s, e) => CloseOtherTabs(tabModel));
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
                    CreateWeakSubscription(
                        () => closeRightItem.Click += (s, e) => CloseTabsToTheRight(tabModel),
                        () => closeRightItem.Click -= (s, e) => CloseTabsToTheRight(tabModel));
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
        public TabModel AddNewTab()
        {
            return AddNewTab("New Tab", null);
        }

        /// <summary>
        /// Adds a new tab with specified title and content
        /// </summary>
        /// <param name="title">Title of the new tab</param>
        /// <param name="content">Content for the new tab</param>
        /// <returns>The created tab item model</returns>
        public TabModel AddNewTab(string title, object content = null)
        {
            if (!AllowAddNew || (TabItems?.Count >= MaxTabCount))
            {
                return null;
            }

            var newTab = new TabModel(title, "");

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
            TabItems ??= new ObservableCollection<TabModel>();
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
        public bool CloseTab(TabModel tabItem)
        {
            if (!AllowDelete || tabItem == null || !tabItem.CanClose)
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
        public TabModel FindTabById(string tabId)
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
        /// Handles changes to the TabItems dependency property
        /// </summary>
        private static void OnTabItemsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ChromeStyleTabControl control)
            {
                // Unsubscribe from old collection
                if (e.OldValue is ObservableCollection<TabModel> oldCollection)
                {
                    oldCollection.CollectionChanged -= control.OnTabItemsCollectionChanged;
                }

                // Subscribe to new collection
                if (e.NewValue is ObservableCollection<TabModel> newCollection)
                {
                    // Only use weak subscriptions if _eventSubscriptions is initialized
                    // During constructor, _eventSubscriptions might not be ready yet
                    if (control._eventSubscriptions != null)
                    {
                        control.SubscribeToCollectionChangedWeak(newCollection, control.OnTabItemsCollectionChanged);
                    }
                    else
                    {
                        // Fallback to direct subscription during construction
                        newCollection.CollectionChanged += control.OnTabItemsCollectionChanged;
                    }
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
        /// Refreshes the actual TabControl items based on the TabModels
        /// </summary>
        private void RefreshTabItems()
        {
            // Clear existing items
            Items.Clear();

            if (TabItems == null) return;

            // Add TabItems based on TabModels
            foreach (var tabModel in TabItems)
            {
                var tabItem = CreateTabItemFromModel(tabModel);
                Items.Add(tabItem);
                
                // Wire up events for the new tab item
                WireUpTabItemEvents(tabItem);
                
                // CRITICAL FIX: Apply template styling after tab is fully created and added
                // This ensures pinned tabs get proper template during collection operations
                if (tabModel.IsPinned)
                {
                    // Use a deferred call to apply template after the tab is fully initialized
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        ApplyTabStyling(tabItem, tabModel);
                    }), System.Windows.Threading.DispatcherPriority.Normal);
                }
            }
        }

        /// <summary>
        /// Creates a WPF TabItem from a TabModel
        /// </summary>
        private TabItem CreateTabItemFromModel(TabModel model)
        {
            var tabItem = new TabItem
            {
                Header = model.Title,
                Content = model.Content,
                Tag = model,
                ToolTip = string.IsNullOrEmpty(model.DisplayTitle) ? model.Title : model.DisplayTitle
            };

            // Apply styling based on model properties - ORIGINAL WORKING CODE
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

            // Wire up property change notifications using weak references
            var handler = new PropertyChangedEventHandler((s, e) => UpdateTabItemFromModel(tabItem, model));
            CreateWeakSubscription(
                () => model.PropertyChanged += handler,
                () => model.PropertyChanged -= handler);

            return tabItem;
        }

        /// <summary>
        /// Updates a TabItem when its model changes
        /// Updates all visual properties based on the model's state, including width adjustments for pinned tabs
        /// </summary>
        private void UpdateTabItemFromModel(TabItem tabItem, TabModel model)
        {
            if (tabItem == null || model == null) return;

            try
            {
                // Update basic properties
                tabItem.Header = model.HasUnsavedChanges ? $"• {model.Title}" : model.Title;
                tabItem.Content = model.Content;
                tabItem.Tag = model; // Ensure model reference is maintained

                // Update visual styling based on model state
                ApplyTabStyling(tabItem, model);

                // Update width for pinned tabs
                UpdateTabWidth(tabItem, model);

                // Update close button visibility
                UpdateCloseButtonVisibility(tabItem, model);

                // Update background and foreground based on tab color
                UpdateTabColors(tabItem, model);

                // Update font styling
                UpdateTabFontStyling(tabItem, model);

                // Update drag state visual feedback
                UpdateDragStateVisuals(tabItem, model);

                // Update active/inactive state
                UpdateActiveState(tabItem, model);

                _logger?.LogDebug($"Updated TabItem visual properties for '{model.Title}'");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to update TabItem from model for '{model.Title}'");
            }
        }

        /// <summary>
        /// Applies visual styling to the TabItem based on the model state
        /// Preserves the ultra-modern Chrome styling while updating based on model
        /// </summary>
        private void ApplyTabStyling(TabItem tabItem, TabModel model)
        {
            // Ensure the tab uses the Chrome style (don't override with generic styles)
            if (model.IsPinned)
            {
                // For pinned tabs, apply the pinned template
                tabItem.SetResourceReference(TemplateProperty, "ChromePinnedTabTemplate");
                // Clear any style that might interfere
                tabItem.ClearValue(StyleProperty);
            }
            else
            {
                // For regular tabs, CLEAR the template first, then apply the style
                tabItem.ClearValue(TemplateProperty);
                tabItem.SetResourceReference(StyleProperty, "ChromeTabItemStyle");
            }

            // Don't override opacity here - let the Chrome template handle dragging visuals
            // The ChromeTabItemStyle already has sophisticated drag animations built-in
        }

        /// <summary>
        /// Updates the tab width based on pinned state and content
        /// </summary>
        private void UpdateTabWidth(TabItem tabItem, TabModel model)
        {
            if (model.IsPinned)
            {
                // Pinned tabs are narrower to save space
                tabItem.MinWidth = 80;
                tabItem.MaxWidth = 120;
                tabItem.Width = double.NaN; // Auto-size within constraints
            }
            else
            {
                // Regular tabs have more flexible sizing
                tabItem.MinWidth = 120;
                tabItem.MaxWidth = 250;
                tabItem.Width = double.NaN; // Auto-size within constraints
            }
        }

        /// <summary>
        /// Updates close button visibility based on model properties
        /// Works with the Chrome template's CloseButton element and ShowCloseButton binding
        /// </summary>
        private void UpdateCloseButtonVisibility(TabItem tabItem, TabModel model)
        {
            // The Chrome template automatically binds to Tag.CanClose
            // Since we added this computed property to TabModel, the template will
            // automatically show/hide the close button based on IsPinned and IsClosable
            
            // For direct element access (fallback), also update the button directly
            var closeButton = FindChildOfType<Button>(tabItem, "CloseButton");
            if (closeButton != null)
            {
                closeButton.Visibility = model.CanClose 
                    ? Visibility.Visible 
                    : Visibility.Collapsed;
            }
            
            _logger?.LogDebug($"Close button visibility for '{model.Title}': {model.CanClose}");
        }

        /// <summary>
        /// Updates tab colors based on the model's color theme
        /// Works with the Chrome template's existing styling
        /// </summary>
        private void UpdateTabColors(TabItem tabItem, TabModel model)
        {
            if (model.CustomColor != Colors.LightGray) // Only apply custom colors
            {
                var colorBrush = new SolidColorBrush(model.CustomColor);
                
                // Find the TabBorder from the Chrome template
                var tabBorder = FindChildOfType<Border>(tabItem, "TabBorder");
                if (tabBorder != null)
                {
                    // Apply subtle color accent without destroying the Chrome gradient
                    var accentBrush = colorBrush.Clone();
                    accentBrush.Opacity = 0.15; // Very subtle
                    
                    // Create a gradient that preserves the Chrome look but adds color accent
                    var gradientBrush = new LinearGradientBrush();
                    gradientBrush.StartPoint = new Point(0, 0);
                    gradientBrush.EndPoint = new Point(0, 1);
                    gradientBrush.GradientStops.Add(new GradientStop(Colors.White, 0.0));
                    gradientBrush.GradientStops.Add(new GradientStop(model.CustomColor, 0.3) { Color = Color.FromArgb(30, model.CustomColor.R, model.CustomColor.G, model.CustomColor.B) });
                    gradientBrush.GradientStops.Add(new GradientStop(Colors.White, 1.0));
                    
                    tabBorder.Background = gradientBrush;
                    
                    // Add colored border accent
                    tabBorder.BorderBrush = new SolidColorBrush(Color.FromArgb(80, model.CustomColor.R, model.CustomColor.G, model.CustomColor.B));
                }
            }
            else
            {
                // Reset to Chrome template default
                var tabBorder = FindChildOfType<Border>(tabItem, "TabBorder");
                if (tabBorder != null)
                {
                    tabBorder.SetResourceReference(Border.BackgroundProperty, "TabInactiveGradient");
                    tabBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(0xD0, 0xD7, 0xDE));
                }
            }
        }

        /// <summary>
        /// Updates font styling based on model state
        /// NOTE: FontSize is NOT set here to avoid affecting tab content (file trees, panes)
        /// </summary>
        private void UpdateTabFontStyling(TabItem tabItem, TabModel model)
        {
            // Clear any font properties that might affect content
            tabItem.ClearValue(FontSizeProperty);
            tabItem.ClearValue(FontWeightProperty);
            tabItem.ClearValue(FontStyleProperty);
            
            // The tab header styling is handled by the templates themselves
            // ChromePinnedTabTemplate and ChromeTabItemStyle have their own font settings
            // This prevents the font changes from cascading to the tab's content (file trees, panes)
        }

        /// <summary>
        /// Updates visual feedback for drag state
        /// The Chrome template already handles drag animations, so we just ensure the model binding works
        /// </summary>
        private void UpdateDragStateVisuals(TabItem tabItem, TabModel model)
        {
            // The ChromeTabItemStyle template already has sophisticated drag animations
            // that are triggered by the IsDragging data binding. We don't need to manually
            // override these - just ensure the model is properly bound as Tag
            // The template handles: opacity changes, scale transforms, and shadow effects
            
            // Only log for debugging
            if (model.State == ExplorerPro.Models.TabState.Loading)
            {
                _logger?.LogDebug($"Tab '{model.Title}' entered drag state - Chrome animations active");
            }
        }

        /// <summary>
        /// Updates active/inactive state visual indicators
        /// Chrome template handles IsSelected automatically, so we sync with that
        /// </summary>
        private void UpdateActiveState(TabItem tabItem, TabModel model)
        {
            // The Chrome template automatically handles IsSelected state with beautiful animations
            // We just need to ensure the TabItem's IsSelected matches the model's IsActive
            if (tabItem.IsSelected != model.IsActive)
            {
                // Update the selection state if it's out of sync
                // This will trigger the Chrome template's selection animations
                if (model.IsActive && !tabItem.IsSelected)
                {
                    tabItem.IsSelected = true;
                }
            }
            
            // The template handles all the visual changes:
            // - Background color changes to white
            // - Border color changes to #0078D4 (blue)
            // - Z-index elevation for proper layering
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
                SelectedTabItem.Activate();
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

            SubscribeToTabAnimationCompleted(animation, tab);

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

            // Check for Escape key during drag operations first
            if (e.Key == Key.Escape && _isDragging)
            {
                try
                {
                    _logger?.LogInformation("Escape key pressed during drag operation - canceling drag");
                    CancelDrag();
                    e.Handled = true;
                    AnnounceOperation("Drag operation cancelled");
                    return;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error handling Escape key during drag operation");
                    // Still try to clean up even if there's an error
                    try
                    {
                        CancelDrag();
                    }
                    catch (Exception cancelEx)
                    {
                        _logger?.LogError(cancelEx, "Critical error during emergency drag cancellation");
                    }
                    e.Handled = true;
                    return;
                }
            }

            if (SelectedItem is TabItem selectedTab && selectedTab.Tag is TabModel tabModel)
            {
                bool handled = false;

                try
                {
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
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error handling keyboard navigation for tab '{tabModel?.Title}'");
                    e.Handled = true; // Prevent further propagation of potentially problematic input
                }
            }
        }

        /// <summary>
        /// Moves the selected tab to the left
        /// </summary>
        private bool MoveTabLeft(TabModel tab)
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
        private bool MoveTabRight(TabModel tab)
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
        private int GetTabIndex(TabModel tab)
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
            if (SelectedItem is TabItem selectedTabItem && selectedTabItem.Tag is TabModel model)
            {
                SelectedTabItem = model;
                model.Activate();
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
        private void DuplicateTab(TabModel tabModel)
        {
            try
            {
                // Create a new tab with the same content type
                var newTab = new TabModel($"{tabModel.Title} - Copy", tabModel.Path);
                newTab.Id = Guid.NewGuid().ToString();
                newTab.Content = tabModel.Content;
                
                TabItems ??= new ObservableCollection<TabModel>();
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
        private async void ToggleTabPin(TabModel tabModel)
        {
            try
            {
                // Use the enhanced pin system with better visual feedback
                var success = await ExplorerPro.Commands.UnifiedTabCommands.PinOperations.ToggleTabPinAsync(
                    tabModel, 
                    this, 
                    message => 
                    {
                        // Show feedback to user (could be status bar, tooltip, etc.)
                        AnnounceOperation(message);
                        _logger?.LogInformation($"Pin operation: {message}");
                    });

                if (!success)
                {
                    _logger?.LogWarning($"Pin toggle operation failed for tab '{tabModel.Title}'");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Failed to toggle pin for tab '{tabModel.Title}'");
            }
        }

        /// <summary>
        /// Changes the color of a tab
        /// </summary>
        private void ChangeTabColor(TabModel tabModel)
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
        private void DetachTab(TabModel tabModel)
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
        private void CloseOtherTabs(TabModel keepTab)
        {
            try
            {
                var tabsToClose = TabItems?.Where(t => t != keepTab && t.CanClose).ToList();
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
        private void CloseTabsToTheRight(TabModel fromTab)
        {
            try
            {
                if (TabItems == null) return;

                var fromIndex = TabItems.IndexOf(fromTab);
                if (fromIndex >= 0)
                {
                    var tabsToClose = TabItems.Skip(fromIndex + 1).Where(t => t.CanClose).ToList();
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
        private readonly CompositeDisposable _eventSubscriptions = new CompositeDisposable();
        private readonly List<IDisposable> _disposables = new List<IDisposable>();
        private readonly List<WeakReference> _eventHandlers = new List<WeakReference>();

        /// <summary>
        /// Disposes the control and its resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected disposal method with comprehensive cleanup
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        // Cancel any ongoing drag operations
                        CancelDragOperation();
                        
                        // Stop animations
                        StopCurrentAnimation();
                        
                        // Dispose managed resources
                        _insertionIndicator?.Dispose();
                        _insertionIndicator = null;
                        
                        // Close windows
                        CloseWindow(_detachPreviewWindow);
                        CloseWindow(_dragVisualWindow);
                        _detachPreviewWindow = null;
                        _dragVisualWindow = null;
                        
                        // Dispose insertion line
                        if (_insertionLine != null)
                        {
                            if (_insertionLine.Parent is Panel parent)
                                parent.Children.Remove(_insertionLine);
                            _insertionLine = null;
                        }
                        
                        // Clean up window highlights
                        RemoveAllWindowHighlights();
                        
                        // Dispose tab operations manager
                        if (_tabOperationsManager is IDisposable disposableManager)
                        {
                            disposableManager.Dispose();
                        }
                        _tabOperationsManager = null;
                        
                        // Dispose drag drop service
                        if (_dragDropService is IDisposable disposableService)
                        {
                            disposableService.Dispose();
                        }
                        _dragDropService = null;
                        
                        // Dispose current drag operation
                        if (_currentDragOperation is IDisposable disposableOperation)
                        {
                            disposableOperation.Dispose();
                        }
                        _currentDragOperation = null;
                        
                        // Dispose all tracked disposables
                        foreach (var disposable in _disposables)
                        {
                            try
                            {
                                disposable?.Dispose();
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogWarning(ex, "Error disposing resource in ChromeStyleTabControl");
                            }
                        }
                        _disposables.Clear();
                        
                        // Dispose all weak event subscriptions
                        _eventSubscriptions.Dispose();
                        
                        // Clear weak event handlers
                        _eventHandlers.Clear();
                        
                        // Unsubscribe from TabItems collection changes
                        if (TabItems != null)
                        {
                            TabItems.CollectionChanged -= OnTabItemsCollectionChanged;
                            
                            // Dispose adapters if they are TabModelAdapter instances
                            foreach (var tabItem in TabItems.OfType<TabModelAdapter>())
                            {
                                tabItem.Dispose();
                            }
                        }
                        
                        // Clear references
                        _draggedTab = null;
                        _dragStartPoint = null;
                        
                        _logger?.LogDebug("ChromeStyleTabControl disposed successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error during ChromeStyleTabControl disposal");
                    }
                }

                _disposed = true;
            }
        }

        /// <summary>
        /// Safely closes a window
        /// </summary>
        private void CloseWindow(Window window)
        {
            if (window != null)
            {
                try
                {
                    window.Close();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error closing window during disposal");
                }
            }
        }

        /// <summary>
        /// Adds a disposable resource to be tracked for cleanup
        /// </summary>
        protected void TrackDisposable(IDisposable disposable)
        {
            if (disposable != null && !_disposed)
            {
                _disposables.Add(disposable);
            }
        }

        /// <summary>
        /// Adds a weak reference to event handlers for cleanup tracking
        /// </summary>
        protected void TrackEventHandler(object handler)
        {
            if (handler != null && !_disposed)
            {
                _eventHandlers.Add(new WeakReference(handler));
            }
        }

        /// <summary>
        /// Checks if the control is disposed and throws if it is
        /// </summary>
        protected void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(ChromeStyleTabControl));
            }
        }

        #endregion

        #region Weak Event Helpers
        // NOTE: This weak event pattern matches MainWindow and WeakEventHelper patterns.
        // While it uses weak references, the lambda closures create strong reference chains.
        // This is a system-wide pattern that maintains consistency with the existing codebase.

        /// <summary>
        /// Subscribes to a routed event using weak references to prevent memory leaks
        /// </summary>
        private void SubscribeToRoutedEventWeak(UIElement element, RoutedEvent routedEvent, RoutedEventHandler handler)
        {
            if (element == null || routedEvent == null || handler == null) return;

            try
            {
                var weakRef = new WeakReference(handler.Target);
                var method = handler.Method;
                
                RoutedEventHandler weakHandler = (s, e) =>
                {
                    var target = weakRef.Target;
                    if (target != null)
                    {
                        try
                        {
                            method.Invoke(target, new object[] { s, e });
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error in weak routed event handler {MethodName}", method.Name);
                        }
                    }
                };
                
                element.AddHandler(routedEvent, weakHandler);
                var subscription = Disposable.Create(() => element.RemoveHandler(routedEvent, weakHandler));
                _eventSubscriptions.Add(subscription);
                
                _logger?.LogDebug("Subscribed to routed event '{EventName}' with weak reference", routedEvent.Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error subscribing to weak routed event '{EventName}'", routedEvent?.Name);
                throw;
            }
        }

        /// <summary>
        /// Subscribes to an EventHandler using weak references
        /// </summary>
        private void SubscribeToEventHandlerWeak<TEventArgs>(object source, string eventName, EventHandler<TEventArgs> handler) 
            where TEventArgs : EventArgs
        {
            if (source == null || string.IsNullOrEmpty(eventName) || handler == null) return;

            try
            {
                var weakRef = new WeakReference(handler.Target);
                var method = handler.Method;
                var eventInfo = source.GetType().GetEvent(eventName);
                
                if (eventInfo == null)
                {
                    _logger?.LogWarning("Event '{EventName}' not found on type {TypeName}", eventName, source.GetType().Name);
                    return;
                }

                EventHandler<TEventArgs> weakHandler = (s, e) =>
                {
                    var target = weakRef.Target;
                    if (target != null)
                    {
                        try
                        {
                            method.Invoke(target, new object[] { s, e });
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error in weak event handler {MethodName}", method.Name);
                        }
                    }
                };
                
                eventInfo.AddEventHandler(source, weakHandler);
                var subscription = Disposable.Create(() => eventInfo.RemoveEventHandler(source, weakHandler));
                _eventSubscriptions.Add(subscription);
                
                _logger?.LogDebug("Subscribed to event '{EventName}' with weak reference", eventName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error subscribing to weak event '{EventName}'", eventName);
                throw;
            }
        }

        /// <summary>
        /// Subscribes to an EventHandler using weak references
        /// </summary>
        private void SubscribeToEventHandlerWeak(object source, string eventName, EventHandler handler)
        {
            if (source == null || string.IsNullOrEmpty(eventName) || handler == null) return;

            try
            {
                var weakRef = new WeakReference(handler.Target);
                var method = handler.Method;
                var eventInfo = source.GetType().GetEvent(eventName);
                
                if (eventInfo == null)
                {
                    _logger?.LogWarning("Event '{EventName}' not found on type {TypeName}", eventName, source.GetType().Name);
                    return;
                }

                EventHandler weakHandler = (s, e) =>
                {
                    var target = weakRef.Target;
                    if (target != null)
                    {
                        try
                        {
                            method.Invoke(target, new object[] { s, e });
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error in weak event handler {MethodName}", method.Name);
                        }
                    }
                };
                
                eventInfo.AddEventHandler(source, weakHandler);
                var subscription = Disposable.Create(() => eventInfo.RemoveEventHandler(source, weakHandler));
                _eventSubscriptions.Add(subscription);
                
                _logger?.LogDebug("Subscribed to event '{EventName}' with weak reference", eventName);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error subscribing to weak event '{EventName}'", eventName);
                throw;
            }
        }

        /// <summary>
        /// Creates a weak subscription for lambda expressions and inline handlers
        /// </summary>
        private IDisposable CreateWeakSubscription(Action subscribe, Action unsubscribe)
        {
            try
            {
                subscribe();
                var subscription = Disposable.Create(() =>
                {
                    try
                    {
                        unsubscribe();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error during weak subscription cleanup");
                    }
                });
                
                _eventSubscriptions.Add(subscription);
                return subscription;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating weak subscription");
                throw;
            }
        }

        /// <summary>
        /// Subscribes to NotifyCollectionChanged event using weak references
        /// </summary>
        private void SubscribeToCollectionChangedWeak(
            System.Collections.Specialized.INotifyCollectionChanged source, 
            System.Collections.Specialized.NotifyCollectionChangedEventHandler handler)
        {
            if (source == null || handler == null) return;

            try
            {
                var weakRef = new WeakReference(handler.Target);
                var method = handler.Method;

                System.Collections.Specialized.NotifyCollectionChangedEventHandler weakHandler = (s, e) =>
                {
                    var target = weakRef.Target;
                    if (target != null)
                    {
                        try
                        {
                            method.Invoke(target, new object[] { s, e });
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error in weak collection changed handler {MethodName}", method.Name);
                        }
                    }
                };

                source.CollectionChanged += weakHandler;
                var subscription = Disposable.Create(() => source.CollectionChanged -= weakHandler);
                _eventSubscriptions.Add(subscription);

                _logger?.LogDebug("Subscribed to CollectionChanged event with weak reference");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error subscribing to weak CollectionChanged event");
                throw;
            }
        }

        /// <summary>
        /// Subscribes to PropertyChanged event using weak references
        /// </summary>
        private void SubscribeToPropertyChangedWeak(
            System.ComponentModel.INotifyPropertyChanged source,
            System.ComponentModel.PropertyChangedEventHandler handler)
        {
            if (source == null || handler == null) return;

            try
            {
                var weakRef = new WeakReference(handler.Target);
                var method = handler.Method;

                System.ComponentModel.PropertyChangedEventHandler weakHandler = (s, e) =>
                {
                    var target = weakRef.Target;
                    if (target != null)
                    {
                        try
                        {
                            method.Invoke(target, new object[] { s, e });
                        }
                        catch (Exception ex)
                        {
                            _logger?.LogWarning(ex, "Error in weak property changed handler {MethodName}", method.Name);
                        }
                    }
                };

                source.PropertyChanged += weakHandler;
                var subscription = Disposable.Create(() => source.PropertyChanged -= weakHandler);
                _eventSubscriptions.Add(subscription);

                _logger?.LogDebug("Subscribed to PropertyChanged event with weak reference");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error subscribing to weak PropertyChanged event");
                throw;
            }
        }

        /// <summary>
        /// Finalizer to ensure cleanup even if Dispose() wasn't called
        /// </summary>
        ~ChromeStyleTabControl()
        {
            Dispose(false);
        }

        #endregion

        #region Template Application

        /// <summary>
        /// Called when the control template is applied
        /// </summary>
        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            
            // Additional template-specific initialization can be done here if needed
        }

        #endregion
    }

    #region Event Args Classes

    /// <summary>
    /// Event arguments for new tab requested event
    /// </summary>
    public class NewTabRequestedEventArgs : EventArgs
    {
        public TabModel TabItem { get; set; }
        public bool Cancel { get; set; }

        public NewTabRequestedEventArgs(TabModel tabItem)
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
        public TabModel TabItem { get; }
        public bool Cancel { get; set; }

        public TabCloseRequestedEventArgs(TabModel tabItem)
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
        public TabModel TabItem { get; }
        public Point StartPosition { get; }
        public Point CurrentPosition { get; }

        public TabDragEventArgs(TabModel tabItem, Point startPosition, Point currentPosition)
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
        public TabModel TabItem { get; }
        public string PropertyName { get; }
        public object OldValue { get; }
        public object NewValue { get; }

        public TabMetadataChangedEventArgs(TabModel tabItem, string propertyName, object oldValue, object newValue)
        {
            TabItem = tabItem;
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }

    #endregion
} 
