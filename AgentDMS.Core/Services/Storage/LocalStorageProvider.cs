using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AgentDMS.Core.Services.Storage;

/// <summary>
/// Local file system storage provider implementation
/// </summary>
public class LocalStorageProvider : IStorageProvider
{
    private readonly string _baseDirectory;
    private readonly ILogger<LocalStorageProvider>? _logger;

    public StorageProviderType ProviderType => StorageProviderType.Local;

    /// <summary>
    /// Initialize local storage provider with specified base directory
    /// </summary>
    /// <param name="baseDirectory">Base directory for file storage. If null or empty, uses temp directory.</param>
    /// <param name="logger">Optional logger</param>
    public LocalStorageProvider(string? baseDirectory = null, ILogger<LocalStorageProvider>? logger = null)
    {
        _baseDirectory = string.IsNullOrWhiteSpace(baseDirectory) 
            ? Path.Combine(Path.GetTempPath(), "AgentDMS_Output")
            : baseDirectory;
        _logger = logger;
        
        // Ensure base directory exists
        Directory.CreateDirectory(_baseDirectory);
        
        _logger?.LogInformation("LocalStorageProvider initialized with base directory: {BaseDirectory}", _baseDirectory);
    }

    public async Task<string> SaveFileAsync(string sourcePath, string destinationPath)
    {
        if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath))
            throw new ArgumentException("Source file does not exist", nameof(sourcePath));

        var fullDestinationPath = GetFullPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
        
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        // Check if source and destination are the same file (normalized paths)
        var normalizedSource = Path.GetFullPath(sourcePath);
        var normalizedDestination = Path.GetFullPath(fullDestinationPath);
        
        if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
        {
            // Source and destination are the same - no copy needed
            _logger?.LogDebug("Source and destination are the same file, skipping copy: {FilePath}", normalizedSource);
            return GetFileUrl(destinationPath);
        }

        // Use retry mechanism for file copy operations to handle file locking
        await CopyFileWithRetryAsync(sourcePath, fullDestinationPath);
        
        _logger?.LogDebug("File copied from {SourcePath} to {DestinationPath}", sourcePath, fullDestinationPath);
        
        return GetFileUrl(destinationPath);
    }

    public async Task<string> SaveFileAsync(byte[] content, string destinationPath, string contentType = "application/octet-stream")
    {
        if (content == null || content.Length == 0)
            throw new ArgumentException("Content cannot be null or empty", nameof(content));

        var fullDestinationPath = GetFullPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
        
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        await File.WriteAllBytesAsync(fullDestinationPath, content);
        
        _logger?.LogDebug("File content saved to {DestinationPath}, size: {Size} bytes", fullDestinationPath, content.Length);
        
        return GetFileUrl(destinationPath);
    }

    public async Task<string> SaveFileAsync(Stream stream, string destinationPath, string contentType = "application/octet-stream")
    {
        if (stream == null)
            throw new ArgumentNullException(nameof(stream));

        var fullDestinationPath = GetFullPath(destinationPath);
        var destinationDirectory = Path.GetDirectoryName(fullDestinationPath);
        
        if (!string.IsNullOrEmpty(destinationDirectory))
        {
            Directory.CreateDirectory(destinationDirectory);
        }

        using (var fileStream = new FileStream(fullDestinationPath, FileMode.Create, FileAccess.Write))
        {
            await stream.CopyToAsync(fileStream);
        }
        
        _logger?.LogDebug("Stream content saved to {DestinationPath}", fullDestinationPath);
        
        return GetFileUrl(destinationPath);
    }

    public Task<bool> FileExistsAsync(string path)
    {
        var fullPath = GetFullPath(path);
        return Task.FromResult(File.Exists(fullPath));
    }

    public Task DeleteFileAsync(string path)
    {
        var fullPath = GetFullPath(path);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
            _logger?.LogDebug("File deleted: {Path}", fullPath);
        }
        
        return Task.CompletedTask;
    }

    public Task<IEnumerable<string>> ListFilesAsync(string directoryPath = "")
    {
        var fullDirectoryPath = GetFullPath(directoryPath);
        
        if (!Directory.Exists(fullDirectoryPath))
            return Task.FromResult(Enumerable.Empty<string>());

        var files = Directory.GetFiles(fullDirectoryPath, "*", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(_baseDirectory, f))
            .Where(f => !f.StartsWith("..")) // Ensure we stay within base directory
            .ToList();
            
        return Task.FromResult<IEnumerable<string>>(files);
    }

    public Task EnsureDirectoryExistsAsync(string directoryPath)
    {
        var fullDirectoryPath = GetFullPath(directoryPath);
        Directory.CreateDirectory(fullDirectoryPath);
        return Task.CompletedTask;
    }

    public string GetFileUrl(string relativePath)
    {
        // For local storage, return the full local path
        return GetFullPath(relativePath);
    }

    public Task CleanupOldFilesAsync(string directoryPath, TimeSpan maxAge)
    {
        var fullDirectoryPath = GetFullPath(directoryPath);
        
        if (!Directory.Exists(fullDirectoryPath))
            return Task.CompletedTask;

        var cutoffTime = DateTime.UtcNow - maxAge;
        var files = Directory.GetFiles(fullDirectoryPath, "*", SearchOption.AllDirectories);
        
        foreach (var file in files)
        {
            try
            {
                var fileInfo = new FileInfo(file);
                if (fileInfo.LastWriteTimeUtc < cutoffTime)
                {
                    File.Delete(file);
                    _logger?.LogDebug("Cleaned up old file: {File}", file);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to clean up file: {File}", file);
            }
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Get the full path by combining base directory with relative path
    /// </summary>
    /// <param name="relativePath">Relative path</param>
    /// <returns>Full absolute path</returns>
    private string GetFullPath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return _baseDirectory;
            
        // Normalize path separators and remove leading separators
        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar)
                                  .Replace('\\', Path.DirectorySeparatorChar)
                                  .TrimStart(Path.DirectorySeparatorChar);
        
        return Path.Combine(_baseDirectory, relativePath);
    }

    /// <summary>
    /// Get the base directory used by this storage provider
    /// </summary>
    public string BaseDirectory => _baseDirectory;

    /// <summary>
    /// Copy a file with retry mechanism to handle file locking issues
    /// </summary>
    /// <param name="sourcePath">Source file path</param>
    /// <param name="destinationPath">Destination file path</param>
    private async Task CopyFileWithRetryAsync(string sourcePath, string destinationPath)
    {
        const int maxRetries = 5;
        const int baseDelayMs = 100;
        
        _logger?.LogDebug("Starting file copy with retry from {SourcePath} to {DestinationPath}", sourcePath, destinationPath);
        
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                // Add delay before retry attempts to allow file handles to be released
                if (attempt > 0)
                {
                    var delayMs = baseDelayMs * (int)Math.Pow(2, attempt - 1); // Exponential backoff
                    _logger?.LogDebug("File copy retry attempt {Attempt} for {DestinationPath}, waiting {DelayMs}ms", 
                        attempt + 1, destinationPath, delayMs);
                    await Task.Delay(delayMs);
                }
                
                // Attempt to copy the file
                await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite: true));
                
                _logger?.LogDebug("Successfully copied file on attempt {Attempt}: {SourcePath} -> {DestinationPath}", 
                    attempt + 1, sourcePath, destinationPath);
                return; // Success - exit the retry loop
            }
            catch (IOException ex) when (attempt < maxRetries - 1 && IsFileLockException(ex))
            {
                _logger?.LogWarning("File lock detected during copy on attempt {Attempt} for {SourcePath}: {Error}. Retrying...", 
                    attempt + 1, sourcePath, ex.Message);
                // Continue to next retry attempt
            }
            catch (UnauthorizedAccessException ex) when (attempt < maxRetries - 1)
            {
                _logger?.LogWarning("Access denied during copy on attempt {Attempt} for {SourcePath}: {Error}. Retrying...", 
                    attempt + 1, sourcePath, ex.Message);
                // Continue to next retry attempt
            }
        }
        
        // Final attempt without catching exceptions - let it throw if it fails
        _logger?.LogWarning("Final attempt to copy file after {MaxRetries} retries: {SourcePath} -> {DestinationPath}", 
            maxRetries, sourcePath, destinationPath);
        await Task.Run(() => File.Copy(sourcePath, destinationPath, overwrite: true));
    }

    /// <summary>
    /// Determines if an IOException is likely due to file locking
    /// </summary>
    /// <param name="ex">The IOException to check</param>
    /// <returns>True if the exception appears to be file lock related</returns>
    private static bool IsFileLockException(IOException ex)
    {
        var message = ex.Message.ToLowerInvariant();
        return message.Contains("being used by another process") ||
               message.Contains("cannot access the file") ||
               message.Contains("sharing violation") ||
               message.Contains("lock");
    }
}