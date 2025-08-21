using Microsoft.AspNetCore.Mvc;
using AgentDMS.Web.Controllers;
using Xunit;

namespace AgentDMS.Tests.Web.Controllers;

/// <summary>
/// Tests for API documentation controller
/// </summary>
public class ApiDocumentationControllerTests
{
    [Fact]
    public void GetApiInfo_Should_Return_Valid_ApiInfo()
    {
        // Arrange
        var controller = new ApiDocumentationController();

        // Act
        var result = controller.GetApiInfo();

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var apiInfo = Assert.IsType<ApiInfoResponse>(okResult.Value);
        
        Assert.Equal("AgentDMS API", apiInfo.ApplicationName);
        Assert.Equal("1.0.0", apiInfo.Version);
        Assert.NotEmpty(apiInfo.Description);
        Assert.NotEmpty(apiInfo.Endpoints);
        Assert.Contains(apiInfo.Endpoints, e => e.Controller == "ImageProcessing");
        Assert.Contains(apiInfo.Endpoints, e => e.Controller == "MistralConfig");
    }

    [Fact]
    public void GetHealth_Should_Return_Valid_HealthStatus()
    {
        // Arrange
        var controller = new ApiDocumentationController();

        // Act
        var result = controller.GetHealth();

        // Assert
        Assert.NotNull(result);
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var health = Assert.IsType<HealthResponse>(okResult.Value);
        
        Assert.Equal("Healthy", health.Status);
        Assert.Equal("1.0.0", health.Version);
        Assert.True(health.Timestamp <= DateTime.UtcNow);
    }
}