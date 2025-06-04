using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExplorerPro.FileOperations;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Services;
using ExplorerPro.UI.FileTree.Commands;
using ExplorerPro.Utilities;

namespace ExplorerPro.UI.FileTree.Helpers
{
    /// <summary>
    /// Helper class that handles all file operations for the FileTreeListView.
    /// Provides a clean interface for copy, cut, paste, delete, and create operations.
    /// </summary>
    public class FileTreeOperationHelper : IDisposable
    {
        #region Private Fields

        private readonly IFileTree _fileTree;
        private readonly SelectionService _selectionService;
        private readonly FileOperationHandler _fileOperationHandler;
        private bool _disposed = false;

        #endregion

        #region Constructor

        public FileTreeOperationHelper(
            IFileTree fileTree,
            SelectionService selectionService,
            FileOperationHandler fileOperationHandler)
        {
            _fileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            _fileOperationHandler = fileOperationHandler ?? throw new ArgumentNullException(nameof(fileOperationHandler));
        }

        #endregion

        #region File Operations

        /// <summary>
        /// Copies the currently selected items to the clipboard
        /// </summary>
        public void CopySelected()
        {
            if (!_selectionService.HasSelection)
            {
                System.Diagnostics.Debug.WriteLine("[FILEOP] No items selected for copy operation");
                return;
            }

            try
            {
                var selectedPaths = _selectionService.SelectedPaths.ToList();
                _fileOperationHandler.CopyMultipleItems(selectedPaths);
                
                System.Diagnostics.Debug.WriteLine($"[FILEOP] Copied {selectedPaths.Count} items to clipboard");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Copy operation failed: {ex.Message}");
                // Could show user notification here
            }
        }

        /// <summary>
        /// Cuts the currently selected items (copy + mark for deletion)
        /// </summary>
        public void CutSelected()
        {
            if (!_selectionService.HasSelection)
            {
                System.Diagnostics.Debug.WriteLine("[FILEOP] No items selected for cut operation");
                return;
            }

            try
            {
                var selectedPaths = _selectionService.SelectedPaths.ToList();
                
                // First copy to clipboard
                _fileOperationHandler.CopyMultipleItems(selectedPaths);
                
                // Then mark as cut (could implement visual feedback here)
                System.Diagnostics.Debug.WriteLine($"[FILEOP] Cut {selectedPaths.Count} items (copied and marked for move)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Cut operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Pastes items from clipboard to the current target location
        /// </summary>
        public async void Paste()
        {
            try
            {
                var targetPath = GetTargetPath();
                if (string.IsNullOrEmpty(targetPath))
                {
                    System.Diagnostics.Debug.WriteLine("[FILEOP] No valid target path for paste operation");
                    return;
                }

                await _fileOperationHandler.PasteItemsAsync(targetPath);
                System.Diagnostics.Debug.WriteLine($"[FILEOP] Paste operation completed to: {targetPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Paste operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Deletes the currently selected items
        /// </summary>
        public async void DeleteSelected()
        {
            if (!_selectionService.HasSelection)
            {
                System.Diagnostics.Debug.WriteLine("[FILEOP] No items selected for delete operation");
                return;
            }

            try
            {
                var selectedPaths = _selectionService.SelectedPaths.ToList();
                await _fileOperationHandler.DeleteMultipleItemsAsync(selectedPaths, _fileTree);
                
                System.Diagnostics.Debug.WriteLine($"[FILEOP] Deleted {selectedPaths.Count} items");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Delete operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new folder in the current target location
        /// </summary>
        public void CreateFolder()
        {
            try
            {
                var targetPath = GetTargetPath();
                if (string.IsNullOrEmpty(targetPath))
                {
                    System.Diagnostics.Debug.WriteLine("[FILEOP] No valid target path for create folder operation");
                    return;
                }

                _fileOperationHandler.CreateNewFolder(targetPath, _fileTree);
                System.Diagnostics.Debug.WriteLine($"[FILEOP] Create folder operation initiated in: {targetPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Create folder operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Creates a new file in the current target location
        /// </summary>
        public void CreateFile()
        {
            try
            {
                var targetPath = GetTargetPath();
                if (string.IsNullOrEmpty(targetPath))
                {
                    System.Diagnostics.Debug.WriteLine("[FILEOP] No valid target path for create file operation");
                    return;
                }

                _fileOperationHandler.CreateNewFile(targetPath, _fileTree);
                System.Diagnostics.Debug.WriteLine($"[FILEOP] Create file operation initiated in: {targetPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Create file operation failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Renames the currently selected item
        /// </summary>
        public async void RenameSelected(string newName)
        {
            if (!_selectionService.HasSelection || _selectionService.SelectionCount != 1)
            {
                System.Diagnostics.Debug.WriteLine("[FILEOP] Exactly one item must be selected for rename operation");
                return;
            }

            try
            {
                var selectedPath = _selectionService.FirstSelectedPath;
                if (string.IsNullOrEmpty(selectedPath) || string.IsNullOrWhiteSpace(newName))
                {
                    return;
                }

                var directory = Path.GetDirectoryName(selectedPath);
                var newPath = Path.Combine(directory, newName);

                if (File.Exists(selectedPath))
                {
                    File.Move(selectedPath, newPath);
                }
                else if (Directory.Exists(selectedPath))
                {
                    Directory.Move(selectedPath, newPath);
                }

                // Refresh the tree to show the renamed item
                _fileTree.RefreshDirectory(directory);
                
                System.Diagnostics.Debug.WriteLine($"[FILEOP] Renamed '{selectedPath}' to '{newPath}'");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Rename operation failed: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Gets the target path for file operations (selected folder or parent of selected item)
        /// </summary>
        private string GetTargetPath()
        {
            // If we have a selection, determine the target based on what's selected
            if (_selectionService.HasSelection)
            {
                var selectedPath = _selectionService.FirstSelectedPath;
                
                // If it's a directory, use it as the target
                if (Directory.Exists(selectedPath))
                {
                    return selectedPath;
                }
                
                // If it's a file, use its parent directory
                if (File.Exists(selectedPath))
                {
                    return Path.GetDirectoryName(selectedPath);
                }
            }
            
            // Fall back to current path
            var currentPath = _fileTree.GetCurrentPath();
            return Directory.Exists(currentPath) ? currentPath : null;
        }

        /// <summary>
        /// Checks if the clipboard contains file data that can be pasted
        /// </summary>
        public bool CanPaste()
        {
            try
            {
                return System.Windows.Clipboard.ContainsFileDropList();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Gets information about the current operation capabilities
        /// </summary>
        public OperationCapabilities GetCapabilities()
        {
            return new OperationCapabilities
            {
                CanCopy = _selectionService.HasSelection,
                CanCut = _selectionService.HasSelection,
                CanPaste = CanPaste() && !string.IsNullOrEmpty(GetTargetPath()),
                CanDelete = _selectionService.HasSelection,
                CanCreateFolder = !string.IsNullOrEmpty(GetTargetPath()),
                CanCreateFile = !string.IsNullOrEmpty(GetTargetPath()),
                CanRename = _selectionService.SelectionCount == 1,
                SelectedCount = _selectionService.SelectionCount,
                TargetPath = GetTargetPath()
            };
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;
                // No explicit cleanup needed for this helper
            }
        }

        #endregion

        #region Nested Types

        /// <summary>
        /// Information about what operations are currently possible
        /// </summary>
        public class OperationCapabilities
        {
            public bool CanCopy { get; set; }
            public bool CanCut { get; set; }
            public bool CanPaste { get; set; }
            public bool CanDelete { get; set; }
            public bool CanCreateFolder { get; set; }
            public bool CanCreateFile { get; set; }
            public bool CanRename { get; set; }
            public int SelectedCount { get; set; }
            public string TargetPath { get; set; }
        }

        #endregion
    }
} 