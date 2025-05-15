namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interface for file tree view components.
    /// Defines methods for navigation, file operations, and handling user interactions.
    /// </summary>
    public interface IFileTree
    {
        /// <summary>
        /// Gets the current path displayed in the file tree.
        /// </summary>
        string GetCurrentPath();

        /// <summary>
        /// Gets the path of the selected item.
        /// </summary>
        /// <returns>The selected path or null if nothing is selected</returns>
        string? GetSelectedPath();

        /// <summary>
        /// Gets the path of the selected folder, or the parent folder if a file is selected.
        /// </summary>
        /// <returns>The selected folder path</returns>
        string GetSelectedFolderPath();

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
        /// Selects an item in the tree by path.
        /// </summary>
        /// <param name="path">The path to select</param>
        void SelectItem(string path);

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
        /// Handles files dropped onto the file tree.
        /// </summary>
        /// <param name="data">Drop data containing files.</param>
        void HandleFileDrop(object data);
    }
}