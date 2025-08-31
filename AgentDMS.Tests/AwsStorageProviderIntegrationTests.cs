using System;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgentDMS.Tests;

/// <summary>
/// Integration tests for Storage Provider Factory with AWS S3
/// </summary>
public class AwsStorageProviderIntegrationTests
{
    [Fact]
    public void CreateProvider_WithValidAwsConfig_ShouldReturnAwsStorageProvider()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        var logger = new Mock<ILogger<AwsStorageProvider>>();
        loggerFactory.Setup(f => f.CreateLogger<AwsStorageProvider>()).Returns(logger.Object);
        
        var factory = new StorageProviderFactory(loggerFactory.Object);
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "test-bucket",
                Region = "us-east-1"
            }
        };

        // Act
        var provider = factory.CreateProvider(config);

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<AwsStorageProvider>(provider);
        Assert.Equal(StorageProviderType.AWS, provider.ProviderType);
        
        // Cleanup
        if (provider is IDisposable disposable)
            disposable.Dispose();
    }

    [Fact]
    public void CreateProvider_WithAwsConfigAndCredentials_ShouldReturnAwsStorageProvider()
    {
        // Arrange
        var loggerFactory = new Mock<ILoggerFactory>();
        var logger = new Mock<ILogger<AwsStorageProvider>>();
        loggerFactory.Setup(f => f.CreateLogger<AwsStorageProvider>()).Returns(logger.Object);
        
        var factory = new StorageProviderFactory(loggerFactory.Object);
        var config = new StorageConfig
        {
            Provider = "aws", // Test case insensitive
            Aws = new AwsStorageConfig
            {
                BucketName = "my-test-bucket",
                Region = "eu-west-1",
                AccessKeyId = "test-key",
                SecretAccessKey = "test-secret"
            }
        };

        // Act
        var provider = factory.CreateProvider(config);

        // Assert
        Assert.NotNull(provider);
        Assert.IsType<AwsStorageProvider>(provider);
        Assert.Equal(StorageProviderType.AWS, provider.ProviderType);
        
        // Cleanup
        if (provider is IDisposable disposable)
            disposable.Dispose();
    }

    [Fact]
    public void CreateProvider_WithAwsConfigMissingBucketName_ShouldThrowArgumentException()
    {
        // Arrange
        var factory = new StorageProviderFactory();
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "", // Empty bucket name
                Region = "us-east-1"
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.CreateProvider(config));
        Assert.Contains("AWS BucketName is required", exception.Message);
    }

    [Fact]
    public void CreateProvider_WithAwsConfigMissingRegion_ShouldThrowArgumentException()
    {
        // Arrange
        var factory = new StorageProviderFactory();
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "test-bucket",
                Region = "" // Empty region
            }
        };

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => factory.CreateProvider(config));
        Assert.Contains("AWS Region is required", exception.Message);
    }

    [Fact]
    public void CreateProvider_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Arrange
        var factory = new StorageProviderFactory();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => factory.CreateProvider(null!));
    }

    [Fact]
    public void AwsStorageProvider_GetFileUrl_ShouldReturnCorrectFormat()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "my-documents",
            Region = "ap-southeast-2"
        };
        using var provider = new AwsStorageProvider(config);

        // Act
        var url = provider.GetFileUrl("folder/document.pdf");

        // Assert
        Assert.Equal("https://my-documents.s3.ap-southeast-2.amazonaws.com/folder/document.pdf", url);
    }

    [Fact]
    public void AwsStorageProvider_GetFileUrl_WithSpecialCharacters_ShouldReturnCorrectFormat()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket-123",
            Region = "us-west-2"
        };
        using var provider = new AwsStorageProvider(config);

        // Act
        var url = provider.GetFileUrl("uploads/2024-08-31/file with spaces.jpg");

        // Assert
        Assert.Equal("https://test-bucket-123.s3.us-west-2.amazonaws.com/uploads/2024-08-31/file with spaces.jpg", url);
    }
}