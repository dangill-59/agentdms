using Microsoft.AspNetCore.Mvc;
using AgentDMS.Web.Models;
using AgentDMS.Web.Services;
using System.Text.Json;
using Swashbuckle.AspNetCore.Annotations;

namespace AgentDMS.Web.Controllers;

/// <summary>
/// Controller for managing Mistral LLM configuration settings
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("Mistral LLM configuration management")]
public class MistralConfigController : ControllerBase
{
    private readonly ILogger<MistralConfigController> _logger;
    private readonly IMistralConfigService _configService;

    /// <summary>
    /// Initializes a new instance of the MistralConfigController
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="configService">Mistral configuration service</param>
    public MistralConfigController(ILogger<MistralConfigController> logger, IMistralConfigService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// Get current Mistral configuration settings
    /// </summary>
    /// <returns>Current Mistral LLM configuration</returns>
    /// <response code="200">Configuration retrieved successfully</response>
    /// <response code="500">Error reading configuration</response>
    [HttpGet]
    [SwaggerOperation(Summary = "Get Mistral configuration", Description = "Retrieves the current Mistral LLM configuration settings")]
    [SwaggerResponse(200, "Current configuration", typeof(MistralConfig))]
    [SwaggerResponse(500, "Error reading configuration")]
    [ProducesResponseType(typeof(MistralConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MistralConfig>> GetConfig()
    {
        try
        {
            var config = await _configService.GetConfigAsync();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading Mistral configuration");
            return StatusCode(500, new { error = "Failed to read configuration", message = ex.Message });
        }
    }

    /// <summary>
    /// Update Mistral configuration settings
    /// </summary>
    /// <param name="config">New configuration settings</param>
    /// <returns>Updated configuration</returns>
    /// <response code="200">Configuration updated successfully</response>
    /// <response code="400">Invalid configuration data</response>
    /// <response code="500">Error updating configuration</response>
    [HttpPost]
    [SwaggerOperation(Summary = "Update Mistral configuration", Description = "Updates the Mistral LLM configuration settings with new values")]
    [SwaggerResponse(200, "Configuration updated", typeof(MistralConfig))]
    [SwaggerResponse(400, "Invalid configuration data")]
    [SwaggerResponse(500, "Error updating configuration")]
    [ProducesResponseType(typeof(MistralConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MistralConfig>> UpdateConfig([FromBody] MistralConfig config)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _configService.UpdateConfigAsync(config);

            _logger.LogInformation("Mistral configuration updated successfully");
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Mistral configuration");
            return StatusCode(500, new { error = "Failed to update configuration", message = ex.Message });
        }
    }

    /// <summary>
    /// Test Mistral configuration by validating endpoint accessibility
    /// </summary>
    /// <param name="config">Configuration to test</param>
    /// <returns>Test result indicating success or failure</returns>
    /// <response code="200">Configuration test successful</response>
    /// <response code="400">Invalid configuration or test failed</response>
    [HttpPost("test")]
    [SwaggerOperation(Summary = "Test Mistral configuration", Description = "Validates the Mistral configuration by testing connectivity to the API endpoint")]
    [SwaggerResponse(200, "Configuration test successful")]
    [SwaggerResponse(400, "Invalid configuration or test failed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> TestConfig([FromBody] MistralConfig config)
    {
        try
        {
            if (string.IsNullOrEmpty(config.ApiKey))
            {
                return BadRequest(new { error = "API Key is required for testing" });
            }

            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.ApiKey);

            // Simple test request to validate API key and endpoint
            var testRequest = new
            {
                model = config.Model,
                messages = new[] { new { role = "user", content = "test" } },
                temperature = config.Temperature
            };

            var requestJson = JsonSerializer.Serialize(testRequest, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var content = new StringContent(requestJson, System.Text.Encoding.UTF8, "application/json");
            var response = await httpClient.PostAsync(config.Endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                return Ok(new { success = true, message = "Configuration test successful" });
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                return BadRequest(new { success = false, message = $"API test failed: {response.StatusCode}", details = errorContent });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Mistral configuration");
            return BadRequest(new { success = false, message = "Configuration test failed", details = ex.Message });
        }
    }
}