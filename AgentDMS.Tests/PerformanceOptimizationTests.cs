using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AgentDMS.Core.Models;
using AgentDMS.Core.Services;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;

namespace AgentDMS.Tests;

/// <summary>
/// Tests for performance optimizations in Mistral AI and OCR services
/// </summary>
public class PerformanceOptimizationTests
{
    private readonly ITestOutputHelper _output;

    public PerformanceOptimizationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void PerformanceCache_ShouldCacheAndRetrieveResults()
    {
        // Arrange
        var cache = new PerformanceCache();
        var testData = "test document content";
        var key = PerformanceCache.GenerateKey(testData, "test");
        var result = new OcrResult { Success = true, Text = "extracted text", Confidence = 0.95 };

        // Act
        cache.Set(key, result);
        var retrieved = cache.Get<OcrResult>(key);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(result.Text, retrieved.Text);
        Assert.Equal(result.Confidence, retrieved.Confidence);
        
        _output.WriteLine($"Cache key generated: {key}");
        _output.WriteLine($"Cached and retrieved result successfully");
    }

    [Fact]
    public void PerformanceCache_ShouldHandleExpiredEntries()
    {
        // Arrange
        var cache = new PerformanceCache(defaultExpiry: TimeSpan.FromMilliseconds(100));
        var testData = "test document content";
        var key = PerformanceCache.GenerateKey(testData, "test");
        var result = new OcrResult { Success = true, Text = "extracted text" };

        // Act
        cache.Set(key, result, TimeSpan.FromMilliseconds(50));
        var immediateRetrieve = cache.Get<OcrResult>(key);
        
        // Wait for expiry
        Thread.Sleep(100);
        var expiredRetrieve = cache.Get<OcrResult>(key);

        // Assert
        Assert.NotNull(immediateRetrieve);
        Assert.Null(expiredRetrieve);
        
        _output.WriteLine("Cache correctly handled expired entries");
    }

    [Fact]
    public void PerformanceCache_ShouldGenerateConsistentKeys()
    {
        // Arrange
        var content1 = "same content";
        var content2 = "same content";
        var content3 = "different content";

        // Act
        var key1 = PerformanceCache.GenerateKey(content1, "ocr");
        var key2 = PerformanceCache.GenerateKey(content2, "ocr");
        var key3 = PerformanceCache.GenerateKey(content3, "ocr");

        // Assert
        Assert.Equal(key1, key2);
        Assert.NotEqual(key1, key3);
        
        _output.WriteLine($"Key1: {key1}");
        _output.WriteLine($"Key2: {key2}");
        _output.WriteLine($"Key3: {key3}");
    }

    [Fact]
    public void MistralOcrService_ShouldUseOptimizedDefaults()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var logMessages = new List<string>();
        var logger = new TestLogger<MistralOcrService>(logMessages);

        // Act
        var ocrService = new MistralOcrService(httpClient, logger: logger);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(2), httpClient.Timeout);
        Assert.False(httpClient.DefaultRequestHeaders.ConnectionClose ?? true);
        
        _output.WriteLine("MistralOcrService configured with optimized HTTP client settings");
    }

    [Fact]
    public void MistralDocumentAiService_ShouldUseOptimizedDefaults()
    {
        // Arrange
        using var httpClient = new HttpClient();
        var logMessages = new List<string>();
        var logger = new TestLogger<MistralDocumentAiService>(logMessages);

        // Act
        var aiService = new MistralDocumentAiService(httpClient, logger: logger);

        // Assert
        Assert.Equal(TimeSpan.FromMinutes(2), httpClient.Timeout);
        Assert.False(httpClient.DefaultRequestHeaders.ConnectionClose ?? true);
        
        _output.WriteLine("MistralDocumentAiService configured with optimized HTTP client settings");
    }

    [Fact]
    public async Task PerformanceCache_ShouldShowMeasurableSpeedImprovement()
    {
        // Arrange
        var cache = new PerformanceCache();
        var testContent = "Large document content for performance testing. " + new string('A', 1000);
        var key = PerformanceCache.GenerateKey(testContent, "perf");
        var result = new DocumentAiResult 
        { 
            Success = true, 
            DocumentType = "invoice", 
            Confidence = 0.9,
            ExtractedData = new Dictionary<string, string> { { "amount", "100.00" }, { "vendor", "Test Corp" } }
        };

        // Measure cache set time
        var setStopwatch = Stopwatch.StartNew();
        cache.Set(key, result);
        setStopwatch.Stop();

        // Measure first retrieval (should be very fast)
        var getStopwatch = Stopwatch.StartNew();
        var retrieved = cache.Get<DocumentAiResult>(key);
        getStopwatch.Stop();

        // Measure multiple retrievals
        var multiGetStopwatch = Stopwatch.StartNew();
        for (int i = 0; i < 100; i++)
        {
            cache.Get<DocumentAiResult>(key);
        }
        multiGetStopwatch.Stop();

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(getStopwatch.ElapsedMilliseconds < 1, "Single cache retrieval should be under 1ms");
        Assert.True(multiGetStopwatch.ElapsedMilliseconds < 10, "100 cache retrievals should be under 10ms");
        
        _output.WriteLine($"Cache set time: {setStopwatch.ElapsedMilliseconds}ms");
        _output.WriteLine($"Cache get time: {getStopwatch.ElapsedTicks} ticks");
        _output.WriteLine($"100 cache gets time: {multiGetStopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public void CacheStats_ShouldProvideUsefulMetrics()
    {
        // Arrange
        var cache = new PerformanceCache();
        
        // Act
        cache.Set("key1", new OcrResult { Text = "test1" });
        cache.Set("key2", new DocumentAiResult { DocumentType = "invoice" });
        var stats = cache.GetStats();

        // Assert
        Assert.Equal(2, stats.EntryCount);
        Assert.Equal(0, stats.InProgressRequestCount);
        Assert.Equal(TimeSpan.FromMinutes(30), stats.DefaultExpiry);
        
        _output.WriteLine($"Cache entries: {stats.EntryCount}");
        _output.WriteLine($"In-progress requests: {stats.InProgressRequestCount}");
        _output.WriteLine($"Default expiry: {stats.DefaultExpiry}");
    }

    [Fact]
    public async Task PerformanceCache_ShouldDeduplicateSimultaneousRequests()
    {
        // Arrange
        var cache = new PerformanceCache();
        var key = "test-deduplication";
        var callCount = 0;
        
        // Factory function that tracks how many times it's called
        async Task<OcrResult> Factory()
        {
            Interlocked.Increment(ref callCount);
            await Task.Delay(50); // Simulate API call
            return new OcrResult { Success = true, Text = $"Result {callCount}" };
        }

        // Act - Start multiple simultaneous requests for the same key
        var tasks = new List<Task<OcrResult>>();
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(cache.GetOrCreateAsync(key, Factory));
        }

        var results = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(1, callCount); // Factory should only be called once due to deduplication
        Assert.All(results, result => Assert.True(result.Success));
        Assert.All(results, result => Assert.Equal("Result 1", result.Text)); // All should have same result
        
        _output.WriteLine($"Factory called {callCount} times for 5 simultaneous requests");
        _output.WriteLine($"All results identical: {results.All(r => r.Text == results[0].Text)}");
    }

    [Fact]
    public async Task PerformanceCache_GetOrCreateAsync_ShouldReturnCachedValue()
    {
        // Arrange
        var cache = new PerformanceCache();
        var key = "test-cached";
        var factoryCalled = false;
        
        // Pre-populate cache
        cache.Set(key, new OcrResult { Text = "cached value" });
        
        // Factory that shouldn't be called
        async Task<OcrResult> Factory()
        {
            factoryCalled = true;
            await Task.Delay(10);
            return new OcrResult { Text = "factory value" };
        }

        // Act
        var result = await cache.GetOrCreateAsync(key, Factory);

        // Assert
        Assert.False(factoryCalled); // Factory should not be called
        Assert.Equal("cached value", result.Text); // Should return cached value
        
        _output.WriteLine($"Factory called: {factoryCalled}");
        _output.WriteLine($"Result: {result.Text}");
    }

    [Fact]
    public void ImageProcessingService_ShouldAcceptSharedCache()
    {
        // Arrange
        var sharedCache = new PerformanceCache();
        var logMessages = new List<string>();
        var logger = new TestLogger<ImageProcessingService>(logMessages);

        // Act
        var imageService = new ImageProcessingService(
            maxConcurrency: 2,
            logger: logger,
            cache: sharedCache);

        // Assert - Service should accept the shared cache without throwing
        Assert.NotNull(imageService);
        
        _output.WriteLine("ImageProcessingService successfully initialized with shared cache");
    }
}