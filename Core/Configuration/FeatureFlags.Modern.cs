using System;
using System.Configuration;
using Microsoft.Extensions.Configuration;

namespace ExplorerPro.Core.Configuration
{
    /// <summary>
    /// Centralized feature flags for the Modern tab migration.
    /// Keeps defaults safe (Chrome) and allows overrides via env/config.
    /// </summary>
    public static class ModernMigrationFlags
    {
        private static IConfiguration _configuration;

        /// <summary>
        /// Initialize with configuration (call from App startup if available)
        /// </summary>
        public static void Initialize(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        /// <summary>
        /// Use Modern tab system instead of Chrome (default false for safety)
        /// Priority order: ENV EXPLOREPRO_USE_MODERN_TABS -> appsettings.json FeatureFlags:UseModernTabs -> App.config -> default false
        /// </summary>
        public static bool UseModernTabs
        {
            get
            {
                // 1. Environment variable
                var envVar = Environment.GetEnvironmentVariable("EXPLOREPRO_USE_MODERN_TABS");
                if (!string.IsNullOrEmpty(envVar))
                {
                    return envVar.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                // 2. appsettings.json via IConfiguration
                if (_configuration != null)
                {
                    var value = _configuration.GetValue<bool?>("FeatureFlags:UseModernTabs");
                    if (value.HasValue) return value.Value;
                }

                // 3. Legacy App.config
                var appSetting = System.Configuration.ConfigurationManager.AppSettings["UseModernTabs"];
                if (!string.IsNullOrEmpty(appSetting))
                {
                    return appSetting.Equals("true", StringComparison.OrdinalIgnoreCase);
                }

                // Default to Chrome for safety
                return false;
            }
        }

        /// <summary>
        /// Enable debug logging for tab operations
        /// </summary>
        public static bool EnableTabDebugLogging =>
            _configuration?.GetValue<bool>("FeatureFlags:EnableTabDebugLogging") ?? false;
    }
}


