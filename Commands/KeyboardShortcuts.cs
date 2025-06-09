using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace ExplorerPro.Commands
{
    /// <summary>
    /// Centralized definition of all keyboard shortcuts used throughout the application.
    /// IMPLEMENTS FIX 6: Command Binding Memory Overhead - Provides organized shortcut definitions
    /// </summary>
    public static class KeyboardShortcuts
    {
        /// <summary>
        /// All standard keyboard shortcuts used in the application.
        /// </summary>
        public static readonly ShortcutDefinition[] Shortcuts = 
        {
            // Navigation shortcuts
            new("GoUp", Key.Up, ModifierKeys.Alt, "Go Up"),
            new("Refresh", Key.F5, ModifierKeys.None, "Refresh"),
            new("FocusAddressBar", Key.D, ModifierKeys.Alt, "Focus Address Bar"),
            new("FocusAddressBarAlt", Key.L, ModifierKeys.Control, "Focus Address Bar (Alt)"),
            new("FocusSearch", Key.F, ModifierKeys.Control, "Focus Search"),
            new("FocusSearchAlt", Key.F3, ModifierKeys.None, "Focus Search (Alt)"),
            new("GoBack", Key.Left, ModifierKeys.Alt, "Go Back"),
            new("GoForward", Key.Right, ModifierKeys.Alt, "Go Forward"),

            // File operation shortcuts
            new("NewFolder", Key.N, ModifierKeys.Control | ModifierKeys.Shift, "New Folder"),
            new("NewFile", Key.N, ModifierKeys.Control | ModifierKeys.Alt, "New File"),
            
            // Panel toggle shortcuts
            new("TogglePinnedPanel", Key.P, ModifierKeys.Control, "Toggle Pinned Panel"),
            new("ToggleBookmarksPanel", Key.B, ModifierKeys.Control, "Toggle Bookmarks Panel"),
            new("ToggleTodoPanel", Key.D, ModifierKeys.Control, "Toggle ToDo Panel"),
            new("ToggleProcorePanel", Key.K, ModifierKeys.Control, "Toggle Procore Panel"),
            
            // Sidebar toggles (VS Code style)
            new("ToggleLeftSidebar", Key.B, ModifierKeys.Alt | ModifierKeys.Shift, "Toggle Left Sidebar"),
            new("ToggleRightSidebar", Key.R, ModifierKeys.Alt | ModifierKeys.Shift, "Toggle Right Sidebar"),

            // Tab shortcuts
            new("NextTab", Key.Tab, ModifierKeys.Control, "Next Tab"),
            new("PreviousTab", Key.Tab, ModifierKeys.Control | ModifierKeys.Shift, "Previous Tab"),
            new("NewTab", Key.T, ModifierKeys.Control, "New Tab"),
            new("CloseTab", Key.W, ModifierKeys.Control, "Close Tab"),
            new("ToggleSplitView", Key.OemBackslash, ModifierKeys.Control, "Toggle Split View"),
            
            // View shortcuts
            new("ToggleFullscreen", Key.F10, ModifierKeys.None, "Toggle Fullscreen"),
            new("ZoomIn", Key.OemPlus, ModifierKeys.Control, "Zoom In"),
            new("ZoomOut", Key.OemMinus, ModifierKeys.Control, "Zoom Out"),
            new("ZoomReset", Key.D0, ModifierKeys.Control, "Reset Zoom"),

            // Theme shortcut
            new("ToggleTheme", Key.T, ModifierKeys.Control | ModifierKeys.Shift, "Toggle Theme"),

            // Utility shortcuts
            new("ShowHelp", Key.F1, ModifierKeys.None, "Help"),
            new("OpenSettings", Key.OemComma, ModifierKeys.Control, "Settings"),
            new("ToggleHiddenFiles", Key.H, ModifierKeys.Control, "Toggle Hidden Files"),
            new("EscapeAction", Key.Escape, ModifierKeys.None, "Escape Current Operation")
        };

        /// <summary>
        /// Standard file operations shortcuts that may be used across different contexts.
        /// </summary>
        public static readonly ShortcutDefinition[] FileOperationShortcuts =
        {
            new("Cut", Key.X, ModifierKeys.Control, "Cut"),
            new("Copy", Key.C, ModifierKeys.Control, "Copy"),
            new("Paste", Key.V, ModifierKeys.Control, "Paste"),
            new("Delete", Key.Delete, ModifierKeys.None, "Delete"),
            new("SelectAll", Key.A, ModifierKeys.Control, "Select All"),
            new("Find", Key.F, ModifierKeys.Control, "Find"),
            new("Rename", Key.F2, ModifierKeys.None, "Rename"),
            new("Properties", Key.Return, ModifierKeys.Alt, "Show Properties")
        };

        /// <summary>
        /// All shortcuts combined for easy enumeration.
        /// </summary>
        public static IEnumerable<ShortcutDefinition> AllShortcuts => 
            Shortcuts.Concat(FileOperationShortcuts);

        /// <summary>
        /// Represents a keyboard shortcut definition.
        /// </summary>
        public class ShortcutDefinition
        {
            public string Name { get; }
            public Key Key { get; }
            public ModifierKeys Modifiers { get; }
            public string Description { get; }
            
            public ShortcutDefinition(string name, Key key, ModifierKeys modifiers = ModifierKeys.None, string description = "")
            {
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException("Shortcut name cannot be null or empty", nameof(name));
                
                Name = name;
                Key = key;
                Modifiers = modifiers;
                Description = string.IsNullOrEmpty(description) ? name : description;
            }
            
            /// <summary>
            /// Gets a string representation of the key combination.
            /// </summary>
            public string KeyCombination
            {
                get
                {
                    var parts = new List<string>();
                    
                    if ((Modifiers & ModifierKeys.Control) != 0)
                        parts.Add("Ctrl");
                    if ((Modifiers & ModifierKeys.Alt) != 0)
                        parts.Add("Alt");
                    if ((Modifiers & ModifierKeys.Shift) != 0)
                        parts.Add("Shift");
                    if ((Modifiers & ModifierKeys.Windows) != 0)
                        parts.Add("Win");
                    
                    parts.Add(Key.ToString());
                    
                    return string.Join("+", parts);
                }
            }
            
            /// <summary>
            /// Creates a KeyGesture for this shortcut.
            /// </summary>
            public KeyGesture CreateKeyGesture()
            {
                return new KeyGesture(Key, Modifiers);
            }
            
            /// <summary>
            /// Creates an InputGestureCollection containing this shortcut.
            /// </summary>
            public InputGestureCollection CreateInputGestureCollection()
            {
                return new InputGestureCollection { CreateKeyGesture() };
            }
            
            public override string ToString()
            {
                return $"{Name}: {KeyCombination} - {Description}";
            }
            
            public override bool Equals(object obj)
            {
                return obj is ShortcutDefinition other &&
                       Name == other.Name &&
                       Key == other.Key &&
                       Modifiers == other.Modifiers;
            }
            
            public override int GetHashCode()
            {
                return HashCode.Combine(Name, Key, Modifiers);
            }
        }
        
        /// <summary>
        /// Finds a shortcut definition by name.
        /// </summary>
        /// <param name="name">The shortcut name to search for</param>
        /// <returns>The shortcut definition if found, null otherwise</returns>
        public static ShortcutDefinition FindByName(string name)
        {
            return AllShortcuts.FirstOrDefault(s => 
                string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase));
        }
        
        /// <summary>
        /// Finds shortcut definitions by key combination.
        /// </summary>
        /// <param name="key">The key</param>
        /// <param name="modifiers">The modifier keys</param>
        /// <returns>All shortcuts that use this key combination</returns>
        public static IEnumerable<ShortcutDefinition> FindByKeyCombination(Key key, ModifierKeys modifiers)
        {
            return AllShortcuts.Where(s => s.Key == key && s.Modifiers == modifiers);
        }
        
        /// <summary>
        /// Gets all shortcut descriptions for help/documentation purposes.
        /// </summary>
        /// <returns>Dictionary mapping shortcut names to their descriptions and key combinations</returns>
        public static Dictionary<string, string> GetShortcutDescriptions()
        {
            return AllShortcuts.ToDictionary(
                s => s.Name,
                s => $"{s.KeyCombination} - {s.Description}"
            );
        }
        
        /// <summary>
        /// Validates that there are no duplicate key combinations.
        /// </summary>
        /// <returns>List of conflicting shortcuts</returns>
        public static List<ShortcutConflict> ValidateShortcuts()
        {
            var conflicts = new List<ShortcutConflict>();
            var shortcuts = AllShortcuts.ToList();
            
            for (int i = 0; i < shortcuts.Count; i++)
            {
                for (int j = i + 1; j < shortcuts.Count; j++)
                {
                    var shortcut1 = shortcuts[i];
                    var shortcut2 = shortcuts[j];
                    
                    if (shortcut1.Key == shortcut2.Key && shortcut1.Modifiers == shortcut2.Modifiers)
                    {
                        conflicts.Add(new ShortcutConflict(shortcut1, shortcut2));
                    }
                }
            }
            
            return conflicts;
        }
        
        /// <summary>
        /// Represents a conflict between two shortcuts with the same key combination.
        /// </summary>
        public class ShortcutConflict
        {
            public ShortcutDefinition Shortcut1 { get; }
            public ShortcutDefinition Shortcut2 { get; }
            
            public ShortcutConflict(ShortcutDefinition shortcut1, ShortcutDefinition shortcut2)
            {
                Shortcut1 = shortcut1;
                Shortcut2 = shortcut2;
            }
            
            public override string ToString()
            {
                return $"Conflict: {Shortcut1.Name} and {Shortcut2.Name} both use {Shortcut1.KeyCombination}";
            }
        }
    }
} 