// UI/FileTree/Services/FileTreeDragDropServiceAdapter.cs - Fixed with IDisposable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Adapter to bridge between IFileTreeDragDropService interface and the enhanced FileTreeDragDropService
    /// Fixed version with proper memory management and IDisposable implementation
    /// </summary>
    public class FileTreeDragDropServiceAdapter : IFileTreeDragDropService, IDisposable
    {
        private readonly FileTreeDragDropService _enhancedService;
        private bool _disposed;
        
        // Store event handlers for proper cleanup
        private EventHandler<FilesDroppedEventArgs> _filesDroppedHandler;
        private EventHandler<FilesMoved> _filesMovedHandler;
        private EventHandler<string> _errorOccurredHandler;
        private EventHandler<OutlookExtractionCompletedEventArgs> _outlookCompletedHandler;
        
        public FileTreeDragDropServiceAdapter(FileTreeDragDropService enhancedService)
        {
            _enhancedService = enhancedService ?? throw new ArgumentNullException(nameof(enhancedService));
            
            // Create and store handlers
            _filesDroppedHandler = (s, e) => FilesDropped?.Invoke(this, e);
            _filesMovedHandler = (s, e) => FilesMoved?.Invoke(this, e);
            _errorOccurredHandler = (s, e) => ErrorOccurred?.Invoke(this, e);
            _outlookCompletedHandler = (s, e) => OutlookExtractionCompleted?.Invoke(this, e);
            
            // Subscribe to events
            _enhancedService.FilesDropped += _filesDroppedHandler;
            _enhancedService.FilesMoved += _filesMovedHandler;
            _enhancedService.ErrorOccurred += _errorOccurredHandler;
            _enhancedService.OutlookExtractionCompleted += _outlookCompletedHandler;
        }
        
        public event EventHandler<FilesDroppedEventArgs> FilesDropped;
        public event EventHandler<FilesMoved> FilesMoved;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<OutlookExtractionCompletedEventArgs> OutlookExtractionCompleted;
        
        public void HandleDragEnter(DragEventArgs e)
        {
            ThrowIfDisposed();
            _enhancedService.HandleDragEnter(e);
        }
        
        public void HandleDragOver(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint)
        {
            ThrowIfDisposed();
            _enhancedService.HandleDragOver(e, getItemFromPoint);
        }
        
        public void HandleDragLeave(DragEventArgs e)
        {
            ThrowIfDisposed();
            _enhancedService.HandleDragLeave(e);
        }
        
        public bool HandleDrop(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint, string currentTreePath = null)
        {
            ThrowIfDisposed();
            return _enhancedService.HandleDrop(e, getItemFromPoint, currentTreePath);
        }
        
        public void StartDrag(DependencyObject source, IEnumerable<string> selectedPaths)
        {
            ThrowIfDisposed();
            // The enhanced service now uses SelectionService internally
            // Just trigger the drag operation
            _enhancedService.StartDrag(source, selectedPaths);
        }
        
        public bool HandleExternalFileDrop(string[] droppedFiles, string targetPath)
        {
            ThrowIfDisposed();
            return _enhancedService.HandleExternalFileDrop(droppedFiles, targetPath);
        }
        
        public bool HandleInternalFileMove(string[] droppedFiles, string targetPath, string currentTreePath)
        {
            ThrowIfDisposed();
            return _enhancedService.HandleInternalFileMove(droppedFiles, targetPath, currentTreePath);
        }
        
        public bool HandleOutlookDrop(DataObject dataObject, string targetPath)
        {
            ThrowIfDisposed();
            return _enhancedService.HandleOutlookDrop(dataObject, targetPath);
        }
        
        public Task<bool> HandleOutlookDropAsync(DataObject dataObject, string targetPath)
        {
            ThrowIfDisposed();
            return _enhancedService.HandleOutlookDropAsync(dataObject, targetPath);
        }
        
        public void CancelOutlookExtraction()
        {
            ThrowIfDisposed();
            _enhancedService.CancelOutlookExtraction();
        }
        
        /// <summary>
        /// Sets the getItemFromPoint function on the enhanced service
        /// </summary>
        public void SetGetItemFromPointFunction(Func<Point, FileTreeItem> getItemFromPoint)
        {
            ThrowIfDisposed();
            _enhancedService.SetGetItemFromPointFunction(getItemFromPoint);
        }
        
        /// <summary>
        /// Attaches the service to a control with the proper function
        /// </summary>
        public void AttachToControl(System.Windows.Controls.Control control, Func<Point, FileTreeItem> getItemFromPoint)
        {
            ThrowIfDisposed();
            _enhancedService.AttachToControl(control, getItemFromPoint);
        }
        
        /// <summary>
        /// Detaches the service from the control
        /// </summary>
        public void DetachFromControl()
        {
            ThrowIfDisposed();
            _enhancedService.DetachFromControl();
        }
        
        #region IDisposable Implementation
        
        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTreeDragDropServiceAdapter));
        }
        
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
                    System.Diagnostics.Debug.WriteLine("[DISPOSE] Disposing FileTreeDragDropServiceAdapter");
                    
                    // Unsubscribe from enhanced service events
                    if (_enhancedService != null)
                    {
                        if (_filesDroppedHandler != null)
                            _enhancedService.FilesDropped -= _filesDroppedHandler;
                        
                        if (_filesMovedHandler != null)
                            _enhancedService.FilesMoved -= _filesMovedHandler;
                        
                        if (_errorOccurredHandler != null)
                            _enhancedService.ErrorOccurred -= _errorOccurredHandler;
                        
                        if (_outlookCompletedHandler != null)
                            _enhancedService.OutlookExtractionCompleted -= _outlookCompletedHandler;
                    }
                    
                    // Clear our own event handlers to release subscribers
                    FilesDropped = null;
                    FilesMoved = null;
                    ErrorOccurred = null;
                    OutlookExtractionCompleted = null;
                    
                    // Clear stored handler references
                    _filesDroppedHandler = null;
                    _filesMovedHandler = null;
                    _errorOccurredHandler = null;
                    _outlookCompletedHandler = null;
                    
                    System.Diagnostics.Debug.WriteLine("[DISPOSE] FileTreeDragDropServiceAdapter disposed");
                }
                
                _disposed = true;
            }
        }
        
        ~FileTreeDragDropServiceAdapter()
        {
            Dispose(false);
        }
        
        #endregion
    }
}