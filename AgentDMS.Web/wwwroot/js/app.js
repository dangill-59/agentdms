// AgentDMS Web UI JavaScript
let progressConnection = null;

document.addEventListener('DOMContentLoaded', function() {
    // Initialize the application
    init();
});

async function init() {
    // Initialize SignalR connection
    await initializeSignalR();
    
    // Load supported formats
    loadSupportedFormats();
    
    // Load Mistral configuration
    loadMistralConfig();
    
    // Initialize scanner functionality
    await initializeScannerInterface();
    
    // Bind event handlers
    bindEventHandlers();
    
    // Initialize image zoom controls
    initializeImageZoomControls();
}

// Initialize SignalR connection for real-time progress updates
async function initializeSignalR() {
    try {
        progressConnection = new signalR.HubConnectionBuilder()
            .withUrl("/progressHub")
            .withAutomaticReconnect()
            .build();

        progressConnection.on("ProgressUpdate", handleProgressUpdate);

        await progressConnection.start();
        console.log("SignalR connection established for real-time progress updates");
    } catch (error) {
        console.error("Error establishing SignalR connection:", error);
    }
}

// Helper function to safely invoke SignalR methods
async function safeSignalRInvoke(methodName, ...args) {
    try {
        if (progressConnection && progressConnection.state === signalR.HubConnectionState.Connected) {
            await progressConnection.invoke(methodName, ...args);
        } else if (progressConnection && progressConnection.state === signalR.HubConnectionState.Disconnected) {
            console.log("SignalR disconnected, attempting to reconnect...");
            await progressConnection.start();
            await progressConnection.invoke(methodName, ...args);
        } else {
            console.log(`SignalR not available (state: ${progressConnection?.state}), skipping ${methodName}`);
        }
    } catch (error) {
        console.warn(`SignalR ${methodName} failed:`, error.message);
        // Don't throw - allow the main operation to continue
    }
}

// Handle real-time progress updates from SignalR
function handleProgressUpdate(progress) {
    console.log("Progress update received:", progress);
    
    // Update the appropriate progress display based on the active tab
    const activeTab = document.querySelector('.nav-link.active');
    if (activeTab) {
        const targetTab = activeTab.getAttribute('data-bs-target');
        
        if (targetTab === '#upload') {
            updateUploadProgress(progress);
        } else if (targetTab === '#batch') {
            updateBatchProgress(progress);
        }
    }
}

function bindEventHandlers() {
    // Upload form
    document.getElementById('uploadForm').addEventListener('submit', handleUpload);
    
    // Batch process form
    document.getElementById('batchForm').addEventListener('submit', handleBatchProcess);
    
    // Folder selection for batch processing
    document.getElementById('folderSelectBtn').addEventListener('click', handleFolderSelection);
    document.getElementById('folderInput').addEventListener('change', handleFolderInputChange);
    
    // Gallery form
    document.getElementById('galleryForm').addEventListener('submit', handleGalleryGeneration);
    
    // Mistral configuration form
    document.getElementById('mistralConfigForm').addEventListener('submit', saveMistralConfig);
    document.getElementById('testConfigBtn').addEventListener('click', testMistralConfig);
    document.getElementById('mistralTemperature').addEventListener('input', updateTemperatureDisplay);
    
    // OCR enable/disable checkboxes
    document.getElementById('enableOcr').addEventListener('change', handleOcrToggle);
    document.getElementById('batchEnableOcr').addEventListener('change', handleBatchOcrToggle);
}

// Initialize image zoom controls
function initializeImageZoomControls() {
    const savedZoom = localStorage.getItem('imageZoom') || '100';
    
    // Single view zoom slider
    const singleSlider = document.getElementById('imageZoomSlider');
    const singleDisplay = document.getElementById('imageZoomDisplay');
    
    if (singleSlider && singleDisplay) {
        singleSlider.value = savedZoom;
        singleDisplay.textContent = savedZoom + '%';
        
        singleSlider.addEventListener('input', function() {
            const zoom = this.value;
            singleDisplay.textContent = zoom + '%';
            updateImageZoom('imageViewer', zoom);
            
            // Also update the batch slider to keep them in sync
            const batchSlider = document.getElementById('batchImageZoomSlider');
            const batchDisplay = document.getElementById('batchImageZoomDisplay');
            if (batchSlider && batchDisplay) {
                batchSlider.value = zoom;
                batchDisplay.textContent = zoom + '%';
                updateImageZoom('batchImageViewer', zoom);
            }
            
            localStorage.setItem('imageZoom', zoom);
        });
    }
    
    // Batch view zoom slider
    const batchSlider = document.getElementById('batchImageZoomSlider');
    const batchDisplay = document.getElementById('batchImageZoomDisplay');
    
    if (batchSlider && batchDisplay) {
        batchSlider.value = savedZoom;
        batchDisplay.textContent = savedZoom + '%';
        
        batchSlider.addEventListener('input', function() {
            const zoom = this.value;
            batchDisplay.textContent = zoom + '%';
            updateImageZoom('batchImageViewer', zoom);
            
            // Also update the single slider to keep them in sync
            const singleSlider = document.getElementById('imageZoomSlider');
            const singleDisplay = document.getElementById('imageZoomDisplay');
            if (singleSlider && singleDisplay) {
                singleSlider.value = zoom;
                singleDisplay.textContent = zoom + '%';
                updateImageZoom('imageViewer', zoom);
            }
            
            localStorage.setItem('imageZoom', zoom);
        });
    }
    
    // Apply initial zoom to both viewers
    updateImageZoom('imageViewer', savedZoom);
    updateImageZoom('batchImageViewer', savedZoom);
    
    // Initialize modal zoom slider
    const modalSlider = document.getElementById('modalZoomSlider');
    if (modalSlider) {
        modalSlider.addEventListener('input', function() {
            const zoom = this.value;
            const modalImage = document.getElementById('modalImage');
            if (modalImage) {
                modalImage.style.transform = `scale(${zoom})`;
            }
        });
    }
}

// Update image zoom for a specific viewer
function updateImageZoom(viewerId, zoom) {
    const viewer = document.getElementById(viewerId);
    if (viewer) {
        viewer.style.setProperty('--image-zoom', zoom);
    }
}

// Helper function to convert absolute file paths to HTTP URLs
function convertToHttpUrl(filePath) {
    if (!filePath) return '';
    
    // Check if it's already an HTTP URL
    if (filePath.startsWith('http://') || filePath.startsWith('https://')) {
        return filePath;
    }
    
    // Convert absolute file path to HTTP URL
    // Check for both AgentDMS_Output and AgentDMS_Scans directories
    const outputFolders = ['AgentDMS_Output', 'AgentDMS_Scans'];
    
    for (const outputFolderName of outputFolders) {
        const outputIndex = filePath.indexOf(outputFolderName);
        
        if (outputIndex !== -1) {
            // Extract the relative path from the output folder onwards
            // This handles both Windows (C:\...\AgentDMS_Scans\file.png) and Unix (/tmp/AgentDMS_Scans/file.png)
            const relativePath = filePath.substring(outputIndex);
            return '/' + relativePath.replace(/\\/g, '/'); // Normalize path separators
        }
    }
    
    // Fallback: return empty string if we can't convert
    console.warn('Could not convert file path to HTTP URL:', filePath);
    return '';
}

// Poll for job completion
async function pollForJobCompletion(jobId, maxAttempts = 120, intervalMs = 2000) {
    let attempts = 0;
    
    while (attempts < maxAttempts) {
        try {
            const statusResponse = await fetch(`/api/imageprocessing/job/${jobId}/status`);
            
            if (statusResponse.ok) {
                const statusData = await statusResponse.json();
                
                if (statusData.status === 'Completed') {
                    // Job completed successfully, get the result
                    const resultResponse = await fetch(`/api/imageprocessing/job/${jobId}/result`);
                    
                    if (resultResponse.ok) {
                        return await resultResponse.json();
                    } else {
                        throw new Error('Failed to get job result');
                    }
                } else if (statusData.status === 'Failed') {
                    throw new Error(statusData.errorMessage || 'Job failed');
                }
                
                // Job is still processing, continue polling
                // Progress updates are handled via SignalR
            }
            
            // Wait before next poll
            await new Promise(resolve => setTimeout(resolve, intervalMs));
            attempts++;
            
        } catch (error) {
            console.error('Error polling job status:', error);
            
            // If it's the last attempt, throw the error
            if (attempts >= maxAttempts - 1) {
                throw error;
            }
            
            // Otherwise, wait and retry
            await new Promise(resolve => setTimeout(resolve, intervalMs));
            attempts++;
        }
    }
    
    throw new Error('Job polling timed out');
}

// API Helper functions
async function apiCall(endpoint, options = {}) {
    const baseUrl = window.location.origin;
    
    try {
        const response = await fetch(`${baseUrl}/api/${endpoint}`, {
            headers: {
                'Content-Type': 'application/json',
                ...options.headers
            },
            ...options
        });
        
        if (!response.ok) {
            const errorData = await response.json().catch(() => ({}));
            throw new Error(errorData.error || `HTTP error! status: ${response.status}`);
        }
        
        return await response.json();
    } catch (error) {
        console.error(`API call failed for ${endpoint}:`, error);
        throw error;
    }
}

// Load supported formats
async function loadSupportedFormats() {
    try {
        const formats = await apiCall('imageprocessing/formats');
        displayFormats(formats);
    } catch (error) {
        displayFormatsError(error.message);
    }
}

function displayFormats(formats) {
    const formatsList = document.getElementById('formatsList');
    
    const formatDescriptions = {
        '.jpg': { icon: 'bi-image', name: 'JPEG', desc: 'Joint Photographic Experts Group - Compressed images' },
        '.jpeg': { icon: 'bi-image', name: 'JPEG', desc: 'Joint Photographic Experts Group - Compressed images' },
        '.png': { icon: 'bi-image', name: 'PNG', desc: 'Portable Network Graphics - Lossless compression' },
        '.bmp': { icon: 'bi-image', name: 'BMP', desc: 'Windows Bitmap - Uncompressed raster graphics' },
        '.gif': { icon: 'bi-image', name: 'GIF', desc: 'Graphics Interchange Format - Animated images' },
        '.tif': { icon: 'bi-layers', name: 'TIFF', desc: 'Tagged Image File Format - Multipage support' },
        '.tiff': { icon: 'bi-layers', name: 'TIFF', desc: 'Tagged Image File Format - Multipage support' },
        '.pdf': { icon: 'bi-file-earmark-pdf', name: 'PDF', desc: 'Portable Document Format - Multipage documents' },
        '.webp': { icon: 'bi-image', name: 'WebP', desc: 'Modern image format by Google' }
    };
    
    const uniqueFormats = [...new Set(formats)];
    
    formatsList.innerHTML = `
        <div class="format-grid">
            ${uniqueFormats.map(format => {
                const info = formatDescriptions[format] || { icon: 'bi-file', name: format.toUpperCase(), desc: 'Supported format' };
                return `
                    <div class="format-item">
                        <div class="format-icon">
                            <i class="bi ${info.icon}"></i>
                        </div>
                        <div class="format-name">${info.name}</div>
                        <div class="format-description">${info.desc}</div>
                        <small class="text-muted">${format}</small>
                    </div>
                `;
            }).join('')}
        </div>
    `;
}

function displayFormatsError(error) {
    document.getElementById('formatsList').innerHTML = `
        <div class="alert alert-danger">
            <i class="bi bi-exclamation-triangle"></i>
            Failed to load supported formats: ${error}
        </div>
    `;
}

// Handle file upload
async function handleUpload(event) {
    event.preventDefault();
    
    const fileInput = document.getElementById('imageFile');
    const uploadBtn = document.getElementById('uploadBtn');
    const progressDiv = document.getElementById('uploadProgress');
    const resultDiv = document.getElementById('uploadResult');
    const imageViewer = document.getElementById('imageViewer');
    
    if (!fileInput.files[0]) {
        showError(resultDiv, 'Please select a file to upload.');
        return;
    }
    
    // Clear previous results and images
    resultDiv.innerHTML = '';
    clearImageViewer(imageViewer);
    
    // Show progress
    showProgress(progressDiv, uploadBtn, 'Uploading file...');
    
    const renderStartTime = performance.now();
    let jobId = null;
    
    try {
        const formData = new FormData();
        formData.append('file', fileInput.files[0]);
        
        // Add Mistral AI flag
        const useMistralAI = document.getElementById('useMistralAI').checked;
        formData.append('useMistralAI', useMistralAI.toString());
        
        // Add OCR enable flag
        const enableOcr = document.getElementById('enableOcr').checked;
        formData.append('enableOcr', enableOcr.toString());
        
        // Add Mistral OCR flag
        const useMistralOcr = document.getElementById('useMistralOcr').checked;
        formData.append('useMistralOcr', useMistralOcr.toString());
        
        const response = await fetch('/api/imageprocessing/upload', {
            method: 'POST',
            body: formData
        });
        
        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Upload failed');
        }
        
        const uploadResponse = await response.json();
        jobId = uploadResponse.jobId;
        
        // Show upload success and start progress monitoring
        updateUploadProgress({
            jobId: jobId,
            status: 'Processing',
            statusMessage: 'File uploaded successfully. Starting processing...',
            progressPercentage: 0
        });
        
        // Join the SignalR group to receive progress updates for this job
        if (jobId) {
            await safeSignalRInvoke("JoinJob", jobId);
        }
        
        // Poll for job completion
        const result = await pollForJobCompletion(jobId);
        
        if (result) {
            // Calculate rendering time
            const renderEndTime = performance.now();
            const renderingTime = renderEndTime - renderStartTime;
            result.renderingTime = formatDuration(renderingTime);
            
            // Display results and images
            displayProcessingResult(resultDiv, result);
            displayImagesInViewer(imageViewer, [result], renderingTime);
        }
        
    } catch (error) {
        showError(resultDiv, `Upload failed: ${error.message}`);
    } finally {
        hideProgress(progressDiv, uploadBtn);
        
        // Leave the SignalR group
        if (jobId) {
            await safeSignalRInvoke("LeaveJob", jobId);
        }
    }
}

// Handle batch processing
async function handleBatchProcess(event) {
    event.preventDefault();
    
    const pathsTextarea = document.getElementById('filePaths');
    const batchBtn = document.getElementById('batchBtn');
    const progressDiv = document.getElementById('batchProgress');
    const resultDiv = document.getElementById('batchResult');
    const imageViewer = document.getElementById('batchImageViewer');
    
    const filePaths = pathsTextarea.value
        .split('\n')
        .map(path => path.trim())
        .filter(path => path.length > 0);
    
    if (filePaths.length === 0) {
        showError(resultDiv, 'Please enter at least one file path.');
        return;
    }
    
    // Clear previous results and images
    resultDiv.innerHTML = '';
    clearImageViewer(imageViewer);
    
    showProgress(progressDiv, batchBtn, 'Starting batch processing...');
    
    const renderStartTime = performance.now();
    let jobId = null;
    let finalFilePaths = filePaths;
    
    try {
        // Check if we have File objects from folder selection that need to be uploaded first
        const hasFiles = pathsTextarea.getAttribute('data-has-files') === 'true';
        
        if (hasFiles && window.selectedFiles && window.selectedFiles.length > 0) {
            // Upload files first
            showProgress(progressDiv, batchBtn, 'Uploading files to server...');
            
            const formData = new FormData();
            window.selectedFiles.forEach(file => {
                formData.append('files', file);
            });
            
            const uploadResponse = await fetch('/api/imageprocessing/upload-batch', {
                method: 'POST',
                body: formData
            });
            
            if (!uploadResponse.ok) {
                const errorData = await uploadResponse.json();
                throw new Error(errorData.error || 'Failed to upload files');
            }
            
            const uploadResult = await uploadResponse.json();
            
            if (uploadResult.errorCount > 0) {
                console.warn('Some files failed to upload:', uploadResult.errors);
            }
            
            if (uploadResult.successCount === 0) {
                throw new Error('No files were successfully uploaded');
            }
            
            // Use the server file paths for processing
            finalFilePaths = uploadResult.uploadedFiles.map(file => file.serverFilePath);
            
            // Clear the file objects after successful upload
            window.selectedFiles = null;
            pathsTextarea.removeAttribute('data-has-files');
            
            showProgress(progressDiv, batchBtn, `Files uploaded (${uploadResult.successCount}/${uploadResult.successCount + uploadResult.errorCount}). Processing...`);
        }
        
        // Get Mistral processing flags
        const useMistralAI = document.getElementById('batchUseMistralAI').checked;
        const useMistralOcr = document.getElementById('batchUseMistralOcr').checked;
        const enableOcr = document.getElementById('batchEnableOcr').checked;
        
        const response = await fetch('/api/imageprocessing/batch-process', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ 
                filePaths: finalFilePaths,
                useMistralAI: useMistralAI,
                useMistralOcr: useMistralOcr,
                enableOcr: enableOcr
            })
        });
        
        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Batch processing failed');
        }
        
        const responseData = await response.json();
        jobId = responseData.jobId;
        const results = responseData.results;
        
        // Join the SignalR group to receive progress updates for this job
        if (jobId) {
            await safeSignalRInvoke("JoinJob", jobId);
        }
        
        // Calculate total batch processing time
        const batchEndTime = performance.now();
        const totalBatchTime = batchEndTime - renderStartTime;
        
        // Add rendering time to results
        results.forEach(result => {
            result.renderingTime = formatDuration(totalBatchTime / results.length);
        });
        
        displayBatchResults(resultDiv, results, totalBatchTime);
        displayImagesInViewer(imageViewer, results, totalBatchTime);
        
    } catch (error) {
        showError(resultDiv, `Batch processing failed: ${error.message}`);
    } finally {
        hideProgress(progressDiv, batchBtn);
        
        // Leave the SignalR group
        if (jobId) {
            await safeSignalRInvoke("LeaveJob", jobId);
        }
    }
}

// Handle folder selection button click
function handleFolderSelection() {
    const folderInput = document.getElementById('folderInput');
    folderInput.click();
}

// Handle folder input change (when folder is selected)
function handleFolderInputChange(event) {
    const files = event.target.files;
    const pathsTextarea = document.getElementById('filePaths');
    
    if (!files || files.length === 0) {
        return;
    }
    
    // Get supported extensions from the system
    const supportedExtensions = ['.jpg', '.jpeg', '.png', '.bmp', '.gif', '.tif', '.tiff', '.pdf', '.webp'];
    
    // Filter files to only include supported formats
    const supportedFiles = Array.from(files).filter(file => {
        const extension = '.' + file.name.split('.').pop().toLowerCase();
        return supportedExtensions.includes(extension);
    });
    
    if (supportedFiles.length === 0) {
        alert('No supported image files found in the selected folder.\nSupported formats: ' + supportedExtensions.join(', '));
        return;
    }
    
    // Store the File objects for later upload and generate display paths
    const filePaths = supportedFiles.map(file => file.webkitRelativePath || file.name);
    pathsTextarea.value = filePaths.join('\n');
    
    // Store the actual File objects for upload when processing starts
    pathsTextarea.setAttribute('data-has-files', 'true');
    window.selectedFiles = supportedFiles;
    
    // Show success message
    const folderName = supportedFiles[0].webkitRelativePath ? supportedFiles[0].webkitRelativePath.split('/')[0] : 'selected folder';
    showSuccessMessage(`Found ${supportedFiles.length} supported image(s) in "${folderName}". Files will be uploaded when processing starts.`);
    
    // Clear the file input for next selection
    event.target.value = '';
}

// Helper function to show success message
function showSuccessMessage(message) {
    // Create a temporary success alert
    const alertDiv = document.createElement('div');
    alertDiv.className = 'alert alert-success alert-dismissible fade show mt-2';
    alertDiv.innerHTML = `
        <i class="bi bi-check-circle"></i> ${message}
        <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
    `;
    
    // Insert after the folder selection controls
    const formElement = document.getElementById('batchForm');
    const firstChild = formElement.querySelector('.mb-3');
    firstChild.appendChild(alertDiv);
    
    // Auto-dismiss after 5 seconds
    setTimeout(() => {
        if (alertDiv && alertDiv.parentNode) {
            alertDiv.remove();
        }
    }, 5000);
}

// Handle gallery generation
async function handleGalleryGeneration(event) {
    event.preventDefault();
    
    const pathsTextarea = document.getElementById('galleryPaths');
    const titleInput = document.getElementById('galleryTitle');
    const sizeSelect = document.getElementById('imageSize');
    const galleryBtn = document.getElementById('galleryBtn');
    const progressDiv = document.getElementById('galleryProgress');
    const resultDiv = document.getElementById('galleryResult');
    
    const imagePaths = pathsTextarea.value
        .split('\n')
        .map(path => path.trim())
        .filter(path => path.length > 0);
    
    if (imagePaths.length === 0) {
        showError(resultDiv, 'Please enter at least one image path.');
        return;
    }
    
    showProgress(progressDiv, galleryBtn, 'Generating gallery...');
    resultDiv.innerHTML = '';
    
    try {
        const result = await apiCall('imageprocessing/generate-gallery', {
            method: 'POST',
            body: JSON.stringify({
                imagePaths,
                title: titleInput.value || 'AgentDMS Gallery',
                thumbnailSize: parseInt(sizeSelect.value)
            })
        });
        
        displayGalleryResult(resultDiv, result);
        
    } catch (error) {
        showError(resultDiv, `Gallery generation failed: ${error.message}`);
    } finally {
        hideProgress(progressDiv, galleryBtn);
    }
}

// Display functions
function displayProcessingResult(container, result) {
    if (result.success && result.processedImage) {
        const img = result.processedImage;
        container.innerHTML = `
            <div class="alert alert-success fade-in">
                <h6><i class="bi bi-check-circle"></i> Processing Successful!</h6>
                <div class="row mt-3">
                    <div class="col-12">
                        <div class="file-info">
                            <div class="file-name">${img.fileName}</div>
                            <small class="text-muted">
                                Format: ${img.originalFormat} | 
                                Size: ${formatFileSize(img.fileSize)} |
                                Dimensions: ${img.width}×${img.height}px
                            </small>
                            ${img.isMultiPage ? `<br><small class="text-info">Multi-page document (${img.pageCount} pages)</small>` : ''}
                        </div>
                        
                        <!-- Detailed Timing Metrics -->
                        ${createTimingMetrics(result)}
                        
                        <!-- Mistral AI Analysis Results -->
                        ${createAiAnalysisDisplay(result.aiAnalysis)}
                        
                        <!-- OCR Text Results -->
                        ${createOcrResultsDisplay(result.extractedText)}
                        
                        <div class="mt-3">
                            <div class="row">
                                <div class="col-md-6">
                                    <small><strong>Processing Details:</strong></small>
                                    ${img.convertedPngPath ? `<br><small>PNG Version: ${img.convertedPngPath}</small>` : ''}
                                </div>
                                <div class="col-md-6">
                                    ${img.splitPagePaths && img.splitPagePaths.length > 0 ? `
                                        <small><strong>Split Pages:</strong></small>
                                        <ul class="list-unstyled ms-3">
                                            ${img.splitPagePaths.slice(0, 3).map(path => `<li><small>${path}</small></li>`).join('')}
                                            ${img.splitPagePaths.length > 3 ? `<li><small>... and ${img.splitPagePaths.length - 3} more</small></li>` : ''}
                                        </ul>
                                    ` : ''}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        `;
    } else {
        showError(container, result.message || 'Processing failed');
    }
}

function createTimingMetrics(result) {
    if (!result.metrics && !result.processingTime && !result.renderingTime) {
        return '';
    }
    
    const metrics = result.metrics || {};
    const processingTime = result.processingTime || '0ms';
    const renderingTime = result.renderingTime || '0ms';
    
    return `
        <div class="timing-metrics">
            <h6><i class="bi bi-stopwatch"></i> Performance Metrics</h6>
            ${metrics.fileLoadTime ? `
                <div class="timing-row">
                    <span class="timing-label">File Load:</span>
                    <span class="timing-value">${formatDuration(metrics.fileLoadTime)}</span>
                </div>
            ` : ''}
            ${metrics.imageDecodeTime ? `
                <div class="timing-row">
                    <span class="timing-label">Image Decode:</span>
                    <span class="timing-value">${formatDuration(metrics.imageDecodeTime)}</span>
                </div>
            ` : ''}
            ${metrics.conversionTime ? `
                <div class="timing-row">
                    <span class="timing-label">Format Conversion:</span>
                    <span class="timing-value">${formatDuration(metrics.conversionTime)}</span>
                </div>
            ` : ''}
            ${metrics.thumbnailGenerationTime ? `
                <div class="timing-row">
                    <span class="timing-label">Thumbnail Generation:</span>
                    <span class="timing-value">${formatDuration(metrics.thumbnailGenerationTime)}</span>
                </div>
            ` : ''}
            ${metrics.aiAnalysisTime ? `
                <div class="timing-row">
                    <span class="timing-label">AI Analysis:</span>
                    <span class="timing-value">${formatDuration(metrics.aiAnalysisTime)}</span>
                </div>
            ` : ''}
            ${metrics.ocrProcessingTime ? `
                <div class="timing-row">
                    <span class="timing-label">OCR Processing:</span>
                    <span class="timing-value">${formatDuration(metrics.ocrProcessingTime)}</span>
                </div>
            ` : ''}
            <div class="timing-row">
                <span class="timing-label">Total Processing:</span>
                <span class="timing-value">${formatDuration(processingTime)}</span>
            </div>
            <div class="timing-row">
                <span class="timing-label">UI Rendering:</span>
                <span class="timing-value">${formatDuration(renderingTime)}</span>
            </div>
            ${metrics.ocrMethod || metrics.mistralModel || metrics.ocrConfidence !== undefined || metrics.extractedTextLength !== undefined ? `
                <div class="timing-row ocr-info-row">
                    <span class="timing-label">OCR Information:</span>
                    <span class="timing-value">
                        ${metrics.ocrMethod ? `Method: ${metrics.ocrMethod}` : ''}
                        ${metrics.mistralModel ? ` | Model: ${metrics.mistralModel}` : ''}
                        ${metrics.ocrConfidence !== undefined ? ` | Confidence: ${(metrics.ocrConfidence * 100).toFixed(1)}%` : ''}
                        ${metrics.extractedTextLength !== undefined ? ` | Text: ${metrics.extractedTextLength} chars` : ''}
                    </span>
                </div>
            ` : ''}
        </div>
    `;
}

function createAiAnalysisDisplay(aiAnalysis) {
    if (!aiAnalysis) {
        return '';
    }
    
    // Handle failed AI analysis - show error information
    if (!aiAnalysis.success) {
        return `
            <div class="ai-analysis-section mt-3">
                <h6><i class="bi bi-robot"></i> Mistral AI Analysis</h6>
                <div class="ai-analysis-content">
                    <div class="alert alert-warning">
                        <i class="bi bi-exclamation-triangle"></i>
                        <strong>AI Analysis Failed:</strong> ${aiAnalysis.message || 'Unknown error occurred'}
                        <div class="small mt-1 text-muted">
                            OCR text extraction was successful, but AI document analysis encountered an issue. 
                            This may be due to API configuration, network connectivity, or service availability.
                        </div>
                    </div>
                </div>
            </div>
        `;
    }
    
    const confidenceClass = aiAnalysis.confidence >= 0.8 ? 'text-success' : 
                           aiAnalysis.confidence >= 0.5 ? 'text-warning' : 'text-danger';
    
    return `
        <div class="ai-analysis-section mt-3">
            <h6><i class="bi bi-robot"></i> Mistral AI Analysis</h6>
            <div class="ai-analysis-content">
                <div class="row">
                    <div class="col-md-6">
                        <div class="ai-field">
                            <strong>Document Type:</strong> 
                            <span class="badge bg-primary ms-1">${aiAnalysis.documentType || 'Unknown'}</span>
                        </div>
                        <div class="ai-field">
                            <strong>Confidence:</strong> 
                            <span class="${confidenceClass} fw-bold ms-1">${(aiAnalysis.confidence * 100).toFixed(1)}%</span>
                        </div>
                    </div>
                    <div class="col-md-6">
                        ${aiAnalysis.summary ? `
                            <div class="ai-field">
                                <strong>Summary:</strong>
                                <div class="text-muted small mt-1">${aiAnalysis.summary}</div>
                            </div>
                        ` : ''}
                    </div>
                </div>
                ${Object.keys(aiAnalysis.extractedData || {}).length > 0 ? `
                    <div class="ai-extracted-data mt-2">
                        <strong>Extracted Data:</strong>
                        <div class="row mt-1">
                            ${Object.entries(aiAnalysis.extractedData).map(([key, value]) => `
                                <div class="col-md-6 mb-1">
                                    <small><strong>${key}:</strong> ${value}</small>
                                </div>
                            `).join('')}
                        </div>
                    </div>
                ` : ''}
            </div>
        </div>
    `;
}

function createOcrResultsDisplay(extractedText) {
    if (!extractedText || extractedText.trim() === '') {
        return '';
    }
    
    // Truncate very long text for initial display
    const maxLength = 300;
    const isLongText = extractedText.length > maxLength;
    const truncatedText = isLongText ? extractedText.substring(0, maxLength) + '...' : extractedText;
    
    // Create unique ID for this OCR section
    const ocrId = 'ocr-' + Math.random().toString(36).substr(2, 9);
    
    // Store full text in a data attribute for retrieval
    const fullTextEncoded = encodeURIComponent(extractedText);
    
    return `
        <div class="ocr-results-section mt-3">
            <h6>
                <i class="bi bi-text-paragraph"></i> OCR Text Results
                <button class="btn btn-sm btn-outline-secondary ms-2" type="button" 
                        onclick="toggleOcrText('${ocrId}')" id="toggle-${ocrId}">
                    <i class="bi bi-eye"></i> Show Text
                </button>
            </h6>
            <div class="ocr-content" id="${ocrId}" style="display: none;" data-full-text="${fullTextEncoded}">
                <div class="card border-light">
                    <div class="card-body">
                        <div class="ocr-text-preview">
                            <small class="text-muted">Extracted ${extractedText.length} characters</small>
                            <pre class="ocr-text mt-2" style="white-space: pre-wrap; font-size: 0.85em; max-height: 200px; overflow-y: auto;">${isLongText ? truncatedText : extractedText}</pre>
                            ${isLongText ? `
                                <div class="mt-2">
                                    <button class="btn btn-sm btn-link p-0" onclick="showFullOcrText('${ocrId}')">
                                        <i class="bi bi-arrows-expand"></i> Show Full Text
                                    </button>
                                </div>
                            ` : ''}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    `;
}

function displayBatchResults(container, results, totalBatchTime) {
    const successful = results.filter(r => r.success).length;
    const failed = results.length - successful;
    const total = results.length;
    
    let html = `
        <div class="alert alert-info fade-in">
            <h6><i class="bi bi-collection"></i> Batch Processing Complete</h6>
            <p class="mb-1">
                <span class="badge bg-success me-2">${successful} Successful</span>
                <span class="badge bg-danger me-2">${failed} Failed</span>
                <span class="badge bg-secondary">${total} Total</span>
            </p>
            <div class="mt-2">
                <small class="text-muted">
                    Success Rate: ${(successful / total * 100).toFixed(1)}% | 
                    Failure Rate: ${(failed / total * 100).toFixed(1)}%
                </small>
            </div>
        </div>
    `;
    
    // Add final batch summary report before individual results
    const finalStats = calculateFinalBatchStatistics(results, totalBatchTime);
    html += createFinalBatchSummary(finalStats);
    
    // Show comprehensive timing summary for successful results
    if (successful > 0) {
        const successfulResults = results.filter(r => r.success);
        const processingTimes = successfulResults
            .map(r => r.processingTime ? parseFloat(r.processingTime) : 0)
            .filter(t => t > 0);
        
        if (processingTimes.length > 0) {
            const totalProcessingTime = processingTimes.reduce((sum, time) => sum + time, 0);
            const avgProcessingTime = totalProcessingTime / processingTimes.length;
            const minProcessingTime = Math.min(...processingTimes);
            const maxProcessingTime = Math.max(...processingTimes);
            const medianProcessingTime = getMedian(processingTimes);
            
            // Collect step-specific metrics
            const stepMetrics = collectStepMetrics(successfulResults);
            
            html += `
                <div class="timing-metrics">
                    <h6><i class="bi bi-stopwatch"></i> Comprehensive Batch Statistics</h6>
                    
                    <!-- Overall Statistics -->
                    <div class="metrics-section">
                        <h6 class="metrics-subtitle"><i class="bi bi-bar-chart"></i> Overall Performance</h6>
                        <div class="timing-row">
                            <span class="timing-label">Total Files Attempted:</span>
                            <span class="timing-value">${total}</span>
                        </div>
                        <div class="timing-row">
                            <span class="timing-label">Files with Metrics:</span>
                            <span class="timing-value">${processingTimes.length}</span>
                        </div>
                        <div class="timing-row">
                            <span class="timing-label">Average Processing Time:</span>
                            <span class="timing-value">${avgProcessingTime.toFixed(2)}s</span>
                        </div>
                        <div class="timing-row">
                            <span class="timing-label">Minimum Processing Time:</span>
                            <span class="timing-value">${minProcessingTime.toFixed(2)}s</span>
                        </div>
                        <div class="timing-row">
                            <span class="timing-label">Maximum Processing Time:</span>
                            <span class="timing-value">${maxProcessingTime.toFixed(2)}s</span>
                        </div>
                        <div class="timing-row">
                            <span class="timing-label">Median Processing Time:</span>
                            <span class="timing-value">${medianProcessingTime.toFixed(2)}s</span>
                        </div>
                        <div class="timing-row">
                            <span class="timing-label">Total Processing Time:</span>
                            <span class="timing-value total">${totalProcessingTime.toFixed(2)}s</span>
                        </div>
                    </div>
                    
                    ${stepMetrics.length > 0 ? `
                        <!-- Step-by-Step Breakdown -->
                        <div class="metrics-section">
                            <h6 class="metrics-subtitle"><i class="bi bi-gear"></i> Processing Step Breakdown</h6>
                            ${stepMetrics.map(step => `
                                <div class="step-metrics">
                                    <strong>${step.name}:</strong>
                                    <div class="timing-row">
                                        <span class="timing-label">Files Processed:</span>
                                        <span class="timing-value">${step.count}</span>
                                    </div>
                                    <div class="timing-row">
                                        <span class="timing-label">Average Time:</span>
                                        <span class="timing-value">${step.avg.toFixed(2)}s</span>
                                    </div>
                                    <div class="timing-row">
                                        <span class="timing-label">Total Time:</span>
                                        <span class="timing-value">${step.total.toFixed(2)}s</span>
                                    </div>
                                    <div class="timing-row">
                                        <span class="timing-label">Min/Max:</span>
                                        <span class="timing-value">${step.min.toFixed(2)}s / ${step.max.toFixed(2)}s</span>
                                    </div>
                                </div>
                            `).join('')}
                        </div>
                    ` : ''}
                </div>
            `;
        }
    }
    
    results.forEach((result, index) => {
        if (result.success && result.processedImage) {
            // Use the same detailed layout as the Upload and Process tab
            const img = result.processedImage;
            html += `
                <div class="alert alert-success fade-in mt-3">
                    <h6><i class="bi bi-check-circle"></i> File ${index + 1}: Processing Successful!</h6>
                    <div class="row mt-3">
                        <div class="col-12">
                            <div class="file-info">
                                <div class="file-name">${img.fileName}</div>
                                <small class="text-muted">
                                    Format: ${img.originalFormat} | 
                                    Size: ${formatFileSize(img.fileSize)} |
                                    Dimensions: ${img.width}×${img.height}px
                                </small>
                                ${img.isMultiPage ? `<br><small class="text-info">Multi-page document (${img.pageCount} pages)</small>` : ''}
                            </div>
                            
                            <!-- Detailed Timing Metrics -->
                            ${createTimingMetrics(result)}
                            
                            <!-- Mistral AI Analysis Results -->
                            ${createAiAnalysisDisplay(result.aiAnalysis)}
                            
                            <!-- OCR Text Results -->
                            ${createOcrResultsDisplay(result.extractedText)}
                            
                            <div class="mt-3">
                                <div class="row">
                                    <div class="col-md-6">
                                        <small><strong>Processing Details:</strong></small>
                                        ${img.convertedPngPath ? `<br><small>PNG Version: ${img.convertedPngPath}</small>` : ''}
                                    </div>
                                    <div class="col-md-6">
                                        ${img.splitPagePaths && img.splitPagePaths.length > 0 ? `
                                            <small><strong>Split Pages:</strong></small>
                                            <ul class="list-unstyled ms-3">
                                                ${img.splitPagePaths.slice(0, 3).map(path => `<li><small>${path}</small></li>`).join('')}
                                                ${img.splitPagePaths.length > 3 ? `<li><small>... and ${img.splitPagePaths.length - 3} more</small></li>` : ''}
                                            </ul>
                                        ` : ''}
                                    </div>
                                </div>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        } else {
            // Error case - keep the simpler layout
            html += `
                <div class="result-item error mt-3">
                    <div class="alert alert-danger">
                        <div class="d-flex justify-content-between align-items-start">
                            <div>
                                <div class="file-name">
                                    <i class="bi bi-x-circle"></i> File ${index + 1}: ${result.processedImage?.fileName || 'Unknown'}
                                </div>
                                <small class="text-danger">${result.message}</small>
                            </div>
                        </div>
                    </div>
                </div>
            `;
        }
    });
    
    container.innerHTML = html;
}

// Helper functions for batch statistics
function getMedian(values) {
    if (!values || values.length === 0) return 0;
    
    const sorted = [...values].sort((a, b) => a - b);
    const middle = Math.floor(sorted.length / 2);
    
    if (sorted.length % 2 === 0) {
        return (sorted[middle - 1] + sorted[middle]) / 2;
    } else {
        return sorted[middle];
    }
}

function collectStepMetrics(successfulResults) {
    const stepMetrics = [];
    
    // Helper function to process a step
    function processStep(stepName, timeExtractor) {
        const times = successfulResults
            .map(timeExtractor)
            .filter(time => time && time > 0);
            
        if (times.length > 0) {
            return {
                name: stepName,
                count: times.length,
                avg: times.reduce((sum, time) => sum + time, 0) / times.length,
                min: Math.min(...times),
                max: Math.max(...times),
                total: times.reduce((sum, time) => sum + time, 0)
            };
        }
        return null;
    }
    
    // Check each processing step
    const stepConfigs = [
        {
            name: 'File Load',
            extractor: r => r.metrics?.fileLoadTime ? parseFloat(r.metrics.fileLoadTime) : 0
        },
        {
            name: 'Image Decode', 
            extractor: r => r.metrics?.imageDecodeTime ? parseFloat(r.metrics.imageDecodeTime) : 0
        },
        {
            name: 'Format Conversion',
            extractor: r => r.metrics?.conversionTime ? parseFloat(r.metrics.conversionTime) : 0
        },
        {
            name: 'Thumbnail Generation',
            extractor: r => r.metrics?.thumbnailGenerationTime ? parseFloat(r.metrics.thumbnailGenerationTime) : 0
        },
        {
            name: 'AI Analysis',
            extractor: r => r.metrics?.aiAnalysisTime ? parseFloat(r.metrics.aiAnalysisTime) : 0
        },
        {
            name: 'OCR Processing',
            extractor: r => r.metrics?.ocrProcessingTime ? parseFloat(r.metrics.ocrProcessingTime) : 0
        }
    ];
    
    stepConfigs.forEach(config => {
        const metric = processStep(config.name, config.extractor);
        if (metric) {
            stepMetrics.push(metric);
        }
    });
    
    return stepMetrics;
}

function calculateFinalBatchStatistics(results, totalBatchTime) {
    const total = results.length;
    const successful = results.filter(r => r.success).length;
    const failed = total - successful;
    const successfulResults = results.filter(r => r.success);
    
    // Processing time statistics
    const processingTimes = successfulResults
        .map(r => r.processingTime ? parseFloat(r.processingTime) : 0)
        .filter(t => t > 0);
    
    const renderingTimes = successfulResults
        .map(r => r.renderingTime ? parseFloat(r.renderingTime) : 0)
        .filter(t => t > 0);
    
    // Calculate total pages processed
    let totalPages = 0;
    successfulResults.forEach(result => {
        if (result.splitPages && result.splitPages.length > 0) {
            // For multi-page documents, count the split pages
            totalPages += result.splitPages.length;
        } else if (result.processedImage && result.processedImage.pageCount) {
            // For single-page documents, use the page count from the processed image
            totalPages += result.processedImage.pageCount;
        }
    });
    
    // OCR statistics
    const ocrStats = {
        totalWithOcr: 0,
        totalCharacters: 0,
        avgCharacters: 0,
        successRate: 0
    };
    
    successfulResults.forEach(result => {
        if (result.extractedText && result.extractedText.trim()) {
            ocrStats.totalWithOcr++;
            ocrStats.totalCharacters += result.extractedText.length;
        }
    });
    
    if (ocrStats.totalWithOcr > 0) {
        ocrStats.avgCharacters = ocrStats.totalCharacters / ocrStats.totalWithOcr;
        ocrStats.successRate = (ocrStats.totalWithOcr / successful) * 100;
    }
    
    // AI Analysis statistics
    const aiStats = {
        totalWithAi: 0,
        totalSuccessful: 0,
        totalFailed: 0,
        avgConfidence: 0,
        documentTypes: {},
        successRate: 0,
        avgConfidenceOfSuccessful: 0
    };
    
    successfulResults.forEach(result => {
        if (result.aiAnalysis) {
            aiStats.totalWithAi++;
            if (result.aiAnalysis.success) {
                aiStats.totalSuccessful++;
                if (result.aiAnalysis.confidence) {
                    aiStats.avgConfidence += result.aiAnalysis.confidence;
                }
                if (result.aiAnalysis.documentType) {
                    const docType = result.aiAnalysis.documentType;
                    aiStats.documentTypes[docType] = (aiStats.documentTypes[docType] || 0) + 1;
                }
            } else {
                aiStats.totalFailed++;
            }
        }
    });
    
    if (aiStats.totalWithAi > 0) {
        aiStats.successRate = (aiStats.totalSuccessful / aiStats.totalWithAi) * 100;
    }
    
    if (aiStats.totalSuccessful > 0) {
        aiStats.avgConfidenceOfSuccessful = (aiStats.avgConfidence / aiStats.totalSuccessful) * 100;
    }
    
    // Aggregate step metrics
    const stepMetrics = collectStepMetrics(successfulResults);
    
    return {
        files: { total, successful, failed, totalPages },
        processing: {
            times: processingTimes,
            totalTime: processingTimes.reduce((sum, time) => sum + time, 0),
            avgTime: processingTimes.length > 0 ? processingTimes.reduce((sum, time) => sum + time, 0) / processingTimes.length : 0,
            minTime: processingTimes.length > 0 ? Math.min(...processingTimes) : 0,
            maxTime: processingTimes.length > 0 ? Math.max(...processingTimes) : 0,
            medianTime: processingTimes.length > 0 ? getMedian(processingTimes) : 0
        },
        rendering: {
            times: renderingTimes,
            totalTime: renderingTimes.reduce((sum, time) => sum + time, 0),
            avgTime: renderingTimes.length > 0 ? renderingTimes.reduce((sum, time) => sum + time, 0) / renderingTimes.length : 0
        },
        batch: {
            totalTime: totalBatchTime ? totalBatchTime / 1000 : 0  // Convert from milliseconds to seconds
        },
        ocr: ocrStats,
        ai: aiStats,
        steps: stepMetrics
    };
}

function createFinalBatchSummary(stats) {
    if (stats.files.total === 0) {
        return '';
    }
    
    // Calculate how many timing cards we'll show
    const hasProcessingTimes = stats.processing.times.length > 0;
    const hasBatchTime = stats.batch.totalTime > 0;
    const timingCardCount = (hasProcessingTimes ? 1 : 0) + (hasBatchTime ? 1 : 0);
    
    // Calculate column classes based on number of cards
    // Base cards: Files, Pages, Successful, Failed = 4 cards
    // Plus timing cards (0-2): Total Processing Time, Total Batch Time
    const totalCards = 4 + timingCardCount;
    const colClass = totalCards === 4 ? 'col-md-3' : 'col-md-2';
    
    return `
        <div class="mt-4 pt-4" style="border-top: 3px solid #dee2e6;">
            <div class="alert alert-primary">
                <h5><i class="bi bi-clipboard-data"></i> Final Batch Summary Report</h5>
                
                <!-- Files Overview -->
                <div class="row mt-3">
                    <div class="${colClass} col-sm-6">
                        <div class="card border-success mb-3">
                            <div class="card-body text-center">
                                <h6 class="card-title text-success"><i class="bi bi-files"></i> Files Processed</h6>
                                <h4 class="text-success">${stats.files.total}</h4>
                                <small class="text-muted">Total Files</small>
                            </div>
                        </div>
                    </div>
                    <div class="${colClass} col-sm-6">
                        <div class="card border-info mb-3">
                            <div class="card-body text-center">
                                <h6 class="card-title text-info"><i class="bi bi-file-earmark-text"></i> Pages Processed</h6>
                                <h4 class="text-info">${stats.files.totalPages}</h4>
                                <small class="text-muted">Total Pages</small>
                            </div>
                        </div>
                    </div>
                    ${hasProcessingTimes ? `
                    <div class="col-md-2 col-sm-6">
                        <div class="card border-primary mb-3">
                            <div class="card-body text-center">
                                <h6 class="card-title text-primary"><i class="bi bi-stopwatch"></i> Total Processing Time</h6>
                                <h4 class="text-primary">${stats.processing.totalTime.toFixed(2)}s</h4>
                                <small class="text-muted">All Files Combined</small>
                            </div>
                        </div>
                    </div>
                    ` : ''}
                    ${hasBatchTime ? `
                    <div class="col-md-2 col-sm-6">
                        <div class="card border-warning mb-3">
                            <div class="card-body text-center">
                                <h6 class="card-title text-warning"><i class="bi bi-clock"></i> Total Batch Time</h6>
                                <h4 class="text-warning">${stats.batch.totalTime.toFixed(2)}s</h4>
                                <small class="text-muted">End-to-End</small>
                            </div>
                        </div>
                    </div>
                    ` : ''}
                    <div class="${colClass} col-sm-6">
                        <div class="card border-success mb-3">
                            <div class="card-body text-center">
                                <h6 class="card-title text-success"><i class="bi bi-check-circle"></i> Successful</h6>
                                <h4 class="text-success">${stats.files.successful}</h4>
                                <small class="text-muted">${((stats.files.successful / stats.files.total) * 100).toFixed(1)}% Success Rate</small>
                            </div>
                        </div>
                    </div>
                    <div class="${colClass} col-sm-6">
                        <div class="card border-danger mb-3">
                            <div class="card-body text-center">
                                <h6 class="card-title text-danger"><i class="bi bi-x-circle"></i> Failed</h6>
                                <h4 class="text-danger">${stats.files.failed}</h4>
                                <small class="text-muted">${((stats.files.failed / stats.files.total) * 100).toFixed(1)}% Failure Rate</small>
                            </div>
                        </div>
                    </div>
                </div>
                
                <!-- Performance Summary -->
                ${stats.processing.times.length > 0 ? `
                    <div class="metrics-section mt-4">
                        <h6 class="metrics-subtitle"><i class="bi bi-speedometer2"></i> Overall Performance Summary</h6>
                        <div class="row">
                            <div class="col-md-6">
                                <div class="timing-row">
                                    <span class="timing-label">Total Processing Time:</span>
                                    <span class="timing-value total">${stats.processing.totalTime.toFixed(2)}s</span>
                                </div>
                                <div class="timing-row">
                                    <span class="timing-label">Average Processing Time:</span>
                                    <span class="timing-value">${stats.processing.avgTime.toFixed(2)}s</span>
                                </div>
                                <div class="timing-row">
                                    <span class="timing-label">Median Processing Time:</span>
                                    <span class="timing-value">${stats.processing.medianTime.toFixed(2)}s</span>
                                </div>
                            </div>
                            <div class="col-md-6">
                                <div class="timing-row">
                                    <span class="timing-label">Fastest File:</span>
                                    <span class="timing-value">${stats.processing.minTime.toFixed(2)}s</span>
                                </div>
                                <div class="timing-row">
                                    <span class="timing-label">Slowest File:</span>
                                    <span class="timing-value">${stats.processing.maxTime.toFixed(2)}s</span>
                                </div>
                                ${stats.rendering.times.length > 0 ? `
                                    <div class="timing-row">
                                        <span class="timing-label">Total UI Rendering:</span>
                                        <span class="timing-value">${stats.rendering.totalTime.toFixed(2)}s</span>
                                    </div>
                                ` : ''}
                            </div>
                        </div>
                    </div>
                ` : ''}
                
                <!-- Step-by-Step Summary -->
                ${stats.steps.length > 0 ? `
                    <div class="metrics-section mt-4">
                        <h6 class="metrics-subtitle"><i class="bi bi-list-ol"></i> Processing Steps Summary</h6>
                        <div class="row">
                            ${stats.steps.map(step => `
                                <div class="col-md-6 col-lg-4 mb-2">
                                    <div class="card border-light">
                                        <div class="card-body p-2">
                                            <h6 class="card-title text-primary">${step.name}</h6>
                                            <small class="text-muted">
                                                Files: ${step.count} | 
                                                Total: ${step.total.toFixed(2)}s |
                                                Avg: ${step.avg.toFixed(2)}s
                                            </small>
                                        </div>
                                    </div>
                                </div>
                            `).join('')}
                        </div>
                    </div>
                ` : ''}
                
                <!-- OCR Statistics -->
                ${stats.ocr.totalWithOcr > 0 ? `
                    <div class="metrics-section mt-4">
                        <h6 class="metrics-subtitle"><i class="bi bi-text-paragraph"></i> OCR Text Extraction Summary</h6>
                        <div class="row">
                            <div class="col-md-3">
                                <div class="timing-row">
                                    <span class="timing-label">Files with OCR:</span>
                                    <span class="timing-value">${stats.ocr.totalWithOcr}</span>
                                </div>
                            </div>
                            <div class="col-md-3">
                                <div class="timing-row">
                                    <span class="timing-label">OCR Success Rate:</span>
                                    <span class="timing-value">${stats.ocr.successRate.toFixed(1)}%</span>
                                </div>
                            </div>
                            <div class="col-md-3">
                                <div class="timing-row">
                                    <span class="timing-label">Total Characters:</span>
                                    <span class="timing-value">${stats.ocr.totalCharacters.toLocaleString()}</span>
                                </div>
                            </div>
                            <div class="col-md-3">
                                <div class="timing-row">
                                    <span class="timing-label">Avg Characters/File:</span>
                                    <span class="timing-value">${Math.round(stats.ocr.avgCharacters)}</span>
                                </div>
                            </div>
                        </div>
                    </div>
                ` : ''}
                
                <!-- AI Analysis Statistics -->
                ${stats.ai.totalWithAi > 0 ? `
                    <div class="metrics-section mt-4">
                        <h6 class="metrics-subtitle"><i class="bi bi-robot"></i> Mistral AI Analysis Summary</h6>
                        <div class="row">
                            <div class="col-md-4">
                                <div class="timing-row">
                                    <span class="timing-label">Files with AI Analysis:</span>
                                    <span class="timing-value">${stats.ai.totalWithAi}</span>
                                </div>
                                <div class="timing-row">
                                    <span class="timing-label">AI Success Rate:</span>
                                    <span class="timing-value">${stats.ai.successRate.toFixed(1)}%</span>
                                </div>
                                ${stats.ai.totalSuccessful > 0 ? `
                                    <div class="timing-row">
                                        <span class="timing-label">Avg Confidence:</span>
                                        <span class="timing-value">${stats.ai.avgConfidenceOfSuccessful.toFixed(1)}%</span>
                                    </div>
                                ` : ''}
                            </div>
                            <div class="col-md-4">
                                <div class="timing-row">
                                    <span class="timing-label">Successful Analysis:</span>
                                    <span class="timing-value text-success">${stats.ai.totalSuccessful}</span>
                                </div>
                                <div class="timing-row">
                                    <span class="timing-label">Failed Analysis:</span>
                                    <span class="timing-value text-danger">${stats.ai.totalFailed}</span>
                                </div>
                            </div>
                            <div class="col-md-4">
                                ${Object.keys(stats.ai.documentTypes).length > 0 ? `
                                    <small><strong>Document Types Found:</strong></small>
                                    ${Object.entries(stats.ai.documentTypes).map(([type, count]) => `
                                        <div class="timing-row">
                                            <span class="timing-label">${type}:</span>
                                            <span class="timing-value">${count}</span>
                                        </div>
                                    `).join('')}
                                ` : ''}
                            </div>
                        </div>
                    </div>
                ` : ''}
            </div>
        </div>
    `;
}

// Image viewer functions
function clearImageViewer(viewer) {
    viewer.innerHTML = `
        <div class="text-center text-muted">
            <i class="bi bi-image" style="font-size: 3rem;"></i>
            <p class="mt-2">Images will appear here</p>
        </div>
    `;
    viewer.classList.remove('has-images');
}

function displayImagesInViewer(viewer, results, renderingTime) {
    const images = [];
    
    results.forEach((result, index) => {
        if (result.success && result.processedImage) {
            const img = result.processedImage;
            
            // Add main image (PNG instead of thumbnail)
            if (img.convertedPngPath || img.thumbnailPath) {
                images.push({
                    name: img.fileName,
                    path: img.convertedPngPath || img.thumbnailPath, // Use PNG file directly
                    processingTime: result.processingTime || '0ms',
                    renderingTime: result.renderingTime || '0ms',
                    isMain: true
                });
            }
            
            // Add split page images for multipage documents
            if (img.isMultiPage && result.splitPages) {
                result.splitPages.forEach((page, pageIndex) => {
                    if (page.convertedPngPath || page.thumbnailPath) {
                        images.push({
                            name: `${img.fileName} - Page ${pageIndex + 1}`,
                            path: page.convertedPngPath || page.thumbnailPath, // Use PNG file directly
                            processingTime: result.processingTime || '0ms',
                            renderingTime: result.renderingTime || '0ms',
                            isPage: true
                        });
                    }
                });
            }
        }
    });
    
    if (images.length === 0) {
        clearImageViewer(viewer);
        return;
    }
    
    viewer.classList.add('has-images');
    
    let html = `
        <div class="mb-2">
            <small class="text-muted">
                <i class="bi bi-images"></i> ${images.length} image${images.length > 1 ? 's' : ''} processed
            </small>
        </div>
        <div class="image-grid">
    `;
    
    images.forEach((img, index) => {
        const httpUrl = convertToHttpUrl(img.path);
        html += `
            <div class="image-item" onclick="openImageModal('${httpUrl}', '${img.name}')">
                ${httpUrl ? `
                    <img src="${httpUrl}" alt="${img.name}" 
                         onerror="this.parentElement.innerHTML='<div class=\\'image-placeholder\\'><i class=\\'bi bi-image\\'></i></div>'">
                ` : `
                    <div class="image-placeholder">
                        <i class="bi bi-image"></i>
                    </div>
                `}
                <div class="image-overlay">
                    <div class="image-name">${img.name}</div>
                    <div class="image-timing">
                        P: ${formatDuration(img.processingTime)} | R: ${formatDuration(img.renderingTime)}
                    </div>
                </div>
            </div>
        `;
    });
    
    html += '</div>';
    viewer.innerHTML = html;
}

// Image modal functions
function openImageModal(imagePath, imageName) {
    const modal = document.getElementById('imageModal');
    const modalImage = document.getElementById('modalImage');
    
    if (modal && modalImage) {
        modalImage.src = imagePath;
        modalImage.alt = imageName;
        modal.style.display = 'block';
        resetModalZoom();
        
        // Prevent body scrolling when modal is open
        document.body.style.overflow = 'hidden';
    }
}

function closeImageModal() {
    const modal = document.getElementById('imageModal');
    if (modal) {
        modal.style.display = 'none';
        // Restore body scrolling
        document.body.style.overflow = 'auto';
    }
}

function resetModalZoom() {
    const modalImage = document.getElementById('modalImage');
    const modalSlider = document.getElementById('modalZoomSlider');
    
    if (modalImage && modalSlider) {
        modalImage.style.transform = 'scale(1)';
        modalSlider.value = 1;
    }
}

// Add keyboard support for modal
document.addEventListener('keydown', function(event) {
    if (event.key === 'Escape') {
        closeImageModal();
    }
});

function displayGalleryResult(container, result) {
    container.innerHTML = `
        <div class="alert alert-success fade-in">
            <h6><i class="bi bi-grid-3x3"></i> Gallery Generated Successfully!</h6>
            <p>Created gallery with ${result.totalImages} images.</p>
            <div class="mt-3">
                <div class="row">
                    <div class="col-sm-6">
                        <small><strong>Gallery HTML:</strong></small><br>
                        <code>${result.galleryPath}</code>
                    </div>
                    <div class="col-sm-6">
                        <small><strong>Output Directory:</strong></small><br>
                        <code>${result.outputDirectory}</code>
                    </div>
                </div>
            </div>
            <div class="mt-3">
                <a href="${convertToHttpUrl(result.galleryPath) || result.galleryPath}" target="_blank" class="btn btn-sm btn-outline-primary">
                    <i class="bi bi-box-arrow-up-right"></i> Open Gallery
                </a>
            </div>
        </div>
    `;
}

// Utility functions
function showProgress(progressDiv, button, statusText = 'Processing...') {
    progressDiv.style.display = 'block';
    button.disabled = true;
    button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';
    
    // Update progress bar and status with indeterminate animation
    const progressBar = progressDiv.querySelector('.progress-bar');
    const statusDiv = progressDiv.querySelector('.progress-status');
    
    if (progressBar) {
        // Add indeterminate animation class
        progressBar.classList.add('progress-indeterminate');
        progressBar.style.width = '100%';
    }
    
    if (statusDiv) {
        statusDiv.textContent = statusText;
        statusDiv.className = 'progress-status processing';
    }
}

function hideProgress(progressDiv, button, showComplete = true) {
    const progressBar = progressDiv.querySelector('.progress-bar');
    const statusDiv = progressDiv.querySelector('.progress-status');
    
    if (showComplete && progressBar && statusDiv) {
        // Remove indeterminate animation and show completion
        progressBar.classList.remove('progress-indeterminate');
        progressBar.style.width = '100%';
        statusDiv.textContent = 'Done!';
        statusDiv.className = 'progress-status complete';
        
        // Fade out after 1.5 seconds
        setTimeout(() => {
            if (statusDiv) {
                statusDiv.classList.add('fade-out');
            }
            setTimeout(() => {
                progressDiv.style.display = 'none';
                // Reset for next time
                if (progressBar) {
                    progressBar.style.width = '0%';
                    progressBar.classList.remove('progress-indeterminate');
                }
                if (statusDiv) {
                    statusDiv.classList.remove('fade-out', 'complete', 'processing');
                    statusDiv.textContent = 'Processing...';
                }
            }, 300); // Wait for fade-out animation
        }, 1500);
    } else {
        progressDiv.style.display = 'none';
        // Reset progress bar
        if (progressBar) {
            progressBar.classList.remove('progress-indeterminate');
            progressBar.style.width = '0%';
        }
    }
    
    button.disabled = false;
    // Reset button text based on button id
    if (button.id === 'uploadBtn') {
        button.innerHTML = '<i class="bi bi-upload"></i> Upload and Process';
    } else if (button.id === 'batchBtn') {
        button.innerHTML = '<i class="bi bi-collection"></i> Process All Files';
    } else if (button.id === 'galleryBtn') {
        button.innerHTML = '<i class="bi bi-grid-3x3"></i> Generate Gallery';
    }
}

function showError(container, message) {
    container.innerHTML = `
        <div class="alert alert-danger fade-in">
            <i class="bi bi-exclamation-triangle"></i> <strong>Error:</strong> ${message}
        </div>
    `;
}

function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

function formatDuration(duration) {
    // Handle different input types
    if (typeof duration === 'number') {
        // Assume milliseconds if number
        return `${(duration / 1000).toFixed(2)}s`;
    }
    
    if (typeof duration === 'string') {
        // Handle C# TimeSpan format like "00:00:01.234567"
        if (duration.includes(':')) {
            const parts = duration.split(':');
            if (parts.length === 3) {
                const seconds = parseFloat(parts[2]);
                return `${seconds.toFixed(2)}s`;
            }
        }
        
        // Handle already formatted strings
        if (duration.endsWith('s') || duration.endsWith('ms')) {
            return duration;
        }
        
        // Try to parse as number
        const num = parseFloat(duration);
        if (!isNaN(num)) {
            return `${num.toFixed(2)}s`;
        }
    }
    
    // Default fallback
    return duration || '0.00s';
}

// Real-time progress update functions
function updateUploadProgress(progress) {
    const progressDiv = document.getElementById('uploadProgress');
    const progressBar = progressDiv?.querySelector('.progress-bar');
    const statusDiv = document.getElementById('uploadProgressStatus');
    
    if (!progressDiv || !progressBar || !statusDiv) return;
    
    // Show progress div if hidden
    progressDiv.style.display = 'block';
    
    // Update progress bar
    if (progress.progressPercentage > 0) {
        progressBar.style.width = `${progress.progressPercentage}%`;
        progressBar.classList.remove('progress-bar-animated');
    } else {
        progressBar.classList.add('progress-bar-animated');
    }
    
    // Update status text based on progress status
    let statusText = getProgressStatusText(progress);
    statusDiv.textContent = statusText;
    
    // Update progress bar color based on status
    updateProgressBarColor(progressBar, progress.status);
}

function updateBatchProgress(progress) {
    const progressDiv = document.getElementById('batchProgress');
    const progressBar = progressDiv?.querySelector('.progress-bar');
    const statusDiv = document.getElementById('batchProgressStatus');
    
    if (!progressDiv || !progressBar || !statusDiv) return;
    
    // Show progress div if hidden
    progressDiv.style.display = 'block';
    
    // Update progress bar
    if (progress.progressPercentage > 0) {
        progressBar.style.width = `${progress.progressPercentage}%`;
        progressBar.classList.remove('progress-bar-animated');
    } else {
        progressBar.classList.add('progress-bar-animated');
    }
    
    // Update status text based on progress status
    let statusText = getProgressStatusText(progress);
    statusDiv.textContent = statusText;
    
    // Update progress bar color based on status
    updateProgressBarColor(progressBar, progress.status);
}

function getProgressStatusText(progress) {
    const fileName = progress.fileName;
    const currentFile = progress.currentFile;
    const totalFiles = progress.totalFiles;
    const currentPage = progress.currentPage;
    const totalPages = progress.totalPages;
    
    // For batch operations, show file progress
    if (totalFiles > 1) {
        if (totalPages > 1) {
            return `Processing ${fileName} (${currentFile}/${totalFiles}) - Page ${currentPage}/${totalPages}`;
        } else {
            return `Processing ${fileName} (${currentFile}/${totalFiles})`;
        }
    }
    
    // For single file operations
    if (totalPages > 1) {
        return `${progress.statusMessage} - Page ${currentPage}/${totalPages}`;
    }
    
    return progress.statusMessage || 'Processing...';
}

function updateProgressBarColor(progressBar, status) {
    // Remove all status classes
    progressBar.classList.remove('bg-success', 'bg-danger', 'bg-warning');
    
    // Add appropriate class based on status
    switch (status) {
        case 'Completed':
            progressBar.classList.add('bg-success');
            break;
        case 'Failed':
            progressBar.classList.add('bg-danger');
            break;
        default:
            // Keep default primary color for processing states
            break;
    }
}

// Mistral Configuration Functions
async function loadMistralConfig() {
    try {
        const config = await apiCall('mistralconfig');
        populateMistralForm(config);
    } catch (error) {
        console.error('Error loading Mistral config:', error);
        showMistralStatus('Error loading configuration', 'error');
    }
}

function populateMistralForm(config) {
    document.getElementById('mistralApiKey').value = config.apiKey || '';
    document.getElementById('mistralEndpoint').value = config.endpoint || 'https://api.mistral.ai/v1/chat/completions';
    document.getElementById('mistralModel').value = config.model || 'mistral-small';
    document.getElementById('mistralTemperature').value = config.temperature || 0.1;
    document.getElementById('temperatureValue').textContent = config.temperature || 0.1;
}

async function saveMistralConfig(event) {
    event.preventDefault();
    
    const form = document.getElementById('mistralConfigForm');
    const formData = new FormData(form);
    
    const config = {
        apiKey: formData.get('apiKey'),
        endpoint: formData.get('endpoint'),
        model: formData.get('model'),
        temperature: parseFloat(formData.get('temperature'))
    };
    
    try {
        await apiCall('mistralconfig', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(config)
        });
        
        showMistralStatus('Configuration saved successfully!', 'success');
    } catch (error) {
        console.error('Error saving Mistral config:', error);
        showMistralStatus('Error saving configuration', 'error');
    }
}

async function testMistralConfig() {
    const form = document.getElementById('mistralConfigForm');
    const formData = new FormData(form);
    
    const config = {
        apiKey: formData.get('apiKey'),
        endpoint: formData.get('endpoint'),
        model: formData.get('model'),
        temperature: parseFloat(formData.get('temperature'))
    };
    
    if (!config.apiKey) {
        showMistralStatus('API Key is required for testing', 'error');
        return;
    }
    
    const testBtn = document.getElementById('testConfigBtn');
    const originalText = testBtn.innerHTML;
    testBtn.innerHTML = '<i class="bi bi-arrow-clockwise spin"></i> Testing...';
    testBtn.disabled = true;
    
    try {
        const result = await apiCall('mistralconfig/test', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify(config)
        });
        
        if (result.success) {
            showMistralStatus('Configuration test successful!', 'success');
        } else {
            showMistralStatus(`Configuration test failed: ${result.message || result.details || 'Unknown error'}`, 'error');
        }
    } catch (error) {
        console.error('Error testing Mistral config:', error);
        showMistralStatus('Error testing configuration', 'error');
    } finally {
        testBtn.innerHTML = originalText;
        testBtn.disabled = false;
    }
}

function showMistralStatus(message, type) {
    const statusDiv = document.getElementById('mistralStatus');
    const alertClass = type === 'success' ? 'alert-success' : 'alert-danger';
    const icon = type === 'success' ? 'bi-check-circle' : 'bi-exclamation-triangle';
    
    statusDiv.innerHTML = `
        <div class="alert ${alertClass} alert-dismissible fade show" role="alert">
            <i class="bi ${icon}"></i> ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        </div>
    `;
    statusDiv.style.display = 'block';
    
    // Auto-hide success messages after 3 seconds
    if (type === 'success') {
        setTimeout(() => {
            const alert = statusDiv.querySelector('.alert');
            if (alert) {
                alert.classList.remove('show');
                setTimeout(() => {
                    statusDiv.style.display = 'none';
                }, 150);
            }
        }, 3000);
    }
}

function updateTemperatureDisplay() {
    const temperatureSlider = document.getElementById('mistralTemperature');
    const temperatureValue = document.getElementById('temperatureValue');
    temperatureValue.textContent = temperatureSlider.value;
}

// Handle OCR enable/disable toggle for single upload
function handleOcrToggle() {
    const enableOcr = document.getElementById('enableOcr').checked;
    const ocrOptions = document.getElementById('ocrOptions');
    const useMistralOcr = document.getElementById('useMistralOcr');
    
    if (enableOcr) {
        ocrOptions.style.display = 'block';
    } else {
        ocrOptions.style.display = 'none';
        useMistralOcr.checked = false;
    }
}

// Handle OCR enable/disable toggle for batch processing
function handleBatchOcrToggle() {
    const enableOcr = document.getElementById('batchEnableOcr').checked;
    const ocrOptions = document.getElementById('batchOcrOptions');
    const useMistralOcr = document.getElementById('batchUseMistralOcr');
    
    if (enableOcr) {
        ocrOptions.style.display = 'block';
    } else {
        ocrOptions.style.display = 'none';
        useMistralOcr.checked = false;
    }
}

// Scanner functionality
let availableScanners = [];
let scannerCapabilities = null;

async function initializeScannerInterface() {
    try {
        // Load available scanners
        await loadAvailableScanners();
        
        // Load scanner capabilities
        await loadScannerCapabilities();
        
        // Update remote scanning guidance
        updateRemoteScanningGuidance();
        
        // Bind scanner event handlers
        bindScannerEventHandlers();
        
        console.log('Scanner interface initialized');
    } catch (error) {
        console.error('Error initializing scanner interface:', error);
        showScannerStatus('Error initializing scanner interface', 'danger');
    }
}

async function loadAvailableScanners() {
    try {
        const response = await apiCall('ImageProcessing/scanners');
        availableScanners = response || [];
        
        const scannerSelect = document.getElementById('scannerSelect');
        scannerSelect.innerHTML = '';
        
        if (availableScanners.length === 0) {
            scannerSelect.innerHTML = '<option value="">No scanners found</option>';
            showScannerStatus('No scanners found. Scanners must be connected to the server machine running AgentDMS.', 'warning');
            
            // Load connectivity info to provide better guidance
            loadScannerConnectivityInfo();
        } else {
            availableScanners.forEach((scanner, index) => {
                const option = document.createElement('option');
                option.value = scanner.deviceId;
                option.textContent = `${scanner.name} (${scanner.manufacturer})`;
                if (scanner.isDefault) {
                    option.selected = true;
                }
                scannerSelect.appendChild(option);
            });
            
            // Update scanner info for the selected scanner
            updateScannerInfo();
            
            // Check if all scanners are mock scanners for remote access scenario
            const allMockScanners = availableScanners.every(s => s.deviceId.startsWith('mock_'));
            if (allMockScanners) {
                loadScannerConnectivityInfo();
            }
        }
    } catch (error) {
        console.error('Error loading scanners:', error);
        const scannerSelect = document.getElementById('scannerSelect');
        scannerSelect.innerHTML = '<option value="">Error loading scanners</option>';
        showScannerStatus('Error loading scanners: ' + error.message, 'danger');
    }
}

async function loadScannerCapabilities() {
    try {
        const response = await apiCall('ImageProcessing/scanners/capabilities');
        scannerCapabilities = response;
        updatePlatformCapabilities();
    } catch (error) {
        console.error('Error loading scanner capabilities:', error);
        showScannerStatus('Error loading scanner capabilities: ' + error.message, 'warning');
    }
}

async function loadScannerConnectivityInfo() {
    try {
        const response = await apiCall('ImageProcessing/scanners/connectivity-info');
        if (response) {
            displayScannerConnectivityInfo(response);
        }
    } catch (error) {
        console.error('Error loading scanner connectivity info:', error);
    }
}

function displayScannerConnectivityInfo(connectivityInfo) {
    let message = '';
    let alertType = 'info';
    
    if (connectivityInfo.isRemoteAccess && !connectivityInfo.hasRealScanners) {
        message = '<strong>Remote Access Detected:</strong><br/>';
        message += 'You are accessing AgentDMS from a remote machine. ';
        message += 'Scanners must be connected to the server machine, not your local computer.<br/><br/>';
        message += '<strong>To use real scanners:</strong><br/>';
        message += '• Connect scanners to the computer running AgentDMS<br/>';
        message += '• Install scanner drivers on the server machine<br/>';
        message += '• Or install AgentDMS on the machine where your scanners are connected';
        alertType = 'warning';
    } else if (!connectivityInfo.hasRealScanners) {
        message = '<strong>Scanner Information:</strong><br/>';
        message += 'Only mock/test scanners are currently available. ';
        message += 'For real scanning, connect physical scanners to this machine and install the appropriate drivers.';
        alertType = 'info';
    }
    
    if (message) {
        showScannerStatus(message, alertType, false); // Don't auto-hide this important message
    }
}

function bindScannerEventHandlers() {
    // Scanner selection change
    const scannerSelect = document.getElementById('scannerSelect');
    scannerSelect?.addEventListener('change', updateScannerInfo);
    
    // Start scan button
    const startScanBtn = document.getElementById('startScanBtn');
    startScanBtn?.addEventListener('click', startScan);
    
    // Preview scan button
    const previewScanBtn = document.getElementById('previewScanBtn');
    previewScanBtn?.addEventListener('click', previewScan);
    
    // Refresh scanners button
    const refreshScannersBtn = document.getElementById('refreshScannersBtn');
    refreshScannersBtn?.addEventListener('click', refreshScanners);
    
    // Modal start scan button
    const modalStartScanBtn = document.getElementById('modalStartScanBtn');
    modalStartScanBtn?.addEventListener('click', handleModalScan);
}

function updateScannerInfo() {
    const scannerSelect = document.getElementById('scannerSelect');
    const selectedDeviceId = scannerSelect.value;
    const selectedScanner = availableScanners.find(s => s.deviceId === selectedDeviceId);
    
    const scannerInfoDiv = document.getElementById('scannerInfo');
    
    if (selectedScanner) {
        scannerInfoDiv.innerHTML = `
            <h6>${selectedScanner.name}</h6>
            <p><strong>Manufacturer:</strong> ${selectedScanner.manufacturer}</p>
            <p><strong>Model:</strong> ${selectedScanner.model}</p>
            <p><strong>Status:</strong> <span class="badge bg-${selectedScanner.isAvailable ? 'success' : 'danger'}">${selectedScanner.isAvailable ? 'Available' : 'Unavailable'}</span></p>
            ${selectedScanner.isDefault ? '<p><span class="badge bg-primary">Default Scanner</span></p>' : ''}
            ${Object.keys(selectedScanner.capabilities || {}).length > 0 ? 
                '<div class="mt-2"><small class="text-muted">Capabilities: ' + 
                JSON.stringify(selectedScanner.capabilities, null, 2).slice(1, -1) + 
                '</small></div>' : ''}
        `;
    } else {
        scannerInfoDiv.innerHTML = '<p class="text-muted">No scanner selected</p>';
    }
}

function updatePlatformCapabilities() {
    const capabilitiesDiv = document.getElementById('platformCapabilities');
    
    if (scannerCapabilities) {
        const supports = [];
        if (scannerCapabilities.supportsTwain) supports.push('TWAIN');
        if (scannerCapabilities.supportsWia) supports.push('WIA');
        if (scannerCapabilities.supportsSane) supports.push('SANE');
        
        capabilitiesDiv.innerHTML = `
            <div class="mb-2">
                <strong>Supported APIs:</strong><br>
                ${supports.length > 0 ? supports.join(', ') : 'Mock Scanner Only'}
            </div>
            <div class="mb-2">
                <strong>Color Modes:</strong><br>
                ${scannerCapabilities.supportedColorModes.map(mode => 
                    mode === 0 ? 'B&W' : mode === 1 ? 'Grayscale' : 'Color'
                ).join(', ')}
            </div>
            <div class="mb-2">
                <strong>Formats:</strong><br>
                ${scannerCapabilities.supportedFormats.map(format => 
                    format === 0 ? 'PNG' : format === 1 ? 'JPEG' : 'TIFF'
                ).join(', ')}
            </div>
            <div class="mb-2">
                <strong>Resolution:</strong><br>
                ${scannerCapabilities.resolutionRange[0]} - ${scannerCapabilities.resolutionRange[1]} DPI
            </div>
            <div>
                <small class="text-muted">${scannerCapabilities.platformInfo}</small>
            </div>
        `;
    } else {
        capabilitiesDiv.innerHTML = '<p class="text-muted">Capabilities not available</p>';
    }
}

async function refreshScanners() {
    const refreshBtn = document.getElementById('refreshScannersBtn');
    const originalText = refreshBtn.innerHTML;
    
    try {
        refreshBtn.innerHTML = '<i class="bi bi-arrow-clockwise spin"></i> Refreshing...';
        refreshBtn.disabled = true;
        
        await loadAvailableScanners();
        showScannerStatus('Scanners refreshed successfully', 'success');
        
        setTimeout(() => {
            hideScannerStatus();
        }, 3000);
    } catch (error) {
        console.error('Error refreshing scanners:', error);
        showScannerStatus('Error refreshing scanners: ' + error.message, 'danger');
    } finally {
        refreshBtn.innerHTML = originalText;
        refreshBtn.disabled = false;
    }
}

async function startScan() {
    // Always perform scan directly - let the backend handle native scanner interface when ShowUserInterface is true
    await performScan(false);
}

async function previewScan() {
    // Preview scans always show the scanner interface (modal)
    showScannerConfigModal(true);
}

async function performScan(isPreview = false) {
    const scannerSelect = document.getElementById('scannerSelect');
    const scanResolution = document.getElementById('scanResolution');
    const scanColorMode = document.getElementById('scanColorMode');
    const scanFormat = document.getElementById('scanFormat');
    const showScannerUI = document.getElementById('showScannerUI');
    const autoProcess = document.getElementById('autoProcess');
    
    const scanRequest = {
        scannerDeviceId: scannerSelect.value,
        resolution: parseInt(scanResolution.value),
        colorMode: parseInt(scanColorMode.value),
        format: parseInt(scanFormat.value),
        showUserInterface: isPreview || showScannerUI.checked,
        autoProcess: !isPreview && autoProcess.checked
    };
    
    const scanBtn = isPreview ? document.getElementById('previewScanBtn') : document.getElementById('startScanBtn');
    const originalText = scanBtn.innerHTML;
    
    try {
        // Disable buttons and show progress
        scanBtn.innerHTML = `<i class="bi bi-hourglass-split"></i> ${isPreview ? 'Previewing...' : 'Scanning...'}`;
        scanBtn.disabled = true;
        showScanProgress();
        showScannerStatus(`${isPreview ? 'Preview scan' : 'Scan'} in progress...`, 'info');
        
        // Clear previous results
        document.getElementById('scanResult').innerHTML = '';
        
        // Start scan
        const endpoint = isPreview ? 'ImageProcessing/scan/preview' : 'ImageProcessing/scan';
        const result = await apiCall(endpoint, {
            method: 'POST',
            body: JSON.stringify(scanRequest)
        });
        
        hideScanProgress();
        
        if (result.success) {
            showScanResult(result, isPreview);
            showScannerStatus(`${isPreview ? 'Preview scan' : 'Scan'} completed successfully!`, 'success');
            
            // Auto-hide success message
            setTimeout(() => {
                hideScannerStatus();
            }, 5000);
        } else {
            showScannerStatus(`${isPreview ? 'Preview scan' : 'Scan'} failed: ${result.errorMessage}`, 'danger');
        }
    } catch (error) {
        hideScanProgress();
        console.error('Scan error:', error);
        showScannerStatus(`${isPreview ? 'Preview scan' : 'Scan'} error: ${error.message}`, 'danger');
    } finally {
        scanBtn.innerHTML = originalText;
        scanBtn.disabled = false;
    }
}

function showScanResult(result, isPreview) {
    const resultDiv = document.getElementById('scanResult');
    const imageUrl = convertToHttpUrl(result.scannedFilePath);
    
    resultDiv.innerHTML = `
        <div class="alert alert-success">
            <h6><i class="bi bi-check-circle"></i> ${isPreview ? 'Preview Scan' : 'Scan'} Completed</h6>
            <p><strong>File:</strong> ${result.fileName}</p>
            <p><strong>Scanner:</strong> ${result.scannerUsed}</p>
            <p><strong>Time:</strong> ${new Date(result.scanTime).toLocaleString()}</p>
            ${result.processingJobId ? `<p><strong>Processing Job ID:</strong> ${result.processingJobId}</p>` : ''}
        </div>
        
        <div class="card">
            <div class="card-header d-flex justify-content-between align-items-center">
                <h6 class="mb-0">Scanned Image</h6>
                <button class="btn btn-sm btn-outline-primary" onclick="openImageModal('${imageUrl}', '${result.fileName}')">
                    <i class="bi bi-zoom-in"></i> View Full Size
                </button>
            </div>
            <div class="card-body text-center">
                <img src="${imageUrl}" alt="${result.fileName}" class="img-fluid rounded" style="max-height: 400px;">
            </div>
        </div>
    `;
}

function showScannerStatus(message, type = 'info', autoHide = true) {
    const statusDiv = document.getElementById('scannerStatus');
    const statusText = document.getElementById('scannerStatusText');
    
    statusDiv.className = `alert alert-${type}`;
    
    // Support HTML content for rich messaging
    if (message.includes('<')) {
        statusText.innerHTML = message;
    } else {
        statusText.textContent = message;
    }
    
    statusDiv.style.display = 'block';
    
    // Auto-hide after 10 seconds if autoHide is true (except for important messages)
    if (autoHide && !message.includes('Remote Access Detected')) {
        setTimeout(() => {
            hideScannerStatus();
        }, 10000);
    }
}

function hideScannerStatus() {
    const statusDiv = document.getElementById('scannerStatus');
    statusDiv.style.display = 'none';
}

function showScanProgress() {
    const progressDiv = document.getElementById('scanProgress');
    const progressBar = progressDiv.querySelector('.progress-bar');
    
    progressBar.style.width = '50%';
    progressDiv.style.display = 'block';
}

function hideScanProgress() {
    const progressDiv = document.getElementById('scanProgress');
    progressDiv.style.display = 'none';
}

// Scanner configuration modal functions
function showScannerConfigModal(isPreview = false) {
    // Sync current settings with modal
    syncSettingsToModal();
    
    // Store the scan type for later use
    const modal = document.getElementById('scannerConfigModal');
    modal.setAttribute('data-is-preview', isPreview.toString());
    
    // Update modal title and button text based on scan type
    const modalTitle = document.getElementById('scannerConfigModalLabel');
    const modalButton = document.getElementById('modalStartScanBtn');
    
    if (isPreview) {
        modalTitle.innerHTML = '<i class="bi bi-eye"></i> Preview Scan Configuration';
        modalButton.innerHTML = '<i class="bi bi-eye"></i> Start Preview';
    } else {
        modalTitle.innerHTML = '<i class="bi bi-gear"></i> Scanner Configuration';
        modalButton.innerHTML = '<i class="bi bi-play-fill"></i> Start Scan';
    }
    
    // Show the modal using Bootstrap
    const modalInstance = new bootstrap.Modal(modal);
    modalInstance.show();
    
    // Ensure aria-hidden is removed when modal is shown for accessibility
    modal.removeAttribute('aria-hidden');
}

function syncSettingsToModal() {
    // Copy current settings from main form to modal
    const scanResolution = document.getElementById('scanResolution');
    const scanColorMode = document.getElementById('scanColorMode');
    const scanFormat = document.getElementById('scanFormat');
    const autoProcess = document.getElementById('autoProcess');
    
    const modalScanResolution = document.getElementById('modalScanResolution');
    const modalScanColorMode = document.getElementById('modalScanColorMode');
    const modalScanFormat = document.getElementById('modalScanFormat');
    const modalAutoProcess = document.getElementById('modalAutoProcess');
    
    if (scanResolution && modalScanResolution) {
        modalScanResolution.value = scanResolution.value;
    }
    if (scanColorMode && modalScanColorMode) {
        modalScanColorMode.value = scanColorMode.value;
    }
    if (scanFormat && modalScanFormat) {
        modalScanFormat.value = scanFormat.value;
    }
    if (autoProcess && modalAutoProcess) {
        modalAutoProcess.checked = autoProcess.checked;
    }
}

function syncSettingsFromModal() {
    // Copy settings from modal back to main form
    const scanResolution = document.getElementById('scanResolution');
    const scanColorMode = document.getElementById('scanColorMode');
    const scanFormat = document.getElementById('scanFormat');
    const autoProcess = document.getElementById('autoProcess');
    
    const modalScanResolution = document.getElementById('modalScanResolution');
    const modalScanColorMode = document.getElementById('modalScanColorMode');
    const modalScanFormat = document.getElementById('modalScanFormat');
    const modalAutoProcess = document.getElementById('modalAutoProcess');
    
    if (scanResolution && modalScanResolution) {
        scanResolution.value = modalScanResolution.value;
    }
    if (scanColorMode && modalScanColorMode) {
        scanColorMode.value = modalScanColorMode.value;
    }
    if (scanFormat && modalScanFormat) {
        scanFormat.value = modalScanFormat.value;
    }
    if (autoProcess && modalAutoProcess) {
        autoProcess.checked = modalAutoProcess.checked;
    }
}

function handleModalScan() {
    // Sync settings back to main form
    syncSettingsFromModal();
    
    // Get the scan type from the modal
    const modal = document.getElementById('scannerConfigModal');
    const isPreview = modal.getAttribute('data-is-preview') === 'true';
    
    // Close the modal
    const modalInstance = bootstrap.Modal.getInstance(modal);
    if (modalInstance) {
        modalInstance.hide();
        // Restore aria-hidden for accessibility
        modal.setAttribute('aria-hidden', 'true');
    }
    
    // Perform the scan with the configured settings
    performScan(isPreview);
}

// Update remote scanning guidance with dynamic information
function updateRemoteScanningGuidance() {
    try {
        // Get the current server URL
        const serverUrlElement = document.getElementById('serverUrl');
        if (serverUrlElement) {
            const currentUrl = window.location.protocol + '//' + window.location.host;
            const serverIpPlaceholder = window.location.hostname === 'localhost' || window.location.hostname === '127.0.0.1' 
                ? 'http://[SERVER-IP]:5249' 
                : currentUrl;
            serverUrlElement.textContent = serverIpPlaceholder;
            
            // Add click handler to copy URL
            serverUrlElement.style.cursor = 'pointer';
            serverUrlElement.title = 'Click to copy URL';
            serverUrlElement.onclick = function() {
                navigator.clipboard.writeText(serverIpPlaceholder).then(() => {
                    // Show brief feedback
                    const originalText = serverUrlElement.textContent;
                    serverUrlElement.textContent = 'Copied!';
                    setTimeout(() => {
                        serverUrlElement.textContent = originalText;
                    }, 1000);
                }).catch(err => {
                    console.warn('Could not copy to clipboard:', err);
                });
            };
        }
        
        // Update guidance based on detected platform and scanners
        const guidanceElement = document.getElementById('remoteScanningGuide');
        if (guidanceElement && availableScanners && platformCapabilities) {
            // Check if we have real scanners or just mock scanners
            const hasRealScanners = availableScanners.some(scanner => 
                scanner.capabilities && scanner.capabilities.Type !== 'Mock'
            );
            
            // Update status indicators based on current setup
            const setupStatus = guidanceElement.querySelector('.alert-info');
            if (setupStatus) {
                if (hasRealScanners) {
                    setupStatus.className = 'alert alert-success';
                    setupStatus.innerHTML = '<i class="bi bi-check-circle"></i> <strong>Real Scanner Detected:</strong> Your scanner is ready for remote access!';
                } else {
                    setupStatus.className = 'alert alert-info';
                    setupStatus.innerHTML = '<i class="bi bi-info-circle"></i> <strong>Mock Scanner Mode:</strong> Connect a real scanner to the server for full functionality.';
                }
            }
        }
    } catch (error) {
        console.warn('Error updating remote scanning guidance:', error);
    }
}

// OCR Text Display Functions
function toggleOcrText(ocrId) {
    const ocrContent = document.getElementById(ocrId);
    const toggleBtn = document.getElementById('toggle-' + ocrId);
    
    if (ocrContent.style.display === 'none' || ocrContent.style.display === '') {
        ocrContent.style.display = 'block';
        toggleBtn.innerHTML = '<i class="bi bi-eye-slash"></i> Hide Text';
    } else {
        ocrContent.style.display = 'none';
        toggleBtn.innerHTML = '<i class="bi bi-eye"></i> Show Text';
    }
}

function showFullOcrText(ocrId) {
    const ocrContent = document.getElementById(ocrId);
    const ocrTextElement = ocrContent.querySelector('.ocr-text');
    
    if (ocrContent && ocrTextElement) {
        const fullText = decodeURIComponent(ocrContent.dataset.fullText);
        ocrTextElement.textContent = fullText;
        ocrTextElement.style.maxHeight = '400px'; // Increase height for full text
        
        // Hide the "Show Full Text" button
        const expandBtn = ocrContent.querySelector('.btn-link');
        if (expandBtn) {
            expandBtn.style.display = 'none';
        }
    }
}