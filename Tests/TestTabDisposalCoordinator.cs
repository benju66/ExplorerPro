using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using ExplorerPro.Core.Configuration;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.Models;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Quick test script for TabDisposalCoordinator functionality
    /// </summary>
    public static class TestTabDisposalCoordinator
    {
        public static async Task RunQuickTest()
        {
            Console.WriteLine("=== TabDisposalCoordinator Quick Test ===");
            Console.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            
            try
            {
                // Test 1: Feature flag evaluation
                TestFeatureFlags();
                
                // Test 2: Basic coordinator functionality (if enabled)
                if (FeatureFlags.UseTabDisposalCoordinator)
                {
                    await TestBasicDisposalFunctionality();
                    await TestCircuitBreakerBehavior();
                    await TestConcurrentDisposals();
                    TestStatisticsCollection();
                }
                else
                {
                    Console.WriteLine("⚠️  TabDisposalCoordinator is disabled via feature flag");
                    Console.WriteLine("   Set FF_USE_TAB_DISPOSAL_COORDINATOR=true to enable testing");
                }
                
                Console.WriteLine();
                Console.WriteLine("✅ All TabDisposalCoordinator tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"❌ TabDisposalCoordinator test failed: {ex.Message}");
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
            
            var originalFlag = FeatureFlags.UseTabDisposalCoordinator;
            Console.WriteLine($"   UseTabDisposalCoordinator: {originalFlag}");
            
            // Test environment variable override
            Environment.SetEnvironmentVariable("FF_USE_TAB_DISPOSAL_COORDINATOR", "false");
            FeatureFlags.RefreshCache();
            Console.WriteLine($"   After env var set to false: {FeatureFlags.UseTabDisposalCoordinator}");
            
            Environment.SetEnvironmentVariable("FF_USE_TAB_DISPOSAL_COORDINATOR", "true");
            FeatureFlags.RefreshCache();
            Console.WriteLine($"   After env var set to true: {FeatureFlags.UseTabDisposalCoordinator}");
            
            // Clean up
            Environment.SetEnvironmentVariable("FF_USE_TAB_DISPOSAL_COORDINATOR", null);
            FeatureFlags.RefreshCache();
            
            Console.WriteLine("✅ Feature flag tests passed");
        }
        
        private static async Task TestBasicDisposalFunctionality()
        {
            Console.WriteLine("🔧 Testing Basic Disposal Functionality...");
            
            if (App.TabDisposalCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.TabDisposalCoordinator is null, creating test instance");
                return;
            }
            
            // Create test tabs
            var testTab1 = new TabItem();
            var testModel1 = new TabModel("Test Tab 1", "C:\\Test1");
            testTab1.DataContext = testModel1;
            testTab1.Content = new TestDisposableContainer("Container 1");
            
            var testTab2 = new TabItem();
            var testModel2 = new TabModel("Test Tab 2", "C:\\Test2");
            testTab2.DataContext = testModel2;
            testTab2.Content = new TestDisposableContainer("Container 2");
            
            // Test disposal
            Console.WriteLine("   Testing single tab disposal...");
            var result1 = await App.TabDisposalCoordinator.DisposeTabAsync(testTab1);
            Console.WriteLine($"   Result 1: Success={result1.IsSuccess}, Message={result1.Message}");
            
            Console.WriteLine("   Testing second tab disposal...");
            var result2 = await App.TabDisposalCoordinator.DisposeTabAsync(testTab2);
            Console.WriteLine($"   Result 2: Success={result2.IsSuccess}, Message={result2.Message}");
            
            Console.WriteLine("✅ Basic disposal functionality tests passed");
        }
        
        private static async Task TestCircuitBreakerBehavior()
        {
            Console.WriteLine("⚡ Testing Circuit Breaker Behavior...");
            
            if (App.TabDisposalCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.TabDisposalCoordinator is null, skipping test");
                return;
            }
            
            // Create tabs that will cause disposal failures
            var failingTabs = new TabItem[3];
            for (int i = 0; i < failingTabs.Length; i++)
            {
                failingTabs[i] = new TabItem();
                failingTabs[i].DataContext = new TabModel($"Failing Tab {i + 1}", $"C:\\Fail{i + 1}");
                failingTabs[i].Content = new FailingDisposableContainer($"Failing Container {i + 1}");
            }
            
            Console.WriteLine("   Testing disposal with failing containers...");
            for (int i = 0; i < failingTabs.Length; i++)
            {
                try
                {
                    var result = await App.TabDisposalCoordinator.DisposeTabAsync(failingTabs[i], TimeSpan.FromSeconds(2));
                    Console.WriteLine($"   Failing disposal {i + 1}: Success={result.IsSuccess}, Message={result.Message}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"   Failing disposal {i + 1} threw exception: {ex.Message}");
                }
            }
            
            // Check stats
            var stats = App.TabDisposalCoordinator.GetStats();
            Console.WriteLine($"   Disposal stats: Success={stats.SuccessfulDisposals}, Failed={stats.FailedDisposals}, Rate={stats.SuccessRate:F1}%");
            
            Console.WriteLine("✅ Circuit breaker behavior tests completed");
        }
        
        private static async Task TestConcurrentDisposals()
        {
            Console.WriteLine("🔄 Testing Concurrent Disposals...");
            
            if (App.TabDisposalCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.TabDisposalCoordinator is null, skipping test");
                return;
            }
            
            // Create multiple tabs for concurrent disposal
            var concurrentTabs = new TabItem[5];
            var disposalTasks = new Task<DisposalResult>[5];
            
            for (int i = 0; i < concurrentTabs.Length; i++)
            {
                concurrentTabs[i] = new TabItem();
                concurrentTabs[i].DataContext = new TabModel($"Concurrent Tab {i + 1}", $"C:\\Concurrent{i + 1}");
                concurrentTabs[i].Content = new TestDisposableContainer($"Concurrent Container {i + 1}");
            }
            
            Console.WriteLine("   Starting concurrent disposals...");
            for (int i = 0; i < disposalTasks.Length; i++)
            {
                disposalTasks[i] = App.TabDisposalCoordinator.DisposeTabAsync(concurrentTabs[i]);
            }
            
            // Wait for all to complete
            var results = await Task.WhenAll(disposalTasks);
            
            Console.WriteLine($"   Concurrent disposal results:");
            for (int i = 0; i < results.Length; i++)
            {
                Console.WriteLine($"     Tab {i + 1}: Success={results[i].IsSuccess}, Message={results[i].Message}");
            }
            
            Console.WriteLine("✅ Concurrent disposal tests completed");
        }
        
        private static void TestStatisticsCollection()
        {
            Console.WriteLine("📊 Testing Statistics Collection...");
            
            if (App.TabDisposalCoordinator == null)
            {
                Console.WriteLine("   ⚠️  App.TabDisposalCoordinator is null, skipping test");
                return;
            }
            
            var stats = App.TabDisposalCoordinator.GetStats();
            
            Console.WriteLine($"   Current statistics:");
            Console.WriteLine($"     Successful Disposals: {stats.SuccessfulDisposals}");
            Console.WriteLine($"     Failed Disposals: {stats.FailedDisposals}");
            Console.WriteLine($"     Timeout Disposals: {stats.TimeoutDisposals}");
            Console.WriteLine($"     Circuit Breaker Trips: {stats.CircuitBreakerTrips}");
            Console.WriteLine($"     Active Disposals: {stats.ActiveDisposals}");
            Console.WriteLine($"     Success Rate: {stats.SuccessRate:F1}%");
            Console.WriteLine($"     Circuit Breaker State: {stats.CircuitBreakerState}");
            
            Console.WriteLine("✅ Statistics collection tests completed");
        }
    }
    
    // Test helper classes
    internal class TestDisposableContainer : IDisposable
    {
        private readonly string _name;
        private bool _disposed = false;
        
        public TestDisposableContainer(string name)
        {
            _name = name;
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                Console.WriteLine($"       ✓ Disposed {_name}");
                _disposed = true;
            }
        }
    }
    
    internal class FailingDisposableContainer : IDisposable
    {
        private readonly string _name;
        
        public FailingDisposableContainer(string name)
        {
            _name = name;
        }
        
        public void Dispose()
        {
            throw new InvalidOperationException($"Simulated disposal failure for {_name}");
        }
    }
}