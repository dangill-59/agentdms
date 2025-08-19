using Microsoft.AspNetCore.Mvc;
using AgentDMS.Web.Models;
using AgentDMS.Web.Services;
using System.Text.Json;

namespace AgentDMS.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MistralConfigController : ControllerBase
{
    private readonly ILogger<MistralConfigController> _logger;
    private readonly IMistralConfigService _configService;

    public MistralConfigController(ILogger<MistralConfigController> logger, IMistralConfigService configService)
    {
        _logger = logger;
        _configService = configService;
    }

    /// <summary>
    /// Get current Mistral configuration settings
    /// </summary>
    [HttpGet]
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
    [HttpPost]
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
    [HttpPost("test")]
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
                temperature = config.Temperature,
                max_tokens = 1
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