using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using System.Drawing;
using System.Drawing.Imaging;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// Service for processing image files, converting formats, and handling multipage documents
/// </summary>
public class ImageProcessingService
{
    private readonly SemaphoreSlim _semaphore;
    private readonly string _outputDirectory;

    public ImageProcessingService(int maxConcurrency = 4, string? outputDirectory = null)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency);
        _outputDirectory = outputDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Output");
        
        // Ensure output directory exists
        Directory.CreateDirectory(_outputDirectory);
    }

    /// <summary>
    /// Process a single image file asynchronously
    /// </summary>
    public async Task<ProcessingResult> ProcessImageAsync(string filePath, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        
        try
        {
            var startTime = DateTime.UtcNow;
            
            if (!File.Exists(filePath))
            {
                return ProcessingResult.Failed($"File not found: {filePath}");
            }

            var fileInfo = new FileInfo(filePath);
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
                    result = await ProcessPdfAsync(imageFile, cancellationToken);
                    break;
                case ".tif":
                case ".tiff":
                    result = await ProcessMultipageTiffAsync(imageFile, cancellationToken);
                    break;
                case ".jpg":
                case ".jpeg":
                case ".png":
                case ".bmp":
                case ".gif":
                case ".webp":
                    result = await ProcessSingleImageAsync(imageFile, cancellationToken);
                    break;
                default:
                    result = ProcessingResult.Failed($"Unsupported file format: {extension}");
                    break;
            }

            if (result.Success && result.ProcessedImage != null)
            {
                result.ProcessingTime = DateTime.UtcNow - startTime;
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

    private async Task<ProcessingResult> ProcessSingleImageAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        try
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imageFile.OriginalFilePath, cancellationToken);
            
            imageFile.Width = image.Width;
            imageFile.Height = image.Height;
            imageFile.IsMultiPage = false;
            imageFile.PageCount = 1;

            // Convert to PNG
            var pngPath = Path.Combine(_outputDirectory, $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}.png");
            await image.SaveAsPngAsync(pngPath, cancellationToken);
            imageFile.ConvertedPngPath = pngPath;

            // Generate thumbnail
            var thumbnailPath = await GenerateThumbnailAsync(image, imageFile.FileName, cancellationToken);
            imageFile.ThumbnailPath = thumbnailPath;

            return ProcessingResult.Successful(imageFile, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            return ProcessingResult.Failed($"Error processing single image: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessMultipageTiffAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        try
        {
            using var bitmap = new Bitmap(imageFile.OriginalFilePath);
            var frameCount = bitmap.GetFrameCount(FrameDimension.Page);
            
            imageFile.IsMultiPage = frameCount > 1;
            imageFile.PageCount = frameCount;
            imageFile.Width = bitmap.Width;
            imageFile.Height = bitmap.Height;

            var splitPages = new List<ImageFile>();

            for (int frame = 0; frame < frameCount; frame++)
            {
                bitmap.SelectActiveFrame(FrameDimension.Page, frame);
                
                var pagePath = Path.Combine(_outputDirectory, 
                    $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}_page_{frame + 1}.png");
                
                bitmap.Save(pagePath, System.Drawing.Imaging.ImageFormat.Png);
                imageFile.SplitPagePaths.Add(pagePath);

                // Create ImageFile for each page
                var pageImage = new ImageFile
                {
                    OriginalFilePath = pagePath,
                    FileName = Path.GetFileName(pagePath),
                    OriginalFormat = ".tiff",
                    Width = bitmap.Width,
                    Height = bitmap.Height,
                    ConvertedPngPath = pagePath,
                    IsMultiPage = false,
                    PageCount = 1
                };

                // Generate thumbnail for each page
                using var pageImg = await SixLabors.ImageSharp.Image.LoadAsync(pagePath, cancellationToken);
                pageImage.ThumbnailPath = await GenerateThumbnailAsync(pageImg, $"page_{frame + 1}_{imageFile.FileName}", cancellationToken);
                
                splitPages.Add(pageImage);
            }

            // Generate thumbnail for the first page as main thumbnail
            if (splitPages.Any())
            {
                imageFile.ThumbnailPath = splitPages[0].ThumbnailPath;
            }

            var result = ProcessingResult.Successful(imageFile, TimeSpan.Zero);
            result.SplitPages = splitPages;
            return result;
        }
        catch (Exception ex)
        {
            return ProcessingResult.Failed($"Error processing TIFF: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessPdfAsync(ImageFile imageFile, CancellationToken cancellationToken)
    {
        try
        {
            // Note: For a complete PDF processing solution, you would need additional libraries
            // like PDFiumSharp or ImageMagick.NET. For now, we'll create a placeholder implementation.
            
            using var pdfReader = new PdfReader(imageFile.OriginalFilePath);
            using var pdfDocument = new PdfDocument(pdfReader);
            
            var pageCount = pdfDocument.GetNumberOfPages();
            imageFile.IsMultiPage = pageCount > 1;
            imageFile.PageCount = pageCount;

            // This is a simplified implementation - in a real scenario you'd convert each PDF page to an image
            // For now, we'll just extract text and create a placeholder
            var splitPages = new List<ImageFile>();
            
            for (int pageNum = 1; pageNum <= pageCount; pageNum++)
            {
                var page = pdfDocument.GetPage(pageNum);
                var strategy = new SimpleTextExtractionStrategy();
                var text = PdfTextExtractor.GetTextFromPage(page, strategy);
                
                // Create a placeholder image file entry for each page
                var pageImage = new ImageFile
                {
                    OriginalFilePath = imageFile.OriginalFilePath,
                    FileName = $"{Path.GetFileNameWithoutExtension(imageFile.FileName)}_page_{pageNum}.pdf",
                    OriginalFormat = ".pdf",
                    IsMultiPage = false,
                    PageCount = 1,
                    Metadata = new Dictionary<string, object> { { "ExtractedText", text } }
                };
                
                splitPages.Add(pageImage);
            }

            var result = ProcessingResult.Successful(imageFile, TimeSpan.Zero);
            result.SplitPages = splitPages;
            return result;
        }
        catch (Exception ex)
        {
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