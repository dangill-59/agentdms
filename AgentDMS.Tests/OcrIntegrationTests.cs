using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing.Processing;
using Xunit;

namespace AgentDMS.Tests;

/// <summary>
/// Integration test to verify OCR works with full processing pipeline
/// </summary>
public class OcrIntegrationTests : IDisposable
{
    private readonly string _tempDirectory;

    public OcrIntegrationTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ProcessImageAsync_WithWhiteImage_ShouldUseOcrNotPlaceholder()
    {
        // Arrange
        var testImagePath = Path.Combine(_tempDirectory, "test_white.png");
        await CreateSimpleWhiteImage(testImagePath);

        var service = new ImageProcessingService(maxConcurrency: 1, outputDirectory: _tempDirectory, logger: null, mistralService: null);

        // Act - Process the image which should trigger OCR
        var result = await service.ProcessImageAsync(testImagePath, null, CancellationToken.None, useMistralAI: false);

        // Assert
        Assert.True(result.Success, $"Processing failed: {result.Message}");
        Assert.NotNull(result.ProcessedImage);
        Assert.True(File.Exists(result.ProcessedImage.ConvertedPngPath));
        
        // Since OCR is now implemented, the result should not contain placeholder text
        // The test verifies that OCR is being called (even if the white image produces no readable text)
        Assert.DoesNotContain("[OCR_PLACEHOLDER]", result.Message ?? "");
        
        // NEW: Test that ExtractedText field is populated (even if empty for white image)
        Assert.NotNull(result.ExtractedText);
    }

    [Fact]
    public async Task ProcessImageAsync_WithTextImage_ShouldExtractText()
    {
        // Arrange
        var testImagePath = Path.Combine(_tempDirectory, "test_text.png");
        await CreateTestImageWithText(testImagePath, "Hello OCR Test");

        var service = new ImageProcessingService(maxConcurrency: 1, outputDirectory: _tempDirectory, logger: null, mistralService: null);

        // Act - Process the image which should trigger OCR
        var result = await service.ProcessImageAsync(testImagePath, null, CancellationToken.None, useMistralAI: false);

        // Assert
        Assert.True(result.Success, $"Processing failed: {result.Message}");
        Assert.NotNull(result.ExtractedText);
        Assert.NotEmpty(result.ExtractedText);
        
        // The extracted text should contain something meaningful (OCR might not be perfect)
        Assert.True(result.ExtractedText.Length > 0, "Should extract some text from image");
    }

    private async Task CreateSimpleWhiteImage(string imagePath)
    {
        // Create a simple 400x200 white image
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(400, 200);
        image.Mutate(ctx => ctx.Fill(Color.White));
        await image.SaveAsPngAsync(imagePath);
    }

    private async Task CreateTestImageWithText(string imagePath, string text)
    {
        // Create a 400x200 white image with text for OCR testing
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(400, 200);
        image.Mutate(ctx => ctx
            .Fill(Color.White)
            .DrawText(text, SixLabors.Fonts.SystemFonts.CreateFont("DejaVu Sans", 24), Color.Black, new SixLabors.ImageSharp.PointF(50, 80)));
        await image.SaveAsPngAsync(imagePath);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}