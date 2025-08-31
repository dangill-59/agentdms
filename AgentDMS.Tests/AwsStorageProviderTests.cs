using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services.Storage;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgentDMS.Tests;

/// <summary>
/// Tests for AWS S3 storage provider functionality
/// </summary>
public class AwsStorageProviderTests : IDisposable
{
    private readonly Mock<ILogger<AwsStorageProvider>> _loggerMock;
    private readonly string _tempDirectory;

    public AwsStorageProviderTests()
    {
        _loggerMock = new Mock<ILogger<AwsStorageProvider>>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public void Constructor_WithValidConfig_ShouldInitializeCorrectly()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-east-1"
        };

        // Act
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);

        // Assert
        Assert.Equal(StorageProviderType.AWS, provider.ProviderType);
    }

    [Fact]
    public void Constructor_WithNullConfig_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new AwsStorageProvider(null!, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithEmptyBucketName_ShouldThrowArgumentException()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "",
            Region = "us-east-1"
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AwsStorageProvider(config, _loggerMock.Object));
    }

    [Fact]
    public void Constructor_WithEmptyRegion_ShouldThrowArgumentException()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = ""
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => new AwsStorageProvider(config, _loggerMock.Object));
    }

    [Fact]
    public void GetFileUrl_ShouldReturnCorrectS3Url()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-west-2"
        };
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);
        var relativePath = "documents/test.pdf";

        // Act
        var url = provider.GetFileUrl(relativePath);

        // Assert
        Assert.Equal("https://test-bucket.s3.us-west-2.amazonaws.com/documents/test.pdf", url);
    }

    [Fact]
    public async Task EnsureDirectoryExistsAsync_ShouldCompleteSuccessfully()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-east-1"
        };
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);

        // Act & Assert (should not throw)
        await provider.EnsureDirectoryExistsAsync("some/directory/path");
    }

    [Fact]
    public async Task SaveFileAsync_WithNullContent_ShouldThrowArgumentException()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-east-1"
        };
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            provider.SaveFileAsync(null!, "test.txt"));
    }

    [Fact]
    public async Task SaveFileAsync_WithEmptyContent_ShouldThrowArgumentException()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-east-1"
        };
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            provider.SaveFileAsync(Array.Empty<byte>(), "test.txt"));
    }

    [Fact]
    public async Task SaveFileAsync_WithNullStream_ShouldThrowArgumentNullException()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-east-1"
        };
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => 
            provider.SaveFileAsync((Stream)null!, "test.txt"));
    }

    [Fact]
    public async Task SaveFileAsync_WithNonExistentFile_ShouldThrowFileNotFoundException()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-east-1"
        };
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);
        var nonExistentFile = Path.Combine(_tempDirectory, "nonexistent.txt");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => 
            provider.SaveFileAsync(nonExistentFile, "test.txt"));
    }

    [Fact]
    public void Constructor_WithCredentials_ShouldInitializeCorrectly()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-east-1",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret"
        };

        // Act
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);

        // Assert
        Assert.Equal(StorageProviderType.AWS, provider.ProviderType);
    }

    [Fact]
    public void Constructor_WithSessionToken_ShouldInitializeCorrectly()
    {
        // Arrange
        var config = new AwsStorageConfig
        {
            BucketName = "test-bucket",
            Region = "us-east-1",
            AccessKeyId = "test-key",
            SecretAccessKey = "test-secret",
            SessionToken = "test-token"
        };

        // Act
        using var provider = new AwsStorageProvider(config, _loggerMock.Object);

        // Assert
        Assert.Equal(StorageProviderType.AWS, provider.ProviderType);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}