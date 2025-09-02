using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services;
using AgentDMS.Web.Controllers;
using AgentDMS.Web.Services;
using Xunit;

namespace AgentDMS.Tests.Integration;

/// <summary>
/// Integration test to demonstrate that AWS storage testing is now implemented
/// </summary>
public class AwsStorageTestImplementationTest
{
    [Fact]
    public async Task TestAwsStorage_WithValidConfig_NoLongerReturnsNotImplementedMessage()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<StorageConfigController>>();
        var configServiceMock = new Mock<IStorageConfigService>();
        var storageServiceMock = new Mock<IStorageService>();
        var controller = new StorageConfigController(loggerMock.Object, configServiceMock.Object, storageServiceMock.Object);
        
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
        var result = await controller.TestConfig(config);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        
        // Get the message from the anonymous response object
        var messageProperty = response.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(response)?.ToString();
        
        // The critical test: The old placeholder message should no longer appear
        Assert.DoesNotContain("AWS storage testing not yet implemented", message);
        
        // Instead, we should get actual AWS-related error messages
        Assert.True(
            message?.Contains("AWS") == true ||
            message?.Contains("S3") == true ||
            message?.Contains("connection") == true ||
            message?.Contains("error") == true,
            $"Expected AWS-related error message, but got: {message}"
        );
    }
    
    [Fact]
    public async Task TestAwsStorage_WithMissingBucketName_ReturnsValidationError()
    {
        // Arrange
        var loggerMock = new Mock<ILogger<StorageConfigController>>();
        var configServiceMock = new Mock<IStorageConfigService>();
        var storageServiceMock = new Mock<IStorageService>();
        var controller = new StorageConfigController(loggerMock.Object, configServiceMock.Object, storageServiceMock.Object);
        
        var config = new StorageConfig
        {
            Provider = "AWS",
            Aws = new AwsStorageConfig
            {
                BucketName = "", // Invalid: empty bucket name
                Region = "us-east-1"
            }
        };

        // Act
        var result = await controller.TestConfig(config);

        // Assert
        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = badRequestResult.Value;
        
        var messageProperty = response.GetType().GetProperty("message");
        Assert.NotNull(messageProperty);
        var message = messageProperty.GetValue(response)?.ToString();
        
        // Should get configuration validation error, not "not implemented"
        Assert.DoesNotContain("not yet implemented", message);
        Assert.True(
            message?.Contains("BucketName") == true || 
            message?.Contains("required") == true,
            $"Expected validation error about BucketName, but got: {message}"
        );
    }
}