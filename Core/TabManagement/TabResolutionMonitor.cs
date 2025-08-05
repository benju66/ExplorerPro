using System;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.Monitoring;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Monitors TabModelResolver health and provides real-time metrics
    /// </summary>
    public class TabResolutionMonitor : IDisposable
    {
        private readonly ILogger<TabResolutionMonitor> _logger;
        private readonly ITelemetryService _telemetryService;
        private readonly Timer _monitoringTimer;
        private readonly TimeSpan _monitoringInterval;
        
        private TabResolutionStats _lastStats;
        private DateTime _startTime;
        private bool _disposed;
        
        // Alert thresholds
        private const double TagFallbackRateWarning = 20.0;
        private const double TagFallbackRateCritical = 50.0;
        private const int NotFoundWarningThreshold = 10;
        
        public TabResolutionMonitor(
            ILogger<TabResolutionMonitor> logger,
            ITelemetryService telemetryService,
            TimeSpan? monitoringInterval = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
            _monitoringInterval = monitoringInterval ?? TimeSpan.FromMinutes(5);
            _startTime = DateTime.UtcNow;
            
            _lastStats = TabModelResolver.GetStats();
            
            // Start monitoring timer
            _monitoringTimer = new Timer(MonitoringCallback, null, _monitoringInterval, _monitoringInterval);
            
            _logger.LogInformation("TabResolutionMonitor started with interval: {Interval}", _monitoringInterval);
        }
        
        private void MonitoringCallback(object state)
        {
            try
            {
                var currentStats = TabModelResolver.GetStats();
                var report = GenerateHealthReport(currentStats);
                
                // Log the report
                _logger.LogInformation("TabResolver Health Report:\n{Report}", report);
                
                // Check for alerts
                CheckAlerts(currentStats);
                
                // Send telemetry
                SendTelemetry(currentStats);
                
                _lastStats = currentStats;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TabResolutionMonitor callback");
            }
        }
        
        private string GenerateHealthReport(TabResolutionStats stats)
        {
            var sb = new StringBuilder();
            var uptime = DateTime.UtcNow - _startTime;
            
            sb.AppendLine("=== TabModel Resolution Health Report ===");
            sb.AppendLine($"Uptime: {uptime:d\\.hh\\:mm\\:ss}");
            sb.AppendLine($"Report Time: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            sb.AppendLine();
            
            sb.AppendLine("Current Statistics:");
            sb.AppendLine($"  DataContext Hits: {stats.DataContextHits:N0}");
            sb.AppendLine($"  Tag Fallbacks: {stats.TagFallbacks:N0}");
            sb.AppendLine($"  Not Found: {stats.NotFound:N0}");
            sb.AppendLine($"  Migrations: {stats.Migrations:N0}");
            sb.AppendLine();
            
            sb.AppendLine("Rates:");
            sb.AppendLine($"  Tag Fallback Rate: {stats.TagFallbackRate:F2}% " + GetHealthIndicator(stats.TagFallbackRate));
            
            var totalResolutions = stats.DataContextHits + stats.TagFallbacks + stats.NotFound;
            if (totalResolutions > 0)
            {
                var notFoundRate = (double)stats.NotFound / totalResolutions * 100;
                sb.AppendLine($"  Not Found Rate: {notFoundRate:F2}%");
                
                var migrationRate = stats.TagFallbacks > 0 ? (double)stats.Migrations / stats.TagFallbacks * 100 : 0;
                sb.AppendLine($"  Migration Success Rate: {migrationRate:F2}%");
            }
            
            sb.AppendLine();
            sb.AppendLine("Changes Since Last Report:");
            sb.AppendLine($"  New DataContext Hits: +{stats.DataContextHits - _lastStats.DataContextHits:N0}");
            sb.AppendLine($"  New Tag Fallbacks: +{stats.TagFallbacks - _lastStats.TagFallbacks:N0}");
            sb.AppendLine($"  New Migrations: +{stats.Migrations - _lastStats.Migrations:N0}");
            
            return sb.ToString();
        }
        
        private string GetHealthIndicator(double tagFallbackRate)
        {
            if (tagFallbackRate < 5) return "✅ Excellent";
            if (tagFallbackRate < 10) return "✅ Good";
            if (tagFallbackRate < TagFallbackRateWarning) return "⚠️ Warning";
            if (tagFallbackRate < TagFallbackRateCritical) return "⚠️ High";
            return "❌ Critical";
        }
        
        private void CheckAlerts(TabResolutionStats stats)
        {
            // Check tag fallback rate
            if (stats.TagFallbackRate > TagFallbackRateCritical)
            {
                _logger.LogError(
                    "CRITICAL: Tag fallback rate is {Rate:F2}% (threshold: {Threshold}%)",
                    stats.TagFallbackRate,
                    TagFallbackRateCritical
                );
                
                _telemetryService.TrackEvent("TabResolver.Alert.Critical", new System.Collections.Generic.Dictionary<string, object>
                {
                    ["Type"] = "TagFallbackRate",
                    ["Value"] = stats.TagFallbackRate,
                    ["Threshold"] = TagFallbackRateCritical
                });
            }
            else if (stats.TagFallbackRate > TagFallbackRateWarning)
            {
                _logger.LogWarning(
                    "WARNING: Tag fallback rate is {Rate:F2}% (threshold: {Threshold}%)",
                    stats.TagFallbackRate,
                    TagFallbackRateWarning
                );
            }
            
            // Check not found count
            if (stats.NotFound > NotFoundWarningThreshold)
            {
                _logger.LogWarning(
                    "WARNING: {Count} TabModel resolutions failed (not found)",
                    stats.NotFound
                );
            }
            
            // Check if migrations are failing
            if (stats.TagFallbacks > 0 && stats.Migrations == 0)
            {
                _logger.LogError("CRITICAL: Tab migrations are not occurring despite tag fallbacks");
            }
        }
        
        private void SendTelemetry(TabResolutionStats stats)
        {
            _telemetryService.TrackEvent("TabResolver.HealthReport", new System.Collections.Generic.Dictionary<string, object>
            {
                ["DataContextHits"] = stats.DataContextHits,
                ["TagFallbacks"] = stats.TagFallbacks,
                ["NotFound"] = stats.NotFound,
                ["Migrations"] = stats.Migrations,
                ["TagFallbackRate"] = stats.TagFallbackRate,
                ["ReportTime"] = DateTime.UtcNow
            });
        }
        
        /// <summary>
        /// Gets current health status as a simple enum
        /// </summary>
        public HealthStatus GetHealthStatus()
        {
            var stats = TabModelResolver.GetStats();
            
            if (stats.TagFallbackRate > TagFallbackRateCritical)
                return HealthStatus.Critical;
            
            if (stats.TagFallbackRate > TagFallbackRateWarning || stats.NotFound > NotFoundWarningThreshold)
                return HealthStatus.Warning;
            
            return HealthStatus.Healthy;
        }
        
        /// <summary>
        /// Forces an immediate health check
        /// </summary>
        public void ForceHealthCheck()
        {
            _logger.LogInformation("Forcing immediate health check");
            MonitoringCallback(null);
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _monitoringTimer?.Dispose();
                _disposed = true;
                
                // Log final statistics
                var finalStats = TabModelResolver.GetStats();
                _logger.LogInformation(
                    "TabResolutionMonitor stopped. Final stats - DataContext: {DC}, Tag: {TF}, Migrations: {M}",
                    finalStats.DataContextHits,
                    finalStats.TagFallbacks,
                    finalStats.Migrations
                );
            }
        }
    }
    
    public enum HealthStatus
    {
        Healthy,
        Warning,
        Critical
    }
}