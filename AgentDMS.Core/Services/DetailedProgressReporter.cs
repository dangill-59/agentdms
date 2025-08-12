using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// Enhanced progress reporter that provides detailed progress information
/// </summary>
public class DetailedProgressReporter
{
    private readonly Func<ProgressReport, Task>? _onProgress;
    
    public string JobId { get; }
    public Func<ProgressReport, Task>? OnProgress => _onProgress;
    
    public DetailedProgressReporter(string jobId, Func<ProgressReport, Task>? onProgress = null)
    {
        JobId = jobId;
        _onProgress = onProgress;
    }

    public async Task ReportProgress(
        string fileName,
        ProgressStatus status,
        string statusMessage,
        int currentFile = 0,
        int totalFiles = 1,
        int currentPage = 0,
        int totalPages = 1,
        string? errorMessage = null)
    {
        var progress = new ProgressReport
        {
            JobId = JobId,
            FileName = fileName,
            Status = status,
            StatusMessage = statusMessage,
            CurrentFile = currentFile,
            TotalFiles = totalFiles,
            CurrentPage = currentPage,
            TotalPages = totalPages,
            ProgressPercentage = CalculateProgress(currentFile, totalFiles, currentPage, totalPages),
            Timestamp = DateTime.UtcNow,
            ErrorMessage = errorMessage
        };

        if (_onProgress != null)
        {
            await _onProgress(progress);
        }
    }

    private double CalculateProgress(int currentFile, int totalFiles, int currentPage, int totalPages)
    {
        if (totalFiles <= 0) return 0;

        // For single file operations
        if (totalFiles == 1)
        {
            if (totalPages <= 0) return 0;
            return (double)currentPage / totalPages * 100;
        }

        // For batch operations
        var filesCompleted = Math.Max(0, currentFile - 1);
        var fileProgress = totalPages > 0 ? (double)currentPage / totalPages : 0;
        var totalProgress = (filesCompleted + fileProgress) / totalFiles;
        
        return Math.Min(100, Math.Max(0, totalProgress * 100));
    }
}