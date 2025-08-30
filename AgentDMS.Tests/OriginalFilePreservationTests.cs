using Xunit;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AgentDMS.Tests;

public class OriginalFilePreservationTests
{
    private readonly string _testOutputDir = Path.Combine(Path.GetTempPath(), "AgentDMS_FilePreservation_Tests");

    public OriginalFilePreservationTests()
    {
        Directory.CreateDirectory(_testOutputDir);
    }

    [Fact]
    public async Task ConvertToPngAsync_WithPngInput_ShouldPreserveOriginalFile()
    {
        // Arrange
        var originalFilePath = CreateTestPngImage("original.png");
        var outputDirectory = Path.Combine(_testOutputDir, "output");
        
        try
        {
            // Verify original file exists before processing
            Assert.True(File.Exists(originalFilePath));
            var originalContent = await File.ReadAllBytesAsync(originalFilePath);
            
            // Act
            var processedFilePath = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            
            // Assert
            // Original file should still exist and be unchanged
            Assert.True(File.Exists(originalFilePath));
            var originalContentAfter = await File.ReadAllBytesAsync(originalFilePath);
            Assert.Equal(originalContent, originalContentAfter);
            
            // Processed file should exist in output directory
            Assert.True(File.Exists(processedFilePath));
            Assert.NotEqual(originalFilePath, processedFilePath);
            
            // Both files should exist
            Assert.True(File.Exists(originalFilePath));
            Assert.True(File.Exists(processedFilePath));
        }
        finally
        {
            // Cleanup
            CleanupFiles(originalFilePath, outputDirectory);
        }
    }

    [Fact]
    public async Task ConvertToPngAsync_WithConflictingNames_ShouldCreateUniqueFilename()
    {
        // Arrange
        var originalFilePath = CreateTestPngImage("test.png");
        var outputDirectory = Path.Combine(_testOutputDir, "output");
        Directory.CreateDirectory(outputDirectory);
        
        // Create a file that would conflict with the expected output name
        var conflictingFilePath = Path.Combine(outputDirectory, "test.png");
        await File.WriteAllTextAsync(conflictingFilePath, "existing file content");
        
        try
        {
            // Act
            var processedFilePath = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            
            // Assert
            // Original file should still exist
            Assert.True(File.Exists(originalFilePath));
            
            // Conflicting file should still exist unchanged
            Assert.True(File.Exists(conflictingFilePath));
            Assert.Equal("existing file content", await File.ReadAllTextAsync(conflictingFilePath));
            
            // Processed file should have a unique name
            Assert.True(File.Exists(processedFilePath));
            Assert.NotEqual(originalFilePath, processedFilePath);
            Assert.NotEqual(conflictingFilePath, processedFilePath);
            Assert.Contains("_processed_", processedFilePath);
            
            // All three files should exist
            Assert.True(File.Exists(originalFilePath));
            Assert.True(File.Exists(conflictingFilePath));
            Assert.True(File.Exists(processedFilePath));
        }
        finally
        {
            // Cleanup
            CleanupFiles(originalFilePath, outputDirectory);
        }
    }

    [Fact]
    public async Task ConvertToPngAsync_WithJpgInput_ShouldPreserveOriginalFile()
    {
        // Arrange
        var originalFilePath = CreateTestJpgImage("original.jpg");
        var outputDirectory = Path.Combine(_testOutputDir, "output");
        
        try
        {
            // Verify original file exists before processing
            Assert.True(File.Exists(originalFilePath));
            var originalContent = await File.ReadAllBytesAsync(originalFilePath);
            
            // Act
            var processedFilePath = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            
            // Assert
            // Original JPG file should still exist and be unchanged
            Assert.True(File.Exists(originalFilePath));
            var originalContentAfter = await File.ReadAllBytesAsync(originalFilePath);
            Assert.Equal(originalContent, originalContentAfter);
            
            // Processed PNG file should exist in output directory
            Assert.True(File.Exists(processedFilePath));
            Assert.NotEqual(originalFilePath, processedFilePath);
            Assert.EndsWith(".png", processedFilePath);
            
            // Both files should exist
            Assert.True(File.Exists(originalFilePath));
            Assert.True(File.Exists(processedFilePath));
        }
        finally
        {
            // Cleanup
            CleanupFiles(originalFilePath, outputDirectory);
        }
    }

    private string CreateTestPngImage(string fileName)
    {
        var path = Path.Combine(_testOutputDir, fileName);
        
        using var image = new Image<Rgba32>(100, 100);
        image.Mutate(x => x.BackgroundColor(Color.Red));
        image.SaveAsPng(path);
        
        return path;
    }

    private string CreateTestJpgImage(string fileName)
    {
        var path = Path.Combine(_testOutputDir, fileName);
        
        using var image = new Image<Rgba32>(100, 100);
        image.Mutate(x => x.BackgroundColor(Color.Blue));
        image.SaveAsJpeg(path);
        
        return path;
    }

    private void CleanupFiles(string originalFile, string outputDirectory)
    {
        try
        {
            if (File.Exists(originalFile))
                File.Delete(originalFile);
                
            if (Directory.Exists(outputDirectory))
                Directory.Delete(outputDirectory, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}