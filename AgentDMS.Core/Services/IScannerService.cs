using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AgentDMS.Core.Models;

namespace AgentDMS.Core.Services;

/// <summary>
/// Interface for scanner operations
/// </summary>
public interface IScannerService
{
    /// <summary>
    /// Get list of available scanners
    /// </summary>
    Task<List<ScannerInfo>> GetAvailableScannersAsync();
    
    /// <summary>
    /// Perform a scan operation
    /// </summary>
    Task<ScanResult> ScanAsync(ScanRequest request);
    
    /// <summary>
    /// Check if scanning functionality is available on this platform
    /// </summary>
    bool IsScanningAvailable();
    
    /// <summary>
    /// Get platform-specific scanning capabilities
    /// </summary>
    Task<ScannerCapabilities> GetCapabilitiesAsync();
}

/// <summary>
/// Scanner capabilities and limitations
/// </summary>
public class ScannerCapabilities
{
    /// <summary>
    /// Whether TWAIN scanning is supported
    /// </summary>
    public bool SupportsTwain { get; set; }
    
    /// <summary>
    /// Whether WIA scanning is supported (Windows)
    /// </summary>
    public bool SupportsWia { get; set; }
    
    /// <summary>
    /// Whether SANE scanning is supported (Linux)
    /// </summary>
    public bool SupportsSane { get; set; }
    
    /// <summary>
    /// Supported color modes
    /// </summary>
    public List<ScanColorMode> SupportedColorModes { get; set; } = new();
    
    /// <summary>
    /// Supported output formats
    /// </summary>
    public List<ScanFormat> SupportedFormats { get; set; } = new();
    
    /// <summary>
    /// Supported resolution range
    /// </summary>
    public (int Min, int Max) ResolutionRange { get; set; } = (50, 1200);
    
    /// <summary>
    /// Platform-specific information
    /// </summary>
    public string PlatformInfo { get; set; } = string.Empty;
}