using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace AgentDMS.Core.Utilities;

/// <summary>
/// Utility for generating thumbnails in various sizes and formats
/// </summary>
public static class ThumbnailGenerator
{
    /// <summary>
    /// Generate a unique filename to avoid conflicts with existing files
    /// </summary>
    /// <param name="baseFilePath">The desired file path</param>
    /// <returns>A unique file path that doesn't conflict with existing files</returns>
    private static string GetUniqueFilePath(string baseFilePath)
    {
        if (!File.Exists(baseFilePath))
        {
            return baseFilePath;
        }

        var directory = Path.GetDirectoryName(baseFilePath) ?? "";
        var fileName = Path.GetFileNameWithoutExtension(baseFilePath);
        var extension = Path.GetExtension(baseFilePath);
        
        int counter = 1;
        string uniquePath;
        
        do
        {
            uniquePath = Path.Combine(directory, $"{fileName}_processed_{counter}{extension}");
            counter++;
        }
        while (File.Exists(uniquePath));
        
        return uniquePath;
    }

    /// <summary>
    /// Convert an image file to PNG format directly without thumbnail generation
    /// </summary>
    /// <param name="inputPath">Path to the input image file</param>
    /// <param name="outputDirectory">Output directory for the PNG file</param>
    /// <param name="customName">Custom name for the PNG file</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Path to the converted PNG file</returns>
    public static async Task<string> ConvertToPngAsync(
        string inputPath, 
        string outputDirectory,
        string? customName = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        Directory.CreateDirectory(outputDirectory);

        var fileName = customName ?? Path.GetFileNameWithoutExtension(inputPath);
        var pngPath = Path.Combine(outputDirectory, $"{fileName}.png");

        // If input is already a PNG, copy it to the output directory
        if (string.Equals(Path.GetExtension(inputPath), ".png", StringComparison.OrdinalIgnoreCase))
        {
            if (inputPath != pngPath)
            {
                // Generate a unique filename to avoid overwriting existing files
                pngPath = GetUniqueFilePath(pngPath);
                File.Copy(inputPath, pngPath, false); // Never overwrite
            }
            return pngPath;
        }

        // Generate a unique filename to avoid overwriting existing files
        pngPath = GetUniqueFilePath(pngPath);

        // Convert to PNG
        using var image = await Image.LoadAsync(inputPath, cancellationToken);
        var pngEncoder = new PngEncoder
        {
            CompressionLevel = PngCompressionLevel.Level1, // Minimal compression for quality
            ColorType = PngColorType.Rgb, // Full RGB color, no palette reduction
            BitDepth = PngBitDepth.Bit8, // 8-bit per channel for full color range
            TransparentColorMode = PngTransparentColorMode.Preserve, // Preserve transparency if present
            Gamma = 1.0f / 2.2f // Standard gamma for proper display
        };

        await image.SaveAsync(pngPath, pngEncoder, cancellationToken);
        return pngPath;
    }

    /// <summary>
    /// Generate a PNG file from an image file (no longer creates thumbnails, returns PNG directly)
    /// </summary>
    public static async Task<string> GenerateThumbnailAsync(
        string inputPath, 
        string outputDirectory,
        int size = 200, 
        string? customName = null,
        CancellationToken cancellationToken = default)
    {
        // Simply convert to PNG instead of generating thumbnails
        return await ConvertToPngAsync(inputPath, outputDirectory, customName, cancellationToken);
    }

    /// <summary>
    /// Generate PNG file copies with different names (no longer creates multiple sizes, just copies)
    /// </summary>
    public static async Task<Dictionary<int, string>> GenerateMultipleThumbnailsAsync(
        string inputPath,
        string outputDirectory,
        int[] sizes,
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<int, string>();
        
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        Directory.CreateDirectory(outputDirectory);

        // Instead of generating different sizes, just create copies with descriptive names
        foreach (var size in sizes)
        {
            var fileName = Path.GetFileNameWithoutExtension(inputPath);
            var customName = $"{fileName}_{size}px";
            
            var pngPath = await ConvertToPngAsync(
                inputPath, outputDirectory, customName, cancellationToken);
            
            results[size] = pngPath;
        }

        return results;
    }

    /// <summary>
    /// Generate PNG copy with HTML (no longer creates thumbnails)
    /// </summary>
    public static async Task<string> GenerateHtmlThumbnailAsync(
        string inputPath,
        string outputDirectory,
        int size = 200,
        CancellationToken cancellationToken = default)
    {
        var pngPath = await ConvertToPngAsync(inputPath, outputDirectory, cancellationToken: cancellationToken);
        
        var fileName = Path.GetFileName(inputPath);
        var pngFileName = Path.GetFileName(pngPath);
        var fileInfo = new FileInfo(inputPath);
        
        var html = $@"
<div class=""image-container"" style=""display: inline-block; margin: 10px; text-align: center; border: 1px solid #ccc; padding: 10px; border-radius: 5px;"">
    <img src=""{pngFileName}"" alt=""{fileName}"" style=""max-width: 100%; height: auto; object-fit: contain;"" />
    <div class=""image-info"" style=""margin-top: 5px; font-size: 12px;"">
        <div><strong>{fileName}</strong></div>
        <div>Size: {fileInfo.Length:N0} bytes</div>
        <div>Modified: {fileInfo.LastWriteTime:yyyy-MM-dd}</div>
    </div>
</div>";

        var htmlPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(inputPath)}.html");
        await File.WriteAllTextAsync(htmlPath, html, cancellationToken);
        
        return htmlPath;
    }

    /// <summary>
    /// Generate a PNG gallery HTML page (no longer creates thumbnails, uses PNG files directly)
    /// </summary>
    public static async Task<string> GenerateThumbnailGalleryAsync(
        IEnumerable<string> imagePaths,
        string outputDirectory,
        int thumbnailSize = 200,
        string title = "Image Gallery",
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        
        var pngTasks = imagePaths.Select(async imagePath =>
        {
            try
            {
                var pngPath = await ConvertToPngAsync(imagePath, outputDirectory, cancellationToken: cancellationToken);
                return new { ImagePath = imagePath, PngPath = pngPath, Success = true };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to convert image for {imagePath}: {ex.Message}");
                return new { ImagePath = imagePath, PngPath = "", Success = false };
            }
        });

        var pngResults = await Task.WhenAll(pngTasks);
        var successfulPngs = pngResults.Where(t => t.Success).ToList();

        var htmlContent = GenerateGalleryHtml(successfulPngs.Select(t => new
        {
            OriginalPath = t.ImagePath,
            PngPath = t.PngPath
        }), title, thumbnailSize);

        var galleryPath = Path.Combine(outputDirectory, "gallery.html");
        await File.WriteAllTextAsync(galleryPath, htmlContent, cancellationToken);
        
        return galleryPath;
    }

    private static (int width, int height) CalculateThumbnailDimensions(int originalWidth, int originalHeight, int maxSize)
    {
        if (originalWidth <= maxSize && originalHeight <= maxSize)
        {
            return (originalWidth, originalHeight);
        }

        double aspectRatio = (double)originalWidth / originalHeight;
        
        if (originalWidth > originalHeight)
        {
            return (maxSize, (int)(maxSize / aspectRatio));
        }
        else
        {
            return ((int)(maxSize * aspectRatio), maxSize);
        }
    }

    private static string GenerateGalleryHtml(IEnumerable<dynamic> images, string title, int thumbnailSize)
    {
        var imageHtml = string.Join("\n", images.Select(t => $@"
        <div class=""image-item"" style=""display: inline-block; margin: 10px; text-align: center; border: 1px solid #ccc; padding: 10px; border-radius: 5px; vertical-align: top;"">
            <img src=""{Path.GetFileName(t.PngPath)}"" alt=""{Path.GetFileName(t.OriginalPath)}"" 
                 style=""max-width: {thumbnailSize}px; max-height: {thumbnailSize}px; object-fit: contain; cursor: pointer;"" 
                 onclick=""openFullImage('{t.PngPath.Replace("\\", "/")}')"" />
            <div class=""image-info"" style=""margin-top: 5px; font-size: 12px; max-width: {thumbnailSize}px; word-wrap: break-word;"">
                <div><strong>{Path.GetFileName(t.OriginalPath)}</strong></div>
            </div>
        </div>"));

        return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>{title}</title>
    <style>
        body {{ font-family: Arial, sans-serif; margin: 20px; background-color: #f5f5f5; }}
        .gallery-container {{ max-width: 1200px; margin: 0 auto; text-align: center; }}
        h1 {{ color: #333; margin-bottom: 30px; }}
        .image-item:hover {{ transform: scale(1.05); transition: transform 0.2s; }}
        #fullImageOverlay {{ 
            display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; 
            background-color: rgba(0,0,0,0.8); z-index: 1000; 
        }}
        #fullImageContainer {{ 
            position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); 
            max-width: 90%; max-height: 90%; text-align: center; 
        }}
        #fullImage {{ 
            max-width: 100%; max-height: 100%; 
            transform-origin: center; 
            transition: transform 0.2s ease;
        }}
        .zoom-controls {{
            position: absolute; bottom: 20px; left: 50%; transform: translateX(-50%);
            background: rgba(255,255,255,0.9); padding: 10px; border-radius: 20px;
            display: flex; align-items: center; gap: 10px;
        }}
        .zoom-slider {{ width: 200px; }}
        .close-btn {{ 
            position: absolute; top: 10px; right: 20px; color: white; font-size: 30px; 
            cursor: pointer; user-select: none; 
        }}
    </style>
</head>
<body>
    <div class=""gallery-container"">
        <h1>{title}</h1>
        <div class=""images"">
            {imageHtml}
        </div>
    </div>
    
    <div id=""fullImageOverlay"" onclick=""closeFullImage(event)"">
        <div class=""close-btn"" onclick=""closeFullImage(event)"">&times;</div>
        <div id=""fullImageContainer"">
            <img id=""fullImage"" src="""" alt="""" />
            <div class=""zoom-controls"">
                <span>-</span>
                <input type=""range"" class=""zoom-slider"" id=""zoomSlider"" min=""0.1"" max=""3"" step=""0.1"" value=""1"" />
                <span>+</span>
                <button onclick=""resetZoom()"" style=""margin-left: 10px;"">Reset</button>
            </div>
        </div>
    </div>

    <script>
        let currentZoom = 1;
        
        function openFullImage(imagePath) {{
            document.getElementById('fullImage').src = imagePath;
            document.getElementById('fullImageOverlay').style.display = 'block';
            resetZoom();
        }}
        
        function closeFullImage(event) {{
            if (event && event.target !== event.currentTarget && !event.target.classList.contains('close-btn')) {{
                return; // Don't close if clicking on the image or controls
            }}
            document.getElementById('fullImageOverlay').style.display = 'none';
        }}
        
        function resetZoom() {{
            currentZoom = 1;
            document.getElementById('fullImage').style.transform = 'scale(1)';
            document.getElementById('zoomSlider').value = 1;
        }}
        
        document.getElementById('zoomSlider').addEventListener('input', function(e) {{
            currentZoom = parseFloat(e.target.value);
            document.getElementById('fullImage').style.transform = `scale(${{currentZoom}})`;
        }});
        
        document.addEventListener('keydown', function(event) {{
            if (event.key === 'Escape') {{
                closeFullImage(event);
            }}
        }});
    </script>
</body>
</html>";
    }
}