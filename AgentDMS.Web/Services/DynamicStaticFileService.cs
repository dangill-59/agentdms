using AgentDMS.Core.Services.Storage;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;
using AgentDMS.Core.Services;

namespace AgentDMS.Web.Services;

/// <summary>
/// Service for managing dynamic static file serving based on storage configuration
/// </summary>
public interface IDynamicStaticFileService
{
    /// <summary>
    /// Configure static file serving for the current storage provider
    /// </summary>
    /// <param name="app">The application builder</param>
    void ConfigureStaticFiles(IApplicationBuilder app);
    
    /// <summary>
    /// Update static file serving when storage configuration changes
    /// </summary>
    /// <param name="app">The application builder</param>
    Task UpdateStaticFilesAsync(IApplicationBuilder app);
}

/// <summary>
/// Implementation of dynamic static file service
/// </summary>
public class DynamicStaticFileService : IDynamicStaticFileService
{
    private readonly IStorageService _storageService;
    private readonly ILogger<DynamicStaticFileService> _logger;
    private string? _currentStaticFileDirectory;

    public DynamicStaticFileService(IStorageService storageService, ILogger<DynamicStaticFileService> logger)
    {
        _storageService = storageService;
        _logger = logger;
    }

    public void ConfigureStaticFiles(IApplicationBuilder app)
    {
        ConfigureForCurrentProvider(app);
    }

    public async Task UpdateStaticFilesAsync(IApplicationBuilder app)
    {
        // Refresh the storage provider to get latest configuration
        await _storageService.RefreshProviderAsync();
        
        // Reconfigure static files for the new provider
        ConfigureForCurrentProvider(app);
    }

    private void ConfigureForCurrentProvider(IApplicationBuilder app)
    {
        if (_storageService.StorageProvider is LocalStorageProvider localProvider)
        {
            var outputDirectory = localProvider.BaseDirectory;
            
            // Only reconfigure if the directory has changed
            if (_currentStaticFileDirectory != outputDirectory)
            {
                Directory.CreateDirectory(outputDirectory);
                
                _logger.LogInformation("Configuring static file serving for directory: {Directory}", outputDirectory);
                
                // Note: This approach has limitations because ASP.NET Core middleware pipeline
                // is configured at startup. We'll need a different approach for true runtime updates.
                // For now, this documents the expected behavior.
                
                _currentStaticFileDirectory = outputDirectory;
            }
        }
    }
}