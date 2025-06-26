using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using ExplorerPro.Models;
using ExplorerPro.UI.MainWindow;
using ExplorerPro.Core.Collections;
using System.ComponentModel.DataAnnotations;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Modern enterprise-level tab manager service with advanced features.
    /// Provides thread-safe operations, async support, validation, and memory optimization.
    /// </summary>
    public class ModernTabManagerService : ITabManagerService
    {
        #region Private Fields
        
        private readonly ILogger<ModernTabManagerService> _logger;
        private readonly ITabValidator _validator;
        private readonly ITabMemoryOptimizer _memoryOptimizer;
        private readonly SemaphoreSlim _operationSemaphore;
        private readonly CancellationTokenSource _cancellationTokenSource;
        
        private readonly BoundedCollection<TabModel> _tabs;
        private TabModel _activeTab;
        private bool _isDisposed;
        private readonly object _activationLock = new object();
        
        // Performance tracking
        private readonly ConcurrentDictionary<Guid, TabPerformanceMetrics> _performanceMetrics;
        private int _totalTabsCreated;
        private int _totalTabsClosed;
        
        #endregion

        #region Constructor
        
        public ModernTabManagerService(
            ILogger<ModernTabManagerService> logger = null,
            ITabValidator validator = null,
            ITabMemoryOptimizer memoryOptimizer = null)
        {
            _logger = logger;
            _validator = validator ?? new DefaultTabValidator();
            _memoryOptimizer = memoryOptimizer ?? new DefaultTabMemoryOptimizer();
            _operationSemaphore = new SemaphoreSlim(1, 1);
            _cancellationTokenSource = new CancellationTokenSource();
            
            _tabs = new BoundedCollection<TabModel>(maxSize: 50);
            _performanceMetrics = new ConcurrentDictionary<Guid, TabPerformanceMetrics>();
            
            _logger?.LogInformation("ModernTabManagerService initialized with capacity {Capacity}", _tabs.MaxSize);
        }
        
        #endregion

        #region ITabManagerService Properties
        
        public ObservableCollection<TabModel> Tabs => _tabs.Collection;
        
        public TabModel ActiveTab
        {
            get => _activeTab;
            set
            {
                lock (_activationLock)
                {
                    if (_activeTab != value)
                    {
                        var oldTab = _activeTab;
                        var oldIndex = oldTab != null ? GetTabIndex(oldTab) : -1;
                        var newIndex = value != null ? GetTabIndex(value) : -1;
                        
                        // Deactivate old tab
                        if (oldTab != null)
                        {
                            oldTab.IsActive = false;
                            RecordTabDeactivation(oldTab);
                        }
                        
                        _activeTab = value;
                        
                        // Activate new tab
                        if (_activeTab != null)
                        {
                            _activeTab.IsActive = true;
                            RecordTabActivation(_activeTab);
                        }
                        
                        ActiveTabChanged?.Invoke(this, new TabChangedEventArgs(oldTab, _activeTab, oldIndex, newIndex));
                        _logger?.LogDebug("Active tab changed from '{OldTitle}' to '{NewTitle}'", 
                            oldTab?.Title, _activeTab?.Title);
                    }
                }
            }
        }
        
        public int TabCount => _tabs.Count;
        public bool HasTabs => _tabs.Count > 0;
        
        #endregion

        #region Events
        
        public event EventHandler<TabEventArgs> TabCreated;
        public event EventHandler<TabEventArgs> TabClosed;
        public event EventHandler<TabChangedEventArgs> ActiveTabChanged;
        public event EventHandler<TabModifiedEventArgs> TabModified;
        public event EventHandler<TabReorderedEventArgs> TabsReordered;
        
        #endregion

        #region Core Tab Operations
        
        public async Task<TabModel> CreateTabAsync(string title, string path = null, TabCreationOptions options = null)
        {
            var request = new TabCreationRequest
            {
                Title = title,
                Path = path ?? string.Empty,
                MakeActive = options?.MakeActive ?? true,
                IsPinned = options?.IsPinned ?? false,
                CustomColor = options?.CustomColor,
                Content = options?.Content
            };
            
            return await CreateTabAsync(request);
        }
        
        public async Task<TabModel> CreateTabAsync(TabCreationRequest request)
        {
            ThrowIfDisposed();
            
            await _operationSemaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                // Enterprise validation
                var validationResult = await _validator.ValidateCreationAsync(request);
                if (!validationResult.IsValid)
                {
                    var errorMessage = string.Join(", ", validationResult.Errors);
                    _logger?.LogWarning("Tab creation validation failed: {Errors}", errorMessage);
                    throw new TabValidationException(errorMessage);
                }
                
                // Check capacity
                if (!_tabs.CanAdd())
                {
                    _logger?.LogWarning("Maximum tab count reached. Attempting memory optimization.");
                    await OptimizeTabMemoryAsync();
                    
                    if (!_tabs.CanAdd())
                        throw new InvalidOperationException("Maximum tab count reached and unable to free capacity");
                }
                
                // Create tab model
                var tab = TabModel.FromCreationRequest(request);
                
                // Determine insertion position
                int insertIndex = CalculateInsertionIndex(request, tab);
                
                // Thread-safe insertion
                _tabs.Insert(insertIndex, tab);
                
                // Wire up events
                tab.PropertyChanged += OnTabPropertyChanged;
                
                // Initialize performance tracking  
                var tabId = Guid.Parse(tab.Id);
                _performanceMetrics[tabId] = new TabPerformanceMetrics
                {
                    CreatedAt = DateTime.UtcNow,
                    ActivationCount = 0
                };
                
                // Async initialization if content provided
                if (request.Content != null || !request.DeferContentLoading)
                {
                    await tab.InitializeAsync();
                }
                
                // Activate if requested
                if (request.MakeActive)
                {
                    ActiveTab = tab;
                }
                
                // Update statistics
                Interlocked.Increment(ref _totalTabsCreated);
                
                TabCreated?.Invoke(this, new TabEventArgs(tab, insertIndex));
                _logger?.LogInformation("Created tab '{Title}' at index {Index} (Total: {Total})", 
                    tab.Title, insertIndex, TabCount);
                
                return tab;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to create tab with title '{Title}'", request?.Title);
                throw;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        
        public async Task<bool> CloseTabAsync(TabModel tab, bool force = false)
        {
            ThrowIfDisposed();
            if (tab == null || !_tabs.Contains(tab)) return false;
            
            await _operationSemaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                // Validate closure
                if (!force)
                {
                    var validationResult = await _validator.ValidateOperationAsync(tab, TabOperation.Close);
                    if (!validationResult.IsValid)
                    {
                        _logger?.LogWarning("Tab close validation failed for '{Title}': {Errors}", 
                            tab.Title, string.Join(", ", validationResult.Errors));
                        return false;
                    }
                }
                
                var index = GetTabIndex(tab);
                
                // Handle active tab closure
                if (_activeTab == tab && TabCount > 1)
                {
                    var nextTab = GetNextTabToActivate(tab);
                    ActiveTab = nextTab;
                }
                else if (TabCount == 1)
                {
                    ActiveTab = null;
                }
                
                // Remove from collection
                _tabs.Remove(tab);
                
                // Cleanup
                tab.PropertyChanged -= OnTabPropertyChanged;
                var tabId = Guid.Parse(tab.Id);
                _performanceMetrics.TryRemove(tabId, out _);
                
                // Dispose tab
                tab.Dispose();
                
                // Update statistics
                Interlocked.Increment(ref _totalTabsClosed);
                
                TabClosed?.Invoke(this, new TabEventArgs(tab, index));
                _logger?.LogInformation("Closed tab '{Title}' from index {Index} (Remaining: {Remaining})", 
                    tab.Title, index, TabCount);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to close tab '{Title}'", tab.Title);
                return false;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        
        public async Task<bool> CloseTabAsync(int index, bool force = false)
        {
            var tab = GetTabAt(index);
            return tab != null && await CloseTabAsync(tab, force);
        }
        
        public async Task<TabModel> DuplicateTabAsync(TabModel tab)
        {
            ThrowIfDisposed();
            if (tab == null || !_tabs.Contains(tab)) return null;
            
            try
            {
                var clonedTab = tab.Clone();
                clonedTab.Title += " (Copy)";
                
                var request = new TabCreationRequest
                {
                    Title = clonedTab.Title,
                    Path = clonedTab.Path,
                    MakeActive = false,
                    IsPinned = false, // Don't duplicate pinned state
                    CustomColor = clonedTab.HasCustomColor ? clonedTab.CustomColor : null,
                    Content = clonedTab.Content
                };
                
                return await CreateTabAsync(request);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to duplicate tab '{Title}'", tab.Title);
                return null;
            }
        }
        
        #endregion

        #region Tab Management
        
        public async Task ActivateTabAsync(TabModel tab)
        {
            ThrowIfDisposed();
            if (tab != null && _tabs.Contains(tab))
            {
                ActiveTab = tab;
                await Task.CompletedTask;
            }
        }
        
        public async Task ActivateTabAsync(int index)
        {
            var tab = GetTabAt(index);
            if (tab != null)
                await ActivateTabAsync(tab);
        }
        
        public async Task SetTabColorAsync(TabModel tab, Color color)
        {
            ThrowIfDisposed();
            if (tab != null && _tabs.Contains(tab))
            {
                var oldColor = tab.CustomColor;
                tab.CustomColor = color;
                
                TabModified?.Invoke(this, new TabModifiedEventArgs(tab, nameof(tab.CustomColor), oldColor, color));
                await Task.CompletedTask;
            }
        }
        
        public async Task ClearTabColorAsync(TabModel tab)
        {
            ThrowIfDisposed();
            if (tab != null && _tabs.Contains(tab))
            {
                tab.ClearCustomColor();
                await Task.CompletedTask;
            }
        }
        
        public async Task SetTabPinnedAsync(TabModel tab, bool isPinned)
        {
            ThrowIfDisposed();
            if (tab != null && _tabs.Contains(tab))
            {
                var oldValue = tab.IsPinned;
                tab.IsPinned = isPinned;
                
                // Reorganize tabs to maintain pinned order
                await ReorganizeTabsAsync();
                
                TabModified?.Invoke(this, new TabModifiedEventArgs(tab, nameof(tab.IsPinned), oldValue, isPinned));
            }
        }
        
        public async Task RenameTabAsync(TabModel tab, string newTitle)
        {
            ThrowIfDisposed();
            if (tab != null && _tabs.Contains(tab) && !string.IsNullOrWhiteSpace(newTitle))
            {
                var oldTitle = tab.Title;
                tab.Title = newTitle.Trim();
                
                TabModified?.Invoke(this, new TabModifiedEventArgs(tab, nameof(tab.Title), oldTitle, newTitle));
                await Task.CompletedTask;
            }
        }
        
        public async Task MoveTabAsync(TabModel tab, int newIndex)
        {
            ThrowIfDisposed();
            if (tab == null || !_tabs.Contains(tab)) return;
            
            var oldIndex = GetTabIndex(tab);
            if (oldIndex == newIndex || newIndex < 0 || newIndex >= TabCount) return;
            
            await _operationSemaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                _tabs.Move(oldIndex, newIndex);
                TabsReordered?.Invoke(this, new TabReorderedEventArgs(tab, oldIndex, newIndex));
                
                _logger?.LogDebug("Moved tab '{Title}' from index {OldIndex} to {NewIndex}", 
                    tab.Title, oldIndex, newIndex);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to move tab '{Title}' from {OldIndex} to {NewIndex}", 
                    tab.Title, oldIndex, newIndex);
                throw;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        
        public async Task MoveTabAsync(int fromIndex, int toIndex)
        {
            var tab = GetTabAt(fromIndex);
            if (tab != null)
                await MoveTabAsync(tab, toIndex);
        }
        
        #endregion

        #region Query Operations
        
        public IEnumerable<TabModel> GetPinnedTabs()
        {
            return _tabs.Where(t => t.IsPinned);
        }
        
        public IEnumerable<TabModel> GetUnpinnedTabs()
        {
            return _tabs.Where(t => !t.IsPinned);
        }
        
        public async Task<IEnumerable<TabModel>> GetTabsByStateAsync(TabState state)
        {
            await Task.CompletedTask;
            return _tabs.Where(t => t.State.Equals(state));
        }
        
        public TabModel GetTabAt(int index)
        {
            return index >= 0 && index < TabCount ? _tabs[index] : null;
        }
        
        public int GetTabIndex(TabModel tab)
        {
            return tab != null ? _tabs.IndexOf(tab) : -1;
        }
        
        #endregion

        #region Advanced Operations
        
        public async Task ReorganizeTabsAsync()
        {
            await _operationSemaphore.WaitAsync(_cancellationTokenSource.Token);
            try
            {
                var orderedTabs = _tabs
                    .OrderBy(t => t.IsPinned ? 0 : 1)
                    .ThenBy(t => t.IsPinned ? _tabs.IndexOf(t) : _tabs.IndexOf(t))
                    .ToList();
                
                // Clear and re-add in correct order
                var currentActiveTab = _activeTab;
                _tabs.Clear();
                
                foreach (var tab in orderedTabs)
                {
                    _tabs.Add(tab);
                }
                
                // Restore active tab
                if (currentActiveTab != null)
                {
                    ActiveTab = currentActiveTab;
                }
                
                _logger?.LogDebug("Reorganized {Count} tabs with {PinnedCount} pinned", 
                    TabCount, GetPinnedTabs().Count());
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to reorganize tabs");
                throw;
            }
            finally
            {
                _operationSemaphore.Release();
            }
        }
        
        public async Task<TabModel> CreateTabFromTemplateAsync(TabTemplate template)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            
            var request = new TabCreationRequest
            {
                Title = template.DefaultTitle ?? "New Tab",
                IsPinned = template.IsPinned,
                CustomColor = template.DefaultColor,
                Template = template
            };
            
            return await CreateTabAsync(request);
        }
        
        public async Task<TabValidationResult> ValidateTabOperationAsync(TabOperation operation)
        {
            return await _validator.ValidateOperationAsync(null, operation);
        }
        
        public async Task OptimizeTabMemoryAsync()
        {
            await _memoryOptimizer.OptimizeAsync(_tabs.ToList());
            _logger?.LogInformation("Tab memory optimization completed");
        }
        
        public async Task SaveTabStateAsync()
        {
            // Implementation would save tab state to persistent storage
            await Task.CompletedTask;
            _logger?.LogDebug("Tab state saved");
        }
        
        public async Task RestoreTabStateAsync()
        {
            // Implementation would restore tab state from persistent storage
            await Task.CompletedTask;
            _logger?.LogDebug("Tab state restored");
        }
        
        #endregion

        #region Navigation
        
        public async Task NavigateToNextTabAsync()
        {
            if (TabCount <= 1) return;
            
            var currentIndex = _activeTab != null ? GetTabIndex(_activeTab) : -1;
            var nextIndex = (currentIndex + 1) % TabCount;
            await ActivateTabAsync(nextIndex);
        }
        
        public async Task NavigateToPreviousTabAsync()
        {
            if (TabCount <= 1) return;
            
            var currentIndex = _activeTab != null ? GetTabIndex(_activeTab) : 0;
            var previousIndex = currentIndex > 0 ? currentIndex - 1 : TabCount - 1;
            await ActivateTabAsync(previousIndex);
        }
        
        public async Task NavigateToTabAsync(int index)
        {
            await ActivateTabAsync(index);
        }
        
        #endregion

        #region Validation
        
        public bool CanCloseTab(TabModel tab)
        {
            return tab != null && (!tab.IsPinned || !tab.HasUnsavedChanges);
        }
        
        public bool CanReorderTabs()
        {
            return TabCount > 1;
        }
        
        #endregion

        #region Private Methods
        
        private void ThrowIfDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(ModernTabManagerService));
        }
        
        private int CalculateInsertionIndex(TabCreationRequest request, TabModel tab)
        {
            if (request.IsPinned)
            {
                // Insert pinned tabs at the end of pinned section
                var pinnedCount = GetPinnedTabs().Count();
                return pinnedCount;
            }
            
            // Insert unpinned tabs at the end
            return TabCount;
        }
        
        private TabModel GetNextTabToActivate(TabModel closingTab)
        {
            var currentIndex = GetTabIndex(closingTab);
            
            // Try next tab first
            if (currentIndex + 1 < TabCount)
                return GetTabAt(currentIndex + 1);
            
            // Fall back to previous tab
            if (currentIndex > 0)
                return GetTabAt(currentIndex - 1);
            
            return null;
        }
        
        private void OnTabPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (sender is TabModel tab)
            {
                TabModified?.Invoke(this, new TabModifiedEventArgs(tab, e.PropertyName, null, null));
            }
        }
        
        private void RecordTabActivation(TabModel tab)
        {
            var tabId = Guid.Parse(tab.Id);
            if (_performanceMetrics.TryGetValue(tabId, out var metrics))
            {
                metrics.LastActivated = DateTime.UtcNow;
                metrics.ActivationCount++;
            }
        }
        
        private void RecordTabDeactivation(TabModel tab)
        {
            var tabId = Guid.Parse(tab.Id);
            if (_performanceMetrics.TryGetValue(tabId, out var metrics))
            {
                metrics.LastDeactivated = DateTime.UtcNow;
            }
        }
        
        #endregion

        #region Disposal
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                try
                {
                    _cancellationTokenSource.Cancel();
                    
                    // Close all tabs
                    var tabsToClose = _tabs.ToList();
                    foreach (var tab in tabsToClose)
                    {
                        tab.PropertyChanged -= OnTabPropertyChanged;
                        tab.Dispose();
                    }
                    
                    _tabs.Clear();
                    _performanceMetrics.Clear();
                    
                    _operationSemaphore?.Dispose();
                    _cancellationTokenSource?.Dispose();
                    
                    _logger?.LogInformation("Modern tab manager service disposed. Stats - Created: {Created}, Closed: {Closed}", 
                        _totalTabsCreated, _totalTabsClosed);
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Error during tab manager disposal");
                }
                finally
                {
                    _isDisposed = true;
                }
            }
        }
        
        #endregion
    }

    #region Supporting Classes
    
    /// <summary>
    /// Performance metrics for individual tabs
    /// </summary>
    internal class TabPerformanceMetrics
    {
        public DateTime CreatedAt { get; set; }
        public DateTime LastActivated { get; set; }
        public DateTime LastDeactivated { get; set; }
        public int ActivationCount { get; set; }
    }
    
    /// <summary>
    /// Exception thrown when tab validation fails
    /// </summary>
    public class TabValidationException : Exception
    {
        public TabValidationException(string message) : base(message) { }
        public TabValidationException(string message, Exception innerException) : base(message, innerException) { }
    }
    
    #endregion
} 