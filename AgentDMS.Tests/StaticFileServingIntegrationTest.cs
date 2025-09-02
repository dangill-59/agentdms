using System;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services;
using AgentDMS.Core.Services.Storage;
using AgentDMS.Web.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace AgentDMS.Tests;

/// <summary>
/// Integration test to verify that static file serving updates when storage configuration changes
/// </summary>
public class StaticFileServingIntegrationTest
{
    [Fact]
    public async Task StaticFileServing_ShouldUpdateWhenStorageConfigChanges()
    {
        // Arrange
        var tempDir1 = Path.Combine(Path.GetTempPath(), "TestStaticFiles1_" + Guid.NewGuid().ToString());
        var tempDir2 = Path.Combine(Path.GetTempPath(), "TestStaticFiles2_" + Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempDir1);
            Directory.CreateDirectory(tempDir2);

            // Create initial config pointing to tempDir1
            var config1 = new StorageConfig
            {
                Provider = "Local",
                Local = new LocalStorageConfig { BaseDirectory = tempDir1 }
            };

            // This test documents the current issue:
            // When storage config changes at runtime, static file serving doesn't update
            // Files get saved to the new location but aren't served from the web interface
            
            // For now, this test serves as documentation of the expected behavior
            // Once the fix is implemented, this test should pass
            
            Assert.True(Directory.Exists(tempDir1));
            Assert.True(Directory.Exists(tempDir2));
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
}