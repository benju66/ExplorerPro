// UI/FileTree/Services/FileTreeService.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerPro.Models;
using ExplorerPro.Utilities;
using ExplorerPro.FileOperations;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service for file tree operations with proper memory management
    /// </summary>
    public class FileTreeService : IFileTreeService, IDisposable
    {
        private readonly MetadataManager _metadataManager;
        private readonly FileIconProvider _iconProvider;
        private bool _disposed;

        public event EventHandler<string> ErrorOccurred;

        public FileTreeService(MetadataManager metadataManager, FileIconProvider iconProvider)
        {
            _metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
            _iconProvider = iconProvider ?? throw new ArgumentNullException(nameof(iconProvider));
        }

        public async Task<IEnumerable<FileTreeItem>> LoadDirectoryAsync(string directoryPath, bool showHiddenFiles = false, int level = 0)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTreeService));

            if (string.IsNullOrEmpty(directoryPath) || !Directory.Exists(directoryPath))
            {
                OnErrorOccurred($"Invalid directory path: {directoryPath}");
                return Enumerable.Empty<FileTreeItem>();
            }

            try
            {
                var (directories, files) = await Task.Run(() =>
                {
                    try
                    {
                        var dirs = Directory.GetDirectories(directoryPath).OrderBy(d => Path.GetFileName(d));
                        var filesList = Directory.GetFiles(directoryPath).OrderBy(f => Path.GetFileName(f));
                        return (dirs, filesList);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Error listing directory contents: {ex.Message}", ex);
                    }
                });

                List<FileTreeItem> items = new List<FileTreeItem>();

                // Add directories
                foreach (var dir in directories)
                {
                    try
                    {
                        if (!showHiddenFiles && IsHidden(dir))
                            continue;

                        var dirItem = CreateFileTreeItem(dir, level);
                        items.Add(dirItem);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Skipping inaccessible directory: {dir}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Error processing directory {dir}: {ex.Message}");
                        continue;
                    }
                }

                // Add files
                foreach (var file in files)
                {
                    try
                    {
                        if (!showHiddenFiles && IsHidden(file))
                            continue;

                        var fileItem = CreateFileTreeItem(file, level);
                        items.Add(fileItem);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        System.Diagnostics.Debug.WriteLine($"[DEBUG] Skipping inaccessible file: {file}");
                        continue;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Error processing file {file}: {ex.Message}");
                        continue;
                    }
                }

                return items;
            }
            catch (UnauthorizedAccessException)
            {
                OnErrorOccurred($"Access denied to directory: {directoryPath}");
                var errorItem = new FileTreeItem
                {
                    Name = "Access Denied",
                    Path = Path.Combine(directoryPath, "Access Denied"),
                    Level = level,
                    Type = "Error",
                    HasChildren = false
                };
                return new[] { errorItem };
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to load directory contents: {ex.Message}");
                var errorItem = new FileTreeItem
                {
                    Name = $"Error: {ex.Message}",
                    Level = level,
                    Type = "Error",
                    HasChildren = false
                };
                return new[] { errorItem };
            }
        }

        public FileTreeItem CreateFileTreeItem(string path, int level = 0)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTreeService));

            try
            {
                var item = FileTreeItem.FromPath(path);
                item.Level = level;

                // Apply styling from metadata
                ApplyMetadataStyling(item);

                // Set icon
                item.Icon = _iconProvider.GetIcon(path);

                // For directories, check if they have children to set HasChildren property
                if (item.IsDirectory)
                {
                    item.HasChildren = DirectoryHasAccessibleChildren(path);
                }

                return item;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to create file tree item: {ex.Message}");

                // Return a basic item if creation fails
                var fallbackItem = new FileTreeItem
                {
                    Name = Path.GetFileName(path),
                    Path = path,
                    Level = level,
                    IsDirectory = Directory.Exists(path),
                    Type = Directory.Exists(path) ? "Folder" : "File",
                    HasChildren = false
                };

                return fallbackItem;
            }
        }

        public bool DirectoryHasAccessibleChildren(string directoryPath, bool showHiddenFiles = false)
        {
            if (_disposed)
                return false;

            try
            {
                // Quick check - just see if we can enumerate anything
                var hasDirectories = Directory.EnumerateDirectories(directoryPath)
                    .Where(d => showHiddenFiles || !IsHidden(d))
                    .Any();

                if (hasDirectories) return true;

                var hasFiles = Directory.EnumerateFiles(directoryPath)
                    .Where(f => showHiddenFiles || !IsHidden(f))
                    .Any();

                return hasFiles;
            }
            catch (UnauthorizedAccessException)
            {
                return false; // Can't access, so effectively no children
            }
            catch (Exception)
            {
                return false; // Error accessing, assume no children
            }
        }

        public bool IsHidden(string path)
        {
            if (_disposed)
                return false;

            try
            {
                var attributes = File.GetAttributes(path);
                return (attributes & FileAttributes.Hidden) == FileAttributes.Hidden;
            }
            catch
            {
                return false;
            }
        }

        public void ApplyMetadataStyling(FileTreeItem item)
        {
            if (_disposed || item == null)
                return;

            try
            {
                // Apply text color if set in metadata
                string colorHex = _metadataManager.GetItemColor(item.Path);
                if (!string.IsNullOrEmpty(colorHex))
                {
                    try
                    {
                        item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                    }
                    catch
                    {
                        // Ignore color conversion errors
                    }
                }

                // Apply bold if set in metadata
                bool isBold = _metadataManager.GetItemBold(item.Path);
                if (isBold)
                {
                    item.FontWeight = System.Windows.FontWeights.Bold;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to apply metadata styling: {ex.Message}");
                // Continue without styling
            }
        }

        public FileTreeItem FindItemByPath(IEnumerable<FileTreeItem> items, string path)
        {
            if (_disposed || string.IsNullOrEmpty(path) || items == null)
                return null;

            foreach (var item in items)
            {
                if (item.Path == path)
                    return item;

                if (item.IsDirectory && item.Children.Count > 0)
                {
                    var foundItem = FindItemByPathRecursive(item, path);
                    if (foundItem != null)
                        return foundItem;
                }
            }

            return null;
        }

        public FileTreeItem FindItemByPathRecursive(FileTreeItem parent, string path)
        {
            if (_disposed || parent == null || string.IsNullOrEmpty(path))
                return null;

            if (parent.Path == path)
                return parent;

            foreach (var child in parent.Children)
            {
                if (child.Path == path)
                    return child;

                if (child.IsDirectory && child.Children.Count > 0)
                {
                    var foundItem = FindItemByPathRecursive(child, path);
                    if (foundItem != null)
                        return foundItem;
                }
            }

            return null;
        }

        protected virtual void OnErrorOccurred(string error)
        {
            if (!_disposed)
            {
                ErrorOccurred?.Invoke(this, error);
            }
        }

        #region IDisposable Implementation

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
                    // Clear event handlers to prevent memory leaks
                    ErrorOccurred = null;

                    // Dispose icon provider if it implements IDisposable
                    (_iconProvider as IDisposable)?.Dispose();

                    // Note: We don't dispose MetadataManager as it's likely shared
                    // and managed at application level
                }

                _disposed = true;
            }
        }

        ~FileTreeService()
        {
            Dispose(false);
        }

        #endregion
    }
}