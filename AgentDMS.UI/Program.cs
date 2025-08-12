using System;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Services;
using AgentDMS.Core.Utilities;

namespace AgentDMS.UI;

class Program
{
    private static ImageProcessingService _imageProcessor = null!;
    private static readonly FileUploadService _fileUpload = new();
    private static CliOptions _options = null!;

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== AgentDMS Image Processing Utility ===");
        Console.WriteLine();

        _options = CliOptions.Parse(args);
        _imageProcessor = new ImageProcessingService(
            maxConcurrency: _options.MaxConcurrency, 
            outputDirectory: _options.OutputDirectory);

        if (args.Length == 0)
        {
            await RunInteractiveMode();
        }
        else
        {
            await ProcessCommandLineArgs(_options);
        }
    }

    private static async Task RunInteractiveMode()
    {
        while (true)
        {
            Console.WriteLine("\nAvailable options:");
            Console.WriteLine("1. Process single file");
            Console.WriteLine("2. Process multiple files from directory");
            Console.WriteLine("3. Generate thumbnail gallery");
            Console.WriteLine("4. List supported formats");
            Console.WriteLine("5. Exit");
            Console.Write("\nSelect an option (1-5): ");

            var choice = Console.ReadLine();

            try
            {
                switch (choice)
                {
                    case "1":
                        await ProcessSingleFile();
                        break;
                    case "2":
                        await ProcessDirectory();
                        break;
                    case "3":
                        await GenerateGallery();
                        break;
                    case "4":
                        ShowSupportedFormats();
                        break;
                    case "5":
                        Console.WriteLine("Goodbye!");
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private static async Task ProcessSingleFile()
    {
        Console.Write("Enter the path to the image file: ");
        var filePath = Console.ReadLine()?.Trim('"');

        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            Console.WriteLine("File not found or invalid path.");
            return;
        }

        Console.WriteLine($"Processing {filePath}...");
        var startTime = DateTime.UtcNow;

        var result = await _imageProcessor.ProcessImageAsync(filePath);

        var processingTime = DateTime.UtcNow - startTime;
        Console.WriteLine($"Processing completed in {processingTime.TotalSeconds:F2} seconds");

        if (result.Success)
        {
            Console.WriteLine("✓ Processing successful!");
            PrintImageDetails(result.ProcessedImage!);

            if (result.SplitPages?.Any() == true)
            {
                Console.WriteLine($"\nSplit into {result.SplitPages.Count} pages:");
                foreach (var page in result.SplitPages)
                {
                    Console.WriteLine($"  - {page.FileName}");
                }
            }
        }
        else
        {
            Console.WriteLine($"✗ Processing failed: {result.Message}");
        }
    }

    private static async Task ProcessDirectory()
    {
        Console.Write("Enter the directory path: ");
        var dirPath = Console.ReadLine()?.Trim('"');

        if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
        {
            Console.WriteLine("Directory not found or invalid path.");
            return;
        }

        var supportedExtensions = ImageProcessingService.GetSupportedExtensions();
        var allFiles = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        // Pre-filter files by size
        var maxSizeBytes = _options.MaxFileSizeMB * 1024 * 1024;
        var files = allFiles
            .Where(f =>
            {
                try
                {
                    var fileInfo = new FileInfo(f);
                    if (fileInfo.Length > maxSizeBytes)
                    {
                        Console.WriteLine($"Skipping large file: {f} ({fileInfo.Length / (1024.0 * 1024.0):F1} MB)");
                        return false;
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .ToArray();

        if (files.Length == 0)
        {
            Console.WriteLine("No supported image files found in the directory (within size limits).");
            return;
        }

        Console.WriteLine($"Found {files.Length} supported files (max concurrency: {_options.MaxConcurrency}). Processing...");
        
        var progress = new Progress<int>(count => 
        {
            Console.Write($"\rProcessed: {count}/{files.Length}");
        });

        var startTime = DateTime.UtcNow;
        var results = await _imageProcessor.ProcessMultipleImagesAsync(files, progress);
        var processingTime = DateTime.UtcNow - startTime;

        Console.WriteLine($"\n\nBatch processing completed in {processingTime.TotalSeconds:F2} seconds");
        
        var successful = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);

        Console.WriteLine($"✓ Successful: {successful}");
        Console.WriteLine($"✗ Failed: {failed}");

        // Log batch metrics if enabled
        if (_options.EnableMetricsLogging)
        {
            LogBatchMetrics(results);
        }

        if (failed > 0)
        {
            Console.WriteLine("\nFailed files:");
            foreach (var failedResult in results.Where(r => !r.Success))
            {
                Console.WriteLine($"  - {failedResult.Message}");
            }
        }
    }

    private static async Task GenerateGallery()
    {
        Console.Write("Enter the directory path containing images: ");
        var dirPath = Console.ReadLine()?.Trim('"');

        if (string.IsNullOrEmpty(dirPath) || !Directory.Exists(dirPath))
        {
            Console.WriteLine("Directory not found or invalid path.");
            return;
        }

        var supportedExtensions = ImageProcessingService.GetSupportedExtensions().Where(ext => ext != ".pdf").ToArray();
        var imageFiles = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToArray();

        if (imageFiles.Length == 0)
        {
            Console.WriteLine("No supported image files found in the directory.");
            return;
        }

        Console.Write("Enter output directory (or press Enter for default): ");
        var outputDir = Console.ReadLine()?.Trim('"');
        
        if (string.IsNullOrEmpty(outputDir))
        {
            outputDir = Path.Combine(Path.GetTempPath(), "AgentDMS_Gallery");
        }

        Console.WriteLine($"Generating thumbnail gallery for {imageFiles.Length} images...");

        var galleryPath = await ThumbnailGenerator.GenerateThumbnailGalleryAsync(
            imageFiles, 
            outputDir, 
            thumbnailSize: 200, 
            title: "AgentDMS Image Gallery");

        Console.WriteLine($"✓ Gallery generated successfully!");
        Console.WriteLine($"Gallery HTML: {galleryPath}");
        Console.WriteLine($"Output directory: {outputDir}");
    }

    private static void ShowSupportedFormats()
    {
        var formats = ImageProcessingService.GetSupportedExtensions();
        Console.WriteLine("\nSupported image formats:");
        foreach (var format in formats)
        {
            Console.WriteLine($"  - {format.ToUpperInvariant()}");
        }
    }

    private static async Task ProcessCommandLineArgs(CliOptions options)
    {
        if (options.ShowHelp)
        {
            ShowHelp();
            return;
        }

        if (options.ShowFormats)
        {
            ShowSupportedFormats();
            return;
        }

        if (!string.IsNullOrEmpty(options.ProcessFile))
        {
            await ProcessSingleFile(options.ProcessFile);
            return;
        }

        if (!string.IsNullOrEmpty(options.ProcessDirectory))
        {
            await ProcessDirectoryBatch(options.ProcessDirectory);
            return;
        }

        if (!string.IsNullOrEmpty(options.BenchmarkFile))
        {
            await RunBenchmark(options.BenchmarkFile);
            return;
        }

        Console.WriteLine("Invalid command line arguments. Use --help for usage information.");
    }

    private static async Task ProcessSingleFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine("File not found or invalid path.");
            return;
        }

        // Check file size
        var fileInfo = new FileInfo(filePath);
        var maxSizeBytes = _options.MaxFileSizeMB * 1024 * 1024;
        if (fileInfo.Length > maxSizeBytes)
        {
            Console.WriteLine($"File too large: {fileInfo.Length / (1024.0 * 1024.0):F1} MB (limit: {_options.MaxFileSizeMB} MB)");
            return;
        }

        Console.WriteLine($"Processing {filePath}...");
        var startTime = DateTime.UtcNow;

        var result = await _imageProcessor.ProcessImageAsync(filePath);

        var processingTime = DateTime.UtcNow - startTime;
        Console.WriteLine($"Processing completed in {processingTime.TotalSeconds:F2} seconds");

        if (result.Success)
        {
            Console.WriteLine("✓ Processing successful!");
            PrintImageDetails(result.ProcessedImage!);

            if (result.SplitPages?.Any() == true)
            {
                Console.WriteLine($"\nSplit into {result.SplitPages.Count} pages:");
                foreach (var page in result.SplitPages)
                {
                    Console.WriteLine($"  - {page.FileName}");
                }
            }

            // Log single file metrics if enabled
            if (_options.EnableMetricsLogging && result.Metrics != null)
            {
                LogSingleFileMetrics(result.Metrics);
            }
        }
        else
        {
            Console.WriteLine($"✗ Processing failed: {result.Message}");
        }
    }

    private static async Task ProcessDirectoryBatch(string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            Console.WriteLine("Directory not found or invalid path.");
            return;
        }

        var supportedExtensions = ImageProcessingService.GetSupportedExtensions();
        var allFiles = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()));

        // Pre-filter files by size
        var maxSizeBytes = _options.MaxFileSizeMB * 1024 * 1024;
        var files = allFiles
            .Where(f =>
            {
                try
                {
                    var fileInfo = new FileInfo(f);
                    if (fileInfo.Length > maxSizeBytes)
                    {
                        Console.WriteLine($"Skipping large file: {f} ({fileInfo.Length / (1024.0 * 1024.0):F1} MB)");
                        return false;
                    }
                    return true;
                }
                catch
                {
                    return false;
                }
            })
            .ToArray();

        if (files.Length == 0)
        {
            Console.WriteLine("No supported image files found in the directory (within size limits).");
            return;
        }

        Console.WriteLine($"Found {files.Length} supported files (max concurrency: {_options.MaxConcurrency}). Processing...");
        
        var progress = new Progress<int>(count => 
        {
            Console.Write($"\rProcessed: {count}/{files.Length}");
        });

        var startTime = DateTime.UtcNow;
        var results = await _imageProcessor.ProcessMultipleImagesAsync(files, progress);
        var processingTime = DateTime.UtcNow - startTime;

        Console.WriteLine($"\n\nBatch processing completed in {processingTime.TotalSeconds:F2} seconds");
        
        var successful = results.Count(r => r.Success);
        var failed = results.Count(r => !r.Success);

        Console.WriteLine($"✓ Successful: {successful}");
        Console.WriteLine($"✗ Failed: {failed}");

        // Log batch metrics if enabled
        if (_options.EnableMetricsLogging)
        {
            LogBatchMetrics(results);
        }

        if (failed > 0)
        {
            Console.WriteLine("\nFailed files:");
            foreach (var failedResult in results.Where(r => !r.Success))
            {
                Console.WriteLine($"  - {failedResult.Message}");
            }
        }
    }

    private static async Task RunBenchmark(string benchmarkFile)
    {
        if (!File.Exists(benchmarkFile))
        {
            Console.WriteLine("Benchmark file not found or invalid path.");
            return;
        }

        var fileExtension = Path.GetExtension(benchmarkFile).ToLowerInvariant();
        var supportedFormats = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
        
        if (!supportedFormats.Contains(fileExtension))
        {
            Console.WriteLine($"Benchmark only supports single-page formats: {string.Join(", ", supportedFormats)}");
            return;
        }

        Console.WriteLine("Running image library benchmark...");
        Console.WriteLine($"Test file: {benchmarkFile}");
        
        var benchmarkOutputDir = _options.OutputDirectory ?? Path.Combine(Path.GetTempPath(), "AgentDMS_Benchmark");
        Directory.CreateDirectory(benchmarkOutputDir);

        try
        {
            var results = await AgentDMS.Core.Utilities.ImageLibraryBenchmark.BenchmarkSinglePageFormatsAsync(
                benchmarkFile, benchmarkOutputDir);
            
            AgentDMS.Core.Utilities.ImageLibraryBenchmark.PrintBenchmarkResults(results);
            var recommendedLibrary = AgentDMS.Core.Utilities.ImageLibraryBenchmark.GetRecommendedLibrary(results);
            
            Console.WriteLine($"\nBenchmark completed. Results saved to: {benchmarkOutputDir}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Benchmark failed: {ex.Message}");
        }
    }

    private static void LogBatchMetrics(List<AgentDMS.Core.Models.ProcessingResult> results)
    {
        Console.WriteLine("\n=== Processing Metrics Summary ===");
        
        var successfulResults = results.Where(r => r.Success && r.Metrics != null).ToList();
        if (!successfulResults.Any())
        {
            Console.WriteLine("No metrics available for successful results.");
            return;
        }

        var metrics = successfulResults.Select(r => r.Metrics!).ToList();

        // Aggregate timing statistics
        Console.WriteLine($"Total files processed: {successfulResults.Count}");
        Console.WriteLine($"Average processing time: {metrics.Average(m => m.TotalProcessingTime?.TotalMilliseconds ?? 0):F0} ms");
        
        if (metrics.Any(m => m.FileLoadTime.HasValue))
        {
            var avgFileLoad = metrics.Where(m => m.FileLoadTime.HasValue).Average(m => m.FileLoadTime!.Value.TotalMilliseconds);
            Console.WriteLine($"Average file load time: {avgFileLoad:F0} ms");
        }

        if (metrics.Any(m => m.ImageDecodeTime.HasValue))
        {
            var avgDecode = metrics.Where(m => m.ImageDecodeTime.HasValue).Average(m => m.ImageDecodeTime!.Value.TotalMilliseconds);
            Console.WriteLine($"Average image decode time: {avgDecode:F0} ms");
        }

        if (metrics.Any(m => m.ConversionTime.HasValue))
        {
            var avgConversion = metrics.Where(m => m.ConversionTime.HasValue).Average(m => m.ConversionTime!.Value.TotalMilliseconds);
            Console.WriteLine($"Average conversion time: {avgConversion:F0} ms");
        }

        if (metrics.Any(m => m.ThumbnailGenerationTime.HasValue))
        {
            var avgThumbnail = metrics.Where(m => m.ThumbnailGenerationTime.HasValue).Average(m => m.ThumbnailGenerationTime!.Value.TotalMilliseconds);
            Console.WriteLine($"Average thumbnail generation time: {avgThumbnail:F0} ms");
        }

        // Identify slowest steps on average
        var stepTimes = new Dictionary<string, double>();
        
        if (metrics.Any(m => m.FileLoadTime.HasValue))
            stepTimes["File Load"] = metrics.Where(m => m.FileLoadTime.HasValue).Average(m => m.FileLoadTime!.Value.TotalMilliseconds);
        
        if (metrics.Any(m => m.ImageDecodeTime.HasValue))
            stepTimes["Image Decode"] = metrics.Where(m => m.ImageDecodeTime.HasValue).Average(m => m.ImageDecodeTime!.Value.TotalMilliseconds);
        
        if (metrics.Any(m => m.ConversionTime.HasValue))
            stepTimes["Conversion"] = metrics.Where(m => m.ConversionTime.HasValue).Average(m => m.ConversionTime!.Value.TotalMilliseconds);
        
        if (metrics.Any(m => m.ThumbnailGenerationTime.HasValue))
            stepTimes["Thumbnail Generation"] = metrics.Where(m => m.ThumbnailGenerationTime.HasValue).Average(m => m.ThumbnailGenerationTime!.Value.TotalMilliseconds);

        if (stepTimes.Any())
        {
            Console.WriteLine("\nSlowest processing steps (average):");
            var sortedSteps = stepTimes.OrderByDescending(kvp => kvp.Value).Take(3);
            foreach (var step in sortedSteps)
            {
                Console.WriteLine($"  {step.Key}: {step.Value:F0} ms");
            }
        }
    }

    private static void LogSingleFileMetrics(AgentDMS.Core.Models.ProcessingMetrics metrics)
    {
        Console.WriteLine("\n=== Processing Metrics ===");
        if (metrics.FileLoadTime.HasValue)
            Console.WriteLine($"File load time: {metrics.FileLoadTime.Value.TotalMilliseconds:F0} ms");
        if (metrics.ImageDecodeTime.HasValue)
            Console.WriteLine($"Image decode time: {metrics.ImageDecodeTime.Value.TotalMilliseconds:F0} ms");
        if (metrics.ConversionTime.HasValue)
            Console.WriteLine($"Conversion time: {metrics.ConversionTime.Value.TotalMilliseconds:F0} ms");
        if (metrics.ThumbnailGenerationTime.HasValue)
            Console.WriteLine($"Thumbnail generation time: {metrics.ThumbnailGenerationTime.Value.TotalMilliseconds:F0} ms");
        if (metrics.TotalProcessingTime.HasValue)
            Console.WriteLine($"Total processing time: {metrics.TotalProcessingTime.Value.TotalMilliseconds:F0} ms");
    }

    private static void ShowHelp()
    {
        Console.WriteLine("AgentDMS Image Processing Utility");
        Console.WriteLine("Usage:");
        Console.WriteLine("  AgentDMS.UI.exe                              - Run in interactive mode");
        Console.WriteLine("  AgentDMS.UI.exe --process <file>             - Process a single file");
        Console.WriteLine("  AgentDMS.UI.exe --directory <path>           - Process all files in directory");
        Console.WriteLine("  AgentDMS.UI.exe --benchmark <file>           - Benchmark library performance");
        Console.WriteLine("  AgentDMS.UI.exe --help                       - Show this help message");
        Console.WriteLine("  AgentDMS.UI.exe --formats                    - Show supported formats");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  -c, --max-concurrency <num>       Maximum concurrent processing tasks (default: CPU count)");
        Console.WriteLine("  -s, --max-file-size <MB>          Maximum file size in MB to process (default: 100)");
        Console.WriteLine("  -o, --output <path>                Output directory (default: temp directory)");
        Console.WriteLine("  -t, --thumbnail-size <pixels>     Thumbnail size in pixels (default: 200, range: 50-1000)");
        Console.WriteLine("  -b, --benchmark <file>             Benchmark ImageSharp vs Magick.NET performance");
        Console.WriteLine("      --no-metrics                  Disable metrics logging");
        Console.WriteLine();
        Console.WriteLine("Environment Variables:");
        Console.WriteLine("  AGENTDMS_MAX_CONCURRENCY          Set max concurrency");
        Console.WriteLine("  AGENTDMS_MAX_FILE_SIZE_MB          Set max file size in MB");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  AgentDMS.UI.exe --process image.jpg");
        Console.WriteLine("  AgentDMS.UI.exe --directory \"C:\\Images\" --max-concurrency 8");
        Console.WriteLine("  AgentDMS.UI.exe --benchmark image.jpg --output \"C:\\BenchmarkResults\"");
        Console.WriteLine("  AgentDMS.UI.exe --process file.pdf --output \"C:\\Output\" --no-metrics");
        Console.WriteLine();
        Console.WriteLine("Supported formats: " + string.Join(", ", ImageProcessingService.GetSupportedExtensions()));
    }

    private static void PrintImageDetails(AgentDMS.Core.Models.ImageFile imageFile)
    {
        Console.WriteLine($"File: {imageFile.FileName}");
        Console.WriteLine($"Original Format: {imageFile.OriginalFormat}");
        Console.WriteLine($"Dimensions: {imageFile.Width}x{imageFile.Height}");
        Console.WriteLine($"File Size: {imageFile.FileSize:N0} bytes");
        Console.WriteLine($"Multi-page: {imageFile.IsMultiPage} (Pages: {imageFile.PageCount})");
        
        if (!string.IsNullOrEmpty(imageFile.ConvertedPngPath))
        {
            Console.WriteLine($"PNG Version: {imageFile.ConvertedPngPath}");
        }
        
        if (!string.IsNullOrEmpty(imageFile.ThumbnailPath))
        {
            Console.WriteLine($"Thumbnail: {imageFile.ThumbnailPath}");
        }
    }
}
