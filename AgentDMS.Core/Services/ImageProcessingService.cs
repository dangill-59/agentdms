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
    private readonly MistralDocumentAiService? _mistralService;

    public ImageProcessingService(
        int maxConcurrency = 4, 
        string? outputDirectory = null, 
        ILogger<ImageProcessingService>? logger = null,
        MistralDocumentAiService? mistralService = null)
    {
        _semaphore = new SemaphoreSlim(maxConcurrency);
        _outputDirectory = outputDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Output");
        _logger = logger;
        _mistralService = mistralService;
        
        // Ensure output directory exists
        Directory.CreateDirectory(_outputDirectory);
        
        // Initialize Magick.NET
        MagickNET.Initialize();
    }

    /// <summary>
    /// Process a single image file asynchronously
    /// </summary>
    public async Task<ProcessingResult> ProcessImageAsync(string filePath, DetailedProgressReporter? progressReporter = null, CancellationToken cancellationToken = default, bool useMistralAI = false)
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
                
                // Perform AI analysis if Mistral service is available and user requested it
                if (_mistralService != null && useMistralAI)
                {
                    await PerformAiAnalysisAsync(result, progressReporter, cancellationToken);
                }
                
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
        bool useMistralAI = false)
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
                
                var result = await ProcessImageAsync(filePath, fileProgressReporter, cancellationToken, useMistralAI);
                
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
            
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.LoadingFile, "Decoding image...", 1, 1, 1, 1);
            
            // Image decode timing
            var decodeStart = DateTime.UtcNow;
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(imageFile.OriginalFilePath, cancellationToken);
            metrics.ImageDecodeTime = DateTime.UtcNow - decodeStart;
            
            imageFile.Width = image.Width;
            imageFile.Height = image.Height;
            imageFile.IsMultiPage = false;
            imageFile.PageCount = 1;

            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.ConvertingPage, "Converting to PNG...", 1, 1, 1, 1);

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
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.Failed, "Failed to process image", 1, 1, 1, 1, ex.Message);
            return ProcessingResult.Failed($"Error processing single image: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessMultipageTiffAsync(ImageFile imageFile, ProcessingMetrics metrics, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken)
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
            if (progressReporter != null)
                await progressReporter.ReportProgress(imageFile.FileName, ProgressStatus.Failed, "Failed to process TIFF", 1, 1, 1, 1, ex.Message);
            return ProcessingResult.Failed($"Error processing TIFF: {ex.Message}", ex);
        }
    }

    private async Task<ProcessingResult> ProcessPdfAsync(ImageFile imageFile, ProcessingMetrics metrics, DetailedProgressReporter? progressReporter, CancellationToken cancellationToken)
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
            // TODO: Implement OCR text extraction from processed images
            // This is a placeholder - in a real implementation, you would:
            // 1. Use an OCR library like Tesseract to extract text from the converted PNG files
            // 2. For PDFs, you might also extract text directly using a PDF library
            // 3. Combine extracted text from all pages for multi-page documents
            
            var extractedText = await ExtractTextFromDocumentAsync(processingResult, cancellationToken);
            
            if (string.IsNullOrWhiteSpace(extractedText))
            {
                _logger?.LogInformation("No text extracted from document {FileName}, skipping AI analysis", fileName);
                return;
            }

            _logger?.LogInformation("Extracted {TextLength} characters of text from {FileName}, starting AI analysis", 
                extractedText.Length, fileName);

            var aiStart = DateTime.UtcNow;
            var aiResult = await _mistralService.AnalyzeDocumentAsync(extractedText, cancellationToken);
            var aiProcessingTime = DateTime.UtcNow - aiStart;

            if (aiResult.Success)
            {
                processingResult.AiAnalysis = aiResult;
                if (processingResult.Metrics != null)
                {
                    processingResult.Metrics.AiAnalysisTime = aiProcessingTime;
                }

                _logger?.LogInformation("AI analysis completed for {FileName}. Document type: {DocumentType}, Confidence: {Confidence:F2}", 
                    fileName, aiResult.DocumentType, aiResult.Confidence);

                if (progressReporter != null)
                    await progressReporter.ReportProgress(fileName, ProgressStatus.ProcessingFile, 
                        $"AI analysis completed - Document type: {aiResult.DocumentType}");
            }
            else
            {
                _logger?.LogWarning("AI analysis failed for {FileName}: {Message}", fileName, aiResult.Message);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error during AI analysis for document");
            // Don't fail the entire processing pipeline if AI analysis fails
        }
    }

    /// <summary>
    /// Extracts text from processed document for AI analysis
    /// </summary>
    /// <param name="processingResult">The processing result containing processed images</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extracted text content</returns>
    private async Task<string> ExtractTextFromDocumentAsync(ProcessingResult processingResult, CancellationToken cancellationToken)
    {
        // TODO: Implement OCR text extraction
        // This is a placeholder implementation. In a production system, you would:
        // 
        // 1. Use an OCR library like Tesseract.NET:
        //    using var engine = new TesseractEngine(@"./tessdata", "eng", EngineMode.Default);
        //    using var img = Pix.LoadFromFile(imagePath);
        //    using var page = engine.Process(img);
        //    return page.GetText();
        //
        // 2. For PDFs, extract text directly using iTextSharp or PdfPig:
        //    using var reader = new PdfReader(pdfPath);
        //    var text = new StringBuilder();
        //    for (int page = 1; page <= reader.NumberOfPages; page++)
        //    {
        //        text.Append(PdfTextExtractor.GetTextFromPage(reader, page));
        //    }
        //    return text.ToString();
        //
        // 3. Combine text from multiple pages for multi-page documents
        // 4. Clean and normalize the extracted text
        
        // For now, return a placeholder that indicates text extraction is needed
        await Task.Delay(1, cancellationToken); // Simulate async operation
        
        if (processingResult.ProcessedImage != null)
        {
            // Return a sample text for testing purposes
            // In production, this would be replaced with actual OCR
            var extension = processingResult.ProcessedImage.OriginalFormat?.ToLowerInvariant();
            return $"[OCR_PLACEHOLDER] Document type: {extension}, File: {processingResult.ProcessedImage.FileName}. " +
                   $"This is placeholder text that would be replaced with actual OCR extraction. " +
                   $"Document appears to be a {extension} file with {processingResult.ProcessedImage.PageCount} page(s).";
        }
        
        return string.Empty;
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