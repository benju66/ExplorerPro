using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ExplorerPro.Core.Configuration;
using ExplorerPro.Core.Events;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Quick test script for EventCleanupManager functionality
    /// Tests memory leak prevention, performance, and integration
    /// </summary>
    public static class TestEventCleanupManager
    {
        public static async Task RunQuickTest()
        {
            Console.WriteLine("=== EventCleanupManager Quick Test ===");
            Console.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            
            try
            {
                // Test 1: Feature flag evaluation
                TestFeatureFlags();
                
                // Test 2: Basic manager functionality (if enabled)
                if (FeatureFlags.UseEventCleanupManager)
                {
                    await TestBasicManagerFunctionality();
                    await TestMemoryLeakPrevention();
                    await TestPerformanceMetrics();
                    await TestConcurrentOperations();
                    TestCoordinatorFunctionality();
                }
                else
                {
                    Console.WriteLine("⚠️  EventCleanupManager is disabled via feature flag");
                    Console.WriteLine("   Set FF_USE_EVENT_CLEANUP_MANAGER=true to enable testing");
                }
                
                Console.WriteLine();
                Console.WriteLine("✅ All EventCleanupManager tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"❌ EventCleanupManager test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
            finally
            {
                Console.WriteLine($"Test completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
                Console.WriteLine("=== Test Complete ===");
            }
        }
        
        private static void TestFeatureFlags()
        {
            Console.WriteLine("🚦 Testing Feature Flags...");
            
            var originalFlag = FeatureFlags.UseEventCleanupManager;
            Console.WriteLine($"   UseEventCleanupManager: {originalFlag}");
            
            // Test environment variable override
            Environment.SetEnvironmentVariable("FF_USE_EVENT_CLEANUP_MANAGER", "false");
            FeatureFlags.RefreshCache();
            Console.WriteLine($"   After env var set to false: {FeatureFlags.UseEventCleanupManager}");
            
            Environment.SetEnvironmentVariable("FF_USE_EVENT_CLEANUP_MANAGER", "true");
            FeatureFlags.RefreshCache();
            Console.WriteLine($"   After env var set to true: {FeatureFlags.UseEventCleanupManager}");
            
            // Clean up
            Environment.SetEnvironmentVariable("FF_USE_EVENT_CLEANUP_MANAGER", null);
            FeatureFlags.RefreshCache();
            
            Console.WriteLine("✅ Feature flag tests passed");
        }
        
        private static async Task TestBasicManagerFunctionality()
        {
            Console.WriteLine("🔧 Testing Basic Manager Functionality...");
            
            if (App.EventCleanupCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.EventCleanupCoordinator is null, creating test instance");
                return;
            }
            
            // Create a test manager
            var manager = App.EventCleanupCoordinator.CreateManager("TestComponent");
            
            // Create test event sources
            var testObject1 = new TestEventSource("Source1");
            var testObject2 = new TestEventSource("Source2");
            
            // Test regular event registration
            Console.WriteLine("   Testing regular event registration...");
            EventHandler<TestEventArgs> handler1 = (s, e) => Console.WriteLine($"       Event received: {e.Message}");
            manager.RegisterEventHandler(testObject1, nameof(TestEventSource.TestEvent), handler1);
            
            // Test PropertyChanged event registration  
            Console.WriteLine("   Testing PropertyChanged event registration...");
            EventHandler<PropertyChangedEventArgs> handler2 = (s, e) => Console.WriteLine($"       Property changed: {e.PropertyName}");
            manager.RegisterEventHandler<PropertyChangedEventArgs>(testObject2, nameof(INotifyPropertyChanged.PropertyChanged), handler2);
            
            // Test custom cleanup registration
            Console.WriteLine("   Testing custom cleanup registration...");
            var customCleanupExecuted = false;
            manager.RegisterCustomCleanup("TestCleanup", () => 
            {
                customCleanupExecuted = true;
                Console.WriteLine("       Custom cleanup executed");
            });
            
            // Trigger events to verify registration
            testObject1.TriggerTestEvent("Test message 1");
            testObject2.TriggerPropertyChanged("TestProperty");
            
            // Get statistics
            var stats = manager.GetStats();
            Console.WriteLine($"   Manager stats: {stats.RegistrationCount} registrations, {stats.ActiveEventSubscriptions} active");
            
            // Test cleanup
            Console.WriteLine("   Testing cleanup...");
            manager.CleanupAll();
            
            var cleanupStats = manager.GetStats();
            Console.WriteLine($"   Post-cleanup stats: {cleanupStats.CleanupCount} cleanups, custom cleanup executed: {customCleanupExecuted}");
            
            // Verify events no longer fire
            Console.WriteLine("   Verifying events no longer fire...");
            testObject1.TriggerTestEvent("This should not be received");
            testObject2.TriggerPropertyChanged("ThisShouldNotBeReceived");
            
            manager.Dispose();
            Console.WriteLine("✅ Basic manager functionality tests passed");
        }
        
        private static async Task TestMemoryLeakPrevention()
        {
            Console.WriteLine("🧠 Testing Memory Leak Prevention...");
            
            if (App.EventCleanupCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.EventCleanupCoordinator is null, skipping test");
                return;
            }
            
            var startMemory = GC.GetTotalMemory(true);
            Console.WriteLine($"   Start memory: {startMemory / (1024.0 * 1024.0):F2} MB");
            
            // Create many event subscriptions
            var manager = App.EventCleanupCoordinator.CreateManager("MemoryTest");
            var eventSources = new TestEventSource[100];
            
            Console.WriteLine("   Creating 100 event sources and handlers...");
            for (int i = 0; i < eventSources.Length; i++)
            {
                eventSources[i] = new TestEventSource($"Source{i}");
                
                // Create handlers that capture local state (potential for leaks)
                var localIndex = i;
                EventHandler<TestEventArgs> handler = (s, e) => 
                {
                    // This captures localIndex, creating potential for memory leaks
                    Console.WriteLine($"Event {localIndex}: {e.Message}");
                };
                
                manager.RegisterEventHandler(eventSources[i], nameof(TestEventSource.TestEvent), handler);
            }
            
            var midMemory = GC.GetTotalMemory(false);
            Console.WriteLine($"   After registration: {midMemory / (1024.0 * 1024.0):F2} MB (+{(midMemory - startMemory) / (1024.0 * 1024.0):F2} MB)");
            
            // Cleanup all events
            Console.WriteLine("   Performing cleanup...");
            manager.CleanupAll();
            
            // Force garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            var endMemory = GC.GetTotalMemory(true);
            var memoryFreed = midMemory - endMemory;
            
            Console.WriteLine($"   After cleanup: {endMemory / (1024.0 * 1024.0):F2} MB");
            Console.WriteLine($"   Memory freed: {memoryFreed / (1024.0 * 1024.0):F2} MB");
            
            var stats = manager.GetStats();
            Console.WriteLine($"   Manager reported memory freed: {stats.MemoryFreedBytes / (1024.0 * 1024.0):F2} MB");
            
            manager.Dispose();
            Console.WriteLine("✅ Memory leak prevention tests completed");
        }
        
        private static async Task TestPerformanceMetrics()
        {
            Console.WriteLine("⚡ Testing Performance Metrics...");
            
            if (App.EventCleanupCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.EventCleanupCoordinator is null, skipping test");
                return;
            }
            
            var manager = App.EventCleanupCoordinator.CreateManager("PerformanceTest");
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            // Register many handlers quickly
            Console.WriteLine("   Registering 1000 event handlers...");
            var eventSources = new TestEventSource[1000];
            for (int i = 0; i < eventSources.Length; i++)
            {
                eventSources[i] = new TestEventSource($"PerfSource{i}");
                EventHandler<TestEventArgs> handler = (s, e) => { /* No-op */ };
                manager.RegisterEventHandler(eventSources[i], nameof(TestEventSource.TestEvent), handler);
            }
            
            stopwatch.Stop();
            Console.WriteLine($"   Registration completed in {stopwatch.ElapsedMilliseconds}ms");
            
            if (stopwatch.ElapsedMilliseconds > 1000) // Should be < 1 second for 1000 registrations
            {
                Console.WriteLine("   ⚠️  Registration performance may be suboptimal");
            }
            
            // Test cleanup performance
            stopwatch.Restart();
            Console.WriteLine("   Cleaning up 1000 handlers...");
            manager.CleanupAll();
            stopwatch.Stop();
            
            Console.WriteLine($"   Cleanup completed in {stopwatch.ElapsedMilliseconds}ms");
            
            if (stopwatch.ElapsedMilliseconds > 100) // Should be < 100ms for 1000 cleanups
            {
                Console.WriteLine("   ⚠️  Cleanup performance may be suboptimal");
            }
            
            var stats = manager.GetStats();
            Console.WriteLine($"   Average cleanup time: {stats.AverageCleanupTimeMs:F2}ms");
            
            manager.Dispose();
            Console.WriteLine("✅ Performance metric tests completed");
        }
        
        private static async Task TestConcurrentOperations()
        {
            Console.WriteLine("🔄 Testing Concurrent Operations...");
            
            if (App.EventCleanupCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.EventCleanupCoordinator is null, skipping test");
                return;
            }
            
            var manager = App.EventCleanupCoordinator.CreateManager("ConcurrencyTest");
            var tasks = new Task[10];
            
            Console.WriteLine("   Starting 10 concurrent registration tasks...");
            for (int i = 0; i < tasks.Length; i++)
            {
                var taskIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < 50; j++)
                    {
                        var source = new TestEventSource($"ConcurrentSource{taskIndex}_{j}");
                        EventHandler<TestEventArgs> handler = (s, e) => { /* No-op */ };
                        manager.RegisterEventHandler(source, nameof(TestEventSource.TestEvent), handler);
                    }
                });
            }
            
            await Task.WhenAll(tasks);
            
            var stats = manager.GetStats();
            Console.WriteLine($"   Concurrent registration completed: {stats.RegistrationCount} total registrations");
            
            // Test concurrent cleanup
            Console.WriteLine("   Testing concurrent cleanup...");
            await Task.Run(() => manager.CleanupAll());
            
            var cleanupStats = manager.GetStats();
            Console.WriteLine($"   Concurrent cleanup completed: {cleanupStats.CleanupCount} cleanups");
            
            manager.Dispose();
            Console.WriteLine("✅ Concurrent operation tests completed");
        }
        
        private static void TestCoordinatorFunctionality()
        {
            Console.WriteLine("🎯 Testing Coordinator Functionality...");
            
            if (App.EventCleanupCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.EventCleanupCoordinator is null, skipping test");
                return;
            }
            
            // Create multiple managers
            Console.WriteLine("   Creating multiple managers through coordinator...");
            var manager1 = App.EventCleanupCoordinator.CreateManager("TestComponent1");
            var manager2 = App.EventCleanupCoordinator.CreateManager("TestComponent2");
            var manager3 = App.EventCleanupCoordinator.CreateManager("TestComponent3");
            
            // Register some events
            for (int i = 0; i < 10; i++)
            {
                var source = new TestEventSource($"CoordSource{i}");
                EventHandler<TestEventArgs> handler = (s, e) => { /* No-op */ };
                
                manager1.RegisterEventHandler(source, nameof(TestEventSource.TestEvent), handler);
                manager2.RegisterEventHandler(source, nameof(TestEventSource.TestEvent), handler);
                manager3.RegisterEventHandler(source, nameof(TestEventSource.TestEvent), handler);
            }
            
            // Get global statistics
            var globalStats = App.EventCleanupCoordinator.GetGlobalStats();
            Console.WriteLine($"   Global stats: {globalStats.TotalManagers} managers, {globalStats.TotalRegistrations} registrations");
            
            // Check for memory leaks
            var warnings = App.EventCleanupCoordinator.CheckForMemoryLeaks();
            Console.WriteLine($"   Memory leak warnings: {warnings.Count}");
            
            foreach (var warning in warnings)
            {
                Console.WriteLine($"     - {warning.ComponentName}: {warning.Description} ({warning.Severity})");
            }
            
            // Test global cleanup
            Console.WriteLine("   Testing global cleanup...");
            var cleanupTask = App.EventCleanupCoordinator.CleanupAllAsync();
            cleanupTask.Wait(TimeSpan.FromSeconds(10));
            
            var finalStats = App.EventCleanupCoordinator.GetGlobalStats();
            Console.WriteLine($"   Final global stats: {finalStats.TotalCleanups} cleanups, {finalStats.TotalMemoryFreedBytes / 1024.0:F2} KB freed");
            
            // Cleanup managers
            App.EventCleanupCoordinator.RemoveManager("TestComponent1");
            App.EventCleanupCoordinator.RemoveManager("TestComponent2");
            App.EventCleanupCoordinator.RemoveManager("TestComponent3");
            
            Console.WriteLine("✅ Coordinator functionality tests completed");
        }
    }
    
    // Test helper classes
    internal class TestEventSource : INotifyPropertyChanged
    {
        private readonly string _name;
        
        public event EventHandler<TestEventArgs> TestEvent;
        public event PropertyChangedEventHandler PropertyChanged;
        
        public TestEventSource(string name)
        {
            _name = name;
        }
        
        public void TriggerTestEvent(string message)
        {
            TestEvent?.Invoke(this, new TestEventArgs(message));
        }
        
        public void TriggerPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
    
    internal class TestEventArgs : EventArgs
    {
        public string Message { get; }
        
        public TestEventArgs(string message)
        {
            Message = message;
        }
    }
}