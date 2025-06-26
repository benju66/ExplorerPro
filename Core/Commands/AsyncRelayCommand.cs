using System;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.Commands
{
    /// <summary>
    /// Modern async relay command implementation with enterprise-level features.
    /// Provides thread-safe execution, proper error handling, and execution state tracking.
    /// </summary>
    public class AsyncRelayCommand : IAsyncCommand
    {
        private readonly Func<Task> _executeAsync;
        private readonly Func<bool> _canExecute;
        private readonly ILogger _logger;
        private bool _isExecuting;
        private readonly object _executionLock = new object();

        public AsyncRelayCommand(
            Func<Task> executeAsync, 
            Func<bool> canExecute = null,
            ILogger logger = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            _logger = logger;
        }

        public bool IsExecuting
        {
            get
            {
                lock (_executionLock)
                {
                    return _isExecuting;
                }
            }
            private set
            {
                bool changed;
                lock (_executionLock)
                {
                    changed = _isExecuting != value;
                    _isExecuting = value;
                }
                
                if (changed)
                {
                    ExecutionStateChanged?.Invoke(this, value);
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public event EventHandler<bool> ExecutionStateChanged;
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return !IsExecuting && (_canExecute?.Invoke() ?? true);
        }

        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
        }

        public async Task ExecuteAsync(object parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                IsExecuting = true;
                _logger?.LogDebug("Executing async command: {CommandType}", GetType().Name);
                
                await _executeAsync();
                
                _logger?.LogDebug("Successfully executed async command: {CommandType}", GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing async command: {CommandType}", GetType().Name);
                throw;
            }
            finally
            {
                IsExecuting = false;
            }
        }
    }

    /// <summary>
    /// Generic async relay command with typed parameter support
    /// </summary>
    /// <typeparam name="T">Parameter type</typeparam>
    public class AsyncRelayCommand<T> : IAsyncCommand<T>
    {
        private readonly Func<T, Task> _executeAsync;
        private readonly Func<T, bool> _canExecute;
        private readonly ILogger _logger;
        private bool _isExecuting;
        private readonly object _executionLock = new object();

        public AsyncRelayCommand(
            Func<T, Task> executeAsync, 
            Func<T, bool> canExecute = null,
            ILogger logger = null)
        {
            _executeAsync = executeAsync ?? throw new ArgumentNullException(nameof(executeAsync));
            _canExecute = canExecute;
            _logger = logger;
        }

        public bool IsExecuting
        {
            get
            {
                lock (_executionLock)
                {
                    return _isExecuting;
                }
            }
            private set
            {
                bool changed;
                lock (_executionLock)
                {
                    changed = _isExecuting != value;
                    _isExecuting = value;
                }
                
                if (changed)
                {
                    ExecutionStateChanged?.Invoke(this, value);
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public event EventHandler<bool> ExecutionStateChanged;
        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return CanExecute((T)parameter);
        }

        public bool CanExecute(T parameter)
        {
            return !IsExecuting && (_canExecute?.Invoke(parameter) ?? true);
        }

        public async void Execute(object parameter)
        {
            await ExecuteAsync(parameter);
        }

        public async Task ExecuteAsync(object parameter)
        {
            await ExecuteAsync((T)parameter);
        }

        public async Task ExecuteAsync(T parameter)
        {
            if (!CanExecute(parameter))
                return;

            try
            {
                IsExecuting = true;
                _logger?.LogDebug("Executing async command with parameter: {CommandType}, {ParameterType}", 
                    GetType().Name, typeof(T).Name);
                
                await _executeAsync(parameter);
                
                _logger?.LogDebug("Successfully executed async command: {CommandType}", GetType().Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing async command: {CommandType}", GetType().Name);
                throw;
            }
            finally
            {
                IsExecuting = false;
            }
        }
    }
} 