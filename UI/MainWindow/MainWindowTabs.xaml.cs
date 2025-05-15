using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Data;
using System.IO;
using System.Globalization;
using ExplorerPro.UI.FileTree;
using ExplorerPro.UI.Converters;

namespace ExplorerPro.UI.MainWindow
{
    /// <summary>
    /// Interaction logic for MainWindowTabs.xaml
    /// Tab widget that contains MainWindowContainer instances.
    /// Handles tab management, detachment, and panel toggling.
    /// </summary>
    public partial class MainWindowTabs : UserControl
    {
        #region Fields

        private readonly List<Window> _detachedWindows = new List<Window>();
        private Point _dragStartPoint;
        private bool _isDragging;
        private TabItem? _draggedItem;
        
        #endregion

        #region Events

        /// <summary>
        /// Event raised when a new tab is added
        /// </summary>
        public event EventHandler<MainWindowContainer>? NewTabAdded;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of MainWindowTabs
        /// </summary>
        public MainWindowTabs()
        {
            InitializeComponent();

            // Enable drag-drop
            TabControl.AllowDrop = true;
            TabControl.MouseMove += TabControl_MouseMove;
            TabControl.PreviewMouseLeftButtonDown += TabControl_PreviewMouseLeftButtonDown;
            TabControl.PreviewMouseLeftButtonUp += TabControl_PreviewMouseLeftButtonUp;
            TabControl.Drop += TabControl_Drop;
            TabControl.DragEnter += TabControl_DragEnter;
            TabControl.DragOver += TabControl_DragOver;

            // Removed automatic tab creation to prevent null reference exceptions
            // AddNewMainWindowTab();

            // Delay tab creation until control is fully loaded
            this.Loaded += (s, e) =>
            {
                if (TabControl.Items.Count == 0)
                {
                    AddNewMainWindowTab();
                }
            };

        }

        #endregion

        #region Tab Management

        /// <summary>
        /// Add a new tab with a MainWindowContainer
        /// </summary>
        /// <param name="rootPath">Optional root path for the tab</param>
        /// <returns>The created MainWindowContainer</returns>
        public MainWindowContainer? AddNewMainWindowTab(string? rootPath = null)
        {
            try
            {
                MainWindow? parentWindow = Window.GetWindow(this) as MainWindow;
                if (parentWindow == null)
                {
                    MessageBox.Show("Cannot create new tab — parent window is not available yet.",
                        "Tab Creation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return null;
                }

                var container = new MainWindowContainer(parentWindow);

                // Make sure we have a valid root path
                if (string.IsNullOrEmpty(rootPath) || !Directory.Exists(rootPath))
                {
                    string onedrivePath = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                        "OneDrive - Fendler Patterson Construction, Inc");

                    rootPath = Directory.Exists(onedrivePath) ? onedrivePath : 
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                }

                // Initialize container with file tree
                container.InitializeWithFileTree(rootPath);

                // Create TabItem
                string tabTitle = $"Window {TabControl.Items.Count + 1}";
                TabItem newTab = new TabItem
                {
                    Header = tabTitle,
                    Content = container
                };

                // Add to TabControl
                TabControl.Items.Add(newTab);
                TabControl.SelectedItem = newTab;

                // Raise event
                NewTabAdded?.Invoke(this, container);

                return container;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating new tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return null;
            }
        }

        /// <summary>
        /// Handler for add tab button click
        /// </summary>
        private void AddTabButton_Click(object sender, RoutedEventArgs e)
        {
            AddNewMainWindowTab();
        }

        /// <summary>
        /// Handler for close tab button click
        /// </summary>
        private void TabCloseButton_Click(object sender, RoutedEventArgs e)
        {
            Button? closeButton = sender as Button;
            if (closeButton != null)
            {
                // Find parent TabItem
                TabItem? tabItem = FindParent<TabItem>(closeButton);
                if (tabItem != null)
                {
                    int index = TabControl.Items.IndexOf(tabItem);
                    if (index >= 0)
                    {
                        CloseTab(index);
                    }
                }
            }
        }

        /// <summary>
        /// Close the specified tab
        /// </summary>
        /// <param name="index">Index of tab to close</param>
        public void CloseTab(int index)
        {
            if (TabControl.Items.Count <= 1)
            {
                // Don't close the last tab
                return;
            }

            if (index >= 0 && index < TabControl.Items.Count)
            {
                TabItem? tabItem = TabControl.Items[index] as TabItem;
                if (tabItem != null)
                {
                    // Get container to dispose it
                    MainWindowContainer? container = tabItem.Content as MainWindowContainer;
                    
                    // Remove from tab control
                    TabControl.Items.RemoveAt(index);
                    
                    // Clean up container
                    if (container != null && container is IDisposable disposable)
                    {
                        disposable.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Close the current tab
        /// </summary>
        public void CloseCurrentTab()
        {
            CloseTab(TabControl.SelectedIndex);
        }

        /// <summary>
        /// Handler for new tab menu item
        /// </summary>
        private void NewTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            AddNewMainWindowTab();
        }

        /// <summary>
        /// Handler for close tab menu item
        /// </summary>
        private void CloseTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            CloseCurrentTab();
        }

        /// <summary>
        /// Handler for duplicate tab menu item
        /// </summary>
        private void DuplicateTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DuplicateCurrentTab();
        }

        /// <summary>
        /// Duplicate the current tab
        /// </summary>
        public void DuplicateCurrentTab()
        {
            try
            {
                var currentContainer = GetCurrentContainer();
                if (currentContainer == null) return;
                
                var fileTree = currentContainer.FindFileTree();
                if (fileTree != null)
                {
                    string rootPath = fileTree.GetCurrentPath(); // Changed from CurrentPath property to method call
                    AddNewMainWindowTab(rootPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error duplicating tab: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Handler for tab selection changed
        /// </summary>
        private void TabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // Notify parent window of tab change
            MainWindow? mainWindow = Window.GetWindow(this) as MainWindow;
            mainWindow?.UpdateAddressBarOnTabChange();
        }

        /// <summary>
        /// Gets the current container
        /// </summary>
        /// <returns>Current MainWindowContainer or null</returns>
        public MainWindowContainer? GetCurrentContainer()
        {
            TabItem? selectedTab = TabControl.SelectedItem as TabItem;
            return selectedTab?.Content as MainWindowContainer;
        }

        #endregion

        #region Tab Dragging

        /// <summary>
        /// Handler for mouse button down to start drag operation
        /// </summary>
        private void TabControl_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Get the tab item being clicked
            _draggedItem = GetTabItemFromPoint(e.GetPosition(TabControl));
            
            if (_draggedItem != null)
            {
                _dragStartPoint = e.GetPosition(TabControl);
                _isDragging = true;
            }
        }

        /// <summary>
        /// Handler for mouse move to drag tabs
        /// </summary>
        private void TabControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDragging && _draggedItem != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentPosition = e.GetPosition(TabControl);
                Vector dragDelta = _dragStartPoint - currentPosition;
                
                // Only start drag once we've moved a threshold distance
                if (Math.Abs(dragDelta.X) > SystemParameters.MinimumHorizontalDragDistance ||
                    Math.Abs(dragDelta.Y) > SystemParameters.MinimumVerticalDragDistance)
                {
                    // Start drag-drop operation
                    DataObject dragData = new DataObject("TabItem", _draggedItem);
                    DragDrop.DoDragDrop(_draggedItem, dragData, DragDropEffects.Move);
                    
                    _isDragging = false;
                    _draggedItem = null;
                }
            }
        }

        /// <summary>
        /// Handler for mouse button up to end drag operation
        /// </summary>
        private void TabControl_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            _draggedItem = null;
        }

        /// <summary>
        /// Handler for drag enter
        /// </summary>
        private void TabControl_DragEnter(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TabItem") && !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handler for drag over
        /// </summary>
        private void TabControl_DragOver(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent("TabItem") && !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handler for drop
        /// </summary>
        private void TabControl_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("TabItem"))
            {
                // Handle tab reordering
                TabItem? draggedItem = e.Data.GetData("TabItem") as TabItem;
                TabItem? targetItem = GetTabItemFromPoint(e.GetPosition(TabControl));
                
                if (draggedItem != null && targetItem != null && draggedItem != targetItem)
                {
                    int sourceIndex = TabControl.Items.IndexOf(draggedItem);
                    int targetIndex = TabControl.Items.IndexOf(targetItem);
                    
                    MoveTab(sourceIndex, targetIndex);
                }
                
                e.Handled = true;
            }
            else if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                // Forward file drops to the current container
                string[]? files = (string[]?)e.Data.GetData(DataFormats.FileDrop);
                var container = GetCurrentContainer();
                
                if (container != null && files != null && files.Length > 0)
                {
                    var fileTree = container.FindFileTree();
                    if (fileTree != null)
                    {
                        fileTree.HandleFileDrop(e.Data);
                    }
                    else if (Directory.Exists(files[0]))
                    {
                        // If no file tree, try to open first directory in a new tab
                        AddNewMainWindowTab(files[0]);
                    }
                }
                
                e.Handled = true;
            }
        }

        /// <summary>
        /// Get the tab item at a specific point
        /// </summary>
        /// <param name="point">Point to check</param>
        /// <returns>TabItem at point or null</returns>
        private TabItem? GetTabItemFromPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(TabControl, point);
            if (result != null)
            {
                DependencyObject? current = result.VisualHit;
                
                // Search up the visual tree for a TabItem
                while (current != null && !(current is TabItem))
                {
                    current = VisualTreeHelper.GetParent(current);
                }
                
                return current as TabItem;
            }
            
            return null;
        }

        /// <summary>
        /// Move a tab from one position to another
        /// </summary>
        /// <param name="sourceIndex">Source index</param>
        /// <param name="targetIndex">Target index</param>
        private void MoveTab(int sourceIndex, int targetIndex)
        {
            if (sourceIndex < 0 || sourceIndex >= TabControl.Items.Count || 
                targetIndex < 0 || targetIndex >= TabControl.Items.Count)
            {
                return;
            }
            
            // Get the item to move
            TabItem? item = TabControl.Items[sourceIndex] as TabItem;
            if (item == null) return;
            
            // Remove from source
            TabControl.Items.RemoveAt(sourceIndex);
            
            // Insert at target
            TabControl.Items.Insert(targetIndex, item);
            
            // Select the moved item
            TabControl.SelectedItem = item;
        }

        #endregion

        #region Detached Tabs

        /// <summary>
        /// Handler for detach tab menu item
        /// </summary>
        private void DetachTabMenuItem_Click(object sender, RoutedEventArgs e)
        {
            DetachMainTab(TabControl.SelectedIndex);
        }

        /// <summary>
        /// Handler for move to new window menu item
        /// </summary>
        private void MoveToNewWindowMenuItem_Click(object sender, RoutedEventArgs e)
        {
            MoveTabToNewWindow();
        }

        /// <summary>
        /// Move the current tab to a new window
        /// </summary>
        public void MoveTabToNewWindow()
        {
            DetachMainTab(TabControl.SelectedIndex);
        }

        /// <summary>
        /// Detach a tab into a new window
        /// </summary>
        /// <param name="index">Index of tab to detach</param>
        public void DetachMainTab(int index)
        {
            if (index < 0 || index >= TabControl.Items.Count || TabControl.Items.Count <= 1)
            {
                // Don't detach invalid tab or last tab
                return;
            }

            try
            {
                // Get the tab item to detach
                TabItem? tabItem = TabControl.Items[index] as TabItem;
                if (tabItem == null) return;

                // Get the container
                MainWindowContainer? container = tabItem.Content as MainWindowContainer;
                if (container == null) return;

                string tabTitle = tabItem.Header?.ToString() ?? "Detached";

                // Create new window
                MainWindow newWindow = new MainWindow();
                _detachedWindows.Add(newWindow);

                // Remove tab from current window
                tabItem.Content = null; // Detach container from tab item
                TabControl.Items.RemoveAt(index);

                // Clear tabs in new window
                while (newWindow.MainTabs.Items.Count > 0)
                {
                    newWindow.MainTabs.Items.RemoveAt(0);
                }

                // Create new tab item for detached container
                TabItem newTabItem = new TabItem
                {
                    Header = tabTitle,
                    Content = container
                };

                // Add to new window
                newWindow.MainTabs.Items.Add(newTabItem);
                newWindow.MainTabs.SelectedItem = newTabItem;

                // Configure new window
                newWindow.Title = $"Detached - {tabTitle}";
                newWindow.Width = 1000;
                newWindow.Height = 700;

                // Position offset from parent
                MainWindow? parentWindow = Window.GetWindow(this) as MainWindow;
                if (parentWindow != null)
                {
                    newWindow.Left = parentWindow.Left + 50;
                    newWindow.Top = parentWindow.Top + 50;
                }

                // Show the new window
                newWindow.Show();
                newWindow.Activate();

                // Connect pinned panel signals
                if (container.PinnedPanel != null)
                {
                    newWindow.ConnectPinnedPanelSignals(container.PinnedPanel);
                }

                // Trim detached windows list (remove closed windows)
                _detachedWindows.RemoveAll(w => !w.IsVisible);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error detaching tab: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #endregion

        #region Split View

        /// <summary>
        /// Handler for toggle split view menu item
        /// </summary>
        private void ToggleSplitViewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ToggleSplitView();
        }

        /// <summary>
        /// Toggle split view in current container
        /// </summary>
        public void ToggleSplitView()
        {
            var container = GetCurrentContainer();
            container?.ToggleSplitView();
        }

        #endregion

        #region Panel Toggle

        /// <summary>
        /// Toggle a panel in the current container
        /// </summary>
        /// <param name="methodName">Name of toggle method</param>
        private void TogglePanelInCurrentContainer(string methodName)
        {
            MainWindow? mainWindow = Window.GetWindow(this) as MainWindow;
            if (mainWindow == null) return;

            var container = GetCurrentContainer();
            if (container == null) return;

            // Use reflection to call the method by name
            var method = container.GetType().GetMethod(methodName);
            if (method != null)
            {
                method.Invoke(container, null);
            }
            else
            {
                Console.WriteLine($"Error: No {methodName} method found in container");
            }
        }

        /// <summary>
        /// Toggle pinned panel in current container
        /// </summary>
        public void TogglePinnedPanel()
        {
            TogglePanelInCurrentContainer("TogglePinnedPanel");
        }

        /// <summary>
        /// Handler for toggle pinned panel button
        /// </summary>
        private void TogglePinnedPanel_Click(object sender, RoutedEventArgs e)
        {
            TogglePinnedPanel();
        }

        /// <summary>
        /// Toggle bookmarks panel in current container
        /// </summary>
        public void ToggleBookmarksPanel()
        {
            TogglePanelInCurrentContainer("ToggleBookmarksPanel");
        }

        /// <summary>
        /// Handler for toggle bookmarks panel button
        /// </summary>
        private void ToggleBookmarksPanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleBookmarksPanel();
        }

        /// <summary>
        /// Toggle Procore panel in current container
        /// </summary>
        public void ToggleProcorePanel()
        {
            TogglePanelInCurrentContainer("ToggleProcorePanel");
        }

        /// <summary>
        /// Handler for toggle Procore panel button
        /// </summary>
        private void ToggleProcorePanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleProcorePanel();
        }

        /// <summary>
        /// Toggle to-do panel in current container
        /// </summary>
        public void ToggleTodoPanel()
        {
            TogglePanelInCurrentContainer("ToggleTodoPanel");
        }

        /// <summary>
        /// Handler for toggle to-do panel button
        /// </summary>
        private void ToggleTodoPanel_Click(object sender, RoutedEventArgs e)
        {
            ToggleTodoPanel();
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Find parent of specified type
        /// </summary>
        /// <typeparam name="T">Type of parent to find</typeparam>
        /// <param name="child">Child element</param>
        /// <returns>Parent of specified type or null</returns>
        private T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null) return null;

            DependencyObject? parent = VisualTreeHelper.GetParent(child);
            
            while (parent != null && !(parent is T))
            {
                parent = VisualTreeHelper.GetParent(parent);
            }
            
            return parent as T;
        }

        #endregion
    }
}