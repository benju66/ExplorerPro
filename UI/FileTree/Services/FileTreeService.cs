// UI/FileTree/Services/FileTreeService.cs - Fixed Threading Issues
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using ExplorerPro.Models;
using ExplorerPro.Utilities;
using ExplorerPro.FileOperations;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Service for file tree operations with proper memory management and async support
    /// Fixed version with proper UI thread handling for WPF objects
    /// </summary>
    public class FileTreeService : IFileTreeService, IDisposable
    {
        private readonly MetadataManager _metadataManager;
        private readonly FileIconProvider _iconProvider;
        private bool _disposed;

        public event EventHandler<string>? ErrorOccurred;

        public FileTreeService(MetadataManager metadataManager, FileIconProvider iconProvider)
        {
            _metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
            _iconProvider = iconProvider ?? throw new ArgumentNullException(nameof(iconProvider));
        }

        public async Task<IEnumerable<FileTreeItem>> LoadDirectoryAsync(string? directoryPath, bool showHiddenFiles = false, int level = 0)
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
                        var dirItem = await CreateFileTreeItemAsync(dir, level, showHiddenFiles).ConfigureAwait(false);
                        
                        // Apply batch metadata - FIXED: styling applied on UI thread
                        if (metadataBatch.TryGetValue(dir, out var metadata))
                        {
                            await ApplyBatchMetadataStylingAsync(dirItem, metadata).ConfigureAwait(false);
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

                        var fileItem = await CreateFileTreeItemInternalAsync(file, level).ConfigureAwait(false);
                        
                        // Apply batch metadata - FIXED: styling applied on UI thread
                        if (metadataBatch.TryGetValue(file, out var metadata))
                        {
                            await ApplyBatchMetadataStylingAsync(fileItem, metadata).ConfigureAwait(false);
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

        public FileTreeItem CreateFileTreeItem(string? path, int level = 0)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTreeService));

            if (string.IsNullOrEmpty(path))
            {
                OnErrorOccurred("Invalid path: path is null or empty");
                return null;
            }

            try
            {
                var item = FileTreeItem.FromPath(path);
                item.Level = level;

                // FIXED: Apply styling synchronously on UI thread
                ApplyMetadataStylingOnUIThread(item);

                // FIXED: Set icon on UI thread
                SetIconOnUIThread(item, path);

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

        public async Task<FileTreeItem> CreateFileTreeItemAsync(string? path, int level = 0, bool showHiddenFiles = false, CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTreeService));

            if (string.IsNullOrEmpty(path))
            {
                OnErrorOccurred("Invalid path: path is null or empty");
                return null;
            }

            var item = await CreateFileTreeItemInternalAsync(path, level, showHiddenFiles, cancellationToken).ConfigureAwait(false);
            
            // FIXED: Apply styling on UI thread
            await ApplyMetadataStylingAsync(item).ConfigureAwait(false);
            
            return item;
        }

        private async Task<FileTreeItem> CreateFileTreeItemInternalAsync(string path, int level = 0, bool showHiddenFiles = false, CancellationToken cancellationToken = default)
        {
            try
            {
                var item = await Task.Run(() => FileTreeItem.FromPath(path), cancellationToken).ConfigureAwait(false);
                item.Level = level;

                // FIXED: Set icon on UI thread
                await SetIconAsync(item, path).ConfigureAwait(false);

                // For directories, use async check for better performance
                if (item.IsDirectory)
                {
                    item.HasChildren = await DirectoryHasAccessibleChildrenAsync(path, showHiddenFiles, cancellationToken).ConfigureAwait(false);
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

        // FIXED: Synchronous version that ensures UI thread execution
        public void ApplyMetadataStyling(FileTreeItem item)
        {
            if (_disposed || item == null)
                return;

            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                ApplyMetadataStylingCore(item);
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() => ApplyMetadataStylingCore(item));
            }
        }

        // FIXED: Async version for use in async methods
        private async Task ApplyMetadataStylingAsync(FileTreeItem item)
        {
            if (_disposed || item == null)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() => ApplyMetadataStylingCore(item));
        }

        // FIXED: Core styling logic that must run on UI thread
        private void ApplyMetadataStylingCore(FileTreeItem item)
        {
            try
            {
                // Apply text color if set in metadata
                string colorHex = _metadataManager.GetItemColor(item.Path);
                if (!string.IsNullOrEmpty(colorHex))
                {
                    try
                    {
                        // FIXED: SolidColorBrush is created on UI thread
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

        // FIXED: Synchronous version that ensures UI thread execution
        private void ApplyMetadataStylingOnUIThread(FileTreeItem item)
        {
            if (_disposed || item == null)
                return;

            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                ApplyMetadataStylingCore(item);
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() => ApplyMetadataStylingCore(item));
            }
        }

        /// <summary>
        /// FIXED: Applies metadata styling from batch metadata info on UI thread
        /// </summary>
        private async Task ApplyBatchMetadataStylingAsync(FileTreeItem item, MetadataInfo metadata)
        {
            if (item == null || metadata == null)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() => ApplyBatchMetadataStylingCore(item, metadata));
        }

        // FIXED: Core batch styling logic that must run on UI thread
        private void ApplyBatchMetadataStylingCore(FileTreeItem item, MetadataInfo metadata)
        {
            try
            {
                // Apply text color if set
                if (!string.IsNullOrEmpty(metadata.Color))
                {
                    try
                    {
                        // FIXED: SolidColorBrush is created on UI thread
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

        // FIXED: Set icon on UI thread synchronously
        private void SetIconOnUIThread(FileTreeItem item, string path)
        {
            if (_disposed || item == null)
                return;

            if (Application.Current?.Dispatcher.CheckAccess() == true)
            {
                item.Icon = _iconProvider.GetIcon(path);
            }
            else
            {
                Application.Current?.Dispatcher.Invoke(() => 
                {
                    item.Icon = _iconProvider.GetIcon(path);
                });
            }
        }

        // FIXED: Set icon on UI thread asynchronously
        private async Task SetIconAsync(FileTreeItem item, string path)
        {
            if (_disposed || item == null)
                return;

            await Application.Current.Dispatcher.InvokeAsync(() => 
            {
                item.Icon = _iconProvider.GetIcon(path);
            });
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

        /// <summary>
        /// Loads multiple directories in batch for improved performance
        /// </summary>
        public async Task<IEnumerable<FileTreeItem>> LoadDirectoryBatchAsync(
            IEnumerable<string> directoryPaths,
            bool showHiddenFiles = false,
            CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(FileTreeService));

            var batchOperation = new BatchFileOperation();
            var results = new System.Collections.Concurrent.ConcurrentBag<FileTreeItem>();
            
            foreach (var path in directoryPaths)
            {
                batchOperation.AddOperation(new FileOperation
                {
                    Description = $"Loading {path}",
                    ExecuteAsync = async (ct) =>
                    {
                        try
                        {
                            var items = await LoadDirectoryAsync(path, showHiddenFiles, 0).ConfigureAwait(false);
                            foreach (var item in items)
                            {
                                results.Add(item);
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load directory {path}: {ex.Message}");
                            // Continue with other directories
                        }
                    }
                });
            }
            
            var progress = new Progress<BatchOperationProgress>(p =>
            {
                System.Diagnostics.Debug.WriteLine($"[BATCH] Loaded {p.CompletedOperations}/{p.TotalOperations} directories");
            });
            
            await batchOperation.ExecuteAsync(progress, cancellationToken);
            batchOperation.Dispose();
            
            return results.OrderBy(item => item.IsDirectory ? 0 : 1).ThenBy(item => item.Name);
        }

        /// <summary>
        /// Loads a large directory with paging support for improved performance
        /// </summary>
        public async Task<IEnumerable<FileTreeItem>> LoadLargeDirectoryAsync(
            string directoryPath, 
            bool showHiddenFiles = false, 
            int pageSize = 500,
            int? maxItems = null,
            CancellationToken cancellationToken = default)
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
                var items = new List<FileTreeItem>();
                var (directories, files) = await Task.Run(() =>
                {
                    IEnumerable<string> dirs = Directory.GetDirectories(directoryPath).OrderBy(d => Path.GetFileName(d));
                    IEnumerable<string> filesList = Directory.GetFiles(directoryPath).OrderBy(f => Path.GetFileName(f));
                    
                    if (maxItems.HasValue)
                    {
                        var totalItems = dirs.Count() + filesList.Count();
                        if (totalItems > maxItems.Value)
                        {
                            dirs = dirs.Take(maxItems.Value / 2);
                            filesList = filesList.Take(maxItems.Value - dirs.Count());
                        }
                    }
                    
                    return (dirs, filesList);
                }, cancellationToken);

                var allPaths = directories.Concat(files).ToList();
                
                // Process items in batches
                for (int i = 0; i < allPaths.Count; i += pageSize)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    var batch = allPaths.Skip(i).Take(pageSize);
                    var batchItems = await ProcessBatchAsync(batch, showHiddenFiles, cancellationToken);
                    items.AddRange(batchItems);
                    
                    // Allow UI updates between batches
                    await Task.Delay(1, cancellationToken);
                }

                return items;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Failed to load large directory: {ex.Message}");
                return Enumerable.Empty<FileTreeItem>();
            }
        }

        private async Task<IEnumerable<FileTreeItem>> ProcessBatchAsync(
            IEnumerable<string> paths, 
            bool showHiddenFiles, 
            CancellationToken cancellationToken)
        {
            var items = new List<FileTreeItem>();
            var metadataBatch = _metadataManager.GetBatchMetadata(paths);

            foreach (var path in paths)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    if (!showHiddenFiles && IsHidden(path))
                        continue;

                    var item = await CreateFileTreeItemInternalAsync(path, 0, showHiddenFiles, cancellationToken);
                    
                    if (metadataBatch.TryGetValue(path, out var metadata))
                    {
                        await ApplyBatchMetadataStylingAsync(item, metadata);
                    }
                    
                    items.Add(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Error processing {path}: {ex.Message}");
                    continue;
                }
            }

            return items;
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