/**
 * AgentDMS Document Viewer Component
 * Handles viewing of images and PDFs with zoom, pan, and rotation
 */
class AgentDMSViewer {
    constructor(containerId, options = {}) {
        this.container = document.getElementById(containerId);
        this.options = {
            allowZoom: true,
            allowPan: true,
            allowRotation: true,
            maxZoom: 5,
            minZoom: 0.1,
            zoomStep: 0.2,
            ...options
        };
        
        this.currentFile = null;
        this.currentZoom = 1;
        this.currentRotation = 0;
        this.isDragging = false;
        this.lastPanPoint = { x: 0, y: 0 };
        
        this.init();
    }

    init() {
        this.container.classList.add('viewer-container');
        this.setupEventListeners();
        this.setupDragAndDrop();
    }

    setupEventListeners() {
        // Mouse wheel for zooming
        this.container.addEventListener('wheel', (e) => {
            if (e.ctrlKey || e.metaKey) {
                e.preventDefault();
                const delta = e.deltaY > 0 ? -this.options.zoomStep : this.options.zoomStep;
                this.zoom(this.currentZoom + delta);
            }
        });

        // Pan functionality
        this.container.addEventListener('mousedown', (e) => {
            if (this.currentFile && this.currentZoom > 1) {
                this.isDragging = true;
                this.lastPanPoint = { x: e.clientX, y: e.clientY };
                e.preventDefault();
            }
        });

        document.addEventListener('mousemove', (e) => {
            if (this.isDragging) {
                const deltaX = e.clientX - this.lastPanPoint.x;
                const deltaY = e.clientY - this.lastPanPoint.y;
                this.pan(deltaX, deltaY);
                this.lastPanPoint = { x: e.clientX, y: e.clientY };
            }
        });

        document.addEventListener('mouseup', () => {
            this.isDragging = false;
        });
    }

    setupDragAndDrop() {
        this.container.addEventListener('dragover', (e) => {
            e.preventDefault();
            this.container.classList.add('drag-over');
        });

        this.container.addEventListener('dragleave', (e) => {
            e.preventDefault();
            this.container.classList.remove('drag-over');
        });

        this.container.addEventListener('drop', (e) => {
            e.preventDefault();
            this.container.classList.remove('drag-over');
            
            const files = Array.from(e.dataTransfer.files);
            if (files.length > 0) {
                this.loadFile(files[0]);
            }
        });

        // Add drag overlay
        const dragOverlay = document.createElement('div');
        dragOverlay.className = 'drag-overlay';
        dragOverlay.innerHTML = '<div class="drag-message"><i class="bi bi-cloud-upload"></i> Drop file to open</div>';
        this.container.appendChild(dragOverlay);
    }

    async loadFile(file) {
        try {
            this.showStatus('Loading file...', 'info');
            this.currentFile = file;
            
            const fileType = this.getFileType(file);
            
            switch (fileType) {
                case 'image':
                    await this.loadImage(file);
                    break;
                case 'pdf':
                    await this.loadPDF(file);
                    break;
                default:
                    throw new Error(`Unsupported file type: ${file.type}`);
            }
            
            this.updateFileInfo(file);
            this.showStatus('File loaded successfully', 'success');
            this.container.classList.add('has-content');
            
        } catch (error) {
            this.showStatus(`Error loading file: ${error.message}`, 'error');
            console.error('Error loading file:', error);
        }
    }

    async loadImage(file) {
        const url = URL.createObjectURL(file);
        
        // Clear content but preserve drag overlay
        const existingOverlay = this.container.querySelector('.drag-overlay');
        this.container.innerHTML = `
            <div class="document-viewer">
                <img class="document-image" src="${url}" alt="Document Image" />
            </div>
        `;
        
        // Re-add drag overlay if it existed
        if (existingOverlay) {
            this.container.appendChild(existingOverlay);
        }
        
        const img = this.container.querySelector('.document-image');
        
        return new Promise((resolve, reject) => {
            img.onload = () => {
                console.log('Image loaded successfully:', file.name);
                this.resetView();
                resolve();
            };
            img.onerror = (e) => {
                console.error('Image failed to load:', file.name, 'Error:', e);
                URL.revokeObjectURL(url);
                reject(new Error(`Failed to load image: ${file.name}`));
            };
        });
    }

    async loadPDF(file) {
        // For PDF viewing, we'll need to either use PDF.js or convert on the server
        // For now, we'll show a placeholder and suggest server-side conversion
        this.container.innerHTML = `
            <div class="document-viewer">
                <div class="text-center">
                    <i class="bi bi-file-earmark-pdf" style="font-size: 4rem; color: #dc3545;"></i>
                    <h4>PDF File Detected</h4>
                    <p>PDF viewing requires server-side processing.</p>
                    <button class="btn btn-primary" onclick="window.agentDMSApp.processPDFFile('${file.name}')">
                        <i class="bi bi-gear"></i> Process PDF
                    </button>
                </div>
            </div>
        `;
    }

    getFileType(file) {
        const imageTypes = ['image/jpeg', 'image/jpg', 'image/png', 'image/gif', 'image/bmp', 'image/tiff', 'image/webp'];
        const pdfTypes = ['application/pdf'];
        
        if (imageTypes.includes(file.type)) {
            return 'image';
        } else if (pdfTypes.includes(file.type)) {
            return 'pdf';
        }
        
        return 'unknown';
    }

    zoom(newZoom) {
        if (!this.currentFile || !this.options.allowZoom) return;
        
        newZoom = Math.max(this.options.minZoom, Math.min(this.options.maxZoom, newZoom));
        this.currentZoom = newZoom;
        
        const img = this.container.querySelector('.document-image');
        if (img) {
            img.style.transform = `scale(${this.currentZoom}) rotate(${this.currentRotation}deg)`;
            img.classList.toggle('zoomed', this.currentZoom > 1);
        }
        
        this.updateStatus();
    }

    pan(deltaX, deltaY) {
        if (!this.options.allowPan) return;
        
        const viewer = this.container.querySelector('.document-viewer');
        if (viewer) {
            const currentTransform = viewer.style.transform || '';
            const translateMatch = currentTransform.match(/translate\(([^)]+)\)/);
            
            let currentX = 0, currentY = 0;
            if (translateMatch) {
                const values = translateMatch[1].split(',');
                currentX = parseFloat(values[0]) || 0;
                currentY = parseFloat(values[1]) || 0;
            }
            
            const newX = currentX + deltaX;
            const newY = currentY + deltaY;
            
            viewer.style.transform = `translate(${newX}px, ${newY}px)`;
        }
    }

    rotate(degrees = 90) {
        if (!this.currentFile || !this.options.allowRotation) return;
        
        this.currentRotation = (this.currentRotation + degrees) % 360;
        
        const img = this.container.querySelector('.document-image');
        if (img) {
            img.style.transform = `scale(${this.currentZoom}) rotate(${this.currentRotation}deg)`;
        }
        
        this.updateStatus();
    }

    resetView() {
        this.currentZoom = 1;
        this.currentRotation = 0;
        
        const img = this.container.querySelector('.document-image');
        const viewer = this.container.querySelector('.document-viewer');
        
        if (img) {
            img.style.transform = 'scale(1) rotate(0deg)';
            img.classList.remove('zoomed');
        }
        
        if (viewer) {
            viewer.style.transform = '';
        }
        
        this.updateStatus();
    }

    updateFileInfo(file) {
        const fileInfoContainer = document.getElementById('fileInfo');
        if (fileInfoContainer) {
            fileInfoContainer.innerHTML = `
                <div class="file-info-item">
                    <span class="file-info-label">Name:</span>
                    <span class="file-info-value">${file.name}</span>
                </div>
                <div class="file-info-item">
                    <span class="file-info-label">Size:</span>
                    <span class="file-info-value">${this.formatFileSize(file.size)}</span>
                </div>
                <div class="file-info-item">
                    <span class="file-info-label">Type:</span>
                    <span class="file-info-value">${file.type}</span>
                </div>
                <div class="file-info-item">
                    <span class="file-info-label">Modified:</span>
                    <span class="file-info-value">${new Date(file.lastModified).toLocaleDateString()}</span>
                </div>
            `;
        }
    }

    formatFileSize(bytes) {
        const units = ['B', 'KB', 'MB', 'GB'];
        let size = bytes;
        let unitIndex = 0;
        
        while (size >= 1024 && unitIndex < units.length - 1) {
            size /= 1024;
            unitIndex++;
        }
        
        return `${size.toFixed(1)} ${units[unitIndex]}`;
    }

    updateStatus() {
        const zoomPercent = Math.round(this.currentZoom * 100);
        this.showStatus(`Zoom: ${zoomPercent}% | Rotation: ${this.currentRotation}°`, 'info', false);
    }

    showStatus(message, type = 'info', autoHide = true) {
        const statusElement = document.getElementById('statusMessage');
        if (statusElement) {
            statusElement.textContent = message;
            statusElement.className = `status-${type}`;
            
            if (autoHide) {
                setTimeout(() => {
                    statusElement.textContent = 'Ready';
                    statusElement.className = '';
                }, 3000);
            }
        }
    }

    // Public API methods
    zoomIn() {
        this.zoom(this.currentZoom + this.options.zoomStep);
    }

    zoomOut() {
        this.zoom(this.currentZoom - this.options.zoomStep);
    }

    rotateClockwise() {
        this.rotate(90);
    }

    rotateCounterClockwise() {
        this.rotate(-90);
    }

    getCurrentFile() {
        return this.currentFile;
    }

    getZoomLevel() {
        return this.currentZoom;
    }

    getRotation() {
        return this.currentRotation;
    }
}

// Make available globally
window.agentDMSViewer = {
    create: (containerId, options) => new AgentDMSViewer(containerId, options)
};