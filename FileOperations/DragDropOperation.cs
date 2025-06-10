using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.FileOperations
{
    /// <summary>
    /// Represents a reversible file action that can be executed and rolled back
    /// </summary>
    public interface IReversibleAction
    {
        Task ExecuteAsync(CancellationToken cancellationToken = default);
        Task RollbackAsync(CancellationToken cancellationToken = default);
        string Description { get; }
    }

    /// <summary>
    /// A copy file action that can be rolled back by deleting the copied file
    /// </summary>
    public class CopyFileAction : IReversibleAction
    {
        private readonly string _sourcePath;
        private readonly string _targetDirectory;
        private string? _targetPath;

        public string Description => $"Copy {Path.GetFileName(_sourcePath)} to {_targetDirectory}";

        public CopyFileAction(string sourcePath, string targetDirectory)
        {
            _sourcePath = sourcePath;
            _targetDirectory = targetDirectory;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(_sourcePath);
            _targetPath = Path.Combine(_targetDirectory, fileName);

            // Handle name conflicts
            if (File.Exists(_targetPath))
            {
                _targetPath = GetUniqueFileName(_targetPath);
            }

            await Task.Run(() => File.Copy(_sourcePath, _targetPath), cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(_targetPath) && File.Exists(_targetPath))
            {
                await Task.Run(() => File.Delete(_targetPath), cancellationToken);
            }
        }

        private static string GetUniqueFileName(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath) ?? "";
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter}){extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }
    }

    /// <summary>
    /// A move file action that can be rolled back by moving the file back
    /// </summary>
    public class MoveFileAction : IReversibleAction
    {
        private readonly string _sourcePath;
        private readonly string _targetDirectory;
        private string? _targetPath;

        public string Description => $"Move {Path.GetFileName(_sourcePath)} to {_targetDirectory}";

        public MoveFileAction(string sourcePath, string targetDirectory)
        {
            _sourcePath = sourcePath;
            _targetDirectory = targetDirectory;
        }

        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var fileName = Path.GetFileName(_sourcePath);
            _targetPath = Path.Combine(_targetDirectory, fileName);

            // Handle name conflicts
            if (File.Exists(_targetPath))
            {
                _targetPath = GetUniqueFileName(_targetPath);
            }

            await Task.Run(() => File.Move(_sourcePath, _targetPath), cancellationToken);
        }

        public async Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            if (!string.IsNullOrEmpty(_targetPath) && File.Exists(_targetPath))
            {
                await Task.Run(() => File.Move(_targetPath, _sourcePath), cancellationToken);
            }
        }

        private static string GetUniqueFileName(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath) ?? "";
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var counter = 1;

            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileNameWithoutExtension} ({counter}){extension}");
                counter++;
            } while (File.Exists(newPath));

            return newPath;
        }
    }

    /// <summary>
    /// Represents a drag & drop operation with rollback support and progress tracking
    /// </summary>
    public class DragDropOperation : IDisposable
    {
        private readonly List<IReversibleAction> _completedActions = new();
        private readonly ILogger? _logger;
        private bool _disposed;

        /// <summary>
        /// Gets the drag & drop effect for this operation
        /// </summary>
        public DragDropEffects Effect { get; private set; }

        /// <summary>
        /// Gets the target directory path
        /// </summary>
        public string TargetPath { get; private set; }

        /// <summary>
        /// Gets the source file paths
        /// </summary>
        public IReadOnlyList<string> SourcePaths { get; private set; }

        /// <summary>
        /// Gets the total estimated size of the operation
        /// </summary>
        public long EstimatedSize { get; private set; }

        /// <summary>
        /// Gets whether this is a large operation that should show progress
        /// </summary>
        public bool IsLargeOperation => SourcePaths.Count > 10 || EstimatedSize > 100 * 1024 * 1024; // 100MB

        /// <summary>
        /// Event raised when progress is updated
        /// </summary>
        public event EventHandler<ProgressEventArgs>? ProgressUpdated;

        /// <summary>
        /// Initializes a new drag & drop operation
        /// </summary>
        public DragDropOperation(
            DragDropEffects effect, 
            string targetPath, 
            IEnumerable<string> sourcePaths, 
            ILogger? logger = null)
        {
            Effect = effect;
            TargetPath = targetPath ?? throw new ArgumentNullException(nameof(targetPath));
            SourcePaths = sourcePaths?.ToList() ?? throw new ArgumentNullException(nameof(sourcePaths));
            _logger = logger;

            // Calculate estimated size
            EstimatedSize = CalculateEstimatedSize();
        }

        /// <summary>
        /// Executes the drag & drop operation
        /// </summary>
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(DragDropOperation));

            _logger?.LogInformation($"Starting {Effect} operation with {SourcePaths.Count} items to {TargetPath}");

            var totalItems = SourcePaths.Count;
            var completedItems = 0;

            try
            {
                foreach (var sourcePath in SourcePaths)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    IReversibleAction action = Effect == DragDropEffects.Move
                        ? new MoveFileAction(sourcePath, TargetPath)
                        : new CopyFileAction(sourcePath, TargetPath);

                    try
                    {
                        _logger?.LogDebug($"Executing: {action.Description}");
                        await action.ExecuteAsync(cancellationToken);
                        _completedActions.Add(action);

                        completedItems++;
                        var progress = (double)completedItems / totalItems * 100;
                        OnProgressUpdated(new ProgressEventArgs(progress, action.Description, completedItems, totalItems));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Failed to execute: {action.Description}");
                        await RollbackAsync();
                        throw new DragDropOperationException($"Failed to {Effect.ToString().ToLower()} {Path.GetFileName(sourcePath)}: {ex.Message}", ex);
                    }
                }

                _logger?.LogInformation($"Successfully completed {Effect} operation");
            }
            catch (OperationCanceledException)
            {
                _logger?.LogInformation("Drag & drop operation was cancelled");
                await RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// Rolls back all completed actions
        /// </summary>
        public async Task RollbackAsync()
        {
            if (_disposed)
                return;

            _logger?.LogInformation($"Rolling back {_completedActions.Count} completed actions");

            foreach (var action in _completedActions.AsEnumerable().Reverse())
            {
                try
                {
                    _logger?.LogDebug($"Rolling back: {action.Description}");
                    await action.RollbackAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Failed to rollback: {action.Description}");
                }
            }

            _completedActions.Clear();
            _logger?.LogInformation("Rollback completed");
        }

        /// <summary>
        /// Calculates the estimated size of all source files
        /// </summary>
        private long CalculateEstimatedSize()
        {
            long totalSize = 0;
            foreach (var path in SourcePaths)
            {
                try
                {
                    if (File.Exists(path))
                    {
                        var fileInfo = new FileInfo(path);
                        totalSize += fileInfo.Length;
                    }
                    else if (Directory.Exists(path))
                    {
                        // For directories, we'll estimate based on file count
                        var fileCount = Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
                        totalSize += fileCount * 1024 * 1024; // Assume 1MB per file average
                    }
                }
                catch
                {
                    // Ignore errors when calculating size
                }
            }
            return totalSize;
        }

        /// <summary>
        /// Raises the ProgressUpdated event
        /// </summary>
        protected virtual void OnProgressUpdated(ProgressEventArgs e)
        {
            ProgressUpdated?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the operation and rolls back if needed
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                // Note: We don't automatically rollback on dispose
                // This should be done explicitly if needed
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// Event arguments for progress updates
    /// </summary>
    public class ProgressEventArgs : EventArgs
    {
        public double ProgressPercentage { get; }
        public string CurrentAction { get; }
        public int CompletedItems { get; }
        public int TotalItems { get; }

        public ProgressEventArgs(double progressPercentage, string currentAction, int completedItems, int totalItems)
        {
            ProgressPercentage = progressPercentage;
            CurrentAction = currentAction;
            CompletedItems = completedItems;
            TotalItems = totalItems;
        }
    }

    /// <summary>
    /// Exception thrown when a drag & drop operation fails
    /// </summary>
    public class DragDropOperationException : Exception
    {
        public DragDropOperationException(string message) : base(message) { }
        public DragDropOperationException(string message, Exception innerException) : base(message, innerException) { }
    }
} 