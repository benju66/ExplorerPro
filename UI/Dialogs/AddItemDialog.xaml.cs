using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for AddItemDialog.xaml
    /// </summary>
    public partial class AddItemDialog : Window
    {
        /// <summary>
        /// The task item created by this dialog
        /// </summary>
        public TaskItem TaskItem { get; private set; } = null!;

        /// <summary>
        /// Gets or sets the task text
        /// </summary>
        public string TaskText { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the due date
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Gets or sets the category
        /// </summary>
        public string Category { get; set; } = "Other";

        /// <summary>
        /// Gets or sets the priority (1-5)
        /// </summary>
        public int Priority { get; set; } = 3;

        /// <summary>
        /// Gets or sets whether the task is recurring
        /// </summary>
        public bool IsRecurring { get; set; }

        /// <summary>
        /// Gets or sets the recurrence pattern
        /// </summary>
        public string RecurrencePattern { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the notes
        /// </summary>
        public string Notes { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets whether the dialog is in edit mode
        /// </summary>
        public bool IsEditMode { get; set; }

        /// <summary>
        /// Constructor for the Add Item Dialog
        /// </summary>
        /// <param name="existingItem">Optional existing item to edit instead of creating a new one</param>
        public AddItemDialog(TaskItem? existingItem = null)
        {
            InitializeComponent();

            // Initialize default values
            DueDatePicker.SelectedDate = DateTime.Today;
            DueDatePicker.IsEnabled = false;
            EndDatePicker.SelectedDate = DateTime.Today.AddMonths(1);
            EndDatePicker.IsEnabled = false;

            // Setup recurrence type change event handler
            RecurrenceTypeComboBox.SelectionChanged += RecurrenceTypeComboBox_SelectionChanged;
            
            // If we're editing an existing item
            if (existingItem != null)
            {
                Title = "Edit Task";
                IsEditMode = true;
                LoadExistingTaskData(existingItem);
            }
        }

        /// <summary>
        /// Loads data from an existing task item into the dialog
        /// </summary>
        private void LoadExistingTaskData(TaskItem item)
        {
            TitleTextBox.Text = item.Title;
            DescriptionTextBox.Text = item.Description;
            
            // Also populate the ToDoPanel compatible properties
            TaskText = item.Title;
            Notes = item.Description;
            Category = item.Tags.Count > 0 ? item.Tags[0] : "Other";
            
            // Map priority from TaskPriority enum to 1-5 scale
            switch (item.Priority)
            {
                case TaskPriority.Low:
                    Priority = 1;
                    break;
                case TaskPriority.Medium:
                    Priority = 3;
                    break;
                case TaskPriority.High:
                    Priority = 5;
                    break;
                default:
                    Priority = 3;
                    break;
            }
            
            // Setup due date
            if (item.DueDate.HasValue)
            {
                HasDueDateCheckBox.IsChecked = true;
                DueDatePicker.SelectedDate = item.DueDate.Value;
                DueDatePicker.IsEnabled = true;
                DueDate = item.DueDate;
            }
            else
            {
                HasDueDateCheckBox.IsChecked = false;
                DueDatePicker.IsEnabled = false;
                DueDate = null;
            }
            
            // Setup priority
            foreach (ComboBoxItem comboItem in PriorityComboBox.Items)
            {
                if (Convert.ToInt32(comboItem.Tag) == (int)item.Priority)
                {
                    PriorityComboBox.SelectedItem = comboItem;
                    break;
                }
            }
            
            // Setup recurrence
            if (item.RecurrenceInfo != null)
            {
                IsRecurringCheckBox.IsChecked = true;
                RecurrenceTypeComboBox.IsEnabled = true;
                RecurrencePanel.Visibility = Visibility.Visible;
                
                // Set recurrence type
                foreach (ComboBoxItem comboItem in RecurrenceTypeComboBox.Items)
                {
                    if (comboItem.Tag.ToString() == item.RecurrenceInfo.Type)
                    {
                        RecurrenceTypeComboBox.SelectedItem = comboItem;
                        break;
                    }
                }
                
                // Set interval
                RepeatIntervalTextBox.Text = item.RecurrenceInfo.Interval.ToString();
                
                // Set end date
                if (item.RecurrenceInfo.EndDate.HasValue)
                {
                    HasEndDateCheckBox.IsChecked = false;
                    EndDatePicker.SelectedDate = item.RecurrenceInfo.EndDate.Value;
                    EndDatePicker.IsEnabled = true;
                }
                else
                {
                    HasEndDateCheckBox.IsChecked = true;
                    EndDatePicker.IsEnabled = false;
                }
                
                // Set the recurrence pattern property for ToDoPanel
                IsRecurring = true;
                RecurrencePattern = MapRecurrenceInfoToPattern(item.RecurrenceInfo);
            }
        }

        /// <summary>
        /// Maps RecurrenceInfo to a pattern string for the ToDoPanel
        /// </summary>
        private string MapRecurrenceInfoToPattern(RecurrenceInfo info)
        {
            if (info == null)
                return string.Empty;
                
            switch (info.Type)
            {
                case "daily":
                    return info.Interval == 1 ? "Daily" : $"Every {info.Interval} days";
                case "weekly":
                    return info.Interval == 1 ? "Weekly" : $"Every {info.Interval} weeks";
                case "monthly":
                    return info.Interval == 1 ? "Monthly" : $"Every {info.Interval} months";
                case "yearly":
                    return info.Interval == 1 ? "Yearly" : $"Every {info.Interval} years";
                default:
                    return info.Type;
            }
        }

        /// <summary>
        /// Event handler for the RecurrenceType combo box selection change
        /// </summary>
        private void RecurrenceTypeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecurrenceTypeComboBox.SelectedItem == null)
                return;
                
            ComboBoxItem selectedItem = (ComboBoxItem)RecurrenceTypeComboBox.SelectedItem;
            string type = selectedItem.Tag.ToString() ?? "daily";
            
            // Update interval unit label based on recurrence type
            switch (type)
            {
                case "daily":
                    IntervalUnitLabel.Content = "Day(s)";
                    break;
                case "weekly":
                    IntervalUnitLabel.Content = "Week(s)";
                    break;
                case "monthly":
                    IntervalUnitLabel.Content = "Month(s)";
                    break;
                case "yearly":
                    IntervalUnitLabel.Content = "Year(s)";
                    break;
                case "custom":
                    IntervalUnitLabel.Content = "Custom";
                    break;
            }
        }

        /// <summary>
        /// Event handler for the HasDueDate checkbox
        /// </summary>
        private void HasDueDateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            DueDatePicker.IsEnabled = true;
        }

        /// <summary>
        /// Event handler for the HasDueDate checkbox
        /// </summary>
        private void HasDueDateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            DueDatePicker.IsEnabled = false;
        }

        /// <summary>
        /// Event handler for the IsRecurring checkbox
        /// </summary>
        private void IsRecurringCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            RecurrenceTypeComboBox.IsEnabled = true;
            RecurrencePanel.Visibility = Visibility.Visible;
            IsRecurring = true;
        }

        /// <summary>
        /// Event handler for the IsRecurring checkbox
        /// </summary>
        private void IsRecurringCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            RecurrenceTypeComboBox.IsEnabled = false;
            RecurrencePanel.Visibility = Visibility.Collapsed;
            IsRecurring = false;
        }

        /// <summary>
        /// Event handler for the HasEndDate checkbox
        /// </summary>
        private void HasEndDateCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            EndDatePicker.IsEnabled = false;
        }

        /// <summary>
        /// Event handler for the HasEndDate checkbox
        /// </summary>
        private void HasEndDateCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            EndDatePicker.IsEnabled = true;
        }

        /// <summary>
        /// Event handler for the Save button
        /// </summary>
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TitleTextBox.Text))
            {
                MessageBox.Show("Title is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                TitleTextBox.Focus();
                return;
            }

            // Create task item
            TaskItem = new TaskItem
            {
                Title = TitleTextBox.Text.Trim(),
                Description = DescriptionTextBox.Text.Trim(),
                Created = DateTime.Now,
                Modified = DateTime.Now,
                Completed = false
            };

            // Update the properties for ToDoPanel
            TaskText = TitleTextBox.Text.Trim();
            Notes = DescriptionTextBox.Text.Trim();

            // Set due date if specified
            if (HasDueDateCheckBox.IsChecked == true && DueDatePicker.SelectedDate.HasValue)
            {
                TaskItem.DueDate = DueDatePicker.SelectedDate.Value;
                DueDate = DueDatePicker.SelectedDate.Value;
            }
            else
            {
                DueDate = null;
            }

            // Set priority
            ComboBoxItem selectedPriorityItem = (ComboBoxItem)PriorityComboBox.SelectedItem;
            if (selectedPriorityItem != null && selectedPriorityItem.Tag != null)
            {
                TaskItem.Priority = (TaskPriority)Convert.ToInt32(selectedPriorityItem.Tag);
                
                // Map priority from TaskPriority enum to 1-5 scale for ToDoPanel
                switch (TaskItem.Priority)
                {
                    case TaskPriority.Low:
                        Priority = 1;
                        break;
                    case TaskPriority.Medium:
                        Priority = 3;
                        break;
                    case TaskPriority.High:
                        Priority = 5;
                        break;
                }
            }

            // Set recurrence if specified
            if (IsRecurringCheckBox.IsChecked == true)
            {
                ComboBoxItem selectedRecurrenceItem = (ComboBoxItem)RecurrenceTypeComboBox.SelectedItem;
                if (selectedRecurrenceItem != null && selectedRecurrenceItem.Tag != null)
                {
                    int interval;
                    if (!int.TryParse(RepeatIntervalTextBox.Text, out interval) || interval < 1)
                    {
                        MessageBox.Show("Interval must be a positive number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        RepeatIntervalTextBox.Focus();
                        return;
                    }

                    TaskItem.RecurrenceInfo = new RecurrenceInfo
                    {
                        Type = selectedRecurrenceItem.Tag.ToString() ?? "daily",
                        Interval = interval
                    };

                    // Set end date if specified
                    if (HasEndDateCheckBox.IsChecked == false && EndDatePicker.SelectedDate.HasValue)
                    {
                        TaskItem.RecurrenceInfo.EndDate = EndDatePicker.SelectedDate.Value;
                    }
                    
                    // Set recurrence pattern for ToDoPanel
                    IsRecurring = true;
                    RecurrencePattern = MapRecurrenceInfoToPattern(TaskItem.RecurrenceInfo);
                }
            }
            else
            {
                IsRecurring = false;
                RecurrencePattern = string.Empty;
            }

            DialogResult = true;
            Close();
        }
    }

    /// <summary>
    /// Represents a task item
    /// </summary>
    public class TaskItem
    {
        /// <summary>
        /// The unique identifier for the task
        /// </summary>
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// The title of the task
        /// </summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// The description of the task
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// When the task was created
        /// </summary>
        public DateTime Created { get; set; }

        /// <summary>
        /// When the task was last modified
        /// </summary>
        public DateTime Modified { get; set; }

        /// <summary>
        /// The optional due date for the task
        /// </summary>
        public DateTime? DueDate { get; set; }

        /// <summary>
        /// Whether the task has been completed
        /// </summary>
        public bool Completed { get; set; }

        /// <summary>
        /// When the task was completed
        /// </summary>
        public DateTime? CompletedDate { get; set; }

        /// <summary>
        /// The priority of the task
        /// </summary>
        public TaskPriority Priority { get; set; } = TaskPriority.Medium;

        /// <summary>
        /// The recurrence information for the task, if it's recurring
        /// </summary>
        public RecurrenceInfo? RecurrenceInfo { get; set; }

        /// <summary>
        /// Tags associated with this task
        /// </summary>
        public List<string> Tags { get; set; } = new List<string>();
    }

    /// <summary>
    /// The priority levels for tasks
    /// </summary>
    public enum TaskPriority
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    /// <summary>
    /// Represents recurrence information for a task
    /// </summary>
    public class RecurrenceInfo
    {
        /// <summary>
        /// The type of recurrence (daily, weekly, monthly, yearly, custom)
        /// </summary>
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// The interval for recurrence (e.g., every 2 weeks)
        /// </summary>
        public int Interval { get; set; } = 1;

        /// <summary>
        /// The optional end date for the recurrence
        /// </summary>
        public DateTime? EndDate { get; set; }
    }
}