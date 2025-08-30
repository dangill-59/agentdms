using Microsoft.AspNetCore.Mvc;
using AgentDMS.Core.Services;
using AgentDMS.Core.Models;
using AgentDMS.Core.Utilities;
using AgentDMS.Web.Hubs;
using System.ComponentModel.DataAnnotations;
using Swashbuckle.AspNetCore.Annotations;
using Microsoft.AspNetCore.Http;

namespace AgentDMS.Web.Controllers;

/// <summary>
/// Controller for image processing operations including upload, scanning, and batch processing
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[SwaggerTag("Image Processing operations including file upload, scanning, and conversion")]
public class ImageProcessingController : ControllerBase
{
    private readonly ImageProcessingService _imageProcessor;
    private readonly FileUploadService _fileUploadService;
    private readonly IProgressBroadcaster _progressBroadcaster;
    private readonly IBackgroundJobService _backgroundJobService;
    private readonly IScannerService _scannerService;
    private readonly ILogger<ImageProcessingController> _logger;

    /// <summary>
    /// Initializes a new instance of the ImageProcessingController
    /// </summary>
    /// <param name="imageProcessor">Image processing service</param>
    /// <param name="fileUploadService">File upload service</param>
    /// <param name="progressBroadcaster">Progress broadcasting service</param>
    /// <param name="backgroundJobService">Background job service</param>
    /// <param name="scannerService">Scanner service</param>
    /// <param name="logger">Logger instance</param>
    public ImageProcessingController(
        ImageProcessingService imageProcessor, 
        FileUploadService fileUploadService,
        IProgressBroadcaster progressBroadcaster,
        IBackgroundJobService backgroundJobService,
        IScannerService scannerService,
        ILogger<ImageProcessingController> logger)
    {
        _imageProcessor = imageProcessor;
        _fileUploadService = fileUploadService;
        _progressBroadcaster = progressBroadcaster;
        _backgroundJobService = backgroundJobService;
        _scannerService = scannerService;
        _logger = logger;
    }

    /// <summary>
    /// Get supported file formats for image processing
    /// </summary>
    /// <returns>List of supported file extensions</returns>
    /// <response code="200">Returns the list of supported file formats</response>
    [HttpGet("formats")]
    [SwaggerOperation(Summary = "Get supported file formats", Description = "Returns a list of all supported file extensions for image processing")]
    [SwaggerResponse(200, "List of supported file formats", typeof(IEnumerable<string>))]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<string>> GetSupportedFormats()
    {
        return Ok(ImageProcessingService.GetSupportedExtensions());
    }

    /// <summary>
    /// Upload and process an image file
    /// </summary>
    /// <param name="file">The image file to upload and process</param>
    /// <returns>Upload response with job ID for tracking processing status</returns>
    /// <response code="200">File uploaded successfully and processing started</response>
    /// <response code="400">No file uploaded or invalid file</response>
    /// <response code="500">Internal server error during upload</response>
    [HttpPost("upload")]
    [SwaggerOperation(Summary = "Upload and process image", Description = "Uploads an image file and starts background processing. Returns a job ID for tracking progress.")]
    [SwaggerResponse(200, "File uploaded successfully", typeof(UploadResponse))]
    [SwaggerResponse(400, "Bad request - no file or invalid file")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(UploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB default, can be overridden by configuration
    [DisableRequestSizeLimit] // Allow configuration to control limits instead
    public async Task<ActionResult<UploadResponse>> UploadAndProcessImage(
        [SwaggerParameter("Image file to upload")] IFormFile file)
    {
        // Manually read the useMistralAI and useMistralOcr parameters from form data to avoid model binding issues
        var useMistralAIValue = Request.Form["useMistralAI"].ToString();
        var useMistralOcrValue = Request.Form["useMistralOcr"].ToString();
        bool useMistralAiBool = useMistralAIValue?.ToLowerInvariant() == "true";
        bool useMistralOcrBool = useMistralOcrValue?.ToLowerInvariant() == "true";
        
        _logger.LogInformation("Upload request received. File: {FileName}, UseMistralAI: {UseMistralAI}, UseMistralOcr: {UseMistralOcr}", 
            file?.FileName ?? "null", useMistralAiBool, useMistralOcrBool);
        
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

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

            // Enqueue job for background processing
            var jobId = await _backgroundJobService.EnqueueJobAsync(tempFilePath, useMistralAiBool, useMistralOcrBool);

            _logger.LogInformation("File uploaded successfully. Job ID: {JobId}, File: {FileName}", jobId, file.FileName);

            // Return immediately with job ID
            return Ok(new UploadResponse
            {
                JobId = jobId,
                FileName = file.FileName,
                FileSize = file.Length,
                Message = "File uploaded successfully. Processing started.",
                Status = "processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
            return StatusCode(500, new { error = "Upload failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Upload multiple files for batch processing
    /// </summary>
    /// <param name="files">The image files to upload</param>
    /// <returns>List of uploaded file paths for batch processing</returns>
    /// <response code="200">Files uploaded successfully</response>
    /// <response code="400">No files uploaded or invalid files</response>
    /// <response code="500">Internal server error during upload</response>
    [HttpPost("upload-batch")]
    [SwaggerOperation(Summary = "Upload multiple files for batch processing", Description = "Uploads multiple image files and returns their server paths for batch processing.")]
    [SwaggerResponse(200, "Files uploaded successfully", typeof(BatchUploadResponse))]
    [SwaggerResponse(400, "Bad request - no files or invalid files")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(BatchUploadResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(100 * 1024 * 1024)] // 100MB default, can be overridden by configuration
    [DisableRequestSizeLimit] // Allow configuration to control limits instead
    public async Task<ActionResult<BatchUploadResponse>> UploadMultipleFiles(
        [SwaggerParameter("Image files to upload")] List<IFormFile> files)
    {
        if (files == null || files.Count == 0)
        {
            return BadRequest(new { error = "No files uploaded" });
        }

        var uploadedFiles = new List<UploadedFileInfo>();
        var errors = new List<string>();

        try
        {
            foreach (var file in files)
            {
                if (file == null || file.Length == 0)
                {
                    errors.Add($"Empty file: {file?.FileName ?? "unknown"}");
                    continue;
                }

                try
                {
                    // Validate file extension
                    var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                    if (!FileUploadService.IsValidImageFile(extension))
                    {
                        errors.Add($"Unsupported file format: {file.FileName}");
                        continue;
                    }

                    // Save uploaded file temporarily
                    var tempPath = Path.GetTempFileName();
                    var originalName = Path.GetFileNameWithoutExtension(file.FileName);
                    var tempFilePath = Path.ChangeExtension(tempPath, extension);

                    using (var stream = new FileStream(tempFilePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    uploadedFiles.Add(new UploadedFileInfo
                    {
                        OriginalFileName = file.FileName,
                        ServerFilePath = tempFilePath,
                        FileSize = file.Length
                    });

                    _logger.LogInformation("File uploaded for batch processing: {FileName} -> {ServerPath}", file.FileName, tempFilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error uploading file: {FileName}", file.FileName);
                    errors.Add($"Failed to upload {file.FileName}: {ex.Message}");
                }
            }

            if (uploadedFiles.Count == 0)
            {
                return BadRequest(new { error = "No files were successfully uploaded", errors });
            }

            return Ok(new BatchUploadResponse
            {
                UploadedFiles = uploadedFiles,
                SuccessCount = uploadedFiles.Count,
                ErrorCount = errors.Count,
                Errors = errors,
                Message = $"Successfully uploaded {uploadedFiles.Count} files for batch processing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch file upload");
            return StatusCode(500, new { error = "Batch upload failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Get job processing status
    /// </summary>
    /// <param name="jobId">The job ID to check status for</param>
    /// <returns>Current status of the processing job</returns>
    /// <response code="200">Job status retrieved successfully</response>
    /// <response code="400">Invalid job ID</response>
    /// <response code="404">Job not found</response>
    /// <response code="500">Internal server error</response>
    [HttpGet("job/{jobId}/status")]
    [SwaggerOperation(Summary = "Get job status", Description = "Retrieves the current status of a processing job by its ID")]
    [SwaggerResponse(200, "Job status retrieved", typeof(JobStatusResponse))]
    [SwaggerResponse(400, "Invalid job ID")]
    [SwaggerResponse(404, "Job not found")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(JobStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<JobStatusResponse>> GetJobStatus(
        [SwaggerParameter("Job ID to check status for")] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest(new { error = "Job ID is required" });
        }

        try
        {
            var job = await _backgroundJobService.GetJobStatusAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            return Ok(new JobStatusResponse
            {
                JobId = job.JobId,
                Status = job.Status.ToString(),
                CreatedAt = job.CreatedAt,
                ErrorMessage = job.ErrorMessage
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job status for job ID: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }

    /// <summary>
    /// Get job processing result
    /// </summary>
    /// <param name="jobId">The job ID to get results for</param>
    /// <returns>Processing result if job is completed</returns>
    /// <response code="200">Job completed successfully, returns processing result</response>
    /// <response code="202">Job is still processing</response>
    /// <response code="400">Invalid job ID</response>
    /// <response code="404">Job not found</response>
    /// <response code="500">Job failed or internal server error</response>
    [HttpGet("job/{jobId}/result")]
    [SwaggerOperation(Summary = "Get job result", Description = "Retrieves the processing result for a completed job")]
    [SwaggerResponse(200, "Job completed successfully", typeof(ProcessingResult))]
    [SwaggerResponse(202, "Job is still processing")]
    [SwaggerResponse(400, "Invalid job ID")]
    [SwaggerResponse(404, "Job not found")]
    [SwaggerResponse(500, "Job failed or internal server error")]
    [ProducesResponseType(typeof(ProcessingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ProcessingResult>> GetJobResult(
        [SwaggerParameter("Job ID to get results for")] string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return BadRequest(new { error = "Job ID is required" });
        }

        try
        {
            var job = await _backgroundJobService.GetJobStatusAsync(jobId);
            if (job == null)
            {
                return NotFound(new { error = "Job not found" });
            }

            if (job.Status == JobStatus.Queued || job.Status == JobStatus.Processing)
            {
                return Accepted(new { message = "Job is still processing", status = job.Status.ToString() });
            }

            if (job.Status == JobStatus.Failed)
            {
                return StatusCode(500, new { error = "Job failed", message = job.ErrorMessage });
            }

            var result = await _backgroundJobService.GetJobResultAsync(jobId);
            if (result == null)
            {
                return NotFound(new { error = "Job result not found" });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting job result for job ID: {JobId}", jobId);
            return StatusCode(500, new { error = "Internal server error", message = ex.Message });
        }
    }
    
    /// <summary>
    /// Process an image file by file path
    /// </summary>
    /// <param name="request">Request containing the file path to process</param>
    /// <returns>Processing result with job ID</returns>
    /// <response code="200">File processed successfully</response>
    /// <response code="400">Invalid file path</response>
    /// <response code="404">File not found</response>
    /// <response code="500">Internal server error during processing</response>
    [HttpPost("process")]
    [SwaggerOperation(Summary = "Process image by path", Description = "Processes an image file located at the specified file path")]
    [SwaggerResponse(200, "File processed successfully", typeof(ProcessingResult))]
    [SwaggerResponse(400, "Invalid file path")]
    [SwaggerResponse(404, "File not found")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(ProcessingResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

            var result = await _imageProcessor.ProcessImageAsync(request.FilePath, progressReporter, CancellationToken.None, request.UseMistralAI, request.UseMistralOcr);
            
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

    /// <summary>
    /// Process multiple images in batch
    /// </summary>
    /// <param name="request">Request containing list of file paths to process</param>
    /// <returns>Batch processing results with job ID</returns>
    /// <response code="200">Batch processing started successfully</response>
    /// <response code="400">No file paths provided</response>
    /// <response code="500">Internal server error during batch processing</response>
    [HttpPost("batch-process")]
    [SwaggerOperation(Summary = "Batch process multiple images", Description = "Processes multiple images simultaneously with progress tracking")]
    [SwaggerResponse(200, "Batch processing started", typeof(IEnumerable<ProcessingResult>))]
    [SwaggerResponse(400, "No file paths provided")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(IEnumerable<ProcessingResult>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

            var results = await _imageProcessor.ProcessMultipleImagesAsync(filePathsList, progressReporter, progress, cancellationToken: default, request.UseMistralAI, request.UseMistralOcr);
            
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

    /// <summary>
    /// Generate thumbnail gallery from images
    /// </summary>
    /// <param name="request">Request containing image paths and gallery settings</param>
    /// <returns>Gallery generation result with output path</returns>
    /// <response code="200">Gallery generated successfully</response>
    /// <response code="400">No image paths provided</response>
    /// <response code="500">Internal server error during gallery generation</response>
    [HttpPost("generate-gallery")]
    [SwaggerOperation(Summary = "Generate thumbnail gallery", Description = "Creates an HTML gallery with thumbnails from the provided image paths")]
    [SwaggerResponse(200, "Gallery generated successfully", typeof(GalleryResult))]
    [SwaggerResponse(400, "No image paths provided")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(GalleryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
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

    // Scanner endpoints
    /// <summary>
    /// Get list of available scanners
    /// </summary>
    /// <returns>List of available scanner devices</returns>
    /// <response code="200">List of available scanners retrieved successfully</response>
    /// <response code="500">Error retrieving scanner information</response>
    [HttpGet("scanners")]
    [SwaggerOperation(Summary = "Get available scanners", Description = "Retrieves a list of all available scanner devices on the system")]
    [SwaggerResponse(200, "List of available scanners", typeof(IEnumerable<ScannerInfo>))]
    [SwaggerResponse(500, "Error retrieving scanner information")]
    [ProducesResponseType(typeof(IEnumerable<ScannerInfo>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IEnumerable<ScannerInfo>>> GetAvailableScanners()
    {
        try
        {
            var scanners = await _scannerService.GetAvailableScannersAsync();
            return Ok(scanners);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available scanners");
            return StatusCode(500, new { error = "Failed to get scanners", message = ex.Message });
        }
    }

    /// <summary>
    /// Get scanner capabilities
    /// </summary>
    /// <returns>Scanner capabilities and supported features</returns>
    /// <response code="200">Scanner capabilities retrieved successfully</response>
    /// <response code="500">Error retrieving scanner capabilities</response>
    [HttpGet("scanners/capabilities")]
    [SwaggerOperation(Summary = "Get scanner capabilities", Description = "Retrieves the capabilities and supported features of the scanner system")]
    [SwaggerResponse(200, "Scanner capabilities", typeof(ScannerCapabilities))]
    [SwaggerResponse(500, "Error retrieving scanner capabilities")]
    [ProducesResponseType(typeof(ScannerCapabilities), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScannerCapabilities>> GetScannerCapabilities()
    {
        try
        {
            var capabilities = await _scannerService.GetCapabilitiesAsync();
            return Ok(capabilities);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scanner capabilities");
            return StatusCode(500, new { error = "Failed to get scanner capabilities", message = ex.Message });
        }
    }

    /// <summary>
    /// Scan a document using connected scanner
    /// </summary>
    /// <param name="request">Scan configuration and settings</param>
    /// <returns>Scan result with file path and optional processing job ID</returns>
    /// <response code="200">Document scanned successfully</response>
    /// <response code="400">Scanning not available or scan failed</response>
    /// <response code="500">Internal server error during scanning</response>
    [HttpPost("scan")]
    [SwaggerOperation(Summary = "Scan document", Description = "Scans a document using the specified scanner with configurable settings. Optionally queues for automatic processing.")]
    [SwaggerResponse(200, "Document scanned successfully", typeof(ScanResult))]
    [SwaggerResponse(400, "Scanning not available or scan failed")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(ScanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScanResult>> ScanDocument([FromBody] ScanRequest request)
    {
        try
        {
            if (!_scannerService.IsScanningAvailable())
            {
                return BadRequest(new { error = "Scanning is not available on this system" });
            }

            var result = await _scannerService.ScanAsync(request);
            
            if (!result.Success)
            {
                return BadRequest(new { error = "Scan failed", message = result.ErrorMessage });
            }

            // If auto-processing is enabled and scan was successful, enqueue for processing
            if (request.AutoProcess && !string.IsNullOrEmpty(result.ScannedFilePath))
            {
                try
                {
                    var jobId = await _backgroundJobService.EnqueueJobAsync(result.ScannedFilePath);
                    result.ProcessingJobId = jobId;
                    _logger.LogInformation("Scanned document queued for processing. Job ID: {JobId}", jobId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to enqueue scanned document for processing");
                    // Don't fail the scan operation if processing queue fails
                }
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during scan operation");
            return StatusCode(500, new { error = "Scan operation failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Preview scan with scanner UI
    /// </summary>
    /// <param name="request">Scan preview configuration</param>
    /// <returns>Preview scan result</returns>
    /// <response code="200">Preview scan completed successfully</response>
    /// <response code="400">Preview scan failed</response>
    /// <response code="500">Internal server error during preview scanning</response>
    [HttpPost("scan/preview")]
    [SwaggerOperation(Summary = "Preview scan", Description = "Performs a preview scan with the scanner user interface enabled, without auto-processing")]
    [SwaggerResponse(200, "Preview scan completed", typeof(ScanResult))]
    [SwaggerResponse(400, "Preview scan failed")]
    [SwaggerResponse(500, "Internal server error")]
    [ProducesResponseType(typeof(ScanResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ScanResult>> PreviewScan([FromBody] ScanRequest request)
    {
        try
        {
            // For preview, always disable auto-processing and show UI
            var previewRequest = new ScanRequest
            {
                ScannerDeviceId = request.ScannerDeviceId,
                Resolution = request.Resolution,
                ColorMode = request.ColorMode,
                Format = request.Format,
                ShowUserInterface = true,
                AutoProcess = false
            };

            var result = await _scannerService.ScanAsync(previewRequest);
            
            if (!result.Success)
            {
                return BadRequest(new { error = "Preview scan failed", message = result.ErrorMessage });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during preview scan operation");
            return StatusCode(500, new { error = "Preview scan failed", message = ex.Message });
        }
    }

    /// <summary>
    /// Get scanner connectivity information for remote access scenarios
    /// </summary>
    /// <returns>Information about scanner connectivity requirements and limitations</returns>
    /// <response code="200">Scanner connectivity information retrieved successfully</response>
    [HttpGet("scanners/connectivity-info")]
    [SwaggerOperation(Summary = "Get scanner connectivity information", Description = "Provides information about scanner connectivity requirements, especially for remote access scenarios")]
    [SwaggerResponse(200, "Scanner connectivity information", typeof(ScannerConnectivityInfo))]
    [ProducesResponseType(typeof(ScannerConnectivityInfo), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScannerConnectivityInfo>> GetScannerConnectivityInfo()
    {
        try
        {
            var availableScanners = await _scannerService.GetAvailableScannersAsync();
            var capabilities = await _scannerService.GetCapabilitiesAsync();
            var diagnostics = await _scannerService.GetDiagnosticInfoAsync();

            var connectivityInfo = new ScannerConnectivityInfo
            {
                IsRemoteAccess = IsRemoteRequest(),
                ServerPlatform = capabilities.PlatformInfo,
                HasRealScanners = availableScanners.Any(s => !s.DeviceId.StartsWith("mock_")),
                TotalScannersFound = availableScanners.Count,
                ScannerConnectivityRequirements = GetScannerConnectivityRequirements(),
                RemoteAccessLimitations = GetRemoteAccessLimitations(),
                RecommendedSolutions = GetRecommendedSolutions()
            };

            return Ok(connectivityInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting scanner connectivity information");
            return StatusCode(500, new { error = "Failed to get connectivity information", message = ex.Message });
        }
    }

    /// <summary>
    /// Determine if this is a remote access request
    /// </summary>
    private bool IsRemoteRequest()
    {
        var remoteIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        var isLocal = remoteIp == "127.0.0.1" || remoteIp == "::1" || 
                     remoteIp?.StartsWith("192.168.") == true || 
                     remoteIp?.StartsWith("10.") == true ||
                     remoteIp?.StartsWith("172.") == true;
        return !isLocal;
    }

    /// <summary>
    /// Get scanner connectivity requirements information
    /// </summary>
    private List<string> GetScannerConnectivityRequirements()
    {
        return new List<string>
        {
            "Scanners must be physically connected to the computer running AgentDMS server",
            "Scanner drivers must be installed on the server machine",
            "TWAIN-compatible scanners are supported on Windows",
            "SANE-compatible scanners are supported on Linux",
            "Wireless or network scanners must be configured on the server machine"
        };
    }

    /// <summary>
    /// Get remote access limitations
    /// </summary>
    private List<string> GetRemoteAccessLimitations()
    {
        return new List<string>
        {
            "Browser security prevents direct access to scanners on remote client machines",
            "AgentDMS cannot detect scanners connected to your local computer when accessed remotely",
            "All scanning operations are performed on the server machine",
            "TWAIN APIs require direct hardware access and cannot work through web browsers"
        };
    }

    /// <summary>
    /// Get recommended solutions for remote scanner access
    /// </summary>
    private List<string> GetRecommendedSolutions()
    {
        return new List<string>
        {
            "Connect scanners directly to the computer running AgentDMS",
            "Install AgentDMS on the same machine where your scanners are connected",
            "Use network-enabled scanners that can be configured on the server machine",
            "Consider using remote desktop software to access the server machine directly",
            "For development/testing, mock scanners are available and will work from any location"
        };
    }
}

// DTOs for API requests and responses

/// <summary>
/// Request model for processing an image by file path
/// </summary>
public class ProcessImageRequest
{
    /// <summary>
    /// Full file path to the image to be processed
    /// </summary>
    /// <example>C:\Images\document.pdf</example>
    [Required]
    [SwaggerSchema("Full file path to the image file")]
    public string FilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether to process with Mistral AI for document classification and data extraction
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema("Whether to process with Mistral AI")]
    public bool UseMistralAI { get; set; } = false;
    
    /// <summary>
    /// Whether to use Mistral OCR instead of Tesseract for text extraction
    /// </summary>
    /// <example>false</example>
    [SwaggerSchema("Whether to use Mistral OCR instead of Tesseract")]
    public bool UseMistralOcr { get; set; } = false;
}

/// <summary>
/// Request model for batch processing multiple images
/// </summary>
public class BatchProcessRequest
{
    /// <summary>
    /// List of file paths to process in batch
    /// </summary>
    /// <example>["C:\Images\image1.jpg", "C:\Images\image2.png"]</example>
    [Required]
    [SwaggerSchema("List of file paths to process")]
    public IEnumerable<string> FilePaths { get; set; } = new List<string>();
    
    /// <summary>
    /// Whether to use Mistral AI for document analysis
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema("Enable Mistral AI processing for document classification and data extraction")]
    public bool UseMistralAI { get; set; } = false;
    
    /// <summary>
    /// Whether to use Mistral OCR for text extraction
    /// </summary>
    /// <example>true</example>
    [SwaggerSchema("Enable Mistral OCR for text extraction from images and documents")]
    public bool UseMistralOcr { get; set; } = false;
}

/// <summary>
/// Request model for generating thumbnail gallery
/// </summary>
public class GalleryRequest
{
    /// <summary>
    /// List of image file paths to include in gallery
    /// </summary>
    /// <example>["C:\Images\photo1.jpg", "C:\Images\photo2.png"]</example>
    [Required]
    [SwaggerSchema("List of image file paths")]
    public IEnumerable<string> ImagePaths { get; set; } = new List<string>();
    
    /// <summary>
    /// Output directory for the generated gallery (optional)
    /// </summary>
    /// <example>C:\Gallery\Output</example>
    [SwaggerSchema("Optional output directory path")]
    public string? OutputDirectory { get; set; }
    
    /// <summary>
    /// Thumbnail size in pixels (optional, default: 200)
    /// </summary>
    /// <example>150</example>
    [SwaggerSchema("Thumbnail size in pixels")]
    public int? ThumbnailSize { get; set; }
    
    /// <summary>
    /// Gallery title (optional)
    /// </summary>
    /// <example>My Photo Gallery</example>
    [SwaggerSchema("Gallery title")]
    public string? Title { get; set; }
}

/// <summary>
/// Result of gallery generation
/// </summary>
public class GalleryResult
{
    /// <summary>
    /// Path to the generated gallery HTML file
    /// </summary>
    public string GalleryPath { get; set; } = string.Empty;
    
    /// <summary>
    /// Output directory containing gallery files
    /// </summary>
    public string OutputDirectory { get; set; } = string.Empty;
    
    /// <summary>
    /// Total number of images included in the gallery
    /// </summary>
    public int TotalImages { get; set; }
}

/// <summary>
/// Response model for file upload operations
/// </summary>
public class UploadResponse
{
    /// <summary>
    /// Unique job identifier for tracking processing status
    /// </summary>
    public string JobId { get; set; } = string.Empty;
    
    /// <summary>
    /// Original uploaded file name
    /// </summary>
    public string FileName { get; set; } = string.Empty;
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }
    
    /// <summary>
    /// Status message
    /// </summary>
    public string Message { get; set; } = string.Empty;
    
    /// <summary>
    /// Current processing status
    /// </summary>
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Response model for job status queries
/// </summary>
public class JobStatusResponse
{
    /// <summary>
    /// Job identifier
    /// </summary>
    public string JobId { get; set; } = string.Empty;
    
    /// <summary>
    /// Current job status (Queued, Processing, Completed, Failed)
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// Job creation timestamp
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Error message if job failed
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Information about scanner connectivity requirements and limitations
/// </summary>
public class ScannerConnectivityInfo
{
    /// <summary>
    /// Whether this request appears to be from a remote machine
    /// </summary>
    public bool IsRemoteAccess { get; set; }
    
    /// <summary>
    /// Platform information of the server running AgentDMS
    /// </summary>
    public string ServerPlatform { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether real (non-mock) scanners were detected on the server
    /// </summary>
    public bool HasRealScanners { get; set; }
    
    /// <summary>
    /// Total number of scanners found (including mock scanners)
    /// </summary>
    public int TotalScannersFound { get; set; }
    
    /// <summary>
    /// Requirements for scanner connectivity
    /// </summary>
    public List<string> ScannerConnectivityRequirements { get; set; } = new();
    
    /// <summary>
    /// Limitations when accessing from remote machines
    /// </summary>
    public List<string> RemoteAccessLimitations { get; set; } = new();
    
    /// <summary>
    /// Recommended solutions for remote scanner access
    /// </summary>
    public List<string> RecommendedSolutions { get; set; } = new();
}

/// <summary>
/// Response model for batch file upload operations
/// </summary>
public class BatchUploadResponse
{
    /// <summary>
    /// List of successfully uploaded files
    /// </summary>
    public List<UploadedFileInfo> UploadedFiles { get; set; } = new();
    
    /// <summary>
    /// Number of files successfully uploaded
    /// </summary>
    public int SuccessCount { get; set; }
    
    /// <summary>
    /// Number of files that failed to upload
    /// </summary>
    public int ErrorCount { get; set; }
    
    /// <summary>
    /// List of error messages for failed uploads
    /// </summary>
    public List<string> Errors { get; set; } = new();
    
    /// <summary>
    /// Overall result message
    /// </summary>
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Information about an uploaded file
/// </summary>
public class UploadedFileInfo
{
    /// <summary>
    /// Original file name as uploaded by the client
    /// </summary>
    public string OriginalFileName { get; set; } = string.Empty;
    
    /// <summary>
    /// Full server-side file path where the file was stored
    /// </summary>
    public string ServerFilePath { get; set; } = string.Empty;
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSize { get; set; }
}