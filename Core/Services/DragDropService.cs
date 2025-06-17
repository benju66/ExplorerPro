using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.MainWindow;
using ExplorerPro.FileOperations;

namespace ExplorerPro.Core.Services
{
    /// <summary>
    /// Service responsible for handling all drag-and-drop operations in ExplorerPro.
    /// Extracted from MainWindow.xaml.cs to improve separation of concerns and testability.
    /// </summary>
    public interface IDragDropService
    {
        /// <summary>
        /// Handles drop events with comprehensive validation and execution
        /// </summary>
        Task<bool> HandleDropAsync(DragEventArgs e, string targetPath);
        
        /// <summary>
        /// Validates a drop operation before execution
        /// </summary>
        DragDropValidationResult ValidateDrop(DragEventArgs e, string targetPath);
        
        /// <summary>
        /// Handles drag over events for visual feedback
        /// </summary>
        void HandleDragOver(DragEventArgs e);
        
        /// <summary>
        /// Estimates the total size of files/directories being dropped
        /// </summary>
        long EstimateDropSize(string[] files);
    }

    /// <summary>
    /// Implementation of IDragDropService
    /// </summary>
    public class DragDropService : IDragDropService
    {
        private readonly ILogger<DragDropService> _logger;
        private readonly IFileOperations _fileOperations;

        public DragDropService(ILogger<DragDropService> logger, IFileOperations fileOperations = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileOperations = fileOperations ?? new FileOperations.FileOperations();
        }

        /// <summary>
        /// Handles drop events with comprehensive validation and execution
        /// </summary>
        public async Task<bool> HandleDropAsync(DragEventArgs e, string targetPath)
        {
            try
            {
                var validation = ValidateDrop(e, targetPath);
                if (!validation.IsValid)
                {
                    ShowDropError(validation.ErrorMessage);
                    e.Effects = DragDropEffects.None;
                    return false;
                }

                // Show confirmation for large operations
                if (validation.RequiresConfirmation)
                {
                    var result = MessageBox.Show(
                        validation.ConfirmationMessage,
                        "Confirm Operation",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        e.Effects = DragDropEffects.None;
                        return false;
                    }
                }

                var operation = CreateDragDropOperation(e, validation, targetPath);
                if (operation != null)
                {
                    // For large operations, show progress dialog
                    if (operation.IsLargeOperation)
                    {
                        await ExecuteOperationWithProgress(operation);
                    }
                    else
                    {
                        await operation.ExecuteAsync();
                    }

                    e.Effects = operation.Effect;
                    return true;
                }
                else
                {
                    // Fallback to existing behavior
                    await HandleDropFallback(e, targetPath);
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Drop operation was cancelled by user");
                e.Effects = DragDropEffects.None;
                return false;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Drop operation failed");
                ShowDropError($"Drop failed: {ex.Message}");
                e.Effects = DragDropEffects.None;
                return false;
            }
        }

        /// <summary>
        /// Validates a drop operation before execution
        /// </summary>
        public DragDropValidationResult ValidateDrop(DragEventArgs e, string targetPath)
        {
            try
            {
                // Check if data is present
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    return DragDropValidationResult.Failure("No file data present");
                }

                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files == null || files.Length == 0)
                {
                    return DragDropValidationResult.Failure("No files to drop");
                }

                // Validate directory access
                if (!Directory.Exists(targetPath))
                {
                    return DragDropValidationResult.Failure("Target directory does not exist");
                }

                // Check write permissions
                try
                {
                    var testFile = Path.Combine(targetPath, $"test_{Guid.NewGuid()}.tmp");
                    File.WriteAllText(testFile, "");
                    File.Delete(testFile);
                }
                catch
                {
                    return DragDropValidationResult.Failure("No write permission to target directory");
                }

                var validation = new DragDropValidationResult { IsValid = true };
                long totalSize = 0;

                foreach (var file in files)
                {
                    if (ValidateDropFile(file, targetPath))
                    {
                        validation.ValidFiles.Add(file);
                        totalSize += EstimateFileSize(file);
                    }
                    else
                    {
                        validation.InvalidFiles.Add(file);
                    }
                }

                if (!validation.ValidFiles.Any())
                {
                    return DragDropValidationResult.Failure("No valid files to drop");
                }

                validation.EstimatedSize = totalSize;
                validation.IsLargeOperation = validation.ValidFiles.Count > 10 || totalSize > 100 * 1024 * 1024;
                validation.AllowedEffects = DragDropEffects.Copy; // Default to copy for main window drops

                if (validation.IsLargeOperation)
                {
                    validation.RequiresConfirmation = true;
                    validation.ConfirmationMessage = $"This will copy {validation.ValidFiles.Count} files ({FormatFileSize(totalSize)}). Continue?";
                }

                return validation;
            }
            catch (Exception ex)
            {
                return DragDropValidationResult.Failure($"Validation error: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles drag over events for visual feedback
        /// </summary>
        public void HandleDragOver(DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            
            e.Handled = true;
        }

        /// <summary>
        /// Estimates the total size of files/directories being dropped
        /// </summary>
        public long EstimateDropSize(string[] files)
        {
            long totalSize = 0;
            foreach (var file in files)
            {
                totalSize += EstimateFileSize(file);
            }
            return totalSize;
        }

        #region Private Helper Methods

        /// <summary>
        /// Validates a single file for dropping
        /// </summary>
        private bool ValidateDropFile(string filePath, string targetPath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath) || (!File.Exists(filePath) && !Directory.Exists(filePath)))
                    return false;

                // Check for circular references
                var fullSourcePath = Path.GetFullPath(filePath);
                var fullTargetPath = Path.GetFullPath(targetPath);

                if (fullTargetPath.StartsWith(fullSourcePath, StringComparison.OrdinalIgnoreCase))
                    return false;

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Estimates the size of a file or directory
        /// </summary>
        private long EstimateFileSize(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).Length;
                }
                else if (Directory.Exists(path))
                {
                    // Rough estimate for directories
                    var fileCount = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
                    return fileCount * 1024 * 1024; // Assume 1MB per file
                }
            }
            catch
            {
                // Ignore errors during estimation
            }

            return 0;
        }

        /// <summary>
        /// Creates a DragDropOperation from validated drop data
        /// </summary>
        private DragDropOperation CreateDragDropOperation(DragEventArgs e, DragDropValidationResult validation, string targetPath)
        {
            try
            {
                var operation = new DragDropOperation(
                    validation.AllowedEffects,
                    targetPath,
                    validation.ValidFiles,
                    _logger);

                return operation;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create drag drop operation");
                return null;
            }
        }

        /// <summary>
        /// Executes a large operation with progress feedback
        /// </summary>
        private async Task ExecuteOperationWithProgress(DragDropOperation operation)
        {
            // TODO: Implement progress dialog
            // For now, just execute the operation
            await operation.ExecuteAsync();
        }

        /// <summary>
        /// Handles drop operations when standard processing fails
        /// </summary>
        private async Task HandleDropFallback(DragEventArgs e, string targetPath)
        {
            try
            {
                var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files != null && files.Length > 0)
                {
                    foreach (var file in files)
                    {
                        var fileName = Path.GetFileName(file);
                        var destinationPath = Path.Combine(targetPath, fileName);
                        
                        if (File.Exists(file))
                        {
                            File.Copy(file, destinationPath, true);
                        }
                        else if (Directory.Exists(file))
                        {
                            // Simple directory copy - would need full implementation
                            _logger?.LogWarning($"Directory drop fallback not fully implemented for: {file}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Drop fallback failed");
                throw;
            }
        }

        /// <summary>
        /// Shows an error message to the user
        /// </summary>
        private void ShowDropError(string message)
        {
            MessageBox.Show(message, "Drop Operation Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        /// <summary>
        /// Formats file size for display
        /// </summary>
        private static string FormatFileSize(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int counter = 0;
            decimal number = bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number /= 1024;
                counter++;
            }
            return $"{number:n1} {suffixes[counter]}";
        }

        #endregion
    }
} 