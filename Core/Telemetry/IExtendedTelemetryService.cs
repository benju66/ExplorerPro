using System;
using System.Collections.Generic;

namespace ExplorerPro.Core.Telemetry
{
    /// <summary>
    /// Extended telemetry service interface that supports events and metrics tracking
    /// in addition to basic exception tracking
    /// </summary>
    public interface IExtendedTelemetryService : ITelemetryService
    {
        /// <summary>
        /// Tracks a custom event with properties
        /// </summary>
        /// <param name="eventName">Name of the event</param>
        /// <param name="properties">Event properties</param>
        void TrackEvent(string eventName, Dictionary<string, object> properties);
        
        /// <summary>
        /// Tracks a metric value
        /// </summary>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="value">Metric value</param>
        void TrackMetric(string metricName, object value);
        
        /// <summary>
        /// Tracks multiple metrics at once
        /// </summary>
        /// <param name="metrics">Dictionary of metric names and values</param>
        void TrackMetrics(Dictionary<string, object> metrics);
        
        /// <summary>
        /// Starts a timed event for performance tracking
        /// </summary>
        /// <param name="eventName">Name of the timed event</param>
        /// <returns>Disposable that ends the timing when disposed</returns>
        IDisposable StartTimedEvent(string eventName);
    }
}