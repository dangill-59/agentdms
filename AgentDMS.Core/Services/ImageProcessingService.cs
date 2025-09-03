using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using ImageMagick;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services.Storage;
using System.Text.Json;

namespace AgentDMS.Core.Services;

/// <summary>
/// Service for processing image files, converting formats, and handling multipage documents
/// </summary>
public class ImageProcessingService
{
    private readonly SemaphoreSlim _semaphore;
    private readonly string _outputDirectory;
    private readonly IStorageService? _storageService;
    private readonly IServiceProvider? _serviceProvider;
    private readonly ILogger<ImageProcessingService>? _logger;
    private readonly MistralDocumentAiService? _mistralService;
    private readonly MistralOcrService? _mistralOcrService;
    private readonly PerformanceCache _cache;

    /// <summary>
    /// Constructor for dependency injection with storage service
    /// </summary>
    public ImageProcessingService(
        IStorageService storageService,
        int maxConcurrency = 4,
        ILogger<ImageProcessingService>? logger = null,
        MistralDocumentAiService? mistralService = null,
        MistralOcrService? mistralOcrService = null,
        PerformanceCache? cache = null,
        IServiceProvider? serviceProvider = null)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency);
        _storageService = storageService ?? throw new ArgumentNullException(nameof(storageService));
        _serviceProvider = serviceProvider;
        _outputDirectory = GetOutputDirectory();
        _logger = logger;
        _mistralService = mistralService;
        _mistralOcrService = mistralOcrService;
        _cache = cache ?? new PerformanceCache(logger);
        
        // Initialize Magick.NET
        MagickNET.Initialize();
    }

    /// <summary>
    /// Legacy constructor for backward compatibility
    /// </summary>
    public ImageProcessingService(
        int maxConcurrency = 4, 
        string? outputDirectory = null, 
        ILogger<ImageProcessingService>? logger = null,
        MistralDocumentAiService? mistralService = null,
        MistralOcrService? mistralOcrService = null,
        PerformanceCache? cache = null)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency);
        _outputDirectory = outputDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Output");
        _storageService = null; // Legacy mode uses local storage directly
        _serviceProvider = null; // Legacy mode doesn't save to database
        _logger = logger;
        _mistralService = mistralService;
        _mistralOcrService = mistralOcrService;
        _cache = cache ?? new PerformanceCache(logger);
        
        // Ensure output directory exists for legacy mode
        Directory.CreateDirectory(_outputDirectory);
        
        // Initialize Magick.NET
        MagickNET.Initialize();
    }

    private string GetOutputDirectory()
    {
        if (_storageService?.StorageProvider is LocalStorageProvider localProvider)
        {
            return localProvider.BaseDirectory;
        }
        
        // For cloud storage providers, use a temp directory for intermediate processing
        return Path.Combine(Path.GetTempPath(), "AgentDMS_Processing");
    }

    /// <summary>
    /// Process a single image file asynchronously
    /// </summary>
    public async Task<ProcessingResult> ProcessImageAsync(string filePath, DetailedProgressReporter? progressReporter = null, CancellationToken cancellationToken = default, bool useMistralAI = false, bool useMistralOcr = false, bool enableOcr = true, string? jobId = null)
    {
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            var fileName = Path.GetFileName(filePath);
            if (progressReporter != null)
                await progressReporter.ReportProgress(fileName, ProgressStatus.Starting, "Starting processing...");
            
            var overallStart = DateTime.UtcNow;
            var metrics = new ProcessingMetrics { StartTime = overallStart };
            
            if (!File.Exists(filePath))
            {
                if (progressReporter != null)
                    await progressReporter.ReportProgress(fileName, ProgressStatus.Failed, "File not found", errorMessage: $"File not found: {filePath}");
                return ProcessingResult.Failed($"File not found: {filePath}");
            }

            if (progressReporter != null)
                await progressReporter.ReportProgress(fileName, ProgressStatus.LoadingFile, "Loading file...");
            
            // File load timing
            var fileLoadStart = DateTime.UtcNow;
            var fileInfo = new FileInfo(filePath);
            metrics.FileLoadTime = DateTime.UtcNow - fileLoadStart;

            var imageFile = new ImageFile
            {
                OriginalFilePath = filePath,
                FileName = fileInfo.Name,
                FileSize = fileInfo.Length,
                CreatedDate = fileInfo.CreationTime
            };

            // Determine file format and process accordingly
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            imageFile.OriginalFormat = extension;

            if (progressReporter != null)
                await progressReporter.ReportProgress(fileName, ProgressStatus.ProcessingFile, $"Processing {extension.ToUpper()} file...");

            ProcessingResult result;
            
            switch (extension)
            {
                case ".pdf":
                    result = await ProcessPdfAsync(imageFile, metrics, progressReporter, cancellationToken, jobId);
                    break;
                case ".tif":
                case ".tiff":
                    result = await ProcessMultipageTiffAsync(imageFile, metrics, progressReporter, cancellationToken, jobId);
                    break;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".webp":
                    result = await ProcessSingleImageAsync(imageFile, metrics, progressReporter, cancellationToken, jobId);
                    break;
                default:
                    if (progressReporter != null)
                        await progressReporter.ReportProgress(fileName, ProgressStatus.Failed, "Unsupported format", errorMessage: $"Unsupported file format: {extension}");
                    result = ProcessingResult.Failed($"Unsupported file format: {extension}");
                    break;
            }

            if (result.Success && result.ProcessedImage != null)
            {
                var totalTime = DateTime.UtcNow - overallStart;
                result.ProcessingTime = totalTime;
                metrics.TotalProcessingTime = totalTime;
                result.Metrics = metrics;
                
                // Optimize OCR and AI processing based on configuration
                await OptimizeTextExtractionAndAnalysisAsync(result, progressReporter, cancellationToken, useMistralOcr, useMistralAI, enableOcr, fileName);
                
                // Save document to database if document service is available
                await SaveDocumentToDatabaseAsync(result, filePath, cancellationToken);
                
                if (progressReporter != null)
                    await progressReporter.ReportProgress(fileName, ProgressStatus.Completed, "Processing completed successfully");
            }

            return result;
        }
        catch (Exception ex)
        {
            var fileName = Path.GetFileName(filePath);
            if (progressReporter != null)
                await progressReporter.ReportProgress(fileName, ProgressStatus.Failed, "Processing failed", errorMessage: ex.Message);
            return ProcessingResult.Failed($"Error processing {filePath}: {ex.Message}", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Process multiple files concurrently with batching to prevent resource exhaustion
    /// </summary>
    public async Task<List<ProcessingResult>> ProcessMultipleImagesAsync(
        IEnumerable<string> filePaths, 
        DetailedProgressReporter? progressReporter = null,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default,
        bool useMistralAI = false,
        bool useMistralOcr = false,
        bool enableOcr = true)
    {
        var filePathsList = filePaths.ToList();
        var results = new List<ProcessingResult>();
        var processedCount = 0;
        var totalFiles = filePathsList.Count;

        // Process files in batches to control concurrency more effectively
        var batchSize = Math.Min(_semaphore.CurrentCount, _semaphore.CurrentCount); // Use semaphore limit as batch size
        var actualBatchSize = Math.Max(1, batchSize);

        _logger?.LogInformation("Processing {FileCount} files in batches of {BatchSize}", filePathsList.Count, actualBatchSize);

        for (int i = 0; i < filePathsList.Count; i += actualBatchSize)
        {
            var batch = filePathsList.Skip(i).Take(actualBatchSize);
            var batchTasks = batch.Select(async (filePath, batchIndex) =>
            {
                var fileIndex = i + batchIndex + 1; // 1-based indexing for display
                var fileName = Path.GetFileName(filePath);
                
                // Create a file-specific progress reporter that includes batch context
                DetailedProgressReporter? fileProgressReporter = null;
                if (progressReporter != null)
                {
                    fileProgressReporter = new DetailedProgressReporter(progressReporter.JobId, async (fileProgress) =>
                    {
                        // Update progress with batch context
                        fileProgress.CurrentFile = fileIndex;
                        fileProgress.TotalFiles = totalFiles;
                        if (progressReporter.OnProgress != null)
                            await progressReporter.OnProgress(fileProgress);
                    });
                }
                
                var result = await ProcessImageAsync(filePath, fileProgressReporter, cancellationToken, useMistralAI, useMistralOcr, enableOcr);
                
                Interlocked.Increment(ref processedCount);
                progress?.Report(processedCount);
                
                return result;
            });

            var batchResults = await Task.WhenAll(batchTasks);
            results.AddRange(batchResults);
        }

        return results;
    }

    private async Task<ProcessingResult> ProcessSingleImageAsync(ImageFile imageFile, ProcessingMetrics metrics, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken, string? jobId = null)
    {
        try
        {
            _logger?.LogInformation("Processing single image: {FilePath}", imageFile.OriginalFilePath);
            
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.LoadingFile, "Decoding image...", 1, 1, 1, 1);
            
            // Image decode timing
            var decodeStart = DateTime.UtcNow;
            var pngPath = Path.Combine(_outputDirectory, $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}.png");
            
            // Conversion timing
            var conversionStart = DateTime.UtcNow;
            
            // Load, process, and save the image in a controlled scope to ensure disposal
            using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageFile.OriginalFilePath, cancellationToken))
            {
                metrics.ImageDecodeTime = DateTime.UtcNow - decodeStart;
                
                imageFile.Width = image.Width;
                imageFile.Height = image.Height;
                imageFile.IsMultiPage = false;
                imageFile.PageCount = 1;

                if (progressReporter != null)
                    await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.ConvertingPage, "Converting to PNG...", 1, 1, 1, 1);

                await image.SaveAsPngAsync(pngPath, cancellationToken);
            } // Image is disposed here, releasing file handles
            
            // Force garbage collection and wait for finalizers to ensure cleanup
            GC.Collect();
            GC.WaitForPendingFinalizers();
            
            // Add small delay to allow OS to release file handles
            await Task.Delay(100, cancellationToken);
            
            // Save to storage provider if configured
            var relativePath = $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}.png";
            var storedPath = await SaveFileAsync(pngPath, relativePath);
            
            // Clean up temporary file after successful storage upload (for cloud storage)
            await CleanupTemporaryFileAsync(pngPath, storedPath);
            
            imageFile.ConvertedPngPath = storedPath;
            metrics.ConversionTime = DateTime.UtcNow - conversionStart;

            // Use the stored file directly instead of generating a thumbnail
            var thumbnailStart = DateTime.UtcNow;
            imageFile.ThumbnailPath = storedPath; // Point to the stored file directly
            metrics.ThumbnailGenerationTime = DateTime.UtcNow - thumbnailStart;

            _logger?.LogDebug("Successfully processed single image: {FilePath}", imageFile.OriginalFilePath);
            return ProcessingResult.Successful(imageFile, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing single image: {FilePath}", imageFile.OriginalFilePath);
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.Failed, "Failed to process image", 1, 1, 1, 1, ex.Message);
            return ProcessingResult.Failed($"Error processing single image: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessMultipageTiffAsync(ImageFile imageFile, ProcessingMetrics metrics, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken, string? jobId = null)
    {
        try
        {
            _logger?.LogInformation("Processing TIFF file: {FilePath}", imageFile.OriginalFilePath);
            
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.LoadingFile, "Reading TIFF pages...", 1, 1, 1, 1);
            
            // Image decode timing
            var decodeStart = DateTime.UtcNow;
            
            using var magickImageCollection = new MagickImageCollection();
            magickImageCollection.Read(imageFile.OriginalFilePath);
            
            var frameCount = magickImageCollection.Count;
            metrics.ImageDecodeTime = DateTime.UtcNow - decodeStart;
            
            imageFile.IsMultiPage = frameCount > 1;
            imageFile.PageCount = frameCount;
            
            // Get dimensions from the first frame
            var firstImage = magickImageCollection[0];
            imageFile.Width = (int)firstImage.Width;
            imageFile.Height = (int)firstImage.Height;

            var splitPages = new List<ImageFile>();

            // Conversion timing starts here
            var conversionStart = DateTime.UtcNow;
            
            for (int frame = 0; frame < frameCount; frame++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (progressReporter != null)
                    await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.ConvertingPage, 
                        $"Converting page {frame + 1} of {frameCount}...", 1, 1, frame + 1, frameCount);
                
                var magickImage = magickImageCollection[frame];
                var fileNameBase = Path.GetFileNameWithoutExtension(imageFile.FileName);
                var pageFileName = !string.IsNullOrEmpty(jobId) 
                    ? $"{fileNameBase}_{jobId}_page_{frame + 1}.png"
                    : $"{fileNameBase}_page_{frame + 1}.png";
                var pagePath = Path.Combine(_outputDirectory, pageFileName);
                
                _logger?.LogDebug("Converting TIFF frame {FrameNum} of {FrameCount} to PNG: {PagePath}", 
                    frame + 1, frameCount, pagePath);
                
                // Convert to PNG using ImageMagick
                magickImage.Format = MagickFormat.Png;
                
                // Use retry mechanism for file write operations to handle file locking
                await WriteFileWithRetryAsync(magickImage, pagePath, cancellationToken);
                
                // Save to storage provider if configured
                var relativePath = pageFileName;
                var storedPath = await SaveFileAsync(pagePath, relativePath);
                
                // Clean up temporary file after successful storage upload (for cloud storage)
                await CleanupTemporaryFileAsync(pagePath, storedPath);
                
                imageFile.SplitPagePaths.Add(storedPath);

                // Create ImageFile for each page
                var pageImage = new ImageFile
                {
                    OriginalFilePath = storedPath,
                    FileName = Path.GetFileName(pagePath),
                    OriginalFormat = ".tiff",
                    Width = (int)magickImage.Width,
                    Height = (int)magickImage.Height,
                    ConvertedPngPath = storedPath,
                    IsMultiPage = false,
                    PageCount = 1
                };

                // Use PNG file directly instead of generating thumbnails
                pageImage.ThumbnailPath = pagePath; // Use the converted PNG directly
                
                splitPages.Add(pageImage);
                _logger?.LogDebug("Generated page {PageNumber} for TIFF: {PagePath}", frame + 1, pagePath);
            }
            
            metrics.ConversionTime = DateTime.UtcNow - conversionStart;

            // Use the first page's PNG as the main thumbnail
            var thumbnailStart = DateTime.UtcNow;
            if (splitPages.Any())
            {
                imageFile.ThumbnailPath = splitPages[0].ThumbnailPath;
            }
            metrics.ThumbnailGenerationTime = DateTime.UtcNow - thumbnailStart;

            var result = ProcessingResult.Successful(imageFile, TimeSpan.Zero);
            result.SplitPages = splitPages;
            
            _logger?.LogInformation("Successfully processed TIFF with {PageCount} pages", frameCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing TIFF file: {FilePath}", imageFile.OriginalFilePath);
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.Failed, "Failed to process TIFF", 1, 1, 1, 1, ex.Message);
            return ProcessingResult.Failed($"Error processing TIFF: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessPdfAsync(ImageFile imageFile, ProcessingMetrics metrics, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken, string? jobId = null)
    {
        try
        {
            _logger?.LogInformation("Processing PDF file: {FilePath}", imageFile.OriginalFilePath);
            
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.LoadingFile, "Reading PDF pages...", 1, 1, 1, 1);
            
            var decodeStart = DateTime.UtcNow;
            
            using var magickImageCollection = new MagickImageCollection();
            
            // Set PDF read settings for better quality
            var readSettings = new MagickReadSettings()
            {
                Density = new Density(150), // 150 DPI for good quality
                Format = MagickFormat.Pdf
            };
            
            magickImageCollection.Read(imageFile.OriginalFilePath, readSettings);
            
            var pageCount = magickImageCollection.Count;
            imageFile.IsMultiPage = pageCount > 1;
            imageFile.PageCount = pageCount;
            metrics.ImageDecodeTime = DateTime.UtcNow - decodeStart;

            // Get dimensions from the first page
            if (magickImageCollection.Count > 0)
            {
                var firstPage = magickImageCollection[0];
                imageFile.Width = (int)firstPage.Width;
                imageFile.Height = (int)firstPage.Height;
            }

            var splitPages = new List<ImageFile>();
            
            var conversionStart = DateTime.UtcNow;
            for (int pageNum = 0; pageNum < pageCount; pageNum++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                
                if (progressReporter != null)
                    await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.ConvertingPage,
                        $"Converting page {pageNum + 1} of {pageCount}...", 1, 1, pageNum + 1, pageCount);
                
                var magickImage = magickImageCollection[pageNum];
                var fileNameBase = Path.GetFileNameWithoutExtension(imageFile.FileName);
                var pageFileName = !string.IsNullOrEmpty(jobId) 
                    ? $"{fileNameBase}_{jobId}_page_{pageNum + 1}.png"
                    : $"{fileNameBase}_page_{pageNum + 1}.png";
                var pagePath = Path.Combine(_outputDirectory, pageFileName);

                _logger?.LogDebug("Converting PDF page {PageNum} of {PageCount} to PNG: {PagePath}", 
                    pageNum + 1, pageCount, pagePath);

                // Convert PDF page to PNG with high quality
                magickImage.Format = MagickFormat.Png;
                magickImage.ColorType = ColorType.TrueColor;
                magickImage.BackgroundColor = MagickColors.White;
                
                // Use retry mechanism for file write operations to handle file locking
                await WriteFileWithRetryAsync(magickImage, pagePath, cancellationToken);
                
                // Get file size with retry mechanism to handle potential file lock conflicts
                long fileSize = await GetFileSizeWithRetryAsync(pagePath, cancellationToken);
                
                // Save to storage provider if configured
                var relativePath = pageFileName;
                var storedPath = await SaveFileAsync(pagePath, relativePath);
                
                // Clean up temporary file after successful storage upload (for cloud storage)
                await CleanupTemporaryFileAsync(pagePath, storedPath);
                
                imageFile.SplitPagePaths.Add(storedPath);

                // Create a proper ImageFile entry for each page
                var pageImage = new ImageFile
                {
                    OriginalFilePath = storedPath,
                    FileName = Path.GetFileName(pagePath),
                    OriginalFormat = ".pdf",
                    Width = (int)magickImage.Width,
                    Height = (int)magickImage.Height,
                    ConvertedPngPath = storedPath,
                    IsMultiPage = false,
                    PageCount = 1,
                    FileSize = fileSize,
                    CreatedDate = DateTime.UtcNow
                };

                // Use stored file directly instead of generating thumbnails
                pageImage.ThumbnailPath = storedPath; // Use the stored PNG directly
                
                splitPages.Add(pageImage);
                _logger?.LogDebug("Generated page {PageNumber} for PDF: {PagePath}", pageNum + 1, pagePath);
            }
            metrics.ConversionTime = DateTime.UtcNow - conversionStart;

            // Use the first page's PNG as the main thumbnail
            var thumbnailStart = DateTime.UtcNow;
            if (splitPages.Any())
            {
                imageFile.ThumbnailPath = splitPages[0].ThumbnailPath;
            }
            metrics.ThumbnailGenerationTime = DateTime.UtcNow - thumbnailStart;

            var result = ProcessingResult.Successful(imageFile, TimeSpan.Zero);
            result.SplitPages = splitPages;
            
            _logger?.LogInformation("Successfully processed PDF with {PageCount} pages", pageCount);
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing PDF file: {FilePath}", imageFile.OriginalFilePath);
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.Failed, "Failed to process PDF", 1, 1, 1, 1, ex.Message);
            return ProcessingResult.Failed($"Error processing PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Performs AI analysis on processed document using Mistral LLM API
    /// </summary>
    /// <param name="processingResult">The processing result to analyze</param>
    /// <param name="progressReporter">Progress reporter for updates</param>
    /// <param name="cancellationToken">Cancellation token</param>
    private async Task PerformAiAnalysisAsync(ProcessingResult processingResult, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken)
    {
        try
        {
            if (_mistralService == null || processingResult.ProcessedImage == null)
                return;

            var fileName = processingResult.ProcessedImage.FileName;
            if (progressReporter != null)
                await progressReporter.ReportProgress(fileName, ProgressStatus.ProcessingFile, "Performing AI document analysis...");

            // Extract text from document
            var extractedText = processingResult.ExtractedText;
            
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger?.LogInformation("No text available for AI analysis for {FileName}", fileName);
                return;
            }

            _logger?.LogInformation("Extracted {TextLength} characters of text from {FileName}, starting AI analysis", 
                extractedText.Length, fileName);

            var aiStart = DateTime.UtcNow;
            var aiResult = await _mistralService.AnalyzeDocumentAsync(extractedText, cancellationToken);
            var aiProcessingTime = DateTime.UtcNow - aiStart;

            // Store AI result regardless of success/failure to provide frontend visibility
            processingResult.AiAnalysis = aiResult;
            if (processingResult.Metrics != null)
            {
                processingResult.Metrics.AiAnalysisTime = aiProcessingTime;
            }

            if (aiResult.Success)
            {
                _logger?.LogInformation("AI analysis completed for {FileName}. Document type: {DocumentType}, Confidence: {Confidence:F2}", 
                    fileName, aiResult.DocumentType, aiResult.Confidence);

                if (progressReporter != null)
                    await progressReporter.ReportProgress(fileName, ProgressStatus.ProcessingFile, 
                        $"AI analysis completed - Document type: {aiResult.DocumentType}");
            }
            else
            {
                _logger?.LogWarning("AI analysis failed for {FileName}: {Message}", fileName, aiResult.Message);
                
                if (progressReporter != null)
                    await progressReporter.ReportProgress(fileName, ProgressStatus.ProcessingFile, 
                        $"AI analysis failed: {aiResult.Message}");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during AI analysis for document");
            
            // Store the error result so users can see what happened
            processingResult.AiAnalysis = DocumentAiResult.Failed($"Analysis error: {ex.Message}");
            
            if (progressReporter != null)
                await progressReporter.ReportProgress(processingResult.ProcessedImage?.FileName ?? "Unknown", 
                    ProgressStatus.ProcessingFile, $"AI analysis error: {ex.Message}");
            
            // Don't fail the entire processing pipeline if AI analysis fails
        }
    }

    /// <summary>
    /// Extracts text from processed document for AI analysis using OCR
    /// </summary>
    /// <param name="processingResult">The processing result containing processed images</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <param name="useMistralOcr">Whether to use Mistral OCR instead of Tesseract</param>
    /// <returns>Extracted text content</returns>
    private async Task<string> ExtractTextFromDocumentAsync(ProcessingResult processingResult, CancellationToken cancellationToken, bool useMistralOcr = false)
    {
        try
        {
            var extractedText = new StringBuilder();

            // Choose OCR method based on configuration and service availability
            if (useMistralOcr && _mistralOcrService != null)
            {
                return await ExtractTextUsingMistralOcrAsync(processingResult, cancellationToken);
            }
            else
            {
                return await ExtractTextUsingTesseractAsync(processingResult, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error extracting text from document using OCR");
            
            // Fallback to placeholder text if OCR fails
            var fileName = processingResult.ProcessedImage?.FileName ?? "Unknown";
            var extension = processingResult.ProcessedImage?.OriginalFormat?.ToLowerInvariant() ?? "unknown";
            return $"[OCR_ERROR] Failed to extract text from {extension} file: {fileName}. Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Extracts text using Mistral OCR API
    /// </summary>
    /// <param name="processingResult">The processing result containing processed images</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    private async Task<string> ExtractTextUsingMistralOcrAsync(ProcessingResult processingResult, CancellationToken cancellationToken)
    {
        var extractedText = new StringBuilder();

        // Process single image or main image
        if (processingResult.ProcessedImage?.ConvertedPngPath != null)
        {
            var ocrResult = await _mistralOcrService!.ProcessDocumentFromFileAsync(
                processingResult.ProcessedImage.ConvertedPngPath, 
                cancellationToken: cancellationToken);
            
            if (ocrResult.Success && !string.IsNullOrWhiteSpace(ocrResult.Text))
            {
                extractedText.AppendLine(ocrResult.Text);
                _logger?.LogInformation("Mistral OCR extracted {TextLength} characters with confidence {Confidence:P1}", 
                    ocrResult.Text.Length, ocrResult.Confidence);
            }
            else
            {
                _logger?.LogWarning("Mistral OCR failed for image: {Error}", ocrResult.Message);
            }
        }

        // Process split pages for multi-page documents
        if (processingResult.SplitPages != null && processingResult.SplitPages.Count > 0)
        {
            foreach (var page in processingResult.SplitPages)
            {
                if (page.ConvertedPngPath != null)
                {
                    var ocrResult = await _mistralOcrService!.ProcessDocumentFromFileAsync(
                        page.ConvertedPngPath,
                        cancellationToken: cancellationToken);
                    
                    if (ocrResult.Success && !string.IsNullOrWhiteSpace(ocrResult.Text))
                    {
                        extractedText.AppendLine($"--- Page {page.FileName} ---");
                        extractedText.AppendLine(ocrResult.Text);
                    }
                }
            }
        }

        var result = extractedText.ToString().Trim();
        
        // Return a meaningful message if no text was extracted
        if (string.IsNullOrWhiteSpace(result))
        {
            var fileName = processingResult.ProcessedImage?.FileName ?? "Unknown";
            var extension = processingResult.ProcessedImage?.OriginalFormat?.ToLowerInvariant() ?? "unknown";
            return $"No text was extracted from {extension} file: {fileName} using Mistral OCR. The document may be an image without readable text.";
        }
        
        return result;
    }

    /// <summary>
    /// Extracts text using Tesseract OCR (existing implementation)
    /// </summary>
    /// <param name="processingResult">The processing result containing processed images</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    private async Task<string> ExtractTextUsingTesseractAsync(ProcessingResult processingResult, CancellationToken cancellationToken)
    {
        try
        {
            var extractedText = new StringBuilder();

            // Process single image or main image
            if (processingResult.ProcessedImage?.ConvertedPngPath != null)
            {
                var imageText = await ExtractTextFromImageAsync(processingResult.ProcessedImage.ConvertedPngPath, cancellationToken);
                if (!string.IsNullOrWhiteSpace(imageText))
                {
                    extractedText.AppendLine(imageText);
                }
            }

            // Process split pages for multi-page documents
            if (processingResult.SplitPages != null && processingResult.SplitPages.Count > 0)
            {
                foreach (var page in processingResult.SplitPages)
                {
                    if (page.ConvertedPngPath != null)
                    {
                        var pageText = await ExtractTextFromImageAsync(page.ConvertedPngPath, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(pageText))
                        {
                            extractedText.AppendLine($"--- Page {page.FileName} ---");
                            extractedText.AppendLine(pageText);
                        }
                    }
                }
            }

            var result = extractedText.ToString().Trim();
            
            // Return a meaningful message if no text was extracted
            if (string.IsNullOrWhiteSpace(result))
            {
                var fileName = processingResult.ProcessedImage?.FileName ?? "Unknown";
                var extension = processingResult.ProcessedImage?.OriginalFormat?.ToLowerInvariant() ?? "unknown";
                return $"No text was extracted from {extension} file: {fileName}. The document may be an image without readable text.";
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error extracting text from document using Tesseract OCR");
            
            // Fallback to placeholder text if OCR fails
            var fileName = processingResult.ProcessedImage?.FileName ?? "Unknown";
            var extension = processingResult.ProcessedImage?.OriginalFormat?.ToLowerInvariant() ?? "unknown";
            return $"[OCR_ERROR] Failed to extract text from {extension} file: {fileName}. Error: {ex.Message}";
        }
    }

    /// <summary>
    /// Extracts text from a single image file using Tesseract OCR
    /// </summary>
    /// <param name="imagePath">Path to the image file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    private async Task<string> ExtractTextFromImageAsync(string imagePath, CancellationToken cancellationToken)
    {
        return await Task.Run(() =>
        {
            try
            {
                if (!File.Exists(imagePath))
                {
                    _logger?.LogWarning("Image file not found for OCR: {ImagePath}", imagePath);
                    return string.Empty;
                }

                // Get the tessdata path relative to the output directory
                var tessdataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");
                if (!Directory.Exists(tessdataPath))
                {
                    _logger?.LogError("Tessdata directory not found at: {TessdataPath}", tessdataPath);
                    return string.Empty;
                }

                using var engine = new Tesseract.TesseractEngine(tessdataPath, "eng", Tesseract.EngineMode.Default);
                using var img = Tesseract.Pix.LoadFromFile(imagePath);
                using var page = engine.Process(img);
                
                var text = page.GetText();
                
                // Clean up the extracted text
                if (!string.IsNullOrWhiteSpace(text))
                {
                    // Remove excessive whitespace and normalize line endings
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
                    text = text.Replace("\n ", "\n").Replace(" \n", "\n");
                    text = System.Text.RegularExpressions.Regex.Replace(text, @"\n\s*\n", "\n");
                }
                
                return text?.Trim() ?? string.Empty;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error during OCR processing of image: {ImagePath}", imagePath);
                return string.Empty;
            }
        }, cancellationToken);
    }

    /// <summary>
    /// Helper method to calculate thumbnail dimensions while maintaining aspect ratio
    /// </summary>
    private static (int width, int height) CalculateThumbnailDimensions(int originalWidth, int originalHeight, int maxSize)
    {
        if (originalWidth <= maxSize && originalHeight <= maxSize)
        {
            return (originalWidth, originalHeight);
        }

        double aspectRatio = (double)originalWidth / originalHeight;
        
        if (originalWidth > originalHeight)
        {
            return (maxSize, (int)(maxSize / aspectRatio));
        }
        else
        {
            return ((int)(maxSize * aspectRatio), maxSize);
        }
    }

    /// <summary>
    /// Optimized text extraction and AI analysis that can run operations in parallel when possible
    /// </summary>
    private async Task OptimizeTextExtractionAndAnalysisAsync(
        ProcessingResult result, 
        DetailedProgressReporter? progressReporter, 
        CancellationToken cancellationToken, 
        bool useMistralOcr, 
        bool useMistralAI, 
        bool enableOcr,
        string fileName)
    {
        // If OCR is disabled, only do AI analysis if requested
        if (!enableOcr)
        {
            if (useMistralAI && _mistralService != null)
            {
                // Perform AI analysis without OCR text
                await PerformAiAnalysisAsync(result, progressReporter, cancellationToken);
            }
            return;
        }
        
        // Scenario 1: Both OCR and AI analysis needed with Mistral OCR
        // We can do OCR and let AI analysis wait for the text
        if (useMistralOcr && useMistralAI && _mistralOcrService != null && _mistralService != null)
        {
            try
            {
                if (progressReporter != null)
                    await progressReporter.ReportProgress(fileName, ProgressStatus.ProcessingFile, "Extracting text and analyzing document...");

                // Do OCR first, then AI analysis (sequential for now since AI needs OCR text)
                var extractedText = await ExtractTextFromDocumentAsync(result, cancellationToken, useMistralOcr);
                result.ExtractedText = extractedText;

                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    _logger?.LogInformation("Extracted {TextLength} characters of text from {FileName}", 
                        extractedText.Length, fileName);

                    // Now do AI analysis with the extracted text
                    await PerformAiAnalysisAsync(result, progressReporter, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Optimized processing failed for {FileName}, falling back to individual operations", fileName);
                await FallbackToSequentialProcessing(result, progressReporter, cancellationToken, useMistralOcr, useMistralAI, enableOcr, fileName);
            }
        }
        // Scenario 2: Only OCR needed
        else if (!useMistralAI)
        {
            try
            {
                if (progressReporter != null)
                    await progressReporter.ReportProgress(fileName, ProgressStatus.ProcessingFile, "Extracting text (OCR)...");
                
                var extractedText = await ExtractTextFromDocumentAsync(result, cancellationToken, useMistralOcr);
                result.ExtractedText = extractedText;
                
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    _logger?.LogInformation("Extracted {TextLength} characters of text from {FileName}", 
                        extractedText.Length, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OCR text extraction failed for {FileName}", fileName);
            }
        }
        // Scenario 3: Both needed but with traditional OCR (we could potentially parallelize if we had cached text)
        else
        {
            await FallbackToSequentialProcessing(result, progressReporter, cancellationToken, useMistralOcr, useMistralAI, enableOcr, fileName);
        }
    }

    /// <summary>
    /// Fallback to original sequential processing
    /// </summary>
    private async Task FallbackToSequentialProcessing(
        ProcessingResult result, 
        DetailedProgressReporter? progressReporter, 
        CancellationToken cancellationToken, 
        bool useMistralOcr, 
        bool useMistralAI, 
        bool enableOcr,
        string fileName)
    {
        // Extract OCR text only if OCR is enabled
        if (enableOcr)
        {
            try
            {
                if (progressReporter != null)
                    await progressReporter.ReportProgress(fileName, ProgressStatus.ProcessingFile, "Extracting text (OCR)...");
                
                var extractedText = await ExtractTextFromDocumentAsync(result, cancellationToken, useMistralOcr);
                result.ExtractedText = extractedText;
                
                if (!string.IsNullOrWhiteSpace(extractedText))
                {
                    _logger?.LogInformation("Extracted {TextLength} characters of text from {FileName}", 
                        extractedText.Length, fileName);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "OCR text extraction failed for {FileName}", fileName);
                // Don't fail the entire processing pipeline if OCR fails
            }
        }
        
        // Perform AI analysis if Mistral service is available and user requested it
        if (_mistralService != null && useMistralAI)
        {
            await PerformAiAnalysisAsync(result, progressReporter, cancellationToken);
        }
    }

    /// <summary>
    /// Write MagickImage to file with retry mechanism to handle file locking issues
    /// </summary>
    /// <param name="magickImage">The IMagickImage to write</param>
    /// <param name="filePath">Path where to write the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the async operation</returns>
    private async Task WriteFileWithRetryAsync(IMagickImage magickImage, string filePath, CancellationToken cancellationToken)
    {
        const int maxRetries = 3;
        const int baseDelayMs = 200;
        
        _logger?.LogDebug("Starting file write with retry for: {FilePath}", filePath);
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Add delay before retry attempts to allow file handles to be released
                if (attempt > 0)
                {
                    var delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                    _logger?.LogDebug("File write retry attempt {Attempt} for {FilePath}, waiting {DelayMs}ms", 
                        attempt + 1, filePath, delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }
                
                // Attempt to write the file
                await magickImage.WriteAsync(filePath, cancellationToken);
                
                // Force immediate disposal of any file handles
                GC.Collect();
                GC.WaitForPendingFinalizers();
                
                // Add small delay to ensure OS releases file handles
                await Task.Delay(50, cancellationToken);
                
                _logger?.LogDebug("Successfully wrote file on attempt {Attempt}: {FilePath}", 
                    attempt + 1, filePath);
                return; // Success - exit the retry loop
            }
            catch (IOException ex) when (attempt < maxRetries - 1 && IsFileLockException(ex))
            {
                _logger?.LogWarning("File lock detected on attempt {Attempt} for {FilePath}: {Error}. Retrying...", 
                    attempt + 1, filePath, ex.Message);
                // Continue to next retry attempt
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxRetries - 1)
            {
                _logger?.LogWarning("Access denied on attempt {Attempt} for {FilePath}: {Error}. Retrying...", 
                    attempt + 1, filePath, ex.Message);
                // Continue to next retry attempt
            }
        }
        
        // Final attempt without catching exceptions - let it throw if it fails
        _logger?.LogWarning("Final attempt to write file after {MaxRetries} retries: {FilePath}", maxRetries, filePath);
        await magickImage.WriteAsync(filePath, cancellationToken);
    }

    /// <summary>
    /// Determines if an IOException is likely due to file locking
    /// </summary>
    /// <param name="ex">The IOException to check</param>
    /// <returns>True if the exception appears to be file lock related</returns>
    private static bool IsFileLockException(IOException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("being used by another process") ||
               message.Contains("cannot access the file") ||
               message.Contains("sharing violation") ||
               message.Contains("lock");
    }

    /// <summary>
    /// Get file size with retry mechanism to handle file locking issues
    /// </summary>
    /// <param name="filePath">Path to the file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>File size in bytes</returns>
    private async Task<long> GetFileSizeWithRetryAsync(string filePath, CancellationToken cancellationToken)
    {
        const int maxRetries = 5;
        const int baseDelayMs = 100;
        
        _logger?.LogDebug("Getting file size with retry for: {FilePath}", filePath);
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Wait a bit before each attempt to allow file handles to be released
                if (attempt > 0)
                {
                    var delayMs = baseDelayMs * attempt; // Linear backoff for file size checks
                    _logger?.LogDebug("File size retry attempt {Attempt} for {FilePath}, waiting {DelayMs}ms", 
                        attempt + 1, filePath, delayMs);
                    await Task.Delay(delayMs, cancellationToken);
                }
                
                var fileInfo = new FileInfo(filePath);
                if (fileInfo.Exists)
                {
                    var size = fileInfo.Length;
                    _logger?.LogDebug("Successfully got file size on attempt {Attempt}: {FilePath} = {FileSize} bytes", 
                        attempt + 1, filePath, size);
                    return size;
                }
                else
                {
                    throw new FileNotFoundException($"File not found: {filePath}");
                }
            }
            catch (IOException ex) when (attempt < maxRetries - 1 && IsFileLockException(ex))
            {
                _logger?.LogWarning("File lock detected during size check on attempt {Attempt} for {FilePath}: {Error}", 
                    attempt + 1, filePath, ex.Message);
                // Continue to next retry attempt
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxRetries - 1)
            {
                _logger?.LogWarning("Access denied during size check on attempt {Attempt} for {FilePath}: {Error}", 
                    attempt + 1, filePath, ex.Message);
                // Continue to next retry attempt
            }
        }
        
        // Final attempt without catching exceptions
        _logger?.LogWarning("Final attempt to get file size after {MaxRetries} retries: {FilePath}", maxRetries, filePath);
        return new FileInfo(filePath).Length;
    }

    /// <summary>
    /// Save a file using the configured storage provider
    /// </summary>
    /// <param name="filePath">Local file path to save</param>
    /// <param name="relativePath">Relative path in storage</param>
    /// <returns>The URL or path where the file can be accessed</returns>
    private async Task<string> SaveFileAsync(string filePath, string relativePath)
    {
        if (_storageService?.StorageProvider != null)
        {
            // Use the configured storage provider
            return await _storageService.StorageProvider.SaveFileAsync(filePath, relativePath);
        }
        
        // Legacy mode: file is already in the correct location
        return filePath;
    }

    /// <summary>
    /// Clean up temporary file after successful storage upload
    /// Only deletes temp files when using cloud storage (not local storage)
    /// </summary>
    /// <param name="tempFilePath">Path to the temporary file</param>
    /// <param name="storedPath">Path where file was stored (to determine if cleanup is needed)</param>
    private async Task CleanupTemporaryFileAsync(string tempFilePath, string storedPath)
    {
        // Only clean up temp files if we're using cloud storage (paths are different)
        // For local storage, tempFilePath and storedPath are the same, so don't delete
        if (_storageService?.StorageProvider != null && tempFilePath != storedPath)
        {
            try
            {
                await DeleteFileWithRetryAsync(tempFilePath);
                _logger?.LogDebug("Cleaned up temporary file after storage upload: {TempFilePath}", tempFilePath);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clean up temporary file {TempFilePath} after storage upload", tempFilePath);
                // Don't throw - cleanup failure shouldn't break the main processing flow
            }
        }
    }

    /// <summary>
    /// Delete a file with retry mechanism to handle file locking issues
    /// </summary>
    /// <param name="filePath">Path to the file to delete</param>
    private async Task DeleteFileWithRetryAsync(string filePath)
    {
        if (!File.Exists(filePath))
            return;

        const int maxRetries = 5;
        const int baseDelayMs = 100;
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                if (attempt > 0)
                {
                    // Use longer delays for file deletion to allow handles to be fully released
                    var delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                    _logger?.LogDebug("File deletion retry attempt {Attempt} for {FilePath}, waiting {DelayMs}ms", 
                        attempt + 1, filePath, delayMs);
                    await Task.Delay(delayMs);
                }
                
                File.Delete(filePath);
                _logger?.LogDebug("Successfully deleted file on attempt {Attempt}: {FilePath}", 
                    attempt + 1, filePath);
                return;
            }
            catch (IOException ex) when (attempt < maxRetries - 1 && IsFileLockException(ex))
            {
                _logger?.LogWarning("File lock detected during deletion on attempt {Attempt} for {FilePath}: {Error}. Retrying...", 
                    attempt + 1, filePath, ex.Message);
                // Continue to next retry attempt
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxRetries - 1)
            {
                _logger?.LogWarning("Access denied during deletion on attempt {Attempt} for {FilePath}: {Error}. Retrying...", 
                    attempt + 1, filePath, ex.Message);
                // Continue to next retry attempt
            }
        }
        
        // Final attempt without catching exceptions - let it throw if it fails
        _logger?.LogWarning("Final attempt to delete file after {MaxRetries} retries: {FilePath}", maxRetries, filePath);
        try
        {
            File.Delete(filePath);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete temporary file after {MaxRetries} attempts: {FilePath}", maxRetries, filePath);
            // Don't throw - this is cleanup, shouldn't break main flow
        }
    }

    /// <summary>
    /// Get supported file extensions
    /// </summary>
    public static string[] GetSupportedExtensions()
    {
        return new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".pdf", ".webp" };
    }

    /// <summary>
    /// Save processed document to database if document service is available
    /// </summary>
    private async Task SaveDocumentToDatabaseAsync(ProcessingResult result, string originalFilePath, CancellationToken cancellationToken)
    {
        if (_serviceProvider == null || result.ProcessedImage == null)
        {
            return;
        }

        try
        {
            // Create a scope to get the scoped DocumentService
            using var scope = _serviceProvider.CreateScope();
            var documentService = scope.ServiceProvider.GetService<IDocumentService>();
            
            if (documentService == null)
            {
                _logger?.LogDebug("DocumentService not available, skipping database save");
                return;
            }

            var document = new Document
            {
                FileName = result.ProcessedImage.FileName,
                FilePath = result.ProcessedImage.OriginalFilePath,
                ContentType = GetContentType(result.ProcessedImage.OriginalFormat),
                FileSizeBytes = result.ProcessedImage.FileSize,
                CreatedAt = result.ProcessedImage.CreatedDate,
                UpdatedAt = DateTime.UtcNow,
                ExtractedText = result.ExtractedText,
                OcrMethod = result.Metrics?.OcrMethod,
                OcrConfidence = result.Metrics?.OcrConfidence,
                OcrProcessingTime = result.Metrics?.OcrProcessingTime,
                PageCount = result.ProcessedImage.PageCount,
                ThumbnailPath = result.ProcessedImage.ThumbnailPath,
                Status = DocumentStatus.Completed
            };

            // Add metadata as JSON
            if (result.ProcessedImage.Metadata.Any() || result.AiAnalysis != null)
            {
                var metadata = new Dictionary<string, object>();
                
                // Copy existing metadata
                foreach (var kvp in result.ProcessedImage.Metadata)
                {
                    metadata[kvp.Key] = kvp.Value;
                }

                // Add AI analysis if available
                if (result.AiAnalysis != null)
                {
                    metadata["AiAnalysis"] = result.AiAnalysis;
                }

                // Add processing metrics
                if (result.Metrics != null)
                {
                    metadata["ProcessingMetrics"] = new
                    {
                        ProcessingTime = result.Metrics.ProcessingTime.TotalSeconds,
                        FileLoadTime = result.Metrics.FileLoadTime?.TotalSeconds,
                        ConversionTime = result.Metrics.ConversionTime?.TotalSeconds,
                        ThumbnailGenerationTime = result.Metrics.ThumbnailGenerationTime?.TotalSeconds,
                        AiAnalysisTime = result.Metrics.AiAnalysisTime?.TotalSeconds,
                        OcrProcessingTime = result.Metrics.OcrProcessingTime?.TotalSeconds
                    };
                }

                document.Metadata = JsonSerializer.Serialize(metadata);
            }

            await documentService.CreateDocumentAsync(document);
            _logger?.LogInformation("Saved document {FileName} to database with ID {DocumentId}", 
                document.FileName, document.Id);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to save document {FileName} to database", 
                result.ProcessedImage.FileName);
            // Don't throw - this shouldn't break the main processing flow
        }
    }

    /// <summary>
    /// Get content type based on file extension
    /// </summary>
    private static string GetContentType(string fileExtension)
    {
        return fileExtension?.ToLowerInvariant() switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".bmp" => "image/bmp",
            ".gif" => "image/gif",
            ".tif" or ".tiff" => "image/tiff",
            ".webp" => "image/webp",
            _ => "application/octet-stream"
        };
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}