// UI/FileTree/Services/IFileTreeDragDropService.cs (UPDATED with Outlook support)
using System;
using System.Collections.Generic;
using System.Windows;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Interface for file tree drag and drop operations
    /// </summary>
    public interface IFileTreeDragDropService
    {
        /// <summary>
        /// Handles drag enter events
        /// </summary>
        /// <param name="e">Drag event arguments</param>
        void HandleDragEnter(DragEventArgs e);

        /// <summary>
        /// Handles drag over events
        /// </summary>
        /// <param name="e">Drag event arguments</param>
        /// <param name="getItemFromPoint">Function to get FileTreeItem from point</param>
        void HandleDragOver(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint);

        /// <summary>
        /// Handles drop events
        /// </summary>
        /// <param name="e">Drag event arguments</param>
        /// <param name="getItemFromPoint">Function to get FileTreeItem from point</param>
        /// <param name="currentTreePath">Current tree root path to detect internal vs external drops</param>
        /// <returns>True if drop was handled successfully</returns>
        bool HandleDrop(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint, string currentTreePath = null);

        /// <summary>
        /// Handles drag leave events
        /// </summary>
        /// <param name="e">Drag event arguments</param>
        void HandleDragLeave(DragEventArgs e);

        /// <summary>
        /// Starts a drag operation for selected items
        /// </summary>
        /// <param name="source">Drag source element</param>
        /// <param name="selectedPaths">Collection of selected file paths</param>
        void StartDrag(DependencyObject source, IEnumerable<string> selectedPaths);

        /// <summary>
        /// Handles files dropped from external sources (Windows Explorer)
        /// </summary>
        /// <param name="droppedFiles">Array of dropped file paths</param>
        /// <param name="targetPath">Target directory path</param>
        /// <returns>True if files were handled successfully</returns>
        bool HandleExternalFileDrop(string[] droppedFiles, string targetPath);

        /// <summary>
        /// Handles files moved internally within the tree
        /// </summary>
        /// <param name="droppedFiles">Array of file paths to move</param>
        /// <param name="targetPath">Target directory path</param>
        /// <param name="currentTreePath">Current tree root path</param>
        /// <returns>True if files were moved successfully</returns>
        bool HandleInternalFileMove(string[] droppedFiles, string targetPath, string currentTreePath);

        /// <summary>
        /// Handles Outlook item drops (emails, attachments)
        /// </summary>
        /// <param name="dataObject">Data object from Outlook</param>
        /// <param name="targetPath">Target directory path</param>
        /// <returns>True if handled successfully</returns>
        bool HandleOutlookDrop(DataObject dataObject, string targetPath);

        /// <summary>
        /// Event raised when files are successfully dropped or moved
        /// </summary>
        event EventHandler<FilesDroppedEventArgs> FilesDropped;

        /// <summary>
        /// Event raised when files are moved internally (need source refresh)
        /// </summary>
        event EventHandler<FilesMoved> FilesMoved;

        /// <summary>
        /// Event raised when an error occurs during drag/drop operations
        /// </summary>
        event EventHandler<string> ErrorOccurred;
    }

    /// <summary>
    /// Event arguments for files dropped events
    /// </summary>
    public class FilesDroppedEventArgs : EventArgs
    {
        public string[] SourceFiles { get; }
        public string TargetPath { get; }
        public DragDropEffects Effects { get; }
        public bool IsInternalMove { get; }

        public FilesDroppedEventArgs(string[] sourceFiles, string targetPath, DragDropEffects effects, bool isInternalMove = false)
        {
            SourceFiles = sourceFiles;
            TargetPath = targetPath;
            Effects = effects;
            IsInternalMove = isInternalMove;
        }
    }

    /// <summary>
    /// Event arguments for files moved internally
    /// </summary>
    public class FilesMoved : EventArgs
    {
        public string[] SourceFiles { get; }
        public string[] SourceDirectories { get; }
        public string TargetPath { get; }

        public FilesMoved(string[] sourceFiles, string[] sourceDirectories, string targetPath)
        {
            SourceFiles = sourceFiles;
            SourceDirectories = sourceDirectories;
            TargetPath = targetPath;
        }
    }
}