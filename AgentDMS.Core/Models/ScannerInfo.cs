using System;

namespace AgentDMS.Core.Models;

/// <summary>
/// Information about a TWAIN scanner device
/// </summary>
public class ScannerInfo
{
    /// <summary>
    /// Scanner device ID
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Scanner display name
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Scanner manufacturer
    /// </summary>
    public string Manufacturer { get; set; } = string.Empty;
    
    /// <summary>
    /// Scanner model
    /// </summary>
    public string Model { get; set; } = string.Empty;
    
    /// <summary>
    /// Whether this scanner is currently available
    /// </summary>
    public bool IsAvailable { get; set; }
    
    /// <summary>
    /// Whether this is the default scanner
    /// </summary>
    public bool IsDefault { get; set; }
    
    /// <summary>
    /// Additional capabilities or metadata
    /// </summary>
    public Dictionary<string, object> Capabilities { get; set; } = new();
}

/// <summary>
/// Result of a scan operation
/// </summary>
public class ScanResult
{
    /// <summary>
    /// Whether the scan operation was successful
    /// </summary>
    public bool Success { get; set; }
    
    /// <summary>
    /// Path to the scanned image file
    /// </summary>
    public string? ScannedFilePath { get; set; }
    
    /// <summary>
    /// Original filename for the scanned image
    /// </summary>
    public string? FileName { get; set; }
    
    /// <summary>
    /// Job ID if auto-processing was enabled
    /// </summary>
    public string? ProcessingJobId { get; set; }
    
    /// <summary>
    /// Error message if scan failed
    /// </summary>
    public string? ErrorMessage { get; set; }
    
    /// <summary>
    /// Scanner that was used for this scan
    /// </summary>
    public string? ScannerUsed { get; set; }
    
    /// <summary>
    /// Scan settings that were used
    /// </summary>
    public ScanRequest? ScanSettings { get; set; }
    
    /// <summary>
    /// Time when scan was completed
    /// </summary>
    public DateTime ScanTime { get; set; } = DateTime.UtcNow;
}