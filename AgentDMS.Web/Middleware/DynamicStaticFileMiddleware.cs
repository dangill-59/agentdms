using AgentDMS.Core.Services;
using AgentDMS.Core.Services.Storage;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Extensions.FileProviders;

namespace AgentDMS.Web.Middleware;

/// <summary>
/// Middleware that dynamically serves static files from the current storage provider's directory
/// </summary>
public class DynamicStaticFileMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IStorageService _storageService;
    private readonly ILogger<DynamicStaticFileMiddleware> _logger;
    private readonly string _requestPath;
    private readonly IContentTypeProvider _contentTypeProvider;

    public DynamicStaticFileMiddleware(
        RequestDelegate next,
        IStorageService storageService,
        ILogger<DynamicStaticFileMiddleware> logger,
        string requestPath = "/AgentDMS_Output")
    {
        _next = next;
        _storageService = storageService;
        _logger = logger;
        _requestPath = requestPath;
        _contentTypeProvider = new FileExtensionContentTypeProvider();
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Check if this request is for our dynamic static files
        if (context.Request.Path.StartsWithSegments(_requestPath, out var remainingPath))
        {
            await HandleStaticFileRequest(context, remainingPath);
            return;
        }

        // If not our path, continue to next middleware
        await _next(context);
    }

    private async Task HandleStaticFileRequest(HttpContext context, PathString remainingPath)
    {
        try
        {
            // Refresh storage provider to ensure we have the latest configuration
            await _storageService.RefreshProviderAsync();

            if (_storageService.StorageProvider is LocalStorageProvider localProvider)
            {
                var baseDirectory = localProvider.BaseDirectory;
                var physicalPath = Path.Combine(baseDirectory, remainingPath.Value?.TrimStart('/') ?? "");

                // Security check: ensure the path is within the base directory
                var fullPath = Path.GetFullPath(physicalPath);
                var fullBaseDirectory = Path.GetFullPath(baseDirectory);
                
                if (!fullPath.StartsWith(fullBaseDirectory, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogWarning("Attempt to access file outside base directory: {RequestPath}", physicalPath);
                    context.Response.StatusCode = 403;
                    return;
                }

                if (File.Exists(fullPath))
                {
                    // Determine content type
                    if (!_contentTypeProvider.TryGetContentType(fullPath, out var contentType))
                    {
                        contentType = "application/octet-stream";
                    }

                    context.Response.ContentType = contentType;
                    context.Response.Headers["Cache-Control"] = "public, max-age=3600"; // Cache for 1 hour

                    _logger.LogDebug("Serving static file from dynamic storage: {FilePath}", fullPath);
                    
                    using var fileStream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await fileStream.CopyToAsync(context.Response.Body);
                    return;
                }
            }

            // File not found or not using local storage
            context.Response.StatusCode = 404;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error serving static file: {RequestPath}", context.Request.Path);
            context.Response.StatusCode = 500;
        }
    }
}