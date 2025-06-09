using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Interface for async commands that properly handle async operations.
    /// Prevents UI freezes and provides proper cancellation support.
    /// </summary>
    public interface IAsyncCommand : ICommand
    {
        /// <summary>
        /// Executes the command asynchronously.
        /// </summary>
        /// <param name="parameter">Command parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the async operation</returns>
        Task ExecuteAsync(object parameter = null, CancellationToken cancellationToken = default);

        /// <summary>
        /// Cancels the currently executing command if possible.
        /// </summary>
        void Cancel();

        /// <summary>
        /// Indicates whether the command is currently executing.
        /// </summary>
        bool IsExecuting { get; }

        /// <summary>
        /// Event fired when the IsExecuting property changes.
        /// </summary>
        event EventHandler IsExecutingChanged;
    }

    /// <summary>
    /// Generic version of IAsyncCommand with typed parameter.
    /// </summary>
    /// <typeparam name="T">Type of the command parameter</typeparam>
    public interface IAsyncCommand<in T> : IAsyncCommand
    {
        /// <summary>
        /// Executes the command asynchronously with typed parameter.
        /// </summary>
        /// <param name="parameter">Typed command parameter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>A task representing the async operation</returns>
        Task ExecuteAsync(T parameter, CancellationToken cancellationToken = default);
    }
} 