using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls.Interfaces;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Implementation of tab drag and drop management.
    /// Handles all drag-drop operations with proper visual feedback and event coordination.
    /// </summary>
    public class TabDragDropManager : ITabDragDropManager
    {
        #region Private Fields
        
        private readonly ILogger<TabDragDropManager> _logger;
        private TabControl _tabControl;
        private bool _isDragging;
        private TabModel _draggedTab;
        private Point _dragStartPoint;
        private TabItem _draggedTabItem;
        private bool _disposed;
        
        // Drag operation state
        private DragOperationType _currentOperationType;
        private Window _dragVisualWindow;
        private TabDropInsertionIndicator _insertionIndicator;
        
        #endregion

        #region Constructor
        
        public TabDragDropManager(ILogger<TabDragDropManager> logger = null)
        {
            _logger = logger;
            DragThreshold = 5.0;
            DetachThreshold = 40.0;
            _logger?.LogDebug("TabDragDropManager initialized");
        }
        
        #endregion

        #region ITabDragDropManager Implementation
        
        public event EventHandler<TabDragEventArgs> DragStarted;
        public event EventHandler<TabDragEventArgs> Dragging;
        public event EventHandler<TabDragEventArgs> DragCompleted;
        public event EventHandler<TabReorderRequestedEventArgs> ReorderRequested;
        public event EventHandler<TabDetachRequestedEventArgs> DetachRequested;

        public bool IsDragging => _isDragging;
        public TabModel DraggedTab => _draggedTab;
        public double DragThreshold { get; set; }
        public double DetachThreshold { get; set; }

        public void Initialize(TabControl tabControl)
        {
            ThrowIfDisposed();
            
            _tabControl = tabControl ?? throw new ArgumentNullException(nameof(tabControl));
            
            // Wire up mouse events
            _tabControl.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            _tabControl.MouseMove += OnMouseMove;
            _tabControl.MouseLeftButtonUp += OnMouseLeftButtonUp;
            _tabControl.LostMouseCapture += OnLostMouseCapture;
            
            _logger?.LogDebug("TabDragDropManager initialized with TabControl");
        }

        public bool StartDrag(TabModel tab, Point startPoint)
        {
            ThrowIfDisposed();
            
            if (_isDragging || tab == null)
                return false;
                
            _draggedTab = tab;
            _dragStartPoint = startPoint;
            _draggedTabItem = FindTabItemFromModel(tab);
            
            if (_draggedTabItem == null)
            {
                _logger?.LogWarning("Could not find TabItem for model {TabId}", tab.Id);
                return false;
            }
            
            _isDragging = true;
            _tabControl.CaptureMouse();
            
            CreateDragVisual();
            ShowDragFeedback(DragOperationType.None);
            
            var args = new TabDragEventArgs(new TabModel { Content = tab }, startPoint, startPoint);
            DragStarted?.Invoke(this, args);
            
            _logger?.LogDebug("Started drag operation for tab '{Title}'", tab.Title);
            return true;
        }

        public void UpdateDrag(Point currentPoint)
        {
            ThrowIfDisposed();
            
            if (!_isDragging || _draggedTab == null)
                return;
                
            var operationType = GetDragOperationType(currentPoint);
            
            if (operationType != _currentOperationType)
            {
                _currentOperationType = operationType;
                ShowDragFeedback(operationType);
            }
            
            UpdateDragVisualPosition(currentPoint);
            UpdateInsertionIndicator(currentPoint, CalculateInsertionIndex(currentPoint));
            
            var args = new TabDragEventArgs(new TabModel { Content = _draggedTab }, _dragStartPoint, currentPoint);
            Dragging?.Invoke(this, args);
        }

        public bool CompleteDrag(Point endPoint)
        {
            ThrowIfDisposed();
            
            if (!_isDragging || _draggedTab == null)
                return false;
                
            var success = false;
            var operationType = GetDragOperationType(endPoint);
            
            try
            {
                switch (operationType)
                {
                    case DragOperationType.Reorder:
                        success = HandleReorderDrop(endPoint);
                        break;
                    case DragOperationType.Detach:
                        success = HandleDetachDrop(endPoint);
                        break;
                    case DragOperationType.Transfer:
                        success = HandleTransferDrop(endPoint);
                        break;
                }
                
                var args = new TabDragEventArgs(new TabModel { Content = _draggedTab }, _dragStartPoint, endPoint);
                DragCompleted?.Invoke(this, args);
                
                _logger?.LogDebug("Completed drag operation for tab '{Title}' with result: {Success}", 
                    _draggedTab.Title, success);
            }
            finally
            {
                ResetDragState();
            }
            
            return success;
        }

        public void CancelDrag()
        {
            ThrowIfDisposed();
            
            if (!_isDragging)
                return;
                
            _logger?.LogDebug("Cancelling drag operation for tab '{Title}'", _draggedTab?.Title);
            ResetDragState();
        }

        public bool CanStartDrag(Point point)
        {
            ThrowIfDisposed();
            
            if (_isDragging)
                return false;
                
            var tabItem = FindTabItemFromPoint(point);
            return tabItem != null && !IsCloseButton(point) && !IsAddNewTabButton(point);
        }

        public DragOperationType GetDragOperationType(Point point)
        {
            ThrowIfDisposed();
            
            if (!_isDragging)
                return DragOperationType.None;
                
            var distanceFromStart = (point - _dragStartPoint).Length;
            
            // Check for detach operation
            if (distanceFromStart > DetachThreshold)
            {
                var tabControlBounds = new Rect(_tabControl.PointToScreen(new Point(0, 0)), 
                    _tabControl.RenderSize);
                var screenPoint = _tabControl.PointToScreen(point);
                
                if (!tabControlBounds.Contains(screenPoint))
                    return DragOperationType.Detach;
            }
            
            // Check for transfer to another window
            var targetWindow = FindWindowUnderPoint(_tabControl.PointToScreen(point));
            if (targetWindow != null && targetWindow != Window.GetWindow(_tabControl))
                return DragOperationType.Transfer;
                
            // Default to reorder
            return DragOperationType.Reorder;
        }

        public void ShowDragFeedback(DragOperationType operationType)
        {
            ThrowIfDisposed();
            
            switch (operationType)
            {
                case DragOperationType.Reorder:
                    ShowReorderFeedback();
                    break;
                case DragOperationType.Detach:
                    ShowDetachFeedback();
                    break;
                case DragOperationType.Transfer:
                    ShowTransferFeedback();
                    break;
                default:
                    HideDragFeedback();
                    break;
            }
        }

        public void HideDragFeedback()
        {
            ThrowIfDisposed();
            
            _insertionIndicator?.HideIndicator();
            RemoveDragVisual();
        }

        public void UpdateInsertionIndicator(Point position, int insertIndex)
        {
            ThrowIfDisposed();
            
            if (_currentOperationType == DragOperationType.Reorder && insertIndex >= 0)
            {
                if (_insertionIndicator == null)
                    CreateInsertionIndicator();
                    
                _insertionIndicator?.ShowIndicator();
                _insertionIndicator?.UpdatePosition(position.X, 30);
            }
            else
            {
                _insertionIndicator?.HideIndicator();
            }
        }
        
        #endregion

        #region Event Handlers
        
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var point = e.GetPosition(_tabControl);
            
            if (CanStartDrag(point))
            {
                var tabItem = FindTabItemFromPoint(point);
                var tabModel = GetTabModelFromItem(tabItem);
                
                if (tabModel != null)
                {
                    _dragStartPoint = point;
                    // Don't start drag immediately - wait for mouse move
                }
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && !_isDragging)
            {
                var currentPoint = e.GetPosition(_tabControl);
                var distance = (currentPoint - _dragStartPoint).Length;
                
                if (distance > DragThreshold)
                {
                    var tabItem = FindTabItemFromPoint(_dragStartPoint);
                    var tabModel = GetTabModelFromItem(tabItem);
                    
                    if (tabModel != null)
                        StartDrag(tabModel, _dragStartPoint);
                }
            }
            else if (_isDragging)
            {
                UpdateDrag(e.GetPosition(_tabControl));
            }
        }

        private void OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDragging)
            {
                CompleteDrag(e.GetPosition(_tabControl));
            }
        }

        private void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            if (_isDragging)
            {
                CancelDrag();
            }
        }
        
        #endregion

        #region Private Helper Methods
        
        private TabItem FindTabItemFromPoint(Point point)
        {
            var element = _tabControl.InputHitTest(point) as DependencyObject;
            while (element != null)
            {
                if (element is TabItem tabItem)
                    return tabItem;
                element = VisualTreeHelper.GetParent(element);
            }
            return null;
        }

        private TabItem FindTabItemFromModel(TabModel model)
        {
            foreach (TabItem item in _tabControl.Items)
            {
                if (GetTabModelFromItem(item) == model)
                    return item;
            }
            return null;
        }

        private TabModel GetTabModelFromItem(TabItem tabItem)
        {
            return tabItem?.DataContext as TabModel ?? tabItem?.Tag as TabModel;
        }

        private bool IsCloseButton(Point point)
        {
            var element = _tabControl.InputHitTest(point) as DependencyObject;
            while (element != null)
            {
                if (element is Button button && button.Name == "CloseButton")
                    return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        private bool IsAddNewTabButton(Point point)
        {
            var element = _tabControl.InputHitTest(point) as DependencyObject;
            while (element != null)
            {
                if (element is Button button && button.Name == "AddTabButton")
                    return true;
                element = VisualTreeHelper.GetParent(element);
            }
            return false;
        }

        private int CalculateInsertionIndex(Point point)
        {
            // Calculate where to insert based on mouse position
            var tabPanel = FindTabPanel();
            if (tabPanel == null)
                return -1;
                
            for (int i = 0; i < tabPanel.Children.Count; i++)
            {
                var child = tabPanel.Children[i] as FrameworkElement;
                if (child != null)
                {
                    var childBounds = new Rect(child.TranslatePoint(new Point(0, 0), _tabControl),
                        child.RenderSize);
                    
                    if (point.X < childBounds.Left + childBounds.Width / 2)
                        return i;
                }
            }
            
            return tabPanel.Children.Count;
        }

        private Panel FindTabPanel()
        {
            // Find the panel that contains the tab items
            return FindChildOfType<Panel>(_tabControl);
        }

        private T FindChildOfType<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                    
                var childResult = FindChildOfType<T>(child);
                if (childResult != null)
                    return childResult;
            }
            return null;
        }

        private Window FindWindowUnderPoint(Point screenPoint)
        {
            // Implementation to find window under cursor
            // This would require Win32 APIs for proper implementation
            return null;
        }

        private void CreateDragVisual()
        {
            if (_draggedTabItem == null)
                return;
                
            // Create a visual representation for dragging
            // This would create a semi-transparent window showing the tab
        }

        private void UpdateDragVisualPosition(Point point)
        {
            if (_dragVisualWindow != null)
            {
                var screenPoint = _tabControl.PointToScreen(point);
                _dragVisualWindow.Left = screenPoint.X - 50;
                _dragVisualWindow.Top = screenPoint.Y - 15;
            }
        }

        private void RemoveDragVisual()
        {
            if (_dragVisualWindow != null)
            {
                _dragVisualWindow.Close();
                _dragVisualWindow = null;
            }
        }

        private void CreateInsertionIndicator()
        {
            _insertionIndicator = new TabDropInsertionIndicator(_tabControl, null);
            // Add to visual tree as needed
        }

        private void ShowReorderFeedback()
        {
            Mouse.SetCursor(Cursors.SizeAll);
        }

        private void ShowDetachFeedback()
        {
            Mouse.SetCursor(Cursors.Hand);
        }

        private void ShowTransferFeedback()
        {
            Mouse.SetCursor(Cursors.Cross);
        }

        private bool HandleReorderDrop(Point endPoint)
        {
            var insertionIndex = CalculateInsertionIndex(endPoint);
            if (insertionIndex >= 0)
            {
                var currentIndex = GetTabIndex(_draggedTab);
                if (currentIndex != insertionIndex)
                {
                    var args = new TabReorderRequestedEventArgs(_draggedTab, currentIndex, insertionIndex);
                    ReorderRequested?.Invoke(this, args);
                    return !args.Cancel;
                }
            }
            return false;
        }

        private bool HandleDetachDrop(Point endPoint)
        {
            var screenPoint = _tabControl.PointToScreen(endPoint);
            var args = new TabDetachRequestedEventArgs(_draggedTab, screenPoint);
            DetachRequested?.Invoke(this, args);
            return !args.Cancel;
        }

        private bool HandleTransferDrop(Point endPoint)
        {
            // Handle transfer to another window
            // Implementation would depend on specific requirements
            return false;
        }

        private int GetTabIndex(TabModel tab)
        {
            for (int i = 0; i < _tabControl.Items.Count; i++)
            {
                var item = _tabControl.Items[i] as TabItem;
                if (GetTabModelFromItem(item) == tab)
                    return i;
            }
            return -1;
        }

        private void ResetDragState()
        {
            _isDragging = false;
            _draggedTab = null;
            _draggedTabItem = null;
            _currentOperationType = DragOperationType.None;
            
            _tabControl?.ReleaseMouseCapture();
            HideDragFeedback();
            Mouse.SetCursor(Cursors.Arrow);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(TabDragDropManager));
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
                if (_tabControl != null)
                {
                    _tabControl.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                    _tabControl.MouseMove -= OnMouseMove;
                    _tabControl.MouseLeftButtonUp -= OnMouseLeftButtonUp;
                    _tabControl.LostMouseCapture -= OnLostMouseCapture;
                }
                
                ResetDragState();
                _insertionIndicator?.Dispose();
                
                _disposed = true;
                _logger?.LogDebug("TabDragDropManager disposed");
            }
        }
        
        #endregion
    }
} 
