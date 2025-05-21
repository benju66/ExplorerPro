// UI/Toolbar/Toolbar.xaml.cs

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using ExplorerPro.Models;
using ExplorerPro.UI.FileTree;
using ExplorerPro.Themes;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.UI.Toolbar
{
    /// <summary>
    /// Interaction logic for Toolbar.xaml
    /// </summary>
    public partial class Toolbar : UserControl
    {
        #region Fields
        
        private Window? _parentWindow;
        private string _currentPath = string.Empty;
        private readonly UndoManager _undoManager;
        private readonly SearchEngine _searchEngine;
        private readonly ILogger<Toolbar>? _logger;
        
        // Changed from readonly to normal fields
        private Brush _placeholderTextBrush;
        private Brush _normalTextBrush;
        private bool _isPlaceholderVisible = true;
        
        #endregion
        
        #region Constructors
        
        /// <summary>
        /// Initializes a new instance of the Toolbar class.
        /// </summary>
        public Toolbar()
        {
            InitializeComponent();
            
            // Get singleton instance of UndoManager
            _undoManager = UndoManager.Instance;
            
            // Initialize the search engine with the fuzzy matcher
            _searchEngine = new SearchEngine(new FuzzySharpMatcher());
            
            // Initialize text brushes - now in constructor since they're not readonly anymore
            _placeholderTextBrush = new SolidColorBrush(Colors.Gray);
            _normalTextBrush = new SolidColorBrush(Colors.Black);
            
            // Set initial placeholder text
            SetPlaceholderText("Search files or enter a path...");
            
            // Hook up to parent window events after loading
            this.Loaded += Toolbar_Loaded;
        }
        
        /// <summary>
        /// Initializes a new instance of the Toolbar class with logging.
        /// </summary>
        /// <param name="logger">Logger for the toolbar</param>
        public Toolbar(ILogger<Toolbar> logger) : this()
        {
            _logger = logger;
        }
        
        #endregion
        
        #region Event Handlers
        
        private void Toolbar_Loaded(object sender, RoutedEventArgs e)
        {
            // Get reference to parent window
            _parentWindow = Window.GetWindow(this);
            _logger?.LogInformation("Toolbar loaded and parent window reference obtained");
        }
        
        private void UpButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow == null)
            {
                _logger?.LogWarning("Up button clicked but parent window reference is null");
                return;
            }
            
            try
            {
                if (_parentWindow is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
                {
                    mainWindow.GoUp();
                    _logger?.LogInformation("Go up command executed");
                }
                else
                {
                    _logger?.LogWarning("Parent window is not MainWindow, cannot execute go up command");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing go up command");
            }
        }
        
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow == null)
            {
                _logger?.LogWarning("Refresh button clicked but parent window reference is null");
                return;
            }
            
            try
            {
                if (_parentWindow is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
                {
                    mainWindow.RefreshFileTree();
                    _logger?.LogInformation("Refresh file tree command executed");
                }
                else
                {
                    _logger?.LogWarning("Parent window is not MainWindow, cannot execute refresh command");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing refresh command");
            }
        }
        
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _undoManager.Undo();
                _logger?.LogInformation("Undo command executed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing undo command");
            }
        }
        
        private void RedoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _undoManager.Redo();
                _logger?.LogInformation("Redo command executed");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing redo command");
            }
        }
        
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_parentWindow == null)
            {
                _logger?.LogWarning("Settings button clicked but parent window reference is null");
                return;
            }
            
            try
            {
                if (_parentWindow is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
                {
                    // Using OpenSettings instead of OpenSettingsDialog
                    mainWindow.OpenSettings();
                    _logger?.LogInformation("Open settings command executed");
                }
                else
                {
                    _logger?.LogWarning("Parent window is not MainWindow, cannot execute open settings command");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error executing open settings command");
            }
        }
        
        private void SearchBar_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                HandleSearchOrNavigation();
                e.Handled = true;
            }
        }
        
        private void SearchBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left && _isPlaceholderVisible)
            {
                // Clear placeholder and set focus
                ClearPlaceholder();
                searchBar.Focus();
            }
        }
        
        private void SearchBar_GotFocus(object sender, RoutedEventArgs e)
        {
            if (_isPlaceholderVisible)
            {
                ClearPlaceholder();
            }
        }
        
        private void SearchBar_LostFocus(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(searchBar.Text))
            {
                if (!string.IsNullOrEmpty(_currentPath))
                {
                    SetPlaceholderText(_currentPath);
                }
                else
                {
                    SetPlaceholderText("Search files or enter a path...");
                }
            }
        }
        
        private void CutMenuItem_Click(object sender, RoutedEventArgs e)
        {
            HandleCut();
        }
        
        private void CopyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            HandleCopy();
        }
        
        private void PasteMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_isPlaceholderVisible)
            {
                ClearPlaceholder();
            }
            searchBar.Paste();
        }
        
        private void CopyPathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentPath))
            {
                CopyToClipboard(_currentPath);
                _logger?.LogInformation("Current path copied to clipboard");
            }
        }
        
        private void EditPathMenuItem_Click(object sender, RoutedEventArgs e)
        {
            EditCurrentPath();
        }
        
        #endregion
        
        #region Search and Navigation
        
        /// <summary>
        /// Handles search bar input, determining whether it's a path navigation or a search query.
        /// </summary>
        private void HandleSearchOrNavigation()
        {
            // Skip if placeholder is visible or text is empty
            if (_isPlaceholderVisible || string.IsNullOrEmpty(searchBar.Text))
                return;
                
            string text = searchBar.Text.Trim();
            if (string.IsNullOrEmpty(text))
                return;
                
            try
            {
                string normalizedPath = Path.GetFullPath(text);
                _logger?.LogDebug($"Search/Address bar triggered: {normalizedPath}");
                
                if (Directory.Exists(normalizedPath) || File.Exists(normalizedPath))
                {
                    // Valid path (file/folder), so navigate
                    if (Directory.Exists(normalizedPath))
                    {
                        _logger?.LogDebug($"Navigating to directory: {normalizedPath}");
                        
                        if (_parentWindow is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
                        {
                            // Added null check on path
                            if (!string.IsNullOrEmpty(normalizedPath))
                            {
                                mainWindow.OpenDirectoryInTab(normalizedPath);
                            }
                        }
                        
                        // Added null check on path
                        if (!string.IsNullOrEmpty(normalizedPath))
                        {
                            UpdateSearchBar(normalizedPath);
                        }
                    }
                    else if (File.Exists(normalizedPath))
                    {
                        // If it's a file, navigate to its parent directory
                        string? parentDir = Path.GetDirectoryName(normalizedPath);
                        _logger?.LogDebug($"Navigating to parent directory: {parentDir}");
                        
                        if (_parentWindow is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
                        {
                            // Added null check on parentDir
                            if (!string.IsNullOrEmpty(parentDir))
                            {
                                mainWindow.OpenDirectoryInTab(parentDir);
                                
                                // Select the file in the file tree
                                ImprovedFileTreeListView? activeFileTree = mainWindow.GetActiveFileTree();
                                if (activeFileTree != null)
                                {
                                    activeFileTree.NavigateAndHighlight(normalizedPath);
                                }
                            }
                        }
                        
                        // Added null check on parentDir
                        if (!string.IsNullOrEmpty(parentDir))
                        {
                            UpdateSearchBar(parentDir);
                        }
                    }
                }
                else
                {
                    // Treat as fuzzy search
                    _logger?.LogDebug($"Performing fuzzy search for: {text}");
                    
                    PerformFuzzySearch(text);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error handling search/navigation");
                MessageBox.Show($"Error handling search: {ex.Message}", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        /// <summary>
        /// Performs a fuzzy search using the SearchEngine.
        /// </summary>
        /// <param name="query">The search query to use</param>
        private async void PerformFuzzySearch(string query)
        {
            if (_parentWindow is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
            {
                try
                {
                    // Get the active file tree
                    ImprovedFileTreeListView? activeFileTree = mainWindow.GetActiveFileTree();
                    if (activeFileTree == null)
                    {
                        _logger?.LogWarning("No active FileTree found for fuzzy searching");
                        return;
                    }
                    
                    // Get the directory to search in
                    string? searchDirectory = GetSearchDirectory(activeFileTree);
                    if (string.IsNullOrEmpty(searchDirectory))
                    {
                        _logger?.LogWarning("Could not determine search directory");
                        return;
                    }
                    
                    _logger?.LogDebug($"Searching in directory: {searchDirectory}");
                    
                    // Show search indicator (if implemented)
                    SetSearchInProgress(true);
                    
                    try
                    {
                        // Use the search engine to perform the search asynchronously
                        int threshold = 60; // Adjust fuzzy matching threshold
                        var results = await _searchEngine.FuzzySearchByNameAsync(
                            directory: searchDirectory,
                            query: query,
                            threshold: threshold,
                            includeFolders: true
                        );
                        
                        if (results.Count > 0)
                        {
                            string? firstMatch = results[0];
                            if (!string.IsNullOrEmpty(firstMatch))
                            {
                                _logger?.LogDebug($"Fuzzy match: {firstMatch}");
                                
                                // Navigate to the match
                                activeFileTree.NavigateAndHighlight(firstMatch);
                            }
                        }
                        else
                        {
                            _logger?.LogInformation("No fuzzy matches found");
                            MessageBox.Show("No matches found for your search.", "Search Results", 
                                MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                    finally
                    {
                        // Hide search indicator
                        SetSearchInProgress(false);
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error performing fuzzy search");
                    MessageBox.Show($"Error performing search: {ex.Message}", "Error", 
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
        
        /// <summary>
        /// Gets the directory to search in based on the active file tree's selection.
        /// </summary>
        /// <param name="fileTree">The active file tree</param>
        /// <returns>The directory to search in</returns>
        private string? GetSearchDirectory(ImprovedFileTreeListView fileTree)
        {
            if (fileTree == null)
                return null;
                
            // Instead of using GetActiveDirectoryPath which doesn't exist, use GetActiveFileTree
            if (_parentWindow is ExplorerPro.UI.MainWindow.MainWindow mainWindow)
            {
                var activeFileTree = mainWindow.GetActiveFileTree();
                if (activeFileTree != null)
                {
                    string? currentPath = activeFileTree.GetCurrentPath();
                    if (!string.IsNullOrEmpty(currentPath))
                    {
                        return currentPath;
                    }
                }
            }
            
            // If current path is set, use that if it's a directory
            if (!string.IsNullOrEmpty(_currentPath) && Directory.Exists(_currentPath))
            {
                return _currentPath;
            }
            
            // Fallback to user's documents folder
            return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
        
        /// <summary>
        /// Sets or clears a visual indicator that a search is in progress.
        /// </summary>
        /// <param name="inProgress">True if search is in progress, false otherwise</param>
        private void SetSearchInProgress(bool inProgress)
        {
            // This is a placeholder for a visual indicator
            // You could change the search bar background, show a spinner, etc.
            if (inProgress)
            {
                searchBar.IsEnabled = false;
            }
            else
            {
                searchBar.IsEnabled = true;
            }
        }
        
        #endregion
        
        #region Public Methods
        
        /// <summary>
        /// Updates the search bar with a path, showing it as a placeholder.
        /// </summary>
        /// <param name="path">The path to display</param>
        public void UpdateSearchBar(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                _logger?.LogWarning("Attempted to update search bar with empty path");
                return;
            }
            
            _currentPath = path;
            
            if (!searchBar.IsFocused)
            {
                SetPlaceholderText(path);
            }
            else
            {
                // If the search bar has focus, update the text directly
                searchBar.Text = path;
                _isPlaceholderVisible = false;
                searchBar.Foreground = _normalTextBrush;
            }
            
            _logger?.LogDebug($"Search bar updated with path: {path}");
        }
        
        #endregion
        
        #region Helper Methods
        
        /// <summary>
        /// Sets the placeholder text for the search bar.
        /// </summary>
        /// <param name="placeholderText">The placeholder text to set</param>
        private void SetPlaceholderText(string placeholderText)
        {
            searchBar.Text = placeholderText;
            searchBar.Foreground = _placeholderTextBrush;
            _isPlaceholderVisible = true;
        }
        
        /// <summary>
        /// Clears the placeholder text from the search bar.
        /// </summary>
        private void ClearPlaceholder()
        {
            if (_isPlaceholderVisible)
            {
                searchBar.Clear();
                searchBar.Foreground = _normalTextBrush;
                _isPlaceholderVisible = false;
            }
        }
        
        /// <summary>
        /// Handles the Cut action for the search bar.
        /// </summary>
        private void HandleCut()
        {
            if (_isPlaceholderVisible)
            {
                if (!string.IsNullOrEmpty(_currentPath))
                {
                    CopyToClipboard(_currentPath);
                    _logger?.LogInformation("Current path copied to clipboard (via Cut)");
                }
            }
            else
            {
                searchBar.Cut();
            }
        }
        
        /// <summary>
        /// Handles the Copy action for the search bar.
        /// </summary>
        private void HandleCopy()
        {
            if (_isPlaceholderVisible)
            {
                if (!string.IsNullOrEmpty(_currentPath))
                {
                    CopyToClipboard(_currentPath);
                    _logger?.LogInformation("Current path copied to clipboard (via Copy)");
                }
            }
            else
            {
                searchBar.Copy();
            }
        }
        
        /// <summary>
        /// Copies text to the system clipboard.
        /// </summary>
        /// <param name="text">The text to copy</param>
        private void CopyToClipboard(string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error copying text to clipboard");
            }
        }
        
        /// <summary>
        /// Switches from placeholder to actual text for editing.
        /// </summary>
        private void EditCurrentPath()
        {
            if (!string.IsNullOrEmpty(_currentPath))
            {
                ClearPlaceholder();
                searchBar.Text = _currentPath;
                searchBar.Focus();
                searchBar.SelectionStart = _currentPath.Length;
                _logger?.LogInformation("Current path set for editing");
            }
        }
        
        #endregion
        
        #region MainWindow Interface Methods
        
        /// <summary>
        /// Sets the address text in the search bar.
        /// </summary>
        /// <param name="path">The path to display</param>
        public void SetAddressText(string path)
        {
            if (!string.IsNullOrEmpty(path))
            {
                UpdateSearchBar(path);
            }
        }
        
        /// <summary>
        /// Sets focus to the address bar.
        /// </summary>
        public void SetAddressBarFocus()
        {
            ClearPlaceholder();
            searchBar.Focus();
        }
        
        /// <summary>
        /// Sets focus to the search box.
        /// </summary>
        public void SetSearchFocus()
        {
            ClearPlaceholder();
            searchBar.Focus();
        }
        
        #endregion

        #region Theme Management
        
        /// <summary>
        /// Refreshes UI elements based on the current theme
        /// </summary>
        public void RefreshThemeElements()
        {
            try
            {
                bool isDarkMode = ThemeManager.Instance.IsDarkMode;
                
                // Update toolbar background and border
                Background = GetResource<SolidColorBrush>("ToolbarBackground");
                BorderBrush = GetResource<SolidColorBrush>("ToolbarBorderBrush");
                
                // Update search bar
                if (searchBar != null)
                {
                    searchBar.Background = GetResource<SolidColorBrush>("TextBoxBackground");
                    
                    // Handle placeholder text separately
                    if (_isPlaceholderVisible)
                    {
                        // Update placeholder brush with theme-appropriate color
                        _placeholderTextBrush = new SolidColorBrush(isDarkMode ? 
                            Colors.DarkGray : Colors.Gray);
                        searchBar.Foreground = _placeholderTextBrush;
                    }
                    else
                    {
                        // Update normal text brush
                        _normalTextBrush = GetResource<SolidColorBrush>("TextBoxForeground");
                        searchBar.Foreground = _normalTextBrush;
                    }
                    
                    // Update border
                    searchBar.BorderBrush = GetResource<SolidColorBrush>("TextBoxBorder");
                }
                
                // Update all buttons
                RefreshToolbarButtons();
                
                // If the toolbar has a context menu, update it
                RefreshContextMenu();
                
                _logger?.LogInformation("Toolbar theme elements refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing toolbar theme elements");
                // Non-critical error, continue
            }
        }
        
        /// <summary>
        /// Refreshes all buttons in the toolbar
        /// </summary>
        private void RefreshToolbarButtons()
        {
            try
            {
                // Update all buttons by finding them in the visual tree
                // instead of hard-coding specific button names
                foreach (var button in FindVisualChildren<Button>(this))
                {
                    RefreshButton(button);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing toolbar buttons");
            }
        }
        
        /// <summary>
        /// Refreshes a single button with theme-appropriate styling
        /// </summary>
        private void RefreshButton(Button button)
        {
            if (button == null) return;
            
            try
            {
                // Apply theme resources to button
                button.Background = GetResource<SolidColorBrush>("ButtonBackground");
                button.Foreground = GetResource<SolidColorBrush>("ButtonForeground");
                button.BorderBrush = GetResource<SolidColorBrush>("ButtonBorder");
                
                // If the button contains an image, ensure it's visible against the background
                var image = FindVisualChild<Image>(button);
                if (image != null)
                {
                    // Special handling for image buttons
                    button.Background = Brushes.Transparent;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing button");
            }
        }
        
        /// <summary>
        /// Refreshes the context menu if it exists
        /// </summary>
        private void RefreshContextMenu()
        {
            try
            {
                if (searchBar?.ContextMenu != null)
                {
                    var menu = searchBar.ContextMenu;
                    
                    // Apply theme resources to context menu
                    menu.Background = GetResource<SolidColorBrush>("ContextMenuBackground");
                    menu.Foreground = GetResource<SolidColorBrush>("TextColor");
                    menu.BorderBrush = GetResource<SolidColorBrush>("ContextMenuBorder");
                    
                    // Update all menu items
                    foreach (var item in menu.Items)
                    {
                        if (item is MenuItem menuItem)
                        {
                            menuItem.Background = GetResource<SolidColorBrush>("MenuItemBackground");
                            menuItem.Foreground = GetResource<SolidColorBrush>("TextColor");
                            menuItem.BorderBrush = GetResource<SolidColorBrush>("MenuItemBorder");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error refreshing context menu");
            }
        }
        
        /// <summary>
        /// Helper method to get a resource from the current theme
        /// </summary>
        private T GetResource<T>(string resourceKey) where T : class
        {
            try
            {
                if (Application.Current.Resources[resourceKey] is T resource)
                {
                    return resource;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error getting resource '{resourceKey}'");
            }
            
            // Default values for common types
            if (typeof(T) == typeof(SolidColorBrush))
            {
                if (resourceKey.Contains("Background"))
                    return new SolidColorBrush(Colors.White) as T;
                if (resourceKey.Contains("Foreground") || resourceKey.Contains("Text"))
                    return new SolidColorBrush(Colors.Black) as T;
                if (resourceKey.Contains("Border"))
                    return new SolidColorBrush(Colors.Gray) as T;
            }
            
            return default;
        }
        
        /// <summary>
        /// Finds the first visual child of the specified type
        /// </summary>
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                {
                    return t;
                }
                
                T childOfChild = FindVisualChild<T>(child);
                if (childOfChild != null)
                {
                    return childOfChild;
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Finds all visual children of the specified type
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) yield break;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);
                
                if (child is T t)
                {
                    yield return t;
                }
                
                foreach (var grandChild in FindVisualChildren<T>(child))
                {
                    yield return grandChild;
                }
            }
        }
        
        #endregion
    }
}