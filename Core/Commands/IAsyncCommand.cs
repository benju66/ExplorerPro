using System;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ExplorerPro.Core.Commands
{
    /// <summary>
    /// Modern async command interface for enterprise-level command execution.
    /// Provides proper async/await support with cancellation and progress reporting.
    /// </summary>
    public interface IAsyncCommand : ICommand
    {
        /// <summary>
        /// Executes the command asynchronously
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        /// <returns>Task representing the async operation</returns>
        Task ExecuteAsync(object parameter);
        
        /// <summary>
        /// Whether the command is currently executing
        /// </summary>
        bool IsExecuting { get; }
        
        /// <summary>
        /// Event raised when execution state changes
        /// </summary>
        event EventHandler<bool> ExecutionStateChanged;
    }
    
    /// <summary>
    /// Generic async command interface with typed parameter
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    public interface IAsyncCommand<in T> : IAsyncCommand
    {
        /// <summary>
        /// Executes the command asynchronously with typed parameter
        /// </summary>
        /// <param name="parameter">Typed command parameter</param>
        /// <returns>Task representing the async operation</returns>
        Task ExecuteAsync(T parameter);
        
        /// <summary>
        /// Determines if the command can execute with the given parameter
        /// </summary>
        /// <param name="parameter">Typed command parameter</param>
        /// <returns>True if command can execute, false otherwise</returns>
        bool CanExecute(T parameter);
    }
} 