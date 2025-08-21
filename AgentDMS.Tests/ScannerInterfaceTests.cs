using Xunit;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AgentDMS.Tests
{
    public class ScannerInterfaceTests
    {
        private class TestLogger<T> : ILogger<T>
        {
            private readonly List<string> _logMessages;

            public TestLogger(List<string> logMessages)
            {
                _logMessages = logMessages;
            }

            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                _logMessages.Add(formatter(state, exception));
            }
        }

        [Fact]
        public async Task ScanAsync_WithShowUserInterface_ShouldIndicateUIMode()
        {
            // Arrange
            var loggedMessages = new List<string>();
            var logger = new TestLogger<ScannerService>(loggedMessages);
            var scannerService = new ScannerService(logger);

            var scanRequest = new ScanRequest
            {
                ScannerDeviceId = "mock_scanner_1",
                ShowUserInterface = true,
                Resolution = 300,
                ColorMode = ScanColorMode.Color,
                Format = ScanFormat.Png
            };

            // Act
            var result = await scannerService.ScanAsync(scanRequest);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("ShowUI: True", string.Join(" ", loggedMessages));
            
            // When ShowUserInterface is true, the result should reflect UI mode
            Assert.NotNull(result.ScannerUsed);
        }

        [Fact]
        public async Task ScanAsync_WithoutShowUserInterface_ShouldUseAutomaticMode()
        {
            // Arrange
            var loggedMessages = new List<string>();
            var logger = new TestLogger<ScannerService>(loggedMessages);
            var scannerService = new ScannerService(logger);

            var scanRequest = new ScanRequest
            {
                ScannerDeviceId = "mock_scanner_1",
                ShowUserInterface = false,
                Resolution = 300,
                ColorMode = ScanColorMode.Color,
                Format = ScanFormat.Png
            };

            // Act
            var result = await scannerService.ScanAsync(scanRequest);

            // Assert
            Assert.True(result.Success);
            Assert.Contains("ShowUI: False", string.Join(" ", loggedMessages));
        }
    }
}