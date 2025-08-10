using System;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// Service for handling file uploads and validation
/// </summary>
public class FileUploadService
{
    private readonly string _uploadDirectory;
    private readonly long _maxFileSize;
    
    public FileUploadService(string? uploadDirectory = null, long maxFileSize = 50 * 1024 * 1024) // 50MB default
    {
        _uploadDirectory = uploadDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Uploads");
        _maxFileSize = maxFileSize;
        
        // Ensure upload directory exists
        Directory.CreateDirectory(_uploadDirectory);
    }

    /// <summary>
    /// Upload and validate a file
    /// </summary>
    public async Task<ProcessingResult> UploadFileAsync(string sourceFilePath, string? destinationFileName = null)
    {
        try
        {
            if (!File.Exists(sourceFilePath))
            {
                return ProcessingResult.Failed($"Source file not found: {sourceFilePath}");
            }

            var fileInfo = new FileInfo(sourceFilePath);
            
            // Validate file size
            if (fileInfo.Length > _maxFileSize)
            {
                return ProcessingResult.Failed($"File size ({fileInfo.Length:N0} bytes) exceeds maximum allowed size ({_maxFileSize:N0} bytes)");
            }

            // Validate file extension
            var extension = Path.GetExtension(sourceFilePath).ToLowerInvariant();
            if (!IsValidImageFile(extension))
            {
                return ProcessingResult.Failed($"Unsupported file format: {extension}");
            }

            // Generate destination file name
            destinationFileName ??= $"{Guid.NewGuid()}_{fileInfo.Name}";
            var destinationPath = Path.Combine(_uploadDirectory, destinationFileName);

            // Copy file to upload directory
            await using var sourceStream = File.OpenRead(sourceFilePath);
            await using var destinationStream = File.Create(destinationPath);
            await sourceStream.CopyToAsync(destinationStream);

            var imageFile = new ImageFile
            {
                OriginalFilePath = destinationPath,
                FileName = destinationFileName,
                OriginalFormat = extension,
                FileSize = fileInfo.Length,
                CreatedDate = fileInfo.CreationTime
            };

            return ProcessingResult.Successful(imageFile, TimeSpan.Zero, "File uploaded successfully");
        }
        catch (Exception ex)
        {
            return ProcessingResult.Failed($"Error uploading file: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Upload multiple files
    /// </summary>
    public async Task<List<ProcessingResult>> UploadMultipleFilesAsync(IEnumerable<string> filePaths)
    {
        var tasks = filePaths.Select(async filePath => await UploadFileAsync(filePath));
        return (await Task.WhenAll(tasks)).ToList();
    }

    /// <summary>
    /// Get all files in the upload directory
    /// </summary>
    public string[] GetUploadedFiles()
    {
        if (!Directory.Exists(_uploadDirectory))
            return Array.Empty<string>();

        return Directory.GetFiles(_uploadDirectory)
            .Where(f => IsValidImageFile(Path.GetExtension(f).ToLowerInvariant()))
            .ToArray();
    }

    /// <summary>
    /// Check if a file extension is valid for image processing
    /// </summary>
    public static bool IsValidImageFile(string extension)
    {
        var supportedExtensions = ImageProcessingService.GetSupportedExtensions();
        return supportedExtensions.Contains(extension.ToLowerInvariant());
    }

    /// <summary>
    /// Clean up old uploaded files
    /// </summary>
    public void CleanupOldFiles(TimeSpan maxAge)
    {
        if (!Directory.Exists(_uploadDirectory))
            return;

        var cutoffDate = DateTime.Now - maxAge;
        var files = Directory.GetFiles(_uploadDirectory);

        foreach (var file in files)
        {
            var fileInfo = new FileInfo(file);
            if (fileInfo.CreationTime < cutoffDate)
            {
                try
                {
                    File.Delete(file);
                }
                catch (Exception ex)
                {
                    // Log error but continue cleanup
                    Console.WriteLine($"Failed to delete old file {file}: {ex.Message}");
                }
            }
        }
    }

    public string UploadDirectory => _uploadDirectory;
}