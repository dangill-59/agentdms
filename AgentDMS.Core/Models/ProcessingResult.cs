using System;
using System.Text.Json.Serialization;
using AgentDMS.Core.Services;

namespace AgentDMS.Core.Models;

/// <summary>
/// Represents the result of an image processing operation
/// </summary>
public class ProcessingResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ImageFile? ProcessedImage { get; set; }
    public List<ImageFile>? SplitPages { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public TimeSpan? RenderingTime { get; set; }
    public ProcessingMetrics? Metrics { get; set; }
    
    /// <summary>
    /// AI analysis result from document classification and data extraction
    /// </summary>
    public DocumentAiResult? AiAnalysis { get; set; }
    
    /// <summary>
    /// OCR extracted text from the document
    /// </summary>
    public string? ExtractedText { get; set; }
    
    [JsonIgnore]
    public Exception? Error { get; set; }
    
    public static ProcessingResult Successful(ImageFile image, TimeSpan processingTime, string? message = null)
    {
        return new ProcessingResult
        {
            Success = true,
            ProcessedImage = image,
            ProcessingTime = processingTime,
            Message = message ?? "Processing completed successfully",
            Metrics = new ProcessingMetrics 
            { 
                ProcessingTime = processingTime,
                StartTime = DateTime.UtcNow - processingTime
            }
        };
    }
    
    public static ProcessingResult Failed(string message, Exception? error = null)
    {
        return new ProcessingResult
        {
            Success = false,
            Message = message,
            Error = error
        };
    }
}

/// <summary>
/// Detailed metrics for processing operations
/// </summary>
public class ProcessingMetrics
{
    public DateTime StartTime { get; set; }
    public TimeSpan ProcessingTime { get; set; }
    public TimeSpan? FileLoadTime { get; set; }
    public TimeSpan? ImageDecodeTime { get; set; }
    public TimeSpan? ConversionTime { get; set; }
    public TimeSpan? ThumbnailGenerationTime { get; set; }
    public TimeSpan? TotalProcessingTime { get; set; }
    
    /// <summary>
    /// Time taken for AI document analysis
    /// </summary>
    public TimeSpan? AiAnalysisTime { get; set; }
}