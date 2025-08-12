// AgentDMS Web UI JavaScript
document.addEventListener('DOMContentLoaded', function() {
    // Initialize the application
    init();
});

function init() {
    // Load supported formats
    loadSupportedFormats();
    
    // Bind event handlers
    bindEventHandlers();
}

function bindEventHandlers() {
    // Upload form
    document.getElementById('uploadForm').addEventListener('submit', handleUpload);
    
    // Batch process form
    document.getElementById('batchForm').addEventListener('submit', handleBatchProcess);
    
    // Gallery form
    document.getElementById('galleryForm').addEventListener('submit', handleGalleryGeneration);
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
    const thumbnailViewer = document.getElementById('thumbnailViewer');
    
    if (!fileInput.files[0]) {
        showError(resultDiv, 'Please select a file to upload.');
        return;
    }
    
    // Clear previous results and thumbnails
    resultDiv.innerHTML = '';
    clearThumbnailViewer(thumbnailViewer);
    
    // Show progress
    showProgress(progressDiv, uploadBtn);
    
    const renderStartTime = performance.now();
    
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
        
        const result = await response.json();
        
        // Calculate rendering time
        const renderEndTime = performance.now();
        const renderingTime = renderEndTime - renderStartTime;
        result.renderingTime = formatDuration(renderingTime);
        
        // Display results and thumbnails separately
        displayProcessingResult(resultDiv, result);
        displayThumbnailsInViewer(thumbnailViewer, [result], renderingTime);
        
    } catch (error) {
        showError(resultDiv, `Upload failed: ${error.message}`);
    } finally {
        hideProgress(progressDiv, uploadBtn);
    }
}

// Handle batch processing
async function handleBatchProcess(event) {
    event.preventDefault();
    
    const pathsTextarea = document.getElementById('filePaths');
    const batchBtn = document.getElementById('batchBtn');
    const progressDiv = document.getElementById('batchProgress');
    const resultDiv = document.getElementById('batchResult');
    const thumbnailViewer = document.getElementById('batchThumbnailViewer');
    
    const filePaths = pathsTextarea.value
        .split('\n')
        .map(path => path.trim())
        .filter(path => path.length > 0);
    
    if (filePaths.length === 0) {
        showError(resultDiv, 'Please enter at least one file path.');
        return;
    }
    
    // Clear previous results and thumbnails
    resultDiv.innerHTML = '';
    clearThumbnailViewer(thumbnailViewer);
    
    showProgress(progressDiv, batchBtn);
    
    const renderStartTime = performance.now();
    
    try {
        const results = await apiCall('imageprocessing/batch-process', {
            method: 'POST',
            body: JSON.stringify({ filePaths })
        });
        
        // Calculate rendering time
        const renderEndTime = performance.now();
        const renderingTime = renderEndTime - renderStartTime;
        
        // Add rendering time to results
        results.forEach(result => {
            result.renderingTime = formatDuration(renderingTime / results.length);
        });
        
        displayBatchResults(resultDiv, results);
        displayThumbnailsInViewer(thumbnailViewer, results, renderingTime);
        
    } catch (error) {
        showError(resultDiv, `Batch processing failed: ${error.message}`);
    } finally {
        hideProgress(progressDiv, batchBtn);
    }
}

// Handle gallery generation
async function handleGalleryGeneration(event) {
    event.preventDefault();
    
    const pathsTextarea = document.getElementById('galleryPaths');
    const titleInput = document.getElementById('galleryTitle');
    const sizeSelect = document.getElementById('thumbnailSize');
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
    
    showProgress(progressDiv, galleryBtn);
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

// Thumbnail viewer functions
function clearThumbnailViewer(viewer) {
    viewer.innerHTML = `
        <div class="text-center text-muted">
            <i class="bi bi-image" style="font-size: 3rem;"></i>
            <p class="mt-2">Thumbnails will appear here</p>
        </div>
    `;
    viewer.classList.remove('has-thumbnails');
}

function displayThumbnailsInViewer(viewer, results, renderingTime) {
    const thumbnails = [];
    
    results.forEach((result, index) => {
        if (result.success && result.processedImage) {
            const img = result.processedImage;
            
            // Add main thumbnail
            if (img.thumbnailPath) {
                thumbnails.push({
                    name: img.fileName,
                    path: img.thumbnailPath,
                    processingTime: result.processingTime || '0ms',
                    renderingTime: result.renderingTime || '0ms',
                    isMain: true
                });
            }
            
            // Add split page thumbnails for multipage documents
            if (img.isMultiPage && result.splitPages) {
                result.splitPages.forEach((page, pageIndex) => {
                    if (page.thumbnailPath) {
                        thumbnails.push({
                            name: `${img.fileName} - Page ${pageIndex + 1}`,
                            path: page.thumbnailPath,
                            processingTime: result.processingTime || '0ms',
                            renderingTime: result.renderingTime || '0ms',
                            isPage: true
                        });
                    }
                });
            }
        }
    });
    
    if (thumbnails.length === 0) {
        clearThumbnailViewer(viewer);
        return;
    }
    
    viewer.classList.add('has-thumbnails');
    
    let html = `
        <div class="mb-2">
            <small class="text-muted">
                <i class="bi bi-images"></i> ${thumbnails.length} thumbnail${thumbnails.length > 1 ? 's' : ''} generated
            </small>
        </div>
        <div class="thumbnail-grid">
    `;
    
    thumbnails.forEach((thumb, index) => {
        const httpUrl = convertToHttpUrl(thumb.path);
        html += `
            <div class="thumbnail-item" onclick="previewThumbnail('${httpUrl}', '${thumb.name}')">
                ${httpUrl ? `
                    <img src="${httpUrl}" alt="${thumb.name}" 
                         onerror="this.parentElement.innerHTML='<div class=\\'thumbnail-placeholder\\'><i class=\\'bi bi-image\\'></i></div>'">
                ` : `
                    <div class="thumbnail-placeholder">
                        <i class="bi bi-image"></i>
                    </div>
                `}
                <div class="thumbnail-overlay">
                    <div class="thumbnail-name">${thumb.name}</div>
                    <div class="thumbnail-timing">
                        P: ${formatDuration(thumb.processingTime)} | R: ${formatDuration(thumb.renderingTime)}
                    </div>
                </div>
            </div>
        `;
    });
    
    html += '</div>';
    viewer.innerHTML = html;
}

function previewThumbnail(imagePath, imageName) {
    // Create a modal or lightbox to preview the full image
    // For now, we'll just show an alert with the image path
    alert(`Preview: ${imageName}\\nPath: ${imagePath}`);
}

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
function showProgress(progressDiv, button) {
    progressDiv.style.display = 'block';
    button.disabled = true;
    button.innerHTML = '<span class="spinner-border spinner-border-sm me-2"></span>Processing...';
}

function hideProgress(progressDiv, button) {
    progressDiv.style.display = 'none';
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