using System;
using System.Threading;
using System.Threading.Tasks;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Circuit breaker pattern implementation for tab disposal operations
    /// Prevents cascading failures by temporarily stopping operations when failure rate is high
    /// </summary>
    public class CircuitBreaker : IDisposable
    {
        private readonly int _failureThreshold;
        private readonly TimeSpan _timeout;
        private readonly Action<CircuitBreakerState, CircuitBreakerState> _onStateChange;
        
        private volatile CircuitBreakerState _state = CircuitBreakerState.Closed;
        private volatile int _failureCount = 0;
        private long _lastFailureTimeTicks = DateTime.MinValue.Ticks;
        private long _circuitOpenTimeTicks = DateTime.MinValue.Ticks;
        
        private readonly object _lock = new object();
        private bool _disposed = false;

        public CircuitBreaker(int failureThreshold, TimeSpan timeout, Action<CircuitBreakerState, CircuitBreakerState> onStateChange = null)
        {
            _failureThreshold = failureThreshold;
            _timeout = timeout;
            _onStateChange = onStateChange;
        }

        public CircuitBreakerState State => _state;
        public int FailureCount => _failureCount;
        public DateTime LastFailureTime => new DateTime(Interlocked.Read(ref _lastFailureTimeTicks));

        public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(CircuitBreaker));

            // Check if circuit should be closed after timeout
            CheckCircuitState();

            switch (_state)
            {
                case CircuitBreakerState.Open:
                    throw new CircuitBreakerOpenException("Circuit breaker is open");
                
                case CircuitBreakerState.HalfOpen:
                    return await ExecuteInHalfOpenState(operation);
                
                case CircuitBreakerState.Closed:
                default:
                    return await ExecuteInClosedState(operation);
            }
        }

        private async Task<T> ExecuteInClosedState<T>(Func<Task<T>> operation)
        {
            try
            {
                var result = await operation();
                OnSuccess();
                return result;
            }
            catch (Exception ex)
            {
                OnFailure(ex);
                throw;
            }
        }

        private async Task<T> ExecuteInHalfOpenState<T>(Func<Task<T>> operation)
        {
            try
            {
                var result = await operation();
                Reset();
                return result;
            }
            catch (Exception ex)
            {
                Trip();
                throw;
            }
        }

        private void CheckCircuitState()
        {
            if (_state == CircuitBreakerState.Open)
            {
                var circuitOpenTime = new DateTime(Interlocked.Read(ref _circuitOpenTimeTicks));
                if (DateTime.UtcNow - circuitOpenTime >= _timeout)
                {
                    ChangeState(CircuitBreakerState.HalfOpen);
                }
            }
        }

        private void OnSuccess()
        {
            lock (_lock)
            {
                _failureCount = 0;
                Interlocked.Exchange(ref _lastFailureTimeTicks, DateTime.MinValue.Ticks);
            }
        }

        private void OnFailure(Exception exception)
        {
            lock (_lock)
            {
                _failureCount++;
                Interlocked.Exchange(ref _lastFailureTimeTicks, DateTime.UtcNow.Ticks);

                if (_failureCount >= _failureThreshold && _state == CircuitBreakerState.Closed)
                {
                    Trip();
                }
            }
        }

        private void Trip()
        {
            lock (_lock)
            {
                Interlocked.Exchange(ref _circuitOpenTimeTicks, DateTime.UtcNow.Ticks);
                ChangeState(CircuitBreakerState.Open);
            }
        }

        private void Reset()
        {
            lock (_lock)
            {
                _failureCount = 0;
                Interlocked.Exchange(ref _lastFailureTimeTicks, DateTime.MinValue.Ticks);
                Interlocked.Exchange(ref _circuitOpenTimeTicks, DateTime.MinValue.Ticks);
                ChangeState(CircuitBreakerState.Closed);
            }
        }

        private void ChangeState(CircuitBreakerState newState)
        {
            var oldState = _state;
            _state = newState;
            _onStateChange?.Invoke(oldState, newState);
        }

        public void Dispose()
        {
            _disposed = true;
        }
    }

    public enum CircuitBreakerState
    {
        Closed,   // Normal operation, failures are counted
        Open,     // Circuit is open, all calls fail immediately
        HalfOpen  // Test state, single call allowed to test if service is back
    }

    public class CircuitBreakerOpenException : Exception
    {
        public CircuitBreakerOpenException(string message) : base(message) { }
        public CircuitBreakerOpenException(string message, Exception innerException) : base(message, innerException) { }
    }
}