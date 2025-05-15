// FileOperations/IFileOperations.cs
using System;

namespace ExplorerPro.FileOperations
{
    /// <summary>
    /// Interface for file system operations.
    /// </summary>
    public interface IFileOperations
    {
        /// <summary>
        /// Renames a file or folder.
        /// </summary>
        /// <param name="path">The path to the item to rename.</param>
        /// <param name="newName">The new name for the item.</param>
        /// <returns>The new path of the renamed item, or null on failure.</returns>
        string RenameItem(string path, string newName);

        /// <summary>
        /// Creates a new file.
        /// </summary>
        /// <param name="parentDir">Directory in which to create the file.</param>
        /// <param name="fileName">Name of the file to create.</param>
        /// <returns>The path to the created file, or null on failure.</returns>
        string CreateNewFile(string parentDir, string fileName);

        /// <summary>
        /// Creates a new folder.
        /// </summary>
        /// <param name="parentDir">Directory in which to create the folder.</param>
        /// <param name="folderName">Name of the folder to create.</param>
        /// <returns>The path to the created folder, or null on failure.</returns>
        string CreateNewFolder(string parentDir, string folderName);

        /// <summary>
        /// Deletes a file or folder.
        /// </summary>
        /// <param name="path">Path to the item to delete.</param>
        /// <returns>True if deletion was successful, false otherwise.</returns>
        bool DeleteItem(string path);

        /// <summary>
        /// Copies a file or folder.
        /// </summary>
        /// <param name="sourcePath">Path to the item to copy.</param>
        /// <param name="destinationDir">Directory to copy the item to.</param>
        /// <returns>The path to the copied item, or null on failure.</returns>
        string CopyItem(string sourcePath, string destinationDir);

        /// <summary>
        /// Moves a file or folder.
        /// </summary>
        /// <param name="sourcePath">Path to the item to move.</param>
        /// <param name="destinationDir">Directory to move the item to.</param>
        /// <returns>True if move was successful, false otherwise.</returns>
        bool MoveItem(string sourcePath, string destinationDir);
    }
}