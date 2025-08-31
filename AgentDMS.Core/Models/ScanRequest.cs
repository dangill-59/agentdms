using System;
using System.ComponentModel.DataAnnotations;

namespace AgentDMS.Core.Models;

/// <summary>
/// Request model for scanner operations
/// </summary>
public class ScanRequest
{
    /// <summary>
    /// Scanner device ID or name
    /// </summary>
    public string? ScannerDeviceId { get; set; }
    
    /// <summary>
    /// Resolution in DPI (default: 300)
    /// </summary>
    [Range(50, 4800)]
    public int Resolution { get; set; } = 300;
    
    /// <summary>
    /// Color mode for scanning
    /// </summary>
    public ScanColorMode ColorMode { get; set; } = ScanColorMode.Color;
    
    /// <summary>
    /// File format for scanned image
    /// </summary>
    public ScanFormat Format { get; set; } = ScanFormat.Png;
    
    /// <summary>
    /// Whether to show scanner UI (default: false for automatic scanning)
    /// </summary>
    public bool ShowUserInterface { get; set; } = false;
    
    /// <summary>
    /// Whether to automatically process the scanned image through the processing pipeline
    /// </summary>
    public bool AutoProcess { get; set; } = true;
    
    /// <summary>
    /// Auto-rotation angle in degrees (0, 90, 180, 270)
    /// </summary>
    [Range(0, 360)]
    public int AutoRotation { get; set; } = 0;
}

/// <summary>
/// Color modes for scanning
/// </summary>
public enum ScanColorMode
{
    BlackAndWhite = 0,
    Grayscale = 1,
    Color = 2
}

/// <summary>
/// Supported scan output formats
/// </summary>
public enum ScanFormat
{
    Png = 0,
    Jpeg = 1,
    Tiff = 2
}