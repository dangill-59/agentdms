// Simple test for timing functionality
using Xunit;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Services;
using AgentDMS.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace AgentDMS.Tests;

public class TimingTests
{
    private readonly string _testOutputDir = Path.Combine(Path.GetTempPath(), "AgentDMS_TimingTests");
    private readonly ImageProcessingService _imageProcessor;

    public TimingTests()
    {
        _imageProcessor = new ImageProcessingService(outputDirectory: _testOutputDir);
        Directory.CreateDirectory(_testOutputDir);
    }

    [Fact]
    public async Task ProcessImageAsync_WithTimingMetrics_ShouldRecordDetailedTiming()
    {
        // Arrange
        var testImagePath = CreateTestImage("timing_test.png");
        
        try
        {
            // Act
            var result = await _imageProcessor.ProcessImageAsync(testImagePath);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.ProcessedImage);
            Assert.NotNull(result.Metrics);
            
            // Verify timing metrics are recorded
            Assert.NotNull(result.Metrics.FileLoadTime);
            Assert.NotNull(result.Metrics.ImageDecodeTime);
            Assert.NotNull(result.Metrics.ConversionTime);
            Assert.NotNull(result.Metrics.ThumbnailGenerationTime);
            Assert.True(result.Metrics.TotalProcessingTime > TimeSpan.Zero);
            
            // Verify individual timing components
            Assert.True(result.Metrics.FileLoadTime > TimeSpan.Zero);
            Assert.True(result.Metrics.ImageDecodeTime > TimeSpan.Zero);
            Assert.True(result.Metrics.ConversionTime > TimeSpan.Zero);
            Assert.True(result.Metrics.ThumbnailGenerationTime > TimeSpan.Zero);
            
            // Verify files were created
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
    public async Task ProcessImageAsync_WithLargerImage_ShouldShowMeasurableTiming()
    {
        // Arrange - Create a larger image to get more measurable timing
        var testImagePath = CreateLargerTestImage("large_timing_test.png", 800, 600);
        
        try
        {
            // Act
            var result = await _imageProcessor.ProcessImageAsync(testImagePath);

            // Assert
            Assert.True(result.Success);
            Assert.NotNull(result.Metrics);
            
            // For a larger image, we should see more noticeable processing times
            Assert.True(result.Metrics.TotalProcessingTime > TimeSpan.FromMilliseconds(10));
        }
        finally
        {
            // Cleanup
            if (File.Exists(testImagePath))
                File.Delete(testImagePath);
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
    
    private string CreateLargerTestImage(string fileName, int width, int height)
    {
        var path = Path.Combine(_testOutputDir, fileName);
        
        // Create a larger image 
        using var image = new Image<SixLabors.ImageSharp.PixelFormats.Rgba32>(width, height);
        image.Mutate(x => {
            x.BackgroundColor(SixLabors.ImageSharp.Color.Blue);
            // Add some gradient for more processing complexity
            x.GaussianBlur(2.0f);
        });
        image.SaveAsPng(path);
        
        return path;
    }
}