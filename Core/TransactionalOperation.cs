using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Provides transaction-like semantics for operations with rollback support.
    /// </summary>
    public class TransactionalOperation<TState> where TState : class, new()
    {
        private readonly ILogger<TransactionalOperation<TState>> _logger;
        private readonly Stack<IUndoableAction> _completedActions;
        private readonly TState _state;
        private readonly TState _originalState;
        private bool _isCommitted;
        private bool _isRolledBack;
        
        public TransactionalOperation(ILogger<TransactionalOperation<TState>> logger, TState initialState = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _state = initialState ?? new TState();
            _originalState = CloneState(_state);
            _completedActions = new Stack<IUndoableAction>();
        }
        
        /// <summary>
        /// Gets the current state of the operation.
        /// </summary>
        public TState State => _state;
        
        /// <summary>
        /// Executes an action within the transaction.
        /// </summary>
        public void Execute(IUndoableAction action)
        {
            EnsureNotFinalized();
            
            try
            {
                _logger.LogDebug($"Executing action: {action.Name}");
                
                action.Execute(_state);
                _completedActions.Push(action);
                
                _logger.LogDebug($"Action completed: {action.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Action failed: {action.Name}");
                throw new TransactionExecutionException($"Failed to execute action: {action.Name}", ex);
            }
        }
        
        /// <summary>
        /// Executes an async action within the transaction.
        /// </summary>
        public async Task ExecuteAsync(IAsyncUndoableAction action)
        {
            EnsureNotFinalized();
            
            try
            {
                _logger.LogDebug($"Executing async action: {action.Name}");
                
                await action.ExecuteAsync(_state);
                _completedActions.Push(action);
                
                _logger.LogDebug($"Async action completed: {action.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Async action failed: {action.Name}");
                throw new TransactionExecutionException($"Failed to execute async action: {action.Name}", ex);
            }
        }
        
        /// <summary>
        /// Commits all actions in the transaction.
        /// </summary>
        public void Commit()
        {
            EnsureNotFinalized();
            
            _logger.LogInformation("Committing transaction");
            _isCommitted = true;
            
            // Clear undo stack as we've committed
            _completedActions.Clear();
        }
        
        /// <summary>
        /// Rolls back all completed actions in reverse order.
        /// </summary>
        public async Task RollbackAsync()
        {
            if (_isCommitted)
            {
                throw new InvalidOperationException("Cannot rollback a committed transaction");
            }
            
            if (_isRolledBack)
            {
                _logger.LogWarning("Transaction already rolled back");
                return;
            }
            
            _logger.LogWarning($"Rolling back transaction with {_completedActions.Count} actions");
            
            var rollbackErrors = new List<Exception>();
            
            while (_completedActions.Count > 0)
            {
                var action = _completedActions.Pop();
                
                try
                {
                    _logger.LogDebug($"Rolling back action: {action.Name}");
                    
                    if (action is IAsyncUndoableAction asyncAction)
                    {
                        await asyncAction.UndoAsync(_state);
                    }
                    else
                    {
                        action.Undo(_state);
                    }
                    
                    _logger.LogDebug($"Successfully rolled back: {action.Name}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to rollback action: {action.Name}");
                    rollbackErrors.Add(ex);
                }
            }
            
            // Restore original state as final fallback
            RestoreOriginalState();
            
            _isRolledBack = true;
            
            if (rollbackErrors.Count > 0)
            {
                throw new AggregateException("One or more rollback operations failed", rollbackErrors);
            }
            
            _logger.LogInformation("Transaction rolled back successfully");
        }
        
        /// <summary>
        /// Executes a complete transactional operation with automatic rollback on failure.
        /// </summary>
        public static async Task<TResult> RunAsync<TResult>(
            ILogger<TransactionalOperation<TState>> logger,
            Func<TransactionalOperation<TState>, Task<TResult>> operation,
            TState initialState = null)
        {
            var transaction = new TransactionalOperation<TState>(logger, initialState);
            
            try
            {
                var result = await operation(transaction);
                transaction.Commit();
                return result;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Transaction failed, initiating rollback");
                
                try
                {
                    await transaction.RollbackAsync();
                }
                catch (Exception rollbackEx)
                {
                    logger.LogCritical(rollbackEx, "Rollback failed");
                }
                
                throw;
            }
        }
        
        #region Private Methods
        
        private void EnsureNotFinalized()
        {
            if (_isCommitted || _isRolledBack)
            {
                throw new InvalidOperationException("Transaction has already been finalized");
            }
        }
        
        private TState CloneState(TState state)
        {
            // Implement deep cloning based on your state type
            // This is a simplified version - implement proper cloning for your needs
            var json = System.Text.Json.JsonSerializer.Serialize(state);
            return System.Text.Json.JsonSerializer.Deserialize<TState>(json);
        }
        
        private void RestoreOriginalState()
        {
            // Copy properties from original state back to current state
            // This is a simplified version - implement based on your state type
            var properties = typeof(TState).GetProperties();
            foreach (var prop in properties)
            {
                if (prop.CanWrite && prop.CanRead)
                {
                    prop.SetValue(_state, prop.GetValue(_originalState));
                }
            }
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    /// <summary>
    /// Represents an action that can be undone.
    /// </summary>
    public interface IUndoableAction
    {
        string Name { get; }
        void Execute(object state);
        void Undo(object state);
    }
    
    /// <summary>
    /// Represents an async action that can be undone.
    /// </summary>
    public interface IAsyncUndoableAction : IUndoableAction
    {
        Task ExecuteAsync(object state);
        Task UndoAsync(object state);
    }
    
    /// <summary>
    /// Exception thrown when a transactional operation fails.
    /// </summary>
    public class TransactionExecutionException : Exception
    {
        public TransactionExecutionException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
    
    #endregion
} 