using AgentDMS.Core.Services;
using AgentDMS.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add SignalR
builder.Services.AddSignalR();

// Add AgentDMS Core services
builder.Services.AddSingleton<ImageProcessingService>();
builder.Services.AddSingleton<FileUploadService>();
builder.Services.AddSingleton<IProgressBroadcaster, SignalRProgressBroadcaster>();
builder.Services.AddSingleton<IBackgroundJobService, BackgroundJobService>();
builder.Services.AddHostedService<BackgroundJobService>(provider => 
    (BackgroundJobService)provider.GetRequiredService<IBackgroundJobService>());

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
