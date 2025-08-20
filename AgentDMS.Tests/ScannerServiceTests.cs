using Xunit;
using System.Threading.Tasks;
using AgentDMS.Core.Services;
using Microsoft.Extensions.Logging;
using System.Linq;

namespace AgentDMS.Tests;

public class ScannerServiceTests
{
    [Fact]
    public async Task GetAvailableScannersAsync_ShouldReturnScanners()
    {
        // Arrange
        using var scannerService = new ScannerService();

        // Act
        var scanners = await scannerService.GetAvailableScannersAsync();

        // Assert
        Assert.NotNull(scanners);
        Assert.NotEmpty(scanners);
        
        // Should have at least one scanner (either real TWAIN or mock)
        Assert.True(scanners.Count > 0);
        
        // Should have a default scanner
        Assert.Contains(scanners, s => s.IsDefault);
        
        // All scanners should have basic properties set
        foreach (var scanner in scanners)
        {
            Assert.NotNull(scanner.DeviceId);
            Assert.NotNull(scanner.Name);
            Assert.NotNull(scanner.Manufacturer);
            Assert.True(scanner.IsAvailable);
        }
    }

    [Fact]
    public async Task GetCapabilitiesAsync_ShouldReturnValidCapabilities()
    {
        // Arrange
        using var scannerService = new ScannerService();

        // Act
        var capabilities = await scannerService.GetCapabilitiesAsync();

        // Assert
        Assert.NotNull(capabilities);
        Assert.NotEmpty(capabilities.SupportedColorModes);
        Assert.NotEmpty(capabilities.SupportedFormats);
        Assert.True(capabilities.ResolutionRange.Min > 0);
        Assert.True(capabilities.ResolutionRange.Max > capabilities.ResolutionRange.Min);
        
        // On Windows, should support TWAIN and WIA
        if (OperatingSystem.IsWindows())
        {
            Assert.True(capabilities.SupportsTwain);
            Assert.True(capabilities.SupportsWia);
        }
        
        // On Linux, should support SANE
        if (OperatingSystem.IsLinux())
        {
            Assert.True(capabilities.SupportsSane);
        }
    }

    [Fact]
    public void IsScanningAvailable_ShouldReturnTrue()
    {
        // Arrange
        using var scannerService = new ScannerService();

        // Act
        var isAvailable = scannerService.IsScanningAvailable();

        // Assert
        Assert.True(isAvailable);
    }
}