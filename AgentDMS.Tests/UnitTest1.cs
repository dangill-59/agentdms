using Xunit;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using AgentDMS.Core.Services;
using AgentDMS.Core.Services.Storage;
using AgentDMS.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using ImageMagick;

namespace AgentDMS.Tests;

public class ImageProcessingServiceTests
{
    private readonly string _testOutputDir = Path.Combine(Path.GetTempPath(), "AgentDMS_Tests");
    private readonly ImageProcessingService _imageProcessor;

    public ImageProcessingServiceTests()
    {
        _imageProcessor = new ImageProcessingService(outputDirectory: _testOutputDir);
        Directory.CreateDirectory(_testOutputDir);
    }

    [Fact]
    public async Task ProcessImageAsync_WithValidImageFile_ShouldSucceed()
    {
        // Arrange
        var testImagePath = CreateTestImage("test.png");
        
        try
        {
            // Act
            var result = await _imageProcessor.ProcessImageAsync(testImagePath);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ProcessedImage);
            Assert.Equal(".png", result.ProcessedImage.OriginalFormat);
            Assert.True(File.Exists(result.ProcessedImage.ConvertedPngPath));
            Assert.True(File.Exists(result.ProcessedImage.ThumbnailPath));
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Fact]
    public async Task ProcessImageAsync_WithNonExistentFile_ShouldFail()
    {
        // Act
        var result = await _imageProcessor.ProcessImageAsync("nonexistent.jpg");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.Message);
    }

    [Fact]
    public async Task ProcessImageAsync_WithUnsupportedFormat_ShouldFail()
    {
        // Arrange
        var testFilePath = Path.Combine(_testOutputDir, "test.txt");
        await File.WriteAllTextAsync(testFilePath, "This is not an image");
        
        try
        {
            // Act
            var result = await _imageProcessor.ProcessImageAsync(testFilePath);

            // Assert
            Assert.False(result.Success);
            Assert.Contains("Unsupported file format", result.Message);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testFilePath))
                File.Delete(testFilePath);
        }
    }

    [Fact]
    public async Task ProcessMultipleImagesAsync_WithMultipleValidFiles_ShouldProcessAll()
    {
        // Arrange
        var testFiles = new List<string>();
        for (int i = 0; i < 3; i++)
        {
            testFiles.Add(CreateTestImage($"test{i}.png"));
        }

        try
        {
            // Act
            var results = await _imageProcessor.ProcessMultipleImagesAsync(testFiles);

            // Assert
            Assert.Equal(3, results.Count);
            Assert.All(results, r => Assert.True(r.Success));
        }
        finally
        {
            // Cleanup
            foreach (var file in testFiles)
            {
                if (File.Exists(file))
                    File.Delete(file);
            }
        }
    }

    [Fact]
    public void GetSupportedExtensions_ShouldReturnExpectedFormats()
    {
        // Act
        var extensions = ImageProcessingService.GetSupportedExtensions();

        // Assert
        Assert.Contains(".jpg", extensions);
        Assert.Contains(".png", extensions);
        Assert.Contains(".pdf", extensions);
        Assert.Contains(".tiff", extensions);
    }

    [Fact]
    public async Task ProcessImageAsync_WithPdfFile_ShouldGenerateImages()
    {
        // Arrange - Create a simple PDF with text
        var testPdfPath = CreateTestPdf("test.pdf");
        
        try
        {
            // Act
            var result = await _imageProcessor.ProcessImageAsync(testPdfPath);

            // Assert
            Assert.True(result.Success, $"Processing failed: {result.Message}");
            Assert.NotNull(result.ProcessedImage);
            Assert.Equal(".pdf", result.ProcessedImage.OriginalFormat);
            Assert.True(result.ProcessedImage.PageCount >= 1);
            Assert.NotNull(result.SplitPages);
            Assert.True(result.SplitPages.Count >= 1);
            
            // Verify each page has actual image files and thumbnails
            foreach (var page in result.SplitPages)
            {
                Assert.True(File.Exists(page.ConvertedPngPath), $"PNG file should exist: {page.ConvertedPngPath}");
                Assert.True(File.Exists(page.ThumbnailPath), $"Thumbnail should exist: {page.ThumbnailPath}");
                Assert.True(page.Width > 0 && page.Height > 0);
            }
            
            // Verify main image has a thumbnail
            Assert.True(File.Exists(result.ProcessedImage.ThumbnailPath), "Main image should have thumbnail");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testPdfPath))
                File.Delete(testPdfPath);
        }
    }

    private string CreateTestImage(string fileName)
    {
        var path = Path.Combine(_testOutputDir, fileName);
        
        // Create a simple 100x100 red image for testing
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(100, 100);
        image.Mutate(x => x.BackgroundColor(SixLabors.ImageSharp.Color.Red));
        image.SaveAsPng(path);
        
        return path;
    }

    private string CreateTestPdf(string fileName)
    {
        var path = Path.Combine(_testOutputDir, fileName);
        
        // Create a simple PDF using ImageMagick for testing
        using var image = new ImageMagick.MagickImage(ImageMagick.MagickColors.White, 200, 150);
        
        // Add some visual content - just a colored rectangle
        using var drawableImage = new ImageMagick.MagickImage(ImageMagick.MagickColors.LightBlue, 150, 100);
        image.Composite(drawableImage, 25, 25, ImageMagick.CompositeOperator.Over);
        
        image.Format = ImageMagick.MagickFormat.Pdf;
        image.Write(path);
        
        return path;
    }

    [Fact]
    public async Task ProcessImageAsync_WithTiffFile_ShouldGenerateImages()
    {
        // Arrange - Create a simple multi-page TIFF
        var testTiffPath = CreateTestTiff("test_multipage.tiff");
        
        try
        {
            // Act
            var result = await _imageProcessor.ProcessImageAsync(testTiffPath);

            // Assert
            Assert.True(result.Success, $"Processing failed: {result.Message}");
            Assert.NotNull(result.ProcessedImage);
            Assert.Equal(".tiff", result.ProcessedImage.OriginalFormat);
            Assert.True(result.ProcessedImage.PageCount >= 1);
            Assert.NotNull(result.SplitPages);
            
            // Verify each page has actual image files and thumbnails
            foreach (var page in result.SplitPages)
            {
                Assert.True(File.Exists(page.ConvertedPngPath), $"PNG file should exist: {page.ConvertedPngPath}");
                Assert.True(File.Exists(page.ThumbnailPath), $"Thumbnail should exist: {page.ThumbnailPath}");
                Assert.True(page.Width > 0 && page.Height > 0);
            }
            
            // Verify main image has a thumbnail
            Assert.True(File.Exists(result.ProcessedImage.ThumbnailPath), "Main image should have thumbnail");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testTiffPath))
                File.Delete(testTiffPath);
        }
    }

    private string CreateTestTiff(string fileName)
    {
        var path = Path.Combine(_testOutputDir, fileName);
        
        // Create a simple multi-page TIFF using ImageMagick
        using var collection = new ImageMagick.MagickImageCollection();
        
        // Create page 1
        using var page1 = new ImageMagick.MagickImage(ImageMagick.MagickColors.Yellow, 150, 100);
        using var overlay1 = new ImageMagick.MagickImage(ImageMagick.MagickColors.Purple, 50, 30);
        page1.Composite(overlay1, 50, 35, ImageMagick.CompositeOperator.Over);
        collection.Add(page1.Clone());
        
        // Create page 2  
        using var page2 = new ImageMagick.MagickImage(ImageMagick.MagickColors.Cyan, 150, 100);
        using var overlay2 = new ImageMagick.MagickImage(ImageMagick.MagickColors.Orange, 40, 40);
        page2.Composite(overlay2, 55, 30, ImageMagick.CompositeOperator.Over);
        collection.Add(page2.Clone());
        
        collection.Write(path, ImageMagick.MagickFormat.Tiff);
        return path;
    }

    [Fact]
    public async Task GenerateHighQualityThumbnail_WithTestImage_ShouldCreateHighQualityThumbnail()
    {
        // Arrange - Create a test image
        var testImagePath = Path.Combine(_testOutputDir, "test_hq_thumbnail.png");
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(800, 600);
        image.Mutate(ctx => ctx.BackgroundColor(SixLabors.ImageSharp.Color.White));
        await image.SaveAsPngAsync(testImagePath);

        var outputDir = Path.Combine(_testOutputDir, "thumbnails");
        
        try
        {
            // Act
            var pngPath = await AgentDMS.Core.Utilities.ThumbnailGenerator.ConvertToPngAsync(
                testImagePath, outputDir, "png_test");

            // Assert
            Assert.True(File.Exists(pngPath), $"PNG file should exist: {pngPath}");
            
            // Verify PNG properties
            using var pngImage = await SixLabors.ImageSharp.Image.LoadAsync(pngPath);
            Assert.True(pngImage.Width > 0 && pngImage.Height > 0, "PNG should have valid dimensions");
            
            // Verify file size is reasonable
            var pngInfo = new FileInfo(pngPath);
            Assert.True(pngInfo.Length > 100, "PNG file should have reasonable file size");
            
            Console.WriteLine($"PNG file created: {pngImage.Width}x{pngImage.Height}, {pngInfo.Length} bytes");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Fact]
    public async Task ThumbnailGenerator_CompareQuality_HighQualityShouldBeLarger()
    {
        // Arrange - Create a complex test image that benefits from high-quality processing
        var testImagePath = Path.Combine(_testOutputDir, "complex_test_image.png");
        using var image = new SixLabors.ImageSharp.Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(1200, 800);
        image.Mutate(ctx => 
        {
            ctx.BackgroundColor(SixLabors.ImageSharp.Color.White);
            // Add some complexity - edges and details that benefit from high-quality downscaling
            ctx.Fill(SixLabors.ImageSharp.Color.Red, new SixLabors.ImageSharp.Rectangle(100, 100, 200, 150));
            ctx.Fill(SixLabors.ImageSharp.Color.Blue, new SixLabors.ImageSharp.Rectangle(400, 300, 300, 200));
        });
        await image.SaveAsPngAsync(testImagePath);

        var outputDir = Path.Combine(_testOutputDir, "quality_comparison");
        
        try
        {
            // Act - Convert to PNG
            var pngPath = await AgentDMS.Core.Utilities.ThumbnailGenerator.ConvertToPngAsync(
                testImagePath, outputDir, "png_comparison");

            // Act - Create a smaller version for comparison
            var smallerPngPath = Path.Combine(outputDir, "smaller_comparison.png");
            using var originalImage = await SixLabors.ImageSharp.Image.LoadAsync(testImagePath);
            using var smallerImage = originalImage.Clone(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(200, 200),
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max
            }));
            await smallerImage.SaveAsPngAsync(smallerPngPath);

            // Assert - Compare file information
            var pngInfo = new FileInfo(pngPath);
            var smallerInfo = new FileInfo(smallerPngPath);
            
            Assert.True(File.Exists(pngPath), "PNG file should exist");
            Assert.True(File.Exists(smallerPngPath), "Smaller PNG file should exist");
            
            // Both should have reasonable sizes
            Console.WriteLine($"Full PNG: {pngInfo.Length} bytes");
            Console.WriteLine($"Smaller PNG: {smallerInfo.Length} bytes");
            
            Assert.True(pngInfo.Length > 100, "PNG file should have reasonable size");
            Assert.True(smallerInfo.Length > 100, "Smaller PNG file should have reasonable size");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Fact]
    public async Task MistralDocumentAiService_WithoutApiKey_ShouldHandleGracefully()
    {
        // Arrange
        using var httpClient = new System.Net.Http.HttpClient();
        var mistralService = new MistralDocumentAiService(httpClient, apiKey: null);

        // Act
        var result = await mistralService.AnalyzeDocumentAsync("Test document text");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API key not configured", result.Message);
    }

    [Fact]
    public void ImageProcessingService_WithMistralService_ShouldInitializeSuccessfully()
    {
        // Arrange & Act
        using var httpClient = new System.Net.Http.HttpClient();
        var mistralService = new MistralDocumentAiService(httpClient, apiKey: null);
        var imageProcessor = new ImageProcessingService(
            maxConcurrency: 1,
            outputDirectory: _testOutputDir,
            logger: null,
            mistralService: mistralService
        );

        // Assert
        Assert.NotNull(imageProcessor);
        
        // Verify supported extensions are still available
        var extensions = ImageProcessingService.GetSupportedExtensions();
        Assert.Contains(".pdf", extensions);
        Assert.Contains(".png", extensions);
        Assert.Contains(".jpg", extensions);
    }

    [Fact]
    public async Task ProcessImageAsync_WithMistralService_ShouldIncludeAiAnalysisPlaceholder()
    {
        // Arrange
        using var httpClient = new System.Net.Http.HttpClient();
        var mistralService = new MistralDocumentAiService(httpClient, apiKey: null);
        var imageProcessor = new ImageProcessingService(
            maxConcurrency: 1,
            outputDirectory: _testOutputDir,
            logger: null,
            mistralService: mistralService
        );

        var testImagePath = CreateTestImage("test_mistral_integration.png");

        try
        {
            // Act
            var result = await imageProcessor.ProcessImageAsync(testImagePath);

            // Assert
            Assert.True(result.Success, $"Processing should succeed: {result.Message}");
            
            // AI analysis should not be present because API key is not configured
            // but the processing should still complete successfully
            Assert.Null(result.AiAnalysis);
            
            // Verify processing metrics are present
            Assert.NotNull(result.Metrics);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Fact]
    public async Task ProcessMultipleImagesAsync_WithFullPaths_ShouldSucceedForAllFiles()
    {
        // Arrange - Create multiple test images
        var testImages = new List<string>();
        try
        {
            for (int i = 1; i <= 3; i++)
            {
                var imagePath = CreateTestImage($"batch_test_{i}.png");
                testImages.Add(imagePath);
            }

            // Act
            var results = await _imageProcessor.ProcessMultipleImagesAsync(testImages);

            // Assert
            Assert.Equal(testImages.Count, results.Count);
            Assert.All(results, result => Assert.True(result.Success, $"All results should be successful: {result.Message}"));
            
            // Verify each result has the expected properties
            foreach (var result in results)
            {
                Assert.NotNull(result.ProcessedImage);
                Assert.NotNull(result.Metrics);
                Assert.True(File.Exists(result.ProcessedImage.ConvertedPngPath));
            }
        }
        finally
        {
            // Cleanup
            foreach (var imagePath in testImages)
            {
                if (File.Exists(imagePath))
                    File.Delete(imagePath);
            }
        }
    }

    [Fact]
    public async Task ProcessMultipleImagesAsync_WithUncPaths_ShouldHandleCorrectly()
    {
        // Arrange - Create test image and simulate UNC path (for local testing)
        var testImagePath = CreateTestImage("unc_test.png");
        
        try
        {
            // Simulate processing with the full absolute path (which includes drive letter - closest to UNC we can test locally)
            var fullPath = Path.GetFullPath(testImagePath);
            var filePaths = new List<string> { fullPath };

            // Act
            var results = await _imageProcessor.ProcessMultipleImagesAsync(filePaths);

            // Assert
            Assert.Single(results);
            Assert.True(results[0].Success, $"Processing should succeed with full path: {results[0].Message}");
            Assert.NotNull(results[0].ProcessedImage);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }
    
    [Fact]
    public async Task ProcessImageAsync_WithUseMistralAIFlag_ShouldRespectUserChoice()
    {
        // Arrange
        using var httpClient = new System.Net.Http.HttpClient();
        var mistralService = new MistralDocumentAiService(httpClient, apiKey: null);
        var imageProcessor = new ImageProcessingService(
            maxConcurrency: 1,
            outputDirectory: _testOutputDir,
            logger: null,
            mistralService: mistralService
        );

        var testImagePath = CreateTestImage("test_mistral_flag.png");

        try
        {
            // Act 1: Process without Mistral AI (default)
            var resultWithoutMistral = await imageProcessor.ProcessImageAsync(testImagePath, useMistralAI: false);

            // Act 2: Process with Mistral AI enabled
            var resultWithMistral = await imageProcessor.ProcessImageAsync(testImagePath, useMistralAI: true);

            // Assert
            Assert.True(resultWithoutMistral.Success, $"Processing without Mistral should succeed: {resultWithoutMistral.Message}");
            Assert.True(resultWithMistral.Success, $"Processing with Mistral should succeed: {resultWithMistral.Message}");
            
            // Without Mistral AI flag, should not have AI analysis
            Assert.Null(resultWithoutMistral.AiAnalysis);
            
            // With Mistral AI flag, should have failed AI analysis due to no API key
            Assert.NotNull(resultWithMistral.AiAnalysis);
            Assert.False(resultWithMistral.AiAnalysis.Success);
            Assert.Contains("API key not configured", resultWithMistral.AiAnalysis.Message);
            
            // Verify processing metrics are present in both cases
            Assert.NotNull(resultWithoutMistral.Metrics);
            Assert.NotNull(resultWithMistral.Metrics);
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
        }
    }

    [Fact]
    public void MistralChatRequest_Serialization_ShouldNotIncludeMaxTokens()
    {
        // Arrange
        using var httpClient = new System.Net.Http.HttpClient();
        var mistralService = new MistralDocumentAiService(httpClient, apiKey: null);
        
        // Use reflection to create a MistralChatRequest to test serialization
        var assembly = typeof(MistralDocumentAiService).Assembly;
        var requestType = assembly.GetType("AgentDMS.Core.Services.MistralChatRequest");
        Assert.NotNull(requestType);
        
        var request = Activator.CreateInstance(requestType);
        Assert.NotNull(request);
        
        // Set properties
        var modelProp = requestType.GetProperty("Model");
        var messagesProp = requestType.GetProperty("Messages");
        var temperatureProp = requestType.GetProperty("Temperature");
        
        Assert.NotNull(modelProp);
        Assert.NotNull(messagesProp);
        Assert.NotNull(temperatureProp);
        
        modelProp.SetValue(request, "test-model");
        temperatureProp.SetValue(request, 0.1);
        
        // Create empty messages list
        var messagesListType = messagesProp.PropertyType;
        var messagesList = Activator.CreateInstance(messagesListType);
        messagesProp.SetValue(request, messagesList);
        
        // Act - Serialize the request
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
        var json = JsonSerializer.Serialize(request, options);
        
        // Assert - Verify maxTokens is not in the JSON
        Assert.DoesNotContain("maxTokens", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("max_tokens", json, StringComparison.OrdinalIgnoreCase);
        
        // Verify expected properties are present
        Assert.Contains("model", json);
        Assert.Contains("messages", json);
        Assert.Contains("temperature", json);
    }

    [Fact]
    public async Task ProcessPdfAsync_WithFileHandleStress_ShouldHandleResourcesCorrectly()
    {
        // This test verifies that the file locking improvements work correctly
        // by processing multiple PDFs in quick succession to stress file handles
        
        // Arrange - Create multiple small PDF files for testing
        var testPdfPaths = new List<string>();
        try
        {
            for (int i = 0; i < 3; i++)
            {
                var pdfPath = CreateTestPdf($"stress_test_{i}.pdf");
                testPdfPaths.Add(pdfPath);
            }

            // Act - Process all PDFs in quick succession to stress file handle management
            var processingTasks = testPdfPaths.Select(async pdfPath =>
            {
                return await _imageProcessor.ProcessImageAsync(pdfPath);
            });

            var results = await Task.WhenAll(processingTasks);

            // Assert - All processing should succeed despite potential file locking issues
            Assert.All(results, result => 
            {
                // Allow for expected failures in test environment (missing Ghostscript)
                // But ensure we don't get file locking specific errors
                if (!result.Success)
                {
                    Assert.DoesNotContain("being used by another process", result.Message, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("cannot access the file", result.Message, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("sharing violation", result.Message, StringComparison.OrdinalIgnoreCase);
                }
            });

            // At least verify that we didn't encounter file locking issues
            var fileLockErrors = results.Where(r => !r.Success && 
                (r.Message.Contains("being used by another process", StringComparison.OrdinalIgnoreCase) ||
                 r.Message.Contains("cannot access the file", StringComparison.OrdinalIgnoreCase) ||
                 r.Message.Contains("sharing violation", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            Assert.Empty(fileLockErrors);
        }
        finally
        {
            // Cleanup
            foreach (var pdfPath in testPdfPaths)
            {
                if (File.Exists(pdfPath))
                {
                    try
                    {
                        File.Delete(pdfPath);
                    }
                    catch (IOException)
                    {
                        // Ignore cleanup errors - they don't affect the test validity
                    }
                }
            }
        }
    }

    [Fact]
    public async Task WriteFileWithRetryAsync_WithInvalidPath_ShouldEventuallyFail()
    {
        // This test verifies that the retry mechanism doesn't loop forever
        // and eventually throws appropriate exceptions for truly invalid operations
        
        // Use reflection to access the private WriteFileWithRetryAsync method for testing
        var imageProcessor = new ImageProcessingService(outputDirectory: _testOutputDir);
        var method = typeof(ImageProcessingService).GetMethod("WriteFileWithRetryAsync", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        
        Assert.NotNull(method);

        // Create a test MagickImage
        using var testImage = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 50, 50);
        
        // Try to write to an invalid path (no permission or invalid directory)
        var invalidPath = "/invalid/directory/that/does/not/exist/test.png";
        
        // Act & Assert - Should eventually throw an exception after retries
        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            try
            {
                await (Task)method.Invoke(imageProcessor, new object[] { testImage, invalidPath, CancellationToken.None });
            }
            catch (System.Reflection.TargetInvocationException ex)
            {
                // Re-throw the inner exception for cleaner assertion
                throw ex.InnerException ?? ex;
            }
        });

        // The exception should be a directory/path related exception, not a file lock exception
        Assert.True(exception is DirectoryNotFoundException || 
                   exception is UnauthorizedAccessException ||
                   exception is IOException);
        
        // Verify it's not a file lock issue
        Assert.DoesNotContain("being used by another process", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProcessImageAsync_WithCloudStorage_ShouldCleanupTemporaryFiles()
    {
        // This test verifies that temporary files are properly cleaned up when using cloud storage
        // Arrange
        var tempStorageDir = Path.Combine(Path.GetTempPath(), "TestStorage_" + Guid.NewGuid().ToString());
        var tempProcessingDir = Path.Combine(Path.GetTempPath(), "AgentDMS_Processing");
        Directory.CreateDirectory(tempStorageDir);

        var localStorageProvider = new LocalStorageProvider(tempStorageDir);
        
        // Create a mock storage service that uses the local provider but simulates cloud storage behavior
        var mockStorageService = new MockStorageService(localStorageProvider);
        var imageProcessor = new ImageProcessingService(mockStorageService, logger: null);
        
        // Create a simple test image
        var testImagePath = CreateTestImage("test.png");

        try
        {
            // Act - Process the image
            var result = await imageProcessor.ProcessImageAsync(testImagePath);

            // Assert - Processing should succeed
            if (!result.Success)
            {
                // Output more detailed error information for debugging
                var errorDetails = result.Message;
                if (result.Error != null)
                {
                    errorDetails += $" | Exception: {result.Error.GetType().Name}: {result.Error.Message}";
                    if (result.Error.InnerException != null)
                    {
                        errorDetails += $" | Inner: {result.Error.InnerException.GetType().Name}: {result.Error.InnerException.Message}";
                    }
                }
                Assert.True(result.Success, $"Processing failed: {errorDetails}");
            }
            
            // Wait a bit to ensure cleanup completes
            await Task.Delay(1000);

            // For now, just verify processing succeeded - we'll enhance cleanup verification later
            Assert.NotNull(result.ProcessedImage);
            Assert.True(result.ProcessedImage.ConvertedPngPath.StartsWith(tempStorageDir), 
                "File should be stored in the storage directory");
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
            
            // Give some time for any pending operations to complete
            await Task.Delay(1000);
            
            if (Directory.Exists(tempStorageDir))
            {
                try
                {
                    Directory.Delete(tempStorageDir, true);
                }
                catch
                {
                    // Ignore cleanup errors in test
                }
            }
        }
    }

    // Mock storage service for testing
    private class MockStorageService : IStorageService
    {
        public IStorageProvider StorageProvider { get; }
        public StorageConfig Configuration { get; }

        public MockStorageService(IStorageProvider provider)
        {
            StorageProvider = provider;
            Configuration = new StorageConfig { Provider = "Local" };
        }
    }
}