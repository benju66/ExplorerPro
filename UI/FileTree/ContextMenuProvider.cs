// UI/FileTree/ContextMenuProvider.cs - Refactored version
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree.Commands;
using ExplorerPro.UI.Dialogs;
using Microsoft.Win32;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Provides comprehensive context menu functionality for the FileTreeView
    /// </summary>
    public class ContextMenuProvider
    {
        private readonly MetadataManager _metadataManager;
        private readonly UndoManager _undoManager;
        private readonly FileOperationHandler _fileOperationHandler;
        private readonly IFileTree _fileTree;
        
        public ContextMenuProvider(
            MetadataManager metadataManager, 
            UndoManager undoManager,
            FileOperationHandler fileOperationHandler,
            IFileTree fileTree)
        {
            _metadataManager = metadataManager ?? throw new ArgumentNullException(nameof(metadataManager));
            _undoManager = undoManager ?? throw new ArgumentNullException(nameof(undoManager));
            _fileOperationHandler = fileOperationHandler ?? throw new ArgumentNullException(nameof(fileOperationHandler));
            _fileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
        }

        /// <summary>
        /// Builds a comprehensive context menu for single item selection
        /// </summary>
        public ContextMenu BuildContextMenu(string selectedPath)
        {
            var contextMenu = new ContextMenu();
            bool isDirectory = Directory.Exists(selectedPath);
            string iconPath = "Assets/Icons/";

            // Open operations
            AddMenuItem(contextMenu, "_Open", iconPath + "folder-open.png", 
                () => OpenItem(selectedPath), Key.Enter);
            
            if (!isDirectory)
            {
                AddMenuItem(contextMenu, "Open _with...", iconPath + "app-window.png", 
                    () => OpenWith(selectedPath));
                    
                // Add "Open with" submenu for common programs
                var openWithMenu = AddSubMenu(contextMenu, "Open with", iconPath + "apps.png");
                AddOpenWithOptions(openWithMenu, selectedPath);
            }
            
            if (isDirectory)
            {
                AddMenuItem(contextMenu, "Open in New _Tab", iconPath + "tab-plus.png", 
                    () => OpenInNewTab(selectedPath), Key.T, ModifierKeys.Control);
                    
                AddMenuItem(contextMenu, "Open in New _Window", iconPath + "window-plus.png", 
                    () => OpenInNewWindow(selectedPath), Key.N, ModifierKeys.Control | ModifierKeys.Shift);
                    
                AddMenuItem(contextMenu, "Open in Windows Explorer", iconPath + "folder.png", 
                    () => ShowInExplorer(selectedPath));
            }

            contextMenu.Items.Add(new Separator());

            // Clipboard operations
            AddMenuItem(contextMenu, "Cu_t", iconPath + "cut.png", 
                () => CutItem(selectedPath), Key.X, ModifierKeys.Control);
                
            AddMenuItem(contextMenu, "_Copy", iconPath + "copy.png", 
                () => CopyItem(selectedPath), Key.C, ModifierKeys.Control);
                
            AddMenuItem(contextMenu, "_Paste", iconPath + "paste.png", 
                () => PasteItems(selectedPath), Key.V, ModifierKeys.Control,
                isEnabled: Clipboard.ContainsFileDropList());
                
            AddMenuItem(contextMenu, "_Duplicate", iconPath + "copy-plus.png", 
                () => DuplicateItem(selectedPath), Key.D, ModifierKeys.Control);

            contextMenu.Items.Add(new Separator());

            // Create operations
            if (isDirectory)
            {
                var newMenu = AddSubMenu(contextMenu, "_New", iconPath + "plus.png");
                
                AddMenuItem(newMenu, "_Folder", iconPath + "folder-plus.png", 
                    () => _fileOperationHandler.CreateNewFolder(selectedPath, _fileTree));
                    
                AddMenuItem(newMenu, "_Text Document", iconPath + "file-text.png", 
                    () => _fileOperationHandler.CreateNewFile(selectedPath, _fileTree, "New Text Document.txt"));
                    
                newMenu.Items.Add(new Separator());
                
                // Add more file types
                AddMenuItem(newMenu, "_Word Document", iconPath + "file-word.png", 
                    () => _fileOperationHandler.CreateNewFile(selectedPath, _fileTree, "New Document.docx"));
                    
                AddMenuItem(newMenu, "_Excel Workbook", iconPath + "file-excel.png", 
                    () => _fileOperationHandler.CreateNewFile(selectedPath, _fileTree, "New Workbook.xlsx"));
                    
                AddMenuItem(newMenu, "_PowerPoint Presentation", iconPath + "file-powerpoint.png", 
                    () => _fileOperationHandler.CreateNewFile(selectedPath, _fileTree, "New Presentation.pptx"));
            }

            contextMenu.Items.Add(new Separator());

            // File operations
            AddMenuItem(contextMenu, "_Delete", iconPath + "delete.png", 
                () => _fileOperationHandler.DeleteItem(selectedPath, _fileTree), Key.Delete);
                
            AddMenuItem(contextMenu, "Rena_me", iconPath + "edit.png", 
                () => RenameItem(selectedPath), Key.F2);

            contextMenu.Items.Add(new Separator());

            // Organization
            var organizeMenu = AddSubMenu(contextMenu, "Organize", iconPath + "folder-cog.png");
            
            bool isPinned = _metadataManager.GetPinnedItems().Contains(selectedPath);
            AddMenuItem(organizeMenu, isPinned ? "Unpin from Quick Access" : "Pin to Quick Access", 
                iconPath + "pin.png", () => TogglePin(selectedPath));
            
            organizeMenu.Items.Add(new Separator());
            
            AddMenuItem(organizeMenu, "Add _Tag...", iconPath + "tag.png", 
                () => ShowAddTagDialog(selectedPath));
            
            var tags = _metadataManager.GetTags(selectedPath);
            if (tags.Count > 0)
            {
                var removeTagMenu = AddSubMenu(organizeMenu, "Remove Tag", iconPath + "tag-remove.png");
                foreach (var tag in tags)
                {
                    AddMenuItem(removeTagMenu, tag, null, () => RemoveTag(selectedPath, tag));
                }
            }
            
            organizeMenu.Items.Add(new Separator());
            
            var colorMenu = AddSubMenu(organizeMenu, "Set Color", iconPath + "palette.png");
            AddColorOptions(colorMenu, selectedPath);
            
            // Archive operations (if applicable)
            string extension = Path.GetExtension(selectedPath).ToLower();
            if (IsArchive(extension))
            {
                contextMenu.Items.Add(new Separator());
                AddMenuItem(contextMenu, "E_xtract Here", iconPath + "archive-extract.png", 
                    () => ExtractArchive(selectedPath, Path.GetDirectoryName(selectedPath)));
                    
                AddMenuItem(contextMenu, "Extract to...", iconPath + "archive-extract.png", 
                    () => ExtractArchiveTo(selectedPath));
            }
            else if (!isDirectory || Directory.GetFiles(selectedPath).Any() || Directory.GetDirectories(selectedPath).Any())
            {
                contextMenu.Items.Add(new Separator());
                AddMenuItem(contextMenu, "Add to archive...", iconPath + "archive-add.png", 
                    () => CompressToArchive(selectedPath));
            }

            // Send to submenu
            contextMenu.Items.Add(new Separator());
            var sendToMenu = AddSubMenu(contextMenu, "Send to", iconPath + "send.png");
            AddSendToOptions(sendToMenu, selectedPath);

            contextMenu.Items.Add(new Separator());

            // Properties
            AddMenuItem(contextMenu, "P_roperties", iconPath + "info.png", 
                () => ShowProperties(selectedPath), Key.Enter, ModifierKeys.Alt);

            return contextMenu;
        }

        /// <summary>
        /// Builds context menu for multiple selection
        /// </summary>
        public ContextMenu BuildMultiSelectContextMenu(IReadOnlyList<string> selectedPaths)
        {
            var contextMenu = new ContextMenu();
            string iconPath = "Assets/Icons/";
            
            bool allDirectories = selectedPaths.All(p => Directory.Exists(p));
            bool allFiles = selectedPaths.All(p => File.Exists(p));
            bool hasArchives = selectedPaths.Any(p => File.Exists(p) && IsArchive(Path.GetExtension(p)));

            // Open operations
            if (selectedPaths.Count <= 10) // Limit to prevent too many windows
            {
                AddMenuItem(contextMenu, "_Open All", iconPath + "folder-open.png", 
                    () => OpenMultipleItems(selectedPaths));
            }

            contextMenu.Items.Add(new Separator());

            // Clipboard operations
            AddMenuItem(contextMenu, "Cu_t", iconPath + "cut.png", 
                () => CutMultipleItems(selectedPaths), Key.X, ModifierKeys.Control);
                
            AddMenuItem(contextMenu, "_Copy", iconPath + "copy.png", 
                () => _fileOperationHandler.CopyMultipleItems(selectedPaths), Key.C, ModifierKeys.Control);

            contextMenu.Items.Add(new Separator());

            // Delete
            AddMenuItem(contextMenu, $"_Delete ({selectedPaths.Count} items)", iconPath + "delete.png", 
                () => _fileOperationHandler.DeleteMultipleItemsAsync(selectedPaths, _fileTree).ConfigureAwait(false), 
                Key.Delete);

            contextMenu.Items.Add(new Separator());

            // Organization
            var organizeMenu = AddSubMenu(contextMenu, "Organize", iconPath + "folder-cog.png");
            
            AddMenuItem(organizeMenu, "Add _Tag to All...", iconPath + "tag.png", 
                () => ShowAddTagDialogForMultiple(selectedPaths));
                
            var colorMenu = AddSubMenu(organizeMenu, "Set Color for All", iconPath + "palette.png");
            AddColorOptionsForMultiple(colorMenu, selectedPaths);

            // Archive operations
            if (hasArchives)
            {
                contextMenu.Items.Add(new Separator());
                AddMenuItem(contextMenu, "E_xtract All Here", iconPath + "archive-extract.png", 
                    () => ExtractMultipleArchives(selectedPaths));
            }
            
            contextMenu.Items.Add(new Separator());
            AddMenuItem(contextMenu, "Add to archive...", iconPath + "archive-add.png", 
                () => CompressMultipleToArchive(selectedPaths));

            // Selection operations
            contextMenu.Items.Add(new Separator());
            
            AddMenuItem(contextMenu, "Select _All", iconPath + "select-all.png", 
                () => _fileTree.SelectAll(), Key.A, ModifierKeys.Control);
                
            AddMenuItem(contextMenu, "_Invert Selection", iconPath + "select-inverse.png", 
                () => _fileTree.InvertSelection(), Key.I, ModifierKeys.Control);
                
            AddMenuItem(contextMenu, "Select by Pattern...", iconPath + "filter.png", 
                () => ShowSelectByPatternDialog(), Key.P, ModifierKeys.Control);

            contextMenu.Items.Add(new Separator());

            // Properties
            AddMenuItem(contextMenu, $"P_roperties ({selectedPaths.Count} items)", iconPath + "info.png", 
                () => ShowMultipleProperties(selectedPaths), Key.Enter, ModifierKeys.Alt);

            return contextMenu;
        }

        #region Menu Item Helpers

        private MenuItem AddMenuItem(ItemsControl parent, string header, string iconPath, 
            Action clickHandler, Key? gestureKey = null, ModifierKeys? gestureModifiers = null, 
            bool isEnabled = true)
        {
            var menuItem = new MenuItem 
            { 
                Header = header,
                IsEnabled = isEnabled
            };
            
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    var image = new Image
                    {
                        Source = new BitmapImage(new Uri(Path.GetFullPath(iconPath))),
                        Width = 16,
                        Height = 16
                    };
                    menuItem.Icon = image;
                }
                catch { }
            }
            
            if (gestureKey.HasValue)
            {
                var gesture = gestureModifiers.HasValue 
                    ? new KeyGesture(gestureKey.Value, gestureModifiers.Value)
                    : new KeyGesture(gestureKey.Value);
                menuItem.InputGestureText = gesture.GetDisplayStringForCulture(
                    System.Globalization.CultureInfo.CurrentCulture);
            }
            
            menuItem.Click += (s, e) => 
            {
                try { clickHandler(); }
                catch (Exception ex) 
                { 
                    MessageBox.Show($"Error: {ex.Message}", "Operation Failed", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
            
            parent.Items.Add(menuItem);
            return menuItem;
        }

        private MenuItem AddSubMenu(ItemsControl parent, string header, string iconPath)
        {
            var menuItem = new MenuItem { Header = header };
            
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                try
                {
                    var image = new Image
                    {
                        Source = new BitmapImage(new Uri(Path.GetFullPath(iconPath))),
                        Width = 16,
                        Height = 16
                    };
                    menuItem.Icon = image;
                }
                catch { }
            }
            
            parent.Items.Add(menuItem);
            return menuItem;
        }

        #endregion

        #region Operation Implementations

        private void OpenItem(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to open:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenWith(string path)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "rundll32.exe",
                    Arguments = $"shell32.dll,OpenAs_RunDLL {path}",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void ShowInExplorer(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start("explorer.exe", $"/select,\"{path}\"");
                }
                else if (Directory.Exists(path))
                {
                    Process.Start("explorer.exe", path);
                }
            }
            catch { }
        }

        private void OpenInNewTab(string path)
        {
            // This would need to be implemented based on your tab system
            var window = Application.Current.MainWindow;
            // Example: ((MainWindow)window).CreateNewTab(path);
        }

        private void OpenInNewWindow(string path)
        {
            // This would open a new instance of your file explorer
            // Example: new ExplorerWindow(path).Show();
        }

        private void CutItem(string path)
        {
            _fileOperationHandler.CopyItem(path);
            // Mark item as "cut" visually (semi-transparent)
            // This would need to be tracked in your file tree
        }

        private void CopyItem(string path)
        {
            _fileOperationHandler.CopyItem(path);
        }

        private void PasteItems(string targetPath)
        {
            string target = Directory.Exists(targetPath) ? targetPath : Path.GetDirectoryName(targetPath);
            _fileOperationHandler.PasteItemsAsync(target).ConfigureAwait(false);
        }

        private void DuplicateItem(string path)
        {
            _fileOperationHandler.CopyItem(path);
            string target = Directory.Exists(path) ? path : Path.GetDirectoryName(path);
            _fileOperationHandler.PasteItemsAsync(target).ConfigureAwait(false);
        }

        private void RenameItem(string path)
        {
            // This could show an inline rename UI in the tree
            // For now, use the input dialog
            string currentName = Path.GetFileName(path);
            string newName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter new name:", 
                "Rename", 
                currentName);
                
            if (!string.IsNullOrWhiteSpace(newName) && newName != currentName)
            {
                _fileOperationHandler.RenameItem(path, newName, _fileTree);
            }
        }

        private void TogglePin(string path)
        {
            if (_metadataManager.GetPinnedItems().Contains(path))
                _metadataManager.RemovePinnedItem(path);
            else
                _metadataManager.AddPinnedItem(path);
        }

        private void ShowAddTagDialog(string path)
        {
            string tag = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter tag name:", "Add Tag", "");
                
            if (!string.IsNullOrWhiteSpace(tag))
            {
                _metadataManager.AddTag(path, tag);
            }
        }

        private void RemoveTag(string path, string tag)
        {
            _metadataManager.RemoveTag(path, tag);
        }

        private bool IsArchive(string extension)
        {
            var archiveExtensions = new[] { ".zip", ".rar", ".7z", ".tar", ".gz", ".bz2" };
            return archiveExtensions.Contains(extension.ToLower());
        }

        private void ExtractArchive(string archivePath, string targetPath)
        {
            // Implement archive extraction
            MessageBox.Show("Archive extraction not yet implemented", "Coming Soon");
        }

        private void ExtractArchiveTo(string archivePath)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                ExtractArchive(archivePath, dialog.SelectedPath);
            }
        }

        private void CompressToArchive(string path)
        {
            // Implement compression
            MessageBox.Show("Archive compression not yet implemented", "Coming Soon");
        }

        private void ShowProperties(string path)
        {
            // Show Windows properties dialog
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{path}\"",
                UseShellExecute = true,
                Verb = "properties"
            });
        }

        private void AddOpenWithOptions(MenuItem parentMenu, string filePath)
        {
            // Add common programs based on file type
            string extension = Path.GetExtension(filePath).ToLower();
            
            switch (extension)
            {
                case ".txt":
                case ".log":
                case ".ini":
                    AddMenuItem(parentMenu, "Notepad", null, () => OpenWithProgram(filePath, "notepad.exe"));
                    break;
                    
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".gif":
                case ".bmp":
                    AddMenuItem(parentMenu, "Paint", null, () => OpenWithProgram(filePath, "mspaint.exe"));
                    AddMenuItem(parentMenu, "Photos", null, () => OpenWithProgram(filePath, "ms-photos:"));
                    break;
            }
            
            parentMenu.Items.Add(new Separator());
            AddMenuItem(parentMenu, "Choose another app...", null, () => OpenWith(filePath));
        }

        private void OpenWithProgram(string filePath, string program)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = program,
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void AddColorOptions(MenuItem parentMenu, string path)
        {
            var colors = new[]
            {
                ("Red", "#FF0000"),
                ("Orange", "#FFA500"),
                ("Yellow", "#FFFF00"),
                ("Green", "#00FF00"),
                ("Blue", "#0000FF"),
                ("Purple", "#800080"),
                ("Black", "#000000"),
                ("Gray", "#808080")
            };
            
            foreach (var (name, hex) in colors)
            {
                AddMenuItem(parentMenu, name, null, () => SetItemColor(path, hex));
            }
            
            parentMenu.Items.Add(new Separator());
            AddMenuItem(parentMenu, "Remove Color", null, () => SetItemColor(path, null));
        }

        private void SetItemColor(string path, string colorHex)
        {
            if (colorHex == null)
            {
                _metadataManager.SetItemColor(path, string.Empty);
            }
            else
            {
                _metadataManager.SetItemColor(path, colorHex);
                _metadataManager.AddRecentColor(colorHex);
            }
            _fileTree.RefreshView();
        }

        private void AddSendToOptions(MenuItem parentMenu, string path)
        {
            AddMenuItem(parentMenu, "Desktop (create shortcut)", null, 
                () => CreateShortcut(path, Environment.GetFolderPath(Environment.SpecialFolder.Desktop)));
                
            AddMenuItem(parentMenu, "Documents", null, 
                () => CopyToFolder(path, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)));
                
            AddMenuItem(parentMenu, "Compressed (zipped) folder", null, 
                () => CompressToArchive(path));
        }

        private void CreateShortcut(string targetPath, string shortcutLocation)
        {
            // Implement shortcut creation
            MessageBox.Show("Shortcut creation not yet implemented", "Coming Soon");
        }

        private void CopyToFolder(string sourcePath, string targetFolder)
        {
            _fileOperationHandler.CopyItem(sourcePath);
            _fileOperationHandler.PasteItemsAsync(targetFolder).ConfigureAwait(false);
        }

        // Multi-select operations
        private void OpenMultipleItems(IReadOnlyList<string> paths)
        {
            foreach (var path in paths.Take(10)) // Limit to prevent too many windows
            {
                OpenItem(path);
            }
        }

        private void CutMultipleItems(IReadOnlyList<string> paths)
        {
            _fileOperationHandler.CopyMultipleItems(paths);
            // Mark items as "cut" visually
        }

        private void ShowAddTagDialogForMultiple(IReadOnlyList<string> paths)
        {
            string tag = Microsoft.VisualBasic.Interaction.InputBox(
                $"Enter tag to add to {paths.Count} items:", "Add Tag", "");
                
            if (!string.IsNullOrWhiteSpace(tag))
            {
                foreach (var path in paths)
                {
                    _metadataManager.AddTag(path, tag);
                }
            }
        }

        private void AddColorOptionsForMultiple(MenuItem parentMenu, IReadOnlyList<string> paths)
        {
            var colors = new[]
            {
                ("Red", "#FF0000"),
                ("Orange", "#FFA500"),
                ("Yellow", "#FFFF00"),
                ("Green", "#00FF00"),
                ("Blue", "#0000FF"),
                ("Purple", "#800080"),
                ("Black", "#000000"),
                ("Gray", "#808080")
            };
            
            foreach (var (name, hex) in colors)
            {
                AddMenuItem(parentMenu, name, null, () => SetMultipleItemsColor(paths, hex));
            }
            
            parentMenu.Items.Add(new Separator());
            AddMenuItem(parentMenu, "Remove Color", null, () => SetMultipleItemsColor(paths, null));
        }

        private void SetMultipleItemsColor(IReadOnlyList<string> paths, string colorHex)
        {
            foreach (var path in paths)
            {
                SetItemColor(path, colorHex);
            }
        }

        private void ExtractMultipleArchives(IReadOnlyList<string> paths)
        {
            var archives = paths.Where(p => File.Exists(p) && IsArchive(Path.GetExtension(p)));
            foreach (var archive in archives)
            {
                ExtractArchive(archive, Path.GetDirectoryName(archive));
            }
        }

        private void CompressMultipleToArchive(IReadOnlyList<string> paths)
        {
            // Implement multiple file compression
            MessageBox.Show("Multi-file compression not yet implemented", "Coming Soon");
        }

        private void ShowSelectByPatternDialog()
        {
            _fileTree.SelectByPattern("*.*", false);
        }

        private void ShowMultipleProperties(IReadOnlyList<string> paths)
        {
            // Could show a custom properties dialog for multiple items
            MessageBox.Show($"Properties for {paths.Count} items", "Properties");
        }

        #endregion
    }
}