using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using AgentDMS.Core.Models;
using Microsoft.Extensions.Logging;

namespace AgentDMS.Core.Services.Storage;

/// <summary>
/// AWS S3 storage provider implementation
/// </summary>
public class AwsStorageProvider : IStorageProvider, IDisposable
{
    private readonly string _bucketName;
    private readonly string _region;
    private readonly ILogger<AwsStorageProvider>? _logger;
    private readonly IAmazonS3 _s3Client;
    private bool _disposed = false;

    public StorageProviderType ProviderType => StorageProviderType.AWS;

    /// <summary>
    /// Initialize AWS S3 storage provider
    /// </summary>
    /// <param name="config">AWS S3 configuration</param>
    /// <param name="logger">Optional logger</param>
    public AwsStorageProvider(AwsStorageConfig config, ILogger<AwsStorageProvider>? logger = null)
    {
        if (config == null)
            throw new ArgumentNullException(nameof(config));
        
        if (string.IsNullOrEmpty(config.BucketName))
            throw new ArgumentException("BucketName is required", nameof(config));
            
        if (string.IsNullOrEmpty(config.Region))
            throw new ArgumentException("Region is required", nameof(config));
        
        _bucketName = config.BucketName;
        _region = config.Region;
        _logger = logger;
        
        // Create S3 client with configuration
        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = Amazon.RegionEndpoint.GetBySystemName(_region),
            UseHttp = false, // Use HTTPS by default
            MaxErrorRetry = 3
        };

        // Create S3 client - AWS SDK will handle credentials via AWS credential chain
        // (environment variables, IAM roles, profiles, etc.)
        if (!string.IsNullOrEmpty(config.AccessKeyId) && !string.IsNullOrEmpty(config.SecretAccessKey))
        {
            // Use explicit credentials if provided
            if (!string.IsNullOrEmpty(config.SessionToken))
            {
                _s3Client = new AmazonS3Client(config.AccessKeyId, config.SecretAccessKey, config.SessionToken, s3Config);
            }
            else
            {
                _s3Client = new AmazonS3Client(config.AccessKeyId, config.SecretAccessKey, s3Config);
            }
        }
        else
        {
            // Use default credential chain (environment variables, IAM roles, etc.)
            _s3Client = new AmazonS3Client(s3Config);
        }
        
        _logger?.LogInformation("AwsStorageProvider initialized for bucket: {BucketName} in region: {Region}", _bucketName, _region);
    }

    /// <summary>
    /// Initialize AWS S3 storage provider (legacy constructor for compatibility)
    /// </summary>
    /// <param name="bucketName">S3 bucket name</param>
    /// <param name="region">AWS region</param>
    /// <param name="logger">Optional logger</param>
    [Obsolete("Use constructor with AwsStorageConfig instead")]
    public AwsStorageProvider(string bucketName, string region, ILogger<AwsStorageProvider>? logger = null)
        : this(new AwsStorageConfig { BucketName = bucketName, Region = region }, logger)
    {
    }

    public async Task<string> SaveFileAsync(string sourcePath, string destinationPath)
    {
        try
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Source file not found: {sourcePath}");

            _logger?.LogInformation("Uploading file to S3: {SourcePath} -> s3://{BucketName}/{DestinationPath}", 
                sourcePath, _bucketName, destinationPath);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = destinationPath,
                FilePath = sourcePath,
                ContentType = GetContentType(sourcePath),
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            var response = await _s3Client.PutObjectAsync(request);
            
            var fileUrl = GetFileUrl(destinationPath);
            _logger?.LogInformation("Successfully uploaded file to S3: {FileUrl}", fileUrl);
            
            return fileUrl;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to upload file to S3: {SourcePath} -> {DestinationPath}", sourcePath, destinationPath);
            throw;
        }
    }

    public async Task<string> SaveFileAsync(byte[] content, string destinationPath, string contentType = "application/octet-stream")
    {
        try
        {
            if (content == null || content.Length == 0)
                throw new ArgumentException("Content cannot be null or empty", nameof(content));

            _logger?.LogInformation("Uploading content to S3: {Size} bytes -> s3://{BucketName}/{DestinationPath}", 
                content.Length, _bucketName, destinationPath);

            using var stream = new MemoryStream(content);
            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = destinationPath,
                InputStream = stream,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            var response = await _s3Client.PutObjectAsync(request);
            
            var fileUrl = GetFileUrl(destinationPath);
            _logger?.LogInformation("Successfully uploaded content to S3: {FileUrl}", fileUrl);
            
            return fileUrl;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to upload content to S3: {Size} bytes -> {DestinationPath}", content?.Length ?? 0, destinationPath);
            throw;
        }
    }

    public async Task<string> SaveFileAsync(Stream stream, string destinationPath, string contentType = "application/octet-stream")
    {
        try
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            _logger?.LogInformation("Uploading stream to S3: s3://{BucketName}/{DestinationPath}", _bucketName, destinationPath);

            var request = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = destinationPath,
                InputStream = stream,
                ContentType = contentType,
                ServerSideEncryptionMethod = ServerSideEncryptionMethod.AES256
            };

            var response = await _s3Client.PutObjectAsync(request);
            
            var fileUrl = GetFileUrl(destinationPath);
            _logger?.LogInformation("Successfully uploaded stream to S3: {FileUrl}", fileUrl);
            
            return fileUrl;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to upload stream to S3: {DestinationPath}", destinationPath);
            throw;
        }
    }

    public async Task<bool> FileExistsAsync(string path)
    {
        try
        {
            _logger?.LogDebug("Checking if file exists in S3: s3://{BucketName}/{Path}", _bucketName, path);

            var request = new GetObjectMetadataRequest
            {
                BucketName = _bucketName,
                Key = path
            };

            await _s3Client.GetObjectMetadataAsync(request);
            return true;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return false;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error checking if file exists in S3: {Path}", path);
            throw;
        }
    }

    public async Task DeleteFileAsync(string path)
    {
        try
        {
            _logger?.LogInformation("Deleting file from S3: s3://{BucketName}/{Path}", _bucketName, path);

            var request = new DeleteObjectRequest
            {
                BucketName = _bucketName,
                Key = path
            };

            await _s3Client.DeleteObjectAsync(request);
            _logger?.LogInformation("Successfully deleted file from S3: {Path}", path);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to delete file from S3: {Path}", path);
            throw;
        }
    }

    public async Task<IEnumerable<string>> ListFilesAsync(string directoryPath = "")
    {
        try
        {
            _logger?.LogDebug("Listing files in S3: s3://{BucketName}/{DirectoryPath}", _bucketName, directoryPath);

            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = string.IsNullOrEmpty(directoryPath) ? "" : directoryPath.TrimEnd('/') + "/",
                MaxKeys = 1000 // Limit to prevent large responses
            };

            var files = new List<string>();
            ListObjectsV2Response response;

            do
            {
                response = await _s3Client.ListObjectsV2Async(request);
                
                foreach (var obj in response.S3Objects)
                {
                    // Skip directories (objects ending with /)
                    if (!obj.Key.EndsWith("/"))
                    {
                        files.Add(obj.Key);
                    }
                }

                request.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);

            _logger?.LogDebug("Found {Count} files in S3 directory: {DirectoryPath}", files.Count, directoryPath);
            return files;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to list files in S3: {DirectoryPath}", directoryPath);
            throw;
        }
    }

    public Task EnsureDirectoryExistsAsync(string directoryPath)
    {
        // S3 doesn't have real directories, so this is a no-op
        // Directories are created implicitly when files are uploaded
        _logger?.LogDebug("EnsureDirectoryExists called for S3 (no-op): {DirectoryPath}", directoryPath);
        return Task.CompletedTask;
    }

    public string GetFileUrl(string relativePath)
    {
        // Return the standard S3 URL format
        return $"https://{_bucketName}.s3.{_region}.amazonaws.com/{relativePath}";
    }

    public async Task CleanupOldFilesAsync(string directoryPath, TimeSpan maxAge)
    {
        try
        {
            _logger?.LogInformation("Starting cleanup of old files in S3: s3://{BucketName}/{DirectoryPath}, maxAge: {MaxAge}", 
                _bucketName, directoryPath, maxAge);

            var cutoffDate = DateTime.UtcNow - maxAge;
            var request = new ListObjectsV2Request
            {
                BucketName = _bucketName,
                Prefix = string.IsNullOrEmpty(directoryPath) ? "" : directoryPath.TrimEnd('/') + "/",
                MaxKeys = 1000
            };

            var filesToDelete = new List<string>();
            ListObjectsV2Response response;

            do
            {
                response = await _s3Client.ListObjectsV2Async(request);
                
                foreach (var obj in response.S3Objects)
                {
                    // Skip directories and check age
                    if (!obj.Key.EndsWith("/") && obj.LastModified < cutoffDate)
                    {
                        filesToDelete.Add(obj.Key);
                    }
                }

                request.ContinuationToken = response.NextContinuationToken;
            }
            while (response.IsTruncated);

            if (filesToDelete.Any())
            {
                _logger?.LogInformation("Found {Count} old files to delete in S3", filesToDelete.Count);

                // Delete files in batches (S3 allows up to 1000 objects per batch)
                const int batchSize = 1000;
                for (int i = 0; i < filesToDelete.Count; i += batchSize)
                {
                    var batch = filesToDelete.Skip(i).Take(batchSize).ToList();
                    var deleteRequest = new DeleteObjectsRequest
                    {
                        BucketName = _bucketName,
                        Objects = batch.Select(key => new KeyVersion { Key = key }).ToList()
                    };

                    var deleteResponse = await _s3Client.DeleteObjectsAsync(deleteRequest);
                    _logger?.LogInformation("Deleted {Count} old files from S3 in batch", deleteResponse.DeletedObjects.Count);
                }
            }
            else
            {
                _logger?.LogInformation("No old files found to delete in S3");
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to cleanup old files in S3: {DirectoryPath}", directoryPath);
            throw;
        }
    }

    /// <summary>
    /// Get content type based on file extension
    /// </summary>
    private static string GetContentType(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        return extension switch
        {
            ".pdf" => "application/pdf",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            ".tiff" or ".tif" => "image/tiff",
            ".txt" => "text/plain",
            ".json" => "application/json",
            ".xml" => "application/xml",
            ".zip" => "application/zip",
            _ => "application/octet-stream"
        };
    }

    /// <summary>
    /// Dispose of AWS S3 client
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _s3Client?.Dispose();
            _disposed = true;
        }
    }
}