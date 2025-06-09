using System;
using System.Collections.Generic;
using System.Threading;
using ExplorerPro.UI.MainWindow;

namespace ExplorerPro.Core
{
    /// <summary>
    /// Context object that tracks the state and progress of window initialization.
    /// Provides a centralized way to manage initialization data, state transitions, and cleanup.
    /// </summary>
    public class WindowInitializationContext
    {
        /// <summary>
        /// The MainWindow instance being initialized.
        /// </summary>
        public MainWindow Window { get; }
        
        /// <summary>
        /// Current initialization state of the window.
        /// </summary>
        public InitializationState CurrentState { get; set; }
        
        /// <summary>
        /// Dictionary of properties that can be used to pass data between initialization steps.
        /// </summary>
        public Dictionary<string, object> Properties { get; } = new();
        
        /// <summary>
        /// List of completed initialization steps for tracking and rollback purposes.
        /// </summary>
        public List<string> CompletedSteps { get; } = new();
        
        /// <summary>
        /// Cancellation token for aborting initialization if needed.
        /// </summary>
        public CancellationToken CancellationToken { get; set; }
        
        /// <summary>
        /// Timestamp when initialization was started.
        /// </summary>
        public DateTime StartTime { get; }
        
        /// <summary>
        /// Initializes a new instance of the WindowInitializationContext.
        /// </summary>
        /// <param name="window">The MainWindow instance to initialize</param>
        /// <exception cref="ArgumentNullException">Thrown when window is null</exception>
        public WindowInitializationContext(MainWindow window)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            CurrentState = InitializationState.Created;
            StartTime = DateTime.UtcNow;
            CancellationToken = CancellationToken.None;
        }
        
        /// <summary>
        /// Initializes a new instance of the WindowInitializationContext with a cancellation token.
        /// </summary>
        /// <param name="window">The MainWindow instance to initialize</param>
        /// <param name="cancellationToken">Token for cancelling initialization</param>
        /// <exception cref="ArgumentNullException">Thrown when window is null</exception>
        public WindowInitializationContext(MainWindow window, CancellationToken cancellationToken)
        {
            Window = window ?? throw new ArgumentNullException(nameof(window));
            CurrentState = InitializationState.Created;
            StartTime = DateTime.UtcNow;
            CancellationToken = cancellationToken;
        }
        
        /// <summary>
        /// Adds a property to the context.
        /// </summary>
        /// <param name="key">Property key</param>
        /// <param name="value">Property value</param>
        public void SetProperty(string key, object value)
        {
            Properties[key] = value;
        }
        
        /// <summary>
        /// Gets a property from the context.
        /// </summary>
        /// <typeparam name="T">Type of the property</typeparam>
        /// <param name="key">Property key</param>
        /// <returns>The property value, or default(T) if not found</returns>
        public T GetProperty<T>(string key)
        {
            return Properties.TryGetValue(key, out var value) && value is T typedValue ? typedValue : default(T);
        }
        
        /// <summary>
        /// Marks an initialization step as completed.
        /// </summary>
        /// <param name="stepName">Name of the completed step</param>
        public void MarkStepCompleted(string stepName)
        {
            if (!CompletedSteps.Contains(stepName))
            {
                CompletedSteps.Add(stepName);
            }
        }
        
        /// <summary>
        /// Checks if a specific step has been completed.
        /// </summary>
        /// <param name="stepName">Name of the step to check</param>
        /// <returns>True if the step has been completed</returns>
        public bool IsStepCompleted(string stepName)
        {
            return CompletedSteps.Contains(stepName);
        }
        
        /// <summary>
        /// Gets the elapsed time since initialization started.
        /// </summary>
        public TimeSpan ElapsedTime => DateTime.UtcNow - StartTime;
    }
} 