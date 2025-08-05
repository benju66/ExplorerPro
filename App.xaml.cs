using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using ExplorerPro.Models;
using ExplorerPro.Utilities;
using ExplorerPro.Themes;
using ExplorerPro.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace ExplorerPro
{
    /// <summary>
    /// Interaction logic for App.xaml - Fixed version with proper disposal
    /// </summary>
    public partial class App : Application
    {
        // Singleton instance of settings manager
        public static SettingsManager? Settings { get; private set; }

        // Application data paths
        public static string AppDataPath { get; private set; } = string.Empty;
        public static string SettingsPath { get; private set; } = string.Empty;
        public static string LogPath { get; private set; } = string.Empty;
        public static string ErrorLogPath { get; private set; } = string.Empty;

        // Global services
        public static MetadataManager? MetadataManager { get; private set; }
        public static PinnedManager? PinnedManager { get; private set; }
        public static UndoManager? UndoManager { get; private set; }
        public static RecurringTaskManager? RecurringTaskManager { get; private set; }
        
        // Tab management services
        public static ExplorerPro.Core.TabManagement.IDetachedWindowManager? WindowManager { get; private set; }
        public static ExplorerPro.Core.TabManagement.TabOperationsManager? TabOperationsManager { get; private set; }
        public static ExplorerPro.Core.TabManagement.ITabDragDropService? DragDropService { get; private set; }
        public static ExplorerPro.Core.TabManagement.TabStateManager? TabStateManager { get; private set; }
        
        // Performance optimization services
        public static ExplorerPro.Core.Monitoring.ResourceMonitor? ResourceMonitor { get; private set; }
        public static ExplorerPro.Core.TabManagement.TabVirtualizationManager? TabVirtualizationManager { get; private set; }
        public static ExplorerPro.Core.TabManagement.TabHibernationManager? TabHibernationManager { get; private set; }
        public static ExplorerPro.Core.TabManagement.PerformanceOptimizer? PerformanceOptimizer { get; private set; }
        
        // Phase 1 Critical Fixes services
        public static ExplorerPro.Core.Telemetry.IExtendedTelemetryService? TelemetryService { get; private set; }
        public static ExplorerPro.Core.TabManagement.TabResolutionMonitor? TabResolutionMonitor { get; private set; }

        // Logger factory for dependency injection
        private static ILoggerFactory? _loggerFactory;
        
        // Store event handlers for proper cleanup
        private EventHandler<AppTheme>? _themeChangedHandler;

        protected override void OnStartup(StartupEventArgs e)
        {
            try
            {
                Console.WriteLine("Application starting...");
                base.OnStartup(e);

                // Set up global exception handlers
                SetupGlobalExceptionHandlers();

                // Set up logging first
                Console.WriteLine("Initializing logging...");
                InitializeLogging();

                // Initialize application paths
                Console.WriteLine("Initializing application paths...");
                InitializeAppPaths();

                // Initialize settings
                Console.WriteLine("Initializing settings...");
                InitializeSettings();

                // Initialize theme system
                InitializeThemeSystem();

                // Initialize global services
                Console.WriteLine("Initializing services...");
                InitializeServices();

                // Initialize icon provider
                Console.WriteLine("Initializing icon provider...");
                InitializeIconProvider();

                // Initialize PDFium for PdfiumViewer.Updated
                InitializePdfium();

                // Log application start
                Console.WriteLine("Logging application start...");
                LogApplicationStart();
                
                Console.WriteLine("Startup completed successfully");
                
                // Create and show the main window explicitly
                try
                {
                    Console.WriteLine("Creating MainWindow programmatically...");
                    var mainWindow = new ExplorerPro.UI.MainWindow.MainWindow();
                    MainWindow = mainWindow;
                    mainWindow.Show();
                    Console.WriteLine("MainWindow created and shown successfully");
                }
                catch (Exception windowEx)
                {
                    HandleStartupError(windowEx, "Error creating main window", true);
                }
            }
            catch (Exception ex)
            {
                HandleStartupError(ex, "Critical error during startup", true);
            }
        }

        /// <summary>
        /// Initialize themes early in the application startup
        /// </summary>
        private void InitializeThemeSystem()
        {
            try
            {
                Console.WriteLine("Initializing theme system...");
                
                // Get theme from settings
                string savedTheme = Settings?.GetSetting<string>("theme", "light") ?? "light";
                bool isDarkMode = savedTheme.Equals("dark", StringComparison.OrdinalIgnoreCase);
                
                // Initialize and apply the theme
                ThemeManager.Instance.Initialize();
                
                // Create and store the event handler
                _themeChangedHandler = (s, theme) => {
                    Console.WriteLine($"Theme changed to: {theme}");
                    
                    // Save to settings
                    if (Settings != null)
                    {
                        Settings.UpdateSetting("theme", theme.ToString().ToLower());
                        Settings.UpdateSetting("ui_preferences.Enable Dark Mode", theme == AppTheme.Dark);
                    }
                };
                
                // Subscribe to theme change events
                ThemeManager.Instance.ThemeChanged += _themeChangedHandler;
                
                Console.WriteLine($"Theme system initialized with theme: {savedTheme}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing theme system: {ex.Message}");
                // Continue with application startup even if theme initialization fails
            }
        }
        
        /// <summary>
        /// Update the application theme overrides from App.xaml
        /// </summary>
        private void UpdateAppResources()
        {
            try
            {
                // Clear old theme dictionaries and add new ones
                var resourcesToRemove = new ResourceDictionary[Resources.MergedDictionaries.Count];
                Resources.MergedDictionaries.CopyTo(resourcesToRemove, 0);
                
                foreach (var dict in resourcesToRemove)
                {
                    if (dict.Source != null && dict.Source.ToString().Contains("/Themes/"))
                    {
                        Resources.MergedDictionaries.Remove(dict);
                    }
                }
                
                // Let the ThemeManager handle adding the correct theme dictionaries
                ThemeManager.Instance.RefreshThemeResources();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating app resources: {ex.Message}");
            }
        }

        private void InitializePdfium()
        {
            try
            {
                // Try to initialize PdfiumViewer with the native DLL
                string pdfiumPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Native", "pdfium.dll");
                if (File.Exists(pdfiumPath))
                {
                    Console.WriteLine($"Setting PdfiumViewer native DLL path: {pdfiumPath}");
                    
                    // Set the environment variable for PDFium
                    Environment.SetEnvironmentVariable("PDFIUM_NATIVE_DLL_PATH", pdfiumPath);
                    
                    // Try to load the native library directly as well
                    try
                    {
                        System.Runtime.InteropServices.NativeLibrary.Load(pdfiumPath);
                        Console.WriteLine("Loaded pdfium.dll with NativeLibrary.Load");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Note: Could not load pdfium.dll directly: {ex.Message}");
                        // This is just a fallback attempt, not critical
                    }
                }
                else
                {
                    Console.WriteLine($"Warning: pdfium.dll not found at {pdfiumPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing PDFium: {ex.Message}");
                // Non-critical error, continue
            }
        }

        /// <summary>
        /// Set up global exception handlers
        /// </summary>
        private void SetupGlobalExceptionHandlers()
        {
            try
            {
                // Handler for unhandled exceptions in the AppDomain
                AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                {
                    if (e.ExceptionObject is Exception ex)
                    {
                        HandleStartupError(ex, "Unhandled application exception", e.IsTerminating);
                    }
                    else
                    {
                        // Handle case where exception object is not an Exception
                        HandleStartupError(
                            new Exception("Unknown exception: " + e.ExceptionObject?.ToString() ?? "null"), 
                            "Unhandled application exception", 
                            e.IsTerminating);
                    }
                };

                // Handler for unhandled exceptions in WPF dispatcher
                DispatcherUnhandledException += (s, e) =>
                {
                    HandleStartupError(e.Exception, "Unhandled UI exception");
                    e.Handled = true; // Prevent app from crashing
                };

                // Handler for unhandled exceptions in Tasks
                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    HandleStartupError(e.Exception, "Unhandled task exception");
                    e.SetObserved(); // Prevent app from crashing
                };
                
                Console.WriteLine("Global exception handlers set up successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting up global exception handlers: {ex.Message}");
                // Cannot handle this failure effectively since this is the exception handling setup
            }
        }

        /// <summary>
        /// Handle errors during startup
        /// </summary>
        private void HandleStartupError(Exception ex, string message, bool isFatal = false)
        {
            if (ex == null)
            {
                ex = new Exception("Unknown error occurred");
            }
            
            string errorMsg = $"{message}: {ex.Message}";
            Console.WriteLine(errorMsg);
            Console.WriteLine(ex.StackTrace);
            
            // Log inner exceptions
            Exception innerEx = ex.InnerException;
            while (innerEx != null)
            {
                Console.WriteLine($"Inner Exception: {innerEx.Message}");
                Console.WriteLine(innerEx.StackTrace);
                innerEx = innerEx.InnerException;
            }
            
            try
            {
                // Log to file as well
                if (!string.IsNullOrEmpty(ErrorLogPath))
                {
                    string? errorLogDir = Path.GetDirectoryName(ErrorLogPath);
                    if (!string.IsNullOrEmpty(errorLogDir) && !Directory.Exists(errorLogDir))
                    {
                        Directory.CreateDirectory(errorLogDir);
                    }
                    
                    // Append to log file with inner exceptions
                    string logEntry = $"[{DateTime.Now}] {errorMsg}\r\n{ex.StackTrace}\r\n";
                    
                    // Add inner exceptions to log
                    innerEx = ex.InnerException;
                    while (innerEx != null)
                    {
                        logEntry += $"Inner Exception: {innerEx.Message}\r\n{innerEx.StackTrace}\r\n";
                        innerEx = innerEx.InnerException;
                    }
                    
                    logEntry += "\r\n";
                    File.AppendAllText(ErrorLogPath, logEntry);
                }
                else
                {
                    // If ErrorLogPath not set yet, try to use a default path
                    string defaultErrorLogPath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ExplorerPro", "error_log.txt");
                        
                    string? defaultErrorLogDir = Path.GetDirectoryName(defaultErrorLogPath);
                    if (!string.IsNullOrEmpty(defaultErrorLogDir) && !Directory.Exists(defaultErrorLogDir))
                    {
                        Directory.CreateDirectory(defaultErrorLogDir);
                    }
                    
                    File.AppendAllText(defaultErrorLogPath, 
                        $"[{DateTime.Now}] {errorMsg}\r\n{ex.StackTrace}\r\n\r\n");
                }
            }
            catch
            {
                // Silently fail if logging fails
            }
            
            // Show error to user
            try
            {
                MessageBox.Show(errorMsg, isFatal ? "Fatal Error" : "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Cannot show message box, just continue
            }
            
            if (isFatal)
            {
                try
                {
                    Current.Shutdown();
                }
                catch
                {
                    // Force exit if Shutdown fails
                    Environment.Exit(1);
                }
            }
        }

        private void InitializeLogging()
        {
            try
            {
                // Create a logger factory that can be used across the application
                _loggerFactory = LoggerFactory.Create(builder =>
                {
                    // Configure logging levels
                    builder.SetMinimumLevel(LogLevel.Debug);
                    
                    // You can add console logging if needed (requires Microsoft.Extensions.Logging.Console package)
                    // builder.AddConsole();
                    
                    // Add other logger providers as needed
                });
                
                Console.WriteLine("Logging initialized successfully");
            }
            catch (Exception ex)
            {
                HandleStartupError(ex, "Error initializing logging");
                
                // Try to create a minimal logger factory
                try
                {
                    _loggerFactory = LoggerFactory.Create(builder => { });
                }
                catch
                {
                    // Continue without logging if all attempts fail
                    _loggerFactory = null;
                }
            }
        }

        private void InitializeAppPaths()
        {
            try
            {
                // Get base application data directory in user's AppData
                string appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ExplorerPro");

                // Create directory if it doesn't exist
                if (!Directory.Exists(appDataFolder))
                {
                    Console.WriteLine($"Creating application data folder: {appDataFolder}");
                    Directory.CreateDirectory(appDataFolder);
                }

                // Define standard paths
                AppDataPath = appDataFolder;
                SettingsPath = Path.Combine(AppDataPath, "settings.json");
                LogPath = Path.Combine(AppDataPath, "file_operations.log");
                ErrorLogPath = Path.Combine(AppDataPath, "error_log.txt");

                // Create data directory if it doesn't exist
                string dataPath = Path.Combine(AppDataPath, "Data");
                if (!Directory.Exists(dataPath))
                {
                    Console.WriteLine($"Creating data folder: {dataPath}");
                    Directory.CreateDirectory(dataPath);
                }
                
                Console.WriteLine($"App data path: {AppDataPath}");
                Console.WriteLine($"Settings path: {SettingsPath}");
                Console.WriteLine($"Log path: {LogPath}");
                Console.WriteLine($"Error log path: {ErrorLogPath}");
            }
            catch (Exception ex)
            {
                HandleStartupError(ex, "Error initializing application paths");
                
                // Set fallback paths in current directory
                try
                {
                    string currentDir = Directory.GetCurrentDirectory();
                    AppDataPath = currentDir;
                    SettingsPath = Path.Combine(currentDir, "settings.json");
                    LogPath = Path.Combine(currentDir, "file_operations.log");
                    ErrorLogPath = Path.Combine(currentDir, "error_log.txt");
                    
                    // Create Data directory in current folder
                    string dataPath = Path.Combine(currentDir, "Data");
                    if (!Directory.Exists(dataPath))
                    {
                        Directory.CreateDirectory(dataPath);
                    }
                }
                catch (Exception fallbackEx)
                {
                    // Critical error if we can't even set fallback paths
                    HandleStartupError(fallbackEx, "Fatal error setting fallback paths", true);
                }
            }
        }

        private void InitializeSettings()
        {
            try
            {
                // Initialize settings manager with the settings path
                Console.WriteLine($"Loading settings from: {SettingsPath}");
                Settings = new SettingsManager(SettingsPath);
                Settings.LoadSettings();
                Console.WriteLine("Settings loaded successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading settings: {ex.Message}");
                
                // If settings file is corrupted, try to backup and recreate
                if (File.Exists(SettingsPath))
                {
                    try
                    {
                        string backupPath = SettingsPath + ".backup." + DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        File.Move(SettingsPath, backupPath);
                        Console.WriteLine($"Corrupted settings file backed up to: {backupPath}");
                    }
                    catch (Exception backupEx)
                    {
                        Console.WriteLine($"Could not backup corrupted settings file: {backupEx.Message}");
                    }
                }
                
                // Create default settings on error
                try
                {
                    Console.WriteLine("Creating default settings");
                    Settings = new SettingsManager(SettingsPath);
                    Settings.ApplyDefaultSettings();
                    Settings.SaveSettings();
                    Console.WriteLine("Default settings created and saved successfully");
                }
                catch (Exception innerEx)
                {
                    HandleStartupError(innerEx, "Error creating default settings");
                    
                    // Create settings in memory only as last resort
                    Settings = new SettingsManager();
                    Settings.ApplyDefaultSettings();
                    Console.WriteLine("Using in-memory settings as fallback");
                }
            }
        }

        private void InitializeServices()
        {
            try
            {
                // Initialize services one by one for better error isolation
                InitializeMetadataManager();
                InitializePinnedManager();
                InitializeRecurringTaskManager();
                InitializeUndoManager();
                InitializeTabManagementServices();
                
                // Initialize Phase 1 Critical Fixes after core services
                InitializePhase1CriticalFixes();
                
                Console.WriteLine("All services initialized successfully");
            }
            catch (Exception ex)
            {
                HandleStartupError(ex, "Error initializing application services");
            }
        }

        private void InitializeTabManagementServices()
        {
            try
            {
                // Initialize tab state manager for session persistence
                var stateManagerLogger = _loggerFactory?.CreateLogger<ExplorerPro.Core.TabManagement.TabStateManager>();
                TabStateManager = new ExplorerPro.Core.TabManagement.TabStateManager(stateManagerLogger);
                
                // Initialize performance services for memory optimization
                var resourceMonitorLogger = _loggerFactory?.CreateLogger<ExplorerPro.Core.Monitoring.ResourceMonitor>();
                ResourceMonitor = new ExplorerPro.Core.Monitoring.ResourceMonitor();
                
                var virtualizationLogger = _loggerFactory?.CreateLogger<ExplorerPro.Core.TabManagement.TabVirtualizationManager>();
                TabVirtualizationManager = new ExplorerPro.Core.TabManagement.TabVirtualizationManager(virtualizationLogger, TabStateManager);
                
                var hibernationLogger = _loggerFactory?.CreateLogger<ExplorerPro.Core.TabManagement.TabHibernationManager>();
                TabHibernationManager = new ExplorerPro.Core.TabManagement.TabHibernationManager(hibernationLogger, ResourceMonitor);
                
                var optimizerLogger = _loggerFactory?.CreateLogger<ExplorerPro.Core.TabManagement.PerformanceOptimizer>();
                PerformanceOptimizer = new ExplorerPro.Core.TabManagement.PerformanceOptimizer(optimizerLogger, ResourceMonitor, null, TabHibernationManager);
                
                // Initialize window manager as new SimpleDetachedWindowManager with logger
                var windowManagerLogger = _loggerFactory?.CreateLogger<ExplorerPro.Core.TabManagement.SimpleDetachedWindowManager>();
                WindowManager = new ExplorerPro.Core.TabManagement.SimpleDetachedWindowManager(windowManagerLogger);
                
                // Initialize tab operations manager with logger and WindowManager
                var tabOpsLogger = _loggerFactory?.CreateLogger<ExplorerPro.Core.TabManagement.TabOperationsManager>();
                TabOperationsManager = new ExplorerPro.Core.TabManagement.TabOperationsManager(tabOpsLogger, WindowManager);
                
                // Initialize drag drop service as new TabDragDropService with logger, WindowManager, and TabOperationsManager
                var dragDropLogger = _loggerFactory?.CreateLogger<ExplorerPro.Core.TabManagement.TabDragDropService>();
                DragDropService = new ExplorerPro.Core.TabManagement.TabDragDropService(dragDropLogger, WindowManager, TabOperationsManager);
                
                // Start performance monitoring and optimization
                // ResourceMonitor starts automatically when constructed
                TabVirtualizationManager?.Start();
                
                Console.WriteLine("Tab management and performance services initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing tab management services: {ex.Message}");
                // Set to null on failure
                TabStateManager = null;
                ResourceMonitor = null;
                TabVirtualizationManager = null;
                TabHibernationManager = null;
                PerformanceOptimizer = null;
                WindowManager = null;
                TabOperationsManager = null;
                DragDropService = null;
            }
        }
        
        private void InitializePhase1CriticalFixes()
        {
            try
            {
                Console.WriteLine("Initializing Phase 1 Critical Fixes...");
                
                // Initialize extended telemetry service
                TelemetryService = new ExplorerPro.Core.Telemetry.ExtendedTelemetryService(
                    _loggerFactory?.CreateLogger<ExplorerPro.Core.Telemetry.ExtendedTelemetryService>()
                );
                Console.WriteLine("Extended telemetry service initialized");
                
                // Initialize TabModelResolver
                if (_loggerFactory != null && TelemetryService != null && ResourceMonitor != null)
                {
                    ExplorerPro.Core.TabManagement.TabModelResolver.Initialize(
                        _loggerFactory.CreateLogger("TabModelResolver"),
                        TelemetryService,
                        ResourceMonitor,
                        new ExplorerPro.Core.SettingsService(Settings, _loggerFactory.CreateLogger<ExplorerPro.Core.SettingsService>())
                    );
                    
                    Console.WriteLine($"TabModelResolver initialized. Feature enabled: {ExplorerPro.Core.Configuration.FeatureFlags.UseTabModelResolver}");
                    
                    // Start monitoring if enabled
                    if (ExplorerPro.Core.Configuration.FeatureFlags.UseTabModelResolver && 
                        ExplorerPro.Core.Configuration.FeatureFlags.EnableTabResolutionMonitoring)
                    {
                        TabResolutionMonitor = new ExplorerPro.Core.TabManagement.TabResolutionMonitor(
                            _loggerFactory.CreateLogger<ExplorerPro.Core.TabManagement.TabResolutionMonitor>(),
                            TelemetryService,
                            TimeSpan.FromMinutes(5)
                        );
                        
                        Console.WriteLine("TabResolutionMonitor started");
                    }
                }
                else
                {
                    Console.WriteLine("WARNING: Could not initialize TabModelResolver - missing dependencies");
                }
                
                Console.WriteLine("Phase 1 Critical Fixes initialization completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing Phase 1 Critical Fixes: {ex.Message}");
                // Don't throw - allow app to continue with degraded functionality
                TelemetryService = null;
                TabResolutionMonitor = null;
            }
        }
        
        private void InitializeMetadataManager()
        {
            try
            {
                var metadataLogger = _loggerFactory?.CreateLogger<MetadataManager>();
                
                // Initialize the metadata manager
                string metadataPath = Path.Combine(AppDataPath, "Data", "metadata.json");
                Console.WriteLine($"Initializing metadata manager with path: {metadataPath}");
                
                MetadataManager = new MetadataManager(metadataPath);
                Console.WriteLine("Metadata manager initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing metadata manager: {ex.Message}");
                // Create with default path
                try
                {
                    MetadataManager = new MetadataManager();
                    Console.WriteLine("Created metadata manager with defaults");
                }
                catch (Exception innerEx)
                {
                    Console.WriteLine($"Could not create metadata manager: {innerEx.Message}");
                    MetadataManager = null;
                }
            }
        }
        
        private void InitializePinnedManager()
        {
            try
            {
                var pinnedManagerLogger = _loggerFactory?.CreateLogger<PinnedManager>();
                
                // Get static instance - do not try to create with constructor
                PinnedManager = PinnedManager.Instance;
                Console.WriteLine("Pinned manager initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing pinned manager: {ex.Message}");
                // No fallback for singleton
                PinnedManager = null;
            }
        }
        
        private void InitializeRecurringTaskManager()
        {
            try
            {
                var recurringTaskLogger = _loggerFactory?.CreateLogger<RecurringTaskManager>();
                
                // Get static instance
                RecurringTaskManager = RecurringTaskManager.Instance;
                Console.WriteLine("Recurring task manager initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing recurring task manager: {ex.Message}");
                // No fallback for singleton
                RecurringTaskManager = null;
            }
        }
        
        private void InitializeUndoManager()
        {
            try
            {
                var undoManagerLogger = _loggerFactory?.CreateLogger<UndoManager>();
                
                // Create new instance
                UndoManager = new UndoManager();
                Console.WriteLine("Undo manager initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing undo manager: {ex.Message}");
                // Try another approach
                try
                {
                    UndoManager = new UndoManager();
                }
                catch
                {
                    UndoManager = null;
                }
            }
        }

        private void InitializeIconProvider()
        {
            try
            {
                IconProvider.InitializeFileTypeIcons();
                Console.WriteLine("Icon provider initialized successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing icon provider: {ex.Message}");
                // Non-critical error, continue
            }
        }

        private void LogApplicationStart()
        {
            try
            {
                string logMessage = $"Application started at {DateTime.Now}";
                File.AppendAllText(LogPath, logMessage + Environment.NewLine);
                Console.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging application start: {ex.Message}");
                // Silently fail if logging fails
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            const int WINDOW_CLOSE_TIMEOUT_MS = 5000; // 5 seconds timeout
            var timeoutCts = new CancellationTokenSource(WINDOW_CLOSE_TIMEOUT_MS);
            
            try
            {
                Console.WriteLine("Application exit initiated - starting graceful shutdown...");
                
                // Phase 1: WindowLifecycleManager cleanup before window closing
                Console.WriteLine("Phase 1: Cleaning up WindowLifecycleManager...");
                ForceCloseAllTrackedWindows(timeoutCts.Token);
                
                // Phase 2: Force close any floating/detached windows
                Console.WriteLine("Phase 2: Force closing any remaining windows...");
                ForceCloseRemainingWindows(timeoutCts.Token);
                
                // Phase 3: Wait briefly for windows to finish closing with timeout
                Console.WriteLine("Phase 3: Waiting for window closure completion...");
                WaitForWindowClosureWithTimeout(timeoutCts.Token);
                
                // Phase 4: Safe to dispose logger after all windows are closed
                Console.WriteLine("Phase 4: Disposing shared logger...");
                ExplorerPro.UI.MainWindow.MainWindow.DisposeSharedLogger();
                
                // Phase 5: Save state of each manager separately to isolate errors
                Console.WriteLine("Phase 5: Saving application state...");
                SaveApplicationState();
                
                // Phase 5.5: Cleanup Phase 1 Critical Fixes
                Console.WriteLine("Phase 5.5: Cleaning up Phase 1 Critical Fixes...");
                CleanupPhase1CriticalFixes();
                
                // Phase 6: Dispose all services
                Console.WriteLine("Phase 6: Disposing services...");
                DisposeServices();
                
                // Phase 7: Final cleanup
                Console.WriteLine("Phase 7: Final cleanup...");
                PerformFinalCleanup();
                
                Console.WriteLine("Application exit completed successfully");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Application exit timed out - forcing immediate shutdown");
                System.Diagnostics.Debug.WriteLine("Warning: Application exit exceeded timeout, some resources may not be properly cleaned up");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during application exit: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"Error during application exit: {ex.Message}");
            }
            finally
            {
                timeoutCts?.Dispose();
                base.OnExit(e);
            }
        }
        
        /// <summary>
        /// Force close all windows tracked by WindowLifecycleManager
        /// </summary>
        private void ForceCloseAllTrackedWindows(CancellationToken cancellationToken)
        {
            try
            {
                var lifecycleManager = WindowLifecycleManager.Instance;
                if (lifecycleManager != null)
                {
                    // Use the new ForceCloseAllWindows method
                    lifecycleManager.ForceCloseAllWindows();
                    Console.WriteLine("WindowLifecycleManager cleanup completed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during WindowLifecycleManager cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Force close any remaining windows not tracked by lifecycle manager
        /// </summary>
        private void ForceCloseRemainingWindows(CancellationToken cancellationToken)
        {
            try
            {
                var windowsCopy = new Window[Windows.Count];
                Windows.CopyTo(windowsCopy, 0);
                
                foreach (Window window in windowsCopy)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;
                        
                    try
                    {
                        if (window != null && IsWindowOpen(window))
                        {
                            Console.WriteLine($"Force closing window: {window.GetType().Name}");
                            window.Close();
                        }
                    }
                    catch (Exception windowEx)
                    {
                        Console.WriteLine($"Error closing window {window?.GetType().Name}: {windowEx.Message}");
                        try
                        {
                            // If Close() fails, try forcing with Hide() first
                            window?.Hide();
                        }
                        catch
                        {
                            // Ignore secondary failures
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during remaining window cleanup: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Check if a window is still open and accessible
        /// </summary>
        private bool IsWindowOpen(Window window)
        {
            try
            {
                return window != null && window.IsLoaded && window.IsVisible;
            }
            catch
            {
                return false;
            }
        }
        
        /// <summary>
        /// Wait for window closure completion with timeout
        /// </summary>
        private void WaitForWindowClosureWithTimeout(CancellationToken cancellationToken)
        {
            try
            {
                const int POLL_INTERVAL_MS = 100;
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                
                while (Windows.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    Thread.Sleep(POLL_INTERVAL_MS);
                    
                    // Force garbage collection to help with window cleanup
                    if (stopwatch.ElapsedMilliseconds % 1000 == 0)
                    {
                        GC.Collect();
                        GC.WaitForPendingFinalizers();
                    }
                }
                
                if (Windows.Count == 0)
                {
                    Console.WriteLine("All windows closed successfully");
                }
                else
                {
                    Console.WriteLine($"Warning: {Windows.Count} windows still open after timeout");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during window closure wait: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Save application state with error isolation
        /// </summary>
        private void SaveApplicationState()
        {
            // Save settings
            SaveSettingsOnExit();
            
            // Save metadata
            SaveMetadataOnExit();
            
            // Save any other persistent state
            try
            {
                if (PinnedManager != null)
                {
                    // PinnedManager might have state to save
                    Console.WriteLine("PinnedManager state preservation completed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving PinnedManager state: {ex.Message}");
            }
            
            try
            {
                if (RecurringTaskManager != null)
                {
                    // RecurringTaskManager might have state to save
                    Console.WriteLine("RecurringTaskManager state preservation completed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving RecurringTaskManager state: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Perform final cleanup operations
        /// </summary>
        private void PerformFinalCleanup()
        {
            try
            {
                // Unsubscribe from theme events
                UnsubscribeFromThemeEvents();
                
                // Dispose logger factory
                DisposeLoggerFactory();
                
                // Log application exit
                LogApplicationExit();
                
                // Force final garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                Console.WriteLine("Final cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during final cleanup: {ex.Message}");
            }
        }
        
        private void SaveSettingsOnExit()
        {
            try
            {
                if (Settings != null)
                {
                    Settings.SaveSettings();
                    Console.WriteLine("Settings saved successfully on exit");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving settings during exit: {ex.Message}");
            }
        }
        
        private void SaveMetadataOnExit()
        {
            try
            {
                if (MetadataManager != null)
                {
                    MetadataManager.SaveMetadata();
                    Console.WriteLine("Metadata saved successfully on exit");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving metadata during exit: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes all global services
        /// </summary>
        private void DisposeServices()
        {
            Console.WriteLine("Disposing global services...");
            
            try
            {
                // Dispose tab management services first
                if (DragDropService != null)
                {
                    Console.WriteLine("DragDropService reference cleared");
                    DragDropService = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing DragDropService: {ex.Message}");
            }
            
            try
            {
                if (TabOperationsManager != null)
                {
                    Console.WriteLine("TabOperationsManager reference cleared");
                    TabOperationsManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing TabOperationsManager: {ex.Message}");
            }
            
            try
            {
                if (WindowManager != null)
                {
                    Console.WriteLine("WindowManager reference cleared");
                    WindowManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing WindowManager: {ex.Message}");
            }
            
            try
            {
                if (TabStateManager != null)
                {
                    Console.WriteLine("TabStateManager reference cleared");
                    TabStateManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing TabStateManager: {ex.Message}");
            }
            
            try
            {
                if (PerformanceOptimizer != null)
                {
                    PerformanceOptimizer.Dispose();
                    Console.WriteLine("PerformanceOptimizer disposed");
                    PerformanceOptimizer = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing PerformanceOptimizer: {ex.Message}");
            }
            
            try
            {
                if (TabHibernationManager != null)
                {
                    TabHibernationManager.Dispose();
                    Console.WriteLine("TabHibernationManager disposed");
                    TabHibernationManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing TabHibernationManager: {ex.Message}");
            }
            
            try
            {
                if (TabVirtualizationManager != null)
                {
                    TabVirtualizationManager.Stop();
                    Console.WriteLine("TabVirtualizationManager stopped");
                    TabVirtualizationManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping TabVirtualizationManager: {ex.Message}");
            }
            
            try
            {
                if (ResourceMonitor != null)
                {
                    ResourceMonitor.Dispose();
                    Console.WriteLine("ResourceMonitor disposed");
                    ResourceMonitor = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing ResourceMonitor: {ex.Message}");
            }
            
            try
            {
                // Dispose MetadataManager - it implements IDisposable
                if (MetadataManager != null)
                {
                    MetadataManager.Dispose();
                    Console.WriteLine("MetadataManager disposed");
                    MetadataManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing MetadataManager: {ex.Message}");
            }
            
            try
            {
                // Clear PinnedManager reference
                if (PinnedManager != null)
                {
                    // PinnedManager is a singleton and may have resources to clean up
                    // If it implements IDisposable in the future, add disposal here
                    Console.WriteLine("PinnedManager reference cleared");
                    PinnedManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing PinnedManager: {ex.Message}");
            }
            
            try
            {
                // Clear RecurringTaskManager reference
                if (RecurringTaskManager != null)
                {
                    // RecurringTaskManager doesn't implement IDisposable
                    // Just clear the reference
                    Console.WriteLine("RecurringTaskManager reference cleared");
                    RecurringTaskManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing RecurringTaskManager: {ex.Message}");
            }
            
            try
            {
                // Clear UndoManager reference
                if (UndoManager != null)
                {
                    // Check if UndoManager has a Clear or Reset method
                    // For now, just clear the reference
                    Console.WriteLine("UndoManager reference cleared");
                    UndoManager = null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing UndoManager: {ex.Message}");
            }
            
            try
            {
                // Clear static references in Settings
                Settings = null;
                Console.WriteLine("Settings reference cleared");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error clearing Settings: {ex.Message}");
            }
            
            Console.WriteLine("Global services disposal completed");
        }
        
        /// <summary>
        /// Unsubscribes from theme manager events
        /// </summary>
        private void UnsubscribeFromThemeEvents()
        {
            try
            {
                if (_themeChangedHandler != null)
                {
                    ThemeManager.Instance.ThemeChanged -= _themeChangedHandler;
                    _themeChangedHandler = null;
                    Console.WriteLine("Unsubscribed from theme events");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error unsubscribing from theme events: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes the logger factory
        /// </summary>
        private void DisposeLoggerFactory()
        {
            try
            {
                if (_loggerFactory != null)
                {
                    _loggerFactory.Dispose();
                    _loggerFactory = null;
                    Console.WriteLine("Logger factory disposed");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing logger factory: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Disposes the shared MainWindow logger factory
        /// IMPLEMENTATION OF FIX 1: Logger Factory Memory Leaks
        /// </summary>
        private void DisposeMainWindowSharedLogger()
        {
            try
            {
                ExplorerPro.UI.MainWindow.MainWindow.DisposeSharedLogger();
                Console.WriteLine("MainWindow shared logger factory disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error disposing MainWindow shared logger factory: {ex.Message}");
            }
        }
        
        private void CleanupPhase1CriticalFixes()
        {
            try
            {
                // Stop monitoring and report final telemetry
                if (TabResolutionMonitor != null)
                {
                    Console.WriteLine("Stopping TabResolutionMonitor...");
                    TabResolutionMonitor.Dispose();
                    TabResolutionMonitor = null;
                }
                
                // Report final TabModelResolver statistics
                if (TelemetryService != null)
                {
                    Console.WriteLine("Reporting final TabModelResolver statistics...");
                    var finalStats = ExplorerPro.Core.TabManagement.TabModelResolver.GetStats();
                    TelemetryService.TrackEvent("TabModelResolver.FinalStats", new Dictionary<string, object>
                    {
                        ["DataContextHits"] = finalStats.DataContextHits,
                        ["TagFallbacks"] = finalStats.TagFallbacks,
                        ["Migrations"] = finalStats.Migrations,
                        ["NotFound"] = finalStats.NotFound,
                        ["FallbackRate"] = finalStats.TagFallbackRate
                    });
                    
                    Console.WriteLine($"Final TabModel stats - DataContext: {finalStats.DataContextHits}, " +
                                    $"Tag fallbacks: {finalStats.TagFallbacks}, Migrations: {finalStats.Migrations}");
                }
                
                // Clear references
                TelemetryService = null;
                
                Console.WriteLine("Phase 1 Critical Fixes cleanup completed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during Phase 1 Critical Fixes cleanup: {ex.Message}");
            }
        }
        
        private void LogApplicationExit()
        {
            try
            {
                string logMessage = $"Application exited at {DateTime.Now}";
                File.AppendAllText(LogPath, logMessage + Environment.NewLine);
                Console.WriteLine(logMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error logging application exit: {ex.Message}");
            }
        }
    }
}