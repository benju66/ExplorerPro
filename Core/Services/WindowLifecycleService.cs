using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core;
using WinForms = System.Windows.Forms;

namespace ExplorerPro.Core.Services
{
    /// <summary>
    /// Service interface for window lifecycle management operations in ExplorerPro.
    /// Handles window geometry, state persistence, and layout restoration.
    /// </summary>
    public interface IWindowLifecycleService
    {
        /// <summary>
        /// Event fired when window layout is saved
        /// </summary>
        event EventHandler<WindowLayoutEventArgs> LayoutSaved;
        
        /// <summary>
        /// Event fired when window layout is restored
        /// </summary>
        event EventHandler<WindowLayoutEventArgs> LayoutRestored;
        
        /// <summary>
        /// Saves the current window layout
        /// </summary>
        void SaveWindowLayout(Window window, string windowId, ISettingsService settingsService);
        
        /// <summary>
        /// Restores window layout from saved settings
        /// </summary>
        bool RestoreWindowLayout(Window window, string windowId, ISettingsService settingsService);
        
        /// <summary>
        /// Gets window geometry as byte array for serialization
        /// </summary>
        byte[] GetWindowGeometryBytes(Window window);
        
        /// <summary>
        /// Gets window state as byte array for serialization
        /// </summary>
        byte[] GetWindowStateBytes(Window window);
        
        /// <summary>
        /// Restores window geometry from byte array
        /// </summary>
        bool TryRestoreWindowGeometry(Window window, byte[] geometryBytes);
        
        /// <summary>
        /// Restores window state from byte array
        /// </summary>
        bool TryRestoreWindowState(Window window, byte[] stateBytes);
        
        /// <summary>
        /// Checks if a rectangle is visible on any screen
        /// </summary>
        bool IsRectOnScreen(Rect rect);
        
        /// <summary>
        /// Validates window geometry parameters
        /// </summary>
        bool IsValidGeometry(double left, double top, double width, double height);
        
        /// <summary>
        /// Centers window on screen
        /// </summary>
        void CenterWindowOnScreen(Window window);
    }

    /// <summary>
    /// Event arguments for window layout operations
    /// </summary>
    public class WindowLayoutEventArgs : EventArgs
    {
        public string WindowId { get; set; }
        public bool Success { get; set; }
        public string Message { get; set; }
        public Exception Exception { get; set; }
    }

    /// <summary>
    /// Implementation of IWindowLifecycleService for managing window lifecycle operations
    /// </summary>
    public class WindowLifecycleService : IWindowLifecycleService
    {
        private readonly ILogger<WindowLifecycleService> _logger;

        public WindowLifecycleService(ILogger<WindowLifecycleService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Event fired when window layout is saved
        /// </summary>
        public event EventHandler<WindowLayoutEventArgs> LayoutSaved;

        /// <summary>
        /// Event fired when window layout is restored
        /// </summary>
        public event EventHandler<WindowLayoutEventArgs> LayoutRestored;

        /// <summary>
        /// Saves the current window layout
        /// </summary>
        public void SaveWindowLayout(Window window, string windowId, ISettingsService settingsService)
        {
            try
            {
                if (window == null)
                    throw new ArgumentNullException(nameof(window));

                var windowSettings = ExplorerPro.Models.WindowSettings.FromWindow(window, _logger);
                _ = settingsService.SaveWindowSettingsAsync(windowId, windowSettings);
                
                // Legacy fallback for compatibility
                byte[] geometryBytes = GetWindowGeometryBytes(window);
                byte[] stateBytes = GetWindowStateBytes(window);
                
                if (App.Settings != null)
                {
                    App.Settings.StoreMainWindowLayout(geometryBytes, stateBytes);
                }

                _logger.LogInformation($"Window layout saved for window: {windowId}");
                
                LayoutSaved?.Invoke(this, new WindowLayoutEventArgs
                {
                    WindowId = windowId,
                    Success = true,
                    Message = "Window layout saved successfully"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error saving window layout for window: {windowId}");
                
                LayoutSaved?.Invoke(this, new WindowLayoutEventArgs
                {
                    WindowId = windowId,
                    Success = false,
                    Message = "Failed to save window layout",
                    Exception = ex
                });
            }
        }

        /// <summary>
        /// Restores window layout from saved settings
        /// </summary>
        public bool RestoreWindowLayout(Window window, string windowId, ISettingsService settingsService)
        {
            try
            {
                if (window == null)
                    throw new ArgumentNullException(nameof(window));

                var windowSettings = settingsService.GetWindowSettings(windowId);
                if (windowSettings != null && windowSettings.IsValid)
                {
                    windowSettings.ApplyTo(window, _logger);
                    
                    _logger.LogInformation($"Window layout restored from settings for window: {windowId}");
                    
                    LayoutRestored?.Invoke(this, new WindowLayoutEventArgs
                    {
                        WindowId = windowId,
                        Success = true,
                        Message = "Window layout restored from settings"
                    });
                    
                    return true;
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
                    _logger.LogError(ex, "Error retrieving legacy window layout");
                }

                bool geometryRestored = false;
                bool stateRestored = false;

                if (geometryBytes != null)
                {
                    geometryRestored = TryRestoreWindowGeometry(window, geometryBytes);
                }

                if (stateBytes != null)
                {
                    stateRestored = TryRestoreWindowState(window, stateBytes);
                }

                bool success = geometryRestored || stateRestored;
                
                if (success)
                {
                    _logger.LogInformation($"Window layout restored from legacy data for window: {windowId}");
                }
                else
                {
                    _logger.LogWarning($"No saved layout found for window: {windowId}");
                }
                
                LayoutRestored?.Invoke(this, new WindowLayoutEventArgs
                {
                    WindowId = windowId,
                    Success = success,
                    Message = success ? "Window layout restored from legacy data" : "No saved layout found"
                });
                
                return success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error restoring window layout for window: {windowId}");
                
                LayoutRestored?.Invoke(this, new WindowLayoutEventArgs
                {
                    WindowId = windowId,
                    Success = false,
                    Message = "Failed to restore window layout",
                    Exception = ex
                });
                
                return false;
            }
        }

        /// <summary>
        /// Gets window geometry as byte array for serialization
        /// </summary>
        public byte[] GetWindowGeometryBytes(Window window)
        {
            try
            {
                if (window == null)
                    throw new ArgumentNullException(nameof(window));

                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write(window.Left);
                    writer.Write(window.Top);
                    writer.Write(window.Width);
                    writer.Write(window.Height);
                    writer.Write((int)window.WindowState);
                    
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing window geometry");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Gets window state as byte array for serialization
        /// </summary>
        public byte[] GetWindowStateBytes(Window window)
        {
            try
            {
                if (window == null)
                    throw new ArgumentNullException(nameof(window));

                using (MemoryStream stream = new MemoryStream())
                using (BinaryWriter writer = new BinaryWriter(stream))
                {
                    writer.Write((int)window.WindowState);
                    
                    // Add other state data as needed
                    
                    return stream.ToArray();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error serializing window state");
                return Array.Empty<byte>();
            }
        }

        /// <summary>
        /// Restores window geometry from byte array
        /// </summary>
        public bool TryRestoreWindowGeometry(Window window, byte[] geometryBytes)
        {
            try
            {
                if (window == null || geometryBytes == null || geometryBytes.Length == 0)
                    return false;

                using (MemoryStream stream = new MemoryStream(geometryBytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Read window position and size
                    double left = reader.ReadDouble();
                    double top = reader.ReadDouble();
                    double width = reader.ReadDouble();
                    double height = reader.ReadDouble();

                    // Validate geometry
                    if (!IsValidGeometry(left, top, width, height))
                    {
                        _logger.LogWarning("Invalid window geometry values");
                        return false;
                    }

                    // Check if window would be visible on screen
                    if (IsRectOnScreen(new Rect(left, top, width, height)))
                    {
                        window.Left = left;
                        window.Top = top;
                        window.Width = width;
                        window.Height = height;
                        
                        _logger.LogDebug($"Window geometry restored: {left},{top} {width}x{height}");
                        return true;
                    }
                    else
                    {
                        // Use default centered position
                        CenterWindowOnScreen(window);
                        _logger.LogWarning("Window would be off-screen, using centered position");
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring window geometry");
                return false;
            }
        }

        /// <summary>
        /// Restores window state from byte array
        /// </summary>
        public bool TryRestoreWindowState(Window window, byte[] stateBytes)
        {
            try
            {
                if (window == null || stateBytes == null || stateBytes.Length == 0)
                    return false;

                using (MemoryStream stream = new MemoryStream(stateBytes))
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    // Read window state
                    int stateValue = reader.ReadInt32();
                    var windowState = (System.Windows.WindowState)stateValue;
                    
                    // Validate state value
                    if (Enum.IsDefined(typeof(System.Windows.WindowState), windowState))
                    {
                        window.WindowState = windowState;
                        _logger.LogDebug($"Window state restored: {windowState}");
                        return true;
                    }
                    else
                    {
                        _logger.LogWarning($"Invalid window state value: {stateValue}");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error restoring window state");
                return false;
            }
        }

        /// <summary>
        /// Checks if a rectangle is visible on any screen
        /// </summary>
        public bool IsRectOnScreen(Rect rect)
        {
            try
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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if rectangle is on screen");
                return false;
            }
        }

        /// <summary>
        /// Validates window geometry parameters
        /// </summary>
        public bool IsValidGeometry(double left, double top, double width, double height)
        {
            // Check for valid dimensions
            if (width <= 0 || height <= 0)
                return false;
                
            // Check for reasonable bounds (not too large or negative)
            if (width > 10000 || height > 10000)
                return false;
                
            // Check for extreme negative positions
            if (left < -10000 || top < -10000)
                return false;
                
            // Check for NaN or infinity
            if (double.IsNaN(left) || double.IsNaN(top) || 
                double.IsNaN(width) || double.IsNaN(height) ||
                double.IsInfinity(left) || double.IsInfinity(top) || 
                double.IsInfinity(width) || double.IsInfinity(height))
                return false;
                
            return true;
        }

        /// <summary>
        /// Centers window on screen
        /// </summary>
        public void CenterWindowOnScreen(Window window)
        {
            try
            {
                if (window == null)
                    return;

                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                _logger.LogDebug("Window centered on screen");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error centering window on screen");
            }
        }
    }
} 