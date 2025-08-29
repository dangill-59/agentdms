# AgentDMS Performance Optimizations

This document describes the performance optimizations implemented for Mistral AI and OCR services in AgentDMS.

## Overview

The AgentDMS system has been optimized to provide significantly faster document processing through intelligent caching, request deduplication, and HTTP client optimization.

## Key Performance Improvements

### 1. Smart Caching Layer (`PerformanceCache`)

- **Content-based hashing**: Uses SHA256 to generate unique keys for document content
- **Automatic expiration**: OCR results cached for 1 hour, AI analysis for 2 hours  
- **Memory efficient**: Stores only successful results to avoid cache pollution
- **Cache statistics**: Provides metrics on cache usage and hit rates

**Performance Impact**: Cache hits return results in <1ms vs API calls taking seconds.

### 2. Request Deduplication

- **Prevents duplicate API calls**: Multiple simultaneous requests for identical content are deduplicated
- **Semaphore-based coordination**: Uses per-key semaphores to ensure only one request per unique content
- **Automatic cleanup**: Semaphores are disposed when no longer needed

**Performance Impact**: Eliminates wasted API calls and reduces server load during batch processing.

### 3. HTTP Client Optimization

- **Connection reuse**: Configured `ConnectionClose = false` for persistent connections
- **Optimal timeouts**: Set 2-minute timeout for API requests to balance performance and reliability
- **Keep-alive headers**: Maintains connections for better throughput

**Performance Impact**: Reduces connection overhead by reusing HTTP connections.

### 4. Payload Optimization

- **Reduced response sizes**: Changed default `includeImageBase64` from `true` to `false`
- **Selective data inclusion**: Only request necessary data from APIs
- **Efficient serialization**: Optimized JSON serialization settings

**Performance Impact**: Significantly reduces network bandwidth usage and response times.

### 5. Intelligent Processing Workflow

- **Optimized processing flow**: `OptimizeTextExtractionAndAnalysisAsync` method provides smarter processing
- **Fallback mechanisms**: Gracefully handles failures with fallback to sequential processing
- **Context-aware optimization**: Different strategies based on processing requirements

## Usage Examples

### Using Shared Cache

```csharp
// Create a shared cache instance
var sharedCache = new PerformanceCache(logger, TimeSpan.FromMinutes(30));

// Initialize services with shared cache
var ocrService = new MistralOcrService(httpClient, apiKey, logger: logger, cache: sharedCache);
var aiService = new MistralDocumentAiService(httpClient, apiKey, logger: logger, cache: sharedCache);
var imageService = new ImageProcessingService(
    maxConcurrency: 4,
    logger: logger,
    mistralService: aiService,
    mistralOcrService: ocrService,
    cache: sharedCache);
```

### Cache Statistics

```csharp
var stats = cache.GetStats();
Console.WriteLine($"Cache entries: {stats.EntryCount}");
Console.WriteLine($"In-progress requests: {stats.InProgressRequestCount}");
Console.WriteLine($"Default expiry: {stats.DefaultExpiry}");
```

### Manual Cache Operations

```csharp
// Generate cache key
var key = PerformanceCache.GenerateKey(documentContent, "ocr");

// Check for cached result
var cachedResult = cache.Get<OcrResult>(key);

// Get or create with deduplication
var result = await cache.GetOrCreateAsync(key, async () => {
    return await ProcessDocument(content);
}, TimeSpan.FromHours(1));
```

## Configuration

### Default Cache Settings

- **OCR Results**: 1 hour expiration
- **AI Analysis**: 2 hours expiration  
- **Default Cache**: 30 minutes expiration
- **HTTP Timeout**: 2 minutes

### Customization

```csharp
// Custom cache expiration
var cache = new PerformanceCache(logger, TimeSpan.FromMinutes(60));

// Custom HTTP client configuration
var httpClient = new HttpClient();
httpClient.Timeout = TimeSpan.FromMinutes(5);
httpClient.DefaultRequestHeaders.ConnectionClose = false;
```

## Performance Metrics

Based on testing with the included performance test suite:

- **Cache Performance**: <1ms retrieval time for cached results
- **Request Deduplication**: 100% elimination of duplicate simultaneous requests
- **HTTP Optimization**: Reduced connection overhead through persistent connections
- **Payload Reduction**: Significant bandwidth savings by eliminating unnecessary data

## Testing

The optimizations include comprehensive test coverage:

- `PerformanceOptimizationTests`: 10 tests validating all optimization features
- Cache hit/miss scenarios
- Request deduplication validation
- HTTP client configuration verification
- Performance benchmarks

Run tests with:
```bash
dotnet test --filter "PerformanceOptimizationTests"
```

## Best Practices

1. **Use Shared Cache**: Initialize one `PerformanceCache` instance and share it across all services
2. **Monitor Cache Stats**: Regularly check cache statistics to optimize expiration times
3. **Batch Processing**: Take advantage of request deduplication during batch operations
4. **Resource Management**: Cache automatically cleans up expired entries and unused semaphores

## Future Enhancements

Potential areas for further optimization:

- **Distributed Caching**: Redis-based caching for multi-instance deployments
- **Persistent Cache**: File-based cache persistence across application restarts
- **Advanced Analytics**: Detailed performance metrics and monitoring
- **Parallel Processing**: True parallel execution of OCR and AI analysis where content permits