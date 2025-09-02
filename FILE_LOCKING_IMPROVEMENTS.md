# File Locking Improvements for PDF Processing

## Overview

This document describes the improvements made to address file locking errors when processing PDFs with Magick.NET that result in "The process cannot access the file ... because it is being used by another process" errors.

## Problem Statement

When processing PDFs using ImageMagick for PDF->PNG conversion, the system would occasionally encounter file locking errors, particularly:
- "The process cannot access the file ... because it is being used by another process"
- File sharing violations
- Temporary access denied errors

These issues were caused by:
1. File handles not being released immediately after write operations
2. Lack of retry mechanisms for write operations
3. Insufficient delays between operations
4. Poor visibility into file locking scenarios
5. **NEW**: Race conditions when using cloud storage where temporary files weren't properly cleaned up
6. **NEW**: LocalStorageProvider attempting to copy files to themselves in certain configurations

## Solution Implementation

### 1. WriteFileWithRetryAsync Method

A new private method that wraps all file write operations with intelligent retry logic:

```csharp
private async Task WriteFileWithRetryAsync(IMagickImage magickImage, string filePath, CancellationToken cancellationToken)
```

**Features:**
- **Exponential Backoff**: 200ms, 400ms, 800ms delays between retry attempts
- **Smart Exception Detection**: Only retries on file locking related exceptions
- **Resource Management**: Forces garbage collection and adds delays to ensure file handle release
- **Detailed Logging**: Debug and warning logs for troubleshooting

**Retry Strategy:**
- Attempt 1: Immediate
- Attempt 2: 200ms delay
- Attempt 3: 400ms delay
- Final attempt: No retry, throws exception

### 2. Enhanced GetFileSizeWithRetryAsync

Improved the existing file size checking method:

```csharp
private async Task<long> GetFileSizeWithRetryAsync(string filePath, CancellationToken cancellationToken)
```

**Improvements:**
- **Linear Backoff**: 100ms, 200ms, 300ms, 400ms, 500ms delays
- **File Lock Detection**: Uses `IsFileLockException` to identify temporary vs permanent failures
- **Enhanced Logging**: Detailed logs with attempt numbers and timing

### 3. File Lock Detection

New utility method to identify file locking exceptions:

```csharp
private static bool IsFileLockException(IOException ex)
```

**Detection Criteria:**
- "being used by another process"
- "cannot access the file"
- "sharing violation"
- "lock"

### 4. ProcessPdfAsync Updates

Updated PDF processing to use the new retry mechanisms:

**Before:**
```csharp
await magickImage.WriteAsync(pagePath, cancellationToken);
GC.Collect();
GC.WaitForPendingFinalizers();
```

**After:**
```csharp
await WriteFileWithRetryAsync(magickImage, pagePath, cancellationToken);
```

### 5. ProcessMultipageTiffAsync Updates

Applied the same improvements to TIFF processing for consistency.

### 6. **NEW**: Temporary File Cleanup Improvements

Added comprehensive cleanup of temporary files when using cloud storage:

```csharp
private async Task CleanupTemporaryFileAsync(string tempFilePath, string storedPath)
private async Task DeleteFileWithRetryAsync(string filePath)
```

**Features:**
- Only cleans up temp files when using cloud storage (paths differ from storage paths)
- Exponential backoff retry for file deletion operations
- Enhanced error handling that doesn't break main processing flow

### 7. **NEW**: LocalStorageProvider Same-File Copy Detection

Enhanced LocalStorageProvider to handle edge cases:

```csharp
// Check if source and destination are the same file (normalized paths)
var normalizedSource = Path.GetFullPath(sourcePath);
var normalizedDestination = Path.GetFullPath(fullDestinationPath);

if (string.Equals(normalizedSource, normalizedDestination, StringComparison.OrdinalIgnoreCase))
{
    // Source and destination are the same - no copy needed
    return GetFileUrl(destinationPath);
}
```

**Improvements:**
- Detects when source and destination paths resolve to the same file
- Skips unnecessary copy operations that would cause file locks
- Added retry mechanism to LocalStorageProvider copy operations

### 8. **NEW**: Enhanced Image Disposal Management

Improved SixLabors.ImageSharp disposal to ensure file handles are released:

```csharp
using (var image = await SixLabors.ImageSharp.Image.LoadAsync(imageFile.OriginalFilePath, cancellationToken))
{
    // Process image
    await image.SaveAsPngAsync(pngPath, cancellationToken);
} // Image disposed here

// Force garbage collection and wait for finalizers
GC.Collect();
GC.WaitForPendingFinalizers();
await Task.Delay(100, cancellationToken);
```

### 9. **NEW**: BackgroundJobService Directory Cleanup

Enhanced cleanup to handle both legacy and current temp directories:

```csharp
var possibleTempDirs = new[]
{
    Path.Combine(tempPath, "AgentDMS_Output"),      // Legacy directory name
    Path.Combine(tempPath, "AgentDMS_Processing")   // Current directory name for cloud storage
};
```

## Logging Enhancements

### Debug Logging
- File write start and completion
- Retry attempt details with delays
- File size check operations

### Warning Logging
- File lock detection during retry attempts
- Access denied scenarios
- Final retry attempt notifications

### Error Context
- Attempt numbers and timing information
- Specific file paths involved
- Exception details for troubleshooting

## Testing

### Stress Testing
New test `ProcessPdfAsync_WithFileHandleStress_ShouldHandleResourcesCorrectly`:
- Processes multiple PDFs concurrently
- Verifies no file locking errors occur
- Ensures graceful handling of resource contention

### Retry Validation
New test `WriteFileWithRetryAsync_WithInvalidPath_ShouldEventuallyFail`:
- Validates retry mechanism doesn't loop forever
- Ensures appropriate exceptions for permanent failures
- Tests reflection-based access to private methods

## Performance Impact

### Minimal Overhead
- Retry logic only activates on actual file lock exceptions
- No performance impact on successful operations
- Strategic delays are only added after failures

### Resource Management
- Explicit garbage collection after write operations
- Strategic delays to allow OS file handle release
- Improved cleanup reduces resource leaks

## Configuration

### Retry Limits
- Write operations: 3 attempts maximum
- File size checks: 5 attempts maximum
- Configurable through constants in the code

### Delay Timings
- Write retry base delay: 200ms (exponential backoff)
- File size check base delay: 100ms (linear backoff)
- Post-write delay: 50ms for handle release

## Compatibility

### Backward Compatibility
- All existing APIs remain unchanged
- No breaking changes to public interfaces
- Existing behavior preserved for successful operations

### Error Handling
- Same exception types thrown for permanent failures
- Additional context in error messages
- Improved logging for troubleshooting

## Troubleshooting

### Enable Debug Logging
To see detailed file operation logs, enable debug logging for the `ImageProcessingService` class.

### Common Scenarios

**File Lock During Write:**
```
DEBUG: Starting file write with retry for: /path/to/file.png
WARN: File lock detected on attempt 1 for /path/to/file.png: The process cannot access the file ... Retrying...
DEBUG: File write retry attempt 2 for /path/to/file.png, waiting 200ms
DEBUG: Successfully wrote file on attempt 2: /path/to/file.png
```

**File Lock During Size Check:**
```
DEBUG: Getting file size with retry for: /path/to/file.png
WARN: File lock detected during size check on attempt 1 for /path/to/file.png: Access denied
DEBUG: File size retry attempt 2 for /path/to/file.png, waiting 100ms
DEBUG: Successfully got file size on attempt 2: /path/to/file.png = 12345 bytes
```

## Future Enhancements

### Configurable Parameters
- Make retry counts and delays configurable
- Add configuration for specific exception handling
- Allow customization of logging levels

### Metrics
- Add performance metrics for retry operations
- Track file lock occurrence rates
- Monitor resource usage patterns

### Additional File Types
- Apply similar improvements to other file operations
- Extend to general storage provider operations
- Consider database transaction retry patterns

## Conclusion

These improvements significantly increase the robustness of PDF processing operations by handling temporary file locking issues gracefully while maintaining excellent performance for normal operations. The enhanced logging provides valuable insights for troubleshooting any remaining edge cases.