using System;
using System.Windows;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Window-specific settings model with validation and screen bounds checking.
    /// IMPLEMENTS FIX 7: Settings Management Coupling - Provides validated window settings
    /// </summary>
    public class WindowSettings
    {
        /// <summary>
        /// Default window width in pixels.
        /// </summary>
        public const double DEFAULT_WIDTH = 1024;

        /// <summary>
        /// Default window height in pixels.
        /// </summary>
        public const double DEFAULT_HEIGHT = 768;

        /// <summary>
        /// Minimum window width in pixels.
        /// </summary>
        public const double MIN_WIDTH = 400;

        /// <summary>
        /// Minimum window height in pixels.
        /// </summary>
        public const double MIN_HEIGHT = 300;

        /// <summary>
        /// Maximum window width as percentage of screen width.
        /// </summary>
        public const double MAX_WIDTH_RATIO = 0.95;

        /// <summary>
        /// Maximum window height as percentage of screen height.
        /// </summary>
        public const double MAX_HEIGHT_RATIO = 0.95;

        /// <summary>
        /// Window's left position on screen.
        /// </summary>
        public double Left { get; set; }

        /// <summary>
        /// Window's top position on screen.
        /// </summary>
        public double Top { get; set; }

        /// <summary>
        /// Window width in pixels.
        /// </summary>
        public double Width { get; set; } = DEFAULT_WIDTH;

        /// <summary>
        /// Window height in pixels.
        /// </summary>
        public double Height { get; set; } = DEFAULT_HEIGHT;

        /// <summary>
        /// Window state (Normal, Minimized, Maximized).
        /// </summary>
        public WindowState WindowState { get; set; } = WindowState.Normal;

        /// <summary>
        /// Gets whether the window settings are valid.
        /// </summary>
        public bool IsValid => 
            Width >= MIN_WIDTH && 
            Height >= MIN_HEIGHT && 
            Width <= SystemParameters.VirtualScreenWidth * MAX_WIDTH_RATIO &&
            Height <= SystemParameters.VirtualScreenHeight * MAX_HEIGHT_RATIO &&
            !double.IsNaN(Width) && !double.IsInfinity(Width) &&
            !double.IsNaN(Height) && !double.IsInfinity(Height) &&
            !double.IsNaN(Left) && !double.IsInfinity(Left) &&
            !double.IsNaN(Top) && !double.IsInfinity(Top);

        /// <summary>
        /// Default constructor with safe default values.
        /// </summary>
        public WindowSettings()
        {
            // Center window on primary screen by default
            var primaryScreen = Screen.PrimaryScreen;
            if (primaryScreen != null)
            {
                Left = (primaryScreen.WorkingArea.Width - DEFAULT_WIDTH) / 2;
                Top = (primaryScreen.WorkingArea.Height - DEFAULT_HEIGHT) / 2;
            }
            else
            {
                Left = 100;
                Top = 100;
            }
        }

        /// <summary>
        /// Applies these settings to a WPF window with validation and error handling.
        /// </summary>
        /// <param name="window">The window to apply settings to</param>
        /// <param name="logger">Optional logger for error reporting</param>
        /// <returns>True if settings were applied successfully, false if fallback was used</returns>
        public bool ApplyTo(Window window, ILogger logger = null)
        {
            if (window == null)
            {
                logger?.LogWarning("Cannot apply window settings: window is null");
                return false;
            }

            try
            {
                if (!IsValid)
                {
                    logger?.LogWarning("Invalid window settings detected, using defaults");
                    ApplyDefaults(window);
                    return false;
                }

                // Validate position is on a visible screen
                var targetBounds = new Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
                var screen = GetScreenFromBounds(targetBounds);
                
                if (screen == null)
                {
                    logger?.LogWarning("Window position is off-screen, centering on primary screen");
                    CenterOnPrimaryScreen(window);
                    return false;
                }

                // Apply validated settings
                var workingArea = screen.WorkingArea;
                
                // Ensure window fits within screen bounds
                var constrainedLeft = Math.Max(workingArea.Left, 
                    Math.Min(Left, workingArea.Right - Width));
                var constrainedTop = Math.Max(workingArea.Top, 
                    Math.Min(Top, workingArea.Bottom - Height));

                window.Left = constrainedLeft;
                window.Top = constrainedTop;
                window.Width = Math.Min(Width, workingArea.Width * MAX_WIDTH_RATIO);
                window.Height = Math.Min(Height, workingArea.Height * MAX_HEIGHT_RATIO);
                window.WindowState = WindowState;

                logger?.LogDebug("Applied window settings: {Left},{Top} {Width}x{Height} {State}", 
                    window.Left, window.Top, window.Width, window.Height, window.WindowState);

                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error applying window settings, using defaults");
                ApplyDefaults(window);
                return false;
            }
        }

        /// <summary>
        /// Creates window settings from an existing WPF window.
        /// </summary>
        /// <param name="window">The window to capture settings from</param>
        /// <param name="logger">Optional logger for error reporting</param>
        /// <returns>WindowSettings object, or default settings if capture fails</returns>
        public static WindowSettings FromWindow(Window window, ILogger logger = null)
        {
            if (window == null)
            {
                logger?.LogWarning("Cannot capture window settings: window is null");
                return new WindowSettings();
            }

            try
            {
                var settings = new WindowSettings
                {
                    Left = window.RestoreBounds != Rect.Empty ? window.RestoreBounds.Left : window.Left,
                    Top = window.RestoreBounds != Rect.Empty ? window.RestoreBounds.Top : window.Top,
                    Width = window.RestoreBounds != Rect.Empty ? window.RestoreBounds.Width : window.Width,
                    Height = window.RestoreBounds != Rect.Empty ? window.RestoreBounds.Height : window.Height,
                    WindowState = window.WindowState
                };

                // Validate captured settings
                if (!settings.IsValid)
                {
                    logger?.LogWarning("Captured window settings are invalid, using defaults");
                    return new WindowSettings();
                }

                logger?.LogDebug("Captured window settings: {Left},{Top} {Width}x{Height} {State}", 
                    settings.Left, settings.Top, settings.Width, settings.Height, settings.WindowState);

                return settings;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error capturing window settings, using defaults");
                return new WindowSettings();
            }
        }

        /// <summary>
        /// Creates default window settings centered on the primary screen.
        /// </summary>
        /// <returns>Default window settings</returns>
        public static WindowSettings CreateDefault()
        {
            return new WindowSettings();
        }

        /// <summary>
        /// Validates and corrects settings to ensure they are within acceptable bounds.
        /// </summary>
        /// <param name="logger">Optional logger for reporting corrections</param>
        /// <returns>True if settings were valid or successfully corrected, false if severe issues exist</returns>
        public bool ValidateAndCorrect(ILogger logger = null)
        {
            bool corrected = false;

            try
            {
                // Correct width and height
                if (Width < MIN_WIDTH || double.IsNaN(Width) || double.IsInfinity(Width))
                {
                    Width = DEFAULT_WIDTH;
                    corrected = true;
                    logger?.LogDebug("Corrected window width to default");
                }

                if (Height < MIN_HEIGHT || double.IsNaN(Height) || double.IsInfinity(Height))
                {
                    Height = DEFAULT_HEIGHT;
                    corrected = true;
                    logger?.LogDebug("Corrected window height to default");
                }

                // Ensure position is valid
                if (double.IsNaN(Left) || double.IsInfinity(Left) || 
                    double.IsNaN(Top) || double.IsInfinity(Top))
                {
                    var primaryScreen = Screen.PrimaryScreen;
                    if (primaryScreen != null)
                    {
                        Left = (primaryScreen.WorkingArea.Width - Width) / 2;
                        Top = (primaryScreen.WorkingArea.Height - Height) / 2;
                    }
                    else
                    {
                        Left = 100;
                        Top = 100;
                    }
                    corrected = true;
                    logger?.LogDebug("Corrected window position to center of primary screen");
                }

                // Validate against screen bounds
                var targetBounds = new Rectangle((int)Left, (int)Top, (int)Width, (int)Height);
                var screen = GetScreenFromBounds(targetBounds);
                
                if (screen == null)
                {
                    // Position is completely off-screen, center on primary
                    var primaryScreen = Screen.PrimaryScreen;
                    if (primaryScreen != null)
                    {
                        Left = (primaryScreen.WorkingArea.Width - Width) / 2;
                        Top = (primaryScreen.WorkingArea.Height - Height) / 2;
                        corrected = true;
                        logger?.LogDebug("Moved off-screen window to primary screen center");
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error validating window settings");
                return false;
            }
        }

        /// <summary>
        /// Applies default settings to a window.
        /// </summary>
        private void ApplyDefaults(Window window)
        {
            var defaults = new WindowSettings();
            window.Left = defaults.Left;
            window.Top = defaults.Top;
            window.Width = defaults.Width;
            window.Height = defaults.Height;
            window.WindowState = defaults.WindowState;
        }

        /// <summary>
        /// Centers a window on the primary screen.
        /// </summary>
        private void CenterOnPrimaryScreen(Window window)
        {
            var primaryScreen = Screen.PrimaryScreen;
            if (primaryScreen != null)
            {
                window.Left = (primaryScreen.WorkingArea.Width - DEFAULT_WIDTH) / 2;
                window.Top = (primaryScreen.WorkingArea.Height - DEFAULT_HEIGHT) / 2;
                window.Width = DEFAULT_WIDTH;
                window.Height = DEFAULT_HEIGHT;
                window.WindowState = WindowState.Normal;
            }
        }

        /// <summary>
        /// Gets the screen that contains or is closest to the specified bounds.
        /// </summary>
        private Screen GetScreenFromBounds(Rectangle bounds)
        {
            // First try to find a screen that contains the bounds
            foreach (var screen in Screen.AllScreens)
            {
                if (screen.WorkingArea.IntersectsWith(bounds))
                {
                    return screen;
                }
            }

            // If no screen contains the bounds, find the closest one
            Screen closestScreen = null;
            double closestDistance = double.MaxValue;
            var boundsCenter = new System.Drawing.Point(
                bounds.X + bounds.Width / 2, 
                bounds.Y + bounds.Height / 2);

            foreach (var screen in Screen.AllScreens)
            {
                var screenCenter = new System.Drawing.Point(
                    screen.WorkingArea.X + screen.WorkingArea.Width / 2,
                    screen.WorkingArea.Y + screen.WorkingArea.Height / 2);

                var distance = Math.Sqrt(
                    Math.Pow(boundsCenter.X - screenCenter.X, 2) +
                    Math.Pow(boundsCenter.Y - screenCenter.Y, 2));

                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestScreen = screen;
                }
            }

            return closestScreen;
        }

        /// <summary>
        /// Returns a string representation of the window settings.
        /// </summary>
        public override string ToString()
        {
            return $"WindowSettings: {Left},{Top} {Width}x{Height} {WindowState} (Valid: {IsValid})";
        }
    }
} 