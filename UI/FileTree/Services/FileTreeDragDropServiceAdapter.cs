// UI/FileTree/Services/FileTreeDragDropServiceAdapter.cs
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Adapter to bridge between IFileTreeDragDropService interface and the enhanced FileTreeDragDropService
    /// </summary>
    public class FileTreeDragDropServiceAdapter : IFileTreeDragDropService
    {
        private readonly FileTreeDragDropService _enhancedService;
        
        public FileTreeDragDropServiceAdapter(FileTreeDragDropService enhancedService)
        {
            _enhancedService = enhancedService ?? throw new ArgumentNullException(nameof(enhancedService));
            
            // Forward events
            _enhancedService.FilesDropped += (s, e) => FilesDropped?.Invoke(this, e);
            _enhancedService.FilesMoved += (s, e) => FilesMoved?.Invoke(this, e);
            _enhancedService.ErrorOccurred += (s, e) => ErrorOccurred?.Invoke(this, e);
            _enhancedService.OutlookExtractionCompleted += (s, e) => OutlookExtractionCompleted?.Invoke(this, e);
        }
        
        public event EventHandler<FilesDroppedEventArgs> FilesDropped;
        public event EventHandler<FilesMoved> FilesMoved;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<OutlookExtractionCompletedEventArgs> OutlookExtractionCompleted;
        
        public void HandleDragEnter(DragEventArgs e)
        {
            _enhancedService.HandleDragEnter(e);
        }
        
        public void HandleDragOver(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint)
        {
            _enhancedService.HandleDragOver(e, getItemFromPoint);
        }
        
        public void HandleDragLeave(DragEventArgs e)
        {
            _enhancedService.HandleDragLeave(e);
        }
        
        public bool HandleDrop(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint, string currentTreePath = null)
        {
            return _enhancedService.HandleDrop(e, getItemFromPoint, currentTreePath);
        }
        
        public void StartDrag(DependencyObject source, IEnumerable<string> selectedPaths)
        {
            _enhancedService.StartDrag(source, selectedPaths);
        }
        
        public bool HandleExternalFileDrop(string[] droppedFiles, string targetPath)
        {
            return _enhancedService.HandleExternalFileDrop(droppedFiles, targetPath);
        }
        
        public bool HandleInternalFileMove(string[] droppedFiles, string targetPath, string currentTreePath)
        {
            return _enhancedService.HandleInternalFileMove(droppedFiles, targetPath, currentTreePath);
        }
        
        public bool HandleOutlookDrop(DataObject dataObject, string targetPath)
        {
            return _enhancedService.HandleOutlookDrop(dataObject, targetPath);
        }
        
        public Task<bool> HandleOutlookDropAsync(DataObject dataObject, string targetPath)
        {
            return _enhancedService.HandleOutlookDropAsync(dataObject, targetPath);
        }
        
        public void CancelOutlookExtraction()
        {
            _enhancedService.CancelOutlookExtraction();
        }
    }
}