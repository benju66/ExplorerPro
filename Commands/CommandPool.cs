using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Windows.Input;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Commands
{
    /// <summary>
    /// Provides a centralized pool of reusable RoutedCommand instances to prevent memory leaks
    /// from repeatedly creating the same commands across multiple windows.
    /// IMPLEMENTS FIX 6: Command Binding Memory Overhead
    /// </summary>
    public static class CommandPool
    {
        private static readonly ConcurrentDictionary<string, RoutedCommand> _commands = new();
        private static readonly object _lock = new object();
        private static readonly ILogger _logger = CreateLogger();
        
        /// <summary>
        /// Statistics for monitoring the command pool
        /// </summary>
        public static class Statistics
        {
            public static int TotalCommands => _commands.Count;
            public static int TotalRequestCount { get; private set; }
            public static int CacheHitCount { get; private set; }
            public static int CacheMissCount { get; private set; }
            
            internal static void RecordRequest(bool isHit)
            {
                TotalRequestCount++;
                if (isHit)
                    CacheHitCount++;
                else
                    CacheMissCount++;
            }
            
            public static double CacheHitRate => TotalRequestCount > 0 ? (double)CacheHitCount / TotalRequestCount : 0.0;
            
            public static void Reset()
            {
                TotalRequestCount = 0;
                CacheHitCount = 0;
                CacheMissCount = 0;
            }
        }
        
        private static ILogger CreateLogger()
        {
            try
            {
                return ExplorerPro.UI.MainWindow.MainWindow.SharedLoggerFactory?.CreateLogger(nameof(CommandPool)) 
                    ?? Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            }
            catch
            {
                return Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
            }
        }
        
        /// <summary>
        /// Gets or creates a reusable RoutedCommand instance for the specified parameters.
        /// Commands are cached and reused to prevent memory leaks.
        /// </summary>
        /// <param name="name">Unique name for the command</param>
        /// <param name="key">Primary key for the shortcut</param>
        /// <param name="modifiers">Modifier keys (default: None)</param>
        /// <param name="ownerType">Owner type for the command (default: typeof(System.Windows.Window))</param>
        /// <returns>A reusable RoutedCommand instance</returns>
        public static RoutedCommand GetCommand(string name, Key key, ModifierKeys modifiers = ModifierKeys.None, Type ownerType = null)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("Command name cannot be null or empty", nameof(name));
            
            ownerType = ownerType ?? typeof(System.Windows.Window);
            var commandKey = CreateCommandKey(name, key, modifiers, ownerType);
            
            bool isHit = _commands.TryGetValue(commandKey, out var command);
            Statistics.RecordRequest(isHit);
            
            if (isHit && command != null)
            {
                _logger.LogTrace("Command cache hit: {CommandKey}", commandKey);
                return command;
            }
            
            // Cache miss - create new command
            lock (_lock)
            {
                // Double-check pattern to prevent race conditions
                if (_commands.TryGetValue(commandKey, out command))
                {
                    _logger.LogTrace("Command cache hit on second check: {CommandKey}", commandKey);
                    return command;
                }
                
                try
                {
                    var gestures = new InputGestureCollection();
                    if (key != Key.None)
                    {
                        gestures.Add(new KeyGesture(key, modifiers));
                    }
                    
                    command = new RoutedCommand(name, ownerType, gestures);
                    _commands[commandKey] = command;
                    
                    _logger.LogDebug("Created new command: {CommandKey} (Pool size: {PoolSize})", 
                        commandKey, _commands.Count);
                    
                    return command;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to create command: {CommandKey}", commandKey);
                    
                    // Return a basic command without gestures as fallback
                    command = new RoutedCommand(name, ownerType);
                    _commands[commandKey] = command;
                    return command;
                }
            }
        }
        
        /// <summary>
        /// Creates a unique key for caching commands.
        /// </summary>
        private static string CreateCommandKey(string name, Key key, ModifierKeys modifiers, Type ownerType)
        {
            return $"{name}_{key}_{modifiers}_{ownerType.FullName}";
        }
        
        /// <summary>
        /// Clears all cached commands. Should only be used during application shutdown
        /// or for testing purposes.
        /// </summary>
        public static void Clear()
        {
            lock (_lock)
            {
                var count = _commands.Count;
                _commands.Clear();
                Statistics.Reset();
                
                _logger.LogInformation("Cleared command pool ({Count} commands removed)", count);
            }
        }
        
        /// <summary>
        /// Gets the current size of the command pool.
        /// </summary>
        /// <returns>Number of cached commands</returns>
        public static int GetPoolSize()
        {
            return _commands.Count;
        }
        
        /// <summary>
        /// Gets detailed statistics about the command pool for monitoring and debugging.
        /// </summary>
        /// <returns>A string containing pool statistics</returns>
        public static string GetPoolStatistics()
        {
            return $"CommandPool Statistics: " +
                   $"Pool Size: {Statistics.TotalCommands}, " +
                   $"Total Requests: {Statistics.TotalRequestCount}, " +
                   $"Cache Hits: {Statistics.CacheHitCount}, " +
                   $"Cache Misses: {Statistics.CacheMissCount}, " +
                   $"Hit Rate: {Statistics.CacheHitRate:P2}";
        }
        
        /// <summary>
        /// Checks if a command with the specified parameters exists in the pool.
        /// </summary>
        /// <param name="name">Command name</param>
        /// <param name="key">Primary key</param>
        /// <param name="modifiers">Modifier keys</param>
        /// <param name="ownerType">Owner type</param>
        /// <returns>True if the command exists in the pool</returns>
        public static bool ContainsCommand(string name, Key key, ModifierKeys modifiers = ModifierKeys.None, Type ownerType = null)
        {
            ownerType = ownerType ?? typeof(System.Windows.Window);
            var commandKey = CreateCommandKey(name, key, modifiers, ownerType);
            return _commands.ContainsKey(commandKey);
        }
        
        /// <summary>
        /// Gets all command names currently in the pool (for debugging).
        /// </summary>
        /// <returns>Collection of command names</returns>
        public static IEnumerable<string> GetCommandNames()
        {
            return _commands.Keys;
        }
    }
} 