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
    public async Task<ProcessingResult> ProcessImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            var overallStart = DateTime.UtcNow;
            var metrics = new ProcessingMetrics { StartTime = overallStart };
            
            if (!File.Exists(filePath))
            {
                return ProcessingResult.Failed($"File not found: {filePath}");
            }

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

            ProcessingResult result;
            
            switch (extension)
            {
                case ".pdf":
                    result = await ProcessPdfAsync(imageFile, metrics, cancellationToken);
                    break;
                case ".tif":
                case ".tiff":
                    result = await ProcessMultipageTiffAsync(imageFile, metrics, cancellationToken);
                    break;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".webp":
                    result = await ProcessSingleImageAsync(imageFile, metrics, cancellationToken);
                    break;
                default:
                    result = ProcessingResult.Failed($"Unsupported file format: {extension}");
                    break;
            }

            if (result.Success && result.ProcessedImage != null)
            {
                var totalTime = DateTime.UtcNow - overallStart;
                result.ProcessingTime = totalTime;
                metrics.TotalProcessingTime = totalTime;
                result.Metrics = metrics;
            }

            return result;
        }
        catch (Exception ex)
        {
            return ProcessingResult.Failed($"Error processing {filePath}: {ex.Message}", ex);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Process multiple files concurrently
    /// </summary>
    public async Task<List<ProcessingResult>> ProcessMultipleImagesAsync(
        IEnumerable<string> filePaths, 
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var tasks = filePaths.Select(async filePath =>
        {
            var result = await ProcessImageAsync(filePath, cancellationToken);
            progress?.Report(1);
            return result;
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<ProcessingResult> ProcessSingleImageAsync(ImageFile imageFile, ProcessingMetrics metrics, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Processing single image: {FilePath}", imageFile.OriginalFilePath);
            
            // Image decode timing
            var decodeStart = DateTime.UtcNow;
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imageFile.OriginalFilePath, cancellationToken);
            metrics.ImageDecodeTime = DateTime.UtcNow - decodeStart;
            
            imageFile.Width = image.Width;
            imageFile.Height = image.Height;
            imageFile.IsMultiPage = false;
            imageFile.PageCount = 1;

            // Conversion timing
            var conversionStart = DateTime.UtcNow;
            var pngPath = Path.Combine(_outputDirectory, $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}.png");
            await image.SaveAsPngAsync(pngPath, cancellationToken);
            imageFile.ConvertedPngPath = pngPath;
            metrics.ConversionTime = DateTime.UtcNow - conversionStart;

            // Thumbnail generation timing
            var thumbnailStart = DateTime.UtcNow;
            var thumbnailPath = await GenerateThumbnailAsync(image, imageFile.FileName, cancellationToken);
            imageFile.ThumbnailPath = thumbnailPath;
            metrics.ThumbnailGenerationTime = DateTime.UtcNow - thumbnailStart;

            _logger?.LogDebug("Successfully processed single image: {FilePath}", imageFile.OriginalFilePath);
            return ProcessingResult.Successful(imageFile, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing single image: {FilePath}", imageFile.OriginalFilePath);
            return ProcessingResult.Failed($"Error processing single image: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessMultipageTiffAsync(ImageFile imageFile, ProcessingMetrics metrics, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Processing TIFF file: {FilePath}", imageFile.OriginalFilePath);
            
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

                // Generate thumbnail for each page using ImageSharp
                using var imageSharpImage = await SixLabors.ImageSharp.Image.LoadAsync(pagePath, cancellationToken);
                pageImage.ThumbnailPath = await GenerateThumbnailAsync(imageSharpImage, $"page_{frame + 1}_{imageFile.FileName}", cancellationToken);
                
                splitPages.Add(pageImage);
                _logger?.LogDebug("Generated page {PageNumber} for TIFF: {PagePath}", frame + 1, pagePath);
            }
            
            metrics.ConversionTime = DateTime.UtcNow - conversionStart;

            // Generate thumbnail for the first page as main thumbnail
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
            return ProcessingResult.Failed($"Error processing TIFF: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessPdfAsync(ImageFile imageFile, ProcessingMetrics metrics, CancellationToken cancellationToken)
    {
        try
        {
            _logger?.LogInformation("Processing PDF file: {FilePath}", imageFile.OriginalFilePath);
            
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

                // Generate thumbnail for each page using ImageSharp
                using var imageSharpImage = await SixLabors.ImageSharp.Image.LoadAsync(pagePath, cancellationToken);
                pageImage.ThumbnailPath = await GenerateThumbnailAsync(imageSharpImage, $"page_{pageNum + 1}_{imageFile.FileName}", cancellationToken);
                
                splitPages.Add(pageImage);
                _logger?.LogDebug("Generated page {PageNumber} for PDF: {PagePath}", pageNum + 1, pagePath);
            }
            metrics.ConversionTime = DateTime.UtcNow - conversionStart;

            // Generate main thumbnail from the first page
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
            return ProcessingResult.Failed($"Error processing PDF: {ex.Message}", ex);
        }
    }

    private async Task<string> GenerateThumbnailAsync(SixLabors.ImageSharp.Image image, string originalFileName, CancellationToken cancellationToken)
    {
        const int thumbnailSize = 200;
        
        var thumbnailPath = Path.Combine(_outputDirectory, 
            $"thumb_{Path.GetFileNameWithoutExtension(originalFileName)}.png");

        using var thumbnail = image.Clone(x => x.Resize(new ResizeOptions
        {
            Size = new SixLabors.ImageSharp.Size(thumbnailSize, thumbnailSize),
            Mode = ResizeMode.Max
        }));

        await thumbnail.SaveAsPngAsync(thumbnailPath, cancellationToken);
        return thumbnailPath;
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