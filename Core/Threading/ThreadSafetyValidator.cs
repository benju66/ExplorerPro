using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Threading;
using ExplorerPro.Core;

namespace ExplorerPro.Core.Threading
{
    /// <summary>
    /// Thread safety validator for debugging and ensuring proper UI thread usage
    /// Phase 6: Thread Safety Standardization
    /// </summary>
    public static class ThreadSafetyValidator
    {
        private static bool _isEnabled = true;

        public static bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        /// <summary>
        /// Asserts that the current thread is the UI thread
        /// </summary>
        [Conditional("DEBUG")]
        public static void AssertUIThread([CallerMemberName] string memberName = "", 
            [CallerFilePath] string filePath = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!_isEnabled) return;

            if (!UIThreadHelper.CheckAccess())
            {
                var message = $"UI thread violation in {memberName} at {System.IO.Path.GetFileName(filePath)}:{lineNumber}";
                Debug.WriteLine(message);
                
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Asserts that the current thread is NOT the UI thread
        /// </summary>
        [Conditional("DEBUG")]
        public static void AssertBackgroundThread([CallerMemberName] string memberName = "", 
            [CallerFilePath] string filePath = "", 
            [CallerLineNumber] int lineNumber = 0)
        {
            if (!_isEnabled) return;

            if (UIThreadHelper.CheckAccess())
            {
                var message = $"Background thread violation in {memberName} at {System.IO.Path.GetFileName(filePath)}:{lineNumber}";
                Debug.WriteLine(message);
                
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Tracks thread safety for an operation and logs any thread switches
        /// </summary>
        public static void TrackThreadSafety(Action action, string operationName)
        {
            var startThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var isUIThread = UIThreadHelper.CheckAccess();

            try
            {
                action();
            }
            finally
            {
                var endThread = System.Threading.Thread.CurrentThread.ManagedThreadId;
                
                if (startThread != endThread)
                {
                    Debug.WriteLine($"Thread switch detected in {operationName}: {startThread} -> {endThread}");
                }
            }
        }

        /// <summary>
        /// Validates that a dependency object operation is thread-safe
        /// </summary>
        [Conditional("DEBUG")]
        public static void ValidateDependencyObjectAccess(DependencyObject obj, [CallerMemberName] string memberName = "")
        {
            if (!_isEnabled || obj == null) return;

            if (!obj.CheckAccess())
            {
                var message = $"Cross-thread DependencyObject access in {memberName}";
                Debug.WriteLine(message);
                
                if (Debugger.IsAttached)
                {
                    Debugger.Break();
                }
                
                throw new InvalidOperationException(message);
            }
        }

        /// <summary>
        /// Logs thread context information for debugging
        /// </summary>
        [Conditional("DEBUG")]
        public static void LogThreadContext(string operation)
        {
            if (!_isEnabled) return;

            var threadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            var isUI = UIThreadHelper.CheckAccess();
            var threadName = System.Threading.Thread.CurrentThread.Name ?? "Unnamed";
            
            Debug.WriteLine($"[Thread Safety] {operation} - Thread: {threadId} ({threadName}), UI: {isUI}");
        }
    }
} 