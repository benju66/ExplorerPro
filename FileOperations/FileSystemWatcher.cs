using System;
using System.IO;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.FileOperations
{
    /// <summary>
    /// Monitors a directory for file system changes.
    /// </summary>
    public class FileSystemWatcherService : IDisposable
    {
        #region Fields

        private readonly ILogger<FileSystemWatcherService> _logger;
        private FileSystemWatcher _watcher;
        private string _currentPath;
        private readonly ConcurrentQueue<FileSystemEventArgs> _eventQueue;
        private readonly Timer _processingTimer;
        private readonly SemaphoreSlim _processingLock = new SemaphoreSlim(1, 1);
        private bool _isDisposed;

        #endregion

        #region Events

        /// <summary>
        /// Raised when a file or directory is created.
        /// </summary>
        public event EventHandler<FileSystemEventArgs> ItemCreated;

        /// <summary>
        /// Raised when a file or directory is deleted.
        /// </summary>
        public event EventHandler<FileSystemEventArgs> ItemDeleted;

        /// <summary>
        /// Raised when a file or directory is changed.
        /// </summary>
        public event EventHandler<FileSystemEventArgs> ItemChanged;

        /// <summary>
        /// Raised when a file or directory is renamed.
        /// </summary>
        public event EventHandler<RenamedEventArgs> ItemRenamed;

        /// <summary>
        /// Raised when a batch of changes has been processed.
        /// </summary>
        public event EventHandler<EventArgs> BatchProcessed;

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the FileSystemWatcherService class.
        /// </summary>
        /// <param name="logger">Optional logger for operation tracking.</param>
        public FileSystemWatcherService(ILogger<FileSystemWatcherService> logger = null)
        {
            _logger = logger;
            _eventQueue = new ConcurrentQueue<FileSystemEventArgs>();
            _processingTimer = new Timer(ProcessEventsAsync, null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts monitoring the specified directory.
        /// </summary>
        /// <param name="path">The directory to monitor.</param>
        /// <param name="includeSubdirectories">Whether to monitor subdirectories.</param>
        /// <returns>True if monitoring started successfully, false otherwise.</returns>
        public bool StartWatching(string path, bool includeSubdirectories = true)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                _logger?.LogError($"Cannot watch invalid or non-existent path: {path}");
                return false;
            }

            try
            {
                StopWatching();

                _currentPath = path;
                _watcher = new FileSystemWatcher(path)
                {
                    IncludeSubdirectories = includeSubdirectories,
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName |
                                  NotifyFilters.LastWrite | NotifyFilters.CreationTime |
                                  NotifyFilters.Size | NotifyFilters.Attributes
                };

                // Set up event handlers
                _watcher.Created += OnFileSystemEvent;
                _watcher.Deleted += OnFileSystemEvent;
                _watcher.Changed += OnFileSystemEvent;
                _watcher.Renamed += OnFileSystemRenamed;
                _watcher.Error += OnWatcherError;

                // Start watching
                _watcher.EnableRaisingEvents = true;
                
                // Start the processing timer (check every 300ms)
                _processingTimer.Change(300, 300);

                _logger?.LogInformation($"Started watching directory: {path}");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, $"Error starting file system watcher for {path}");
                return false;
            }
        }

        /// <summary>
        /// Stops monitoring the current directory.
        /// </summary>
        public void StopWatching()
        {
            if (_watcher != null)
            {
                _watcher.EnableRaisingEvents = false;
                _watcher.Created -= OnFileSystemEvent;
                _watcher.Deleted -= OnFileSystemEvent;
                _watcher.Changed -= OnFileSystemEvent;
                _watcher.Renamed -= OnFileSystemRenamed;
                _watcher.Error -= OnWatcherError;
                _watcher.Dispose();
                _watcher = null;

                // Stop the processing timer
                _processingTimer.Change(Timeout.Infinite, Timeout.Infinite);

                _logger?.LogInformation($"Stopped watching directory: {_currentPath}");
                _currentPath = null;
            }
        }

        /// <summary>
        /// Gets the currently monitored path.
        /// </summary>
        /// <returns>The path being monitored, or null if not monitoring.</returns>
        public string GetCurrentPath()
        {
            return _currentPath;
        }

        #endregion

        #region Event Handlers

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            // Add event to queue for batch processing
            _eventQueue.Enqueue(e);
            _logger?.LogDebug($"Queued file system event: {e.ChangeType} - {e.FullPath}");
        }

        private void OnFileSystemRenamed(object sender, RenamedEventArgs e)
        {
            // Add renamed event to queue
            _eventQueue.Enqueue(e);
            _logger?.LogDebug($"Queued rename event: {e.OldFullPath} -> {e.FullPath}");
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            _logger?.LogError(e.GetException(), "File system watcher error occurred");
        }

        #endregion

        #region Private Methods

        private async void ProcessEventsAsync(object state)
        {
            // Ensure we don't have multiple processing tasks running in parallel
            if (!await _processingLock.WaitAsync(0))
                return;

            try
            {
                bool processed = false;

                // Process up to 50 events at once to avoid overwhelming the UI
                int eventCount = 0;
                while (eventCount < 50 && _eventQueue.TryDequeue(out FileSystemEventArgs e))
                {
                    eventCount++;
                    processed = true;

                    try
                    {
                        await Task.Run(() => ProcessSingleEvent(e));
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, $"Error processing file system event for {e.FullPath}");
                    }
                }

                if (processed)
                {
                    // Notify that a batch has been processed
                    BatchProcessed?.Invoke(this, EventArgs.Empty);
                }
            }
            finally
            {
                _processingLock.Release();
            }
        }

        private void ProcessSingleEvent(FileSystemEventArgs e)
        {
            // Handle different event types
            switch (e.ChangeType)
            {
                case WatcherChangeTypes.Created:
                    ItemCreated?.Invoke(this, e);
                    break;
                case WatcherChangeTypes.Deleted:
                    ItemDeleted?.Invoke(this, e);
                    break;
                case WatcherChangeTypes.Changed:
                    ItemChanged?.Invoke(this, e);
                    break;
                case WatcherChangeTypes.Renamed:
                    if (e is RenamedEventArgs renamedArgs)
                    {
                        ItemRenamed?.Invoke(this, renamedArgs);
                    }
                    break;
            }
        }

        #endregion

        #region IDisposable Implementation

        /// <summary>
        /// Disposes the FileSystemWatcherService.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the FileSystemWatcherService and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    // Dispose managed resources
                    StopWatching();
                    _processingTimer?.Dispose();
                    _processingLock?.Dispose();
                }

                _isDisposed = true;
            }
        }

        #endregion
    }
}