using System;

namespace ExplorerPro.UI.Dialogs
{
    /// <summary>
    /// View model for recurring task items
    /// </summary>
    public class RecurringItemViewModel
    {
        /// <summary>
        /// Gets or sets the task UUID
        /// </summary>
        public string TaskUuid { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the task name
        /// </summary>
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the frequency (Daily, Weekly, Monthly, etc.)
        /// </summary>
        public string Frequency { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the next due date (as string in format yyyy-MM-dd)
        /// </summary>
        public string NextDueDate { get; set; } = string.Empty;
        
        /// <summary>
        /// Gets or sets the priority (1-5)
        /// </summary>
        public string Priority { get; set; } = "3";
        
        /// <summary>
        /// Gets or sets the original day of month (for monthly recurrences)
        /// </summary>
        public int? OriginalDueDay { get; set; }
        
        /// <summary>
        /// Gets or sets whether to shift weekend due dates
        /// </summary>
        public bool ShiftWeekends { get; set; } = true;
        
        /// <summary>
        /// Creates a new RecurringItemViewModel from a RecurrenceItem
        /// </summary>
        /// <param name="taskUuid">The task UUID</param>
        /// <param name="item">The recurrence item</param>
        /// <returns>A new RecurringItemViewModel</returns>
        public static RecurringItemViewModel FromRecurrenceItem(string taskUuid, Models.RecurrenceItem item)
        {
            return new RecurringItemViewModel
            {
                TaskUuid = taskUuid,
                Name = item.Name,
                Frequency = item.Frequency,
                NextDueDate = item.NextDueDate,
                Priority = item.Priority,
                OriginalDueDay = item.OriginalDueDay,
                ShiftWeekends = item.ShiftWeekends
            };
        }
        
        /// <summary>
        /// Creates a new RecurringItemViewModel with default values
        /// </summary>
        public RecurringItemViewModel()
        {
            TaskUuid = Guid.NewGuid().ToString();
            Name = string.Empty;
            Frequency = "Weekly";
            NextDueDate = DateTime.Now.ToString("yyyy-MM-dd");
            Priority = "3";
            OriginalDueDay = null;
            ShiftWeekends = true;
        }
    }
}