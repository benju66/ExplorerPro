// UI/FileTree/Services/FileTreeDragDropService.cs (UPDATED with Outlook support and COM exception handling)
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service for handling file tree drag and drop operations with Outlook support
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

            // Get the item under the cursor
            var item = getItemFromPoint(e.GetPosition((IInputElement)e.Source));

            if (item != null && item.IsDirectory)
            {
                // Determine if this is a copy or move operation
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.Move; // Default to move for file operations
                }
                else if (IsOutlookData(e.Data))
                {
                    e.Effects = DragDropEffects.Copy; // Outlook items are always copied
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
                // Handle standard file drop
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
                // Handle Outlook data
                else if (IsOutlookData(e.Data))
                {
                    success = HandleOutlookDrop(e.Data as DataObject, targetPath);
                    if (success)
                    {
                        OnFilesDropped(new string[0], targetPath, DragDropEffects.Copy, false);
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

        public bool HandleOutlookDrop(DataObject dataObject, string targetPath)
        {
            if (dataObject == null || !Directory.Exists(targetPath))
                return false;

            try
            {
                System.Diagnostics.Debug.WriteLine("Starting Outlook drop handling...");

                // Try different approaches with COM exception handling
                bool success = false;

                // Method 1: Try to handle as email message
                try
                {
                    if (SafeCheckDataPresent(dataObject, CFSTR_OUTLOOKMESSAGE))
                    {
                        System.Diagnostics.Debug.WriteLine("Trying to handle as Outlook message...");
                        success = HandleOutlookMessage(dataObject, targetPath);
                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine("Successfully handled as Outlook message");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling as message: {ex.Message}");
                }

                // Method 2: Try to handle as Outlook item
                try
                {
                    if (SafeCheckDataPresent(dataObject, CFSTR_OUTLOOK_ITEM))
                    {
                        System.Diagnostics.Debug.WriteLine("Trying to handle as Outlook item...");
                        success = HandleOutlookItem(dataObject, targetPath);
                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine("Successfully handled as Outlook item");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling as item: {ex.Message}");
                }

                // Method 3: Try to handle as file attachments
                try
                {
                    if (SafeCheckDataPresent(dataObject, CFSTR_FILEDESCRIPTOR))
                    {
                        System.Diagnostics.Debug.WriteLine("Trying to handle as file attachments...");
                        success = HandleOutlookAttachments(dataObject, targetPath);
                        if (success)
                        {
                            System.Diagnostics.Debug.WriteLine("Successfully handled as file attachments");
                            return true;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error handling as attachments: {ex.Message}");
                }

                // Method 4: Fallback - save as generic Outlook data
                try
                {
                    success = HandleGenericOutlookData(dataObject, targetPath);
                    if (success)
                    {
                        System.Diagnostics.Debug.WriteLine("Successfully handled as generic Outlook data");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in fallback handling: {ex.Message}");
                }

                System.Diagnostics.Debug.WriteLine("All Outlook handling methods failed");
                return false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error handling Outlook drop: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Safely checks if data is present without throwing COM exceptions
        /// </summary>
        private bool SafeCheckDataPresent(DataObject dataObject, string format)
        {
            try
            {
                return dataObject.GetDataPresent(format);
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"COM exception checking format {format}: 0x{comEx.HResult:X}");
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception checking format {format}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Safely gets data without throwing COM exceptions
        /// </summary>
        private object SafeGetData(DataObject dataObject, string format)
        {
            try
            {
                return dataObject.GetData(format);
            }
            catch (System.Runtime.InteropServices.COMException comEx)
            {
                System.Diagnostics.Debug.WriteLine($"COM exception getting data {format}: 0x{comEx.HResult:X}");
                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Exception getting data {format}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Handles generic Outlook data as a fallback
        /// </summary>
        private bool HandleGenericOutlookData(DataObject dataObject, string targetPath)
        {
            try
            {
                // Try to get any available data format
                string[] commonFormats = { 
                    "Text", 
                    "UnicodeText", 
                    "Html", 
                    "Rich Text Format",
                    "FileContents"
                };

                foreach (string format in commonFormats)
                {
                    try
                    {
                        if (SafeCheckDataPresent(dataObject, format))
                        {
                            var data = SafeGetData(dataObject, format);
                            if (data != null)
                            {
                                string fileName = $"OutlookData_{DateTime.Now:yyyyMMdd_HHmmss}";
                                string extension = ".txt";
                                
                                if (format.ToLower().Contains("html"))
                                    extension = ".html";
                                else if (format.ToLower().Contains("rtf"))
                                    extension = ".rtf";

                                string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName + extension));

                                if (data is string text)
                                {
                                    File.WriteAllText(filePath, text);
                                    return true;
                                }
                                else if (data is byte[] bytes)
                                {
                                    File.WriteAllBytes(filePath, bytes);
                                    return true;
                                }
                                else if (data is MemoryStream stream)
                                {
                                    using (var fileStream = File.Create(filePath))
                                    {
                                        stream.Seek(0, SeekOrigin.Begin);
                                        stream.CopyTo(fileStream);
                                    }
                                    return true;
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error with format {format}: {ex.Message}");
                        continue;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in generic handling: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Detects if the data object contains Outlook data with COM exception handling
        /// </summary>
        private bool IsOutlookData(IDataObject data)
        {
            try
            {
                // Try to detect Outlook data formats safely
                string[] outlookFormats = {
                    CFSTR_FILEDESCRIPTOR,
                    CFSTR_OUTLOOKMESSAGE, 
                    CFSTR_OUTLOOK_ITEM,
                    "RenPrivateItem",
                    "FileGroupDescriptor", // Alternative name
                    "FileGroupDescriptorW" // Wide version
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
                    catch (System.Runtime.InteropServices.COMException comEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"COM error checking format {format}: 0x{comEx.HResult:X} - {comEx.Message}");
                        // Continue checking other formats
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error checking format {format}: {ex.Message}");
                        // Continue checking other formats
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
        /// Handles Outlook email messages with improved error handling
        /// </summary>
        private bool HandleOutlookMessage(DataObject dataObject, string targetPath)
        {
            try
            {
                // Get the message data safely
                var messageData = SafeGetData(dataObject, CFSTR_OUTLOOKMESSAGE);
                
                if (messageData is MemoryStream messageStream)
                {
                    // Generate a filename for the email
                    string fileName = $"Email_{DateTime.Now:yyyyMMdd_HHmmss}.msg";
                    string filePath = Path.Combine(targetPath, fileName);

                    // Ensure unique filename
                    filePath = GetUniqueFilePath(filePath);

                    // Save the email
                    using (var fileStream = File.Create(filePath))
                    {
                        messageStream.Seek(0, SeekOrigin.Begin);
                        messageStream.CopyTo(fileStream);
                    }

                    return true;
                }
                else if (messageData is byte[] messageBytes)
                {
                    // Handle byte array format
                    string fileName = $"Email_{DateTime.Now:yyyyMMdd_HHmmss}.msg";
                    string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));

                    File.WriteAllBytes(filePath, messageBytes);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving Outlook message: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles Outlook items (alternative format) with improved error handling
        /// </summary>
        private bool HandleOutlookItem(DataObject dataObject, string targetPath)
        {
            try
            {
                var itemData = SafeGetData(dataObject, CFSTR_OUTLOOK_ITEM);
                
                if (itemData is MemoryStream itemStream)
                {
                    string fileName = $"OutlookItem_{DateTime.Now:yyyyMMdd_HHmmss}.msg";
                    string filePath = GetUniqueFilePath(Path.Combine(targetPath, fileName));

                    using (var fileStream = File.Create(filePath))
                    {
                        itemStream.Seek(0, SeekOrigin.Begin);
                        itemStream.CopyTo(fileStream);
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error saving Outlook item: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Handles Outlook attachments using the OutlookDataExtractor
        /// </summary>
        private bool HandleOutlookAttachments(DataObject dataObject, string targetPath)
        {
            try
            {
                return OutlookDataExtractor.ExtractOutlookFiles(dataObject, targetPath);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Error extracting Outlook attachments: {ex.Message}");
                return false;
            }
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

        /// <summary>
        /// Gets a unique file path by appending a number if the file already exists
        /// </summary>
        private string GetUniqueFilePath(string originalPath)
        {
            if (!File.Exists(originalPath))
                return originalPath;

            string directory = Path.GetDirectoryName(originalPath);
            string nameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
            string extension = Path.GetExtension(originalPath);

            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{nameWithoutExt}_{counter}{extension}");
                counter++;
            }
            while (File.Exists(newPath));

            return newPath;
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