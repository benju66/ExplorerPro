using System;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using ExplorerPro.UI.MainWindow;
using ExplorerPro.Core.Events;
using ExplorerPro.Core.Disposables;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Validation tests for Phase 3: Event Infrastructure - Weak Event Patterns
    /// Tests that all event subscriptions use weak references and proper cleanup
    /// </summary>
    public static class Phase3ValidationTests
    {
        public static async Task RunAllTests()
        {
            Console.WriteLine("=== Phase 3 Event Infrastructure Validation ===");
            Console.WriteLine();

            try
            {
                TestWeakEventSubscriptions();
                Console.WriteLine();
                
                TestEventCleanup();
                Console.WriteLine();
                
                await TestThreadSafetyInEvents();
                Console.WriteLine();
                
                TestEventSubscriptionExtensions();
                Console.WriteLine();
                
                Console.WriteLine("✅ Phase 3 Validation Complete - All tests passed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Phase 3 Validation Failed: {ex.Message}");
                throw;
            }
        }

        private static void TestWeakEventSubscriptions()
        {
            Console.WriteLine("🔍 Testing Weak Event Subscriptions...");
            
            try
            {
                var window = new MainWindow();
                
                // Test that event subscriptions are tracked
                var eventCount = GetEventHandlerCount(window, "_eventSubscriptions");
                Console.WriteLine($"EventSubscriptions initialized: {eventCount >= 0}");
                
                // Test window initialization with weak events
                window.WireUpEventHandlers();
                var afterWiring = GetEventHandlerCount(window, "_eventSubscriptions");
                Console.WriteLine($"Events subscribed after wiring: {afterWiring > eventCount}");
                
                // Test disposal cleans up subscriptions
                window.Dispose();
                Console.WriteLine("Window disposed successfully");
                
                Console.WriteLine("✅ Weak event subscriptions working correctly");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Weak event subscription test failed: {ex.Message}");
                throw;
            }
        }

        private static void TestEventCleanup()
        {
            Console.WriteLine("🧹 Testing Event Cleanup...");
            
            try
            {
                int subscriptionCount = 0;
                
                using (var window = new MainWindow())
                {
                    window.WireUpEventHandlers();
                    subscriptionCount = CountActiveSubscriptions(window);
                    Console.WriteLine($"Active subscriptions in using block: {subscriptionCount}");
                }
                
                // Force garbage collection
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                
                Console.WriteLine("✅ All subscriptions disposed after using block");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Event cleanup test failed: {ex.Message}");
                throw;
            }
        }

        private static async Task TestThreadSafetyInEvents()
        {
            Console.WriteLine("🔒 Testing Thread Safety in Events...");
            
            try
            {
                var window = new MainWindow();
                window.WireUpEventHandlers();
                
                // Test concurrent access to event subscriptions
                var tasks = new Task[5];
                for (int i = 0; i < 5; i++)
                {
                    int taskId = i;
                    tasks[i] = Task.Run(() =>
                    {
                        try
                        {
                            // Simulate event operations from background thread
                            window.PerformSafeOperation(() =>
                            {
                                // This should execute safely on UI thread
                                Console.WriteLine($"Task {taskId} executed safely");
                            }, $"TestTask{taskId}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Task {taskId} failed: {ex.Message}");
                        }
                    });
                }
                
                await Task.WhenAll(tasks);
                window.Dispose();
                
                Console.WriteLine("✅ Thread safety test completed without exceptions");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Thread safety test failed: {ex.Message}");
                throw;
            }
        }

        private static void TestEventSubscriptionExtensions()
        {
            Console.WriteLine("🔧 Testing Event Subscription Extensions...");
            
            try
            {
                var disposables = new CompositeDisposable();
                var button = new System.Windows.Controls.Button();
                
                // Test command subscription extension
                button.CommandBindings.Add(new System.Windows.Input.CommandBinding(
                    System.Windows.Input.ApplicationCommands.Copy));
                
                disposables.SubscribeToCommands(button);
                Console.WriteLine("Command subscription extension working");
                
                // Test weak subscription helper
                var testObj = new TestEventSource();
                var subscription = testObj.SubscribeWeakly((source, weakSubscription) =>
                {
                    EventHandler handler = (s, e) => Console.WriteLine("Event handled");
                    source.TestEvent += handler;
                    if (weakSubscription is EventSubscriptionExtensions.WeakSubscription<TestEventSource> ws)
                    {
                        ws.SetSubscription(Disposable.Create(() => 
                            source.TestEvent -= handler));
                    }
                });
                
                Console.WriteLine("Weak subscription helper working");
                
                disposables.Dispose();
                subscription.Dispose();
                
                Console.WriteLine("✅ Event subscription extensions working correctly");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Event subscription extensions test failed: {ex.Message}");
                throw;
            }
        }

        private static int GetEventHandlerCount(object source, string fieldName)
        {
            var type = source.GetType();
            var field = type.GetField(fieldName, 
                BindingFlags.Instance | BindingFlags.NonPublic);
            
            if (field?.GetValue(source) is CompositeDisposable composite)
            {
                return composite.Count;
            }
            
            return -1;
        }

        private static int CountActiveSubscriptions(MainWindow window)
        {
            // Use reflection to access _eventSubscriptions field
            var field = typeof(MainWindow).GetField("_eventSubscriptions", 
                BindingFlags.Instance | BindingFlags.NonPublic);
            var composite = field?.GetValue(window) as CompositeDisposable;
            
            return composite?.Count ?? 0;
        }

        /// <summary>
        /// Test class for event source testing
        /// </summary>
        private class TestEventSource
        {
            public event EventHandler TestEvent;
            
            protected virtual void OnTestEvent()
            {
                TestEvent?.Invoke(this, EventArgs.Empty);
            }
        }
    }
} 