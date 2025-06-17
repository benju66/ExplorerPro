using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using ExplorerPro.Models;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Core interface for tab management operations.
    /// Centralizes all tab-related functionality to reduce coupling and improve maintainability.
    /// </summary>
    public interface ITabManagerService : IDisposable
    {
        #region Properties
        
        /// <summary>
        /// Collection of all tabs managed by this service
        /// </summary>
        ObservableCollection<TabModel> Tabs { get; }
        
        /// <summary>
        /// Currently active/selected tab
        /// </summary>
        TabModel ActiveTab { get; set; }
        
        /// <summary>
        /// Number of tabs currently managed
        /// </summary>
        int TabCount { get; }
        
        /// <summary>
        /// Whether there are any tabs open
        /// </summary>
        bool HasTabs { get; }
        
        #endregion

        #region Events
        
        /// <summary>
        /// Fired when a new tab is created
        /// </summary>
        event EventHandler<TabEventArgs> TabCreated;
        
        /// <summary>
        /// Fired when a tab is closed
        /// </summary>
        event EventHandler<TabEventArgs> TabClosed;
        
        /// <summary>
        /// Fired when the active tab changes
        /// </summary>
        event EventHandler<TabChangedEventArgs> ActiveTabChanged;
        
        /// <summary>
        /// Fired when a tab is modified (color, pin status, etc.)
        /// </summary>
        event EventHandler<TabModifiedEventArgs> TabModified;
        
        /// <summary>
        /// Fired when tabs are reordered
        /// </summary>
        event EventHandler<TabReorderedEventArgs> TabsReordered;
        
        #endregion

        #region Core Tab Operations
        
        /// <summary>
        /// Creates a new tab with the specified parameters
        /// </summary>
        Task<TabModel> CreateTabAsync(string title, string path = null, TabCreationOptions options = null);
        
        /// <summary>
        /// Closes the specified tab
        /// </summary>
        Task<bool> CloseTabAsync(TabModel tab, bool force = false);
        
        /// <summary>
        /// Closes tab by index
        /// </summary>
        Task<bool> CloseTabAsync(int index, bool force = false);
        
        /// <summary>
        /// Duplicates an existing tab
        /// </summary>
        Task<TabModel> DuplicateTabAsync(TabModel tab);
        
        /// <summary>
        /// Activates the specified tab
        /// </summary>
        Task ActivateTabAsync(TabModel tab);
        
        /// <summary>
        /// Activates tab by index
        /// </summary>
        Task ActivateTabAsync(int index);
        
        #endregion

        #region Tab Customization
        
        /// <summary>
        /// Changes the color of a tab
        /// </summary>
        Task SetTabColorAsync(TabModel tab, Color color);
        
        /// <summary>
        /// Clears the custom color from a tab
        /// </summary>
        Task ClearTabColorAsync(TabModel tab);
        
        /// <summary>
        /// Pins or unpins a tab
        /// </summary>
        Task SetTabPinnedAsync(TabModel tab, bool isPinned);
        
        /// <summary>
        /// Renames a tab
        /// </summary>
        Task RenameTabAsync(TabModel tab, string newTitle);
        
        #endregion

        #region Tab Organization
        
        /// <summary>
        /// Moves a tab to a new position
        /// </summary>
        Task MoveTabAsync(TabModel tab, int newIndex);
        
        /// <summary>
        /// Moves a tab by current index to new index
        /// </summary>
        Task MoveTabAsync(int fromIndex, int toIndex);
        
        /// <summary>
        /// Gets all pinned tabs
        /// </summary>
        IEnumerable<TabModel> GetPinnedTabs();
        
        /// <summary>
        /// Gets all unpinned tabs
        /// </summary>
        IEnumerable<TabModel> GetUnpinnedTabs();
        
        /// <summary>
        /// Reorganizes tabs (pinned first, then unpinned)
        /// </summary>
        Task ReorganizeTabsAsync();
        
        #endregion

        #region Navigation
        
        /// <summary>
        /// Navigates to the next tab
        /// </summary>
        Task NavigateToNextTabAsync();
        
        /// <summary>
        /// Navigates to the previous tab
        /// </summary>
        Task NavigateToPreviousTabAsync();
        
        /// <summary>
        /// Navigates to tab by index
        /// </summary>
        Task NavigateToTabAsync(int index);
        
        #endregion

        #region Validation
        
        /// <summary>
        /// Validates if a tab can be closed
        /// </summary>
        bool CanCloseTab(TabModel tab);
        
        /// <summary>
        /// Validates if tabs can be reordered
        /// </summary>
        bool CanReorderTabs();
        
        /// <summary>
        /// Gets the tab at the specified index
        /// </summary>
        TabModel GetTabAt(int index);
        
        /// <summary>
        /// Gets the index of the specified tab
        /// </summary>
        int GetTabIndex(TabModel tab);
        
        #endregion
    }

    #region Event Args Classes
    
    /// <summary>
    /// Event arguments for tab-related events
    /// </summary>
    public class TabEventArgs : EventArgs
    {
        public TabModel Tab { get; }
        public int Index { get; }
        
        public TabEventArgs(TabModel tab, int index = -1)
        {
            Tab = tab;
            Index = index;
        }
    }
    
    /// <summary>
    /// Event arguments for tab change events
    /// </summary>
    public class TabChangedEventArgs : EventArgs
    {
        public TabModel OldTab { get; }
        public TabModel NewTab { get; }
        public int OldIndex { get; }
        public int NewIndex { get; }
        
        public TabChangedEventArgs(TabModel oldTab, TabModel newTab, int oldIndex, int newIndex)
        {
            OldTab = oldTab;
            NewTab = newTab;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }
    }
    
    /// <summary>
    /// Event arguments for tab modification events
    /// </summary>
    public class TabModifiedEventArgs : EventArgs
    {
        public TabModel Tab { get; }
        public string PropertyName { get; }
        public object OldValue { get; }
        public object NewValue { get; }
        
        public TabModifiedEventArgs(TabModel tab, string propertyName, object oldValue, object newValue)
        {
            Tab = tab;
            PropertyName = propertyName;
            OldValue = oldValue;
            NewValue = newValue;
        }
    }
    
    /// <summary>
    /// Event arguments for tab reordering events
    /// </summary>
    public class TabReorderedEventArgs : EventArgs
    {
        public TabModel Tab { get; }
        public int OldIndex { get; }
        public int NewIndex { get; }
        
        public TabReorderedEventArgs(TabModel tab, int oldIndex, int newIndex)
        {
            Tab = tab;
            OldIndex = oldIndex;
            NewIndex = newIndex;
        }
    }
    
    #endregion

    #region Options Classes
    
    /// <summary>
    /// Options for creating new tabs
    /// </summary>
    public class TabCreationOptions
    {
        public bool IsPinned { get; set; } = false;
        public Color? CustomColor { get; set; }
        public bool MakeActive { get; set; } = true;
        public object Content { get; set; }
        public int? InsertAtIndex { get; set; }
    }
    
    #endregion
} 