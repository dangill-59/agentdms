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
    /// <param name="baseDirectory">Base directory for file storage. If null, uses temp directory.</param>
    /// <param name="logger">Optional logger</param>
    public LocalStorageProvider(string? baseDirectory = null, ILogger<LocalStorageProvider>? logger = null)
    {
        _baseDirectory = baseDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Output");
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

        await Task.Run(() => File.Copy(sourcePath, fullDestinationPath, overwrite: true));
        
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

    public async Task CleanupOldFilesAsync(string directoryPath, TimeSpan maxAge)
    {
        var fullDirectoryPath = GetFullPath(directoryPath);
        
        if (!Directory.Exists(fullDirectoryPath))
            return;

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
}