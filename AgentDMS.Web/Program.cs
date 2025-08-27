using AgentDMS.Core.Services;
using AgentDMS.Web.Hubs;
using AgentDMS.Web.Services;
using Microsoft.OpenApi.Models;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel server limits based on upload configuration
builder.Services.Configure<Microsoft.AspNetCore.Server.Kestrel.Core.KestrelServerOptions>(options =>
{
    // Get upload configuration to set appropriate limits
    var configuration = builder.Configuration;
    var uploadLimitsSection = configuration.GetSection("UploadLimits");
    
    var maxRequestBodySize = uploadLimitsSection.GetValue<long>("MaxRequestBodySizeBytes", 100 * 1024 * 1024);
    
    // Check environment variable override
    var envMaxRequestSize = Environment.GetEnvironmentVariable("AGENTDMS_MAX_REQUEST_SIZE_MB");
    if (!string.IsNullOrEmpty(envMaxRequestSize) && double.TryParse(envMaxRequestSize, out var maxRequestSizeMB))
    {
        maxRequestBodySize = (long)(maxRequestSizeMB * 1024 * 1024);
    }
    
    options.Limits.MaxRequestBodySize = maxRequestBodySize;
});

// Configure form options for multipart uploads
builder.Services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
{
    var configuration = builder.Configuration;
    var uploadLimitsSection = configuration.GetSection("UploadLimits");
    
    var maxMultipartBodyLength = uploadLimitsSection.GetValue<long>("MaxMultipartBodyLengthBytes", 100 * 1024 * 1024);
    
    // Check environment variable override
    var envMaxFileSize = Environment.GetEnvironmentVariable("AGENTDMS_MAX_FILE_SIZE_MB");
    if (!string.IsNullOrEmpty(envMaxFileSize) && double.TryParse(envMaxFileSize, out var maxFileSizeMB))
    {
        maxMultipartBodyLength = (long)(maxFileSizeMB * 1024 * 1024);
    }
    
    options.MultipartBodyLengthLimit = maxMultipartBodyLength;
    options.ValueLengthLimit = int.MaxValue;
    options.ValueCountLimit = int.MaxValue;
    options.KeyLengthLimit = int.MaxValue;
});

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Enhanced Swagger configuration
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Version = "v1",
        Title = "AgentDMS API",
        Description = "A comprehensive API for AgentDMS - Image Processing and Document Management System",
        Contact = new OpenApiContact
        {
            Name = "AgentDMS Support",
            Email = "support@agentdms.com"
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT")
        }
    });

    // Include XML comments
    var xmlFilename = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFilename);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }

    // Add security definition for future API key authentication if needed
    options.AddSecurityDefinition("ApiKey", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Header,
        Name = "X-API-Key",
        Description = "API Key authentication"
    });

    // Enable annotations for better documentation
    options.EnableAnnotations();
    
    // Group endpoints by tags
    options.TagActionsBy(api => new[] { api.GroupName ?? api.ActionDescriptor.RouteValues["controller"] });
    options.DocInclusionPredicate((name, api) => true);
});

// Add SignalR
builder.Services.AddSignalR();

// Add Upload Configuration Service
builder.Services.AddSingleton<IUploadConfigService, UploadConfigService>();

// Add AgentDMS Core services with configurable file upload service
builder.Services.AddSingleton<FileUploadService>(provider =>
{
    var uploadConfigService = provider.GetRequiredService<IUploadConfigService>();
    var config = uploadConfigService.GetConfigAsync().Result;
    return new FileUploadService(uploadDirectory: null, maxFileSize: config.MaxFileSizeBytes);
});
builder.Services.AddSingleton<IProgressBroadcaster, SignalRProgressBroadcaster>();
builder.Services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
builder.Services.AddHostedService<BackgroundJobService>(provider => 
    (BackgroundJobService)provider.GetRequiredService<IBackgroundJobService>());

// Add Scanner Service
builder.Services.AddSingleton<IScannerService, ScannerService>();

// Add Mistral Configuration Service
builder.Services.AddSingleton<IMistralConfigService, MistralConfigService>();

// Add Mistral Document AI Service (optional - only if API key is configured)
// Configuration example:
// Set environment variable: MISTRAL_API_KEY=your_api_key_here
// Or configure in appsettings.json and inject via IOptions<T>
/*
Example appsettings.json configuration:
{
  "MistralAI": {
    "ApiKey": "your_api_key_here", 
    "Endpoint": "https://api.mistral.ai/v1/chat/completions"
  }
}
*/
builder.Services.AddHttpClient<MistralDocumentAiService>();
builder.Services.AddSingleton<MistralDocumentAiService>(provider =>
{
    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();
    var httpClient = httpClientFactory.CreateClient(nameof(MistralDocumentAiService));
    var logger = provider.GetService<ILogger<MistralDocumentAiService>>();
    var configService = provider.GetRequiredService<IMistralConfigService>();
    
    // Get configuration from the config service
    var config = configService.GetConfigAsync().Result;
    
    // Fallback to environment variable if config is empty
    var apiKey = !string.IsNullOrEmpty(config.ApiKey) ? config.ApiKey : Environment.GetEnvironmentVariable("MISTRAL_API_KEY");
    var endpoint = !string.IsNullOrEmpty(config.Endpoint) ? config.Endpoint : "https://api.mistral.ai/v1/chat/completions";
    
    var service = new MistralDocumentAiService(httpClient, apiKey, endpoint, logger);
    
    // Subscribe to configuration changes to update the service at runtime
    configService.ConfigChanged += (sender, newConfig) =>
    {
        // Note: Due to singleton pattern, we can't easily update the existing service
        // This is a limitation of the current architecture
        logger?.LogInformation("Mistral configuration changed. Restart the application to apply new settings.");
    };
    
    return service;
});

// Update ImageProcessingService registration to include MistralDocumentAiService
builder.Services.AddSingleton<ImageProcessingService>(provider =>
{
    var logger = provider.GetService<ILogger<ImageProcessingService>>();
    var mistralService = provider.GetService<MistralDocumentAiService>();
    
    return new ImageProcessingService(
        maxConcurrency: 4, 
        outputDirectory: null, 
        logger: logger, 
        mistralService: mistralService);
});

// Configure CORS to allow all origins for development (updated for SignalR)
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.AllowAnyOrigin()
               .AllowAnyMethod()
               .AllowAnyHeader();
    });
    
    // Add specific policy for SignalR - allow all origins for remote access
    options.AddPolicy("SignalRPolicy", builder =>
    {
        builder.SetIsOriginAllowed(_ => true)  // Allow any origin for remote access
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AgentDMS API v1");
        options.RoutePrefix = "swagger";
        options.DisplayRequestDuration();
        options.DocExpansion(Swashbuckle.AspNetCore.SwaggerUI.DocExpansion.None);
        options.DefaultModelExpandDepth(2);
        options.DefaultModelRendering(Swashbuckle.AspNetCore.SwaggerUI.ModelRendering.Example);
        options.ShowExtensions();
        options.EnableTryItOutByDefault();
    });
}
else
{
    // Enable Swagger in production for API documentation
    // Note: In production, consider adding authentication/authorization to protect the Swagger UI
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "AgentDMS API v1");
        options.RoutePrefix = "api-docs";
        options.DocumentTitle = "AgentDMS API Documentation";
    });
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// Configure static file serving for AgentDMS_Output directory
var outputDirectory = Path.Combine(Path.GetTempPath(), "AgentDMS_Output");
Directory.CreateDirectory(outputDirectory);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(outputDirectory),
    RequestPath = "/AgentDMS_Output"
});

// Configure static file serving for AgentDMS_Scans directory
var scansDirectory = Path.Combine(Path.GetTempPath(), "AgentDMS_Scans");
Directory.CreateDirectory(scansDirectory);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(scansDirectory),
    RequestPath = "/AgentDMS_Scans"
});

app.UseRouting();
app.UseCors();
app.MapControllers();

// Map SignalR hub with specific CORS policy for remote access
app.MapHub<ProgressHub>("/progressHub").RequireCors("SignalRPolicy");

// Serve the main HTML page at root
app.MapGet("/", () => Results.File("index.html", "text/html"));

// Serve the remote scanning documentation
app.MapGet("/REMOTE_SCANNING.md", () => 
{
    var filePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "REMOTE_SCANNING.md");
    if (File.Exists(filePath))
    {
        var content = File.ReadAllText(filePath);
        return Results.Text(content, "text/markdown");
    }
    return Results.NotFound("Remote scanning documentation not found");
});

app.Run();
