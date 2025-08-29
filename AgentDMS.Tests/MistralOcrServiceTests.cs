using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AgentDMS.Core.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AgentDMS.Tests;

/// <summary>
/// Tests for MistralOcrService functionality
/// </summary>
public class MistralOcrServiceTests : IDisposable
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly HttpClient _httpClient;
    private readonly Mock<ILogger<MistralOcrService>> _loggerMock;
    private readonly string _tempDirectory;

    public MistralOcrServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object);
        _loggerMock = new Mock<ILogger<MistralOcrService>>();
        _tempDirectory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDirectory);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithValidResponse_ShouldReturnSuccessResult()
    {
        // Arrange
        var apiKey = "test-api-key";
        var endpoint = "https://api.mistral.ai/v1/ocr/process";
        var service = new MistralOcrService(_httpClient, apiKey, endpoint, _loggerMock.Object);

        var mockResponse = new
        {
            text = "Extracted text from document",
            confidence = 0.95,
            imageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChAHIrEeUTAAAAABJRU5ErkJggg=="
        };

        var jsonResponse = JsonSerializer.Serialize(mockResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        // Act
        var result = await service.ProcessDocumentAsync("test-document-data", "mistral-ocr-latest", true);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Extracted text from document", result.Text);
        Assert.Equal(0.95, result.Confidence);
        Assert.NotNull(result.ImageBase64);
        Assert.True(result.ProcessingTime.TotalMilliseconds > 0);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithApiError_ShouldReturnFailedResult()
    {
        // Arrange
        var apiKey = "test-api-key";
        var service = new MistralOcrService(_httpClient, apiKey, logger: _loggerMock.Object);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.BadRequest,
                Content = new StringContent("API Error")
            });

        // Act
        var result = await service.ProcessDocumentAsync("test-document-data");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("API error", result.Message);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithEmptyDocumentData_ShouldReturnFailedResult()
    {
        // Arrange
        var apiKey = "test-api-key";
        var service = new MistralOcrService(_httpClient, apiKey, logger: _loggerMock.Object);

        // Act
        var result = await service.ProcessDocumentAsync("");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Document data is empty or null", result.Message);
    }

    [Fact]
    public async Task ProcessDocumentAsync_WithoutApiKey_ShouldReturnFailedResult()
    {
        // Arrange
        var service = new MistralOcrService(_httpClient, logger: _loggerMock.Object);

        // Act
        var result = await service.ProcessDocumentAsync("test-document-data");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("API key not configured", result.Message);
    }

    [Fact]
    public async Task ProcessDocumentFromFileAsync_WithValidFile_ShouldCallProcessDocumentAsync()
    {
        // Arrange
        var apiKey = "test-api-key";
        var service = new MistralOcrService(_httpClient, apiKey, logger: _loggerMock.Object);

        // Create a test image file
        var testImagePath = Path.Combine(_tempDirectory, "test.png");
        await CreateTestImageFileAsync(testImagePath);

        var mockResponse = new
        {
            text = "Test image text",
            confidence = 0.85
        };

        var jsonResponse = JsonSerializer.Serialize(mockResponse, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        // Act
        var result = await service.ProcessDocumentFromFileAsync(testImagePath);

        // Assert
        Assert.True(result.Success);
        Assert.Equal("Test image text", result.Text);
        Assert.Equal(0.85, result.Confidence);
    }

    [Fact]
    public async Task ProcessDocumentFromFileAsync_WithNonexistentFile_ShouldReturnFailedResult()
    {
        // Arrange
        var apiKey = "test-api-key";
        var service = new MistralOcrService(_httpClient, apiKey, logger: _loggerMock.Object);

        // Act
        var result = await service.ProcessDocumentFromFileAsync("nonexistent-file.png");

        // Assert
        Assert.False(result.Success);
        Assert.Contains("File not found", result.Message);
    }

    [Fact]
    public void OcrResult_Failed_ShouldCreateFailedResult()
    {
        // Act
        var result = OcrResult.Failed("Test error message");

        // Assert
        Assert.False(result.Success);
        Assert.Equal("Test error message", result.Message);
        Assert.Empty(result.Text);
    }

    private async Task CreateTestImageFileAsync(string imagePath)
    {
        // Create a minimal PNG file (1x1 pixel transparent PNG)
        var pngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNkYPhfDwAChAHIrEeUTAAAAABJRU5ErkJggg==");
        await File.WriteAllBytesAsync(imagePath, pngBytes);
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
        
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, true);
        }
    }
}