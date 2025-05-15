using System.Windows;
using System.Windows.Controls;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Dialog for getting text input from the user.
    /// </summary>
    public partial class TextInputDialog : Window
    {
        /// <summary>
        /// Gets the text entered by the user.
        /// </summary>
        public string InputText { get; private set; }

        /// <summary>
        /// Initialize a new TextInputDialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="prompt">Prompt text</param>
        /// <param name="defaultText">Default text to display</param>
        public TextInputDialog(string title, string prompt, string defaultText = "")
        {
            InitializeComponent();
            
            Title = title;
            promptTextBlock.Text = prompt;
            inputTextBox.Text = defaultText;
            
            // Set focus to the textbox
            Loaded += (s, e) => inputTextBox.Focus();
            
            // Select all text if there's default text
            if (!string.IsNullOrEmpty(defaultText))
            {
                Loaded += (s, e) => inputTextBox.SelectAll();
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            InputText = inputTextBox.Text;
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