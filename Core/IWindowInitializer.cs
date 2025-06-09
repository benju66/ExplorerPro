using System.Threading.Tasks;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Interface for window initialization components.
    /// Provides standardized methods for initializing windows with proper error handling and rollback capabilities.
    /// </summary>
    public interface IWindowInitializer
    {
        /// <summary>
        /// Asynchronously initializes a window with the provided context.
        /// </summary>
        /// <param name="context">The initialization context containing window and state information</param>
        /// <returns>True if initialization succeeded, false otherwise</returns>
        Task<bool> InitializeAsync(WindowInitializationContext context);
        
        /// <summary>
        /// Validates that all prerequisites for initialization are met.
        /// </summary>
        /// <returns>True if prerequisites are satisfied, false otherwise</returns>
        bool ValidatePrerequisites();
        
        /// <summary>
        /// Rolls back any initialization changes made during a failed initialization attempt.
        /// </summary>
        /// <param name="context">The initialization context to roll back</param>
        void Rollback(WindowInitializationContext context);
    }
} 