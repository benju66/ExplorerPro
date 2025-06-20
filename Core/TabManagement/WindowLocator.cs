using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Helper class to locate windows under cursor
    /// </summary>
    public static class WindowLocator
    {
        [DllImport("user32.dll")]
        private static extern IntPtr WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        private static extern IntPtr GetAncestor(IntPtr hwnd, uint flags);

        private const uint GA_ROOT = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        /// <summary>
        /// Finds the WPF window under the specified screen point
        /// </summary>
        public static Window FindWindowUnderPoint(Point screenPoint)
        {
            var point = new POINT { X = (int)screenPoint.X, Y = (int)screenPoint.Y };
            var hwnd = WindowFromPoint(point);
            
            if (hwnd == IntPtr.Zero)
                return null;

            // Get root window
            var rootHwnd = GetAncestor(hwnd, GA_ROOT);
            if (rootHwnd == IntPtr.Zero)
                rootHwnd = hwnd;

            // Find WPF window
            foreach (Window window in Application.Current.Windows)
            {
                var helper = new WindowInteropHelper(window);
                if (helper.Handle == rootHwnd)
                {
                    return window;
                }
            }

            return null;
        }
    }
} 