// UI/FileTree/Services/FileTreeDragDropService.cs - Enhanced version with all improvements
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
        private bool _isDragging;
        
        private DragAdorner _dragAdorner;
        private AdornerLayer _adornerLayer;
        private AutoScrollHelper _autoScrollHelper;
        private SpringLoadedFolderHelper _springLoadedHelper;
        
        private FileTreeItem _currentDropTarget;
        private bool _isValidDropTarget;
        private Visual _dropIndicator;
        
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
        public void AttachToControl(Control control)
        {
            if (_control != null)
                DetachFromControl();
            
            _control = control;
            
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
        }
        
        #endregion
        
        #region Public Interface Methods (for compatibility)
        
        public void HandleDragEnter(DragEventArgs e)
        {
            OnDragEnter(this, e);
        }
        
        public void HandleDragOver(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint)
        {
            var item = getItemFromPoint(e.GetPosition(_control));
            UpdateDropTarget(item);
            OnDragOver(this, e);
        }
        
        public void HandleDragLeave(DragEventArgs e)
        {
            OnDragLeave(this, e);
        }
        
        public bool HandleDrop(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint, string currentTreePath = null)
        {
            var item = getItemFromPoint(e.GetPosition(_control));
            UpdateDropTarget(item);
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
                DragDropEffects effects = DragDrop.DoDragDrop(_control, dataObject, 
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
                // Create simple visual element for the adorner
                var visual = CreateDragVisual();
                var offset = Mouse.GetPosition(_control);
                
                _dragAdorner = new DragAdorner(_control, visual, offset, _selectionService.SelectionCount);
                _adornerLayer.Add(_dragAdorner);
            }
        }
        
        private Visual CreateDragVisual()
        {
            // Create a simple text block for now
            var textBlock = new TextBlock
            {
                Text = _selectionService.SelectionCount == 1 
                    ? Path.GetFileName(_selectionService.SelectedPaths.First())
                    : $"{_selectionService.SelectionCount} items",
                Padding = new Thickness(8, 4, 8, 4),
                Background = new SolidColorBrush(Color.FromArgb(180, 0, 120, 212)),
                Foreground = Brushes.White,
                FontSize = 12
            };
            
            // Force layout
            textBlock.Measure(new Size(200, 50));
            textBlock.Arrange(new Rect(textBlock.DesiredSize));
            
            return textBlock;
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
                // TODO: Remove visual highlight from old target
            }
            
            _currentDropTarget = item;
            _isValidDropTarget = ValidateDropTarget(item);
            
            // Apply new highlight
            if (_currentDropTarget != null && _isValidDropTarget)
            {
                // TODO: Add visual highlight to new target
            }
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