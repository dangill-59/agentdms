using System;

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
    public Exception? Error { get; set; }
    
    public static ProcessingResult Successful(ImageFile image, TimeSpan processingTime, string? message = null)
    {
        return new ProcessingResult
        {
            Success = true,
            ProcessedImage = image,
            ProcessingTime = processingTime,
            Message = message ?? "Processing completed successfully"
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