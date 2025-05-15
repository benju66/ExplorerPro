using System.Windows;
using System.Windows.Controls;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Dialog for getting text input from the user
    /// </summary>
    public class InputDialog : Window
    {
        private TextBox textBox;
        
        /// <summary>
        /// Gets the text entered by the user
        /// </summary>
        public string ResponseText { get; private set; } = string.Empty;
        
        /// <summary>
        /// Initializes a new instance of the InputDialog class
        /// </summary>
        /// <param name="title">The dialog title</param>
        /// <param name="prompt">The prompt to display</param>
        /// <param name="defaultValue">The default value for the text box</param>
        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            Title = title;
            Width = 350;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            
            // Create the layout
            Grid grid = new Grid();
            grid.Margin = new Thickness(10);
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            Content = grid;
            
            // Add the prompt text
            TextBlock promptBlock = new TextBlock
            {
                Text = prompt,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(promptBlock, 0);
            grid.Children.Add(promptBlock);
            
            // Add the text box
            textBox = new TextBox
            {
                Text = defaultValue,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);
            
            // Add the buttons
            StackPanel buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            
            Button okButton = new Button
            {
                Content = "OK",
                IsDefault = true,
                MinWidth = 70,
                Margin = new Thickness(0, 0, 10, 0)
            };
            okButton.Click += OkButton_Click;
            
            Button cancelButton = new Button
            {
                Content = "Cancel",
                IsCancel = true,
                MinWidth = 70
            };
            cancelButton.Click += CancelButton_Click;
            
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);
            
            // Focus the text box
            Loaded += (s, e) => textBox.Focus();
        }
        
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            ResponseText = textBox.Text;
            DialogResult = true;
            Close();
        }
        
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}