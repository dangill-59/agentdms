using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using ImageMagick;
using Microsoft.Extensions.Logging;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// Service for processing image files, converting formats, and handling multipage documents
/// </summary>
public class ImageProcessingService
{
    private readonly SemaphoreSlim _semaphore;
    private readonly string _outputDirectory;
    private readonly ILogger<ImageProcessingService>? _logger;

    public ImageProcessingService(int maxConcurrency = 4, string? outputDirectory = null, ILogger<ImageProcessingService>? logger = null)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency);
        _outputDirectory = outputDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Output");
        _logger = logger;
        
        // Ensure output directory exists
        Directory.CreateDirectory(_outputDirectory);
        
        // Initialize Magick.NET
        MagickNET.Initialize();
    }

    /// <summary>
    /// Process a single image file asynchronously
    /// </summary>
    public async Task<ProcessingResult> ProcessImageAsync(string filePath, DetailedProgressReporter? progressReporter = null, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            var fileName = Path.GetFileName(filePath);
            await progressReporter?.ReportProgress(fileName, ProgressStatus.Starting, "Starting processing...");
            
            var overallStart = DateTime.UtcNow;
            var metrics = new ProcessingMetrics { StartTime = overallStart };
            
            if (!File.Exists(filePath))
            {
                await progressReporter?.ReportProgress(fileName, ProgressStatus.Failed, "File not found", errorMessage: $"File not found: {filePath}");
                return ProcessingResult.Failed($"File not found: {filePath}");
            }

            await progressReporter?.ReportProgress(fileName, ProgressStatus.LoadingFile, "Loading file...");
            
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

            await progressReporter?.ReportProgress(fileName, ProgressStatus.ProcessingFile, $"Processing {extension.ToUpper()} file...");

            ProcessingResult result;
            
            switch (extension)
            {
                case ".pdf":
                    result = await ProcessPdfAsync(imageFile, metrics, progressReporter, cancellationToken);
                    break;
                case ".tif":
                case ".tiff":
                    result = await ProcessMultipageTiffAsync(imageFile, metrics, progressReporter, cancellationToken);
                    break;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".webp":
                    result = await ProcessSingleImageAsync(imageFile, metrics, progressReporter, cancellationToken);
                    break;
                default:
                    await progressReporter?.ReportProgress(fileName, ProgressStatus.Failed, "Unsupported format", errorMessage: $"Unsupported file format: {extension}");
                    result = ProcessingResult.Failed($"Unsupported file format: {extension}");
                    break;
            }

            if (result.Success && result.ProcessedImage != null)
            {
                var totalTime = DateTime.UtcNow - overallStart;
                result.ProcessingTime = totalTime;
                metrics.TotalProcessingTime = totalTime;
                result.Metrics = metrics;
                
                await progressReporter?.ReportProgress(fileName, ProgressStatus.Completed, "Processing completed successfully");
            }

            return result;
        }
        catch (Exception ex)
        {
            var fileName = Path.GetFileName(filePath);
            await progressReporter?.ReportProgress(fileName, ProgressStatus.Failed, "Processing failed", errorMessage: ex.Message);
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
        CancellationToken cancellationToken = default)
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
                        await progressReporter.OnProgress(fileProgress);
                    });
                }
                
                var result = await ProcessImageAsync(filePath, fileProgressReporter, cancellationToken);
                
                Interlocked.Increment(ref processedCount);
                progress?.Report(processedCount);
                
                return result;
            });

            var batchResults = await Task.WhenAll(batchTasks);
            results.AddRange(batchResults);
        }

        return results;
    }

    private async Task<ProcessingResult> ProcessSingleImageAsync(ImageFile imageFile, ProcessingMetrics metrics, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Processing single image: {FilePath}", imageFile.OriginalFilePath);
            
            await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.LoadingFile, "Decoding image...", 1, 1, 1, 1);
            
            // Image decode timing
            var decodeStart = DateTime.UtcNow;
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imageFile.OriginalFilePath, cancellationToken);
            metrics.ImageDecodeTime = DateTime.UtcNow - decodeStart;
            
            imageFile.Width = image.Width;
            imageFile.Height = image.Height;
            imageFile.IsMultiPage = false;
            imageFile.PageCount = 1;

            await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.ConvertingPage, "Converting to PNG...", 1, 1, 1, 1);

            // Conversion timing
            var conversionStart = DateTime.UtcNow;
            var pngPath = Path.Combine(_outputDirectory, $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}.png");
            await image.SaveAsPngAsync(pngPath, cancellationToken);
            imageFile.ConvertedPngPath = pngPath;
            metrics.ConversionTime = DateTime.UtcNow - conversionStart;

            // Use the PNG file directly instead of generating a thumbnail
            var thumbnailStart = DateTime.UtcNow;
            imageFile.ThumbnailPath = pngPath; // Point to the PNG file directly
            metrics.ThumbnailGenerationTime = DateTime.UtcNow - thumbnailStart;

            _logger?.LogDebug("Successfully processed single image: {FilePath}", imageFile.OriginalFilePath);
            return ProcessingResult.Successful(imageFile, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing single image: {FilePath}", imageFile.OriginalFilePath);
            await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.Failed, "Failed to process image", 1, 1, 1, 1, ex.Message);
            return ProcessingResult.Failed($"Error processing single image: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessMultipageTiffAsync(ImageFile imageFile, ProcessingMetrics metrics, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Processing TIFF file: {FilePath}", imageFile.OriginalFilePath);
            
            await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.LoadingFile, "Reading TIFF pages...", 1, 1, 1, 1);
            
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
                
                await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.ConvertingPage, 
                    $"Converting page {frame + 1} of {frameCount}...", 1, 1, frame + 1, frameCount);
                
                var magickImage = magickImageCollection[frame];
                var pagePath = Path.Combine(_outputDirectory, 
                    $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}_page_{frame + 1}.png");
                
                // Convert to PNG using ImageMagick
                magickImage.Format = MagickFormat.Png;
                await magickImage.WriteAsync(pagePath, cancellationToken);
                imageFile.SplitPagePaths.Add(pagePath);

                // Create ImageFile for each page
                var pageImage = new ImageFile
                {
                    OriginalFilePath = pagePath,
                    FileName = Path.GetFileName(pagePath),
                    OriginalFormat = ".tiff",
                    Width = (int)magickImage.Width,
                    Height = (int)magickImage.Height,
                    ConvertedPngPath = pagePath,
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
            await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.Failed, "Failed to process TIFF", 1, 1, 1, 1, ex.Message);
            return ProcessingResult.Failed($"Error processing TIFF: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessPdfAsync(ImageFile imageFile, ProcessingMetrics metrics, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Processing PDF file: {FilePath}", imageFile.OriginalFilePath);
            
            await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.LoadingFile, "Reading PDF pages...", 1, 1, 1, 1);
            
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
                
                await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.ConvertingPage,
                    $"Converting page {pageNum + 1} of {pageCount}...", 1, 1, pageNum + 1, pageCount);
                
                var magickImage = magickImageCollection[pageNum];
                var pagePath = Path.Combine(_outputDirectory,
                    $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}_page_{pageNum + 1}.png");

                // Convert PDF page to PNG with high quality
                magickImage.Format = MagickFormat.Png;
                magickImage.ColorType = ColorType.TrueColor;
                magickImage.BackgroundColor = MagickColors.White;
                
                await magickImage.WriteAsync(pagePath, cancellationToken);
                imageFile.SplitPagePaths.Add(pagePath);

                // Create a proper ImageFile entry for each page
                var pageImage = new ImageFile
                {
                    OriginalFilePath = pagePath,
                    FileName = Path.GetFileName(pagePath),
                    OriginalFormat = ".pdf",
                    Width = (int)magickImage.Width,
                    Height = (int)magickImage.Height,
                    ConvertedPngPath = pagePath,
                    IsMultiPage = false,
                    PageCount = 1,
                    FileSize = new FileInfo(pagePath).Length,
                    CreatedDate = DateTime.UtcNow
                };

                // Use PNG file directly instead of generating thumbnails
                pageImage.ThumbnailPath = pagePath; // Use the converted PNG directly
                
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
            await progressReporter?.ReportProgress(imageFile.FileName, ProgressStatus.Failed, "Failed to process PDF", 1, 1, 1, 1, ex.Message);
            return ProcessingResult.Failed($"Error processing PDF: {ex.Message}", ex);
        }
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
    /// Get supported file extensions
    /// </summary>
    public static string[] GetSupportedExtensions()
    {
        return new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tif", ".tiff", ".pdf", ".webp" };
    }

    public void Dispose()
    {
        _semaphore?.Dispose();
    }
}