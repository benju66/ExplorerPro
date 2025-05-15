// RecurringTaskManager.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ExplorerPro.Models
{
    /// <summary>
    /// Manager for recurring tasks
    /// </summary>
    public sealed class RecurringTaskManager
    {
        #region Singleton
        
        private static readonly Lazy<RecurringTaskManager> _instance = 
            new Lazy<RecurringTaskManager>(() => new RecurringTaskManager());
            
        /// <summary>
        /// Gets the singleton instance
        /// </summary>
        public static RecurringTaskManager Instance => _instance.Value;
        
        #endregion
        
        #region Fields
        
        private readonly string _filePath;
        private Dictionary<string, RecurrenceItem> _data;
        private readonly ILogger<RecurringTaskManager>? _logger;
        private readonly IRecurrenceTaskSpawner? _taskSpawner;
        
        #endregion
        
        #region Constructors
        
        /// <summary>
        /// Private constructor for singleton
        /// </summary>
        private RecurringTaskManager()
        {
            _filePath = Path.Combine("Data", "recurrence.json");
            _data = new Dictionary<string, RecurrenceItem>();
            LoadData();
        }
        
        /// <summary>
        /// Internal constructor for testing
        /// </summary>
        internal RecurringTaskManager(IRecurrenceTaskSpawner? taskSpawner, ILogger<RecurringTaskManager>? logger)
        {
            _taskSpawner = taskSpawner;
            _logger = logger;
            _filePath = Path.Combine("Data", "recurrence.json");
            _data = new Dictionary<string, RecurrenceItem>();
            LoadData();
        }
        
        /// <summary>
        /// Internal constructor with filepath for testing
        /// </summary>
        internal RecurringTaskManager(string filePath, IRecurrenceTaskSpawner? taskSpawner, ILogger<RecurringTaskManager>? logger)
        {
            _filePath = filePath;
            _taskSpawner = taskSpawner;
            _logger = logger;
            _data = new Dictionary<string, RecurrenceItem>();
            LoadData();
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Loads recurrence data from JSON
        /// </summary>
        public void LoadData()
        {
            if (!File.Exists(_filePath))
            {
                _data = new Dictionary<string, RecurrenceItem>();
                return;
            }
            
            try
            {
                string json = File.ReadAllText(_filePath);
                JObject jsonObject = JObject.Parse(json);
                
                _data = new Dictionary<string, RecurrenceItem>();
                
                foreach (var property in jsonObject.Properties())
                {
                    string taskUuid = property.Name;
                    JToken itemData = property.Value;
                    
                    _data[taskUuid] = new RecurrenceItem
                    {
                        Name = itemData["name"]?.ToString() ?? string.Empty,
                        Frequency = itemData["frequency"]?.ToString() ?? string.Empty,
                        NextDueDate = itemData["next_due_date"]?.ToString() ?? string.Empty,
                        Priority = itemData["priority"]?.ToString() ?? string.Empty,
                        OriginalDueDay = itemData["original_due_day"]?.Type == JTokenType.Null 
                            ? null 
                            : (int?)itemData["original_due_day"],
                        ShiftWeekends = itemData["shift_weekends"]?.ToObject<bool>() ?? true
                    };
                }
                
                _logger?.LogInformation($"Loaded {_data.Count} recurring items from {_filePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error loading recurrence data from {_filePath}");
                _data = new Dictionary<string, RecurrenceItem>();
            }
        }
        
        /// <summary>
        /// Saves recurrence data to JSON
        /// </summary>
        public void SaveData()
        {
            try
            {
                string? directoryPath = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }
                
                JObject json = new JObject();
                
                foreach (var kvp in _data)
                {
                    string taskUuid = kvp.Key;
                    RecurrenceItem item = kvp.Value;
                    
                    JObject itemJson = new JObject
                    {
                        ["name"] = item.Name,
                        ["frequency"] = item.Frequency,
                        ["next_due_date"] = item.NextDueDate,
                        ["priority"] = item.Priority,
                        ["original_due_day"] = item.OriginalDueDay.HasValue ? new JValue(item.OriginalDueDay.Value) : JValue.CreateNull(),
                        ["shift_weekends"] = item.ShiftWeekends
                    };
                    
                    json[taskUuid] = itemJson;
                }
                
                File.WriteAllText(_filePath, json.ToString(Formatting.Indented));
                _logger?.LogInformation($"Saved {_data.Count} recurring items to {_filePath}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error saving recurrence data to {_filePath}");
            }
        }
        
        /// <summary>
        /// Adds or updates a recurring task
        /// </summary>
        public void AddOrUpdateRecurrence(string taskUuid, string name, string frequency, string dueDate, string priority = "")
        {
            // Remove recurrence if frequency is None
            if (frequency == "None")
            {
                if (_data.ContainsKey(taskUuid))
                {
                    _data.Remove(taskUuid);
                    SaveData();
                }
                return;
            }
            
            // Calculate original day for monthly recurrences
            int? originalDay = null;
            if (frequency == "Monthly" && !string.IsNullOrEmpty(dueDate))
            {
                if (DateTime.TryParse(dueDate, out DateTime dt))
                {
                    originalDay = dt.Day;
                }
            }
            
            // Create or update the recurrence
            _data[taskUuid] = new RecurrenceItem
            {
                Name = name,
                Frequency = frequency,
                NextDueDate = dueDate,
                Priority = priority,
                OriginalDueDay = originalDay,
                ShiftWeekends = true
            };
            
            SaveData();
        }
        
        /// <summary>
        /// Updates an existing recurrence
        /// </summary>
        public void UpdateRecurrence(string taskUuid, string name, string frequency, string nextDueDate, 
            string priority, int? originalDueDay, bool shiftWeekends)
        {
            if (!_data.ContainsKey(taskUuid))
                return;
            
            _data[taskUuid] = new RecurrenceItem
            {
                Name = name,
                Frequency = frequency,
                NextDueDate = nextDueDate,
                Priority = priority,
                OriginalDueDay = originalDueDay,
                ShiftWeekends = shiftWeekends
            };
            
            SaveData();
            _logger?.LogInformation($"Updated recurring task: {name} with pattern {frequency}");
        }
        
        /// <summary>
        /// Removes a recurrence
        /// </summary>
        public void RemoveRecurrence(string taskUuid)
        {
            if (_data.ContainsKey(taskUuid))
            {
                string name = _data[taskUuid].Name;
                _data.Remove(taskUuid);
                SaveData();
                _logger?.LogInformation($"Removed recurring task: {name}");
            }
        }
        
        /// <summary>
        /// Adds a recurring task from a TaskItem
        /// </summary>
        public void AddRecurringTask(UI.Panels.ToDoPanel.TaskItem task)
        {
            if (task == null || !task.IsRecurring || string.IsNullOrEmpty(task.RecurrencePattern))
                return;
            
            string taskUuid = Guid.NewGuid().ToString();
            
            AddOrUpdateRecurrence(
                taskUuid,
                task.Text,
                task.RecurrencePattern,
                task.DueDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                task.Priority.ToString()
            );
        }
        
        /// <summary>
        /// Updates an existing recurring task
        /// </summary>
        public void UpdateRecurringTask(UI.Panels.ToDoPanel.TaskItem task)
        {
            var match = _data.FirstOrDefault(r => r.Value.Name == task.Text);
            
            if (!string.IsNullOrEmpty(match.Key))
            {
                UpdateRecurrence(
                    match.Key,
                    task.Text,
                    task.RecurrencePattern,
                    task.DueDate?.ToString("yyyy-MM-dd") ?? DateTime.Now.ToString("yyyy-MM-dd"),
                    task.Priority.ToString(),
                    task.DueDate?.Day,
                    true
                );
            }
            else
            {
                AddRecurringTask(task);
            }
        }
        
        /// <summary>
        /// Removes a recurring task
        /// </summary>
        public void RemoveRecurringTask(UI.Panels.ToDoPanel.TaskItem task)
        {
            var match = _data.FirstOrDefault(r => r.Value.Name == task.Text);
            
            if (!string.IsNullOrEmpty(match.Key))
            {
                RemoveRecurrence(match.Key);
            }
        }
        
        /// <summary>
        /// Checks for recurring tasks due tomorrow
        /// </summary>
        public void CheckAndSpawnRecurrences()
        {
            if (_taskSpawner == null)
            {
                _logger?.LogWarning("Cannot check recurrences: no task spawner provided");
                return;
            }
            
            string today = DateTime.Now.ToString("yyyy-MM-dd");
            
            foreach (var kvp in _data.ToList())
            {
                string taskUuid = kvp.Key;
                RecurrenceItem recurrence = kvp.Value;
                
                string dueStr = recurrence.NextDueDate;
                if (string.IsNullOrEmpty(dueStr) || recurrence.Frequency == "None")
                {
                    continue;
                }
                
                try
                {
                    DateTime dueDate = DateTime.Parse(dueStr);
                    
                    if (dueDate.AddDays(-1).ToString("yyyy-MM-dd") == today)
                    {
                        _taskSpawner.SpawnRecurringItem(taskUuid, recurrence);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, $"Error checking recurrence for task {taskUuid}");
                }
            }
        }
        
        /// <summary>
        /// Processes due recurring tasks
        /// </summary>
        public List<UI.Panels.ToDoPanel.TaskItem> ProcessDueRecurringTasks()
        {
            var newTasks = new List<UI.Panels.ToDoPanel.TaskItem>();
            var today = DateTime.Today;
            var tasksToUpdate = new List<string>();
            
            foreach (var item in _data)
            {
                string taskUuid = item.Key;
                var info = item.Value;
                
                if (DateTime.TryParse(info.NextDueDate, out DateTime dueDate) && dueDate <= today)
                {
                    var newTask = new UI.Panels.ToDoPanel.TaskItem
                    {
                        Text = info.Name,
                        DueDate = dueDate,
                        Category = "Other",
                        Priority = int.TryParse(info.Priority, out int priority) ? priority : 3,
                        IsRecurring = false
                    };
                    
                    newTasks.Add(newTask);
                    
                    DateTime nextDue = CalculateNextDueDate(
                        dueDate, 
                        info.Frequency,
                        info.OriginalDueDay,
                        info.ShiftWeekends);
                    
                    info.NextDueDate = nextDue.ToString("yyyy-MM-dd");
                    tasksToUpdate.Add(taskUuid);
                }
            }
            
            if (tasksToUpdate.Count > 0)
            {
                SaveData();
                _logger?.LogInformation($"Generated {newTasks.Count} tasks from recurring patterns");
            }
            
            return newTasks;
        }
        
        /// <summary>
        /// Handles completion of a recurring task
        /// </summary>
        public void HandleCompletionOfRecurring(string taskUuid)
        {
            if (!_data.ContainsKey(taskUuid))
            {
                return;
            }
            
            RecurrenceItem recurrence = _data[taskUuid];
            string frequency = recurrence.Frequency;
            string dueStr = recurrence.NextDueDate;
            
            if (string.IsNullOrEmpty(dueStr))
            {
                return;
            }
            
            try
            {
                DateTime oldDate = DateTime.Parse(dueStr);
                DateTime newDate = oldDate;
                
                switch (frequency)
                {
                    case "Daily":
                        newDate = oldDate.AddDays(1);
                        break;
                    case "Weekly":
                        newDate = oldDate.AddDays(7);
                        break;
                    case "Monthly":
                        int origDay = recurrence.OriginalDueDay ?? oldDate.Day;
                        newDate = GetNextMonthDate(oldDate, origDay);
                        break;
                }
                
                if (recurrence.ShiftWeekends)
                {
                    newDate = ShiftIfWeekend(newDate);
                }
                
                recurrence.NextDueDate = newDate.ToString("yyyy-MM-dd");
                _data[taskUuid] = recurrence;
                SaveData();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error handling completion of recurring task {taskUuid}");
            }
        }
        
        /// <summary>
        /// Gets all recurrences
        /// </summary>
        public Dictionary<string, RecurrenceItem> GetAllRecurrences()
        {
            return new Dictionary<string, RecurrenceItem>(_data);
        }
        
        /// <summary>
        /// Gets a specific recurrence
        /// </summary>
        public RecurrenceItem? GetRecurrence(string taskUuid)
        {
            return _data.TryGetValue(taskUuid, out RecurrenceItem item) ? item : null;
        }
        
        /// <summary>
        /// Gets all recurring tasks as TaskItems
        /// </summary>
        public List<UI.Panels.ToDoPanel.TaskItem> GetRecurringTasks()
        {
            var tasks = new List<UI.Panels.ToDoPanel.TaskItem>();
            
            foreach (var item in _data)
            {
                var info = item.Value;
                DateTime? dueDate = null;
                
                if (DateTime.TryParse(info.NextDueDate, out DateTime parsedDate))
                {
                    dueDate = parsedDate;
                }
                
                int priority = 3;
                if (int.TryParse(info.Priority, out int parsedPriority))
                {
                    priority = parsedPriority;
                }
                
                tasks.Add(new UI.Panels.ToDoPanel.TaskItem
                {
                    Text = info.Name,
                    DueDate = dueDate,
                    Category = "Other",
                    Priority = priority,
                    IsRecurring = true,
                    RecurrencePattern = info.Frequency
                });
            }
            
            return tasks;
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Calculates next due date
        /// </summary>
        private DateTime CalculateNextDueDate(DateTime currentDueDate, string? frequency, int? originalDueDay, bool shiftWeekends)
        {
            DateTime nextDue = currentDueDate;
            
            switch (frequency?.ToLower())
            {
                case "daily":
                    nextDue = currentDueDate.AddDays(1);
                    break;
                case "weekly":
                    nextDue = currentDueDate.AddDays(7);
                    break;
                case "monthly":
                    if (originalDueDay.HasValue)
                    {
                        nextDue = GetNextMonthDate(currentDueDate, originalDueDay.Value);
                    }
                    else
                    {
                        nextDue = GetNextMonthDate(currentDueDate, currentDueDate.Day);
                    }
                    break;
                case "yearly":
                    nextDue = currentDueDate.AddYears(1);
                    break;
                case "every weekday":
                    if (currentDueDate.DayOfWeek == DayOfWeek.Friday)
                    {
                        nextDue = currentDueDate.AddDays(3);
                    }
                    else
                    {
                        nextDue = currentDueDate.AddDays(1);
                    }
                    break;
                case "every weekend":
                    if (currentDueDate.DayOfWeek == DayOfWeek.Saturday)
                    {
                        nextDue = currentDueDate.AddDays(1);
                    }
                    else if (currentDueDate.DayOfWeek == DayOfWeek.Sunday)
                    {
                        nextDue = currentDueDate.AddDays(6);
                    }
                    else
                    {
                        int daysUntilSaturday = ((int)DayOfWeek.Saturday - (int)currentDueDate.DayOfWeek + 7) % 7;
                        nextDue = currentDueDate.AddDays(daysUntilSaturday);
                    }
                    break;
                case "every monday":
                case "every tuesday":
                case "every wednesday":
                case "every thursday":
                case "every friday":
                    nextDue = currentDueDate.AddDays(7);
                    break;
                default:
                    nextDue = currentDueDate.AddDays(7);
                    break;
            }
            
            if (shiftWeekends)
            {
                nextDue = ShiftIfWeekend(nextDue);
            }
            
            return nextDue;
        }
        
        /// <summary>
        /// Shifts weekend dates to Friday
        /// </summary>
        private DateTime ShiftIfWeekend(DateTime date)
        {
            while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
            {
                date = date.AddDays(-1);
            }
            return date;
        }
        
        /// <summary>
        /// Gets date for next month
        /// </summary>
        private DateTime GetNextMonthDate(DateTime oldDate, int originalDay)
        {
            int year = oldDate.Year;
            int month = oldDate.Month;
            
            if (month == 12)
            {
                year++;
                month = 1;
            }
            else
            {
                month++;
            }
            
            int daysInNewMonth = DateTime.DaysInMonth(year, month);
            int day = Math.Min(originalDay, daysInNewMonth);
            
            return new DateTime(year, month, day);
        }
        
        #endregion
    }
    
    /// <summary>
    /// Interface for spawning recurring tasks
    /// </summary>
    public interface IRecurrenceTaskSpawner
    {
        /// <summary>
        /// Creates a new UI item for a recurring task
        /// </summary>
        void SpawnRecurringItem(string taskUuid, RecurrenceItem recurrence);
    }
    
    /// <summary>
    /// Represents a recurring task
    /// </summary>
    public class RecurrenceItem
    {
        /// <summary>
        /// Gets or sets the task name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the frequency
        /// </summary>
        public string Frequency { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the next due date
        /// </summary>
        public string NextDueDate { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the priority
        /// </summary>
        public string Priority { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the original day of month
        /// </summary>
        public int? OriginalDueDay { get; set; }
        
        /// <summary>
        /// Gets or sets a value indicating whether to shift weekend due dates
        /// </summary>
        public bool ShiftWeekends { get; set; } = true;
    }
}