// UI/FileTree/Services/IFileTreeDragDropService.cs (UPDATED interface)
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using ExplorerPro.Models;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Interface for file tree drag and drop operations with enhanced Outlook support
    /// </summary>
    public interface IFileTreeDragDropService
    {
        #region Events

        /// <summary>
        /// Raised when files are successfully dropped or moved
        /// </summary>
        event EventHandler<FilesDroppedEventArgs> FilesDropped;

        /// <summary>
        /// Raised when files are moved internally (need source refresh)
        /// </summary>
        event EventHandler<FilesMoved> FilesMoved;

        /// <summary>
        /// Raised when an error occurs during drag/drop operations
        /// </summary>
        event EventHandler<string> ErrorOccurred;

        /// <summary>
        /// Event raised when Outlook extraction is completed
        /// </summary>
        event EventHandler<OutlookExtractionCompletedEventArgs> OutlookExtractionCompleted;

        #endregion

        #region Methods

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
        /// Handles Outlook item drops (emails, attachments) synchronously
        /// </summary>
        /// <param name="dataObject">Data object from Outlook</param>
        /// <param name="targetPath">Target directory path</param>
        /// <returns>True if handled successfully</returns>
        bool HandleOutlookDrop(DataObject dataObject, string targetPath);

        /// <summary>
        /// Handles Outlook item drops (emails, attachments) asynchronously
        /// </summary>
        /// <param name="dataObject">Data object from Outlook</param>
        /// <param name="targetPath">Target directory path</param>
        /// <returns>Task with true if handled successfully</returns>
        Task<bool> HandleOutlookDropAsync(DataObject dataObject, string targetPath);

        /// <summary>
        /// Cancels the current Outlook extraction operation
        /// </summary>
        void CancelOutlookExtraction();

        /// <summary>
        /// Validates a drop operation before execution
        /// </summary>
        /// <param name="data">Data object being dropped</param>
        /// <param name="targetPath">Target directory path</param>
        /// <param name="keyStates">Current key states (Ctrl, Shift, etc.)</param>
        /// <returns>Validation result indicating whether the drop is valid</returns>
        DragDropValidationResult ValidateDrop(IDataObject data, string targetPath, DragDropKeyStates keyStates);

        /// <summary>
        /// Validates a specific file path for dropping
        /// </summary>
        /// <param name="sourcePath">Source file or directory path</param>
        /// <param name="targetPath">Target directory path</param>
        /// <returns>True if the path can be dropped to the target</returns>
        bool ValidateDropPath(string sourcePath, string targetPath);

        /// <summary>
        /// Determines the appropriate drop effect based on key states and source/target
        /// </summary>
        /// <param name="keyStates">Current key states</param>
        /// <param name="targetPath">Target directory path</param>
        /// <returns>The appropriate drag drop effect</returns>
        DragDropEffects DetermineDropEffect(DragDropKeyStates keyStates, string targetPath);

        /// <summary>
        /// Extracts file paths from a data object
        /// </summary>
        /// <param name="data">Data object to extract paths from</param>
        /// <returns>List of extracted file paths</returns>
        List<string> ExtractPaths(IDataObject data);

        #endregion
    }

    #region Event Arguments

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

    /// <summary>
    /// Event arguments for Outlook extraction completion
    /// </summary>
    public class OutlookExtractionCompletedEventArgs : EventArgs
    {
        public OutlookDataExtractor.ExtractionResult Result { get; }
        public string TargetPath { get; }
        
        public OutlookExtractionCompletedEventArgs(OutlookDataExtractor.ExtractionResult result, string targetPath)
        {
            Result = result;
            TargetPath = targetPath;
        }
    }

    #endregion
}