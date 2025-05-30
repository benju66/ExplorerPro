// UI/FileTree/Services/IFileTreeService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ExplorerPro.UI.FileTree;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Interface for file tree operations
    /// </summary>
    public interface IFileTreeService
    {
        /// <summary>
        /// Loads directory contents asynchronously
        /// </summary>
        /// <param name="directoryPath">Path to directory to load</param>
        /// <param name="showHiddenFiles">Whether to include hidden files</param>
        /// <param name="level">Hierarchical level for new items</param>
        /// <returns>Collection of file tree items</returns>
        Task<IEnumerable<FileTreeItem>> LoadDirectoryAsync(string directoryPath, bool showHiddenFiles = false, int level = 0);

        /// <summary>
        /// Creates a file tree item from a path
        /// </summary>
        /// <param name="path">File or directory path</param>
        /// <param name="level">Hierarchical level</param>
        /// <returns>Created file tree item</returns>
        FileTreeItem CreateFileTreeItem(string path, int level = 0);

        /// <summary>
        /// Checks if a directory has accessible children
        /// </summary>
        /// <param name="directoryPath">Directory path to check</param>
        /// <param name="showHiddenFiles">Whether to consider hidden files</param>
        /// <returns>True if directory has accessible children</returns>
        bool DirectoryHasAccessibleChildren(string directoryPath, bool showHiddenFiles = false);

        /// <summary>
        /// Checks if a file or directory is hidden
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if the item is hidden</returns>
        bool IsHidden(string path);

        /// <summary>
        /// Applies metadata styling to a file tree item
        /// </summary>
        /// <param name="item">Item to apply styling to</param>
        void ApplyMetadataStyling(FileTreeItem item);

        /// <summary>
        /// Finds a FileTreeItem by path in a collection
        /// </summary>
        /// <param name="items">Collection to search in</param>
        /// <param name="path">Path to find</param>
        /// <returns>Found item or null</returns>
        FileTreeItem FindItemByPath(IEnumerable<FileTreeItem> items, string path);

        /// <summary>
        /// Recursively searches for an item by path
        /// </summary>
        /// <param name="parent">Parent item to search in</param>
        /// <param name="path">Path to find</param>
        /// <returns>Found item or null</returns>
        FileTreeItem FindItemByPathRecursive(FileTreeItem parent, string path);

        /// <summary>
        /// Event raised when an error occurs during file operations
        /// </summary>
        event EventHandler<string> ErrorOccurred;
    }
}