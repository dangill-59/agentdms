using System;
using AgentDMS.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentDMS.Core.Services.Storage;

/// <summary>
/// Factory for creating storage providers based on configuration
/// </summary>
public class StorageProviderFactory
{
    private readonly ILoggerFactory? _loggerFactory;

    public StorageProviderFactory(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Create a storage provider based on the configuration
    /// </summary>
    /// <param name="config">Storage configuration</param>
    /// <returns>Configured storage provider</returns>
    /// <exception cref="ArgumentException">Thrown when provider type is not supported</exception>
    public IStorageProvider CreateProvider(StorageConfig config)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));

        return config.Provider.ToLowerInvariant() switch
        {
            "local" => CreateLocalProvider(config.Local),
            "aws" => CreateAwsProvider(config.Aws),
            "azure" => CreateAzureProvider(config.Azure),
            _ => throw new ArgumentException($"Unsupported storage provider: {config.Provider}. Supported providers: Local, AWS, Azure")
        };
    }

    private IStorageProvider CreateLocalProvider(LocalStorageConfig config)
    {
        var logger = _loggerFactory?.CreateLogger<LocalStorageProvider>();
        var baseDirectory = string.IsNullOrWhiteSpace(config.BaseDirectory) ? null : config.BaseDirectory;
        return new LocalStorageProvider(baseDirectory, logger);
    }

    private IStorageProvider CreateAwsProvider(AwsStorageConfig config)
    {
        if (string.IsNullOrEmpty(config.BucketName))
            throw new ArgumentException("AWS BucketName is required for AWS storage provider");
        
        if (string.IsNullOrEmpty(config.Region))
            throw new ArgumentException("AWS Region is required for AWS storage provider");

        var logger = _loggerFactory?.CreateLogger<AwsStorageProvider>();
        return new AwsStorageProvider(config, logger);
    }

    private IStorageProvider CreateAzureProvider(AzureStorageConfig config)
    {
        if (string.IsNullOrEmpty(config.AccountName))
            throw new ArgumentException("Azure AccountName is required for Azure storage provider");
        
        if (string.IsNullOrEmpty(config.ContainerName))
            throw new ArgumentException("Azure ContainerName is required for Azure storage provider");

        var logger = _loggerFactory?.CreateLogger<AzureStorageProvider>();
        return new AzureStorageProvider(config.AccountName, config.ContainerName, logger);
    }
}