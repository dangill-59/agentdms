using Xunit;
using AgentDMS.Web.Models;
using AgentDMS.Web.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Hosting;
using Moq;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AgentDMS.Tests.Web;

/// <summary>
/// Tests for upload configuration functionality
/// </summary>
public class UploadConfigTests : IDisposable
{
    private readonly Mock<IWebHostEnvironment> _mockEnv;
    private readonly Mock<IConfiguration> _mockConfig;
    private readonly Mock<ILogger<UploadConfigService>> _mockLogger;
    private readonly string _tempDirectory;

    public UploadConfigTests()
    {
        _mockEnv = new Mock<IWebHostEnvironment>();
        _mockConfig = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<UploadConfigService>>();
        
        _tempDirectory = Path.Combine(Path.GetTempPath(), "AgentDMS_Tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
        
        _mockEnv.Setup(x => x.ContentRootPath).Returns(_tempDirectory);
    }

    [Fact]
    public async Task GetConfigAsync_WithDefaultSettings_ReturnsDefaultConfig()
    {
        // Arrange
        var mockSection = new Mock<IConfigurationSection>();
        _mockConfig.Setup(x => x.GetSection("UploadLimits")).Returns(mockSection.Object);
        
        var service = new UploadConfigService(_mockEnv.Object, _mockConfig.Object, _mockLogger.Object);

        // Act
        var config = await service.GetConfigAsync();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(100 * 1024 * 1024, config.MaxFileSizeBytes);
        Assert.Equal(100 * 1024 * 1024, config.MaxRequestBodySizeBytes);
        Assert.Equal(100 * 1024 * 1024, config.MaxMultipartBodyLengthBytes);
        Assert.True(config.ApplySizeLimits);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithValidConfig_SavesAndLoadsCorrectly()
    {
        // Arrange
        var mockSection = new Mock<IConfigurationSection>();
        _mockConfig.Setup(x => x.GetSection("UploadLimits")).Returns(mockSection.Object);
        
        var service = new UploadConfigService(_mockEnv.Object, _mockConfig.Object, _mockLogger.Object);
        
        var newConfig = new UploadConfig
        {
            MaxFileSizeBytes = 200 * 1024 * 1024, // 200MB
            MaxRequestBodySizeBytes = 200 * 1024 * 1024,
            MaxMultipartBodyLengthBytes = 200 * 1024 * 1024,
            ApplySizeLimits = true
        };

        // Act
        await service.UpdateConfigAsync(newConfig);
        var retrievedConfig = await service.GetConfigAsync();

        // Assert
        Assert.Equal(200 * 1024 * 1024, retrievedConfig.MaxFileSizeBytes);
        Assert.Equal(200 * 1024 * 1024, retrievedConfig.MaxRequestBodySizeBytes);
        Assert.Equal(200 * 1024 * 1024, retrievedConfig.MaxMultipartBodyLengthBytes);
        Assert.True(retrievedConfig.ApplySizeLimits);
    }

    [Fact]
    public async Task UpdateConfigAsync_WithInvalidConfig_ThrowsArgumentException()
    {
        // Arrange
        var mockSection = new Mock<IConfigurationSection>();
        _mockConfig.Setup(x => x.GetSection("UploadLimits")).Returns(mockSection.Object);
        
        var service = new UploadConfigService(_mockEnv.Object, _mockConfig.Object, _mockLogger.Object);
        
        var invalidConfig = new UploadConfig
        {
            MaxFileSizeBytes = 100 * 1024 * 1024,
            MaxRequestBodySizeBytes = 50 * 1024 * 1024, // Smaller than max file size - invalid
            MaxMultipartBodyLengthBytes = 100 * 1024 * 1024
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateConfigAsync(invalidConfig));
    }

    [Fact]
    public void UploadConfig_MBProperties_ConvertCorrectly()
    {
        // Arrange
        var config = new UploadConfig();

        // Act
        config.MaxFileSizeMB = 50; // Set 50MB

        // Assert
        Assert.Equal(50 * 1024 * 1024, config.MaxFileSizeBytes);
        Assert.Equal(50, config.MaxFileSizeMB);
    }

    [Fact]
    public void UploadConfig_WithEnvironmentVariable_UsesEnvValue()
    {
        // Arrange
        Environment.SetEnvironmentVariable("AGENTDMS_MAX_FILE_SIZE_MB", "150");
        
        try
        {
            var mockSection = new Mock<IConfigurationSection>();
            _mockConfig.Setup(x => x.GetSection("UploadLimits")).Returns(mockSection.Object);
            
            var service = new UploadConfigService(_mockEnv.Object, _mockConfig.Object, _mockLogger.Object);

            // Act
            var config = service.GetConfigAsync().Result;

            // Assert
            Assert.Equal(150, config.MaxFileSizeMB);
        }
        finally
        {
            Environment.SetEnvironmentVariable("AGENTDMS_MAX_FILE_SIZE_MB", null);
        }
    }

    /// <summary>
    /// Cleanup test resources
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}