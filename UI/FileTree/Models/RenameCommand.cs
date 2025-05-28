// Models/RenameCommand.cs
using System;
using System.IO;
using ExplorerPro.FileOperations;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Command for undoable rename operations
    /// </summary>
    public class RenameCommand : Command
    {
        private readonly IFileOperations _fileOperations;
        private readonly string _oldPath;
        private readonly string _newName;
        private readonly ILogger<RenameCommand> _logger;
        
        /// <summary>
        /// Gets the new path after rename (null if rename failed)
        /// </summary>
        public string NewPath { get; private set; }
        
        /// <summary>
        /// Gets whether the operation was successful
        /// </summary>
        public bool Success { get; private set; }
        
        public RenameCommand(IFileOperations fileOperations, string oldPath, string newName, ILogger<RenameCommand> logger = null)
        {
            _fileOperations = fileOperations ?? throw new ArgumentNullException(nameof(fileOperations));
            _oldPath = oldPath ?? throw new ArgumentNullException(nameof(oldPath));
            _newName = newName ?? throw new ArgumentNullException(nameof(newName));
            _logger = logger;
            Success = false;
        }
        
        public override void Execute()
        {
            try
            {
                _logger?.LogInformation($"Executing rename: '{_oldPath}' to '{_newName}'");
                
                // Validate input
                if (string.IsNullOrWhiteSpace(_newName))
                {
                    throw new ArgumentException("New name cannot be empty");
                }
                
                if (!File.Exists(_oldPath) && !Directory.Exists(_oldPath))
                {
                    throw new FileNotFoundException($"Source path not found: {_oldPath}");
                }
                
                // Perform rename
                NewPath = _fileOperations.RenameItem(_oldPath, _newName);
                Success = !string.IsNullOrEmpty(NewPath);
                
                if (Success)
                {
                    _logger?.LogInformation($"Rename successful: '{_oldPath}' -> '{NewPath}'");
                }
                else
                {
                    _logger?.LogWarning($"Rename failed: '{_oldPath}' to '{_newName}'");
                    throw new InvalidOperationException("Rename operation failed");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error renaming '{_oldPath}' to '{_newName}'");
                Success = false;
                NewPath = null;
                throw;
            }
        }
        
        public override void Undo()
        {
            if (!Success || string.IsNullOrEmpty(NewPath))
            {
                _logger?.LogWarning("Cannot undo rename - operation was not successful");
                return;
            }
            
            try
            {
                _logger?.LogInformation($"Undoing rename: '{NewPath}' back to '{Path.GetFileName(_oldPath)}'");
                
                // Get the original name
                string originalName = Path.GetFileName(_oldPath);
                
                // Rename back to original
                string restoredPath = _fileOperations.RenameItem(NewPath, originalName);
                
                if (string.IsNullOrEmpty(restoredPath))
                {
                    throw new InvalidOperationException("Failed to undo rename");
                }
                
                // Clear the new path since we've undone the operation
                NewPath = null;
                Success = false;
                
                _logger?.LogInformation($"Undo rename successful");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error undoing rename from '{NewPath}' to original");
                throw;
            }
        }
    }
}