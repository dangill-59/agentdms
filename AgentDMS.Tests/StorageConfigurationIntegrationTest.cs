using System;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services;
using AgentDMS.Core.Services.Storage;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentDMS.Tests;

/// <summary>
/// Integration test to verify that StorageService uses runtime configuration
/// from StorageConfigService instead of static configuration
/// </summary>
public class StorageConfigurationIntegrationTest
{
    [Fact]
    public async Task StorageService_ShouldUseRuntimeConfiguration_WhenConfigIsUpdated()
    {
        // Arrange
        var tempDir1 = Path.Combine(Path.GetTempPath(), "TestStorage1_" + Guid.NewGuid().ToString());
        var tempDir2 = Path.Combine(Path.GetTempPath(), "TestStorage2_" + Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempDir1);
            Directory.CreateDirectory(tempDir2);

            // Create a config provider that we can change
            var currentConfig = new StorageConfig
            {
                Provider = "Local",
                Local = new LocalStorageConfig { BaseDirectory = tempDir1 }
            };

            // Create the StorageService with a dynamic config provider
            var factory = new StorageProviderFactory();
            Func<Task<StorageConfig>> configProvider = () => Task.FromResult(currentConfig);
            var storageService = new StorageService(factory, configProvider);

            // Verify initial configuration
            Assert.Equal(tempDir1, (storageService.StorageProvider as LocalStorageProvider)?.BaseDirectory);

            // Update the configuration to point to a different directory
            currentConfig = new StorageConfig
            {
                Provider = "Local",
                Local = new LocalStorageConfig { BaseDirectory = tempDir2 }
            };

            // Refresh the storage provider
            await storageService.RefreshProviderAsync();

            // Verify the storage provider now uses the new directory
            Assert.Equal(tempDir2, (storageService.StorageProvider as LocalStorageProvider)?.BaseDirectory);

            // Test that files are saved to the new directory
            var testData = System.Text.Encoding.UTF8.GetBytes("Test file content");
            var fileName = "test.txt";
            
            var savedUrl = await storageService.StorageProvider.SaveFileAsync(testData, fileName);
            var expectedPath = Path.Combine(tempDir2, fileName);
            
            Assert.True(File.Exists(expectedPath), $"File should exist at {expectedPath}");
            Assert.Equal("Test file content", await File.ReadAllTextAsync(expectedPath));
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir1))
                Directory.Delete(tempDir1, true);
            if (Directory.Exists(tempDir2))
                Directory.Delete(tempDir2, true);
        }
    }

    [Fact]
    public async Task StorageService_ShouldNotRecreateProvider_WhenConfigurationHasNotChanged()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempDir);

            var config = new StorageConfig
            {
                Provider = "Local",
                Local = new LocalStorageConfig { BaseDirectory = tempDir }
            };

            var factory = new StorageProviderFactory();
            Func<Task<StorageConfig>> configProvider = () => Task.FromResult(config);
            var storageService = new StorageService(factory, configProvider);

            var originalProvider = storageService.StorageProvider;

            // Refresh with the same configuration
            await storageService.RefreshProviderAsync();

            // Verify the provider instance is the same (not recreated)
            Assert.Same(originalProvider, storageService.StorageProvider);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}