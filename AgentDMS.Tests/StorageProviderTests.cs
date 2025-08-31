using System;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services.Storage;
using Xunit;

namespace AgentDMS.Tests;

public class StorageProviderTests
{
    [Fact]
    public void LocalStorageProvider_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var provider = new LocalStorageProvider();
        
        // Assert
        Assert.Equal(StorageProviderType.Local, provider.ProviderType);
        Assert.True(Directory.Exists(provider.BaseDirectory));
    }
    
    [Fact]
    public void LocalStorageProvider_WithCustomDirectory_ShouldUseCustomDirectory()
    {
        // Arrange
        var customDir = Path.Combine(Path.GetTempPath(), "TestCustomDir");
        
        // Act
        var provider = new LocalStorageProvider(customDir);
        
        // Assert
        Assert.Equal(StorageProviderType.Local, provider.ProviderType);
        Assert.Equal(customDir, provider.BaseDirectory);
        Assert.True(Directory.Exists(customDir));
        
        // Cleanup
        if (Directory.Exists(customDir))
            Directory.Delete(customDir, true);
    }
    
    [Fact]
    public async Task LocalStorageProvider_SaveFileAsync_ShouldSaveFile()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "StorageTest_" + Guid.NewGuid().ToString());
        var provider = new LocalStorageProvider(tempDir);
        
        var sourceFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(sourceFile, "Test content");
        
        try
        {
            // Act
            var result = await provider.SaveFileAsync(sourceFile, "test.txt");
            
            // Assert
            Assert.True(await provider.FileExistsAsync("test.txt"));
            var savedPath = Path.Combine(tempDir, "test.txt");
            Assert.True(File.Exists(savedPath));
            Assert.Equal("Test content", await File.ReadAllTextAsync(savedPath));
        }
        finally
        {
            // Cleanup
            File.Delete(sourceFile);
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
    
    [Fact]
    public void StorageProviderFactory_ShouldCreateLocalProvider()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "Local",
            Local = new LocalStorageConfig
            {
                BaseDirectory = Path.GetTempPath()
            }
        };
        var factory = new StorageProviderFactory();
        
        // Act
        var provider = factory.CreateProvider(config);
        
        // Assert
        Assert.IsType<LocalStorageProvider>(provider);
        Assert.Equal(StorageProviderType.Local, provider.ProviderType);
    }
    
    [Fact]
    public void StorageProviderFactory_WithInvalidProvider_ShouldThrowException()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "InvalidProvider"
        };
        var factory = new StorageProviderFactory();
        
        // Act & Assert
        Assert.Throws<ArgumentException>(() => factory.CreateProvider(config));
    }
    
    [Fact]
    public void AwsStorageProvider_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var provider = new AwsStorageProvider("test-bucket", "us-east-1");
        
        // Assert
        Assert.Equal(StorageProviderType.AWS, provider.ProviderType);
        Assert.Equal("https://test-bucket.s3.us-east-1.amazonaws.com/test.txt", provider.GetFileUrl("test.txt"));
    }
    
    [Fact]
    public void AzureStorageProvider_ShouldInitializeCorrectly()
    {
        // Arrange & Act
        var provider = new AzureStorageProvider("testaccount", "testcontainer");
        
        // Assert
        Assert.Equal(StorageProviderType.Azure, provider.ProviderType);
        Assert.Equal("https://testaccount.blob.core.windows.net/testcontainer/test.txt", provider.GetFileUrl("test.txt"));
    }
    
    [Fact]
    public async Task AwsStorageProvider_MethodsThrowNotImplemented()
    {
        // Arrange
        var provider = new AwsStorageProvider("test-bucket", "us-east-1");
        
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => provider.SaveFileAsync("source", "dest"));
        await Assert.ThrowsAsync<NotImplementedException>(() => provider.SaveFileAsync(new byte[0], "dest"));
        await Assert.ThrowsAsync<NotImplementedException>(() => provider.FileExistsAsync("test"));
    }
    
    [Fact]
    public async Task AzureStorageProvider_MethodsThrowNotImplemented()
    {
        // Arrange
        var provider = new AzureStorageProvider("testaccount", "testcontainer");
        
        // Act & Assert
        await Assert.ThrowsAsync<NotImplementedException>(() => provider.SaveFileAsync("source", "dest"));
        await Assert.ThrowsAsync<NotImplementedException>(() => provider.SaveFileAsync(new byte[0], "dest"));
        await Assert.ThrowsAsync<NotImplementedException>(() => provider.FileExistsAsync("test"));
    }
}