// UI/FileTree/Services/FileTreeDragDropService.cs - Optimized with throttling and caching
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using ExplorerPro.Models;
using ExplorerPro.FileOperations;
using ExplorerPro.UI.FileTree.Commands;
using ExplorerPro.UI.FileTree.DragDrop;
using ExplorerPro.UI.FileTree.Utilities;

namespace ExplorerPro.UI.FileTree.Services
{
    /// <summary>
    /// Enhanced drag and drop service with multi-selection, visual feedback, and advanced features
    /// Optimized version with event throttling and caching to prevent UI stuttering
    /// </summary>
    public class FileTreeDragDropService : IDisposable
    {
        #region Constants
        
        private const double DRAG_THRESHOLD = 10.0;
        private const string INTERNAL_FORMAT = "ExplorerPro.InternalDrop";
        private const string OUTLOOK_FORMAT = "FileGroupDescriptor";
        
        // Throttling constants
        private const int DRAG_OVER_THROTTLE_MS = 50; // Process drag over every 50ms max
        private const double POSITION_TOLERANCE = 5.0; // Tolerance for position changes
        private const int ITEM_CACHE_SIZE = 10; // Size of item position cache
        
        #endregion
        
        #region Fields
        
        private readonly UndoManager _undoManager;
        private readonly IFileOperations _fileOperations;
        private readonly SelectionService _selectionService;
        
        private Control _control;
        private Point? _dragStartPoint;
        private bool _isDragging;
        private bool _isMouseDown;
        private FileTreeItem _potentialDragItem;
        
        private DragAdorner _dragAdorner;
        private AdornerLayer _adornerLayer;
        private AutoScrollHelper _autoScrollHelper;
        private SpringLoadedFolderHelper _springLoadedHelper;
        
        private FileTreeItem _currentDropTarget;
        private bool _isValidDropTarget;
        
        // Store the getItemFromPoint delegate
        private Func<Point, FileTreeItem> _getItemFromPoint;
        
        // Store event handlers for proper cleanup
        private EventHandler<FileTreeItem> _springLoadedExpandingHandler;
        private EventHandler<FileTreeItem> _springLoadedCollapsingHandler;
        private EventHandler<FileTreeSelectionChangedEventArgs> _selectionChangedHandler;
        
        // Throttling and caching fields
        private DateTime _lastDragOverTime = DateTime.MinValue;
        private Point _lastDragOverPosition;
        private readonly Dictionary<Point, CachedItemResult> _itemPositionCache = new Dictionary<Point, CachedItemResult>();
        private readonly Queue<Point> _cacheKeyQueue = new Queue<Point>();
        
        #endregion
        
        #region Nested Types
        
        /// <summary>
        /// Cached result for GetItemFromPoint
        /// </summary>
        private class CachedItemResult
        {
            public FileTreeItem Item { get; set; }
            public DateTime CacheTime { get; set; }
            public bool IsValid => (DateTime.Now - CacheTime).TotalMilliseconds < 500; // Cache for 500ms
        }
        
        #endregion
        
        #region Events
        
        public event EventHandler<FilesDroppedEventArgs> FilesDropped;
        public event EventHandler<FilesMoved> FilesMoved;
        public event EventHandler<string> ErrorOccurred;
        public event EventHandler<OutlookExtractionCompletedEventArgs> OutlookExtractionCompleted;
        
        #endregion
        
        #region Properties
        
        /// <summary>
        /// Gets whether a drag operation is in progress
        /// </summary>
        public bool IsDragging => _isDragging;
        
        #endregion
        
        #region Constructor
        
        public FileTreeDragDropService(UndoManager undoManager = null, IFileOperations fileOperations = null, SelectionService selectionService = null)
        {
            _undoManager = undoManager ?? UndoManager.Instance;
            _fileOperations = fileOperations ?? new FileOperations.FileOperations();
            _selectionService = selectionService ?? throw new ArgumentNullException(nameof(selectionService));
            
            // Initialize helper objects
            _springLoadedHelper = new SpringLoadedFolderHelper();
            
            // Create and store event handlers
            _springLoadedExpandingHandler = OnSpringLoadedFolderExpanding;
            _springLoadedCollapsingHandler = OnSpringLoadedFolderCollapsing;
            _selectionChangedHandler = OnSelectionChanged;
            
            // Subscribe to events
            _springLoadedHelper.FolderExpanding += _springLoadedExpandingHandler;
            _springLoadedHelper.FolderCollapsing += _springLoadedCollapsingHandler;
        }
        
        #endregion
        
        #region Initialization
        
        /// <summary>
        /// Attaches the service to a control
        /// </summary>
        public void AttachToControl(Control control, Func<Point, FileTreeItem> getItemFromPoint = null)
        {
            if (_control != null)
                DetachFromControl();
            
            _control = control;
            _getItemFromPoint = getItemFromPoint;
            
            // Find ScrollViewer for auto-scroll
            var scrollViewer = VisualTreeHelperEx.FindScrollViewer(_control);
            if (scrollViewer != null)
            {
                // Dispose old helper if exists
                _autoScrollHelper?.Dispose();
                _autoScrollHelper = new AutoScrollHelper(scrollViewer);
            }
            
            // Attach ONLY drag/drop event handlers, not mouse events
            // Let the tree view handle selection through mouse events
            _control.DragEnter += OnDragEnter;
            _control.DragOver += OnDragOver;
            _control.DragLeave += OnDragLeave;
            _control.Drop += OnDrop;
            _control.GiveFeedback += OnGiveFeedback;
            _control.QueryContinueDrag += OnQueryContinueDrag;
            
            // Monitor mouse for drag initiation, but with lower priority
            _control.PreviewMouseMove += OnPreviewMouseMove;
            _control.PreviewMouseLeftButtonUp += OnPreviewMouseLeftButtonUp;
            
            // Subscribe to selection changes to know when to enable dragging
            if (_selectionService != null)
            {
                _selectionService.SelectionChanged += _selectionChangedHandler;
            }
        }
        
        /// <summary>
        /// Sets the getItemFromPoint function after attachment
        /// </summary>
        public void SetGetItemFromPointFunction(Func<Point, FileTreeItem> getItemFromPoint)
        {
            _getItemFromPoint = getItemFromPoint;
        }
        
        /// <summary>
        /// Detaches the service from the current control
        /// </summary>
        public void DetachFromControl()
        {
            if (_control == null) return;
            
            // Clean up any ongoing drag operation
            CleanupDrag();
            
            // Detach event handlers
            _control.DragEnter -= OnDragEnter;
            _control.DragOver -= OnDragOver;
            _control.DragLeave -= OnDragLeave;
            _control.Drop -= OnDrop;
            _control.GiveFeedback -= OnGiveFeedback;
            _control.QueryContinueDrag -= OnQueryContinueDrag;
            _control.PreviewMouseMove -= OnPreviewMouseMove;
            _control.PreviewMouseLeftButtonUp -= OnPreviewMouseLeftButtonUp;
            
            // Unsubscribe from selection service
            if (_selectionService != null && _selectionChangedHandler != null)
            {
                _selectionService.SelectionChanged -= _selectionChangedHandler;
            }
            
            // Dispose and null out helper objects
            if (_autoScrollHelper != null)
            {
                _autoScrollHelper.Dispose();
                _autoScrollHelper = null;
            }
            
            // Clear caches
            ClearCaches();
            
            // Clear references
            _control = null;
            _getItemFromPoint = null;
        }
        
        #endregion
        
        #region Public Interface Methods (for compatibility)
        
        public void HandleDragEnter(DragEventArgs e)
        {
            OnDragEnter(this, e);
        }
        
        public void HandleDragOver(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint)
        {
            // Store the function if provided
            if (getItemFromPoint != null)
                _getItemFromPoint = getItemFromPoint;
                
            OnDragOver(this, e);
        }
        
        public void HandleDragLeave(DragEventArgs e)
        {
            OnDragLeave(this, e);
        }
        
        public bool HandleDrop(DragEventArgs e, Func<Point, FileTreeItem> getItemFromPoint, string currentTreePath = null)
        {
            // Store the function if provided
            if (getItemFromPoint != null)
                _getItemFromPoint = getItemFromPoint;
                
            OnDrop(this, e);
            return true;
        }
        
        public void StartDrag(DependencyObject source, IEnumerable<string> selectedPaths)
        {
            // Use the selection service's current selection
            if (_selectionService?.HasSelection == true)
            {
                StartDragOperation();
            }
        }
        
        public bool HandleExternalFileDrop(string[] droppedFiles, string targetPath)
        {
            return HandleFileDropInternal(droppedFiles, targetPath, DragDropEffects.Copy, false);
        }
        
        public bool HandleInternalFileMove(string[] droppedFiles, string targetPath, string currentTreePath)
        {
            return HandleFileDropInternal(droppedFiles, targetPath, DragDropEffects.Move, true);
        }
        
        public bool HandleOutlookDrop(DataObject dataObject, string targetPath)
        {
            // DEADLOCK FIX: Instead of using task.Wait() which can cause UI thread deadlock,
            // we use Task.Run to execute the async operation on a background thread.
            // This approach ensures that:
            // 1. The UI thread is not blocked
            // 2. We maintain the same return type (bool)
            // 3. The method can be safely called from the UI thread
            // 4. Error handling and events are properly maintained
            
            try
            {
                // Execute the async operation on a background thread to prevent UI deadlock
                var task = Task.Run(async () => await HandleOutlookDropAsync(dataObject, targetPath));
                
                // Use ConfigureAwait(false) and GetAwaiter().GetResult() for safer synchronous waiting
                // This approach is safer than task.Wait() as it doesn't wrap exceptions in AggregateException
                return task.ConfigureAwait(false).GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                // Ensure any exceptions are properly handled and reported
                OnError($"Outlook drop operation failed: {ex.Message}");
                return false;
            }
        }
        
        public async Task<bool> HandleOutlookDropAsync(DataObject dataObject, string targetPath)
        {
            try
            {
                // Use ConfigureAwait(false) to avoid potential deadlocks and improve performance
                // since we don't need to continue on the original synchronization context
                var extractionResult = await OutlookDataExtractor.ExtractOutlookFilesAsync(dataObject, targetPath)
                    .ConfigureAwait(false);
                
                // THREAD SAFETY: Ensure events are raised on the UI thread
                // This is important because WPF controls and event handlers typically expect to run on the UI thread
                if (Application.Current?.Dispatcher != null)
                {
                    // Use Dispatcher.Invoke to marshal the event to the UI thread synchronously
                    // We use synchronous invoke here because the caller is already handling async execution
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnOutlookExtractionCompleted(new OutlookExtractionCompletedEventArgs(extractionResult, targetPath));
                    });
                }
                else
                {
                    // Fallback: raise event directly if no dispatcher is available (e.g., in unit tests)
                    OnOutlookExtractionCompleted(new OutlookExtractionCompletedEventArgs(extractionResult, targetPath));
                }
                
                return extractionResult.Success;
            }
            catch (Exception ex)
            {
                // THREAD SAFETY: Ensure error event is also raised on the UI thread
                if (Application.Current?.Dispatcher != null)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        OnError($"Outlook extraction failed: {ex.Message}");
                    });
                }
                else
                {
                    OnError($"Outlook extraction failed: {ex.Message}");
                }
                return false;
            }
        }
        
        public void CancelOutlookExtraction()
        {
            // Cancellation not implemented in current version
        }
        
        #endregion
        
        #region Drag Initiation
        
        private void OnSelectionChanged(object sender, FileTreeSelectionChangedEventArgs e)
        {
            // Track when mouse is down and we have a selection
            if (Mouse.LeftButton == MouseButtonState.Pressed && _selectionService?.HasSelection == true)
            {
                _isMouseDown = true;
                _dragStartPoint = Mouse.GetPosition(_control);
                
                // Get the item under the mouse
                if (_getItemFromPoint != null)
                {
                    _potentialDragItem = GetItemFromPointCached(_dragStartPoint.Value);
                    
                    // Only prepare for drag if the clicked item is selected
                    if (_potentialDragItem == null || !_selectionService.IsItemSelected(_potentialDragItem))
                    {
                        _dragStartPoint = null;
                        _potentialDragItem = null;
                    }
                }
            }
        }
        
        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            // Only process if we have a potential drag operation
            if (e.LeftButton != MouseButtonState.Pressed || !_dragStartPoint.HasValue || _isDragging)
                return;
            
            // Check if we should start dragging
            if (_selectionService?.HasSelection == true && _potentialDragItem != null && 
                _selectionService.IsItemSelected(_potentialDragItem))
            {
                Point currentPosition = e.GetPosition(_control);
                Vector difference = _dragStartPoint.Value - currentPosition;
                
                if (Math.Abs(difference.X) > DRAG_THRESHOLD || Math.Abs(difference.Y) > DRAG_THRESHOLD)
                {
                    StartDragOperation();
                }
            }
        }
        
        private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _dragStartPoint = null;
            _potentialDragItem = null;
            _isMouseDown = false;
        }
        
        private void StartDragOperation()
        {
            if (_selectionService == null || !_selectionService.HasSelection || _isDragging)
                return;
            
            try
            {
                _isDragging = true;
                
                // Clear caches at start of drag
                ClearCaches();
                
                // Create data object with selected files from SelectionService
                var dataObject = CreateDragDataObject();
                
                // Create visual feedback
                CreateDragAdorner();
                
                // Start drag operation
                DragDropEffects effects = System.Windows.DragDrop.DoDragDrop(_control, dataObject, 
                    DragDropEffects.Copy | DragDropEffects.Move | DragDropEffects.Link);
                
                // Handle result
                if (effects != DragDropEffects.None)
                {
                    // Operation completed
                }
            }
            catch (Exception ex)
            {
                OnError($"Failed to start drag: {ex.Message}");
            }
            finally
            {
                CleanupDrag();
            }
        }
        
        private DataObject CreateDragDataObject()
        {
            var dataObject = new DataObject();
            
            // Use SelectionService to get selected paths
            var paths = _selectionService.SelectedPaths.ToArray();
            dataObject.SetData(DataFormats.FileDrop, paths);
            
            // Add internal marker
            dataObject.SetData(INTERNAL_FORMAT, true);
            
            // Add selection count for visual feedback
            dataObject.SetData("ItemCount", _selectionService.SelectionCount);
            
            return dataObject;
        }
        
        private void CreateDragAdorner()
        {
            if (_control == null || _selectionService == null || !_selectionService.HasSelection)
                return;
            
            _adornerLayer = AdornerLayer.GetAdornerLayer(_control);
            if (_adornerLayer != null)
            {
                // Create visual element for the adorner
                var visual = CreateDragVisual();
                
                // Calculate proper offset based on initial click position
                Point offset = CalculateDragOffset(visual);
                
                _dragAdorner = new DragAdorner(_control, visual, offset, _selectionService.SelectionCount);
                _adornerLayer.Add(_dragAdorner);
                
                // Set initial position
                _dragAdorner.UpdatePosition(Mouse.GetPosition(_adornerLayer));
            }
        }
        
        /// <summary>
        /// Calculates the drag offset based on where the user initially clicked
        /// </summary>
        private Point CalculateDragOffset(Visual dragVisual)
        {
            if (!_dragStartPoint.HasValue)
                return new Point(10, 10); // Fallback offset
            
            // Calculate offset from the drag visual size
            double offsetX = 10; // Small offset to show the ghost image slightly offset from cursor
            double offsetY = 5;  // Reduced Y offset for better positioning
            
            // If we have the visual size, we can position it more intelligently
            if (dragVisual is FrameworkElement fe)
            {
                // Make sure the visual is measured
                fe.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                
                // Position the ghost so the cursor appears near the icon/beginning of the text
                // This makes it feel like you're "holding" the file item
                offsetX = Math.Max(5, Math.Min(fe.DesiredSize.Width * 0.15, 15)); 
                offsetY = Math.Max(3, Math.Min(fe.DesiredSize.Height * 0.3, 10));
            }
            
            // If we have information about the tree view item that was clicked,
            // we could further refine the offset based on the click position within that item
            if (_potentialDragItem != null)
            {
                // Try to find the actual tree view item to get more precise positioning
                var treeViewItem = FindTreeViewItemForData(_potentialDragItem);
                if (treeViewItem != null)
                {
                    // Get the relative position within the tree view item
                    var relativePosition = treeViewItem.TranslatePoint(new Point(0, 0), _control);
                    var clickOffsetInItem = new Point(
                        _dragStartPoint.Value.X - relativePosition.X,
                        _dragStartPoint.Value.Y - relativePosition.Y
                    );
                    
                    // Adjust offset to maintain the visual relationship
                    // Keep the ghost image positioned relative to where they clicked
                    offsetX = Math.Max(5, Math.Min(clickOffsetInItem.X, 25));
                    offsetY = Math.Max(2, Math.Min(clickOffsetInItem.Y, 12));
                }
            }
            
            return new Point(offsetX, offsetY);
        }
        
        private Visual CreateDragVisual()
        {
            // Create a visual based on SelectionService's selected items
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Background = new SolidColorBrush(Color.FromArgb(220, 248, 248, 248)), // Slightly more opaque background
                Margin = new Thickness(2)  // Adjusted margin for spacing
            };
            
            // Add icon from first selected item
            var firstItem = _selectionService?.FirstSelectedItem;
            if (firstItem?.Icon != null)
            {
                var icon = new Image
                {
                    Source = firstItem.Icon,
                    Width = 16,
                    Height = 16,
                    Margin = new Thickness(2, 1, 4, 1)  // Adjusted margins
                };
                panel.Children.Add(icon);
            }
            
            // Add text
            var fileName = _selectionService?.SelectionCount == 1 
                ? Path.GetFileName(_selectionService.FirstSelectedPath ?? "")
                : $"{_selectionService?.SelectionCount ?? 0} items";
                
            var textBlock = new TextBlock
            {
                Text = fileName,
                Padding = new Thickness(0, 1, 4, 1),  // Adjusted padding
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 12,  // Explicit font size
                Foreground = new SolidColorBrush(Color.FromRgb(40, 40, 40))  // Darker text for better contrast
            };
            panel.Children.Add(textBlock);
            
            // Create border with more subtle styling
            var border = new Border
            {
                Child = panel,
                BorderBrush = new SolidColorBrush(Color.FromArgb(180, 160, 160, 160)),  // More subtle border
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(3),  // Slightly more rounded
                Background = new SolidColorBrush(Color.FromArgb(245, 255, 255, 255)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = Colors.Gray,
                    BlurRadius = 3,
                    ShadowDepth = 1,  // Reduced shadow depth
                    Opacity = 0.3
                }
            };
            
            // Force layout with more reasonable constraints
            border.Measure(new Size(200, 50));  // Smaller max size
            border.Arrange(new Rect(new Point(0, 0), border.DesiredSize));
            
            return border;
        }
        
        #endregion
        
        #region Drag Over Handling - OPTIMIZED WITH THROTTLING
        
        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            // Clear caches when entering
            ClearCaches();
            
            ProcessDragOver(e);
        }
        
        private void OnDragOver(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            var position = e.GetPosition(_control);
            
            // Check if we should throttle this event
            if (ShouldThrottleDragOver(position))
            {
                // Skip processing but still allow drop
                e.Effects = _isValidDropTarget ? DetermineDropEffects(e) : DragDropEffects.None;
                return;
            }
            
            // Update last processed time and position
            _lastDragOverTime = DateTime.Now;
            _lastDragOverPosition = position;
            
            // Get item under mouse using cached function
            if (_getItemFromPoint != null)
            {
                var item = GetItemFromPointCached(position);
                
                // Only update if item changed
                if (item != _currentDropTarget)
                {
                    UpdateDropTarget(item);
                }
            }
            
            ProcessDragOver(e);
            
            // Update auto-scroll
            if (_autoScrollHelper != null)
            {
                _autoScrollHelper.UpdatePosition(position);
                
                if (_autoScrollHelper.IsInScrollZone(position))
                    _autoScrollHelper.Start();
                else
                    _autoScrollHelper.Stop();
            }
            
            // Update adorner position (always update for smooth movement)
            if (_dragAdorner != null && _adornerLayer != null)
            {
                _dragAdorner.UpdatePosition(e.GetPosition(_adornerLayer));
                _dragAdorner.UpdateEffects(e.Effects);
            }
        }
        
        private void OnDragLeave(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            // Check if really leaving
            Point pt = e.GetPosition(_control);
            Rect bounds = new Rect(_control.RenderSize);
            
            if (!bounds.Contains(pt))
            {
                UpdateDropTarget(null);
                _autoScrollHelper?.Stop();
                _springLoadedHelper?.CancelAll();
                
                // Clear caches when leaving
                ClearCaches();
            }
        }
        
        /// <summary>
        /// Determines if drag over event should be throttled
        /// </summary>
        private bool ShouldThrottleDragOver(Point currentPosition)
        {
            // Check time throttling
            var timeSinceLastUpdate = (DateTime.Now - _lastDragOverTime).TotalMilliseconds;
            if (timeSinceLastUpdate < DRAG_OVER_THROTTLE_MS)
            {
                // Also check if position changed significantly
                var distance = (currentPosition - _lastDragOverPosition).Length;
                if (distance < POSITION_TOLERANCE)
                {
                    return true; // Throttle this event
                }
            }
            
            return false;
        }
        
        /// <summary>
        /// Gets item from point with caching
        /// </summary>
        private FileTreeItem GetItemFromPointCached(Point position)
        {
            if (_getItemFromPoint == null) return null;
            
            // Round position to reduce cache misses for minor movements
            var roundedPosition = new Point(
                Math.Round(position.X / POSITION_TOLERANCE) * POSITION_TOLERANCE,
                Math.Round(position.Y / POSITION_TOLERANCE) * POSITION_TOLERANCE
            );
            
            // Check cache
            if (_itemPositionCache.TryGetValue(roundedPosition, out var cachedResult) && cachedResult.IsValid)
            {
                return cachedResult.Item;
            }
            
            // Not in cache or expired, get the item
            var item = _getItemFromPoint(position);
            
            // Update cache
            if (_itemPositionCache.Count >= ITEM_CACHE_SIZE)
            {
                // Remove oldest entry
                if (_cacheKeyQueue.Count > 0)
                {
                    var oldestKey = _cacheKeyQueue.Dequeue();
                    _itemPositionCache.Remove(oldestKey);
                }
            }
            
            _itemPositionCache[roundedPosition] = new CachedItemResult 
            { 
                Item = item, 
                CacheTime = DateTime.Now 
            };
            _cacheKeyQueue.Enqueue(roundedPosition);
            
            return item;
        }
        
        /// <summary>
        /// Clears all caches
        /// </summary>
        private void ClearCaches()
        {
            _itemPositionCache.Clear();
            _cacheKeyQueue.Clear();
            _lastDragOverTime = DateTime.MinValue;
        }
        
        private void ProcessDragOver(DragEventArgs e)
        {
            // Validate drop
            if (!IsValidDrop(e))
            {
                e.Effects = DragDropEffects.None;
                return;
            }
            
            // Determine effects based on modifiers
            e.Effects = DetermineDropEffects(e);
            
            // Handle spring-loaded folders
            if (_currentDropTarget != null && _currentDropTarget.IsDirectory && !_currentDropTarget.IsExpanded)
            {
                _springLoadedHelper?.StartHover(_currentDropTarget);
            }
            else if (_currentDropTarget != null)
            {
                _springLoadedHelper?.StopHover(_currentDropTarget);
            }
        }
        
        private DragDropEffects DetermineDropEffects(DragEventArgs e)
        {
            if (!_isValidDropTarget)
                return DragDropEffects.None;
            
            // Check modifier keys
            if ((e.KeyStates & DragDropKeyStates.ControlKey) != 0)
                return DragDropEffects.Copy;
            
            if ((e.KeyStates & DragDropKeyStates.AltKey) != 0)
                return DragDropEffects.Link;
            
            // Default behavior
            bool isInternal = e.Data.GetDataPresent(INTERNAL_FORMAT);
            return isInternal ? DragDropEffects.Move : DragDropEffects.Copy;
        }
        
        #endregion
        
        #region Drop Handling
        
        private void OnDrop(object sender, DragEventArgs e)
        {
            e.Handled = true;
            
            try
            {
                _autoScrollHelper?.Stop();
                _springLoadedHelper?.CollapseAll();
                
                // Clear caches after drop
                ClearCaches();
                
                // Get final drop target
                if (_getItemFromPoint != null)
                {
                    var position = e.GetPosition(_control);
                    var item = GetItemFromPointCached(position);
                    UpdateDropTarget(item);
                }
                
                if (!_isValidDropTarget || _currentDropTarget == null)
                {
                    e.Effects = DragDropEffects.None;
                    return;
                }
                
                string targetPath = _currentDropTarget.Path;
                var effects = DetermineDropEffects(e);
                
                // Handle different data types
                if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    var files = (string[])e.Data.GetData(DataFormats.FileDrop);
                    bool isInternal = e.Data.GetDataPresent(INTERNAL_FORMAT);
                    
                    HandleFileDropInternal(files, targetPath, effects, isInternal);
                }
                else if (IsOutlookData(e.Data))
                {
                    _ = HandleOutlookDropAsync(e.Data as DataObject, targetPath);
                }
            }
            finally
            {
                UpdateDropTarget(null);
            }
        }
        
        private bool HandleFileDropInternal(string[] files, string targetPath, DragDropEffects effects, bool isInternal)
        {
            try
            {
                // Filter out invalid files
                files = files.Where(f => File.Exists(f) || Directory.Exists(f)).ToArray();
                if (!files.Any()) return false;
                
                // Create and execute command for undo support
                var command = new DragDropCommand(_fileOperations, files, targetPath, effects, MetadataManager.Instance);
                _undoManager.ExecuteCommand(command);
                
                // Raise appropriate events
                if (effects == DragDropEffects.Move && isInternal)
                {
                    var sourceDirs = files.Select(f => Path.GetDirectoryName(f))
                        .Where(d => !string.IsNullOrEmpty(d))
                        .Distinct()
                        .ToArray();
                    
                    OnFilesMoved(new FilesMoved(files, sourceDirs, targetPath));
                }
                
                OnFilesDropped(new FilesDroppedEventArgs(files, targetPath, effects, isInternal));
                
                return true;
            }
            catch (Exception ex)
            {
                OnError($"Drop operation failed: {ex.Message}");
                return false;
            }
        }
        
        #endregion
        
        #region Visual Feedback - OPTIMIZED
        
        private void OnGiveFeedback(object sender, GiveFeedbackEventArgs e)
        {
            if (_dragAdorner != null)
            {
                // Custom cursors based on effect
                e.UseDefaultCursors = false;
                
                switch (e.Effects)
                {
                    case DragDropEffects.Copy:
                        Mouse.SetCursor(Cursors.Cross);
                        break;
                    case DragDropEffects.Move:
                        Mouse.SetCursor(Cursors.Hand);
                        break;
                    case DragDropEffects.Link:
                        Mouse.SetCursor(Cursors.UpArrow);
                        break;
                    case DragDropEffects.None:
                        Mouse.SetCursor(Cursors.No);
                        break;
                    default:
                        e.UseDefaultCursors = true;
                        break;
                }
                
                e.Handled = true;
            }
        }
        
        private void OnQueryContinueDrag(object sender, QueryContinueDragEventArgs e)
        {
            if (e.EscapePressed)
            {
                e.Action = DragAction.Cancel;
                CleanupDrag();
            }
        }
        
        private void UpdateDropTarget(FileTreeItem item)
        {
            // Only update if target changed
            if (_currentDropTarget == item)
                return;
            
            // Clear old highlight
            if (_currentDropTarget != null)
            {
                ClearDropTargetVisual(_currentDropTarget);
            }
            
            _currentDropTarget = item;
            _isValidDropTarget = ValidateDropTarget(item);
            
            // Apply new highlight
            if (_currentDropTarget != null)
            {
                ApplyDropTargetVisual(_currentDropTarget, _isValidDropTarget);
            }
        }
        
        private void ApplyDropTargetVisual(FileTreeItem item, bool isValid)
        {
            if (item == null) return;
            
            // Find the TreeViewItem for this data
            var treeViewItem = FindTreeViewItemForData(item);
            if (treeViewItem != null)
            {
                if (isValid)
                {
                    DragDropHelper.SetIsDropTarget(treeViewItem, true);
                    DragDropHelper.SetIsInvalidDropTarget(treeViewItem, false);
                }
                else
                {
                    DragDropHelper.SetIsDropTarget(treeViewItem, false);
                    DragDropHelper.SetIsInvalidDropTarget(treeViewItem, true);
                }
            }
        }
        
        private void ClearDropTargetVisual(FileTreeItem item)
        {
            if (item == null) return;
            
            var treeViewItem = FindTreeViewItemForData(item);
            if (treeViewItem != null)
            {
                DragDropHelper.SetIsDropTarget(treeViewItem, false);
                DragDropHelper.SetIsInvalidDropTarget(treeViewItem, false);
            }
        }
        
        private TreeViewItem FindTreeViewItemForData(FileTreeItem data)
        {
            if (_control is TreeView treeView)
            {
                return VisualTreeHelperEx.FindTreeViewItem(treeView, data);
            }
            return null;
        }
        
        #endregion
        
        #region Validation
        
        private bool IsValidDrop(DragEventArgs e)
        {
            return e.Data.GetDataPresent(DataFormats.FileDrop) || IsOutlookData(e.Data);
        }
        
        private bool ValidateDropTarget(FileTreeItem target)
        {
            if (target == null || !target.IsDirectory)
                return false;
            
            // Can't drop on selected items
            if (_selectionService?.IsItemSelected(target) == true)
                return false;
            
            // Can't drop parent on child
            if (_selectionService != null)
            {
                foreach (var selectedPath in _selectionService.SelectedPaths)
                {
                    if (target.Path.StartsWith(selectedPath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        return false;
                }
            }
            
            return true;
        }
        
        private bool IsOutlookData(IDataObject data)
        {
            return data.GetDataPresent("FileGroupDescriptor") || 
                   data.GetDataPresent("FileGroupDescriptorW");
        }
        
        #endregion
        
        #region Helper Methods
        
        private void CleanupDrag()
        {
            _isDragging = false;
            _dragStartPoint = null;
            _potentialDragItem = null;
            _isMouseDown = false;
            
            if (_dragAdorner != null && _adornerLayer != null)
            {
                _adornerLayer.Remove(_dragAdorner);
                _dragAdorner = null;
            }
            
            _autoScrollHelper?.Stop();
            _springLoadedHelper?.CancelAll();
            UpdateDropTarget(null);
            
            // Clear caches
            ClearCaches();
        }
        
        private void OnSpringLoadedFolderExpanding(object sender, FileTreeItem item)
        {
            if (item != null)
                item.IsExpanded = true;
        }
        
        private void OnSpringLoadedFolderCollapsing(object sender, FileTreeItem item)
        {
            if (item != null && _springLoadedHelper?.WasAutoExpanded(item) == true)
                item.IsExpanded = false;
        }
        
        #endregion
        
        #region Event Raising
        
        private void OnFilesDropped(FilesDroppedEventArgs e)
        {
            FilesDropped?.Invoke(this, e);
        }
        
        private void OnFilesMoved(FilesMoved e)
        {
            FilesMoved?.Invoke(this, e);
        }
        
        private void OnError(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }
        
        private void OnOutlookExtractionCompleted(OutlookExtractionCompletedEventArgs e)
        {
            OutlookExtractionCompleted?.Invoke(this, e);
        }
        
        #endregion
        
        #region IDisposable
        
        private bool _disposed;
        
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
                    // Detach from control (which also cleans up event handlers)
                    DetachFromControl();
                    
                    // Clean up any ongoing drag
                    CleanupDrag();
                    
                    // Dispose helper objects
                    if (_autoScrollHelper != null)
                    {
                        _autoScrollHelper.Dispose();
                        _autoScrollHelper = null;
                    }
                    
                    // Unsubscribe from spring-loaded helper events
                    if (_springLoadedHelper != null)
                    {
                        if (_springLoadedExpandingHandler != null)
                        {
                            _springLoadedHelper.FolderExpanding -= _springLoadedExpandingHandler;
                        }
                        if (_springLoadedCollapsingHandler != null)
                        {
                            _springLoadedHelper.FolderCollapsing -= _springLoadedCollapsingHandler;
                        }
                        _springLoadedHelper.Dispose();
                        _springLoadedHelper = null;
                    }
                    
                    // Clear event handlers
                    FilesDropped = null;
                    FilesMoved = null;
                    ErrorOccurred = null;
                    OutlookExtractionCompleted = null;
                    
                    // Clear stored delegates
                    _springLoadedExpandingHandler = null;
                    _springLoadedCollapsingHandler = null;
                    _selectionChangedHandler = null;
                    _getItemFromPoint = null;
                    
                    // Clear references
                    _dragAdorner = null;
                    _adornerLayer = null;
                    _currentDropTarget = null;
                    _potentialDragItem = null;
                    
                    // Clear caches
                    ClearCaches();
                }
                
                _disposed = true;
            }
        }
        
        ~FileTreeDragDropService()
        {
            Dispose(false);
        }
        
        #endregion
    }
}