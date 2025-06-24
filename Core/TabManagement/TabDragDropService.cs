using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using ExplorerPro.Models;
using ExplorerPro.UI.Controls;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Service handling tab drag and drop operations
    /// </summary>
    public class TabDragDropService : ITabDragDropService
    {
        private readonly ILogger<TabDragDropService> _logger;
        private readonly IDetachedWindowManager _windowManager;
        private readonly TabOperationsManager _operationsManager;

        private DragOperation _currentDrag;
        private Window _floatingWindow;
        private Canvas _dropIndicatorCanvas;
        private Rectangle _dropIndicator;

        // Thresholds
        private const double REORDER_THRESHOLD = 5.0;
        private const double DETACH_THRESHOLD = 40.0;
        private const double SNAP_THRESHOLD = 100.0;

        public TabDragDropService(
            ILogger<TabDragDropService> logger,
            IDetachedWindowManager windowManager,
            TabOperationsManager operationsManager)
        {
            _logger = logger;
            _windowManager = windowManager;
            _operationsManager = operationsManager;
        }

        /// <summary>
        /// Gets whether a drag operation is in progress
        /// </summary>
        public bool IsDragging => _currentDrag?.IsActive ?? false;

        /// <summary>
        /// Starts a drag operation
        /// </summary>
        public void StartDrag(TabItemModel tab, Point startPoint, Window sourceWindow)
        {
            try
            {
                if (_currentDrag?.IsActive == true)
                {
                    CancelDrag();
                }

                var sourceTabControl = FindTabControl(sourceWindow);
                if (sourceTabControl == null)
                {
                    _logger.LogWarning("No tab control found in source window");
                    return;
                }

                // VALIDATION: Don't drag the last tab
                if (sourceTabControl.Items.Count <= 1)
                {
                    _logger.LogWarning("Cannot drag the last remaining tab");
                    return;
                }

                // VALIDATION: Check if tab is draggable
                if (!tab.IsClosable)
                {
                    _logger.LogWarning($"Tab '{tab.Title}' is not draggable");
                    return;
                }

                // Find the actual TabItem reference
                var tabItem = sourceTabControl.Items
                    .OfType<TabItem>()
                    .FirstOrDefault(ti => ti.Tag == tab);

                if (tabItem == null)
                {
                    _logger.LogWarning($"Tab item not found for '{tab.Title}'");
                    return;
                }

                // VALIDATION: Ensure tab is not already being dragged
                if (tab.IsDragging)
                {
                    _logger.LogWarning($"Tab '{tab.Title}' is already being dragged");
                    return;
                }

                _currentDrag = new DragOperation
                {
                    Tab = tab,
                    SourceWindow = sourceWindow,
                    SourceTabControl = sourceTabControl,
                    StartPoint = startPoint,
                    Offset = Mouse.GetPosition(tabItem),
                    DraggedTabItem = tabItem, // Store actual TabItem reference
                    OriginalIndex = sourceTabControl.Items.IndexOf(tabItem),
                    IsActive = true
                };

                // Mark tab as dragging
                tab.IsDragging = true;
                tab.SourceWindow = sourceWindow;
                tab.OriginalIndex = _currentDrag.OriginalIndex;

                // Set up visual drag indicator
                SetupVisualDragIndicator(tabItem);

                _logger.LogInformation($"Started dragging tab '{tab.Title}' from index {_currentDrag.OriginalIndex}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start drag operation");
                CancelDrag();
            }
        }

        /// <summary>
        /// Sets up visual indicators for the dragged tab
        /// </summary>
        private void SetupVisualDragIndicator(TabItem tabItem)
        {
            if (tabItem == null) return;

            try
            {
                // Set dragging visual state
                tabItem.Opacity = 0.6;
                
                // Add transform for potential animations
                if (tabItem.RenderTransform == null)
                {
                    tabItem.RenderTransform = new TranslateTransform();
                }

                // Update cursor to indicate dragging
                Mouse.OverrideCursor = Cursors.Hand;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to set up visual drag indicator");
            }
        }

        /// <summary>
        /// Updates the drag operation
        /// </summary>
        public void UpdateDrag(Point currentPoint)
        {
            if (_currentDrag?.IsActive != true) return;

            try
            {
                _currentDrag.CurrentPoint = currentPoint;
                
                // Determine operation type
                var operationType = GetOperationType(currentPoint);
                
                if (operationType != _currentDrag.CurrentOperationType)
                {
                    _currentDrag.CurrentOperationType = operationType;
                    UpdateVisualFeedback(operationType, currentPoint);
                }

                // Update floating window if detaching
                if (operationType == DragOperationType.Detach && !_currentDrag.IsTornOff)
                {
                    CreateFloatingWindow();
                    _currentDrag.IsTornOff = true;
                }
                else if (operationType != DragOperationType.Detach && _currentDrag.IsTornOff)
                {
                    DestroyFloatingWindow();
                    _currentDrag.IsTornOff = false;
                }

                // Update positions
                if (_floatingWindow != null)
                {
                    _floatingWindow.Left = currentPoint.X - _currentDrag.Offset.X;
                    _floatingWindow.Top = currentPoint.Y - _currentDrag.Offset.Y;
                }

                // Show drop indicators
                UpdateDropIndicators(operationType, currentPoint);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating drag operation");
            }
        }

        /// <summary>
        /// Completes the drag operation
        /// </summary>
        public bool CompleteDrag(Window targetWindow, Point dropPoint)
        {
            if (_currentDrag?.IsActive != true)
                return false;

            bool success = false;
            DragOperationType operationType = DragOperationType.None;

            try
            {
                // Determine operation type safely
                operationType = _currentDrag.CurrentOperationType;
                if (operationType == DragOperationType.None)
                {
                    operationType = GetOperationType(dropPoint);
                }

                _logger.LogInformation($"Attempting to complete {operationType} operation for tab '{_currentDrag.Tab.Title}'");

                // Validate operation before proceeding
                if (!ValidateDropOperation(operationType, targetWindow, dropPoint))
                {
                    _logger.LogWarning($"Invalid {operationType} operation - cancelling");
                    return false;
                }

                // Execute the operation with proper error handling
                switch (operationType)
                {
                    case DragOperationType.Reorder:
                        success = ExecuteReorderOperation(dropPoint);
                        break;

                    case DragOperationType.Detach:
                        success = ExecuteDetachOperation(dropPoint);
                        break;

                    case DragOperationType.Transfer:
                        success = ExecuteTransferOperation(targetWindow, dropPoint);
                        break;

                    default:
                        _logger.LogWarning($"Unsupported operation type: {operationType}");
                        success = false;
                        break;
                }

                if (success)
                {
                    _logger.LogInformation($"Successfully completed {operationType} operation for tab '{_currentDrag.Tab.Title}'");
                }
                else
                {
                    _logger.LogWarning($"Failed to complete {operationType} operation for tab '{_currentDrag.Tab.Title}'");
                }

                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to complete drag operation ({operationType}) for tab '{_currentDrag?.Tab?.Title ?? "unknown"}'");
                return false;
            }
            finally
            {
                // Ensure cleanup always happens in finally block
                try
                {
                    CleanupDrag();
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Error during drag cleanup");
                }
            }
        }

        /// <summary>
        /// Validates if the drop operation is allowed
        /// </summary>
        private bool ValidateDropOperation(DragOperationType operationType, Window targetWindow, Point dropPoint)
        {
            try
            {
                switch (operationType)
                {
                    case DragOperationType.Reorder:
                        return _currentDrag.SourceTabControl != null;

                    case DragOperationType.Detach:
                        return _currentDrag.SourceTabControl?.Items.Count > 1;

                    case DragOperationType.Transfer:
                        return targetWindow != null && 
                               targetWindow != _currentDrag.SourceWindow &&
                               FindTabControl(targetWindow) != null;

                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating drop operation");
                return false;
            }
        }

        /// <summary>
        /// Executes reorder operation with error handling
        /// </summary>
        private bool ExecuteReorderOperation(Point dropPoint)
        {
            try
            {
                if (_currentDrag.SourceTabControl == null) return false;

                var dropIndex = _operationsManager.CalculateDropIndex(_currentDrag.SourceTabControl, dropPoint);
                return _operationsManager.ReorderTab(_currentDrag.SourceTabControl, _currentDrag.Tab, dropIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing reorder operation");
                return false;
            }
        }

        /// <summary>
        /// Executes detach operation with error handling
        /// </summary>
        private bool ExecuteDetachOperation(Point dropPoint)
        {
            try
            {
                var newWindow = _windowManager.DetachTab(_currentDrag.Tab, _currentDrag.SourceWindow);
                if (newWindow != null)
                {
                    // Position at drop point with safe bounds checking
                    newWindow.Left = Math.Max(0, dropPoint.X - 100);
                    newWindow.Top = Math.Max(0, dropPoint.Y - 20);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing detach operation");
                return false;
            }
        }

        /// <summary>
        /// Executes transfer operation with error handling
        /// </summary>
        private bool ExecuteTransferOperation(Window targetWindow, Point dropPoint)
        {
            try
            {
                if (targetWindow == null) return false;

                var targetTabControl = FindTabControl(targetWindow);
                if (targetTabControl == null) return false;

                var dropIndex = _operationsManager.CalculateDropIndex(targetTabControl, dropPoint);
                return _operationsManager.TransferTab(
                    _currentDrag.SourceTabControl,
                    targetTabControl,
                    _currentDrag.Tab,
                    dropIndex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing transfer operation");
                return false;
            }
        }

        /// <summary>
        /// Cancels the current drag operation
        /// </summary>
        public void CancelDrag()
        {
            if (_currentDrag?.IsActive != true)
                return;

            try
            {
                _logger.LogInformation("Drag operation cancelled");
                
                // Return tab to original position if needed
                if (_currentDrag.DraggedTabItem != null && _currentDrag.SourceTabControl != null)
                {
                    var currentIndex = _currentDrag.SourceTabControl.Items.IndexOf(_currentDrag.DraggedTabItem);
                    if (currentIndex != _currentDrag.OriginalIndex && currentIndex >= 0)
                    {
                        _operationsManager.ReorderTab(
                            _currentDrag.SourceTabControl,
                            _currentDrag.Tab,
                            _currentDrag.OriginalIndex);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling drag operation");
            }
            finally
            {
                CleanupDrag();
            }
        }

        /// <summary>
        /// Determines if a drop is valid
        /// </summary>
        public bool CanDrop(Window targetWindow, Point dropPoint)
        {
            if (_currentDrag?.IsActive != true)
                return false;

            var operationType = GetOperationType(dropPoint);
            
            switch (operationType)
            {
                case DragOperationType.Reorder:
                case DragOperationType.Detach:
                    return true;
                    
                case DragOperationType.Transfer:
                    return targetWindow != null && targetWindow != _currentDrag.SourceWindow;
                    
                default:
                    return false;
            }
        }

        /// <summary>
        /// Gets the operation type for current position
        /// </summary>
        public DragOperationType GetOperationType(Point currentPoint)
        {
            if (_currentDrag == null) return DragOperationType.None;

            try
            {
                // Calculate distance from original tab strip properly
                var sourceTabControl = _currentDrag.SourceTabControl;
                if (sourceTabControl == null) return DragOperationType.None;

                Point localPoint;
                try
                {
                    localPoint = sourceTabControl.PointFromScreen(currentPoint);
                }
                catch
                {
                    // If coordinate conversion fails, assume we're outside
                    localPoint = new Point(-1000, -1000);
                }

                // Define tab strip bounds (just the header area, not the entire control)
                const double TAB_STRIP_HEIGHT = 35.0;
                var tabStripBounds = new Rect(0, 0, sourceTabControl.ActualWidth, TAB_STRIP_HEIGHT);
                
                // Return Reorder if within same tab strip bounds
                if (tabStripBounds.Contains(localPoint))
                {
                    return DragOperationType.Reorder;
                }

                // Use Window.GetWindow to find current window under cursor
                var targetWindow = FindWindowUnderCursor(currentPoint);
                
                // Return Transfer if over another window's tab strip
                if (targetWindow != null && targetWindow != _currentDrag.SourceWindow)
                {
                    var targetTabControl = FindTabControl(targetWindow);
                    if (targetTabControl != null)
                    {
                        Point targetLocalPoint;
                        try
                        {
                            targetLocalPoint = targetTabControl.PointFromScreen(currentPoint);
                        }
                        catch
                        {
                            targetLocalPoint = new Point(-1000, -1000);
                        }

                        var targetTabStripBounds = new Rect(0, 0, targetTabControl.ActualWidth, TAB_STRIP_HEIGHT);
                        
                        if (targetTabStripBounds.Contains(targetLocalPoint))
                        {
                            return DragOperationType.Transfer;
                        }
                    }
                }

                // Return Detach if outside bounds but no target window
                var verticalDistance = Math.Abs(localPoint.Y - (TAB_STRIP_HEIGHT / 2));
                if (verticalDistance > DETACH_THRESHOLD)
                {
                    return DragOperationType.Detach;
                }

                return DragOperationType.None;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error determining operation type");
                return DragOperationType.None;
            }
        }

        /// <summary>
        /// Finds the window under the cursor using proper window enumeration
        /// </summary>
        private Window FindWindowUnderCursor(Point screenPoint)
        {
            try
            {
                // Try each application window to see if the point is within it
                foreach (Window window in Application.Current.Windows)
                {
                    if (window.WindowState == System.Windows.WindowState.Minimized) continue;

                    var windowBounds = new Rect(window.Left, window.Top, window.Width, window.Height);
                    if (windowBounds.Contains(screenPoint))
                    {
                        return window;
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding window under cursor");
                return null;
            }
        }

        #region Private Methods

        private bool CompleteReorder(Point dropPoint)
        {
            if (_currentDrag.SourceTabControl == null) return false;

            var dropIndex = _operationsManager.CalculateDropIndex(_currentDrag.SourceTabControl, dropPoint);
            return _operationsManager.ReorderTab(_currentDrag.SourceTabControl, _currentDrag.Tab, dropIndex);
        }

        private bool CompleteDetach(Point dropPoint)
        {
            var newWindow = _windowManager.DetachTab(_currentDrag.Tab, _currentDrag.SourceWindow);
            if (newWindow != null)
            {
                // Position at drop point
                newWindow.Left = dropPoint.X - 100;
                newWindow.Top = dropPoint.Y - 20;
                return true;
            }
            return false;
        }

        private bool CompleteTransfer(Window targetWindow, Point dropPoint)
        {
            if (targetWindow == null) return false;

            var targetTabControl = FindTabControl(targetWindow);
            if (targetTabControl == null) return false;

            var dropIndex = _operationsManager.CalculateDropIndex(targetTabControl, dropPoint);
            return _operationsManager.TransferTab(
                _currentDrag.SourceTabControl,
                targetTabControl,
                _currentDrag.Tab,
                dropIndex);
        }

        private void CreateFloatingWindow()
        {
            if (_floatingWindow != null) return;

            _floatingWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ShowInTaskbar = false,
                Topmost = true,
                IsHitTestVisible = false,
                Width = 250,
                Height = 150,
                Opacity = 0.0
            };

            // Create preview content
            var grid = new Grid();
            
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(240, 245, 245, 245)),
                BorderBrush = new SolidColorBrush(Color.FromArgb(255, 0, 120, 212)),
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(10)
            };

            var shadow = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 20,
                ShadowDepth = 0,
                Opacity = 0.3
            };
            border.Effect = shadow;

            var content = new TextBlock
            {
                Text = _currentDrag.Tab.Title,
                FontSize = 14,
                FontWeight = FontWeights.Medium,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = new SolidColorBrush(Color.FromRgb(32, 32, 32))
            };

            border.Child = content;
            grid.Children.Add(border);
            _floatingWindow.Content = grid;
            
            _floatingWindow.Show();

            // Animate in
            var fadeIn = new DoubleAnimation(0, 0.9, TimeSpan.FromMilliseconds(200));
            _floatingWindow.BeginAnimation(Window.OpacityProperty, fadeIn);
        }

        private void DestroyFloatingWindow()
        {
            if (_floatingWindow == null) return;

            var fadeOut = new DoubleAnimation(0.9, 0, TimeSpan.FromMilliseconds(150));
            fadeOut.Completed += (s, e) =>
            {
                _floatingWindow?.Close();
                _floatingWindow = null;
            };
            _floatingWindow.BeginAnimation(Window.OpacityProperty, fadeOut);
        }

        private void UpdateVisualFeedback(DragOperationType operationType, Point currentPoint)
        {
            // Update cursor based on operation
            switch (operationType)
            {
                case DragOperationType.Reorder:
                    Mouse.OverrideCursor = Cursors.Hand;
                    break;
                    
                case DragOperationType.Detach:
                    Mouse.OverrideCursor = Cursors.SizeAll;
                    break;
                    
                case DragOperationType.Transfer:
                    Mouse.OverrideCursor = Cursors.Hand;
                    break;
                    
                default:
                    Mouse.OverrideCursor = Cursors.No;
                    break;
            }
        }

        private void UpdateDropIndicators(DragOperationType operationType, Point currentPoint)
        {
            // Clean existing indicators
            HideDropIndicator();

            if (operationType == DragOperationType.Reorder || operationType == DragOperationType.Transfer)
            {
                var targetControl = operationType == DragOperationType.Reorder 
                    ? _currentDrag.SourceTabControl 
                    : FindTabControlUnderPoint(currentPoint);

                if (targetControl != null)
                {
                    var dropIndex = _operationsManager.CalculateDropIndex(targetControl, currentPoint);
                    ShowDropIndicator(targetControl, dropIndex);
                }
            }
        }

        private void ShowDropIndicator(ChromeStyleTabControl tabControl, int dropIndex)
        {
            if (_dropIndicator == null)
            {
                _dropIndicator = new Rectangle
                {
                    Width = 3,
                    Height = 30,
                    Fill = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                    RadiusX = 1.5,
                    RadiusY = 1.5
                };
            }

            // Calculate position
            double xPosition = 0;
            if (dropIndex < tabControl.Items.Count)
            {
                var tabItem = tabControl.Items[dropIndex] as TabItem;
                if (tabItem != null)
                {
                    var transform = tabItem.TransformToAncestor(tabControl);
                    var position = transform.Transform(new Point(0, 0));
                    xPosition = position.X - 1.5;
                }
            }
            else if (tabControl.Items.Count > 0)
            {
                var lastTab = tabControl.Items[tabControl.Items.Count - 1] as TabItem;
                if (lastTab != null)
                {
                    var transform = lastTab.TransformToAncestor(tabControl);
                    var position = transform.Transform(new Point(lastTab.ActualWidth, 0));
                    xPosition = position.X - 1.5;
                }
            }

            // Position indicator
            Canvas.SetLeft(_dropIndicator, xPosition);
            Canvas.SetTop(_dropIndicator, 5);

            // Add to tab control's adorner layer
            var adornerLayer = System.Windows.Documents.AdornerLayer.GetAdornerLayer(tabControl);
            if (adornerLayer != null)
            {
                // This is simplified - you'd need a proper adorner implementation
                _dropIndicator.Visibility = Visibility.Visible;
            }
        }

        private void HideDropIndicator()
        {
            if (_dropIndicator != null)
            {
                _dropIndicator.Visibility = Visibility.Collapsed;
            }
        }

        private void CleanupDrag()
        {
            if (_currentDrag != null)
            {
                _currentDrag.Tab.IsDragging = false;
                _currentDrag.IsActive = false;
            }

            DestroyFloatingWindow();
            HideDropIndicator();
            Mouse.OverrideCursor = null;

            _currentDrag = null;
        }

        private ChromeStyleTabControl FindTabControl(Window window)
        {
            if (window is UI.MainWindow.MainWindow mainWindow)
            {
                return mainWindow.MainTabs as ChromeStyleTabControl;
            }
            return null;
        }

        private ChromeStyleTabControl FindTabControlUnderPoint(Point screenPoint)
        {
            var window = WindowLocator.FindWindowUnderPoint(screenPoint);
            return window != null ? FindTabControl(window) : null;
        }

        #endregion
    }
} 