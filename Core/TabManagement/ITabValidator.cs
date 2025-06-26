using System.Threading.Tasks;
using ExplorerPro.Models;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Interface for validating tab operations
    /// </summary>
    public interface ITabValidator
    {
        /// <summary>
        /// Validates a tab creation request
        /// </summary>
        Task<TabValidationResult> ValidateCreationAsync(TabCreationRequest request);
        
        /// <summary>
        /// Validates a tab operation
        /// </summary>
        Task<TabValidationResult> ValidateOperationAsync(TabModel tab, TabOperation operation);
    }
    
    /// <summary>
    /// Interface for optimizing tab memory usage
    /// </summary>
    public interface ITabMemoryOptimizer
    {
        /// <summary>
        /// Optimizes memory usage for the given tabs
        /// </summary>
        Task OptimizeAsync(System.Collections.Generic.IEnumerable<TabModel> tabs);
    }
    
    /// <summary>
    /// Default tab validator implementation
    /// </summary>
    public class DefaultTabValidator : ITabValidator
    {
        public async Task<TabValidationResult> ValidateCreationAsync(TabCreationRequest request)
        {
            if (request == null)
                return TabValidationResult.Invalid("Tab creation request cannot be null");
                
            if (string.IsNullOrWhiteSpace(request.Title))
                return TabValidationResult.Invalid("Tab title cannot be empty");
                
            if (request.Title.Length > 100)
                return TabValidationResult.Invalid("Tab title cannot exceed 100 characters");
                
            await Task.CompletedTask;
            return TabValidationResult.Valid();
        }
        
        public async Task<TabValidationResult> ValidateOperationAsync(TabModel tab, TabOperation operation)
        {
            switch (operation)
            {
                case TabOperation.Close:
                    if (tab?.IsPinned == true && tab.HasUnsavedChanges)
                        return TabValidationResult.Invalid("Cannot close pinned tab with unsaved changes");
                    break;
                    
                case TabOperation.Move:
                    // Allow all move operations for now
                    break;
                    
                default:
                    // Allow other operations
                    break;
            }
            
            await Task.CompletedTask;
            return TabValidationResult.Valid();
        }
    }
    
    /// <summary>
    /// Default tab memory optimizer implementation
    /// </summary>
    public class DefaultTabMemoryOptimizer : ITabMemoryOptimizer
    {
        public async Task OptimizeAsync(System.Collections.Generic.IEnumerable<TabModel> tabs)
        {
            // Basic optimization: hibernate inactive tabs
            var inactiveTabs = tabs.Where(t => !t.IsActive && t.State != Models.TabState.Hibernated);
            
            foreach (var tab in inactiveTabs)
            {
                if (tab.State == Models.TabState.Normal)
                {
                    tab.State = Models.TabState.Hibernated;
                    // Could dispose heavy content here
                }
            }
            
            await Task.CompletedTask;
        }
    }
} 