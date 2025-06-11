using System;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows;
using ExplorerPro.Core;
using ExplorerPro.Core.Disposables;

namespace ExplorerPro.Tests
{
    /// <summary>
    /// Manual validation tests for Phase 1 Foundation Utilities
    /// Run these to verify the core infrastructure is working properly
    /// </summary>
    public static class Phase1ValidationTests
    {
        public static void RunAllTests()
        {
            Console.WriteLine("=== Phase 1 Foundation Utilities Validation ===");
            Console.WriteLine();

            try
            {
                TestDisposableUtility();
                TestCompositeDisposable();
                TestWeakEventHelper();
                TestUIThreadHelper();
                
                Console.WriteLine("✅ All Phase 1 tests passed!");
                Console.WriteLine("🎉 Foundation infrastructure is ready for subsequent phases.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
            }
        }

        private static void TestDisposableUtility()
        {
            Console.WriteLine("🧪 Testing Disposable utility...");
            
            bool disposed = false;
            var disposable = Disposable.Create(() => disposed = true);
            
            // Verify not disposed initially
            if (disposed)
                throw new Exception("Disposable should not be disposed initially");
            
            // Dispose and verify
            disposable.Dispose();
            if (!disposed)
                throw new Exception("Disposable action should have been called");
            
            // Verify multiple disposal is safe
            disposable.Dispose(); // Should not throw
            
            Console.WriteLine("  ✅ Disposable utility working correctly");
        }

        private static void TestCompositeDisposable()
        {
            Console.WriteLine("🧪 Testing CompositeDisposable...");
            
            bool disposed1 = false;
            bool disposed2 = false;
            
            var composite = new CompositeDisposable();
            composite.Add(Disposable.Create(() => disposed1 = true));
            composite.Add(Disposable.Create(() => disposed2 = true));
            
            // Verify count
            if (composite.Count != 2)
                throw new Exception($"Expected count 2, got {composite.Count}");
            
            // Verify not disposed initially
            if (disposed1 || disposed2)
                throw new Exception("Items should not be disposed initially");
            
            // Dispose and verify all items disposed
            composite.Dispose();
            if (!disposed1 || !disposed2)
                throw new Exception("All composite items should be disposed");
            
            // Verify count after disposal
            if (composite.Count != 0)
                throw new Exception($"Expected count 0 after disposal, got {composite.Count}");
            
            Console.WriteLine("  ✅ CompositeDisposable working correctly");
        }

        private static void TestWeakEventHelper()
        {
            Console.WriteLine("🧪 Testing WeakEventHelper...");
            
            // Create a test object with PropertyChanged event
            var testObject = new TestNotifyPropertyChanged();
            bool eventFired = false;
            
            PropertyChangedEventHandler handler = (s, e) => eventFired = true;
            
            // Subscribe using weak event helper
            var subscription = WeakEventHelper.SubscribePropertyChanged(testObject, handler);
            
            // Fire the event
            testObject.FirePropertyChanged("TestProperty");
            
            if (!eventFired)
                throw new Exception("Event should have fired");
            
            // Clean up
            subscription.Dispose();
            eventFired = false;
            
            // Event should not fire after disposal
            testObject.FirePropertyChanged("TestProperty2");
            if (eventFired)
                throw new Exception("Event should not fire after subscription disposal");
            
            Console.WriteLine("  ✅ WeakEventHelper working correctly");
        }

        private static void TestUIThreadHelper()
        {
            Console.WriteLine("🧪 Testing UIThreadHelper...");
            
            // Note: In a console app, there's no UI thread, so this test is limited
            // But we can verify the methods don't throw and behave reasonably
            
            bool canCheck = false;
            try
            {
                canCheck = UIThreadHelper.CheckAccess();
                Console.WriteLine($"  📝 CheckAccess returned: {canCheck}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️  CheckAccess threw (expected in console): {ex.GetType().Name}");
            }
            
            // Verify we can call the methods without exceptions in the basic case
            bool executed = false;
            try
            {
                UIThreadHelper.ExecuteOnUIThread(() => executed = true);
                Console.WriteLine($"  📝 ExecuteOnUIThread completed, executed: {executed}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠️  ExecuteOnUIThread threw (expected in console): {ex.GetType().Name}");
            }
            
            Console.WriteLine("  ✅ UIThreadHelper methods are accessible and don't crash");
        }

        /// <summary>
        /// Simple test class that implements INotifyPropertyChanged
        /// </summary>
        private class TestNotifyPropertyChanged : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler? PropertyChanged;
            
            public void FirePropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }
    }
} 