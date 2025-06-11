using System;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Represents the initialization state of a MainWindow instance.
    /// </summary>
    public enum InitializationState
    {
        /// <summary>
        /// Window instance created but not initialized.
        /// </summary>
        Created,
        
        /// <summary>
        /// XAML components are being initialized.
        /// </summary>
        InitializingComponents,
        
        /// <summary>
        /// Components initialized, setting up window.
        /// </summary>
        InitializingWindow,
        
        /// <summary>
        /// Window fully initialized and ready for use.
        /// </summary>
        Ready,
        
        /// <summary>
        /// Initialization failed, window is in error state.
        /// </summary>
        Failed,
        
        /// <summary>
        /// Window is being disposed.
        /// </summary>
        Disposing,
        
        /// <summary>
        /// Window has been disposed.
        /// </summary>
        Disposed
    }

    /// <summary>
    /// Exception thrown when window initialization fails.
    /// Updated to use unified WindowState enum instead of separate InitializationState.
    /// </summary>
    public class WindowInitializationException : Exception
    {
        public WindowState FailedState { get; }
        
        public WindowInitializationException(string message, WindowState failedState, Exception innerException = null)
            : base(message, innerException)
        {
            FailedState = failedState;
        }
    }
} 