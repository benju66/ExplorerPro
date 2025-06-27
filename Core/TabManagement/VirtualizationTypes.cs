using System;
using ExplorerPro.Models;

namespace ExplorerPro.Core.TabManagement
{
    #region Virtualization Settings
    
    public class VirtualizationSettings
    {
        public int MaxVisibleTabs { get; set; } = 20;
        public int BufferTabs { get; set; } = 5;
        public TimeSpan HibernationDelay { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromMinutes(5);
        
        public static VirtualizationSettings Default => new VirtualizationSettings();
    }
    
    #endregion

    #region Virtualized Tab Data
    
    public class VirtualizedTab : IDisposable
    {
        public TabModel Tab { get; }
        public bool IsVisible { get; set; }
        public bool IsHibernated { get; set; }
        public TabVirtualizationPriority Priority { get; set; }
        public DateTime LastAccessed { get; set; }
        public int AccessCount { get; set; }
        public long HibernatedMemorySize { get; set; }
        
        public VirtualizedTab(TabModel tab)
        {
            Tab = tab ?? throw new ArgumentNullException(nameof(tab));
            LastAccessed = DateTime.UtcNow;
            Priority = TabVirtualizationPriority.Medium;
        }
        
        public void Dispose()
        {
            // Cleanup resources if needed
        }
    }
    
    #endregion

    #region Performance Data
    
    public class TabPerformanceData
    {
        public TimeSpan LastActivationTime { get; set; }
        public int TotalActivations { get; set; }
        public DateTime FirstAccess { get; set; } = DateTime.UtcNow;
    }
    
    #endregion

    #region Enumerations
    
    public enum TabVirtualizationPriority
    {
        Low = 0,
        Medium = 1,
        High = 2,
        Critical = 3
    }
    
    #endregion

    #region Event Args
    
    public class TabHibernationEventArgs : EventArgs
    {
        public string TabId { get; set; }
        public long MemoryFreed { get; set; }
        public DateTime HibernatedAt { get; set; }
        public HibernationReason Reason { get; set; }
        public TimeSpan HibernationTime { get; set; }
    }
    
    public class TabReactivationEventArgs : EventArgs
    {
        public string TabId { get; set; }
        public TimeSpan ReactivationTime { get; set; }
        public long MemoryRestored { get; set; }
        public TimeSpan WasHibernatedFor { get; set; }
    }
    
    public class VirtualizationStatsEventArgs : EventArgs
    {
        public int TotalTabs { get; set; }
        public int VisibleTabs { get; set; }
        public int HibernatedTabs { get; set; }
        public long MemorySavedMB { get; set; }
        public bool IsVirtualizationActive { get; set; }
    }
    
    #endregion

    #region Hibernation Types
    
    public class HibernationSettings
    {
        public TimeSpan StandardHibernationThreshold { get; set; } = TimeSpan.FromMinutes(30);
        public TimeSpan AggressiveHibernationThreshold { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan HibernationInterval { get; set; } = TimeSpan.FromMinutes(5);
        public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(1);
        public TimeSpan HibernationDelay { get; set; } = TimeSpan.FromMinutes(2);
        public TimeSpan DataRetentionPeriod { get; set; } = TimeSpan.FromDays(7);
        public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(45);
        public bool AllowPinnedHibernation { get; set; } = false;
        public bool AllowUnsavedHibernation { get; set; } = false;
        public bool AggressiveHibernation { get; set; } = false;
        public bool EnableProactiveHibernation { get; set; } = true;
        public long MaxMemoryUsage { get; set; } = 1024 * 1024 * 1024; // 1GB
        public int MaxHibernationsPerCycle { get; set; } = 3;
        
        public static HibernationSettings Default => new HibernationSettings();
    }
    
    public class HibernatedTabData
    {
        public string TabId { get; set; }
        public string OriginalTitle { get; set; }
        public string OriginalPath { get; set; }
        public System.Windows.Media.Color CustomColor { get; set; }
        public bool IsPinned { get; set; }
        public object Metadata { get; set; }
        public DateTime HibernatedAt { get; set; }
        public HibernationReason Reason { get; set; }
        public TabMemoryProfile MemoryProfile { get; set; }
        public PreservationLevel PreservationLevel { get; set; }
        public object ExtendedState { get; set; }
    }
    
    public class TabMemoryProfile
    {
        public string TabId { get; set; }
        public long EstimatedSize { get; set; }
        public ContentType ContentType { get; set; }
        public bool HasLargeContent { get; set; }
        public DateTime ProfiledAt { get; set; }
    }
    
    public class HibernationCandidate
    {
        public string TabId { get; set; }
        public TabModel Tab { get; set; }
        public DateTime QueuedAt { get; set; }
        public HibernationReason Reason { get; set; }
        public int Priority { get; set; }
    }
    
    public class HibernationAnalysis
    {
        public string TabId { get; set; }
        public bool CanHibernate { get; set; }
        public HibernationAction RecommendedAction { get; set; }
        public long EstimatedMemorySavings { get; set; }
        public int Priority { get; set; }
        public TimeSpan TimeSinceLastAccess { get; set; }
    }
    
    public class HibernationStats
    {
        public int TotalHibernated { get; set; }
        public int CurrentlyHibernated { get; set; }
        public long TotalMemorySavedMB { get; set; }
        public TimeSpan AverageHibernationDuration { get; set; }
        public bool IsMemoryPressureActive { get; set; }
    }
    
    public class HibernationStatsEventArgs : EventArgs
    {
        public HibernationStats Stats { get; set; }
    }
    
    public enum HibernationReason
    {
        Automatic,
        Manual,
        MemoryPressure,
        Scheduled,
        ApplicationShutdown
    }
    
    public enum HibernationAction
    {
        None,
        Monitor,
        Scheduled,
        Immediate
    }
    
    public enum PreservationLevel
    {
        Basic,
        Extended,
        Full
    }
    
    public enum ContentType
    {
        Unknown,
        FileSystem,
        Document,
        Image,
        Web,
        Application
    }
    
    #endregion
} 