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
            .build();

        progressConnection.on("ProgressUpdate", handleProgressUpdate);

        await progressConnection.start();
        console.log("SignalR connection established for real-time progress updates");
    } catch (error) {
        console.error("Error establishing SignalR connection:", error);
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
    
    // Gallery form
    document.getElementById('galleryForm').addEventListener('submit', handleGalleryGeneration);
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
    if (filePath.startsWith('http://') || filePath.startsWith('https://') || filePath.startsWith('/')) {
        return filePath;
    }
    
    // Convert absolute file path to HTTP URL
    // Extract the AgentDMS_Output part and everything after it
    const outputFolderName = 'AgentDMS_Output';
    const outputIndex = filePath.indexOf(outputFolderName);
    
    if (outputIndex !== -1) {
        // Extract the relative path from AgentDMS_Output onwards
        const relativePath = filePath.substring(outputIndex);
        return '/' + relativePath.replace(/\\/g, '/'); // Normalize path separators
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
        if (progressConnection && jobId) {
            await progressConnection.invoke("JoinJob", jobId);
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
        if (progressConnection && jobId) {
            await progressConnection.invoke("LeaveJob", jobId);
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
    
    try {
        const response = await fetch('/api/imageprocessing/batch-process', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json'
            },
            body: JSON.stringify({ filePaths })
        });
        
        if (!response.ok) {
            const errorData = await response.json();
            throw new Error(errorData.error || 'Batch processing failed');
        }
        
        const responseData = await response.json();
        jobId = responseData.jobId;
        const results = responseData.results;
        
        // Join the SignalR group to receive progress updates for this job
        if (progressConnection && jobId) {
            await progressConnection.invoke("JoinJob", jobId);
        }
        
        // Calculate rendering time
        const renderEndTime = performance.now();
        const renderingTime = renderEndTime - renderStartTime;
        
        // Add rendering time to results
        results.forEach(result => {
            result.renderingTime = formatDuration(renderingTime / results.length);
        });
        
        displayBatchResults(resultDiv, results);
        displayImagesInViewer(imageViewer, results, renderingTime);
        
    } catch (error) {
        showError(resultDiv, `Batch processing failed: ${error.message}`);
    } finally {
        hideProgress(progressDiv, batchBtn);
        
        // Leave the SignalR group
        if (progressConnection && jobId) {
            await progressConnection.invoke("LeaveJob", jobId);
        }
    }
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
            <div class="timing-row">
                <span class="timing-label">Total Processing:</span>
                <span class="timing-value">${formatDuration(processingTime)}</span>
            </div>
            <div class="timing-row">
                <span class="timing-label">UI Rendering:</span>
                <span class="timing-value">${formatDuration(renderingTime)}</span>
            </div>
        </div>
    `;
}

function displayBatchResults(container, results) {
    const successful = results.filter(r => r.success).length;
    const failed = results.length - successful;
    
    let html = `
        <div class="alert alert-info fade-in">
            <h6><i class="bi bi-collection"></i> Batch Processing Complete</h6>
            <p class="mb-1">
                <span class="badge bg-success me-2">${successful} Successful</span>
                <span class="badge bg-danger">${failed} Failed</span>
            </p>
        </div>
    `;
    
    // Show timing summary for successful results
    if (successful > 0) {
        const successfulResults = results.filter(r => r.success);
        const totalProcessingTime = successfulResults.reduce((sum, r) => {
            return sum + (r.processingTime ? parseFloat(r.processingTime) : 0);
        }, 0);
        
        html += `
            <div class="timing-metrics">
                <h6><i class="bi bi-stopwatch"></i> Batch Performance Summary</h6>
                <div class="timing-row">
                    <span class="timing-label">Total Files Processed:</span>
                    <span class="timing-value">${successful}</span>
                </div>
                <div class="timing-row">
                    <span class="timing-label">Average Processing Time:</span>
                    <span class="timing-value">${(totalProcessingTime / successful).toFixed(2)}s</span>
                </div>
                <div class="timing-row">
                    <span class="timing-label">Total Processing Time:</span>
                    <span class="timing-value total">${totalProcessingTime.toFixed(2)}s</span>
                </div>
            </div>
        `;
    }
    
    results.forEach((result, index) => {
        html += `
            <div class="result-item ${result.success ? 'success' : 'error'}">
                <div class="d-flex justify-content-between align-items-start">
                    <div>
                        <div class="file-name">
                            File ${index + 1}: ${result.processedImage?.fileName || 'Unknown'}
                        </div>
                        ${result.success ? `
                            <small class="text-muted">
                                ${result.processedImage?.originalFormat} | 
                                ${formatFileSize(result.processedImage?.fileSize || 0)} |
                                ${result.processedImage?.width || 0}×${result.processedImage?.height || 0}px
                            </small>
                        ` : `
                            <small class="text-danger">${result.message}</small>
                        `}
                    </div>
                    <div class="text-end">
                        ${result.success ? 
                            `<i class="bi bi-check-circle text-success"></i>` : 
                            `<i class="bi bi-x-circle text-danger"></i>`
                        }
                    </div>
                </div>
            </div>
        `;
    });
    
    container.innerHTML = html;
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