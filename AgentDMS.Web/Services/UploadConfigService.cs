using AgentDMS.Web.Models;
using System.Text.Json;

namespace AgentDMS.Web.Services;

/// <summary>
/// Service for managing upload configuration runtime updates
/// </summary>
public interface IUploadConfigService
{
    /// <summary>
    /// Gets the current upload configuration asynchronously
    /// </summary>
    /// <returns>The current upload configuration</returns>
    Task<UploadConfig> GetConfigAsync();
    
    /// <summary>
    /// Updates the upload configuration asynchronously
    /// </summary>
    /// <param name="config">The new configuration to save</param>
    Task UpdateConfigAsync(UploadConfig config);
    
    /// <summary>
    /// Event raised when the configuration changes
    /// </summary>
    event EventHandler<UploadConfig>? ConfigChanged;
}

/// <summary>
/// Implementation of upload configuration service
/// </summary>
public class UploadConfigService : IUploadConfigService
{
    private readonly string _configFilePath;
    private readonly ILogger<UploadConfigService> _logger;
    private readonly IConfiguration _configuration;
    private UploadConfig? _cachedConfig;
    
    /// <summary>
    /// Event raised when the configuration changes
    /// </summary>
    public event EventHandler<UploadConfig>? ConfigChanged;

    /// <summary>
    /// Initializes a new instance of the UploadConfigService class
    /// </summary>
    /// <param name="env">The web host environment</param>
    /// <param name="configuration">The configuration instance</param>
    /// <param name="logger">The logger instance</param>
    public UploadConfigService(IWebHostEnvironment env, IConfiguration configuration, ILogger<UploadConfigService> logger)
    {
        _configFilePath = Path.Combine(env.ContentRootPath, "App_Data", "uploadconfig.json");
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current upload configuration asynchronously
    /// </summary>
    /// <returns>The current upload configuration</returns>
    public async Task<UploadConfig> GetConfigAsync()
    {
        if (_cachedConfig == null)
        {
            await LoadConfigAsync();
        }
        
        return _cachedConfig ?? CreateDefaultConfig();
    }

    /// <summary>
    /// Updates the upload configuration asynchronously
    /// </summary>
    /// <param name="config">The new configuration to save</param>
    public async Task UpdateConfigAsync(UploadConfig config)
    {
        try
        {
            // Validate configuration
            ValidateConfig(config);
            
            // Ensure App_Data directory exists
            var directory = Path.GetDirectoryName(_configFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }

            // Serialize and save configuration
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_configFilePath, json);
            
            // Update cached config
            _cachedConfig = config;
            
            // Notify subscribers of config change
            ConfigChanged?.Invoke(this, config);
            
            _logger.LogInformation("Upload configuration updated successfully. MaxFileSize: {MaxFileSize}MB, MaxRequestBodySize: {MaxRequestBodySize}MB", 
                config.MaxFileSizeMB, config.MaxRequestBodySizeMB);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating upload configuration");
            throw;
        }
    }

    private async Task LoadConfigAsync()
    {
        try
        {
            UploadConfig? config = null;
            
            // Try to load from file first
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                config = JsonSerializer.Deserialize<UploadConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            
            // If no file config, create from appsettings
            config ??= CreateConfigFromAppSettings();
            
            _cachedConfig = config ?? CreateDefaultConfig();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading upload configuration, using defaults");
            _cachedConfig = CreateDefaultConfig();
        }
    }
    
    private UploadConfig CreateConfigFromAppSettings()
    {
        var config = new UploadConfig();
        
        // Try to bind from appsettings.json UploadLimits section
        _configuration.GetSection("UploadLimits").Bind(config);
        
        // Check for environment variables as fallback (following existing pattern)
        var envMaxSize = Environment.GetEnvironmentVariable("AGENTDMS_MAX_FILE_SIZE_MB");
        if (!string.IsNullOrEmpty(envMaxSize) && double.TryParse(envMaxSize, out var maxSize))
        {
            config.MaxFileSizeMB = Math.Max(1, maxSize);
            // Set request body size to be at least as large as file size
            if (config.MaxRequestBodySizeBytes < config.MaxFileSizeBytes)
            {
                config.MaxRequestBodySizeBytes = config.MaxFileSizeBytes;
            }
            // Set multipart body length to be at least as large as file size
            if (config.MaxMultipartBodyLengthBytes < config.MaxFileSizeBytes)
            {
                config.MaxMultipartBodyLengthBytes = config.MaxFileSizeBytes;
            }
        }
        
        var envMaxRequestSize = Environment.GetEnvironmentVariable("AGENTDMS_MAX_REQUEST_SIZE_MB");
        if (!string.IsNullOrEmpty(envMaxRequestSize) && double.TryParse(envMaxRequestSize, out var maxRequestSize))
        {
            config.MaxRequestBodySizeMB = Math.Max(1, maxRequestSize);
        }
        
        return config;
    }
    
    private static UploadConfig CreateDefaultConfig()
    {
        return new UploadConfig
        {
            MaxFileSizeBytes = 100 * 1024 * 1024, // 100MB
            MaxRequestBodySizeBytes = 100 * 1024 * 1024, // 100MB
            MaxMultipartBodyLengthBytes = 100 * 1024 * 1024, // 100MB
            ApplySizeLimits = true
        };
    }
    
    private static void ValidateConfig(UploadConfig config)
    {
        if (config.MaxFileSizeBytes < 1024)
        {
            throw new ArgumentException("MaxFileSizeBytes must be at least 1KB");
        }
        
        if (config.MaxRequestBodySizeBytes < config.MaxFileSizeBytes)
        {
            throw new ArgumentException("MaxRequestBodySizeBytes should be at least as large as MaxFileSizeBytes");
        }
        
        if (config.MaxMultipartBodyLengthBytes < config.MaxFileSizeBytes)
        {
            throw new ArgumentException("MaxMultipartBodyLengthBytes should be at least as large as MaxFileSizeBytes");
        }
    }
}