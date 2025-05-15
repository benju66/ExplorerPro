// UI/FileTree/ContextMenuProvider.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using ExplorerPro.Models;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Provides context menu functionality for the FileTreeView
    /// </summary>
    public class ContextMenuProvider
    {
        private readonly MetadataManager metadataManager;
        private readonly UndoManager undoManager;
        
        public ContextMenuProvider(MetadataManager metadataManager, UndoManager undoManager)
        {
            this.metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
            this.undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
        }

        /// <summary>
        /// Builds a context menu for the given file path
        /// </summary>
        public ContextMenu BuildContextMenu(string selectedPath, Action<string, string> actionHandler)
        {
            var contextMenu = new ContextMenu();
            bool isFolder = Directory.Exists(selectedPath);
            string iconPath = "Assets/Icons";

            // File & Folder Actions
            AddMenuItem(contextMenu, "Open", iconPath + "/folder-open.png", 
                () => actionHandler("open", selectedPath));
            
            AddMenuItem(contextMenu, "Show Metadata", iconPath + "/info.png", 
                () => actionHandler("show_metadata", selectedPath));
            
            AddMenuItem(contextMenu, "Show in File Explorer", iconPath + "/folder.png", 
                () => actionHandler("show_in_explorer", selectedPath));

            contextMenu.Items.Add(new Separator());

            // Editing Actions
            AddMenuItem(contextMenu, "Rename", iconPath + "/folder-pen.png", 
                () => actionHandler("rename", selectedPath));
            
            AddMenuItem(contextMenu, "Delete", iconPath + "/delete.png", 
                () => actionHandler("delete", selectedPath));

            contextMenu.Items.Add(new Separator());

            // Pin & Tag
            AddMenuItem(contextMenu, "Pin Item", iconPath + "/pin.png", 
                () => actionHandler("pin", selectedPath));
            
            AddMenuItem(contextMenu, "Add Tag", iconPath + "/tag.png", 
                () => actionHandler("add_tag", selectedPath));
            
            // For null safety, use empty string instead of null
            AddMenuItem(contextMenu, "Remove Tag", string.Empty, 
                () => actionHandler("remove_tag", selectedPath));

            contextMenu.Items.Add(new Separator());

            // Copy/Paste
            AddMenuItem(contextMenu, "Copy", iconPath + "/copy.png", 
                () => actionHandler("copy", selectedPath));
            
            AddMenuItem(contextMenu, "Paste", iconPath + "/clipboard-paste.png", 
                () => actionHandler("paste", selectedPath));
            
            AddMenuItem(contextMenu, "Duplicate", iconPath + "/copy-plus.png", 
                () => actionHandler("duplicate", selectedPath));

            contextMenu.Items.Add(new Separator());

            // File/Folder Creation
            AddMenuItem(contextMenu, "Add New File", iconPath + "/file-plus.png", 
                () => actionHandler("new_file", selectedPath));
            
            AddMenuItem(contextMenu, "Add New Folder", iconPath + "/folder-plus.png", 
                () => actionHandler("new_folder", selectedPath));

            contextMenu.Items.Add(new Separator());

            // Tree Navigation
            AddMenuItem(contextMenu, "Collapse All", iconPath + "/list-collapse.png", 
                () => actionHandler("collapse_all", selectedPath));
            
            AddMenuItem(contextMenu, "Expand All", iconPath + "/expand.png", 
                () => actionHandler("expand_all", selectedPath));

            // Folder-specific actions
            if (isFolder)
            {
                contextMenu.Items.Add(new Separator());

                AddMenuItem(contextMenu, "Open in New Tab", iconPath + "/app-window.png", 
                    () => actionHandler("open_in_new_tab", selectedPath));
                
                AddMenuItem(contextMenu, "Open in New Window", iconPath + "/app-window.png", 
                    () => actionHandler("open_in_new_window", selectedPath));
                
                AddMenuItem(contextMenu, "Toggle Split View", iconPath + "/square-split-horizontal.png", 
                    () => actionHandler("toggle_split_view", selectedPath));
            }

            // File-specific actions
            if (File.Exists(selectedPath))
            {
                string extension = Path.GetExtension(selectedPath).ToLower();
                
                if (extension == ".pdf")
                {
                    AddMenuItem(contextMenu, "Preview PDF", iconPath + "/pdf-preview.png", 
                        () => actionHandler("preview_pdf", selectedPath));
                }
                
                List<string> supportedImages = new List<string> { ".jpg", ".jpeg", ".png", ".svg", ".heic", ".gif", ".bmp" };
                if (supportedImages.Contains(extension))
                {
                    AddMenuItem(contextMenu, "Preview Image", iconPath + "/image-preview.png", 
                        () => actionHandler("preview_image", selectedPath));
                }
            }

            // Text Color
            // For null safety, use empty string instead of null
            AddMenuItem(contextMenu, "Change Text Color", string.Empty, 
                () => actionHandler("change_text_color", selectedPath));

            return contextMenu;
        }

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
    }
}