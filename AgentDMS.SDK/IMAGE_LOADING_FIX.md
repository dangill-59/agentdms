# Image Loading Fix for Electron SDK

## Problem
Previously, the Electron SDK was unable to view images due to security restrictions in the renderer process. The code was attempting to use `fetch('file://${filePath}')` which is blocked by Electron's security model.

## Solution
Added a new IPC handler `file:readContent` in the main process that:

1. Reads file contents securely from the main process
2. Detects MIME type based on file extension
3. Converts file content to base64 data URL
4. Returns the data URL to the renderer process

The renderer process then uses this data URL to create a File object for the viewer.

## Changes Made

### Main Process (`src/index.js`)
- Added `fs` import
- Added `file:readContent` IPC handler with security checks and MIME type detection

### Preload Script (`src/renderer/preload.js`) 
- Exposed `readFileContent` method to renderer process

### App Logic (`src/renderer/app.js`)
- Modified `openFile()` method to use the new IPC handler instead of fetch

### Scanner Component (`src/components/scanner.js`)
- Updated `loadScannedImage()` method to use the same approach

## Benefits
- Images can now be viewed properly in the Electron SDK
- Maintains security by reading files through the main process
- Works with all supported image formats (PNG, JPEG, GIF, BMP, TIFF, WEBP)
- Proper error handling and MIME type detection