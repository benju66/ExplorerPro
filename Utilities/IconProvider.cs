using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ExplorerPro.Utilities
{
    /// <summary>
    /// Provides icons for files and folders based on their extension or type
    /// </summary>
    public static class IconProvider
    {
        // Cache to avoid repeatedly loading the same icon
        private static Dictionary<string, BitmapSource?> _iconCache = new Dictionary<string, BitmapSource?>();
        private static Dictionary<string, BitmapSource?> _coloredIconCache = new Dictionary<string, BitmapSource?>();
        
        /// <summary>
        /// Initializes common file type icons for the application
        /// </summary>
        public static void InitializeFileTypeIcons()
        {
            // Pre-cache common file type icons
            try
            {
                // Pre-load common file type icons
                GetIconForPath("file.txt", true);
                GetIconForPath("file.pdf", true);
                GetIconForPath("file.docx", true);
                GetIconForPath("file.xlsx", true);
                GetIconForPath("file.pptx", true);
                GetIconForPath("file.jpg", true);
                GetIconForPath("file.png", true);
                GetIconForPath("file.zip", true);
                
                // Also load folder icon
                GetIconForPath(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), true);
                
                System.Diagnostics.Debug.WriteLine("Successfully initialized file type icons");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing file type icons: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets the system icon for a file or folder path
        /// </summary>
        /// <param name="path">File or folder path</param>
        /// <param name="isSmall">Whether to return a small (16x16) or large (32x32) icon</param>
        /// <returns>The system icon as a BitmapSource, or null if an error occurs</returns>
        public static BitmapSource? GetIconForPath(string path, bool isSmall = true)
        {
            if (string.IsNullOrEmpty(path))
                return null;
                
            // Use a cache key that combines path and size
            string cacheKey = path + "|" + (isSmall ? "small" : "large");

            // Check if icon is already in cache
            if (_iconCache.TryGetValue(cacheKey, out BitmapSource? cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                // Get appropriate icon from system
                Icon? icon;
                if (Directory.Exists(path))
                {
                    // Get folder icon
                    icon = GetFolderIcon(isSmall);
                }
                else if (File.Exists(path))
                {
                    // Get file icon based on extension from actual file
                    icon = GetFileIcon(path, isSmall);
                }
                else
                {
                    // Get file icon based on extension from dummy file name
                    // This is useful for getting icons for non-existent files
                    // or to pre-cache icons for common file types
                    icon = GetFileIcon(path, isSmall);
                }

                // Convert to BitmapSource for WPF
                BitmapSource? bitmapSource = null;
                if (icon != null)
                {
                    bitmapSource = Imaging.CreateBitmapSourceFromHIcon(
                        icon.Handle,
                        Int32Rect.Empty,
                        BitmapSizeOptions.FromEmptyOptions());
                    
                    // Make the bitmap source freezable for better performance
                    if (bitmapSource?.CanFreeze == true)
                        bitmapSource.Freeze();
                        
                    icon.Dispose();
                }

                // Cache the icon
                _iconCache[cacheKey] = bitmapSource;

                return bitmapSource;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting icon for {path}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the system icon for a folder
        /// </summary>
        /// <returns>The folder icon, or null if an error occurs</returns>
        private static Icon? GetFolderIcon(bool isSmall)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;

            if (isSmall)
                flags |= SHGFI_SMALLICON;
            else
                flags |= SHGFI_LARGEICON;

            SHGetFileInfo(
                "folder",
                FILE_ATTRIBUTE_DIRECTORY,
                ref shfi,
                (uint)Marshal.SizeOf(shfi),
                flags);

            if (shfi.hIcon == IntPtr.Zero)
                return null;

            Icon icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
            DestroyIcon(shfi.hIcon);
            return icon;
        }

        /// <summary>
        /// Gets the system icon for a file based on its extension
        /// </summary>
        /// <returns>The file icon, or null if an error occurs</returns>
        private static Icon? GetFileIcon(string path, bool isSmall)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;

            if (isSmall)
                flags |= SHGFI_SMALLICON;
            else
                flags |= SHGFI_LARGEICON;

            SHGetFileInfo(
                path,
                FILE_ATTRIBUTE_NORMAL,
                ref shfi,
                (uint)Marshal.SizeOf(shfi),
                flags);

            if (shfi.hIcon == IntPtr.Zero)
                return null;

            Icon icon = (Icon)Icon.FromHandle(shfi.hIcon).Clone();
            DestroyIcon(shfi.hIcon);
            return icon;
        }

        /// <summary>
        /// Gets file icon by extension directly (alternative method)
        /// </summary>
        /// <returns>The icon for the specified extension, or null if an error occurs</returns>
        public static BitmapSource? GetIconByExtension(string extension, bool isSmall = true)
        {
            if (string.IsNullOrEmpty(extension))
                return null;
                
            // Ensure extension starts with a dot
            if (!extension.StartsWith("."))
                extension = "." + extension;
                
            string dummyFilePath = "file" + extension;
            return GetIconForPath(dummyFilePath, isSmall);
        }

        /// <summary>
        /// Creates a colored SVG icon from a template SVG file
        /// </summary>
        /// <param name="svgPath">Path to the SVG file</param>
        /// <param name="color">Color to apply to the SVG</param>
        /// <returns>The colored icon as a BitmapSource, or null if an error occurs</returns>
        public static BitmapSource? CreateColoredSvgIcon(string svgPath, System.Windows.Media.Color color)
        {
            if (string.IsNullOrEmpty(svgPath) || !File.Exists(svgPath))
                return null;
                
            // Use a cache key that combines path and color
            string cacheKey = svgPath + "|" + color.ToString();

            // Check if icon is already in cache
            if (_coloredIconCache.TryGetValue(cacheKey, out BitmapSource? cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                // Load SVG content
                string svgContent = File.ReadAllText(svgPath);

                // Replace the fill color in the SVG
                // This assumes the SVG has a 'fill' attribute that we can replace
                // Adjust as needed for your specific SVG templates
                string colorHex = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
                string coloredSvg = svgContent.Replace("fill=\"currentColor\"", $"fill=\"{colorHex}\"");
                
                // Create a temporary file with the colored SVG
                string tempPath = Path.Combine(Path.GetTempPath(), $"colored_icon_{Guid.NewGuid()}.svg");
                File.WriteAllText(tempPath, coloredSvg);

                // Load the colored SVG as a bitmap
                try
                {
                    // Use SVG.NET or other SVG rendering library
                    // For this example, we'll use a placeholder BitmapImage
                    BitmapImage? bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(tempPath);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze();

                    // Cache the icon
                    _coloredIconCache[cacheKey] = bitmap;

                    return bitmap;
                }
                finally
                {
                    // Clean up temporary file
                    try { File.Delete(tempPath); } catch { }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating colored SVG icon: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Clears the icon cache
        /// </summary>
        public static void ClearCache()
        {
            _iconCache.Clear();
            _coloredIconCache.Clear();
        }

        #region Win32 API for icon extraction

        [StructLayout(LayoutKind.Sequential)]
        private struct SHFILEINFO
        {
            public IntPtr hIcon;
            public int iIcon;
            public uint dwAttributes;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
            public string szDisplayName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
            public string szTypeName;
        }

        [DllImport("shell32.dll")]
        private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbSizeFileInfo, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        private const uint SHGFI_ICON = 0x000000100;
        private const uint SHGFI_SMALLICON = 0x000000001;
        private const uint SHGFI_LARGEICON = 0x000000000;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x000000010;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

        #endregion
    }
}