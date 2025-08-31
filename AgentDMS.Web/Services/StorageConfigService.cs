using AgentDMS.Core.Models;
using System.Text.Json;

namespace AgentDMS.Web.Services;

/// <summary>
/// Service for managing storage configuration runtime updates
/// </summary>
public interface IStorageConfigService
{
    /// <summary>
    /// Gets the current storage configuration asynchronously
    /// </summary>
    /// <returns>The current storage configuration</returns>
    Task<StorageConfig> GetConfigAsync();
    
    /// <summary>
    /// Updates the storage configuration asynchronously
    /// </summary>
    /// <param name="config">The new configuration to save</param>
    Task UpdateConfigAsync(StorageConfig config);
    
    /// <summary>
    /// Event raised when the configuration changes
    /// </summary>
    event EventHandler<StorageConfig>? ConfigChanged;
}

/// <summary>
/// Implementation of storage configuration service
/// </summary>
public class StorageConfigService : IStorageConfigService
{
    private readonly string _configFilePath;
    private readonly ILogger<StorageConfigService> _logger;
    private StorageConfig? _cachedConfig;
    
    /// <summary>
    /// Event raised when the configuration changes
    /// </summary>
    public event EventHandler<StorageConfig>? ConfigChanged;

    /// <summary>
    /// Initializes a new instance of the StorageConfigService class
    /// </summary>
    /// <param name="env">The web host environment</param>
    /// <param name="logger">The logger instance</param>
    public StorageConfigService(IWebHostEnvironment env, ILogger<StorageConfigService> logger)
    {
        _configFilePath = Path.Combine(env.ContentRootPath, "App_Data", "storageconfig.json");
        _logger = logger;
        
        // Ensure the App_Data directory exists
        var appDataPath = Path.GetDirectoryName(_configFilePath);
        if (!string.IsNullOrEmpty(appDataPath) && !Directory.Exists(appDataPath))
        {
            Directory.CreateDirectory(appDataPath);
        }
    }

    /// <summary>
    /// Gets the current storage configuration asynchronously
    /// </summary>
    /// <returns>The current storage configuration</returns>
    public async Task<StorageConfig> GetConfigAsync()
    {
        if (_cachedConfig != null)
        {
            return _cachedConfig;
        }

        try
        {
            if (File.Exists(_configFilePath))
            {
                var jsonContent = await File.ReadAllTextAsync(_configFilePath);
                var config = JsonSerializer.Deserialize<StorageConfig>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                
                _cachedConfig = config ?? new StorageConfig();
            }
            else
            {
                _cachedConfig = new StorageConfig(); // Default configuration
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reading storage configuration from {ConfigPath}", _configFilePath);
            _cachedConfig = new StorageConfig(); // Fallback to default
        }

        return _cachedConfig;
    }

    /// <summary>
    /// Updates the storage configuration asynchronously
    /// </summary>
    /// <param name="config">The new configuration to save</param>
    public async Task UpdateConfigAsync(StorageConfig config)
    {
        try
        {
            var jsonContent = JsonSerializer.Serialize(config, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(_configFilePath, jsonContent);
            _cachedConfig = config;
            
            // Raise the configuration changed event
            ConfigChanged?.Invoke(this, config);
            
            _logger.LogInformation("Storage configuration updated successfully at {ConfigPath}", _configFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving storage configuration to {ConfigPath}", _configFilePath);
            throw;
        }
    }
}