using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;

namespace ExplorerPro.Utilities
{
    /// <summary>
    /// Collection of extension methods to enhance functionality across the application
    /// </summary>
    public static class Extensions
    {
        #region String Extensions

        /// <summary>
        /// Truncates a string to the specified maximum length
        /// </summary>
        /// <param name="str">The string to truncate</param>
        /// <param name="maxLength">Maximum length</param>
        /// <param name="ellipsis">Whether to add an ellipsis when truncated</param>
        /// <returns>Truncated string</returns>
        public static string Truncate(this string str, int maxLength, bool ellipsis = true)
        {
            if (string.IsNullOrEmpty(str))
                return str;
                
            if (str.Length <= maxLength)
                return str;
                
            return ellipsis 
                ? str.Substring(0, maxLength - 3) + "..." 
                : str.Substring(0, maxLength);
        }

        /// <summary>
        /// Truncates a file path to a reasonable display length by shortening middle directories
        /// </summary>
        /// <param name="path">The file path to truncate</param>
        /// <param name="maxLength">Maximum desired length</param>
        /// <returns>Truncated path with full filename preserved</returns>
        public static string TruncatePath(this string path, int maxLength = 60)
        {
            if (string.IsNullOrEmpty(path) || path.Length <= maxLength)
                return path;

            // Get directory name and filename separately
            string directory = Path.GetDirectoryName(path);
            string fileName = Path.GetFileName(path);
            
            // Calculate how much space we have for the directory
            int directoryMaxLength = maxLength - fileName.Length - 3; // -3 for "..."
            
            if (directoryMaxLength <= 0)
            {
                // Not enough space for directory at all
                return "..." + Path.DirectorySeparatorChar + fileName;
            }
            
            if (directory.Length <= directoryMaxLength)
            {
                // Directory fits within max length
                return path;
            }
            
            // Split the directory path
            string[] dirParts = directory.Split(
                new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
                
            // Always keep the drive/root and the last directory
            string start = dirParts.Length > 0 ? dirParts[0] : "";
            string end = dirParts.Length > 1 ? dirParts[dirParts.Length - 1] : "";
            
            // Format the truncated path
            return $"{start}{Path.DirectorySeparatorChar}...{Path.DirectorySeparatorChar}{end}{Path.DirectorySeparatorChar}{fileName}";
        }

        /// <summary>
        /// Converts the first character of a string to uppercase
        /// </summary>
        /// <param name="input">Input string</param>
        /// <returns>String with first character uppercase</returns>
        public static string ToUpperFirst(this string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;
                
            if (input.Length == 1)
                return input.ToUpper();
                
            return char.ToUpper(input[0]) + input.Substring(1);
        }

        /// <summary>
        /// Checks if a string contains another string, case-insensitive
        /// </summary>
        /// <param name="source">Source string</param>
        /// <param name="toCheck">String to check for</param>
        /// <returns>True if source contains toCheck, ignoring case</returns>
        public static bool ContainsIgnoreCase(this string source, string toCheck)
        {
            return source?.IndexOf(toCheck, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        #endregion

        #region UI Extensions

        /// <summary>
        /// Finds a child element of the specified type and name within a parent element
        /// </summary>
        /// <typeparam name="T">Type of element to find</typeparam>
        /// <param name="parent">Parent dependency object</param>
        /// <param name="childName">Name of child element (optional)</param>
        /// <returns>Found child element or null</returns>
        public static T FindChild<T>(this DependencyObject parent, string childName = null) where T : DependencyObject
        {
            // Validate parameters
            if (parent == null) return null;

            T foundChild = null;
            int childrenCount = VisualTreeHelper.GetChildrenCount(parent);

            for (int i = 0; i < childrenCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                // If the child is of the correct type
                if (child is T typedChild)
                {
                    // Check if child's name matches, if provided
                    if (childName != null)
                    {
                        if (child is FrameworkElement frameworkElement && frameworkElement.Name == childName)
                        {
                            foundChild = typedChild;
                            break;
                        }
                    }
                    else
                    {
                        // Name doesn't matter, just return the first match
                        foundChild = typedChild;
                        break;
                    }
                }

                // Recursively check child elements
                T result = FindChild<T>(child, childName);
                if (result != null)
                {
                    foundChild = result;
                    break;
                }
            }

            return foundChild;
        }

        /// <summary>
        /// Gets the parent element of the specified type
        /// </summary>
        /// <typeparam name="T">Type of parent to find</typeparam>
        /// <param name="child">Child element</param>
        /// <returns>Parent element of specified type or null</returns>
        public static T FindParent<T>(this DependencyObject child) where T : DependencyObject
        {
            while (true)
            {
                // Get parent element
                DependencyObject parentObject = VisualTreeHelper.GetParent(child);
                
                if (parentObject == null)
                    return null;
                
                // Check if it's the requested type
                if (parentObject is T parent)
                    return parent;
                
                // Move up to the next parent
                child = parentObject;
            }
        }

        /// <summary>
        /// Executes an action on the UI thread
        /// </summary>
        /// <param name="control">Control to execute on</param>
        /// <param name="action">Action to execute</param>
        public static void InvokeOnUI(this Control control, Action action)
        {
            if (control.Dispatcher.CheckAccess())
            {
                action();
            }
            else
            {
                control.Dispatcher.Invoke(action);
            }
        }

        /// <summary>
        /// Executes an action on the UI thread asynchronously
        /// </summary>
        /// <param name="control">Control to execute on</param>
        /// <param name="action">Action to execute</param>
        /// <returns>Task representing the operation</returns>
        public static Task InvokeOnUIAsync(this Control control, Action action)
        {
            if (control.Dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }
            else
            {
                return control.Dispatcher.InvokeAsync(action).Task;
            }
        }

        #endregion

        #region File System Extensions

        /// <summary>
        /// Gets file attributes with exception handling
        /// </summary>
        /// <param name="path">File path</param>
        /// <returns>FileAttributes or null if failed</returns>
        public static FileAttributes? GetAttributesSafely(this string path)
        {
            try
            {
                return File.GetAttributes(path);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Checks if a path is a directory
        /// </summary>
        /// <param name="path">Path to check</param>
        /// <returns>True if path is a directory</returns>
        public static bool IsDirectory(this string path)
        {
            try
            {
                FileAttributes attr = File.GetAttributes(path);
                return (attr & FileAttributes.Directory) == FileAttributes.Directory;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Generates a unique file name in the specified directory
        /// </summary>
        /// <param name="directory">Directory path</param>
        /// <param name="fileName">Base file name</param>
        /// <returns>Unique file name</returns>
        public static string GetUniqueFileName(this string directory, string fileName)
        {
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            string extension = Path.GetExtension(fileName);
            string fullPath = Path.Combine(directory, fileName);
            int counter = 1;

            while (File.Exists(fullPath))
            {
                string newFileName = $"{baseName} ({counter}){extension}";
                fullPath = Path.Combine(directory, newFileName);
                counter++;
            }

            return Path.GetFileName(fullPath);
        }

        /// <summary>
        /// Gets a list of files in a directory matching a pattern, with exception handling
        /// </summary>
        /// <param name="directory">Directory to search</param>
        /// <param name="pattern">Search pattern</param>
        /// <param name="recursive">Whether to search recursively</param>
        /// <returns>Array of file paths, or empty array if failed</returns>
        public static string[] GetFilesSafely(this string directory, string pattern = "*.*", bool recursive = false)
        {
            try
            {
                return Directory.GetFiles(directory, pattern, 
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Gets a list of subdirectories in a directory, with exception handling
        /// </summary>
        /// <param name="directory">Directory to search</param>
        /// <param name="recursive">Whether to search recursively</param>
        /// <returns>Array of directory paths, or empty array if failed</returns>
        public static string[] GetDirectoriesSafely(this string directory, bool recursive = false)
        {
            try
            {
                return Directory.GetDirectories(directory, "*", 
                    recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        #endregion

        #region Collection Extensions

        /// <summary>
        /// Adds a range of items to a List
        /// </summary>
        /// <typeparam name="T">Type of items</typeparam>
        /// <param name="list">List to add to</param>
        /// <param name="items">Items to add</param>
        public static void AddRange<T>(this IList<T> list, IEnumerable<T> items)
        {
            if (list == null || items == null)
                return;
                
            foreach (var item in items)
            {
                list.Add(item);
            }
        }

        /// <summary>
        /// Returns a default value if the list is null or empty
        /// </summary>
        /// <typeparam name="T">Type of items</typeparam>
        /// <param name="source">Source collection</param>
        /// <param name="defaultValue">Default value</param>
        /// <returns>First item or default value</returns>
        public static T FirstOrDefault<T>(this IEnumerable<T> source, T defaultValue)
        {
            if (source == null || !source.Any())
                return defaultValue;
                
            return source.FirstOrDefault();
        }

        /// <summary>
        /// Converts an enumerable to a HashSet
        /// </summary>
        /// <typeparam name="T">Type of items</typeparam>
        /// <param name="source">Source collection</param>
        /// <returns>HashSet containing the items</returns>
        public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
        {
            return new HashSet<T>(source);
        }

        #endregion
    }
}

/// <summary>
/// Thread-safe extension methods for ObservableCollection
/// </summary>
public static class CollectionExtensions
{
    /// <summary>
    /// Adds an item to the collection in a thread-safe manner
    /// </summary>
    public static void AddSafe<T>(this ObservableCollection<T> collection, T item)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            collection.Add(item);
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(() => collection.Add(item));
        }
    }

    /// <summary>
    /// Removes an item from the collection in a thread-safe manner
    /// </summary>
    public static void RemoveSafe<T>(this ObservableCollection<T> collection, T item)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            collection.Remove(item);
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(() => collection.Remove(item));
        }
    }

    /// <summary>
    /// Clears the collection in a thread-safe manner
    /// </summary>
    public static void ClearSafe<T>(this ObservableCollection<T> collection)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            collection.Clear();
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(() => collection.Clear());
        }
    }

    /// <summary>
    /// Adds multiple items to the collection in a thread-safe manner
    /// </summary>
    public static void AddRangeSafe<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        if (Application.Current?.Dispatcher.CheckAccess() == true)
        {
            foreach (var item in items)
            {
                collection.Add(item);
            }
        }
        else
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (var item in items)
                {
                    collection.Add(item);
                }
            });
        }
    }
}