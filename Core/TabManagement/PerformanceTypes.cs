using System;
using System.Collections.Generic;

namespace ExplorerPro.Core.TabManagement
{
    #region Performance Settings
    
    public class PerformanceSettings
    {
        public TimeSpan OptimizationInterval { get; set; } = TimeSpan.FromMinutes(10);
        public long MemoryWarningThresholdMB { get; set; } = 800;
        public long MemoryCriticalThresholdMB { get; set; } = 1200;
        public int ThreadWarningThreshold { get; set; } = 50;
        public int VirtualizationRecommendationThreshold { get; set; } = 50;
        public bool EnableAutoOptimization { get; set; } = true;
        public bool EnableEmergencyOptimization { get; set; } = true;
        
        public static PerformanceSettings Default => new PerformanceSettings();
    }
    
    #endregion

    #region Performance Metrics
    
    public class PerformanceMetrics
    {
        public DateTime LastUpdate { get; set; } = DateTime.UtcNow;
        public DateTime LastOptimization { get; set; }
        public long CurrentMemoryUsageMB { get; set; }
        public int CurrentThreadCount { get; set; }
        public int TotalOptimizations { get; set; }
        public long TotalMemorySaved { get; set; }
        public TimeSpan TotalTimeSaved { get; set; }
        public int EmergencyOptimizations { get; set; }
        public string TabId { get; set; } = string.Empty;
        public TabPerformanceEvent LastEvent { get; set; }
        public DateTime LastEventTime { get; set; }
        public int EventCount { get; set; }
        public TimeSpan TotalActiveTime { get; set; }
        public long MemoryUsage { get; set; }
    }
    
    #endregion

    #region Optimization Types
    
    public class OptimizationOptions
    {
        public bool OptimizeVirtualization { get; set; } = true;
        public bool OptimizeHibernation { get; set; } = true;
        public bool ForceGarbageCollection { get; set; } = false;
        public bool AggressiveMode { get; set; } = false;
        
        public static OptimizationOptions Default => new OptimizationOptions();
    }
    
    public class OptimizationResult
    {
        public OptimizationStatus Status { get; set; }
        public string Service { get; set; } = "Unknown";
        public bool Success { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public long InitialMemoryUsage { get; set; }
        public long FinalMemoryUsage { get; set; }
        public long MemorySaved { get; set; }
        public int InitialHibernatedTabs { get; set; }
        public int TabsHibernated { get; set; }
        public bool VirtualizationOptimized { get; set; }
        public bool HibernationOptimized { get; set; }
        public bool GarbageCollectionForced { get; set; }
        public List<string> Errors { get; set; } = new List<string>();
        public string ErrorMessage { get; set; }
        
        public static OptimizationResult Skipped => new OptimizationResult { Status = OptimizationStatus.Skipped };
        public static OptimizationResult AlreadyInProgress => new OptimizationResult { Status = OptimizationStatus.AlreadyInProgress };
    }
    
    public enum OptimizationStatus
    {
        Pending,
        InProgress,
        Completed,
        Failed,
        Skipped,
        AlreadyInProgress
    }
    
    #endregion

    #region Performance Analysis
    
    public class PerformanceAnalysis
    {
        public DateTime Timestamp { get; set; }
        public long MemoryUsageMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public List<PerformanceRecommendation> Recommendations { get; set; }
        public int OverallScore { get; set; }
    }
    
    public class PerformanceRecommendation
    {
        public RecommendationType Type { get; set; }
        public RecommendationPriority Priority { get; set; }
        public string Description { get; set; }
        public string[] SuggestedActions { get; set; }
    }
    
    public enum RecommendationType
    {
        Memory,
        Virtualization,
        Hibernation,
        Threading,
        UI,
        General
    }
    
    public enum RecommendationPriority
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    #endregion

    #region Event Args
    
    public class OptimizationCompletedEventArgs : EventArgs
    {
        public OptimizationResult Result { get; set; }
    }
    
    public class PerformanceThresholdEventArgs : EventArgs
    {
        public ThresholdType ThresholdType { get; set; }
        public string Threshold { get; set; } = string.Empty;
        public long CurrentValue { get; set; }
        public long ThresholdValue { get; set; }
        public string Recommendation { get; set; }
    }
    
    public enum ThresholdType
    {
        Memory,
        Threads,
        Handles,
        CPU,
        GarbageCollection
    }
    
    #endregion

    public enum TabPerformanceEvent
    {
        Created,
        Activated,
        Deactivated,
        Hibernated,
        Reactivated,
        Optimized,
        Error
    }

    public class PerformanceIntegrationSettings
    {
        // General settings
        public bool EnableOptimization { get; set; } = true;
        public TimeSpan OptimizationInterval { get; set; } = TimeSpan.FromMinutes(10);
        public TimeSpan PerformanceCheckInterval { get; set; } = TimeSpan.FromMinutes(2);
        
        // Virtualization settings
        public bool EnableVirtualization { get; set; } = true;
        public int MaxVisibleTabs { get; set; } = 20;
        public int BufferTabs { get; set; } = 5;
        public int VirtualizationRecommendationThreshold { get; set; } = 50;
        public TimeSpan HibernationDelay { get; set; } = TimeSpan.FromMinutes(30);
        
        // Hibernation settings
        public bool EnableHibernation { get; set; } = true;
        public int HibernationRecommendationThreshold { get; set; } = 30;
        public TimeSpan MaxIdleTime { get; set; } = TimeSpan.FromMinutes(45);
        public bool EnableProactiveHibernation { get; set; } = true;
        public long MaxMemoryUsage { get; set; } = 1024 * 1024 * 1024; // 1GB
        
        // Memory thresholds
        public long MemoryThreshold { get; set; } = 800 * 1024 * 1024; // 800MB
        public bool EnableMemoryProfiling { get; set; } = true;
        
        public static PerformanceIntegrationSettings Default => new PerformanceIntegrationSettings();
    }

    public class PerformanceIntegrationStats
    {
        public int TotalTabsRegistered { get; set; }
        public int TotalTabsOptimized { get; set; }
        public long TotalMemorySaved { get; set; }
        public VirtualizationStats? VirtualizationStats { get; set; }
        public HibernationStats? HibernationStats { get; set; }
        public PerformanceStats? PerformanceStats { get; set; }
    }

    public class VirtualizationStats
    {
        public int TotalTabs { get; set; }
        public int VisibleTabs { get; set; }
        public int HibernatedTabs { get; set; }
        public long MemorySavedBytes { get; set; }
    }

    public class TabPerformanceEventArgs : EventArgs
    {
        public string TabId { get; set; } = string.Empty;
        public PerformanceMetrics Metrics { get; set; } = new PerformanceMetrics();
        public TabPerformanceEvent EventType { get; set; }
    }

    public class PerformanceIntegrationEventArgs : EventArgs
    {
        public List<OptimizationResult> OptimizationResults { get; set; } = new List<OptimizationResult>();
        public TimeSpan TotalTime { get; set; }
        public int TabsAffected { get; set; }
    }

    public class IntegrationStatsEventArgs : EventArgs
    {
        public PerformanceIntegrationStats Stats { get; set; } = new PerformanceIntegrationStats();
        public DateTime Timestamp { get; set; }
    }

    public class PerformanceStats
    {
        public DateTime Timestamp { get; set; }
        public bool IsOptimizationEnabled { get; set; }
        public bool IsVirtualizationEnabled { get; set; }
        public bool IsHibernationEnabled { get; set; }
        public long CurrentMemoryUsageMB { get; set; }
        public int ThreadCount { get; set; }
        public int HandleCount { get; set; }
        public int TotalTabs { get; set; }
        public int VisibleTabs { get; set; }
        public int HibernatedTabs { get; set; }
        public long MemorySavedMB { get; set; }
        public int TotalOptimizations { get; set; }
        public long TotalMemorySavedMB { get; set; }
        public DateTime LastOptimization { get; set; }
    }

    public class PerformanceConfiguration
    {
        public bool EnableOptimization { get; set; } = true;
        public bool EnableVirtualization { get; set; } = true;
        public bool EnableHibernation { get; set; } = true;
        public VirtualizationSettings? VirtualizationSettings { get; set; }
        public HibernationSettings? HibernationSettings { get; set; }
        public PerformanceSettings? PerformanceSettings { get; set; }
    }
} 