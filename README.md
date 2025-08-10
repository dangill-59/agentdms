# AgentDMS - Image Processing Utility

A comprehensive C# utility for image file processing with support for multiple formats, multipage documents, and thumbnail generation. Now featuring a modern web interface for drag-and-drop PNG processing!

## 🌟 New Web Interface

AgentDMS now includes a beautiful, responsive web interface that allows users to:
- **Drag & Drop PNG Files**: Intuitive drag-and-drop with visual feedback
- **Instant Thumbnail Preview**: See processed thumbnails immediately
- **Multiple File Support**: Process multiple PNG files in sequence  
- **Real-time Processing**: Watch your files being processed with loading indicators
- **Mobile-Friendly Design**: Works perfectly on desktop, tablet, and mobile devices

### Quick Start - Web Interface
```bash
cd AgentDMS.UI
dotnet run
# Navigate to http://localhost:5000
```

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

The solution consists of three main projects:

- **AgentDMS.Core**: Core functionality with services and utilities
- **AgentDMS.UI**: Modern web interface with drag-and-drop functionality + legacy console interface
- **AgentDMS.Tests**: Unit tests for core functionality

### Core Components

- `ImageProcessingService`: Main service for processing images and documents
- `FileUploadService`: Handles file uploads and validation
- `ThumbnailGenerator`: Utility for creating thumbnails and galleries
- `ImageFile` & `ProcessingResult`: Models for representing processed images and results

## Usage

### Web Interface (Recommended)

The modern web interface provides the best user experience:

```bash
# Start the web application
dotnet run --project AgentDMS.UI

# Open your browser and navigate to:
# http://localhost:5000
```

**Web Features:**
- 🖱️ **Drag & Drop**: Simply drag PNG files onto the drop zone
- 📁 **File Picker**: Click "Choose Files" for traditional file selection
- 👁️ **Instant Preview**: See thumbnails immediately after processing
- 📱 **Responsive Design**: Works on desktop, tablet, and mobile
- ⚡ **Real-time Feedback**: Processing indicators and error messages
- 🎯 **PNG Focus**: Optimized specifically for PNG file processing

### Command Line Interface

The legacy command-line interface is available in `ConsoleProgram.cs` for advanced users and automation scenarios.

### Interactive Menu Options

1. **Process single file**: Convert and generate thumbnails for one image
2. **Process multiple files from directory**: Batch process all supported files in a directory
3. **Generate thumbnail gallery**: Create an HTML gallery with thumbnails
4. **List supported formats**: Display all supported file formats

### Programmatic Usage

```csharp
// Initialize services
var imageProcessor = new ImageProcessingService(maxConcurrency: 4);

// Process a single file
var result = await imageProcessor.ProcessImageAsync("image.jpg");

if (result.Success)
{
    Console.WriteLine($"PNG: {result.ProcessedImage.ConvertedPngPath}");
    Console.WriteLine($"Thumbnail: {result.ProcessedImage.ThumbnailPath}");
}

// Batch processing
var results = await imageProcessor.ProcessMultipleImagesAsync(filePaths, progress);

// Generate thumbnail gallery
var galleryPath = await ThumbnailGenerator.GenerateThumbnailGalleryAsync(
    imagePaths, outputDirectory, thumbnailSize: 200);
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

- **Async/Await**: Non-blocking operations throughout
- **Concurrent Processing**: Parallel processing of multiple files
- **Memory Efficient**: Proper disposal of image resources
- **Progress Reporting**: Real-time feedback for long operations

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