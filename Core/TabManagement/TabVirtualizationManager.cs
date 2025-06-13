using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Manages tab virtualization and memory optimization
    /// </summary>
    public class TabVirtualizationManager
    {
        private readonly ILogger<TabVirtualizationManager> _logger;
        private readonly TabStateManager _stateManager;
        private readonly int _maxActiveTabs;
        private readonly TimeSpan _hibernationDelay;
        private readonly Dictionary<string, DateTime> _lastAccessTimes;
        private readonly object _lock = new object();
        private CancellationTokenSource? _hibernationCts;

        public TabVirtualizationManager(
            ILogger<TabVirtualizationManager> logger,
            TabStateManager stateManager,
            int maxActiveTabs = 10,
            TimeSpan? hibernationDelay = null)
        {
            _logger = logger;
            _stateManager = stateManager;
            _maxActiveTabs = maxActiveTabs;
            _hibernationDelay = hibernationDelay ?? TimeSpan.FromMinutes(30);
            _lastAccessTimes = new Dictionary<string, DateTime>();
        }

        /// <summary>
        /// Start the virtualization manager
        /// </summary>
        public void Start()
        {
            _hibernationCts = new CancellationTokenSource();
            Task.Run(() => HibernationLoop(_hibernationCts.Token));
        }

        /// <summary>
        /// Stop the virtualization manager
        /// </summary>
        public void Stop()
        {
            _hibernationCts?.Cancel();
            _hibernationCts?.Dispose();
            _hibernationCts = null;
        }

        /// <summary>
        /// Register a tab access
        /// </summary>
        public void RegisterTabAccess(string tabId)
        {
            lock (_lock)
            {
                _lastAccessTimes[tabId] = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Get tabs that should be hibernated
        /// </summary>
        private List<string> GetTabsToHibernate()
        {
            var now = DateTime.UtcNow;
            var activeTabs = new List<(string TabId, DateTime LastAccess)>();

            lock (_lock)
            {
                foreach (var kvp in _lastAccessTimes)
                {
                    if (now - kvp.Value > _hibernationDelay)
                    {
                        activeTabs.Add((kvp.Key, kvp.Value));
                    }
                }
            }

            // Sort by last access time and take the oldest ones
            return activeTabs
                .OrderBy(x => x.LastAccess)
                .Take(Math.Max(0, activeTabs.Count - _maxActiveTabs))
                .Select(x => x.TabId)
                .ToList();
        }

        /// <summary>
        /// Main hibernation loop
        /// </summary>
        private async Task HibernationLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    var tabsToHibernate = GetTabsToHibernate();
                    foreach (var tabId in tabsToHibernate)
                    {
                        var state = _stateManager.GetTabState(tabId);
                        if (state != null && !state.IsHibernated)
                        {
                            state.IsHibernated = true;
                            _stateManager.SaveTabState(tabId, state);
                            _logger.LogInformation("Hibernated tab {TabId}", tabId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in hibernation loop");
                }

                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
            }
        }

        /// <summary>
        /// Get memory usage statistics
        /// </summary>
        public TabMemoryStats GetMemoryStats()
        {
            lock (_lock)
            {
                return new TabMemoryStats
                {
                    TotalTabs = _lastAccessTimes.Count,
                    ActiveTabs = _lastAccessTimes.Count(x => DateTime.UtcNow - x.Value <= _hibernationDelay),
                    HibernatedTabs = _lastAccessTimes.Count(x => DateTime.UtcNow - x.Value > _hibernationDelay)
                };
            }
        }
    }

    /// <summary>
    /// Memory usage statistics for tabs
    /// </summary>
    public class TabMemoryStats
    {
        public int TotalTabs { get; set; }
        public int ActiveTabs { get; set; }
        public int HibernatedTabs { get; set; }
    }
} 