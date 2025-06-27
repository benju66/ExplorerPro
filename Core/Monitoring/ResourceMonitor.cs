using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ExplorerPro.Core.Monitoring
{
    /// <summary>
    /// Monitors application resource usage and provides memory pressure detection
    /// Phase 5: Resource Bounds - History and Collection Limits
    /// </summary>
    public class ResourceMonitor : IDisposable
    {
        private readonly Timer _monitorTimer;
        private readonly Process _currentProcess;
        private long _lastWorkingSet;
        private long _lastGCMemory;
        private int _lastGen2Collections;
        private bool _disposed;

        public ResourceMonitor(TimeSpan? monitorInterval = null)
        {
            _currentProcess = Process.GetCurrentProcess();
            var interval = monitorInterval ?? TimeSpan.FromSeconds(10);
            _monitorTimer = new Timer(MonitorCallback, null, interval, interval);
            
            // Initialize baseline values
            _currentProcess.Refresh();
            _lastWorkingSet = _currentProcess.WorkingSet64;
            _lastGCMemory = GC.GetTotalMemory(false);
            _lastGen2Collections = GC.CollectionCount(2);
        }

        public event EventHandler<ResourceUsageEventArgs> ResourceUsageUpdated;
        public event EventHandler<MemoryPressureEventArgs> HighMemoryPressure;
        public event EventHandler<GarbageCollectionEventArgs> FrequentGarbageCollection;

        /// <summary>
        /// Manually trigger resource monitoring
        /// </summary>
        public void UpdateResourceUsage()
        {
            MonitorCallback(null);
        }

        private void MonitorCallback(object state)
        {
            if (_disposed) return;

            try
            {
                _currentProcess.Refresh();
                
                var workingSet = _currentProcess.WorkingSet64;
                var gcMemory = GC.GetTotalMemory(false);
                var gen2Count = GC.CollectionCount(2);

                var usage = new ResourceUsageEventArgs
                {
                    WorkingSetBytes = workingSet,
                    ManagedMemoryBytes = gcMemory,
                    Gen0CollectionCount = GC.CollectionCount(0),
                    Gen1CollectionCount = GC.CollectionCount(1),
                    Gen2CollectionCount = gen2Count,
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount,
                    PrivateMemoryBytes = _currentProcess.PrivateMemorySize64,
                    VirtualMemoryBytes = _currentProcess.VirtualMemorySize64
                };

                ResourceUsageUpdated?.Invoke(this, usage);

                // Check for memory pressure
                CheckMemoryPressure(workingSet, gcMemory);
                
                // Check for frequent garbage collection
                CheckGarbageCollectionFrequency(gen2Count);

                // Update last values
                _lastWorkingSet = workingSet;
                _lastGCMemory = gcMemory;
                _lastGen2Collections = gen2Count;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Resource monitoring error: {ex.Message}");
            }
        }

        private void CheckMemoryPressure(long workingSet, long gcMemory)
        {
            var workingSetMB = workingSet / (1024 * 1024);
            var gcMemoryMB = gcMemory / (1024 * 1024);
            
            // High memory pressure conditions:
            // 1. Working set > 500MB and growing by more than 20%
            // 2. Managed memory > 250MB and growing by more than 50%
            // 3. Working set > 1GB regardless of growth
            
            bool highPressure = false;
            double workingSetGrowth = 0;
            double gcMemoryGrowth = 0;
            
            if (_lastWorkingSet > 0)
            {
                workingSetGrowth = (double)workingSet / _lastWorkingSet;
                gcMemoryGrowth = (double)gcMemory / Math.Max(1, _lastGCMemory);
            }
            
            if (workingSetMB > 1024)
            {
                highPressure = true;
            }
            else if (workingSetMB > 500 && workingSetGrowth > 1.2)
            {
                highPressure = true;
            }
            else if (gcMemoryMB > 250 && gcMemoryGrowth > 1.5)
            {
                highPressure = true;
            }
            
            if (highPressure)
            {
                HighMemoryPressure?.Invoke(this, new MemoryPressureEventArgs
                {
                    CurrentWorkingSetMB = workingSetMB,
                    CurrentManagedMemoryMB = gcMemoryMB,
                    WorkingSetGrowthRate = workingSetGrowth,
                    ManagedMemoryGrowthRate = gcMemoryGrowth,
                    Recommendation = GetMemoryRecommendation(workingSetMB, gcMemoryMB, workingSetGrowth)
                });
            }
        }

        private void CheckGarbageCollectionFrequency(int currentGen2Count)
        {
            var gen2Increase = currentGen2Count - _lastGen2Collections;
            
            // If we've had more than 5 Gen2 collections in the monitoring interval, that's frequent
            if (gen2Increase > 5)
            {
                FrequentGarbageCollection?.Invoke(this, new GarbageCollectionEventArgs
                {
                    Gen2CollectionsInInterval = gen2Increase,
                    TotalGen2Collections = currentGen2Count,
                    Recommendation = "Consider reducing object allocations or increasing collection sizes"
                });
            }
        }

        private string GetMemoryRecommendation(long workingSetMB, long gcMemoryMB, double growth)
        {
            if (workingSetMB > 1024)
                return "Critical: Application using over 1GB memory. Consider restarting.";
            if (growth > 1.5)
                return "Warning: Rapid memory growth detected. Check for memory leaks.";
            if (gcMemoryMB > 500)
                return "Info: High managed memory usage. Consider triggering garbage collection.";
            
            return "Monitor: Memory usage elevated but within acceptable limits.";
        }

        public void ForceGarbageCollection()
        {
            try
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                Debug.WriteLine("Forced garbage collection completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error during forced garbage collection: {ex.Message}");
            }
        }

        public ResourceSnapshot GetCurrentSnapshot()
        {
            if (_disposed) 
                throw new ObjectDisposedException(nameof(ResourceMonitor));

            try
            {
                _currentProcess.Refresh();
                
                return new ResourceSnapshot
                {
                    Timestamp = DateTime.Now,
                    WorkingSetMB = _currentProcess.WorkingSet64 / (1024 * 1024),
                    PrivateMemoryMB = _currentProcess.PrivateMemorySize64 / (1024 * 1024),
                    VirtualMemoryMB = _currentProcess.VirtualMemorySize64 / (1024 * 1024),
                    ManagedMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024),
                    ThreadCount = _currentProcess.Threads.Count,
                    HandleCount = _currentProcess.HandleCount,
                    Gen0Collections = GC.CollectionCount(0),
                    Gen1Collections = GC.CollectionCount(1),
                    Gen2Collections = GC.CollectionCount(2),
                    ProcessorTimeMs = _currentProcess.TotalProcessorTime.TotalMilliseconds
                };
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating resource snapshot: {ex.Message}");
                return new ResourceSnapshot
                {
                    Timestamp = DateTime.Now,
                    WorkingSetMB = -1 // Indicates error
                };
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;
            
            if (disposing)
            {
                _monitorTimer?.Dispose();
                _currentProcess?.Dispose();
            }
            
            _disposed = true;
        }
    }

    public class ResourceUsageEventArgs : EventArgs
    {
        public long WorkingSetBytes { get; set; }
        public long PrivateMemoryBytes { get; set; }
        public long VirtualMemoryBytes { get; set; }
        public long ManagedMemoryBytes { get; set; }
        public int Gen0CollectionCount { get; set; }
        public int Gen1CollectionCount { get; set; }
        public int Gen2CollectionCount { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        
        public long WorkingSetMB => WorkingSetBytes / (1024 * 1024);
        public long ManagedMemoryMB => ManagedMemoryBytes / (1024 * 1024);
    }

    public class MemoryPressureEventArgs : EventArgs
    {
        public long CurrentWorkingSetMB { get; set; }
        public long CurrentManagedMemoryMB { get; set; }
        public double WorkingSetGrowthRate { get; set; }
        public double ManagedMemoryGrowthRate { get; set; }
        public string Recommendation { get; set; } = "";
    }

    public class GarbageCollectionEventArgs : EventArgs
    {
        public int Gen2CollectionsInInterval { get; set; }
        public int TotalGen2Collections { get; set; }
        public string Recommendation { get; set; } = "";
    }

    public class ResourceSnapshot
    {
        public DateTime Timestamp { get; set; }
        public long WorkingSetMB { get; set; }
        public long PrivateMemoryMB { get; set; }
        public long VirtualMemoryMB { get; set; }
        public long ManagedMemoryMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public double ProcessorTimeMs { get; set; }
        
        public bool IsValid => WorkingSetMB >= 0;
        
        // Performance indicators
        public PerformanceLevel MemoryPerformance => 
            WorkingSetMB > 1000 ? PerformanceLevel.Poor :
            WorkingSetMB > 500 ? PerformanceLevel.Warning :
            PerformanceLevel.Good;
            
        public PerformanceLevel ThreadPerformance =>
            ThreadCount > 50 ? PerformanceLevel.Poor :
            ThreadCount > 25 ? PerformanceLevel.Warning :
            PerformanceLevel.Good;
        
        public override string ToString()
        {
            return $"Memory: {WorkingSetMB}MB working set, {ManagedMemoryMB}MB managed, " +
                   $"Threads: {ThreadCount}, Handles: {HandleCount}, " +
                   $"GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}";
        }
    }
    
    public enum PerformanceLevel
    {
        Good,
        Warning,
        Poor,
        Critical
    }
} 