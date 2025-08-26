# AgentDMS SDK

A comprehensive Electron-based image/document viewer and scanner SDK that provides modular components for viewing, scanning, annotating, and uploading documents. Built to integrate with the AgentDMS backend or work standalone.

## Features

- 📁 **Document Viewer**: View images (JPG, PNG, TIFF, BMP, GIF, WebP) and PDFs with zoom, pan, and rotation
- 🖨️ **Scanner Integration**: Native scanning support via TWAIN (Windows) and SANE (Linux/Mac)
- ✏️ **Annotation Tools**: Drawing, highlighting, redaction, and text annotation using Fabric.js
- ☁️ **Upload Capabilities**: Configurable upload to AgentDMS or custom backends
- 🔧 **Modular Design**: Use individual components or the complete application
- 📦 **Distributable**: Package as Electron app or npm module for integration

## Installation

### As Standalone Electron App

```bash
# Clone the repository
git clone https://github.com/dangill-59/agentdms.git
cd agentdms/AgentDMS.SDK

# Install dependencies
npm install

# Run the application
npm start

# Build distributables
npm run build           # All platforms
npm run build:win       # Windows
npm run build:mac       # macOS
npm run build:linux     # Linux
```

### As NPM Module for Integration

```bash
npm install @agentdms/sdk
```

## Quick Start

### Standalone Application

Launch the Electron app and start viewing, scanning, and annotating documents immediately:

```bash
npm start
```

### Integration into Existing Projects

```javascript
// Import the SDK
const AgentDMS = require('@agentdms/sdk');

// Create a complete application
const app = AgentDMS.createApp({
    apiBaseUrl: 'http://your-agentdms-server:5249'
});

// Or use individual components
const viewer = AgentDMS.createViewer('viewerContainer', {
    allowZoom: true,
    allowPan: true,
    allowRotation: true
});

const scanner = AgentDMS.createScanner({
    autoLoadScanners: true,
    defaultResolution: 300
});

const annotator = AgentDMS.createAnnotator('viewerContainer', {
    enableDrawing: true,
    enableHighlighting: true,
    enableRedaction: true
});

const uploader = AgentDMS.createUploader({
    apiBaseUrl: 'http://your-server:5249'
});
```

## Component Documentation

### Document Viewer

The viewer component handles display of images and PDFs with full zoom, pan, and rotation capabilities.

```javascript
const viewer = AgentDMS.createViewer('containerId', {
    allowZoom: true,        // Enable zoom functionality
    allowPan: true,         // Enable pan when zoomed
    allowRotation: true,    // Enable rotation
    maxZoom: 5,            // Maximum zoom level
    minZoom: 0.1,          // Minimum zoom level
    zoomStep: 0.2          // Zoom increment
});

// Load a file
await viewer.loadFile(file);

// Control the viewer
viewer.zoomIn();
viewer.zoomOut();
viewer.resetView();
viewer.rotateClockwise();
viewer.rotateCounterClockwise();

// Get current state
const currentFile = viewer.getCurrentFile();
const zoomLevel = viewer.getZoomLevel();
const rotation = viewer.getRotation();
```

### Scanner Component

Provides native scanning capabilities through the AgentDMS backend.

```javascript
const scanner = AgentDMS.createScanner({
    autoLoadScanners: true,     // Automatically load available scanners
    defaultResolution: 300,     // Default scan resolution
    defaultColorMode: 'Color',  // Default color mode
    defaultPaperSize: 'A4',     // Default paper size
    showProgressDialog: true    // Show progress during scanning
});

// Get available scanners
const scanners = await scanner.getAvailableScanners();

// Perform a scan
const result = await scanner.scan({
    scannerName: 'My Scanner',
    resolution: 300,
    colorMode: 'Color',
    paperSize: 'A4',
    duplex: false,
    showUserInterface: false
});

// Preset scan methods
await scanner.scanDocument();  // Standard document scan
await scanner.scanPhoto();     // High-resolution photo scan
await scanner.scanText();      // Black & white text scan
await scanner.quickScan();     // Fast, lower-resolution scan
```

### Annotation Component

Adds powerful annotation capabilities using Fabric.js.

```javascript
const annotator = AgentDMS.createAnnotator('containerId', {
    enableDrawing: true,           // Enable freehand drawing
    enableHighlighting: true,      // Enable highlighting
    enableRedaction: true,         // Enable redaction boxes
    enableText: true,              // Enable text annotations
    strokeWidth: 2,                // Default stroke width
    defaultStrokeColor: '#ff0000', // Default drawing color
    defaultHighlightColor: '#ffff00', // Default highlight color
    defaultRedactionColor: '#000000'  // Default redaction color
});

// Enable/disable annotation mode
annotator.enableAnnotation();
annotator.disableAnnotation();
annotator.toggle();

// Set annotation tools
annotator.setTool('draw');
annotator.setTool('highlight');
annotator.setTool('redact');
annotator.setTool('text');

// Manage annotations
annotator.clearAnnotations();
const annotations = annotator.getAnnotations();
annotator.loadAnnotations(annotations);

// Export annotated image
const dataUrl = annotator.exportAnnotatedImage();
```

### Upload Component

Handles file uploads to configurable backends.

```javascript
const uploader = AgentDMS.createUploader({
    apiBaseUrl: 'http://localhost:5249',     // AgentDMS server URL
    uploadEndpoint: null,                     // Custom upload endpoint (optional)
    maxFileSize: 100 * 1024 * 1024,          // 100MB max file size
    supportedFormats: ['.jpg', '.png', ...], // Supported file formats
    showProgress: true,                       // Show upload progress
    autoProcess: true,                        // Auto-process after upload
    thumbnailSize: 200                        // Thumbnail size for processing
});

// Upload a file
const result = await uploader.uploadFile(file, {
    thumbnailSize: 300,
    outputFormat: 'png'
});

// Upload current viewer file
await uploader.uploadCurrentFile();

// Upload with annotations
await uploader.uploadWithAnnotations();

// Configuration
uploader.setApiBaseUrl('http://new-server:5249');
uploader.setUploadEndpoint('http://custom-endpoint/upload');
uploader.setMaxFileSize(100 * 1024 * 1024); // 100MB
```

## API Integration

The SDK is designed to work with the AgentDMS REST API. Configure the base URL to point to your AgentDMS server:

```javascript
// Set API base URL
const config = {
    apiBaseUrl: 'http://your-agentdms-server:5249'
};

// For complete app
const app = AgentDMS.createApp(config);

// For individual components
const scanner = AgentDMS.createScanner(config);
const uploader = AgentDMS.createUploader(config);
```

### Required AgentDMS Endpoints

- `GET /api/ImageProcessing/formats` - Get supported formats
- `GET /api/ImageProcessing/scanners` - Get available scanners
- `GET /api/ImageProcessing/scanners/capabilities` - Get scanner capabilities
- `POST /api/ImageProcessing/scan` - Perform scan
- `POST /api/ImageProcessing/upload` - Upload file
- `POST /api/ImageProcessing/process` - Process file
- `GET /api/ImageProcessing/job/{id}/status` - Get job status

## Browser vs Electron Usage

### Electron Application (Recommended)
- Full native scanning support via TWAIN/SANE
- File system access for local file operations
- Native file dialogs
- Desktop application packaging

### Browser/Web Integration
- Limited to web-based functionality
- Scanning requires server-side scanner access
- File operations via HTML5 File API
- Upload and annotation capabilities available

```javascript
// Detect environment
if (window.electronAPI) {
    // Running in Electron - full functionality
    const scanner = AgentDMS.createScanner({ autoLoadScanners: true });
} else {
    // Running in browser - limited functionality
    console.log('Scanner functionality requires Electron environment');
}
```

## Configuration

### Application Configuration

```javascript
const config = {
    apiBaseUrl: 'http://localhost:5249',  // AgentDMS server URL
    uploadEndpoint: null,                  // Custom upload endpoint
    maxFileSize: 100 * 1024 * 1024,       // Maximum file size
    autoProcess: true,                     // Auto-process uploads
    showProgress: true                     // Show progress dialogs
};
```

### Environment Variables

Set environment variables for default configuration:

```bash
# API Configuration
AGENTDMS_API_BASE_URL=http://localhost:5249
AGENTDMS_UPLOAD_ENDPOINT=http://custom-server/upload

# Development
NODE_ENV=development
```

### Build Configuration

The SDK includes pre-configured electron-builder settings for packaging:

```json
{
  "build": {
    "appId": "com.agentdms.sdk",
    "productName": "AgentDMS SDK",
    "directories": {
      "output": "dist"
    },
    "mac": {
      "category": "public.app-category.productivity"
    },
    "win": {
      "target": "nsis"
    },
    "linux": {
      "target": "AppImage"
    }
  }
}
```

## Events and Callbacks

### Viewer Events

```javascript
viewer.on('fileLoaded', (file) => {
    console.log('File loaded:', file.name);
});

viewer.on('zoomChanged', (zoomLevel) => {
    console.log('Zoom level:', zoomLevel);
});

viewer.on('rotationChanged', (rotation) => {
    console.log('Rotation:', rotation);
});
```

### Scanner Events

```javascript
scanner.on('scanStarted', () => {
    console.log('Scan started');
});

scanner.on('scanCompleted', (result) => {
    console.log('Scan completed:', result);
});

scanner.on('scanError', (error) => {
    console.error('Scan error:', error);
});
```

### Upload Events

```javascript
uploader.on('uploadStarted', (file) => {
    console.log('Upload started:', file.name);
});

uploader.on('uploadProgress', (percent) => {
    console.log('Upload progress:', percent + '%');
});

uploader.on('uploadCompleted', (result) => {
    console.log('Upload completed:', result);
});
```

## Error Handling

All SDK methods return promises and include comprehensive error handling:

```javascript
try {
    const result = await scanner.scan(options);
    console.log('Scan successful:', result);
} catch (error) {
    console.error('Scan failed:', error.message);
    
    // Handle specific error types
    if (error.message.includes('No scanner selected')) {
        // Show scanner selection dialog
    } else if (error.message.includes('Scanner busy')) {
        // Wait and retry
    }
}
```

## Development

### Setup Development Environment

```bash
# Clone and install
git clone https://github.com/dangill-59/agentdms.git
cd agentdms/AgentDMS.SDK
npm install

# Run in development mode
npm run dev

# Run tests
npm test

# Lint code
npm run lint
```

### Building

```bash
# Build for current platform
npm run build

# Build for specific platforms
npm run build:win     # Windows
npm run build:mac     # macOS  
npm run build:linux   # Linux
```

### Testing

The SDK includes comprehensive testing and validation capabilities:

```bash
# Run all unit tests
npm test

# Run specific test suites
npm test -- --grep "viewer"
npm test -- --grep "scanner"
npm test -- --grep "annotator"
npm test -- --grep "uploader"

# Run end-to-end validation (tests complete Electron app)
npm run validate:e2e

# Run all validations (E2E + component validation)
npm run validate

# Run individual component validation
node validate-image-loading.js
```

#### End-to-End Validation

The `validate:e2e` command provides comprehensive testing of the complete Electron application:

- ✅ **Electron App Launch** - Verifies app starts successfully  
- ✅ **Window Creation** - Confirms main window creation
- ✅ **Page Loading** - Ensures renderer process loads properly
- ✅ **IPC Communication** - Tests file reading via IPC handlers
- ✅ **Image Rendering** - Validates image loading in viewer component
- ✅ **DOM Validation** - Confirms image elements exist in DOM
- ✅ **Visual Verification** - Checks that images are actually visible
- ✅ **Functionality Testing** - Tests zoom, pan, and viewer features

This validation runs the actual Electron app and confirms that images are properly rendered and visible in the DOM, providing confidence that the complete user experience works correctly.

For detailed information about the validation process, see [E2E_VALIDATION_GUIDE.md](./E2E_VALIDATION_GUIDE.md).

## Contributing

1. Fork the repository
2. Create a feature branch
3. Make your changes
4. Add tests for new functionality
5. Run tests and linting
6. Submit a pull request

## License

MIT License - see LICENSE file for details.

## Support

- GitHub Issues: https://github.com/dangill-59/agentdms/issues
- Documentation: https://github.com/dangill-59/agentdms/tree/main/AgentDMS.SDK/docs
- AgentDMS Main Project: https://github.com/dangill-59/agentdms