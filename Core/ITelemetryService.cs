using System;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Interface for telemetry services
    /// </summary>
    public interface ITelemetryService
    {
        /// <summary>
        /// Tracks an exception with the given context
        /// </summary>
        /// <param name="ex">The exception to track</param>
        /// <param name="context">The context in which the exception occurred</param>
        void TrackException(Exception ex, string context);
    }
} 