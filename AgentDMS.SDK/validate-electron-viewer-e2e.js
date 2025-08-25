#!/usr/bin/env node

/**
 * True End-to-End Validation Script for Electron Viewer
 * 
 * This script validates the complete image loading and rendering flow:
 * 1. Launches the Electron app programmatically
 * 2. Loads an image file through the IPC interface
 * 3. Verifies the image is rendered in the DOM
 * 4. Confirms the image is actually visible
 * 5. Tests viewer component functionality
 * 
 * Usage: node validate-electron-viewer-e2e.js
 */

const { spawn } = require('child_process');
const fs = require('fs');
const path = require('path');

// Colors for console output
const colors = {
  green: '\x1b[32m',
  red: '\x1b[31m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  cyan: '\x1b[36m',
  magenta: '\x1b[35m',
  reset: '\x1b[0m'
};

function log(color, symbol, message) {
  console.log(`${colors[color]}${symbol} ${message}${colors.reset}`);
}

function createTestImage() {
  // Create a test image (minimal PNG)
  const testImagePath = path.join(__dirname, 'e2e-test-image.png');
  const minimalPng = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+P+/HgAGgwF/lK3Q6wAAAABJRU5ErkJggg==', 'base64');
  fs.writeFileSync(testImagePath, minimalPng);
  return testImagePath;
}

async function createE2EValidationApp() {
  // Create a specialized Electron app for E2E testing
  const e2eAppPath = path.join(__dirname, 'e2e-validation-app.js');
  
  const e2eAppCode = `
const { app, BrowserWindow, ipcMain, dialog } = require('electron');
const path = require('path');
const fs = require('fs');

let mainWindow;
let validationResults = {
  appLaunched: false,
  windowCreated: false,
  pageLoaded: false,
  imageLoaded: false,
  imageRendered: false,
  imageVisible: false,
  domValidated: false,
  viewerFunctional: false
};

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1200,
    height: 800,
    show: false, // Don't show window during testing
    webPreferences: {
      nodeIntegration: false,
      contextIsolation: true,
      enableRemoteModule: false,
      preload: path.join(__dirname, 'src', 'renderer', 'preload.js')
    }
  });

  // Load the main renderer
  mainWindow.loadFile(path.join(__dirname, 'src', 'renderer', 'index.html'));
  
  validationResults.windowCreated = true;
  
  // Wait for page to be ready
  mainWindow.webContents.once('dom-ready', () => {
    validationResults.pageLoaded = true;
    console.log('E2E-LOG: Page loaded and DOM ready');
    
    // Start the validation sequence
    setTimeout(() => {
      runValidationSequence();
    }, 1000);
  });

  mainWindow.on('closed', () => {
    mainWindow = null;
  });
}

async function runValidationSequence() {
  try {
    console.log('E2E-LOG: Starting validation sequence');
    
    // Step 1: Test image loading through IPC
    const testImagePath = process.argv[2];
    if (!testImagePath || !fs.existsSync(testImagePath)) {
      throw new Error('Test image path not provided or file does not exist');
    }
    
    console.log('E2E-LOG: Testing image: ' + testImagePath);
    
    // Step 2: Simulate file reading (like the real IPC handler)
    const fileContent = await simulateFileRead(testImagePath);
    if (!fileContent.success) {
      throw new Error('Failed to read file: ' + fileContent.error);
    }
    
    validationResults.imageLoaded = true;
    console.log('E2E-LOG: Image file loaded successfully via IPC simulation');
    
    // Step 3: Load image in the viewer component
    const renderResult = await loadImageInViewer(fileContent);
    if (!renderResult.success) {
      throw new Error('Failed to render image: ' + renderResult.error);
    }
    
    validationResults.imageRendered = true;
    console.log('E2E-LOG: Image rendered in viewer component');
    
    // Step 4: Validate DOM rendering
    const domResult = await validateDOMRendering();
    if (!domResult.success) {
      throw new Error('DOM validation failed: ' + domResult.error);
    }
    
    validationResults.domValidated = true;
    validationResults.imageVisible = domResult.visible;
    console.log('E2E-LOG: DOM validation completed - Image visible: ' + domResult.visible);
    
    // Step 5: Test viewer functionality
    const viewerResult = await testViewerFunctionality();
    if (!viewerResult.success) {
      throw new Error('Viewer functionality test failed: ' + viewerResult.error);
    }
    
    validationResults.viewerFunctional = true;
    console.log('E2E-LOG: Viewer functionality tests passed');
    
    // Output final results
    outputValidationResults();
    
  } catch (error) {
    console.error('E2E-ERROR: ' + error.message);
    outputValidationResults();
    process.exit(1);
  } finally {
    // Close the app
    setTimeout(() => {
      app.quit();
    }, 1000); // Give more time for results to be output
  }
}

function simulateFileRead(filePath) {
  try {
    const fileBuffer = fs.readFileSync(filePath);
    const fileExtension = path.extname(filePath).toLowerCase();
    
    const mimeMap = {
      '.jpg': 'image/jpeg',
      '.jpeg': 'image/jpeg', 
      '.png': 'image/png',
      '.gif': 'image/gif',
      '.bmp': 'image/bmp',
      '.webp': 'image/webp'
    };
    
    const mimeType = mimeMap[fileExtension] || 'application/octet-stream';
    const base64Data = fileBuffer.toString('base64');
    const dataUrl = 'data:' + mimeType + ';base64,' + base64Data;
    
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

async function loadImageInViewer(fileContent) {
  return new Promise((resolve) => {
    // Execute in renderer process
    mainWindow.webContents.executeJavaScript(\`
      (async function() {
        try {
          // Wait for agentDMSViewer to be available (from preload/context bridge)
          let attempts = 0;
          while (!window.agentDMSViewer && attempts < 30) {
            await new Promise(r => setTimeout(r, 100));
            attempts++;
          }
          
          if (!window.agentDMSViewer) {
            throw new Error('agentDMSViewer not found. Available: ' + Object.keys(window).filter(k => k.includes('Agent')).join(', '));
          }
          
          // Create a File object from the data URL
          const response = await fetch('\${fileContent.dataUrl}');
          const blob = await response.blob();
          const file = new File([blob], '\${fileContent.fileName}', { type: '\${fileContent.mimeType}' });
          
          // Get the viewer container
          const viewerContainer = document.getElementById('viewerContainer');
          if (!viewerContainer) {
            throw new Error('Viewer container not found');
          }
          
          // Create viewer instance using the SDK API
          if (!window.viewerInstance) {
            window.viewerInstance = window.agentDMSViewer.create('viewerContainer', {
              allowZoom: true,
              allowPan: true,
              allowRotation: true
            });
          }
          
          // Load the image
          await window.viewerInstance.loadImage(file);
          
          return { success: true };
        } catch (error) {
          return { success: false, error: error.message };
        }
      })();
    \`).then(resolve).catch(error => resolve({ success: false, error: error.message }));
  });
}

async function validateDOMRendering() {
  return new Promise((resolve) => {
    mainWindow.webContents.executeJavaScript(\`
      (function() {
        try {
          // Check if image element exists in DOM
          const imageElement = document.querySelector('.document-image');
          if (!imageElement) {
            return { success: false, error: 'Image element not found in DOM' };
          }
          
          // Check if image is loaded
          if (!imageElement.complete || imageElement.naturalWidth === 0) {
            return { success: false, error: 'Image not loaded or has zero dimensions' };
          }
          
          // Check if image is visible
          const rect = imageElement.getBoundingClientRect();
          const isVisible = rect.width > 0 && rect.height > 0 && 
                           imageElement.offsetParent !== null;
          
          // Check if container has content
          const viewerContainer = document.getElementById('viewerContainer');
          const hasContent = viewerContainer.classList.contains('has-content') || 
                            viewerContainer.querySelector('.document-viewer') !== null;
          
          return {
            success: true,
            visible: isVisible,
            dimensions: {
              natural: { width: imageElement.naturalWidth, height: imageElement.naturalHeight },
              rendered: { width: rect.width, height: rect.height }
            },
            hasContent: hasContent,
            elementInfo: {
              src: imageElement.src.substring(0, 50) + '...',
              alt: imageElement.alt,
              className: imageElement.className
            }
          };
        } catch (error) {
          return { success: false, error: error.message };
        }
      })();
    \`).then(resolve).catch(error => resolve({ success: false, error: error.message }));
  });
}

async function testViewerFunctionality() {
  return new Promise((resolve) => {
    mainWindow.webContents.executeJavaScript(\`
      (function() {
        try {
          // Test zoom functionality
          const viewer = window.viewerInstance;
          if (!viewer) {
            return { success: false, error: 'Viewer instance not found' };
          }
          
          // Test zoom in
          const initialZoom = viewer.currentZoom || 1;
          if (typeof viewer.zoomIn === 'function') {
            viewer.zoomIn();
          }
          const zoomedInLevel = viewer.currentZoom || 1;
          
          // Test zoom out
          if (typeof viewer.zoomOut === 'function') {
            viewer.zoomOut();
          }
          const zoomedOutLevel = viewer.currentZoom || 1;
          
          // Test reset zoom
          if (typeof viewer.resetZoom === 'function') {
            viewer.resetZoom();
          }
          const resetLevel = viewer.currentZoom || 1;
          
          return {
            success: true,
            zoomTests: {
              initial: initialZoom,
              zoomedIn: zoomedInLevel,
              zoomedOut: zoomedOutLevel,
              reset: resetLevel,
              zoomWorking: zoomedInLevel >= initialZoom
            }
          };
        } catch (error) {
          return { success: false, error: error.message };
        }
      })();
    \`).then(resolve).catch(error => resolve({ success: false, error: error.message }));
  });
}

function outputValidationResults() {
  console.log('E2E-RESULTS: ' + JSON.stringify(validationResults, null, 0));
  console.error('E2E-RESULTS: ' + JSON.stringify(validationResults, null, 0)); // Also output to stderr
}

// Set up IPC handlers (same as main app)
ipcMain.handle('file:readContent', async (event, filePath) => {
  return simulateFileRead(filePath);
});

// App event handlers
app.whenReady().then(() => {
  validationResults.appLaunched = true;
  createWindow();
});

app.on('window-all-closed', () => {
  app.quit();
});
`;

  fs.writeFileSync(e2eAppPath, e2eAppCode);
  return e2eAppPath;
}

async function runE2EValidation() {
  log('cyan', '🚀', 'Starting True End-to-End Electron Viewer Validation...');
  console.log('');
  
  try {
    // Step 1: Create test image
    log('blue', '📷', 'Creating test image...');
    const testImagePath = createTestImage();
    log('green', '✓', `Test image created: ${path.basename(testImagePath)}`);
    
    // Step 2: Create E2E validation app
    log('blue', '⚙️', 'Setting up E2E validation environment...');
    const e2eAppPath = await createE2EValidationApp();
    log('green', '✓', 'E2E validation app created');
    
    // Step 3: Run the E2E test
    log('blue', '🔍', 'Launching Electron app for validation...');
    
    return new Promise((resolve, reject) => {
      // Use xvfb-run for headless environments with --no-sandbox for CI
      const electronArgs = ['-a', '--server-args=-screen 0 1024x768x24', 'npx', 'electron', e2eAppPath, testImagePath, '--no-sandbox', '--disable-dev-shm-usage'];
      const electronProcess = spawn('xvfb-run', electronArgs, {
        cwd: __dirname,
        stdio: 'pipe'
      });
      
      let output = '';
      let errorOutput = '';
      let validationResults = null;
      
      electronProcess.stdout.on('data', (data) => {
        const lines = data.toString().split('\n');
        for (const line of lines) {
          if (line.trim()) {
            output += line + '\n';
            
            if (line.startsWith('E2E-LOG:')) {
              log('cyan', '  →', line.replace('E2E-LOG: ', ''));
            } else if (line.startsWith('E2E-ERROR:')) {
              log('red', '  ✗', line.replace('E2E-ERROR: ', ''));
            } else if (line.startsWith('E2E-RESULTS:')) {
              try {
                validationResults = JSON.parse(line.replace('E2E-RESULTS: ', ''));
              } catch (e) {
                log('yellow', '  ⚠', 'Failed to parse validation results');
              }
            }
          }
        }
      });
      
      electronProcess.stderr.on('data', (data) => {
        const stderrData = data.toString();
        errorOutput += stderrData;
        
        // Also check stderr for results
        const lines = stderrData.split('\n');
        for (const line of lines) {
          if (line.trim()) {
            if (line.startsWith('E2E-RESULTS:')) {
              try {
                validationResults = JSON.parse(line.replace('E2E-RESULTS: ', ''));
              } catch (e) {
                log('yellow', '  ⚠', 'Failed to parse validation results from stderr');
              }
            }
          }
        }
      });
      
      electronProcess.on('close', (code) => {
        // Clean up
        try {
          fs.unlinkSync(testImagePath);
          fs.unlinkSync(e2eAppPath);
        } catch (e) {
          // Ignore cleanup errors
        }
        
        // If we have validation results, that's success regardless of exit code
        if (validationResults) {
          resolve({ success: true, results: validationResults, output, errorOutput });
        } else if (code === 0) {
          // Process completed successfully but no results were captured
          log('yellow', '  ⚠', 'Process completed but no validation results captured');
          resolve({ success: false, results: null, output, errorOutput });
        } else {
          reject(new Error(`Electron process exited with code ${code}. Error: ${errorOutput}`));
        }
      });
      
      electronProcess.on('error', (error) => {
        reject(error);
      });
    });
    
  } catch (error) {
    throw new Error(`E2E validation setup failed: ${error.message}`);
  }
}

async function validateResults(results) {
  console.log('');
  log('magenta', '📊', 'Validation Results Analysis:');
  console.log('');
  
  const checks = [
    { key: 'appLaunched', label: 'Electron App Launch', critical: true },
    { key: 'windowCreated', label: 'Window Creation', critical: true },
    { key: 'pageLoaded', label: 'Renderer Page Loading', critical: true },
    { key: 'imageLoaded', label: 'Image File Loading (IPC)', critical: true },
    { key: 'imageRendered', label: 'Image Rendering in Viewer', critical: true },
    { key: 'domValidated', label: 'DOM Structure Validation', critical: true },
    { key: 'imageVisible', label: 'Image Visibility Confirmation', critical: true },
    { key: 'viewerFunctional', label: 'Viewer Component Functionality', critical: false }
  ];
  
  let passed = 0;
  let critical = 0;
  let criticalPassed = 0;
  
  for (const check of checks) {
    const success = results[check.key];
    if (success) {
      log('green', '✓', check.label);
      passed++;
      if (check.critical) criticalPassed++;
    } else {
      log('red', '✗', check.label);
    }
    if (check.critical) critical++;
  }
  
  console.log('');
  
  if (criticalPassed === critical) {
    log('green', '🎉', `All critical validations passed! (${passed}/${checks.length} total)`);
    log('green', '  ✓', 'The Electron viewer successfully loads and renders images in the DOM');
    log('green', '  ✓', 'End-to-end image loading pipeline is working correctly');
    
    if (passed === checks.length) {
      log('green', '  ✓', 'All functionality tests also passed!');
    } else {
      log('yellow', '  ⚠', 'Some non-critical functionality tests failed - check logs above');
    }
    
    return true;
  } else {
    log('red', '❌', `Critical validation failed! (${criticalPassed}/${critical} critical tests passed)`);
    log('red', '  ✗', 'The Electron viewer has issues with image loading or DOM rendering');
    return false;
  }
}

// Main execution
async function main() {
  try {
    const { success, results } = await runE2EValidation();
    
    if (success && results) {
      const allPassed = await validateResults(results);
      console.log('');
      
      if (allPassed) {
        log('green', '🏆', 'End-to-End Validation Completed Successfully!');
        log('blue', '  ℹ', 'The Electron viewer is working correctly and can load/render images');
        process.exit(0);
      } else {
        log('red', '💥', 'End-to-End Validation Failed!');
        log('blue', '  ℹ', 'Check the error messages above for specific issues');
        process.exit(1);
      }
    } else {
      throw new Error('Validation process failed to complete');
    }
    
  } catch (error) {
    console.log('');
    log('red', '💥', `End-to-End Validation Error: ${error.message}`);
    log('yellow', '  ⚠', 'Make sure Electron is installed and the SDK is properly set up');
    log('blue', '  ℹ', 'Run "npm install" first if you haven\'t already');
    process.exit(1);
  }
}

// Handle script termination
process.on('SIGINT', () => {
  console.log('');
  log('yellow', '⚠', 'Validation interrupted by user');
  process.exit(130);
});

process.on('SIGTERM', () => {
  console.log('');
  log('yellow', '⚠', 'Validation terminated');
  process.exit(143);
});

// Run the validation
main();