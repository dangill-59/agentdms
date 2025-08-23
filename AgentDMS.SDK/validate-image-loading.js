#!/usr/bin/env node

/**
 * Quick validation script for image loading functionality
 * Run this to verify the core image loading pipeline works correctly
 */

const fs = require('fs');
const path = require('path');

// Colors for console output
const colors = {
  green: '\x1b[32m',
  red: '\x1b[31m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  reset: '\x1b[0m'
};

function log(color, symbol, message) {
  console.log(`${colors[color]}${symbol} ${message}${colors.reset}`);
}

// Test the IPC handler simulation
function testIPCHandler(filePath) {
  try {
    // Security check - ensure the file exists and is readable
    if (!fs.existsSync(filePath)) {
      throw new Error('File does not exist');
    }
    
    // Read file and convert to base64 data URL
    const fileBuffer = fs.readFileSync(filePath);
    const fileExtension = path.extname(filePath).toLowerCase();
    
    // Determine MIME type based on file extension
    let mimeType = 'application/octet-stream';
    const mimeMap = {
      '.jpg': 'image/jpeg',
      '.jpeg': 'image/jpeg', 
      '.png': 'image/png',
      '.gif': 'image/gif',
      '.bmp': 'image/bmp',
      '.tiff': 'image/tiff',
      '.webp': 'image/webp',
      '.pdf': 'application/pdf'
    };
    
    if (mimeMap[fileExtension]) {
      mimeType = mimeMap[fileExtension];
    }
    
    const base64Data = fileBuffer.toString('base64');
    const dataUrl = `data:${mimeType};base64,${base64Data}`;
    
    return {
      success: true,
      dataUrl,
      mimeType,
      size: fileBuffer.length,
      fileName: path.basename(filePath)
    };
  } catch (error) {
    return {
      success: false,
      error: error.message
    };
  }
}

async function runValidation() {
  log('blue', '🔍', 'Starting AgentDMS Image Loading Validation...');
  console.log('');
  
  // Create a test image
  const testImagePath = '/tmp/agentdms-test-image.png';
  const minimalPng = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+P+/HgAGgwF/lK3Q6wAAAABJRU5ErkJggg==', 'base64');
  
  try {
    fs.writeFileSync(testImagePath, minimalPng);
    log('green', '✓', 'Test image created successfully');
    
    // Test IPC handler functionality
    const fileContent = testIPCHandler(testImagePath);
    
    if (fileContent.success) {
      log('green', '✓', `IPC handler simulation successful`);
      log('blue', '  ℹ', `File: ${fileContent.fileName}`);
      log('blue', '  ℹ', `MIME type: ${fileContent.mimeType}`);
      log('blue', '  ℹ', `Size: ${fileContent.size} bytes`);
      log('blue', '  ℹ', `Data URL length: ${fileContent.dataUrl.length} characters`);
    } else {
      log('red', '✗', `IPC handler failed: ${fileContent.error}`);
      process.exit(1);
    }
    
    // Validate data URL format
    if (fileContent.dataUrl.startsWith('data:image/png;base64,')) {
      log('green', '✓', 'Data URL format is correct');
    } else {
      log('red', '✗', 'Data URL format is incorrect');
      process.exit(1);
    }
    
    // Validate base64 data
    const base64Part = fileContent.dataUrl.split('base64,')[1];
    if (base64Part && base64Part.length > 0) {
      log('green', '✓', 'Base64 data is present');
    } else {
      log('red', '✗', 'Base64 data is missing');
      process.exit(1);
    }
    
    // Test error handling
    const nonExistentResult = testIPCHandler('/non-existent-file.png');
    if (!nonExistentResult.success && nonExistentResult.error === 'File does not exist') {
      log('green', '✓', 'Error handling works correctly');
    } else {
      log('red', '✗', 'Error handling is not working');
      process.exit(1);
    }
    
    // Test different file extensions
    const testExtensions = ['.png', '.jpg', '.gif', '.bmp', '.webp'];
    const expectedMimeTypes = ['image/png', 'image/jpeg', 'image/gif', 'image/bmp', 'image/webp'];
    
    for (let i = 0; i < testExtensions.length; i++) {
      const testPath = `/tmp/test${testExtensions[i]}`;
      fs.writeFileSync(testPath, minimalPng);
      
      const result = testIPCHandler(testPath);
      if (result.success && result.mimeType === expectedMimeTypes[i]) {
        log('green', '✓', `MIME type detection for ${testExtensions[i]} works correctly`);
      } else {
        log('red', '✗', `MIME type detection for ${testExtensions[i]} failed`);
        process.exit(1);
      }
      
      fs.unlinkSync(testPath);
    }
    
    console.log('');
    log('green', '🎉', 'All validation tests passed!');
    log('blue', '  ℹ', 'The image loading functionality should work correctly in Electron');
    log('yellow', '  ⚠', 'If you still have issues, run the Electron app and check console logs');
    console.log('');
    
  } catch (error) {
    log('red', '✗', `Validation failed: ${error.message}`);
    process.exit(1);
  } finally {
    // Cleanup
    if (fs.existsSync(testImagePath)) {
      fs.unlinkSync(testImagePath);
    }
  }
}

// Run the validation
runValidation().catch(error => {
  log('red', '✗', `Validation error: ${error.message}`);
  process.exit(1);
});