// UI/FileTree/Services/FileTreeDragDropService.cs (UPDATED)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service for handling file tree drag and drop operations
    /// </summary>
    public class FileTreeDragDropService : IFileTreeDragDropService
    {
        private const double DragThreshold = 10.0;

        public event EventHandler<FilesDroppedEventArgs> FilesDropped;
        public event EventHandler<FilesMoved> FilesMoved;
        public event EventHandler<string> ErrorOccurred;

        public void HandleDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        public void HandleDragOver(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint)
        {
            if (getItemFromPoint == null)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }

            // Get the item under the cursor
            var item = getItemFromPoint(e.GetPosition((IInputElement)e.Source));

            if (item != null && item.IsDirectory)
            {
                // Determine if this is a copy or move operation
                // For internal drops (within the same tree), default to move
                // For external drops, default to copy
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.Move; // Default to move for file operations
                }
                else
                {
                    e.Effects = DragDropEffects.Copy;
                }
                
                item.IsSelected = true;
                Mouse.OverrideCursor = Cursors.Arrow;
            }
            else
            {
                e.Effects = DragDropEffects.None;
                Mouse.OverrideCursor = Cursors.No;
            }

            e.Handled = true;
        }

        public bool HandleDrop(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint, string currentTreePath = null)
        {
            if (getItemFromPoint == null)
            {
                e.Handled = true;
                return false;
            }

            var item = getItemFromPoint(e.GetPosition((IInputElement)e.Source));
            if (item == null || !item.IsDirectory)
            {
                e.Handled = true;
                Mouse.OverrideCursor = null;
                return false;
            }

            string targetPath = item.Path;
            bool success = false;

            try
            {
                // Handle file drop
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    
                    // Determine if this is an internal move or external copy
                    bool isInternalMove = IsInternalDrop(files, currentTreePath);
                    
                    if (isInternalMove)
                    {
                        success = HandleInternalFileMove(files, targetPath, currentTreePath);
                        if (success)
                        {
                            OnFilesDropped(files, targetPath, DragDropEffects.Move, true);
                        }
                    }
                    else
                    {
                        success = HandleExternalFileDrop(files, targetPath);
                        if (success)
                        {
                            OnFilesDropped(files, targetPath, DragDropEffects.Copy, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error processing dropped files: {ex.Message}");
                success = false;
            }

            Mouse.OverrideCursor = null;
            e.Handled = true;
            return success;
        }

        public void HandleDragLeave(DragEventArgs e)
        {
            Mouse.OverrideCursor = null;
            e.Handled = true;
        }

        public void StartDrag(DependencyObject source, IEnumerable<string> selectedPaths)
        {
            if (source == null || selectedPaths == null)
                return;

            var pathsArray = selectedPaths.ToArray();
            if (pathsArray.Length == 0)
                return;

            try
            {
                // Create data object for drag and drop
                DataObject dataObject = new DataObject(DataFormats.FileDrop, pathsArray);

                // Add a custom format to identify internal drops
                dataObject.SetData("ExplorerPro.InternalDrop", true);

                // Start drag-drop operation
                DragDrop.DoDragDrop(source, dataObject, DragDropEffects.Copy | DragDropEffects.Move);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error starting drag operation: {ex.Message}");
            }
        }

        public bool HandleExternalFileDrop(string[] droppedFiles, string targetPath)
        {
            if (droppedFiles == null || droppedFiles.Length == 0 || string.IsNullOrEmpty(targetPath))
                return false;

            if (!Directory.Exists(targetPath))
            {
                OnErrorOccurred("Target directory does not exist");
                return false;
            }

            bool allSucceeded = true;

            foreach (string sourcePath in droppedFiles)
            {
                try
                {
                    string fileName = Path.GetFileName(sourcePath);
                    string destPath = Path.Combine(targetPath, fileName);

                    if (File.Exists(sourcePath))
                    {
                        // Handle file copy
                        if (File.Exists(destPath))
                        {
                            if (MessageBox.Show(
                                $"File '{fileName}' already exists. Overwrite?",
                                "File Exists", 
                                MessageBoxButton.YesNo, 
                                MessageBoxImage.Question) != MessageBoxResult.Yes)
                            {
                                continue;
                            }
                        }

                        File.Copy(sourcePath, destPath, true);
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        // Handle directory copy
                        if (Directory.Exists(destPath))
                        {
                            if (MessageBox.Show(
                                $"Folder '{fileName}' already exists. Merge?",
                                "Folder Exists", 
                                MessageBoxButton.YesNo, 
                                MessageBoxImage.Question) != MessageBoxResult.Yes)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            Directory.CreateDirectory(destPath);
                        }

                        // Copy directory contents recursively
                        CopyDirectory(sourcePath, destPath);
                    }
                    else
                    {
                        OnErrorOccurred($"Source path does not exist: {sourcePath}");
                        allSucceeded = false;
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Error copying '{sourcePath}': {ex.Message}");
                    allSucceeded = false;
                }
            }

            return allSucceeded;
        }

        public bool HandleInternalFileMove(string[] droppedFiles, string targetPath, string currentTreePath)
        {
            if (droppedFiles == null || droppedFiles.Length == 0 || string.IsNullOrEmpty(targetPath))
                return false;

            if (!Directory.Exists(targetPath))
            {
                OnErrorOccurred("Target directory does not exist");
                return false;
            }

            bool allSucceeded = true;
            var sourceDirectories = new HashSet<string>();

            foreach (string sourcePath in droppedFiles)
            {
                try
                {
                    string fileName = Path.GetFileName(sourcePath);
                    string destPath = Path.Combine(targetPath, fileName);

                    // Check if we're trying to move into the same directory
                    string sourceDir = Path.GetDirectoryName(sourcePath);
                    if (string.Equals(sourceDir, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue; // Skip - same directory
                    }

                    // Check if we're trying to move a directory into itself
                    if (Directory.Exists(sourcePath) && destPath.StartsWith(sourcePath + Path.DirectorySeparatorChar))
                    {
                        OnErrorOccurred($"Cannot move folder '{fileName}' into itself");
                        allSucceeded = false;
                        continue;
                    }

                    if (File.Exists(sourcePath))
                    {
                        // Handle file move
                        if (File.Exists(destPath))
                        {
                            if (MessageBox.Show(
                                $"File '{fileName}' already exists in the destination. Replace it?",
                                "File Exists", 
                                MessageBoxButton.YesNo, 
                                MessageBoxImage.Question) != MessageBoxResult.Yes)
                            {
                                continue;
                            }
                            File.Delete(destPath); // Delete existing file before move
                        }

                        File.Move(sourcePath, destPath);
                        
                        // Track source directory for refresh
                        if (!string.IsNullOrEmpty(sourceDir))
                        {
                            sourceDirectories.Add(sourceDir);
                        }
                    }
                    else if (Directory.Exists(sourcePath))
                    {
                        // Handle directory move
                        if (Directory.Exists(destPath))
                        {
                            if (MessageBox.Show(
                                $"Folder '{fileName}' already exists in the destination. Merge?",
                                "Folder Exists", 
                                MessageBoxButton.YesNo, 
                                MessageBoxImage.Question) != MessageBoxResult.Yes)
                            {
                                continue;
                            }
                            
                            // For merging, copy contents then delete source
                            CopyDirectory(sourcePath, destPath);
                            Directory.Delete(sourcePath, true);
                        }
                        else
                        {
                            // Simple move
                            Directory.Move(sourcePath, destPath);
                        }
                        
                        // Track source parent directory for refresh
                        string sourceParent = Path.GetDirectoryName(sourcePath);
                        if (!string.IsNullOrEmpty(sourceParent))
                        {
                            sourceDirectories.Add(sourceParent);
                        }
                    }
                    else
                    {
                        OnErrorOccurred($"Source path does not exist: {sourcePath}");
                        allSucceeded = false;
                    }
                }
                catch (Exception ex)
                {
                    OnErrorOccurred($"Error moving '{sourcePath}': {ex.Message}");
                    allSucceeded = false;
                }
            }

            // Notify about the move so UI can refresh source and target
            if (allSucceeded && sourceDirectories.Count > 0)
            {
                OnFilesMoved(droppedFiles, sourceDirectories.ToArray(), targetPath);
            }

            return allSucceeded;
        }

        /// <summary>
        /// Determines if a drop operation is internal (within the same tree)
        /// </summary>
        private bool IsInternalDrop(string[] files, string currentTreePath)
        {
            if (string.IsNullOrEmpty(currentTreePath) || files == null || files.Length == 0)
                return false;

            // Check if all files are within the current tree path
            return files.All(file => file.StartsWith(currentTreePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Recursively copies a directory and its contents
        /// </summary>
        private void CopyDirectory(string sourceDirName, string destDirName)
        {
            try
            {
                // Create the destination directory
                Directory.CreateDirectory(destDirName);

                // Copy files
                foreach (string file in Directory.GetFiles(sourceDirName))
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(destDirName, fileName);
                    File.Copy(file, destFile, true);
                }

                // Copy subdirectories
                foreach (string dir in Directory.GetDirectories(sourceDirName))
                {
                    string dirName = Path.GetFileName(dir);
                    string destDir = Path.Combine(destDirName, dirName);
                    CopyDirectory(dir, destDir);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Error copying directory '{sourceDirName}': {ex.Message}", ex);
            }
        }

        protected virtual void OnFilesDropped(string[] sourceFiles, string targetPath, DragDropEffects effects, bool isInternalMove)
        {
            FilesDropped?.Invoke(this, new FilesDroppedEventArgs(sourceFiles, targetPath, effects, isInternalMove));
        }

        protected virtual void OnFilesMoved(string[] sourceFiles, string[] sourceDirectories, string targetPath)
        {
            FilesMoved?.Invoke(this, new FilesMoved(sourceFiles, sourceDirectories, targetPath));
        }

        protected virtual void OnErrorOccurred(string error)
        {
            ErrorOccurred?.Invoke(this, error);
        }
    }
}