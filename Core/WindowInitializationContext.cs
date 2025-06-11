using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Thread-safe context for window initialization with proper state tracking
    /// </summary>
    public sealed class WindowInitializationContext : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly List<string> _completedSteps = new List<string>();
        private readonly Dictionary<string, object> _contextData = new Dictionary<string, object>();
        
        public InitializationState CurrentState { get; private set; }
        public Exception LastError { get; private set; }
        public bool IsDisposed { get; private set; }
        public DateTime StartTime { get; }
        
        public WindowInitializationContext()
        {
            CurrentState = InitializationState.Created;
            StartTime = DateTime.UtcNow;
        }
        
        public bool TransitionTo(InitializationState newState)
        {
            lock (_lock)
            {
                if (IsDisposed) return false;
                
                // Validate state transition
                if (!IsValidTransition(CurrentState, newState))
                {
                    LastError = new InvalidOperationException(
                        $"Invalid state transition from {CurrentState} to {newState}");
                    return false;
                }
                
                CurrentState = newState;
                _completedSteps.Add($"{newState} at {_stopwatch.ElapsedMilliseconds}ms");
                return true;
            }
        }
        
        private bool IsValidTransition(InitializationState from, InitializationState to)
        {
            // Define valid state transitions
            return (from, to) switch
            {
                (InitializationState.Created, InitializationState.InitializingComponents) => true,
                (InitializationState.InitializingComponents, InitializationState.InitializingWindow) => true,
                (InitializationState.InitializingWindow, InitializationState.Ready) => true,
                (_, InitializationState.Failed) => true, // Can fail from any state
                _ => false
            };
        }
        
        public void RecordStep(string stepName)
        {
            lock (_lock)
            {
                _completedSteps.Add($"{stepName} at {_stopwatch.ElapsedMilliseconds}ms");
            }
        }
        
        public void SetData(string key, object value)
        {
            lock (_lock)
            {
                _contextData[key] = value;
            }
        }
        
        public T GetData<T>(string key)
        {
            lock (_lock)
            {
                return _contextData.TryGetValue(key, out var value) ? (T)value : default;
            }
        }
        
        /// <summary>
        /// Gets the elapsed time since initialization started.
        /// </summary>
        public TimeSpan ElapsedTime => _stopwatch.Elapsed;
        
        public void Dispose()
        {
            lock (_lock)
            {
                IsDisposed = true;
                _contextData.Clear();
                _completedSteps.Clear();
            }
        }
    }
} 