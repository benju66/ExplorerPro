using System;
using System.Collections.Concurrent;
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
    /// FIXED: Implemented thread-safe weak event pattern with proper cleanup to prevent memory leaks
    /// </summary>
    public class FileTreeLoadChildrenManager : IDisposable
    {
        private readonly IFileTreeService _fileTreeService;
        private readonly IFileTreeCache _fileTreeCache;
        
        // FIXED: Hybrid approach for better tracking and cleanup
        private readonly ConcurrentDictionary<string, WeakReference<FileTreeItem>> _trackedItems 
            = new ConcurrentDictionary<string, WeakReference<FileTreeItem>>();
        private readonly ConditionalWeakTable<FileTreeItem, WeakLoadChildrenHandler> _loadChildrenHandlers 
            = new ConditionalWeakTable<FileTreeItem, WeakLoadChildrenHandler>();
        
        private CancellationTokenSource _loadCancellationTokenSource;
        private bool _showHiddenFiles;
        private bool _disposed = false;

        // FIXED: Use ReaderWriterLockSlim for better performance
        private readonly ReaderWriterLockSlim _eventLock = new ReaderWriterLockSlim();
        
        // FIXED: Cleanup timer for dead references
        private readonly Timer _cleanupTimer;
        private readonly object _cleanupLock = new object();

        public event EventHandler<DirectoryLoadedEventArgs>? DirectoryLoaded;
        public event EventHandler<DirectoryLoadErrorEventArgs>? DirectoryLoadError;

        // FIXED: Public property to check if disposed (for WeakLoadChildrenHandler)
        public bool IsDisposed => _disposed;

        public FileTreeLoadChildrenManager(IFileTreeService fileTreeService, IFileTreeCache fileTreeCache)
        {
            _fileTreeService = fileTreeService ?? throw new ArgumentNullException(nameof(fileTreeService));
            _fileTreeCache = fileTreeCache ?? throw new ArgumentNullException(nameof(fileTreeCache));
            _loadCancellationTokenSource = new CancellationTokenSource();
            
            // FIXED: Setup cleanup timer that runs every 30 seconds
            _cleanupTimer = new Timer(CleanupDeadReferences, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
        }

        public void UpdateSettings(bool showHiddenFiles)
        {
            _showHiddenFiles = showHiddenFiles;
        }

        #region LoadChildren Event Management - FIXED for Memory Leaks

        /// <summary>
        /// FIXED: Thread-safe subscription with proper duplicate checking and weak reference tracking
        /// </summary>
        public void SubscribeToLoadChildren(FileTreeItem item)
        {
            if (item == null || _disposed) return;
            
            _eventLock.EnterWriteLock();
            try
            {
                // FIXED: Check if already subscribed to prevent duplicates
                if (_loadChildrenHandlers.TryGetValue(item, out _)) return;
                
                // Create weak handler that doesn't keep strong reference to manager
                var weakHandler = new WeakLoadChildrenHandler(this, item);
                _loadChildrenHandlers.Add(item, weakHandler);
                
                // FIXED: Track item in concurrent dictionary for cleanup
                _trackedItems.TryAdd(item.Path, new WeakReference<FileTreeItem>(item));
                
                // Subscribe using the weak handler
                item.LoadChildren += weakHandler.HandleLoadChildren;
            }
            finally
            {
                _eventLock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// FIXED: Thread-safe unsubscription with proper cleanup
        /// </summary>
        public void UnsubscribeFromLoadChildren(FileTreeItem item)
        {
            if (item == null || _disposed) return;
            
            _eventLock.EnterWriteLock();
            try
            {
                if (_loadChildrenHandlers.TryGetValue(item, out var weakHandler))
                {
                    // Unsubscribe from the event
                    item.LoadChildren -= weakHandler.HandleLoadChildren;
                    
                    // Mark handler as disposed and remove from tracking
                    weakHandler.Dispose();
                    _loadChildrenHandlers.Remove(item);
                    
                    // FIXED: Remove from tracking dictionary
                    _trackedItems.TryRemove(item.Path, out _);
                }
            }
            finally
            {
                _eventLock.ExitWriteLock();
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
        /// FIXED: Proper thread-safe cleanup of all subscriptions using tracking dictionary
        /// </summary>
        public void UnsubscribeAllLoadChildren()
        {
            _eventLock.EnterWriteLock();
            try
            {
                // FIXED: Use tracking dictionary to safely enumerate items
                var itemsToUnsubscribe = new List<FileTreeItem>();
                
                foreach (var kvp in _trackedItems.ToList()) // ToList for safe enumeration
                {
                    if (kvp.Value.TryGetTarget(out var item))
                    {
                        itemsToUnsubscribe.Add(item);
                    }
                    else
                    {
                        // Remove dead reference
                        _trackedItems.TryRemove(kvp.Key, out _);
                    }
                }
                
                foreach (var item in itemsToUnsubscribe)
                {
                    if (_loadChildrenHandlers.TryGetValue(item, out var weakHandler))
                    {
                        item.LoadChildren -= weakHandler.HandleLoadChildren;
                        weakHandler.Dispose();
                        _loadChildrenHandlers.Remove(item);
                    }
                }
                
                // Clear tracking dictionary
                _trackedItems.Clear();
            }
            finally
            {
                _eventLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// FIXED: Periodic cleanup of dead weak references
        /// </summary>
        private void CleanupDeadReferences(object? state)
        {
            if (_disposed) return;
            
            lock (_cleanupLock)
            {
                var deadPaths = new List<string>();
                
                foreach (var kvp in _trackedItems.ToList())
                {
                    if (!kvp.Value.TryGetTarget(out _))
                    {
                        deadPaths.Add(kvp.Key);
                    }
                }
                
                foreach (var path in deadPaths)
                {
                    _trackedItems.TryRemove(path, out _);
                }
                
                if (deadPaths.Count > 0)
                {
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Cleaned up {deadPaths.Count} dead weak references");
                }
            }
        }

        #endregion

        #region Directory Loading

        /// <summary>
        /// FIXED: Added defensive checks and ensured single subscription per item
        /// OPTIMIZED: Added ConfigureAwait(false) for better async performance
        /// </summary>
        public async Task LoadDirectoryContentsAsync(FileTreeItem parentItem)
        {
            // FIXED: Defensive check for disposed state at start
            if (_disposed || parentItem == null || !parentItem.IsDirectory)
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] LoadDirectoryContentsAsync: Invalid parent item or disposed");
                return;
            }
                
            string path = parentItem.Path;
            int childLevel = parentItem.Level + 1;
            var cancellationToken = _loadCancellationTokenSource?.Token ?? CancellationToken.None;

            try
            {
                // FIXED: Check disposed state before any async operation
                if (_disposed || cancellationToken.IsCancellationRequested) return;
                
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
                
                // FIXED: Check disposed again before continuing
                if (_disposed || cancellationToken.IsCancellationRequested) return;
                
                // Clear children and add loading indicator on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    parentItem.ClearChildren();
                    parentItem.Children.Add(new FileTreeItem { Name = "Loading...", Level = childLevel });
                });

                // OPTIMIZED: Use ConfigureAwait(false) to avoid deadlocks and improve performance
                var children = await _fileTreeService.LoadDirectoryAsync(path, _showHiddenFiles, childLevel).ConfigureAwait(false);

                // FIXED: Check disposed and cancelled before updating UI
                if (_disposed || cancellationToken.IsCancellationRequested) return;

                // Update children collection on UI thread
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    parentItem.ClearChildren();

                    foreach (var child in children)
                    {
                        if (_disposed || cancellationToken.IsCancellationRequested) return;
                        
                        // Set parent reference for efficient parent/child operations
                        child.Parent = parentItem;
                        
                        parentItem.Children.Add(child);
                        _fileTreeCache.SetItem(child.Path, child);
                        
                        if (child.IsDirectory)
                        {
                            // FIXED: Ensure subscription only happens once per item
                            _eventLock.EnterReadLock();
                            try
                            {
                                if (!_loadChildrenHandlers.TryGetValue(child, out _))
                                {
                                    _eventLock.ExitReadLock();
                                    SubscribeToLoadChildren(child);
                                    _eventLock.EnterReadLock();
                                }
                            }
                            finally
                            {
                                _eventLock.ExitReadLock();
                            }
                        }
                    }
                    
                    parentItem.HasChildren = parentItem.Children.Count > 0;
                });
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loaded {parentItem.Children.Count} items for directory: {path}");
                
                // Notify successful load if not disposed
                if (!_disposed)
                {
                    DirectoryLoaded?.Invoke(this, new DirectoryLoadedEventArgs(parentItem, children));
                }
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
            // FIXED: Defensive check for disposed state
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
            
            // FIXED: Check disposed before async operations
            if (_disposed || cancellationToken.IsCancellationRequested)
                return;
            
            // OPTIMIZED: Use ConfigureAwait(false) for async HasChildren check
            directoryItem.HasChildren = await _fileTreeService.DirectoryHasAccessibleChildrenAsync(
                directoryItem.Path, _showHiddenFiles, cancellationToken).ConfigureAwait(false);
            
            if (_disposed || cancellationToken.IsCancellationRequested)
                return;
            
            if (wasExpanded && directoryItem.HasChildren)
            {
                // OPTIMIZED: Use ConfigureAwait(false) for async loading
                await LoadDirectoryContentsAsync(directoryItem).ConfigureAwait(false);
                
                if (!(_disposed || cancellationToken.IsCancellationRequested))
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

                // FIXED: Dispose cleanup timer
                _cleanupTimer?.Dispose();

                // Cancel any active operations
                _loadCancellationTokenSource?.Cancel();
                _loadCancellationTokenSource?.Dispose();
                _loadCancellationTokenSource = null;

                // Unsubscribe from all LoadChildren events
                UnsubscribeAllLoadChildren();

                // FIXED: Dispose reader-writer lock
                _eventLock?.Dispose();

                // Clear event handlers
                DirectoryLoaded = null;
                DirectoryLoadError = null;
            }
        }

        #endregion
    }

    #region Weak Event Handler Implementation

    /// <summary>
    /// FIXED: Thread-safe weak event handler that prevents memory leaks
    /// </summary>
    internal class WeakLoadChildrenHandler : IDisposable
    {
        private readonly WeakReference<FileTreeLoadChildrenManager> _managerRef;
        private readonly WeakReference<FileTreeItem> _itemRef;
        private volatile bool _disposed = false;
        private readonly object _handlerLock = new object();

        public WeakLoadChildrenHandler(FileTreeLoadChildrenManager manager, FileTreeItem item)
        {
            _managerRef = new WeakReference<FileTreeLoadChildrenManager>(manager);
            _itemRef = new WeakReference<FileTreeItem>(item);
        }

        public void HandleLoadChildren(object? sender, EventArgs e)
        {
            // FIXED: Thread-safe handling with proper locking
            lock (_handlerLock)
            {
                if (_disposed) return;
                
                // Try to get manager and item from weak references
                if (_managerRef.TryGetTarget(out var manager) && 
                    _itemRef.TryGetTarget(out var item))
                {
                    // FIXED: Ensure manager is not disposed before calling
                    if (!manager.IsDisposed)
                    {
                        // Call the actual load method
                        _ = manager.LoadDirectoryContentsAsync(item);
                    }
                    else
                    {
                        // Manager is disposed, auto-dispose this handler
                        Dispose();
                    }
                }
                else
                {
                    // References are dead, auto-dispose this handler
                    Dispose();
                }
            }
        }

        public void Dispose()
        {
            lock (_handlerLock)
            {
                _disposed = true;
                // Weak references will be cleaned up by GC automatically
            }
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