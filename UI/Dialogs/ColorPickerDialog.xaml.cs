using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ExplorerPro.Commands;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for ColorPickerDialog.xaml
    /// Dialog for selecting tab colors with predefined color palette
    /// </summary>
    public partial class ColorPickerDialog : Window
    {
        /// <summary>
        /// Gets the selected color
        /// </summary>
        public Color SelectedColor { get; private set; }

        /// <summary>
        /// Gets the original color for comparison
        /// </summary>
        public Color OriginalColor { get; private set; }

        /// <summary>
        /// Currently selected color button for visual feedback
        /// </summary>
        private Button _selectedColorButton;

        /// <summary>
        /// Initializes a new instance of ColorPickerDialog
        /// </summary>
        /// <param name="currentColor">The current color of the tab</param>
        public ColorPickerDialog(Color currentColor)
        {
            InitializeComponent();
            
            OriginalColor = currentColor;
            SelectedColor = currentColor;
            
            InitializeColorPalette();
            UpdateCurrentColorPreview();
        }

        /// <summary>
        /// Initializes the color palette with predefined colors
        /// </summary>
        private void InitializeColorPalette()
        {
            var predefinedColors = TabCommands.PredefinedColors;
            var colorNames = TabCommands.ColorNames;

            for (int i = 0; i < predefinedColors.Length; i++)
            {
                var color = predefinedColors[i];
                var colorName = i < colorNames.Length ? colorNames[i] : $"Color {i + 1}";
                
                var colorButton = CreateColorButton(color, colorName);
                ColorGrid.Children.Add(colorButton);

                // Select the current color button
                if (ColorsAreEqual(color, OriginalColor))
                {
                    SelectColorButton(colorButton);
                }
            }
        }

        /// <summary>
        /// Creates a color button for the palette
        /// </summary>
        /// <param name="color">The color for the button</param>
        /// <param name="colorName">The name/tooltip for the color</param>
        /// <returns>A configured Button control</returns>
        private Button CreateColorButton(Color color, string colorName)
        {
            var button = new Button
            {
                Style = (Style)FindResource("ColorButtonStyle"),
                Background = new SolidColorBrush(color),
                ToolTip = colorName,
                Tag = color
            };

            button.Click += ColorButton_Click;
            return button;
        }

        /// <summary>
        /// Handles color button clicks
        /// </summary>
        private void ColorButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is Color color)
            {
                SelectedColor = color;
                SelectColorButton(button);
                UpdateCurrentColorPreview();
            }
        }

        /// <summary>
        /// Selects a color button and updates visual feedback
        /// </summary>
        /// <param name="button">The button to select</param>
        private void SelectColorButton(Button button)
        {
            // Clear previous selection
            if (_selectedColorButton != null)
            {
                _selectedColorButton.BorderBrush = Brushes.Transparent;
            }

            // Set new selection
            _selectedColorButton = button;
            if (_selectedColorButton != null)
            {
                _selectedColorButton.BorderBrush = new SolidColorBrush(Color.FromRgb(9, 105, 218)); // #0969DA
            }
        }

        /// <summary>
        /// Updates the current color preview
        /// </summary>
        private void UpdateCurrentColorPreview()
        {
            CurrentColorPreview.Background = new SolidColorBrush(SelectedColor);
        }

        /// <summary>
        /// Handles the Apply button click
        /// </summary>
        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        /// <summary>
        /// Handles the Cancel button click
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            SelectedColor = OriginalColor;
            DialogResult = false;
            Close();
        }

        /// <summary>
        /// Compares two colors for equality with tolerance for slight differences
        /// </summary>
        /// <param name="color1">First color</param>
        /// <param name="color2">Second color</param>
        /// <returns>True if colors are equal, false otherwise</returns>
        private bool ColorsAreEqual(Color color1, Color color2)
        {
            return Math.Abs(color1.R - color2.R) <= 1 &&
                   Math.Abs(color1.G - color2.G) <= 1 &&
                   Math.Abs(color1.B - color2.B) <= 1 &&
                   Math.Abs(color1.A - color2.A) <= 1;
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
            {
                SelectedColor = OriginalColor;
                DialogResult = false;
            }
            
            base.OnClosing(e);
        }
    }
} 