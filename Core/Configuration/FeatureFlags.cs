using System;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace ExplorerPro.Core.Configuration
{
    /// <summary>
    /// Centralized feature flag management with caching and multiple configuration sources
    /// </summary>
    public static class FeatureFlags
    {
        // Cache flag values for performance
        private static readonly Dictionary<string, bool> _flagCache = new();
        private static DateTime _lastCacheUpdate = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);
        private static readonly object _cacheLock = new object();
        
        // Phase 1 Feature Flags
        public static bool UseTabModelResolver => GetFlag("UseTabModelResolver", true);
        public static bool UseTabDisposalCoordinator => GetFlag("UseTabDisposalCoordinator", true); // Enable by default for Phase 1
        public static bool UseEventCleanupManager => GetFlag("UseEventCleanupManager", true); // Enable by default for Phase 1
        
        // Monitoring and Diagnostics
        public static bool EnableTabResolutionMonitoring => GetFlag("EnableTabResolutionMonitoring", true);
        public static bool EnableTabDisposalMonitoring => GetFlag("EnableTabDisposalMonitoring", true); // New flag for disposal monitoring
        public static bool EnableVerboseTelemetry => GetFlag("EnableVerboseTelemetry", false);
        public static bool EnablePerformanceMetrics => GetFlag("EnablePerformanceMetrics", true);
        
        // Development and Testing
        public static bool EnableDebugLogging => GetFlag("EnableDebugLogging", false);
        public static bool EnableManualTests => GetFlag("EnableManualTests", true);
        
        /// <summary>
        /// Gets a feature flag value with caching and multiple configuration sources
        /// </summary>
        /// <param name="name">Feature flag name</param>
        /// <param name="defaultValue">Default value if not found in any source</param>
        /// <returns>Feature flag value</returns>
        private static bool GetFlag(string name, bool defaultValue)
        {
            try
            {
                lock (_cacheLock)
                {
                    // Check cache first
                    if (_flagCache.TryGetValue(name, out var cachedValue) && 
                        DateTime.UtcNow - _lastCacheUpdate < CacheExpiry)
                    {
                        return cachedValue;
                    }

                    // Priority 1: Environment variable (highest priority)
                    var envName = $"FF_{name.ToUpperInvariant()}";
                    var envValue = Environment.GetEnvironmentVariable(envName);
                    if (bool.TryParse(envValue, out var envResult))
                    {
                        UpdateCache(name, envResult);
                        LogFlagSource(name, envResult, "Environment Variable");
                        return envResult;
                    }

                    // Priority 2: Settings service (if available)
                    try
                    {
                        var settingsValue = App.Settings?.GetSetting<bool?>($"FeatureFlags.{name}");
                        if (settingsValue.HasValue)
                        {
                            UpdateCache(name, settingsValue.Value);
                            LogFlagSource(name, settingsValue.Value, "Settings Service");
                            return settingsValue.Value;
                        }
                    }
                    catch (Exception ex)
                    {
                        // Settings might not be initialized yet or might be corrupted
                        Console.WriteLine($"Error reading feature flag '{name}' from settings: {ex.Message}");
                    }

                    // Priority 3: Default value
                    UpdateCache(name, defaultValue);
                    LogFlagSource(name, defaultValue, "Default Value");
                    return defaultValue;
                }
            }
            catch (Exception ex)
            {
                // Something went wrong with flag resolution - use default and log error
                Console.WriteLine($"Error resolving feature flag '{name}': {ex.Message}. Using default: {defaultValue}");
                return defaultValue;
            }
        }

        /// <summary>
        /// Updates the cache with a new flag value
        /// </summary>
        private static void UpdateCache(string name, bool value)
        {
            _flagCache[name] = value;
            _lastCacheUpdate = DateTime.UtcNow;
        }
        
        /// <summary>
        /// Logs the source of a feature flag value
        /// </summary>
        private static void LogFlagSource(string name, bool value, string source)
        {
            if (EnableVerboseTelemetry)
            {
                Console.WriteLine($"[FEATURE FLAG] {name} = {value} (from {source})");
            }
        }

        /// <summary>
        /// Forces a refresh of all cached flag values
        /// </summary>
        public static void RefreshCache()
        {
            lock (_cacheLock)
            {
                _flagCache.Clear();
                _lastCacheUpdate = DateTime.MinValue;
                Console.WriteLine("[FEATURE FLAGS] Cache refreshed - all flags will be re-evaluated");
            }
        }
        
        /// <summary>
        /// Gets all current flag values for debugging/monitoring
        /// </summary>
        public static Dictionary<string, bool> GetAllFlags()
        {
            return new Dictionary<string, bool>
            {
                [nameof(UseTabModelResolver)] = UseTabModelResolver,
                [nameof(UseTabDisposalCoordinator)] = UseTabDisposalCoordinator,
                [nameof(UseEventCleanupManager)] = UseEventCleanupManager,
                [nameof(EnableTabResolutionMonitoring)] = EnableTabResolutionMonitoring,
                [nameof(EnableVerboseTelemetry)] = EnableVerboseTelemetry,
                [nameof(EnablePerformanceMetrics)] = EnablePerformanceMetrics,
                [nameof(EnableDebugLogging)] = EnableDebugLogging,
                [nameof(EnableManualTests)] = EnableManualTests
            };
        }
        
        /// <summary>
        /// Sets a feature flag programmatically (useful for testing)
        /// </summary>
        /// <param name="name">Flag name</param>
        /// <param name="value">Flag value</param>
        public static void SetFlag(string name, bool value)
        {
            lock (_cacheLock)
            {
                // Set as environment variable so it has highest priority
                var envName = $"FF_{name.ToUpperInvariant()}";
                Environment.SetEnvironmentVariable(envName, value.ToString());
                
                // Update cache immediately
                UpdateCache(name, value);
                
                Console.WriteLine($"[FEATURE FLAGS] {name} set to {value} programmatically");
            }
        }
        
        /// <summary>
        /// Clears a programmatically set flag
        /// </summary>
        /// <param name="name">Flag name</param>
        public static void ClearFlag(string name)
        {
            lock (_cacheLock)
            {
                var envName = $"FF_{name.ToUpperInvariant()}";
                Environment.SetEnvironmentVariable(envName, null);
                
                // Remove from cache to force re-evaluation
                _flagCache.Remove(name);
                
                Console.WriteLine($"[FEATURE FLAGS] {name} cleared - will use next priority source");
            }
        }
        
        /// <summary>
        /// Gets diagnostic information about feature flags
        /// </summary>
        public static string GetDiagnosticInfo()
        {
            var info = new System.Text.StringBuilder();
            info.AppendLine("=== Feature Flags Diagnostic Info ===");
            info.AppendLine($"Cache expiry: {CacheExpiry}");
            info.AppendLine($"Last cache update: {_lastCacheUpdate}");
            info.AppendLine($"Cached flags count: {_flagCache.Count}");
            info.AppendLine();
            
            info.AppendLine("Current Flag Values:");
            foreach (var flag in GetAllFlags())
            {
                info.AppendLine($"  {flag.Key}: {flag.Value}");
            }
            
            return info.ToString();
        }
    }
}