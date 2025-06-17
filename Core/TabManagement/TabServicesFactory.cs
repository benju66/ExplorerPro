using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using ExplorerPro.ViewModels;

namespace ExplorerPro.Core.TabManagement
{
    /// <summary>
    /// Factory for creating and managing tab-related services.
    /// Handles dependency injection and service lifecycle for the tab system.
    /// </summary>
    public class TabServicesFactory : IDisposable
    {
        #region Private Fields
        
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TabServicesFactory> _logger;
        private bool _isDisposed;
        
        #endregion

        #region Constructor
        
        /// <summary>
        /// Creates a new TabServicesFactory with the specified service provider
        /// </summary>
        public TabServicesFactory(IServiceProvider serviceProvider = null, ILogger<TabServicesFactory> logger = null)
        {
            _serviceProvider = serviceProvider ?? CreateDefaultServiceProvider();
            _logger = logger ?? _serviceProvider.GetService<ILogger<TabServicesFactory>>();
            
            _logger?.LogInformation("TabServicesFactory initialized");
        }
        
        #endregion

        #region Factory Methods
        
        /// <summary>
        /// Creates a new ITabManagerService instance
        /// </summary>
        public ITabManagerService CreateTabManagerService()
        {
            try
            {
                var logger = _serviceProvider.GetService<ILogger<TabManagerService>>();
                var service = new TabManagerService(logger);
                
                _logger?.LogDebug("Created TabManagerService instance");
                return service;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating TabManagerService");
                throw;
            }
        }
        
        /// <summary>
        /// Creates a new MainWindowTabsViewModel instance
        /// </summary>
        public MainWindowTabsViewModel CreateTabsViewModel(ITabManagerService tabManager = null)
        {
            try
            {
                tabManager ??= CreateTabManagerService();
                var logger = _serviceProvider.GetService<ILogger<MainWindowTabsViewModel>>();
                var viewModel = new MainWindowTabsViewModel(tabManager, logger);
                
                _logger?.LogDebug("Created MainWindowTabsViewModel instance");
                return viewModel;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating MainWindowTabsViewModel");
                throw;
            }
        }
        
        /// <summary>
        /// Creates a complete tab management system (service + viewmodel)
        /// </summary>
        public (ITabManagerService Service, MainWindowTabsViewModel ViewModel) CreateTabSystem()
        {
            try
            {
                var service = CreateTabManagerService();
                var viewModel = CreateTabsViewModel(service);
                
                _logger?.LogInformation("Created complete tab management system");
                return (service, viewModel);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error creating tab management system");
                throw;
            }
        }
        
        #endregion

        #region Service Provider Creation
        
        /// <summary>
        /// Creates a default service provider with logging infrastructure
        /// </summary>
        private static IServiceProvider CreateDefaultServiceProvider()
        {
            var services = new ServiceCollection();
            
            // Add logging
            services.AddLogging(builder =>
            {
                builder.AddConsole()
                       .SetMinimumLevel(LogLevel.Debug);
            });
            
            // Add tab management services
            services.AddTransient<ITabManagerService, TabManagerService>();
            services.AddTransient<MainWindowTabsViewModel>();
            services.AddTransient<TabServicesFactory>();
            
            return services.BuildServiceProvider();
        }
        
        #endregion

        #region Singleton Pattern for Global Access
        
        private static TabServicesFactory _instance;
        private static readonly object _lockObject = new object();
        
        /// <summary>
        /// Gets the global instance of TabServicesFactory
        /// </summary>
        public static TabServicesFactory Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lockObject)
                    {
                        if (_instance == null)
                        {
                            _instance = new TabServicesFactory();
                        }
                    }
                }
                return _instance;
            }
        }
        
        /// <summary>
        /// Initializes the global instance with a specific service provider
        /// </summary>
        public static void Initialize(IServiceProvider serviceProvider, ILogger<TabServicesFactory> logger = null)
        {
            lock (_lockObject)
            {
                if (_instance != null)
                {
                    _instance.Dispose();
                }
                _instance = new TabServicesFactory(serviceProvider, logger);
            }
        }
        
        /// <summary>
        /// Disposes the global instance
        /// </summary>
        public static void DisposeGlobalInstance()
        {
            lock (_lockObject)
            {
                if (_instance != null)
                {
                    _instance.Dispose();
                    _instance = null;
                }
            }
        }
        
        #endregion

        #region Validation Methods
        
        /// <summary>
        /// Validates that all required services can be created
        /// </summary>
        public bool ValidateServices()
        {
            try
            {
                using var service = CreateTabManagerService();
                using var viewModel = CreateTabsViewModel(service);
                
                _logger?.LogInformation("Service validation successful");
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Service validation failed");
                return false;
            }
        }
        
        /// <summary>
        /// Gets diagnostic information about the service factory
        /// </summary>
        public string GetDiagnostics()
        {
            try
            {
                var info = new
                {
                    IsDisposed = _isDisposed,
                    HasServiceProvider = _serviceProvider != null,
                    HasLogger = _logger != null,
                    ValidationResult = ValidateServices()
                };
                
                return $"TabServicesFactory: {info}";
            }
            catch (Exception ex)
            {
                return $"TabServicesFactory: Error getting diagnostics - {ex.Message}";
            }
        }
        
        #endregion

        #region IDisposable Implementation
        
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        
        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                _logger?.LogInformation("Disposing TabServicesFactory");
                
                // Dispose service provider if we created it
                if (_serviceProvider is IDisposable disposableProvider)
                {
                    disposableProvider.Dispose();
                }
                
                _isDisposed = true;
            }
        }
        
        #endregion
    }
    
    /// <summary>
    /// Extension methods for easier service factory usage
    /// </summary>
    public static class TabServicesFactoryExtensions
    {
        /// <summary>
        /// Adds tab management services to the service collection
        /// </summary>
        public static IServiceCollection AddTabManagement(this IServiceCollection services)
        {
            services.AddTransient<ITabManagerService, TabManagerService>();
            services.AddTransient<MainWindowTabsViewModel>();
            services.AddSingleton<TabServicesFactory>();
            
            return services;
        }
        
        /// <summary>
        /// Gets or creates a tab management system from the service provider
        /// </summary>
        public static (ITabManagerService, MainWindowTabsViewModel) GetTabSystem(this IServiceProvider provider)
        {
            var factory = provider.GetService<TabServicesFactory>() ?? new TabServicesFactory(provider);
            return factory.CreateTabSystem();
        }
    }
} 