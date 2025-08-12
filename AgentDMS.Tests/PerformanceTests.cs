using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using AgentDMS.Core.Services;
using AgentDMS.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AgentDMS.Tests;

public class PerformanceTests
{
    [Fact]
    public void CliOptions_ParsesConcurrencyFromArgs()
    {
        // Arrange
        var args = new[] { "--max-concurrency", "8" };

        // Act
        var options = CliOptions.Parse(args);

        // Assert
        Assert.Equal(8, options.MaxConcurrency);
    }

    [Fact]
    public void CliOptions_ParsesFileSizeFromArgs()
    {
        // Arrange
        var args = new[] { "--max-file-size", "50" };

        // Act
        var options = CliOptions.Parse(args);

        // Assert
        Assert.Equal(50, options.MaxFileSizeMB);
    }

    [Fact]
    public void CliOptions_ParsesEnvironmentVariables()
    {
        // Arrange - Set environment variables
        Environment.SetEnvironmentVariable("AGENTDMS_MAX_CONCURRENCY", "16");
        Environment.SetEnvironmentVariable("AGENTDMS_MAX_FILE_SIZE_MB", "200");

        try
        {
            // Act
            var options = CliOptions.Parse(new string[0]);

            // Assert
            Assert.Equal(16, options.MaxConcurrency);
            Assert.Equal(200, options.MaxFileSizeMB);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("AGENTDMS_MAX_CONCURRENCY", null);
            Environment.SetEnvironmentVariable("AGENTDMS_MAX_FILE_SIZE_MB", null);
        }
    }

    [Fact]
    public async Task ImageProcessingService_ProcessMultipleImagesAsync_UsesBatchingCorrectly()
    {
        // Arrange
        var testOutputDir = Path.Combine(Path.GetTempPath(), "AgentDMS_BatchTest");
        Directory.CreateDirectory(testOutputDir);
        
        var imageProcessor = new ImageProcessingService(maxConcurrency: 2, outputDirectory: testOutputDir);
        
        // Create multiple test images
        var testFiles = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            var testImagePath = Path.Combine(testOutputDir, $"test_{i}.png");
            await CreateTestImage(testImagePath);
            testFiles.Add(testImagePath);
        }

        try
        {
            // Act
            var results = await imageProcessor.ProcessMultipleImagesAsync(testFiles);

            // Assert
            Assert.Equal(5, results.Count);
            Assert.All(results, result => Assert.True(result.Success));
            
            // Verify all images were processed
            foreach (var result in results)
            {
                Assert.NotNull(result.ProcessedImage);
                Assert.True(File.Exists(result.ProcessedImage.ConvertedPngPath));
                Assert.True(File.Exists(result.ProcessedImage.ThumbnailPath));
            }
        }
        finally
        {
            // Cleanup
            foreach (var file in testFiles)
            {
                if (File.Exists(file)) File.Delete(file);
            }
            if (Directory.Exists(testOutputDir))
            {
                Directory.Delete(testOutputDir, true);
            }
            imageProcessor.Dispose();
        }
    }

    private static async Task CreateTestImage(string path)
    {
        using var image = new Image<Rgba32>(100, 100);
        image.Mutate(x => x.BackgroundColor(Color.Blue));
        await image.SaveAsPngAsync(path);
    }
}