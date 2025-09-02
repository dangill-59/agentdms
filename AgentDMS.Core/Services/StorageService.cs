using AgentDMS.Core.Models;
using AgentDMS.Core.Services.Storage;
using Microsoft.Extensions.Options;

namespace AgentDMS.Core.Services;

/// <summary>
/// Service for managing storage provider configuration and instances
/// </summary>
public interface IStorageService
{
    /// <summary>
    /// Get the configured storage provider
    /// </summary>
    IStorageProvider StorageProvider { get; }
    
    /// <summary>
    /// Get the storage configuration
    /// </summary>
    StorageConfig Configuration { get; }
    
    /// <summary>
    /// Refresh the storage provider with current configuration
    /// </summary>
    Task RefreshProviderAsync();
}

/// <summary>
/// Implementation of storage service that dynamically uses runtime configuration
/// </summary>
public class StorageService : IStorageService
{
    private readonly StorageProviderFactory _factory;
    private readonly Func<Task<StorageConfig>> _configProvider;
    private IStorageProvider _storageProvider;
    private StorageConfig _configuration;

    public IStorageProvider StorageProvider => _storageProvider;
    public StorageConfig Configuration => _configuration;

    public StorageService(StorageProviderFactory factory, Func<Task<StorageConfig>> configProvider)
    {
        _factory = factory;
        _configProvider = configProvider;
        
        // Initialize with current configuration
        _configuration = _configProvider().Result;
        _storageProvider = _factory.CreateProvider(_configuration);
    }

    public async Task RefreshProviderAsync()
    {
        var newConfiguration = await _configProvider();
        
        // Only recreate the provider if the configuration has actually changed
        if (!ConfigurationsEqual(_configuration, newConfiguration))
        {
            _configuration = newConfiguration;
            _storageProvider = _factory.CreateProvider(_configuration);
        }
    }

    private static bool ConfigurationsEqual(StorageConfig config1, StorageConfig config2)
    {
        if (config1.Provider != config2.Provider)
            return false;

        // Compare relevant properties based on provider type
        return config1.Provider.ToLowerInvariant() switch
        {
            "local" => config1.Local.BaseDirectory == config2.Local.BaseDirectory,
            "aws" => config1.Aws.BucketName == config2.Aws.BucketName &&
                    config1.Aws.Region == config2.Aws.Region &&
                    config1.Aws.AccessKeyId == config2.Aws.AccessKeyId &&
                    config1.Aws.SecretAccessKey == config2.Aws.SecretAccessKey,
            "azure" => config1.Azure.AccountName == config2.Azure.AccountName &&
                      config1.Azure.ContainerName == config2.Azure.ContainerName &&
                      config1.Azure.AccountKey == config2.Azure.AccountKey &&
                      config1.Azure.ConnectionString == config2.Azure.ConnectionString,
            _ => false
        };
    }
}