using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Services;

namespace ExplorerPro.UI.FileTree.Managers
{
    /// <summary>
    /// Manages LoadChildren event subscriptions and directory loading operations
    /// FIXED: Implemented weak event pattern to prevent memory leaks
    /// </summary>
    public class FileTreeLoadChildrenManager : IDisposable
    {
        private readonly IFileTreeService _fileTreeService;
        private readonly IFileTreeCache _fileTreeCache;
        
        // FIXED: Use WeakEventManager pattern to prevent memory leaks
        private readonly ConditionalWeakTable<FileTreeItem, WeakLoadChildrenHandler> _loadChildrenHandlers 
            = new ConditionalWeakTable<FileTreeItem, WeakLoadChildrenHandler>();
        
        private CancellationTokenSource _loadCancellationTokenSource;
        private bool _showHiddenFiles;
        private bool _disposed = false;

        // FIXED: Thread synchronization for event management
        private readonly object _eventLock = new object();

        public event EventHandler<DirectoryLoadedEventArgs>? DirectoryLoaded;
        public event EventHandler<DirectoryLoadErrorEventArgs>? DirectoryLoadError;

        public FileTreeLoadChildrenManager(IFileTreeService fileTreeService, IFileTreeCache fileTreeCache)
        {
            _fileTreeService = fileTreeService ?? throw new ArgumentNullException(nameof(fileTreeService));
            _fileTreeCache = fileTreeCache ?? throw new ArgumentNullException(nameof(fileTreeCache));
            _loadCancellationTokenSource = new CancellationTokenSource();
        }

        public void UpdateSettings(bool showHiddenFiles)
        {
            _showHiddenFiles = showHiddenFiles;
        }

        #region LoadChildren Event Management - FIXED for Memory Leaks

        /// <summary>
        /// FIXED: Subscribes to LoadChildren event using weak reference pattern to prevent memory leaks
        /// </summary>
        public void SubscribeToLoadChildren(FileTreeItem item)
        {
            if (item == null || _disposed) return;
            
            lock (_eventLock)
            {
                // Check if already subscribed
                if (_loadChildrenHandlers.TryGetValue(item, out _)) return;
                
                // Create weak handler that doesn't keep strong reference to manager
                var weakHandler = new WeakLoadChildrenHandler(this, item);
                _loadChildrenHandlers.Add(item, weakHandler);
                
                // Subscribe using the weak handler
                item.LoadChildren += weakHandler.HandleLoadChildren;
            }
        }
        
        /// <summary>
        /// FIXED: Proper unsubscription with weak reference cleanup
        /// </summary>
        public void UnsubscribeFromLoadChildren(FileTreeItem item)
        {
            if (item == null || _disposed) return;
            
            lock (_eventLock)
            {
                if (_loadChildrenHandlers.TryGetValue(item, out var weakHandler))
                {
                    // Unsubscribe from the event
                    item.LoadChildren -= weakHandler.HandleLoadChildren;
                    
                    // Mark handler as disposed and remove from tracking
                    weakHandler.Dispose();
                    _loadChildrenHandlers.Remove(item);
                }
            }
        }
        
        /// <summary>
        /// Unsubscribes from LoadChildren events for all children of an item recursively
        /// </summary>
        public void UnsubscribeChildrenLoadEvents(FileTreeItem parentItem)
        {
            if (parentItem == null) return;

            foreach (var child in parentItem.Children.ToList()) // ToList to avoid collection modification
            {
                if (child.IsDirectory)
                {
                    UnsubscribeFromLoadChildren(child);
                    UnsubscribeChildrenLoadEvents(child); // Recursive
                }
            }
        }
        
        /// <summary>
        /// FIXED: Thread-safe unsubscription from all LoadChildren events
        /// </summary>
        public void UnsubscribeAllLoadChildren()
        {
            lock (_eventLock)
            {
                // Get current items (ConditionalWeakTable doesn't support enumeration directly)
                // We'll iterate through items that may still be alive
                var itemsToUnsubscribe = new List<FileTreeItem>();
                
                // Since we can't enumerate ConditionalWeakTable directly,
                // we'll rely on disposal to clean up remaining handlers
                foreach (var kvp in _loadChildrenHandlers)
                {
                    itemsToUnsubscribe.Add(kvp.Key);
                }
                
                foreach (var item in itemsToUnsubscribe)
                {
                    UnsubscribeFromLoadChildren(item);
                }
            }
        }

        #endregion

        #region Directory Loading

        /// <summary>
        /// Loads directory contents asynchronously
        /// OPTIMIZED: Added ConfigureAwait(false) for better async performance
        /// </summary>
        public async Task LoadDirectoryContentsAsync(FileTreeItem parentItem)
        {
            if (_disposed || parentItem == null || !parentItem.IsDirectory)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] LoadDirectoryContentsAsync: Invalid parent item");
                return;
            }
                
            string path = parentItem.Path;
            int childLevel = parentItem.Level + 1;
            var cancellationToken = _loadCancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loading directory contents for: {path}");
                
                // Check if already loaded (must be on UI thread)
                bool alreadyLoaded = false;
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    alreadyLoaded = parentItem.Children.Count > 0 && !parentItem.HasDummyChild();
                });
                
                if (alreadyLoaded)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Directory already loaded: {path}");
                    return;
                }
                
                // Clear children and add loading indicator on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    parentItem.ClearChildren();
                    parentItem.Children.Add(new FileTreeItem { Name = "Loading...", Level = childLevel });
                });

                // OPTIMIZED: Use ConfigureAwait(false) to avoid deadlocks and improve performance
                var children = await _fileTreeService.LoadDirectoryAsync(path, _showHiddenFiles, childLevel).ConfigureAwait(false);

                if (_disposed || cancellationToken.IsCancellationRequested) return;

                // Update children collection on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    parentItem.ClearChildren();

                    foreach (var child in children)
                    {
                        if (cancellationToken.IsCancellationRequested) return;
                        
                        // Set parent reference for efficient parent/child operations
                        child.Parent = parentItem;
                        
                        parentItem.Children.Add(child);
                        _fileTreeCache.SetItem(child.Path, child);
                        
                        if (child.IsDirectory)
                        {
                            // Subscribe to LoadChildren event with proper tracking
                            SubscribeToLoadChildren(child);
                        }
                    }
                    
                    parentItem.HasChildren = parentItem.Children.Count > 0;
                });
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loaded {parentItem.Children.Count} items for directory: {path}");
                
                // Notify successful load
                DirectoryLoaded?.Invoke(this, new DirectoryLoadedEventArgs(parentItem, children));
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine($"[INFO] Loading cancelled for: {path}");
                if (!_disposed)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        parentItem.ClearChildren();
                        parentItem.HasChildren = false;
                    });
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load directory contents: {ex.Message}");
                
                if (!_disposed)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        parentItem.ClearChildren();
                        parentItem.Children.Add(new FileTreeItem { 
                            Name = $"Error: {ex.Message}", 
                            Level = childLevel,
                            Type = "Error"
                        });
                        parentItem.HasChildren = true;
                    });
                    
                    // Notify load error
                    DirectoryLoadError?.Invoke(this, new DirectoryLoadErrorEventArgs(parentItem, ex));
                }
            }
        }

        /// <summary>
        /// Refreshes directory contents asynchronously
        /// OPTIMIZED: Added ConfigureAwait(false) for better async performance
        /// </summary>
        public async Task RefreshDirectoryAsync(FileTreeItem directoryItem)
        {
            if (_disposed || directoryItem == null || !directoryItem.IsDirectory)
                return;

            bool wasExpanded = directoryItem.IsExpanded;
            
            // Unsubscribe LoadChildren events for all children before clearing (on UI thread)
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                UnsubscribeChildrenLoadEvents(directoryItem);
                directoryItem.ClearChildren();
            });
            
            var cancellationToken = _loadCancellationTokenSource?.Token ?? CancellationToken.None;
            
            // OPTIMIZED: Use ConfigureAwait(false) for async HasChildren check
            directoryItem.HasChildren = await _fileTreeService.DirectoryHasAccessibleChildrenAsync(
                directoryItem.Path, _showHiddenFiles, cancellationToken).ConfigureAwait(false);
            
            if (cancellationToken.IsCancellationRequested)
                return;
            
            if (wasExpanded && directoryItem.HasChildren)
            {
                // OPTIMIZED: Use ConfigureAwait(false) for async loading
                await LoadDirectoryContentsAsync(directoryItem).ConfigureAwait(false);
                
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        directoryItem.IsExpanded = true;
                    });
                }
            }
        }

        #endregion

        #region Cancellation Management

        /// <summary>
        /// Cancels all active loading operations
        /// </summary>
        public void CancelActiveOperations()
        {
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource = new CancellationTokenSource();
        }

        /// <summary>
        /// Updates the cancellation token source
        /// </summary>
        public void UpdateCancellationTokenSource(CancellationTokenSource tokenSource)
        {
            _loadCancellationTokenSource?.Cancel();
            _loadCancellationTokenSource = tokenSource;
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            if (!_disposed)
            {
                _disposed = true;

                // Cancel any active operations
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource?.Dispose();
                _loadCancellationTokenSource = null;

                // Unsubscribe from all LoadChildren events
                UnsubscribeAllLoadChildren();

                // Clear event handlers
                DirectoryLoaded = null;
                DirectoryLoadError = null;
            }
        }

        #endregion
    }

    #region Weak Event Handler Implementation

    /// <summary>
    /// FIXED: Weak event handler that prevents memory leaks by not holding strong reference to manager
    /// </summary>
    internal class WeakLoadChildrenHandler : IDisposable
    {
        private readonly WeakReference<FileTreeLoadChildrenManager> _managerRef;
        private readonly WeakReference<FileTreeItem> _itemRef;
        private bool _disposed = false;

        public WeakLoadChildrenHandler(FileTreeLoadChildrenManager manager, FileTreeItem item)
        {
            _managerRef = new WeakReference<FileTreeLoadChildrenManager>(manager);
            _itemRef = new WeakReference<FileTreeItem>(item);
        }

        public void HandleLoadChildren(object? sender, EventArgs e)
        {
            if (_disposed) return;
            
            // Try to get manager and item from weak references
            if (_managerRef.TryGetTarget(out var manager) && 
                _itemRef.TryGetTarget(out var item))
            {
                // Call the actual load method
                _ = manager.LoadDirectoryContentsAsync(item);
            }
            // If references are dead, we can't do anything - this prevents memory leaks
        }

        public void Dispose()
        {
            _disposed = true;
            // Weak references will be cleaned up by GC automatically
        }
    }

    #endregion

    #region Event Args

    public class DirectoryLoadedEventArgs : EventArgs
    {
        public FileTreeItem ParentItem { get; }
        public IEnumerable<FileTreeItem> Children { get; }

        public DirectoryLoadedEventArgs(FileTreeItem parentItem, IEnumerable<FileTreeItem> children)
        {
            ParentItem = parentItem;
            Children = children;
        }
    }

    public class DirectoryLoadErrorEventArgs : EventArgs
    {
        public FileTreeItem ParentItem { get; }
        public Exception Exception { get; }

        public DirectoryLoadErrorEventArgs(FileTreeItem parentItem, Exception exception)
        {
            ParentItem = parentItem;
            Exception = exception;
        }
    }

    #endregion
} 