using System;
using System.Threading;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Unified window state with thread-safe transitions
    /// </summary>
    public enum WindowState
    {
        // Initialization states
        Created = 0,
        Initializing = 1,
        ComponentsReady = 2,
        LoadingUI = 3,
        
        // Operational states
        Ready = 10,
        Busy = 11,
        
        // Closing states  
        Closing = 20,
        Disposed = 21,
        
        // Error state
        Failed = 99
    }
    
    /// <summary>
    /// Thread-safe window state machine
    /// </summary>
    public sealed class WindowStateManager
    {
        private WindowState _currentState = WindowState.Created;
        private readonly object _stateLock = new object();
        private readonly AutoResetEvent _stateChanged = new AutoResetEvent(false);
        
        public event EventHandler<WindowStateChangedEventArgs> StateChanged;
        
        /// <summary>
        /// Current state (thread-safe)
        /// </summary>
        public WindowState CurrentState
        {
            get
            {
                lock (_stateLock)
                {
                    return _currentState;
                }
            }
        }
        
        /// <summary>
        /// Check if in any of the specified states
        /// </summary>
        public bool IsInState(params WindowState[] states)
        {
            lock (_stateLock)
            {
                return Array.IndexOf(states, _currentState) >= 0;
            }
        }
        
        /// <summary>
        /// Check if transition to state is valid
        /// </summary>
        public bool CanTransitionTo(WindowState newState)
        {
            lock (_stateLock)
            {
                return IsValidTransition(_currentState, newState);
            }
        }
        
        /// <summary>
        /// Attempt state transition
        /// </summary>
        public bool TryTransitionTo(WindowState newState, out string error)
        {
            error = null;
            WindowState oldState;
            
            lock (_stateLock)
            {
                oldState = _currentState;
                
                if (!IsValidTransition(oldState, newState))
                {
                    error = $"Invalid transition from {oldState} to {newState}";
                    return false;
                }
                
                _currentState = newState;
                _stateChanged.Set();
            }
            
            // Raise event outside lock
            OnStateChanged(oldState, newState);
            return true;
        }
        
        /// <summary>
        /// Wait for specific state with timeout
        /// </summary>
        public bool WaitForState(WindowState targetState, TimeSpan timeout)
        {
            var endTime = DateTime.UtcNow + timeout;
            
            while (DateTime.UtcNow < endTime)
            {
                lock (_stateLock)
                {
                    if (_currentState == targetState)
                        return true;
                        
                    if (_currentState == WindowState.Failed || 
                        _currentState == WindowState.Disposed)
                        return false;
                }
                
                var remaining = endTime - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero)
                    break;
                    
                _stateChanged.WaitOne(Math.Min((int)remaining.TotalMilliseconds, 100));
            }
            
            return false;
        }
        
        // Helper properties for common checks
        public bool IsInitializing => IsInState(
            WindowState.Created, 
            WindowState.Initializing, 
            WindowState.ComponentsReady, 
            WindowState.LoadingUI);
            
        public bool IsOperational => IsInState(
            WindowState.Ready, 
            WindowState.Busy);
            
        public bool IsClosing => IsInState(
            WindowState.Closing, 
            WindowState.Disposed);
            
        public bool HasFailed => CurrentState == WindowState.Failed;
        
        /// <summary>
        /// Define valid state transitions
        /// </summary>
        private bool IsValidTransition(WindowState from, WindowState to)
        {
            // Can always transition to Failed or Disposed
            if (to == WindowState.Failed || to == WindowState.Disposed)
                return true;
                
            return (from, to) switch
            {
                // Initialization flow
                (WindowState.Created, WindowState.Initializing) => true,
                (WindowState.Initializing, WindowState.ComponentsReady) => true,
                (WindowState.ComponentsReady, WindowState.LoadingUI) => true,
                (WindowState.LoadingUI, WindowState.Ready) => true,
                
                // Operational transitions
                (WindowState.Ready, WindowState.Busy) => true,
                (WindowState.Busy, WindowState.Ready) => true,
                (WindowState.Ready, WindowState.Closing) => true,
                (WindowState.Busy, WindowState.Closing) => true,
                
                // Closing flow
                (WindowState.Closing, WindowState.Disposed) => true,
                
                _ => false
            };
        }
        
        private void OnStateChanged(WindowState oldState, WindowState newState)
        {
            StateChanged?.Invoke(this, new WindowStateChangedEventArgs(oldState, newState));
        }
        
        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            _stateChanged?.Dispose();
        }
    }
    
    public class WindowStateChangedEventArgs : EventArgs
    {
        public WindowState OldState { get; }
        public WindowState NewState { get; }
        
        public WindowStateChangedEventArgs(WindowState oldState, WindowState newState)
        {
            OldState = oldState;
            NewState = newState;
        }
    }
} 