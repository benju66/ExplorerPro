using System;
using System.Globalization;

namespace ExplorerPro.Utilities
{
    /// <summary>
    /// Provides methods for formatting dates and times in various user-friendly formats
    /// </summary>
    public static class DateFormatter
    {
        /// <summary>
        /// Formats a DateTime to a relative string like "Today", "Yesterday", "Last week", etc.
        /// </summary>
        /// <param name="dateTime">The date to format</param>
        /// <returns>A relative date string</returns>
        public static string FormatRelative(DateTime dateTime)
        {
            TimeSpan diff = DateTime.Now - dateTime;
            
            // For today, show the time
            if (dateTime.Date == DateTime.Today)
            {
                return $"Today at {dateTime.ToString("h:mm tt")}";
            }
            
            // For yesterday, show "Yesterday"
            if (dateTime.Date == DateTime.Today.AddDays(-1))
            {
                return $"Yesterday at {dateTime.ToString("h:mm tt")}";
            }
            
            // For within the last week, show day of week
            if (diff.TotalDays < 7)
            {
                return $"{dateTime.DayOfWeek} at {dateTime.ToString("h:mm tt")}";
            }
            
            // For within the last month
            if (diff.TotalDays < 30)
            {
                int weeks = (int)(diff.TotalDays / 7);
                return weeks == 1 
                    ? "Last week" 
                    : $"{weeks} weeks ago";
            }
            
            // For within the last year
            if (diff.TotalDays < 365)
            {
                int months = (int)(diff.TotalDays / 30);
                return months == 1 
                    ? "Last month" 
                    : $"{months} months ago";
            }
            
            // For older dates, show the full date
            int years = (int)(diff.TotalDays / 365);
            return years == 1 
                ? "Last year" 
                : $"{years} years ago";
        }

        /// <summary>
        /// Formats a DateTime to a short date string with time if it's today
        /// </summary>
        /// <param name="dateTime">The date to format</param>
        /// <returns>A formatted date string</returns>
        public static string FormatFileDate(DateTime dateTime)
        {
            // For today, just show the time
            if (dateTime.Date == DateTime.Today)
            {
                return dateTime.ToString("h:mm tt");
            }
            
            // For this year, show month and day
            if (dateTime.Year == DateTime.Today.Year)
            {
                return dateTime.ToString("MMM d");
            }
            
            // For other years, include the year
            return dateTime.ToString("MMM d, yyyy");
        }

        /// <summary>
        /// Formats a DateTime to a full date and time string
        /// </summary>
        /// <param name="dateTime">The date to format</param>
        /// <returns>A full formatted date and time string</returns>
        public static string FormatFull(DateTime dateTime)
        {
            return dateTime.ToString("F"); // e.g., "Tuesday, April 30, 2025 11:42:15 AM"
        }

        /// <summary>
        /// Formats a DateTime to a sortable string (useful for file naming)
        /// </summary>
        /// <param name="dateTime">The date to format</param>
        /// <returns>A sortable date string (e.g., "2025-04-30_11-42-15")</returns>
        public static string FormatSortable(DateTime dateTime)
        {
            return dateTime.ToString("yyyy-MM-dd_HH-mm-ss");
        }

        /// <summary>
        /// Formats a TimeSpan to a concise, human-readable string
        /// </summary>
        /// <param name="timeSpan">The TimeSpan to format</param>
        /// <returns>A human-readable duration string</returns>
        public static string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalSeconds < 1)
                return "Just now";
                
            if (timeSpan.TotalMinutes < 1)
                return $"{(int)timeSpan.TotalSeconds} sec";
                
            if (timeSpan.TotalHours < 1)
                return $"{(int)timeSpan.TotalMinutes} min" + 
                       (timeSpan.Seconds > 0 ? $" {timeSpan.Seconds} sec" : "");
                
            if (timeSpan.TotalDays < 1)
                return $"{(int)timeSpan.TotalHours} hr" + 
                       (timeSpan.Minutes > 0 ? $" {timeSpan.Minutes} min" : "");
                
            return $"{(int)timeSpan.TotalDays} day" + 
                   ((int)timeSpan.TotalDays != 1 ? "s" : "") +
                   (timeSpan.Hours > 0 ? $" {timeSpan.Hours} hr" : "");
        }

        /// <summary>
        /// Tries to parse a string into a DateTime using various common formats
        /// </summary>
        /// <param name="dateString">String to parse</param>
        /// <param name="result">Parsed DateTime if successful</param>
        /// <returns>True if parsing succeeded, false otherwise</returns>
        public static bool TryParseFlexible(string dateString, out DateTime result)
        {
            // Try exact formats first
            string[] formats = new[]
            {
                "yyyy-MM-dd",
                "yyyy/MM/dd",
                "MM/dd/yyyy",
                "MM-dd-yyyy",
                "dd/MM/yyyy",
                "dd-MM-yyyy",
                "yyyy-MM-dd HH:mm:ss",
                "yyyy/MM/dd HH:mm:ss",
                "MM/dd/yyyy HH:mm:ss",
            };

            if (DateTime.TryParseExact(dateString, formats, 
                CultureInfo.InvariantCulture, DateTimeStyles.None, out result))
            {
                return true;
            }

            // Then try general parsing
            return DateTime.TryParse(dateString, out result);
        }
    }
}