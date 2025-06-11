using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Thread-safe context for window initialization with proper state tracking
    /// Updated to use unified WindowState enum instead of separate InitializationState
    /// </summary>
    public sealed class WindowInitializationContext : IDisposable
    {
        private readonly object _lock = new object();
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly List<string> _completedSteps = new List<string>();
        private readonly Dictionary<string, object> _contextData = new Dictionary<string, object>();
        
        public WindowState CurrentState { get; private set; }
        public Exception LastError { get; private set; }
        public bool IsDisposed { get; private set; }
        public DateTime StartTime { get; }
        
        public WindowInitializationContext()
        {
            CurrentState = WindowState.Created;
            StartTime = DateTime.UtcNow;
        }
        
        public bool TransitionTo(WindowState newState)
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
        
        private bool IsValidTransition(WindowState from, WindowState to)
        {
            // Define valid state transitions for initialization context
            return (from, to) switch
            {
                (WindowState.Created, WindowState.Initializing) => true,
                (WindowState.Initializing, WindowState.ComponentsReady) => true,
                (WindowState.ComponentsReady, WindowState.LoadingUI) => true,
                (WindowState.LoadingUI, WindowState.Ready) => true,
                (_, WindowState.Failed) => true, // Can fail from any state
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