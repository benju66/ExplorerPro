using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.FileOperations
{
    /// <summary>
    /// Provides file and folder icons from the system shell.
    /// </summary>
    public class FileIconProvider
    {
        #region Fields

        private readonly ILogger<FileIconProvider> _logger;
        private readonly bool _useCache;
        private readonly System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource> _iconCache;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the FileIconProvider class.
        /// </summary>
        /// <param name="useCache">Whether to cache icons for better performance.</param>
        /// <param name="logger">Optional logger for operation tracking.</param>
        public FileIconProvider(bool useCache = true, ILogger<FileIconProvider> logger = null)
        {
            _logger = logger;
            _useCache = useCache;

            if (_useCache)
            {
                _iconCache = new System.Collections.Concurrent.ConcurrentDictionary<string, ImageSource>();
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Gets the system icon for a file or folder.
        /// </summary>
        /// <param name="path">The path to the file or folder.</param>
        /// <param name="isSmall">Whether to get the small (16x16) or large (32x32) icon.</param>
        /// <returns>The icon as an ImageSource, or null if not found.</returns>
        public ImageSource GetIcon(string path, bool isSmall = true)
        {
            // Check if path exists
            if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                return GetDefaultIcon(path, isSmall);
            }

            // Check cache first if enabled
            string cacheKey = $"{path}_{isSmall}";
            if (_useCache && _iconCache != null && _iconCache.TryGetValue(cacheKey, out ImageSource cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                ImageSource icon = GetShellIcon(path, isSmall);
                
                // Cache the icon if caching is enabled
                if (_useCache && _iconCache != null && icon != null)
                {
                    _iconCache[cacheKey] = icon;
                }
                
                return icon;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error getting icon for {path}");
                return GetDefaultIcon(path, isSmall);
            }
        }

        /// <summary>
        /// Gets the system icon for a file extension.
        /// </summary>
        /// <param name="extension">The file extension (including the dot, e.g., ".txt").</param>
        /// <param name="isSmall">Whether to get the small (16x16) or large (32x32) icon.</param>
        /// <returns>The icon as an ImageSource, or null if not found.</returns>
        public ImageSource GetIconByExtension(string extension, bool isSmall = true)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return GetDefaultIcon(string.Empty, isSmall);
            }

            // Ensure extension starts with a dot
            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }

            // Check cache first if enabled
            string cacheKey = $"ext_{extension}_{isSmall}";
            if (_useCache && _iconCache != null && _iconCache.TryGetValue(cacheKey, out ImageSource cachedIcon))
            {
                return cachedIcon;
            }

            try
            {
                // Create a temporary file with this extension to get its icon
                string tempDir = Path.GetTempPath();
                string tempFile = Path.Combine(tempDir, $"temp{extension}");
                
                // Check if the temp file already exists
                if (!File.Exists(tempFile))
                {
                    // Create an empty file with this extension
                    using (File.Create(tempFile)) { }
                }

                // Get the icon for this file
                ImageSource icon = GetShellIcon(tempFile, isSmall);
                
                // Cache the icon if caching is enabled
                if (_useCache && _iconCache != null && icon != null)
                {
                    _iconCache[cacheKey] = icon;
                }
                
                // Delete the temporary file
                try
                {
                    if (File.Exists(tempFile))
                    {
                        File.Delete(tempFile);
                    }
                }
                catch { /* Ignore cleanup errors */ }
                
                return icon;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error getting icon for extension {extension}");
                return GetDefaultIcon(extension, isSmall);
            }
        }

        /// <summary>
        /// Clears the icon cache.
        /// </summary>
        public void ClearCache()
        {
            if (_useCache && _iconCache != null)
            {
                _iconCache.Clear();
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Gets a system shell icon for a file or folder using Win32 API.
        /// </summary>
        /// <param name="path">The path to the file or folder.</param>
        /// <param name="isSmall">Whether to get the small (16x16) or large (32x32) icon.</param>
        /// <returns>The icon as an ImageSource, or null if not found.</returns>
        private ImageSource GetShellIcon(string path, bool isSmall)
        {
            SHFILEINFO shfi = new SHFILEINFO();
            uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
            
            if (isSmall)
                flags |= SHGFI_SMALLICON;
            else
                flags |= SHGFI_LARGEICON;

            IntPtr hSuccess = SHGetFileInfo(
                path,
                Directory.Exists(path) ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL,
                ref shfi,
                (uint)Marshal.SizeOf(shfi),
                flags);

            if (hSuccess == IntPtr.Zero)
                return null;

            try
            {
                // Get system icon
                Icon icon = Icon.FromHandle(shfi.hIcon);
                
                // Convert to WPF ImageSource
                ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                
                // Cleanup
                DestroyIcon(shfi.hIcon);
                
                return imageSource;
            }
            catch
            {
                // Ensure we destroy the icon handle even if conversion fails
                if (shfi.hIcon != IntPtr.Zero)
                {
                    DestroyIcon(shfi.hIcon);
                }
                throw;
            }
        }

        /// <summary>
        /// Gets a default icon for a file or folder when the actual icon can't be retrieved.
        /// </summary>
        /// <param name="path">The path to the file or folder (for determining if it's a directory).</param>
        /// <param name="isSmall">Whether to get the small (16x16) or large (32x32) icon.</param>
        /// <returns>A default icon as an ImageSource.</returns>
        private ImageSource GetDefaultIcon(string path, bool isSmall)
        {
            try
            {
                bool isDirectory = !string.IsNullOrEmpty(path) && Directory.Exists(path);
                
                // Get generic folder or file icon
                SHFILEINFO shfi = new SHFILEINFO();
                uint flags = SHGFI_ICON | SHGFI_USEFILEATTRIBUTES;
                
                if (isSmall)
                    flags |= SHGFI_SMALLICON;
                else
                    flags |= SHGFI_LARGEICON;

                SHGetFileInfo(
                    isDirectory ? "folder" : "file",
                    isDirectory ? FILE_ATTRIBUTE_DIRECTORY : FILE_ATTRIBUTE_NORMAL,
                    ref shfi,
                    (uint)Marshal.SizeOf(shfi),
                    flags);

                if (shfi.hIcon == IntPtr.Zero)
                    return null;

                // Convert to WPF ImageSource
                Icon icon = Icon.FromHandle(shfi.hIcon);
                ImageSource imageSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                
                // Cleanup
                DestroyIcon(shfi.hIcon);
                
                return imageSource;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error getting default icon");
                return null;
            }
        }

        #endregion

        #region Win32 API Declarations

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

        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        private const uint FILE_ATTRIBUTE_DIRECTORY = 0x10;
        private const uint SHGFI_ICON = 0x100;
        private const uint SHGFI_SMALLICON = 0x1;
        private const uint SHGFI_LARGEICON = 0x0;
        private const uint SHGFI_USEFILEATTRIBUTES = 0x10;

        #endregion
    }
}