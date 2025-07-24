using System.Collections.Generic;
using System.Windows;
using ExplorerPro.Models;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Manages detached window lifecycle and operations
    /// </summary>
    public interface IDetachedWindowManager
    {
        /// <summary>
        /// Detaches a tab to a new window
        /// </summary>
        Window DetachTab(TabModel tab, Window sourceWindow);

        /// <summary>
        /// Reattaches a tab to a target window
        /// </summary>
        void ReattachTab(TabModel tab, Window targetWindow, int insertIndex = -1);

        /// <summary>
        /// Gets all currently detached windows
        /// </summary>
        IReadOnlyList<DetachedWindowInfo> GetDetachedWindows();

        /// <summary>
        /// Registers a window for management
        /// </summary>
        void RegisterWindow(Window window);

        /// <summary>
        /// Unregisters a window from management
        /// </summary>
        void UnregisterWindow(Window window);

        /// <summary>
        /// Finds the window containing a specific tab
        /// </summary>
        Window FindWindowContainingTab(TabModel tab);

        /// <summary>
        /// Gets all windows that can accept tab drops
        /// </summary>
        IEnumerable<Window> GetDropTargetWindows();
    }
} 
