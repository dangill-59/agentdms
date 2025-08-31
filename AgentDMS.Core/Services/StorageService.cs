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
}

/// <summary>
/// Implementation of storage service
/// </summary>
public class StorageService : IStorageService
{
    public IStorageProvider StorageProvider { get; }
    public StorageConfig Configuration { get; }

    public StorageService(IOptions<StorageConfig> options, StorageProviderFactory factory)
    {
        Configuration = options.Value;
        StorageProvider = factory.CreateProvider(Configuration);
    }
}