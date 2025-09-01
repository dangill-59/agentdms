using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using AgentDMS.Core.Models;
using AgentDMS.Web.Controllers;
using AgentDMS.Web.Services;
using Xunit;

namespace AgentDMS.Tests.Web.Controllers;

/// <summary>
/// Tests for Storage Configuration Controller
/// </summary>
public class StorageConfigControllerTests
{
    private readonly Mock<ILogger<StorageConfigController>> _loggerMock;
    private readonly Mock<IStorageConfigService> _configServiceMock;
    private readonly StorageConfigController _controller;

    public StorageConfigControllerTests()
    {
        _loggerMock = new Mock<ILogger<StorageConfigController>>();
        _configServiceMock = new Mock<IStorageConfigService>();
        _controller = new StorageConfigController(_loggerMock.Object, _configServiceMock.Object);
    }

    [Fact]
    public async Task TestConfig_WithInvalidProvider_ShouldReturnBadRequest()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "InvalidProvider"
        };

        // Act
        var result = await _controller.TestConfig(config);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        
        // Use reflection to check the anonymous object
        var successProperty = response.GetType().GetProperty("success");
        var messageProperty = response.GetType().GetProperty("message");
        
        Assert.NotNull(successProperty);
        Assert.NotNull(messageProperty);
        Assert.False((bool)successProperty.GetValue(response));
        Assert.Contains("Unknown storage provider", messageProperty.GetValue(response).ToString());
    }

    [Fact]
    public async Task TestConfig_WithLocalProvider_ShouldCallTestLocalStorage()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "LOCAL",
            Local = new LocalStorageConfig
            {
                BaseDirectory = Path.GetTempPath()
            }
        };

        // Act
        var result = await _controller.TestConfig(config);

        // Assert
        // Should either return OK (success) or BadRequest (but not the "not implemented" message)
        Assert.True(result is OkObjectResult || result is BadRequestObjectResult);
        
        if (result is BadRequestObjectResult badRequest)
        {
            var response = badRequest.Value;
            var messageProperty = response.GetType().GetProperty("message");
            Assert.NotNull(messageProperty);
            var message = messageProperty.GetValue(response).ToString();
            Assert.DoesNotContain("not yet implemented", message);
        }
    }

    [Fact]
    public async Task TestConfig_WithAwsProvider_InvalidConfig_ShouldReturnBadRequest()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "", // Empty bucket name should cause validation error
                Region = "us-east-1"
            }
        };

        // Act
        var result = await _controller.TestConfig(config);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        
        var successProperty = response.GetType().GetProperty("success");
        var messageProperty = response.GetType().GetProperty("message");
        
        Assert.NotNull(successProperty);
        Assert.NotNull(messageProperty);
        Assert.False((bool)successProperty.GetValue(response));
        
        var message = messageProperty.GetValue(response).ToString();
        // Should get a configuration error, not "not yet implemented"
        Assert.DoesNotContain("not yet implemented", message);
        Assert.True(message.Contains("BucketName") || message.Contains("required") || message.Contains("AWS"));
    }

    [Fact] 
    public async Task TestConfig_WithAwsProvider_ValidConfig_ShouldNotReturnNotImplemented()
    {
        // Arrange
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "test-bucket",
                Region = "us-east-1",
                AccessKeyId = "fake-key-id",
                SecretAccessKey = "fake-secret-key"
            }
        };

        // Act
        var result = await _controller.TestConfig(config);

        // Assert
        // Should return BadRequest with actual AWS error, not "not implemented"
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        
        var successProperty = response.GetType().GetProperty("success");
        var messageProperty = response.GetType().GetProperty("message");
        
        Assert.NotNull(successProperty);
        Assert.NotNull(messageProperty);
        Assert.False((bool)successProperty.GetValue(response));
        
        var message = messageProperty.GetValue(response).ToString();
        // The key test: should NOT contain "not yet implemented"
        Assert.DoesNotContain("AWS storage testing not yet implemented", message);
        // Should contain some kind of AWS-related error instead
        Assert.True(message.Contains("AWS") || message.Contains("S3") || message.Contains("error") || message.Contains("connection"));
    }
}