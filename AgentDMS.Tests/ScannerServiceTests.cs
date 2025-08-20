using Xunit;
using System.Threading.Tasks;
using AgentDMS.Core.Services;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.IO;

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

    [Fact]
    public async Task GetAvailableScannersAsync_ShouldIncludeDetailedLogging()
    {
        // Arrange
        var loggedMessages = new List<string>();
        var logger = new TestLogger<ScannerService>(loggedMessages);
        using var scannerService = new ScannerService(logger);

        // Act
        var scanners = await scannerService.GetAvailableScannersAsync();

        // Assert
        Assert.NotNull(scanners);
        Assert.NotEmpty(scanners);
        
        // Verify that detailed logging occurred - check for any scanner-related logging
        var hasInitLogging = loggedMessages.Any(msg => msg.Contains("Scanner service initialized"));
        var hasScannerLogging = loggedMessages.Any(msg => 
            msg.Contains("real scanners") || 
            msg.Contains("mock scanners") || 
            msg.Contains("Total scanners available"));
        
        Assert.True(hasInitLogging, "Should log scanner service initialization");
        Assert.True(hasScannerLogging, "Should log scanner detection process");
    }

    [Fact] 
    public async Task GetAvailableScannersAsync_OnWindows_ShouldAttemptTwainDetection()
    {
        // Arrange
        var loggedMessages = new List<string>();
        var logger = new TestLogger<ScannerService>(loggedMessages);
        using var scannerService = new ScannerService(logger);

        // Act
        var scanners = await scannerService.GetAvailableScannersAsync();

        // Assert
        Assert.NotNull(scanners);
        
        if (OperatingSystem.IsWindows())
        {
            // On Windows, should log real scanner support and attempt TWAIN detection
            var hasRealScannerLogging = loggedMessages.Any(msg => 
                msg.Contains("Real scanner support") || 
                msg.Contains("real TWAIN scanners") ||
                msg.Contains("real scanners"));
            Assert.True(hasRealScannerLogging, "Should attempt real scanner detection on Windows");
        }
        
        // Should always have at least mock scanners
        Assert.NotEmpty(scanners);
    }

    [Fact]
    public async Task GetDiagnosticInfoAsync_ShouldReturnDiagnosticInformation()
    {
        // Arrange
        using var scannerService = new ScannerService();

        // Act
        var diagnostics = await scannerService.GetDiagnosticInfoAsync();

        // Assert
        Assert.NotNull(diagnostics);
        Assert.True(diagnostics.ContainsKey("Platform"));
        Assert.True(diagnostics.ContainsKey("IsWindows"));
        Assert.True(diagnostics.ContainsKey("RealScannerSupport"));
        Assert.True(diagnostics.ContainsKey("Timestamp"));
        
        if (OperatingSystem.IsWindows())
        {
            // On Windows, should have additional diagnostic information
            Assert.True(diagnostics.ContainsKey("TwainDirectories"));
            Assert.True(diagnostics.ContainsKey("RegistryKeys"));
            Assert.True(diagnostics.ContainsKey("TwainSession"));
        }
        else
        {
            // On non-Windows, should have a message explaining limitation
            Assert.True(diagnostics.ContainsKey("Message"));
        }
    }
}

/// <summary>
/// Test logger that captures log messages for verification
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    private readonly List<string> _loggedMessages;

    public TestLogger(List<string> loggedMessages)
    {
        _loggedMessages = loggedMessages;
    }

    public IDisposable? BeginScope<TState>(TState state) => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        _loggedMessages.Add(message);
    }
}