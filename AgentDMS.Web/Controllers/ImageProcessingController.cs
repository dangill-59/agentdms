using Microsoft.AspNetCore.Mvc;
using AgentDMS.Core.Services;
using AgentDMS.Core.Models;
using AgentDMS.Core.Utilities;
using AgentDMS.Web.Hubs;
using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Web.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImageProcessingController : ControllerBase
{
    private readonly ImageProcessingService _imageProcessor;
    private readonly FileUploadService _fileUploadService;
    private readonly IProgressBroadcaster _progressBroadcaster;
    private readonly ILogger<ImageProcessingController> _logger;

    public ImageProcessingController(
        ImageProcessingService imageProcessor, 
        FileUploadService fileUploadService,
        IProgressBroadcaster progressBroadcaster,
        ILogger<ImageProcessingController> logger)
    {
        _imageProcessor = imageProcessor;
        _fileUploadService = fileUploadService;
        _progressBroadcaster = progressBroadcaster;
        _logger = logger;
    }

    [HttpGet("formats")]
    public ActionResult<IEnumerable<string>> GetSupportedFormats()
    {
        return Ok(ImageProcessingService.GetSupportedExtensions());
    }

    [HttpPost("upload")]
    public async Task<ActionResult<ProcessingResult>> UploadAndProcessImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

        var jobId = Guid.NewGuid().ToString();

        try
        {
            // Save uploaded file temporarily
            var tempPath = Path.GetTempFileName();
            var originalName = Path.GetFileNameWithoutExtension(file.FileName);
            var extension = Path.GetExtension(file.FileName);
            var tempFilePath = Path.ChangeExtension(tempPath, extension);

            using (var stream = new FileStream(tempFilePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            // Create progress reporter for real-time updates
            var progressReporter = new DetailedProgressReporter(jobId, async (progress) =>
            {
                await _progressBroadcaster.BroadcastProgress(jobId, progress);
            });

            // Process the image with progress updates
            var result = await _imageProcessor.ProcessImageAsync(tempFilePath, progressReporter);
            
            // Clean up temp file
            System.IO.File.Delete(tempFilePath);

            // Add job ID to response
            return Ok(new { jobId, result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing uploaded file");
            
            // Report failure via SignalR
            await _progressBroadcaster.BroadcastProgress(jobId, new ProgressReport
            {
                JobId = jobId,
                Status = ProgressStatus.Failed,
                StatusMessage = "Processing failed",
                ErrorMessage = ex.Message
            });
            
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost("process")]
    public async Task<ActionResult<ProcessingResult>> ProcessImageByPath([FromBody] ProcessImageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FilePath))
        {
            return BadRequest(new { error = "File path is required" });
        }

        if (!System.IO.File.Exists(request.FilePath))
        {
            return NotFound(new { error = "File not found" });
        }

        var jobId = Guid.NewGuid().ToString();

        try
        {
            // Create progress reporter for real-time updates
            var progressReporter = new DetailedProgressReporter(jobId, async (progress) =>
            {
                await _progressBroadcaster.BroadcastProgress(jobId, progress);
            });

            var result = await _imageProcessor.ProcessImageAsync(request.FilePath, progressReporter);
            
            return Ok(new { jobId, result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing file: {FilePath}", request.FilePath);
            
            // Report failure via SignalR
            await _progressBroadcaster.BroadcastProgress(jobId, new ProgressReport
            {
                JobId = jobId,
                Status = ProgressStatus.Failed,
                StatusMessage = "Processing failed",
                ErrorMessage = ex.Message
            });
            
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost("batch-process")]
    public async Task<ActionResult<IEnumerable<ProcessingResult>>> ProcessMultipleImages([FromBody] BatchProcessRequest request)
    {
        if (request.FilePaths == null || !request.FilePaths.Any())
        {
            return BadRequest(new { error = "File paths are required" });
        }

        var jobId = Guid.NewGuid().ToString();

        try
        {
            var filePathsList = request.FilePaths.ToList();
            
            var progress = new Progress<int>(processed => 
            {
                _logger.LogInformation("Batch job {JobId}: Processed {Count}/{Total} files", jobId, processed, filePathsList.Count);
            });

            // Create progress reporter for real-time updates
            var progressReporter = new DetailedProgressReporter(jobId, async (progressReport) =>
            {
                await _progressBroadcaster.BroadcastProgress(jobId, progressReport);
            });

            var results = await _imageProcessor.ProcessMultipleImagesAsync(filePathsList, progressReporter, progress);
            
            // Report completion
            await _progressBroadcaster.BroadcastProgress(jobId, new ProgressReport
            {
                JobId = jobId,
                Status = ProgressStatus.Completed,
                StatusMessage = "Batch processing completed",
                CurrentFile = filePathsList.Count,
                TotalFiles = filePathsList.Count,
                ProgressPercentage = 100
            });

            return Ok(new { jobId, results });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing multiple files");
            
            // Report failure via SignalR
            await _progressBroadcaster.BroadcastProgress(jobId, new ProgressReport
            {
                JobId = jobId,
                Status = ProgressStatus.Failed,
                StatusMessage = "Batch processing failed",
                ErrorMessage = ex.Message
            });
            
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    [HttpPost("generate-gallery")]
    public async Task<ActionResult<GalleryResult>> GenerateThumbnailGallery([FromBody] GalleryRequest request)
    {
        if (request.ImagePaths == null || !request.ImagePaths.Any())
        {
            return BadRequest(new { error = "Image paths are required" });
        }

        try
        {
            var outputDir = request.OutputDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Gallery");
            var galleryPath = await ThumbnailGenerator.GenerateThumbnailGalleryAsync(
                request.ImagePaths,
                outputDir,
                request.ThumbnailSize ?? 200,
                request.Title ?? "AgentDMS Gallery"
            );

            return Ok(new GalleryResult
            {
                GalleryPath = galleryPath,
                OutputDirectory = outputDir,
                TotalImages = request.ImagePaths.Count()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating gallery");
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
}

// DTOs for API requests
public class ProcessImageRequest
{
    [Required]
    public string FilePath { get; set; } = string.Empty;
}

public class BatchProcessRequest
{
    [Required]
    public IEnumerable<string> FilePaths { get; set; } = new List<string>();
}

public class GalleryRequest
{
    [Required]
    public IEnumerable<string> ImagePaths { get; set; } = new List<string>();
    public string? OutputDirectory { get; set; }
    public int? ThumbnailSize { get; set; }
    public string? Title { get; set; }
}

public class GalleryResult
{
    public string GalleryPath { get; set; } = string.Empty;
    public string OutputDirectory { get; set; } = string.Empty;
    public int TotalImages { get; set; }
}