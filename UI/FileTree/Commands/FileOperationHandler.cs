using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.UI.FileTree.Commands
{
    /// <summary>
    /// Handles all file system operations for the file tree, including undo support
    /// </summary>
    public class FileOperationHandler
    {
        #region Fields
        
        private readonly IFileOperations _fileOperations;
        private readonly UndoManager _undoManager;
        private readonly MetadataManager _metadataManager;
        private readonly ILogger<FileOperationHandler> _logger;
        
        #endregion
        
        #region Events
        
        /// <summary>
        /// Raised when a directory needs to be refreshed after an operation
        /// </summary>
        public event EventHandler<DirectoryRefreshEventArgs> DirectoryRefreshRequested;
        
        /// <summary>
        /// Raised when multiple directories need to be refreshed
        /// </summary>
        public event EventHandler<MultipleDirectoriesRefreshEventArgs> MultipleDirectoriesRefreshRequested;
        
        /// <summary>
        /// Raised when an operation fails
        /// </summary>
        public event EventHandler<FileOperationErrorEventArgs> OperationError;
        
        /// <summary>
        /// Raised when a paste operation completes
        /// </summary>
        public event EventHandler<PasteCompletedEventArgs> PasteCompleted;
        
        #endregion
        
        #region Constructor
        
        public FileOperationHandler(
            IFileOperations fileOperations,
            UndoManager undoManager,
            MetadataManager metadataManager,
            ILogger<FileOperationHandler> logger = null)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
            _metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
            _logger = logger;
        }
        
        #endregion
        
        #region Delete Operations
        
        /// <summary>
        /// Deletes a single item with undo support
        /// </summary>
        public bool DeleteItem(string path, IFileTree fileTree, bool confirmDelete = true)
        {
            if (!ValidatePathExists(path))
            {
                ShowError($"'{path}' does not exist.", "Delete Error");
                return false;
            }

            if (confirmDelete)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete:\n{path}?", 
                    "Confirm Delete", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                    
                if (result != MessageBoxResult.Yes)
                    return false;
            }

            try
            {
                var command = new DeleteItemCommand(_fileOperations, fileTree, path, _logger);
                _undoManager.ExecuteCommand(command);
                
                RequestRefreshParentDirectory(path);
                return true;
            }
            catch (Exception ex)
            {
                HandleOperationError("Delete failed", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Deletes multiple items with undo support
        /// </summary>
        public async Task<bool> DeleteMultipleItemsAsync(IReadOnlyList<string> paths, IFileTree fileTree, bool confirmDelete = true)
        {
            if (paths == null || paths.Count == 0)
                return false;
                
            // Validate all paths exist
            var invalidPaths = paths.Where(p => !ValidatePathExists(p, false)).ToList();
            if (invalidPaths.Any())
            {
                ShowError($"The following items do not exist:\n{string.Join("\n", invalidPaths.Take(5))}" +
                         (invalidPaths.Count > 5 ? $"\n... and {invalidPaths.Count - 5} more" : ""), 
                         "Delete Error");
                return false;
            }

            if (confirmDelete)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete {paths.Count} items?", 
                    "Confirm Delete", 
                    MessageBoxButton.YesNo, 
                    MessageBoxImage.Question);
                    
                if (result != MessageBoxResult.Yes)
                    return false;
            }

            return await Task.Run(() =>
            {
                var successCount = 0;
                var errors = new List<string>();
                
                foreach (var path in paths)
                {
                    try
                    {
                        var command = new DeleteItemCommand(_fileOperations, fileTree, path, _logger);
                        _undoManager.ExecuteCommand(command);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{Path.GetFileName(path)}: {ex.Message}");
                        _logger?.LogError(ex, $"Failed to delete: {path}");
                    }
                }
                
                // Request refresh for affected directories
                var affectedDirectories = paths
                    .Select(p => Path.GetDirectoryName(p))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .ToList();
                    
                RequestRefreshMultipleDirectories(affectedDirectories);
                
                // Show summary if there were errors
                if (errors.Any())
                {
                    var message = errors.Count == paths.Count 
                        ? "Failed to delete all items:\n" 
                        : $"Deleted {successCount} of {paths.Count} items.\nFailed items:\n";
                    
                    ShowError(message + string.Join("\n", errors.Take(5)) +
                             (errors.Count > 5 ? $"\n... and {errors.Count - 5} more errors" : ""),
                             "Delete Operation Completed with Errors");
                }
                
                return successCount > 0;
            });
        }
        
        #endregion
        
        #region Copy Operations
        
        /// <summary>
        /// Copies a single item to clipboard
        /// </summary>
        public bool CopyItem(string path)
        {
            if (!ValidatePathExists(path))
            {
                ShowError("The selected file or folder does not exist.", "Copy Error");
                return false;
            }
            
            try
            {
                var filePaths = new System.Collections.Specialized.StringCollection();
                filePaths.Add(path);
                Clipboard.SetFileDropList(filePaths);
                
                _logger?.LogInformation($"Copied to clipboard: {path}");
                return true;
            }
            catch (Exception ex)
            {
                HandleOperationError("Copy failed", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Copies multiple items to clipboard
        /// </summary>
        public bool CopyMultipleItems(IReadOnlyList<string> paths)
        {
            if (paths == null || paths.Count == 0)
                return false;
                
            // Validate all paths exist
            var validPaths = paths.Where(p => ValidatePathExists(p, false)).ToList();
            if (!validPaths.Any())
            {
                ShowError("No valid items to copy.", "Copy Error");
                return false;
            }
            
            try
            {
                var filePaths = new System.Collections.Specialized.StringCollection();
                filePaths.AddRange(validPaths.ToArray());
                Clipboard.SetFileDropList(filePaths);
                
                _logger?.LogInformation($"Copied {validPaths.Count} items to clipboard");
                
                if (validPaths.Count < paths.Count)
                {
                    ShowInfo($"Copied {validPaths.Count} of {paths.Count} items.\n" +
                            $"{paths.Count - validPaths.Count} items were invalid.",
                            "Copy Operation");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                HandleOperationError("Copy failed", ex);
                return false;
            }
        }
        
        #endregion
        
        #region Paste Operations
        
        /// <summary>
        /// Pastes items from clipboard to target directory
        /// </summary>
        public async Task<bool> PasteItemsAsync(string targetPath)
        {
            if (!Directory.Exists(targetPath))
            {
                ShowError("You can only paste into a directory.", "Paste Error");
                return false;
            }

            var files = Clipboard.GetFileDropList();
            if (files == null || files.Count == 0)
            {
                ShowError("No valid file path(s) in clipboard.", "Paste Error");
                return false;
            }

            return await Task.Run(() =>
            {
                var successCount = 0;
                var errors = new List<string>();
                
                foreach (string sourcePath in files)
                {
                    try
                    {
                        if (File.Exists(sourcePath) || Directory.Exists(sourcePath))
                        {
                            string newPath = _fileOperations.CopyItem(sourcePath, targetPath);
                            if (!string.IsNullOrEmpty(newPath))
                            {
                                successCount++;
                                _logger?.LogInformation($"Pasted: {sourcePath} -> {newPath}");
                            }
                            else
                            {
                                errors.Add(Path.GetFileName(sourcePath));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"{Path.GetFileName(sourcePath)}: {ex.Message}");
                        _logger?.LogError(ex, $"Failed to paste: {sourcePath}");
                    }
                }
                
                RequestRefreshDirectory(targetPath);
                
                // Show summary
                if (errors.Any())
                {
                    var message = successCount == 0 
                        ? "Failed to paste all items:\n"
                        : $"Pasted {successCount} of {files.Count} items.\nFailed items:\n";
                        
                    ShowError(message + string.Join("\n", errors.Take(5)) +
                             (errors.Count > 5 ? $"\n... and {errors.Count - 5} more errors" : ""),
                             "Paste Operation Completed with Errors");
                }
                else if (successCount > 0)
                {
                    _logger?.LogInformation($"Successfully pasted {successCount} items");
                }
                
                // Raise paste completed event
                OnPasteCompleted(new PasteCompletedEventArgs(targetPath, successCount, files.Count));
                
                return successCount > 0;
            });
        }
        
        #endregion
        
        #region Create Operations
        
        /// <summary>
        /// Creates a new file in the specified directory
        /// </summary>
        public bool CreateNewFile(string directoryPath, IFileTree fileTree, string defaultFileName = "")
        {
            if (!ValidateDirectory(directoryPath))
            {
                ShowError("Cannot create a file outside a folder.", "Invalid Target");
                return false;
            }

            string newFileName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter file name (e.g., new_file.txt):", 
                "Add New File", 
                defaultFileName);
            
            if (string.IsNullOrWhiteSpace(newFileName))
                return false;
                
            // Validate filename
            if (newFileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ShowError("The file name contains invalid characters.", "Invalid File Name");
                return false;
            }
            
            try
            {
                var command = new CreateFileCommand(_fileOperations, fileTree, directoryPath, newFileName, _logger);
                _undoManager.ExecuteCommand(command);
                
                RequestRefreshDirectory(directoryPath);
                return true;
            }
            catch (Exception ex)
            {
                HandleOperationError("Create file failed", ex);
                return false;
            }
        }
        
        /// <summary>
        /// Creates a new folder in the specified directory
        /// </summary>
        public bool CreateNewFolder(string directoryPath, IFileTree fileTree, string defaultFolderName = "New Folder")
        {
            if (!ValidateDirectory(directoryPath))
            {
                ShowError("Cannot create a folder outside a directory.", "Invalid Target");
                return false;
            }

            string newFolderName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter folder name:", 
                "Add New Folder", 
                defaultFolderName);
            
            if (string.IsNullOrWhiteSpace(newFolderName))
                return false;
                
            // Validate folder name
            if (newFolderName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ShowError("The folder name contains invalid characters.", "Invalid Folder Name");
                return false;
            }
            
            try
            {
                var command = new CreateFolderCommand(_fileOperations, fileTree, directoryPath, newFolderName, _logger);
                _undoManager.ExecuteCommand(command);
                
                RequestRefreshDirectory(directoryPath);
                return true;
            }
            catch (Exception ex)
            {
                HandleOperationError("Create folder failed", ex);
                return false;
            }
        }
        
        #endregion
        
        #region Rename Operations
        
        /// <summary>
        /// Renames a file or folder
        /// </summary>
        public bool RenameItem(string oldPath, string newName, IFileTree fileTree)
        {
            if (!ValidatePathExists(oldPath))
            {
                ShowError("The item to rename does not exist.", "Rename Error");
                return false;
            }
            
            if (string.IsNullOrWhiteSpace(newName))
            {
                ShowError("Please provide a valid name.", "Rename Error");
                return false;
            }
            
            // Validate new name
            if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
            {
                ShowError("The new name contains invalid characters.", "Invalid Name");
                return false;
            }
            
            try
            {
                var command = new RenameCommand(_fileOperations, oldPath, newName, _logger);
                _undoManager.ExecuteCommand(command);
                
                // Update metadata references
                var newPath = command.NewPath;
                if (!string.IsNullOrEmpty(newPath))
                {
                    _metadataManager.UpdatePathReferences(oldPath, newPath);
                }
                
                RequestRefreshParentDirectory(oldPath);
                return true;
            }
            catch (Exception ex)
            {
                HandleOperationError("Rename failed", ex);
                return false;
            }
        }
        
        #endregion
        
        #region Utility Methods
        
        private bool ValidatePathExists(string path, bool showError = true)
        {
            if (string.IsNullOrEmpty(path))
                return false;
                
            bool exists = File.Exists(path) || Directory.Exists(path);
            if (!exists && showError)
            {
                ShowError($"'{path}' does not exist.", "Path Not Found");
            }
            
            return exists;
        }
        
        private bool ValidateDirectory(string path, bool showError = true)
        {
            if (string.IsNullOrEmpty(path))
                return false;
                
            bool exists = Directory.Exists(path);
            if (!exists && showError)
            {
                ShowError($"Directory '{path}' does not exist.", "Directory Not Found");
            }
            
            return exists;
        }
        
        private void ShowError(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
        
        private void ShowInfo(string message, string title)
        {
            MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        
        private void HandleOperationError(string operation, Exception ex)
        {
            _logger?.LogError(ex, $"{operation}: {ex.Message}");
            OnOperationError(new FileOperationErrorEventArgs(operation, ex));
            ShowError($"{operation}: {ex.Message}", "Operation Failed");
        }
        
        private void RequestRefreshDirectory(string directoryPath)
        {
            if (!string.IsNullOrEmpty(directoryPath))
            {
                OnDirectoryRefreshRequested(new DirectoryRefreshEventArgs(directoryPath));
            }
        }
        
        private void RequestRefreshParentDirectory(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                string parentDir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(parentDir))
                {
                    RequestRefreshDirectory(parentDir);
                }
            }
        }
        
        private void RequestRefreshMultipleDirectories(IEnumerable<string> directories)
        {
            var validDirs = directories?.Where(d => !string.IsNullOrEmpty(d)).ToList();
            if (validDirs?.Any() == true)
            {
                OnMultipleDirectoriesRefreshRequested(new MultipleDirectoriesRefreshEventArgs(validDirs));
            }
        }
        
        #endregion
        
        #region Event Raising
        
        protected virtual void OnDirectoryRefreshRequested(DirectoryRefreshEventArgs e)
        {
            DirectoryRefreshRequested?.Invoke(this, e);
        }
        
        protected virtual void OnMultipleDirectoriesRefreshRequested(MultipleDirectoriesRefreshEventArgs e)
        {
            MultipleDirectoriesRefreshRequested?.Invoke(this, e);
        }
        
        protected virtual void OnOperationError(FileOperationErrorEventArgs e)
        {
            OperationError?.Invoke(this, e);
        }
        
        protected virtual void OnPasteCompleted(PasteCompletedEventArgs e)
        {
            PasteCompleted?.Invoke(this, e);
        }
        
        #endregion
    }
    
    #region Event Arguments
    
    /// <summary>
    /// Event arguments for directory refresh requests
    /// </summary>
    public class DirectoryRefreshEventArgs : EventArgs
    {
        public string DirectoryPath { get; }
        
        public DirectoryRefreshEventArgs(string directoryPath)
        {
            DirectoryPath = directoryPath;
        }
    }
    
    /// <summary>
    /// Event arguments for multiple directory refresh requests
    /// </summary>
    public class MultipleDirectoriesRefreshEventArgs : EventArgs
    {
        public IReadOnlyList<string> DirectoryPaths { get; }
        
        public MultipleDirectoriesRefreshEventArgs(IEnumerable<string> directoryPaths)
        {
            DirectoryPaths = directoryPaths?.ToList() ?? new List<string>();
        }
    }
    
    /// <summary>
    /// Event arguments for file operation errors
    /// </summary>
    public class FileOperationErrorEventArgs : EventArgs
    {
        public string Operation { get; }
        public Exception Exception { get; }
        
        public FileOperationErrorEventArgs(string operation, Exception exception)
        {
            Operation = operation;
            Exception = exception;
        }
    }
    
    /// <summary>
    /// Event arguments for paste operation completion
    /// </summary>
    public class PasteCompletedEventArgs : EventArgs
    {
        public string TargetPath { get; }
        public int SuccessCount { get; }
        public int TotalCount { get; }
        
        public PasteCompletedEventArgs(string targetPath, int successCount, int totalCount)
        {
            TargetPath = targetPath;
            SuccessCount = successCount;
            TotalCount = totalCount;
        }
    }
    
    #endregion
}