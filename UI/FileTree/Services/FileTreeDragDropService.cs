// UI/FileTree/Services/FileTreeDragDropService.cs (UPDATED with async Outlook support)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service for handling file tree drag and drop operations with enhanced Outlook support
    /// </summary>
    public class FileTreeDragDropService : IFileTreeDragDropService
    {
        private const double DragThreshold = 10.0;
        
        // Outlook data format constants
        private const string CFSTR_FILEDESCRIPTOR = "FileGroupDescriptor";
        private const string CFSTR_FILECONTENTS = "FileContents";
        private const string CFSTR_OUTLOOKMESSAGE = "RenPrivateMessages";
        private const string CFSTR_OUTLOOK_ITEM = "RenPrivateItem";

        public event EventHandler<FilesDroppedEventArgs> FilesDropped;
        public event EventHandler<FilesMoved> FilesMoved;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<OutlookExtractionCompletedEventArgs> OutlookExtractionCompleted;

        private CancellationTokenSource _cancellationTokenSource;

        public void HandleDragEnter(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) || IsOutlookData(e.Data))
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

            var item = getItemFromPoint(e.GetPosition((IInputElement)e.Source));

            if (item != null && item.IsDirectory)
            {
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.Move;
                }
                else if (IsOutlookData(e.Data))
                {
                    e.Effects = DragDropEffects.Copy;
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
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
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
                else if (IsOutlookData(e.Data))
                {
                    // Handle Outlook data asynchronously
                    _ = HandleOutlookDropAsync(e.Data as DataObject, targetPath);
                    success = true; // Return true immediately, actual result will come via event
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
                DataObject dataObject = new DataObject(DataFormats.FileDrop, pathsArray);
                dataObject.SetData("ExplorerPro.InternalDrop", true);
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

                    string sourceDir = Path.GetDirectoryName(sourcePath);
                    if (string.Equals(sourceDir, targetPath, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (Directory.Exists(sourcePath) && destPath.StartsWith(sourcePath + Path.DirectorySeparatorChar))
                    {
                        OnErrorOccurred($"Cannot move folder '{fileName}' into itself");
                        allSucceeded = false;
                        continue;
                    }

                    if (File.Exists(sourcePath))
                    {
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
                            File.Delete(destPath);
                        }

                        File.Move(sourcePath, destPath);
                        
                        if (!string.IsNullOrEmpty(sourceDir))
                        {
                            sourceDirectories.Add(sourceDir);
                        }
                    }
                    else if (Directory.Exists(sourcePath))
                    {
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
                            
                            CopyDirectory(sourcePath, destPath);
                            Directory.Delete(sourcePath, true);
                        }
                        else
                        {
                            Directory.Move(sourcePath, destPath);
                        }
                        
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

            if (allSucceeded && sourceDirectories.Count > 0)
            {
                OnFilesMoved(droppedFiles, sourceDirectories.ToArray(), targetPath);
            }

            return allSucceeded;
        }

        public bool HandleOutlookDrop(DataObject dataObject, string targetPath)
        {
            // Synchronous wrapper for backwards compatibility
            var task = HandleOutlookDropAsync(dataObject, targetPath);
            task.Wait();
            return task.Result;
        }

        /// <summary>
        /// Handles Outlook drops asynchronously with progress reporting
        /// </summary>
        public async Task<bool> HandleOutlookDropAsync(DataObject dataObject, string targetPath)
        {
            if (dataObject == null || !Directory.Exists(targetPath))
                return false;

            // Cancel any existing extraction
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var progress = new Progress<string>(message => 
                {
                    // Report progress - this will be handled by the UI
                    System.Diagnostics.Debug.WriteLine($"[OUTLOOK] {message}");
                });

                var result = await OutlookDataExtractor.ExtractOutlookFilesAsync(
                    dataObject, targetPath, progress, _cancellationTokenSource.Token);

                // Notify completion
                OnOutlookExtractionCompleted(result, targetPath);

                return result.Success;
            }
            catch (OperationCanceledException)
            {
                OnErrorOccurred("Outlook extraction was cancelled");
                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error handling Outlook drop: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Cancels the current Outlook extraction operation
        /// </summary>
        public void CancelOutlookExtraction()
        {
            _cancellationTokenSource?.Cancel();
        }

        /// <summary>
        /// Detects if the data object contains Outlook data
        /// </summary>
        private bool IsOutlookData(IDataObject data)
        {
            try
            {
                string[] outlookFormats = {
                    CFSTR_FILEDESCRIPTOR,
                    CFSTR_OUTLOOKMESSAGE, 
                    CFSTR_OUTLOOK_ITEM,
                    "RenPrivateItem",
                    "FileGroupDescriptorW"
                };

                foreach (string format in outlookFormats)
                {
                    try
                    {
                        if (data.GetDataPresent(format))
                        {
                            System.Diagnostics.Debug.WriteLine($"Detected Outlook format: {format}");
                            return true;
                        }
                    }
                    catch (COMException comEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"COM error checking format {format}: 0x{comEx.HResult:X}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking format {format}: {ex.Message}");
                        continue;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in IsOutlookData: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Determines if a drop operation is internal
        /// </summary>
        private bool IsInternalDrop(string[] files, string currentTreePath)
        {
            if (string.IsNullOrEmpty(currentTreePath) || files == null || files.Length == 0)
                return false;

            return files.All(file => file.StartsWith(currentTreePath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Recursively copies a directory and its contents
        /// </summary>
        private void CopyDirectory(string sourceDirName, string destDirName)
        {
            try
            {
                Directory.CreateDirectory(destDirName);

                foreach (string file in Directory.GetFiles(sourceDirName))
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(destDirName, fileName);
                    File.Copy(file, destFile, true);
                }

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

        protected virtual void OnOutlookExtractionCompleted(OutlookDataExtractor.ExtractionResult result, string targetPath)
        {
            OutlookExtractionCompleted?.Invoke(this, new OutlookExtractionCompletedEventArgs(result, targetPath));
        }
    }

    /// <summary>
    /// Event arguments for Outlook extraction completion
    /// </summary>
    public class OutlookExtractionCompletedEventArgs : EventArgs
    {
        public OutlookDataExtractor.ExtractionResult Result { get; }
        public string TargetPath { get; }

        public OutlookExtractionCompletedEventArgs(OutlookDataExtractor.ExtractionResult result, string targetPath)
        {
            Result = result;
            TargetPath = targetPath;
        }
    }
}