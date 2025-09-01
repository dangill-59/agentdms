using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace AgentDMS.Core.Models;

/// <summary>
/// Configuration for storage providers
/// </summary>
public class StorageConfig : IValidatableObject
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

    /// <summary>
    /// Validates the storage configuration based on the selected provider
    /// </summary>
    /// <param name="validationContext">The validation context</param>
    /// <returns>Validation results</returns>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var results = new List<ValidationResult>();

        // Only validate the configuration for the selected provider
        switch (Provider?.ToUpper())
        {
            case "LOCAL":
                // Local storage has no required fields
                break;
                
            case "AWS":
                ValidateAwsConfiguration(results);
                break;
                
            case "AZURE":
                ValidateAzureConfiguration(results);
                break;
                
            default:
                results.Add(new ValidationResult(
                    $"Invalid storage provider '{Provider}'. Supported providers are: Local, AWS, Azure.",
                    new[] { nameof(Provider) }));
                break;
        }

        return results;
    }

    private void ValidateAwsConfiguration(List<ValidationResult> results)
    {
        if (string.IsNullOrWhiteSpace(Aws.BucketName))
        {
            results.Add(new ValidationResult(
                "AWS BucketName is required when using AWS storage provider.",
                new[] { $"{nameof(Aws)}.{nameof(Aws.BucketName)}" }));
        }

        if (string.IsNullOrWhiteSpace(Aws.Region))
        {
            results.Add(new ValidationResult(
                "AWS Region is required when using AWS storage provider.",
                new[] { $"{nameof(Aws)}.{nameof(Aws.Region)}" }));
        }
    }

    private void ValidateAzureConfiguration(List<ValidationResult> results)
    {
        if (string.IsNullOrWhiteSpace(Azure.AccountName))
        {
            results.Add(new ValidationResult(
                "Azure AccountName is required when using Azure storage provider.",
                new[] { $"{nameof(Azure)}.{nameof(Azure.AccountName)}" }));
        }

        if (string.IsNullOrWhiteSpace(Azure.ContainerName))
        {
            results.Add(new ValidationResult(
                "Azure ContainerName is required when using Azure storage provider.",
                new[] { $"{nameof(Azure)}.{nameof(Azure.ContainerName)}" }));
        }
    }
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
    public string BucketName { get; set; } = string.Empty;
    
    /// <summary>
    /// AWS region
    /// </summary>
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
    public string AccountName { get; set; } = string.Empty;
    
    /// <summary>
    /// Blob container name
    /// </summary>
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