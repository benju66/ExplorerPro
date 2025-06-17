using System;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Interface for handling exceptions in the application
    /// </summary>
    public interface IExceptionHandler
    {
        /// <summary>
        /// Handles an exception with the given context
        /// </summary>
        /// <param name="ex">The exception to handle</param>
        /// <param name="context">The context in which the exception occurred</param>
        void HandleException(Exception ex, string context);
    }
} 