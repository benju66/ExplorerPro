using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExplorerPro.Core.TabManagement;
using ExplorerPro.Core.Threading;
using ExplorerPro.Core.Services;
using ExplorerPro.ViewModels;
using ExplorerPro.Models;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Integration tests for the TIER 2B service integration and threading safety implementation.
    /// Demonstrates the proper usage of the new service architecture.
    /// </summary>
    public class ServiceIntegrationTests
    {
        /// <summary>
        /// Tests basic service integration and initialization
        /// </summary>
        public static async Task<bool> TestBasicServiceIntegrationAsync()
        {
            try
            {
                Console.WriteLine("=== Testing Basic Service Integration ===");
                
                // Create service collection
                var services = new ServiceCollection();
                services.AddLogging(builder => builder.AddConsole());
                services.AddScoped<ITabManagerService, TabManagerService>();
                services.AddScoped<MainWindowTabsViewModel>();
                
                var serviceProvider = services.BuildServiceProvider();
                var factoryLogger = serviceProvider.GetService<ILogger<TabServicesFactory>>();
                var factory = new TabServicesFactory(serviceProvider, factoryLogger);
                var (tabManager, tabsViewModel) = factory.CreateTabSystem();
                var threadSafeOps = new ThreadSafeTabOperations(tabManager);
                
                // Verify services are initialized
                if (tabManager == null)
                {
                    Console.WriteLine("❌ TabManagerService not initialized");
                    return false;
                }
                
                if (threadSafeOps == null)
                {
                    Console.WriteLine("❌ ThreadSafeOperations not initialized");
                    return false;
                }
                
                if (tabsViewModel == null)
                {
                    Console.WriteLine("❌ TabsViewModel not initialized");
                    return false;
                }
                
                Console.WriteLine("✅ All services initialized successfully");
                
                // Test basic functionality
                var tabModel = await threadSafeOps.CreateTabSafeAsync(
                    "Test Tab", @"C:\\Test\\Path", null, CancellationToken.None);
                
                if (tabModel == null)
                {
                    Console.WriteLine("❌ Failed to create tab");
                    return false;
                }
                
                Console.WriteLine($"✅ Created tab: {tabModel.Title}");
                
                // Cleanup
                threadSafeOps.Dispose();
                serviceProvider.Dispose();
                
                Console.WriteLine("✅ Basic service integration test passed");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Basic service integration test failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Tests thread-safe operations under concurrent load
        /// </summary>
        public static async Task<bool> TestThreadSafeOperationsAsync()
        {
            try
            {
                Console.WriteLine("=== Testing Thread-Safe Operations ===");
                
                // Create services
                var services = new ServiceCollection();
                services.AddLogging(builder => builder.AddConsole());
                services.AddScoped<ITabManagerService, TabManagerService>();
                
                var serviceProvider = services.BuildServiceProvider();
                var tabManager = serviceProvider.GetService<ITabManagerService>();
                var logger = serviceProvider.GetService<ILogger<ThreadSafeTabOperations>>();
                
                var threadSafeOps = new ThreadSafeTabOperations(tabManager);
                
                // Test concurrent tab creation
                var cancellationToken = CancellationToken.None;
                var tasks = new Task<TabModel>[10];
                
                for (int i = 0; i < 10; i++)
                {
                    var index = i;
                    tasks[i] = threadSafeOps.CreateTabSafeAsync(
                        $"Concurrent Tab {index}", 
                        $@"C:\Test\Path{index}", 
                        null, 
                        cancellationToken);
                }
                
                var results = await Task.WhenAll(tasks);
                
                // Verify all tabs were created
                if (results.Length != 10)
                {
                    Console.WriteLine($"❌ Expected 10 tabs, got {results.Length}");
                    return false;
                }
                
                foreach (var tab in results)
                {
                    if (tab == null)
                    {
                        Console.WriteLine("❌ Null tab found in results");
                        return false;
                    }
                }
                
                Console.WriteLine($"✅ Created {results.Length} tabs concurrently");
                
                // Test pending operations tracking
                var pendingCount = threadSafeOps.GetPendingOperationCount();
                Console.WriteLine($"✅ Pending operations: {pendingCount}");
                
                // Cleanup
                threadSafeOps.Dispose();
                serviceProvider.Dispose();
                
                Console.WriteLine("✅ Thread-safe operations test passed");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Thread-safe operations test failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Tests service health monitoring
        /// </summary>
        public static async Task<bool> TestServiceHealthMonitoringAsync()
        {
            try
            {
                Console.WriteLine("=== Testing Service Health Monitoring ===");
                
                // Create services
                var services = new ServiceCollection();
                services.AddLogging(builder => builder.AddConsole());
                services.AddScoped<ITabManagerService, TabManagerService>();
                
                var serviceProvider = services.BuildServiceProvider();
                var factoryLogger = serviceProvider.GetService<ILogger<TabServicesFactory>>();
                var factory = new TabServicesFactory(serviceProvider, factoryLogger);
                var (service, viewModel) = factory.CreateTabSystem();
                var monitor = new ServiceHealthMonitor();
                
                // Register services to monitor
                monitor.RegisterService("TabManagerService", service);
                monitor.RegisterService("MainWindowTabsViewModel", viewModel);
                
                // Give health monitor time to perform initial checks
                await Task.Delay(100);
                
                // Get health status
                var healthInfos = monitor.GetAllServiceHealth();
                
                if (healthInfos == null || healthInfos.Length == 0)
                {
                    Console.WriteLine("❌ No health information available");
                    return false;
                }
                
                Console.WriteLine($"✅ Monitoring {healthInfos.Length} services");
                
                foreach (var health in healthInfos)
                {
                    var status = health.IsHealthy ? "✅ Healthy" : "❌ Unhealthy";
                    Console.WriteLine($"  {health.ServiceName}: {status}");
                }
                
                // Cleanup
                monitor.Dispose();
                serviceProvider.Dispose();
                
                Console.WriteLine("✅ Service health monitoring test passed");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Service health monitoring test failed: {ex.Message}");
                return false;
            }
        }
        
        /// <summary>
        /// Runs all service integration tests
        /// </summary>
        public static async Task<bool> RunAllTestsAsync()
        {
            Console.WriteLine("=======================================================");
            Console.WriteLine("  TIER 2B: Service Integration & Threading Safety Tests");
            Console.WriteLine("=======================================================");
            
            var results = new bool[3];
            
            results[0] = await TestBasicServiceIntegrationAsync();
            Console.WriteLine();
            
            results[1] = await TestThreadSafeOperationsAsync();
            Console.WriteLine();
            
            results[2] = await TestServiceHealthMonitoringAsync();
            Console.WriteLine();
            
            var passedCount = 0;
            foreach (var result in results)
            {
                if (result) passedCount++;
            }
            
            Console.WriteLine("=======================================================");
            Console.WriteLine($"  Test Results: {passedCount}/{results.Length} PASSED");
            Console.WriteLine("=======================================================");
            
            return passedCount == results.Length;
        }
    }
} 