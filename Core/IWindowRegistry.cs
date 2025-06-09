using System;
using System.Collections.Generic;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Defines the contract for managing MainWindow instances lifecycle.
    /// </summary>
    public interface IWindowRegistry
    {
        /// <summary>
        /// Registers a new window instance.
        /// </summary>
        void RegisterWindow(ExplorerPro.UI.MainWindow.MainWindow window);
        
        /// <summary>
        /// Unregisters a window instance.
        /// </summary>
        bool UnregisterWindow(ExplorerPro.UI.MainWindow.MainWindow window);
        
        /// <summary>
        /// Gets all active window instances.
        /// </summary>
        IEnumerable<ExplorerPro.UI.MainWindow.MainWindow> GetActiveWindows();
        
        /// <summary>
        /// Gets the count of active windows.
        /// </summary>
        int ActiveWindowCount { get; }
        
        /// <summary>
        /// Finds a window by its ID.
        /// </summary>
        ExplorerPro.UI.MainWindow.MainWindow FindWindow(Guid windowId);
        
        /// <summary>
        /// Performs cleanup of disposed windows.
        /// </summary>
        void CleanupDisposedWindows();
    }
} 