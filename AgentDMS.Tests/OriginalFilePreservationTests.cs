using Xunit;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace AgentDMS.Tests;

/// <summary>
/// Tests to verify that original files are preserved completely untouched 
/// while PNG copies are created for web-friendly viewing
/// </summary>
public class OriginalFilePreservationTests
{
    private readonly string _testOutputDir = Path.Combine(Path.GetTempPath(), "AgentDMS_FilePreservation_Tests");

    public OriginalFilePreservationTests()
    {
        Directory.CreateDirectory(_testOutputDir);
    }

    [Fact]
    public async Task ConvertToPngAsync_WithPngInput_ShouldPreserveOriginalAndCreateWebCopy()
    {
        // Arrange
        var originalFilePath = CreateTestPngImage("original.png");
        var outputDirectory = Path.Combine(_testOutputDir, "output");
        
        try
        {
            // Verify original file exists before processing
            Assert.True(File.Exists(originalFilePath));
            var originalContent = await File.ReadAllBytesAsync(originalFilePath);
            var originalTimestamp = File.GetLastWriteTime(originalFilePath);
            
            // Act
            var webCopyPath = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            
            // Assert
            // Original file should still exist and be completely unchanged
            Assert.True(File.Exists(originalFilePath));
            var originalContentAfter = await File.ReadAllBytesAsync(originalFilePath);
            var originalTimestampAfter = File.GetLastWriteTime(originalFilePath);
            
            Assert.Equal(originalContent, originalContentAfter);
            Assert.Equal(originalTimestamp, originalTimestampAfter);
            
            // Web copy should exist in output directory
            Assert.True(File.Exists(webCopyPath));
            Assert.NotEqual(originalFilePath, webCopyPath);
            Assert.Contains(outputDirectory, webCopyPath);
            
            // Both files should exist and be accessible
            Assert.True(File.Exists(originalFilePath));
            Assert.True(File.Exists(webCopyPath));
        }
        finally
        {
            // Cleanup
            CleanupFiles(originalFilePath, outputDirectory);
        }
    }

    [Fact]
    public async Task ConvertToPngAsync_WithConflictingNames_ShouldCreateUniqueWebCopy()
    {
        // Arrange
        var originalFilePath = CreateTestPngImage("test.png");
        var outputDirectory = Path.Combine(_testOutputDir, "output");
        Directory.CreateDirectory(outputDirectory);
        
        // Create a file that would conflict with the expected output name
        var conflictingFilePath = Path.Combine(outputDirectory, "test.png");
        await File.WriteAllTextAsync(conflictingFilePath, "existing web file content");
        
        try
        {
            // Act
            var webCopyPath = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            
            // Assert
            // Original file should still exist unchanged
            Assert.True(File.Exists(originalFilePath));
            
            // Conflicting file should still exist unchanged
            Assert.True(File.Exists(conflictingFilePath));
            Assert.Equal("existing web file content", await File.ReadAllTextAsync(conflictingFilePath));
            
            // Web copy should have a unique name to avoid conflicts
            Assert.True(File.Exists(webCopyPath));
            Assert.NotEqual(originalFilePath, webCopyPath);
            Assert.NotEqual(conflictingFilePath, webCopyPath);
            Assert.Contains("_web_", webCopyPath);
            
            // All three files should exist independently
            Assert.True(File.Exists(originalFilePath));
            Assert.True(File.Exists(conflictingFilePath));
            Assert.True(File.Exists(webCopyPath));
        }
        finally
        {
            // Cleanup
            CleanupFiles(originalFilePath, outputDirectory);
        }
    }

    [Fact]
    public async Task ConvertToPngAsync_WithJpgInput_ShouldPreserveOriginalAndCreatePngWebCopy()
    {
        // Arrange
        var originalFilePath = CreateTestJpgImage("original.jpg");
        var outputDirectory = Path.Combine(_testOutputDir, "output");
        
        try
        {
            // Verify original file exists before processing
            Assert.True(File.Exists(originalFilePath));
            var originalContent = await File.ReadAllBytesAsync(originalFilePath);
            var originalTimestamp = File.GetLastWriteTime(originalFilePath);
            
            // Act
            var webCopyPath = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            
            // Assert
            // Original JPG file should still exist and be completely unchanged
            Assert.True(File.Exists(originalFilePath));
            var originalContentAfter = await File.ReadAllBytesAsync(originalFilePath);
            var originalTimestampAfter = File.GetLastWriteTime(originalFilePath);
            
            Assert.Equal(originalContent, originalContentAfter);
            Assert.Equal(originalTimestamp, originalTimestampAfter);
            Assert.EndsWith(".jpg", originalFilePath);
            
            // Web copy should be a PNG in output directory
            Assert.True(File.Exists(webCopyPath));
            Assert.NotEqual(originalFilePath, webCopyPath);
            Assert.EndsWith(".png", webCopyPath);
            Assert.Contains(outputDirectory, webCopyPath);
            
            // Both files should exist independently
            Assert.True(File.Exists(originalFilePath));
            Assert.True(File.Exists(webCopyPath));
        }
        finally
        {
            // Cleanup
            CleanupFiles(originalFilePath, outputDirectory);
        }
    }

    [Fact]
    public async Task ConvertToPngAsync_MultipleConversions_ShouldCreateUniqueWebCopies()
    {
        // Arrange
        var originalFilePath = CreateTestPngImage("document.png");
        var outputDirectory = Path.Combine(_testOutputDir, "output");
        
        try
        {
            // Act - convert the same file multiple times
            var webCopy1 = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            var webCopy2 = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            var webCopy3 = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            
            // Assert
            // Original should still exist
            Assert.True(File.Exists(originalFilePath));
            
            // All web copies should exist with unique names
            Assert.True(File.Exists(webCopy1));
            Assert.True(File.Exists(webCopy2));
            Assert.True(File.Exists(webCopy3));
            
            // All paths should be different
            Assert.NotEqual(webCopy1, webCopy2);
            Assert.NotEqual(webCopy2, webCopy3);
            Assert.NotEqual(webCopy1, webCopy3);
            
            // All should be in output directory
            Assert.Contains(outputDirectory, webCopy1);
            Assert.Contains(outputDirectory, webCopy2);
            Assert.Contains(outputDirectory, webCopy3);
            
            // Second and third copies should have unique suffixes
            Assert.Contains("_web_", webCopy2);
            Assert.Contains("_web_", webCopy3);
        }
        finally
        {
            // Cleanup
            CleanupFiles(originalFilePath, outputDirectory);
        }
    }

    [Fact]
    public async Task ConvertToPngAsync_OriginalInOutputDirectory_ShouldStillCreateSeparateWebCopy()
    {
        // Arrange - original file is already in the output directory
        var outputDirectory = Path.Combine(_testOutputDir, "output");
        Directory.CreateDirectory(outputDirectory);
        var originalFilePath = CreateTestPngImageInDirectory("source.png", outputDirectory);
        
        try
        {
            var originalContent = await File.ReadAllBytesAsync(originalFilePath);
            
            // Act
            var webCopyPath = await ThumbnailGenerator.ConvertToPngAsync(
                originalFilePath, outputDirectory);
            
            // Assert
            // Original should still exist and be unchanged
            Assert.True(File.Exists(originalFilePath));
            var originalContentAfter = await File.ReadAllBytesAsync(originalFilePath);
            Assert.Equal(originalContent, originalContentAfter);
            
            // Web copy should be created with a different name
            Assert.True(File.Exists(webCopyPath));
            Assert.NotEqual(originalFilePath, webCopyPath);
            
            // Both files should exist in the same directory
            Assert.Equal(outputDirectory, Path.GetDirectoryName(originalFilePath));
            Assert.Equal(outputDirectory, Path.GetDirectoryName(webCopyPath));
        }
        finally
        {
            // Cleanup
            CleanupFiles(originalFilePath, outputDirectory);
        }
    }

    private string CreateTestPngImage(string fileName)
    {
        return CreateTestPngImageInDirectory(fileName, _testOutputDir);
    }

    private string CreateTestPngImageInDirectory(string fileName, string directory)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        
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