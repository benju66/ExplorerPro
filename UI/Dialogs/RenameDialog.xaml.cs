using System;
using System.Windows;
using System.Windows.Input;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for RenameDialog.xaml
    /// Dialog for renaming tabs with validation and modern UI
    /// </summary>
    public partial class RenameDialog : Window
    {
        /// <summary>
        /// Gets the new name entered by the user
        /// </summary>
        public string NewName { get; private set; }

        /// <summary>
        /// Gets the original name for comparison
        /// </summary>
        public string OriginalName { get; private set; }

        /// <summary>
        /// Initializes a new instance of RenameDialog
        /// </summary>
        /// <param name="currentName">The current name of the tab</param>
        public RenameDialog(string currentName) : this(currentName, null, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of RenameDialog with smart positioning
        /// </summary>
        /// <param name="currentName">The current name of the tab</param>
        /// <param name="owner">The owner window for positioning</param>
        /// <param name="relativeElement">The UI element to position relative to (optional)</param>
        public RenameDialog(string currentName, Window owner, FrameworkElement relativeElement)
        {
            InitializeComponent();
            
            OriginalName = currentName ?? string.Empty;
            NameTextBox.Text = OriginalName;
            NewName = OriginalName;
            
            // Set owner for proper modal behavior
            if (owner != null)
            {
                Owner = owner;
                WindowStartupLocation = WindowStartupLocation.Manual;
                
                // Position dialog smartly relative to the element or owner
                PositionDialog(owner, relativeElement);
            }
            
            // Focus and select all text for easy editing
            Loaded += (s, e) =>
            {
                NameTextBox.Focus();
                NameTextBox.SelectAll();
            };
        }

        /// <summary>
        /// Positions the dialog smartly relative to the owner and optional element
        /// </summary>
        /// <param name="owner">The owner window</param>
        /// <param name="relativeElement">The element to position relative to</param>
        private void PositionDialog(Window owner, FrameworkElement relativeElement)
        {
            try
            {
                if (relativeElement != null)
                {
                    // Get the position of the relative element
                    var elementPosition = relativeElement.PointToScreen(new Point(0, 0));
                    
                    // Position dialog near the element but offset to avoid covering it
                    Left = elementPosition.X + relativeElement.ActualWidth / 2 - 150; // Center horizontally on element
                    Top = elementPosition.Y + relativeElement.ActualHeight + 10; // Below the element with padding
                    
                    // Ensure dialog stays within screen bounds
                    var screenBounds = SystemParameters.WorkArea;
                    if (Left + Width > screenBounds.Right)
                        Left = screenBounds.Right - Width - 10;
                    if (Left < screenBounds.Left)
                        Left = screenBounds.Left + 10;
                    if (Top + Height > screenBounds.Bottom)
                        Top = elementPosition.Y - Height - 10; // Above the element if no room below
                    if (Top < screenBounds.Top)
                        Top = screenBounds.Top + 10;
                }
                else
                {
                    // Center on owner window
                    Left = owner.Left + (owner.Width - Width) / 2;
                    Top = owner.Top + (owner.Height - Height) / 2;
                }
            }
            catch
            {
                // Fallback to center screen
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
        }

        /// <summary>
        /// Handles the Rename button click
        /// </summary>
        private void RenameButton_Click(object sender, RoutedEventArgs e)
        {
            var newName = NameTextBox.Text?.Trim();
            
            if (ValidateName(newName))
            {
                NewName = newName;
                DialogResult = true;
                Close();
            }
        }

        /// <summary>
        /// Handles the Cancel button click
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Handles key down events in the text box
        /// </summary>
        private void NameTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                RenameButton_Click(sender, new RoutedEventArgs());
            }
            else if (e.Key == Key.Escape)
            {
                CancelButton_Click(sender, new RoutedEventArgs());
            }
        }

        /// <summary>
        /// Validates the entered name
        /// </summary>
        /// <param name="name">The name to validate</param>
        /// <returns>True if the name is valid, false otherwise</returns>
        private bool ValidateName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowValidationError("Tab name cannot be empty.");
                return false;
            }

            if (name.Length > 100)
            {
                ShowValidationError("Tab name cannot exceed 100 characters.");
                return false;
            }

            // Check for invalid characters
            var invalidChars = new char[] { '/', '\\', ':', '*', '?', '"', '<', '>', '|' };
            if (name.IndexOfAny(invalidChars) >= 0)
            {
                ShowValidationError("Tab name contains invalid characters.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Shows a validation error message
        /// </summary>
        /// <param name="message">The error message to display</param>
        private void ShowValidationError(string message)
        {
            MessageBox.Show(this, message, "Invalid Name", MessageBoxButton.OK, MessageBoxImage.Warning);
            NameTextBox.Focus();
            NameTextBox.SelectAll();
        }

        /// <summary>
        /// Handles mouse down events for dragging the dialog
        /// </summary>
        private void Dialog_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        /// <summary>
        /// Override to handle close button behavior
        /// </summary>
        /// <param name="e">Cancel event args</param>
        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (DialogResult == null)
                DialogResult = false;
            
            base.OnClosing(e);
        }
    }
} 