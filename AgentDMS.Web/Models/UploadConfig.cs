using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace AgentDMS.Web.Models;

/// <summary>
/// Configuration model for file upload settings
/// </summary>
[SwaggerSchema("Configuration settings for file upload limits and behavior")]
public class UploadConfig
{
    /// <summary>
    /// Maximum file size allowed for uploads in bytes
    /// </summary>
    /// <example>104857600</example>
    [Range(1024, long.MaxValue)]
    [Display(Name = "Max File Size (bytes)")]
    [SwaggerSchema("Maximum file size allowed for uploads in bytes (minimum 1KB)")]
    public long MaxFileSizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB default
    
    /// <summary>
    /// Maximum request body size for ASP.NET Core in bytes
    /// </summary>
    /// <example>104857600</example>
    [Range(1024, long.MaxValue)]
    [Display(Name = "Max Request Body Size (bytes)")]
    [SwaggerSchema("Maximum request body size for ASP.NET Core in bytes (should be >= MaxFileSizeBytes)")]
    public long MaxRequestBodySizeBytes { get; set; } = 100 * 1024 * 1024; // 100MB default
    
    /// <summary>
    /// Maximum multipart body length limit in bytes
    /// </summary>
    /// <example>104857600</example>
    [Range(1024, long.MaxValue)]
    [Display(Name = "Max Multipart Body Length (bytes)")]
    [SwaggerSchema("Maximum multipart body length limit in bytes for form uploads")]
    public long MaxMultipartBodyLengthBytes { get; set; } = 100 * 1024 * 1024; // 100MB default
    
    /// <summary>
    /// Whether to apply size limits to all upload endpoints
    /// </summary>
    /// <example>true</example>
    [Display(Name = "Apply Size Limits")]
    [SwaggerSchema("Whether to apply configured size limits to all upload endpoints")]
    public bool ApplySizeLimits { get; set; } = true;
    
    /// <summary>
    /// Maximum file size in MB for easier configuration
    /// </summary>
    [SwaggerSchema("Maximum file size in MB for easier configuration")]
    public double MaxFileSizeMB
    {
        get => MaxFileSizeBytes / (1024.0 * 1024.0);
        set => MaxFileSizeBytes = (long)(value * 1024 * 1024);
    }
    
    /// <summary>
    /// Maximum request body size in MB for easier configuration
    /// </summary>
    [SwaggerSchema("Maximum request body size in MB for easier configuration")]
    public double MaxRequestBodySizeMB
    {
        get => MaxRequestBodySizeBytes / (1024.0 * 1024.0);
        set => MaxRequestBodySizeBytes = (long)(value * 1024 * 1024);
    }
    
    /// <summary>
    /// Maximum multipart body length in MB for easier configuration
    /// </summary>
    [SwaggerSchema("Maximum multipart body length in MB for easier configuration")]
    public double MaxMultipartBodyLengthMB
    {
        get => MaxMultipartBodyLengthBytes / (1024.0 * 1024.0);
        set => MaxMultipartBodyLengthBytes = (long)(value * 1024 * 1024);
    }
}