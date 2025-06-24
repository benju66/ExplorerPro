using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core
{
    /// <summary>
    /// ENHANCED FOR FIX 5: Window Lifecycle Manager Thread Safety
    /// Simplified thread-safe window lifecycle manager without complex locking to eliminate deadlock risks
    /// </summary>
    public sealed class WindowLifecycleManager : IWindowRegistry
    {
        private static readonly Lazy<WindowLifecycleManager> _instance = 
            new Lazy<WindowLifecycleManager>(() => new WindowLifecycleManager());
            
        private readonly ConcurrentDictionary<Guid, WindowRegistration> _windows = 
            new ConcurrentDictionary<Guid, WindowRegistration>();
            
        private readonly ILogger<WindowLifecycleManager> _logger;
        private long _operationCounter = 0;
        
        public static WindowLifecycleManager Instance => _instance.Value;
        
        // Events use weak references internally
        private readonly WeakEventManager<WindowEventArgs> _windowRegistered = 
            new WeakEventManager<WindowEventArgs>();
        private readonly WeakEventManager<WindowEventArgs> _windowUnregistered = 
            new WeakEventManager<WindowEventArgs>();
        
        private WindowLifecycleManager()
        {
            _logger = ExplorerPro.UI.MainWindow.MainWindow.SharedLoggerFactory.CreateLogger<WindowLifecycleManager>();
            _logger?.LogInformation("WindowLifecycleManager initialized with simplified thread-safe implementation");
        }
        
        /// <summary>
        /// Register window with automatic cleanup on close
        /// Implements IWindowRegistry.RegisterWindow
        /// </summary>
        public void RegisterWindow(ExplorerPro.UI.MainWindow.MainWindow window)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            
            var id = Guid.NewGuid();
            var registration = new WindowRegistration(id, window);
            
            // Atomic add
            if (!_windows.TryAdd(id, registration))
            {
                _logger?.LogError($"Failed to register window {id}");
                throw new InvalidOperationException("Window registration failed");
            }
            
            // Auto-cleanup on close (weak subscription)
            try 
            {
                var cleanup = WeakEventHelper.Subscribe<EventArgs>(
                    window,
                    nameof(window.Closed),
                    (s, e) => UnregisterWindowById(id));
                registration.CleanupSubscription = cleanup;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, $"Failed to subscribe to window close event for {id}");
                // Still register the window, but without auto-cleanup
            }
            
            _logger?.LogInformation($"Window registered: {id}");
            
            // Raise event (non-blocking)
            ThreadPool.QueueUserWorkItem(_ => 
                _windowRegistered.RaiseEvent(this, new WindowEventArgs(id, window)));
        }
        
        /// <summary>
        /// Unregister window by window instance
        /// Implements IWindowRegistry.UnregisterWindow
        /// </summary>
        public bool UnregisterWindow(ExplorerPro.UI.MainWindow.MainWindow window)
        {
            if (window == null) return false;
            
            // Find registration by window instance
            foreach (var kvp in _windows)
            {
                if (kvp.Value.GetWindow() == window)
                {
                    return UnregisterWindowById(kvp.Key);
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Unregister window by ID
        /// </summary>
        public bool UnregisterWindowById(Guid id)
        {
            if (_windows.TryRemove(id, out var registration))
            {
                registration.Dispose();
                
                _logger?.LogInformation($"Window unregistered: {id}");
                
                // Raise event (non-blocking)
                ThreadPool.QueueUserWorkItem(_ => 
                    _windowUnregistered.RaiseEvent(this, new WindowEventArgs(id, null)));
                
                return true;
            }
            
            return false;
        }
        
        /// <summary>
        /// Try get window by ID (no locks)
        /// </summary>
        public bool TryGetWindow(Guid id, out ExplorerPro.UI.MainWindow.MainWindow window)
        {
            window = null;
            
            if (_windows.TryGetValue(id, out var registration))
            {
                window = registration.GetWindow();
                return window != null;
            }
            
            return false;
        }
        
        /// <summary>
        /// Get all active windows (snapshot)
        /// Implements IWindowRegistry.GetActiveWindows
        /// </summary>
        public IEnumerable<ExplorerPro.UI.MainWindow.MainWindow> GetActiveWindows()
        {
            var activeWindows = new List<ExplorerPro.UI.MainWindow.MainWindow>();
            var deadRegistrations = new List<Guid>();
            
            // Collect active windows and identify dead references
            foreach (var kvp in _windows)
            {
                var window = kvp.Value.GetWindow();
                if (window != null)
                {
                    activeWindows.Add(window);
                }
                else
                {
                    deadRegistrations.Add(kvp.Key);
                }
            }
            
            // Clean up dead references
            foreach (var id in deadRegistrations)
            {
                if (_windows.TryRemove(id, out var registration))
                {
                    registration.Dispose();
                    _logger?.LogDebug($"Cleaned up dead window reference: {id}");
                }
            }
            
            return activeWindows;
        }
        
        /// <summary>
        /// Get all window IDs (snapshot)
        /// </summary>
        public IReadOnlyList<Guid> GetWindowIds()
        {
            return _windows.Keys.ToList().AsReadOnly();
        }
        
        /// <summary>
        /// Count of registered windows
        /// Implements IWindowRegistry.ActiveWindowCount
        /// </summary>
        public int ActiveWindowCount 
        { 
            get 
            {
                // Count only windows that are still alive
                int count = 0;
                foreach (var registration in _windows.Values)
                {
                    if (registration.GetWindow() != null)
                        count++;
                }
                return count;
            }
        }

        /// <summary>
        /// Force close all tracked windows and clear collections
        /// Used during application shutdown
        /// </summary>
        public void ForceCloseAllWindows()
        {
            try
            {
                _logger?.LogInformation("ForceCloseAllWindows initiated - forcing closure of all tracked windows");
                
                var windowsToClose = new List<ExplorerPro.UI.MainWindow.MainWindow>();
                var registrationsToRemove = new List<Guid>();

                // Collect all windows and their IDs for cleanup
                foreach (var kvp in _windows)
                {
                    try
                    {
                        var window = kvp.Value.GetWindow();
                        if (window != null)
                        {
                            windowsToClose.Add(window);
                        }
                        registrationsToRemove.Add(kvp.Key);
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error collecting window for force close: {kvp.Key}");
                        registrationsToRemove.Add(kvp.Key);
                    }
                }

                // Force close all collected windows
                foreach (var window in windowsToClose)
                {
                    try
                    {
                        if (window != null && !window.IsDisposed)
                        {
                            _logger?.LogDebug($"Force closing window: {window.GetType().Name}");
                            
                            // Try graceful close first
                            window.Close();
                            
                            // If that doesn't work, try disposing
                            if (!window.IsDisposed)
                            {
                                window.Dispose();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error force closing window {window?.GetType().Name}");
                        
                        // Last resort - try to hide the window
                        try
                        {
                            window?.Hide();
                        }
                        catch
                        {
                            // Ignore final cleanup failures
                        }
                    }
                }

                // Clear all tracking collections with exception handling
                ClearTrackingCollections(registrationsToRemove);

                _logger?.LogInformation($"ForceCloseAllWindows completed - closed {windowsToClose.Count} windows, cleared {registrationsToRemove.Count} registrations");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Critical error during ForceCloseAllWindows");
            }
        }

        /// <summary>
        /// Clear all tracking collections gracefully
        /// </summary>
        private void ClearTrackingCollections(List<Guid> registrationIds)
        {
            try
            {
                // Remove all registrations
                foreach (var id in registrationIds)
                {
                    try
                    {
                        if (_windows.TryRemove(id, out var registration))
                        {
                            registration.Dispose();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error removing registration {id}");
                    }
                }

                // Force clear any remaining items (should be empty at this point)
                var remainingCount = _windows.Count;
                if (remainingCount > 0)
                {
                    _logger?.LogWarning($"Forcing clear of {remainingCount} remaining window registrations");
                    _windows.Clear();
                }

                // Reset operation counter
                Interlocked.Exchange(ref _operationCounter, 0);

                _logger?.LogDebug("Tracking collections cleared successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during tracking collection cleanup");
            }
        }
        
        /// <summary>
        /// Find window by ID
        /// Implements IWindowRegistry.FindWindow
        /// </summary>
        public ExplorerPro.UI.MainWindow.MainWindow FindWindow(Guid windowId)
        {
            TryGetWindow(windowId, out var window);
            return window;
        }
        
        /// <summary>
        /// Cleanup disposed windows
        /// Implements IWindowRegistry.CleanupDisposedWindows
        /// </summary>
        public void CleanupDisposedWindows()
        {
            var deadRegistrations = new List<Guid>();
            
            foreach (var kvp in _windows)
            {
                if (kvp.Value.GetWindow() == null)
                {
                    deadRegistrations.Add(kvp.Key);
                }
            }
            
            foreach (var id in deadRegistrations)
            {
                if (_windows.TryRemove(id, out var registration))
                {
                    registration.Dispose();
                    _logger?.LogDebug($"Cleaned up disposed window: {id}");
                }
            }
            
            if (deadRegistrations.Count > 0)
            {
                _logger?.LogInformation($"Cleaned up {deadRegistrations.Count} disposed windows");
            }
        }
        
        /// <summary>
        /// Perform operation on window if it exists
        /// </summary>
        public bool TryOperateOnWindow(Guid id, Action<ExplorerPro.UI.MainWindow.MainWindow> operation)
        {
            if (operation == null) throw new ArgumentNullException(nameof(operation));
            
            if (TryGetWindow(id, out var window))
            {
                var operationId = Interlocked.Increment(ref _operationCounter);
                
                try
                {
                    _logger?.LogDebug($"Operation {operationId} starting on window {id}");
                    operation(window);
                    _logger?.LogDebug($"Operation {operationId} completed on window {id}");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Operation {operationId} failed on window {id}");
                    throw;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Subscribe to window registered event (weak)
        /// </summary>
        public IDisposable SubscribeToWindowRegistered(EventHandler<WindowEventArgs> handler)
        {
            return _windowRegistered.Subscribe(handler);
        }
        
        /// <summary>
        /// Subscribe to window unregistered event (weak)
        /// </summary>
        public IDisposable SubscribeToWindowUnregistered(EventHandler<WindowEventArgs> handler)
        {
            return _windowUnregistered.Subscribe(handler);
        }
        
        /// <summary>
        /// Window registration with weak reference
        /// </summary>
        private class WindowRegistration : IDisposable
        {
            private readonly WeakReference<ExplorerPro.UI.MainWindow.MainWindow> _windowRef;
            public Guid Id { get; }
            public IDisposable CleanupSubscription { get; set; }
            
            public WindowRegistration(Guid id, ExplorerPro.UI.MainWindow.MainWindow window)
            {
                Id = id;
                _windowRef = new WeakReference<ExplorerPro.UI.MainWindow.MainWindow>(window);
            }
            
            public ExplorerPro.UI.MainWindow.MainWindow GetWindow()
            {
                return _windowRef.TryGetTarget(out var window) ? window : null;
            }
            
            public void Dispose()
            {
                CleanupSubscription?.Dispose();
            }
        }
    }
    
    /// <summary>
    /// Window lifecycle event args
    /// </summary>
    public class WindowEventArgs : EventArgs
    {
        public Guid WindowId { get; }
        public ExplorerPro.UI.MainWindow.MainWindow Window { get; }
        
        public WindowEventArgs(Guid windowId, ExplorerPro.UI.MainWindow.MainWindow window)
        {
            WindowId = windowId;
            Window = window;
        }
    }
    
    /// <summary>
    /// Simple weak event manager for WindowLifecycleManager internal use
    /// </summary>
    internal class WeakEventManager<TEventArgs> where TEventArgs : EventArgs
    {
        private readonly List<WeakReference> _handlers = new List<WeakReference>();
        private readonly object _lock = new object();
        
        public IDisposable Subscribe(EventHandler<TEventArgs> handler)
        {
            lock (_lock)
            {
                _handlers.Add(new WeakReference(handler));
                return new Subscription(this, handler);
            }
        }
        
        public void RaiseEvent(object sender, TEventArgs args)
        {
            List<EventHandler<TEventArgs>> handlers;
            
            lock (_lock)
            {
                // Clean up dead references and get live handlers
                handlers = new List<EventHandler<TEventArgs>>();
                for (int i = _handlers.Count - 1; i >= 0; i--)
                {
                    if (_handlers[i].Target is EventHandler<TEventArgs> handler)
                    {
                        handlers.Add(handler);
                    }
                    else
                    {
                        _handlers.RemoveAt(i);
                    }
                }
            }
            
            // Invoke outside lock
            foreach (var handler in handlers)
            {
                try
                {
                    handler(sender, args);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Event handler error: {ex.Message}");
                }
            }
        }
        
        private class Subscription : IDisposable
        {
            private WeakEventManager<TEventArgs> _manager;
            private EventHandler<TEventArgs> _handler;
            
            public Subscription(WeakEventManager<TEventArgs> manager, EventHandler<TEventArgs> handler)
            {
                _manager = manager;
                _handler = handler;
            }
            
            public void Dispose()
            {
                if (_manager != null && _handler != null)
                {
                    lock (_manager._lock)
                    {
                        _manager._handlers.RemoveAll(wr => wr.Target == _handler);
                    }
                    _manager = null;
                    _handler = null;
                }
            }
        }
    }
} 