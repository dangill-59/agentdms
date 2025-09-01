using Xunit;
using System.ComponentModel.DataAnnotations;
using AgentDMS.Core.Models;

namespace AgentDMS.Tests;

/// <summary>
/// Tests for conditional storage configuration validation
/// </summary>
public class StorageConfigValidationTests
{
    [Fact]
    public void StorageConfig_AwsProvider_WithValidConfig_ShouldBeValid()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "my-bucket",
                Region = "us-east-1"
            }
            // Azure and Local configs remain with default values (empty for Azure AccountName)
        };

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(config, new ValidationContext(config), results, true);

        // Assert
        Assert.True(isValid, $"Validation should pass for valid AWS config. Errors: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
        Assert.Empty(results);
    }

    [Fact]
    public void StorageConfig_AwsProvider_WithMissingBucketName_ShouldBeInvalid()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "", // Missing bucket name
                Region = "us-east-1"
            }
        };

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(config, new ValidationContext(config), results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("AWS BucketName is required"));
        Assert.Contains(results, r => r.MemberNames.Contains("Aws.BucketName"));
    }

    [Fact]
    public void StorageConfig_AwsProvider_WithMissingRegion_ShouldBeInvalid()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "my-bucket",
                Region = "" // Missing region
            }
        };

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(config, new ValidationContext(config), results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("AWS Region is required"));
        Assert.Contains(results, r => r.MemberNames.Contains("Aws.Region"));
    }

    [Fact]
    public void StorageConfig_AzureProvider_WithValidConfig_ShouldBeValid()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "Azure",
            Azure = new AzureStorageConfig
            {
                AccountName = "myaccount",
                ContainerName = "mycontainer"
            }
            // AWS config remains with default values (empty bucket name)
        };

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(config, new ValidationContext(config), results, true);

        // Assert
        Assert.True(isValid, $"Validation should pass for valid Azure config. Errors: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
        Assert.Empty(results);
    }

    [Fact]
    public void StorageConfig_AzureProvider_WithMissingAccountName_ShouldBeInvalid()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "Azure",
            Azure = new AzureStorageConfig
            {
                AccountName = "", // Missing account name
                ContainerName = "mycontainer"
            }
        };

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(config, new ValidationContext(config), results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Azure AccountName is required"));
        Assert.Contains(results, r => r.MemberNames.Contains("Azure.AccountName"));
    }

    [Fact]
    public void StorageConfig_LocalProvider_ShouldAlwaysBeValid()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "Local"
            // Both AWS and Azure configs remain with default values (empty required fields)
        };

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(config, new ValidationContext(config), results, true);

        // Assert
        Assert.True(isValid, $"Validation should pass for Local provider regardless of other configs. Errors: {string.Join(", ", results.Select(r => r.ErrorMessage))}");
        Assert.Empty(results);
    }

    [Fact]
    public void StorageConfig_InvalidProvider_ShouldBeInvalid()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "InvalidProvider"
        };

        // Act
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(config, new ValidationContext(config), results, true);

        // Assert
        Assert.False(isValid);
        Assert.Contains(results, r => r.ErrorMessage!.Contains("Invalid storage provider"));
        Assert.Contains(results, r => r.MemberNames.Contains("Provider"));
    }
}