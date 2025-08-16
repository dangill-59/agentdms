# AgentDMS - Image Processing Utility

A comprehensive C# utility for image file processing with support for multiple formats, multipage documents, and thumbnail generation.

## Features

✅ **Multi-format Support**: JPEG, PNG, BMP, GIF, TIFF, PDF, WebP  
✅ **Multipage Processing**: Automatically splits TIFF and PDF files into individual pages  
✅ **PNG Conversion**: Converts all supported formats to PNG while preserving original format metadata  
✅ **Thumbnail Generation**: Creates browser-friendly thumbnails with customizable sizes  
✅ **Multithreading**: Optimized for handling large files and batch processing  
✅ **Interactive CLI**: User-friendly command-line interface for testing and operation  
✅ **HTML Gallery**: Generates responsive thumbnail galleries with full-image preview  
✅ **Error Handling**: Comprehensive error handling and progress reporting  

## Architecture

The solution consists of four main projects:

- **AgentDMS.Core**: Core functionality with services and utilities
- **AgentDMS.UI**: Command-line interface for testing and demonstration  
- **AgentDMS.Web**: Web-based HTML interface for browser access
- **AgentDMS.Tests**: Unit tests for core functionality

### Core Components

- `ImageProcessingService`: Main service for processing images and documents
- `FileUploadService`: Handles file uploads and validation
- `ThumbnailGenerator`: Utility for creating thumbnails and galleries
- `ImageFile` & `ProcessingResult`: Models for representing processed images and results

## Database

This application uses Entity Framework Core with SQLite for data persistence.

### Database Setup

The application automatically creates the database (`agentdms.db`) on first run. The database includes a `Documents` table with the following key columns:
- `IsActive` - Whether the document is active (default: true)
- `IsArchived` - Whether the document is archived (default: false)

### Migration Commands

To work with database migrations, use the following commands from the `AgentDMS.Core` project directory:

```bash
# Navigate to the Core project
cd AgentDMS.Core

# Create a new migration
dotnet ef migrations add <MigrationName>

# Apply migrations to the database
dotnet ef database update

# Revert to a specific migration
dotnet ef database update <MigrationName>

# Remove the last migration (if not applied to database)
dotnet ef migrations remove

# View migration status
dotnet ef migrations list
```

### Database Configuration

The database connection is configured in `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=./agentdms.db"
  }
}
```

**Important:** All migrations and database operations target `agentdms.db` in the application root. Never use `agentdms_design.db` or similar design-time database files for production operations.

## Usage

### Web Interface (Recommended)

The easiest way to use AgentDMS is through the web interface:

```bash
# Start the web server
dotnet run --project AgentDMS.Web

# Open your browser to http://localhost:5249
```

**Web Interface Features:**
- **Upload & Process**: Drag-and-drop file upload with real-time processing
- **Batch Processing**: Process multiple images from file paths
- **Gallery Generation**: Create thumbnail galleries with customizable sizes
- **Format Support**: View all supported formats with descriptions

### Command Line Interface

```bash
# Run in interactive mode
dotnet run --project AgentDMS.UI

# Process a single file
dotnet run --project AgentDMS.UI -- --process "path/to/image.jpg"

# Process directory with custom concurrency
dotnet run --project AgentDMS.UI -- --directory "path/to/images" --max-concurrency 8

# Benchmark library performance
dotnet run --project AgentDMS.UI -- --benchmark "test.jpg"

# Show help
dotnet run --project AgentDMS.UI -- --help

# Show supported formats
dotnet run --project AgentDMS.UI -- --formats
```

### Interactive Menu Options

1. **Process single file**: Convert and generate thumbnails for one image
2. **Process multiple files from directory**: Batch process all supported files in a directory
3. **Generate thumbnail gallery**: Create an HTML gallery with thumbnails
4. **List supported formats**: Display all supported file formats

### Programmatic Usage

```csharp
// Initialize services with custom concurrency
var imageProcessor = new ImageProcessingService(
    maxConcurrency: 8, 
    outputDirectory: "custom/output");

// Process a single file
var result = await imageProcessor.ProcessImageAsync("image.jpg");

if (result.Success)
{
    Console.WriteLine($"PNG: {result.ProcessedImage.ConvertedPngPath}");
    Console.WriteLine($"Thumbnail: {result.ProcessedImage.ThumbnailPath}");
    
    // Check metrics if available
    if (result.Metrics != null)
    {
        Console.WriteLine($"Processing time: {result.Metrics.TotalProcessingTime?.TotalMilliseconds:F0}ms");
    }
}

// Batch processing with progress tracking
var progress = new Progress<int>(count => Console.WriteLine($"Processed: {count}"));
var results = await imageProcessor.ProcessMultipleImagesAsync(filePaths, progress);

// Generate thumbnail gallery
var galleryPath = await ThumbnailGenerator.GenerateThumbnailGalleryAsync(
    imagePaths, outputDirectory, thumbnailSize: 200);

// Benchmark library performance
var benchmarkResults = await ImageLibraryBenchmark.BenchmarkSinglePageFormatsAsync(
    "test.jpg", "benchmark_output");
ImageLibraryBenchmark.PrintBenchmarkResults(benchmarkResults);
```

## Supported Formats

| Format | Extension | Features |
|--------|-----------|----------|
| JPEG   | .jpg, .jpeg | Single page conversion |
| PNG    | .png      | Single page conversion |
| BMP    | .bmp      | Single page conversion |
| GIF    | .gif      | Single page conversion |
| TIFF   | .tif, .tiff | **Multipage splitting** |
| PDF    | .pdf      | **Multipage processing** |
| WebP   | .webp     | Single page conversion |

## Multithreading

The system uses `SemaphoreSlim` to control concurrency and prevent resource exhaustion:

- **Configurable Concurrency**: Default is `Environment.ProcessorCount`
- **Memory Management**: Automatic resource cleanup and disposal
- **Progress Reporting**: Real-time progress updates for batch operations

## Output Structure

```
AgentDMS_Output/
├── converted_image.png          # PNG versions of all processed images
├── thumb_image.png             # 200x200 thumbnails
└── image_page_1.png            # Individual pages from multipage files

AgentDMS_Gallery/
├── gallery.html                # Interactive HTML gallery
├── thumb_image1.png           # Gallery thumbnails
└── thumb_image2.png
```

## Testing

Run the unit tests:

```bash
dotnet test
```

The test suite includes:
- Single image processing tests
- Batch processing validation
- Error handling scenarios
- Supported format verification

## Performance Features

### Configurable Concurrency
- **CLI Configuration**: Use `--max-concurrency` or `-c` to set concurrent processing tasks
- **Environment Variable**: Set `AGENTDMS_MAX_CONCURRENCY` for system-wide configuration
- **Intelligent Batching**: Large file sets are processed in batches to prevent resource exhaustion

### File Size Management
- **Configurable Limits**: Use `--max-file-size` or `-s` to set maximum file size in MB (default: 100MB)
- **Environment Variable**: Set `AGENTDMS_MAX_FILE_SIZE_MB` for system-wide configuration
- **Pre-filtering**: Large files are automatically skipped with informative messages

### Performance Monitoring
- **Detailed Metrics**: Track file load, decode, conversion, and thumbnail generation times
- **Batch Analytics**: Aggregate metrics for batch processing with bottleneck identification
- **Metrics Logging**: Enable/disable with `--no-metrics` flag

### Library Benchmarking
- **Performance Comparison**: Built-in benchmarking tool for ImageSharp vs Magick.NET
- **Format-Specific Testing**: Benchmark single-page formats to determine optimal library
- **Usage**: `dotnet run --project AgentDMS.UI -- --benchmark image.jpg`

### Enhanced CLI Options
```bash
# Set concurrency and file size limits
dotnet run --project AgentDMS.UI -- --directory "Images" --max-concurrency 8 --max-file-size 200

# Use environment variables
AGENTDMS_MAX_CONCURRENCY=16 dotnet run --project AgentDMS.UI -- --directory "Images"

# Benchmark library performance
dotnet run --project AgentDMS.UI -- --benchmark test.jpg --output "BenchmarkResults"

# Process with custom settings and no metrics
dotnet run --project AgentDMS.UI -- --process image.pdf --output "Output" --no-metrics
```

## Error Handling

- File not found errors
- Unsupported format detection
- Processing failures with detailed messages
- Graceful handling of corrupted files

## Dependencies

- **SixLabors.ImageSharp**: Modern image processing
- **SixLabors.ImageSharp.Drawing**: Advanced image manipulation
- **iText7**: PDF processing capabilities
- **System.Drawing.Common**: TIFF multipage support

## Future Enhancements

- Complete PDF to image conversion (requires additional libraries)
- OCR text extraction from images
- Image metadata preservation
- Batch watermarking capabilities
- Cloud storage integration