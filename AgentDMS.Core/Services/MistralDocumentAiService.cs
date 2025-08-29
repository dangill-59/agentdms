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
/// Service for integrating with Mistral LLM API for document classification and data capture
/// </summary>
/// <remarks>
/// This service provides document AI capabilities including:
/// - Document classification (invoice, contract, receipt, etc.)
/// - Key-value data extraction from document text
/// - Confidence scoring for AI predictions
/// 
/// Configuration Requirements:
/// - API Key: Set via environment variable MISTRAL_API_KEY or inject via constructor
/// - Endpoint: Default is "https://api.mistral.ai/v1/chat/completions" or configure via constructor
/// 
/// Example Usage:
/// <code>
/// // In Program.cs or Startup.cs:
/// builder.Services.AddSingleton&lt;MistralDocumentAiService&gt;(provider =>
/// {
///     var logger = provider.GetService&lt;ILogger&lt;MistralDocumentAiService&gt;&gt;();
///     var httpClient = provider.GetService&lt;HttpClient&gt;();
///     var apiKey = Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
///     return new MistralDocumentAiService(httpClient, apiKey, logger: logger);
/// });
/// 
/// // In your service or controller:
/// var result = await mistralService.AnalyzeDocumentAsync(extractedText, cancellationToken);
/// if (result.Success)
/// {
///     Console.WriteLine($"Document Type: {result.DocumentType}");
///     foreach (var kvp in result.ExtractedData)
///     {
///         Console.WriteLine($"{kvp.Key}: {kvp.Value}");
///     }
/// }
/// </code>
/// </remarks>
public class MistralDocumentAiService
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _endpoint;
    private readonly ILogger<MistralDocumentAiService>? _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly PerformanceCache _cache;

    /// <summary>
    /// Initializes a new instance of the MistralDocumentAiService
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls</param>
    /// <param name="apiKey">Mistral API key (can be null if provided via environment variable)</param>
    /// <param name="endpoint">API endpoint URL (optional, uses default if not provided)</param>
    /// <param name="logger">Logger instance (optional)</param>
    /// <param name="cache">Performance cache instance (optional, creates new if not provided)</param>
    public MistralDocumentAiService(
        HttpClient httpClient,
        string? apiKey = null,
        string? endpoint = null,
        ILogger<MistralDocumentAiService>? logger = null,
        PerformanceCache? cache = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
        _endpoint = endpoint ?? "https://api.mistral.ai/v1/chat/completions";
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
    /// Analyzes document text for classification and data extraction
    /// </summary>
    /// <param name="documentText">Extracted text from the document</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Document AI analysis result</returns>
    public async Task<DocumentAiResult> AnalyzeDocumentAsync(string documentText, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(documentText))
            {
                return DocumentAiResult.Failed("Document text is empty or null");
            }

            if (string.IsNullOrEmpty(_apiKey))
            {
                _logger?.LogWarning("Mistral API key not configured. Skipping AI analysis.");
                return DocumentAiResult.Failed("API key not configured");
            }

            // Check cache first
            var cacheKey = PerformanceCache.GenerateKey(documentText, "ai");
            var cachedResult = _cache.Get<DocumentAiResult>(cacheKey);
            if (cachedResult != null)
            {
                _logger?.LogInformation("Returning cached AI analysis result for text length: {TextLength}", documentText.Length);
                return cachedResult;
            }

            _logger?.LogInformation("Starting Mistral AI document analysis for text length: {TextLength}", documentText.Length);

            var prompt = BuildAnalysisPrompt(documentText);
            var request = new MistralChatRequest
            {
                Model = "mistral-large-latest", // Use the latest Mistral model
                Messages = new List<MistralMessage>
                {
                    new MistralMessage
                    {
                        Role = "user",
                        Content = prompt
                    }
                },
                Temperature = 0.1 // Low temperature for consistent results
            };

            var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            var startTime = DateTime.UtcNow;
            var response = await _httpClient.PostAsync(_endpoint, content, cancellationToken);
            var processingTime = DateTime.UtcNow - startTime;

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger?.LogError("Mistral API error: {StatusCode} - {Error}", response.StatusCode, errorContent);
                return DocumentAiResult.Failed($"API error: {response.StatusCode}");
            }

            var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            var mistralResponse = JsonSerializer.Deserialize<MistralChatResponse>(responseJson, _jsonOptions);

            if (mistralResponse?.Choices?.FirstOrDefault()?.Message?.Content == null)
            {
                return DocumentAiResult.Failed("Invalid response from Mistral API");
            }

            var analysisResult = ParseAnalysisResult(mistralResponse.Choices.First().Message.Content);
            analysisResult.ProcessingTime = processingTime;

            // Cache successful results
            _cache.Set(cacheKey, analysisResult, TimeSpan.FromHours(2)); // Cache for 2 hours

            _logger?.LogInformation("Mistral AI analysis completed in {ProcessingTime}ms. Document type: {DocumentType}", 
                processingTime.TotalMilliseconds, analysisResult.DocumentType);

            return analysisResult;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during Mistral AI document analysis");
            return DocumentAiResult.Failed($"Analysis error: {ex.Message}");
        }
    }

    /// <summary>
    /// Builds the analysis prompt for document classification and data extraction
    /// </summary>
    private string BuildAnalysisPrompt(string documentText)
    {
        return $@"Please analyze the following document text and provide a structured response in JSON format.

Classify the document type (invoice, receipt, contract, letter, report, form, etc.) and extract key data points.

Document text:
{documentText}

Please respond with a JSON object in this exact format:
{{
  ""documentType"": ""[document type]"",
  ""confidence"": [0.0-1.0],
  ""extractedData"": {{
    ""key1"": ""value1"",
    ""key2"": ""value2""
  }},
  ""summary"": ""[brief description of document content]""
}}

For extractedData, please identify and extract relevant fields based on document type:
- For invoices: amount, date, vendor, invoice_number, etc.
- For receipts: total, date, merchant, items, etc.  
- For contracts: parties, date, amount, terms, etc.
- For other documents: relevant fields based on content

Provide confidence score based on text clarity and completeness.";
    }

    /// <summary>
    /// Parses the Mistral API response into a structured result
    /// </summary>
    private DocumentAiResult ParseAnalysisResult(string content)
    {
        try
        {
            // Try to extract JSON from the response
            var jsonStart = content.IndexOf('{');
            var jsonEnd = content.LastIndexOf('}');
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                
                var parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent, options);
                
                if (parsed == null)
                {
                    return DocumentAiResult.Failed("Failed to parse AI response");
                }
                
                var result = new DocumentAiResult
                {
                    Success = true,
                    DocumentType = parsed.TryGetValue("documentType", out var docType) ? docType.GetString() ?? "unknown" : "unknown",
                    Confidence = parsed.TryGetValue("confidence", out var conf) ? conf.GetDouble() : 0.0,
                    Summary = parsed.TryGetValue("summary", out var sum) ? sum.GetString() ?? "" : "",
                    ExtractedData = new Dictionary<string, string>()
                };

                if (parsed.TryGetValue("extractedData", out var extractedData) && extractedData.ValueKind == JsonValueKind.Object)
                {
                    foreach (var property in extractedData.EnumerateObject())
                    {
                        result.ExtractedData[property.Name] = property.Value.GetString() ?? "";
                    }
                }

                return result;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to parse Mistral AI response as JSON: {Content}", content);
        }

        // Fallback: create basic result from content
        return new DocumentAiResult
        {
            Success = true,
            DocumentType = "unknown",
            Confidence = 0.5,
            Summary = content.Length > 200 ? content.Substring(0, 200) + "..." : content,
            ExtractedData = new Dictionary<string, string>()
        };
    }
}

/// <summary>
/// Represents the result of document AI analysis
/// </summary>
public class DocumentAiResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public string Summary { get; set; } = string.Empty;
    public Dictionary<string, string> ExtractedData { get; set; } = new();
    public TimeSpan ProcessingTime { get; set; }

    public static DocumentAiResult Failed(string message)
    {
        return new DocumentAiResult
        {
            Success = false,
            Message = message
        };
    }
}

/// <summary>
/// Internal models for Mistral API communication
/// </summary>
internal class MistralChatRequest
{
    public string Model { get; set; } = string.Empty;
    public List<MistralMessage> Messages { get; set; } = new();
    public double Temperature { get; set; } = 0.1;
}

internal class MistralMessage
{
    public string Role { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}

internal class MistralChatResponse
{
    public List<MistralChoice> Choices { get; set; } = new();
}

internal class MistralChoice
{
    public MistralMessage Message { get; set; } = new();
}