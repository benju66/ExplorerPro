using System.Collections.Generic;
using System.Windows;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Represents the result of drag & drop validation
    /// </summary>
    public class DragDropValidationResult
    {
        /// <summary>
        /// Gets or sets whether the drop operation is valid
        /// </summary>
        public bool IsValid { get; set; }

        /// <summary>
        /// Gets or sets the error message if validation failed
        /// </summary>
        public string ErrorMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the allowed drag & drop effects
        /// </summary>
        public DragDropEffects AllowedEffects { get; set; }

        /// <summary>
        /// Gets or sets the list of valid files that can be dropped
        /// </summary>
        public List<string> ValidFiles { get; set; } = new();

        /// <summary>
        /// Gets or sets the list of invalid files that cannot be dropped
        /// </summary>
        public List<string> InvalidFiles { get; set; } = new();

        /// <summary>
        /// Gets or sets additional validation warnings
        /// </summary>
        public List<string> Warnings { get; set; } = new();

        /// <summary>
        /// Gets or sets whether the operation requires confirmation
        /// </summary>
        public bool RequiresConfirmation { get; set; }

        /// <summary>
        /// Gets or sets the confirmation message
        /// </summary>
        public string ConfirmationMessage { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the estimated total size of files to be transferred
        /// </summary>
        public long EstimatedSize { get; set; }

        /// <summary>
        /// Gets or sets whether the operation will be time-consuming
        /// </summary>
        public bool IsLargeOperation { get; set; }

        /// <summary>
        /// Creates a successful validation result
        /// </summary>
        /// <param name="effects">The allowed drag & drop effects</param>
        /// <returns>A successful validation result</returns>
        public static DragDropValidationResult Success(DragDropEffects effects)
        {
            return new DragDropValidationResult 
            { 
                IsValid = true, 
                AllowedEffects = effects 
            };
        }

        /// <summary>
        /// Creates a failed validation result
        /// </summary>
        /// <param name="error">The error message</param>
        /// <returns>A failed validation result</returns>
        public static DragDropValidationResult Failure(string error)
        {
            return new DragDropValidationResult 
            { 
                IsValid = false, 
                ErrorMessage = error,
                AllowedEffects = DragDropEffects.None
            };
        }

        /// <summary>
        /// Creates a validation result that requires user confirmation
        /// </summary>
        /// <param name="effects">The allowed drag & drop effects</param>
        /// <param name="confirmationMessage">The confirmation message</param>
        /// <returns>A validation result requiring confirmation</returns>
        public static DragDropValidationResult WithConfirmation(DragDropEffects effects, string confirmationMessage)
        {
            return new DragDropValidationResult
            {
                IsValid = true,
                AllowedEffects = effects,
                RequiresConfirmation = true,
                ConfirmationMessage = confirmationMessage
            };
        }
    }
} 