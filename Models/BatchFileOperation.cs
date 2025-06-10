using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Represents a batch operation for improved file tree performance.
    /// Supports concurrent execution with progress reporting and error handling.
    /// </summary>
    public class BatchFileOperation
    {
        private readonly List<FileOperation> _operations = new();
        private readonly SemaphoreSlim _semaphore;
        
        public BatchFileOperation(int maxConcurrency = 4)
        {
            _semaphore = new SemaphoreSlim(maxConcurrency);
        }
        
        public void AddOperation(FileOperation operation)
        {
            _operations.Add(operation);
        }
        
        public async Task<BatchOperationResult> ExecuteAsync(
            IProgress<BatchOperationProgress> progress = null,
            CancellationToken cancellationToken = default)
        {
            var result = new BatchOperationResult();
            var completed = 0;
            
            var tasks = _operations.Select(async operation =>
            {
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    await operation.ExecuteAsync(cancellationToken);
                    Interlocked.Increment(ref completed);
                    
                    progress?.Report(new BatchOperationProgress
                    {
                        TotalOperations = _operations.Count,
                        CompletedOperations = completed,
                        CurrentOperation = operation.Description
                    });
                    
                    result.SuccessfulOperations.Add(operation);
                }
                catch (Exception ex)
                {
                    result.FailedOperations.Add((operation, ex));
                }
                finally
                {
                    _semaphore.Release();
                }
            });
            
            await Task.WhenAll(tasks);
            return result;
        }
        
        public void Dispose()
        {
            _semaphore?.Dispose();
        }
    }
    
    /// <summary>
    /// Represents a single file operation within a batch.
    /// </summary>
    public class FileOperation
    {
        public string Description { get; set; }
        public Func<CancellationToken, Task> ExecuteAsync { get; set; }
    }
    
    /// <summary>
    /// Result of a batch operation execution.
    /// </summary>
    public class BatchOperationResult
    {
        public List<FileOperation> SuccessfulOperations { get; } = new();
        public List<(FileOperation Operation, Exception Exception)> FailedOperations { get; } = new();
        
        public bool HasErrors => FailedOperations.Any();
        public int TotalOperations => SuccessfulOperations.Count + FailedOperations.Count;
        public double SuccessRate => TotalOperations > 0 ? (double)SuccessfulOperations.Count / TotalOperations : 0;
    }
    
    /// <summary>
    /// Progress information for batch operations.
    /// </summary>
    public class BatchOperationProgress
    {
        public int TotalOperations { get; set; }
        public int CompletedOperations { get; set; }
        public string CurrentOperation { get; set; }
        public double PercentageComplete => TotalOperations > 0 ? (double)CompletedOperations / TotalOperations * 100 : 0;
    }
} 