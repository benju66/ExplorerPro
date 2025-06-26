using System;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Modern tab creation request with comprehensive validation and options.
    /// Provides enterprise-level parameter validation and extensibility.
    /// </summary>
    public class TabCreationRequest
    {
        /// <summary>
        /// Tab title (required)
        /// </summary>
        [Required(ErrorMessage = "Tab title is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Tab title must be between 1 and 100 characters")]
        public string Title { get; set; }

        /// <summary>
        /// File system path for the tab (optional)
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// Whether to make this tab active after creation
        /// </summary>
        public bool MakeActive { get; set; } = true;

        /// <summary>
        /// Whether the tab should be pinned
        /// </summary>
        public bool IsPinned { get; set; } = false;

        /// <summary>
        /// Custom color for the tab (optional)
        /// </summary>
        public Color? CustomColor { get; set; }

        /// <summary>
        /// Specific index to insert the tab at (optional)
        /// </summary>
        public int? InsertAtIndex { get; set; }

        /// <summary>
        /// Content to be hosted in the tab (optional)
        /// </summary>
        public object Content { get; set; }

        /// <summary>
        /// Tab template to use for creation (optional)
        /// </summary>
        public TabTemplate Template { get; set; }

        /// <summary>
        /// Whether to defer content loading until tab is activated
        /// </summary>
        public bool DeferContentLoading { get; set; } = false;

        /// <summary>
        /// Priority level for tab creation
        /// </summary>
        public TabPriority Priority { get; set; } = TabPriority.Normal;

        /// <summary>
        /// Creates a default tab creation request
        /// </summary>
        public static TabCreationRequest CreateDefault(string title = "New Tab", string path = null)
        {
            return new TabCreationRequest
            {
                Title = title,
                Path = path ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                MakeActive = true,
                Priority = TabPriority.Normal
            };
        }

        /// <summary>
        /// Creates a pinned tab creation request
        /// </summary>
        public static TabCreationRequest CreatePinned(string title, string path = null)
        {
            return new TabCreationRequest
            {
                Title = title,
                Path = path,
                IsPinned = true,
                MakeActive = true,
                Priority = TabPriority.High
            };
        }

        /// <summary>
        /// Creates a background tab creation request
        /// </summary>
        public static TabCreationRequest CreateBackground(string title, string path = null)
        {
            return new TabCreationRequest
            {
                Title = title,
                Path = path,
                MakeActive = false,
                DeferContentLoading = true,
                Priority = TabPriority.Low
            };
        }
    }

    /// <summary>
    /// Tab priority levels for creation and resource allocation
    /// </summary>
    public enum TabPriority
    {
        Low = 0,
        Normal = 1,
        High = 2,
        Critical = 3
    }

    /// <summary>
    /// Tab template for consistent tab creation
    /// </summary>
    public class TabTemplate
    {
        public string Name { get; set; }
        public string DefaultTitle { get; set; }
        public Color? DefaultColor { get; set; }
        public bool IsPinned { get; set; }
        public Type ContentType { get; set; }
        public object ContentParameters { get; set; }
    }

    /// <summary>
    /// Result of tab validation operations
    /// </summary>
    public class TabValidationResult
    {
        public bool IsValid { get; set; }
        public string[] Errors { get; set; } = Array.Empty<string>();
        public string[] Warnings { get; set; } = Array.Empty<string>();

        public static TabValidationResult Valid() => new TabValidationResult { IsValid = true };
        
        public static TabValidationResult Invalid(params string[] errors) => 
            new TabValidationResult { IsValid = false, Errors = errors };
    }

    /// <summary>
    /// Tab operation types for validation and processing
    /// </summary>
    public enum TabOperation
    {
        Create,
        Close,
        Move,
        Duplicate,
        Pin,
        Unpin,
        ChangeColor,
        Rename
    }
} 