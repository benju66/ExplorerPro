// UI/FileTree/CustomFileSystemModel.cs - Fixed version with IDisposable

using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Custom file system model that provides additional functionality beyond the standard
    /// .NET file system capabilities, including support for custom styling and handling rename operations.
    /// Fixed version with proper disposal pattern to prevent memory leaks.
    /// </summary>
    public class CustomFileSystemModel : IDisposable
    {
        private readonly MetadataManager metadataManager;
        private readonly UndoManager undoManager;
        private readonly IFileOperations fileOperations;
        private readonly ILogger<CustomFileSystemModel> logger;
        private bool _disposed;

        public CustomFileSystemModel(MetadataManager metadataManager, UndoManager undoManager, IFileOperations fileOperations, ILogger<CustomFileSystemModel>? logger = null)
        {
            this.metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
            this.undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
            this.fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            this.logger = logger;
        }

        /// <summary>
        /// Gets the text color for a file or folder based on metadata settings
        /// </summary>
        public Brush GetItemForeground(string path)
        {
            ThrowIfDisposed();
            
            string colorHex = metadataManager.GetItemColor(path);
            if (!string.IsNullOrEmpty(colorHex))
            {
                try
                {
                    return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorHex));
                }
                catch
                {
                    return SystemColors.WindowTextBrush;
                }
            }
            return SystemColors.WindowTextBrush;
        }

        /// <summary>
        /// Gets whether a file or folder should be displayed in bold text
        /// </summary>
        public FontWeight GetItemFontWeight(string path)
        {
            ThrowIfDisposed();
            
            return metadataManager.GetItemBold(path) ? FontWeights.Bold : FontWeights.Normal;
        }

        /// <summary>
        /// Handles rename operations (temporarily without undo support)
        /// </summary>
        public bool RenameItem(string oldPath, string newName)
        {
            ThrowIfDisposed();
            
            if (string.IsNullOrWhiteSpace(newName) || newName == Path.GetFileName(oldPath))
                return false;

            try
            {
                string parentDir = Path.GetDirectoryName(oldPath) ?? string.Empty;
                string newPath = Path.Combine(parentDir, newName);

                // Check if destination already exists
                if (File.Exists(newPath) || Directory.Exists(newPath))
                {
                    MessageBox.Show($"Cannot rename: {newName} already exists.", 
                        "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }

                // Perform rename directly (without command pattern for now)
                string resultPath = fileOperations.RenameItem(oldPath, newName);
                
                if (string.IsNullOrEmpty(resultPath))
                {
                    MessageBox.Show($"Failed to rename item.", 
                        "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return false;
                }
                
                // Update metadata references
                metadataManager.UpdatePathReferences(oldPath, resultPath);
                
                logger?.LogInformation($"Renamed '{oldPath}' to '{resultPath}'");
                return true;
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, $"Failed to rename: {ex.Message}");
                MessageBox.Show($"Failed to rename: {ex.Message}", 
                    "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        /// <summary>
        /// Clears cached data and prepares for disposal
        /// </summary>
        public void ClearCache()
        {
            if (_disposed) return;
            
            // Currently no caching in this class, but this method
            // is here for future use and consistency with other models
            logger?.LogDebug("CustomFileSystemModel cache cleared");
        }

        /// <summary>
        /// Throws if the object has been disposed
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CustomFileSystemModel));
            }
        }

        #region IDisposable Implementation

        /// <summary>
        /// Disposes resources used by the CustomFileSystemModel
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Protected implementation of Dispose pattern
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    logger?.LogDebug("Disposing CustomFileSystemModel");
                    
                    // Clear any caches
                    ClearCache();
                    
                    // Note: We don't dispose the injected dependencies (metadataManager, undoManager, fileOperations)
                    // as they are managed externally and may be used by other components
                    
                    logger?.LogDebug("CustomFileSystemModel disposed");
                }
                
                _disposed = true;
            }
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~CustomFileSystemModel()
        {
            Dispose(false);
        }

        #endregion
    }
}