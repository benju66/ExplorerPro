using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.UI.Dialogs;
using ExplorerPro.UI.FileTree;
using ExplorerPro.UI.TabManagement;
using ExplorerPro.UI.Panels;
using ExplorerPro.UI.Panels.PinnedPanel;
using ExplorerPro.Utilities;
using ExplorerPro.Themes;
using ExplorerPro.Core;
// Add reference to System.Windows.Forms but use an alias
using WinForms = System.Windows.Forms;
using WPF = System.Windows;

namespace ExplorerPro.UI.MainWindow
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// Main application window that contains the toolbar and tab widget.
    /// Manages the overall application layout and windows.
    /// </summary>
    public partial class MainWindow : Window
    {
        #region Fields

        // Static list for tracking all main windows
        private static List<MainWindow> _allMainWindows = new List<MainWindow>();

        // Core managers
        private SettingsManager _settingsManager;
        private MetadataManager _metadataManager;

        // Navigation history
        private List<string> _history = new List<string>();
        private int _currentHistoryIndex = -1;

        // Detached windows tracking
        private List<MainWindow> _detachedWindows = new List<MainWindow>();

        // Robust initialization system fields
        private readonly ILogger<MainWindow> _logger;
        private readonly MainWindowInitializer _initializer;
        private readonly object _stateLock = new object();
        
        private InitializationState _initState = InitializationState.Created;
        private TabControl _mainTabsControl;
        private bool _isDisposed;

        // Value converter for tab count
        // private CountToVisibilityConverter _countToVisibilityConverter;
        // private CountToEnableConverter _countToEnableConverter;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of MainWindow with comprehensive error handling.
        /// </summary>
        public MainWindow(ILogger<MainWindow> logger, MainWindowInitializer initializer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _initializer = initializer ?? throw new ArgumentNullException(nameof(initializer));

            try
            {
                _logger.LogInformation("Creating new MainWindow instance");
                
                // Initialize core fields first
                InitializeCoreFields();
                
                // Perform initialization through the initializer
                var result = _initializer.InitializeWindow(this);
                
                if (!result.Success)
                {
                    // Initialization failed - throw to prevent invalid window creation
                    throw new WindowInitializationException(
                        result.ErrorMessage, 
                        result.State, 
                        result.Error);
                }
                
                // Window is ready for use
                _logger.LogInformation("MainWindow created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogCritical(ex, "Failed to create MainWindow");
                
                // Ensure window is properly disposed on construction failure
                try { Dispose(); } catch { }
                
                throw;
            }
        }

        /// <summary>
        /// Default constructor that creates MainWindow with default logger and initializer.
        /// This maintains backward compatibility for existing code.
        /// </summary>
        public MainWindow() 
            : this(CreateDefaultLogger(), CreateDefaultInitializer())
        {
        }

        /// <summary>
        /// Creates a default logger for backward compatibility.
        /// </summary>
        private static ILogger<MainWindow> CreateDefaultLogger()
        {
            // Create a simple console logger for backward compatibility
            // Don't use 'using' here as it would dispose the factory before the logger is used
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            return loggerFactory.CreateLogger<MainWindow>();
        }

        /// <summary>
        /// Creates a default initializer for backward compatibility.
        /// </summary>
        private static MainWindowInitializer CreateDefaultInitializer()
        {
            // Create a simple console logger for backward compatibility
            // Don't use 'using' here as it would dispose the factory before the logger is used
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Information));
            var logger = loggerFactory.CreateLogger<MainWindowInitializer>();
            return new MainWindowInitializer(logger);
        }

        /// <summary>
        /// Initializes core fields that need to be available before InitializeComponent.
        /// </summary>
        private void InitializeCoreFields()
        {
            _settingsManager = App.Settings ?? new SettingsManager();
            _metadataManager = App.MetadataManager ?? new MetadataManager();
            _allMainWindows = _allMainWindows ?? new List<MainWindow>();
            _detachedWindows = new List<MainWindow>();
            _history = new List<string>();
            _currentHistoryIndex = -1;
            
            // Register in global tracking
            _allMainWindows.Add(this);
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
        /// Setup handlers for theme changes
        /// </summary>
        private void SetupThemeHandlers()
        {
            // Subscribe to theme change events
            ThemeManager.Instance.ThemeChanged += (s, theme) => {
                Dispatcher.Invoke(() => {
                    RefreshThemeElements();
                });
            };
        }

        #endregion

        #region Robust Initialization Support Methods

        /// <summary>
        /// Updates the initialization state in a thread-safe manner.
        /// </summary>
        internal void UpdateInitializationState(InitializationState newState)
        {
            lock (_stateLock)
            {
                var oldState = _initState;
                _initState = newState;
                _logger.LogDebug($"Initialization state changed: {oldState} -> {newState}");
            }
        }

        /// <summary>
        /// Sets the MainTabs control reference after validation.
        /// </summary>
        internal void SetMainTabsControl(TabControl tabControl)
        {
            System.Diagnostics.Debug.Assert(tabControl != null, "TabControl cannot be null");
            _mainTabsControl = tabControl;
        }

        /// <summary>
        /// Safely accesses the MainTabs control with null checking.
        /// </summary>
        protected TabControl MainTabsSafe
        {
            get
            {
                if (_mainTabsControl == null)
                {
                    throw new InvalidOperationException(
                        "MainTabs control is not initialized. Current state: " + _initState);
                }
                return _mainTabsControl;
            }
        }



        /// <summary>
        /// Checks if the window is ready for tab operations.
        /// </summary>
        internal bool IsReadyForTabOperations()
        {
            lock (_stateLock)
            {
                // Allow tab operations during window initialization and when ready
                return (_initState == InitializationState.Ready || _initState == InitializationState.InitializingWindow) && 
                       _mainTabsControl != null && 
                       !_isDisposed;
            }
        }

        /// <summary>
        /// Tests if tab operations can be performed.
        /// </summary>
        internal bool CanPerformTabOperations()
        {
            try
            {
                lock (_stateLock)
                {
                    // Check basic readiness
                    if (!IsReadyForTabOperations()) return false;
                    
                    // During initialization, use direct MainTabs access
                    if (_initState == InitializationState.InitializingWindow)
                    {
                        return _mainTabsControl != null && _mainTabsControl.IsLoaded;
                    }
                    
                    // When ready, use safe accessor
                    return MainTabsSafe.IsLoaded;
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Validates the current window state.
        /// </summary>
        internal bool ValidateWindowState()
        {
            try
            {
                lock (_stateLock)
                {
                    if (_isDisposed) return false;
                    
                    // During initialization, allow validation in InitializingWindow state as well
                    if (_initState != InitializationState.Ready && 
                        _initState != InitializationState.InitializingWindow) 
                    {
                        return false;
                    }
                    
                    if (_mainTabsControl == null) return false;
                    if (!IsInitialized) return false;
                    
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Window state validation failed");
                return false;
            }
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
                this.Loaded += MainWindow_Loaded;
                
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
            try
            {
                // Create initial tab if needed
                if (_mainTabsControl != null && _mainTabsControl.Items.Count == 0)
                {
                    AddNewMainWindowTabSafely();
                }

                // Connect all pinned panels
                ConnectAllPinnedPanels();

                // Refresh pinned panels
                RefreshAllPinnedPanels();
                
                // Refresh theme elements
                RefreshThemeElements();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Loaded event");
                
                // Make sure we have at least one tab
                if (_mainTabsControl != null && _mainTabsControl.Items.Count == 0)
                {
                    SafeAddNewTab();
                }
            }
        }

        /// <summary>
        /// Safely adds a new tab with comprehensive error handling.
        /// </summary>
        internal void AddNewMainWindowTabSafely()
        {
            // Only enforce operation state if we're in ready state
            if (_initState == InitializationState.Ready)
            {
                EnsureOperationAllowed("AddNewMainWindowTab");
            }
            
            try
            {
                _logger.LogDebug("Adding new main window tab");
                
                // Use direct access to MainTabs during initialization, safe access when ready
                TabControl tabControl;
                if (_initState == InitializationState.Ready)
                {
                    tabControl = MainTabsSafe;
                }
                else
                {
                    tabControl = MainTabs; // Use direct XAML access during initialization
                }
                
                // Try to create new tab using existing method
                var container = AddNewMainWindowTab();
                if (container == null)
                {
                    // Fallback to safe method
                    SafeAddNewTab();
                }
                
                _logger.LogInformation("New tab added successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to add new tab");
                // Try fallback method
                try
                {
                    SafeAddNewTab();
                }
                catch (Exception fallbackEx)
                {
                    _logger.LogError(fallbackEx, "Fallback tab creation also failed");
                    throw;
                }
            }
        }

        /// <summary>
        /// Ensures an operation is allowed in the current state.
        /// </summary>
        private void EnsureOperationAllowed(string operationName)
        {
            lock (_stateLock)
            {
                if (_isDisposed)
                {
                    throw new ObjectDisposedException(nameof(MainWindow), 
                        $"Cannot perform {operationName} on disposed window");
                }
                
                if (_initState != InitializationState.Ready)
                {
                    throw new InvalidOperationException(
                        $"Cannot perform {operationName} in state {_initState}");
                }
            }
        }

        /// <summary>
        /// Performs emergency cleanup when initialization fails.
        /// </summary>
        internal void PerformEmergencyCleanup()
        {
            try
            {
                lock (_stateLock)
                {
                    _initState = InitializationState.Failed;
                    _mainTabsControl = null;
                    
                    // Unsubscribe from events
                    try
                    {
                        Loaded -= MainWindow_Loaded;
                        Closing -= MainWindow_Closing;
                        DragOver -= MainWindow_DragOver;
                        Drop -= MainWindow_Drop;
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Emergency cleanup encountered an error");
            }
        }

        /// <summary>
        /// Properly disposes of the window and its resources.
        /// </summary>
        protected virtual void Dispose()
        {
            lock (_stateLock)
            {
                if (_isDisposed) return;
                
                _initState = InitializationState.Disposing;
                _isDisposed = true;
                
                try
                {
                    // Cleanup logic here
                    _mainTabsControl = null;
                    
                    // Remove from global tracking
                    _allMainWindows.Remove(this);
                }
                finally
                {
                    _initState = InitializationState.Disposed;
                }
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
            
            if (Toolbar == null)
            {
                Console.WriteLine("ERROR: Toolbar is null - this is a critical UI element");
            }
            
            if (StatusText == null)
            {
                Console.WriteLine("ERROR: StatusText is null - this is a UI element");
            }
            
            // Log success if everything is available
            if (MainTabs != null && Toolbar != null && StatusText != null)
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
                
                // Restore window layout
                RestoreWindowLayout();

                // Delayed tab creation until the window is fully loaded
                this.Loaded += (sender, e) =>
                {
                    try
                    {
                        // Create initial tab if needed
                        if (MainTabs.Items.Count == 0)
                        {
                            var container = AddNewMainWindowTab();
                            if (container == null)
                            {
                                SafeAddNewTab();
                            }
                        }

                        // Connect all pinned panels
                        ConnectAllPinnedPanels();

                        // Refresh pinned panels
                        RefreshAllPinnedPanels();
                        
                        // Refresh theme elements
                        RefreshThemeElements();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error in Loaded event: {ex.Message}");
                        
                        // Make sure we have at least one tab
                        if (MainTabs.Items.Count == 0)
                        {
                            SafeAddNewTab();
                        }
                    }
                };

                // Set up keyboard shortcuts
                InitializeKeyboardShortcuts();
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error initializing main window: {ex.Message}", 
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Restore window layout from saved settings.
        /// </summary>
        private void RestoreWindowLayout()
        {
            try
            {
                var (geometryBytes, stateBytes) = _settingsManager.RetrieveMainWindowLayout();
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
                    WindowState = (WindowState)stateValue;

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
        /// </summary>
        private void InitializeKeyboardShortcuts()
        {
            // Navigation shortcuts
            RegisterShortcut(Key.Up, ModifierKeys.Alt, GoUp, "Go Up");
            RegisterShortcut(Key.F5, ModifierKeys.None, RefreshFileTree, "Refresh");
            RegisterShortcut(Key.D, ModifierKeys.Alt, FocusAddressBar, "Focus Address Bar");
            RegisterShortcut(Key.L, ModifierKeys.Control, FocusAddressBar, "Focus Address Bar (Alt)");
            RegisterShortcut(Key.F, ModifierKeys.Control, FocusSearch, "Focus Search");
            RegisterShortcut(Key.F3, ModifierKeys.None, FocusSearch, "Focus Search (Alt)");
            RegisterShortcut(Key.Left, ModifierKeys.Alt, GoBack, "Go Back");
            RegisterShortcut(Key.Right, ModifierKeys.Alt, GoForward, "Go Forward");

            // File operation shortcuts
            RegisterShortcut(Key.N, ModifierKeys.Control | ModifierKeys.Shift, NewFolder, "New Folder");
            RegisterShortcut(Key.N, ModifierKeys.Control | ModifierKeys.Alt, NewFile, "New File");
            
            // Panel toggle shortcuts
            RegisterShortcut(Key.P, ModifierKeys.Control, TogglePinnedPanel, "Toggle Pinned Panel");
            RegisterShortcut(Key.B, ModifierKeys.Control, ToggleBookmarksPanel, "Toggle Bookmarks Panel");
            RegisterShortcut(Key.D, ModifierKeys.Control, ToggleTodoPanel, "Toggle ToDo Panel");
            RegisterShortcut(Key.K, ModifierKeys.Control, ToggleProcorePanel, "Toggle Procore Panel");
            
            // Sidebar toggles (VS Code style) - Using Alt+Shift to avoid conflicts
            RegisterShortcut(Key.B, ModifierKeys.Alt | ModifierKeys.Shift, ToggleLeftSidebar, "Toggle Left Sidebar");
            RegisterShortcut(Key.R, ModifierKeys.Alt | ModifierKeys.Shift, ToggleRightSidebar, "Toggle Right Sidebar");

            // Tab shortcuts
            RegisterShortcut(Key.Tab, ModifierKeys.Control, NextTab, "Next Tab");
            RegisterShortcut(Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, PreviousTab, "Previous Tab");
            RegisterShortcut(Key.T, ModifierKeys.Control, NewTab, "New Tab");
            RegisterShortcut(Key.W, ModifierKeys.Control, CloseCurrentTab, "Close Tab");
            RegisterShortcut(Key.OemBackslash, ModifierKeys.Control, () => ToggleSplitView(null), "Toggle Split View");
            // View shortcuts
            RegisterShortcut(Key.F10, ModifierKeys.None, ToggleFullscreen, "Toggle Fullscreen");
            RegisterShortcut(Key.OemPlus, ModifierKeys.Control, ZoomIn, "Zoom In");
            RegisterShortcut(Key.OemMinus, ModifierKeys.Control, ZoomOut, "Zoom Out");
            RegisterShortcut(Key.D0, ModifierKeys.Control, ZoomReset, "Reset Zoom");

            // Theme shortcut
            RegisterShortcut(Key.T, ModifierKeys.Control | ModifierKeys.Shift, () => ThemeManager.Instance.ToggleTheme(), "Toggle Theme");

            // Utility shortcuts
            RegisterShortcut(Key.F1, ModifierKeys.None, ShowHelp, "Help");
            RegisterShortcut(Key.OemComma, ModifierKeys.Control, OpenSettings, "Settings");
            RegisterShortcut(Key.H, ModifierKeys.Control, ToggleHiddenFiles, "Toggle Hidden Files");
            RegisterShortcut(Key.Escape, ModifierKeys.None, EscapeAction, "Escape Current Operation");

            Console.WriteLine("Keyboard shortcuts initialized");
        }

        /// <summary>
        /// Register a keyboard shortcut.
        /// </summary>
        /// <param name="key">Key to register</param>
        /// <param name="modifiers">Modifier keys</param>
        /// <param name="action">Action to execute</param>
        /// <param name="description">Description for the shortcut</param>
        private void RegisterShortcut(Key key, ModifierKeys modifiers, Action? action, string description)
        {
            try
            {
                KeyGesture gesture = new KeyGesture(key, modifiers);
                RoutedCommand command = new RoutedCommand(description, GetType());
                command.InputGestures.Add(gesture);

                CommandBindings.Add(new CommandBinding(
                    command,
                    (sender, e) =>
                    {
                        action?.Invoke();
                        e.Handled = true;
                    }
                ));

                Console.WriteLine($"Added shortcut: {gesture} - {description}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error registering shortcut {description}: {ex.Message}");
            }
        }

        #endregion

        #region Theme Management

        /// <summary>
        /// Refreshes UI elements based on the current theme
        /// </summary>
        public void RefreshThemeElements()
        {
            try
            {
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
                
                // Refresh toolbar
                if (Toolbar != null)
                {
                    Toolbar.RefreshThemeElements();
                }
                
                // Update theme-sensitive button content
                UpdateThemeToggleButtonContent();
                
                Console.WriteLine("MainWindow theme elements refreshed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error refreshing main window theme elements: {ex.Message}");
                // Non-critical error, continue
            }
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
                
                // Save the theme to settings
                _settingsManager.UpdateSetting("theme", theme.ToLower());
                _settingsManager.UpdateSetting("ui_preferences.Enable Dark Mode", theme.ToLower() == "dark");
                
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
                
                // Initialize container
                try
                {
                    container.InitializeWithFileTree(validPath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing file tree: {ex.Message}");
                    // Try one more time with User folder as a last resort
                    try
                    {
                        container.InitializeWithFileTree(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
                    }
                    catch (Exception innerEx)
                    {
                        Console.WriteLine($"Critical error initializing with fallback path: {innerEx.Message}");
                        MessageBox.Show("Unable to initialize file browser with any valid path.",
                            "Critical Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return null;
                    }
                }

                // Create tab item with proper null checks
                string tabTitle = $"Window {MainTabs.Items.Count + 1}";
                TabItem newTab = new TabItem
                {
                    Header = tabTitle,
                    Content = container
                };

                // Add to tabs
                MainTabs.Items.Add(newTab);
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
                        Console.WriteLine($"Error connecting pinned panel: {ex.Message}");
                    }
                }

                // Notify of new tab
                try
                {
                    OnNewTabAdded(container);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error in OnNewTabAdded: {ex.Message}");
                }

                Console.WriteLine($"Added new main window tab with root path: {validPath}");
                return container;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unhandled error in AddNewMainWindowTab: {ex.Message}");
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
                    Console.WriteLine("ERROR: Cannot add tab because MainTabs is null");
                    MessageBox.Show("Cannot create a new tab - critical UI component not available.", 
                        "Tab Creation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Create an empty tab as a fallback
                TabItem emptyTab = new TabItem
                {
                    Header = $"Tab {MainTabs.Items.Count + 1}"
                };
                
                // Add it to the tab control
                MainTabs.Items.Add(emptyTab);
                MainTabs.SelectedItem = emptyTab;
                
                // Then try to initialize it with content
                try
                {
                    // Create a grid panel as minimal content
                    Grid contentGrid = new Grid();
                    
                    // Add an informational text block
                    TextBlock infoText = new TextBlock
                    {
                        Text = "Loading folder view...",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    
                    contentGrid.Children.Add(infoText);
                    emptyTab.Content = contentGrid;
                    
                    // Try to create container in background
                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            // Use Dispatcher to update UI from background thread
                            Dispatcher.Invoke(() =>
                            {
                                // Try to create container
                                var container = new MainWindowContainer(this);
                                
                                // Try to initialize with a safe path
                                string safePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                                container.InitializeWithFileTree(safePath);
                                
                                // Set the container as tab content
                                emptyTab.Content = container;
                                
                                // Connect signals if needed
                                if (container.PinnedPanel != null)
                                {
                                    ConnectPinnedPanel(container);
                                }
                                
                                // Update title with path name
                                string folderName = Path.GetFileName(safePath);
                                emptyTab.Header = !string.IsNullOrEmpty(folderName) ? folderName : "Home";
                                
                                Console.WriteLine("Successfully created tab with fallback approach");
                            });
                        }
                        catch (Exception ex)
                        {
                            // Use Dispatcher to update UI from background thread
                            Dispatcher.Invoke(() =>
                            {
                                Console.WriteLine($"Error initializing tab content: {ex.Message}");
                                
                                // Update the content to show the error
                                if (emptyTab.Content is Grid grid)
                                {
                                    grid.Children.Clear();
                                    
                                    TextBlock errorText = new TextBlock
                                    {
                                        Text = "Error loading folder view.\nPlease try again or restart the application.",
                                        TextWrapping = TextWrapping.Wrap,
                                        Margin = new Thickness(20),
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        VerticalAlignment = VerticalAlignment.Center,
                                        Foreground = Brushes.Red
                                    };
                                    
                                    grid.Children.Add(errorText);
                                }
                            });
                        }
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error initializing tab content: {ex.Message}");
                    
                    // The empty tab is already added, so at least the UI won't break
                    // Set some minimal content
                    TextBlock errorText = new TextBlock
                    {
                        Text = "Error loading folder view.\nPlease try again or restart the application.",
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(20),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = Brushes.Red
                    };
                    
                    emptyTab.Content = errorText;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Critical error in SafeAddNewTab: {ex.Message}");
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
        /// </summary>
        private void NewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddNewMainWindowTab();
        }

        /// <summary>
        /// Handler for duplicate tab menu item click.
        /// </summary>
        private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DuplicateCurrentTab();
        }

        /// <summary>
        /// Handler for close tab menu item click.
        /// </summary>
        private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CloseCurrentTab();
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
        /// </summary>
        private void DetachTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DetachMainTab(MainTabs.SelectedIndex);
        }

        /// <summary>
        /// Handler for move to new window menu item click.
        /// </summary>
        private void MoveToNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MoveTabToNewWindow();
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
                
                // Track detached window
                _detachedWindows.Add(newWindow);
                
                Console.WriteLine($"Detached tab '{tabTitle}' to new window");
            }
            catch (Exception ex)
            {
                WPF.MessageBox.Show($"Error detaching tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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

        /// <summary>
        /// Move the current tab to a new window.
        /// </summary>
        public void MoveTabToNewWindow()
        {
            DetachMainTab(MainTabs.SelectedIndex);
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
            foreach (var window in _allMainWindows)
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
                foreach (var window in _allMainWindows)
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
        /// Toggle pinned panel visibility in current container.
        /// </summary>
        private void TogglePinnedPanel()
        {
            TogglePanelInCurrentContainer("TogglePinnedPanel");
        }

        /// <summary>
        /// Event handler for pinned panel toggle button.
        /// </summary>
        private void TogglePinnedPanel_Click(object sender, RoutedEventArgs e)
        {
            TogglePinnedPanel();
        }

        /// <summary>
        /// Toggle bookmarks panel visibility in current container.
        /// </summary>
        private void ToggleBookmarksPanel()
        {
            TogglePanelInCurrentContainer("ToggleBookmarksPanel");
        }

        /// <summary>
        /// Event handler for bookmarks panel toggle button.
        /// </summary>
        private void ToggleBookmarksPanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleBookmarksPanel();
        }

        /// <summary>
        /// Toggle to-do panel visibility in current container.
        /// </summary>
        private void ToggleTodoPanel()
        {
            TogglePanelInCurrentContainer("ToggleTodoPanel");
        }

        /// <summary>
        /// Event handler for to-do panel toggle button.
        /// </summary>
        private void ToggleTodoPanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleTodoPanel();
        }

        /// <summary>
        /// Toggle Procore links panel visibility in current container.
        /// </summary>
        private void ToggleProcorePanel()
        {
            TogglePanelInCurrentContainer("ToggleProcorePanel");
        }

        /// <summary>
        /// Event handler for Procore panel toggle button.
        /// </summary>
        private void ToggleProcorePanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleProcorePanel();
        }

        /// <summary>
        /// Toggle the entire left sidebar visibility.
        /// </summary>
        private void ToggleLeftSidebar()
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
        private void ToggleRightSidebar()
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
                // Apply theme
                string theme = _settingsManager.GetSetting<string>("theme", "light");
                ApplyTheme(theme);

                // Apply panel visibility
                Dictionary<string, bool> panels = _settingsManager.GetSetting<Dictionary<string, bool>>(
                    "dockable_panels", new Dictionary<string, bool>());
                
                var container = GetCurrentContainer();
                if (container != null)
                {
                    container.ApplySavedPanelVisibility(panels);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error applying saved settings: {ex.Message}");
            }
        }

        #endregion

        #region Navigation and File Operations

        /// <summary>
        /// Update the address bar with path.
        /// </summary>
        /// <param name="path">Path to display</param>
        public void UpdateToolbarAddressBar(string path) // Changed method name
        {
            if (string.IsNullOrEmpty(path)) return;
            
            if (!Directory.Exists(path))
            {
                Console.WriteLine($"Warning: Invalid path provided: {path}");
                return;
            }

            // Update toolbar address bar
            Toolbar.SetAddressText(path); // Changed method name
            
            // Update status bar
            UpdateStatus($"Current path: {path}");
        }

        /// <summary>
        /// Update address bar for API compatibility with MainWindowContainer
        /// </summary>
        /// <param name="path">Path to display</param>
        public void UpdateAddressBar(string path)
        {
            UpdateToolbarAddressBar(path);
        }

        /// <summary>
        /// Update status bar text.
        /// </summary>
        /// <param name="text">Status text</param>
        private void UpdateStatus(string text)
        {
            StatusText.Text = text;
        }

        /// <summary>
        /// Update item count display.
        /// </summary>
        /// <param name="count">Number of items</param>
        public void UpdateItemCount(int count)
        {
            ItemCountText.Text = count == 1 ? "1 item" : $"{count} items";
        }

        /// <summary>
        /// Update selection info.
        /// </summary>
        /// <param name="count">Number of selected items</param>
        /// <param name="size">Total size of selection</param>
        public void UpdateSelectionInfo(int count, long size = 0)
        {
            if (count == 0)
            {
                SelectionText.Text = string.Empty;
                return;
            }
            
            string sizeText = FileSizeFormatter.FormatSize(size);
            SelectionText.Text = count == 1 
                ? $"1 item selected ({sizeText})" 
                : $"{count} items selected ({sizeText})";
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
        /// </summary>
        public void GoBack()
        {
            var container = GetCurrentContainer();
            container?.GoBack();
        }

        /// <summary>
        /// Navigate forward in history.
        /// </summary>
        public void GoForward()
        {
            var container = GetCurrentContainer();
            container?.GoForward();
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
            // Save settings
            _settingsManager.SaveSettings();
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
            Toolbar?.SetAddressBarFocus(); // Changed method name
        }

        /// <summary>
        /// Focus the search box.
        /// </summary>
        private void FocusSearch()
        {
            Toolbar?.SetSearchFocus(); // Changed method name
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
            if (MainTabs.Items.Count <= 1) return;
            
            int current = MainTabs.SelectedIndex;
            MainTabs.SelectedIndex = (current + 1) % MainTabs.Items.Count;
        }

        /// <summary>
        /// Switch to the previous tab.
        /// </summary>
        private void PreviousTab()
        {
            if (MainTabs.Items.Count <= 1) return;
            
            int current = MainTabs.SelectedIndex;
            MainTabs.SelectedIndex = (current - 1 + MainTabs.Items.Count) % MainTabs.Items.Count;
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
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.CanResize;
            }
            else
            {
                // Enter fullscreen
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
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
            var settingsDialog = new SettingsDialog(_settingsManager);
            
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
        /// Handler for drop on the main window.
        /// </summary>
        private void MainWindow_Drop(object sender, System.Windows.DragEventArgs e)
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
                
                e.Handled = true;
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
                
                // Save settings
                _settingsManager.SaveSettings();
                
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
            byte[] geometryBytes = GetWindowGeometryBytes();
            byte[] stateBytes = GetWindowStateBytes();
            
            _settingsManager.StoreMainWindowLayout(geometryBytes, stateBytes);
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

        #endregion
    }

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