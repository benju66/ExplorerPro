// Commands/DragCopyCommand.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ExplorerPro.FileOperations;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree;

namespace ExplorerPro.Commands
{
    /// <summary>
    /// Command for undoable drag-and-drop copy operations
    /// </summary>
    public class DragCopyCommand : Command
    {
        private readonly IFileOperations _fileOperations;
        private readonly List<CopyOperation> _operations;
        private readonly IFileTree _fileTree;
        private bool _executed;

        /// <summary>
        /// Represents a single copy operation
        /// </summary>
        private class CopyOperation
        {
            public string SourcePath { get; set; }
            public string DestinationPath { get; set; }
            public bool IsDirectory { get; set; }
            public bool Completed { get; set; }
        }

        /// <summary>
        /// Creates a new drag copy command
        /// </summary>
        /// <param name="fileOperations">File operations service</param>
        /// <param name="fileTree">File tree to refresh after operation</param>
        /// <param name="items">Items to copy</param>
        /// <param name="targetDirectory">Target directory path</param>
        public DragCopyCommand(IFileOperations fileOperations, IFileTree fileTree,
            IEnumerable<FileTreeItem> items, string targetDirectory)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _fileTree = fileTree;
            _operations = new List<CopyOperation>();

            if (items == null || !items.Any())
                throw new ArgumentException("No items to copy", nameof(items));

            if (string.IsNullOrEmpty(targetDirectory) || !Directory.Exists(targetDirectory))
                throw new ArgumentException("Invalid target directory", nameof(targetDirectory));

            // Build operations list
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Path))
                    continue;

                string fileName = Path.GetFileName(item.Path);
                string destPath = GetUniqueDestinationPath(targetDirectory, fileName);

                _operations.Add(new CopyOperation
                {
                    SourcePath = item.Path,
                    DestinationPath = destPath,
                    IsDirectory = item.IsDirectory,
                    Completed = false
                });
            }

            if (!_operations.Any())
                throw new InvalidOperationException("No valid copy operations");
        }

        /// <summary>
        /// Executes the copy operations
        /// </summary>
        public override void Execute()
        {
            if (_executed)
                throw new InvalidOperationException("Command has already been executed");

            var errors = new List<string>();
            var affectedDirectories = new HashSet<string>();

            foreach (var operation in _operations)
            {
                try
                {
                    // Perform the copy
                    if (operation.IsDirectory)
                    {
                        CopyDirectory(operation.SourcePath, operation.DestinationPath);
                    }
                    else
                    {
                        File.Copy(operation.SourcePath, operation.DestinationPath, false);
                    }

                    operation.Completed = true;

                    // Add destination directory to refresh list
                    string destDir = Path.GetDirectoryName(operation.DestinationPath);
                    if (!string.IsNullOrEmpty(destDir))
                        affectedDirectories.Add(destDir);
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to copy '{Path.GetFileName(operation.SourcePath)}': {ex.Message}");
                }
            }

            _executed = true;

            // Refresh affected directories
            RefreshDirectories(affectedDirectories);

            if (errors.Any())
            {
                throw new AggregateException("Some copy operations failed",
                    errors.Select(e => new Exception(e)));
            }
        }

        /// <summary>
        /// Undoes the copy operations by deleting the copied items
        /// </summary>
        public override void Undo()
        {
            if (!_executed)
                throw new InvalidOperationException("Command has not been executed");

            var errors = new List<string>();
            var affectedDirectories = new HashSet<string>();

            // Undo in reverse order
            foreach (var operation in _operations.Where(o => o.Completed).Reverse())
            {
                try
                {
                    // Add destination directory to refresh list
                    string destDir = Path.GetDirectoryName(operation.DestinationPath);
                    if (!string.IsNullOrEmpty(destDir))
                        affectedDirectories.Add(destDir);

                    // Delete the copied item
                    if (operation.IsDirectory)
                    {
                        if (Directory.Exists(operation.DestinationPath))
                            Directory.Delete(operation.DestinationPath, true);
                    }
                    else
                    {
                        if (File.Exists(operation.DestinationPath))
                            File.Delete(operation.DestinationPath);
                    }

                    operation.Completed = false;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to undo copy of '{Path.GetFileName(operation.SourcePath)}': {ex.Message}");
                }
            }

            _executed = false;

            // Refresh affected directories
            RefreshDirectories(affectedDirectories);

            if (errors.Any())
            {
                throw new AggregateException("Some undo operations failed",
                    errors.Select(e => new Exception(e)));
            }
        }

        /// <summary>
        /// Gets a unique destination path by appending a number if necessary
        /// </summary>
        private string GetUniqueDestinationPath(string targetDirectory, string fileName)
        {
            string destPath = Path.Combine(targetDirectory, fileName);
            
            if (!File.Exists(destPath) && !Directory.Exists(destPath))
                return destPath;

            // Generate unique name
            string nameWithoutExt = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            int counter = 1;

            do
            {
                string newName = $"{nameWithoutExt} ({counter}){extension}";
                destPath = Path.Combine(targetDirectory, newName);
                counter++;
            }
            while (File.Exists(destPath) || Directory.Exists(destPath));

            return destPath;
        }

        /// <summary>
        /// Recursively copies a directory and its contents
        /// </summary>
        private void CopyDirectory(string sourcePath, string destPath)
        {
            // Create directory
            Directory.CreateDirectory(destPath);

            // Copy files
            foreach (string file in Directory.GetFiles(sourcePath))
            {
                string fileName = Path.GetFileName(file);
                string destFile = Path.Combine(destPath, fileName);
                File.Copy(file, destFile, false);
            }

            // Copy subdirectories
            foreach (string dir in Directory.GetDirectories(sourcePath))
            {
                string dirName = Path.GetFileName(dir);
                string destDir = Path.Combine(destPath, dirName);
                CopyDirectory(dir, destDir);
            }

            // Copy attributes
            var sourceInfo = new DirectoryInfo(sourcePath);
            var destInfo = new DirectoryInfo(destPath);
            destInfo.Attributes = sourceInfo.Attributes;
            destInfo.CreationTime = sourceInfo.CreationTime;
            destInfo.LastWriteTime = sourceInfo.LastWriteTime;
        }

        /// <summary>
        /// Refreshes the specified directories in the file tree
        /// </summary>
        private void RefreshDirectories(IEnumerable<string> directories)
        {
            if (_fileTree == null)
                return;

            foreach (var dir in directories.Distinct())
            {
                try
                {
                    _fileTree.RefreshView();
                }
                catch
                {
                    // Ignore refresh errors
                }
            }
        }
    }
}