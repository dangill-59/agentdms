using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AgentDMS.Core.Services;

/// <summary>
/// Simple in-memory cache for OCR and AI results to improve performance
/// </summary>
public class PerformanceCache
{
    private readonly ConcurrentDictionary<string, CacheEntry<object>> _cache = new();
    private readonly ILogger? _logger;
    private readonly TimeSpan _defaultExpiry;

    public PerformanceCache(ILogger? logger = null, TimeSpan? defaultExpiry = null)
    {
        _logger = logger;
        _defaultExpiry = defaultExpiry ?? TimeSpan.FromMinutes(30); // Default 30 minute cache
    }

    /// <summary>
    /// Get cached result if available and not expired
    /// </summary>
    public T? Get<T>(string key) where T : class
    {
        if (_cache.TryGetValue(key, out var entry))
        {
            if (entry.ExpiryTime > DateTime.UtcNow)
            {
                _logger?.LogDebug("Cache hit for key: {Key}", key);
                return entry.Value as T;
            }
            else
            {
                // Remove expired entry
                _cache.TryRemove(key, out _);
                _logger?.LogDebug("Cache expired for key: {Key}", key);
            }
        }
        
        _logger?.LogDebug("Cache miss for key: {Key}", key);
        return null;
    }

    /// <summary>
    /// Store result in cache
    /// </summary>
    public void Set<T>(string key, T value, TimeSpan? expiry = null) where T : class
    {
        var expiryTime = DateTime.UtcNow.Add(expiry ?? _defaultExpiry);
        var entry = new CacheEntry<object>(value, expiryTime);
        
        _cache.AddOrUpdate(key, entry, (k, existing) => entry);
        _logger?.LogDebug("Cache stored for key: {Key}, expires: {ExpiryTime}", key, expiryTime);
    }

    /// <summary>
    /// Generate a cache key from content using SHA256 hash
    /// </summary>
    public static string GenerateKey(string content, string prefix = "")
    {
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(content));
        var hash = Convert.ToBase64String(hashBytes)[..16]; // Take first 16 chars for shorter key
        return string.IsNullOrEmpty(prefix) ? hash : $"{prefix}:{hash}";
    }

    /// <summary>
    /// Clear expired entries from cache
    /// </summary>
    public void ClearExpired()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = new List<string>();
        
        foreach (var kvp in _cache)
        {
            if (kvp.Value.ExpiryTime <= now)
            {
                expiredKeys.Add(kvp.Key);
            }
        }
        
        foreach (var key in expiredKeys)
        {
            _cache.TryRemove(key, out _);
        }
        
        if (expiredKeys.Count > 0)
        {
            _logger?.LogDebug("Cleared {Count} expired cache entries", expiredKeys.Count);
        }
    }

    /// <summary>
    /// Get cache statistics
    /// </summary>
    public CacheStats GetStats()
    {
        return new CacheStats
        {
            EntryCount = _cache.Count,
            DefaultExpiry = _defaultExpiry
        };
    }

    private record CacheEntry<T>(T Value, DateTime ExpiryTime);
}

/// <summary>
/// Cache statistics
/// </summary>
public class CacheStats
{
    public int EntryCount { get; set; }
    public TimeSpan DefaultExpiry { get; set; }
}