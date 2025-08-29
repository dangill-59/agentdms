using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// Service for integrating with Mistral OCR API for optical character recognition
/// </summary>
/// <remarks>
/// This service provides OCR capabilities using Mistral's OCR API including:
/// - Text extraction from document images
/// - Support for various document formats
/// - Base64 image data handling
/// - Integration with mistral-ocr-latest model
/// 
/// Configuration Requirements:
/// - API Key: Set via environment variable MISTRAL_API_KEY or inject via constructor
/// - Endpoint: Default is "https://api.mistral.ai/v1/ocr/process" or configure via constructor
/// 
/// Example Usage:
/// <code>
/// var result = await ocrService.ProcessDocumentAsync(imageData, cancellationToken);
/// if (result.Success)
/// {
///     Console.WriteLine($"Extracted Text: {result.Text}");
/// }
/// </code>
/// </remarks>
public class MistralOcrService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _endpoint;
    private readonly ILogger<MistralOcrService>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly PerformanceCache _cache;

    /// <summary>
    /// Initializes a new instance of the MistralOcrService
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls</param>
    /// <param name="apiKey">Mistral API key (can be null if provided via environment variable)</param>
    /// <param name="endpoint">API endpoint URL (optional, uses default if not provided)</param>
    /// <param name="logger">Logger instance (optional)</param>
    /// <param name="cache">Performance cache instance (optional, creates new if not provided)</param>
    public MistralOcrService(
        HttpClient httpClient,
        string? apiKey = null,
        string? endpoint = null,
        ILogger<MistralOcrService>? logger = null,
        PerformanceCache? cache = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
        _endpoint = endpoint ?? "https://api.mistral.ai/v1/ocr/process";
        _logger = logger;
        _cache = cache ?? new PerformanceCache(logger);
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        // Configure HttpClient for optimal performance
        if (!string.IsNullOrEmpty(_apiKey))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        }
        
        // Optimize HTTP client for performance
        _httpClient.Timeout = TimeSpan.FromMinutes(2); // Set reasonable timeout
        _httpClient.DefaultRequestHeaders.ConnectionClose = false; // Keep connection alive for reuse
    }

    /// <summary>
    /// Processes a document for OCR using Mistral API
    /// </summary>
    /// <param name="documentData">Document data (file path, URL, or base64 data)</param>
    /// <param name="model">OCR model to use (default: mistral-ocr-latest)</param>
    /// <param name="includeImageBase64">Include base64 image data in response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OCR processing result</returns>
    public async Task<OcrResult> ProcessDocumentAsync(
        string documentData, 
        string model = "mistral-ocr-latest",
        bool includeImageBase64 = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(documentData))
            {
                return OcrResult.Failed("Document data is empty or null");
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger?.LogWarning("Mistral API key not configured. Skipping OCR processing.");
                return OcrResult.Failed("API key not configured");
            }

            // Check cache first (combine document data and model for cache key)
            var cacheKey = PerformanceCache.GenerateKey($"{documentData}:{model}:{includeImageBase64}", "ocr");
            var cachedResult = _cache.Get<OcrResult>(cacheKey);
            if (cachedResult != null)
            {
                _logger?.LogInformation("Returning cached OCR result for model: {Model}", model);
                return cachedResult;
            }

            _logger?.LogInformation("Starting Mistral OCR processing with model: {Model}", model);

            var request = new MistralOcrRequest
            {
                Model = model,
                Document = documentData,
                IncludeImageBase64 = includeImageBase64
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var startTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
            var processingTime = DateTime.UtcNow - startTime;

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogError("Mistral OCR API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return OcrResult.Failed($"API error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var ocrResponse = JsonSerializer.Deserialize<MistralOcrResponse>(responseJson, _jsonOptions);

            if (ocrResponse?.Text == null)
            {
                return OcrResult.Failed("Invalid response from Mistral OCR API");
            }

            var result = new OcrResult
            {
                Success = true,
                Text = ocrResponse.Text,
                Confidence = ocrResponse.Confidence,
                ProcessingTime = processingTime,
                ImageBase64 = ocrResponse.ImageBase64
            };

            // Cache successful results
            _cache.Set(cacheKey, result, TimeSpan.FromHours(1)); // Cache for 1 hour

            _logger?.LogInformation("Mistral OCR processing completed in {ProcessingTime}ms. Extracted {TextLength} characters", 
                processingTime.TotalMilliseconds, result.Text.Length);

            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during Mistral OCR processing");
            return OcrResult.Failed($"Processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes a document from a file path
    /// </summary>
    /// <param name="filePath">Path to the document file</param>
    /// <param name="model">OCR model to use</param>
    /// <param name="includeImageBase64">Include base64 image data in response</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>OCR processing result</returns>
    public async Task<OcrResult> ProcessDocumentFromFileAsync(
        string filePath,
        string model = "mistral-ocr-latest", 
        bool includeImageBase64 = false,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return OcrResult.Failed($"File not found: {filePath}");
            }

            // Convert file to base64 for API
            var fileBytes = await File.ReadAllBytesAsync(filePath, cancellationToken);
            var base64Data = Convert.ToBase64String(fileBytes);
            var mimeType = GetMimeType(filePath);
            var dataUrl = $"data:{mimeType};base64,{base64Data}";

            return await ProcessDocumentAsync(dataUrl, model, includeImageBase64, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing document from file: {FilePath}", filePath);
            return OcrResult.Failed($"File processing error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets MIME type based on file extension
    /// </summary>
    private static string GetMimeType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// Represents the result of OCR processing
/// </summary>
public class OcrResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public string? ImageBase64 { get; set; }

    public static OcrResult Failed(string message)
    {
        return new OcrResult
        {
            Success = false,
            Message = message
        };
    }
}

/// <summary>
/// Internal models for Mistral OCR API communication
/// </summary>
internal class MistralOcrRequest
{
    public string Model { get; set; } = string.Empty;
    public string Document { get; set; } = string.Empty;
    public bool IncludeImageBase64 { get; set; } = true;
}

internal class MistralOcrResponse
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string? ImageBase64 { get; set; }
}