using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using ExplorerPro.Core;

namespace ExplorerPro.Core.Threading
{
    /// <summary>
    /// Extension methods for thread-safe operations
    /// Phase 6: Thread Safety Standardization
    /// </summary>
    public static class ThreadSafeExtensions
    {
        /// <summary>
        /// Sets a property value in a thread-safe manner with property change notification
        /// </summary>
        public static void SetPropertyThreadSafe<T>(
            this INotifyPropertyChanged source,
            T currentValue,
            T newValue,
            Action<T> setField,
            Action<string> raisePropertyChanged,
            [CallerMemberName] string propertyName = "")
        {
            if (Equals(currentValue, newValue)) return;

            UIThreadHelper.ExecuteOnUIThread(() =>
            {
                setField(newValue);
                raisePropertyChanged(propertyName);
            });
        }

        /// <summary>
        /// Updates a dependency property in a thread-safe manner
        /// </summary>
        public static void UpdateUIProperty<T>(
            this DependencyObject obj,
            DependencyProperty property,
            T value)
        {
            if (obj == null) return;

            UIThreadHelper.ExecuteOnUIThread(() =>
            {
                ThreadSafetyValidator.ValidateDependencyObjectAccess(obj);
                obj.SetValue(property, value);
            });
        }

        /// <summary>
        /// Gets a dependency property value in a thread-safe manner
        /// </summary>
        public static T GetUIProperty<T>(
            this DependencyObject obj,
            DependencyProperty property)
        {
            if (obj == null) return default(T);

            return UIThreadHelper.ExecuteOnUIThread(() =>
            {
                ThreadSafetyValidator.ValidateDependencyObjectAccess(obj);
                return (T)obj.GetValue(property);
            });
        }

        /// <summary>
        /// Executes an action on the UI thread with thread safety validation
        /// </summary>
        public static void ExecuteOnUIThreadSafe(this DependencyObject obj, Action action)
        {
            if (obj == null || action == null) return;

            UIThreadHelper.ExecuteOnUIThread(() =>
            {
                ThreadSafetyValidator.ValidateDependencyObjectAccess(obj);
                action();
            });
        }

        /// <summary>
        /// Executes an async action on the UI thread with thread safety validation
        /// </summary>
        public static async Task ExecuteOnUIThreadSafeAsync(this DependencyObject obj, Func<Task> asyncAction)
        {
            if (obj == null || asyncAction == null) return;

            await UIThreadHelper.ExecuteOnUIThreadAsync(async () =>
            {
                ThreadSafetyValidator.ValidateDependencyObjectAccess(obj);
                await asyncAction();
            });
        }

        /// <summary>
        /// Safely updates a collection on the UI thread
        /// </summary>
        public static void UpdateCollectionSafe<T>(
            this System.Collections.ObjectModel.ObservableCollection<T> collection,
            Action<System.Collections.ObjectModel.ObservableCollection<T>> updateAction)
        {
            if (collection == null || updateAction == null) return;

            UIThreadHelper.ExecuteOnUIThread(() =>
            {
                ThreadSafetyValidator.AssertUIThread();
                updateAction(collection);
            });
        }

        /// <summary>
        /// Safely clears and adds items to a collection
        /// </summary>
        public static void ReplaceItemsSafe<T>(
            this System.Collections.ObjectModel.ObservableCollection<T> collection,
            System.Collections.Generic.IEnumerable<T> newItems)
        {
            if (collection == null) return;

            UIThreadHelper.ExecuteOnUIThread(() =>
            {
                ThreadSafetyValidator.AssertUIThread();
                collection.Clear();
                if (newItems != null)
                {
                    foreach (var item in newItems)
                    {
                        collection.Add(item);
                    }
                }
            });
        }

        /// <summary>
        /// Validates and executes a UI operation with proper error handling
        /// </summary>
        public static void SafeUIOperation(this DependencyObject obj, Action operation, string operationName = "UI Operation")
        {
            if (obj == null || operation == null) return;

            try
            {
                ThreadSafetyValidator.LogThreadContext(operationName);
                ThreadSafetyValidator.TrackThreadSafety(() =>
                {
                    UIThreadHelper.ExecuteOnUIThread(() =>
                    {
                        ThreadSafetyValidator.ValidateDependencyObjectAccess(obj);
                        operation();
                    });
                }, operationName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Safe UI operation failed in {operationName}: {ex.Message}");
                throw;
            }
        }
    }
} 