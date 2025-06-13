using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ExplorerPro.ViewModels;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Extension methods for registering tab management services
    /// </summary>
    public static class TabManagementServiceExtensions
    {
        /// <summary>
        /// Registers all tab management services with the DI container
        /// </summary>
        public static IServiceCollection AddTabManagement(this IServiceCollection services)
        {
            // Register core tab management services
            services.AddSingleton<TabStateManager>();
            services.AddSingleton<TabVirtualizationManager>();
            services.AddSingleton<TabSearchManager>();
            services.AddSingleton<TabPreviewManager>();
            services.AddSingleton<TabManager>();

            // Register view models
            services.AddTransient<TabControlViewModel>();
            services.AddTransient<TabViewModel>();

            return services;
        }

        /// <summary>
        /// Creates a configured TabManager instance with all dependencies
        /// </summary>
        public static TabManager CreateTabManager(ILoggerFactory loggerFactory)
        {
            var stateManager = new TabStateManager(loggerFactory.CreateLogger<TabStateManager>());
            var virtualizationManager = new TabVirtualizationManager(loggerFactory.CreateLogger<TabVirtualizationManager>(), stateManager);
            var searchManager = new TabSearchManager(loggerFactory.CreateLogger<TabSearchManager>(), stateManager);
            var previewManager = new TabPreviewManager(loggerFactory.CreateLogger<TabPreviewManager>(), stateManager);
            
            return new TabManager(
                loggerFactory.CreateLogger<TabManager>(),
                stateManager,
                virtualizationManager,
                searchManager,
                previewManager
            );
        }

        /// <summary>
        /// Creates a configured TabControlViewModel instance
        /// </summary>
        public static TabControlViewModel CreateTabControlViewModel(ILoggerFactory loggerFactory, TabManager tabManager)
        {
            return new TabControlViewModel(
                loggerFactory.CreateLogger<TabControlViewModel>(),
                loggerFactory,
                tabManager
            );
        }
    }
} 