using System;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.Core.Commands;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.UI.Dialogs;

namespace ExplorerPro.Commands
{
    /// <summary>
    /// Modern unified command system for tab operations.
    /// Provides enterprise-level command management with async support and validation.
    /// </summary>
    public static class ModernTabCommandSystem
    {
        #region Command Factory Methods
        
        /// <summary>
        /// Creates a new tab command
        /// </summary>
        public static IAsyncCommand CreateNewTabCommand(
            ITabManagerService tabManager, 
            ILogger logger = null)
        {
            return new AsyncRelayCommand(
                async () => await ExecuteNewTabAsync(tabManager),
                () => CanExecuteNewTab(tabManager),
                logger);
        }
        
        /// <summary>
        /// Creates a close tab command
        /// </summary>
        public static IAsyncCommand<TabModel> CreateCloseTabCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand<TabModel>(
                async tab => await ExecuteCloseTabAsync(tabManager, tab),
                tab => CanExecuteCloseTab(tabManager, tab),
                logger);
        }
        
        /// <summary>
        /// Creates a duplicate tab command
        /// </summary>
        public static IAsyncCommand<TabModel> CreateDuplicateTabCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand<TabModel>(
                async tab => await ExecuteDuplicateTabAsync(tabManager, tab),
                tab => CanExecuteDuplicateTab(tabManager, tab),
                logger);
        }
        
        /// <summary>
        /// Creates a rename tab command
        /// </summary>
        public static IAsyncCommand<TabModel> CreateRenameTabCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand<TabModel>(
                async tab => await ExecuteRenameTabAsync(tabManager, tab),
                tab => CanExecuteRenameTab(tabManager, tab),
                logger);
        }
        
        /// <summary>
        /// Creates a change tab color command
        /// </summary>
        public static IAsyncCommand<TabModel> CreateChangeColorCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand<TabModel>(
                async tab => await ExecuteChangeColorAsync(tabManager, tab),
                tab => CanExecuteChangeColor(tabManager, tab),
                logger);
        }
        
        /// <summary>
        /// Creates a clear tab color command
        /// </summary>
        public static IAsyncCommand<TabModel> CreateClearColorCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand<TabModel>(
                async tab => await ExecuteClearColorAsync(tabManager, tab),
                tab => CanExecuteClearColor(tabManager, tab),
                logger);
        }
        
        /// <summary>
        /// Creates a toggle pin tab command
        /// </summary>
        public static IAsyncCommand<TabModel> CreateTogglePinCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand<TabModel>(
                async tab => await ExecuteTogglePinAsync(tabManager, tab),
                tab => CanExecuteTogglePin(tabManager, tab),
                logger);
        }
        
        /// <summary>
        /// Creates a move tab command
        /// </summary>
        public static IAsyncCommand<(TabModel tab, int newIndex)> CreateMoveTabCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand<(TabModel tab, int newIndex)>(
                async param => await ExecuteMoveTabAsync(tabManager, param.tab, param.newIndex),
                param => CanExecuteMoveTab(tabManager, param.tab, param.newIndex),
                logger);
        }
        
        /// <summary>
        /// Creates a navigate to next tab command
        /// </summary>
        public static IAsyncCommand CreateNextTabCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand(
                async () => await tabManager.NavigateToNextTabAsync(),
                () => tabManager.HasTabs && tabManager.TabCount > 1,
                logger);
        }
        
        /// <summary>
        /// Creates a navigate to previous tab command
        /// </summary>
        public static IAsyncCommand CreatePreviousTabCommand(
            ITabManagerService tabManager,
            ILogger logger = null)
        {
            return new AsyncRelayCommand(
                async () => await tabManager.NavigateToPreviousTabAsync(),
                () => tabManager.HasTabs && tabManager.TabCount > 1,
                logger);
        }
        
        #endregion

        #region Command Implementations
        
        private static async Task ExecuteNewTabAsync(ITabManagerService tabManager)
        {
            try
            {
                var request = TabCreationRequest.CreateDefault();
                await tabManager.CreateTabAsync(request.Title, request.Path, new TabCreationOptions
                {
                    IsPinned = request.IsPinned,
                    CustomColor = request.CustomColor,
                    MakeActive = request.MakeActive,
                    Content = request.Content
                });
            }
            catch (Exception ex)
            {
                throw new TabCommandException("Failed to create new tab", ex);
            }
        }
        
        private static async Task ExecuteCloseTabAsync(ITabManagerService tabManager, TabModel tab)
        {
            try
            {
                if (tab == null) return;
                
                // Check for unsaved changes
                if (tab.HasUnsavedChanges)
                {
                    // In a real implementation, show confirmation dialog
                    // For now, just proceed
                }
                
                await tabManager.CloseTabAsync(tab);
            }
            catch (Exception ex)
            {
                throw new TabCommandException($"Failed to close tab '{tab?.Title}'", ex);
            }
        }
        
        private static async Task ExecuteDuplicateTabAsync(ITabManagerService tabManager, TabModel tab)
        {
            try
            {
                if (tab == null) return;
                await tabManager.DuplicateTabAsync(tab);
            }
            catch (Exception ex)
            {
                throw new TabCommandException($"Failed to duplicate tab '{tab?.Title}'", ex);
            }
        }
        
        private static async Task ExecuteRenameTabAsync(ITabManagerService tabManager, TabModel tab)
        {
            try
            {
                if (tab == null) return;
                
                // Show rename dialog
                var dialog = new RenameDialog(tab.Title);
                if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
                {
                    await tabManager.RenameTabAsync(tab, dialog.NewName.Trim());
                }
            }
            catch (Exception ex)
            {
                throw new TabCommandException($"Failed to rename tab '{tab?.Title}'", ex);
            }
        }
        
        private static async Task ExecuteChangeColorAsync(ITabManagerService tabManager, TabModel tab)
        {
            try
            {
                if (tab == null) return;
                
                // Show color picker dialog
                var currentColor = tab.HasCustomColor ? tab.CustomColor : Colors.LightGray;
                var dialog = new ColorPickerDialog(currentColor);
                
                if (dialog.ShowDialog() == true)
                {
                    await tabManager.SetTabColorAsync(tab, dialog.SelectedColor);
                }
            }
            catch (Exception ex)
            {
                throw new TabCommandException($"Failed to change color for tab '{tab?.Title}'", ex);
            }
        }
        
        private static async Task ExecuteClearColorAsync(ITabManagerService tabManager, TabModel tab)
        {
            try
            {
                if (tab == null) return;
                await tabManager.ClearTabColorAsync(tab);
            }
            catch (Exception ex)
            {
                throw new TabCommandException($"Failed to clear color for tab '{tab?.Title}'", ex);
            }
        }
        
        private static async Task ExecuteTogglePinAsync(ITabManagerService tabManager, TabModel tab)
        {
            try
            {
                if (tab == null) return;
                await tabManager.SetTabPinnedAsync(tab, !tab.IsPinned);
            }
            catch (Exception ex)
            {
                throw new TabCommandException($"Failed to toggle pin for tab '{tab?.Title}'", ex);
            }
        }
        
        private static async Task ExecuteMoveTabAsync(ITabManagerService tabManager, TabModel tab, int newIndex)
        {
            try
            {
                if (tab == null) return;
                await tabManager.MoveTabAsync(tab, newIndex);
            }
            catch (Exception ex)
            {
                throw new TabCommandException($"Failed to move tab '{tab?.Title}' to index {newIndex}", ex);
            }
        }
        
        #endregion

        #region Can Execute Logic
        
        private static bool CanExecuteNewTab(ITabManagerService tabManager)
        {
            return tabManager != null && tabManager.TabCount < 50; // Reasonable limit
        }
        
        private static bool CanExecuteCloseTab(ITabManagerService tabManager, TabModel tab)
        {
            return tabManager != null && tab != null && tabManager.CanCloseTab(tab);
        }
        
        private static bool CanExecuteDuplicateTab(ITabManagerService tabManager, TabModel tab)
        {
            return tabManager != null && tab != null && tabManager.TabCount < 50;
        }
        
        private static bool CanExecuteRenameTab(ITabManagerService tabManager, TabModel tab)
        {
            return tabManager != null && tab != null;
        }
        
        private static bool CanExecuteChangeColor(ITabManagerService tabManager, TabModel tab)
        {
            return tabManager != null && tab != null;
        }
        
        private static bool CanExecuteClearColor(ITabManagerService tabManager, TabModel tab)
        {
            return tabManager != null && tab != null && tab.HasCustomColor;
        }
        
        private static bool CanExecuteTogglePin(ITabManagerService tabManager, TabModel tab)
        {
            return tabManager != null && tab != null;
        }
        
        private static bool CanExecuteMoveTab(ITabManagerService tabManager, TabModel tab, int newIndex)
        {
            return tabManager != null && 
                   tab != null && 
                   tabManager.CanReorderTabs() && 
                   newIndex >= 0 && 
                   newIndex < tabManager.TabCount;
        }
        
        #endregion

        #region Predefined Color Palette
        
        /// <summary>
        /// Predefined colors for tab theming
        /// </summary>
        public static readonly Color[] PredefinedColors = new[]
        {
            Color.FromRgb(255, 87, 87),   // Red
            Color.FromRgb(255, 154, 0),   // Orange  
            Color.FromRgb(255, 206, 84),  // Yellow
            Color.FromRgb(129, 212, 250), // Light Blue
            Color.FromRgb(0, 200, 83),    // Green
            Color.FromRgb(171, 71, 188),  // Purple
            Color.FromRgb(158, 158, 158), // Gray
            Color.FromRgb(121, 85, 72),   // Brown
        };
        
        /// <summary>
        /// Gets a command to set a specific predefined color
        /// </summary>
        public static IAsyncCommand<TabModel> CreateSetPredefinedColorCommand(
            ITabManagerService tabManager,
            Color color,
            ILogger logger = null)
        {
            return new AsyncRelayCommand<TabModel>(
                async tab => 
                {
                    if (tab != null)
                        await tabManager.SetTabColorAsync(tab, color);
                },
                tab => tab != null,
                logger);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Exception thrown when tab command execution fails
    /// </summary>
    public class TabCommandException : Exception
    {
        public TabCommandException(string message) : base(message) { }
        public TabCommandException(string message, Exception innerException) : base(message, innerException) { }
    }
} 