// UI/Panels/ToDoPanel/ToDoPanel.xaml.cs

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using ExplorerPro.Models;
using ExplorerPro.UI.Dialogs;

namespace ExplorerPro.UI.Panels.ToDoPanel
{
    /// <summary>
    /// Interaction logic for ToDoPanel.xaml
    /// </summary>
    public partial class ToDoPanel : DockPanel, INotifyPropertyChanged
    {
        #region Events
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        #endregion
        
        #region Fields
        
        private readonly RecurringTaskManager _recurringTaskManager;
        private readonly ILogger<ToDoPanel> _logger;
        private readonly string _tasksFilePath = "Data/tasks.json";
        private ObservableCollection<TaskItem> _tasks;
        private ICollectionView _tasksView;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Gets the collection of tasks
        /// </summary>
        public ObservableCollection<TaskItem> Tasks
        {
            get { return _tasks; }
            private set
            {
                _tasks = value;
                OnPropertyChanged(nameof(Tasks));
            }
        }
        
        #endregion
        
        #region Constructors
        
        /// <summary>
        /// Initializes a new instance of the ToDoPanel class
        /// </summary>
        public ToDoPanel()
        {
            InitializeComponent();
            
            // Initialize managers
            _recurringTaskManager = RecurringTaskManager.Instance;
            
            // Initialize tasks collection
            Tasks = new ObservableCollection<TaskItem>();
            
            // Set up the collection view for grouping and sorting
            _tasksView = CollectionViewSource.GetDefaultView(Tasks);
            _tasksView.GroupDescriptions.Add(new PropertyGroupDescription("Category"));
            _tasksView.SortDescriptions.Add(new SortDescription("DueDate", ListSortDirection.Ascending));
            _tasksView.SortDescriptions.Add(new SortDescription("Priority", ListSortDirection.Descending));
            
            // Bind the ListView to the collection view
            taskListView.ItemsSource = _tasksView;
            
            // Load tasks from file
            LoadTasks();
            
            // Check for due recurring tasks
            CheckRecurringTasks();
        }
        
        /// <summary>
        /// Initializes a new instance of the ToDoPanel class with logging
        /// </summary>
        public ToDoPanel(ILogger<ToDoPanel> logger) : this()
        {
            _logger = logger;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void AddTaskButton_Click(object sender, RoutedEventArgs e)
        {
            ShowAddTaskDialog();
        }
        
        private void ManageRecurringButton_Click(object sender, RoutedEventArgs e)
        {
            ShowManageRecurringTasksDialog();
        }
        
        private void TaskListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var item = GetSelectedTask();
            if (item != null)
            {
                EditTask(item);
            }
        }
        
        private void TaskListView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            var item = GetSelectedTask();
            if (item != null)
            {
                ShowTaskContextMenu(item);
            }
            else
            {
                e.Handled = true;
            }
        }
        
        private void TaskCheckBox_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox checkBox && checkBox.DataContext is TaskItem task)
            {
                task.IsCompleted = checkBox.IsChecked ?? false;
                SaveTasks();
            }
        }
        
        #endregion
        
        #region Task Management
        
        /// <summary>
        /// Loads tasks from the data file
        /// </summary>
        private void LoadTasks()
        {
            try
            {
                if (File.Exists(_tasksFilePath) && new FileInfo(_tasksFilePath).Length > 0)
                {
                    string json = File.ReadAllText(_tasksFilePath);
                    List<TaskItem>? loadedTasks = JsonConvert.DeserializeObject<List<TaskItem>>(json);
                    
                    Tasks.Clear();
                    if (loadedTasks != null)
                    {
                        foreach (var task in loadedTasks)
                        {
                            Tasks.Add(task);
                        }
                        
                        _logger?.LogInformation($"Loaded {Tasks.Count} tasks");
                    }
                    else
                    {
                        _logger?.LogInformation("Tasks file was corrupted or empty; starting with empty list");
                    }
                }
                else
                {
                    _logger?.LogInformation("Tasks file not found or empty, starting with empty list");
                    
                    // Make sure the directory exists
                    string? directory = Path.GetDirectoryName(_tasksFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                    
                    // Create an empty tasks file
                    File.WriteAllText(_tasksFilePath, "[]");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error loading tasks");
                MessageBox.Show($"Error loading tasks: {ex.Message}", 
                    "Task Loading Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Saves tasks to the data file
        /// </summary>
        private void SaveTasks()
        {
            try
            {
                // Create directory if it doesn't exist
                string directory = Path.GetDirectoryName(_tasksFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                // Create backup of current file if it exists
                if (File.Exists(_tasksFilePath))
                {
                    string backupDir = "todo_backups";
                    if (!Directory.Exists(backupDir))
                    {
                        Directory.CreateDirectory(backupDir);
                    }
                    
                    string backupFile = Path.Combine(backupDir, 
                        $"tasks_backup_{DateTime.Now:yyyyMMdd_HHmmss}.json");
                    
                    File.Copy(_tasksFilePath, backupFile);
                }
                
                // Save current tasks
                string json = JsonConvert.SerializeObject(Tasks, Formatting.Indented);
                File.WriteAllText(_tasksFilePath, json);
                
                _logger?.LogInformation($"Saved {Tasks.Count} tasks");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error saving tasks");
                MessageBox.Show($"Error saving tasks: {ex.Message}", 
                    "Task Saving Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Adds a new task
        /// </summary>
        /// <param name="task">The task to add</param>
        public void AddTask(TaskItem task)
        {
            if (task == null)
                return;
                
            Tasks.Add(task);
            SaveTasks();
            _logger?.LogInformation($"Added task: {task.Text}");
        }
        
        /// <summary>
        /// Updates an existing task
        /// </summary>
        /// <param name="task">The task to update</param>
        public void UpdateTask(TaskItem task)
        {
            // The observable collection will automatically update the UI
            // since TaskItem implements INotifyPropertyChanged
            SaveTasks();
            _logger?.LogInformation($"Updated task: {task.Text}");
        }
        
        /// <summary>
        /// Deletes a task
        /// </summary>
        /// <param name="task">The task to delete</param>
        public void DeleteTask(TaskItem task)
        {
            if (task == null)
                return;
                
            Tasks.Remove(task);
            SaveTasks();
            _logger?.LogInformation($"Deleted task: {task.Text}");
        }
        
        /// <summary>
        /// Gets the currently selected task
        /// </summary>
        /// <returns>The selected task, or null if none is selected</returns>
        private TaskItem GetSelectedTask()
        {
            return taskListView.SelectedItem as TaskItem;
        }
        
        /// <summary>
        /// Shows the dialog to add a new task
        /// </summary>
        private void ShowAddTaskDialog()
        {
            var dialog = new AddItemDialog();
            
            if (dialog.ShowDialog() == true)
            {
                var newTask = new TaskItem
                {
                    Text = dialog.TaskText,
                    DueDate = dialog.DueDate,
                    Category = dialog.Category,
                    Priority = dialog.Priority,
                    IsRecurring = dialog.IsRecurring,
                    RecurrencePattern = dialog.RecurrencePattern,
                    Notes = dialog.Notes
                };
                
                AddTask(newTask);
                
                // If recurring, add to recurrence manager
                if (dialog.IsRecurring && !string.IsNullOrEmpty(dialog.RecurrencePattern))
                {
                    _recurringTaskManager.AddRecurringTask(newTask);
                }
            }
        }
        
        /// <summary>
        /// Shows the dialog to edit an existing task
        /// </summary>
        /// <param name="task">The task to edit</param>
        private void EditTask(TaskItem task)
        {
            var dialog = new AddItemDialog
            {
                TaskText = task.Text,
                DueDate = task.DueDate,
                Category = task.Category,
                Priority = task.Priority,
                IsRecurring = task.IsRecurring,
                RecurrencePattern = task.RecurrencePattern,
                Notes = task.Notes,
                IsEditMode = true
            };
            
            if (dialog.ShowDialog() == true)
            {
                task.Text = dialog.TaskText;
                task.DueDate = dialog.DueDate;
                task.Category = dialog.Category;
                task.Priority = dialog.Priority;
                task.IsRecurring = dialog.IsRecurring;
                task.RecurrencePattern = dialog.RecurrencePattern;
                task.Notes = dialog.Notes;
                
                UpdateTask(task);
                
                // Update recurrence info if needed
                if (task.IsRecurring)
                {
                    _recurringTaskManager.UpdateRecurringTask(task);
                }
                else
                {
                    _recurringTaskManager.RemoveRecurringTask(task);
                }
            }
        }
        
        /// <summary>
        /// Shows the dialog to manage recurring tasks
        /// </summary>
        private void ShowManageRecurringTasksDialog()
        {
            var dialog = new ManageRecurringItemsDialog(_recurringTaskManager);
            
            if (dialog.ShowDialog() == true)
            {
                // Refresh tasks after managing recurring items
                CheckRecurringTasks();
            }
        }
        
        /// <summary>
        /// Checks for due recurring tasks and creates instances
        /// </summary>
        private void CheckRecurringTasks()
        {
            var newTasks = _recurringTaskManager.ProcessDueRecurringTasks();
            
            foreach (var task in newTasks)
            {
                Tasks.Add(task);
            }
            
            if (newTasks.Count > 0)
            {
                SaveTasks();
                _logger?.LogInformation($"Added {newTasks.Count} tasks from recurring patterns");
            }
        }
        
        #endregion
        
        #region Context Menu
        
        /// <summary>
        /// Shows the context menu for a task
        /// </summary>
        /// <param name="item">The task to show the menu for</param>
        private void ShowTaskContextMenu(TaskItem item)
        {
            ContextMenu menu = new ContextMenu();
            
            // Complete/Uncomplete
            MenuItem toggleCompleteMenuItem = new MenuItem
            {
                Header = item.IsCompleted ? "Mark as Incomplete" : "Mark as Complete",
                Icon = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri(item.IsCompleted ? 
                            "/Assets/Icons/square.png" : 
                            "/Assets/Icons/check-square.png", 
                            UriKind.Relative)),
                    Width = 16,
                    Height = 16
                }
            };
            toggleCompleteMenuItem.Click += (s, e) => 
            {
                item.IsCompleted = !item.IsCompleted;
                SaveTasks();
            };
            menu.Items.Add(toggleCompleteMenuItem);
            
            menu.Items.Add(new Separator());
            
            // Edit
            MenuItem editMenuItem = new MenuItem
            {
                Header = "Edit Task",
                Icon = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("/Assets/Icons/edit.png", UriKind.Relative)),
                    Width = 16,
                    Height = 16
                }
            };
            editMenuItem.Click += (s, e) => EditTask(item);
            menu.Items.Add(editMenuItem);
            
            // Delete
            MenuItem deleteMenuItem = new MenuItem
            {
                Header = "Delete Task",
                Icon = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("/Assets/Icons/trash.png", UriKind.Relative)),
                    Width = 16,
                    Height = 16
                }
            };
            deleteMenuItem.Click += (s, e) =>
            {
                if (MessageBox.Show($"Are you sure you want to delete this task?\n\n{item.Text}", 
                    "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                {
                    DeleteTask(item);
                }
            };
            menu.Items.Add(deleteMenuItem);
            
            menu.Items.Add(new Separator());
            
            // Change Category
            MenuItem categoryMenuItem = new MenuItem
            {
                Header = "Change Category",
                Icon = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("/Assets/Icons/folder.png", UriKind.Relative)),
                    Width = 16,
                    Height = 16
                }
            };
            
            // Add common categories as submenu items
            string[] commonCategories = { "Work", "Personal", "Shopping", "Health", "Finance", "Other" };
            foreach (string category in commonCategories)
            {
                MenuItem subItem = new MenuItem { Header = category };
                subItem.Click += (s, e) =>
                {
                    item.Category = category;
                    SaveTasks();
                };
                categoryMenuItem.Items.Add(subItem);
            }
            
            // Custom category option
            MenuItem customCategoryItem = new MenuItem { Header = "Custom..." };
            customCategoryItem.Click += (s, e) =>
            {
                var dialog = new InputDialog("Change Category", "Enter category name:", item.Category);
                if (dialog.ShowDialog() == true)
                {
                    item.Category = dialog.ResponseText;
                    SaveTasks();
                }
            };
            categoryMenuItem.Items.Add(new Separator());
            categoryMenuItem.Items.Add(customCategoryItem);
            
            menu.Items.Add(categoryMenuItem);
            
            // Change Priority
            MenuItem priorityMenuItem = new MenuItem
            {
                Header = "Change Priority",
                Icon = new Image
                {
                    Source = new System.Windows.Media.Imaging.BitmapImage(
                        new Uri("/Assets/Icons/alert-triangle.png", UriKind.Relative)),
                    Width = 16,
                    Height = 16
                }
            };
            
            for (int i = 5; i >= 1; i--)
            {
                MenuItem priorityItem = new MenuItem { Header = $"Priority {i}" };
                int priority = i; // Capture the value
                priorityItem.Click += (s, e) =>
                {
                    item.Priority = priority;
                    SaveTasks();
                };
                priorityMenuItem.Items.Add(priorityItem);
            }
            
            menu.Items.Add(priorityMenuItem);
            
            // Set the context menu
            taskContextMenu = menu;
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
    }
    
    /// <summary>
    /// Converter to show strikethrough text for completed tasks
    /// </summary>
    public class BoolToStrikethroughConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isCompleted = (bool)value;
            return isCompleted ? TextDecorations.Strikethrough : null;
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// Converter to show different colors for due dates
    /// </summary>
    public class DateToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || value == DependencyProperty.UnsetValue)
                return Brushes.Black;
                
            DateTime dueDate = (DateTime)value;
            DateTime today = DateTime.Today;
            
            if (dueDate < today)
                return Brushes.Red;       // Overdue
            if (dueDate == today)
                return Brushes.OrangeRed; // Due today
            if (dueDate <= today.AddDays(3))
                return Brushes.Orange;    // Due soon
                
            return Brushes.Black;         // Due later
        }
        
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
    
    /// <summary>
    /// Data model for a task item
    /// </summary>
    public class TaskItem : INotifyPropertyChanged
    {
        private string _text;
        private DateTime? _dueDate;
        private string _category;
        private int _priority;
        private bool _isCompleted;
        private bool _isRecurring;
        private string _recurrencePattern;
        private string _notes;
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        /// <summary>
        /// Gets or sets the task text
        /// </summary>
        public string Text
        {
            get { return _text; }
            set
            {
                _text = value;
                OnPropertyChanged(nameof(Text));
            }
        }
        
        /// <summary>
        /// Gets or sets the due date
        /// </summary>
        public DateTime? DueDate
        {
            get { return _dueDate; }
            set
            {
                _dueDate = value;
                OnPropertyChanged(nameof(DueDate));
            }
        }
        
        /// <summary>
        /// Gets or sets the category
        /// </summary>
        public string Category
        {
            get { return _category; }
            set
            {
                _category = value;
                OnPropertyChanged(nameof(Category));
            }
        }
        
        /// <summary>
        /// Gets or sets the priority (1-5, 5 being highest)
        /// </summary>
        public int Priority
        {
            get { return _priority; }
            set
            {
                _priority = value;
                OnPropertyChanged(nameof(Priority));
            }
        }
        
        /// <summary>
        /// Gets or sets whether the task is completed
        /// </summary>
        public bool IsCompleted
        {
            get { return _isCompleted; }
            set
            {
                _isCompleted = value;
                OnPropertyChanged(nameof(IsCompleted));
            }
        }
        
        /// <summary>
        /// Gets or sets whether the task is recurring
        /// </summary>
        public bool IsRecurring
        {
            get { return _isRecurring; }
            set
            {
                _isRecurring = value;
                OnPropertyChanged(nameof(IsRecurring));
            }
        }
        
        /// <summary>
        /// Gets or sets the recurrence pattern
        /// </summary>
        public string RecurrencePattern
        {
            get { return _recurrencePattern; }
            set
            {
                _recurrencePattern = value;
                OnPropertyChanged(nameof(RecurrencePattern));
            }
        }
        
        /// <summary>
        /// Gets or sets notes for the task
        /// </summary>
        public string Notes
        {
            get { return _notes; }
            set
            {
                _notes = value;
                OnPropertyChanged(nameof(Notes));
            }
        }
        
        /// <summary>
        /// Creates a new task item with default values
        /// </summary>
        public TaskItem()
        {
            Text = string.Empty;
            DueDate = null;
            Category = "Other";
            Priority = 3;
            IsCompleted = false;
            IsRecurring = false;
            RecurrencePattern = string.Empty;
            Notes = string.Empty;
        }
        
        /// <summary>
        /// Raises the PropertyChanged event
        /// </summary>
        /// <param name="propertyName">Name of the property that changed</param>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        /// <summary>
        /// Creates a clone of this task item
        /// </summary>
        /// <returns>A new task item with the same properties</returns>
        public TaskItem Clone()
        {
            return new TaskItem
            {
                Text = this.Text,
                DueDate = this.DueDate,
                Category = this.Category,
                Priority = this.Priority,
                IsCompleted = this.IsCompleted,
                IsRecurring = this.IsRecurring,
                RecurrencePattern = this.RecurrencePattern,
                Notes = this.Notes
            };
        }
    }
}