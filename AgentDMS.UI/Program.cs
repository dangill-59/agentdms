using AgentDMS.Core.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<ImageProcessingService>(provider => 
    new ImageProcessingService(maxConcurrency: Environment.ProcessorCount));
builder.Services.AddSingleton<FileUploadService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseDefaultFiles();
app.UseStaticFiles();

// Configure API endpoints
app.MapPost("/api/upload", async (IFormFile file, ImageProcessingService imageProcessor) =>
{
    if (file == null || file.Length == 0)
    {
        return Results.BadRequest("No file uploaded");
    }

    // Check if it's a PNG file
    if (!file.ContentType.StartsWith("image/png") && !Path.GetExtension(file.FileName).Equals(".png", StringComparison.OrdinalIgnoreCase))
    {
        return Results.BadRequest("Only PNG files are supported");
    }

    try
    {
        // Create temp directory for uploaded file
        var tempDir = Path.Combine(Path.GetTempPath(), "AgentDMS_WebUploads");
        Directory.CreateDirectory(tempDir);
        
        var tempFilePath = Path.Combine(tempDir, $"{Guid.NewGuid()}_{file.FileName}");
        
        // Save uploaded file
        using (var stream = new FileStream(tempFilePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // Process the image
        var result = await imageProcessor.ProcessImageAsync(tempFilePath);

        if (result.Success && result.ProcessedImage != null)
        {
            // Read thumbnail as base64 for display
            var thumbnailBase64 = "";
            if (!string.IsNullOrEmpty(result.ProcessedImage.ThumbnailPath) && File.Exists(result.ProcessedImage.ThumbnailPath))
            {
                var thumbnailBytes = await File.ReadAllBytesAsync(result.ProcessedImage.ThumbnailPath);
                thumbnailBase64 = Convert.ToBase64String(thumbnailBytes);
            }

            // Clean up temp file
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }

            return Results.Ok(new
            {
                success = true,
                fileName = result.ProcessedImage.FileName,
                originalFormat = result.ProcessedImage.OriginalFormat,
                dimensions = new { width = result.ProcessedImage.Width, height = result.ProcessedImage.Height },
                fileSize = result.ProcessedImage.FileSize,
                thumbnail = $"data:image/png;base64,{thumbnailBase64}",
                message = result.Message
            });
        }
        else
        {
            // Clean up temp file on failure
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
            
            return Results.BadRequest(new { success = false, message = result.Message });
        }
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error processing file: {ex.Message}");
    }
}).DisableAntiforgery();

app.Run();