using Microsoft.AspNetCore.Mvc;
using AgentDMS.Web.Models;
using AgentDMS.Web.Services;
using Swashbuckle.AspNetCore.Annotations;
using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Web.Controllers;

/// <summary>
/// Controller for managing upload configuration settings
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("Upload configuration management for file size limits and server settings")]
public class UploadConfigController : ControllerBase
{
    private readonly IUploadConfigService _uploadConfigService;
    private readonly ILogger<UploadConfigController> _logger;

    /// <summary>
    /// Initializes a new instance of the UploadConfigController
    /// </summary>
    /// <param name="uploadConfigService">The upload configuration service</param>
    /// <param name="logger">The logger instance</param>
    public UploadConfigController(IUploadConfigService uploadConfigService, ILogger<UploadConfigController> logger)
    {
        _uploadConfigService = uploadConfigService;
        _logger = logger;
    }

    /// <summary>
    /// Get current upload configuration
    /// </summary>
    /// <returns>Current upload configuration settings</returns>
    /// <response code="200">Returns the current upload configuration</response>
    /// <response code="500">Internal server error</response>
    [HttpGet]
    [SwaggerOperation(Summary = "Get upload configuration", Description = "Retrieves the current upload configuration including file size limits")]
    [SwaggerResponse(200, "Upload configuration retrieved successfully", typeof(UploadConfig))]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(UploadConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadConfig>> GetConfig()
    {
        try
        {
            var config = await _uploadConfigService.GetConfigAsync();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upload configuration");
            return StatusCode(500, new { error = "Failed to retrieve upload configuration", message = ex.Message });
        }
    }

    /// <summary>
    /// Update upload configuration
    /// </summary>
    /// <param name="config">New upload configuration settings</param>
    /// <returns>Success response with updated configuration</returns>
    /// <response code="200">Configuration updated successfully</response>
    /// <response code="400">Invalid configuration data</response>
    /// <response code="500">Internal server error</response>
    [HttpPost]
    [SwaggerOperation(Summary = "Update upload configuration", Description = "Updates the upload configuration settings. Note: Some changes may require server restart to take full effect.")]
    [SwaggerResponse(200, "Configuration updated successfully", typeof(UploadConfigUpdateResponse))]
    [SwaggerResponse(400, "Invalid configuration data")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(UploadConfigUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadConfigUpdateResponse>> UpdateConfig([FromBody] UploadConfig config)
    {
        try
        {
            // Validate the model
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            // Additional validation
            if (config.MaxRequestBodySizeBytes < config.MaxFileSizeBytes)
            {
                return BadRequest(new { error = "MaxRequestBodySizeBytes must be at least as large as MaxFileSizeBytes" });
            }

            if (config.MaxMultipartBodyLengthBytes < config.MaxFileSizeBytes)
            {
                return BadRequest(new { error = "MaxMultipartBodyLengthBytes must be at least as large as MaxFileSizeBytes" });
            }

            await _uploadConfigService.UpdateConfigAsync(config);

            _logger.LogInformation("Upload configuration updated by API request. MaxFileSize: {MaxFileSize}MB", config.MaxFileSizeMB);

            return Ok(new UploadConfigUpdateResponse
            {
                Success = true,
                Message = "Upload configuration updated successfully",
                UpdatedConfig = config,
                RequiresRestart = false // File upload service will pick up changes dynamically
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid upload configuration provided");
            return BadRequest(new { error = "Invalid configuration", message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating upload configuration");
            return StatusCode(500, new { error = "Failed to update upload configuration", message = ex.Message });
        }
    }

    /// <summary>
    /// Reset upload configuration to defaults
    /// </summary>
    /// <returns>Success response with default configuration</returns>
    /// <response code="200">Configuration reset to defaults successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpPost("reset")]
    [SwaggerOperation(Summary = "Reset upload configuration", Description = "Resets the upload configuration to default values")]
    [SwaggerResponse(200, "Configuration reset successfully", typeof(UploadConfigUpdateResponse))]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(UploadConfigUpdateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadConfigUpdateResponse>> ResetConfig()
    {
        try
        {
            var defaultConfig = new UploadConfig
            {
                MaxFileSizeBytes = 100 * 1024 * 1024, // 100MB
                MaxRequestBodySizeBytes = 100 * 1024 * 1024, // 100MB
                MaxMultipartBodyLengthBytes = 100 * 1024 * 1024, // 100MB
                ApplySizeLimits = true
            };

            await _uploadConfigService.UpdateConfigAsync(defaultConfig);

            _logger.LogInformation("Upload configuration reset to defaults by API request");

            return Ok(new UploadConfigUpdateResponse
            {
                Success = true,
                Message = "Upload configuration reset to defaults successfully",
                UpdatedConfig = defaultConfig,
                RequiresRestart = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resetting upload configuration");
            return StatusCode(500, new { error = "Failed to reset upload configuration", message = ex.Message });
        }
    }

    /// <summary>
    /// Get upload configuration info including environment variables
    /// </summary>
    /// <returns>Detailed configuration information</returns>
    /// <response code="200">Configuration info retrieved successfully</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("info")]
    [SwaggerOperation(Summary = "Get upload configuration info", Description = "Retrieves detailed information about upload configuration including environment variable overrides")]
    [SwaggerResponse(200, "Configuration info retrieved successfully", typeof(UploadConfigInfo))]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(UploadConfigInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UploadConfigInfo>> GetConfigInfo()
    {
        try
        {
            var config = await _uploadConfigService.GetConfigAsync();
            
            var info = new UploadConfigInfo
            {
                CurrentConfig = config,
                EnvironmentVariables = new Dictionary<string, string?>
                {
                    ["AGENTDMS_MAX_FILE_SIZE_MB"] = Environment.GetEnvironmentVariable("AGENTDMS_MAX_FILE_SIZE_MB"),
                    ["AGENTDMS_MAX_REQUEST_SIZE_MB"] = Environment.GetEnvironmentVariable("AGENTDMS_MAX_REQUEST_SIZE_MB")
                },
                ConfigurationSource = "Runtime configuration file with appsettings.json fallback",
                LastModified = GetConfigFileLastModified()
            };

            return Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving upload configuration info");
            return StatusCode(500, new { error = "Failed to retrieve configuration info", message = ex.Message });
        }
    }

    private DateTime? GetConfigFileLastModified()
    {
        try
        {
            var configFilePath = Path.Combine(Directory.GetCurrentDirectory(), "App_Data", "uploadconfig.json");
            if (System.IO.File.Exists(configFilePath))
            {
                return System.IO.File.GetLastWriteTime(configFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error getting config file last modified time");
        }
        return null;
    }
}

/// <summary>
/// Response model for upload configuration updates
/// </summary>
public class UploadConfigUpdateResponse
{
    /// <summary>
    /// Whether the update was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Result message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// The updated configuration
    /// </summary>
    public UploadConfig? UpdatedConfig { get; set; }

    /// <summary>
    /// Whether a server restart is required for changes to take effect
    /// </summary>
    public bool RequiresRestart { get; set; }
}

/// <summary>
/// Detailed information about upload configuration
/// </summary>
public class UploadConfigInfo
{
    /// <summary>
    /// Current active configuration
    /// </summary>
    public UploadConfig CurrentConfig { get; set; } = new();

    /// <summary>
    /// Environment variables that can override configuration
    /// </summary>
    public Dictionary<string, string?> EnvironmentVariables { get; set; } = new();

    /// <summary>
    /// Source of the current configuration
    /// </summary>
    public string ConfigurationSource { get; set; } = string.Empty;

    /// <summary>
    /// When the configuration file was last modified
    /// </summary>
    public DateTime? LastModified { get; set; }
}