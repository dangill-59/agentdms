using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AgentDMS.Core.Services.Storage;

/// <summary>
/// Interface for storage providers that can handle file operations locally or on cloud platforms
/// </summary>
public interface IStorageProvider
{
    /// <summary>
    /// The type of storage provider (Local, AWS, Azure)
    /// </summary>
    StorageProviderType ProviderType { get; }
    
    /// <summary>
    /// Save a file to the storage provider
    /// </summary>
    /// <param name="sourcePath">Local path to the source file</param>
    /// <param name="destinationPath">Relative path where the file should be stored</param>
    /// <returns>The URL or path where the file can be accessed</returns>
    Task<string> SaveFileAsync(string sourcePath, string destinationPath);
    
    /// <summary>
    /// Save file content directly to the storage provider
    /// </summary>
    /// <param name="content">File content as byte array</param>
    /// <param name="destinationPath">Relative path where the file should be stored</param>
    /// <param name="contentType">MIME type of the content</param>
    /// <returns>The URL or path where the file can be accessed</returns>
    Task<string> SaveFileAsync(byte[] content, string destinationPath, string contentType = "application/octet-stream");
    
    /// <summary>
    /// Save file content from a stream to the storage provider
    /// </summary>
    /// <param name="stream">Stream containing file content</param>
    /// <param name="destinationPath">Relative path where the file should be stored</param>
    /// <param name="contentType">MIME type of the content</param>
    /// <returns>The URL or path where the file can be accessed</returns>
    Task<string> SaveFileAsync(Stream stream, string destinationPath, string contentType = "application/octet-stream");
    
    /// <summary>
    /// Check if a file exists in the storage provider
    /// </summary>
    /// <param name="path">Relative path to check</param>
    /// <returns>True if the file exists</returns>
    Task<bool> FileExistsAsync(string path);
    
    /// <summary>
    /// Delete a file from the storage provider
    /// </summary>
    /// <param name="path">Relative path to the file to delete</param>
    Task DeleteFileAsync(string path);
    
    /// <summary>
    /// Get a list of files in a directory
    /// </summary>
    /// <param name="directoryPath">Relative path to the directory</param>
    /// <returns>List of file paths</returns>
    Task<IEnumerable<string>> ListFilesAsync(string directoryPath = "");
    
    /// <summary>
    /// Ensure a directory exists (create if necessary)
    /// </summary>
    /// <param name="directoryPath">Relative path to the directory</param>
    Task EnsureDirectoryExistsAsync(string directoryPath);
    
    /// <summary>
    /// Get the full URL or path for accessing a file
    /// </summary>
    /// <param name="relativePath">Relative path to the file</param>
    /// <returns>Full URL or path for accessing the file</returns>
    string GetFileUrl(string relativePath);
    
    /// <summary>
    /// Clean up old files based on age
    /// </summary>
    /// <param name="directoryPath">Directory to clean</param>
    /// <param name="maxAge">Maximum age of files to keep</param>
    Task CleanupOldFilesAsync(string directoryPath, TimeSpan maxAge);
}

/// <summary>
/// Types of storage providers supported
/// </summary>
public enum StorageProviderType
{
    Local,
    AWS,
    Azure
}