using System;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Simple telemetry service that logs to console
    /// </summary>
    public class ConsoleTelemetryService : ITelemetryService
    {
        protected readonly ILogger _logger;
        
        public ConsoleTelemetryService(ILogger logger = null)
        {
            _logger = logger;
        }
        
        /// <summary>
        /// Tracks an exception with the given context
        /// </summary>
        /// <param name="ex">The exception to track</param>
        /// <param name="context">The context in which the exception occurred</param>
        public virtual void TrackException(Exception ex, string context)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] [TELEMETRY EXCEPTION] Context: {context}");
            Console.WriteLine($"[{timestamp}] Exception: {ex.Message}");
            Console.WriteLine($"[{timestamp}] Stack trace: {ex.StackTrace}");
            
            _logger?.LogError(ex, "Telemetry Exception in context: {Context}", context);
        }
    }
} 