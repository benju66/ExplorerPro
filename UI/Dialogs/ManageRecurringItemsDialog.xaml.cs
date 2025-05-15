using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using ExplorerPro.UI.Panels.ToDoPanel;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// Interaction logic for ManageRecurringItemsDialog.xaml
    /// </summary>
    public partial class ManageRecurringItemsDialog : Window
    {
        private readonly Models.RecurringTaskManager _recurringTaskManager;
        
        /// <summary>
        /// Initializes a new instance of the ManageRecurringItemsDialog class.
        /// </summary>
        /// <param name="recurringTaskManager">The recurring task manager instance.</param>
        public ManageRecurringItemsDialog(Models.RecurringTaskManager recurringTaskManager)
        {
            InitializeComponent();
            _recurringTaskManager = recurringTaskManager;
            
            // Load recurring items
            LoadRecurringItems();
        }
        
        /// <summary>
        /// Initializes a new instance of the ManageRecurringItemsDialog class.
        /// </summary>
        public ManageRecurringItemsDialog()
        {
            InitializeComponent();
            _recurringTaskManager = Models.RecurringTaskManager.Instance;
            
            // Load recurring items
            LoadRecurringItems();
        }
        
        /// <summary>
        /// Loads recurring items into the grid.
        /// </summary>
        private void LoadRecurringItems()
        {
            var recurrences = _recurringTaskManager.GetAllRecurrences();
            var items = new List<RecurrenceDisplayItem>();
            
            foreach (var kvp in recurrences)
            {
                items.Add(new RecurrenceDisplayItem
                {
                    Id = kvp.Key,
                    Name = kvp.Value.Name,
                    Frequency = kvp.Value.Frequency,
                    NextDueDate = kvp.Value.NextDueDate,
                    Priority = kvp.Value.Priority
                });
            }
            
            recurringItemsGrid.ItemsSource = items;
        }
        
        /// <summary>
        /// Handles the click event for the Edit button.
        /// </summary>
        private void BtnEdit_Click(object sender, RoutedEventArgs e)
        {
            if (recurringItemsGrid.SelectedItem is RecurrenceDisplayItem selectedItem)
            {
                var recurrence = _recurringTaskManager.GetRecurrence(selectedItem.Id);
                if (recurrence != null)
                {
                    // Create a ViewModel from the RecurrenceItem
                    var viewModel = new RecurringItemViewModel
                    {
                        TaskUuid = selectedItem.Id,
                        Name = recurrence.Name,
                        Frequency = recurrence.Frequency,
                        NextDueDate = recurrence.NextDueDate,
                        Priority = recurrence.Priority,
                        OriginalDueDay = recurrence.OriginalDueDay,
                        ShiftWeekends = recurrence.ShiftWeekends
                    };
                    
                    var dialog = new EditRecurrenceDialog(viewModel);
                    if (dialog.ShowDialog() == true)
                    {
                        // Get updated data and save to manager
                        var updatedData = dialog.GetUpdatedData();
                        _recurringTaskManager.UpdateRecurrence(
                            updatedData.TaskUuid,
                            updatedData.Name,
                            updatedData.Frequency,
                            updatedData.NextDueDate,
                            updatedData.Priority,
                            updatedData.OriginalDueDay,
                            updatedData.ShiftWeekends
                        );
                        
                        // Refresh the list
                        LoadRecurringItems();
                    }
                }
            }
            else
            {
                MessageBox.Show("Please select an item to edit.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        /// <summary>
        /// Handles the click event for the Remove button.
        /// </summary>
        private void BtnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (recurringItemsGrid.SelectedItem is RecurrenceDisplayItem selectedItem)
            {
                if (MessageBox.Show($"Are you sure you want to remove the recurring task '{selectedItem.Name}'?", 
                    "Confirm Removal", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    _recurringTaskManager.RemoveRecurrence(selectedItem.Id);
                    LoadRecurringItems();
                }
            }
            else
            {
                MessageBox.Show("Please select an item to remove.", "No Selection", 
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        
        /// <summary>
        /// Handles the click event for the Close button.
        /// </summary>
        private void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
    
    /// <summary>
    /// Display item for recurring tasks in the grid.
    /// </summary>
    public class RecurrenceDisplayItem
    {
        /// <summary>
        /// Gets or sets the ID of the recurrence.
        /// </summary>
        public string Id { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the name of the task.
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the frequency of the recurrence.
        /// </summary>
        public string Frequency { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the next due date.
        /// </summary>
        public string NextDueDate { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the priority.
        /// </summary>
        public string Priority { get; set; } = string.Empty;
    }
}