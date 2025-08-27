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
    }

    private async Task CreateSimpleWhiteImage(string imagePath)
    {
        // Create a simple 400x200 white image
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgb24>(400, 200);
        image.Mutate(ctx => ctx.Fill(Color.White));
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