using System;
using System.Windows.Input;
using System.Windows.Media;
using ExplorerPro.Models;
using ExplorerPro.UI.Dialogs;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Commands
{
    /// <summary>
    /// Commands for tab operations including rename, color change, and pin/unpin
    /// </summary>
    public static class TabCommands
    {
        #region RelayCommand Implementation

        /// <summary>
        /// Simple relay command implementation for tab operations
        /// </summary>
        public class RelayCommand : ICommand
        {
            private readonly Action<object> _execute;
            private readonly Func<object, bool> _canExecute;

            public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
            {
                _execute = execute ?? throw new ArgumentNullException(nameof(execute));
                _canExecute = canExecute;
            }

            public event EventHandler CanExecuteChanged
            {
                add { CommandManager.RequerySuggested += value; }
                remove { CommandManager.RequerySuggested -= value; }
            }

            public bool CanExecute(object parameter)
            {
                return _canExecute?.Invoke(parameter) ?? true;
            }

            public void Execute(object parameter)
            {
                _execute(parameter);
            }
        }

        #endregion

        #region Command Factory Methods

        /// <summary>
        /// Creates a rename tab command
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <returns>ICommand for renaming tabs</returns>
        public static ICommand CreateRenameTabCommand(ILogger logger = null)
        {
            return new RelayCommand(
                parameter => ExecuteRenameTab(parameter, logger),
                parameter => CanExecuteRenameTab(parameter)
            );
        }

        /// <summary>
        /// Creates a change color command
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <returns>ICommand for changing tab colors</returns>
        public static ICommand CreateChangeColorCommand(ILogger logger = null)
        {
            return new RelayCommand(
                parameter => ExecuteChangeColor(parameter, logger),
                parameter => CanExecuteChangeColor(parameter)
            );
        }

        /// <summary>
        /// Creates a toggle pin command
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <returns>ICommand for toggling tab pin state</returns>
        public static ICommand CreateTogglePinCommand(ILogger logger = null)
        {
            return new RelayCommand(
                parameter => ExecuteTogglePin(parameter, logger),
                parameter => CanExecuteTogglePin(parameter)
            );
        }

        /// <summary>
        /// Creates a close tab command
        /// </summary>
        /// <param name="logger">Logger instance</param>
        /// <returns>ICommand for closing tabs</returns>
        public static ICommand CreateCloseTabCommand(ILogger logger = null)
        {
            return new RelayCommand(
                parameter => ExecuteCloseTab(parameter, logger),
                parameter => CanExecuteCloseTab(parameter)
            );
        }

        #endregion

        #region Command Implementations

        /// <summary>
        /// Executes the rename tab command
        /// </summary>
        private static void ExecuteRenameTab(object parameter, ILogger logger)
        {
            try
            {
                if (parameter is TabModel tabModel)
                {
                    var dialog = new RenameDialog(tabModel.Title);
                    if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.NewName))
                    {
                        var oldTitle = tabModel.Title;
                        tabModel.Title = dialog.NewName.Trim();
                        tabModel.MarkAsModified();
                        logger?.LogDebug($"Tab renamed from '{oldTitle}' to '{tabModel.Title}'");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error executing rename tab command");
            }
        }

        /// <summary>
        /// Executes the change color command
        /// </summary>
        private static void ExecuteChangeColor(object parameter, ILogger logger)
        {
            try
            {
                if (parameter is TabModel tabModel)
                {
                    var dialog = new ColorPickerDialog(tabModel.CustomColor);
                    if (dialog.ShowDialog() == true)
                    {
                        var oldColor = tabModel.CustomColor;
                        tabModel.CustomColor = dialog.SelectedColor;
                        tabModel.MarkAsModified();
                        logger?.LogDebug($"Tab color changed from {oldColor} to {tabModel.CustomColor}");
                    }
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error executing change color command");
            }
        }

        /// <summary>
        /// Executes the toggle pin command
        /// </summary>
        private static void ExecuteTogglePin(object parameter, ILogger logger)
        {
            try
            {
                if (parameter is TabModel tabModel)
                {
                    tabModel.IsPinned = !tabModel.IsPinned;
                    tabModel.MarkAsModified();
                    logger?.LogDebug($"Tab '{tabModel.Title}' pin state changed to {tabModel.IsPinned}");
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error executing toggle pin command");
            }
        }

        /// <summary>
        /// Executes the close tab command
        /// </summary>
        private static void ExecuteCloseTab(object parameter, ILogger logger)
        {
            try
            {
                if (parameter is TabModel tabModel)
                {
                    // This should be handled by the ChromeStyleTabControl
                    // We just mark it as requested for closure
                    logger?.LogDebug($"Close requested for tab '{tabModel.Title}'");
                    
                    // The actual closing logic should be implemented in the parent control
                    // This is just a marker for the UI to handle the close operation
                }
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "Error executing close tab command");
            }
        }

        #endregion

        #region Can Execute Methods

        /// <summary>
        /// Determines if the rename tab command can execute
        /// </summary>
        private static bool CanExecuteRenameTab(object parameter)
        {
            return parameter is TabModel tabModel && tabModel.CanClose;
        }

        /// <summary>
        /// Determines if the change color command can execute
        /// </summary>
        private static bool CanExecuteChangeColor(object parameter)
        {
            return parameter is TabModel;
        }

        /// <summary>
        /// Determines if the toggle pin command can execute
        /// </summary>
        private static bool CanExecuteTogglePin(object parameter)
        {
            return parameter is TabModel;
        }

        /// <summary>
        /// Determines if the close tab command can execute
        /// </summary>
        private static bool CanExecuteCloseTab(object parameter)
        {
            return parameter is TabModel tabModel && tabModel.CanClose;
        }

        #endregion

        #region Predefined Color Sets

        /// <summary>
        /// Gets a collection of predefined colors for tab customization
        /// </summary>
        public static Color[] PredefinedColors => new[]
        {
            Colors.LightGray,    // Default
            Colors.LightBlue,    // Information
            Colors.LightGreen,   // Success
            Colors.LightYellow,  // Warning
            Colors.LightPink,    // Error
            Colors.LightCyan,    // Note
            Colors.Lavender,     // Special
            Colors.LightSalmon,  // Important
            Colors.PaleGreen,    // Completed
            Colors.Wheat,        // In Progress
            Colors.LightSteelBlue, // Review
            Colors.MistyRose     // Draft
        };

        /// <summary>
        /// Gets color names corresponding to predefined colors
        /// </summary>
        public static string[] ColorNames => new[]
        {
            "Default",
            "Information",
            "Success",
            "Warning",
            "Error",
            "Note",
            "Special",
            "Important",
            "Completed",
            "In Progress",
            "Review",
            "Draft"
        };

        #endregion
    }
} 
