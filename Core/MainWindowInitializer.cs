using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Handles safe, consistent initialization of MainWindow instances
    /// </summary>
    public sealed class MainWindowInitializer
    {
        private readonly ILogger<MainWindowInitializer> _logger;
        
        public MainWindowInitializer(ILogger<MainWindowInitializer> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        
        /// <summary>
        /// Initialize window with comprehensive error handling and rollback
        /// </summary>
        public async Task<InitializationResult> InitializeWindowAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            if (window == null) throw new ArgumentNullException(nameof(window));
            if (context == null) throw new ArgumentNullException(nameof(context));
            
            _logger.LogInformation("Starting window initialization");
            
            try
            {
                // Step 1: Validate prerequisites
                if (!await ValidatePrerequisitesAsync(window, context))
                {
                    return InitializationResult.Failure(
                        "Prerequisites validation failed", 
                        context.CurrentState);
                }
                
                // Step 2: Initialize core components
                if (!await InitializeCoreComponentsAsync(window, context))
                {
                    return InitializationResult.Failure(
                        "Core components initialization failed", 
                        context.CurrentState);
                }
                
                // Step 3: Initialize UI elements
                if (!await InitializeUIElementsAsync(window, context))
                {
                    await RollbackCoreComponentsAsync(window, context);
                    return InitializationResult.Failure(
                        "UI elements initialization failed", 
                        context.CurrentState);
                }
                
                // Step 4: Wire up event handlers
                if (!await WireEventHandlersAsync(window, context))
                {
                    await RollbackUIElementsAsync(window, context);
                    await RollbackCoreComponentsAsync(window, context);
                    return InitializationResult.Failure(
                        "Event handler setup failed", 
                        context.CurrentState);
                }
                
                // Step 5: Final validation
                if (!await ValidateFinalStateAsync(window, context))
                {
                    return InitializationResult.Failure(
                        "Final validation failed", 
                        context.CurrentState);
                }
                
                context.TransitionTo(WindowState.Ready);
                _logger.LogInformation("Window initialization completed successfully");
                
                return InitializationResult.Success();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Window initialization failed with exception");
                context.TransitionTo(WindowState.Failed);
                
                // Attempt cleanup
                try
                {
                    await EmergencyCleanupAsync(window, context);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "Emergency cleanup failed");
                }
                
                return InitializationResult.Failure(
                    $"Initialization failed: {ex.Message}", 
                    context.CurrentState, 
                    ex);
            }
        }
        
        private async Task<bool> ValidatePrerequisitesAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            await Task.Yield(); // Ensure async
            
            context.RecordStep("ValidatePrerequisites");
            
            // Check window is in correct state
            if (!window.IsInitialized)
            {
                _logger.LogError("Window InitializeComponent not called");
                return false;
            }
            
            // Validate required services
            if (window.MainTabs == null)
            {
                _logger.LogError("MainTabs control not found");
                return false;
            }
            
            return true;
        }
        
        private async Task<bool> InitializeCoreComponentsAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            context.RecordStep("InitializeCoreComponents");
            
            if (!context.TransitionTo(WindowState.Initializing))
            {
                _logger.LogError("Failed to transition to Initializing state");
                return false;
            }
            
            try
            {
                // Initialize in correct order with individual error handling
                _logger.LogDebug("Initializing MetadataManager...");
                window.InitializeMetadataManager();
                context.RecordStep("MetadataManager initialized");
                
                _logger.LogDebug("Initializing Navigation history...");
                window.InitializeNavigationHistory();
                context.RecordStep("Navigation history initialized");
                
                _logger.LogDebug("Registering with lifecycle manager...");
                window.RegisterWithLifecycleManager();
                context.RecordStep("Registered with lifecycle manager");
                
                await Task.Delay(10); // Allow UI thread to process
                
                _logger.LogDebug("Core components initialization completed successfully");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception during core components initialization: {Message}", ex.Message);
                return false;
            }
        }
        
        private async Task<bool> InitializeUIElementsAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            context.RecordStep("InitializeUIElements");
            
            if (!context.TransitionTo(WindowState.ComponentsReady))
            {
                return false;
            }
            
            // Initialize UI on UI thread
            await window.Dispatcher.InvokeAsync(() =>
            {
                window.SetupDragDrop();
                context.RecordStep("Drag-drop configured");
                
                window.InitializeKeyboardShortcuts();
                context.RecordStep("Keyboard shortcuts initialized");
                
                window.RestoreWindowLayout();
                context.RecordStep("Window layout restored");
            });
            
            return true;
        }
        
        private async Task<bool> WireEventHandlersAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            context.RecordStep("WireEventHandlers");
            
            await window.Dispatcher.InvokeAsync(() =>
            {
                window.WireUpEventHandlers();
                context.RecordStep("Event handlers wired");
                
                window.SetupThemeHandlers();
                context.RecordStep("Theme handlers setup");
            });
            
            return true;
        }
        
        private async Task<bool> ValidateFinalStateAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            context.RecordStep("ValidateFinalState");
            
            await Task.Yield();
            
            // Ensure everything is properly initialized
            return window.ValidateInitialization();
        }
        
        private async Task RollbackCoreComponentsAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            context.RecordStep("RollbackCoreComponents");
            _logger.LogWarning("Rolling back core components");
            
            await Task.Run(() =>
            {
                window.UnregisterFromLifecycleManager();
                window.ClearNavigationHistory();
            });
        }
        
        private async Task RollbackUIElementsAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            context.RecordStep("RollbackUIElements");
            _logger.LogWarning("Rolling back UI elements");
            
            await window.Dispatcher.InvokeAsync(() =>
            {
                window.ClearDragDrop();
                window.ClearKeyboardShortcuts();
            });
        }
        
        private async Task EmergencyCleanupAsync(
            MainWindow window, 
            WindowInitializationContext context)
        {
            context.RecordStep("EmergencyCleanup");
            _logger.LogWarning("Performing emergency cleanup");
            
            await Task.Run(() =>
            {
                try { window.UnregisterFromLifecycleManager(); } catch { }
                try { window.ClearNavigationHistory(); } catch { }
                try { window.Dispatcher.Invoke(() => window.ClearAllEventHandlers()); } catch { }
            });
        }
    }
    
    /// <summary>
    /// Result of initialization attempt
    /// </summary>
    public class InitializationResult
    {
        public bool IsSuccess { get; }
        public string ErrorMessage { get; }
        public WindowState State { get; }
        public Exception Error { get; }
        
        private InitializationResult(bool success, string errorMessage, WindowState state, Exception error)
        {
            IsSuccess = success;
            ErrorMessage = errorMessage;
            State = state;
            Error = error;
        }
        
        public static InitializationResult Success()
        {
            return new InitializationResult(true, null, WindowState.Ready, null);
        }
        
        public static InitializationResult Failure(string message, WindowState state, Exception error = null)
        {
            return new InitializationResult(false, message, state, error);
        }
    }
} 