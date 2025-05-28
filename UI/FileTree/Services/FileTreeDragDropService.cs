// UI/FileTree/Services/FileTreeDragDropService.cs - Fixed version with proper drag and drop
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.UI.FileTree.Commands;
using ExplorerPro.UI.FileTree.DragDrop;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Enhanced drag and drop service with multi-selection, visual feedback, and advanced features
    /// </summary>
    public class FileTreeDragDropService : IDisposable
    {
        #region Constants
        
        private const double DRAG_THRESHOLD = 10.0;
        private const string INTERNAL_FORMAT = "ExplorerPro.InternalDrop";
        private const string OUTLOOK_FORMAT = "FileGroupDescriptor";
        
        #endregion
        
        #region Fields
        
        private readonly UndoManager _undoManager;
        private readonly IFileOperations _fileOperations;
        private readonly SelectionService _selectionService;
        
        private Control _control;
        private Point? _dragStartPoint;
        private DependencyObject _dragStartElement;
        private bool _isDragging;
        
        private DragAdorner _dragAdorner;
        private AdornerLayer _adornerLayer;
        private AutoScrollHelper _autoScrollHelper;
        private SpringLoadedFolderHelper _springLoadedHelper;
        
        private FileTreeItem _currentDropTarget;
        private bool _isValidDropTarget;
        private Visual _dropIndicator;
        
        // Store the getItemFromPoint delegate
        private Func<Point, FileTreeItem> _getItemFromPoint;
        
        #endregion
        
        #region Events
        
        public event EventHandler<FilesDroppedEventArgs> FilesDropped;
        public event EventHandler<FilesMoved> FilesMoved;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<OutlookExtractionCompletedEventArgs> OutlookExtractionCompleted;
        
        #endregion
        
        #region Constructor
        
        public FileTreeDragDropService(UndoManager undoManager = null, IFileOperations fileOperations = null, SelectionService selectionService = null)
        {
            _undoManager = undoManager ?? UndoManager.Instance;
            _fileOperations = fileOperations ?? new FileOperations.FileOperations();
            _selectionService = selectionService ?? new SelectionService();
            
            _springLoadedHelper = new SpringLoadedFolderHelper();
            _springLoadedHelper.FolderExpanding += OnSpringLoadedFolderExpanding;
            _springLoadedHelper.FolderCollapsing += OnSpringLoadedFolderCollapsing;
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Attaches the service to a control
        /// </summary>
        public void AttachToControl(Control control, Func<Point, FileTreeItem> getItemFromPoint = null)
        {
            if (_control != null)
                DetachFromControl();
            
            _control = control;
            _getItemFromPoint = getItemFromPoint;
            
            // Find ScrollViewer for auto-scroll
            var scrollViewer = AutoScrollHelper.FindScrollViewer(_control);
            if (scrollViewer != null)
            {
                _autoScrollHelper = new AutoScrollHelper(scrollViewer);
            }
            
            // Attach event handlers
            _control.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
            _control.PreviewMouseMove += OnPreviewMouseMove;
            _control.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            _control.DragEnter += OnDragEnter;
            _control.DragOver += OnDragOver;
            _control.DragLeave += OnDragLeave;
            _control.Drop += OnDrop;
            _control.GiveFeedback += OnGiveFeedback;
            _control.QueryContinueDrag += OnQueryContinueDrag;
        }
        
        /// <summary>
        /// Sets the getItemFromPoint function after attachment
        /// </summary>
        public void SetGetItemFromPointFunction(Func<Point, FileTreeItem> getItemFromPoint)
        {
            _getItemFromPoint = getItemFromPoint;
        }
        
        /// <summary>
        /// Detaches the service from the current control
        /// </summary>
        public void DetachFromControl()
        {
            if (_control == null) return;
            
            _control.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
            _control.PreviewMouseMove -= OnPreviewMouseMove;
            _control.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            _control.DragEnter -= OnDragEnter;
            _control.DragOver -= OnDragOver;
            _control.DragLeave -= OnDragLeave;
            _control.Drop -= OnDrop;
            _control.GiveFeedback -= OnGiveFeedback;
            _control.QueryContinueDrag -= OnQueryContinueDrag;
            
            _autoScrollHelper?.Dispose();
            _autoScrollHelper = null;
            _control = null;
            _getItemFromPoint = null;
        }
        
        #endregion
        
        #region Public Interface Methods (for compatibility)
        
        public void HandleDragEnter(DragEventArgs e)
        {
            OnDragEnter(this, e);
        }
        
        public void HandleDragOver(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint)
        {
            // Store the function if provided
            if (getItemFromPoint != null)
                _getItemFromPoint = getItemFromPoint;
                
            OnDragOver(this, e);
        }
        
        public void HandleDragLeave(DragEventArgs e)
        {
            OnDragLeave(this, e);
        }
        
        public bool HandleDrop(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint, string currentTreePath = null)
        {
            // Store the function if provided
            if (getItemFromPoint != null)
                _getItemFromPoint = getItemFromPoint;
                
            OnDrop(this, e);
            return true;
        }
        
        public void StartDrag(DependencyObject source, IEnumerable<string> selectedPaths)
        {
            // This is now handled internally with multi-selection support
            StartDragOperation();
        }
        
        public bool HandleExternalFileDrop(string[] droppedFiles, string targetPath)
        {
            return HandleFileDropInternal(droppedFiles, targetPath, DragDropEffects.Copy, false);
        }
        
        public bool HandleInternalFileMove(string[] droppedFiles, string targetPath, string currentTreePath)
        {
            return HandleFileDropInternal(droppedFiles, targetPath, DragDropEffects.Move, true);
        }
        
        public bool HandleOutlookDrop(DataObject dataObject, string targetPath)
        {
            var task = HandleOutlookDropAsync(dataObject, targetPath);
            task.Wait();
            return task.Result;
        }
        
        public async Task<bool> HandleOutlookDropAsync(DataObject dataObject, string targetPath)
        {
            try
            {
                var extractionResult = await OutlookDataExtractor.ExtractOutlookFilesAsync(dataObject, targetPath);
                
                OnOutlookExtractionCompleted(new OutlookExtractionCompletedEventArgs(extractionResult, targetPath));
                
                return extractionResult.Success;
            }
            catch (Exception ex)
            {
                OnError($"Outlook extraction failed: {ex.Message}");
                return false;
            }
        }
        
        public void CancelOutlookExtraction()
        {
            // Cancellation not implemented in current version
        }
        
        #endregion
        
        #region Drag Initiation
        
        private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = e.GetPosition(_control);
            _dragStartElement = e.OriginalSource as DependencyObject;
        }
        
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || !_dragStartPoint.HasValue)
                return;
            
            Point currentPosition = e.GetPosition(_control);
            Vector difference = _dragStartPoint.Value - currentPosition;
            
            if (Math.Abs(difference.X) > DRAG_THRESHOLD || Math.Abs(difference.Y) > DRAG_THRESHOLD)
            {
                StartDragOperation();
            }
        }
        
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = null;
            _dragStartElement = null;
        }
        
        private void StartDragOperation()
        {
            if (!_selectionService.HasSelection || _isDragging)
                return;
            
            try
            {
                _isDragging = true;
                
                // Create data object with selected files
                var dataObject = CreateDragDataObject();
                
                // Create visual feedback
                CreateDragAdorner();
                
                // Start drag operation
                DragDropEffects effects = System.Windows.DragDrop.DoDragDrop(_control, dataObject, 
                    DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
                
                // Handle result
                if (effects != DragDropEffects.None)
                {
                    // Operation completed
                }
            }
            catch (Exception ex)
            {
                OnError($"Failed to start drag: {ex.Message}");
            }
            finally
            {
                CleanupDrag();
            }
        }
        
        private DataObject CreateDragDataObject()
        {
            var dataObject = new DataObject();
            
            // Add file paths
            var paths = _selectionService.SelectedPaths.ToArray();
            dataObject.SetData(DataFormats.FileDrop, paths);
            
            // Add internal marker
            dataObject.SetData(INTERNAL_FORMAT, true);
            
            // Add selection count for visual feedback
            dataObject.SetData("ItemCount", _selectionService.SelectionCount);
            
            return dataObject;
        }
        
        private void CreateDragAdorner()
        {
            if (_control == null || _selectionService.SelectedItems.Count == 0)
                return;
            
            _adornerLayer = AdornerLayer.GetAdornerLayer(_control);
            if (_adornerLayer != null)
            {
                // Create visual element for the adorner
                var visual = CreateDragVisual();
                
                // Calculate proper offset from the drag start element
                Point offset = new Point(0, 0);
                if (_dragStartElement != null && _dragStartPoint.HasValue)
                {
                    // Find the visual element of the selected item
                    var itemVisual = FindItemVisual(_dragStartElement);
                    if (itemVisual != null)
                    {
                        // Get position of item relative to control
                        var itemPosition = itemVisual.TransformToAncestor(_control).Transform(new Point(0, 0));
                        // Calculate offset from mouse position to item position
                        offset = new Point(
                            _dragStartPoint.Value.X - itemPosition.X,
                            _dragStartPoint.Value.Y - itemPosition.Y
                        );
                    }
                }
                
                _dragAdorner = new DragAdorner(_control, visual, offset, _selectionService.SelectionCount);
                _adornerLayer.Add(_dragAdorner);
                
                // Set initial position
                _dragAdorner.UpdatePosition(Mouse.GetPosition(_adornerLayer));
            }
        }
        
        private Visual FindItemVisual(DependencyObject element)
        {
            // Walk up the visual tree to find the TreeViewItem
            var current = element;
            while (current != null && !(current is TreeViewItem))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            
            if (current is TreeViewItem treeViewItem)
            {
                // Find the content presenter
                return FindVisualChild<ContentPresenter>(treeViewItem);
            }
            
            return element as Visual;
        }
        
        private T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result)
                    return result;
                    
                var childResult = FindVisualChild<T>(child);
                if (childResult != null)
                    return childResult;
            }
            return null;
        }
        
        private Visual CreateDragVisual()
        {
            // Create a more sophisticated visual with icon
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromArgb(200, 240, 240, 240)),
                Margin = new Thickness(2)
            };
            
            // Add icon
            if (_selectionService.SelectedItems.Count > 0)
            {
                var firstItem = _selectionService.SelectedItems[0];
                if (firstItem.Icon != null)
                {
                    var icon = new Image
                    {
                        Source = firstItem.Icon,
                        Width = 16,
                        Height = 16,
                        Margin = new Thickness(4, 2, 2, 2)
                    };
                    panel.Children.Add(icon);
                }
            }
            
            // Add text
            var textBlock = new TextBlock
            {
                Text = _selectionService.SelectionCount == 1 
                    ? Path.GetFileName(_selectionService.SelectedPaths.First())
                    : $"{_selectionService.SelectionCount} items",
                Padding = new Thickness(4, 2, 4, 2),
                VerticalAlignment = VerticalAlignment.Center
            };
            panel.Children.Add(textBlock);
            
            // Create border
            var border = new Border
            {
                Child = panel,
                BorderBrush = new SolidColorBrush(Colors.Gray),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Background = new SolidColorBrush(Color.FromArgb(240, 255, 255, 255))
            };
            
            // Force layout
            border.Measure(new Size(300, 100));
            border.Arrange(new Rect(border.DesiredSize));
            
            return border;
        }
        
        #endregion
        
        #region Drag Over Handling
        
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
            ProcessDragOver(e);
        }
        
        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            // Get item under mouse using the stored function
            if (_getItemFromPoint != null)
            {
                var position = e.GetPosition(_control);
                var item = _getItemFromPoint(position);
                UpdateDropTarget(item);
            }
            
            ProcessDragOver(e);
            
            // Update auto-scroll
            if (_autoScrollHelper != null)
            {
                var position = e.GetPosition(_control);
                _autoScrollHelper.UpdatePosition(position);
                
                if (_autoScrollHelper.IsInScrollZone(position))
                    _autoScrollHelper.Start();
                else
                    _autoScrollHelper.Stop();
            }
            
            // Update adorner position
            if (_dragAdorner != null && _adornerLayer != null)
            {
                _dragAdorner.UpdatePosition(e.GetPosition(_adornerLayer));
                _dragAdorner.UpdateEffects(e.Effects);
            }
        }
        
        private void OnDragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            // Check if really leaving
            Point pt = e.GetPosition(_control);
            Rect bounds = new Rect(_control.RenderSize);
            
            if (!bounds.Contains(pt))
            {
                UpdateDropTarget(null);
                _autoScrollHelper?.Stop();
                _springLoadedHelper?.CancelAll();
            }
        }
        
        private void ProcessDragOver(DragEventArgs e)
        {
            // Validate drop
            if (!IsValidDrop(e))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            
            // Determine effects based on modifiers
            e.Effects = DetermineDropEffects(e);
            
            // Handle spring-loaded folders
            if (_currentDropTarget != null && _currentDropTarget.IsDirectory && !_currentDropTarget.IsExpanded)
            {
                _springLoadedHelper.StartHover(_currentDropTarget);
            }
            else if (_currentDropTarget != null)
            {
                _springLoadedHelper.StopHover(_currentDropTarget);
            }
        }
        
        private DragDropEffects DetermineDropEffects(DragEventArgs e)
        {
            if (!_isValidDropTarget)
                return DragDropEffects.None;
            
            // Check modifier keys
            if ((e.KeyStates & DragDropKeyStates.ControlKey) != 0)
                return DragDropEffects.Copy;
            
            if ((e.KeyStates & DragDropKeyStates.AltKey) != 0)
                return DragDropEffects.Link;
            
            // Default behavior
            bool isInternal = e.Data.GetDataPresent(INTERNAL_FORMAT);
            return isInternal ? DragDropEffects.Move : DragDropEffects.Copy;
        }
        
        #endregion
        
        #region Drop Handling
        
        private void OnDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            try
            {
                _autoScrollHelper?.Stop();
                _springLoadedHelper?.CollapseAll();
                
                // Get final drop target
                if (_getItemFromPoint != null)
                {
                    var position = e.GetPosition(_control);
                    var item = _getItemFromPoint(position);
                    UpdateDropTarget(item);
                }
                
                if (!_isValidDropTarget || _currentDropTarget == null)
                {
                    e.Effects = DragDropEffects.None;
                    return;
                }
                
                string targetPath = _currentDropTarget.Path;
                var effects = DetermineDropEffects(e);
                
                // Handle different data types
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    bool isInternal = e.Data.GetDataPresent(INTERNAL_FORMAT);
                    
                    HandleFileDropInternal(files, targetPath, effects, isInternal);
                }
                else if (IsOutlookData(e.Data))
                {
                    _ = HandleOutlookDropAsync(e.Data as DataObject, targetPath);
                }
            }
            finally
            {
                UpdateDropTarget(null);
            }
        }
        
        private bool HandleFileDropInternal(string[] files, string targetPath, DragDropEffects effects, bool isInternal)
        {
            try
            {
                // Filter out invalid files
                files = files.Where(f => File.Exists(f) || Directory.Exists(f)).ToArray();
                if (!files.Any()) return false;
                
                // Create and execute command for undo support
                var command = new DragDropCommand(_fileOperations, files, targetPath, effects);
                _undoManager.ExecuteCommand(command);
                
                // Raise appropriate events
                if (effects == DragDropEffects.Move && isInternal)
                {
                    var sourceDirs = files.Select(f => Path.GetDirectoryName(f))
                        .Where(d => !string.IsNullOrEmpty(d))
                        .Distinct()
                        .ToArray();
                    
                    OnFilesMoved(new FilesMoved(files, sourceDirs, targetPath));
                }
                
                OnFilesDropped(new FilesDroppedEventArgs(files, targetPath, effects, isInternal));
                
                return true;
            }
            catch (Exception ex)
            {
                OnError($"Drop operation failed: {ex.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region Visual Feedback
        
        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (_dragAdorner != null)
            {
                // Custom cursors based on effect
                e.UseDefaultCursors = false;
                
                switch (e.Effects)
                {
                    case DragDropEffects.Copy:
                        Mouse.SetCursor(Cursors.Cross);
                        break;
                    case DragDropEffects.Move:
                        Mouse.SetCursor(Cursors.Hand);
                        break;
                    case DragDropEffects.Link:
                        Mouse.SetCursor(Cursors.UpArrow);
                        break;
                    case DragDropEffects.None:
                        Mouse.SetCursor(Cursors.No);
                        break;
                    default:
                        e.UseDefaultCursors = true;
                        break;
                }
                
                e.Handled = true;
            }
        }
        
        private void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed)
            {
                e.Action = DragAction.Cancel;
                CleanupDrag();
            }
        }
        
        private void UpdateDropTarget(FileTreeItem item)
        {
            // Clear old highlight
            if (_currentDropTarget != null)
            {
                ClearDropTargetVisual(_currentDropTarget);
            }
            
            _currentDropTarget = item;
            _isValidDropTarget = ValidateDropTarget(item);
            
            // Apply new highlight
            if (_currentDropTarget != null)
            {
                ApplyDropTargetVisual(_currentDropTarget, _isValidDropTarget);
            }
        }
        
        private void ApplyDropTargetVisual(FileTreeItem item, bool isValid)
        {
            if (item == null) return;
            
            // Find the TreeViewItem for this data
            var treeViewItem = FindTreeViewItemForData(item);
            if (treeViewItem != null)
            {
                if (isValid)
                {
                    DragDropHelper.SetIsDropTarget(treeViewItem, true);
                    DragDropHelper.SetIsInvalidDropTarget(treeViewItem, false);
                }
                else
                {
                    DragDropHelper.SetIsDropTarget(treeViewItem, false);
                    DragDropHelper.SetIsInvalidDropTarget(treeViewItem, true);
                }
            }
        }
        
        private void ClearDropTargetVisual(FileTreeItem item)
        {
            if (item == null) return;
            
            var treeViewItem = FindTreeViewItemForData(item);
            if (treeViewItem != null)
            {
                DragDropHelper.SetIsDropTarget(treeViewItem, false);
                DragDropHelper.SetIsInvalidDropTarget(treeViewItem, false);
            }
        }
        
        private TreeViewItem FindTreeViewItemForData(FileTreeItem data)
        {
            if (_control is TreeView treeView)
            {
                return FindTreeViewItem(treeView, data);
            }
            return null;
        }
        
        private TreeViewItem FindTreeViewItem(ItemsControl container, object data)
        {
            if (container == null) return null;
            
            if (container.ItemContainerGenerator.Status == System.Windows.Controls.Primitives.GeneratorStatus.ContainersGenerated)
            {
                var item = container.ItemContainerGenerator.ContainerFromItem(data) as TreeViewItem;
                if (item != null) return item;
                
                foreach (var childData in container.Items)
                {
                    var childContainer = container.ItemContainerGenerator.ContainerFromItem(childData) as TreeViewItem;
                    if (childContainer != null)
                    {
                        var result = FindTreeViewItem(childContainer, data);
                        if (result != null) return result;
                    }
                }
            }
            return null;
        }
        
        #endregion
        
        #region Validation
        
        private bool IsValidDrop(DragEventArgs e)
        {
            return e.Data.GetDataPresent(DataFormats.FileDrop) || IsOutlookData(e.Data);
        }
        
        private bool ValidateDropTarget(FileTreeItem target)
        {
            if (target == null || !target.IsDirectory)
                return false;
            
            // Can't drop on selected items
            if (_selectionService.SelectedPaths.Contains(target.Path))
                return false;
            
            // Can't drop parent on child
            foreach (var selectedPath in _selectionService.SelectedPaths)
            {
                if (target.Path.StartsWith(selectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            
            return true;
        }
        
        private bool IsOutlookData(IDataObject data)
        {
            return data.GetDataPresent("FileGroupDescriptor") || 
                   data.GetDataPresent("FileGroupDescriptorW");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void CleanupDrag()
        {
            _isDragging = false;
            _dragStartPoint = null;
            _dragStartElement = null;
            
            if (_dragAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_dragAdorner);
                _dragAdorner = null;
            }
            
            _autoScrollHelper?.Stop();
            _springLoadedHelper?.CollapseAll();
            UpdateDropTarget(null);
        }
        
        private void OnSpringLoadedFolderExpanding(object sender, FileTreeItem item)
        {
            if (item != null)
                item.IsExpanded = true;
        }
        
        private void OnSpringLoadedFolderCollapsing(object sender, FileTreeItem item)
        {
            if (item != null && _springLoadedHelper.WasAutoExpanded(item))
                item.IsExpanded = false;
        }
        
        #endregion
        
        #region Event Raising
        
        private void OnFilesDropped(FilesDroppedEventArgs e)
        {
            FilesDropped?.Invoke(this, e);
        }
        
        private void OnFilesMoved(FilesMoved e)
        {
            FilesMoved?.Invoke(this, e);
        }
        
        private void OnError(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }
        
        private void OnOutlookExtractionCompleted(OutlookExtractionCompletedEventArgs e)
        {
            OutlookExtractionCompleted?.Invoke(this, e);
        }
        
        #endregion
        
        #region IDisposable
        
        private bool _disposed;
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    DetachFromControl();
                    CleanupDrag();
                    
                    _autoScrollHelper?.Dispose();
                    _springLoadedHelper?.Dispose();
                }
                
                _disposed = true;
            }
        }
        
        #endregion
    }
}