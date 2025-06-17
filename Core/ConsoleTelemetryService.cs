using System;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Simple telemetry service that logs to console
    /// </summary>
    public class ConsoleTelemetryService : ITelemetryService
    {
        /// <summary>
        /// Tracks an exception with the given context
        /// </summary>
        /// <param name="ex">The exception to track</param>
        /// <param name="context">The context in which the exception occurred</param>
        public void TrackException(Exception ex, string context)
        {
            Console.WriteLine($"Telemetry: Exception in {context}: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
        }
    }
} 