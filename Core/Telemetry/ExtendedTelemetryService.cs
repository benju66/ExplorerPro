using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.Telemetry
{
    /// <summary>
    /// Enhanced console-based telemetry service that supports events, metrics, and timing
    /// </summary>
    public class ExtendedTelemetryService : ConsoleTelemetryService, IExtendedTelemetryService
    {
        private readonly ILogger<ExtendedTelemetryService> _logger;
        private readonly object _lock = new object();
        
        public ExtendedTelemetryService(ILogger<ExtendedTelemetryService> logger = null)
        {
            _logger = logger;
        }

        public void TrackEvent(string eventName, Dictionary<string, object> properties)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return;
                
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var propsString = properties?.Any() == true 
                        ? string.Join(", ", properties.Select(p => $"{p.Key}={p.Value}"))
                        : "No properties";
                    
                    var message = $"[{timestamp}] [TELEMETRY EVENT] {eventName} | {propsString}";
                    
                    Console.WriteLine(message);
                    _logger?.LogInformation("Telemetry Event: {EventName} with properties: {Properties}", 
                        eventName, propsString);
                }
            }
            catch (Exception ex)
            {
                // Don't let telemetry errors break the application
                Console.WriteLine($"Error tracking event '{eventName}': {ex.Message}");
                _logger?.LogError(ex, "Error tracking telemetry event: {EventName}", eventName);
            }
        }

        public void TrackMetric(string metricName, object value)
        {
            if (string.IsNullOrWhiteSpace(metricName))
                return;
                
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var message = $"[{timestamp}] [TELEMETRY METRIC] {metricName} = {value}";
                    
                    Console.WriteLine(message);
                    _logger?.LogInformation("Telemetry Metric: {MetricName} = {Value}", metricName, value);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking metric '{metricName}': {ex.Message}");
                _logger?.LogError(ex, "Error tracking telemetry metric: {MetricName}", metricName);
            }
        }
        
        public void TrackMetrics(Dictionary<string, object> metrics)
        {
            if (metrics?.Any() != true)
                return;
                
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    var metricsString = string.Join(", ", metrics.Select(m => $"{m.Key}={m.Value}"));
                    var message = $"[{timestamp}] [TELEMETRY METRICS] {metricsString}";
                    
                    Console.WriteLine(message);
                    _logger?.LogInformation("Telemetry Metrics: {Metrics}", metricsString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error tracking metrics: {ex.Message}");
                _logger?.LogError(ex, "Error tracking telemetry metrics");
            }
        }
        
        public IDisposable StartTimedEvent(string eventName)
        {
            if (string.IsNullOrWhiteSpace(eventName))
                return new EmptyDisposable();
                
            return new TimedEvent(eventName, this, _logger);
        }
        
        /// <summary>
        /// Override the base TrackException to add timestamps and better formatting
        /// </summary>
        public override void TrackException(Exception ex, string context)
        {
            try
            {
                lock (_lock)
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
                    Console.WriteLine($"[{timestamp}] [TELEMETRY EXCEPTION] Context: {context}");
                    Console.WriteLine($"[{timestamp}] Exception: {ex.Message}");
                    if (!string.IsNullOrWhiteSpace(ex.StackTrace))
                    {
                        Console.WriteLine($"[{timestamp}] Stack trace: {ex.StackTrace}");
                    }
                    
                    _logger?.LogError(ex, "Telemetry Exception in context: {Context}", context);
                }
            }
            catch
            {
                // Fallback to base implementation if enhanced version fails
                base.TrackException(ex, context);
            }
        }
        
        /// <summary>
        /// Flushes any pending telemetry data (for compatibility)
        /// </summary>
        public void Flush()
        {
            // Console output is immediate, so nothing to flush
            _logger?.LogDebug("Telemetry flush requested (no-op for console output)");
        }
    }
    
    /// <summary>
    /// Implements timed event tracking
    /// </summary>
    internal class TimedEvent : IDisposable
    {
        private readonly string _eventName;
        private readonly ExtendedTelemetryService _telemetryService;
        private readonly ILogger _logger;
        private readonly Stopwatch _stopwatch;
        private bool _disposed;
        
        public TimedEvent(string eventName, ExtendedTelemetryService telemetryService, ILogger logger)
        {
            _eventName = eventName;
            _telemetryService = telemetryService;
            _logger = logger;
            _stopwatch = Stopwatch.StartNew();
            
            // Track start
            _telemetryService.TrackEvent($"{eventName}.Started", new Dictionary<string, object>
            {
                ["StartTime"] = DateTime.UtcNow
            });
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _stopwatch.Stop();
                
                // Track completion with duration
                _telemetryService.TrackEvent($"{_eventName}.Completed", new Dictionary<string, object>
                {
                    ["DurationMs"] = _stopwatch.ElapsedMilliseconds,
                    ["EndTime"] = DateTime.UtcNow
                });
                
                _telemetryService.TrackMetric($"{_eventName}.Duration", _stopwatch.ElapsedMilliseconds);
                
                _disposed = true;
            }
        }
    }
    
    /// <summary>
    /// Empty disposable for error cases
    /// </summary>
    internal class EmptyDisposable : IDisposable
    {
        public void Dispose() { }
    }
}