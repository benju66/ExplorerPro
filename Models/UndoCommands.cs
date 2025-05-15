using System;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ExplorerPro.FileOperations;
using ExplorerPro.UI.FileTree;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Command for renaming a file or folder.
    /// </summary>
    public class RenameCommand : Command
    {
        private readonly string _oldPath;
        private string _newPath;
        private readonly string _newName;
        private readonly IFileOperations _fileOperations;
        private readonly ILogger<RenameCommand> _logger;

        /// <summary>
        /// Gets the new path after the rename operation.
        /// </summary>
        public string NewPath => _newPath;

        /// <summary>
        /// Creates a command for renaming an item.
        /// </summary>
        /// <param name="fileOperations">File operations service.</param>
        /// <param name="oldPath">The original full path of the item to rename.</param>
        /// <param name="newName">The new name for the item (without path).</param>
        /// <param name="logger">Logger for operation tracking.</param>
        public RenameCommand(IFileOperations fileOperations, string oldPath, string newName, ILogger<RenameCommand> logger = null)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _oldPath = oldPath ?? throw new ArgumentNullException(nameof(oldPath));
            _newName = newName ?? throw new ArgumentNullException(nameof(newName));
            _logger = logger;
        }

        /// <summary>
        /// Execute the rename operation.
        /// </summary>
        public override void Execute()
        {
            _logger?.LogInformation($"Executing rename of '{_oldPath}' to '{_newName}'");
            _newPath = _fileOperations.RenameItem(_oldPath, _newName);
            if (string.IsNullOrEmpty(_newPath))
            {
                _logger?.LogWarning($"Failed to rename '{_oldPath}' to '{_newName}'");
            }
        }

        /// <summary>
        /// Undo the rename operation by renaming back to the original name.
        /// </summary>
        public override void Undo()
        {
            if (!string.IsNullOrEmpty(_newPath))
            {
                _logger?.LogInformation($"Undoing rename: renaming '{_newPath}' back to original name");
                string oldName = Path.GetFileName(_oldPath);
                _fileOperations.RenameItem(_newPath, oldName);
            }
        }
    }

    /// <summary>
    /// Command for creating a new file.
    /// </summary>
    public class CreateFileCommand : Command
    {
        private readonly IFileTree _fileTree;
        private readonly string _parentDir;
        private readonly string _fileName;
        private string _createdPath;
        private readonly IFileOperations _fileOperations;
        private readonly ILogger<CreateFileCommand> _logger;

        /// <summary>
        /// Creates a command for creating a new file.
        /// </summary>
        /// <param name="fileOperations">File operations service.</param>
        /// <param name="fileTree">The file tree to refresh.</param>
        /// <param name="parentDir">The directory in which to create the file.</param>
        /// <param name="fileName">The name of the new file.</param>
        /// <param name="logger">Logger for operation tracking.</param>
        public CreateFileCommand(IFileOperations fileOperations, IFileTree fileTree, string parentDir, string fileName, ILogger<CreateFileCommand> logger = null)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _fileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
            _parentDir = parentDir ?? throw new ArgumentNullException(nameof(parentDir));
            _fileName = fileName ?? throw new ArgumentNullException(nameof(fileName));
            _logger = logger;
        }

        /// <summary>
        /// Execute the file creation operation.
        /// </summary>
        public override void Execute()
        {
            _logger?.LogInformation($"Creating new file '{_fileName}' in '{_parentDir}'");
            _createdPath = _fileOperations.CreateNewFile(_parentDir, _fileName);
            
            if (!string.IsNullOrEmpty(_createdPath))
            {
                _logger?.LogInformation($"Created file at '{_createdPath}'");
                // Refresh the FileTree to show the new file
                _fileTree.SetRootDirectory(_parentDir);
            }
            else
            {
                _logger?.LogWarning($"Failed to create file '{_fileName}' in '{_parentDir}'");
            }
        }

        /// <summary>
        /// Undo the file creation by deleting the file.
        /// </summary>
        public override void Undo()
        {
            if (!string.IsNullOrEmpty(_createdPath) && File.Exists(_createdPath))
            {
                _logger?.LogInformation($"Undoing file creation: deleting '{_createdPath}'");
                bool success = _fileOperations.DeleteItem(_createdPath);
                
                if (success)
                {
                    _fileTree.SetRootDirectory(_parentDir);
                }
                else
                {
                    _logger?.LogWarning($"Failed to delete file '{_createdPath}' during undo operation");
                }
            }
        }
    }

    /// <summary>
    /// Command for creating a new folder.
    /// </summary>
    public class CreateFolderCommand : Command
    {
        private readonly IFileTree _fileTree;
        private readonly string _parentDir;
        private readonly string _folderName;
        private string _createdPath;
        private readonly IFileOperations _fileOperations;
        private readonly ILogger<CreateFolderCommand> _logger;

        /// <summary>
        /// Creates a command for creating a new folder.
        /// </summary>
        /// <param name="fileOperations">File operations service.</param>
        /// <param name="fileTree">The file tree to refresh.</param>
        /// <param name="parentDir">The directory in which to create the folder.</param>
        /// <param name="folderName">The name of the new folder.</param>
        /// <param name="logger">Logger for operation tracking.</param>
        public CreateFolderCommand(IFileOperations fileOperations, IFileTree fileTree, string parentDir, string folderName, ILogger<CreateFolderCommand> logger = null)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _fileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
            _parentDir = parentDir ?? throw new ArgumentNullException(nameof(parentDir));
            _folderName = folderName ?? throw new ArgumentNullException(nameof(folderName));
            _logger = logger;
        }

        /// <summary>
        /// Execute the folder creation operation.
        /// </summary>
        public override void Execute()
        {
            _logger?.LogInformation($"Creating new folder '{_folderName}' in '{_parentDir}'");
            _createdPath = _fileOperations.CreateNewFolder(_parentDir, _folderName);
            
            if (!string.IsNullOrEmpty(_createdPath))
            {
                _logger?.LogInformation($"Created folder at '{_createdPath}'");
                _fileTree.SetRootDirectory(_parentDir);
            }
            else
            {
                _logger?.LogWarning($"Failed to create folder '{_folderName}' in '{_parentDir}'");
            }
        }

        /// <summary>
        /// Undo the folder creation by deleting the folder.
        /// </summary>
        public override void Undo()
        {
            if (!string.IsNullOrEmpty(_createdPath) && Directory.Exists(_createdPath))
            {
                _logger?.LogInformation($"Undoing folder creation: deleting '{_createdPath}'");
                bool success = _fileOperations.DeleteItem(_createdPath);
                
                if (success)
                {
                    _fileTree.SetRootDirectory(_parentDir);
                }
                else
                {
                    _logger?.LogWarning($"Failed to delete folder '{_createdPath}' during undo operation");
                }
            }
        }
    }

    /// <summary>
    /// Command for deleting a file or folder.
    /// Note: This implementation does not support true undo of deletion.
    /// For a real undo, consider implementing a recycle bin mechanism.
    /// </summary>
    public class DeleteItemCommand : Command
    {
        private readonly IFileTree _fileTree;
        private readonly string _targetPath;
        private readonly string _parentDir;
        private bool _wasDeleted;
        private readonly IFileOperations _fileOperations;
        private readonly ILogger<DeleteItemCommand> _logger;

        /// <summary>
        /// Creates a command for deleting an item.
        /// </summary>
        /// <param name="fileOperations">File operations service.</param>
        /// <param name="fileTree">The file tree to refresh.</param>
        /// <param name="targetPath">The path of the item to delete.</param>
        /// <param name="logger">Logger for operation tracking.</param>
        public DeleteItemCommand(IFileOperations fileOperations, IFileTree fileTree, string targetPath, ILogger<DeleteItemCommand> logger = null)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _fileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
            _targetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
            _parentDir = Path.GetDirectoryName(targetPath);
            _logger = logger;
        }

        /// <summary>
        /// Execute the deletion operation.
        /// </summary>
        public override void Execute()
        {
            if (File.Exists(_targetPath) || Directory.Exists(_targetPath))
            {
                _logger?.LogInformation($"Deleting item '{_targetPath}'");
                _wasDeleted = _fileOperations.DeleteItem(_targetPath);
                
                if (!_wasDeleted)
                {
                    _logger?.LogWarning($"Failed to delete item '{_targetPath}'");
                }
            }
            else
            {
                _logger?.LogWarning($"Item '{_targetPath}' does not exist, cannot delete");
            }
            
            _fileTree.SetRootDirectory(_parentDir);
        }

        /// <summary>
        /// Undo the deletion operation.
        /// Note: This implementation can't truly restore deleted items.
        /// For a real implementation, consider a recycle bin mechanism instead of true deletion.
        /// </summary>
        public override void Undo()
        {
            // Cannot undo deletion as the file is gone
            // If you want real undo, you must implement a recycle bin mechanism
            // that moves files to a hidden folder instead of truly deleting them
            _logger?.LogInformation($"Cannot undo deletion of '{_targetPath}' - item has been permanently deleted");
        }
    }

    /// <summary>
    /// Command for copying a file or folder.
    /// </summary>
    public class CopyItemCommand : Command
    {
        private readonly IFileTree _fileTree;
        private readonly string _sourcePath;
        private readonly string _destinationDir;
        private string _copiedPath;
        private readonly IFileOperations _fileOperations;
        private readonly ILogger<CopyItemCommand> _logger;

        /// <summary>
        /// Creates a command for copying an item.
        /// </summary>
        /// <param name="fileOperations">File operations service.</param>
        /// <param name="fileTree">The file tree to refresh.</param>
        /// <param name="sourcePath">The path of the item to copy.</param>
        /// <param name="destinationDir">The directory to copy the item to.</param>
        /// <param name="logger">Logger for operation tracking.</param>
        public CopyItemCommand(IFileOperations fileOperations, IFileTree fileTree, string sourcePath, string destinationDir, ILogger<CopyItemCommand> logger = null)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _fileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
            _sourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            _destinationDir = destinationDir ?? throw new ArgumentNullException(nameof(destinationDir));
            _logger = logger;
        }

        /// <summary>
        /// Execute the copy operation.
        /// </summary>
        public override void Execute()
        {
            _logger?.LogInformation($"Copying item from '{_sourcePath}' to '{_destinationDir}'");
            _copiedPath = _fileOperations.CopyItem(_sourcePath, _destinationDir);
            
            if (!string.IsNullOrEmpty(_copiedPath))
            {
                _logger?.LogInformation($"Copied to '{_copiedPath}'");
                _fileTree.SetRootDirectory(_destinationDir);
            }
            else
            {
                _logger?.LogWarning($"Failed to copy item from '{_sourcePath}' to '{_destinationDir}'");
            }
        }

        /// <summary>
        /// Undo the copy operation by deleting the copied item.
        /// </summary>
        public override void Undo()
        {
            if (!string.IsNullOrEmpty(_copiedPath) && (File.Exists(_copiedPath) || Directory.Exists(_copiedPath)))
            {
                _logger?.LogInformation($"Undoing copy: deleting '{_copiedPath}'");
                bool success = _fileOperations.DeleteItem(_copiedPath);
                
                if (success)
                {
                    _fileTree.SetRootDirectory(_destinationDir);
                }
                else
                {
                    _logger?.LogWarning($"Failed to delete copied item '{_copiedPath}' during undo operation");
                }
            }
        }
    }

    /// <summary>
    /// Command for moving a file or folder.
    /// </summary>
    public class MoveItemCommand : Command
    {
        private readonly IFileTree _fileTree;
        private readonly string _sourcePath;
        private readonly string _destinationDir;
        private readonly string _sourceDir;
        private string _destinationPath;
        private bool _wasSuccessful;
        private readonly IFileOperations _fileOperations;
        private readonly ILogger<MoveItemCommand> _logger;

        /// <summary>
        /// Creates a command for moving an item.
        /// </summary>
        /// <param name="fileOperations">File operations service.</param>
        /// <param name="fileTree">The file tree to refresh.</param>
        /// <param name="sourcePath">The path of the item to move.</param>
        /// <param name="destinationDir">The directory to move the item to.</param>
        /// <param name="logger">Logger for operation tracking.</param>
        public MoveItemCommand(IFileOperations fileOperations, IFileTree fileTree, string sourcePath, string destinationDir, ILogger<MoveItemCommand> logger = null)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _fileTree = fileTree ?? throw new ArgumentNullException(nameof(fileTree));
            _sourcePath = sourcePath ?? throw new ArgumentNullException(nameof(sourcePath));
            _destinationDir = destinationDir ?? throw new ArgumentNullException(nameof(destinationDir));
            _sourceDir = Path.GetDirectoryName(sourcePath);
            _logger = logger;
        }

        /// <summary>
        /// Execute the move operation.
        /// </summary>
        public override void Execute()
        {
            _logger?.LogInformation($"Moving item from '{_sourcePath}' to '{_destinationDir}'");
            _destinationPath = Path.Combine(_destinationDir, Path.GetFileName(_sourcePath));
            _wasSuccessful = _fileOperations.MoveItem(_sourcePath, _destinationDir);
            
            if (_wasSuccessful)
            {
                _logger?.LogInformation($"Moved to '{_destinationPath}'");
                _fileTree.SetRootDirectory(_destinationDir);
            }
            else
            {
                _logger?.LogWarning($"Failed to move item from '{_sourcePath}' to '{_destinationDir}'");
            }
        }

        /// <summary>
        /// Undo the move operation by moving the item back to its original location.
        /// </summary>
        public override void Undo()
        {
            if (_wasSuccessful && (File.Exists(_destinationPath) || Directory.Exists(_destinationPath)))
            {
                _logger?.LogInformation($"Undoing move: moving '{_destinationPath}' back to '{_sourceDir}'");
                bool success = _fileOperations.MoveItem(_destinationPath, _sourceDir);
                
                if (success)
                {
                    _fileTree.SetRootDirectory(_sourceDir);
                }
                else
                {
                    _logger?.LogWarning($"Failed to move item '{_destinationPath}' back to '{_sourceDir}' during undo operation");
                }
            }
        }
    }
}