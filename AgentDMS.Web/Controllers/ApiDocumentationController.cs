using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace AgentDMS.Web.Controllers;

/// <summary>
/// Controller for providing API documentation and information
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("API documentation and information")]
public class ApiDocumentationController : ControllerBase
{
    /// <summary>
    /// Get API information and available endpoints overview
    /// </summary>
    /// <returns>API information including available endpoints and their descriptions</returns>
    /// <response code="200">API information retrieved successfully</response>
    [HttpGet("info")]
    [SwaggerOperation(Summary = "Get API information", Description = "Returns overview of available API endpoints and their functionality")]
    [SwaggerResponse(200, "API information", typeof(ApiInfoResponse))]
    [ProducesResponseType(typeof(ApiInfoResponse), StatusCodes.Status200OK)]
    public ActionResult<ApiInfoResponse> GetApiInfo()
    {
        var apiInfo = new ApiInfoResponse
        {
            ApplicationName = "AgentDMS API",
            Version = "1.0.0",
            Description = "A comprehensive API for AgentDMS - Image Processing and Document Management System",
            SwaggerUrl = "/swagger/v1/swagger.json",
            SwaggerUiUrl = "/swagger",
            Endpoints = new List<EndpointInfo>
            {
                new EndpointInfo
                {
                    Controller = "ImageProcessing",
                    Path = "/api/imageprocessing",
                    Description = "Image processing operations including upload, scanning, and batch processing",
                    Methods = new[] { "GET", "POST" }
                },
                new EndpointInfo
                {
                    Controller = "MistralConfig",
                    Path = "/api/mistralconfig",
                    Description = "Mistral LLM configuration management",
                    Methods = new[] { "GET", "POST" }
                },
                new EndpointInfo
                {
                    Controller = "ApiDocumentation",
                    Path = "/api/apidocumentation",
                    Description = "API documentation and information",
                    Methods = new[] { "GET" }
                }
            },
            Features = new[]
            {
                "Multi-format image processing (JPEG, PNG, BMP, GIF, TIFF, PDF, WebP)",
                "Document scanning with TWAIN support",
                "Batch processing with progress tracking",
                "Thumbnail gallery generation",
                "Mistral LLM integration for document analysis",
                "Real-time progress updates via SignalR",
                "Background job processing"
            }
        };

        return Ok(apiInfo);
    }

    /// <summary>
    /// Get health status of the API
    /// </summary>
    /// <returns>API health status</returns>
    /// <response code="200">API is healthy</response>
    [HttpGet("health")]
    [SwaggerOperation(Summary = "Get API health status", Description = "Returns the current health status of the API")]
    [SwaggerResponse(200, "API health status", typeof(HealthResponse))]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> GetHealth()
    {
        return Ok(new HealthResponse
        {
            Status = "Healthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0.0",
            Environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"
        });
    }
}

/// <summary>
/// API information response model
/// </summary>
public class ApiInfoResponse
{
    /// <summary>
    /// Application name
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// API version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// API description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Swagger JSON specification URL
    /// </summary>
    public string SwaggerUrl { get; set; } = string.Empty;

    /// <summary>
    /// Swagger UI URL
    /// </summary>
    public string SwaggerUiUrl { get; set; } = string.Empty;

    /// <summary>
    /// Available API endpoints
    /// </summary>
    public List<EndpointInfo> Endpoints { get; set; } = new();

    /// <summary>
    /// List of application features
    /// </summary>
    public string[] Features { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Endpoint information model
/// </summary>
public class EndpointInfo
{
    /// <summary>
    /// Controller name
    /// </summary>
    public string Controller { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint path
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Endpoint description
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Supported HTTP methods
    /// </summary>
    public string[] Methods { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Health status response model
/// </summary>
public class HealthResponse
{
    /// <summary>
    /// Health status
    /// </summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Response timestamp
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// API version
    /// </summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>
    /// Environment name
    /// </summary>
    public string Environment { get; set; } = string.Empty;
}