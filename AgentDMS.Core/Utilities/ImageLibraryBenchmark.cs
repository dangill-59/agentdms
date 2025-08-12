using System;
using System.IO;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;
using ImageMagick;
using Microsoft.Extensions.Logging;

namespace AgentDMS.Core.Utilities;

/// <summary>
/// Utility for benchmarking ImageSharp vs Magick.NET performance
/// </summary>
public static class ImageLibraryBenchmark
{
    /// <summary>
    /// Benchmark results for a specific library and operation
    /// </summary>
    public class BenchmarkResult
    {
        public string Library { get; set; } = string.Empty;
        public string Operation { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public TimeSpan ElapsedTime { get; set; }
        public bool Success { get; set; }
        public string? Error { get; set; }
    }

    /// <summary>
    /// Benchmark image processing performance for supported single-page formats
    /// </summary>
    public static async Task<List<BenchmarkResult>> BenchmarkSinglePageFormatsAsync(
        string testFilePath, 
        string outputDirectory,
        CancellationToken cancellationToken = default)
    {
        var results = new List<BenchmarkResult>();
        
        if (!File.Exists(testFilePath))
        {
            throw new FileNotFoundException($"Test file not found: {testFilePath}");
        }

        var fileExtension = Path.GetExtension(testFilePath).ToLowerInvariant();
        var supportedSinglePageFormats = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
        
        if (!supportedSinglePageFormats.Contains(fileExtension))
        {
            throw new ArgumentException($"File format {fileExtension} is not supported for benchmarking");
        }

        Directory.CreateDirectory(outputDirectory);

        Console.WriteLine($"Benchmarking {fileExtension} file: {Path.GetFileName(testFilePath)}");
        
        // Benchmark ImageSharp
        await BenchmarkImageSharp(testFilePath, outputDirectory, results, cancellationToken);
        
        // Benchmark Magick.NET
        await BenchmarkMagickNet(testFilePath, outputDirectory, results, cancellationToken);

        return results;
    }

    private static async Task BenchmarkImageSharp(string testFilePath, string outputDirectory, 
        List<BenchmarkResult> results, CancellationToken cancellationToken)
    {
        var fileExtension = Path.GetExtension(testFilePath).ToLowerInvariant();
        var outputPath = Path.Combine(outputDirectory, $"imagesharp_output{fileExtension}");

        // Test load and save performance
        var loadResult = await MeasureOperationAsync(async () =>
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(testFilePath, cancellationToken);
        }, "ImageSharp", "Load", fileExtension);
        
        results.Add(loadResult);

        var saveResult = await MeasureOperationAsync(async () =>
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(testFilePath, cancellationToken);
            await image.SaveAsPngAsync(outputPath, cancellationToken);
        }, "ImageSharp", "LoadAndSave", fileExtension);
        
        results.Add(saveResult);

        // Test thumbnail generation
        var thumbnailResult = await MeasureOperationAsync(async () =>
        {
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(testFilePath, cancellationToken);
            image.Mutate(x => x.Resize(new SixLabors.ImageSharp.Processing.ResizeOptions
            {
                Size = new SixLabors.ImageSharp.Size(200, 200),
                Mode = SixLabors.ImageSharp.Processing.ResizeMode.Max
            }));
            var thumbPath = Path.Combine(outputDirectory, $"imagesharp_thumb{fileExtension}");
            await image.SaveAsPngAsync(thumbPath, cancellationToken);
        }, "ImageSharp", "Thumbnail", fileExtension);
        
        results.Add(thumbnailResult);
    }

    private static async Task BenchmarkMagickNet(string testFilePath, string outputDirectory, 
        List<BenchmarkResult> results, CancellationToken cancellationToken)
    {
        var fileExtension = Path.GetExtension(testFilePath).ToLowerInvariant();
        var outputPath = Path.Combine(outputDirectory, $"magicknet_output.png");

        // Test load performance
        var loadResult = await MeasureOperationAsync(() =>
        {
            using var image = new MagickImage(testFilePath);
            return Task.CompletedTask;
        }, "Magick.NET", "Load", fileExtension);
        
        results.Add(loadResult);

        // Test load and save performance
        var saveResult = await MeasureOperationAsync(async () =>
        {
            using var image = new MagickImage(testFilePath);
            image.Format = MagickFormat.Png;
            await image.WriteAsync(outputPath, cancellationToken);
        }, "Magick.NET", "LoadAndSave", fileExtension);
        
        results.Add(saveResult);

        // Test thumbnail generation
        var thumbnailResult = await MeasureOperationAsync(async () =>
        {
            using var image = new MagickImage(testFilePath);
            image.Resize(200, 200);
            image.Format = MagickFormat.Png;
            var thumbPath = Path.Combine(outputDirectory, $"magicknet_thumb.png");
            await image.WriteAsync(thumbPath, cancellationToken);
        }, "Magick.NET", "Thumbnail", fileExtension);
        
        results.Add(thumbnailResult);
    }

    private static async Task<BenchmarkResult> MeasureOperationAsync(Func<Task> operation, 
        string library, string operationName, string format)
    {
        var result = new BenchmarkResult
        {
            Library = library,
            Operation = operationName,
            Format = format
        };

        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await operation();
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }
        
        stopwatch.Stop();
        result.ElapsedTime = stopwatch.Elapsed;

        return result;
    }

    /// <summary>
    /// Print benchmark results in a formatted table
    /// </summary>
    public static void PrintBenchmarkResults(List<BenchmarkResult> results)
    {
        if (!results.Any())
        {
            Console.WriteLine("No benchmark results available.");
            return;
        }

        Console.WriteLine("\n=== Image Library Benchmark Results ===");
        Console.WriteLine($"{"Library",-12} {"Operation",-12} {"Format",-8} {"Time (ms)",-10} {"Status",-8}");
        Console.WriteLine(new string('-', 60));

        foreach (var result in results.OrderBy(r => r.Library).ThenBy(r => r.Operation))
        {
            var timeMs = result.Success ? $"{result.ElapsedTime.TotalMilliseconds:F2}" : "N/A";
            var status = result.Success ? "Success" : "Failed";
            
            Console.WriteLine($"{result.Library,-12} {result.Operation,-12} {result.Format,-8} {timeMs,-10} {status,-8}");
            
            if (!result.Success && !string.IsNullOrEmpty(result.Error))
            {
                Console.WriteLine($"    Error: {result.Error}");
            }
        }

        // Summary recommendations
        Console.WriteLine("\n=== Performance Summary ===");
        var successfulResults = results.Where(r => r.Success).ToList();
        
        if (successfulResults.Any())
        {
            var operationGroups = successfulResults.GroupBy(r => r.Operation);
            
            foreach (var group in operationGroups)
            {
                var fastest = group.OrderBy(r => r.ElapsedTime).First();
                var slowest = group.OrderByDescending(r => r.ElapsedTime).First();
                
                Console.WriteLine($"{group.Key}: {fastest.Library} is fastest ({fastest.ElapsedTime.TotalMilliseconds:F2}ms vs {slowest.ElapsedTime.TotalMilliseconds:F2}ms)");
            }
        }
    }

    /// <summary>
    /// Get recommended library for single-page formats based on benchmark results
    /// </summary>
    public static string GetRecommendedLibrary(List<BenchmarkResult> results)
    {
        var successfulResults = results.Where(r => r.Success).ToList();
        
        if (!successfulResults.Any())
        {
            return "ImageSharp"; // Default fallback
        }

        // Score each library based on average performance across operations
        var libraryScores = successfulResults
            .GroupBy(r => r.Library)
            .ToDictionary(g => g.Key, g => g.Average(r => r.ElapsedTime.TotalMilliseconds));

        var bestLibrary = libraryScores.OrderBy(kvp => kvp.Value).First();
        
        Console.WriteLine($"\nRecommendation: Use {bestLibrary.Key} for single-page formats (average: {bestLibrary.Value:F2}ms)");
        
        return bestLibrary.Key;
    }
}