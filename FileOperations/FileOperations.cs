using System;
using System.IO;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.FileOperations
{
    /// <summary>
    /// Provides file system operations for creating, moving, copying, renaming, and deleting files and folders.
    /// </summary>
    public class FileOperations : IFileOperations
    {
        private readonly ILogger<FileOperations>? _logger;

        /// <summary>
        /// Initializes a new instance of the FileOperations class.
        /// </summary>
        /// <param name="logger">Optional logger for operation tracking.</param>
        public FileOperations(ILogger<FileOperations>? logger = null)
        {
            _logger = logger;
        }

        /// <summary>
        /// Creates a new file in the specified directory.
        /// </summary>
        /// <param name="parentDirectory">The directory in which to create the file.</param>
        /// <param name="fileName">The desired file name. Defaults to "New File.txt".</param>
        /// <returns>The path to the newly created file, or null if an error occurred.</returns>
        public string? CreateNewFile(string parentDirectory, string fileName = "New File.txt")
        {
            string newFilePath = Path.Combine(parentDirectory, fileName);
            try
            {
                if (!Directory.Exists(parentDirectory))
                {
                    throw new DirectoryNotFoundException($"Parent directory '{parentDirectory}' does not exist.");
                }

                // Ensure unique file name
                int counter = 1;
                while (File.Exists(newFilePath))
                {
                    string baseName = Path.GetFileNameWithoutExtension(fileName);
                    string extension = Path.GetExtension(fileName);
                    newFilePath = Path.Combine(
                        parentDirectory,
                        $"{baseName} ({counter}){extension}"
                    );
                    counter++;
                }

                // Create the file
                using (FileStream fs = File.Create(newFilePath))
                {
                    // Empty file
                }

                _logger?.LogInformation($"File created: {newFilePath}");
                return newFilePath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creating file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates a new folder in the specified directory.
        /// </summary>
        /// <param name="parentDirectory">The directory in which to create the folder.</param>
        /// <param name="folderName">The desired folder name. Defaults to "New Folder".</param>
        /// <returns>The path to the newly created folder, or null if an error occurred.</returns>
        public string? CreateNewFolder(string parentDirectory, string folderName = "New Folder")
        {
            try
            {
                if (!Directory.Exists(parentDirectory))
                {
                    throw new DirectoryNotFoundException($"Parent directory '{parentDirectory}' does not exist.");
                }

                // Ensure unique folder name
                int counter = 1;
                string newFolderPath = Path.Combine(parentDirectory, folderName);
                while (Directory.Exists(newFolderPath))
                {
                    newFolderPath = Path.Combine(
                        parentDirectory,
                        $"{folderName} ({counter})"
                    );
                    counter++;
                }

                Directory.CreateDirectory(newFolderPath);
                _logger?.LogInformation($"Folder created: {newFolderPath}");
                return newFolderPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error creating folder: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Renames a file or folder.
        /// </summary>
        /// <param name="itemPath">The path to the item to rename.</param>
        /// <param name="newName">The new name for the item.</param>
        /// <returns>The new path if the rename succeeded, or null if it failed.</returns>
        public string? RenameItem(string itemPath, string newName)
        {
            try
            {
                if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
                {
                    _logger?.LogError($"Error: Item '{itemPath}' does not exist.");
                    return null;
                }

                string? parentDirectory = Path.GetDirectoryName(itemPath);
                if (parentDirectory == null)
                {
                    _logger?.LogError($"Error: Unable to get parent directory for '{itemPath}'");
                    return null;
                }
                
                string newPath = Path.Combine(parentDirectory, newName);

                // Prevent overwriting an existing file
                if (File.Exists(newPath) || Directory.Exists(newPath))
                {
                    _logger?.LogError($"Error: An item with the name '{newName}' already exists.");
                    return null;
                }

                // Prevent invalid file names on Windows
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    if (newName.Contains(c))
                    {
                        _logger?.LogError($"Error: Invalid characters in filename '{newName}'.");
                        return null;
                    }
                }

                // Perform the rename
                if (File.Exists(itemPath))
                {
                    File.Move(itemPath, newPath);
                }
                else if (Directory.Exists(itemPath))
                {
                    Directory.Move(itemPath, newPath);
                }

                _logger?.LogInformation($"Renamed '{itemPath}' to '{newPath}'");
                return newPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error renaming item: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Deletes a file or folder.
        /// </summary>
        /// <param name="itemPath">The path of the file or folder to delete.</param>
        /// <returns>True if the deletion succeeded, False otherwise.</returns>
        public bool DeleteItem(string itemPath)
        {
            try
            {
                if (!File.Exists(itemPath) && !Directory.Exists(itemPath))
                {
                    throw new FileNotFoundException($"Item '{itemPath}' does not exist.");
                }

                if (File.Exists(itemPath))
                {
                    File.Delete(itemPath);
                }
                else if (Directory.Exists(itemPath))
                {
                    Directory.Delete(itemPath, true); // true means recursive delete
                }

                _logger?.LogInformation($"Deleted: {itemPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error deleting item: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Copies a file or folder to a new location, ensuring unique names.
        /// </summary>
        /// <param name="sourcePath">The path to the file or folder to copy.</param>
        /// <param name="destinationPath">The directory to which the item will be copied.</param>
        /// <returns>The path to the copied item, or null if an error occurred.</returns>
        public string? CopyItem(string sourcePath, string destinationPath)
        {
            try
            {
                if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"Source '{sourcePath}' does not exist.");
                }

                string baseName = Path.GetFileNameWithoutExtension(sourcePath);
                string extension = Path.GetExtension(sourcePath);
                string newPath = Path.Combine(destinationPath, Path.GetFileName(sourcePath));
                int counter = 1;

                // Generate a unique name if there's a conflict
                while (File.Exists(newPath) || Directory.Exists(newPath))
                {
                    if (Directory.Exists(sourcePath))
                    {
                        newPath = Path.Combine(destinationPath, $"{baseName} ({counter})");
                    }
                    else
                    {
                        newPath = Path.Combine(destinationPath, $"{baseName} ({counter}){extension}");
                    }
                    counter++;
                }

                if (File.Exists(sourcePath))
                {
                    File.Copy(sourcePath, newPath);
                    
                    // Preserve file attributes and timestamps
                    File.SetCreationTime(newPath, File.GetCreationTime(sourcePath));
                    File.SetLastWriteTime(newPath, File.GetLastWriteTime(sourcePath));
                    File.SetAttributes(newPath, File.GetAttributes(sourcePath));
                }
                else if (Directory.Exists(sourcePath))
                {
                    CopyDirectory(sourcePath, newPath);
                }

                _logger?.LogInformation($"Copied '{sourcePath}' to '{newPath}'");
                return newPath;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error copying item from '{sourcePath}' to '{destinationPath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Moves a file or folder to a new location.
        /// </summary>
        /// <param name="sourcePath">The path to the file or folder to move.</param>
        /// <param name="destinationPath">The directory where the item will be moved.</param>
        /// <returns>True if the move succeeded, False otherwise.</returns>
        public bool MoveItem(string sourcePath, string destinationPath)
        {
            try
            {
                if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
                {
                    throw new FileNotFoundException($"Source '{sourcePath}' does not exist.");
                }

                // Check if destinationPath is a directory
                // If it is, we need to combine with the source filename
                string finalDestination;
                if (Directory.Exists(destinationPath))
                {
                    finalDestination = Path.Combine(destinationPath, Path.GetFileName(sourcePath));
                }
                else
                {
                    finalDestination = destinationPath;
                }

                // If the destination already exists, we need to handle it
                if (File.Exists(finalDestination) || Directory.Exists(finalDestination))
                {
                    _logger?.LogError($"Destination already exists: {finalDestination}");
                    return false;
                }

                // Move the file or directory
                if (File.Exists(sourcePath))
                {
                    File.Move(sourcePath, finalDestination);
                }
                else if (Directory.Exists(sourcePath))
                {
                    Directory.Move(sourcePath, finalDestination);
                }

                _logger?.LogInformation($"Moved '{sourcePath}' to '{finalDestination}'");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error moving item: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Recursively copies a directory and its contents.
        /// </summary>
        /// <param name="sourceDir">Source directory path.</param>
        /// <param name="destDir">Destination directory path.</param>
        private void CopyDirectory(string sourceDir, string destDir)
        {
            // Create the destination directory
            Directory.CreateDirectory(destDir);

            // Get the files in the source directory and copy them to the new location
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destDir, fileName);
                File.Copy(file, destFile);
                
                // Preserve file attributes and timestamps
                File.SetCreationTime(destFile, File.GetCreationTime(file));
                File.SetLastWriteTime(destFile, File.GetLastWriteTime(file));
                File.SetAttributes(destFile, File.GetAttributes(file));
            }

            // Copy subdirectories and their contents
            foreach (string directory in Directory.GetDirectories(sourceDir))
            {
                string dirName = Path.GetFileName(directory);
                string destSubDir = Path.Combine(destDir, dirName);
                CopyDirectory(directory, destSubDir);
            }
        }
        
        #region MainWindow Interface Methods
        
        /// <summary>
        /// Deletes a file or folder.
        /// </summary>
        /// <param name="path">The path of the file or folder to delete.</param>
        /// <returns>True if the deletion succeeded, False otherwise.</returns>
        public bool Delete(string path)
        {
            return DeleteItem(path);
        }
        
        /// <summary>
        /// Renames a file or folder.
        /// </summary>
        /// <param name="path">The path to the item to rename.</param>
        /// <param name="newName">The new name for the item.</param>
        /// <returns>The new path if the rename succeeded, or null if it failed.</returns>
        public string? Rename(string path, string newName)
        {
            return RenameItem(path, newName);
        }
        
        #endregion
        
        /// <summary>
        /// Static helper method to copy items.
        /// </summary>
        public static string? StaticCopyItem(string sourcePath, string destinationPath)
        {
            var fileOps = new FileOperations();
            return fileOps.CopyItem(sourcePath, destinationPath);
        }
    }
}