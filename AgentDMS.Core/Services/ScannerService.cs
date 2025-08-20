using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AgentDMS.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.Fonts;
using IOPath = System.IO.Path;
using IODirectory = System.IO.Directory;
using NTwain;
using NTwain.Data;

namespace AgentDMS.Core.Services;

/// <summary>
/// Cross-platform scanner service with mock functionality for development and testing
/// </summary>
public class ScannerService : IScannerService, IDisposable
{
    private readonly ILogger<ScannerService>? _logger;
    private readonly string _tempDirectory;
    private readonly bool _isRealScanner;
    private bool _disposed = false;

    public ScannerService(ILogger<ScannerService>? logger = null)
    {
        _logger = logger;
        _tempDirectory = IOPath.Combine(IOPath.GetTempPath(), "AgentDMS_Scans");
        IODirectory.CreateDirectory(_tempDirectory);
        
        // Check if we're on Windows and can potentially use real scanners
        _isRealScanner = OperatingSystem.IsWindows();
        
        _logger?.LogInformation("Scanner service initialized. Real scanner support: {IsRealScanner}", _isRealScanner);
    }

    /// <summary>
    /// Get list of available scanners
    /// </summary>
    public async Task<List<ScannerInfo>> GetAvailableScannersAsync()
    {
        var scanners = new List<ScannerInfo>();
        
        try
        {
            if (_isRealScanner)
            {
                // Add real TWAIN scanner detection when running on Windows
                _logger?.LogInformation("Detecting real TWAIN scanners...");
                var twainScanners = await GetTwainScannersAsync();
                scanners.AddRange(twainScanners);
                _logger?.LogInformation("Found {Count} real TWAIN scanners", twainScanners.Count);
            }
            
            // Provide mock scanners for development and testing when no real scanners found
            if (scanners.Count == 0)
            {
                scanners.AddRange(GetMockScanners());
                _logger?.LogInformation("No real scanners found, using mock scanners");
            }
            
            _logger?.LogInformation("Total scanners available: {Count}", scanners.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error enumerating scanners");
            // Fallback to mock scanners on error
            scanners.Clear();
            scanners.AddRange(GetMockScanners());
        }

        await Task.CompletedTask;
        return scanners;
    }

    /// <summary>
    /// Get real TWAIN scanners available on the system
    /// </summary>
    private async Task<List<ScannerInfo>> GetTwainScannersAsync()
    {
        var scanners = new List<ScannerInfo>();
        
        try
        {
            await Task.Run(() =>
            {
                // Initialize TWAIN session
                var session = new TwainSession(TWIdentity.CreateFromAssembly(DataGroups.Image, typeof(ScannerService).Assembly));
                
                try
                {
                    session.Open();
                    
                    // Get list of available TWAIN data sources (scanners)
                    foreach (var source in session.GetSources())
                    {
                        var scannerInfo = new ScannerInfo
                        {
                            DeviceId = $"twain_{source.Name}",
                            Name = source.Name?.Trim() ?? "Unknown TWAIN Scanner",
                            Manufacturer = source.Manufacturer?.Trim() ?? "Unknown",
                            Model = source.ProductFamily?.Trim() ?? "TWAIN Scanner",
                            IsAvailable = true,
                            IsDefault = scanners.Count == 0, // First scanner is default
                            Capabilities = new Dictionary<string, object>
                            {
                                ["Type"] = "TWAIN",
                                ["Version"] = source.Version.Info ?? "Unknown",
                                ["DataSource"] = source.Name ?? "Unknown"
                            }
                        };
                        
                        scanners.Add(scannerInfo);
                        _logger?.LogInformation("Found TWAIN scanner: {Name} by {Manufacturer}", 
                            scannerInfo.Name, scannerInfo.Manufacturer);
                    }
                }
                finally
                {
                    session.Close();
                }
            });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to enumerate TWAIN scanners. This is normal if no TWAIN drivers are installed.");
        }
        
        return scanners;
    }

    /// <summary>
    /// Perform a scan operation
    /// </summary>
    public async Task<ScanResult> ScanAsync(ScanRequest request)
    {
        var result = new ScanResult
        {
            Success = false,
            ScanSettings = request,
            ScanTime = DateTime.UtcNow
        };

        try
        {
            _logger?.LogInformation("Starting scan operation with settings: {Resolution}dpi, {ColorMode}, {Format}", 
                request.Resolution, request.ColorMode, request.Format);

            // Simulate scanning delay
            await Task.Delay(2000);

            // Generate a mock scanned document
            var scannedImage = await GenerateMockScannedDocument(request);
            
            // Generate output filename
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var extension = GetFileExtension(request.Format);
            var fileName = $"scan_{timestamp}{extension}";
            var outputPath = IOPath.Combine(_tempDirectory, fileName);

            // Save the generated image
            await scannedImage.SaveAsync(outputPath);
            scannedImage.Dispose();

            result.Success = true;
            result.ScannedFilePath = outputPath;
            result.FileName = fileName;
            result.ScannerUsed = _isRealScanner ? "Mock Scanner (Development)" : "Mock Scanner";
            
            _logger?.LogInformation("Scan operation completed successfully: {FileName}", fileName);
        }
        catch (Exception ex)
        {
            result.ErrorMessage = ex.Message;
            _logger?.LogError(ex, "Error during scan operation");
        }

        return result;
    }

    /// <summary>
    /// Check if scanning functionality is available
    /// </summary>
    public bool IsScanningAvailable()
    {
        return true; // Mock scanner is always available
    }

    /// <summary>
    /// Get platform-specific scanning capabilities
    /// </summary>
    public async Task<ScannerCapabilities> GetCapabilitiesAsync()
    {
        var capabilities = new ScannerCapabilities
        {
            SupportsTwain = OperatingSystem.IsWindows(),
            SupportsWia = OperatingSystem.IsWindows(),
            SupportsSane = OperatingSystem.IsLinux(),
            SupportedColorModes = new List<ScanColorMode>
            {
                ScanColorMode.BlackAndWhite,
                ScanColorMode.Grayscale,
                ScanColorMode.Color
            },
            SupportedFormats = new List<ScanFormat>
            {
                ScanFormat.Png,
                ScanFormat.Jpeg,
                ScanFormat.Tiff
            },
            ResolutionRange = (50, 1200),
            PlatformInfo = Environment.OSVersion.ToString()
        };

        await Task.CompletedTask;
        return capabilities;
    }

    /// <summary>
    /// Get mock scanners for development and testing
    /// </summary>
    private List<ScannerInfo> GetMockScanners()
    {
        return new List<ScannerInfo>
        {
            new ScannerInfo
            {
                DeviceId = "mock_scanner_1",
                Name = "AgentDMS Mock Scanner",
                Manufacturer = "AgentDMS",
                Model = "MockScan Pro 2024",
                IsAvailable = true,
                IsDefault = true,
                Capabilities = new Dictionary<string, object>
                {
                    ["Type"] = "Mock",
                    ["MaxResolution"] = 1200,
                    ["ColorModes"] = new[] { "Color", "Grayscale", "BlackAndWhite" }
                }
            },
            new ScannerInfo
            {
                DeviceId = "mock_scanner_2",
                Name = "Development Test Scanner",
                Manufacturer = "DevTools",
                Model = "TestScan v1.0",
                IsAvailable = true,
                IsDefault = false,
                Capabilities = new Dictionary<string, object>
                {
                    ["Type"] = "Mock",
                    ["MaxResolution"] = 600,
                    ["ColorModes"] = new[] { "Color", "Grayscale" }
                }
            }
        };
    }

    /// <summary>
    /// Generate a realistic mock scanned document
    /// </summary>
    private async Task<Image> GenerateMockScannedDocument(ScanRequest request)
    {
        // Create a document-sized image (8.5" x 11" at specified resolution)
        var dpi = request.Resolution;
        var width = (int)(8.5 * dpi);
        var height = (int)(11 * dpi);

        var image = new Image<Rgba32>(width, height);

        await Task.Run(() =>
        {
            image.Mutate(ctx =>
            {
                // Set background color based on color mode
                var backgroundColor = request.ColorMode switch
                {
                    ScanColorMode.BlackAndWhite => Color.White,
                    ScanColorMode.Grayscale => Color.FromRgb(248, 248, 248),
                    ScanColorMode.Color => Color.FromRgb(252, 252, 250),
                    _ => Color.White
                };
                
                ctx.BackgroundColor(backgroundColor);

                // Add a simple border to make it look like a document
                var borderColor = request.ColorMode == ScanColorMode.Color ? 
                    Color.FromRgb(128, 128, 128) : Color.FromRgb(64, 64, 64);
                
                ctx.Draw(borderColor, 2, new RectangleF(10, 10, width - 20, height - 20));

                // Add some content areas to simulate a document
                var contentColor = request.ColorMode switch
                {
                    ScanColorMode.BlackAndWhite => Color.Black,
                    ScanColorMode.Grayscale => Color.FromRgb(64, 64, 64),
                    ScanColorMode.Color => Color.FromRgb(32, 32, 96),
                    _ => Color.Black
                };

                // Add header area
                var margin = dpi / 2; // 0.5 inch margin
                ctx.Fill(contentColor, new RectangleF(margin, margin, width - (2 * margin), dpi / 6));
                
                // Add some body content rectangles to simulate text
                var lineHeight = dpi / 12; // ~1/12 inch line height
                var lineSpacing = lineHeight * 1.5f;
                
                for (int i = 0; i < 15; i++)
                {
                    var lineY = margin + (dpi / 3) + (i * lineSpacing);
                    var lineWidth = (width - (2 * margin)) * (0.7f + (float)(new Random(i).NextDouble() * 0.25));
                    
                    if (lineY + lineHeight < height - margin)
                    {
                        ctx.Fill(contentColor, new RectangleF(margin, lineY, lineWidth, lineHeight / 3));
                    }
                }

                // Add some noise/texture to make it look more realistic
                AddScannerNoise(ctx, width, height, request.ColorMode);
            });
        });

        return image;
    }

    /// <summary>
    /// Add realistic scanner noise and imperfections
    /// </summary>
    private void AddScannerNoise(IImageProcessingContext ctx, int width, int height, ScanColorMode colorMode)
    {
        var random = new Random(42); // Use fixed seed for consistent results
        
        // Add some subtle noise
        for (int i = 0; i < 50; i++)
        {
            var x = random.Next(width);
            var y = random.Next(height);
            var size = random.Next(1, 3);
            
            var noiseColor = colorMode switch
            {
                ScanColorMode.BlackAndWhite => Color.FromRgb(224, 224, 224),
                ScanColorMode.Grayscale => Color.FromRgb(240, 240, 240),
                ScanColorMode.Color => Color.FromRgb(245, 245, 240),
                _ => Color.LightGray
            };
            
            ctx.Fill(noiseColor, new EllipsePolygon(new PointF(x, y), size));
        }
    }

    /// <summary>
    /// Get file extension for scan format
    /// </summary>
    private static string GetFileExtension(ScanFormat format)
    {
        return format switch
        {
            ScanFormat.Jpeg => ".jpg",
            ScanFormat.Tiff => ".tiff",
            ScanFormat.Png => ".png",
            _ => ".png"
        };
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            // Cleanup if needed
            _disposed = true;
        }
    }
}