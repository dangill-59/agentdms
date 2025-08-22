/**
 * AgentDMS Upload Component
 * Handles file uploads to configurable backend endpoints
 */
class AgentDMSUploader {
    constructor(options = {}) {
        this.options = {
            apiBaseUrl: 'http://localhost:5249',
            uploadEndpoint: null, // Will use apiBaseUrl + '/api/ImageProcessing/upload' if not specified
            maxFileSize: 50 * 1024 * 1024, // 50MB default
            supportedFormats: ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.tiff', '.pdf', '.webp'],
            showProgress: true,
            autoProcess: true,
            thumbnailSize: 200,
            ...options
        };
        
        this.isUploading = false;
        this.currentUpload = null;
    }

    async uploadFile(file, options = {}) {
        if (this.isUploading) {
            throw new Error('Upload already in progress');
        }

        // Validate file
        this.validateFile(file);

        const uploadOptions = {
            thumbnailSize: this.options.thumbnailSize,
            outputFormat: 'png',
            ...options
        };

        try {
            this.isUploading = true;
            this.showProgress('Preparing upload...');

            let result;
            if (window.electronAPI) {
                // Running in Electron - use IPC
                result = await this.uploadViaElectron(file, uploadOptions);
            } else {
                // Running in browser - direct HTTP
                result = await this.uploadViaHTTP(file, uploadOptions);
            }

            this.hideProgress();
            this.showStatus('Upload completed successfully', 'success');
            
            return result;

        } catch (error) {
            this.hideProgress();
            this.showStatus(`Upload failed: ${error.message}`, 'error');
            throw error;
        } finally {
            this.isUploading = false;
            this.currentUpload = null;
        }
    }

    async uploadViaElectron(file, options) {
        // Convert File to a path that Electron can handle
        // This is a simplified approach - in practice, you'd save the file temporarily
        const tempPath = await this.saveTemporaryFile(file);
        
        try {
            return await window.electronAPI.uploadFile(tempPath, options);
        } finally {
            // Clean up temporary file
            this.cleanupTemporaryFile(tempPath);
        }
    }

    async uploadViaHTTP(file, options) {
        const formData = new FormData();
        formData.append('file', file);
        
        // Add options as form fields
        Object.keys(options).forEach(key => {
            if (options[key] !== null && options[key] !== undefined) {
                formData.append(key, options[key].toString());
            }
        });

        const endpoint = this.getUploadEndpoint();
        
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            
            // Track upload progress
            xhr.upload.addEventListener('progress', (e) => {
                if (e.lengthComputable && this.options.showProgress) {
                    const percentComplete = (e.loaded / e.total) * 100;
                    this.updateProgress(Math.round(percentComplete));
                }
            });
            
            xhr.addEventListener('load', () => {
                if (xhr.status >= 200 && xhr.status < 300) {
                    try {
                        const result = JSON.parse(xhr.responseText);
                        resolve(result);
                    } catch (e) {
                        reject(new Error('Invalid response format'));
                    }
                } else {
                    reject(new Error(`Upload failed: ${xhr.status} ${xhr.statusText}`));
                }
            });
            
            xhr.addEventListener('error', () => {
                reject(new Error('Network error during upload'));
            });
            
            xhr.addEventListener('timeout', () => {
                reject(new Error('Upload timeout'));
            });
            
            xhr.open('POST', endpoint);
            xhr.timeout = 300000; // 5 minutes
            xhr.send(formData);
            
            this.currentUpload = xhr;
        });
    }

    async saveTemporaryFile(file) {
        // In a real implementation, this would use Node.js fs to save the file
        // For now, we'll use a simplified approach
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = (e) => {
                // In Electron, you'd save this to a temp directory
                // For demo purposes, we'll just pass the data URL
                resolve(e.target.result);
            };
            reader.onerror = () => reject(new Error('Failed to read file'));
            reader.readAsDataURL(file);
        });
    }

    cleanupTemporaryFile(tempPath) {
        // In a real implementation, this would delete the temporary file
        // For demo purposes, this is a no-op
    }

    validateFile(file) {
        if (!file) {
            throw new Error('No file provided');
        }

        if (file.size > this.options.maxFileSize) {
            const maxSizeMB = Math.round(this.options.maxFileSize / (1024 * 1024));
            throw new Error(`File size exceeds ${maxSizeMB}MB limit`);
        }

        const fileExtension = this.getFileExtension(file.name);
        if (!this.options.supportedFormats.includes(fileExtension)) {
            throw new Error(`Unsupported file format: ${fileExtension}`);
        }
    }

    getFileExtension(filename) {
        return '.' + filename.split('.').pop().toLowerCase();
    }

    getUploadEndpoint() {
        if (this.options.uploadEndpoint) {
            return this.options.uploadEndpoint;
        }
        return `${this.options.apiBaseUrl}/api/ImageProcessing/upload`;
    }

    async uploadCurrentFile() {
        if (window.agentDMSApp && window.agentDMSApp.viewer) {
            const currentFile = window.agentDMSApp.viewer.getCurrentFile();
            if (currentFile) {
                return await this.uploadFile(currentFile);
            } else {
                throw new Error('No file currently loaded');
            }
        } else {
            throw new Error('No viewer instance available');
        }
    }

    async uploadWithAnnotations() {
        if (window.agentDMSApp && window.agentDMSApp.annotator) {
            const annotatedImageDataUrl = window.agentDMSApp.annotator.exportAnnotatedImage();
            if (annotatedImageDataUrl) {
                // Convert data URL to blob
                const response = await fetch(annotatedImageDataUrl);
                const blob = await response.blob();
                const file = new File([blob], 'annotated-document.png', { type: 'image/png' });
                
                return await this.uploadFile(file, { 
                    annotated: true,
                    annotations: window.agentDMSApp.annotator.getAnnotations()
                });
            } else {
                throw new Error('No annotations available');
            }
        } else {
            throw new Error('No annotator instance available');
        }
    }

    cancelUpload() {
        if (this.currentUpload && this.isUploading) {
            this.currentUpload.abort();
            this.isUploading = false;
            this.currentUpload = null;
            this.hideProgress();
            this.showStatus('Upload cancelled', 'warning');
        }
    }

    showProgress(message = 'Uploading...') {
        if (!this.options.showProgress) return;
        
        const statusElement = document.getElementById('statusMessage');
        const progressContainer = document.getElementById('progressContainer');
        
        if (statusElement) {
            statusElement.textContent = message;
            statusElement.className = 'status-info';
        }
        
        if (progressContainer) {
            progressContainer.style.display = 'block';
        }
    }

    updateProgress(percent) {
        if (!this.options.showProgress) return;
        
        const progressBar = document.getElementById('progressBar');
        const statusElement = document.getElementById('statusMessage');
        
        if (progressBar) {
            progressBar.style.width = `${percent}%`;
            progressBar.setAttribute('aria-valuenow', percent);
        }
        
        if (statusElement) {
            statusElement.textContent = `Uploading... ${percent}%`;
        }
    }

    hideProgress() {
        const progressContainer = document.getElementById('progressContainer');
        const progressBar = document.getElementById('progressBar');
        
        if (progressContainer) {
            progressContainer.style.display = 'none';
        }
        
        if (progressBar) {
            progressBar.style.width = '0%';
            progressBar.setAttribute('aria-valuenow', 0);
        }
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

    // Configuration methods
    setApiBaseUrl(url) {
        this.options.apiBaseUrl = url;
    }

    setUploadEndpoint(endpoint) {
        this.options.uploadEndpoint = endpoint;
    }

    setMaxFileSize(sizeInBytes) {
        this.options.maxFileSize = sizeInBytes;
    }

    setSupportedFormats(formats) {
        this.options.supportedFormats = formats;
    }

    // Utility methods
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

    // Public API methods
    isReady() {
        return !this.isUploading;
    }

    isBusy() {
        return this.isUploading;
    }

    getConfiguration() {
        return { ...this.options };
    }

    updateConfiguration(newOptions) {
        this.options = { ...this.options, ...newOptions };
    }
}

// Make available globally
window.agentDMSUploader = {
    create: (options) => new AgentDMSUploader(options)
};