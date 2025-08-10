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
    /// Generate a thumbnail from an image file
    /// </summary>
    public static async Task<string> GenerateThumbnailAsync(
        string inputPath, 
        string outputDirectory,
        int size = 200, 
        string? customName = null,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(inputPath))
        {
            throw new FileNotFoundException($"Input file not found: {inputPath}");
        }

        Directory.CreateDirectory(outputDirectory);

        var fileName = customName ?? Path.GetFileNameWithoutExtension(inputPath);
        var thumbnailPath = Path.Combine(outputDirectory, $"thumb_{fileName}.png");

        using var image = await Image.LoadAsync(inputPath, cancellationToken);
        
        // Calculate dimensions to maintain aspect ratio
        var (width, height) = CalculateThumbnailDimensions(image.Width, image.Height, size);
        
        using var thumbnail = image.Clone(x => x.Resize(width, height));
        await thumbnail.SaveAsPngAsync(thumbnailPath, cancellationToken);

        return thumbnailPath;
    }

    /// <summary>
    /// Generate multiple thumbnail sizes from an image
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

        using var image = await Image.LoadAsync(inputPath, cancellationToken);
        
        foreach (var size in sizes)
        {
            var fileName = Path.GetFileNameWithoutExtension(inputPath);
            var thumbnailPath = Path.Combine(outputDirectory, $"thumb_{fileName}_{size}px.png");
            
            var (width, height) = CalculateThumbnailDimensions(image.Width, image.Height, size);
            
            using var thumbnail = image.Clone(x => x.Resize(width, height));
            await thumbnail.SaveAsPngAsync(thumbnailPath, cancellationToken);
            
            results[size] = thumbnailPath;
        }

        return results;
    }

    /// <summary>
    /// Generate a thumbnail with browser-friendly HTML output
    /// </summary>
    public static async Task<string> GenerateHtmlThumbnailAsync(
        string inputPath,
        string outputDirectory,
        int size = 200,
        CancellationToken cancellationToken = default)
    {
        var thumbnailPath = await GenerateThumbnailAsync(inputPath, outputDirectory, size, cancellationToken: cancellationToken);
        
        var fileName = Path.GetFileName(inputPath);
        var thumbnailFileName = Path.GetFileName(thumbnailPath);
        var fileInfo = new FileInfo(inputPath);
        
        var html = $@"
<div class=""thumbnail-container"" style=""display: inline-block; margin: 10px; text-align: center; border: 1px solid #ccc; padding: 10px; border-radius: 5px;"">
    <img src=""{thumbnailFileName}"" alt=""{fileName}"" style=""max-width: {size}px; max-height: {size}px; object-fit: cover;"" />
    <div class=""thumbnail-info"" style=""margin-top: 5px; font-size: 12px;"">
        <div><strong>{fileName}</strong></div>
        <div>Size: {fileInfo.Length:N0} bytes</div>
        <div>Modified: {fileInfo.LastWriteTime:yyyy-MM-dd}</div>
    </div>
</div>";

        var htmlPath = Path.Combine(outputDirectory, $"thumb_{Path.GetFileNameWithoutExtension(inputPath)}.html");
        await File.WriteAllTextAsync(htmlPath, html, cancellationToken);
        
        return htmlPath;
    }

    /// <summary>
    /// Generate a thumbnail gallery HTML page
    /// </summary>
    public static async Task<string> GenerateThumbnailGalleryAsync(
        IEnumerable<string> imagePaths,
        string outputDirectory,
        int thumbnailSize = 200,
        string title = "Image Gallery",
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        
        var thumbnailTasks = imagePaths.Select(async imagePath =>
        {
            try
            {
                var thumbnailPath = await GenerateThumbnailAsync(imagePath, outputDirectory, thumbnailSize, cancellationToken: cancellationToken);
                return new { ImagePath = imagePath, ThumbnailPath = thumbnailPath, Success = true };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to generate thumbnail for {imagePath}: {ex.Message}");
                return new { ImagePath = imagePath, ThumbnailPath = "", Success = false };
            }
        });

        var thumbnails = await Task.WhenAll(thumbnailTasks);
        var successfulThumbnails = thumbnails.Where(t => t.Success).ToList();

        var htmlContent = GenerateGalleryHtml(successfulThumbnails.Select(t => new
        {
            OriginalPath = t.ImagePath,
            ThumbnailPath = t.ThumbnailPath
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

    private static string GenerateGalleryHtml(IEnumerable<dynamic> thumbnails, string title, int thumbnailSize)
    {
        var thumbnailHtml = string.Join("\n", thumbnails.Select(t => $@"
        <div class=""thumbnail-item"" style=""display: inline-block; margin: 10px; text-align: center; border: 1px solid #ccc; padding: 10px; border-radius: 5px; vertical-align: top;"">
            <img src=""{Path.GetFileName(t.ThumbnailPath)}"" alt=""{Path.GetFileName(t.OriginalPath)}"" 
                 style=""max-width: {thumbnailSize}px; max-height: {thumbnailSize}px; object-fit: cover; cursor: pointer;"" 
                 onclick=""openFullImage('{t.OriginalPath.Replace("\\", "/")}')"" />
            <div class=""thumbnail-info"" style=""margin-top: 5px; font-size: 12px; max-width: {thumbnailSize}px; word-wrap: break-word;"">
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
        .thumbnail-item:hover {{ transform: scale(1.05); transition: transform 0.2s; }}
        #fullImageOverlay {{ 
            display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; 
            background-color: rgba(0,0,0,0.8); z-index: 1000; 
        }}
        #fullImageContainer {{ 
            position: absolute; top: 50%; left: 50%; transform: translate(-50%, -50%); 
            max-width: 90%; max-height: 90%; text-align: center; 
        }}
        #fullImage {{ max-width: 100%; max-height: 100%; }}
        .close-btn {{ 
            position: absolute; top: 10px; right: 20px; color: white; font-size: 30px; 
            cursor: pointer; user-select: none; 
        }}
    </style>
</head>
<body>
    <div class=""gallery-container"">
        <h1>{title}</h1>
        <div class=""thumbnails"">
            {thumbnailHtml}
        </div>
    </div>
    
    <div id=""fullImageOverlay"" onclick=""closeFullImage()"">
        <div class=""close-btn"" onclick=""closeFullImage()"">&times;</div>
        <div id=""fullImageContainer"">
            <img id=""fullImage"" src="""" alt="""" />
        </div>
    </div>

    <script>
        function openFullImage(imagePath) {{
            document.getElementById('fullImage').src = imagePath;
            document.getElementById('fullImageOverlay').style.display = 'block';
        }}
        
        function closeFullImage() {{
            document.getElementById('fullImageOverlay').style.display = 'none';
        }}
        
        document.addEventListener('keydown', function(event) {{
            if (event.key === 'Escape') {{
                closeFullImage();
            }}
        }});
    </script>
</body>
</html>";
    }
}