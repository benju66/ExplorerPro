using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using ExplorerPro.Models;
using ExplorerPro.Utilities;
using ExplorerPro.Themes;
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
                    
                    // Append to log file
                    File.AppendAllText(ErrorLogPath, 
                        $"[{DateTime.Now}] {errorMsg}\r\n{ex.StackTrace}\r\n\r\n");
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
                
                // Create default settings on error
                try
                {
                    Console.WriteLine("Creating default settings");
                    Settings = new SettingsManager(SettingsPath);
                    Settings.ApplyDefaultSettings();
                    Settings.SaveSettings();
                }
                catch (Exception innerEx)
                {
                    HandleStartupError(innerEx, "Error creating default settings");
                    
                    // Create settings in memory only as last resort
                    Settings = new SettingsManager();
                    Settings.ApplyDefaultSettings();
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
                
                Console.WriteLine("All services initialized successfully");
            }
            catch (Exception ex)
            {
                HandleStartupError(ex, "Error initializing application services");
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
            try
            {
                // Close all windows first
                foreach (Window window in Windows)
                {
                    try
                    {
                        window.Close();
                    }
                    catch { }
                }

                // Wait briefly for windows to finish closing
                System.Threading.Thread.Sleep(100);

                // Now safe to dispose logger
                ExplorerPro.UI.MainWindow.MainWindow.DisposeSharedLogger();
                
                // Save state of each manager separately to isolate errors
                SaveSettingsOnExit();
                SaveMetadataOnExit();
                
                // Dispose all services
                DisposeServices();
                
                // Unsubscribe from theme events
                UnsubscribeFromThemeEvents();
                
                // Dispose logger factory
                DisposeLoggerFactory();
                
                // Log application exit
                LogApplicationExit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during application exit: {ex.Message}");
            }
            finally
            {
                base.OnExit(e);
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