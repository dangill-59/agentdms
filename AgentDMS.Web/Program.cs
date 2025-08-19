using AgentDMS.Core.Services;
using AgentDMS.Web.Hubs;
using AgentDMS.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR();

// Add AgentDMS Core services
builder.Services.AddSingleton<FileUploadService>();
builder.Services.AddSingleton<IProgressBroadcaster, SignalRProgressBroadcaster>();
builder.Services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
builder.Services.AddHostedService<BackgroundJobService>(provider => 
    (BackgroundJobService)provider.GetRequiredService<IBackgroundJobService>());

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
    
    // Add specific policy for SignalR
    options.AddPolicy("SignalRPolicy", builder =>
    {
        builder.WithOrigins("http://localhost", "https://localhost")
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
    app.UseSwaggerUI();
}

app.UseCors();
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

app.UseRouting();
app.MapControllers();

// Map SignalR hub
app.MapHub<ProgressHub>("/progressHub");

// Serve the main HTML page at root
app.MapGet("/", () => Results.File("index.html", "text/html"));

app.Run();
