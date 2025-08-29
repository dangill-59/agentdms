using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;

namespace AgentDMS.Web.Models;

/// <summary>
/// Configuration model for Mistral LLM integration settings
/// </summary>
[SwaggerSchema("Configuration settings for Mistral LLM integration")]
public class MistralConfig
{
    /// <summary>
    /// Mistral API key for authentication
    /// </summary>
    /// <example>sk-xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx</example>
    [Display(Name = "API Key")]
    [SwaggerSchema("Mistral API key for authentication")]
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Mistral API endpoint URL
    /// </summary>
    /// <example>https://api.mistral.ai/v1/chat/completions</example>
    [Required]
    [Display(Name = "Endpoint")]
    [Url]
    [SwaggerSchema("Mistral API endpoint URL")]
    public string Endpoint { get; set; } = "https://api.mistral.ai/v1/chat/completions";
    
    /// <summary>
    /// Mistral model to use for processing
    /// </summary>
    /// <example>mistral-small</example>
    [Required]
    [Display(Name = "Model")]
    [SwaggerSchema("Mistral model identifier (e.g., mistral-small, mistral-medium, mistral-large)")]
    public string Model { get; set; } = "mistral-small";
    
    /// <summary>
    /// Temperature setting for response generation (0.0 - 2.0)
    /// </summary>
    /// <example>0.1</example>
    [Range(0.0, 2.0)]
    [Display(Name = "Temperature")]
    [SwaggerSchema("Temperature for response generation (0.0 = deterministic, 2.0 = very creative)")]
    public double Temperature { get; set; } = 0.1;
    
    /// <summary>
    /// Enable Mistral OCR processing instead of Tesseract
    /// </summary>
    [Display(Name = "Enable Mistral OCR")]
    [SwaggerSchema("Use Mistral OCR API instead of local Tesseract OCR")]
    public bool EnableOcrProcessing { get; set; } = false;
    
    /// <summary>
    /// Mistral OCR model to use for processing
    /// </summary>
    /// <example>mistral-ocr-latest</example>
    [Display(Name = "OCR Model")]
    [SwaggerSchema("Mistral OCR model identifier")]
    public string OcrModel { get; set; } = "mistral-ocr-latest";
    
    /// <summary>
    /// Include base64 image data in OCR response
    /// </summary>
    [Display(Name = "Include Image Base64")]
    [SwaggerSchema("Include base64 encoded image data in OCR response")]
    public bool IncludeImageBase64 { get; set; } = true;
    
    /// <summary>
    /// HTTP request timeout in seconds for Mistral API calls
    /// </summary>
    /// <example>300</example>
    [Range(30, 1800)]
    [Display(Name = "Request Timeout (seconds)")]
    [SwaggerSchema("HTTP request timeout in seconds for Mistral API calls (30-1800 seconds)")]
    public int TimeoutSeconds { get; set; } = 300;
}