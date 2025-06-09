using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Thread-safe singleton manager for MainWindow lifecycle with weak references.
    /// </summary>
    public sealed class WindowLifecycleManager : IWindowRegistry, IDisposable
    {
        #region Singleton Implementation
        
        private static readonly Lazy<WindowLifecycleManager> _instance = 
            new Lazy<WindowLifecycleManager>(() => new WindowLifecycleManager());
        
        /// <summary>
        /// Gets the singleton instance of WindowLifecycleManager.
        /// </summary>
        public static WindowLifecycleManager Instance => _instance.Value;
        
        #endregion

        #region Fields
        
        private readonly ConcurrentDictionary<Guid, WeakReference<ExplorerPro.UI.MainWindow.MainWindow>> _windows;
        private readonly ConditionalWeakTable<ExplorerPro.UI.MainWindow.MainWindow, WindowMetadata> _metadata;
        private readonly ReaderWriterLockSlim _lock;
        private readonly Timer _cleanupTimer;
        private readonly ILogger<WindowLifecycleManager> _logger;
        private volatile bool _isDisposed;
        
        #endregion

        #region Constructor
        
        private WindowLifecycleManager()
        {
            _windows = new ConcurrentDictionary<Guid, WeakReference<ExplorerPro.UI.MainWindow.MainWindow>>();
            _metadata = new ConditionalWeakTable<ExplorerPro.UI.MainWindow.MainWindow, WindowMetadata>();
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            
            // Configure logger - in production, inject via DI
            _logger = LoggerFactory.Create(builder => builder.AddConsole())
                .CreateLogger<WindowLifecycleManager>();
            
            // Setup periodic cleanup of dead references
            _cleanupTimer = new Timer(
                CleanupCallback, 
                null, 
                TimeSpan.FromMinutes(1), 
                TimeSpan.FromMinutes(1));
            
            _logger.LogInformation("WindowLifecycleManager initialized");
        }
        
        #endregion

        #region IWindowRegistry Implementation
        
        /// <summary>
        /// Registers a new MainWindow instance with thread-safe tracking.
        /// </summary>
        public void RegisterWindow(ExplorerPro.UI.MainWindow.MainWindow window)
        {
            ThrowIfDisposed();
            
            if (window == null)
                throw new ArgumentNullException(nameof(window));

            _lock.EnterWriteLock();
            try
            {
                var metadata = new WindowMetadata
                {
                    Id = Guid.NewGuid(),
                    CreatedAt = DateTime.UtcNow,
                    ThreadId = Thread.CurrentThread.ManagedThreadId
                };
                
                // Store metadata
                _metadata.Add(window, metadata);
                
                // Store weak reference
                var weakRef = new WeakReference<ExplorerPro.UI.MainWindow.MainWindow>(window);
                if (_windows.TryAdd(metadata.Id, weakRef))
                {
                    _logger.LogInformation($"Registered window {metadata.Id} from thread {metadata.ThreadId}");
                    
                    // Hook into window events for automatic unregistration
                    window.Closed += OnWindowClosed;
                }
                else
                {
                    _logger.LogWarning($"Failed to register window {metadata.Id}");
                    throw new InvalidOperationException("Failed to register window");
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Unregisters a MainWindow instance.
        /// </summary>
        public bool UnregisterWindow(ExplorerPro.UI.MainWindow.MainWindow window)
        {
            ThrowIfDisposed();
            
            if (window == null)
                return false;

            _lock.EnterWriteLock();
            try
            {
                if (_metadata.TryGetValue(window, out var metadata))
                {
                    // Unhook events
                    window.Closed -= OnWindowClosed;
                    
                    // Remove from collections
                    bool removed = _windows.TryRemove(metadata.Id, out _);
                    _metadata.Remove(window);
                    
                    if (removed)
                    {
                        _logger.LogInformation($"Unregistered window {metadata.Id}");
                    }
                    
                    return removed;
                }
                
                return false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        /// <summary>
        /// Gets all active (non-garbage collected) windows.
        /// </summary>
        public IEnumerable<ExplorerPro.UI.MainWindow.MainWindow> GetActiveWindows()
        {
            ThrowIfDisposed();
            
            _lock.EnterReadLock();
            try
            {
                var activeWindows = new List<ExplorerPro.UI.MainWindow.MainWindow>();
                var deadReferences = new List<Guid>();
                
                foreach (var kvp in _windows)
                {
                    if (kvp.Value.TryGetTarget(out var window))
                    {
                        activeWindows.Add(window);
                    }
                    else
                    {
                        deadReferences.Add(kvp.Key);
                    }
                }
                
                // Upgrade to write lock for cleanup if needed
                if (deadReferences.Any())
                {
                    _lock.ExitReadLock();
                    _lock.EnterWriteLock();
                    try
                    {
                        foreach (var id in deadReferences)
                        {
                            _windows.TryRemove(id, out _);
                            _logger.LogDebug($"Removed dead reference for window {id}");
                        }
                    }
                    finally
                    {
                        _lock.ExitWriteLock();
                        _lock.EnterReadLock();
                    }
                }
                
                return activeWindows;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Gets the count of active windows.
        /// </summary>
        public int ActiveWindowCount
        {
            get
            {
                ThrowIfDisposed();
                return GetActiveWindows().Count();
            }
        }
        
        /// <summary>
        /// Finds a window by its unique ID.
        /// </summary>
        public ExplorerPro.UI.MainWindow.MainWindow FindWindow(Guid windowId)
        {
            ThrowIfDisposed();
            
            _lock.EnterReadLock();
            try
            {
                if (_windows.TryGetValue(windowId, out var weakRef) && 
                    weakRef.TryGetTarget(out var window))
                {
                    return window;
                }
                
                return null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Manually triggers cleanup of disposed windows.
        /// </summary>
        public void CleanupDisposedWindows()
        {
            ThrowIfDisposed();
            
            _lock.EnterWriteLock();
            try
            {
                var deadReferences = _windows
                    .Where(kvp => !kvp.Value.TryGetTarget(out _))
                    .Select(kvp => kvp.Key)
                    .ToList();
                
                foreach (var id in deadReferences)
                {
                    if (_windows.TryRemove(id, out _))
                    {
                        _logger.LogDebug($"Cleaned up disposed window {id}");
                    }
                }
                
                _logger.LogInformation($"Cleanup completed. Removed {deadReferences.Count} dead references");
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        
        #endregion

        #region Helper Methods
        
        /// <summary>
        /// Retrieves metadata for a window.
        /// </summary>
        public WindowMetadata GetWindowMetadata(ExplorerPro.UI.MainWindow.MainWindow window)
        {
            ThrowIfDisposed();
            
            _lock.EnterReadLock();
            try
            {
                return _metadata.TryGetValue(window, out var metadata) ? metadata : null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        /// <summary>
        /// Gets diagnostic information about the registry state.
        /// </summary>
        public RegistryDiagnostics GetDiagnostics()
        {
            ThrowIfDisposed();
            
            _lock.EnterReadLock();
            try
            {
                var totalReferences = _windows.Count;
                var activeCount = 0;
                var deadCount = 0;
                
                foreach (var weakRef in _windows.Values)
                {
                    if (weakRef.TryGetTarget(out _))
                        activeCount++;
                    else
                        deadCount++;
                }
                
                return new RegistryDiagnostics
                {
                    TotalReferences = totalReferences,
                    ActiveWindows = activeCount,
                    DeadReferences = deadCount,
                    LastCleanup = _lastCleanup
                };
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }
        
        #endregion

        #region Event Handlers
        
        private void OnWindowClosed(object sender, EventArgs e)
        {
            if (sender is ExplorerPro.UI.MainWindow.MainWindow window)
            {
                // Unregister on background thread to avoid blocking UI
                Task.Run(() => UnregisterWindow(window));
            }
        }
        
        #endregion

        #region Cleanup Timer
        
        private DateTime _lastCleanup = DateTime.UtcNow;
        
        private void CleanupCallback(object state)
        {
            try
            {
                CleanupDisposedWindows();
                _lastCleanup = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during periodic cleanup");
            }
        }
        
        #endregion

        #region IDisposable
        
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(WindowLifecycleManager));
        }
        
        public void Dispose()
        {
            if (_isDisposed) return;
            
            _isDisposed = true;
            
            _cleanupTimer?.Dispose();
            
            _lock.EnterWriteLock();
            try
            {
                // Clear all references
                _windows.Clear();
                
                _logger.LogInformation("WindowLifecycleManager disposed");
            }
            finally
            {
                _lock.ExitWriteLock();
                _lock?.Dispose();
            }
        }
        
        #endregion

        #region Nested Types
        
        /// <summary>
        /// Metadata associated with each registered window.
        /// </summary>
        public class WindowMetadata
        {
            public Guid Id { get; set; }
            public DateTime CreatedAt { get; set; }
            public int ThreadId { get; set; }
            public Dictionary<string, object> CustomData { get; set; } = new Dictionary<string, object>();
        }
        
        /// <summary>
        /// Diagnostic information about the registry state.
        /// </summary>
        public class RegistryDiagnostics
        {
            public int TotalReferences { get; set; }
            public int ActiveWindows { get; set; }
            public int DeadReferences { get; set; }
            public DateTime LastCleanup { get; set; }
        }
        
        #endregion
    }
} 