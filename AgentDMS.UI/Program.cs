using System;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Services;
using AgentDMS.Core.Utilities;
using AgentDMS.Core.Models;

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
        var results = await _imageProcessor.ProcessMultipleImagesAsync(files, null, progress);
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
        var results = await _imageProcessor.ProcessMultipleImagesAsync(files, null, progress);
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
        Console.WriteLine("\n=== Batch Processing Summary Statistics ===");
        
        var totalFiles = results.Count;
        var successfulResults = results.Where(r => r.Success).ToList();
        var failedResults = results.Where(r => !r.Success).ToList();
        var successfulWithMetrics = successfulResults.Where(r => r.Metrics != null).ToList();
        
        // Overall Statistics
        Console.WriteLine($"Total files attempted: {totalFiles}");
        Console.WriteLine($"Successful: {successfulResults.Count} ({(successfulResults.Count * 100.0 / totalFiles):F1}%)");
        Console.WriteLine($"Failed: {failedResults.Count} ({(failedResults.Count * 100.0 / totalFiles):F1}%)");
        
        if (!successfulWithMetrics.Any())
        {
            Console.WriteLine("No detailed metrics available for successful results.");
            return;
        }

        var metrics = successfulWithMetrics.Select(r => r.Metrics!).ToList();
        Console.WriteLine($"Files with detailed metrics: {metrics.Count}");

        // Total Processing Time Statistics
        var totalProcessingTimes = metrics.Where(m => m.TotalProcessingTime.HasValue)
            .Select(m => m.TotalProcessingTime!.Value.TotalMilliseconds).ToList();
        
        if (totalProcessingTimes.Any())
        {
            Console.WriteLine("\n--- Total Processing Time Statistics ---");
            Console.WriteLine($"Average: {totalProcessingTimes.Average():F0} ms");
            Console.WriteLine($"Minimum: {totalProcessingTimes.Min():F0} ms");
            Console.WriteLine($"Maximum: {totalProcessingTimes.Max():F0} ms");
            Console.WriteLine($"Median: {GetMedian(totalProcessingTimes):F0} ms");
            Console.WriteLine($"Total cumulative: {totalProcessingTimes.Sum():F0} ms ({totalProcessingTimes.Sum() / 1000:F1} seconds)");
        }

        // Detailed Step Statistics
        Console.WriteLine("\n--- Processing Step Statistics ---");
        
        PrintStepStatistics("File Load", metrics.Where(m => m.FileLoadTime.HasValue).Select(m => m.FileLoadTime!.Value.TotalMilliseconds));
        PrintStepStatistics("Image Decode", metrics.Where(m => m.ImageDecodeTime.HasValue).Select(m => m.ImageDecodeTime!.Value.TotalMilliseconds));
        PrintStepStatistics("Conversion", metrics.Where(m => m.ConversionTime.HasValue).Select(m => m.ConversionTime!.Value.TotalMilliseconds));
        PrintStepStatistics("Thumbnail Generation", metrics.Where(m => m.ThumbnailGenerationTime.HasValue).Select(m => m.ThumbnailGenerationTime!.Value.TotalMilliseconds));
        PrintStepStatistics("AI Analysis", metrics.Where(m => m.AiAnalysisTime.HasValue).Select(m => m.AiAnalysisTime!.Value.TotalMilliseconds));
        PrintStepStatistics("OCR Processing", metrics.Where(m => m.OcrProcessingTime.HasValue).Select(m => m.OcrProcessingTime!.Value.TotalMilliseconds));

        // Performance Insights
        var stepTimes = new Dictionary<string, double>();
        
        if (metrics.Any(m => m.FileLoadTime.HasValue))
            stepTimes["File Load"] = metrics.Where(m => m.FileLoadTime.HasValue).Average(m => m.FileLoadTime!.Value.TotalMilliseconds);
        
        if (metrics.Any(m => m.ImageDecodeTime.HasValue))
            stepTimes["Image Decode"] = metrics.Where(m => m.ImageDecodeTime.HasValue).Average(m => m.ImageDecodeTime!.Value.TotalMilliseconds);
        
        if (metrics.Any(m => m.ConversionTime.HasValue))
            stepTimes["Conversion"] = metrics.Where(m => m.ConversionTime.HasValue).Average(m => m.ConversionTime!.Value.TotalMilliseconds);
        
        if (metrics.Any(m => m.ThumbnailGenerationTime.HasValue))
            stepTimes["Thumbnail Generation"] = metrics.Where(m => m.ThumbnailGenerationTime.HasValue).Average(m => m.ThumbnailGenerationTime!.Value.TotalMilliseconds);
        
        if (metrics.Any(m => m.AiAnalysisTime.HasValue))
            stepTimes["AI Analysis"] = metrics.Where(m => m.AiAnalysisTime.HasValue).Average(m => m.AiAnalysisTime!.Value.TotalMilliseconds);
        
        if (metrics.Any(m => m.OcrProcessingTime.HasValue))
            stepTimes["OCR Processing"] = metrics.Where(m => m.OcrProcessingTime.HasValue).Average(m => m.OcrProcessingTime!.Value.TotalMilliseconds);

        if (stepTimes.Any())
        {
            Console.WriteLine("\n--- Performance Insights ---");
            Console.WriteLine("Slowest processing steps (by average time):");
            var sortedSteps = stepTimes.OrderByDescending(kvp => kvp.Value).Take(5);
            foreach (var step in sortedSteps)
            {
                Console.WriteLine($"  {step.Key}: {step.Value:F0} ms");
            }
            
            // Show step time distribution
            var totalStepTime = stepTimes.Values.Sum();
            if (totalStepTime > 0)
            {
                Console.WriteLine("\nProcessing time distribution:");
                foreach (var step in stepTimes.OrderByDescending(kvp => kvp.Value))
                {
                    var percentage = (step.Value / totalStepTime) * 100;
                    Console.WriteLine($"  {step.Key}: {percentage:F1}%");
                }
            }
        }
        
        // OCR Statistics
        var resultsWithOcr = successfulResults.Where(r => !string.IsNullOrEmpty(r.ExtractedText)).ToList();
        var ocrMetrics = metrics.Where(m => m.OcrProcessingTime.HasValue || !string.IsNullOrEmpty(m.OcrMethod)).ToList();
        
        if (resultsWithOcr.Any() || ocrMetrics.Any())
        {
            Console.WriteLine("\n--- OCR Processing Statistics ---");
            Console.WriteLine($"Files with OCR processing: {resultsWithOcr.Count}");
            
            if (resultsWithOcr.Any())
            {
                var successRate = (resultsWithOcr.Count * 100.0) / totalFiles;
                Console.WriteLine($"OCR success rate: {successRate:F1}%");
                
                var textLengths = resultsWithOcr.Where(r => r.ExtractedText != null)
                    .Select(r => r.ExtractedText!.Length).ToList();
                
                if (textLengths.Any())
                {
                    Console.WriteLine($"Extracted text statistics:");
                    Console.WriteLine($"  Average text length: {textLengths.Average():F0} characters");
                    Console.WriteLine($"  Min text length: {textLengths.Min()} characters");
                    Console.WriteLine($"  Max text length: {textLengths.Max()} characters");
                    Console.WriteLine($"  Total characters extracted: {textLengths.Sum():N0}");
                }
            }
            
            // OCR Method breakdown
            var ocrMethodCounts = ocrMetrics.Where(m => !string.IsNullOrEmpty(m.OcrMethod))
                .GroupBy(m => m.OcrMethod)
                .ToDictionary(g => g.Key!, g => g.Count());
            
            if (ocrMethodCounts.Any())
            {
                Console.WriteLine($"OCR methods used:");
                foreach (var method in ocrMethodCounts.OrderByDescending(kvp => kvp.Value))
                {
                    var percentage = (method.Value * 100.0) / ocrMetrics.Count;
                    Console.WriteLine($"  {method.Key}: {method.Value} files ({percentage:F1}%)");
                }
            }
            
            // OCR Confidence statistics
            var confidenceScores = ocrMetrics.Where(m => m.OcrConfidence.HasValue)
                .Select(m => m.OcrConfidence!.Value).ToList();
            
            if (confidenceScores.Any())
            {
                Console.WriteLine($"OCR confidence statistics:");
                Console.WriteLine($"  Average confidence: {confidenceScores.Average():F2}");
                Console.WriteLine($"  Min confidence: {confidenceScores.Min():F2}");
                Console.WriteLine($"  Max confidence: {confidenceScores.Max():F2}");
                Console.WriteLine($"  Files with high confidence (>0.8): {confidenceScores.Count(c => c > 0.8)}");
            }
        }
        
        // Mistral Configuration Statistics
        var mistralMetrics = ocrMetrics.Where(m => !string.IsNullOrEmpty(m.MistralModel)).ToList();
        
        if (mistralMetrics.Any())
        {
            Console.WriteLine("\n--- Mistral Integration Statistics ---");
            Console.WriteLine($"Files processed with Mistral: {mistralMetrics.Count}");
            
            // Mistral Model breakdown
            var modelCounts = mistralMetrics.GroupBy(m => m.MistralModel)
                .ToDictionary(g => g.Key!, g => g.Count());
            
            Console.WriteLine($"Mistral models used:");
            foreach (var model in modelCounts.OrderByDescending(kvp => kvp.Value))
            {
                var percentage = (model.Value * 100.0) / mistralMetrics.Count;
                Console.WriteLine($"  {model.Key}: {model.Value} files ({percentage:F1}%)");
            }
            
            // Mistral processing times
            var mistralOcrTimes = mistralMetrics.Where(m => m.OcrProcessingTime.HasValue)
                .Select(m => m.OcrProcessingTime!.Value.TotalMilliseconds).ToList();
            
            if (mistralOcrTimes.Any())
            {
                Console.WriteLine($"Mistral OCR processing times:");
                Console.WriteLine($"  Average: {mistralOcrTimes.Average():F0} ms");
                Console.WriteLine($"  Min: {mistralOcrTimes.Min():F0} ms");
                Console.WriteLine($"  Max: {mistralOcrTimes.Max():F0} ms");
                Console.WriteLine($"  Total: {mistralOcrTimes.Sum():F0} ms ({mistralOcrTimes.Sum() / 1000:F1} seconds)");
            }
        }
    }

    private static void PrintStepStatistics(string stepName, IEnumerable<double> times)
    {
        var timesList = times.ToList();
        if (!timesList.Any()) return;

        Console.WriteLine($"{stepName}:");
        Console.WriteLine($"  Count: {timesList.Count} files");
        Console.WriteLine($"  Average: {timesList.Average():F0} ms");
        Console.WriteLine($"  Min: {timesList.Min():F0} ms");
        Console.WriteLine($"  Max: {timesList.Max():F0} ms");
        Console.WriteLine($"  Median: {GetMedian(timesList):F0} ms");
        Console.WriteLine($"  Total: {timesList.Sum():F0} ms");
    }

    private static double GetMedian(List<double> values)
    {
        if (!values.Any()) return 0;
        
        var sorted = values.OrderBy(x => x).ToList();
        var count = sorted.Count;
        
        if (count % 2 == 0)
        {
            return (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;
        }
        else
        {
            return sorted[count / 2];
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
        if (metrics.AiAnalysisTime.HasValue)
            Console.WriteLine($"AI analysis time: {metrics.AiAnalysisTime.Value.TotalMilliseconds:F0} ms");
        if (metrics.OcrProcessingTime.HasValue)
            Console.WriteLine($"OCR processing time: {metrics.OcrProcessingTime.Value.TotalMilliseconds:F0} ms");
        if (metrics.TotalProcessingTime.HasValue)
            Console.WriteLine($"Total processing time: {metrics.TotalProcessingTime.Value.TotalMilliseconds:F0} ms");
        
        // OCR and Mistral information
        if (!string.IsNullOrEmpty(metrics.OcrMethod))
            Console.WriteLine($"OCR method: {metrics.OcrMethod}");
        if (!string.IsNullOrEmpty(metrics.MistralModel))
            Console.WriteLine($"Mistral model: {metrics.MistralModel}");
        if (metrics.OcrConfidence.HasValue)
            Console.WriteLine($"OCR confidence: {metrics.OcrConfidence.Value:F2}");
        if (metrics.ExtractedTextLength.HasValue)
            Console.WriteLine($"Extracted text length: {metrics.ExtractedTextLength.Value} characters");
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
