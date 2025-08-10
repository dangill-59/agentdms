using System;
using System.IO;
using System.Threading.Tasks;
using AgentDMS.Core.Services;
using AgentDMS.Core.Utilities;

namespace AgentDMS.UI;

class Program
{
    private static readonly ImageProcessingService _imageProcessor = new(maxConcurrency: Environment.ProcessorCount);
    private static readonly FileUploadService _fileUpload = new();

    static async Task Main(string[] args)
    {
        Console.WriteLine("=== AgentDMS Image Processing Utility ===");
        Console.WriteLine();

        if (args.Length == 0)
        {
            await RunInteractiveMode();
        }
        else
        {
            await ProcessCommandLineArgs(args);
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
        var files = Directory.GetFiles(dirPath, "*.*", SearchOption.AllDirectories)
            .Where(f => supportedExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .ToArray();

        if (files.Length == 0)
        {
            Console.WriteLine("No supported image files found in the directory.");
            return;
        }

        Console.WriteLine($"Found {files.Length} supported files. Processing...");
        
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

    private static async Task ProcessCommandLineArgs(string[] args)
    {
        // Simple command line processing
        if (args[0] == "--help" || args[0] == "-h")
        {
            ShowHelp();
            return;
        }

        if (args.Length >= 2 && args[0] == "--process")
        {
            var filePath = args[1];
            Console.WriteLine($"Processing {filePath}...");
            
            var result = await _imageProcessor.ProcessImageAsync(filePath);
            
            if (result.Success)
            {
                Console.WriteLine("✓ Processing successful!");
                PrintImageDetails(result.ProcessedImage!);
            }
            else
            {
                Console.WriteLine($"✗ Processing failed: {result.Message}");
            }
            
            return;
        }

        Console.WriteLine("Invalid command line arguments. Use --help for usage information.");
    }

    private static void ShowHelp()
    {
        Console.WriteLine("AgentDMS Image Processing Utility");
        Console.WriteLine("Usage:");
        Console.WriteLine("  AgentDMS.UI.exe                    - Run in interactive mode");
        Console.WriteLine("  AgentDMS.UI.exe --process <file>   - Process a single file");
        Console.WriteLine("  AgentDMS.UI.exe --help             - Show this help message");
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
