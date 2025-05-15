using System;
using System.Windows;
using System.Windows.Controls;

namespace ExplorerPro.UI.Controls
{
    /// <summary>
    /// Interaction logic for DateEditControl.xaml
    /// </summary>
    public partial class DateEditControl : UserControl
    {
        /// <summary>
        /// Event fired when the selected date changes
        /// </summary>
        public event EventHandler<DateChangedEventArgs> DateChanged;

        /// <summary>
        /// The currently selected date
        /// </summary>
        public DateTime? SelectedDate
        {
            get { return DatePicker.SelectedDate; }
            set { DatePicker.SelectedDate = value; }
        }

        /// <summary>
        /// Whether the date picker shows a calendar popup
        /// </summary>
        public bool IsCalendarOpen
        {
            get { return DatePicker.IsDropDownOpen; }
            set { DatePicker.IsDropDownOpen = value; }
        }

        /// <summary>
        /// The string format used to display the date
        /// </summary>
        public string DateFormat
        {
            get { return DatePicker.SelectedDateFormat == DatePickerFormat.Long ? "yyyy-MM-dd" : "MM/dd/yyyy"; }
            set 
            { 
                if (value == "yyyy-MM-dd")
                {
                    DatePicker.SelectedDateFormat = DatePickerFormat.Long;
                }
                else
                {
                    DatePicker.SelectedDateFormat = DatePickerFormat.Short;
                }
            }
        }

        /// <summary>
        /// Constructor for the DateEditControl
        /// </summary>
        public DateEditControl()
        {
            InitializeComponent();
            
            // Set default values
            DatePicker.SelectedDate = DateTime.Today;
            DatePicker.SelectedDateFormat = DatePickerFormat.Long;
            DatePicker.DisplayDateStart = new DateTime(2000, 1, 1);
            DatePicker.DisplayDateEnd = new DateTime(2050, 12, 31);
        }

        /// <summary>
        /// Event handler for when the selected date changes
        /// </summary>
        private void DatePicker_SelectedDateChanged(object sender, SelectionChangedEventArgs e)
        {
            // Raise the DateChanged event
            DateChanged?.Invoke(this, new DateChangedEventArgs(DatePicker.SelectedDate));
        }

        /// <summary>
        /// Event handler for the clear button
        /// </summary>
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            // Clear the selected date
            DatePicker.SelectedDate = null;
            
            // Raise the DateChanged event
            DateChanged?.Invoke(this, new DateChangedEventArgs(null));
        }

        /// <summary>
        /// Sets the focus to the date picker
        /// </summary>
        public new void Focus()
        {
            DatePicker.Focus();
        }

        /// <summary>
        /// Apply a custom theme to the control
        /// </summary>
        /// <param name="isDarkMode">Whether dark mode is enabled</param>
        public void ApplyTheme(bool isDarkMode)
        {
            if (isDarkMode)
            {
                // Set dark mode colors
                DatePicker.Background = System.Windows.Media.Brushes.DarkGray;
                DatePicker.Foreground = System.Windows.Media.Brushes.White;
                ClearButton.Background = System.Windows.Media.Brushes.DarkGray;
                ClearButton.Foreground = System.Windows.Media.Brushes.White;
            }
            else
            {
                // Set light mode colors
                DatePicker.Background = System.Windows.Media.Brushes.White;
                DatePicker.Foreground = System.Windows.Media.Brushes.Black;
                ClearButton.Background = System.Windows.Media.Brushes.LightGray;
                ClearButton.Foreground = System.Windows.Media.Brushes.Black;
            }
        }
    }

    /// <summary>
    /// Event args for the DateChanged event
    /// </summary>
    public class DateChangedEventArgs : EventArgs
    {
        /// <summary>
        /// The new selected date
        /// </summary>
        public DateTime? NewDate { get; private set; }

        /// <summary>
        /// Constructor for DateChangedEventArgs
        /// </summary>
        /// <param name="newDate">The new selected date</param>
        public DateChangedEventArgs(DateTime? newDate)
        {
            NewDate = newDate;
        }
    }
}