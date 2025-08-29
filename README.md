# AgentDMS - Image Processing Utility

A comprehensive C# utility for image file processing with support for multiple formats, multipage documents, and thumbnail generation. Includes REST API with full Swagger documentation.

## Features

✅ **Multi-format Support**: JPEG, PNG, BMP, GIF, TIFF, PDF, WebP  
✅ **Multipage Processing**: Automatically splits TIFF and PDF files into individual pages  
✅ **PNG Conversion**: Converts all supported formats to PNG while preserving original format metadata  
✅ **OCR Text Extraction**: Extracts text from images using Tesseract OCR for AI analysis  
✅ **Thumbnail Generation**: Creates browser-friendly thumbnails with customizable sizes  
✅ **Multithreading**: Optimized for handling large files and batch processing  
✅ **Interactive CLI**: User-friendly command-line interface for testing and operation  
✅ **HTML Gallery**: Generates responsive thumbnail galleries with full-image preview  
✅ **Error Handling**: Comprehensive error handling and progress reporting  
✅ **REST API**: Full REST API with Swagger documentation for integration  
✅ **Real-time Updates**: SignalR integration for live progress tracking  

## Architecture

The solution consists of four main projects:

- **AgentDMS.Core**: Core functionality with services and utilities
- **AgentDMS.UI**: Command-line interface for testing and demonstration  
- **AgentDMS.Web**: Web-based interface and REST API with Swagger documentation
- **AgentDMS.Tests**: Unit tests for core functionality

### Core Components

- `ImageProcessingService`: Main service for processing images and documents with OCR capabilities
- `FileUploadService`: Handles file uploads and validation
- `ThumbnailGenerator`: Utility for creating thumbnails and galleries
- `ImageFile` & `ProcessingResult`: Models for representing processed images and results

## Usage

### Web Interface (Recommended)

The easiest way to use AgentDMS is through the web interface:

```bash
# Start the web server
dotnet run --project AgentDMS.Web

# Open your browser to http://localhost:5249
# For remote access, see REMOTE_ACCESS.md
```

**Web Interface Features:**
- **Upload & Process**: Drag-and-drop file upload with real-time processing
- **Batch Processing**: Process multiple images from file paths
- **Gallery Generation**: Create thumbnail galleries with customizable sizes
- **Format Support**: View all supported formats with descriptions
- **Mistral Settings**: Configure Mistral LLM integration for document AI

### Remote Access

AgentDMS can be accessed from other computers on your network. The application is pre-configured to allow remote connections on all network interfaces.

**Quick Start for Remote Access:**
1. Find your server's IP address (e.g., `192.168.1.100`)
2. Access from remote computer: `http://[SERVER-IP]:5249`

📖 **For detailed remote access setup and configuration, see [REMOTE_ACCESS.md](REMOTE_ACCESS.md)**

### Remote Scanning

AgentDMS supports scanning from remote computers with multiple setup options:

**Recommended Setup (Server-Side Scanning):**
- Connect scanner to the computer running AgentDMS
- Access scanner remotely through the web interface from any device
- Full scanner driver support with centralized management

**Key Benefits:**
- ✅ Works from any device with a web browser
- ✅ No additional software needed on client devices  
- ✅ Full TWAIN/WIA scanner driver support
- ✅ Cross-platform compatibility (Windows, Linux, macOS)

📖 **For comprehensive remote scanning guide, troubleshooting, and future network scanner support, see [REMOTE_SCANNING.md](REMOTE_SCANNING.md)**

### REST API

AgentDMS provides a comprehensive REST API for programmatic access:

**API Documentation:**
- **Swagger UI**: `http://localhost:5249/swagger` (Development) or `http://localhost:5249/api-docs` (Production)
- **API Info**: `GET /api/apidocumentation/info` - Get API overview and endpoint information
- **Health Check**: `GET /api/apidocumentation/health` - Check API health status

**Key API Endpoints:**

**Image Processing:**
- `GET /api/imageprocessing/formats` - Get supported file formats
- `POST /api/imageprocessing/upload` - Upload and process image
- `POST /api/imageprocessing/process` - Process image by file path
- `POST /api/imageprocessing/batch-process` - Batch process multiple images
- `POST /api/imageprocessing/generate-gallery` - Generate thumbnail gallery
- `GET /api/imageprocessing/job/{jobId}/status` - Get job status
- `GET /api/imageprocessing/job/{jobId}/result` - Get job result

**Scanner Operations:**
- `GET /api/imageprocessing/scanners` - Get available scanners
- `GET /api/imageprocessing/scanners/capabilities` - Get scanner capabilities
- `POST /api/imageprocessing/scan` - Scan document
- `POST /api/imageprocessing/scan/preview` - Preview scan

**Mistral Configuration:**
- `GET /api/mistralconfig` - Get current configuration
- `POST /api/mistralconfig` - Update configuration
- `POST /api/mistralconfig/test` - Test configuration

**Upload Configuration:**
- `GET /api/uploadconfig` - Get current upload configuration
- `POST /api/uploadconfig` - Update upload configuration
- `POST /api/uploadconfig/reset` - Reset upload configuration to defaults
- `GET /api/uploadconfig/info` - Get detailed upload configuration information

**Example API Usage:**

```bash
# Get supported formats
curl -X GET "http://localhost:5249/api/imageprocessing/formats"

# Upload and process an image
curl -X POST "http://localhost:5249/api/imageprocessing/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@path/to/image.jpg"

# Check job status
curl -X GET "http://localhost:5249/api/imageprocessing/job/{jobId}/status"

# Get current upload configuration
curl -X GET "http://localhost:5249/api/uploadconfig"

# Update upload limits to 200MB
curl -X POST "http://localhost:5249/api/uploadconfig" \
  -H "Content-Type: application/json" \
  -d '{"maxFileSizeBytes": 209715200, "maxRequestBodySizeBytes": 209715200, "maxMultipartBodyLengthBytes": 209715200, "applySizeLimits": true}'

# Get API information
curl -X GET "http://localhost:5249/api/apidocumentation/info"
```

## Configuration

### Upload Size Limits

AgentDMS allows you to configure upload size limits both at startup and at runtime:

**Configuration File (appsettings.json):**
```json
{
  "UploadLimits": {
    "MaxFileSizeBytes": 104857600,
    "MaxRequestBodySizeBytes": 104857600,
    "MaxMultipartBodyLengthBytes": 104857600,
    "ApplySizeLimits": true
  }
}
```

**Environment Variables:**
```bash
# Set maximum file size (in MB)
export AGENTDMS_MAX_FILE_SIZE_MB=200

# Set maximum request body size (in MB)  
export AGENTDMS_MAX_REQUEST_SIZE_MB=200
```

**Runtime Configuration via API:**
- Use the `/api/uploadconfig` endpoints to modify limits while the server is running
- Configuration changes are saved to `App_Data/uploadconfig.json`
- No server restart required for most changes

**Configuration Priority:**
1. Runtime configuration file (`App_Data/uploadconfig.json`)
2. Environment variables
3. appsettings.json defaults

**Example API Usage:**

```bash
# Get supported formats
curl -X GET "http://localhost:5249/api/imageprocessing/formats"

# Upload and process an image
curl -X POST "http://localhost:5249/api/imageprocessing/upload" \
  -H "Content-Type: multipart/form-data" \
  -F "file=@path/to/image.jpg"

# Check job status
curl -X GET "http://localhost:5249/api/imageprocessing/job/{jobId}/status"

# Get API information
curl -X GET "http://localhost:5249/api/apidocumentation/info"
```

### Mistral LLM Integration

AgentDMS includes optional integration with Mistral LLM for document classification and data extraction. This feature enhances the image processing workflow with AI-powered document analysis.

**Configuration via Web Interface:**

1. Navigate to the **Mistral Settings** tab in the web interface
2. Enter your Mistral API credentials and configuration:
   - **API Key**: Your Mistral API key (stored locally)
   - **Endpoint**: Mistral API endpoint (default: `https://api.mistral.ai/v1/chat/completions`)
   - **Model**: Select from available models (mistral-small, mistral-medium, mistral-large, etc.)
   - **Temperature**: Control randomness (0 = focused, 2 = creative)
3. Click **Test Configuration** to validate your settings
4. Click **Save Configuration** to store your settings

**Available Models:**
- `mistral-small`: Fast and efficient for basic tasks
- `mistral-medium`: Balanced performance and capability  
- `mistral-large`: Most capable model for complex analysis
- `mistral-ocr`: Specialized model for optical character recognition and document processing
- `open-mistral-7b`: Open-source 7B parameter model
- `open-mixtral-8x7b`: Open-source mixture of experts model
- `open-mixtral-8x22b`: Large open-source mixture of experts model

**Features:**
- **Document Classification**: Automatic document type detection (invoice, contract, receipt, etc.)
- **Data Extraction**: Key-value pair extraction from document text
- **Confidence Scoring**: AI prediction confidence levels
- **Runtime Configuration**: Update settings without restarting the application
- **Secure Storage**: API keys stored locally in encrypted configuration files

**Configuration File:**
Settings are stored in `AgentDMS.Web/App_Data/mistralconfig.json`:
```json
{
  "apiKey": "your-api-key-here",
  "endpoint": "https://api.mistral.ai/v1/chat/completions",
  "model": "mistral-small",
  "temperature": 0.1
}
```

**Environment Variables (Alternative):**
```bash
# Set via environment variable (fallback option)
export MISTRAL_API_KEY="your-api-key-here"
```

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
- OCR processing errors with fallback to placeholder text

## OCR Text Extraction

AgentDMS includes integrated OCR (Optical Character Recognition) capabilities using Tesseract for extracting text from image-based documents. This enables meaningful AI analysis of scanned documents, invoices, and other image files.

**Features:**
- **Automatic Text Extraction**: Extracts text from all processed images (PNG, JPEG, TIFF, PDF pages, etc.)
- **Multi-page Document Support**: Processes each page of multi-page documents separately
- **AI Integration**: Extracted text is automatically sent to Mistral AI for document analysis
- **Error Handling**: Graceful fallback when OCR fails, with detailed error reporting
- **Text Normalization**: Cleans and normalizes extracted text for better AI analysis

**Supported Languages:**
- English (eng) - Included by default
- Additional language packs can be added to the `tessdata` directory

**Technical Details:**
- Uses Tesseract 5.x OCR engine
- Processes PNG-converted images for optimal OCR accuracy
- Includes text cleaning and whitespace normalization
- Asynchronous processing to prevent UI blocking
- Comprehensive logging for troubleshooting OCR issues

**Output:**
When OCR succeeds, the extracted text replaces placeholder content and is used for AI document analysis. If OCR fails, a descriptive error message is provided while allowing the rest of the processing pipeline to continue.

## Dependencies

- **SixLabors.ImageSharp**: Modern image processing
- **SixLabors.ImageSharp.Drawing**: Advanced image manipulation
- **iText7**: PDF processing capabilities
- **System.Drawing.Common**: TIFF multipage support
- **Tesseract**: OCR text extraction from images for AI analysis

## Future Enhancements

- Complete PDF to image conversion (requires additional libraries)
- Image metadata preservation
- Batch watermarking capabilities
- Cloud storage integration