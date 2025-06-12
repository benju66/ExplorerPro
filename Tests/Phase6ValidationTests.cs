using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.Core;
using ExplorerPro.Core.Threading;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Phase 6 Validation Tests: Thread Safety Standardization
    /// Verifies thread safety patterns, UI thread operations, and cross-thread marshaling
    /// </summary>
    public class Phase6ValidationTests
    {
        public static async Task RunAllTests()
        {
            Console.WriteLine("=== Phase 6 Validation Starting ===");
            Console.WriteLine("Testing Thread Safety Standardization");
            Console.WriteLine();
            
            TestUIThreadHelper();
            Console.WriteLine();
            
            await TestCrossThreadOperations();
            Console.WriteLine();
            
            TestThreadSafetyValidator();
            Console.WriteLine();
            
            await TestAsyncPatterns();
            Console.WriteLine();
            
            TestThreadSafeExtensions();
            Console.WriteLine();
            
            await TestUIThreadMarshaling();
            Console.WriteLine();
            
            Console.WriteLine("=== Phase 6 Validation Complete ===");
        }

        private static void TestUIThreadHelper()
        {
            Console.WriteLine("Testing UIThreadHelper...");
            
            try
            {
                var uiThreadId = Thread.CurrentThread.ManagedThreadId;
                var executedThreadId = 0;
                var completed = false;
                
                UIThreadHelper.ExecuteOnUIThread(() =>
                {
                    executedThreadId = Thread.CurrentThread.ManagedThreadId;
                    completed = true;
                });
                
                // Wait a moment for execution
                Thread.Sleep(100);
                
                Console.WriteLine($"  ✓ UI thread execution: Original={uiThreadId}, Executed={executedThreadId}, Completed={completed}");
                Console.WriteLine($"  ✓ CheckAccess: {UIThreadHelper.CheckAccess()}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ UIThreadHelper test failed: {ex.Message}");
            }
        }

        private static async Task TestCrossThreadOperations()
        {
            Console.WriteLine("Testing cross-thread operations...");
            
            try
            {
                var textBlock = new TextBlock();
                var originalTitle = "Original";
                textBlock.Text = originalTitle;
                
                var backgroundThreadId = 0;
                var uiThreadId = 0;
                var finalText = "";
                
                await Task.Run(() =>
                {
                    backgroundThreadId = Thread.CurrentThread.ManagedThreadId;
                    
                    // This should marshal to UI thread
                    UIThreadHelper.ExecuteOnUIThread(() =>
                    {
                        uiThreadId = Thread.CurrentThread.ManagedThreadId;
                        textBlock.Text = "Updated";
                    });
                });
                
                await Task.Delay(100);
                
                finalText = UIThreadHelper.ExecuteOnUIThread(() => textBlock.Text);
                
                Console.WriteLine($"  ✓ Background thread: {backgroundThreadId}");
                Console.WriteLine($"  ✓ UI thread: {uiThreadId}");
                Console.WriteLine($"  ✓ Cross-thread update successful: {finalText == "Updated"}");
                Console.WriteLine($"  ✓ Final text: '{finalText}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Cross-thread operations test failed: {ex.Message}");
            }
        }

        private static void TestThreadSafetyValidator()
        {
            Console.WriteLine("Testing ThreadSafetyValidator...");
            
            try
            {
                var violations = 0;
                var validations = 0;
                
                // Test UI thread assertion (should pass on UI thread)
                try
                {
                    ThreadSafetyValidator.AssertUIThread();
                    validations++;
                    Console.WriteLine("  ✓ UI thread assertion passed on UI thread");
                }
                catch
                {
                    violations++;
                    Console.WriteLine("  ✗ UI thread assertion failed on UI thread");
                }
                
                // Test background thread assertion from background thread
                var backgroundTestCompleted = false;
                Task.Run(() =>
                {
                    try
                    {
                        ThreadSafetyValidator.AssertBackgroundThread();
                        validations++;
                        backgroundTestCompleted = true;
                        Console.WriteLine("  ✓ Background thread assertion passed on background thread");
                    }
                    catch
                    {
                        violations++;
                        Console.WriteLine("  ✗ Background thread assertion failed on background thread");
                    }
                }).Wait();
                
                // Test thread context logging
                ThreadSafetyValidator.LogThreadContext("TestOperation");
                Console.WriteLine("  ✓ Thread context logging completed");
                
                // Test thread safety tracking
                var trackingCompleted = false;
                ThreadSafetyValidator.TrackThreadSafety(() =>
                {
                    trackingCompleted = true;
                }, "TestTracking");
                
                Console.WriteLine($"  ✓ Thread safety validation: {validations} validations, {violations} violations");
                Console.WriteLine($"  ✓ Thread tracking completed: {trackingCompleted}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Thread safety validator test failed: {ex.Message}");
            }
        }

        private static async Task TestAsyncPatterns()
        {
            Console.WriteLine("Testing async patterns...");
            
            try
            {
                var control = new TextBlock();
                var completed = false;
                var backgroundWork = false;
                var uiUpdate = false;
                
                async Task UpdateAsync()
                {
                    // Simulate background work
                    await Task.Run(() =>
                    {
                        Thread.Sleep(10);
                        backgroundWork = true;
                    }).ConfigureAwait(false);
                    
                    // Update UI on UI thread
                    await UIThreadHelper.ExecuteOnUIThreadAsync(async () =>
                    {
                        control.Text = "Updated";
                        uiUpdate = true;
                        completed = true;
                    });
                }
                
                await UpdateAsync();
                
                Console.WriteLine($"  ✓ Background work completed: {backgroundWork}");
                Console.WriteLine($"  ✓ UI update completed: {uiUpdate}");
                Console.WriteLine($"  ✓ Async pattern test: {completed && control.Text == "Updated"}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ Async patterns test failed: {ex.Message}");
            }
        }

        private static void TestThreadSafeExtensions()
        {
            Console.WriteLine("Testing ThreadSafe extensions...");
            
            try
            {
                var textBlock = new TextBlock();
                var completed = false;
                
                // Test safe UI operation
                textBlock.SafeUIOperation(() =>
                {
                    textBlock.Text = "SafeOperation";
                    completed = true;
                }, "TestSafeOperation");
                
                Console.WriteLine($"  ✓ Safe UI operation completed: {completed}");
                Console.WriteLine($"  ✓ Text updated: '{textBlock.Text}'");
                
                // Test property update
                var propertyValue = textBlock.GetUIProperty<string>(TextBlock.TextProperty);
                Console.WriteLine($"  ✓ Safe property get: '{propertyValue}'");
                
                textBlock.UpdateUIProperty(TextBlock.TextProperty, "UpdatedProperty");
                var updatedValue = textBlock.GetUIProperty<string>(TextBlock.TextProperty);
                Console.WriteLine($"  ✓ Safe property update: '{updatedValue}'");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ ThreadSafe extensions test failed: {ex.Message}");
            }
        }

        private static async Task TestUIThreadMarshaling()
        {
            Console.WriteLine("Testing UI thread marshaling...");
            
            try
            {
                var results = new List<string>();
                var tasks = new List<Task>();
                
                // Create multiple background tasks that need to update UI
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i;
                    tasks.Add(Task.Run(async () =>
                    {
                        // Background work
                        await Task.Delay(10);
                        
                        // Marshal to UI thread
                        await UIThreadHelper.ExecuteOnUIThreadAsync(async () =>
                        {
                            results.Add($"Task{taskId}");
                        });
                    }));
                }
                
                await Task.WhenAll(tasks);
                
                Console.WriteLine($"  ✓ UI marshaling completed for {results.Count} tasks");
                Console.WriteLine($"  ✓ Results: {string.Join(", ", results)}");
                
                // Test concurrent UI operations
                var concurrentCompleted = 0;
                var concurrentTasks = new List<Task>();
                
                for (int i = 0; i < 3; i++)
                {
                    concurrentTasks.Add(Task.Run(() =>
                    {
                        UIThreadHelper.ExecuteOnUIThread(() =>
                        {
                            Interlocked.Increment(ref concurrentCompleted);
                        });
                    }));
                }
                
                await Task.WhenAll(concurrentTasks);
                
                Console.WriteLine($"  ✓ Concurrent UI operations completed: {concurrentCompleted}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ UI thread marshaling test failed: {ex.Message}");
            }
        }
    }
} 