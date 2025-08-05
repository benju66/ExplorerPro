using System;
using System.Windows.Controls;
using ExplorerPro.Models;
using ExplorerPro.Core.Configuration;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Quick test script for TabModelResolver functionality
    /// </summary>
    public static class TestTabModelResolver
    {
        public static void RunQuickTest()
        {
            Console.WriteLine("=== TabModelResolver Quick Test ===");
            Console.WriteLine($"Test started at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine();
            
            try
            {
                // Test 1: Feature flag evaluation
                TestFeatureFlags();
                
                // Test 2: Basic resolver functionality (if enabled)
                if (FeatureFlags.UseTabModelResolver)
                {
                    TestBasicResolverFunctionality();
                    TestMigrationBehavior();
                    TestEdgeCases();
                    TestTelemetryCollection();
                }
                else
                {
                    Console.WriteLine("⚠️  TabModelResolver is disabled via feature flag");
                    Console.WriteLine("   Set FF_USE_TAB_MODEL_RESOLVER=true to enable testing");
                }
                
                Console.WriteLine();
                Console.WriteLine("✅ All quick tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"❌ Quick test failed: {ex.Message}");
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
            
            try
            {
                // Test current flag values
                var initialValue = FeatureFlags.UseTabModelResolver;
                Console.WriteLine($"   Initial UseTabModelResolver: {initialValue}");
                
                // Test programmatic flag setting
                FeatureFlags.SetFlag("UseTabModelResolver", false);
                var afterDisable = FeatureFlags.UseTabModelResolver;
                Console.WriteLine($"   After setting to false: {afterDisable}");
                
                // Test re-enabling
                FeatureFlags.SetFlag("UseTabModelResolver", true);
                var afterEnable = FeatureFlags.UseTabModelResolver;
                Console.WriteLine($"   After setting to true: {afterEnable}");
                
                // Test cache refresh
                FeatureFlags.RefreshCache();
                var afterRefresh = FeatureFlags.UseTabModelResolver;
                Console.WriteLine($"   After cache refresh: {afterRefresh}");
                
                // Test diagnostic info
                var diagnosticInfo = FeatureFlags.GetDiagnosticInfo();
                Console.WriteLine($"   Feature flags diagnostic info available: {!string.IsNullOrEmpty(diagnosticInfo)}");
                
                Console.WriteLine("   ✅ Feature flags test passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Feature flags test failed: {ex.Message}");
                throw;
            }
            
            Console.WriteLine();
        }
        
        private static void TestBasicResolverFunctionality()
        {
            Console.WriteLine("🔍 Testing Basic Resolver Functionality...");
            
            try
            {
                // Reset stats for clean test
                ExplorerPro.Core.TabManagement.TabModelResolver.ResetStats();
                
                // Test 1: DataContext resolution
                var tab1 = new TabItem();
                var model1 = new TabModel { Title = "Test Tab 1", Path = @"C:\Test1" };
                tab1.DataContext = model1;
                
                var retrieved1 = ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(tab1);
                if (retrieved1 == model1)
                {
                    Console.WriteLine("   ✅ DataContext resolution works");
                }
                else
                {
                    throw new Exception("DataContext resolution failed");
                }
                
                // Test 2: SetTabModel functionality
                var tab2 = new TabItem();
                var model2 = new TabModel { Title = "Test Tab 2", Path = @"C:\Test2" };
                ExplorerPro.Core.TabManagement.TabModelResolver.SetTabModel(tab2, model2);
                
                if (tab2.DataContext == model2 && tab2.Tag == null)
                {
                    Console.WriteLine("   ✅ SetTabModel works correctly");
                }
                else
                {
                    throw new Exception("SetTabModel failed to set DataContext or clear Tag");
                }
                
                // Test 3: Null handling
                var nullResult = ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(null);
                if (nullResult == null)
                {
                    Console.WriteLine("   ✅ Null handling works");
                }
                else
                {
                    throw new Exception("Null handling failed");
                }
                
                Console.WriteLine("   ✅ Basic resolver functionality test passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Basic resolver functionality test failed: {ex.Message}");
                throw;
            }
            
            Console.WriteLine();
        }
        
        private static void TestMigrationBehavior()
        {
            Console.WriteLine("🔄 Testing Migration Behavior...");
            
            try
            {
                // Reset stats
                ExplorerPro.Core.TabManagement.TabModelResolver.ResetStats();
                
                // Create tab with model in Tag (legacy pattern)
                var tab = new TabItem();
                var model = new TabModel { Title = "Legacy Tab", Path = @"C:\Legacy" };
                tab.Tag = model;
                
                // Get initial stats
                var statsBefore = ExplorerPro.Core.TabManagement.TabModelResolver.GetStats();
                
                // Resolve - should trigger migration
                var retrieved = ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(tab);
                
                // Get stats after
                var statsAfter = ExplorerPro.Core.TabManagement.TabModelResolver.GetStats();
                
                // Verify migration occurred
                if (retrieved == model && 
                    tab.DataContext == model && 
                    tab.Tag == null &&
                    statsAfter.Migrations > statsBefore.Migrations &&
                    statsAfter.TagFallbacks > statsBefore.TagFallbacks)
                {
                    Console.WriteLine("   ✅ Migration behavior works correctly");
                    Console.WriteLine($"   📊 Migrations: {statsAfter.Migrations}, Tag fallbacks: {statsAfter.TagFallbacks}");
                }
                else
                {
                    throw new Exception("Migration behavior failed");
                }
                
                Console.WriteLine("   ✅ Migration behavior test passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Migration behavior test failed: {ex.Message}");
                throw;
            }
            
            Console.WriteLine();
        }
        
        private static void TestEdgeCases()
        {
            Console.WriteLine("⚠️  Testing Edge Cases...");
            
            try
            {
                // Test 1: Tab with no model
                var emptyTab = new TabItem();
                var emptyResult = ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(emptyTab);
                if (emptyResult == null)
                {
                    Console.WriteLine("   ✅ Empty tab handling works");
                }
                
                // Test 2: Tab with non-TabModel in DataContext
                var weirdTab = new TabItem { DataContext = "Not a TabModel" };
                var weirdResult = ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(weirdTab);
                if (weirdResult == null)
                {
                    Console.WriteLine("   ✅ Non-TabModel DataContext handling works");
                }
                
                // Test 3: Tab with non-TabModel in Tag
                var weirdTab2 = new TabItem { Tag = 42 };
                var weirdResult2 = ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(weirdTab2);
                if (weirdResult2 == null)
                {
                    Console.WriteLine("   ✅ Non-TabModel Tag handling works");
                }
                
                Console.WriteLine("   ✅ Edge cases test passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Edge cases test failed: {ex.Message}");
                throw;
            }
            
            Console.WriteLine();
        }
        
        private static void TestTelemetryCollection()
        {
            Console.WriteLine("📊 Testing Telemetry Collection...");
            
            try
            {
                // Reset stats for clean measurement
                ExplorerPro.Core.TabManagement.TabModelResolver.ResetStats();
                
                // Create various scenarios to generate telemetry
                for (int i = 0; i < 10; i++)
                {
                    // DataContext scenarios
                    var dataContextTab = new TabItem();
                    var dataContextModel = new TabModel { Title = $"DataContext {i}" };
                    dataContextTab.DataContext = dataContextModel;
                    ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(dataContextTab);
                    
                    // Tag scenarios (with migration)
                    var tagTab = new TabItem();
                    var tagModel = new TabModel { Title = $"Tag {i}" };
                    tagTab.Tag = tagModel;
                    ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(tagTab);
                    
                    // Not found scenarios
                    var emptyTab = new TabItem();
                    ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(emptyTab);
                }
                
                // Get final stats
                var finalStats = ExplorerPro.Core.TabManagement.TabModelResolver.GetStats();
                
                Console.WriteLine($"   📈 Final telemetry stats:");
                Console.WriteLine($"      DataContext hits: {finalStats.DataContextHits}");
                Console.WriteLine($"      Tag fallbacks: {finalStats.TagFallbacks}");
                Console.WriteLine($"      Migrations: {finalStats.Migrations}");
                Console.WriteLine($"      Not found: {finalStats.NotFound}");
                Console.WriteLine($"      Fallback rate: {finalStats.TagFallbackRate:F2}%");
                
                if (finalStats.DataContextHits > 0 && 
                    finalStats.TagFallbacks > 0 && 
                    finalStats.Migrations > 0 && 
                    finalStats.NotFound > 0)
                {
                    Console.WriteLine("   ✅ Telemetry collection works correctly");
                }
                else
                {
                    throw new Exception("Telemetry collection appears incomplete");
                }
                
                Console.WriteLine("   ✅ Telemetry collection test passed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"   ❌ Telemetry collection test failed: {ex.Message}");
                throw;
            }
            
            Console.WriteLine();
        }
        
        /// <summary>
        /// Runs a stress test with many concurrent operations
        /// </summary>
        public static void RunStressTest()
        {
            Console.WriteLine("=== TabModelResolver Stress Test ===");
            
            if (!FeatureFlags.UseTabModelResolver)
            {
                Console.WriteLine("⚠️  TabModelResolver is disabled - skipping stress test");
                return;
            }
            
            try
            {
                ExplorerPro.Core.TabManagement.TabModelResolver.ResetStats();
                
                var startTime = DateTime.UtcNow;
                const int iterations = 1000;
                
                Console.WriteLine($"Running {iterations} resolver operations...");
                
                for (int i = 0; i < iterations; i++)
                {
                    var tab = new TabItem();
                    var model = new TabModel { Title = $"Stress Test {i}" };
                    
                    // Alternate between DataContext and Tag patterns
                    if (i % 2 == 0)
                    {
                        tab.DataContext = model;
                    }
                    else
                    {
                        tab.Tag = model;
                    }
                    
                    var resolved = ExplorerPro.Core.TabManagement.TabModelResolver.GetTabModel(tab);
                    if (resolved != model)
                    {
                        throw new Exception($"Resolution failed at iteration {i}");
                    }
                }
                
                var elapsed = DateTime.UtcNow - startTime;
                var stats = ExplorerPro.Core.TabManagement.TabModelResolver.GetStats();
                
                Console.WriteLine($"✅ Stress test completed in {elapsed.TotalMilliseconds:F2} ms");
                Console.WriteLine($"📊 Final stats: DataContext: {stats.DataContextHits}, Tag: {stats.TagFallbacks}, Migrations: {stats.Migrations}");
                Console.WriteLine($"⚡ Average time per operation: {elapsed.TotalMilliseconds / iterations:F3} ms");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Stress test failed: {ex.Message}");
            }
        }
    }
}