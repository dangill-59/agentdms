// DOM elements
const dropZone = document.getElementById('drop-zone');
const fileInput = document.getElementById('file-input');
const fileInputBtn = document.getElementById('file-input-btn');
const processingStatus = document.getElementById('processing-status');
const resultsContainer = document.getElementById('results');

// Initialize the application
document.addEventListener('DOMContentLoaded', () => {
    setupEventListeners();
});

// Setup all event listeners
function setupEventListeners() {
    // Drop zone events
    dropZone.addEventListener('click', () => fileInput.click());
    dropZone.addEventListener('dragover', handleDragOver);
    dropZone.addEventListener('dragenter', handleDragEnter);
    dropZone.addEventListener('dragleave', handleDragLeave);
    dropZone.addEventListener('drop', handleDrop);
    
    // File input events
    fileInput.addEventListener('change', handleFileSelect);
    fileInputBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        fileInput.click();
    });
    
    // Prevent default drag behaviors on document
    ['dragenter', 'dragover', 'dragleave', 'drop'].forEach(eventName => {
        document.addEventListener(eventName, preventDefaults, false);
    });
}

// Prevent default behaviors
function preventDefaults(e) {
    e.preventDefault();
    e.stopPropagation();
}

// Handle drag over
function handleDragOver(e) {
    e.preventDefault();
    dropZone.classList.add('drag-over');
}

// Handle drag enter
function handleDragEnter(e) {
    e.preventDefault();
    dropZone.classList.add('drag-over');
}

// Handle drag leave
function handleDragLeave(e) {
    e.preventDefault();
    if (!dropZone.contains(e.relatedTarget)) {
        dropZone.classList.remove('drag-over');
    }
}

// Handle file drop
function handleDrop(e) {
    e.preventDefault();
    dropZone.classList.remove('drag-over');
    
    const files = Array.from(e.dataTransfer.files);
    processFiles(files);
}

// Handle file selection via input
function handleFileSelect(e) {
    const files = Array.from(e.target.files);
    processFiles(files);
    
    // Clear the input so the same file can be selected again
    e.target.value = '';
}

// Process selected files
async function processFiles(files) {
    // Filter for PNG files only
    const pngFiles = files.filter(file => {
        return file.type === 'image/png' || file.name.toLowerCase().endsWith('.png');
    });
    
    if (pngFiles.length === 0) {
        showNotification('Please select PNG files only.', 'error');
        return;
    }
    
    if (pngFiles.length !== files.length) {
        showNotification(`${files.length - pngFiles.length} non-PNG files were ignored.`, 'warning');
    }
    
    // Show processing status
    showProcessingStatus(true);
    
    try {
        // Process each file
        for (const file of pngFiles) {
            await processFile(file);
        }
    } catch (error) {
        console.error('Error processing files:', error);
        showNotification('An error occurred while processing files.', 'error');
    } finally {
        // Hide processing status
        showProcessingStatus(false);
    }
}

// Process a single file
async function processFile(file) {
    try {
        // Validate file size (50MB limit)
        const maxSize = 50 * 1024 * 1024; // 50MB
        if (file.size > maxSize) {
            addResultCard({
                success: false,
                fileName: file.name,
                message: `File size (${formatFileSize(file.size)}) exceeds maximum allowed size (${formatFileSize(maxSize)})`
            });
            return;
        }
        
        // Prepare form data
        const formData = new FormData();
        formData.append('file', file);
        
        // Upload and process the file
        const response = await fetch('/api/upload', {
            method: 'POST',
            body: formData
        });
        
        const result = await response.json();
        
        if (response.ok && result.success) {
            // Success - add result card with thumbnail
            addResultCard({
                success: true,
                fileName: result.fileName,
                originalFormat: result.originalFormat,
                dimensions: result.dimensions,
                fileSize: result.fileSize,
                thumbnail: result.thumbnail,
                message: result.message
            });
        } else {
            // Error - add error result card
            addResultCard({
                success: false,
                fileName: file.name,
                message: result.message || 'Processing failed'
            });
        }
    } catch (error) {
        console.error('Error processing file:', file.name, error);
        addResultCard({
            success: false,
            fileName: file.name,
            message: `Network error: ${error.message}`
        });
    }
}

// Add a result card to the results container
function addResultCard(result) {
    const card = document.createElement('div');
    card.className = `result-card ${result.success ? 'success' : 'error'}`;
    
    let cardContent = `
        <div class="result-header">
            <span class="result-status">${result.success ? '✅' : '❌'}</span>
            <span class="result-filename">${result.fileName}</span>
        </div>
    `;
    
    if (result.success && result.thumbnail) {
        cardContent += `
            <img src="${result.thumbnail}" alt="Thumbnail" class="thumbnail" />
            <div class="result-details">
                <div><strong>Format:</strong> ${result.originalFormat}</div>
                <div><strong>Size:</strong> ${formatFileSize(result.fileSize)}</div>
                <div><strong>Dimensions:</strong> ${result.dimensions.width}×${result.dimensions.height}</div>
                <div><strong>Status:</strong> Processed</div>
            </div>
        `;
    }
    
    cardContent += `
        <div class="result-message ${result.success ? '' : 'error-message'}">
            ${result.message}
        </div>
    `;
    
    card.innerHTML = cardContent;
    
    // Add to results container at the top
    resultsContainer.insertBefore(card, resultsContainer.firstChild);
    
    // Scroll the new card into view
    card.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
}

// Show/hide processing status
function showProcessingStatus(show) {
    processingStatus.style.display = show ? 'flex' : 'none';
}

// Show notification (simple alert for now)
function showNotification(message, type = 'info') {
    // For now, using alert. In a real app, you might use a toast notification system
    alert(message);
}

// Format file size for display
function formatFileSize(bytes) {
    if (bytes === 0) return '0 Bytes';
    
    const k = 1024;
    const sizes = ['Bytes', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    
    return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
}

// Utility function for creating elements (if needed)
function createElement(tag, className, innerHTML) {
    const element = document.createElement(tag);
    if (className) element.className = className;
    if (innerHTML) element.innerHTML = innerHTML;
    return element;
}