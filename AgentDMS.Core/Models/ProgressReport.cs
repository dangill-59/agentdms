using System;

namespace AgentDMS.Core.Models;

/// <summary>
/// Represents a detailed progress report for real-time updates
/// </summary>
public class ProgressReport
{
    public string JobId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public ProgressStatus Status { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
    public int CurrentFile { get; set; }
    public int TotalFiles { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    public double ProgressPercentage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Status types for progress reporting
/// </summary>
public enum ProgressStatus
{
    Starting,
    LoadingFile,
    ProcessingFile,
    ConvertingPage,
    GeneratingThumbnail,
    Completed,
    Failed
}