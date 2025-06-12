using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Documents;
using System.Windows.Threading;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Services;
using ExplorerPro.UI.FileTree.Utilities;

namespace ExplorerPro.UI.FileTree.Managers
{
    /// <summary>
    /// Handles all UI events and interactions for the FileTreeListView.
    /// Responsible for mouse events, keyboard events, and UI state management.
    /// </summary>
    public class FileTreeUIEventManager : IDisposable
    {
        #region Private Fields

        private readonly TreeView _treeView;
        private readonly IFileTree _fileTree;
        private readonly SelectionService _selectionService;
        private readonly FileTreePerformanceManager _performanceManager;
        
        // Selection rectangle fields
        private bool _isSelectionRectangleMode = false;
        private Point _selectionStartPoint;
        private SelectionRectangleAdorner _selectionAdorner;
        private AdornerLayer _adornerLayer;
        
        // State tracking
        private bool _isHandlingDoubleClick = false;
        private bool _isProcessingSelection = false;
        private bool _disposed = false;

        #endregion

        #region Events

        public event EventHandler<string>? ItemDoubleClicked;
        public event EventHandler<FileTreeItem>? ItemClicked;
        public event EventHandler<Point>? EmptySpaceClicked;
        public event EventHandler? SelectionRectangleCompleted;

        #endregion

        #region Constructor

        public FileTreeUIEventManager(
            TreeView treeView, 
            IFileTree fileTree,
            SelectionService selectionService,
            FileTreePerformanceManager performanceManager)
        {
            _treeView = treeView ?? throw new ArgumentNullException(nameof(treeView));
            _fileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            _performanceManager = performanceManager ?? throw new ArgumentNullException(nameof(performanceManager));

            AttachEventHandlers();
        }

        #endregion

        #region Event Handler Management

        private void AttachEventHandlers()
        {
            _treeView.SelectedItemChanged += OnTreeViewSelectedItemChanged;
            _treeView.MouseDoubleClick += OnTreeViewMouseDoubleClick;
            _treeView.PreviewMouseLeftButtonDown += OnTreeViewPreviewMouseLeftButtonDown;
            _treeView.PreviewMouseLeftButtonUp += OnTreeViewPreviewMouseLeftButtonUp;
            _treeView.PreviewMouseMove += OnTreeViewPreviewMouseMove;
            _treeView.PreviewKeyDown += OnTreeViewPreviewKeyDown;
            _treeView.PreviewMouseRightButtonDown += OnTreeViewPreviewMouseRightButtonDown;
            
            if (_treeView.ItemContainerGenerator != null)
            {
                _treeView.ItemContainerGenerator.StatusChanged += OnItemContainerGeneratorStatusChanged;
            }
        }

        private void DetachEventHandlers()
        {
            _treeView.SelectedItemChanged -= OnTreeViewSelectedItemChanged;
            _treeView.MouseDoubleClick -= OnTreeViewMouseDoubleClick;
            _treeView.PreviewMouseLeftButtonDown -= OnTreeViewPreviewMouseLeftButtonDown;
            _treeView.PreviewMouseLeftButtonUp -= OnTreeViewPreviewMouseLeftButtonUp;
            _treeView.PreviewMouseMove -= OnTreeViewPreviewMouseMove;
            _treeView.PreviewKeyDown -= OnTreeViewPreviewKeyDown;
            _treeView.PreviewMouseRightButtonDown -= OnTreeViewPreviewMouseRightButtonDown;
            
            if (_treeView.ItemContainerGenerator != null)
            {
                _treeView.ItemContainerGenerator.StatusChanged -= OnItemContainerGeneratorStatusChanged;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the item bounds for selection rectangle calculations
        /// </summary>
        public Rect GetItemBounds(TreeViewItem item)
        {
            if (item == null) return Rect.Empty;
            
            var topLeft = item.TranslatePoint(new Point(0, 0), _treeView);
            return new Rect(topLeft, new Size(item.ActualWidth, item.ActualHeight));
        }

        /// <summary>
        /// Checks if a point is within the selection rectangle
        /// </summary>
        public bool IsPointInSelectionRectangle(Point point)
        {
            return _selectionAdorner?.GetSelectionBounds().Contains(point) ?? false;
        }

        /// <summary>
        /// Cancels any active selection rectangle operation
        /// </summary>
        public void CancelSelectionRectangle()
        {
            if (_isSelectionRectangleMode)
            {
                _isSelectionRectangleMode = false;
                CleanupSelectionAdorner();
            }
        }

        #endregion

        #region Event Handlers

        private void OnTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_isProcessingSelection || _disposed) return;

            try
            {
                _isProcessingSelection = true;
                
                if (e.NewValue is FileTreeItem newItem)
                {
                    // Sync selection service with TreeView selection
                    if (!_selectionService.IsMultiSelectMode)
                    {
                        // In single-select mode, update selection service to match TreeView
                        _selectionService.SelectSingle(newItem);
                    }
                    else if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    {
                        // In multi-select with Ctrl, toggle the item
                        _selectionService.ToggleSelection(newItem);
                    }
                    else if (!_selectionService.IsItemSelected(newItem))
                    {
                        // In multi-select without modifier, select only this item
                        _selectionService.SelectSingle(newItem);
                    }
                    
                    // Notify that an item was clicked/selected
                    ItemClicked?.Invoke(this, newItem);
                }
            }
            finally
            {
                _isProcessingSelection = false;
            }
        }

        private void OnTreeViewMouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (_disposed) return;
            
            // Get the item that was actually clicked on
            var originalSource = e.OriginalSource as DependencyObject;
            var treeViewItem = VisualTreeHelperEx.FindAncestor<TreeViewItem>(originalSource);
            
            if (treeViewItem?.DataContext is FileTreeItem item)
            {
                // Only handle double-click for files, let TreeView handle folders naturally
                if (!item.IsDirectory)
                {
                    // Set flag to prevent selection changed from creating new tabs
                    _isHandlingDoubleClick = true;
                    
                    // Notify double-click
                    ItemDoubleClicked?.Invoke(this, item.Path);
                    
                    // Mark as handled to prevent bubbling
                    e.Handled = true;
                    
                    // Reset flag after a delay
                    Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        _isHandlingDoubleClick = false;
                    }));
                }
                // For directories, don't handle the event - let TreeView do its default behavior
            }
        }

        private void OnTreeViewPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_disposed) return;
            
            _selectionStartPoint = e.GetPosition(_treeView);
            
            // Get the clicked item
            var item = _performanceManager.GetItemFromPoint(_selectionStartPoint);
            
            // Check if we clicked on empty space (start selection rectangle)
            if (item == null)
            {
                // Clear selection if not holding Ctrl
                if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                {
                    _selectionService.ClearSelection();
                }
                
                // Notify empty space click
                EmptySpaceClicked?.Invoke(this, _selectionStartPoint);
                
                // Prepare for selection rectangle
                _isSelectionRectangleMode = true;
                e.Handled = true;
            }
            else
            {
                // Check if we clicked on a checkbox
                var originalSource = e.OriginalSource as DependencyObject;
                var checkbox = VisualTreeHelperEx.FindAncestor<CheckBox>(originalSource);
                
                if (checkbox == null)
                {
                    // Handle normal item selection
                    _selectionService.HandleSelection(item, Keyboard.Modifiers, _fileTree.RootItems);
                    
                    // Don't mark as handled - let drag/drop service also see this event
                }
                // If checkbox clicked, let the checkbox handler deal with it
            }
        }

        private void OnTreeViewPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_disposed) return;
            
            if (_isSelectionRectangleMode && _selectionAdorner != null)
            {
                // Complete selection rectangle
                CompleteSelectionRectangle();
            }
            
            _isSelectionRectangleMode = false;
            
            // Don't mark as handled - let drag/drop service also see this event
        }

        private void OnTreeViewPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (_disposed) return;
            
            if (_isSelectionRectangleMode && e.LeftButton == MouseButtonState.Pressed)
            {
                var currentPoint = e.GetPosition(_treeView);
                var diff = currentPoint - _selectionStartPoint;
                
                // Start drawing selection rectangle if moved enough
                if (Math.Abs(diff.X) > 5 || Math.Abs(diff.Y) > 5)
                {
                    if (_selectionAdorner == null)
                    {
                        // Create selection rectangle adorner
                        _adornerLayer = AdornerLayer.GetAdornerLayer(_treeView);
                        if (_adornerLayer != null)
                        {
                            _selectionAdorner = new SelectionRectangleAdorner(_treeView, _selectionStartPoint);
                            _adornerLayer.Add(_selectionAdorner);
                        }
                    }
                    else
                    {
                        // Update selection rectangle
                        _selectionAdorner.UpdateEndPoint(currentPoint);
                        UpdateSelectionRectangleItems();
                    }
                    
                    // Mark as handled to prevent drag/drop when doing selection rectangle
                    e.Handled = true;
                }
            }
            
            // If not doing selection rectangle, let drag/drop service handle mouse move
        }

        private void OnTreeViewPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_disposed) return;
            
            // Handle F2 for inline rename
            if (e.Key == Key.F2 && _selectionService.HasSelection && _selectionService.SelectionCount == 1)
            {
                var selectedItem = _selectionService.SelectedItems.FirstOrDefault();
                if (selectedItem != null)
                {
                    selectedItem.IsInEditMode = true;
                    e.Handled = true;
                    return;
                }
            }
            
            // Delegate keyboard shortcuts to selection service
            if (_selectionService.HandleKeyboardShortcut(e.Key, Keyboard.Modifiers, _fileTree.RootItems))
            {
                e.Handled = true;
            }
            else if (e.Key == Key.A && Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                // Ctrl+Shift+A - Open select by pattern dialog
                ShowSelectByPatternDialog();
                e.Handled = true;
            }
        }

        private void OnTreeViewPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_disposed) return;
            
            var position = e.GetPosition(_treeView);
            var item = _performanceManager.GetItemFromPoint(position);
            
            if (item != null)
            {
                // Check if the item is already selected
                if (!_selectionService.IsItemSelected(item))
                {
                    // If not selected, select it (following Windows standard behavior)
                    if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control) && 
                        !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                    {
                        // No modifier keys: clear selection and select only this item
                        _selectionService.SelectSingle(item);
                    }
                    else
                    {
                        // With modifier keys: add to selection
                        _selectionService.ToggleSelection(item);
                    }
                    
                    // Update visual selection immediately
                    item.IsSelected = _selectionService.IsItemSelected(item);
                }
            }
        }

        private void OnItemContainerGeneratorStatusChanged(object sender, EventArgs e)
        {
            if (_disposed) return;
            
            if (_treeView.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                TreeViewItemExtensions.InitializeTreeViewItemLevels(_treeView);
            }
        }

        #endregion

        #region Private Methods

        private void UpdateSelectionRectangleItems()
        {
            if (_selectionAdorner == null || _disposed) return;
            
            var selectionBounds = _selectionAdorner.GetSelectionBounds();
            var addToSelection = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            
            if (!addToSelection)
            {
                _selectionService.ClearSelection();
            }
            
            var items = _performanceManager.GetAllVisibleTreeViewItems();
            foreach (var treeViewItem in items)
            {
                if (treeViewItem.DataContext is FileTreeItem fileItem)
                {
                    var itemBounds = GetItemBounds(treeViewItem);
                    if (itemBounds.IntersectsWith(selectionBounds))
                    {
                        if (!_selectionService.SelectedPaths.Contains(fileItem.Path))
                        {
                            _selectionService.ToggleSelection(fileItem);
                        }
                    }
                }
            }
        }

        private void CompleteSelectionRectangle()
        {
            CleanupSelectionAdorner();
            
            if (_selectionService.HasMultipleSelection && !_selectionService.IsMultiSelectMode)
            {
                _selectionService.IsMultiSelectMode = true;
            }
            
            SelectionRectangleCompleted?.Invoke(this, EventArgs.Empty);
        }
        
        private void CleanupSelectionAdorner()
        {
            if (_adornerLayer != null && _selectionAdorner != null)
            {
                _adornerLayer.Remove(_selectionAdorner);
                _selectionAdorner = null;
                _adornerLayer = null;
            }
        }

        private void ShowSelectByPatternDialog()
        {
            try
            {
                var window = Window.GetWindow(_treeView);
                var dialog = new UI.FileTree.Dialogs.SelectByPatternDialog(window);
                if (dialog.ShowDialog() == true)
                {
                    var visibleItems = GetVisibleItems();
                    _selectionService.SelectByPattern(
                        dialog.Pattern, 
                        dialog.IncludeSubfolders ? _fileTree.RootItems : visibleItems,
                        dialog.AddToSelection);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Select by pattern dialog failed: {ex.Message}");
            }
        }

        private IEnumerable<FileTreeItem> GetVisibleItems()
        {
            var result = new List<FileTreeItem>();
            GetVisibleItemsRecursive(_fileTree.RootItems, result);
            return result;
        }

        private void GetVisibleItemsRecursive(IEnumerable<FileTreeItem> items, List<FileTreeItem> result)
        {
            foreach (var item in items)
            {
                result.Add(item);
                if (item.IsExpanded && item.Children != null)
                {
                    GetVisibleItemsRecursive(item.Children, result);
                }
            }
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                
                DetachEventHandlers();
                CleanupSelectionAdorner();
                
                ItemDoubleClicked = null;
                ItemClicked = null;
                EmptySpaceClicked = null;
                SelectionRectangleCompleted = null;
            }
        }

        #endregion
    }
} 