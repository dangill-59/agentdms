using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// Interface for broadcasting progress updates (moved from Web layer)
/// </summary>
public interface IProgressBroadcaster
{
    Task BroadcastProgress(string jobId, ProgressReport progress);
}

/// <summary>
/// Represents a background processing job
/// </summary>
public class ProcessingJob
{
    public string JobId { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public JobStatus Status { get; set; } = JobStatus.Queued;
    public string? ErrorMessage { get; set; }
    public ProcessingResult? Result { get; set; }
}

/// <summary>
/// Status of a processing job
/// </summary>
public enum JobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

/// <summary>
/// Interface for background job processing
/// </summary>
public interface IBackgroundJobService
{
    Task<string> EnqueueJobAsync(string filePath);
    Task<ProcessingJob?> GetJobStatusAsync(string jobId);
    Task<ProcessingResult?> GetJobResultAsync(string jobId);
}

/// <summary>
/// Background service that processes image processing jobs
/// </summary>
public class BackgroundJobService : BackgroundService, IBackgroundJobService
{
    private readonly ConcurrentDictionary<string, ProcessingJob> _jobs = new();
    private readonly ConcurrentQueue<ProcessingJob> _jobQueue = new();
    private readonly SemaphoreSlim _queueSemaphore = new(0);
    private readonly ImageProcessingService _imageProcessingService;
    private readonly IProgressBroadcaster _progressBroadcaster;
    private readonly ILogger<BackgroundJobService> _logger;

    public BackgroundJobService(
        ImageProcessingService imageProcessingService,
        IProgressBroadcaster progressBroadcaster,
        ILogger<BackgroundJobService> logger)
    {
        _imageProcessingService = imageProcessingService;
        _progressBroadcaster = progressBroadcaster;
        _logger = logger;
    }

    public Task<string> EnqueueJobAsync(string filePath)
    {
        var jobId = Guid.NewGuid().ToString();
        var job = new ProcessingJob
        {
            JobId = jobId,
            FilePath = filePath,
            CreatedAt = DateTime.UtcNow,
            Status = JobStatus.Queued
        };

        _jobs[jobId] = job;
        _jobQueue.Enqueue(job);
        _queueSemaphore.Release();

        _logger.LogInformation("Enqueued job {JobId} for file {FilePath}", jobId, filePath);
        return Task.FromResult(jobId);
    }

    public Task<ProcessingJob?> GetJobStatusAsync(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    public Task<ProcessingResult?> GetJobResultAsync(string jobId)
    {
        _jobs.TryGetValue(jobId, out var job);
        return Task.FromResult(job?.Result);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Background job service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            await _queueSemaphore.WaitAsync(stoppingToken);

            if (_jobQueue.TryDequeue(out var job))
            {
                _ = Task.Run(async () => await ProcessJobAsync(job, stoppingToken), stoppingToken);
            }
        }

        _logger.LogInformation("Background job service stopped");
    }

    private async Task ProcessJobAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting processing job {JobId}", job.JobId);
            job.Status = JobStatus.Processing;

            // Create progress reporter for real-time updates
            var progressReporter = new DetailedProgressReporter(job.JobId, async (progress) =>
            {
                await _progressBroadcaster.BroadcastProgress(job.JobId, progress);
            });

            // Process the image
            var result = await _imageProcessingService.ProcessImageAsync(job.FilePath, progressReporter, cancellationToken);

            job.Result = result;
            job.Status = result.Success ? JobStatus.Completed : JobStatus.Failed;
            job.ErrorMessage = result.Success ? null : result.Message;

            _logger.LogInformation("Completed processing job {JobId} with status {Status}", job.JobId, job.Status);

            // Clean up temp file if it exists and processing is complete
            if (File.Exists(job.FilePath) && job.FilePath.StartsWith(Path.GetTempPath()))
            {
                try
                {
                    File.Delete(job.FilePath);
                    _logger.LogDebug("Deleted temporary file {FilePath}", job.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to delete temporary file {FilePath}", job.FilePath);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing job {JobId}", job.JobId);
            job.Status = JobStatus.Failed;
            job.ErrorMessage = ex.Message;

            // Report failure via SignalR
            await _progressBroadcaster.BroadcastProgress(job.JobId, new Models.ProgressReport
            {
                JobId = job.JobId,
                Status = Models.ProgressStatus.Failed,
                StatusMessage = "Processing failed",
                ErrorMessage = ex.Message
            });
        }
    }
}