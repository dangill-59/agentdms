using System;
using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Core.Models;

/// <summary>
/// Configuration for storage providers
/// </summary>
public class StorageConfig
{
    /// <summary>
    /// Type of storage provider to use
    /// </summary>
    [Required]
    public string Provider { get; set; } = "Local";
    
    /// <summary>
    /// Local storage configuration
    /// </summary>
    public LocalStorageConfig Local { get; set; } = new();
    
    /// <summary>
    /// AWS S3 storage configuration
    /// </summary>
    public AwsStorageConfig Aws { get; set; } = new();
    
    /// <summary>
    /// Azure Blob storage configuration
    /// </summary>
    public AzureStorageConfig Azure { get; set; } = new();
}

/// <summary>
/// Local storage configuration
/// </summary>
public class LocalStorageConfig
{
    /// <summary>
    /// Base directory for file storage. If not specified, uses system temp directory + "AgentDMS_Output"
    /// </summary>
    public string? BaseDirectory { get; set; }
}

/// <summary>
/// AWS S3 storage configuration
/// </summary>
public class AwsStorageConfig
{
    /// <summary>
    /// S3 bucket name
    /// </summary>
    [Required]
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// AWS region
    /// </summary>
    [Required]
    public string Region { get; set; } = "us-east-1";
    
    /// <summary>
    /// AWS access key ID (can also be set via environment variable AWS_ACCESS_KEY_ID)
    /// </summary>
    public string? AccessKeyId { get; set; }
    
    /// <summary>
    /// AWS secret access key (can also be set via environment variable AWS_SECRET_ACCESS_KEY)
    /// </summary>
    public string? SecretAccessKey { get; set; }
    
    /// <summary>
    /// Optional AWS session token for temporary credentials
    /// </summary>
    public string? SessionToken { get; set; }
}

/// <summary>
/// Azure Blob storage configuration
/// </summary>
public class AzureStorageConfig
{
    /// <summary>
    /// Azure storage account name
    /// </summary>
    [Required]
    public string AccountName { get; set; } = string.Empty;
    
    /// <summary>
    /// Blob container name
    /// </summary>
    [Required]
    public string ContainerName { get; set; } = "agentdms";
    
    /// <summary>
    /// Azure storage account key (can also be set via environment variable AZURE_STORAGE_KEY)
    /// </summary>
    public string? AccountKey { get; set; }
    
    /// <summary>
    /// Azure storage connection string (alternative to account name and key)
    /// </summary>
    public string? ConnectionString { get; set; }
}