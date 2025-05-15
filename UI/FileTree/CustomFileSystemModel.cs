// UI/FileTree/CustomFileSystemModel.cs

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
    /// </summary>
    public class CustomFileSystemModel
    {
        private readonly MetadataManager metadataManager;
        private readonly UndoManager undoManager;
        private readonly IFileOperations fileOperations;
        private readonly ILogger<CustomFileSystemModel> logger;

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
            return metadataManager.GetItemBold(path) ? FontWeights.Bold : FontWeights.Normal;
        }

        /// <summary>
        /// Handles rename operations with undo support
        /// </summary>
        public bool RenameItem(string oldPath, string newName)
        {
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

                // Create and execute a rename command - pass null for logger to avoid type conflict
                var command = new RenameCommand(fileOperations, oldPath, newName, null);
                undoManager.ExecuteCommand(command);
                
                return command.NewPath != null;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to rename: {ex.Message}", 
                    "Rename Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}