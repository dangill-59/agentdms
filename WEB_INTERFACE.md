# Web Interface Documentation

## Overview

The AgentDMS web interface provides a user-friendly drag-and-drop interface for uploading and processing PNG files. Built using ASP.NET Core with a modern, responsive design.

## Features

### 🖼️ PNG File Processing
- **Drag & Drop Support**: Intuitive drag-and-drop functionality with visual feedback
- **File Selection**: Traditional file picker as alternative to drag-and-drop  
- **PNG-Only Validation**: Strict validation ensuring only PNG files are processed
- **File Size Limits**: Maximum 50MB per file with clear error messages
- **Thumbnail Generation**: Automatic thumbnail creation and display

### 🎨 User Interface
- **Responsive Design**: Mobile-friendly interface that works on all devices
- **Visual Feedback**: Clear drag-over states with color and scale animations
- **Real-time Processing**: Loading indicators and progress feedback
- **Result Cards**: Beautiful cards showing processed images with metadata
- **Multiple File Support**: Process multiple files sequentially with results history

### 🔧 Technical Features
- **Base64 Thumbnails**: Thumbnails embedded directly in responses for immediate display
- **Async Processing**: Non-blocking file processing using existing AgentDMS services
- **Error Handling**: Comprehensive error handling with user-friendly messages
- **Clean Architecture**: Separation of web layer from core business logic

## Web Components

### 1. Main Interface (`wwwroot/index.html`)
Modern HTML5 interface featuring:
- Semantic markup with ARIA accessibility
- Clean typography and iconography
- Responsive meta viewport configuration

### 2. Stylesheet (`wwwroot/css/styles.css`)
Comprehensive CSS with:
- CSS Grid and Flexbox layouts
- Smooth animations and transitions
- Mobile-first responsive design
- CSS custom properties for theming
- Glassmorphism effects with backdrop filters

Key CSS classes:
- `.drop-zone`: Main drag-and-drop area with hover and drag-over states
- `.result-card`: Individual result display cards with success/error styling
- `.processing-status`: Loading indicator with spinning animation
- `.thumbnail`: Responsive thumbnail display with rounded corners

### 3. JavaScript Application (`wwwroot/js/app.js`)
Feature-rich JavaScript implementation:

#### Core Functions:
- `setupEventListeners()`: Initializes all drag-and-drop and click events
- `processFiles()`: Main file processing orchestrator with validation
- `processFile()`: Individual file upload and processing via fetch API
- `addResultCard()`: Dynamic result card creation and display

#### Validation Features:
- File type validation (PNG only)
- File size validation (50MB limit)
- MIME type checking
- Extension validation as fallback

#### User Experience:
- Visual drag-over feedback with CSS class toggles
- Progress indicators during processing
- Smooth animations for new result cards
- File size formatting utilities
- Error message display

### 4. Web API (`Program.cs`)
Minimal API implementation with:

#### Endpoint: `POST /api/upload`
- **Input**: Multipart form data with PNG file
- **Validation**: Content type and extension checking
- **Processing**: Integration with existing `ImageProcessingService`
- **Output**: JSON response with thumbnail data and metadata
- **Security**: Antiforgery protection disabled for API endpoint

#### Response Format:
```json
{
  "success": true,
  "fileName": "processed_image.png",
  "originalFormat": ".png",
  "dimensions": { "width": 300, "height": 200 },
  "fileSize": 1234,
  "thumbnail": "data:image/png;base64,iVBORw0KGgoAAAANS...",
  "message": "Processing completed successfully"
}
```

## Integration with Existing Services

### ImageProcessingService Integration
The web interface seamlessly integrates with the existing `AgentDMS.Core.Services.ImageProcessingService`:
- Reuses existing PNG processing logic
- Maintains thumbnail generation capabilities
- Preserves file validation and error handling
- Utilizes existing concurrency controls

### FileUploadService Integration
While not directly used in the web interface, the validation logic follows the same patterns as the existing `FileUploadService` for consistency.

## File Structure
```
AgentDMS.UI/
├── Program.cs                 # ASP.NET Core web application entry point
├── ConsoleProgram.cs          # Original console application (preserved)
├── AgentDMS.UI.csproj        # Updated project file for web SDK
└── wwwroot/                   # Static web content
    ├── index.html            # Main web interface
    ├── css/
    │   └── styles.css        # Application stylesheet
    └── js/
        └── app.js            # Client-side JavaScript
```

## Usage

### Starting the Web Application
```bash
cd AgentDMS.UI
dotnet run
```
The application will be available at `http://localhost:5000`

### Uploading Files
1. **Drag & Drop**: Drag PNG files directly onto the drop zone
2. **File Picker**: Click "Choose Files" button to select files
3. **Multiple Files**: Select or drop multiple PNG files for batch processing

### Processing Results
- Each processed file appears as a card with thumbnail and metadata
- Success: Green indicator with thumbnail preview
- Error: Red indicator with error message
- Results are ordered with newest at the top

## Error Handling

### Client-Side Validation
- File type checking before upload
- File size validation (50MB limit)
- User-friendly alert messages
- Prevention of non-PNG file processing

### Server-Side Validation
- Content-type verification
- File extension checking
- Processing error handling
- Temporary file cleanup

## Responsive Design

The interface adapts to different screen sizes:
- **Desktop**: Multi-column grid layout for results
- **Tablet**: Responsive grid with adjusted spacing
- **Mobile**: Single-column layout with optimized touch targets

## Browser Compatibility

- Modern browsers supporting HTML5 File API
- CSS Grid and Flexbox support
- ES6 JavaScript features (async/await, arrow functions)
- Fetch API for network requests

## Security Considerations

- File type validation on both client and server
- File size limits to prevent abuse
- Temporary file cleanup after processing
- No persistent file storage in web interface
- Content-Type validation for uploaded files

## Performance Features

- Base64 thumbnail embedding for immediate display
- Async file processing to prevent UI blocking
- Efficient DOM manipulation for result display
- Debounced drag events for smooth interactions
- Memory-efficient file handling