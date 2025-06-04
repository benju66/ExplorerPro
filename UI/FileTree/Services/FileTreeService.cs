// UI/FileTree/Services/FileTreeService.cs - Enhanced with batch operations and optimized HasChildren
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using ExplorerPro.Models;
using ExplorerPro.Utilities;
using ExplorerPro.FileOperations;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service for file tree operations with proper memory management and async support
    /// Enhanced with batch operations and optimized HasChildren check
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
                
                // Collect all paths for batch metadata retrieval
                var allPaths = new List<string>();
                allPaths.AddRange(directories);
                allPaths.AddRange(files);
                
                // Get metadata for all items in one batch operation
                var metadataBatch = _metadataManager.GetBatchMetadata(allPaths);

                // Add directories
                foreach (var dir in directories)
                {
                    try
                    {
                        if (!showHiddenFiles && IsHidden(dir))
                            continue;

                        // Use async version for better performance on network drives
                        var dirItem = await CreateFileTreeItemAsync(dir, level, showHiddenFiles);
                        
                        // Apply batch metadata
                        if (metadataBatch.TryGetValue(dir, out var metadata))
                        {
                            ApplyBatchMetadataStyling(dirItem, metadata);
                        }
                        
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
                        
                        // Apply batch metadata
                        if (metadataBatch.TryGetValue(file, out var metadata))
                        {
                            ApplyBatchMetadataStyling(fileItem, metadata);
                        }
                        
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

                // For directories, use synchronous check (deprecated)
                if (item.IsDirectory)
                {
                    #pragma warning disable CS0618 // Using obsolete method for backward compatibility
                    item.HasChildren = DirectoryHasAccessibleChildren(path);
                    #pragma warning restore CS0618
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

        public async Task<FileTreeItem> CreateFileTreeItemAsync(string path, int level = 0, bool showHiddenFiles = false, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTreeService));

            try
            {
                var item = await Task.Run(() => FileTreeItem.FromPath(path), cancellationToken);
                item.Level = level;

                // Apply styling from metadata
                ApplyMetadataStyling(item);

                // Set icon
                item.Icon = _iconProvider.GetIcon(path);

                // For directories, use async check for better performance
                if (item.IsDirectory)
                {
                    item.HasChildren = await DirectoryHasAccessibleChildrenAsync(path, showHiddenFiles, cancellationToken);
                }

                return item;
            }
            catch (OperationCanceledException)
            {
                throw; // Re-throw cancellation
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

        [Obsolete("Use DirectoryHasAccessibleChildrenAsync for better performance on network drives")]
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

        /// <summary>
        /// Optimized async method to check if directory has children
        /// Uses early exit and minimal file system operations
        /// </summary>
        public async Task<bool> DirectoryHasAccessibleChildrenAsync(string directoryPath, bool showHiddenFiles = false, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return false;

            try
            {
                // Run the enumeration on a background thread to avoid blocking UI
                return await Task.Run(() =>
                {
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var directoryInfo = new DirectoryInfo(directoryPath);
                        
                        // Use EnumerateFileSystemInfos for a single pass through the directory
                        // This is more efficient than separate calls to EnumerateDirectories and EnumerateFiles
                        var enumerator = directoryInfo.EnumerateFileSystemInfos().GetEnumerator();
                        
                        try
                        {
                            while (enumerator.MoveNext())
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                var entry = enumerator.Current;
                                
                                // Skip hidden entries if needed
                                if (!showHiddenFiles && (entry.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                                    continue;
                                
                                // Skip system entries that might cause issues
                                if ((entry.Attributes & FileAttributes.System) == FileAttributes.System)
                                    continue;
                                
                                // Found at least one visible, non-system entry
                                return true;
                            }
                        }
                        finally
                        {
                            enumerator?.Dispose();
                        }
                        
                        return false;
                    }
                    catch (UnauthorizedAccessException)
                    {
                        return false; // Can't access, so effectively no children
                    }
                    catch (DirectoryNotFoundException)
                    {
                        return false; // Directory doesn't exist
                    }
                    catch (IOException)
                    {
                        return false; // I/O error, assume no children
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ERROR] Error checking directory children: {ex.Message}");
                        return false; // Error accessing, assume no children
                    }
                }, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                return false; // Cancelled, return false
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Async directory check failed: {ex.Message}");
                return false;
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

        /// <summary>
        /// Applies metadata styling from batch metadata info
        /// </summary>
        private void ApplyBatchMetadataStyling(FileTreeItem item, MetadataInfo metadata)
        {
            if (item == null || metadata == null)
                return;

            try
            {
                // Apply text color if set
                if (!string.IsNullOrEmpty(metadata.Color))
                {
                    try
                    {
                        item.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString(metadata.Color));
                    }
                    catch
                    {
                        // Ignore color conversion errors
                    }
                }

                // Apply bold if set
                if (metadata.IsBold)
                {
                    item.FontWeight = System.Windows.FontWeights.Bold;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to apply batch metadata styling: {ex.Message}");
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