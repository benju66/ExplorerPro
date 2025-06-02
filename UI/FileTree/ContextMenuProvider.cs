// UI/FileTree/ContextMenuProvider.cs - Updated to work with FileOperationHandler

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Commands;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Provides context menu functionality for the FileTreeView
    /// Updated to optionally work with FileOperationHandler for cleaner separation
    /// </summary>
    public class ContextMenuProvider
    {
        private readonly MetadataManager metadataManager;
        private readonly UndoManager undoManager;
        private readonly FileOperationHandler fileOperationHandler;
        private readonly IFileTree fileTree;
        
        /// <summary>
        /// Creates a new ContextMenuProvider with FileOperationHandler support
        /// </summary>
        /// <param name="metadataManager">Metadata manager for tags and colors</param>
        /// <param name="undoManager">Undo manager for operations</param>
        /// <param name="fileOperationHandler">Optional file operation handler for direct operations</param>
        /// <param name="fileTree">Optional file tree reference for operations</param>
        public ContextMenuProvider(
            MetadataManager metadataManager, 
            UndoManager undoManager,
            FileOperationHandler fileOperationHandler = null,
            IFileTree fileTree = null)
        {
            this.metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
            this.undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
            this.fileOperationHandler = fileOperationHandler;
            this.fileTree = fileTree;
        }
        
        /// <summary>
        /// Legacy constructor for backward compatibility
        /// </summary>
        public ContextMenuProvider(MetadataManager metadataManager, UndoManager undoManager)
            : this(metadataManager, undoManager, null, null)
        {
        }

        /// <summary>
        /// Builds a context menu for the given file path
        /// </summary>
        /// <param name="selectedPath">The path of the selected item</param>
        /// <param name="actionHandler">Handler for menu actions (used when FileOperationHandler is not available)</param>
        /// <returns>A configured context menu</returns>
        public ContextMenu BuildContextMenu(string selectedPath, Action<string, string> actionHandler = null)
        {
            var contextMenu = new ContextMenu();
            bool isFolder = Directory.Exists(selectedPath);
            string iconPath = "Assets/Icons";

            // File & Folder Actions
            AddMenuItem(contextMenu, "Open", iconPath + "/folder-open.png", 
                () => HandleAction("open", selectedPath, actionHandler));
            
            AddMenuItem(contextMenu, "Show Metadata", iconPath + "/info.png", 
                () => HandleAction("show_metadata", selectedPath, actionHandler));
            
            AddMenuItem(contextMenu, "Show in File Explorer", iconPath + "/folder.png", 
                () => HandleAction("show_in_explorer", selectedPath, actionHandler));

            contextMenu.Items.Add(new Separator());

            // Editing Actions - Use FileOperationHandler when available
            if (fileOperationHandler != null && fileTree != null)
            {
                AddMenuItem(contextMenu, "Rename", iconPath + "/folder-pen.png", 
                    () => HandleRename(selectedPath));
                
                AddMenuItem(contextMenu, "Delete", iconPath + "/delete.png", 
                    () => HandleDelete(selectedPath));
            }
            else
            {
                AddMenuItem(contextMenu, "Rename", iconPath + "/folder-pen.png", 
                    () => HandleAction("rename", selectedPath, actionHandler));
                
                AddMenuItem(contextMenu, "Delete", iconPath + "/delete.png", 
                    () => HandleAction("delete", selectedPath, actionHandler));
            }

            contextMenu.Items.Add(new Separator());

            // Pin & Tag - These use MetadataManager directly
            bool isPinned = metadataManager.GetPinnedItems().Contains(selectedPath);
            AddMenuItem(contextMenu, isPinned ? "Unpin Item" : "Pin Item", iconPath + "/pin.png", 
                () => HandlePinToggle(selectedPath, actionHandler));
            
            AddMenuItem(contextMenu, "Add Tag", iconPath + "/tag.png", 
                () => HandleAction("add_tag", selectedPath, actionHandler));
            
            var tags = metadataManager.GetTags(selectedPath);
            if (tags.Count > 0)
            {
                AddMenuItem(contextMenu, "Remove Tag", string.Empty, 
                    () => HandleAction("remove_tag", selectedPath, actionHandler));
            }

            contextMenu.Items.Add(new Separator());

            // Copy/Paste - Use FileOperationHandler when available
            if (fileOperationHandler != null)
            {
                AddMenuItem(contextMenu, "Copy", iconPath + "/copy.png", 
                    () => HandleCopy(selectedPath));
                
                AddMenuItem(contextMenu, "Paste", iconPath + "/clipboard-paste.png", 
                    () => HandlePaste(selectedPath));
                
                AddMenuItem(contextMenu, "Duplicate", iconPath + "/copy-plus.png", 
                    () => HandleDuplicate(selectedPath));
            }
            else
            {
                AddMenuItem(contextMenu, "Copy", iconPath + "/copy.png", 
                    () => HandleAction("copy", selectedPath, actionHandler));
                
                AddMenuItem(contextMenu, "Paste", iconPath + "/clipboard-paste.png", 
                    () => HandleAction("paste", selectedPath, actionHandler));
                
                AddMenuItem(contextMenu, "Duplicate", iconPath + "/copy-plus.png", 
                    () => HandleAction("duplicate", selectedPath, actionHandler));
            }

            contextMenu.Items.Add(new Separator());

            // File/Folder Creation - Use FileOperationHandler when available
            if (fileOperationHandler != null && fileTree != null && isFolder)
            {
                AddMenuItem(contextMenu, "Add New File", iconPath + "/file-plus.png", 
                    () => fileOperationHandler.CreateNewFile(selectedPath, fileTree));
                
                AddMenuItem(contextMenu, "Add New Folder", iconPath + "/folder-plus.png", 
                    () => fileOperationHandler.CreateNewFolder(selectedPath, fileTree));
            }
            else
            {
                AddMenuItem(contextMenu, "Add New File", iconPath + "/file-plus.png", 
                    () => HandleAction("new_file", selectedPath, actionHandler));
                
                AddMenuItem(contextMenu, "Add New Folder", iconPath + "/folder-plus.png", 
                    () => HandleAction("new_folder", selectedPath, actionHandler));
            }

            contextMenu.Items.Add(new Separator());

            // Tree Navigation
            AddMenuItem(contextMenu, "Collapse All", iconPath + "/list-collapse.png", 
                () => HandleAction("collapse_all", selectedPath, actionHandler));
            
            AddMenuItem(contextMenu, "Expand All", iconPath + "/expand.png", 
                () => HandleAction("expand_all", selectedPath, actionHandler));

            // Folder-specific actions
            if (isFolder)
            {
                contextMenu.Items.Add(new Separator());

                AddMenuItem(contextMenu, "Open in New Tab", iconPath + "/app-window.png", 
                    () => HandleAction("open_in_new_tab", selectedPath, actionHandler));
                
                AddMenuItem(contextMenu, "Open in New Window", iconPath + "/app-window.png", 
                    () => HandleAction("open_in_new_window", selectedPath, actionHandler));
                
                AddMenuItem(contextMenu, "Toggle Split View", iconPath + "/square-split-horizontal.png", 
                    () => HandleAction("toggle_split_view", selectedPath, actionHandler));
            }

            // File-specific actions
            if (File.Exists(selectedPath))
            {
                string extension = Path.GetExtension(selectedPath).ToLower();
                
                if (extension == ".pdf")
                {
                    AddMenuItem(contextMenu, "Preview PDF", iconPath + "/pdf-preview.png", 
                        () => HandleAction("preview_pdf", selectedPath, actionHandler));
                }
                
                List<string> supportedImages = new List<string> { ".jpg", ".jpeg", ".png", ".svg", ".heic", ".gif", ".bmp" };
                if (supportedImages.Contains(extension))
                {
                    AddMenuItem(contextMenu, "Preview Image", iconPath + "/image-preview.png", 
                        () => HandleAction("preview_image", selectedPath, actionHandler));
                }
            }

            // Text Color
            AddMenuItem(contextMenu, "Change Text Color", string.Empty, 
                () => HandleAction("change_text_color", selectedPath, actionHandler));

            return contextMenu;
        }

        /// <summary>
        /// Builds a context menu for multiple selected items
        /// </summary>
        /// <param name="selectedPaths">Collection of selected paths</param>
        /// <param name="actionHandler">Handler for menu actions</param>
        /// <returns>A configured context menu</returns>
        public ContextMenu BuildMultiSelectContextMenu(IReadOnlyList<string> selectedPaths, Action<string, string> actionHandler = null)
        {
            var contextMenu = new ContextMenu();
            string iconPath = "Assets/Icons";

            // Multi-select operations
            if (fileOperationHandler != null && fileTree != null)
            {
                AddMenuItem(contextMenu, $"Delete {selectedPaths.Count} items", iconPath + "/delete.png",
                    () => fileOperationHandler.DeleteMultipleItemsAsync(selectedPaths, fileTree).ConfigureAwait(false));
                
                AddMenuItem(contextMenu, $"Copy {selectedPaths.Count} items", iconPath + "/copy.png",
                    () => fileOperationHandler.CopyMultipleItems(selectedPaths));
            }
            else
            {
                AddMenuItem(contextMenu, $"Delete {selectedPaths.Count} items", iconPath + "/delete.png",
                    () => HandleAction("delete_multiple", string.Join("|", selectedPaths), actionHandler));
                
                AddMenuItem(contextMenu, $"Copy {selectedPaths.Count} items", iconPath + "/copy.png",
                    () => HandleAction("copy_multiple", string.Join("|", selectedPaths), actionHandler));
            }
            
            contextMenu.Items.Add(new Separator());
            
            // Selection operations
            AddMenuItem(contextMenu, "Select by Pattern...", iconPath + "/filter.png",
                () => HandleAction("select_by_pattern", "", actionHandler));
            
            AddMenuItem(contextMenu, "Invert Selection", iconPath + "/selection-inverse.png",
                () => HandleAction("invert_selection", "", actionHandler));
            
            contextMenu.Items.Add(new Separator());
            
            AddMenuItem(contextMenu, "Clear Selection", iconPath + "/selection-none.png",
                () => HandleAction("clear_selection", "", actionHandler));
            
            return contextMenu;
        }

        #region Private Helper Methods

        private void AddMenuItem(ContextMenu menu, string header, string iconPath, Action clickHandler)
        {
            var menuItem = new MenuItem { Header = header };
            
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    var image = new Image
                    {
                        Source = new BitmapImage(new Uri(iconPath, UriKind.Relative)),
                        Width = 16,
                        Height = 16
                    };
                    menuItem.Icon = image;
                }
                catch
                {
                    // Ignore icon loading errors
                }
            }
            
            menuItem.Click += (s, e) => clickHandler();
            menu.Items.Add(menuItem);
        }

        private void HandleAction(string action, string path, Action<string, string> actionHandler)
        {
            actionHandler?.Invoke(action, path);
        }

        private void HandleRename(string path)
        {
            string currentName = Path.GetFileName(path);
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new name:", 
                "Rename", 
                currentName);
                
            if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
            {
                fileOperationHandler.RenameItem(path, newName, fileTree);
            }
        }

        private void HandleDelete(string path)
        {
            fileOperationHandler.DeleteItem(path, fileTree);
        }

        private void HandleCopy(string path)
        {
            fileOperationHandler.CopyItem(path);
        }

        private void HandlePaste(string path)
        {
            string targetPath = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(targetPath))
            {
                fileOperationHandler.PasteItemsAsync(targetPath).ConfigureAwait(false);
            }
        }

        private void HandleDuplicate(string path)
        {
            // Copy the item
            fileOperationHandler.CopyItem(path);
            
            // Then paste it in the same directory
            string targetPath = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(targetPath))
            {
                fileOperationHandler.PasteItemsAsync(targetPath).ConfigureAwait(false);
            }
        }

        private void HandlePinToggle(string path, Action<string, string> actionHandler)
        {
            var pinnedItems = metadataManager.GetPinnedItems();
            if (pinnedItems.Contains(path))
            {
                metadataManager.RemovePinnedItem(path);
                actionHandler?.Invoke("unpin", path);
            }
            else
            {
                metadataManager.AddPinnedItem(path);
                actionHandler?.Invoke("pin", path);
            }
        }

        #endregion
    }
}