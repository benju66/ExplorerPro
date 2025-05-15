using System;

namespace ExplorerPro.UI.Panels.PinnedPanel
{
    /// <summary>
    /// Event arguments for events that pass a string value
    /// </summary>
    public class StringEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the string value associated with the event
        /// </summary>
        public string Value { get; }
        
        /// <summary>
        /// Initializes a new instance of StringEventArgs with the specified value
        /// </summary>
        /// <param name="value">The string value to pass with the event</param>
        public StringEventArgs(string value)
        {
            Value = value;
        }
    }

    /// <summary>
    /// Event arguments for events that pass an old path and new path
    /// </summary>
    public class ItemModifiedEventArgs : EventArgs
    {
        /// <summary>
        /// Gets the original path before modification
        /// </summary>
        public string OldPath { get; }
        
        /// <summary>
        /// Gets the new path after modification
        /// </summary>
        public string NewPath { get; }
        
        /// <summary>
        /// Initializes a new instance of ItemModifiedEventArgs with the specified paths
        /// </summary>
        /// <param name="oldPath">The original path before modification</param>
        /// <param name="newPath">The new path after modification</param>
        public ItemModifiedEventArgs(string oldPath, string newPath)
        {
            OldPath = oldPath;
            NewPath = newPath;
        }
    }
}