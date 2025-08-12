using Xunit;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Services;
using AgentDMS.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
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
}