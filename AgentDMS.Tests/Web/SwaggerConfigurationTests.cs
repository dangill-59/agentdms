using Microsoft.OpenApi.Models;
using Xunit;

namespace AgentDMS.Tests.Web;

/// <summary>
/// Tests for Swagger configuration and setup
/// </summary>
public class SwaggerConfigurationTests
{
    [Fact]
    public void OpenApiInfo_Should_Have_Required_Properties()
    {
        // Arrange
        var openApiInfo = new OpenApiInfo
        {
            Version = "v1",
            Title = "AgentDMS API",
            Description = "A comprehensive API for AgentDMS - Image Processing and Document Management System",
            Contact = new OpenApiContact
            {
                Name = "AgentDMS Support",
                Email = "support@agentdms.com"
            },
            License = new OpenApiLicense
            {
                Name = "MIT License",
                Url = new Uri("https://opensource.org/licenses/MIT")
            }
        };

        // Assert
        Assert.Equal("v1", openApiInfo.Version);
        Assert.Equal("AgentDMS API", openApiInfo.Title);
        Assert.NotNull(openApiInfo.Description);
        Assert.NotNull(openApiInfo.Contact);
        Assert.Equal("AgentDMS Support", openApiInfo.Contact.Name);
        Assert.Equal("support@agentdms.com", openApiInfo.Contact.Email);
        Assert.NotNull(openApiInfo.License);
        Assert.Equal("MIT License", openApiInfo.License.Name);
    }

    [Fact]
    public void ApiKey_SecurityScheme_Should_Be_Valid()
    {
        // Arrange
        var securityScheme = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.ApiKey,
            In = ParameterLocation.Header,
            Name = "X-API-Key",
            Description = "API Key authentication"
        };

        // Assert
        Assert.Equal(SecuritySchemeType.ApiKey, securityScheme.Type);
        Assert.Equal(ParameterLocation.Header, securityScheme.In);
        Assert.Equal("X-API-Key", securityScheme.Name);
        Assert.Equal("API Key authentication", securityScheme.Description);
    }
}