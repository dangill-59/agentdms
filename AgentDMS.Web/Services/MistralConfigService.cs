using AgentDMS.Web.Models;
using System.Text.Json;

namespace AgentDMS.Web.Services;

/// <summary>
/// Service for managing Mistral configuration runtime updates
/// </summary>
public interface IMistralConfigService
{
    Task<MistralConfig> GetConfigAsync();
    Task UpdateConfigAsync(MistralConfig config);
    event EventHandler<MistralConfig>? ConfigChanged;
}

/// <summary>
/// Implementation of Mistral configuration service
/// </summary>
public class MistralConfigService : IMistralConfigService
{
    private readonly string _configFilePath;
    private readonly ILogger<MistralConfigService> _logger;
    private MistralConfig? _cachedConfig;
    
    public event EventHandler<MistralConfig>? ConfigChanged;

    public MistralConfigService(IWebHostEnvironment env, ILogger<MistralConfigService> logger)
    {
        _configFilePath = Path.Combine(env.ContentRootPath, "App_Data", "mistralconfig.json");
        _logger = logger;
    }

    public async Task<MistralConfig> GetConfigAsync()
    {
        if (_cachedConfig == null)
        {
            await LoadConfigFromFileAsync();
        }
        
        return _cachedConfig ?? new MistralConfig();
    }

    public async Task UpdateConfigAsync(MistralConfig config)
    {
        try
        {
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
            
            _logger.LogInformation("Mistral configuration updated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating Mistral configuration");
            throw;
        }
    }

    private async Task LoadConfigFromFileAsync()
    {
        try
        {
            if (File.Exists(_configFilePath))
            {
                var json = await File.ReadAllTextAsync(_configFilePath);
                _cachedConfig = JsonSerializer.Deserialize<MistralConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new MistralConfig();
            }
            else
            {
                _cachedConfig = new MistralConfig();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading Mistral configuration, using defaults");
            _cachedConfig = new MistralConfig();
        }
    }
}