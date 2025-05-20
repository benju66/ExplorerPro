// UI/FileTree/ImprovedFileTreeListView.xaml.cs (UPDATED with progress UI and enhanced Outlook support)
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.Utilities;
using ExplorerPro.UI.FileTree.Services;

namespace ExplorerPro.UI.FileTree
{
    /// <summary>
    /// Interaction logic for ImprovedFileTreeListView.xaml with enhanced Outlook attachment support
    /// </summary>
    public partial class ImprovedFileTreeListView : UserControl, IFileTree, IDisposable, INotifyPropertyChanged
    {
        #region Services and Dependencies

        private IFileTreeService _fileTreeService;
        private IFileTreeCache _fileTreeCache;
        private IFileTreeDragDropService _dragDropService;
        private MetadataManager _metadataManager;
        private CustomFileSystemModel _fileSystemModel;
        private UndoManager _undoManager;
        private SettingsManager _settingsManager;
        private readonly IFileOperations _fileOperations;

        #endregion

        #region Property Changed Implementation
        
        public event PropertyChangedEventHandler PropertyChanged;
        
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        
        #endregion
        
        #region Column Width Properties
        
        private double _nameColumnWidth = 250;
        public double NameColumnWidth
        {
            get { return _nameColumnWidth; }
            set
            {
                if (_nameColumnWidth != value)
                {
                    _nameColumnWidth = value;
                    OnPropertyChanged(nameof(NameColumnWidth));
                }
            }
        }
        
        private double _sizeColumnWidth = 100;
        public double SizeColumnWidth
        {
            get { return _sizeColumnWidth; }
            set
            {
                if (_sizeColumnWidth != value)
                {
                    _sizeColumnWidth = value;
                    OnPropertyChanged(nameof(SizeColumnWidth));
                }
            }
        }
        
        private double _typeColumnWidth = 120;
        public double TypeColumnWidth
        {
            get { return _typeColumnWidth; }
            set
            {
                if (_typeColumnWidth != value)
                {
                    _typeColumnWidth = value;
                    OnPropertyChanged(nameof(TypeColumnWidth));
                }
            }
        }
        
        private double _dateColumnWidth = 150;
        public double DateColumnWidth
        {
            get { return _dateColumnWidth; }
            set
            {
                if (_dateColumnWidth != value)
                {
                    _dateColumnWidth = value;
                    OnPropertyChanged(nameof(DateColumnWidth));
                }
            }
        }
        
        #endregion

        #region Events
        
        public event EventHandler<string> LocationChanged = delegate { };
        public event EventHandler<Tuple<string, string>> ContextMenuActionTriggered = delegate { };
        public event EventHandler FileTreeClicked = delegate { };
        
        #endregion

        #region Properties
        
        /// <summary>
        /// Gets the current path being displayed
        /// </summary>
        public string CurrentPath 
        { 
            get { return _currentFolderPath; }
            private set { _currentFolderPath = value; }
        }

        /// <summary>
        /// Gets whether any items are selected
        /// </summary>
        public bool HasSelectedItems => fileTreeView.SelectedItem != null;
        
        /// <summary>
        /// Gets or sets whether to show hidden files
        /// </summary>
        public bool ShowHiddenFiles
        {
            get { return _showHiddenFiles; }
            set
            {
                if (_showHiddenFiles != value)
                {
                    _showHiddenFiles = value;
                    RefreshView();
                }
            }
        }
        
        #endregion

        #region Fields
        
        private string _currentFolderPath = string.Empty;
        private bool _showHiddenFiles = false;
        private ObservableCollection<FileTreeItem> _rootItems = new ObservableCollection<FileTreeItem>();
        private Point? _dragStartPosition;
        private List<GridViewColumnHeader> _columnHeaders = new List<GridViewColumnHeader>();
        private ContextMenu _treeContextMenu;
        private GridView _columnHeaderGridView;
        
        private GridViewColumn NameColumn { get; set; }
        private GridViewColumn SizeColumn { get; set; }
        private GridViewColumn TypeColumn { get; set; }
        private GridViewColumn DateColumn { get; set; }
        
        private GridViewColumnHeader _draggedColumn;
        private double _originalWidth;
        
        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the ImprovedFileTreeListView class
        /// </summary>
        public ImprovedFileTreeListView()
        {
            try
            {
                InitializeComponent();

                // Initialize dependencies first
                _fileOperations = new FileOperations.FileOperations();
                InitializeManagersAndModel();
                InitializeServices();

                // Get the GridView from resources
                _columnHeaderGridView = (GridView)FindResource("columnHeaderGridView");

                // Create context menu
                _treeContextMenu = new ContextMenu();
                fileTreeView.ContextMenu = _treeContextMenu;

                // Set the TreeView ItemsSource
                fileTreeView.ItemsSource = _rootItems;

                // Set up event handlers
                SetupEventHandlers();

                // Setup column headers
                SetupColumnHeaders();

                // Setup column click events
                this.Loaded += ImprovedFileTreeListView_Loaded;

                // Set DataContext to self for bindings
                DataContext = this;

                System.Diagnostics.Debug.WriteLine("[INIT] ImprovedFileTreeListView initialized with services");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error initializing ImprovedFileTreeListView: {ex.Message}",
                    "Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        #endregion

        #region Initialization
        
        private void InitializeManagersAndModel()
        {
            try
            {
                _metadataManager = App.MetadataManager ?? MetadataManager.Instance;
                _undoManager = App.UndoManager ?? UndoManager.Instance;
                _settingsManager = App.Settings ?? new SettingsManager();
                _fileSystemModel = new CustomFileSystemModel(_metadataManager, _undoManager, _fileOperations);
                
                System.Diagnostics.Debug.WriteLine("[DEBUG] File tree components initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to initialize file tree components: {ex.Message}");
                
                try 
                {
                    if (_metadataManager == null) _metadataManager = new MetadataManager();
                    if (_undoManager == null) _undoManager = new UndoManager();
                    if (_settingsManager == null) _settingsManager = new SettingsManager();
                    if (_fileSystemModel == null) _fileSystemModel = new CustomFileSystemModel(_metadataManager, _undoManager, _fileOperations);
                }
                catch (Exception fallbackEx) 
                {
                    System.Diagnostics.Debug.WriteLine($"[CRITICAL] Failed to create fallback components: {fallbackEx.Message}");
                    _metadataManager = new MetadataManager();
                    _undoManager = new UndoManager();
                    _settingsManager = new SettingsManager();
                    _fileSystemModel = new CustomFileSystemModel(_metadataManager, _undoManager, _fileOperations);
                }
            }
        }

        private void InitializeServices()
        {
            try
            {
                var iconProvider = new FileIconProvider(true);
                _fileTreeService = new FileTreeService(_metadataManager, iconProvider);
                _fileTreeCache = new FileTreeCacheService(1000);
                _dragDropService = new FileTreeDragDropService();

                _fileTreeService.ErrorOccurred += (s, error) => 
                {
                    Dispatcher.Invoke(() => MessageBox.Show(error, "File Tree Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                };

                _dragDropService.FilesDropped += async (s, e) => 
                {
                    await RefreshDirectoryAsync(e.TargetPath);
                    
                    if (e.IsInternalMove)
                    {
                        var sourceDirectories = e.SourceFiles
                            .Select(f => Path.GetDirectoryName(f))
                            .Where(d => !string.IsNullOrEmpty(d))
                            .Distinct()
                            .ToArray();
                            
                        foreach (var sourceDir in sourceDirectories)
                        {
                            await RefreshDirectoryAsync(sourceDir);
                        }
                    }
                };

                _dragDropService.FilesMoved += async (s, e) => 
                {
                    await RefreshDirectoryAsync(e.TargetPath);
                    
                    foreach (var sourceDir in e.SourceDirectories)
                    {
                        await RefreshDirectoryAsync(sourceDir);
                    }
                };

                _dragDropService.ErrorOccurred += (s, error) => 
                {
                    Dispatcher.Invoke(() => MessageBox.Show(error, "Drag/Drop Error", MessageBoxButton.OK, MessageBoxImage.Warning));
                };

                // Handle Outlook extraction completion
                _dragDropService.OutlookExtractionCompleted += (s, e) => 
                {
                    Dispatcher.Invoke(() => 
                    {
                        HideProgressOverlay();
                        ShowOutlookExtractionResults(e.Result);
                        
                        // Refresh the target directory
                        _ = RefreshDirectoryAsync(e.TargetPath);
                    });
                };

                _fileTreeCache.ItemEvicted += (s, e) => 
                {
                    System.Diagnostics.Debug.WriteLine($"[CACHE] Item evicted: {e.Key} (Reason: {e.Reason})");
                };

                System.Diagnostics.Debug.WriteLine("[DEBUG] File tree services initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to initialize services: {ex.Message}");
                throw;
            }
        }

        private void SetupEventHandlers()
        {
            fileTreeView.SelectedItemChanged += FileTreeView_SelectedItemChanged;
            fileTreeView.MouseDoubleClick += FileTreeView_MouseDoubleClick;
            fileTreeView.ContextMenuOpening += FileTreeView_ContextMenuOpening;
            fileTreeView.AddHandler(TreeViewItem.ExpandedEvent, new RoutedEventHandler(TreeViewItem_Expanded));
            fileTreeView.PreviewMouseLeftButtonDown += FileTreeView_PreviewMouseLeftButtonDown;
            fileTreeView.PreviewMouseLeftButtonUp += FileTreeView_PreviewMouseLeftButtonUp;
            fileTreeView.MouseMove += FileTreeView_MouseMove;

            fileTreeView.AllowDrop = true;
            fileTreeView.DragEnter += FileTreeView_DragEnter;
            fileTreeView.DragOver += FileTreeView_DragOver;
            fileTreeView.Drop += FileTreeView_Drop;
            fileTreeView.DragLeave += FileTreeView_DragLeave;
        }

        private void ImprovedFileTreeListView_Loaded(object sender, RoutedEventArgs e)
        {
            _showHiddenFiles = _settingsManager.GetSetting("file_view.show_hidden", false);
            MakeColumnsResizable();
            TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
            fileTreeView.ItemContainerGenerator.StatusChanged += ItemContainerGenerator_StatusChanged;
        }
        
        private void ItemContainerGenerator_StatusChanged(object sender, EventArgs e)
        {
            if (fileTreeView.ItemContainerGenerator.Status == GeneratorStatus.ContainersGenerated)
            {
                TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
            }
        }
        
        #endregion

        #region Progress UI Management

        /// <summary>
        /// Shows the progress overlay for Outlook extraction
        /// </summary>
        private void ShowProgressOverlay()
        {
            ProgressOverlay.Visibility = Visibility.Visible;
            ExtractionProgress.IsIndeterminate = true;
            ProgressText.Text = "Analyzing Outlook data...";
            
            // Disable the main tree view during extraction
            fileTreeView.IsEnabled = false;
        }

        /// <summary>
        /// Hides the progress overlay
        /// </summary>
        private void HideProgressOverlay()
        {
            ProgressOverlay.Visibility = Visibility.Collapsed;
            fileTreeView.IsEnabled = true;
        }

        /// <summary>
        /// Updates the progress text
        /// </summary>
        /// <param name="message">Progress message</param>
        private void UpdateProgressText(string message)
        {
            ProgressText.Text = message;
        }

        /// <summary>
        /// Shows the results of Outlook extraction
        /// </summary>
        /// <param name="result">Extraction result</param>
        private void ShowOutlookExtractionResults(OutlookDataExtractor.ExtractionResult result)
        {
            string title;
            MessageBoxImage icon;
            string message;

            if (result.Success)
            {
                title = "Extraction Complete";
                icon = result.FilesSkipped > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information;
                message = $"Successfully extracted {result.FilesExtracted} attachment(s)";
                
                if (result.FilesSkipped > 0)
                {
                    message += $"\n{result.FilesSkipped} file(s) could not be extracted";
                }
                
                if (result.Errors.Count > 0)
                {
                    message += "\n\nIssues encountered:";
                    foreach (var error in result.Errors.Take(3))
                    {
                        message += $"\n• {error}";
                    }
                    
                    if (result.Errors.Count > 3)
                    {
                        message += $"\n... and {result.Errors.Count - 3} more";
                    }
                }
            }
            else
            {
                title = "Extraction Failed";
                icon = MessageBoxImage.Error;
                message = "Failed to extract any attachments";
                
                if (result.Errors.Count > 0)
                {
                    message += "\n\nErrors:";
                    foreach (var error in result.Errors.Take(3))
                    {
                        message += $"\n• {error}";
                    }
                    
                    if (result.Errors.Count > 3)
                    {
                        message += $"\n... and {result.Errors.Count - 3} more";
                    }
                }
            }

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);
        }

        /// <summary>
        /// Handler for cancel button click
        /// </summary>
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            _dragDropService?.CancelOutlookExtraction();
            HideProgressOverlay();
        }

        #endregion

        #region Column Management

        private void SetupColumnHeaders()
        {
            try
            {
                NameColumn = new GridViewColumn { 
                    Header = "Name", 
                    Width = NameColumnWidth 
                };
                
                SizeColumn = new GridViewColumn { 
                    Header = "Size", 
                    Width = SizeColumnWidth 
                };
                
                TypeColumn = new GridViewColumn { 
                    Header = "Type", 
                    Width = TypeColumnWidth 
                };
                
                DateColumn = new GridViewColumn { 
                    Header = "Date Modified", 
                    Width = DateColumnWidth 
                };
                
                BindingOperations.SetBinding(NameColumn, GridViewColumn.WidthProperty, 
                    new Binding(nameof(NameColumnWidth)) { Source = this, Mode = BindingMode.TwoWay });
                
                BindingOperations.SetBinding(SizeColumn, GridViewColumn.WidthProperty, 
                    new Binding(nameof(SizeColumnWidth)) { Source = this, Mode = BindingMode.TwoWay });
                
                BindingOperations.SetBinding(TypeColumn, GridViewColumn.WidthProperty, 
                    new Binding(nameof(TypeColumnWidth)) { Source = this, Mode = BindingMode.TwoWay });
                
                BindingOperations.SetBinding(DateColumn, GridViewColumn.WidthProperty, 
                    new Binding(nameof(DateColumnWidth)) { Source = this, Mode = BindingMode.TwoWay });
                
                if (_columnHeaderGridView != null)
                {
                    _columnHeaderGridView.Columns.Clear();
                    _columnHeaderGridView.Columns.Add(NameColumn);
                    _columnHeaderGridView.Columns.Add(SizeColumn);
                    _columnHeaderGridView.Columns.Add(TypeColumn);
                    _columnHeaderGridView.Columns.Add(DateColumn);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to setup column headers: {ex.Message}");
            }
        }
        
        private void MakeColumnsResizable()
        {
            try
            {
                _columnHeaders.Clear();
                
                var headerPresenter = FindVisualChild<GridViewHeaderRowPresenter>(this);
                if (headerPresenter == null)
                {
                    System.Diagnostics.Debug.WriteLine("[WARNING] GridViewHeaderRowPresenter not found");
                    return;
                }
                
                foreach (var header in FindVisualChildren<GridViewColumnHeader>(headerPresenter))
                {
                    _columnHeaders.Add(header);
                    
                    if (FindVisualChild<Thumb>(header) is Thumb thumb)
                    {
                        thumb.DragStarted += ColumnHeader_DragStarted;
                        thumb.DragDelta += ColumnHeader_DragDelta;
                        thumb.DragCompleted += ColumnHeader_DragCompleted;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to make columns resizable: {ex.Message}");
            }
        }
        
        private void ColumnHeader_DragStarted(object sender, DragStartedEventArgs e)
        {
            if (sender is Thumb thumb)
            {
                _draggedColumn = FindAncestor<GridViewColumnHeader>(thumb);
                if (_draggedColumn != null)
                {
                    _originalWidth = _draggedColumn.ActualWidth;
                }
            }
        }
        
        private void ColumnHeader_DragDelta(object sender, DragDeltaEventArgs e)
        {
            if (_draggedColumn != null)
            {
                double newWidth = Math.Max(20, _originalWidth + e.HorizontalChange);
                int index = _columnHeaders.IndexOf(_draggedColumn);
                
                switch (index)
                {
                    case 0: NameColumnWidth = newWidth; break;
                    case 1: SizeColumnWidth = newWidth; break;
                    case 2: TypeColumnWidth = newWidth; break;
                    case 3: DateColumnWidth = newWidth; break;
                }
            }
        }
        
        private void ColumnHeader_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _draggedColumn = null;
        }
        
        #endregion

        #region IFileTree Implementation
        
        public string GetCurrentPath()
        {
            return _currentFolderPath;
        }
        
        public string? GetSelectedPath()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                return selectedItem.Path;
            }
            return null;
        }

        public string GetSelectedFolderPath()
        {
            var selected = GetSelectedPath();
            if (string.IsNullOrEmpty(selected))
                return _currentFolderPath;
                
            if (Directory.Exists(selected))
                return selected;
                
            return Path.GetDirectoryName(selected) ?? _currentFolderPath;
        }

        public void RefreshView()
        {
            RefreshTreeView();
        }

        public void SelectItem(string path)
        {
            SelectItemByPath(path);
        }

        public void CopySelected()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                CopyItem(selectedItem.Path);
            }
        }

        public void CutSelected()
        {
            CopySelected();
            DeleteSelected();
        }

        public void Paste()
        {
            string targetPath = _currentFolderPath;
            PasteItem(targetPath);
        }

        public void DeleteSelected()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                DeleteItemWithUndo(selectedItem.Path);
            }
        }

        public void CreateFolder()
        {
            CreateNewFolder(_currentFolderPath);
        }

        public void CreateFile()
        {
            CreateNewFile(_currentFolderPath);
        }

        public void ToggleShowHidden()
        {
            ShowHiddenFiles = !ShowHiddenFiles;
            _settingsManager.UpdateSetting("file_view.show_hidden", ShowHiddenFiles);
        }

        public void ClearSelection()
        {
            var selectedTreeViewItem = GetSelectedTreeViewItem();
            selectedTreeViewItem?.SetValue(TreeViewItem.IsSelectedProperty, false);
        }

        public void HandleFileDrop(object data)
        {
            if (data is DataObject dataObject && dataObject.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])dataObject.GetData(DataFormats.FileDrop);
                _dragDropService.HandleExternalFileDrop(files, _currentFolderPath);
            }
        }
        
        public async void SetRootDirectory(string directory)
        {
            try 
            {
                if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                {
                    System.Diagnostics.Debug.WriteLine($"[ERROR] Invalid directory path: {directory}");
                    return;
                }

                System.Diagnostics.Debug.WriteLine($"[DEBUG] Setting root directory to: {directory}");
                
                _rootItems.Clear();
                _fileTreeCache.Clear();

                var rootItem = _fileTreeService.CreateFileTreeItem(directory, 0);
                if (rootItem != null)
                {
                    _rootItems.Add(rootItem);
                    _fileTreeCache.SetItem(directory, rootItem);
                    await LoadDirectoryContentsAsync(rootItem);
                    rootItem.IsExpanded = true;
                    _currentFolderPath = directory;
                    LocationChanged?.Invoke(this, directory);
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Root directory set to: {directory}, Children count: {rootItem.Children.Count}");
                    
                    Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                        TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                    }));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to set root directory: {ex.Message}");
                MessageBox.Show($"Error setting root directory: {ex.Message}", 
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        
        public void NavigateAndHighlight(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            try
            {
                if (File.Exists(path))
                {
                    string dirPath = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(dirPath))
                    {
                        SetRootDirectory(dirPath);
                    }
                }
                else if (Directory.Exists(path))
                {
                    SetRootDirectory(path);
                }
                
                SelectItemByPath(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to navigate and highlight: {ex.Message}");
            }
        }
        
        #endregion

        #region File Loading

        private async Task LoadDirectoryContentsAsync(FileTreeItem parentItem)
        {
            if (parentItem == null || !parentItem.IsDirectory)
            {
                return;
            }
                
            string path = parentItem.Path;
            int childLevel = parentItem.Level + 1;

            try
            {
                if (parentItem.Children.Count > 0 && !parentItem.HasDummyChild())
                {
                    return;
                }
                
                parentItem.ClearChildren();
                parentItem.Children.Add(new FileTreeItem { Name = "Loading...", Level = childLevel });

                var children = await _fileTreeService.LoadDirectoryAsync(path, _showHiddenFiles, childLevel);

                parentItem.ClearChildren();

                foreach (var child in children)
                {
                    parentItem.Children.Add(child);
                    _fileTreeCache.SetItem(child.Path, child);
                    
                    if (child.IsDirectory)
                    {
                        child.LoadChildren += async (s, e) => await LoadDirectoryContentsAsync(child);
                    }
                }
                
                parentItem.HasChildren = parentItem.Children.Count > 0;
                
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Loaded {parentItem.Children.Count} items for directory: {path}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to load directory contents: {ex.Message}");
                
                parentItem.ClearChildren();
                parentItem.Children.Add(new FileTreeItem { 
                    Name = $"Error: {ex.Message}", 
                    Level = childLevel,
                    Type = "Error"
                });
                parentItem.HasChildren = true;
            }
        }

        #endregion

        #region Event Handlers

        private void FileTreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is FileTreeItem item)
            {
                OnTreeItemClicked(item.Path);
            }
        }

        private async void TreeViewItem_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem treeViewItem && 
                treeViewItem.DataContext is FileTreeItem item && 
                item.IsDirectory)
            {
                if (item.HasDummyChild() || item.Children.Count == 0)
                {
                    await LoadDirectoryContentsAsync(item);
                }
                
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                    TreeViewItemExtensions.InitializeTreeViewItemLevels(fileTreeView);
                }));
            }
        }

        private void FileTreeView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (fileTreeView.SelectedItem is FileTreeItem item)
            {
                HandleDoubleClick(item.Path);
            }
        }

        private void FileTreeView_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (fileTreeView.SelectedItem is FileTreeItem item)
            {
                BuildContextMenu(item.Path);
            }
            else
            {
                e.Handled = true;
            }
        }

        private void FileTreeView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _dragStartPosition = e.GetPosition(fileTreeView);
        }

        private void FileTreeView_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStartPosition = null;
        }

        private void FileTreeView_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _dragStartPosition.HasValue)
            {
                Point currentPosition = e.GetPosition(fileTreeView);
                Vector dragVector = currentPosition - _dragStartPosition.Value;
                double dragDistance = Math.Sqrt(Math.Pow(dragVector.X, 2) + Math.Pow(dragVector.Y, 2));

                if (dragDistance > 10.0)
                {
                    StartDrag();
                }
            }
        }

        private void FileTreeView_DragEnter(object sender, DragEventArgs e)
        {
            // Show progress overlay for Outlook data
            if (IsOutlookData(e.Data))
            {
                ShowProgressOverlay();
            }
            
            _dragDropService.HandleDragEnter(e);
        }

        private void FileTreeView_DragOver(object sender, DragEventArgs e)
        {
            _dragDropService.HandleDragOver(e, GetItemFromPoint);
        }

        private void FileTreeView_Drop(object sender, DragEventArgs e)
        {
            string currentTreePath = _rootItems.Count > 0 ? _rootItems[0].Path : string.Empty;
            _dragDropService.HandleDrop(e, GetItemFromPoint, currentTreePath);
        }

        private void FileTreeView_DragLeave(object sender, DragEventArgs e)
        {
            _dragDropService.HandleDragLeave(e);
        }

        /// <summary>
        /// Quick check for Outlook data during drag operations
        /// </summary>
        private bool IsOutlookData(IDataObject data)
        {
            try
            {
                return data.GetDataPresent("FileGroupDescriptor") || 
                       data.GetDataPresent("RenPrivateMessages") ||
                       data.GetDataPresent("RenPrivateItem");
            }
            catch
            {
                return false;
            }
        }
        
        #endregion

        #region Navigation and Context Menu

        private void HandleDoubleClick(string path)
        {
            if (File.Exists(path))
            {
                try
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = path,
                        UseShellExecute = true
                    });
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open file:\n{path}\n\n{ex.Message}", 
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else if (Directory.Exists(path))
            {
                var item = _fileTreeService.FindItemByPath(_rootItems, path);
                if (item != null)
                {
                    item.IsExpanded = !item.IsExpanded;
                }
            }
        }

        private void OnTreeItemClicked(string path)
        {
            if (string.IsNullOrEmpty(path) || (!File.Exists(path) && !Directory.Exists(path)))
            {
                return;
            }

            if (Directory.Exists(path))
            {
                _currentFolderPath = path;
            }
            else
            {
                _currentFolderPath = Path.GetDirectoryName(path) ?? string.Empty;
            }

            LocationChanged?.Invoke(this, path);
            FileTreeClicked?.Invoke(this, EventArgs.Empty);
        }

        private void BuildContextMenu(string selectedPath)
        {
            try 
            {
                _treeContextMenu.Items.Clear();
    
                var contextMenuProvider = new ContextMenuProvider(_metadataManager, _undoManager);
                ContextMenu menu = contextMenuProvider.BuildContextMenu(selectedPath, 
                    (action, path) => ContextMenuActionTriggered?.Invoke(this, new Tuple<string, string>(action, path)));
                
                foreach (var item in menu.Items)
                {
                    _treeContextMenu.Items.Add(item);
                }
            }
            catch (Exception ex) 
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to build context menu: {ex.Message}");
                _treeContextMenu.Items.Clear();
                var menuItem = new MenuItem { Header = "Refresh" };
                menuItem.Click += (s, e) => RefreshView();
                _treeContextMenu.Items.Add(menuItem);
            }
        }
        
        #endregion

        #region File Operations

        private void DeleteItemWithUndo(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path))
            {
                MessageBox.Show($"'{path}' does not exist.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (MessageBox.Show($"Are you sure you want to delete:\n{path}?", 
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                var command = new DeleteItemCommand(_fileOperations, this, path);
                _undoManager.ExecuteCommand(command);
                
                RefreshParentDirectory(path);
            }
        }

        private void CopyItem(string path)
        {
            if (File.Exists(path) || Directory.Exists(path))
            {
                var filePaths = new System.Collections.Specialized.StringCollection();
                filePaths.Add(path);
                Clipboard.SetFileDropList(filePaths);
            }
            else
            {
                MessageBox.Show("The selected file or folder does not exist.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void PasteItem(string targetPath)
        {
            if (!Directory.Exists(targetPath))
            {
                MessageBox.Show("You can only paste into a directory.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var files = Clipboard.GetFileDropList();
            if (files.Count == 0)
            {
                MessageBox.Show("No valid file path(s) in clipboard.", "Error", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            foreach (string sourcePath in files)
            {
                if (File.Exists(sourcePath) || Directory.Exists(sourcePath))
                {
                    string newPath = _fileOperations.CopyItem(sourcePath, targetPath);
                    if (string.IsNullOrEmpty(newPath))
                    {
                        MessageBox.Show($"Failed to paste item: {sourcePath}", "Error", 
                            MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            RefreshDirectory(targetPath);
        }

        private void CreateNewFile(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("Cannot create a file outside a folder.", "Invalid Target", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newFileName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter file name (e.g., new_file.txt):", "Add New File", "");
            
            if (!string.IsNullOrWhiteSpace(newFileName))
            {
                var command = new CreateFileCommand(_fileOperations, this, directoryPath, newFileName);
                _undoManager.ExecuteCommand(command);
                
                RefreshDirectory(directoryPath);
            }
        }

        private void CreateNewFolder(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                MessageBox.Show("Cannot create a folder outside a directory.", "Invalid Target", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string newFolderName = Microsoft.VisualBasic.Interaction.InputBox(
                "Enter folder name:", "Add New Folder", "");
            
            if (!string.IsNullOrWhiteSpace(newFolderName))
            {
                var command = new CreateFolderCommand(_fileOperations, this, directoryPath, newFolderName);
                _undoManager.ExecuteCommand(command);
                
                RefreshDirectory(directoryPath);
            }
        }

        #endregion

        #region Drag and Drop

        private void StartDrag()
        {
            if (fileTreeView.SelectedItem is FileTreeItem selectedItem)
            {
                var selectedPaths = new List<string> { selectedItem.Path };
                _dragDropService.StartDrag(fileTreeView, selectedPaths);
                _dragStartPosition = null;
            }
        }
        
        private FileTreeItem GetItemFromPoint(Point point)
        {
            HitTestResult result = VisualTreeHelper.HitTest(fileTreeView, point);
            if (result == null)
                return null;
                
            DependencyObject obj = result.VisualHit;
            
            while (obj != null && !(obj is TreeViewItem))
            {
                obj = VisualTreeHelper.GetParent(obj);
            }
            
            if (obj is TreeViewItem item)
            {
                return item.DataContext as FileTreeItem;
            }
            
            return null;
        }
        
        #endregion

        #region Utility Methods

        private void RefreshParentDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            string parentDir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(parentDir))
            {
                RefreshDirectory(parentDir);
            }
        }

        private async void RefreshDirectory(string directoryPath)
        {
            await RefreshDirectoryAsync(directoryPath);
        }

        private async Task RefreshDirectoryAsync(string directoryPath)
        {
            if (string.IsNullOrEmpty(directoryPath))
                return;

            try
            {
                System.Diagnostics.Debug.WriteLine($"[DEBUG] Refreshing directory: {directoryPath}");
                
                _fileTreeCache.RemoveWhere(kvp => kvp.Key.StartsWith(directoryPath, StringComparison.OrdinalIgnoreCase));
                
                var item = _fileTreeService.FindItemByPath(_rootItems, directoryPath);
                if (item != null)
                {
                    bool wasExpanded = item.IsExpanded;
                    
                    item.ClearChildren();
                    item.HasChildren = _fileTreeService.DirectoryHasAccessibleChildren(directoryPath, _showHiddenFiles);
                    
                    if (wasExpanded && item.HasChildren)
                    {
                        await LoadDirectoryContentsAsync(item);
                        item.IsExpanded = true;
                    }
                    
                    System.Diagnostics.Debug.WriteLine($"[DEBUG] Directory refreshed: {directoryPath}, Children: {item.Children.Count}");
                }
                else if (_rootItems.Count > 0 && string.Equals(_rootItems[0].Path, directoryPath, StringComparison.OrdinalIgnoreCase))
                {
                    SetRootDirectory(directoryPath);
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[WARNING] Could not find item to refresh: {directoryPath}");
                }

                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
                {
                    OnPropertyChanged(nameof(_rootItems));
                }));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Failed to refresh directory {directoryPath}: {ex.Message}");
            }
        }

        private void RefreshTreeView()
        {
            string currentRoot = null;
            if (_rootItems.Count > 0)
            {
                currentRoot = _rootItems[0].Path;
            }
            
            if (!string.IsNullOrEmpty(currentRoot))
            {
                SetRootDirectory(currentRoot);
            }
        }
        
        public void SelectItemByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return;
                
            var item = _fileTreeService.FindItemByPath(_rootItems, path);
            if (item != null)
            {
                item.IsSelected = true;
                
                Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => {
                    var treeViewItem = FindTreeViewItemForData(fileTreeView, item);
                    treeViewItem?.BringIntoView();
                }));
            }
        }

        private TreeViewItem GetSelectedTreeViewItem()
        {
            var items = FindVisualChildren<TreeViewItem>(fileTreeView);
            return items.FirstOrDefault(item => item.IsSelected);
        }
        
        #endregion

        #region Helper Methods

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
        
        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            
            int childCount = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < childCount; i++)
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
        
        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null && !(current is T))
            {
                current = VisualTreeHelper.GetParent(current);
            }
            
            return current as T;
        }
        
        private TreeViewItem FindTreeViewItemForData(ItemsControl container, object item)
        {
            if (container == null || item == null)
                return null;
                
            if (container.ItemContainerGenerator.ContainerFromItem(item) is TreeViewItem tvi)
                return tvi;
                
            for (int i = 0; i < container.Items.Count; i++)
            {
                var childContainer = container.ItemContainerGenerator.ContainerFromIndex(i) as TreeViewItem;
                if (childContainer != null)
                {
                    var result = FindTreeViewItemForData(childContainer, item);
                    if (result != null)
                        return result;
                }
            }
            
            return null;
        }
        
        #endregion

        #region IDisposable Implementation
        
        private bool _disposed = false;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // Cancel any ongoing operations
                    _dragDropService?.CancelOutlookExtraction();
                    
                    if (fileTreeView != null)
                    {
                        fileTreeView.ItemContainerGenerator.StatusChanged -= ItemContainerGenerator_StatusChanged;
                    }
                    
                    _rootItems.Clear();
                    _fileTreeCache?.Clear();
                    (_fileTreeCache as IDisposable)?.Dispose();
                }
                
                _disposed = true;
            }
        }
        
        #endregion
    }
}