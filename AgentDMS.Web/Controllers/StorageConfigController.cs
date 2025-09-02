using Microsoft.AspNetCore.Mvc;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services.Storage;
using AgentDMS.Core.Services;
using AgentDMS.Web.Services;
using Swashbuckle.AspNetCore.Annotations;

namespace AgentDMS.Web.Controllers;

/// <summary>
/// Controller for managing storage configuration settings
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("Storage configuration management for output destinations")]
public class StorageConfigController : ControllerBase
{
    private readonly ILogger<StorageConfigController> _logger;
    private readonly IStorageConfigService _configService;
    private readonly IStorageService _storageService;

    /// <summary>
    /// Initializes a new instance of the StorageConfigController
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="configService">Storage configuration service</param>
    /// <param name="storageService">Storage service that needs to be refreshed when config changes</param>
    public StorageConfigController(ILogger<StorageConfigController> logger, IStorageConfigService configService, IStorageService storageService)
    {
        _logger = logger;
        _configService = configService;
        _storageService = storageService;
    }

    /// <summary>
    /// Get current storage configuration settings
    /// </summary>
    /// <returns>Current storage configuration</returns>
    /// <response code="200">Configuration retrieved successfully</response>
    /// <response code="500">Error reading configuration</response>
    [HttpGet]
    [SwaggerOperation(Summary = "Get storage configuration", Description = "Retrieves the current storage configuration settings")]
    [SwaggerResponse(200, "Current configuration", typeof(StorageConfig))]
    [SwaggerResponse(500, "Error reading configuration")]
    [ProducesResponseType(typeof(StorageConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StorageConfig>> GetConfig()
    {
        try
        {
            var config = await _configService.GetConfigAsync();
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading storage configuration");
            return StatusCode(500, new { error = "Failed to read configuration", message = ex.Message });
        }
    }

    /// <summary>
    /// Update storage configuration settings
    /// </summary>
    /// <param name="config">New configuration settings</param>
    /// <returns>Updated configuration</returns>
    /// <response code="200">Configuration updated successfully</response>
    /// <response code="400">Invalid configuration data</response>
    /// <response code="500">Error updating configuration</response>
    [HttpPost]
    [SwaggerOperation(Summary = "Update storage configuration", Description = "Updates the storage configuration settings with new values")]
    [SwaggerResponse(200, "Configuration updated", typeof(StorageConfig))]
    [SwaggerResponse(400, "Invalid configuration data")]
    [SwaggerResponse(500, "Error updating configuration")]
    [ProducesResponseType(typeof(StorageConfig), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StorageConfig>> UpdateConfig([FromBody] StorageConfig config)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            await _configService.UpdateConfigAsync(config);
            
            // Refresh the storage service to use the new configuration
            await _storageService.RefreshProviderAsync();

            _logger.LogInformation("Storage configuration updated successfully");
            return Ok(config);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating storage configuration");
            return StatusCode(500, new { error = "Failed to update configuration", message = ex.Message });
        }
    }

    /// <summary>
    /// Test storage configuration by validating provider accessibility
    /// </summary>
    /// <param name="config">Configuration to test</param>
    /// <returns>Test result indicating success or failure</returns>
    /// <response code="200">Configuration test successful</response>
    /// <response code="400">Invalid configuration or test failed</response>
    [HttpPost("test")]
    [SwaggerOperation(Summary = "Test storage configuration", Description = "Validates the storage configuration by testing connectivity and permissions")]
    [SwaggerResponse(200, "Configuration test successful")]
    [SwaggerResponse(400, "Invalid configuration or test failed")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> TestConfig([FromBody] StorageConfig config)
    {
        try
        {
            // Test the configuration based on provider type
            switch (config.Provider.ToUpper())
            {
                case "LOCAL":
                    return await TestLocalStorage(config);
                case "AWS":
                    return await TestAwsStorage(config);
                case "AZURE":
                    return await TestAzureStorage(config);
                default:
                    return BadRequest(new { success = false, message = $"Unknown storage provider: {config.Provider}" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing storage configuration");
            return BadRequest(new { success = false, message = "Configuration test failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Test local storage configuration
    /// </summary>
    private async Task<ActionResult> TestLocalStorage(StorageConfig config)
    {
        try
        {
            var testDirectory = config.Local.BaseDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Output");
            
            if (!Directory.Exists(testDirectory))
            {
                Directory.CreateDirectory(testDirectory);
            }

            // Test write permissions
            var testFile = Path.Combine(testDirectory, $"test_{Guid.NewGuid()}.txt");
            await System.IO.File.WriteAllTextAsync(testFile, "Test file for storage validation");
            
            if (System.IO.File.Exists(testFile))
            {
                System.IO.File.Delete(testFile);
                return Ok(new { success = true, message = "Local storage test successful", directory = testDirectory });
            }
            else
            {
                return BadRequest(new { success = false, message = "Failed to write test file to local storage" });
            }
        }
        catch (Exception ex)
        {
            return BadRequest(new { success = false, message = "Local storage test failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Test AWS storage configuration
    /// </summary>
    private async Task<ActionResult> TestAwsStorage(StorageConfig config)
    {
        try
        {
            _logger.LogInformation("Testing AWS storage configuration for bucket: {BucketName}", config.Aws.BucketName);
            
            // Create the AWS storage provider with the provided configuration
            using var awsProvider = new AwsStorageProvider(config.Aws);
            
            // Test basic connectivity by attempting to list objects (this will validate credentials and bucket access)
            await awsProvider.ListFilesAsync("");
            
            // Test write permissions by creating a small test file
            var testPath = $"agentdms-test/{Guid.NewGuid()}.txt";
            var testContent = System.Text.Encoding.UTF8.GetBytes($"AgentDMS connectivity test - {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
            
            var savedPath = await awsProvider.SaveFileAsync(testContent, testPath, "text/plain");
            
            // Verify the file exists
            var fileExists = await awsProvider.FileExistsAsync(testPath);
            if (!fileExists)
            {
                return BadRequest(new { success = false, message = "Failed to verify test file creation in AWS S3" });
            }
            
            // Clean up the test file
            await awsProvider.DeleteFileAsync(testPath);
            
            _logger.LogInformation("AWS storage test successful for bucket: {BucketName}", config.Aws.BucketName);
            
            return Ok(new { 
                success = true, 
                message = "AWS storage test successful", 
                bucket = config.Aws.BucketName,
                region = config.Aws.Region,
                testFile = savedPath
            });
        }
        catch (Amazon.S3.AmazonS3Exception s3Ex)
        {
            _logger.LogError(s3Ex, "AWS S3 error during storage test: {ErrorCode}", s3Ex.ErrorCode);
            
            return s3Ex.ErrorCode switch
            {
                "NoSuchBucket" => BadRequest(new { success = false, message = $"AWS S3 bucket '{config.Aws.BucketName}' does not exist", errorCode = s3Ex.ErrorCode }),
                "AccessDenied" => BadRequest(new { success = false, message = "Access denied to AWS S3 bucket. Check your credentials and permissions", errorCode = s3Ex.ErrorCode }),
                "InvalidAccessKeyId" => BadRequest(new { success = false, message = "Invalid AWS access key ID", errorCode = s3Ex.ErrorCode }),
                "SignatureDoesNotMatch" => BadRequest(new { success = false, message = "Invalid AWS secret access key", errorCode = s3Ex.ErrorCode }),
                "InvalidBucketName" => BadRequest(new { success = false, message = $"Invalid AWS S3 bucket name: {config.Aws.BucketName}", errorCode = s3Ex.ErrorCode }),
                _ => BadRequest(new { success = false, message = $"AWS S3 error: {s3Ex.Message}", errorCode = s3Ex.ErrorCode, details = s3Ex.ToString() })
            };
        }
        catch (Amazon.Runtime.AmazonServiceException awsEx)
        {
            _logger.LogError(awsEx, "AWS service error during storage test");
            return BadRequest(new { success = false, message = $"AWS service error: {awsEx.Message}", errorCode = awsEx.ErrorCode });
        }
        catch (Amazon.Runtime.AmazonClientException clientEx)
        {
            _logger.LogError(clientEx, "AWS client error during storage test");
            return BadRequest(new { success = false, message = $"AWS connection error: {clientEx.Message}. Check your network connection and AWS region settings." });
        }
        catch (ArgumentException argEx)
        {
            _logger.LogError(argEx, "Invalid AWS configuration");
            return BadRequest(new { success = false, message = $"Invalid AWS configuration: {argEx.Message}" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during AWS storage test");
            return BadRequest(new { success = false, message = "AWS storage test failed", details = ex.Message });
        }
    }

    /// <summary>
    /// Test Azure storage configuration (placeholder)
    /// </summary>
    private async Task<ActionResult> TestAzureStorage(StorageConfig config)
    {
        await Task.Delay(1); // Avoid async warning
        return BadRequest(new { success = false, message = "Azure storage testing not yet implemented" });
    }
}