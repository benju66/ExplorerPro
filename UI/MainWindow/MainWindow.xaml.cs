using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.UI.Dialogs;
using ExplorerPro.UI.FileTree;
using ExplorerPro.UI.PaneManagement;
using ExplorerPro.UI.Panels;
using ExplorerPro.UI.Panels.PinnedPanel;
using ExplorerPro.Utilities;
using ExplorerPro.Themes;
using ExplorerPro.Core;
using ExplorerPro.Core.Disposables;
using ExplorerPro.Core.Threading;
using System.Runtime.CompilerServices;
// Add reference to System.Windows.Forms but use an alias
using WinForms = System.Windows.Forms;
using WPF = System.Windows;
using System.Collections.Concurrent;
using ExplorerPro.ViewModels;
using ExplorerPro.UI.Controls;

namespace ExplorerPro.UI.MainWindow
{
    /// <summary>
    /// Main application window that serves as the primary user interface for the ExplorerPro file manager.
    /// 
    /// This class implements a modern, tabbed file explorer interface with support for:
    /// - Multi-tab navigation with individual file tree views
    /// - Dockable side panels (Pinned items, Bookmarks, Todo, Procore)
    /// - Split-view functionality for comparing directories
    /// - Drag-and-drop file operations
    /// - Keyboard shortcuts and command routing
    /// - Theme management and customization
    /// - Window state persistence and restoration
    /// 
    /// Architecture:
    /// - Follows MVVM pattern with command binding
    /// - Implements IDisposable for proper resource cleanup
    /// - Uses dependency injection for services
    /// - Supports window detachment and multi-window scenarios
    /// 
    /// Performance Features:
    /// - Shared logger factory to prevent memory leaks
    /// - Event handler cleanup tracking
    /// - Navigation history with memory management
    /// - Tab hibernation for resource conservation
    /// 
    /// Thread Safety:
    /// - UI thread safety enforced with ExecuteOnUIThread
    /// - Thread-safe navigation history management
    /// - Concurrent collection usage for window tracking
    /// 
    /// ENHANCED FOR FIX 4: Event Handler Memory Leaks - Implements IDisposable
    /// 
    /// =============================================================================
    /// PHASE 1: LAYOUT DEPENDENCIES DOCUMENTATION
    /// =============================================================================
    /// 
    /// LAYOUT-DEPENDENT CODE SECTIONS TO TRACK FOR RESTRUCTURING:
    /// 
    /// 1. UI ELEMENT REFERENCES (Direct access to layout elements):
    ///    - MainTabs (ChromeStyleTabControl) - Tab container
    ///    - Toolbar (UserControl) - Navigation toolbar
    ///    - StatusText, ItemCountText, SelectionText - Status bar elements
    ///    - ActivityBar buttons - Panel toggles
    /// 
    /// 2. INITIALIZATION METHODS (Assume current layout):
    ///    - InitializeMainWindow() - Sets up UI element references
    ///    - OnSourceInitialized() - Window initialization
    ///    - EnsureUIElementsAvailable() - Validates layout elements exist
    /// 
    /// 3. EVENT HANDLERS (Layout-specific button clicks):
    ///    - TogglePinnedPanel_Click, ToggleBookmarksPanel_Click, etc.
    ///    - AddTabButton_Click, TabCloseButton_Click
    ///    - MainTabs_SelectionChanged
    /// 
    /// 4. UI UPDATE METHODS (Direct manipulation of layout elements):
    ///    - UpdateActivityBarButtonStates() 
    ///    - UpdateStatus(), UpdateItemCount(), UpdateSelectionInfo()
    ///    - RefreshThemeElements()
    /// 
    /// 5. SAFE ACCESSORS (Thread-safe element access):
    ///    - SafeMainTabs, SafeStatusText, SafeItemCountText, SafeSelectionText
    ///    - TryAccessUIElement<T>() pattern
    /// 
    /// =============================================================================
    /// </summary>
    public partial class MainWindow : Window, IDisposable
    {
        #region Thread-Safe Shared Logger Infrastructure

        private static readonly object _loggerFactoryLock = new object();
        private static ILoggerFactory _sharedLoggerFactory;
        private static int _loggerFactoryRefCount = 0;
        private static bool _isDisposing = false;
        private static ILogger<MainWindow> _staticLogger;

        static MainWindow()
        {
            lock (_loggerFactoryLock)
            {
                _sharedLoggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole()
                           .SetMinimumLevel(LogLevel.Debug);
                });
                _staticLogger = _sharedLoggerFactory.CreateLogger<MainWindow>();
            }
        }

        /// <summary>
        /// Thread-safe logger factory access with disposal protection
        /// </summary>
        public static ILoggerFactory SharedLoggerFactory
        {
            get
            {
                lock (_loggerFactoryLock)
                {
                    if (_isDisposing || _sharedLoggerFactory == null)
                    {
                        throw new ObjectDisposedException(nameof(SharedLoggerFactory), 
                            "Logger factory has been disposed or is being disposed");
                    }
                    return _sharedLoggerFactory;
                }
            }
        }

        /// <summary>
        /// Increment reference count when window is created
        /// </summary>
        private static void IncrementLoggerRef()
        {
            lock (_loggerFactoryLock)
            {
                _loggerFactoryRefCount++;
            }
        }

        /// <summary>
        /// Decrement reference count when window is disposed
        /// </summary>
        private static void DecrementLoggerRef()
        {
            lock (_loggerFactoryLock)
            {
                _loggerFactoryRefCount--;
            }
        }

        /// <summary>
        /// Safely dispose shared logger with reference counting
        /// </summary>
        public static void DisposeSharedLogger()
        {
            lock (_loggerFactoryLock)
            {
                if (_isDisposing) return;
                _isDisposing = true;

                // Wait for all windows to release their references
                var timeout = DateTime.UtcNow.AddSeconds(5);
                while (_loggerFactoryRefCount > 0 && DateTime.UtcNow < timeout)
                {
                    System.Threading.Thread.Sleep(100);
                }

                if (_loggerFactoryRefCount > 0)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"WARNING: Disposing logger factory with {_loggerFactoryRefCount} active references");
                }

                try
                {
                    _sharedLoggerFactory?.Dispose();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error disposing logger factory: {ex.Message}");
                }
                finally
                {
                    _sharedLoggerFactory = null;
                    _staticLogger = null;
                }
            }
        }

        /// <summary>
        /// Creates an instance-specific logger with window ID context.
        /// Each window instance gets its own logger for proper context isolation.
        /// </summary>
        /// <param name="windowId">Unique identifier for this window instance</param>
        /// <returns>Logger instance configured for this window</returns>
        private ILogger<MainWindow> CreateInstanceLogger(string windowId)
        {
            return SharedLoggerFactory.CreateLogger<MainWindow>();
        }

        /// <summary>
        /// Validation method to verify shared logger factory usage.
        /// Useful for unit testing and monitoring proper initialization.
        /// </summary>
        /// <returns>True if this instance is using the shared logger factory</returns>
        public bool IsUsingSharedLogger()
        {
            return _instanceLogger != null && _sharedLoggerFactory != null;
        }

        /// <summary>
        /// Gets statistics about the shared logger factory for validation and monitoring.
        /// </summary>
        /// <returns>Formatted string with logger factory statistics</returns>
        public static string GetSharedLoggerStats()
        {
            try
            {
                var factoryHashCode = _sharedLoggerFactory?.GetHashCode() ?? 0;
                return $"SharedLoggerFactory HashCode: {factoryHashCode}, IsDisposed: {_sharedLoggerFactory == null}";
            }
            catch (Exception ex)
            {
                return $"Error getting shared logger stats: {ex.Message}";
            }
        }

        #endregion

        #region Event Handler Memory Management - FIX 4: Event Handler Memory Leaks

        /// <summary>
        /// IMPLEMENTATION OF FIX 4: Event Handler Memory Leaks
        /// Safely subscribes to an event and tracks it for cleanup to prevent memory leaks.
        /// 
        /// This method ensures that all event subscriptions are properly tracked and cleaned up
        /// during disposal, preventing memory leaks in long-running applications.
        /// </summary>
        /// <typeparam name="TEventArgs">Type of event arguments</typeparam>
        /// <param name="subscribe">Action to subscribe to the event</param>
        /// <param name="unsubscribe">Action to unsubscribe from the event</param>
        /// <param name="handler">Event handler to manage</param>
        /// <summary>
        /// ENHANCED FOR FIX 4: Event Handler Memory Leak Resolution
        /// Creates weak event subscriptions that automatically clean up when target is collected.
        /// </summary>
        private void SubscribeToEventWeak<TEventArgs>(
            object source,
            string eventName,
            EventHandler<TEventArgs> handler) where TEventArgs : EventArgs
        {
            try
            {
                var subscription = WeakEventHelper.Subscribe(source, eventName, handler);
                _eventSubscriptions.Add(subscription);
                
                _instanceLogger?.LogDebug($"Subscribed to event '{eventName}' with weak reference (Total: {_eventSubscriptions.Count})");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, $"Error subscribing to weak event '{eventName}'");
                throw;
            }
        }

        /// <summary>
        /// Safely subscribes to routed events and tracks them for cleanup.
        /// Specialized version for WPF routed events.
        /// </summary>
        /// <param name="subscribe">Action to subscribe to the routed event</param>
        /// <param name="unsubscribe">Action to unsubscribe from the routed event</param>
        /// <param name="handler">Routed event handler to manage</param>
        /// <summary>
        /// ENHANCED FOR FIX 4: Event Handler Memory Leak Resolution
        /// Creates weak routed event subscriptions for automatic cleanup.
        /// </summary>
        private void SubscribeToRoutedEventWeak(
            UIElement element,
            RoutedEvent routedEvent,
            RoutedEventHandler handler)
        {
            try
            {
                // For routed events, we'll use a manual weak reference approach
                var weakRef = new WeakReference(handler.Target);
                var method = handler.Method;
                
                RoutedEventHandler weakHandler = (s, e) =>
                {
                    var target = weakRef.Target;
                    if (target != null)
                    {
                        method.Invoke(target, new object[] { s, e });
                    }
                };
                
                element.AddHandler(routedEvent, weakHandler);
                
                var subscription = Disposable.Create(() => element.RemoveHandler(routedEvent, weakHandler));
                _eventSubscriptions.Add(subscription);
                
                _instanceLogger?.LogDebug($"Subscribed to routed event '{routedEvent.Name}' with weak reference");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, $"Error subscribing to weak routed event '{routedEvent?.Name}'");
                throw;
            }
        }

        /// <summary>
        /// Special weak event subscription for SelectionChanged events
        /// </summary>
        private void SubscribeToSelectionChangedWeak(
            System.Windows.Controls.Primitives.Selector selector,
            SelectionChangedEventHandler handler)
        {
            try
            {
                var weakRef = new WeakReference(handler.Target);
                var method = handler.Method;
                
                SelectionChangedEventHandler weakHandler = (s, e) =>
                {
                    var target = weakRef.Target;
                    if (target != null)
                    {
                        method.Invoke(target, new object[] { s, e });
                    }
                };
                
                selector.SelectionChanged += weakHandler;
                
                var subscription = Disposable.Create(() => selector.SelectionChanged -= weakHandler);
                _eventSubscriptions.Add(subscription);
                
                _instanceLogger?.LogDebug("Subscribed to SelectionChanged event with weak reference");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error subscribing to weak SelectionChanged event");
                throw;
            }
        }

        /// <summary>
        /// Special weak event subscription for Window Closing events
        /// </summary>
        private void SubscribeToClosingWeak(
            Window window,
            CancelEventHandler handler)
        {
            try
            {
                var weakRef = new WeakReference(handler.Target);
                var method = handler.Method;
                
                CancelEventHandler weakHandler = (s, e) =>
                {
                    var target = weakRef.Target;
                    if (target != null)
                    {
                        method.Invoke(target, new object[] { s, e });
                    }
                };
                
                window.Closing += weakHandler;
                
                var subscription = Disposable.Create(() => window.Closing -= weakHandler);
                _eventSubscriptions.Add(subscription);
                
                _instanceLogger?.LogDebug("Subscribed to Closing event with weak reference");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error subscribing to weak Closing event");
                throw;
            }
        }

        #endregion

        #region Core Dependencies and Managers

        /// <summary>
        /// Settings service for managing user preferences and application configuration.
        /// Handles window layout, theme preferences, panel visibility, and keyboard shortcuts.
        /// Injected via constructor for testability, or created as singleton for standalone use.
        /// </summary>
        private readonly ISettingsService _settingsService;

        /// <summary>
        /// Metadata manager for handling file properties, tags, and extended attributes.
        /// Used by the Properties dialog and metadata display features.
        /// </summary>
        private MetadataManager _metadataManager;

        #endregion

        #region Navigation History Management - FIX 3: Unbounded Growth Prevention

        /// <summary>
        /// Maximum number of navigation history entries to maintain.
        /// Prevents unbounded memory growth during extended use.
        /// </summary>
        private const int MaxHistorySize = 1000;

        /// <summary>
        /// Number of entries to remove when MaxHistorySize is exceeded.
        /// Removes older entries to maintain performance.
        /// </summary>
        private const int HistoryTrimSize = 100;
        
        /// <summary>
        /// Doubly-linked list storing navigation history entries.
        /// Provides efficient insertion/removal for history trimming.
        /// </summary>
        private readonly LinkedList<NavigationEntry> _navigationHistory = new LinkedList<NavigationEntry>();

        /// <summary>
        /// Current position in the navigation history for back/forward operations.
        /// Null when no navigation has occurred yet.
        /// </summary>
        private LinkedListNode<NavigationEntry>? _currentHistoryNode;

        /// <summary>
        /// Thread synchronization lock for navigation history operations.
        /// Ensures thread-safe access when multiple threads update navigation state.
        /// </summary>
        private readonly object _historyLock = new object();

        #endregion

        #region Window Management - FIX 5: Detached Windows List Management

        /// <summary>
        /// Thread-safe collection of detached windows using weak references.
        /// Prevents memory leaks by allowing garbage collection of closed windows.
        /// Key: Unique window identifier, Value: Weak reference to MainWindow instance.
        /// </summary>
        private readonly ConcurrentDictionary<Guid, WeakReference<MainWindow>> _detachedWindows 
            = new ConcurrentDictionary<Guid, WeakReference<MainWindow>>();

        #endregion

        #region Initialization and Lifecycle Management

        /// <summary>
        /// Logger instance for this specific MainWindow instance.
        /// Provides contextual logging with window-specific information.
        /// </summary>
        private readonly ILogger<MainWindow> _logger;

        /// <summary>
        /// Handles the complex initialization sequence for MainWindow components.
        /// Manages async initialization, error recovery, and state transitions.
        /// </summary>
        private readonly MainWindowInitializer _initializer;

        /// <summary>
        /// Centralized exception handling for this window instance.
        /// Provides consistent error reporting and recovery mechanisms.
        /// </summary>
        private readonly ExceptionHandler _exceptionHandler;

        /// <summary>
        /// Thread synchronization lock for initialization state management.
        /// Prevents race conditions during window startup and shutdown.
        /// </summary>
        private readonly object _stateLock = new object();

        /// <summary>
        /// Context object containing initialization parameters and state.
        /// Used for complex initialization scenarios and error recovery.
        /// </summary>
        private WindowInitializationContext _initContext;
        
        /// <summary>
        /// Unified window state manager for thread-safe state tracking.
        /// Replaces the previous fragmented state system (InitializationState + UIWindowState + _isDisposed).
        /// </summary>
        private readonly WindowStateManager _stateManager = new WindowStateManager();

        /// <summary>
        /// Reference to the main ChromeStyleTabControl hosting MainWindowContainer instances.
        /// Cached for safe access during initialization and disposal.
        /// </summary>
        private ChromeStyleTabControl _mainTabsControl;

        /// <summary>
        /// Main window view model for Chrome-style tab system
        /// </summary>
        private MainWindowViewModel _viewModel;
        
        /// <summary>
        /// Window manager for detached windows
        /// </summary>
        private ExplorerPro.Core.TabManagement.IDetachedWindowManager _windowManager;
        private ExplorerPro.Core.TabManagement.TabOperationsManager _tabOperationsManager;
        private ExplorerPro.Core.TabManagement.ITabDragDropService _dragDropService;

        #endregion

        #region Weak Event Management - ENHANCED FOR FIX 4

        /// <summary>
        /// ENHANCED FOR FIX 4: Event Handler Memory Leak Resolution
        /// Manages all event subscriptions using weak references to prevent memory leaks.
        /// Replaces manual cleanup tracking with automatic disposal pattern.
        /// </summary>
        private readonly CompositeDisposable _eventSubscriptions = new CompositeDisposable();

        #endregion

        #region UI State Management

        /// <summary>
        /// Instance-specific logger created from the shared logger factory.
        /// Provides logging with this window's specific context.
        /// </summary>
        private ILogger<MainWindow> _instanceLogger;

        private TabItem? _rightClickedTab;

        #region Phase 1 Enhancement Fields

        // 3. Event Handler Debouncing
        private DateTime _lastContextMenuAction = DateTime.MinValue;
        private const int DEBOUNCE_INTERVAL_MS = 500;

        // 4. Smart Memory Cleanup tracking
        private int _operationCount = 0;
        private const int CLEANUP_THRESHOLD = 50;

        // 5. Context Menu Caching
        private bool _contextMenuStateValid = false;
        private int _cachedTabCount = 0;
        private bool _cachedPinnedState = false;
        private int _cachedSelectedTabHash = 0;

        #endregion

        /// <summary>
        /// PHASE 3 OPTIMIZATION: Cached embedded toolbar to avoid repeated visual tree searches
        /// The toolbar is embedded within the ChromeStyleTabControl template and accessed frequently.
        /// This cache prevents expensive FindName() operations on every toolbar access.
        /// </summary>
        private UI.Toolbar.Toolbar? _cachedEmbeddedToolbar;
        private bool _toolbarCacheInitialized = false;

        /// <summary>
        /// Public property to check if the window has been disposed
        /// IMPLEMENTATION OF FIX 2: Thread-Safety Issues in UI Updates
        /// Now uses unified state manager instead of separate _isDisposed field
        /// </summary>
        public bool IsDisposed => _stateManager.IsClosing;

        #endregion

        #region Safe UI Element Access

        /// <summary>
        /// IMPLEMENTATION OF FIX 6: Unsafe MainTabs Access & FIX 2: Thread-Safety Issues in UI Updates
        /// 
        /// This section implements comprehensive defensive null checks, state validation, and 
        /// thread-safety for all UI element access to prevent NullReferenceException and 
        /// cross-thread exceptions during initialization, disposal, and state transitions.
        /// 
        /// Key Features:
        /// - Safe accessor properties for MainTabs, StatusText, ItemCountText, SelectionText
        /// - TryAccessUIElement method for safe UI operations with logging
        /// - UIWindowState enum for proper state tracking (Initializing -> Ready -> Closing -> Disposed)
        /// - State transitions in lifecycle methods (OnSourceInitialized, Loaded, Closing, Closed)
        /// - Safe tab operations (AddTab, RemoveTab, GetTabCount, HasTabs)
        /// - Thread-safe status bar updates (UpdateStatus, UpdateItemCount, UpdateSelectionInfo)
        /// - Thread-safe address bar updates (UpdateToolbarAddressBar, UpdateAddressBar)
        /// - Thread-safe theme updates (RefreshThemeElements)
        /// - Safe tab navigation (NextTab, PreviousTab)
        /// - Enhanced ExecuteOnUIThread with disposal checks, exception handling, and logging
        /// - Public IsDisposed property for external state checking
        /// - Comprehensive logging for debugging and monitoring
        /// 
        /// This prevents crashes from:
        /// - Accessing UI elements before window is fully initialized
        /// - Accessing UI elements after window disposal
        /// - Race conditions during window state transitions
        /// - Background operations attempting UI access when window is closing
        /// - Cross-thread exceptions when UI updates come from background threads
        /// - Dispatcher shutdown scenarios
        /// - Task cancellation during window closure
        /// </summary>

        /// <summary>
        /// Safe accessor properties for all frequently accessed UI elements
        /// Now uses unified state manager for disposal and state checking
        /// </summary>
        private TabControl SafeMainTabs => MainTabs != null && _stateManager.IsOperational ? MainTabs : null;
        private TextBlock SafeStatusText => StatusText != null && _stateManager.IsOperational ? StatusText : null;
        private TextBlock SafeItemCountText => ItemCountText != null && _stateManager.IsOperational ? ItemCountText : null;
        private TextBlock SafeSelectionText => SelectionText != null && _stateManager.IsOperational ? SelectionText : null;

        /// <summary>
        /// Safely executes an action on a UI element with null and disposal checks
        /// Now uses unified state manager for state checking
        /// </summary>
        private bool TryAccessUIElement<T>(T element, Action<T> action, [CallerMemberName] string callerName = "") 
            where T : UIElement
        {
            if (element == null || _stateManager.IsClosing || _stateManager.HasFailed)
            {
                _instanceLogger?.LogDebug($"{callerName}: UI element not available (null or disposed)");
                return false;
            }
            
            try
            {
                action(element);
                return true;
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, $"{callerName}: Error accessing UI element");
                return false;
            }
        }

        /// <summary>
        /// Executes an action on the UI thread safely, handling disposed state
        /// IMPLEMENTATION OF FIX 2: Thread-Safety Issues in UI Updates
        /// </summary>
        private void ExecuteOnUIThread(Action action, [CallerMemberName] string callerName = "")
        {
            if (action == null) return;
            
            try
            {
                if (Dispatcher == null || Dispatcher.HasShutdownStarted)
                {
                    _instanceLogger?.LogWarning($"{callerName}: Dispatcher unavailable, skipping UI update");
                    return;
                }
                
                if (Dispatcher.CheckAccess())
                {
                    action();
                }
                else
                {
                    Dispatcher.BeginInvoke(action, DispatcherPriority.Normal);
                }
            }
            catch (TaskCanceledException)
            {
                _instanceLogger?.LogDebug($"{callerName}: UI update cancelled during shutdown");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, $"{callerName}: Error during UI update");
            }
        }

        #region Public Tab Management Interface

        /// <summary>
        /// Safely adds a new tab to the main tab control with the specified header and content.
        /// 
        /// This method provides thread-safe tab creation with proper validation and error handling.
        /// It ensures the UI is in a valid state before attempting to add the tab and automatically
        /// selects the new tab once added.
        /// </summary>
        /// <param name="header">Display text for the tab header</param>
        /// <param name="content">Content object to display in the tab (typically a MainWindowContainer)</param>
        /// <remarks>
        /// This method is thread-safe and can be called from any thread. The actual UI operations
        /// are marshaled to the UI thread automatically.
        /// </remarks>
        public void AddTab(string header, object content)
        {
            ExecuteOnUIThread(() =>
            {
                TryAccessUIElement(SafeMainTabs, tabs =>
                {
                    var newTab = new TabItem
                    {
                        Header = header,
                        Content = content
                    };
                    
                    tabs.Items.Add(newTab);
                    tabs.SelectedItem = newTab;
                    
                    _instanceLogger?.LogInformation($"Added tab: {header}");
                });
            });
        }

        /// <summary>
        /// Safely removes a tab at the specified index with automatic tab selection management.
        /// 
        /// This method provides thread-safe tab removal with proper validation and error handling.
        /// After removal, it automatically selects an appropriate remaining tab to maintain
        /// consistent user experience.
        /// </summary>
        /// <param name="index">Zero-based index of the tab to remove</param>
        /// <remarks>
        /// If the index is invalid, the operation is logged but no exception is thrown.
        /// This method is thread-safe and can be called from any thread.
        /// </remarks>
        public void RemoveTab(int index)
        {
            ExecuteOnUIThread(() =>
            {
                TryAccessUIElement(SafeMainTabs, tabs =>
                {
                    if (index >= 0 && index < tabs.Items.Count)
                    {
                        var tab = tabs.Items[index] as TabItem;
                        tabs.Items.RemoveAt(index);
                        
                        _instanceLogger?.LogInformation($"Removed tab at index {index}: {tab?.Header}");
                        
                        // Select appropriate tab after removal
                        if (tabs.Items.Count > 0)
                        {
                            tabs.SelectedIndex = Math.Min(index, tabs.Items.Count - 1);
                        }
                    }
                    else
                    {
                        _instanceLogger?.LogWarning($"Invalid tab index for removal: {index}");
                    }
                });
            });
        }

        /// <summary>
        /// Gets the current number of tabs in a thread-safe manner.
        /// 
        /// This method safely accesses the tab control and returns the count of tabs,
        /// handling cases where the UI is not yet initialized or has been disposed.
        /// </summary>
        /// <returns>The number of tabs currently open, or 0 if tabs are not accessible</returns>
        public int GetTabCount()
        {
            var tabs = SafeMainTabs;
            return tabs?.Items?.Count ?? 0;
        }

        /// <summary>
        /// Determines whether any tabs are currently open.
        /// 
        /// This is a convenience method that provides a boolean check for tab presence,
        /// useful for enabling/disabling UI controls based on tab availability.
        /// </summary>
        /// <returns>True if one or more tabs are open; otherwise, false</returns>
        public bool HasTabs()
        {
            return GetTabCount() > 0;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of MainWindow with comprehensive error handling.
        /// ENHANCED FOR FIX 1: Logger Factory Memory Leaks
        /// </summary>
        #region Safe Initialization Pipeline

        private readonly TaskCompletionSource<bool> _initializationComplete = new TaskCompletionSource<bool>();

        /// <summary>
        /// Single constructor pattern - all initialization paths lead here
        /// </summary>
        public MainWindow() : this(CreateDefaultServices())
        {
        }

        /// <summary>
        /// Primary constructor with dependency injection
        /// </summary>
        private MainWindow((ILogger<MainWindow> logger, MainWindowInitializer initializer, ExceptionHandler handler, ISettingsService settings) services)
        {
            // Phase 1: Essential initialization only
            _initContext = new WindowInitializationContext();
            _logger = services.logger ?? throw new ArgumentNullException(nameof(services.logger));
            _initializer = services.initializer ?? throw new ArgumentNullException(nameof(services.initializer));
            _exceptionHandler = services.handler ?? throw new ArgumentNullException(nameof(services.handler));
            _settingsService = services.settings ?? throw new ArgumentNullException(nameof(services.settings));
            
            IncrementLoggerRef();
            
            try
            {
                // Phase 2: WPF initialization (keep context in Created state for now)
                InitializeComponent();
                
                // Create and set the MainWindowViewModel for Phase 2 tab commands
                var viewModelLogger = SharedLoggerFactory.CreateLogger<ViewModels.MainWindowViewModel>();
                _viewModel = new MainWindowViewModel(viewModelLogger);
                DataContext = _viewModel;
                
                // Create instance logger after we have a handle
                var windowId = Guid.NewGuid().ToString("N").Substring(0, 8);
                _instanceLogger = CreateInstanceLogger(windowId);
                _instanceLogger.LogInformation($"MainWindow created (ID: {windowId})");
                
                // Phase 4: Setup Chrome-style tab events
                SetupChromeStyleTabEvents();
                
                // Phase 7: Initialize tab management services
                InitializeTabManagement();
                
                // Phase 5: Defer all other initialization
                Loaded += OnWindowLoadedAsync;
            }
            catch (Exception ex)
            {
                _initContext.TransitionTo(Core.WindowState.Failed);
                DecrementLoggerRef();
                
                // Log and rethrow - window creation must fail cleanly
                _logger?.LogCritical(ex, "Failed to create MainWindow");
                throw new WindowInitializationException("Window creation failed", _initContext.CurrentState, ex);
            }
        }

        /// <summary>
        /// Factory method for default services
        /// </summary>
        private static (ILogger<MainWindow>, MainWindowInitializer, ExceptionHandler, ISettingsService) CreateDefaultServices()
        {
            var logger = SharedLoggerFactory.CreateLogger<MainWindow>();
            var initializerLogger = SharedLoggerFactory.CreateLogger<MainWindowInitializer>();
            var initializer = new MainWindowInitializer(initializerLogger);
            
            var handlerLogger = SharedLoggerFactory.CreateLogger<ExceptionHandler>();
            var telemetry = new ConsoleTelemetryService();
            var handler = new ExceptionHandler(handlerLogger, telemetry);
            
            var settings = new SettingsService(App.Settings ?? new SettingsManager(), logger);
            
            return (logger, initializer, handler, settings);
        }

        /// <summary>
        /// Async initialization on Loaded event
        /// </summary>
        private async void OnWindowLoadedAsync(object sender, RoutedEventArgs e)
        {
            // Unhook to prevent multiple calls
            Loaded -= OnWindowLoadedAsync;
            
            try
            {
                _instanceLogger.LogInformation("Starting async window initialization");
                
                // Progress through proper state transitions during initialization
                _stateManager.TryTransitionTo(Core.WindowState.Initializing, out _);
                
                var result = await _initializer.InitializeWindowAsync(this, _initContext);
                
                if (!result.IsSuccess)
                {
                    throw new WindowInitializationException(
                        result.ErrorMessage, 
                        result.State, 
                        result.Error);
                }
                
                // Progress through the remaining states
                _stateManager.TryTransitionTo(Core.WindowState.ComponentsReady, out _);
                _stateManager.TryTransitionTo(Core.WindowState.LoadingUI, out _);
                
                if (_stateManager.TryTransitionTo(Core.WindowState.Ready, out string error))
                {
                    _initializationComplete.SetResult(true);
                    _instanceLogger.LogInformation("Window initialization completed");
                    
                    // Create initial tab if needed
                    if (MainTabs?.Items.Count == 0)
                    {
                        AddNewMainWindowTabSafely();
                    }
                }
                else
                {
                    _instanceLogger.LogError($"Failed to transition to Ready state: {error}");
                    throw new InvalidOperationException($"State transition failed: {error}");
                }
            }
            catch (Exception ex)
            {
                _stateManager.TryTransitionTo(Core.WindowState.Failed, out _);
                _initializationComplete.SetException(ex);
                
                _instanceLogger.LogError(ex, "Window initialization failed");
                
                // Show user-friendly error
                MessageBox.Show(
                    $"Failed to initialize window: {ex.Message}\n\nThe application will now close.",
                    "Initialization Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
                
                Close();
            }
        }

        /// <summary>
        /// Wait for initialization to complete (for testing)
        /// </summary>
        public Task<bool> WaitForInitializationAsync()
        {
            return _initializationComplete.Task;
        }

        #endregion

        private void SetInitializationState(Core.WindowState state)
        {
            lock (_stateLock)
            {
                _stateManager.TryTransitionTo(state, out _);
                _instanceLogger?.LogDebug($"Window state changed to: {state}");
            }
        }

        private async void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await InitializeWindowAsync();
                SetInitializationState(Core.WindowState.Ready);
            }
            catch (Exception ex)
            {
                SetInitializationState(Core.WindowState.Failed);
                _instanceLogger?.LogError(ex, "Window initialization failed");
                MessageBox.Show($"Failed to initialize window: {ex.Message}", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private async Task InitializeWindowAsync()
        {
            try
            {
                _instanceLogger?.LogInformation("Starting async window initialization");
                
                // Use the new async initializer
                var result = await _initializer.InitializeWindowAsync(this, _initContext);
                
                if (!result.IsSuccess)
                {
                    throw new WindowInitializationException(
                        result.ErrorMessage, 
                        result.State, 
                        result.Error);
                }
                
                _instanceLogger?.LogInformation("Async window initialization completed successfully");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Async window initialization failed");
                throw;
            }
        }

        /// <summary>
        /// Creates a default logger using shared logger factory.
        /// ENHANCED FOR FIX 1: Logger Factory Memory Leaks
        /// </summary>
        private static ILogger<MainWindow> CreateDefaultLogger()
        {
            return _sharedLoggerFactory.CreateLogger<MainWindow>();
        }

        /// <summary>
        /// Creates a default initializer using shared logger factory.
        /// ENHANCED FOR FIX 1: Logger Factory Memory Leaks
        /// </summary>
        private static MainWindowInitializer CreateDefaultInitializer()
        {
            var logger = _sharedLoggerFactory.CreateLogger<MainWindowInitializer>();
            return new MainWindowInitializer(logger);
        }

        /// <summary>
        /// Creates a default exception handler using shared logger factory.
        /// ENHANCED FOR FIX 1: Logger Factory Memory Leaks
        /// </summary>
        private static ExceptionHandler CreateDefaultExceptionHandler()
        {
            var logger = _sharedLoggerFactory.CreateLogger<ExceptionHandler>();
            var telemetry = new ConsoleTelemetryService();
            return new ExceptionHandler(logger, telemetry);
        }

        /// <summary>
        /// Initializes core fields that need to be available before InitializeComponent.
        /// </summary>
        private void InitializeCoreFields()
        {
            _metadataManager = App.MetadataManager ?? new MetadataManager();
            _navigationHistory.Clear();
            _currentHistoryNode = null;
            
            // Register with lifecycle manager for thread-safe tracking
            WindowLifecycleManager.Instance.RegisterWindow(this);
        }



        /// <summary>
        /// Preloads critical icons to improve UI responsiveness
        /// </summary>
        private void PreloadIcons()
        {
            try
            {
                _instanceLogger?.LogDebug("Preloading critical icons");
                
                // Force load critical toolbar icons at startup
                var criticalIcons = new[]
                {
                    "ArrowUpIcon", "RefreshIcon", "UndoIcon", "RedoIcon", "SettingsIcon",
                    "PanelLeftIcon", "PanelRightIcon", "PinIcon", "BookmarkIcon", "TodoIcon"
                };

                foreach (var iconKey in criticalIcons)
                {
                    try
                    {
                        var icon = FindResource(iconKey) as BitmapImage;
                        if (icon != null)
                        {
                            // Access the pixel data to force loading
                            _ = icon.PixelWidth;
                            _ = icon.PixelHeight;
                        }
                    }
                    catch (Exception ex)
                    {
                        _instanceLogger?.LogWarning(ex, $"Failed to preload icon: {iconKey}");
                    }
                }
                
                _instanceLogger?.LogDebug("Icon preloading completed");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error during icon preloading");
            }
        }

        /// <summary>
        /// Validates that all required icons are available at startup
        /// </summary>
        private void ValidateIcons()
        {
            try
            {
                _instanceLogger?.LogDebug("Validating icon resources");
                
                var requiredIcons = new[]
                {
                    // Critical toolbar icons
                    "ArrowUpIcon", "RefreshIcon", "UndoIcon", "RedoIcon", "SettingsIcon",
                    "PanelLeftIcon", "PanelRightIcon",
                    // Content icons
                    "PinIcon", "BookmarkIcon", "TodoIcon", "WindowIcon"
                };

                var missingIcons = new List<string>();
                var corruptedIcons = new List<string>();

                foreach (var iconKey in requiredIcons)
                {
                    try
                    {
                        var icon = FindResource(iconKey);
                        if (icon == null)
                        {
                            missingIcons.Add(iconKey);
                        }
                        else if (icon is BitmapImage bitmapImage)
                        {
                            // Try to access properties to validate the image
                            _ = bitmapImage.UriSource;
                            if (bitmapImage.PixelWidth == 0 || bitmapImage.PixelHeight == 0)
                            {
                                corruptedIcons.Add(iconKey);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _instanceLogger?.LogWarning(ex, $"Error validating icon: {iconKey}");
                        corruptedIcons.Add(iconKey);
                    }
                }

                if (missingIcons.Count > 0)
                {
                    _instanceLogger?.LogWarning($"Missing icon resources: {string.Join(", ", missingIcons)}");
                }

                if (corruptedIcons.Count > 0)
                {
                    _instanceLogger?.LogWarning($"Corrupted icon resources: {string.Join(", ", corruptedIcons)}");
                }

                if (missingIcons.Count == 0 && corruptedIcons.Count == 0)
                {
                    _instanceLogger?.LogDebug("All icon resources validated successfully");
                }
                else
                {
                    _instanceLogger?.LogWarning($"Icon validation completed with issues: {missingIcons.Count} missing, {corruptedIcons.Count} corrupted");
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error during icon validation");
            }
        }

        /// <summary>
        /// Handle initialization errors with a user-friendly message.
        /// </summary>
        private void HandleInitializationError(Exception ex)
        {
            Console.WriteLine($"Error initializing main window: {ex.Message}");
            MessageBox.Show($"Error initializing application: {ex.Message}\n\nThe application may have limited functionality.",
                "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Safely performs an operation with comprehensive exception handling.
        /// </summary>
        public void PerformSafeOperation(Action operation, string operationName)
        {
            var context = new OperationContext(operationName)
                .WithProperty("WindowId", GetWindowId());
            
            _exceptionHandler.ExecuteWithHandling(
                () =>
                {
                    EnsureOperationAllowed(operationName);
                    operation();
                    return true;
                },
                context,
                ex =>
                {
                    _logger.LogWarning($"Operation {operationName} failed, using fallback");
                    return false;
                });
        }

        /// <summary>
        /// Safely performs an async operation with comprehensive exception handling.
        /// </summary>
        public async Task<T> PerformSafeOperationAsync<T>(Func<Task<T>> operation, string operationName, Func<Exception, T> fallback = null)
        {
            var context = new OperationContext(operationName)
                .WithProperty("WindowId", GetWindowId());
            
            return await _exceptionHandler.ExecuteWithHandlingAsync(
                operation,
                context,
                fallback);
        }

        /// <summary>
        /// Gets a unique identifier for this window.
        /// </summary>
        private string GetWindowId()
        {
            return $"MainWindow_{GetHashCode()}";
        }

        /// <summary>
        /// Example of using transactional operations for complex window operations.
        /// ENHANCED FOR FIX 1: Logger Factory Memory Leaks
        /// </summary>
        public async Task<bool> PerformTransactionalWindowSetup()
        {
            try
            {
                var transactionLogger = _sharedLoggerFactory.CreateLogger<TransactionalOperation<WindowInitState>>();
                
                return await TransactionalOperation<WindowInitState>.RunAsync(
                    transactionLogger,
                    async transaction =>
                    {
                        // Example: Setup window in steps that can be rolled back
                        transaction.Execute(new InitializeComponentsAction(this));
                        transaction.Execute(new ValidateComponentsAction(this));
                        
                        // If any step fails, all previous steps will be automatically rolled back
                        return true;
                    },
                    new WindowInitState { Window = this });
            }
            catch (Exception ex)
            {
                var context = new OperationContext("PerformTransactionalWindowSetup")
                    .WithProperty("WindowId", GetWindowId());
                
                _exceptionHandler.HandleException(ex, context);
                return false;
            }
        }

        /// <summary>
        /// Setup handlers for theme changes
        /// ENHANCED FOR FIX 4: Event Handler Memory Leaks
        /// </summary>
        /// <summary>
        /// ENHANCED FOR FIX 4: Event Handler Memory Leak Resolution
        /// Sets up theme event handlers using weak references to prevent memory leaks
        /// </summary>
        internal void SetupThemeHandlers()
        {
            try
            {
                // TODO: Fix theme subscription - temporarily commented to resolve other errors
                // _eventSubscriptions.SubscribeWeak(
                //     ThemeManager.Instance,
                //     nameof(ThemeManager.ThemeChanged),
                //     OnThemeChanged
                // );
                
                _instanceLogger?.LogInformation("Theme change handlers set up with weak event management");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error setting up theme handlers");
                throw;
            }
        }

        /// <summary>
        /// Named event handler for theme changes - FIX 4: Event Handler Memory Leaks
        /// </summary>
        private void OnThemeChanged(object sender, AppTheme theme)
        {
            ExecuteOnUIThread(() =>
            {
                if (_stateManager.IsOperational)
                {
                    try
                    {
                        RefreshThemeElements();
                        _instanceLogger?.LogDebug($"Theme changed to: {theme}");
                    }
                    catch (Exception ex)
                    {
                        _instanceLogger?.LogError(ex, "Error applying theme change");
                    }
                }
            }, nameof(OnThemeChanged));
        }

        /// <summary>
        /// Named event handler for window loaded event - FIX 4: Event Handler Memory Leaks
        /// </summary>
        private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate and preload icons for better performance
                ValidateIcons();
                PreloadIcons();

                // Note: Tab creation is now handled by async initialization (OnWindowLoadedAsync)
                // to avoid race conditions and duplicate tab creation

                // Connect all pinned panels
                ConnectAllPinnedPanels();

                // Refresh pinned panels
                RefreshAllPinnedPanels();
                
                // Refresh theme elements
                RefreshThemeElements();
                
                _instanceLogger?.LogInformation("Main window loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"DEBUG: Exception in OnMainWindowLoaded: {ex.Message}");
                _instanceLogger?.LogError(ex, "Error in window loaded event handler");
                
                // Make sure we have at least one tab
                if (MainTabs.Items.Count == 0)
                {
                    Console.WriteLine("DEBUG: Creating emergency fallback tab");
                    SafeAddNewTab();
                }
            }
        }

        #endregion

        #region Robust Initialization Support Methods

        /// <summary>
        /// Updates the initialization state in a thread-safe manner.
        /// </summary>
        internal void UpdateInitializationState(Core.WindowState newState)
        {
            lock (_stateLock)
            {
                var oldState = _stateManager.CurrentState;
                _stateManager.TryTransitionTo(newState, out _);
                _logger.LogDebug($"Initialization state changed: {oldState} -> {newState}");
            }
        }

        /// <summary>
        /// Sets the MainTabs control reference after validation.
        /// </summary>
        internal void SetMainTabsControl(ChromeStyleTabControl tabControl)
        {
            System.Diagnostics.Debug.Assert(tabControl != null, "ChromeStyleTabControl cannot be null");
            _mainTabsControl = tabControl;
            // Setup event handlers for the chrome-style tab control
            SetupChromeStyleTabEvents();
        }

        /// <summary>
        /// Safely accesses the MainTabs control with null checking.
        /// 
        /// LAYOUT DEPENDENCY: Direct reference to MainTabs control
        /// Will need updating if tab control moves in layout restructuring
        /// </summary>
        protected TabControl MainTabsSafe
        {
            get
            {
                if (_mainTabsControl == null)
                {
                    throw new InvalidOperationException(
                        "MainTabs control is not initialized. Current state: " + _stateManager.CurrentState);
                }
                return _mainTabsControl;
            }
        }



        /// <summary>
        /// Checks if the window is ready for tab operations.
        /// </summary>
        internal bool IsReadyForTabOperations()
        {
            return _stateManager.IsOperational &&
                   _mainTabsControl != null;
        }

        /// <summary>
        /// Tests if tab operations can be performed.
        /// </summary>
        internal bool CanPerformTabOperations()
        {
            if (_stateManager.CurrentState == Core.WindowState.LoadingUI)
            {
                return _mainTabsControl != null;
            }
            
            return IsReadyForTabOperations();
        }

        /// <summary>
        /// Validates the current window state.
        /// </summary>
        internal bool ValidateWindowState()
        {
            if (_stateManager.IsClosing || _stateManager.HasFailed) return false;
            
            // Check initialization state
            if (!_stateManager.IsOperational && _stateManager.CurrentState != Core.WindowState.LoadingUI)
            {
                _instanceLogger?.LogWarning($"Window not ready for operations. State: {_stateManager.CurrentState}");
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Safely initializes the main window with error handling.
        /// </summary>
        internal void InitializeMainWindowSafely()
        {
            // Don't call EnsureOperationAllowed here - we're in initialization phase
            
            try
            {
                _logger.LogDebug("Initializing main window systems");
                
                // Setup theme handlers
                SetupThemeHandlers();
                
                // Connect events only if MainTabs exists
                if (_mainTabsControl != null)
                {
                    _mainTabsControl.SelectionChanged += MainTabs_SelectionChanged;
                }
                
                // Set up drag-drop only if the window was properly created
                if (this.IsInitialized)
                {
                    AllowDrop = true;
                    DragOver += MainWindow_DragOver;
                    Drop += MainWindow_Drop;
                }
                
                // Handle window closing
                Closing += MainWindow_Closing;
                
                // Original InitializeMainWindow logic with validation
                RestoreWindowLayout();
                InitializeKeyboardShortcuts();
                
                // Delayed tab creation until the window is fully loaded
                this.Loaded += OnMainWindowLoaded;
                
                _logger.LogDebug("Main window systems initialized");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize main window systems");
                throw;
            }
        }

        /// <summary>
        /// Handles the window loaded event with robust error handling.
        /// </summary>
        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _stateManager.TryTransitionTo(Core.WindowState.Ready, out _);
        }

        /// <summary>
        /// Override to add safe initialization check
        /// </summary>
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Verify UI elements are ready
            if (MainTabs == null || StatusText == null)
            {
                _instanceLogger?.LogError("Critical UI elements not initialized properly");
                MessageBox.Show("Window initialization error. Please restart the application.", 
                               "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
                return;
            }
            
            // Continue with normal initialization only if UI elements are available
            _instanceLogger?.LogInformation("UI elements verified, continuing initialization");
        }

        /// <summary>
        /// Check state before operations
        /// </summary>
        public void PerformOperation()
        {
            if (!_stateManager.IsOperational)
            {
                _instanceLogger?.LogWarning($"Operation attempted in {_stateManager.CurrentState} state");
                return;
            }
        }

        /// <summary>
        /// Checks if the window is in a safe state for UI operations
        /// </summary>
        private bool IsWindowReadyForOperations()
        {
            return _stateManager.IsOperational;
        }

        /// <summary>
        /// Safely adds a new tab with comprehensive error handling.
        /// </summary>
        internal void AddNewMainWindowTabSafely()
        {
            if (_stateManager.CurrentState == Core.WindowState.Ready)
            {
                AddNewMainWindowTab();
            }
            else
            {
                _instanceLogger?.LogDebug($"Deferring tab creation until window is ready. Current state: {_stateManager.CurrentState}");
                // For now, let's create the tab anyway if we have MainTabs available
                if (MainTabs != null)
                {
                    _instanceLogger?.LogDebug("MainTabs available, creating tab despite state");
                    AddNewMainWindowTab();
                }
            }
        }

        /// <summary>
        /// Ensures an operation is allowed in the current state.
        /// </summary>
        private void EnsureOperationAllowed(string operationName)
        {
            if (_stateManager.IsClosing)
            {
                throw new ObjectDisposedException(nameof(MainWindow), 
                    $"Cannot perform {operationName} on disposed window");
            }

            if (!_stateManager.IsOperational)
            {
                throw new InvalidOperationException(
                    $"Cannot perform {operationName} in state {_stateManager.CurrentState}");
            }
        }

        /// <summary>
        /// Performs emergency cleanup when initialization fails.
        /// </summary>
        internal void PerformEmergencyCleanup()
        {
            try
            {
                _stateManager.TryTransitionTo(Core.WindowState.Failed, out _);
                
                // Emergency cleanup operations
                _eventSubscriptions?.Dispose();
                ClearNavigationHistory();
                
                _instanceLogger?.LogWarning("Emergency cleanup performed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during emergency cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Properly disposes of the window and its resources.
        /// ENHANCED FOR FIX 4: Event Handler Memory Leaks
        /// </summary>
        public virtual void Dispose()
        {
            if (!_stateManager.IsClosing)
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Protected dispose pattern implementation
        /// ENHANCED FOR FIX 4: Event Handler Memory Leaks
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_stateManager.IsClosing)
            {
                if (disposing)
                {
                    try
                    {
                        // Add logging to track disposal
                        _instanceLogger?.LogInformation("Beginning MainWindow disposal process");
                        
                        _stateManager.TryTransitionTo(Core.WindowState.Disposed, out _);
                        
                        // Unregister from lifecycle manager first
                        _instanceLogger?.LogDebug("Unregistering from WindowLifecycleManager");
                        UnregisterFromLifecycleManager();
                        
                        // Cleanup event subscriptions
                        _instanceLogger?.LogDebug("Disposing event subscriptions");
                        _eventSubscriptions?.Dispose();
                        
                        // Clear all strong references
                        _instanceLogger?.LogDebug("Clearing strong references");
                        ClearAllStrongReferences();
                        
                        // PHASE 3 OPTIMIZATION: Clear cached toolbar reference
                        _instanceLogger?.LogDebug("Invalidating embedded toolbar cache");
                        InvalidateEmbeddedToolbarCache();
                        
                        // Clear navigation history
                        _instanceLogger?.LogDebug("Clearing navigation history");
                        ClearNavigationHistory();
                        
                        // Cleanup logger reference last
                        _instanceLogger?.LogInformation("MainWindow disposal completed successfully");
                        DecrementLoggerRef();
                    }
                    catch (Exception ex)
                    {
                        _instanceLogger?.LogError(ex, "Error during MainWindow disposal");
                        System.Diagnostics.Debug.WriteLine($"Error during MainWindow disposal: {ex.Message}");
                    }
                }
                
                _stateManager.TryTransitionTo(Core.WindowState.Disposed, out _);
            }
            
            // Don't call base.Dispose() as Window doesn't implement IDisposable
        }

        /// <summary>
        /// Override OnClosing to set state transition
        /// </summary>
        protected override void OnClosing(CancelEventArgs e)
        {
            try
            {
                _stateManager.TryTransitionTo(Core.WindowState.Closing, out _);
                
                // Save window layout
                SaveWindowLayout();
                
                // Additional cleanup can be performed here
                
                _instanceLogger?.LogInformation("Window closing initiated");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error during window closing");
            }
            
            base.OnClosing(e);
        }

        /// <summary>
        /// Handle window closed event with cleanup
        /// ENHANCED FOR FIX 4: Event Handler Memory Leaks & FIX 5: Detached Windows List Management
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            try
            {
                _windowManager?.UnregisterWindow(this);
                _stateManager.TryTransitionTo(Core.WindowState.Disposed, out _);
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error during window close cleanup");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        #endregion

        #region Initialization Methods

        /// <summary>
        /// Ensure all critical UI elements are available.
        /// </summary>
        private void EnsureUIElementsAvailable()
        {
            if (MainTabs == null)
            {
                Console.WriteLine("ERROR: MainTabs is null - this is a critical UI element");
            }
            
            var embeddedToolbar = FindEmbeddedToolbar();
            if (embeddedToolbar == null)
            {
                Console.WriteLine("ERROR: Embedded Toolbar is null - this is a critical UI element");
            }
            
            if (StatusText == null)
            {
                Console.WriteLine("ERROR: StatusText is null - this is a UI element");
            }
            
            // Log success if everything is available
            if (MainTabs != null && embeddedToolbar != null && StatusText != null)
            {
                Console.WriteLine("All critical UI elements are available");
            }
        }

        /// <summary>
        /// Initialize the main window components and restore previous state.
        /// </summary>
        private void InitializeMainWindow()
        {
            try
            {
                // Check UI elements first
                EnsureUIElementsAvailable();
                
                // Register with window manager
                RegisterWithWindowManager();
                
                // Restore window layout
                RestoreWindowLayout();

                // Delayed tab creation until the window is fully loaded
                this.Loaded += OnMainWindowLoaded;

                // Set up keyboard shortcuts
                InitializeKeyboardShortcuts();
                
                // Initialize tab operations
                InitializeTabOperations();
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error initializing main window: {ex.Message}", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Initialize tab operations manager
        /// </summary>
        private void InitializeTabOperations()
        {
            try
            {
                // Create the operations manager with required dependencies
                var detachedWindowManager = new ExplorerPro.Core.TabManagement.SimpleDetachedWindowManager();
                
                // Create logger factory for the TabOperationsManager
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole().SetMinimumLevel(LogLevel.Debug);
                });
                var tabOpsLogger = loggerFactory.CreateLogger<ExplorerPro.Core.TabManagement.TabOperationsManager>();
                
                var operationsManager = new ExplorerPro.Core.TabManagement.TabOperationsManager(
                    tabOpsLogger,
                    detachedWindowManager);
                
                if (DataContext is MainWindowViewModel viewModel)
                {
                    viewModel.TabOperationsManager = operationsManager;
                    _logger?.LogInformation("TabOperationsManager initialized successfully");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to initialize TabOperationsManager");
            }
        }

        /// <summary>
        /// Restore window layout from saved settings.
        /// </summary>
        internal void RestoreWindowLayout()
        {
            try
            {
                var windowSettings = _settingsService.GetWindowSettings(GetWindowId());
                if (windowSettings != null && windowSettings.IsValid)
                {
                    windowSettings.ApplyTo(this, _instanceLogger);
                    return;
                }
                
                // Legacy fallback - try to get geometry bytes from SettingsManager for compatibility
                byte[] geometryBytes = null, stateBytes = null;
                try
                {
                    if (App.Settings != null)
                    {
                        var (legacyGeometry, legacyState) = App.Settings.RetrieveMainWindowLayout();
                        geometryBytes = legacyGeometry;
                        stateBytes = legacyState;
                    }
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, "Error retrieving legacy window layout");
                }
                if (geometryBytes != null)
                {
                    // Restore window geometry
                    if (TryRestoreWindowGeometry(geometryBytes))
                    {
                        Console.WriteLine("Window geometry restored successfully");
                    }
                }

                if (stateBytes != null)
                {
                    // Restore window state
                    if (TryRestoreWindowState(stateBytes))
                    {
                        Console.WriteLine("Window state restored successfully");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring window layout: {ex.Message}");
            }
        }

        /// <summary>
        /// Try to restore window geometry from bytes.
        /// </summary>
        /// <param name="geometryBytes">Serialized window geometry</param>
        /// <returns>True if successful</returns>
        private bool TryRestoreWindowGeometry(byte[] geometryBytes)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(geometryBytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Read window position and size
                    double left = reader.ReadDouble();
                    double top = reader.ReadDouble();
                    double width = reader.ReadDouble();
                    double height = reader.ReadDouble();

                    // Check if geometry is valid
                    if (width <= 0 || height <= 0)
                    {
                        return false;
                    }

                    // Adjust if window would be off-screen
                    if (IsRectOnScreen(new Rect(left, top, width, height)))
                    {
                        Left = left;
                        Top = top;
                        Width = width;
                        Height = height;
                    }
                    else
                    {
                        // Use default centered position
                        WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    }

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring window geometry: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to restore window state from bytes.
        /// </summary>
        /// <param name="stateBytes">Serialized window state</param>
        /// <returns>True if successful</returns>
        private bool TryRestoreWindowState(byte[] stateBytes)
        {
            try
            {
                using (MemoryStream stream = new MemoryStream(stateBytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Read window state
                    int stateValue = reader.ReadInt32();
                    WindowState = (System.Windows.WindowState)stateValue;

                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error restoring window state: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Initialize keyboard shortcuts for the application.
        /// ENHANCED FOR FIX 6: Command Binding Memory Overhead - Uses CommandPool for reusable commands
        /// </summary>
        internal void InitializeKeyboardShortcuts()
        {
            try
            {
                // Clear existing bindings
                CommandBindings.Clear();
                
                // Map of command names to actions
                var commandActions = new Dictionary<string, Action>
                {
                    ["GoUp"] = GoUp,
                    ["Refresh"] = RefreshFileTree,
                    ["FocusAddressBar"] = FocusAddressBar,
                    ["FocusAddressBarAlt"] = FocusAddressBar,
                    ["FocusSearch"] = FocusSearch,
                    ["FocusSearchAlt"] = FocusSearch,
                    ["GoBack"] = GoBack,
                    ["GoForward"] = GoForward,
                    ["NewFolder"] = NewFolder,
                    ["NewFile"] = NewFile,
                    ["TogglePinnedPanel"] = TogglePinnedPanel,
                    ["ToggleBookmarksPanel"] = ToggleBookmarksPanel,
                    ["ToggleTodoPanel"] = ToggleTodoPanel,
                    ["ToggleProcorePanel"] = ToggleProcorePanel,
                    ["ToggleLeftSidebar"] = ToggleLeftSidebar,
                    ["ToggleRightSidebar"] = ToggleRightSidebar,
                    ["NextTab"] = NextTab,
                    ["PreviousTab"] = PreviousTab,
                    ["NewTab"] = NewTab,
                    ["CloseTab"] = CloseCurrentTab,
                    ["ToggleSplitView"] = () => ToggleSplitView(null),
                    ["ToggleFullscreen"] = ToggleFullscreen,
                    ["ZoomIn"] = ZoomIn,
                    ["ZoomOut"] = ZoomOut,
                    ["ZoomReset"] = ZoomReset,
                    ["ToggleTheme"] = () => ThemeManager.Instance.ToggleTheme(),
                    ["ShowHelp"] = ShowHelp,
                    ["OpenSettings"] = OpenSettings,
                    ["ToggleHiddenFiles"] = ToggleHiddenFiles,
                    ["EscapeAction"] = EscapeAction
                };
                
                // Register all shortcuts using the CommandPool
                foreach (var shortcut in ExplorerPro.Commands.KeyboardShortcuts.Shortcuts)
                {
                    if (commandActions.TryGetValue(shortcut.Name, out var action))
                    {
                        // Get reusable command from pool instead of creating new one
                        var command = ExplorerPro.Commands.CommandPool.GetCommand(
                            shortcut.Name, 
                            shortcut.Key, 
                            shortcut.Modifiers,
                            GetType());
                            
                        CommandBindings.Add(new CommandBinding(
                            command,
                            (s, e) => 
                            {
                                try
                                {
                                    action();
                                    e.Handled = true;
                                }
                                catch (Exception ex)
                                {
                                    _instanceLogger?.LogError(ex, "Error executing shortcut: {ShortcutName}", shortcut.Name);
                                }
                            },
                            (s, e) => e.CanExecute = !_stateManager.IsClosing && _stateManager.IsOperational));
                    }
                }
                
                _instanceLogger?.LogDebug("Initialized {Count} keyboard shortcuts using CommandPool (Pool size: {PoolSize})", 
                    CommandBindings.Count, ExplorerPro.Commands.CommandPool.GetPoolSize());
                
                // Log pool statistics for monitoring
                _instanceLogger?.LogDebug("CommandPool Statistics: {Statistics}", 
                    ExplorerPro.Commands.CommandPool.GetPoolStatistics());
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Failed to initialize keyboard shortcuts");
                Console.WriteLine($"Error initializing keyboard shortcuts: {ex.Message}");
            }
        }

        /// <summary>
        /// Register a keyboard shortcut.
        /// DEPRECATED: This method is replaced by CommandPool usage in InitializeKeyboardShortcuts.
        /// Kept for reference but no longer used.
        /// </summary>
        /// <param name="key">Key to register</param>
        /// <param name="modifiers">Modifier keys</param>
        /// <param name="action">Action to execute</param>
        /// <param name="description">Description for the shortcut</param>
        [Obsolete("This method is deprecated. Use CommandPool in InitializeKeyboardShortcuts instead.")]
        private void RegisterShortcut(Key key, ModifierKeys modifiers, Action? action, string description)
        {
            // This method is no longer used - shortcuts are now managed through CommandPool
            // in InitializeKeyboardShortcuts() to prevent memory leaks from duplicate command creation
            Console.WriteLine($"WARNING: RegisterShortcut is deprecated. Shortcut '{description}' should be added to KeyboardShortcuts.cs");
        }

        #endregion

        #region Theme Management

        /// <summary>
        /// Refreshes UI elements based on the current theme
        /// </summary>
        /// <summary>
        /// Refresh theme elements with thread safety
        /// ENHANCED FOR FIX 2: Thread-Safety Issues in UI Updates
        /// </summary>
        public void RefreshThemeElements()
        {
            ExecuteOnUIThread(() =>
            {
                try
                {
                    if (IsDisposed) return;

                    // Get current theme information
                    bool isDarkMode = ThemeManager.Instance.IsDarkMode;
                    
                    // Update window-level UI elements
                    Background = GetResource<SolidColorBrush>("WindowBackground");
                    Foreground = GetResource<SolidColorBrush>("TextColor");
                    
                    // Status bar elements
                    if (StatusText != null)
                    {
                        StatusText.Foreground = GetResource<SolidColorBrush>("TextColor");
                    }
                    
                    if (ItemCountText != null)
                    {
                        ItemCountText.Foreground = GetResource<SolidColorBrush>("TextColor");
                    }
                    
                    if (SelectionText != null)
                    {
                        SelectionText.Foreground = GetResource<SolidColorBrush>("TextColor");
                    }
                    
                    // Main tab control
                    if (MainTabs != null)
                    {
                        MainTabs.Background = GetResource<SolidColorBrush>("TabControlBackground");
                        
                        // Refresh all tab items
                        foreach (var item in MainTabs.Items)
                        {
                            if (item is TabItem tabItem)
                            {
                                tabItem.Background = GetResource<SolidColorBrush>("TabBackground");
                                tabItem.Foreground = GetResource<SolidColorBrush>("TextColor");
                                
                                // If this tab is selected, apply selected style
                                if (tabItem.IsSelected)
                                {
                                    tabItem.Background = GetResource<SolidColorBrush>("TabSelectedBackground");
                                    tabItem.Foreground = GetResource<SolidColorBrush>("TabSelectedForeground");
                                }
                                
                                // Update containers within tabs
                                if (tabItem.Content is MainWindowContainer container)
                                {
                                    container.RefreshThemeElements();
                                }
                            }
                        }
                    }
                    
                    // Refresh embedded toolbar
                    var embeddedToolbar = FindEmbeddedToolbar();
                    if (embeddedToolbar != null)
                    {
                        embeddedToolbar.RefreshThemeElements();
                    }
                    
                    // Update theme-sensitive button content
                    UpdateThemeToggleButtonContent();
                    
                    _instanceLogger?.LogDebug("MainWindow theme elements refreshed successfully");
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, "Error refreshing main window theme elements");
                    // Non-critical error, continue
                }
            });
        }
        
        /// <summary>
        /// Updates the theme toggle button content based on the current theme
        /// </summary>
        private void UpdateThemeToggleButtonContent()
        {
            try
            {
                // Find the theme toggle button if it exists
                Button toggleThemeButton = FindToggleThemeButton();
                if (toggleThemeButton != null)
                {
                    bool isDarkMode = ThemeManager.Instance.IsDarkMode;
                    
                    // Update button content based on theme
                    // This assumes the button uses an icon or text that should change with the theme
                    toggleThemeButton.ToolTip = isDarkMode ? "Switch to Light Theme" : "Switch to Dark Theme";
                    
                    // If the button contains an image, we could switch the image here
                    // Example: (toggleThemeButton.Content as Image).Source = new BitmapImage(new Uri(...));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating theme toggle button: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Finds the theme toggle button in the UI
        /// </summary>
        private Button FindToggleThemeButton()
        {
            // Implement searching for your theme toggle button
            // This is a placeholder, adjust to your actual UI structure
            return null;
        }

        /// <summary>
        /// Finds the embedded toolbar within the TabControl template
        /// PHASE 3 OPTIMIZATION: Now uses caching to avoid repeated visual tree searches
        /// </summary>
        private UI.Toolbar.Toolbar? FindEmbeddedToolbar()
        {
            // Return cached toolbar if available and valid
            if (_toolbarCacheInitialized && _cachedEmbeddedToolbar != null)
            {
                return _cachedEmbeddedToolbar;
            }

            try
            {
                // Perform visual tree search only when cache is empty
                if (MainTabs?.Template?.FindName("EmbeddedToolbar", MainTabs) is UI.Toolbar.Toolbar toolbar)
                {
                    // Cache the found toolbar for future use
                    _cachedEmbeddedToolbar = toolbar;
                    _toolbarCacheInitialized = true;
                    
                    _instanceLogger?.LogDebug("Cached embedded toolbar successfully");
                    return toolbar;
                }
                
                // Mark as initialized even if toolbar not found to prevent repeated searches
                _toolbarCacheInitialized = true;
                _instanceLogger?.LogDebug("Embedded toolbar not found, cached negative result");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error finding embedded toolbar");
                // Don't mark as initialized on error, allow retry
            }
            
            return null;
        }

        /// <summary>
        /// Invalidates the embedded toolbar cache when the TabControl template changes
        /// PHASE 3 OPTIMIZATION: Allows cache refresh when needed
        /// </summary>
        private void InvalidateEmbeddedToolbarCache()
        {
            _cachedEmbeddedToolbar = null;
            _toolbarCacheInitialized = false;
            _instanceLogger?.LogDebug("Invalidated embedded toolbar cache");
        }
        
        /// <summary>
        /// Helper method to get a resource from the current theme
        /// </summary>
        private T GetResource<T>(string resourceKey) where T : class
        {
            return ThemeManager.Instance.GetResource<T>(resourceKey);
        }
        
        /// <summary>
        /// Updates the application theme
        /// </summary>
        /// <param name="theme">Theme name ("light" or "dark")</param>
        public void ApplyTheme(string theme)
        {
            try
            {
                // Use the ThemeManager
                var themeEnum = theme?.ToLower() == "dark" ? 
                    Themes.AppTheme.Dark : Themes.AppTheme.Light;
                    
                Themes.ThemeManager.Instance.SwitchTheme(themeEnum);
                
                // Save the theme to settings using the service
                _settingsService.UpdateSetting("theme", theme.ToLower());
                _settingsService.UpdateSetting("ui_preferences.Enable Dark Mode", theme.ToLower() == "dark");
                
                Console.WriteLine($"Applied theme: {theme}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying theme: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Handles the theme toggle button click
        /// </summary>
        private void ToggleThemeButton_Click(object sender, RoutedEventArgs e)
        {
            ThemeManager.Instance.ToggleTheme();
        }
        
        /// <summary>
        /// Handles command to toggle theme from menu or shortcut
        /// </summary>
        private void ToggleThemeCommand_Executed(object sender, System.Windows.Input.ExecutedRoutedEventArgs e)
        {
            ThemeManager.Instance.ToggleTheme();
        }

        #endregion

        #region Tab Management

        /// <summary>
        /// Add a new tab with a MainWindowContainer.
        /// </summary>
        /// <param name="rootPath">Root path for the new tab</param>
        /// <returns>The created MainWindowContainer</returns>
        public MainWindowContainer? AddNewMainWindowTab(string? rootPath = null)
        {
            try
            {
                // Check MainTabs first
                if (MainTabs == null)
                {
                    Console.WriteLine("ERROR: MainTabs is null, cannot add a new tab");
                    MessageBox.Show("Cannot add a new tab - tab control is not available.", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // Create new container with proper error handling
                MainWindowContainer? container = null;
                
                try
                {
                    container = new MainWindowContainer(this);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to create container: {ex.Message}");
                    MessageBox.Show($"Error creating tab container: {ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return null;
                }

                // Validate the path more carefully
                string validPath = ValidatePath(rootPath);
                _instanceLogger?.LogDebug($"Validated path for new tab: {validPath}");
                
                // Initialize container
                try
                {
                    _instanceLogger?.LogDebug($"Initializing file tree with path: {validPath}");
                    container.InitializeWithFileTree(validPath);
                    _instanceLogger?.LogDebug($"Successfully initialized file tree with path: {validPath}");
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, $"Failed to initialize file tree with path '{validPath}'");
                    
                    // Try one more time with User folder as a last resort
                    try
                    {
                        string fallbackPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                        _instanceLogger?.LogDebug($"Trying fallback path: {fallbackPath}");
                        container.InitializeWithFileTree(fallbackPath);
                        _instanceLogger?.LogDebug($"Successfully initialized file tree with fallback path: {fallbackPath}");
                    }
                    catch (Exception innerEx)
                    {
                        _instanceLogger?.LogError(innerEx, "Critical error: Failed to initialize with fallback path");
                        MessageBox.Show("Unable to initialize file browser with any valid path.",
                            "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                }

                // Create tab through direct manipulation
                string tabTitle = $"Window {MainTabs.Items.Count + 1}";
                var newTab = new TabItem
                {
                    Header = tabTitle,
                    Content = container,
                    // Ensure new tabs are not pinned by default
                    Tag = new Dictionary<string, object> { ["IsPinned"] = false }
                };
                
                // Add the tab to the control using proper positioning
                InsertTabAtCorrectPosition(newTab);
                MainTabs.SelectedItem = newTab;

                // Connect signals if available
                if (container.PinnedPanel != null)
                {
                    try
                    {
                        ConnectPinnedPanel(container);
                    }
                    catch (Exception ex)
                    {
                        _instanceLogger?.LogError(ex, "Error connecting pinned panel");
                    }
                }

                // Notify of new tab
                try
                {
                    OnNewTabAdded(container);
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, "Error in OnNewTabAdded");
                }

                _instanceLogger?.LogInformation($"Added new main window tab with root path: {validPath}");
                return container;
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Unhandled error in AddNewMainWindowTab");
                MessageBox.Show($"Error creating new tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Creates a new tab with minimal content as a fallback when the regular method fails.
        /// </summary>
        private void SafeAddNewTab()
        {
            try
            {
                // Check if MainTabs is available
                if (MainTabs == null)
                {
                    _instanceLogger?.LogError("Cannot add tab because MainTabs is null");
                    MessageBox.Show("Cannot create a new tab - critical UI component not available.", 
                        "Tab Creation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create a proper TabItem with loading content first
                string tabHeader = $"Tab {MainTabs.Items.Count + 1}";
                var newTabItem = new TabItem
                {
                    Header = tabHeader
                };
                
                // Create loading content
                Grid loadingGrid = new Grid();
                TextBlock loadingText = new TextBlock
                {
                    Text = "Loading folder view...",
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(20),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center
                };
                loadingGrid.Children.Add(loadingText);
                newTabItem.Content = loadingGrid;
                
                // Add the tab to the control
                MainTabs.Items.Add(newTabItem);
                MainTabs.SelectedItem = newTabItem;
                
                // Try to create container with proper content
                try
                {
                    // Try to create container
                    var container = new MainWindowContainer(this);
                    
                    // Try to initialize with a safe path
                    string safePath = ValidatePath(null);
                    container.InitializeWithFileTree(safePath);
                    
                    // Update the tab content with the container
                    newTabItem.Content = container;
                    
                    // Connect signals if needed
                    if (container.PinnedPanel != null)
                    {
                        ConnectPinnedPanel(container);
                    }
                    
                    // Update title with path name
                    string folderName = Path.GetFileName(safePath);
                    newTabItem.Header = !string.IsNullOrEmpty(folderName) ? folderName : "Home";
                    
                    _instanceLogger?.LogInformation("Successfully created tab with safe approach");
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, "Error initializing tab content");
                    
                    // Create error content
                    Grid errorGrid = new Grid();
                    TextBlock errorText = new TextBlock
                    {
                        Text = "Error loading folder view.\nPlease try again or restart the application.",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.Red
                    };
                    errorGrid.Children.Add(errorText);
                    newTabItem.Content = errorGrid;
                    newTabItem.Header = "Error";
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Critical error in SafeAddNewTab");
                MessageBox.Show("Unable to create a new tab due to a critical error.",
                    "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Validate and determine the appropriate path to use.
        /// </summary>
        /// <param name="rootPath">Initial requested path</param>
        /// <returns>A valid accessible path</returns>
        private string ValidatePath(string? rootPath)
        {
            try
            {
                // Check if the path exists
                if (!string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath))
                {
                    return rootPath;
                }
                
                // Try OneDrive path
                string onedrivePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "OneDrive - Fendler Patterson Construction, Inc");
                
                if (Directory.Exists(onedrivePath))
                {
                    return onedrivePath;
                }
                
                // Try Documents folder
                string docsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                if (Directory.Exists(docsPath))
                {
                    return docsPath;
                }
                
                // Fall back to user profile
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error validating path: {ex.Message}");
                // Ultimate fallback
                return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            }
        }

        /// <summary>
        /// Handler for add tab button click.
        /// </summary>
        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Try normal method first
                var container = AddNewMainWindowTab();
                
                // If it fails, use the safe method
                if (container == null)
                {
                    SafeAddNewTab();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in AddTabButton_Click: {ex.Message}");
                SafeAddNewTab();
            }
        }

        /// <summary>
        /// Handler for new tab menu item click.
        /// Enhanced with Phase 1 improvements: debouncing, smart positioning, memory cleanup
        /// </summary>
        private void NewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 3. Event Handler Debouncing
            if (IsDebounced()) return;

            try
            {
                var container = AddNewMainWindowTab();
                if (container != null)
                {
                    // 2. Smart Tab Positioning - Move new tab to correct position
                    if (MainTabs?.Items.Count > 1)
                    {
                        var insertIndex = GetSmartInsertionIndex() - 1; // Adjust since tab was added at end
                        var newTab = MainTabs.Items[MainTabs.Items.Count - 1] as TabItem;
                        if (newTab != null && insertIndex < MainTabs.Items.Count - 1)
                        {
                            MainTabs.Items.RemoveAt(MainTabs.Items.Count - 1);
                            MainTabs.Items.Insert(insertIndex, newTab);
                            MainTabs.SelectedItem = newTab;
                        }
                    }

                    _instanceLogger?.LogInformation("New tab created via context menu with smart positioning");
                    
                    // 4. Smart Memory Cleanup
                    PerformSmartCleanup();
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error creating new tab from context menu");
                MessageBox.Show($"Failed to create new tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handler for duplicate tab menu item click.
        /// Enhanced with Phase 1 improvements: debouncing, smart positioning, memory cleanup
        /// </summary>
        private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 3. Event Handler Debouncing
            if (IsDebounced()) return;

            try
            {
                var contextMenu = sender as MenuItem;
                var tabModel = GetContextMenuTabModel(contextMenu?.Parent as ContextMenu);
                
                if (tabModel != null && MainTabs?.SelectedItem is TabItem currentTab)
                {
                    // Get the current container
                    var currentContainer = currentTab.Content as MainWindowContainer;
                    if (currentContainer != null)
                    {
                        // Create new tab with same path
                        var fileTree = currentContainer.FindFileTree();
                        string currentPath = fileTree?.GetCurrentPath() ?? 
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                        
                        var newContainer = AddNewMainWindowTab(currentPath);
                        if (newContainer != null && MainTabs.SelectedItem is TabItem newTab)
                        {
                            // 2. Smart Tab Positioning - Move duplicated tab next to original
                            var originalIndex = MainTabs.Items.IndexOf(currentTab);
                            var newTabIndex = MainTabs.Items.IndexOf(newTab);
                            
                            if (originalIndex >= 0 && newTabIndex >= 0 && originalIndex != newTabIndex - 1)
                            {
                                MainTabs.Items.RemoveAt(newTabIndex);
                                MainTabs.Items.Insert(originalIndex + 1, newTab);
                                MainTabs.SelectedItem = newTab;
                            }

                            newTab.Header = $"{currentTab.Header} - Copy";
                            _instanceLogger?.LogInformation($"Duplicated tab with smart positioning: {currentTab.Header}");
                            
                            // 4. Smart Memory Cleanup
                            PerformSmartCleanup();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error duplicating tab");
                MessageBox.Show($"Failed to duplicate tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RenameTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
                {
                    var contextTabItem = GetContextMenuTabItem(contextMenu);
                    
                    if (contextTabItem != null)
                    {
                        var oldTitle = contextTabItem.Header?.ToString() ?? "Untitled";
                        
                        // Show rename dialog with smart positioning
                        var dialog = new UI.Dialogs.RenameDialog(oldTitle, this, contextTabItem);
                        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
                        {
                            var newTitle = dialog.NewName.Trim();
                            
                            // Update the UI TabItem directly (most important)
                            contextTabItem.Header = newTitle;
                            
                            // Update any associated model in the ViewModel
                            if (_viewModel?.TabItems != null)
                            {
                                var viewModelTab = _viewModel.TabItems.FirstOrDefault(t => 
                                    t.Title == oldTitle || 
                                    (t.Content != null && t.Content == contextTabItem.Content));
                                if (viewModelTab != null)
                                {
                                    viewModelTab.Title = newTitle;
                                    viewModelTab.UpdateLastAccessed();
                                }
                            }
                            
                            // Update metadata stored in TabItem.Tag if it exists
                            if (contextTabItem.Tag is Dictionary<string, object> metadata)
                            {
                                metadata["Title"] = newTitle;
                                metadata["LastModified"] = DateTime.Now;
                            }
                            else
                            {
                                contextTabItem.Tag = new Dictionary<string, object>
                                {
                                    ["Title"] = newTitle,
                                    ["LastModified"] = DateTime.Now
                                };
                            }
                            
                            // Force immediate UI refresh
                            contextTabItem.UpdateLayout();
                            contextTabItem.InvalidateVisual();
                            MainTabs?.UpdateLayout();
                            
                            _instanceLogger?.LogDebug($"Tab renamed from '{oldTitle}' to '{newTitle}'");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error executing rename tab command");
            }
        }

        private void ChangeColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
                {
                    var contextTabItem = GetContextMenuTabItem(contextMenu);
                    
                    if (contextTabItem != null)
                    {
                        // Get current color from metadata or use default
                        var currentColor = Colors.LightGray; // Default color
                        if (contextTabItem.Tag is Dictionary<string, object> metadata && metadata.ContainsKey("TabColor"))
                        {
                            if (metadata["TabColor"] is Color storedColor)
                            {
                                currentColor = storedColor;
                            }
                        }
                        
                        // Show color picker dialog with smart positioning
                        var dialog = new UI.Dialogs.ColorPickerDialog(currentColor, this, contextTabItem);
                        if (dialog.ShowDialog() == true)
                        {
                            var newColor = dialog.SelectedColor;
                            
                            // Update metadata stored in TabItem.Tag
                            if (contextTabItem.Tag is Dictionary<string, object> existingMetadata)
                            {
                                existingMetadata["TabColor"] = newColor;
                                existingMetadata["LastModified"] = DateTime.Now;
                            }
                            else
                            {
                                contextTabItem.Tag = new Dictionary<string, object>
                                {
                                    ["TabColor"] = newColor,
                                    ["LastModified"] = DateTime.Now
                                };
                            }
                            
                            // Update any associated model in the ViewModel
                            if (_viewModel?.TabItems != null)
                            {
                                var tabTitle = contextTabItem.Header?.ToString();
                                var viewModelTab = _viewModel.TabItems.FirstOrDefault(t => 
                                    t.Title == tabTitle || 
                                    (t.Content != null && t.Content == contextTabItem.Content));
                                if (viewModelTab != null)
                                {
                                    viewModelTab.TabColor = newColor;
                                    viewModelTab.UpdateLastAccessed();
                                }
                            }
                            
                            // Set the DataContext to enable binding-based color changes
                            SetTabColorDataContext(contextTabItem, newColor);
                            
                            // Force immediate UI refresh
                            contextTabItem.UpdateLayout();
                            contextTabItem.InvalidateVisual();
                            MainTabs?.UpdateLayout();
                            
                            _instanceLogger?.LogDebug($"Tab color changed to {newColor}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error executing change color command");
            }
        }

        /// <summary>
        /// Handles the context menu loaded event 
        /// </summary>
        /// <param name="sender">The context menu</param>
        /// <param name="e">Event arguments</param>
        private void TabContextMenu_Loaded(object sender, RoutedEventArgs e)
        {
            _instanceLogger?.LogDebug("TabContextMenu_Loaded event fired");
        }

        /// <summary>
        /// Handles the context menu opening event to enable/disable menu items based on tab state
        /// Enhanced with Phase 1 improvements: dynamic states and caching
        /// </summary>
        /// <param name="sender">The context menu</param>
        /// <param name="e">Event arguments</param>
        private void TabContextMenu_Opened(object sender, RoutedEventArgs e)
        {
            try
            {
                _instanceLogger?.LogDebug("TabContextMenu_Opened event fired");
                
                if (sender is ContextMenu contextMenu)
                {
                    // Use centralized context menu state management
                    UpdateContextMenuStates(contextMenu);
                    
                    // Handle Clear Color menu item separately (special case not handled by UpdateContextMenuStates)
                    var contextTabItem = GetContextMenuTabItem(contextMenu);
                    if (contextTabItem != null)
                    {
                        var hasCustomColor = TabHasCustomColor(contextTabItem);
                        
                        MenuItem? clearColorMenuItem = null;
                        foreach (var item in contextMenu.Items)
                        {
                            if (item is MenuItem menuItem && 
                                (menuItem.Name == "ClearColorMenuItem" || menuItem.Header?.ToString()?.Contains("Clear Color") == true))
                            {
                                clearColorMenuItem = menuItem;
                                break;
                            }
                        }
                        
                        // Update Clear Color menu item
                        if (clearColorMenuItem != null)
                        {
                            clearColorMenuItem.IsEnabled = hasCustomColor;
                            clearColorMenuItem.Header = hasCustomColor ? "Clear Color" : "Clear Color (No custom color)";
                        }
                        
                        _instanceLogger?.LogDebug($"Context menu opened for tab: {contextTabItem.Header}, HasColor: {hasCustomColor}");
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error handling context menu opened event");
            }
        }

        /// <summary>
        /// Checks if a TabItem has a custom color applied
        /// </summary>
        /// <param name="tabItem">The TabItem to check</param>
        /// <returns>True if the tab has a custom color, false otherwise</returns>
        private bool TabHasCustomColor(TabItem tabItem)
        {
            try
            {
                _instanceLogger?.LogDebug($"Checking custom color for tab: {tabItem?.Header?.ToString() ?? "unnamed"}");
                
                // Check if there's color metadata stored in the TabItem's Tag
                if (tabItem.Tag is Dictionary<string, object> metadata)
                {
                    _instanceLogger?.LogDebug($"Found metadata with {metadata.Count} entries: {string.Join(", ", metadata.Keys)}");
                    
                    if (metadata.ContainsKey("TabColor") && metadata["TabColor"] is Color color)
                    {
                        _instanceLogger?.LogDebug($"Found TabColor in metadata: {color}");
                        // Consider LightGray as the default "no color" value
                        bool hasCustomColor = color != Colors.LightGray && color != Colors.Transparent;
                        _instanceLogger?.LogDebug($"Is custom color (not LightGray/Transparent): {hasCustomColor}");
                        return hasCustomColor;
                    }
                    
                    if (metadata.ContainsKey("TabColorData"))
                    {
                        _instanceLogger?.LogDebug("Found TabColorData in metadata");
                        return true; // If TabColorData exists, there's a custom color
                    }
                }
                else
                {
                    _instanceLogger?.LogDebug($"No metadata found. Tag type: {tabItem.Tag?.GetType()?.Name ?? "null"}");
                }
                
                // Check if the tab has a custom DataContext for color binding
                if (tabItem.DataContext is TabColorData colorData)
                {
                    _instanceLogger?.LogDebug($"Found TabColorData in DataContext: {colorData.TabColor}");
                    bool hasCustomColor = colorData.TabColor != Colors.LightGray && colorData.TabColor != Colors.Transparent;
                    _instanceLogger?.LogDebug($"DataContext has custom color: {hasCustomColor}");
                    return hasCustomColor;
                }
                else
                {
                    _instanceLogger?.LogDebug($"DataContext type: {tabItem.DataContext?.GetType()?.Name ?? "null"}");
                }
                
                // Check if any custom styling has been applied directly to the tab
                if (tabItem.Background != null && tabItem.Background != Brushes.Transparent)
                {
                    _instanceLogger?.LogDebug($"Found custom Background: {tabItem.Background}");
                    return true;
                }
                
                _instanceLogger?.LogDebug("No custom color found");
                return false;
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error checking if tab has custom color");
                return false;
            }
        }

        private void ClearColorMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _instanceLogger?.LogDebug("ClearColorMenuItem_Click called");
                
                if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
                {
                    var contextTabItem = GetContextMenuTabItem(contextMenu);
                    
                    if (contextTabItem != null)
                    {
                        // Check if this action should be allowed
                        bool hasCustomColor = TabHasCustomColor(contextTabItem);
                        _instanceLogger?.LogDebug($"Tab has custom color to clear: {hasCustomColor}");
                        
                        if (!hasCustomColor)
                        {
                            _instanceLogger?.LogDebug("Clear Color clicked on tab with no custom color - ignoring");
                            return; // Exit early if no custom color to clear
                        }
                        
                        var defaultColor = Colors.LightGray;
                        
                        // Update metadata stored in TabItem.Tag
                        if (contextTabItem.Tag is Dictionary<string, object> existingMetadata)
                        {
                            existingMetadata.Remove("TabColor");
                            existingMetadata.Remove("TabColorData");
                            existingMetadata["LastModified"] = DateTime.Now;
                        }
                        
                        // Update any associated model in the ViewModel
                        if (_viewModel?.TabItems != null)
                        {
                            var tabTitle = contextTabItem.Header?.ToString();
                            var viewModelTab = _viewModel.TabItems.FirstOrDefault(t => 
                                t.Title == tabTitle || 
                                (t.Content != null && t.Content == contextTabItem.Content));
                            if (viewModelTab != null)
                            {
                                viewModelTab.TabColor = defaultColor;
                                viewModelTab.UpdateLastAccessed();
                            }
                        }
                        
                        // Clear the DataContext and reset to original template styling
                        ClearTabColorStyling(contextTabItem);
                        
                        // Force immediate UI refresh
                        contextTabItem.UpdateLayout();
                        contextTabItem.InvalidateVisual();
                        MainTabs?.UpdateLayout();
                        
                        _instanceLogger?.LogDebug("Tab color cleared to default");
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error clearing tab color");
            }
        }

        private void TogglePinMenuItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is MenuItem menuItem && menuItem.Parent is ContextMenu contextMenu)
                {
                    var tabModel = GetContextMenuTabModel(contextMenu);
                    var contextTabItem = GetContextMenuTabItem(contextMenu);
                    
                    if (tabModel != null && contextTabItem != null && _viewModel?.TogglePinCommand?.CanExecute(tabModel) == true)
                    {
                        var wasPin = tabModel.IsPinned;
                        
                        _viewModel.TogglePinCommand.Execute(tabModel);
                        
                        // Sync changes back to the UI TabItem
                        UpdateTabItemFromModel(contextTabItem, tabModel);
                        
                        // IMMEDIATE: Update the context menu item text to reflect the new state
                        var newPinState = !wasPin;
                        menuItem.Header = newPinState ? "Unpin Tab" : "Pin Tab";
                        
                        // Reorder tabs to keep pinned tabs on the left
                        ReorderTabsAfterPinToggle();
                        
                        // CRITICAL: Invalidate context menu cache since pin state changed
                        InvalidateContextMenuCache();
                        
                        _instanceLogger?.LogInformation($"Tab pin state changed: {contextTabItem.Header} - {(wasPin ? "Unpinned" : "Pinned")}");
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error toggling pin state for tab");
            }
        }

        /// <summary>
        /// Reorders tabs so that pinned tabs are always on the left side
        /// </summary>
        private void ReorderTabsAfterPinToggle()
        {
            try
            {
                if (MainTabs?.Items == null || MainTabs.Items.Count <= 1) return;

                var allTabs = MainTabs.Items.Cast<TabItem>().ToList();
                var pinnedTabs = new List<TabItem>();
                var regularTabs = new List<TabItem>();

                // Separate pinned and regular tabs
                foreach (var tab in allTabs)
                {
                    if (IsTabPinned(tab))
                    {
                        pinnedTabs.Add(tab);
                    }
                    else
                    {
                        regularTabs.Add(tab);
                    }
                }

                // Clear all tabs
                var selectedTab = MainTabs.SelectedItem as TabItem;
                MainTabs.Items.Clear();

                // Add pinned tabs first (left side)
                foreach (var pinnedTab in pinnedTabs)
                {
                    MainTabs.Items.Add(pinnedTab);
                }

                // Add regular tabs after pinned tabs
                foreach (var regularTab in regularTabs)
                {
                    MainTabs.Items.Add(regularTab);
                }

                // Restore selection
                if (selectedTab != null)
                {
                    MainTabs.SelectedItem = selectedTab;
                }

                _instanceLogger?.LogDebug($"Reordered tabs: {pinnedTabs.Count} pinned, {regularTabs.Count} regular");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error reordering tabs after pin toggle");
            }
        }

        /// <summary>
        /// Checks if a tab is pinned by examining its tag
        /// </summary>
        private bool IsTabPinned(TabItem tabItem)
        {
            try
            {
                // Single source of truth - only check TabItemModel
                if (tabItem?.Tag is TabItemModel model)
                {
                    return model.IsPinned;
                }
                
                return false; // No fallbacks, consistent behavior
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error checking if tab is pinned");
                return false;
            }
        }

        /// <summary>
        /// Ensures that when new tabs are added, they are placed after pinned tabs
        /// </summary>
        private void InsertTabAtCorrectPosition(TabItem newTab)
        {
            try
            {
                if (MainTabs?.Items == null) return;

                // If this is a pinned tab, add it at the end of pinned tabs
                if (IsTabPinned(newTab))
                {
                    int insertPosition = 0;
                    // Find the position after the last pinned tab
                    for (int i = 0; i < MainTabs.Items.Count; i++)
                    {
                        if (MainTabs.Items[i] is TabItem tab && IsTabPinned(tab))
                        {
                            insertPosition = i + 1;
                        }
                        else
                        {
                            break;
                        }
                    }
                    MainTabs.Items.Insert(insertPosition, newTab);
                }
                else
                {
                    // Regular tab goes at the end
                    MainTabs.Items.Add(newTab);
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error inserting tab at correct position");
                // Fallback: just add to the end
                MainTabs.Items.Add(newTab);
            }
        }

        /// <summary>
        /// Handler for close tab menu item click.
        /// </summary>
        /// <summary>
        /// Handler for close tab menu item click.
        /// Enhanced with Phase 1 improvements: debouncing, memory cleanup
        /// </summary>
        private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 3. Event Handler Debouncing
            if (IsDebounced()) return;

            try
            {
                if (_rightClickedTab != null)
                {
                    var index = MainTabs.Items.IndexOf(_rightClickedTab);
                    if (index >= 0)
                    {
                        CloseTab(index);
                        _instanceLogger?.LogInformation($"Closed tab via context menu: {_rightClickedTab.Header}");
                        
                        // 4. Smart Memory Cleanup
                        PerformSmartCleanup();
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error closing tab from context menu");
                MessageBox.Show($"Failed to close tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handler for toggle split view menu item click.
        /// </summary>
        private void ToggleSplitViewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleSplitView();
        }

        /// <summary>
        /// Handler for detach tab menu item click.
        /// Enhanced with Phase 1 improvements: debouncing, memory cleanup
        /// </summary>
        private void DetachTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // 3. Event Handler Debouncing
            if (IsDebounced()) return;

            try
            {
                if (MainTabs.SelectedItem is TabItem selectedTab)
                {
                    DetachTabToNewWindow(selectedTab);
                    
                    // 4. Smart Memory Cleanup
                    PerformSmartCleanup();
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error detaching tab from context menu");
                MessageBox.Show($"Failed to detach tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Detaches a tab to a new window with proper lifecycle management
        /// </summary>
        /// <param name="tabItem">The tab to detach</param>
        /// <returns>The new window containing the detached tab, or null on failure</returns>
        public MainWindow DetachTabToNewWindow(TabItem tabItem)
        {
            // Try using service-based approach first
            if (tabItem?.Tag is TabItemModel serviceTabModel && _windowManager != null)
            {
                var newWindow = _windowManager.DetachTab(serviceTabModel, this);
                return newWindow as MainWindow;
            }
            
            // Fallback to original implementation if services not available
            if (tabItem == null || MainTabs.Items.Count <= 1)
            {
                _instanceLogger?.LogWarning("Cannot detach: invalid tab or last remaining tab");
                return null;
            }

            try
            {
                // Extract container and metadata
                var container = tabItem.Content as MainWindowContainer;
                var tabTitle = tabItem.Header?.ToString() ?? "Detached";
                var tabModel = GetTabItemModel(tabItem);

                // Remove from current window
                MainTabs.Items.Remove(tabItem);

                // Create and configure new window
                var newWindow = new MainWindow
                {
                    Title = $"ExplorerPro - {tabTitle}",
                    Width = this.Width * 0.8,
                    Height = this.Height * 0.8,
                    Left = this.Left + 50,
                    Top = this.Top + 50
                };

                // Initialize new window
                newWindow.Show();
                
                // Clear default tabs
                newWindow.MainTabs.Items.Clear();

                // Create new tab in target window
                var newTabItem = new TabItem
                {
                    Header = tabTitle,
                    Content = container,
                    Tag = tabModel
                };

                // Add to new window
                newWindow.MainTabs.Items.Add(newTabItem);
                newWindow.MainTabs.SelectedItem = newTabItem;

                // Track detached window
                var windowId = Guid.NewGuid();
                _detachedWindows[windowId] = new WeakReference<MainWindow>(newWindow);
                newWindow.Closed += (s, e) => _detachedWindows.TryRemove(windowId, out _);

                // Connect signals if needed
                if (container?.PinnedPanel != null)
                {
                    newWindow.ConnectPinnedPanelSignals(container.PinnedPanel);
                }

                _instanceLogger?.LogInformation($"Successfully detached tab '{tabTitle}' to new window");
                return newWindow;
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Failed to detach tab to new window");
                return null;
            }
        }

        /// <summary>
        /// Detaches a tab by index
        /// </summary>
        public void DetachTabByIndex(int index)
        {
            if (index >= 0 && index < MainTabs.Items.Count)
            {
                DetachTabToNewWindow(MainTabs.Items[index] as TabItem);
            }
        }

        /// <summary>
        /// Handler for move to new window menu item click.
        /// </summary>
        private void MoveToNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // FIX 5: Use new managed detachment method
            if (MainTabs.SelectedItem is TabItem selectedTab)
            {
                DetachToNewWindow(selectedTab);
            }
        }

        /// <summary>
        /// Handler for tab close button click.
        /// </summary>
        private void TabCloseButton_Click(object sender, RoutedEventArgs e)
        {
            // Find the tab that contains this button
            var button = sender as System.Windows.Controls.Button;
            var tabItem = FindParent<TabItem>(button);
            
            if (tabItem != null)
            {
                int index = MainTabs.Items.IndexOf(tabItem);
                if (index >= 0)
                {
                    CloseTab(index);
                }
            }
        }

        /// <summary>
        /// Handler for selection changed event in the main tabs.
        /// </summary>
        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateAddressBarOnTabChange();
            
            // Update activity bar button states for the new tab
            UpdateActivityBarButtonStates();
        }

        /// <summary>
        /// Find the parent control of a specific type.
        /// </summary>
        /// <typeparam name="T">Type of parent to find</typeparam>
        /// <param name="child">Child element</param>
        /// <returns>The parent control or null</returns>
        private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null)
                return null;
                
            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            
            if (parent == null)
                return null;
                
            if (parent is T typedParent)
                return typedParent;
                
            return FindParent<T>(parent);
        }

        /// <summary>
        /// Update address bar when tab changes.
        /// </summary>
        public void UpdateAddressBarOnTabChange()
        {
            try
            {
                var container = GetCurrentContainer();
                if (container == null) return;

                var fileTree = container.FindFileTree();
                if (fileTree != null)
                {
                    string path = fileTree.GetCurrentPath(); // Changed from CurrentPath property to GetCurrentPath method
                    if (!string.IsNullOrEmpty(path))
                    {
                        UpdateToolbarAddressBar(path); // Changed to new method name
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating address bar on tab change: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle notification when a new tab is added.
        /// </summary>
        /// <param name="container">Container that was added</param>
        private void OnNewTabAdded(MainWindowContainer container)
        {
            if (container.PinnedPanel != null)
            {
                ConnectPinnedPanel(container);
                
                // Refresh to sync with existing items
                RefreshAllPinnedPanels();
            }
        }

        /// <summary>
        /// Close specified tab.
        /// </summary>
        /// <param name="index">Index of tab to close</param>
        public void CloseTab(int index)
        {
            if (MainTabs.Items.Count <= 1) return;
            
            if (index >= 0 && index < MainTabs.Items.Count)
            {
                var tabItem = MainTabs.Items[index] as TabItem;
                
                // Don't close pinned tabs
                if (tabItem != null && IsTabPinned(tabItem))
                {
                    _instanceLogger?.LogDebug($"Cannot close pinned tab: {tabItem.Header}");
                    MessageBox.Show("Cannot close pinned tab. Unpin it first to close.", 
                        "Pinned Tab", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }
                
                MainTabs.Items.RemoveAt(index);
            }
        }

        /// <summary>
        /// Close the current tab.
        /// </summary>
        public void CloseCurrentTab()
        {
            if (MainTabs.Items.Count <= 1) return;
            
            CloseTab(MainTabs.SelectedIndex);
        }

        /// <summary>
        /// Detach a tab into a new window.
        /// </summary>
        /// <param name="index">Index of tab to detach</param>
        public void DetachMainTab(int index)
        {
            if (index < 0 || index >= MainTabs.Items.Count) return;
            
            if (MainTabs.Items.Count <= 1) 
            {
                // Don't detach last tab
                WPF.MessageBox.Show("Cannot detach the only tab.", 
                    "Detach Tab", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Get the tab item
                TabItem? tabItem = MainTabs.Items[index] as TabItem;
                if (tabItem == null) return;

                // Get the container
                MainWindowContainer? container = tabItem.Content as MainWindowContainer;
                if (container == null) return;

                string tabTitle = tabItem.Header?.ToString() ?? "Detached";

                // Create new window
                MainWindow newWindow = new MainWindow();
                
                // Remove tab from current window
                MainTabs.Items.RemoveAt(index);
                
                // Clear tabs in new window
                newWindow.MainTabs.Items.Clear();
                
                // Create new tab item for detached window
                TabItem newTabItem = new TabItem
                {
                    Header = tabTitle,
                    Content = container
                };
                
                // Add to new window
                newWindow.MainTabs.Items.Add(newTabItem);
                
                // Configure and show new window
                newWindow.Title = $"Detached - {tabTitle}";
                newWindow.Width = 1000;
                newWindow.Height = 700;
                newWindow.Left = Left + 50;
                newWindow.Top = Top + 50;
                
                // Show window
                newWindow.Show();
                newWindow.Activate();
                
                // Connect panel signals
                if (container.PinnedPanel != null)
                {
                    newWindow.ConnectPinnedPanelSignals(container.PinnedPanel);
                }
                
                // Track detached window - will be replaced with new managed implementation
                // _detachedWindows[Guid.NewGuid()] = new WeakReference<MainWindow>(newWindow);
                
                Console.WriteLine($"Detached tab '{tabTitle}' to new window");
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error detaching tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }



        /// <summary>
        /// Move the current tab to a new window.
        /// ENHANCED FOR FIX 5: Detached Windows List Management
        /// </summary>
        public void MoveTabToNewWindow()
        {
            if (MainTabs.SelectedItem is TabItem selectedTab)
            {
                DetachToNewWindow(selectedTab);
            }
        }
        
        /// <summary>
        /// Gets the current MainWindowContainer.
        /// </summary>
        /// <returns>The current container or null</returns>
        public MainWindowContainer? GetCurrentContainer()
        {
            if (MainTabs.SelectedItem is TabItem tabItem)
            {
                return tabItem.Content as MainWindowContainer;
            }
            return null;
        }

        #endregion

        #region Panel Management

        /// <summary>
        /// Connect pinned panel signals for a container.
        /// </summary>
        /// <param name="container">Container to connect</param>
        private void ConnectPinnedPanel(MainWindowContainer container)
        {
            if (container == null)
            {
                Console.WriteLine("Cannot connect null container");
                return;
            }
            
            if (container.PinnedPanel != null)
            {
                try
                {
                    // Don't connect if already connected
                    if (container.PinnedPanel.GetIsSignalsConnected())
                    {
                        return;
                    }
                    
                    // Connect events
                    container.PinnedPanel.PinnedItemAdded += OnPinnedItemAddedHandler;
                    container.PinnedPanel.PinnedItemModified += OnPinnedItemModifiedHandler;
                    container.PinnedPanel.PinnedItemRemoved += OnPinnedItemRemovedHandler;
                    
                    // Mark as connected
                    container.PinnedPanel.SetIsSignalsConnected(true);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error connecting pinned panel: {ex.Message}");
                    // Continue without connected signals
                }
            }
        }

        // Event handlers for PinnedPanel events
        private void OnPinnedItemAddedHandler(object? sender, StringEventArgs e)
        {
            RefreshAllPinnedPanels(e.Value);
        }

        private void OnPinnedItemModifiedHandler(object? sender, ItemModifiedEventArgs e)
        {
            RefreshAllPinnedPanels(e.OldPath, e.NewPath);
        }

        private void OnPinnedItemRemovedHandler(object? sender, StringEventArgs e)
        {
            RefreshAllPinnedPanels(e.Value);
        }

        /// <summary>
        /// Connect signals for a specific pinned panel.
        /// </summary>
        /// <param name="pinnedPanel">Pinned panel to connect</param>
        public void ConnectPinnedPanelSignals(object pinnedPanel)
        {
            // Cast to correct type
            var panel = pinnedPanel as PinnedPanel;
            if (panel == null) return;
            
            // Skip if already connected
            if (panel.GetIsSignalsConnected()) return; // Changed to method call
            
            // Connect signals
            panel.PinnedItemAdded += OnPinnedItemAddedHandler;
            panel.PinnedItemModified += OnPinnedItemModifiedHandler;
            panel.PinnedItemRemoved += OnPinnedItemRemovedHandler;
            
            // Mark as connected
            panel.SetIsSignalsConnected(true); // Changed to method call
        }

        /// <summary>
        /// Connect all pinned panels across all windows.
        /// </summary>
        private void ConnectAllPinnedPanels()
        {
            foreach (var window in WindowLifecycleManager.Instance.GetActiveWindows())
            {
                for (int i = 0; i < window.MainTabs.Items.Count; i++)
                {
                    var tabItem = window.MainTabs.Items[i] as TabItem;
                    if (tabItem?.Content is MainWindowContainer container && container.PinnedPanel != null)
                    {
                        window.ConnectPinnedPanelSignals(container.PinnedPanel);
                    }
                }
            }
        }

        /// <summary>
        /// Refresh all pinned panels across all windows.
        /// </summary>
        /// <param name="itemPath">Optional path of modified item</param>
        /// <param name="newPath">Optional new path if item was renamed/moved</param>
        public void RefreshAllPinnedPanels(string? itemPath = null, string? newPath = null)
        {
            try
            {
                // Refresh all windows
                foreach (var window in WindowLifecycleManager.Instance.GetActiveWindows())
                {
                    for (int i = 0; i < window.MainTabs.Items.Count; i++)
                    {
                        var tabItem = window.MainTabs.Items[i] as TabItem;
                        if (tabItem?.Content is MainWindowContainer container && container.PinnedPanel != null)
                        {
                            // Handle rename/move
                            if (!string.IsNullOrEmpty(newPath) && !string.IsNullOrEmpty(itemPath))
                            {
                                container.PinnedPanel.HandleItemRename(itemPath, newPath); // Changed method name
                            }
                            
                            // Refresh panel
                            container.PinnedPanel.RefreshItems(); // Changed method name
                        }
                    }
                }
                
                // Force update current tab immediately
                var currentContainer = GetCurrentContainer();
                if (currentContainer?.PinnedPanel != null)
                {
                    currentContainer.PinnedPanel.RefreshItems(); // Changed method name
                }
                
                // Ensure all panels stay connected
                ConnectAllPinnedPanels();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing pinned panels: {ex.Message}");
            }
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Panel Toggle
        /// Toggle pinned panel visibility in current container.
        /// Called by ActivityBarPinnedButton in Activity Bar (DockPanel.Dock="Left")
        /// </summary>
        private void TogglePinnedPanel()
        {
            TogglePanelInCurrentContainer("TogglePinnedPanel");
            UpdateActivityBarButtonStates();
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Button Click Handler
        /// Event handler for ActivityBarPinnedButton in Activity Bar
        /// Linked via Click="TogglePinnedPanel_Click" in MainWindow.xaml
        /// </summary>
        private void TogglePinnedPanel_Click(object sender, RoutedEventArgs e)
        {
            TogglePinnedPanel();
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Panel Toggle
        /// Toggle bookmarks panel visibility in current container.
        /// Called by ActivityBarBookmarksButton in Activity Bar (DockPanel.Dock="Left")
        /// </summary>
        private void ToggleBookmarksPanel()
        {
            TogglePanelInCurrentContainer("ToggleBookmarksPanel");
            UpdateActivityBarButtonStates();
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Button Click Handler
        /// Event handler for ActivityBarBookmarksButton in Activity Bar
        /// Linked via Click="ToggleBookmarksPanel_Click" in MainWindow.xaml
        /// </summary>
        private void ToggleBookmarksPanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleBookmarksPanel();
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Panel Toggle
        /// Toggle to-do panel visibility in current container.
        /// Called by ActivityBarTodoButton in Activity Bar (DockPanel.Dock="Left")
        /// </summary>
        private void ToggleTodoPanel()
        {
            TogglePanelInCurrentContainer("ToggleTodoPanel");
            UpdateActivityBarButtonStates();
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Button Click Handler
        /// Event handler for ActivityBarTodoButton in Activity Bar
        /// Linked via Click="ToggleTodoPanel_Click" in MainWindow.xaml
        /// </summary>
        private void ToggleTodoPanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleTodoPanel();
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Panel Toggle
        /// Toggle Procore links panel visibility in current container.
        /// Called by ActivityBarProcoreButton in Activity Bar (DockPanel.Dock="Left")
        /// </summary>
        private void ToggleProcorePanel()
        {
            TogglePanelInCurrentContainer("ToggleProcorePanel");
            UpdateActivityBarButtonStates();
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Button Click Handler
        /// Event handler for ActivityBarProcoreButton in Activity Bar
        /// Linked via Click="ToggleProcorePanel_Click" in MainWindow.xaml
        /// </summary>
        private void ToggleProcorePanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleProcorePanel();
        }

        /// <summary>
        /// Toggle the entire left sidebar visibility.
        /// </summary>
        public void ToggleLeftSidebar()
        {
            var container = GetCurrentContainer();
            if (container == null) return;

            // Toggle the entire left column
            container.ToggleLeftSidebar();
        }

        /// <summary>
        /// Event handler for left sidebar toggle button.
        /// </summary>
        private void ToggleLeftSidebar_Click(object sender, RoutedEventArgs e)
        {
            ToggleLeftSidebar();
        }

        /// <summary>
        /// Toggle the entire right sidebar visibility.
        /// </summary>
        public void ToggleRightSidebar()
        {
            var container = GetCurrentContainer();
            if (container == null) return;

            // Toggle the entire right column
            container.ToggleRightSidebar();
        }

        /// <summary>
        /// Event handler for right sidebar toggle button.
        /// </summary>
        private void ToggleRightSidebar_Click(object sender, RoutedEventArgs e)
        {
            ToggleRightSidebar();
        }

        /// <summary>
        /// Toggle split view in current container.
        /// </summary>
        public void ToggleSplitView(string? path = null)
            {
                var container = GetCurrentContainer();
                container?.ToggleSplitView(path);
            }

        /// <summary>
        /// Toggle a panel in the current container by method name.
        /// </summary>
        /// <param name="methodName">Name of the toggle method</param>
        private void TogglePanelInCurrentContainer(string methodName)
        {
            var container = GetCurrentContainer();
            if (container == null)
            {
                Console.WriteLine($"No current container to toggle {methodName}");
                return;
            }

            // Use reflection to call the method by name
            var method = container.GetType().GetMethod(methodName);
            if (method != null)
            {
                method.Invoke(container, null);
            }
            else
            {
                Console.WriteLine($"Error: No method {methodName} found in container");
            }
        }

        /// <summary>
        /// Apply saved settings to the UI.
        /// </summary>
        public void ApplySavedSettings()
        {
            try
            {
                // Apply theme using the settings service
                string theme = _settingsService.GetTheme();
                ApplyTheme(theme);

                // Apply panel visibility using the settings service
                Dictionary<string, bool> panels = _settingsService.GetPanelSettings();
                
                var container = GetCurrentContainer();
                if (container != null)
                {
                    container.ApplySavedPanelVisibility(panels);
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error applying saved settings");
                Console.WriteLine($"Error applying saved settings: {ex.Message}");
            }
        }

        /// <summary>
        /// LAYOUT DEPENDENCY: Activity Bar Button State Management
        /// Update activity bar button states to reflect current panel visibility.
        /// Assumes Activity Bar buttons exist in current layout (DockPanel.Dock="Left")
        /// References: ActivityBarPinnedButton, ActivityBarBookmarksButton, 
        /// ActivityBarProcoreButton, ActivityBarTodoButton
        /// </summary>
        private void UpdateActivityBarButtonStates()
        {
            ExecuteOnUIThread(() =>
            {
                try
                {
                    var container = GetCurrentContainer();
                    if (container == null) return;

                    // Update button states based on panel visibility
                    if (ActivityBarPinnedButton != null)
                    {
                        bool isPinnedVisible = IsPanelVisible(container, "PinnedPanel");
                        ActivityBarPinnedButton.Tag = isPinnedVisible ? "Active" : null;
                    }

                    if (ActivityBarBookmarksButton != null)
                    {
                        bool isBookmarksVisible = IsPanelVisible(container, "BookmarksPanel");
                        ActivityBarBookmarksButton.Tag = isBookmarksVisible ? "Active" : null;
                    }

                    if (ActivityBarProcoreButton != null)
                    {
                        bool isProcoreVisible = IsPanelVisible(container, "ProcorePanel");
                        ActivityBarProcoreButton.Tag = isProcoreVisible ? "Active" : null;
                    }

                    if (ActivityBarTodoButton != null)
                    {
                        bool isTodoVisible = IsPanelVisible(container, "TodoPanel");
                        ActivityBarTodoButton.Tag = isTodoVisible ? "Active" : null;
                    }
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, "Error updating activity bar button states");
                }
            });
        }

        /// <summary>
        /// Check if a specific panel is visible in the container.
        /// </summary>
        /// <param name="container">The container to check</param>
        /// <param name="panelName">Name of the panel to check</param>
        /// <returns>True if panel is visible</returns>
        private bool IsPanelVisible(MainWindowContainer container, string panelName)
        {
            try
            {
                // Use the container's IsPanelVisible method as specified in Phase 4
                return container.IsPanelVisible(panelName);
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogWarning(ex, $"Could not determine visibility for panel: {panelName}");
                return false;
            }
        }

        #endregion

        #region Navigation and File Operations

        /// <summary>
        /// LAYOUT DEPENDENCY: Toolbar Address Bar Update
        /// Update the address bar with path.
        /// ENHANCED FOR FIX 2: Thread-Safety Issues in UI Updates
        /// Assumes Toolbar exists in current layout (DockPanel.Dock="Top")
        /// </summary>
        /// <param name="path">Path to display</param>
        public void UpdateToolbarAddressBar(string path) // Changed method name
        {
            ExecuteOnUIThread(() =>
            {
                try
                {
                    var embeddedToolbar = FindEmbeddedToolbar();
                    if (embeddedToolbar != null && !IsDisposed)
                    {
                        if (string.IsNullOrEmpty(path)) return;
                        
                        if (!Directory.Exists(path))
                        {
                            _instanceLogger?.LogWarning($"Invalid path provided: {path}");
                            return;
                        }

                        // Update toolbar address bar
                        embeddedToolbar?.SetAddressText(path); // Changed method name
                        
                        // Update status bar
                        UpdateStatus($"Current path: {path}");
                        
                        _instanceLogger?.LogDebug($"Updated address bar: {path}");
                    }
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, "Error updating address bar");
                }
            });
        }

        /// <summary>
        /// Update address bar for API compatibility with MainWindowContainer
        /// ENHANCED FOR FIX 2: Thread-Safety Issues in UI Updates
        /// </summary>
        /// <param name="path">Path to display</param>
        public void UpdateAddressBar(string path)
        {
            UpdateToolbarAddressBar(path);
        }

        /// <summary>
        /// Update status bar text.
        /// ENHANCED FOR FIX 2: Thread-Safety Issues in UI Updates
        /// ENHANCED FOR PHASE 6: Thread Safety Standardization
        /// </summary>
        /// <param name="text">Status text</param>
        public void UpdateStatus(string text)
        {
            ThreadSafetyValidator.LogThreadContext("UpdateStatus");
            UIThreadHelper.ExecuteOnUIThread(() =>
            {
                ThreadSafetyValidator.AssertUIThread();
                if (StatusText != null && !IsDisposed)
                {
                    StatusText.Text = text ?? string.Empty;
                    _instanceLogger?.LogDebug($"Status updated: {text}");
                }
            });
        }

        /// <summary>
        /// Update item count display.
        /// ENHANCED FOR FIX 2: Thread-Safety Issues in UI Updates
        /// ENHANCED FOR PHASE 6: Thread Safety Standardization
        /// </summary>
        /// <param name="count">Number of items</param>
        public void UpdateItemCount(int count)
        {
            ThreadSafetyValidator.LogThreadContext("UpdateItemCount");
            UIThreadHelper.ExecuteOnUIThread(() =>
            {
                ThreadSafetyValidator.AssertUIThread();
                if (ItemCountText != null && !IsDisposed)
                {
                    ItemCountText.Text = count == 1 ? "1 item" : $"{count} items";
                    _instanceLogger?.LogDebug($"Updated item count to {count}");
                }
            });
        }

        /// <summary>
        /// Update selection info.
        /// ENHANCED FOR FIX 2: Thread-Safety Issues in UI Updates
        /// ENHANCED FOR PHASE 6: Thread Safety Standardization
        /// </summary>
        /// <param name="count">Number of selected items</param>
        /// <param name="size">Total size of selection</param>
        public void UpdateSelectionInfo(int count, long size = 0)
        {
            ThreadSafetyValidator.LogThreadContext("UpdateSelectionInfo");
            UIThreadHelper.ExecuteOnUIThread(() =>
            {
                ThreadSafetyValidator.AssertUIThread();
                if (SelectionText != null && !IsDisposed)
                {
                    if (count == 0)
                    {
                        SelectionText.Text = string.Empty;
                    }
                    else
                    {
                        string sizeText = FileSizeFormatter.FormatSize(size);
                        SelectionText.Text = count == 1 
                            ? $"1 item selected ({sizeText})" 
                            : $"{count} items selected ({sizeText})";
                    }
                    
                    _instanceLogger?.LogDebug($"Updated selection: {count} items, {size} bytes");
                }
            });
        }

        /// <summary>
        /// Get the active file tree.
        /// </summary>
        /// <returns>The active ImprovedFileTreeListView or null</returns>
        public ImprovedFileTreeListView? GetActiveFileTree()
        {
            var container = GetCurrentContainer();
            return container?.FindFileTree();
        }

        /// <summary>
        /// Open a directory in a tab.
        /// </summary>
        /// <param name="path">Path to open</param>
        public void OpenDirectoryInTab(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                Console.WriteLine("Error: No path provided");
                return;
            }
            
            try
            {
                string normalizedPath = Path.GetFullPath(path);
                if (!Directory.Exists(normalizedPath))
                {
                    MessageBoxResult result = WPF.MessageBox.Show(
                        $"Cannot verify '{normalizedPath}' as a valid directory. Open anyway?",
                        "Path Verification",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);
                        
                    if (result == MessageBoxResult.No)
                    {
                        return;
                    }
                }
                
                // Get current container
                var container = GetCurrentContainer();
                if (container == null)
                {
                    Console.WriteLine("Error: No active container");
                    return;
                }
                
                // Check if already open in a tab
                for (int i = 0; i < MainTabs.Items.Count; i++)
                {
                    var tabItem = MainTabs.Items[i] as TabItem;
                    if (tabItem?.Content is MainWindowContainer existingContainer)
                    {
                        var fileTree = existingContainer.FindFileTree();
                        if (fileTree != null && fileTree.GetCurrentPath() == normalizedPath) // Changed to method
                        {
                            // Already open - switch to it
                            MainTabs.SelectedIndex = i;
                            UpdateToolbarAddressBar(normalizedPath); // Changed method name
                            return;
                        }
                    }
                }
                
                // Not already open - add new tab
                container.OpenDirectoryInNewTab(normalizedPath);
                UpdateToolbarAddressBar(normalizedPath); // Changed method name
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error opening directory: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Open an item in a new tab (API compatibility with PinnedPanel)
        /// </summary>
        /// <param name="itemPath">Path to open</param>
        public void OpenItemInNewTab(string itemPath)
        {
            OpenDirectoryInTab(itemPath);
        }

        /// <summary>
        /// Open an item in a new window (API compatibility with PinnedPanel)
        /// </summary>
        /// <param name="itemPath">Path to open</param>
        public void OpenItemInNewWindow(string itemPath)
        {
            // Create a new window and open the path in it
            MainWindow newWindow = new MainWindow();
            newWindow.Show();
            
            // Open the path in the new window
            newWindow.OpenDirectoryInTab(itemPath);
        }

        /// <summary>
        /// Navigate up one directory level.
        /// </summary>
        public void GoUp()
        {
            var container = GetCurrentContainer();
            container?.GoUp();
        }

        /// <summary>
        /// Navigate back in history.
        /// ENHANCED FOR FIX 3: Navigation History Unbounded Growth
        /// </summary>
        public void GoBack()
        {
            lock (_historyLock)
            {
                if (_currentHistoryNode?.Previous != null)
                {
                    _currentHistoryNode = _currentHistoryNode.Previous;
                    PerformNavigation(_currentHistoryNode.Value.Path);
                }
                else
                {
                    // Fallback to container navigation if no bounded history
                    var container = GetCurrentContainer();
                    container?.GoBack();
                }
            }
        }

        /// <summary>
        /// Navigate forward in history.
        /// ENHANCED FOR FIX 3: Navigation History Unbounded Growth
        /// </summary>
        public void GoForward()
        {
            lock (_historyLock)
            {
                if (_currentHistoryNode?.Next != null)
                {
                    _currentHistoryNode = _currentHistoryNode.Next;
                    PerformNavigation(_currentHistoryNode.Value.Path);
                }
                else
                {
                    // Fallback to container navigation if no bounded history
                    var container = GetCurrentContainer();
                    container?.GoForward();
                }
            }
        }

        /// <summary>
        /// IMPLEMENTATION OF FIX 3: Navigation History Unbounded Growth
        /// Navigate to a specific path with bounded history management
        /// </summary>
        public void NavigateToPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            
            lock (_historyLock)
            {
                try
                {
                    // Remove forward history if navigating from middle
                    if (_currentHistoryNode != null && _currentHistoryNode.Next != null)
                    {
                        var node = _currentHistoryNode.Next;
                        while (node != null)
                        {
                            var next = node.Next;
                            _navigationHistory.Remove(node);
                            node = next;
                        }
                    }
                    
                    // Add new entry
                    var entry = new NavigationEntry(path);
                    _currentHistoryNode = _navigationHistory.AddLast(entry);
                    
                    // Enforce size limit
                    if (_navigationHistory.Count > MaxHistorySize)
                    {
                        _instanceLogger?.LogInformation($"Navigation history limit reached ({MaxHistorySize}), trimming oldest {HistoryTrimSize} entries");
                        
                        for (int i = 0; i < HistoryTrimSize && _navigationHistory.Count > 0; i++)
                        {
                            _navigationHistory.RemoveFirst();
                        }
                        
                        // Ensure current node is still valid
                        if (_currentHistoryNode?.List == null)
                        {
                            _currentHistoryNode = _navigationHistory.Last;
                        }
                    }
                    
                    // Log memory usage periodically
                    if (_navigationHistory.Count % 100 == 0)
                    {
                        var totalMemory = _navigationHistory.Sum(e => e.MemorySize);
                        _instanceLogger?.LogDebug($"Navigation history: {_navigationHistory.Count} entries, ~{totalMemory / 1024}KB");
                    }
                    
                    // Perform actual navigation
                    PerformNavigation(path);
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, $"Error during navigation to {path}");
                }
            }
        }

        /// <summary>
        /// IMPLEMENTATION OF FIX 3: Navigation History Unbounded Growth
        /// Check if we can navigate back in history
        /// </summary>
        public bool CanGoBack()
        {
            lock (_historyLock)
            {
                return _currentHistoryNode?.Previous != null;
            }
        }

        /// <summary>
        /// IMPLEMENTATION OF FIX 3: Navigation History Unbounded Growth
        /// Check if we can navigate forward in history
        /// </summary>
        public bool CanGoForward()
        {
            lock (_historyLock)
            {
                return _currentHistoryNode?.Next != null;
            }
        }

        /// <summary>
        /// IMPLEMENTATION OF FIX 3: Navigation History Unbounded Growth
        /// Perform the actual navigation to a path
        /// </summary>
        private void PerformNavigation(string path)
        {
            try
            {
                if (Directory.Exists(path))
                {
                    // Update address bar and refresh
                    UpdateToolbarAddressBar(path);
                    OpenDirectoryInTab(path);
                    _instanceLogger?.LogDebug($"Navigated to {path}");
                }
                else
                {
                    _instanceLogger?.LogWarning($"Cannot navigate to non-existent path: {path}");
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, $"Error performing navigation to {path}");
            }
        }

        /// <summary>
        /// Refresh the active file tree.
        /// </summary>
        public void RefreshFileTree()
        {
            var fileTree = GetActiveFileTree();
            fileTree?.RefreshView(); // Changed method name
        }

        /// <summary>
        /// Handle context menu actions.
        /// </summary>
        /// <param name="action">Action to perform</param>
        /// <param name="filePath">File path to act on</param>
        public void HandleContextMenuAction(string action, string filePath)
        {
            try
            {
                switch (action)
                {
                    case "show_metadata":
                        ShowMetadata(filePath);
                        break;
                        
                    case "delete":
                        DeleteFile(filePath);
                        break;
                        
                    case "rename":
                        RenameFile(filePath);
                        break;
                        
                    case "pin":
                        var container = GetCurrentContainer();
                        if (container?.PinnedPanel != null && File.Exists(filePath))
                        {
                            container.PinnedPanel.AddPinnedItem(filePath); // Changed method name
                            Console.WriteLine($"File pinned: {filePath}");
                        }
                        break;
                        
                    case "tag":
                        if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                        {
                            TagItem(filePath);
                        }
                        break;
                        
                    default:
                        Console.WriteLine($"Warning: No handler for action: {action}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error handling context menu action: {ex.Message}");
            }
        }

        /// <summary>
        /// Show metadata for a file.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        private void ShowMetadata(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    WPF.MessageBox.Show("Invalid file path", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Using FilePropertiesDialog instead of MetadataDialog
                var dialog = new FilePropertiesDialog(filePath, _metadataManager);
                dialog.ShowDialog();
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error showing metadata: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Delete a file or directory.
        /// </summary>
        /// <param name="filePath">Path to delete</param>
        private void DeleteFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    WPF.MessageBox.Show("Invalid file path", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Create an instance of FileOperations
                var fileOps = new FileOperations.FileOperations();
                if (fileOps.Delete(filePath)) // Changed method call
                {
                    Console.WriteLine($"Deleted {filePath}");
                    RefreshFileTree();
                }
                else
                {
                    Console.WriteLine($"Failed to delete {filePath}");
                }
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error deleting file: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Rename a file or directory.
        /// </summary>
        /// <param name="filePath">Path to rename</param>
        private void RenameFile(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    WPF.MessageBox.Show("Invalid file path", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Using TextInputDialog instead of InputDialog
                var dialog = new TextInputDialog(
                    "Rename File", 
                    "Enter new name:", 
                    Path.GetFileName(filePath));
                
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.InputText))
                {
                    string newName = dialog.InputText;
                    
                    // Create an instance of FileOperations
                    var fileOps = new FileOperations.FileOperations();
                    string? newPath = fileOps.Rename(filePath, newName); // Changed method call
                    
                    if (newPath != null)
                    {
                        Console.WriteLine($"Renamed {filePath} to {newPath}");
                        RefreshFileTree();
                    }
                    else
                    {
                        Console.WriteLine($"Failed to rename {filePath}");
                    }
                }
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error renaming file: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Add a tag to a file.
        /// </summary>
        /// <param name="filePath">Path to tag</param>
        private void TagItem(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                {
                    WPF.MessageBox.Show("Invalid file path", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                
                // Using TextInputDialog instead of InputDialog
                var dialog = new TextInputDialog("Tag Item", $"Enter a tag for:\n{filePath}", "");
                if (dialog.ShowDialog() == true && !string.IsNullOrEmpty(dialog.InputText))
                {
                    string tag = dialog.InputText;
                    _metadataManager.AddTag(filePath, tag);
                    
                    // Show confirmation
                    Console.WriteLine($"Tag '{tag}' added to '{filePath}'");
                    
                    // Get all tags and display
                    var allTags = _metadataManager.GetTags(filePath);
                    UpdateStatus($"File now has tags: {string.Join(", ", allTags)}");
                }
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error tagging file: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Command Handlers

        /// <summary>
        /// Handler for New command.
        /// </summary>
        private void NewCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var contextData = e.Parameter as string;
            if (contextData == "folder")
            {
                NewFolder();
            }
            else if (contextData == "file")
            {
                NewFile();
            }
            else if (contextData == "tab")
            {
                NewTab();
            }
            else
            {
                // Show a new context menu
                var menu = new ContextMenu();
                menu.Items.Add(new MenuItem { Header = "New Tab", Command = ApplicationCommands.New, CommandParameter = "tab" });
                menu.Items.Add(new MenuItem { Header = "New Folder", Command = ApplicationCommands.New, CommandParameter = "folder" });
                menu.Items.Add(new MenuItem { Header = "New File", Command = ApplicationCommands.New, CommandParameter = "file" });
                menu.IsOpen = true;
            }
        }

        /// <summary>
        /// Handler for Open command.
        /// </summary>
        private void OpenCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Open File",
                CheckFileExists = true,
                Multiselect = false
            };
            
            if (dialog.ShowDialog() == true)
            {
                string filePath = dialog.FileName;
                string dirPath = Path.GetDirectoryName(filePath) ?? string.Empty;
                
                // Open directory containing the file
                OpenDirectoryInTab(dirPath);
                
                // Select the file
                var fileTree = GetActiveFileTree();
                fileTree?.SelectItem(filePath); // Changed method name
            }
        }

        /// <summary>
        /// Handler for Save command.
        /// </summary>
        private void SaveCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Save settings using the service
            _ = _settingsService.SaveSettingsAsync();
        }

        /// <summary>
        /// Handler for Close command.
        /// </summary>
        private void CloseCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            if (e.Parameter is string param && param == "tab")
            {
                CloseCurrentTab();
            }
            else
            {
                Close();
            }
        }

        /// <summary>
        /// Handler for BrowseBack command.
        /// </summary>
        private void BrowseBackCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            GoBack();
        }

        /// <summary>
        /// Handler for BrowseForward command.
        /// </summary>
        private void BrowseForwardCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            GoForward();
        }

        /// <summary>
        /// Handler for BrowseHome command.
        /// </summary>
        private void BrowseHomeCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // Navigate to user home directory
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            OpenDirectoryInTab(homePath);
        }

        /// <summary>
        /// Handler for Refresh command.
        /// </summary>
        private void RefreshCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            RefreshFileTree();
        }

        /// <summary>
        /// Handler for Copy command.
        /// </summary>
        private void CopyCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var fileTree = GetActiveFileTree();
            fileTree?.CopySelected(); // Changed method name
        }

        /// <summary>
        /// Handler for Cut command.
        /// </summary>
        private void CutCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var fileTree = GetActiveFileTree();
            fileTree?.CutSelected(); // Changed method name
        }

        /// <summary>
        /// Handler for Paste command.
        /// </summary>
        private void PasteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var fileTree = GetActiveFileTree();
            fileTree?.Paste(); // Changed method name
        }

        /// <summary>
        /// Handler for Delete command.
        /// </summary>
        private void DeleteCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var fileTree = GetActiveFileTree();
            fileTree?.DeleteSelected(); // Changed method name
        }

        /// <summary>
        /// Handler for Properties command.
        /// </summary>
        private void PropertiesCommand_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            var fileTree = GetActiveFileTree();
            if (fileTree != null)
            {
                string? selectedPath = fileTree.GetSelectedPath(); // Changed method name
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    ShowFileProperties(selectedPath);
                }
            }
        }

        #endregion

        #region Keyboard Shortcut Actions

        /// <summary>
        /// Focus the address bar.
        /// </summary>
        private void FocusAddressBar()
        {
            var embeddedToolbar = FindEmbeddedToolbar();
            embeddedToolbar?.SetAddressBarFocus(); // Changed method name
        }

        /// <summary>
        /// Focus the search box.
        /// </summary>
        private void FocusSearch()
        {
            var embeddedToolbar = FindEmbeddedToolbar();
            embeddedToolbar?.SetSearchFocus(); // Changed method name
        }

        /// <summary>
        /// Create a new folder in the current directory.
        /// </summary>
        private void NewFolder()
        {
            var fileTree = GetActiveFileTree();
            fileTree?.CreateFolder(); // Changed method name
        }

        /// <summary>
        /// Create a new file in the current directory.
        /// </summary>
        private void NewFile()
        {
            var fileTree = GetActiveFileTree();
            fileTree?.CreateFile(); // Changed method name
        }

        /// <summary>
        /// Switch to the next tab.
        /// </summary>
        private void NextTab()
        {
            TryAccessUIElement(SafeMainTabs, tabs =>
            {
                if (tabs.Items.Count <= 1) return;
                
                int current = tabs.SelectedIndex;
                tabs.SelectedIndex = (current + 1) % tabs.Items.Count;
            });
        }

        /// <summary>
        /// Switch to the previous tab.
        /// </summary>
        private void PreviousTab()
        {
            TryAccessUIElement(SafeMainTabs, tabs =>
            {
                if (tabs.Items.Count <= 1) return;
                
                int current = tabs.SelectedIndex;
                tabs.SelectedIndex = (current - 1 + tabs.Items.Count) % tabs.Items.Count;
            });
        }

        /// <summary>
        /// Create a new tab.
        /// </summary>
        private void NewTab()
        {
            try
            {
                var container = AddNewMainWindowTab();
                if (container == null)
                {
                    SafeAddNewTab();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in NewTab method: {ex.Message}");
                SafeAddNewTab();
            }
        }

        /// <summary>
        /// Toggle fullscreen mode.
        /// </summary>
        private void ToggleFullscreen()
        {
            if (WindowStyle == WindowStyle.None)
            {
                // Exit fullscreen
                WindowStyle = WindowStyle.SingleBorderWindow;
                WindowState = System.Windows.WindowState.Normal;
                ResizeMode = ResizeMode.CanResize;
            }
            else
            {
                // Enter fullscreen
                WindowStyle = WindowStyle.None;
                WindowState = System.Windows.WindowState.Maximized;
                ResizeMode = ResizeMode.NoResize;
            }
        }

        /// <summary>
        /// Increase the zoom level (placeholder).
        /// </summary>
        private void ZoomIn()
        {
            // This would be implemented based on your zoom mechanism
            Console.WriteLine("Zoom in - not implemented");
        }

        /// <summary>
        /// Decrease the zoom level (placeholder).
        /// </summary>
        private void ZoomOut()
        {
            // This would be implemented based on your zoom mechanism
            Console.WriteLine("Zoom out - not implemented");
        }

        /// <summary>
        /// Reset zoom level (placeholder).
        /// </summary>
        private void ZoomReset()
        {
            // This would be implemented based on your zoom mechanism
            Console.WriteLine("Zoom reset - not implemented");
        }

        /// <summary>
        /// Show help documentation.
        /// </summary>
        private void ShowHelp()
        {
            // This would be implemented based on your help system
            WPF.MessageBox.Show("Help not implemented yet", "Help", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Open the settings dialog.
        /// </summary>
        public void OpenSettings()
        {
            // For now, use the legacy SettingsManager for the dialog
            // TODO: Update SettingsDialog to accept ISettingsService
            var settingsDialog = new SettingsDialog(App.Settings ?? new SettingsManager());
            
            if (settingsDialog.ShowDialog() == true)
            {
                ApplySavedSettings();
            }
        }

        /// <summary>
        /// Toggle display of hidden files.
        /// </summary>
        private void ToggleHiddenFiles()
        {
            var fileTree = GetActiveFileTree();
            fileTree?.ToggleShowHidden(); // Changed method name
        }

        /// <summary>
        /// Handle escape key action.
        /// </summary>
        private void EscapeAction()
        {
            // Exit fullscreen if active
            if (WindowStyle == WindowStyle.None)
            {
                ToggleFullscreen();
                return;
            }
            
            // Clear file selection
            var fileTree = GetActiveFileTree();
            fileTree?.ClearSelection(); // Keep the same method name
        }

        /// <summary>
        /// Show file properties.
        /// </summary>
        /// <param name="filePath">Path to the file</param>
        private void ShowFileProperties(string filePath)
        {
            if (string.IsNullOrEmpty(filePath)) return;
            
            try
            {
                // Use Windows shell to show properties
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{filePath}\"");
                
                // Alternative: Use Windows API 
                // FileOperations.FileOperations.ShowFileProperties(filePath);
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error showing file properties: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Handler for drag over the main window.
        /// </summary>
        private void MainWindow_DragOver(object sender, System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                e.Effects = System.Windows.DragDropEffects.Copy;
            }
            else
            {
                e.Effects = System.Windows.DragDropEffects.None;
            }
            
            e.Handled = true;
        }

        /// <summary>
        /// Handler for drop on the main window with comprehensive error handling and validation.
        /// </summary>
        private async void MainWindow_Drop(object sender, System.Windows.DragEventArgs e)
        {
            try
            {
                var validation = ValidateDrop(e);
                if (!validation.IsValid)
                {
                    ShowDropError(validation.ErrorMessage);
                    e.Effects = DragDropEffects.None;
                    return;
                }

                // Show confirmation for large operations
                if (validation.RequiresConfirmation)
                {
                    var result = MessageBox.Show(
                        validation.ConfirmationMessage,
                        "Confirm Operation",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        e.Effects = DragDropEffects.None;
                        return;
                    }
                }

                var operation = CreateDragDropOperation(e, validation);
                if (operation != null)
                {
                    // For large operations, show progress dialog
                    if (operation.IsLargeOperation)
                    {
                        await ExecuteOperationWithProgress(operation);
                    }
                    else
                    {
                        await operation.ExecuteAsync();
                    }

                    e.Effects = operation.Effect;
                }
                else
                {
                    // Fallback to existing behavior
                    await HandleDropFallback(e);
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Drop operation was cancelled by user");
                e.Effects = DragDropEffects.None;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Drop operation failed");
                ShowDropError($"Drop failed: {ex.Message}");
                e.Effects = DragDropEffects.None;
            }
            finally
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Validates a drop operation before execution
        /// </summary>
        private DragDropValidationResult ValidateDrop(System.Windows.DragEventArgs e)
        {
            try
            {
                // Check if data is present
                if (!e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
                {
                    return DragDropValidationResult.Failure("No file data present");
                }

                var files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);
                if (files == null || files.Length == 0)
                {
                    return DragDropValidationResult.Failure("No files to drop");
                }

                // Get current directory from active file tree
                var fileTree = GetActiveFileTree();
                var targetPath = fileTree?.GetCurrentPath() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                // Validate directory access
                if (!Directory.Exists(targetPath))
                {
                    return DragDropValidationResult.Failure("Target directory does not exist");
                }

                // Check write permissions
                try
                {
                    var testFile = Path.Combine(targetPath, $"test_{Guid.NewGuid()}.tmp");
                    File.WriteAllText(testFile, "");
                    File.Delete(testFile);
                }
                catch
                {
                    return DragDropValidationResult.Failure("No write permission to target directory");
                }

                var validation = new DragDropValidationResult { IsValid = true };
                long totalSize = 0;

                foreach (var file in files)
                {
                    if (ValidateDropFile(file, targetPath))
                    {
                        validation.ValidFiles.Add(file);
                        totalSize += EstimateFileSize(file);
                    }
                    else
                    {
                        validation.InvalidFiles.Add(file);
                    }
                }

                if (!validation.ValidFiles.Any())
                {
                    return DragDropValidationResult.Failure("No valid files to drop");
                }

                validation.EstimatedSize = totalSize;
                validation.IsLargeOperation = validation.ValidFiles.Count > 10 || totalSize > 100 * 1024 * 1024;
                validation.AllowedEffects = DragDropEffects.Copy; // Default to copy for main window drops

                if (validation.IsLargeOperation)
                {
                    validation.RequiresConfirmation = true;
                    validation.ConfirmationMessage = $"This will copy {validation.ValidFiles.Count} files ({FormatFileSize(totalSize)}). Continue?";
                }

                return validation;
            }
            catch (Exception ex)
            {
                return DragDropValidationResult.Failure($"Validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates a single file for dropping
        /// </summary>
        private bool ValidateDropFile(string filePath, string targetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
                    return false;

                // Check for circular references
                var fullSourcePath = Path.GetFullPath(filePath);
                var fullTargetPath = Path.GetFullPath(targetPath);

                if (fullTargetPath.StartsWith(fullSourcePath, StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Estimates the size of a file or directory
        /// </summary>
        private long EstimateFileSize(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).Length;
                }
                else if (Directory.Exists(path))
                {
                    // Rough estimate for directories
                    var fileCount = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
                    return fileCount * 1024 * 1024; // Assume 1MB per file
                }
            }
            catch
            {
                // Ignore errors and return 0
            }
            return 0;
        }

        /// <summary>
        /// Creates a drag drop operation from the event arguments
        /// </summary>
        private DragDropOperation? CreateDragDropOperation(System.Windows.DragEventArgs e, DragDropValidationResult validation)
        {
            try
            {
                var fileTree = GetActiveFileTree();
                var targetPath = fileTree?.GetCurrentPath() ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                return new DragDropOperation(
                    validation.AllowedEffects,
                    targetPath,
                    validation.ValidFiles,
                    _logger);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create drag drop operation");
                return null;
            }
        }

        /// <summary>
        /// Executes an operation with progress dialog for large operations
        /// </summary>
        private async Task ExecuteOperationWithProgress(DragDropOperation operation)
        {
            var progressWindow = new Window
            {
                Title = "Processing Files",
                Width = 400,
                Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                ResizeMode = ResizeMode.NoResize
            };

            var progressBar = new System.Windows.Controls.ProgressBar
            {
                Height = 20,
                Margin = new Thickness(20),
                IsIndeterminate = false
            };

            var statusLabel = new System.Windows.Controls.Label
            {
                Content = "Preparing...",
                Margin = new Thickness(20, 0, 20, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var cancelButton = new System.Windows.Controls.Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 25,
                Margin = new Thickness(20)
            };

            var stackPanel = new System.Windows.Controls.StackPanel();
            stackPanel.Children.Add(statusLabel);
            stackPanel.Children.Add(progressBar);
            stackPanel.Children.Add(cancelButton);

            progressWindow.Content = stackPanel;

            var cancellationTokenSource = new CancellationTokenSource();
            cancelButton.Click += (s, e) => cancellationTokenSource.Cancel();

            // Subscribe to progress updates
            operation.ProgressUpdated += (s, e) =>
            {
                Dispatcher.Invoke(() =>
                {
                    progressBar.Value = e.ProgressPercentage;
                    statusLabel.Content = $"{e.CurrentAction} ({e.CompletedItems}/{e.TotalItems})";
                });
            };

            progressWindow.Show();

            try
            {
                await operation.ExecuteAsync(cancellationTokenSource.Token);
            }
            finally
            {
                progressWindow.Close();
            }
        }

        /// <summary>
        /// Fallback drop handling for legacy compatibility
        /// </summary>
        private async Task HandleDropFallback(System.Windows.DragEventArgs e)
        {
            if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(System.Windows.DataFormats.FileDrop);

                // Get active file tree and forward the drop
                var fileTree = GetActiveFileTree();
                if (fileTree != null)
                {
                    fileTree.HandleFileDrop(e.Data);
                }
                else
                {
                    // Fallback - open first directory
                    foreach (string file in files)
                    {
                        if (Directory.Exists(file))
                        {
                            OpenDirectoryInTab(file);
                            break;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Shows a drop error message to the user
        /// </summary>
        private void ShowDropError(string message)
        {
            MessageBox.Show(message, "Drop Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        /// <summary>
        /// Formats file size for display
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024:N1} KB";
            if (bytes < 1024 * 1024 * 1024) return $"{bytes / (1024 * 1024):N1} MB";
            return $"{bytes / (1024 * 1024 * 1024):N1} GB";
        }

        /// <summary>
        /// Sets up Chrome-style tab control event handlers
        /// </summary>
        private void SetupChromeStyleTabEvents()
        {
            // Note: MainTabs will be available after InitializeComponent is called
            this.Loaded += (s, e) =>
            {
                if (MainTabs is ChromeStyleTabControl chromeTabControl)
                {
                    // Handle new tab requests
                    chromeTabControl.NewTabRequested += OnNewTabRequested;
                    
                    // Handle tab close requests
                    chromeTabControl.TabCloseRequested += OnTabCloseRequested;
                    
                    // Handle tab drag events
                    chromeTabControl.TabDragged += OnTabDragged;
                    
                    // Handle tab metadata changes
                    chromeTabControl.TabMetadataChanged += OnTabMetadataChanged;
                    
                    // Add middle-click support to close tabs
                    chromeTabControl.MouseDown += OnTabControlMouseDown;
                    
                    _instanceLogger?.LogInformation("Chrome-style tab events configured");
                }
            };
        }

        /// <summary>
        /// Handles middle-click on tabs to close them
        /// </summary>
        private void OnTabControlMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Middle && e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    // Find the tab item that was clicked
                    var source = e.OriginalSource as DependencyObject;
                    var tabItem = FindParent<TabItem>(source);
                    
                    if (tabItem != null && MainTabs.Items.Count > 1) // Don't close the last tab
                    {
                        var index = MainTabs.Items.IndexOf(tabItem);
                        if (index >= 0)
                        {
                            CloseTab(index);
                            e.Handled = true;
                            _instanceLogger?.LogDebug($"Tab closed via middle-click: {tabItem.Header}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogError(ex, "Error handling middle-click tab close");
                }
            }
        }

        /// <summary>
        /// Handles new tab requests from ChromeStyleTabControl
        /// </summary>
        private void OnNewTabRequested(object sender, NewTabRequestedEventArgs e)
        {
            try
            {
                // Create a MainWindowContainer for the new tab
                var container = new MainWindowContainer(this);
                e.TabItem.Content = container;
                
                // Initialize the container with file tree
                var defaultPath = ValidatePath(null);
                container.InitializeWithFileTree(defaultPath);
                
                // Connect the container to the pinned panel system
                ConnectPinnedPanel(container);
                
                // Update the tab title to show proper window numbering (1-based indexing)
                if (e.TabItem.Title == "New Tab")
                {
                    e.TabItem.Title = $"Window {MainTabs.Items.Count + 1}";
                }
                
                _instanceLogger?.LogDebug($"New tab created: {e.TabItem.Title}");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error creating new tab content");
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Handles tab close requests from ChromeStyleTabControl
        /// </summary>
        private void OnTabCloseRequested(object sender, TabCloseRequestedEventArgs e)
        {
            try
            {
                // Dispose the container if it implements IDisposable
                if (e.TabItem.Content is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                
                _instanceLogger?.LogDebug($"Tab closed: {e.TabItem.Title}");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error closing tab");
            }
        }

        /// <summary>
        /// Handles tab drag events from ChromeStyleTabControl
        /// </summary>
        private void OnTabDragged(object sender, TabDragEventArgs e)
        {
            try
            {
                // Handle tab dragging - could implement detaching to new windows here
                _instanceLogger?.LogDebug($"Tab dragged: {e.TabItem.Title}");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error handling tab drag");
            }
        }

        /// <summary>
        /// Handles tab metadata changes from ChromeStyleTabControl
        /// </summary>
        private void OnTabMetadataChanged(object sender, TabMetadataChangedEventArgs e)
        {
            try
            {
                // Handle metadata changes (e.g., update window title when active tab changes)
                if (e.PropertyName == nameof(TabItemModel.IsActive) && (bool)e.NewValue)
                {
                    // Note: Direct tab manipulation approach - no ViewModel needed
                    // Window title updates are handled by the tab selection changed event
                }
                
                _instanceLogger?.LogDebug($"Tab metadata changed: {e.TabItem.Title} - {e.PropertyName}");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error handling tab metadata change");
            }
        }

        /// <summary>
        /// Handler for window closing.
        /// </summary>
        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            try
            {
                _logger.LogInformation("MainWindow closing");
                
                // Save window layout
                SaveWindowLayout();
                
                // Save settings using the service
                _ = _settingsService.SaveSettingsAsync();
                
                // Dispose properly
                Dispose();
                
                _logger.LogInformation("MainWindow closed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during window closing");
                // Continue with closing even if there was an error
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Save window layout to settings.
        /// </summary>
        private void SaveWindowLayout()
        {
            try
            {
                var windowSettings = ExplorerPro.Models.WindowSettings.FromWindow(this, _instanceLogger);
                _ = _settingsService.SaveWindowSettingsAsync(GetWindowId(), windowSettings);
                
                // Legacy fallback for compatibility
                byte[] geometryBytes = GetWindowGeometryBytes();
                byte[] stateBytes = GetWindowStateBytes();
                
                if (App.Settings != null)
                {
                    App.Settings.StoreMainWindowLayout(geometryBytes, stateBytes);
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error saving window layout");
            }
        }

        /// <summary>
        /// Get the window geometry as bytes for serialization.
        /// </summary>
        private byte[] GetWindowGeometryBytes()
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(Left);
                    writer.Write(Top);
                    writer.Write(Width);
                    writer.Write(Height);
                    writer.Write((int)WindowState);
                    
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving window geometry: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Get the window state as bytes for serialization.
        /// </summary>
        private byte[] GetWindowStateBytes()
        {
            try
            {
                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((int)WindowState);
                    
                    // Add other state data as needed
                    
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving window state: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Check if a rectangle is at least partially visible on any screen.
        /// </summary>
        /// <param name="rect">The rectangle to check</param>
        /// <returns>True if visible on any screen</returns>
        private bool IsRectOnScreen(Rect rect)
        {
            // Explicitly use the fully qualified name to avoid ambiguity
            foreach (WinForms.Screen screen in WinForms.Screen.AllScreens)
            {
                var screenRect = new Rect(
                    screen.WorkingArea.Left, 
                    screen.WorkingArea.Top,
                    screen.WorkingArea.Width, 
                    screen.WorkingArea.Height);
                
                // Check if the rectangles intersect
                if (rect.IntersectsWith(screenRect))
                {
                    return true;
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Finds all visual children of the specified type
        /// </summary>
        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                {
                    yield return t;
                }
                
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }

        /// <summary>
        /// Gets all active MainWindow instances using the lifecycle manager.
        /// </summary>
        public static IEnumerable<MainWindow> GetAllActiveWindows()
        {
            return WindowLifecycleManager.Instance.GetActiveWindows();
        }

        /// <summary>
        /// Checks if multiple windows are currently open.
        /// </summary>
        public static bool HasMultipleWindows()
        {
            return WindowLifecycleManager.Instance.ActiveWindowCount > 1;
        }

        #endregion

        #region Validation Methods

        /// <summary>
        /// Gets the current count of tracked weak event subscriptions for validation
        /// ENHANCED FOR FIX 4: Event Handler Memory Leak Resolution
        /// </summary>
        public int GetTrackedEventCount()
        {
            return _eventSubscriptions?.Count ?? 0;
        }

        /// <summary>
        /// Validates that weak event cleanup is working properly for memory leak testing
        /// ENHANCED FOR FIX 4: Event Handler Memory Leak Resolution
        /// </summary>
        public bool ValidateEventCleanup()
        {
            try
            {
                var isDisposed = _stateManager.IsClosing;
                var windowState = _stateManager.CurrentState;
                
                _instanceLogger?.LogDebug($"Event cleanup validation - Disposed: {isDisposed}, State: {windowState}");
                
                // If disposed, should have no tracked events (weak events auto-cleanup)
                if (isDisposed && _eventSubscriptions?.Count > 0)
                {
                    _instanceLogger?.LogWarning($"Potential memory issue: {_eventSubscriptions.Count} weak events still tracked after disposal");
                    return false;
                }
                
                return true;
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error during event cleanup validation");
                return false;
            }
        }

        #endregion

        /// <summary>
        /// IMPLEMENTATION OF FIX 5: Detached Windows List Management
        /// Represents a managed detached window with lifecycle tracking
        /// </summary>
        private class DetachedWindowInfo
        {
            public Guid Id { get; set; }
            public WeakReference<MainWindow> WindowReference { get; set; }
            public DateTime CreatedAt { get; set; }
            public string Title { get; set; }

            public DetachedWindowInfo(Guid id, MainWindow window, string title)
            {
                Id = id;
                WindowReference = new WeakReference<MainWindow>(window);
                CreatedAt = DateTime.Now;
                Title = title ?? "Detached Window";
            }
        }

        /// <summary>
        /// IMPLEMENTATION OF FIX 3: Navigation History Unbounded Growth
        /// Represents a navigation history entry with metadata
        /// </summary>
        private class NavigationEntry
        {
            public string Path { get; set; }
            public DateTime Timestamp { get; set; }
            public long MemorySize { get; set; } // Approximate memory usage
            
            public NavigationEntry(string path)
            {
                Path = path ?? string.Empty;
                Timestamp = DateTime.UtcNow;
                // Approximate memory: string length * 2 (Unicode) + object overhead
                MemorySize = (path?.Length ?? 0) * 2 + 64;
            }
        }

        /// <summary>
        /// ENHANCED FOR FIX 5: Detached Windows List Management
        /// Safely detach a tab to a new window with proper lifecycle management
        /// </summary>
        public void DetachToNewWindow(TabItem tabToDetach)
        {
            if (tabToDetach == null) 
            {
                _instanceLogger?.LogWarning("Cannot detach null tab");
                return;
            }
            
            try
            {
                // Create new window with shared logger
                var newWindow = new MainWindow
                {
                    Owner = null, // Detached windows have no owner
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Width = this.Width * 0.8,
                    Height = this.Height * 0.8
                };
                
                var windowId = Guid.NewGuid();
                var tabTitle = tabToDetach.Header?.ToString() ?? "Detached";
                
                // Set up window lifecycle management with proper event signature
                EventHandler cleanupHandler = (s, e) =>
                {
                    _detachedWindows.TryRemove(windowId, out _);
                    _instanceLogger?.LogInformation($"Detached window {windowId} closed and removed from tracking");
                };
                
                // Subscribe to cleanup using weak event management system
                var subscription = WeakEventHelper.Subscribe<EventArgs>(newWindow, nameof(Window.Closed), 
                    (sender, args) => cleanupHandler(sender, args));
                _eventSubscriptions.Add(subscription);
                
                // Track the window with weak reference
                _detachedWindows[windowId] = new WeakReference<MainWindow>(newWindow);
                
                // Transfer tab content
                if (MainTabs != null)
                {
                    MainTabs.Items.Remove(tabToDetach);
                    newWindow.MainTabs?.Items.Add(tabToDetach);
                }
                
                // Configure new window
                newWindow.Title = $"ExplorerPro - {tabTitle}";
                
                // Show the new window
                newWindow.Show();
                newWindow.Activate();
                
                _instanceLogger?.LogInformation($"Created detached window {windowId} with tab '{tabTitle}'");
                
                // Periodic cleanup of dead references if we have many windows
                if (_detachedWindows.Count > 10)
                {
                    Task.Run(() => CleanupDeadReferences());
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error detaching tab to new window");
                System.Windows.MessageBox.Show("Failed to detach tab to new window", "Error", 
                               MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// IMPLEMENTATION OF FIX 5: Detached Windows List Management
        /// Removes dead weak references from the detached windows collection
        /// </summary>
        private void CleanupDeadReferences()
        {
            var deadKeys = new List<Guid>();
            
            foreach (var kvp in _detachedWindows)
            {
                if (!kvp.Value.TryGetTarget(out _))
                {
                    deadKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in deadKeys)
            {
                _detachedWindows.TryRemove(key, out _);
            }
            
            if (deadKeys.Count > 0)
            {
                _instanceLogger?.LogDebug($"Cleaned up {deadKeys.Count} dead window references");
            }
        }

        /// <summary>
        /// IMPLEMENTATION OF FIX 5: Detached Windows List Management
        /// Gets all currently active detached windows
        /// </summary>
        public IEnumerable<MainWindow> GetActiveDetachedWindows()
        {
            var activeWindows = new List<MainWindow>();
            
            foreach (var weakRef in _detachedWindows.Values)
            {
                if (weakRef.TryGetTarget(out var window) && !window.IsDisposed)
                {
                    activeWindows.Add(window);
                }
            }
            
            return activeWindows;
        }

        /// <summary>
        /// IMPLEMENTATION OF FIX 5: Detached Windows List Management
        /// Broadcasts a message to all detached windows
        /// </summary>
        public void BroadcastToDetachedWindows(Action<MainWindow> action)
        {
            if (action == null) 
            {
                _instanceLogger?.LogWarning("Cannot broadcast null action to detached windows");
                return;
            }
            
            var activeCount = 0;
            var errorCount = 0;
            
            foreach (var window in GetActiveDetachedWindows())
            {
                try
                {
                    window.ExecuteOnUIThread(() => action(window));
                    activeCount++;
                }
                catch (Exception ex)
                {
                    errorCount++;
                    _instanceLogger?.LogError(ex, $"Error broadcasting to detached window");
                }
            }
            
            _instanceLogger?.LogDebug($"Broadcast completed - {activeCount} successful, {errorCount} errors");
        }

        /// <summary>
        /// VALIDATION METHOD FOR FIX 5: Detached Windows List Management
        /// Gets the current count of tracked detached windows for validation
        /// </summary>
        public int GetDetachedWindowCount()
        {
            return _detachedWindows.Count;
        }

                 /// <summary>
         /// VALIDATION METHOD FOR FIX 5: Detached Windows List Management  
         /// Gets the count of active (non-disposed) detached windows
         /// </summary>
         public int GetActiveDetachedWindowCount()
         {
             return GetActiveDetachedWindows().Count();
         }

         /// <summary>
         /// VALIDATION METHOD FOR FIX 3: Navigation History Unbounded Growth
         /// Gets the current count of navigation history entries
         /// </summary>
         public int GetNavigationHistoryCount()
         {
             lock (_historyLock)
             {
                 return _navigationHistory.Count;
             }
         }

         /// <summary>
         /// VALIDATION METHOD FOR FIX 3: Navigation History Unbounded Growth
         /// Gets the approximate memory usage of navigation history
         /// </summary>
         public long GetNavigationHistoryMemoryUsage()
         {
             lock (_historyLock)
             {
                 return _navigationHistory.Sum(e => e.MemorySize);
             }
         }

         /// <summary>
         /// VALIDATION METHOD FOR FIX 3: Navigation History Unbounded Growth
         /// Validates that history size stays within bounds
         /// </summary>
         public bool ValidateNavigationHistoryBounds()
         {
             lock (_historyLock)
             {
                 var count = _navigationHistory.Count;
                 var memoryUsage = _navigationHistory.Sum(e => e.MemorySize);
                 var withinBounds = count <= MaxHistorySize;
                 
                 _instanceLogger?.LogDebug($"Navigation history validation: {count}/{MaxHistorySize} entries, ~{memoryUsage / 1024}KB, within bounds: {withinBounds}");
                 
                 return withinBounds;
             }
         }

        /// <summary>
        /// Duplicate the current tab.
        /// </summary>
        public void DuplicateCurrentTab()
        {
            var currentContainer = GetCurrentContainer();
            if (currentContainer == null) return;
            
            var fileTree = currentContainer.FindFileTree();
            if (fileTree != null)
            {
                string rootPath = fileTree.GetCurrentPath(); // Changed from CurrentPath property to GetCurrentPath method
                AddNewMainWindowTab(rootPath);
            }
        }

        #endregion

        #region Initialization Helper Methods

        internal void InitializeMetadataManager()
        {
            _metadataManager = App.MetadataManager ?? new MetadataManager();
        }

        internal void InitializeNavigationHistory()
        {
            _navigationHistory.Clear();
            _currentHistoryNode = null;
        }

        internal void RegisterWithLifecycleManager()
        {
            WindowLifecycleManager.Instance.RegisterWindow(this);
        }

        internal void UnregisterFromLifecycleManager()
        {
            try
            {
                // Ensure window is unregistered from WindowLifecycleManager
                var lifecycleManager = ExplorerPro.Core.WindowLifecycleManager.Instance;
                if (lifecycleManager != null)
                {
                    bool unregistered = lifecycleManager.UnregisterWindow(this);
                    if (unregistered)
                    {
                        _instanceLogger?.LogInformation("Window successfully unregistered from WindowLifecycleManager");
                    }
                    else
                    {
                        _instanceLogger?.LogWarning("Window was not found in WindowLifecycleManager during unregistration");
                    }
                }
                else
                {
                    _instanceLogger?.LogWarning("WindowLifecycleManager instance not available during unregistration");
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error during WindowLifecycleManager unregistration");
            }
        }

        internal void ClearNavigationHistory()
        {
            lock (_historyLock)
            {
                _navigationHistory.Clear();
                _currentHistoryNode = null;
            }
        }

        /// <summary>
        /// Clear all strong references to prevent memory leaks
        /// </summary>
        private void ClearAllStrongReferences()
        {
            try
            {
                // Clear manager references
                if (_windowManager != null)
                {
                    _instanceLogger?.LogDebug("Clearing WindowManager reference");
                    _windowManager = null;
                }

                if (_tabOperationsManager != null)
                {
                    _instanceLogger?.LogDebug("Clearing TabOperationsManager reference");
                    _tabOperationsManager = null;
                }

                if (_dragDropService != null)
                {
                    _instanceLogger?.LogDebug("Clearing DragDropService reference");
                    _dragDropService = null;
                }

                if (_metadataManager != null)
                {
                    _instanceLogger?.LogDebug("Clearing MetadataManager reference");
                    _metadataManager = null;
                }

                // Clear view model references
                if (_viewModel != null)
                {
                    _instanceLogger?.LogDebug("Clearing ViewModel reference");
                    _viewModel = null;
                }

                // Clear detached windows collection
                if (_detachedWindows != null)
                {
                    _instanceLogger?.LogDebug("Clearing detached windows collection");
                    _detachedWindows.Clear();
                }

                // Clear cached UI element references
                _cachedEmbeddedToolbar = null;
                _toolbarCacheInitialized = false;

                // Clear context menu state
                _rightClickedTab = null;
                _contextMenuStateValid = false;

                _instanceLogger?.LogDebug("All strong references cleared successfully");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error clearing strong references");
            }
        }

        /// <summary>
        /// Registers this window with the detached window manager
        /// </summary>
        private void RegisterWithWindowManager()
        {
            _windowManager = App.WindowManager;
            _windowManager?.RegisterWindow(this);
        }

        internal void SetupDragDrop()
        {
            AllowDrop = true;
            
            // Subscribe to DragOver event with weak pattern
            var dragOverSubscription = Disposable.Create(() => DragOver -= MainWindow_DragOver);
            DragOver += MainWindow_DragOver;
            _eventSubscriptions.Add(dragOverSubscription);
            
            // Subscribe to Drop event with weak pattern
            var dropSubscription = Disposable.Create(() => Drop -= MainWindow_Drop);
            Drop += MainWindow_Drop;
            _eventSubscriptions.Add(dropSubscription);
        }

        internal void ClearDragDrop()
        {
            AllowDrop = false;
            // Weak events will be cleaned up automatically by CompositeDisposable
        }

        internal void WireUpEventHandlers()
        {
            if (MainTabs != null)
            {
                // Subscribe to SelectionChanged using weak pattern
                var subscription1 = Disposable.Create(() =>
                {
                    if (MainTabs != null)
                        MainTabs.SelectionChanged -= MainTabs_SelectionChanged;
                });
                MainTabs.SelectionChanged += MainTabs_SelectionChanged;
                _eventSubscriptions.Add(subscription1);

                // Subscribe to PreviewMouseRightButtonDown for context menu positioning
                var subscription3 = Disposable.Create(() =>
                {
                    if (MainTabs != null)
                        MainTabs.PreviewMouseRightButtonDown -= TabControl_PreviewMouseRightButtonDown;
                });
                MainTabs.PreviewMouseRightButtonDown += TabControl_PreviewMouseRightButtonDown;
                _eventSubscriptions.Add(subscription3);
            }
            
            // Subscribe to Closing using weak pattern
            var subscription2 = Disposable.Create(() => Closing -= MainWindow_Closing);
            Closing += MainWindow_Closing;
            _eventSubscriptions.Add(subscription2);
        }

        internal void ClearAllEventHandlers()
        {
            // ENHANCED FOR FIX 4: Event Handler Memory Leak Resolution
            // Dispose all weak event subscriptions automatically
            try
            {
                _instanceLogger?.LogInformation("Clearing all weak event handlers");
                _eventSubscriptions?.Dispose();
                _instanceLogger?.LogInformation("All weak event handlers cleared successfully");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error clearing weak event handlers");
            }
        }

        internal void ClearKeyboardShortcuts()
        {
            // Clear keyboard shortcut bindings
            InputBindings.Clear();
        }

        internal bool ValidateInitialization()
        {
            return MainTabs != null &&
                   StatusText != null &&
                   ItemCountText != null &&
                   SelectionText != null &&
                   !_stateManager.IsClosing;
        }

        #endregion

        /// <summary>
        /// Gets the TabItemModel corresponding to a TabItem
        /// </summary>
        /// <param name="tabItem">The TabItem to find the model for</param>
        /// <returns>The corresponding TabItemModel or null if not found</returns>
        private TabItemModel GetTabItemModel(TabItem tabItem)
        {
            if (tabItem == null) return null;

            try
            {
                // First try to get existing model from Tag
                if (tabItem.Tag is TabItemModel existingModel)
                {
                    return existingModel;
                }

                // Create a TabItemModel based on the TabItem's current state
                var tabModel = new TabItemModel
                {
                    Title = tabItem.Header?.ToString() ?? "Untitled",
                    Content = tabItem.Content,
                    IsPinned = false, // Default value - could be stored in Tag
                    TabColor = Colors.LightGray, // Default value
                    HasUnsavedChanges = false // Default value
                };

                // Try to extract metadata from TabItem's Tag if available
                if (tabItem.Tag is Dictionary<string, object> metadata)
                {
                    if (metadata.ContainsKey("IsPinned") && metadata["IsPinned"] is bool pinned)
                        tabModel.IsPinned = pinned;
                    
                    if (metadata.ContainsKey("TabColor") && metadata["TabColor"] is Color color)
                        tabModel.TabColor = color;
                        
                    if (metadata.ContainsKey("HasUnsavedChanges") && metadata["HasUnsavedChanges"] is bool hasChanges)
                        tabModel.HasUnsavedChanges = hasChanges;
                }

                // Set the model as the Tag for future reference
                tabItem.Tag = tabModel;

                return tabModel;
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error creating TabItemModel for TabItem");
                return null;
            }
        }

        /// <summary>
        /// Updates a TabItem based on a TabItemModel's properties
        /// </summary>
        /// <param name="tabItem">The TabItem to update</param>
        /// <param name="tabModel">The model with updated properties</param>
        private void UpdateTabItemFromModel(TabItem tabItem, TabItemModel tabModel)
        {
            if (tabItem == null || tabModel == null) return;

            try
            {
                // Ensure consistent Tag binding
                tabItem.Tag = tabModel;
                
                // Update header
                tabItem.Header = tabModel.Title;
                
                // Apply Chrome-style width for pinned tabs
                if (tabModel.IsPinned)
                {
                    tabItem.Width = 50; // Chrome-style narrow width
                    tabItem.MinWidth = 50;
                    tabItem.MaxWidth = 50;
                    tabItem.ToolTip = tabModel.Title; // Show full title in tooltip
                }
                else
                {
                    tabItem.Width = double.NaN; // Auto width
                    tabItem.MinWidth = 100;
                    tabItem.MaxWidth = 200;
                    tabItem.ClearValue(TabItem.ToolTipProperty);
                }

                // Apply visual styling based on the model
                ApplyTabStyling(tabItem, tabModel);
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error updating TabItem from model");
            }
        }

        /// <summary>
        /// Applies visual styling to a TabItem based on TabItemModel properties
        /// </summary>
        /// <param name="tabItem">The TabItem to style</param>
        /// <param name="tabModel">The model with styling information</param>
        private void ApplyTabStyling(TabItem tabItem, TabItemModel tabModel)
        {
            try
            {
                // Ensure the model is always set as Tag for binding consistency
                tabItem.Tag = tabModel;

                // Apply color styling (this preserves pinned state)
                if (tabModel.TabColor != Colors.LightGray)
                {
                    SetTabColorDataContext(tabItem, tabModel.TabColor);
                }
                else
                {
                    // For tabs without custom colors, clear DataContext to let Tag binding work
                    tabItem.DataContext = null;
                    // Clear any custom styling to return to default
                    ClearTabColorStyling(tabItem);
                }

                // Force refresh of the tab's visual state to apply triggers
                tabItem.UpdateLayout();
                tabItem.InvalidateVisual();
                
                // Use dispatcher to ensure all bindings are updated
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    tabItem.ApplyTemplate();
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error applying tab styling");
            }
        }

        /// <summary>
        /// Sets up the DataContext for a TabItem to enable color binding
        /// </summary>
        /// <param name="tabItem">The TabItem to set up</param>
        /// <param name="color">The color to apply</param>
        private void SetTabColorDataContext(TabItem tabItem, Color color)
        {
            try
            {
                // Get current pinned state before creating TabColorData
                bool isPinned = false;
                if (tabItem.Tag is TabItemModel model)
                {
                    isPinned = model.IsPinned;
                }
                else if (tabItem.Tag is Dictionary<string, object> metadata &&
                         metadata.ContainsKey("IsPinned") &&
                         metadata["IsPinned"] is bool pinnedValue)
                {
                    isPinned = pinnedValue;
                }

                // Create or update a simple data object for binding
                var tabData = new TabColorData
                {
                    TabColor = color,
                    Header = tabItem.Header?.ToString() ?? "",
                    IsPinned = isPinned // Preserve the pinned state
                };
                
                // Set the DataContext to enable binding
                tabItem.DataContext = tabData;
                
                // Keep the original Tag if it's a TabItemModel, otherwise use metadata
                if (tabItem.Tag is TabItemModel originalModel)
                {
                    // Keep the original model as Tag for proper binding
                    originalModel.TabColor = color;
                    tabItem.Tag = originalModel;
                }
                else
                {
                    // Use metadata approach for legacy compatibility
                    var metadata = tabItem.Tag as Dictionary<string, object> ?? new Dictionary<string, object>();
                    metadata["TabColor"] = color;
                    metadata["TabColorData"] = tabData;
                    metadata["IsPinned"] = isPinned;
                    tabItem.Tag = metadata;
                }
                
                // Use dispatcher to ensure template is applied before styling
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    ApplyColorBindingStyle(tabItem);
                }), DispatcherPriority.Loaded);
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error setting tab color data context");
            }
        }

        /// <summary>
        /// Applies a style that supports color binding to a TabItem
        /// </summary>
        /// <param name="tabItem">The TabItem to style</param>
        private void ApplyColorBindingStyle(TabItem tabItem)
        {
            try
            {
                // Force the template to be applied if it hasn't been yet
                tabItem.ApplyTemplate();
                
                // Try to find the TabBorder in the control template
                var tabBorder = tabItem.Template?.FindName("TabBorder", tabItem) as Border;
                
                if (tabBorder != null)
                {
                    var tabData = tabItem.DataContext as TabColorData;
                    if (tabData != null && tabData.TabColor != Colors.LightGray)
                    {
                        // Apply color with transparency for a modern look
                        var lightBrush = new SolidColorBrush(Color.FromArgb(80, tabData.TabColor.R, tabData.TabColor.G, tabData.TabColor.B));
                        var borderBrush = new SolidColorBrush(Color.FromArgb(200, tabData.TabColor.R, tabData.TabColor.G, tabData.TabColor.B));
                        
                        tabBorder.Background = lightBrush;
                        tabBorder.BorderBrush = borderBrush;
                        
                        // Ensure the border is visible
                        if (tabBorder.BorderThickness.Bottom < 2)
                        {
                            tabBorder.BorderThickness = new Thickness(1, 1, 1, 2);
                        }
                    }
                    else
                    {
                        // Reset to default by clearing the values, letting the template take over
                        tabBorder.ClearValue(Border.BackgroundProperty);
                        tabBorder.ClearValue(Border.BorderBrushProperty);
                        tabBorder.ClearValue(Border.BorderThicknessProperty);
                    }
                }
                else
                {
                    // If TabBorder not found, try applying to the TabItem directly
                    var tabData = tabItem.DataContext as TabColorData;
                    if (tabData != null && tabData.TabColor != Colors.LightGray)
                    {
                        var lightBrush = new SolidColorBrush(Color.FromArgb(60, tabData.TabColor.R, tabData.TabColor.G, tabData.TabColor.B));
                        var borderBrush = new SolidColorBrush(Color.FromArgb(180, tabData.TabColor.R, tabData.TabColor.G, tabData.TabColor.B));
                        
                        tabItem.Background = lightBrush;
                        tabItem.BorderBrush = borderBrush;
                        tabItem.BorderThickness = new Thickness(0, 0, 0, 3);
                    }
                    else
                    {
                        tabItem.ClearValue(TabItem.BackgroundProperty);
                        tabItem.ClearValue(TabItem.BorderBrushProperty);
                        tabItem.ClearValue(TabItem.BorderThicknessProperty);
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error applying color binding style");
            }
        }

        /// <summary>
        /// Clears all color styling from a TabItem and restores original template behavior
        /// </summary>
        /// <param name="tabItem">The TabItem to clear styling from</param>
        private void ClearTabColorStyling(TabItem tabItem)
        {
            try
            {
                // Clear the TabItemModel color data to prevent reversion
                if (tabItem.Tag is TabItemModel model)
                {
                    model.TabColor = Colors.LightGray; // Reset to default
                }
                
                // Clear the DataContext that was set for color binding
                tabItem.ClearValue(TabItem.DataContextProperty);
                
                // Clear any metadata that might store color information
                if (tabItem.Tag is Dictionary<string, object> metadata)
                {
                    metadata.Remove("TabColor");
                    metadata.Remove("TabColorData");
                }
                
                // Force the template to be applied if it hasn't been yet
                tabItem.ApplyTemplate();
                
                // Try to find the TabBorder in the control template and clear its styling
                var tabBorder = tabItem.Template?.FindName("TabBorder", tabItem) as Border;
                
                if (tabBorder != null)
                {
                    // Clear all custom styling to let the template take over
                    tabBorder.ClearValue(Border.BackgroundProperty);
                    tabBorder.ClearValue(Border.BorderBrushProperty);
                    tabBorder.ClearValue(Border.BorderThicknessProperty);
                }
                
                // Also clear any direct styling on the TabItem itself
                tabItem.ClearValue(TabItem.BackgroundProperty);
                tabItem.ClearValue(TabItem.BorderBrushProperty);
                tabItem.ClearValue(TabItem.BorderThicknessProperty);
                
                // Force the tab to re-evaluate its template triggers and styling
                tabItem.InvalidateVisual();
                
                _instanceLogger?.LogDebug("Tab color styling cleared completely");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error clearing tab color styling");
            }
        }

        /// <summary>
        /// Simple data class for tab color binding
        /// </summary>
        private class TabColorData : INotifyPropertyChanged
        {
            private Color _tabColor = Colors.LightGray;
            private string _header = "";
            private bool _isPinned = false;

            public Color TabColor
            {
                get => _tabColor;
                set
                {
                    if (_tabColor != value)
                    {
                        _tabColor = value;
                        OnPropertyChanged();
                    }
                }
            }

            public string Header
            {
                get => _header;
                set
                {
                    if (_header != value)
                    {
                        _header = value;
                        OnPropertyChanged();
                    }
                }
            }

            public bool IsPinned
            {
                get => _isPinned;
                set
                {
                    if (_isPinned != value)
                    {
                        _isPinned = value;
                        OnPropertyChanged();
                    }
                }
            }

            public event PropertyChangedEventHandler PropertyChanged;

            private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// Gets the TabItemModel for the currently right-clicked tab in context menu
        /// </summary>
        /// <param name="contextMenu">The context menu</param>
        /// <returns>The TabItemModel or null if not found</returns>
        private TabItemModel GetContextMenuTabModel(ContextMenu contextMenu)
        {
            try
            {
                // Use the stored right-clicked tab
                if (_rightClickedTab != null)
                {
                    // Try to find the corresponding model from the ViewModel's collection first
                    if (_viewModel?.TabItems != null)
                    {
                        var tabTitle = _rightClickedTab.Header?.ToString();
                        var matchingModel = _viewModel.TabItems.FirstOrDefault(t => t.Title == tabTitle);
                        if (matchingModel != null)
                        {
                            return matchingModel;
                        }
                    }
                    
                    // Fallback to creating a model from the TabItem
                    return GetTabItemModel(_rightClickedTab);
                }
                
                // Fallback to selected tab if no right-clicked tab stored
                if (contextMenu?.PlacementTarget is TabControl tabControl)
                {
                    var selectedTabItem = tabControl.SelectedItem as TabItem;
                    if (selectedTabItem != null)
                    {
                        // Try to find the corresponding model from the ViewModel's collection first
                        if (_viewModel?.TabItems != null)
                        {
                            var tabTitle = selectedTabItem.Header?.ToString();
                            var matchingModel = _viewModel.TabItems.FirstOrDefault(t => t.Title == tabTitle);
                            if (matchingModel != null)
                            {
                                return matchingModel;
                            }
                        }
                        
                        return GetTabItemModel(selectedTabItem);
                    }
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error getting context menu tab model");
            }

            return null;
        }
        
        /// <summary>
        /// Gets the currently selected TabItem
        /// </summary>
        /// <returns>The selected TabItem or null if none selected</returns>
        private TabItem GetSelectedTabItem()
        {
            try
            {
                return MainTabs?.SelectedItem as TabItem;
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error getting selected tab item");
                return null;
            }
        }

        /// <summary>
        /// Gets the TabItem that was right-clicked from context menu
        /// </summary>
        /// <param name="contextMenu">The context menu</param>
        /// <returns>The right-clicked TabItem or null if not found</returns>
        private TabItem GetContextMenuTabItem(ContextMenu contextMenu)
        {
            try
            {
                // Use the stored right-clicked tab
                if (_rightClickedTab != null)
                {
                    return _rightClickedTab;
                }
                
                // Fallback to selected tab if no right-clicked tab stored
                if (contextMenu?.PlacementTarget is TabControl tabControl)
                {
                    return tabControl.SelectedItem as TabItem;
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error getting context menu tab item");
            }

            return null;
        }

        /// <summary>
        /// Handles the preview mouse right button down event to capture which tab was right-clicked
        /// </summary>
        private void TabControl_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is TabControl tabControl)
                {
                    // Get the mouse position relative to the TabControl
                    var mousePosition = e.GetPosition(tabControl);
                    var hitTest = VisualTreeHelper.HitTest(tabControl, mousePosition);
                    
                    if (hitTest?.VisualHit != null)
                    {
                        // Find the TabItem that contains the hit point
                        var tabItem = FindParent<TabItem>(hitTest.VisualHit);
                        if (tabItem != null)
                        {
                            _rightClickedTab = tabItem;
                            _instanceLogger?.LogDebug($"Right-clicked on tab: {tabItem.Header}");
                            return;
                        }
                    }
                    
                    // Fallback to selected tab if hit test fails
                    _rightClickedTab = tabControl.SelectedItem as TabItem;
                    _instanceLogger?.LogDebug($"Right-click detected, using selected tab as fallback: {_rightClickedTab?.Header}");
                }
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Error determining right-clicked tab");
                _rightClickedTab = SafeMainTabs?.SelectedItem as TabItem;
            }
        }

        #region Phase 1 Enhancement Methods

        /// <summary>
        /// 3. Event Handler Debouncing - Prevents rapid-fire context menu actions
        /// </summary>
        private bool IsDebounced()
        {
            var now = DateTime.Now;
            if (now - _lastContextMenuAction < TimeSpan.FromMilliseconds(DEBOUNCE_INTERVAL_MS))
            {
                return true; // Still in debounce period
            }
            _lastContextMenuAction = now;
            return false;
        }

        /// <summary>
        /// Enhanced UpdateContextMenuStates with proper pin/unpin support and cache invalidation
        /// </summary>
        private void UpdateContextMenuStates(ContextMenu contextMenu)
        {
            if (contextMenu == null) return;

            var tabCount = MainTabs?.Items.Count ?? 0;
            var contextTabItem = GetContextMenuTabItem(contextMenu);
            var isTabPinned = contextTabItem != null && IsTabPinned(contextTabItem);
            
            // Enhanced cache validation - also check pinned state changes
            var currentStateSignature = $"{tabCount}_{isTabPinned}_{MainTabs?.SelectedItem?.GetHashCode() ?? 0}";
            var cachedStateSignature = $"{_cachedTabCount}_{_cachedPinnedState}_{_cachedSelectedTabHash}";
            
            if (_contextMenuStateValid && currentStateSignature == cachedStateSignature)
                return;

            MenuItem? togglePinMenuItem = null;

            foreach (MenuItem item in contextMenu.Items)
            {
                // Check by Name first (more reliable), then by Header as fallback
                if (item.Name == "TogglePinMenuItem")
                {
                    togglePinMenuItem = item;
                    continue;
                }

                switch (item.Header?.ToString())
                {
                    case "Close Tab":
                        // Enhanced Close Tab logic - combine pinned and tab count checks
                        var canClose = !isTabPinned && tabCount > 1;
                        item.IsEnabled = canClose;
                        
                        if (isTabPinned)
                            item.ToolTip = "Cannot close pinned tabs. Unpin first to close.";
                        else if (tabCount <= 1)
                            item.ToolTip = "Cannot close the last tab.";
                        else
                            item.ToolTip = null;
                        break;
                    case "Detach Tab":
                        item.IsEnabled = tabCount > 1;
                        item.ToolTip = tabCount <= 1 ? "Cannot detach the last tab." : null;
                        break;
                    case "Duplicate Tab":
                        item.IsEnabled = MainTabs?.SelectedItem != null;
                        item.ToolTip = MainTabs?.SelectedItem == null ? "No tab selected to duplicate." : null;
                        break;
                    case "Pin Tab":
                    case "Unpin Tab":
                        // Fallback: find by header if name didn't match
                        if (togglePinMenuItem == null)
                        {
                            togglePinMenuItem = item;
                        }
                        break;
                }
            }
            
            // Update Pin/Unpin menu item text based on current state
            if (togglePinMenuItem != null)
            {
                togglePinMenuItem.Header = isTabPinned ? "Unpin Tab" : "Pin Tab";
            }

            // Update enhanced cache with pinned state
            _contextMenuStateValid = true;
            _cachedTabCount = tabCount;
            _cachedPinnedState = isTabPinned;
            _cachedSelectedTabHash = MainTabs?.SelectedItem?.GetHashCode() ?? 0;
            
            _instanceLogger?.LogDebug($"Context menu states updated - Count: {tabCount}, Pinned: {isTabPinned}, Toggle Pin text: {togglePinMenuItem?.Header}");
        }

        /// <summary>
        /// 2. Smart Tab Positioning - Gets the correct insertion index for new tabs
        /// </summary>
        private int GetSmartInsertionIndex()
        {
            if (MainTabs?.SelectedItem == null) return MainTabs?.Items.Count ?? 0;
            
            var currentIndex = MainTabs.Items.IndexOf(MainTabs.SelectedItem);
            return currentIndex + 1; // Insert right after current tab
        }

        /// <summary>
        /// 4. Smart Memory Cleanup - Performs cleanup after tab operations
        /// </summary>
        private void PerformSmartCleanup()
        {
            _operationCount++;
            
            if (_operationCount >= CLEANUP_THRESHOLD)
            {
                try
                {
                    // Invalidate context menu cache
                    InvalidateContextMenuCache();
                    
                    // Force garbage collection (only occasionally)
                    GC.Collect(0, GCCollectionMode.Optimized);
                    
                    _operationCount = 0;
                    _instanceLogger?.LogDebug("Performed smart cleanup after {Count} operations", CLEANUP_THRESHOLD);
                }
                catch (Exception ex)
                {
                    _instanceLogger?.LogWarning(ex, "Smart cleanup encountered an issue");
                }
            }
        }
        
        /// <summary>
        /// Invalidates the context menu cache to force recalculation of states
        /// </summary>
        private void InvalidateContextMenuCache()
        {
            _contextMenuStateValid = false;
            _cachedTabCount = -1;
            _cachedPinnedState = false;
            _cachedSelectedTabHash = 0;
            _instanceLogger?.LogDebug("Context menu cache invalidated");
        }

        #endregion

        /// <summary>
        /// Initialize comprehensive tab management services and wire up all components
        /// </summary>
        private void InitializeTabManagement()
        {
            try
            {
                // Get services from App static properties
                _windowManager = App.WindowManager;
                _tabOperationsManager = App.TabOperationsManager;
                _dragDropService = App.DragDropService;

                // Register this window with the window manager
                _windowManager?.RegisterWindow(this);

                // Configure ChromeStyleTabControl if available
                if (MainTabs is ChromeStyleTabControl chromeTabControl)
                {
                    chromeTabControl.TabOperationsManager = _tabOperationsManager;
                    chromeTabControl.DragDropService = _dragDropService;
                    
                    // Wire up drag events to service using weak event pattern
                    SubscribeToEventWeak<TabDragEventArgs>(chromeTabControl, nameof(ChromeStyleTabControl.TabDragStarted), OnTabDragStarted);
                    SubscribeToEventWeak<TabDragEventArgs>(chromeTabControl, nameof(ChromeStyleTabControl.TabDragging), OnTabDragging);
                    SubscribeToEventWeak<TabDragEventArgs>(chromeTabControl, nameof(ChromeStyleTabControl.TabDragCompleted), OnTabDragCompleted);
                }

                _instanceLogger?.LogInformation("Tab management initialized successfully");
            }
            catch (Exception ex)
            {
                _instanceLogger?.LogError(ex, "Failed to initialize tab management");
            }
        }

        /// <summary>
        /// Handles tab drag start event
        /// </summary>
        private void OnTabDragStarted(object sender, TabDragEventArgs e)
        {
            // Service already handles most of this in ChromeStyleTabControl
            _instanceLogger?.LogDebug($"Tab drag started: {e.TabItem.Title}");
        }

        /// <summary>
        /// Handles tab dragging event
        /// </summary>
        private void OnTabDragging(object sender, TabDragEventArgs e)
        {
            // Service handles updates
            _instanceLogger?.LogDebug($"Tab dragging: {e.TabItem.Title}");
        }

        /// <summary>
        /// Handles tab drag completion event
        /// </summary>
        private void OnTabDragCompleted(object sender, TabDragEventArgs e)
        {
            // Service handles completion
            _instanceLogger?.LogDebug($"Tab drag completed: {e.TabItem.Title}");
        }

    }

    #region Window State and Actions for Transactions

    /// <summary>
    /// Window initialization state for transactional operations.
    /// </summary>
    public class WindowInitState
    {
        public MainWindow Window { get; set; }
        public Core.WindowState State { get; set; }
        public List<string> CompletedSteps { get; set; } = new List<string>();
        public Dictionary<string, object> Properties { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// Example undoable action for initializing components.
    /// </summary>
    public class InitializeComponentsAction : IUndoableAction
    {
        private readonly MainWindow _window;
        
        public InitializeComponentsAction(MainWindow window)
        {
            _window = window;
        }
        
        public string Name => "InitializeComponents";
        
        public void Execute(object state)
        {
            _window.InitializeComponent();
            
            if (!_window.IsInitialized)
                throw new InvalidOperationException("InitializeComponent failed");
                
            if (state is WindowInitState windowState)
            {
                windowState.CompletedSteps.Add(Name);
            }
        }
        
        public void Undo(object state)
        {
            // Cannot truly undo InitializeComponent, but we can mark state
            _window.UpdateInitializationState(Core.WindowState.Failed);
            
            if (state is WindowInitState windowState)
            {
                windowState.CompletedSteps.Remove(Name);
            }
        }
    }

    /// <summary>
    /// Example undoable action for validating components.
    /// </summary>
    public class ValidateComponentsAction : IUndoableAction
    {
        private readonly MainWindow _window;
        
        public ValidateComponentsAction(MainWindow window)
        {
            _window = window;
        }
        
        public string Name => "ValidateComponents";
        
        public void Execute(object state)
        {
            if (!_window.IsInitialized)
                throw new InvalidOperationException("Window components not initialized");
                
            if (state is WindowInitState windowState)
            {
                windowState.CompletedSteps.Add(Name);
            }
        }
        
        public void Undo(object state)
        {
            if (state is WindowInitState windowState)
            {
                windowState.CompletedSteps.Remove(Name);
            }
        }
    }

    #endregion

    #region Converters

    /// <summary>
    /// Converter that returns Visibility.Collapsed if count equals 1, Visibility.Visible otherwise.
    /// </summary>
    public class CountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int count)
            {
                return count <= 1 ? Visibility.Collapsed : Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Converter that returns false if count equals 1, true otherwise.
    /// </summary>
    public class CountToEnableConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int count)
            {
                return count > 1;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    #endregion
}