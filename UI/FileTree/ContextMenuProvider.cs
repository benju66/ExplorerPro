// UI/FileTree/ContextMenuProvider.cs - Refactored version
using System;
using System.Collections.Generic;
using System.Diagnostics;
using IOPath = System.IO.Path;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
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
        
        // Menu item caching for performance
        private readonly Dictionary<string, MenuItem> _menuItemCache = new Dictionary<string, MenuItem>();
        private readonly Dictionary<string, BitmapImage> _iconCache = new Dictionary<string, BitmapImage>();
        private bool _disposed = false;
        
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
                AddLazySubMenu(contextMenu, "Open with", iconPath + "apps.png", 
                    (menu) => AddOpenWithOptions(menu, selectedPath));
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

            // Clipboard operations - enhanced with submenu
            var clipboardMenu = AddSubMenu(contextMenu, "Clip_board", iconPath + "clipboard.png");

            AddMenuItem(clipboardMenu, "Cu_t", iconPath + "cut.png", 
                () => CutItem(selectedPath), Key.X, ModifierKeys.Control);
                
            AddMenuItem(clipboardMenu, "_Copy", iconPath + "copy.png", 
                () => CopyItem(selectedPath), Key.C, ModifierKeys.Control);
                
            AddDynamicMenuItem(clipboardMenu, "_Paste", iconPath + "paste.png", 
                () => PasteItems(selectedPath), 
                () => Clipboard.ContainsFileDropList(),
                Key.V, ModifierKeys.Control);

            clipboardMenu.Items.Add(new Separator());

            // New advanced clipboard features
            AddMenuItem(clipboardMenu, "Copy as Pat_h", iconPath + "copy-path.png", 
                () => CopyAsPath(selectedPath), Key.C, ModifierKeys.Control | ModifierKeys.Shift);
                
            AddMenuItem(clipboardMenu, "Copy _File Name", iconPath + "copy-name.png", 
                () => CopyFileName(selectedPath));
                
            AddMenuItem(clipboardMenu, "Copy Fol_der Path", iconPath + "folder-path.png", 
                () => CopyFolderPath(selectedPath));

            // Keep the main menu items for quick access
            AddMenuItem(contextMenu, "Cu_t", iconPath + "cut.png", 
                () => CutItem(selectedPath), Key.X, ModifierKeys.Control);
                
            AddMenuItem(contextMenu, "_Copy", iconPath + "copy.png", 
                () => CopyItem(selectedPath), Key.C, ModifierKeys.Control);
                
            AddDynamicMenuItem(contextMenu, "_Paste", iconPath + "paste.png", 
                () => PasteItems(selectedPath), 
                () => Clipboard.ContainsFileDropList(),
                Key.V, ModifierKeys.Control);

            AddMenuItem(contextMenu, "_Duplicate", iconPath + "copy-plus.png", 
                () => DuplicateItem(selectedPath), Key.D, ModifierKeys.Control);

            contextMenu.Items.Add(new Separator());

            // Add Undo/Redo
            AddDynamicMenuItem(contextMenu, "_Undo", iconPath + "undo.png",
                () => _undoManager.Undo(),
                () => _undoManager.CanUndo,
                Key.Z, ModifierKeys.Control);

            AddDynamicMenuItem(contextMenu, "_Redo", iconPath + "redo.png",
                () => _undoManager.Redo(),
                () => _undoManager.CanRedo,
                Key.Y, ModifierKeys.Control);

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
            AddLazySubMenu(contextMenu, "Organize", iconPath + "folder-cog.png", (organizeMenu) =>
            {
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
            });
            
            // Archive operations (if applicable)
            string extension = IOPath.GetExtension(selectedPath).ToLower();
            if (IsArchive(extension))
            {
                contextMenu.Items.Add(new Separator());
                AddMenuItem(contextMenu, "E_xtract Here", iconPath + "archive-extract.png", 
                    () => ExtractArchive(selectedPath, IOPath.GetDirectoryName(selectedPath)));
                    
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

            SetupDynamicUpdates(contextMenu);

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
            bool hasArchives = selectedPaths.Any(p => File.Exists(p) && IsArchive(IOPath.GetExtension(p)));

            // Open operations
            if (selectedPaths.Count <= 10) // Limit to prevent too many windows
            {
                AddMenuItem(contextMenu, "_Open All", iconPath + "folder-open.png", 
                    () => OpenMultipleItems(selectedPaths));
            }

            contextMenu.Items.Add(new Separator());

            // Clipboard operations - enhanced with submenu
            var clipboardMenu = AddSubMenu(contextMenu, "Clip_board", iconPath + "clipboard.png");

            AddMenuItem(clipboardMenu, "Cu_t", iconPath + "cut.png", 
                () => CutMultipleItems(selectedPaths), Key.X, ModifierKeys.Control);
                
            AddMenuItem(clipboardMenu, "_Copy", iconPath + "copy.png", 
                () => _fileOperationHandler.CopyMultipleItems(selectedPaths), Key.C, ModifierKeys.Control);

            clipboardMenu.Items.Add(new Separator());

            // Advanced clipboard features for multi-select
            AddMenuItem(clipboardMenu, "Copy as Pat_hs", iconPath + "copy-path.png", 
                () => CopyMultipleAsPath(selectedPaths), Key.C, ModifierKeys.Control | ModifierKeys.Shift);
                
            AddMenuItem(clipboardMenu, "Copy _File Names", iconPath + "copy-name.png", 
                () => CopyMultipleFileNames(selectedPaths));

            // Keep the main menu items for quick access
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

            SetupDynamicUpdates(contextMenu);

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
                        Source = new BitmapImage(new Uri(IOPath.GetFullPath(iconPath))),
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

        private MenuItem AddDynamicMenuItem(ItemsControl parent, string header, string iconPath,
            Action clickHandler, Func<bool> canExecute, Key? gestureKey = null, 
            ModifierKeys? gestureModifiers = null)
        {
            var menuItem = AddMenuItem(parent, header, iconPath, clickHandler, 
                gestureKey, gestureModifiers, canExecute?.Invoke() ?? true);
            
            // Store the canExecute function for later updates
            menuItem.Tag = canExecute;
            
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
                        Source = new BitmapImage(new Uri(IOPath.GetFullPath(iconPath))),
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

        private MenuItem AddLazySubMenu(ItemsControl parent, string header, string iconPath, 
            Action<MenuItem> buildMenuAction)
        {
            var menuItem = new MenuItem { Header = header };
            var isBuilt = false;
            
            // Set icon if available
            if (!string.IsNullOrEmpty(iconPath) && File.Exists(iconPath))
            {
                menuItem.Icon = GetCachedIcon(iconPath);
            }
            
            // Build submenu on first open
            menuItem.SubmenuOpened += (s, e) =>
            {
                if (!isBuilt && !_disposed)
                {
                    isBuilt = true;
                    menuItem.Items.Clear(); // Remove placeholder
                    
                    try
                    {
                        Mouse.OverrideCursor = Cursors.Wait;
                        buildMenuAction(menuItem);
                    }
                    finally
                    {
                        Mouse.OverrideCursor = null;
                    }
                }
            };
            
            // Add loading placeholder
            menuItem.Items.Add(new MenuItem 
            { 
                Header = "Loading...", 
                IsEnabled = false,
                FontStyle = FontStyles.Italic 
            });
            
            parent.Items.Add(menuItem);
            return menuItem;
        }

        private Image GetCachedIcon(string iconPath)
        {
            if (!_iconCache.TryGetValue(iconPath, out var cachedImage))
            {
                try
                {
                    cachedImage = new BitmapImage(new Uri(IOPath.GetFullPath(iconPath)));
                    cachedImage.Freeze(); // Important for performance
                    _iconCache[iconPath] = cachedImage;
                }
                catch
                {
                    return null;
                }
            }
            
            return new Image
            {
                Source = cachedImage,
                Width = 16,
                Height = 16
            };
        }

        private void SetupDynamicUpdates(ContextMenu menu)
        {
            menu.Opened += (s, e) =>
            {
                UpdateDynamicMenuItems(menu);
            };
        }

        private void UpdateDynamicMenuItems(ItemsControl parent)
        {
            foreach (var item in parent.Items)
            {
                if (item is MenuItem menuItem)
                {
                    // Update if it has a canExecute function
                    if (menuItem.Tag is Func<bool> canExecute)
                    {
                        menuItem.IsEnabled = canExecute();
                        
                        // Update header for context-aware items
                        UpdateDynamicHeader(menuItem);
                    }
                    
                    // Recursively update submenus
                    if (menuItem.Items.Count > 0)
                    {
                        UpdateDynamicMenuItems(menuItem);
                    }
                }
            }
        }

        private void UpdateDynamicHeader(MenuItem menuItem)
        {
            var header = menuItem.Header?.ToString() ?? "";
            
            // Update Undo/Redo with operation names
            if (header.StartsWith("_Undo"))
            {
                if (_undoManager.CanUndo)
                {
                    menuItem.Header = $"_Undo {GetUndoOperationName()}";
                }
                else
                {
                    menuItem.Header = "_Undo";
                }
            }
            else if (header.StartsWith("_Redo"))
            {
                if (_undoManager.CanRedo)
                {
                    menuItem.Header = $"_Redo {GetRedoOperationName()}";
                }
                else
                {
                    menuItem.Header = "_Redo";
                }
            }
        }

        private string GetUndoOperationName()
        {
            return _undoManager.GetUndoOperationName();
        }

        private string GetRedoOperationName()
        {
            return _undoManager.GetRedoOperationName();
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

        private void CopyAsPath(string path)
        {
            try
            {
                // Add quotes around the path (Windows standard)
                string quotedPath = $"\"{path}\"";
                Clipboard.SetText(quotedPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy path:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyMultipleAsPath(IReadOnlyList<string> paths)
        {
            try
            {
                // Join multiple paths with newlines, each quoted
                var quotedPaths = paths.Select(p => $"\"{p}\"");
                var allPaths = string.Join(Environment.NewLine, quotedPaths);
                Clipboard.SetText(allPaths);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy paths:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyFileName(string path)
        {
            try
            {
                string fileName = IOPath.GetFileName(path);
                Clipboard.SetText(fileName);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy file name:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CopyFolderPath(string path)
        {
            try
            {
                string folderPath = Directory.Exists(path) ? path : IOPath.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    Clipboard.SetText(folderPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy folder path:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Multi-select versions
        private void CopyMultipleFileNames(IReadOnlyList<string> paths)
        {
            try
            {
                var fileNames = paths.Select(p => IOPath.GetFileName(p));
                var allNames = string.Join(Environment.NewLine, fileNames);
                Clipboard.SetText(allNames);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to copy file names:\n{ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PasteItems(string targetPath)
        {
            string target = Directory.Exists(targetPath) ? targetPath : IOPath.GetDirectoryName(targetPath);
            _fileOperationHandler.PasteItemsAsync(target).ConfigureAwait(false);
        }

        private void DuplicateItem(string path)
        {
            _fileOperationHandler.CopyItem(path);
            string target = Directory.Exists(path) ? path : IOPath.GetDirectoryName(path);
            _fileOperationHandler.PasteItemsAsync(target).ConfigureAwait(false);
        }

        private void RenameItem(string path)
        {
            // Start inline editing mode for the selected item
            if (_fileTree is ImprovedFileTreeListView fileTreeView)
            {
                var item = fileTreeView.FindItemByPath(path);
                if (item != null)
                {
                    item.IsInEditMode = true;
                    return;
                }
            }
            
            // Fallback to dialog if inline editing is not available
            string currentName = IOPath.GetFileName(path);
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
            string extension = IOPath.GetExtension(filePath).ToLower();
            
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
                var menuItem = new MenuItem { Header = name };
                
                // Create color swatch
                var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                // Color rectangle
                var colorRect = new Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Stroke = new SolidColorBrush(Colors.DarkGray),
                    StrokeThickness = 1,
                    Margin = new Thickness(0, 0, 8, 0),
                    RadiusX = 2,
                    RadiusY = 2
                };
                
                // Color name
                var textBlock = new TextBlock 
                { 
                    Text = name,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                stackPanel.Children.Add(colorRect);
                stackPanel.Children.Add(textBlock);
                
                menuItem.Header = stackPanel;
                menuItem.Click += (s, e) => SetItemColor(path, hex);
                
                parentMenu.Items.Add(menuItem);
            }
            
            parentMenu.Items.Add(new Separator());
            
            // Recent colors section
            var recentColors = _metadataManager.GetRecentColors();
            if (recentColors.Any())
            {
                parentMenu.Items.Add(new MenuItem 
                { 
                    Header = "Recent Colors", 
                    IsEnabled = false,
                    FontWeight = FontWeights.Bold 
                });
                
                foreach (var hex in recentColors)
                {
                    var menuItem = new MenuItem();
                    
                    var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                    
                    var colorRect = new Rectangle
                    {
                        Width = 16,
                        Height = 16,
                        Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                        Stroke = new SolidColorBrush(Colors.DarkGray),
                        StrokeThickness = 1,
                        Margin = new Thickness(0, 0, 8, 0),
                        RadiusX = 2,
                        RadiusY = 2
                    };
                    
                    var textBlock = new TextBlock 
                    { 
                        Text = hex,
                        VerticalAlignment = VerticalAlignment.Center,
                        FontFamily = new FontFamily("Consolas")
                    };
                    
                    stackPanel.Children.Add(colorRect);
                    stackPanel.Children.Add(textBlock);
                    
                    menuItem.Header = stackPanel;
                    menuItem.Click += (s, e) => SetItemColor(path, hex);
                    
                    parentMenu.Items.Add(menuItem);
                }
                
                parentMenu.Items.Add(new Separator());
            }
            
            // Remove color option with icon
            var removeItem = new MenuItem { Header = "Remove Color" };
            try
            {
                removeItem.Icon = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Assets/Icons/remove-color.png")),
                    Width = 16,
                    Height = 16
                };
            }
            catch { }
            removeItem.Click += (s, e) => SetItemColor(path, null);
            parentMenu.Items.Add(removeItem);
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
            
            // Use RefreshDirectory on the parent directory instead of RefreshView
            // to avoid changing the current root directory
            string directoryToRefresh = Directory.Exists(path) ? path : IOPath.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directoryToRefresh))
            {
                _fileTree.RefreshDirectory(directoryToRefresh);
            }
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
                var menuItem = new MenuItem();
                
                // Create the same visual stack panel as single selection
                var stackPanel = new StackPanel { Orientation = Orientation.Horizontal };
                
                var colorRect = new Rectangle
                {
                    Width = 16,
                    Height = 16,
                    Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)),
                    Stroke = new SolidColorBrush(Colors.DarkGray),
                    StrokeThickness = 1,
                    Margin = new Thickness(0, 0, 8, 0),
                    RadiusX = 2,
                    RadiusY = 2
                };
                
                var textBlock = new TextBlock 
                { 
                    Text = name,
                    VerticalAlignment = VerticalAlignment.Center
                };
                
                stackPanel.Children.Add(colorRect);
                stackPanel.Children.Add(textBlock);
                
                menuItem.Header = stackPanel;
                menuItem.Click += (s, e) => SetMultipleItemsColor(paths, hex);
                
                parentMenu.Items.Add(menuItem);
            }
            
            parentMenu.Items.Add(new Separator());
            
            // Remove color option with icon
            var removeItem = new MenuItem { Header = "Remove Color" };
            try
            {
                removeItem.Icon = new Image
                {
                    Source = new BitmapImage(new Uri("pack://application:,,,/Assets/Icons/remove-color.png")),
                    Width = 16,
                    Height = 16
                };
            }
            catch { }
            removeItem.Click += (s, e) => SetMultipleItemsColor(paths, null);
            parentMenu.Items.Add(removeItem);
        }

        private void SetMultipleItemsColor(IReadOnlyList<string> paths, string colorHex)
        {
            // Update metadata for all items first
            foreach (var path in paths)
            {
                if (colorHex == null)
                {
                    _metadataManager.SetItemColor(path, string.Empty);
                }
                else
                {
                    _metadataManager.SetItemColor(path, colorHex);
                }
            }
            
            // Add to recent colors only once if setting a color
            if (colorHex != null)
            {
                _metadataManager.AddRecentColor(colorHex);
            }
            
            // Collect unique directories to refresh
            var directoriesToRefresh = paths
                .Select(path => Directory.Exists(path) ? path : IOPath.GetDirectoryName(path))
                .Where(dir => !string.IsNullOrEmpty(dir))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            
            // Refresh each unique directory once
            foreach (var directory in directoriesToRefresh)
            {
                _fileTree.RefreshDirectory(directory);
            }
        }

        private void ExtractMultipleArchives(IReadOnlyList<string> paths)
        {
            var archives = paths.Where(p => File.Exists(p) && IsArchive(IOPath.GetExtension(p)));
            foreach (var archive in archives)
            {
                ExtractArchive(archive, IOPath.GetDirectoryName(archive));
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
            // This would show a properties dialog for multiple items
            MessageBox.Show($"Properties for {paths.Count} items", "Properties", 
                MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion

        public void Dispose()
        {
            _menuItemCache.Clear();
            _iconCache.Clear();
            _disposed = true;
        }
    }
}