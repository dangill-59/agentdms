using System;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services;
using AgentDMS.Core.Services.Storage;
using AgentDMS.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AgentDMS.Tests.Integration;

/// <summary>
/// Integration test to verify that the dynamic static file middleware properly serves files
/// from the updated storage directory when configuration changes at runtime
/// </summary>
public class DynamicStaticFileMiddlewareIntegrationTest
{
    [Fact]
    public async Task DynamicStaticFileMiddleware_ShouldServeFilesFromUpdatedStorageDirectory()
    {
        // Arrange
        var tempDir1 = Path.Combine(Path.GetTempPath(), "DynamicStatic1_" + Guid.NewGuid().ToString());
        var tempDir2 = Path.Combine(Path.GetTempPath(), "DynamicStatic2_" + Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempDir1);
            Directory.CreateDirectory(tempDir2);

            // Create test files in both directories
            var testFile1Path = Path.Combine(tempDir1, "test1.txt");
            var testFile2Path = Path.Combine(tempDir2, "test2.txt");
            await File.WriteAllTextAsync(testFile1Path, "Content from directory 1");
            await File.WriteAllTextAsync(testFile2Path, "Content from directory 2");

            // Create a mutable config provider
            var currentConfig = new StorageConfig
            {
                Provider = "Local",
                Local = new LocalStorageConfig { BaseDirectory = tempDir1 }
            };

            // Create storage service
            var factory = new StorageProviderFactory();
            Func<Task<StorageConfig>> configProvider = () => Task.FromResult(currentConfig);
            var storageService = new StorageService(factory, configProvider);

            // Create middleware
            var logger = Mock.Of<ILogger<DynamicStaticFileMiddleware>>();
            var middleware = new DynamicStaticFileMiddleware(
                next: (context) => Task.CompletedTask,
                storageService: storageService,
                logger: logger);

            // Test initial configuration (tempDir1)
            var context1 = CreateHttpContext("/AgentDMS_Output/test1.txt");
            await middleware.InvokeAsync(context1);
            
            Assert.Equal(200, context1.Response.StatusCode);
            var responseBody1 = await GetResponseBodyAsync(context1);
            Assert.Equal("Content from directory 1", responseBody1);

            // Update configuration to point to tempDir2
            currentConfig = new StorageConfig
            {
                Provider = "Local", 
                Local = new LocalStorageConfig { BaseDirectory = tempDir2 }
            };

            // Test updated configuration (tempDir2)
            var context2 = CreateHttpContext("/AgentDMS_Output/test2.txt");
            await middleware.InvokeAsync(context2);
            
            Assert.Equal(200, context2.Response.StatusCode);
            var responseBody2 = await GetResponseBodyAsync(context2);
            Assert.Equal("Content from directory 2", responseBody2);

            // Verify old file is no longer accessible
            var context3 = CreateHttpContext("/AgentDMS_Output/test1.txt");
            await middleware.InvokeAsync(context3);
            Assert.Equal(404, context3.Response.StatusCode);
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
    public async Task DynamicStaticFileMiddleware_ShouldPreventDirectoryTraversal()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), "SecurityTest_" + Guid.NewGuid().ToString());
        var outsideDir = Path.Combine(Path.GetTempPath(), "Outside_" + Guid.NewGuid().ToString());
        
        try
        {
            Directory.CreateDirectory(tempDir);
            Directory.CreateDirectory(outsideDir);

            // Create a file outside the base directory
            var outsideFile = Path.Combine(outsideDir, "secret.txt");
            await File.WriteAllTextAsync(outsideFile, "Secret content");

            var config = new StorageConfig
            {
                Provider = "Local",
                Local = new LocalStorageConfig { BaseDirectory = tempDir }
            };

            var factory = new StorageProviderFactory();
            Func<Task<StorageConfig>> configProvider = () => Task.FromResult(config);
            var storageService = new StorageService(factory, configProvider);

            var logger = Mock.Of<ILogger<DynamicStaticFileMiddleware>>();
            var middleware = new DynamicStaticFileMiddleware(
                next: (context) => Task.CompletedTask,
                storageService: storageService,
                logger: logger);

            // Act & Assert - Attempt directory traversal
            var context = CreateHttpContext("/AgentDMS_Output/../Outside_" + Path.GetFileName(outsideDir) + "/secret.txt");
            await middleware.InvokeAsync(context);
            
            // Should return 403 Forbidden (not 200 with secret content)
            Assert.Equal(403, context.Response.StatusCode);
        }
        finally
        {
            // Cleanup
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
            if (Directory.Exists(outsideDir))
                Directory.Delete(outsideDir, true);
        }
    }

    private static DefaultHttpContext CreateHttpContext(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<string> GetResponseBodyAsync(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }
}