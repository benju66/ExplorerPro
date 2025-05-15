using System;
using System.Windows;
using System.Windows.Controls;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for EditRecurrenceDialog.xaml
    /// </summary>
    public partial class EditRecurrenceDialog : Window
    {
        private readonly RecurringItemViewModel _originalItem;

        /// <summary>
        /// Initializes a new instance of the EditRecurrenceDialog class
        /// </summary>
        /// <param name="item">The item to edit</param>
        public EditRecurrenceDialog(RecurringItemViewModel item)
        {
            InitializeComponent();
            
            _originalItem = item;
            
            // Populate fields with the item's data
            txtName.Text = item.Name;
            
            // Set frequency combobox
            cmbFrequency.Text = item.Frequency;
            
            // Set due date
            if (DateTime.TryParse(item.NextDueDate, out DateTime dueDate))
            {
                dpNextDueDate.SelectedDate = dueDate;
            }
            
            // Set priority combobox
            if (int.TryParse(item.Priority, out int priority))
            {
                cmbPriority.SelectedIndex = Math.Clamp(priority - 1, 0, 4);
            }
            else
            {
                cmbPriority.SelectedIndex = 2; // Default to 3 (middle priority)
            }
            
            // Set original due day
            if (item.OriginalDueDay.HasValue)
            {
                chkUseOriginalDueDay.IsChecked = true;
                txtOriginalDueDay.Text = item.OriginalDueDay.Value.ToString();
            }
            else
            {
                chkUseOriginalDueDay.IsChecked = false;
                txtOriginalDueDay.Text = string.Empty;
            }
            
            // Set shift weekends checkbox
            chkShiftWeekends.IsChecked = item.ShiftWeekends;
        }
        
        /// <summary>
        /// Gets the updated data from the dialog
        /// </summary>
        /// <returns>A RecurringItemViewModel with the updated data</returns>
        public RecurringItemViewModel GetUpdatedData()
        {
            int? originalDueDay = null;
            if (chkUseOriginalDueDay.IsChecked == true && int.TryParse(txtOriginalDueDay.Text, out int parsedDay))
            {
                originalDueDay = parsedDay;
            }
            
            return new RecurringItemViewModel
            {
                TaskUuid = _originalItem.TaskUuid,
                Name = txtName.Text,
                Frequency = cmbFrequency.Text,
                NextDueDate = dpNextDueDate.SelectedDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                Priority = (cmbPriority.SelectedIndex + 1).ToString(),
                OriginalDueDay = originalDueDay,
                ShiftWeekends = chkShiftWeekends.IsChecked == true
            };
        }
        
        /// <summary>
        /// Event handler for the Save button click
        /// </summary>
        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // Validate input
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Task name cannot be empty.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (string.IsNullOrWhiteSpace(cmbFrequency.Text))
            {
                MessageBox.Show("Frequency cannot be empty.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (!dpNextDueDate.SelectedDate.HasValue)
            {
                MessageBox.Show("Please select a next due date.", "Validation Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            
            if (chkUseOriginalDueDay.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(txtOriginalDueDay.Text) || 
                    !int.TryParse(txtOriginalDueDay.Text, out int day) || 
                    day < 1 || day > 31)
                {
                    MessageBox.Show("Original due day must be a number between 1 and 31.", 
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            
            // Close dialog with success result
            DialogResult = true;
            Close();
        }
        
        /// <summary>
        /// Event handler for the Cancel button click
        /// </summary>
        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        /// <summary>
        /// Event handler for the UseOriginalDueDay checkbox checked event
        /// </summary>
        private void ChkUseOriginalDueDay_Checked(object sender, RoutedEventArgs e)
        {
            txtOriginalDueDay.IsEnabled = true;
            
            // If the field is empty and we have a due date, use that day
            if (string.IsNullOrWhiteSpace(txtOriginalDueDay.Text) && dpNextDueDate.SelectedDate.HasValue)
            {
                txtOriginalDueDay.Text = dpNextDueDate.SelectedDate.Value.Day.ToString();
            }
        }
        
        /// <summary>
        /// Event handler for the UseOriginalDueDay checkbox unchecked event
        /// </summary>
        private void ChkUseOriginalDueDay_Unchecked(object sender, RoutedEventArgs e)
        {
            txtOriginalDueDay.IsEnabled = false;
        }
    }
}