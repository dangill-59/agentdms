# Manual Testing Guide for Image Viewing in Electron SDK

## Prerequisites
1. Make sure you have Node.js installed
2. Navigate to the AgentDMS.SDK directory
3. Run `npm install` to install dependencies

## Testing Steps

### 1. Run the Electron Application
```bash
cd AgentDMS.SDK
npm start
```

If you encounter sandbox issues, try:
```bash
npx electron . --no-sandbox
```

### 2. Test Image Loading
1. Click the **"Open File"** button in the toolbar
2. Select an image file (PNG, JPEG, GIF, BMP, WEBP supported)
3. The image should load and display in the viewer area

### 3. What to Look For

#### ✅ Success Indicators:
- Image displays correctly in the viewer area
- File information appears in the left sidebar
- You can zoom in/out using the controls
- You can rotate the image using the rotate button
- Status shows "File loaded successfully"

#### ⚠️ Troubleshooting:
If images don't load, check the browser console (F12) for:
- Detailed logging messages starting with:
  - "Opening file dialog..."
  - "Reading file content for: [file path]"
  - "File content result: [details]"
  - "Creating File object from data URL..."
  - "Loading file in viewer..."
  - "Image loaded successfully: [file name]"

#### 🚨 Error Indicators:
- Red error message in status area
- Console errors with specific file names and error details
- Image placeholder remains visible instead of actual image

### 4. Test Different Image Formats
Try loading different image types:
- PNG files
- JPEG/JPG files  
- GIF files
- BMP files
- WEBP files (if supported by your system)

### 5. Test Edge Cases
- Files with spaces in names
- Files with special characters
- Very large images
- Very small images
- Images from different directories

### 6. Test Drag and Drop
1. Drag an image file from your file manager
2. Drop it onto the viewer area
3. The image should load just like using "Open File"

## Expected Console Output (Success Case)
When working correctly, you should see console output similar to:
```
Opening file dialog...
File dialog result: {canceled: false, filePaths: ["/path/to/image.png"]}
Reading file content for: /path/to/image.png
File content result: {success: true, fileName: "image.png", mimeType: "image/png", size: 12345, dataUrlLength: 16460}
Creating File object from data URL...
File object created: {name: "image.png", type: "image/png", size: 12345}
Loading file in viewer...
Image loaded successfully: image.png
File loaded in viewer successfully
```

## If You Still Have Issues

1. **Check the console logs** - The enhanced debugging will show exactly where the process fails
2. **Try different image files** - Some files might be corrupted or in unsupported formats
3. **Verify file permissions** - Make sure the Electron app can read your image files
4. **Check the file path** - Paths with special characters might cause issues
5. **Try running with elevated permissions** - Some systems may have strict file access controls

## Report Issues
If you continue having problems, please include:
1. The complete console output
2. The specific image file that's not working (if possible)
3. Your operating system and version
4. The exact error messages displayed

The enhanced error reporting should now provide much more detailed information about what's going wrong, making it easier to identify and fix issues.