using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Centralized exception handling with telemetry and recovery strategies.
    /// </summary>
    public class ExceptionHandler
    {
        private readonly ILogger<ExceptionHandler> _logger;
        private readonly ITelemetryService _telemetry;
        private readonly List<IExceptionPolicy> _policies;
        
        // Circuit breaker fields to prevent infinite loops
        private readonly ThreadLocal<int> _recursionDepth = new ThreadLocal<int>(() => 0);
        private const int MaxRecursionDepth = 3;
        private readonly ConcurrentDictionary<string, DateTime> _recentErrors = new ConcurrentDictionary<string, DateTime>();
        private readonly TimeSpan _errorThrottleWindow = TimeSpan.FromSeconds(1);
        
        public ExceptionHandler(ILogger<ExceptionHandler> logger, ITelemetryService telemetry)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _telemetry = telemetry ?? throw new ArgumentNullException(nameof(telemetry));
            _policies = new List<IExceptionPolicy>();
            
            InitializeDefaultPolicies();
        }
        
        /// <summary>
        /// Handles an exception with appropriate logging, telemetry, and recovery.
        /// </summary>
        public ExceptionResult HandleException(
            Exception exception,
            OperationContext context,
            [CallerMemberName] string memberName = "",
            [CallerFilePath] string filePath = "",
            [CallerLineNumber] int lineNumber = 0)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            // Circuit breaker pattern to prevent infinite loops
            if (_recursionDepth.Value >= MaxRecursionDepth)
            {
                // Emergency fallback - write to console only
                System.Diagnostics.Debug.WriteLine($"[CRITICAL] Exception handler recursion limit reached: {exception?.Message}");
                return new ExceptionResult
                {
                    Exception = exception,
                    Context = context,
                    Timestamp = DateTime.UtcNow,
                    CallerInfo = new CallerInfo(memberName, filePath, lineNumber),
                    CanContinue = false,
                    Severity = ExceptionSeverity.Critical
                };
            }

            // Throttle repeated errors
            var errorKey = $"{exception?.GetType().Name}:{exception?.Message}";
            if (_recentErrors.TryGetValue(errorKey, out var lastError))
            {
                if (DateTime.UtcNow - lastError < _errorThrottleWindow)
                {
                    return new ExceptionResult
                    {
                        Exception = exception,
                        Context = context,
                        Timestamp = DateTime.UtcNow,
                        CallerInfo = new CallerInfo(memberName, filePath, lineNumber),
                        CanContinue = true,
                        Severity = ExceptionSeverity.Low
                    };
                }
            }
            _recentErrors[errorKey] = DateTime.UtcNow;
            
            var result = new ExceptionResult
            {
                Exception = exception,
                Context = context,
                Timestamp = DateTime.UtcNow,
                CallerInfo = new CallerInfo(memberName, filePath, lineNumber)
            };

            _recursionDepth.Value++;
            try
            {
                // 1. Classify the exception
                result.Severity = ClassifyException(exception);
                result.Category = CategorizeException(exception);
                
                // 2. Log with appropriate level
                LogException(result);
                
                // 3. Send telemetry
                SendTelemetry(result);
                
                // 4. Apply recovery policies
                ApplyRecoveryPolicies(result);
                
                // 5. Determine if operation should continue
                result.CanContinue = DetermineIfCanContinue(result);
                
                return result;
            }
            catch (Exception handlerEx)
            {
                // Last resort logging
                try
                {
                    _logger.LogCritical(handlerEx, 
                        "Critical failure in exception handler while handling {OriginalException}", 
                        exception.GetType().Name);
                }
                catch
                {
                    // If logging fails, write to debug output
                    Debug.WriteLine($"CRITICAL: Exception handler failed: {handlerEx}");
                }
                
                result.HandlerException = handlerEx;
                result.CanContinue = false;
                return result;
            }
            finally
            {
                _recursionDepth.Value--;
                
                // Clean up old throttle entries periodically
                if (_recentErrors.Count > 100)
                {
                    var cutoff = DateTime.UtcNow - _errorThrottleWindow;
                    var keysToRemove = _recentErrors.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
                    foreach (var key in keysToRemove)
                    {
                        _recentErrors.TryRemove(key, out _);
                    }
                }
            }
        }
        
        /// <summary>
        /// Wraps an operation with comprehensive exception handling.
        /// </summary>
        public T ExecuteWithHandling<T>(
            Func<T> operation,
            OperationContext context,
            Func<Exception, T> fallbackValue = null)
        {
            try
            {
                return operation();
            }
            catch (Exception ex)
            {
                var result = HandleException(ex, context);
                
                if (result.CanContinue && fallbackValue != null)
                {
                    return fallbackValue(ex);
                }
                
                throw new OperationFailedException(
                    "Operation failed and cannot continue", 
                    ex, 
                    result);
            }
        }
        
        /// <summary>
        /// Wraps an async operation with comprehensive exception handling.
        /// </summary>
        public async Task<T> ExecuteWithHandlingAsync<T>(
            Func<Task<T>> operation,
            OperationContext context,
            Func<Exception, T> fallbackValue = null)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex)
            {
                var result = HandleException(ex, context);
                
                if (result.CanContinue && fallbackValue != null)
                {
                    return fallbackValue(ex);
                }
                
                throw new OperationFailedException(
                    "Async operation failed and cannot continue", 
                    ex, 
                    result);
            }
        }
        
        #region Private Methods
        
        private void InitializeDefaultPolicies()
        {
            _policies.Add(new RetryPolicy());
            _policies.Add(new CircuitBreakerPolicy());
            _policies.Add(new FallbackPolicy());
            _policies.Add(new StateRollbackPolicy());
        }
        
        private ExceptionSeverity ClassifyException(Exception exception)
        {
            return exception switch
            {
                OutOfMemoryException => ExceptionSeverity.Critical,
                StackOverflowException => ExceptionSeverity.Critical,
                AccessViolationException => ExceptionSeverity.Critical,
                WindowInitializationException => ExceptionSeverity.High,
                InvalidOperationException => ExceptionSeverity.Medium,
                ArgumentException => ExceptionSeverity.Low,
                _ => ExceptionSeverity.Medium
            };
        }
        
        private ExceptionCategory CategorizeException(Exception exception)
        {
            return exception switch
            {
                WindowInitializationException => ExceptionCategory.Initialization,
                InvalidOperationException => ExceptionCategory.StateViolation,
                ArgumentException => ExceptionCategory.Validation,
                NullReferenceException => ExceptionCategory.NullReference,
                _ => ExceptionCategory.General
            };
        }
        
        private void LogException(ExceptionResult result)
        {
            var message = BuildDetailedMessage(result);
            
            switch (result.Severity)
            {
                case ExceptionSeverity.Critical:
                    _logger.LogCritical(result.Exception, message);
                    break;
                case ExceptionSeverity.High:
                    _logger.LogError(result.Exception, message);
                    break;
                case ExceptionSeverity.Medium:
                    _logger.LogWarning(result.Exception, message);
                    break;
                case ExceptionSeverity.Low:
                    _logger.LogInformation(result.Exception, message);
                    break;
            }
        }
        
        private string BuildDetailedMessage(ExceptionResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Exception in {result.Context.OperationName}");
            sb.AppendLine($"Category: {result.Category}, Severity: {result.Severity}");
            sb.AppendLine($"Location: {result.CallerInfo}");
            
            if (result.Context.Properties.Count > 0)
            {
                sb.AppendLine("Context Properties:");
                foreach (var prop in result.Context.Properties)
                {
                    sb.AppendLine($"  {prop.Key}: {prop.Value}");
                }
            }
            
            return sb.ToString();
        }
        
        private void SendTelemetry(ExceptionResult result)
        {
            var context = $"Operation: {result.Context.OperationName}, Category: {result.Category}, Severity: {result.Severity}, CanContinue: {result.CanContinue}, Location: {result.CallerInfo}";
            _telemetry.TrackException(result.Exception, context);
        }
        
        private void ApplyRecoveryPolicies(ExceptionResult result)
        {
            foreach (var policy in _policies)
            {
                if (policy.CanHandle(result))
                {
                    policy.Apply(result);
                }
            }
        }
        
        private bool DetermineIfCanContinue(ExceptionResult result)
        {
            // Critical exceptions cannot continue
            if (result.Severity == ExceptionSeverity.Critical)
                return false;
            
            // Check if any policy determined we cannot continue
            if (result.RecoveryActions.Contains(RecoveryAction.Terminate))
                return false;
            
            // State violations usually cannot continue
            if (result.Category == ExceptionCategory.StateViolation)
                return false;
            
            return true;
        }
        
        #endregion
    }
    
    #region Supporting Types
    
    public enum ExceptionSeverity
    {
        Low,
        Medium,
        High,
        Critical
    }
    
    public enum ExceptionCategory
    {
        General,
        Initialization,
        StateViolation,
        Validation,
        NullReference,
        Resource,
        Network,
        Security
    }
    
    public enum RecoveryAction
    {
        None,
        Retry,
        Fallback,
        Rollback,
        Terminate,
        Restart
    }
    
    public class ExceptionResult
    {
        public Exception Exception { get; set; }
        public Exception HandlerException { get; set; }
        public OperationContext Context { get; set; }
        public ExceptionSeverity Severity { get; set; }
        public ExceptionCategory Category { get; set; }
        public bool CanContinue { get; set; }
        public List<RecoveryAction> RecoveryActions { get; set; } = new List<RecoveryAction>();
        public DateTime Timestamp { get; set; }
        public CallerInfo CallerInfo { get; set; }
        public Dictionary<string, object> AdditionalData { get; set; } = new Dictionary<string, object>();
    }
    
    public class CallerInfo
    {
        public string MemberName { get; }
        public string FilePath { get; }
        public int LineNumber { get; }
        
        public CallerInfo(string memberName, string filePath, int lineNumber)
        {
            MemberName = memberName;
            FilePath = filePath;
            LineNumber = lineNumber;
        }
        
        public override string ToString()
        {
            return $"{MemberName} at {FilePath}:{LineNumber}";
        }
    }
    
    public interface IExceptionPolicy
    {
        bool CanHandle(ExceptionResult result);
        void Apply(ExceptionResult result);
    }
    
    
    
    public class OperationFailedException : Exception
    {
        public ExceptionResult Result { get; }
        
        public OperationFailedException(string message, Exception innerException, ExceptionResult result)
            : base(message, innerException)
        {
            Result = result;
        }
    }
    
    #region Default Policy Implementations
    
    public class RetryPolicy : IExceptionPolicy
    {
        public bool CanHandle(ExceptionResult result)
        {
            return result.Severity != ExceptionSeverity.Critical &&
                   result.Category != ExceptionCategory.StateViolation;
        }
        
        public void Apply(ExceptionResult result)
        {
            result.RecoveryActions.Add(RecoveryAction.Retry);
        }
    }
    
    public class CircuitBreakerPolicy : IExceptionPolicy
    {
        public bool CanHandle(ExceptionResult result)
        {
            return result.Severity == ExceptionSeverity.High;
        }
        
        public void Apply(ExceptionResult result)
        {
            result.RecoveryActions.Add(RecoveryAction.Fallback);
        }
    }
    
    public class FallbackPolicy : IExceptionPolicy
    {
        public bool CanHandle(ExceptionResult result)
        {
            return result.Severity != ExceptionSeverity.Critical;
        }
        
        public void Apply(ExceptionResult result)
        {
            result.RecoveryActions.Add(RecoveryAction.Fallback);
        }
    }
    
    public class StateRollbackPolicy : IExceptionPolicy
    {
        public bool CanHandle(ExceptionResult result)
        {
            return result.Category == ExceptionCategory.StateViolation;
        }
        
        public void Apply(ExceptionResult result)
        {
            result.RecoveryActions.Add(RecoveryAction.Rollback);
        }
    }
    
    #endregion
    
    #endregion
} 