using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Provides context information for operations with correlation support.
    /// </summary>
    public class OperationContext
    {
        private readonly Stopwatch _stopwatch;
        
        public OperationContext(string operationName)
        {
            OperationName = operationName ?? throw new ArgumentNullException(nameof(operationName));
            OperationId = Guid.NewGuid();
            CorrelationId = Guid.NewGuid();
            StartTime = DateTime.UtcNow;
            Properties = new Dictionary<string, object>();
            _stopwatch = Stopwatch.StartNew();
        }
        
        /// <summary>
        /// Creates a child context that shares the same correlation ID.
        /// </summary>
        public static OperationContext CreateChild(OperationContext parent, string operationName)
        {
            if (parent == null)
                throw new ArgumentNullException(nameof(parent));
                
            var child = new OperationContext(operationName)
            {
                CorrelationId = parent.CorrelationId,
                ParentOperationId = parent.OperationId
            };
            
            // Copy parent properties with prefix
            foreach (var prop in parent.Properties)
            {
                child.Properties[$"Parent.{prop.Key}"] = prop.Value;
            }
            
            return child;
        }
        
        /// <summary>
        /// Unique identifier for this specific operation.
        /// </summary>
        public Guid OperationId { get; }
        
        /// <summary>
        /// Correlation ID shared across related operations.
        /// </summary>
        public Guid CorrelationId { get; private set; }
        
        /// <summary>
        /// Parent operation ID if this is a child operation.
        /// </summary>
        public Guid? ParentOperationId { get; private set; }
        
        /// <summary>
        /// Name of the operation being performed.
        /// </summary>
        public string OperationName { get; }
        
        /// <summary>
        /// When the operation started.
        /// </summary>
        public DateTime StartTime { get; }
        
        /// <summary>
        /// How long the operation has been running.
        /// </summary>
        public TimeSpan Elapsed => _stopwatch.Elapsed;
        
        /// <summary>
        /// Additional properties for logging and telemetry.
        /// </summary>
        public Dictionary<string, object> Properties { get; }
        
        /// <summary>
        /// Adds or updates a property value.
        /// </summary>
        public OperationContext WithProperty(string key, object value)
        {
            Properties[key] = value;
            return this;
        }
        
        /// <summary>
        /// Creates a timing scope for measuring sub-operations.
        /// </summary>
        public IDisposable MeasureTime(string scopeName)
        {
            return new TimingScope(this, scopeName);
        }
        
        /// <summary>
        /// Marks the operation as completed and stops timing.
        /// </summary>
        public void Complete()
        {
            _stopwatch.Stop();
            Properties["Duration"] = Elapsed.TotalMilliseconds;
            Properties["Completed"] = true;
        }
        
        /// <summary>
        /// Marks the operation as failed and stops timing.
        /// </summary>
        public void Fail(Exception exception)
        {
            _stopwatch.Stop();
            Properties["Duration"] = Elapsed.TotalMilliseconds;
            Properties["Failed"] = true;
            Properties["ExceptionType"] = exception?.GetType().Name;
            Properties["ExceptionMessage"] = exception?.Message;
        }
        
        private class TimingScope : IDisposable
        {
            private readonly OperationContext _context;
            private readonly string _scopeName;
            private readonly Stopwatch _scopeWatch;
            
            public TimingScope(OperationContext context, string scopeName)
            {
                _context = context;
                _scopeName = scopeName;
                _scopeWatch = Stopwatch.StartNew();
            }
            
            public void Dispose()
            {
                _scopeWatch.Stop();
                _context.Properties[$"Timing.{_scopeName}"] = _scopeWatch.ElapsedMilliseconds;
            }
        }
    }
} 