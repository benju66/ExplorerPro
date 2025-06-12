using System.Collections.Generic;
using System.Collections.ObjectModel;
using ExplorerPro.UI.FileTree.Services;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interface for file tree view components.
    /// Defines methods for navigation, file operations, and handling user interactions.
    /// </summary>
    public interface IFileTree
    {
        /// <summary>
        /// Gets the root items collection for the file tree.
        /// </summary>
        ObservableCollection<FileTreeItem> RootItems { get; }

        /// <summary>
        /// Gets the current path displayed in the file tree.
        /// </summary>
        string GetCurrentPath();

        /// <summary>
        /// Gets the path of the first selected item.
        /// </summary>
        /// <returns>The selected path or null if nothing is selected</returns>
        string? GetSelectedPath();

        /// <summary>
        /// Gets all selected paths when multiple items are selected.
        /// </summary>
        /// <returns>Collection of selected paths</returns>
        IReadOnlyList<string> GetSelectedPaths();

        /// <summary>
        /// Gets the path of the selected folder, or the parent folder if a file is selected.
        /// </summary>
        /// <returns>The selected folder path</returns>
        string GetSelectedFolderPath();

        /// <summary>
        /// Gets the selection service that manages all selection state.
        /// </summary>
        SelectionService? SelectionService { get; }

        /// <summary>
        /// Sets the root directory for the file tree.
        /// </summary>
        /// <param name="path">Path to set as root.</param>
        void SetRootDirectory(string path);

        /// <summary>
        /// Refreshes the file tree view.
        /// </summary>
        void RefreshView();

        /// <summary>
        /// Refreshes a specific directory in the tree.
        /// </summary>
        /// <param name="directoryPath">Path of the directory to refresh</param>
        void RefreshDirectory(string directoryPath);

        /// <summary>
        /// Selects an item in the tree by path.
        /// </summary>
        /// <param name="path">The path to select</param>
        void SelectItem(string path);

        /// <summary>
        /// Selects multiple items by their paths.
        /// </summary>
        /// <param name="paths">Collection of paths to select</param>
        void SelectItems(IEnumerable<string> paths);

        /// <summary>
        /// Copies selected items to clipboard.
        /// </summary>
        void CopySelected();

        /// <summary>
        /// Cuts selected items to clipboard.
        /// </summary>
        void CutSelected();

        /// <summary>
        /// Pastes items from clipboard to current location.
        /// </summary>
        void Paste();

        /// <summary>
        /// Deletes the selected items.
        /// </summary>
        void DeleteSelected();

        /// <summary>
        /// Creates a new folder in the current directory.
        /// </summary>
        void CreateFolder();

        /// <summary>
        /// Creates a new file in the current directory.
        /// </summary>
        void CreateFile();

        /// <summary>
        /// Toggles the display of hidden files.
        /// </summary>
        void ToggleShowHidden();

        /// <summary>
        /// Clears the current selection.
        /// </summary>
        void ClearSelection();

        /// <summary>
        /// Selects all items in the current view.
        /// </summary>
        void SelectAll();

        /// <summary>
        /// Inverts the current selection.
        /// </summary>
        void InvertSelection();

        /// <summary>
        /// Selects items matching a pattern.
        /// </summary>
        /// <param name="pattern">Wildcard pattern to match</param>
        /// <param name="addToSelection">Whether to add to existing selection</param>
        void SelectByPattern(string pattern, bool addToSelection = false);

        /// <summary>
        /// Toggles multi-select mode.
        /// </summary>
        void ToggleMultiSelectMode();

        /// <summary>
        /// Gets whether multi-select mode is active.
        /// </summary>
        bool IsMultiSelectMode { get; }

        /// <summary>
        /// Gets whether any items are selected.
        /// </summary>
        bool HasSelection { get; }

        /// <summary>
        /// Gets the count of selected items.
        /// </summary>
        int SelectionCount { get; }

        /// <summary>
        /// Handles files dropped onto the file tree.
        /// </summary>
        /// <param name="data">Drop data containing files.</param>
        void HandleFileDrop(object data);

        /// <summary>
        /// Navigates to a path and highlights it.
        /// </summary>
        /// <param name="path">Path to navigate to and highlight</param>
        void NavigateAndHighlight(string path);

        /// <summary>
        /// Expands the tree to show a specific path.
        /// </summary>
        /// <param name="path">Path to expand to</param>
        void ExpandToPath(string path);

        /// <summary>
        /// Collapses all expanded nodes.
        /// </summary>
        void CollapseAll();

        /// <summary>
        /// Expands all nodes in the tree.
        /// </summary>
        void ExpandAll();

        /// <summary>
        /// Gets the file tree item for a given path.
        /// </summary>
        /// <param name="path">Path to find</param>
        /// <returns>The FileTreeItem or null if not found</returns>
        FileTreeItem FindItemByPath(string path);

        /// <summary>
        /// Refreshes theme elements in the tree view.
        /// </summary>
        void RefreshThemeElements();
    }
}