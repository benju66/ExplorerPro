using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.Core.TabManagement;

namespace ExplorerPro.Core.Threading
{
    /// <summary>
    /// Thread-safe tab operations manager that ensures all tab operations are properly marshalled to the UI thread.
    /// Provides enterprise-level threading safety for tab management operations.
    /// </summary>
    public class ThreadSafeTabOperations : IDisposable
    {
        #region Private Fields
        
        private readonly ILogger<ThreadSafeTabOperations> _logger;
        private readonly ITabManagerService _tabManagerService;
        private readonly Dispatcher _uiDispatcher;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly object _syncLock = new object();
        private bool _disposed;
        
        // Operation tracking
        private readonly Dictionary<string, Task> _pendingOperations;
        private int _operationCounter;
        
        #endregion

        #region Constructor
        
        public ThreadSafeTabOperations(
            ITabManagerService tabManagerService,
            ILogger<ThreadSafeTabOperations> logger = null,
            Dispatcher dispatcher = null)
        {
            _tabManagerService = tabManagerService ?? throw new ArgumentNullException(nameof(tabManagerService));
            _logger = logger;
            _uiDispatcher = dispatcher ?? Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
            _operationSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
            _pendingOperations = new Dictionary<string, Task>();
            
            _logger?.LogDebug("ThreadSafeTabOperations initialized");
        }
        
        #endregion

        #region Thread-Safe Tab Creation
        
        /// <summary>
        /// Creates a new tab in a thread-safe manner
        /// </summary>
        public async Task<TabModel> CreateTabSafeAsync(string title, string path = null, TabCreationOptions options = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            var operationId = GetNextOperationId("CreateTab");
            
            try
            {
                return await ExecuteThreadSafeOperationAsync(async () =>
                {
                    _logger?.LogDebug("Creating tab '{Title}' on thread {ThreadId}", title, Thread.CurrentThread.ManagedThreadId);
                    
                    // Service operation (thread-safe)
                    var tabModel = await _tabManagerService.CreateTabAsync(title, path, options);
                    
                    _logger?.LogDebug("Tab '{Title}' created successfully", title);
                    return tabModel;
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create tab '{Title}'", title);
                throw;
            }
        }

        /// <summary>
        /// Creates multiple tabs in a thread-safe batch operation
        /// </summary>
        public async Task<IReadOnlyList<TabModel>> CreateTabsBatchSafeAsync(
            IEnumerable<(string title, string path, TabCreationOptions options)> tabRequests, 
            CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            var operationId = GetNextOperationId("CreateTabsBatch");
            var results = new List<TabModel>();
            
            try
            {
                return await ExecuteThreadSafeOperationAsync(async () =>
                {
                    foreach (var (title, path, options) in tabRequests)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        var tabModel = await _tabManagerService.CreateTabAsync(title, path, options);
                        results.Add(tabModel);
                    }
                    
                    _logger?.LogDebug("Created {Count} tabs in batch operation", results.Count);
                    return results.AsReadOnly();
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create batch tabs");
                throw;
            }
        }
        
        #endregion

        #region Thread-Safe Tab Closing
        
        /// <summary>
        /// Closes a tab in a thread-safe manner
        /// </summary>
        public async Task<bool> CloseTabSafeAsync(TabModel tab, bool force = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (tab == null)
                return false;
                
            var operationId = GetNextOperationId("CloseTab");
            
            try
            {
                return await ExecuteThreadSafeOperationAsync(async () =>
                {
                    _logger?.LogDebug("Closing tab '{Title}' on thread {ThreadId}", tab.Title, Thread.CurrentThread.ManagedThreadId);
                    
                    var result = await _tabManagerService.CloseTabAsync(tab, force);
                    
                    _logger?.LogDebug("Tab '{Title}' closed with result: {Result}", tab.Title, result);
                    return result;
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to close tab '{Title}'", tab.Title);
                throw;
            }
        }

        /// <summary>
        /// Closes multiple tabs in a thread-safe batch operation
        /// </summary>
        public async Task<int> CloseTabsBatchSafeAsync(IEnumerable<TabModel> tabs, bool force = false, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            var operationId = GetNextOperationId("CloseTabsBatch");
            var closedCount = 0;
            
            try
            {
                return await ExecuteThreadSafeOperationAsync(async () =>
                {
                    foreach (var tab in tabs)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        
                        if (await _tabManagerService.CloseTabAsync(tab, force))
                            closedCount++;
                    }
                    
                    _logger?.LogDebug("Closed {Count} tabs in batch operation", closedCount);
                    return closedCount;
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to close batch tabs");
                throw;
            }
        }
        
        #endregion

        #region Thread-Safe Tab Manipulation
        
        /// <summary>
        /// Moves a tab to a new position in a thread-safe manner
        /// </summary>
        public async Task MoveTabSafeAsync(TabModel tab, int newIndex, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (tab == null)
                return;
                
            var operationId = GetNextOperationId("MoveTab");
            
            try
            {
                await ExecuteThreadSafeOperationAsync(async () =>
                {
                    _logger?.LogDebug("Moving tab '{Title}' to index {NewIndex}", tab.Title, newIndex);
                    
                    await _tabManagerService.MoveTabAsync(tab, newIndex);
                    
                    _logger?.LogDebug("Tab '{Title}' moved successfully", tab.Title);
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to move tab '{Title}' to index {NewIndex}", tab.Title, newIndex);
                throw;
            }
        }

        /// <summary>
        /// Activates a tab in a thread-safe manner
        /// </summary>
        public async Task ActivateTabSafeAsync(TabModel tab, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (tab == null)
                return;
                
            var operationId = GetNextOperationId("ActivateTab");
            
            try
            {
                await ExecuteThreadSafeOperationAsync(async () =>
                {
                    _logger?.LogDebug("Activating tab '{Title}'", tab.Title);
                    
                    await _tabManagerService.ActivateTabAsync(tab);
                    
                    _logger?.LogDebug("Tab '{Title}' activated successfully", tab.Title);
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to activate tab '{Title}'", tab.Title);
                throw;
            }
        }

        /// <summary>
        /// Duplicates a tab in a thread-safe manner
        /// </summary>
        public async Task<TabModel> DuplicateTabSafeAsync(TabModel tab, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            
            if (tab == null)
                return null;
                
            var operationId = GetNextOperationId("DuplicateTab");
            
            try
            {
                return await ExecuteThreadSafeOperationAsync(async () =>
                {
                    _logger?.LogDebug("Duplicating tab '{Title}'", tab.Title);
                    
                    var duplicatedTab = await _tabManagerService.DuplicateTabAsync(tab);
                    
                    _logger?.LogDebug("Tab '{Title}' duplicated successfully", tab.Title);
                    return duplicatedTab;
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to duplicate tab '{Title}'", tab.Title);
                throw;
            }
        }
        
        #endregion

        #region Thread-Safe UI Collection Operations
        
        /// <summary>
        /// Safely updates a UI collection with tab items
        /// </summary>
        public async Task UpdateUICollectionSafeAsync<T>(
            ObservableCollection<T> uiCollection,
            IEnumerable<T> newItems,
            CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            
            if (uiCollection == null)
                return;
                
            var operationId = GetNextOperationId("UpdateUICollection");
            
            try
            {
                await ExecuteUIThreadOperationAsync(() =>
                {
                    _logger?.LogTrace("Updating UI collection with {Count} items", newItems?.Count() ?? 0);
                    
                    uiCollection.Clear();
                    
                    if (newItems != null)
                    {
                        foreach (var item in newItems)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            uiCollection.Add(item);
                        }
                    }
                    
                    _logger?.LogTrace("UI collection updated successfully");
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to update UI collection");
                throw;
            }
        }

        /// <summary>
        /// Safely adds an item to a UI collection
        /// </summary>
        public async Task AddToUICollectionSafeAsync<T>(
            ObservableCollection<T> uiCollection,
            T item,
            int? index = null,
            CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            
            if (uiCollection == null || item == null)
                return;
                
            var operationId = GetNextOperationId("AddToUICollection");
            
            try
            {
                await ExecuteUIThreadOperationAsync(() =>
                {
                    if (index.HasValue && index.Value >= 0 && index.Value <= uiCollection.Count)
                    {
                        uiCollection.Insert(index.Value, item);
                        _logger?.LogTrace("Added item to UI collection at index {Index}", index.Value);
                    }
                    else
                    {
                        uiCollection.Add(item);
                        _logger?.LogTrace("Added item to UI collection at end");
                    }
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to add item to UI collection");
                throw;
            }
        }

        /// <summary>
        /// Safely removes an item from a UI collection
        /// </summary>
        public async Task RemoveFromUICollectionSafeAsync<T>(
            ObservableCollection<T> uiCollection,
            T item,
            CancellationToken cancellationToken = default) where T : class
        {
            ThrowIfDisposed();
            
            if (uiCollection == null || item == null)
                return;
                
            var operationId = GetNextOperationId("RemoveFromUICollection");
            
            try
            {
                await ExecuteUIThreadOperationAsync(() =>
                {
                    var removed = uiCollection.Remove(item);
                    _logger?.LogTrace("Removed item from UI collection: {Success}", removed);
                    
                }, operationId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to remove item from UI collection");
                throw;
            }
        }
        
        #endregion

        #region Core Threading Infrastructure
        
        /// <summary>
        /// Executes an operation with thread safety and operation tracking
        /// </summary>
        private async Task<T> ExecuteThreadSafeOperationAsync<T>(
            Func<Task<T>> operation,
            string operationId,
            CancellationToken cancellationToken = default)
        {
            using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token);
                
            await _operationSemaphore.WaitAsync(combinedToken.Token);
            
            try
            {
                var task = operation();
                
                lock (_syncLock)
                {
                    _pendingOperations[operationId] = task;
                }
                
                var result = await task;
                
                return result;
            }
            finally
            {
                lock (_syncLock)
                {
                    _pendingOperations.Remove(operationId);
                }
                
                _operationSemaphore.Release();
            }
        }

        /// <summary>
        /// Executes an operation with thread safety and operation tracking (void return)
        /// </summary>
        private async Task ExecuteThreadSafeOperationAsync(
            Func<Task> operation,
            string operationId,
            CancellationToken cancellationToken = default)
        {
            await ExecuteThreadSafeOperationAsync(async () =>
            {
                await operation();
                return true; // Dummy return value
            }, operationId, cancellationToken);
        }

        /// <summary>
        /// Executes an operation on the UI thread with proper error handling
        /// </summary>
        private async Task ExecuteUIThreadOperationAsync(
            Action operation,
            string operationId,
            CancellationToken cancellationToken = default)
        {
            using var combinedToken = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, _cancellationTokenSource.Token);
                
            if (_uiDispatcher.CheckAccess())
            {
                // Already on UI thread
                combinedToken.Token.ThrowIfCancellationRequested();
                operation();
            }
            else
            {
                // Marshal to UI thread
                var tcs = new TaskCompletionSource<bool>();
                
                var dispatcherOperation = _uiDispatcher.BeginInvoke(new Action(() =>
                {
                    try
                    {
                        combinedToken.Token.ThrowIfCancellationRequested();
                        operation();
                        tcs.SetResult(true);
                    }
                    catch (Exception ex)
                    {
                        tcs.SetException(ex);
                    }
                }), DispatcherPriority.Normal);
                
                // Track the operation
                lock (_syncLock)
                {
                    _pendingOperations[operationId] = tcs.Task;
                }
                
                try
                {
                    await tcs.Task;
                }
                finally
                {
                    lock (_syncLock)
                    {
                        _pendingOperations.Remove(operationId);
                    }
                }
            }
        }

        /// <summary>
        /// Generates a unique operation ID for tracking
        /// </summary>
        private string GetNextOperationId(string operationName)
        {
            var counter = Interlocked.Increment(ref _operationCounter);
            return $"{operationName}_{counter}_{DateTime.UtcNow.Ticks}";
        }

        /// <summary>
        /// Gets the count of currently pending operations
        /// </summary>
        public int GetPendingOperationCount()
        {
            lock (_syncLock)
            {
                return _pendingOperations.Count;
            }
        }

        /// <summary>
        /// Waits for all pending operations to complete
        /// </summary>
        public async Task WaitForPendingOperationsAsync(TimeSpan timeout = default)
        {
            if (timeout == default)
                timeout = TimeSpan.FromSeconds(30);
                
            var deadline = DateTime.UtcNow.Add(timeout);
            
            while (DateTime.UtcNow < deadline)
            {
                Task[] pendingTasks;
                
                lock (_syncLock)
                {
                    if (_pendingOperations.Count == 0)
                        return;
                        
                    pendingTasks = _pendingOperations.Values.ToArray();
                }
                
                try
                {
                    var remainingTime = deadline - DateTime.UtcNow;
                    if (remainingTime > TimeSpan.Zero)
                    {
                        await Task.WhenAll(pendingTasks).WaitAsync(remainingTime);
                        return;
                    }
                }
                catch (TimeoutException)
                {
                    // Continue loop to check again
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error waiting for pending operations");
                }
                
                await Task.Delay(100); // Brief delay before checking again
            }
            
            _logger?.LogWarning("Timeout waiting for {Count} pending operations", GetPendingOperationCount());
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(ThreadSafeTabOperations));
        }
        
        #endregion

        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    // Cancel all operations
                    _cancellationTokenSource.Cancel();
                    
                    // Wait for pending operations (with timeout)
                    try
                    {
                        WaitForPendingOperationsAsync(TimeSpan.FromSeconds(5)).Wait();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning(ex, "Error waiting for operations during disposal");
                    }
                    
                    // Dispose resources
                    _operationSemaphore?.Dispose();
                    _cancellationTokenSource?.Dispose();
                    
                    lock (_syncLock)
                    {
                        _pendingOperations.Clear();
                    }
                    
                    _disposed = true;
                    _logger?.LogDebug("ThreadSafeTabOperations disposed");
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during ThreadSafeTabOperations disposal");
                }
            }
        }
        
        #endregion
    }
} 