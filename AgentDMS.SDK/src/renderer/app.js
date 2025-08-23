/**
 * AgentDMS SDK Main Application
 * Integrates all components and provides the main application interface
 */
class AgentDMSApp {
    constructor() {
        this.viewer = null;
        this.scanner = null;
        this.annotator = null;
        this.uploader = null;
        this.config = {
            apiBaseUrl: 'http://localhost:5249'
        };
        
        this.init();
    }

    async init() {
        try {
            // Initialize components
            this.viewer = window.agentDMSViewer.create('viewerContainer', {
                allowZoom: true,
                allowPan: true,
                allowRotation: true
            });
            
            this.scanner = window.agentDMSScanner.create({
                autoLoadScanners: true,
                showProgressDialog: true
            });
            
            this.annotator = window.agentDMSAnnotator.create('viewerContainer', {
                enableDrawing: true,
                enableHighlighting: true,
                enableRedaction: true
            });
            
            this.uploader = window.agentDMSUploader.create({
                apiBaseUrl: this.config.apiBaseUrl,
                showProgress: true
            });
            
            // Setup event handlers
            this.setupEventHandlers();
            
            // Setup menu handlers if in Electron
            if (window.electronAPI) {
                this.setupElectronMenuHandlers();
            }
            
            // Load initial data
            await this.loadInitialData();
            
            console.log('AgentDMS SDK initialized successfully');
            
        } catch (error) {
            console.error('Failed to initialize AgentDMS SDK:', error);
            this.showError('Failed to initialize application: ' + error.message);
        }
    }

    setupEventHandlers() {
        // File operations
        const openFileBtn = document.getElementById('openFileBtn');
        if (openFileBtn) {
            openFileBtn.addEventListener('click', () => this.openFile());
        }

        // Scan operations
        const scanBtn = document.getElementById('scanBtn');
        if (scanBtn) {
            scanBtn.addEventListener('click', () => this.showScanDialog());
        }

        const startScanBtn = document.getElementById('startScanBtn');
        if (startScanBtn) {
            startScanBtn.addEventListener('click', () => this.performScan());
        }

        // Annotation operations
        const annotateBtn = document.getElementById('annotateBtn');
        if (annotateBtn) {
            annotateBtn.addEventListener('click', () => this.toggleAnnotation());
        }

        const drawBtn = document.getElementById('drawBtn');
        if (drawBtn) {
            drawBtn.addEventListener('click', () => this.annotator.setTool('draw'));
        }

        const highlightBtn = document.getElementById('highlightBtn');
        if (highlightBtn) {
            highlightBtn.addEventListener('click', () => this.annotator.setTool('highlight'));
        }

        const redactBtn = document.getElementById('redactBtn');
        if (redactBtn) {
            redactBtn.addEventListener('click', () => this.annotator.setTool('redact'));
        }

        const clearAnnotationsBtn = document.getElementById('clearAnnotationsBtn');
        if (clearAnnotationsBtn) {
            clearAnnotationsBtn.addEventListener('click', () => this.annotator.clearAnnotations());
        }

        // Viewer controls
        const zoomInBtn = document.getElementById('zoomInBtn');
        if (zoomInBtn) {
            zoomInBtn.addEventListener('click', () => this.viewer.zoomIn());
        }

        const zoomOutBtn = document.getElementById('zoomOutBtn');
        if (zoomOutBtn) {
            zoomOutBtn.addEventListener('click', () => this.viewer.zoomOut());
        }

        const resetZoomBtn = document.getElementById('resetZoomBtn');
        if (resetZoomBtn) {
            resetZoomBtn.addEventListener('click', () => this.viewer.resetView());
        }

        const rotateBtn = document.getElementById('rotateBtn');
        if (rotateBtn) {
            rotateBtn.addEventListener('click', () => this.viewer.rotateClockwise());
        }

        // Upload operations
        const uploadBtn = document.getElementById('uploadBtn');
        if (uploadBtn) {
            uploadBtn.addEventListener('click', () => this.uploadCurrentFile());
        }

        // Configuration
        const saveConfigBtn = document.getElementById('saveConfigBtn');
        if (saveConfigBtn) {
            saveConfigBtn.addEventListener('click', () => this.saveConfiguration());
        }
    }

    setupElectronMenuHandlers() {
        if (!window.electronAPI) return;

        window.electronAPI.onMenuAction((event, action) => {
            switch (action) {
                case 'menu-open-file':
                    this.openFile();
                    break;
                case 'menu-scan-document':
                    this.showScanDialog();
                    break;
                case 'menu-zoom-in':
                    this.viewer.zoomIn();
                    break;
                case 'menu-zoom-out':
                    this.viewer.zoomOut();
                    break;
                case 'menu-zoom-reset':
                    this.viewer.resetView();
                    break;
                case 'menu-toggle-annotation':
                    this.toggleAnnotation();
                    break;
                case 'menu-upload':
                    this.uploadCurrentFile();
                    break;
            }
        });
    }

    async loadInitialData() {
        try {
            // Load supported formats if API is available
            if (window.electronAPI) {
                const formats = await window.electronAPI.getSupportedFormats();
                console.log('Supported formats:', formats);
            }
        } catch (error) {
            console.warn('Could not load initial data:', error);
        }
    }

    async openFile() {
        try {
            if (window.electronAPI) {
                // In Electron, use native file dialog
                const result = await window.electronAPI.openFile();
                if (result && result.filePaths && result.filePaths.length > 0) {
                    // Read file content through main process
                    const filePath = result.filePaths[0];
                    const fileContent = await window.electronAPI.readFileContent(filePath);
                    
                    if (fileContent.success) {
                        // Create File object from data URL
                        const response = await fetch(fileContent.dataUrl);
                        const blob = await response.blob();
                        const file = new File([blob], fileContent.fileName, { type: fileContent.mimeType });
                        await this.viewer.loadFile(file);
                    } else {
                        throw new Error(fileContent.error);
                    }
                }
            } else {
                // In browser, use HTML file input
                const input = document.createElement('input');
                input.type = 'file';
                input.accept = '.jpg,.jpeg,.png,.gif,.bmp,.tiff,.pdf,.webp';
                input.onchange = async (e) => {
                    if (e.target.files.length > 0) {
                        await this.viewer.loadFile(e.target.files[0]);
                    }
                };
                input.click();
            }
        } catch (error) {
            this.showError('Failed to open file: ' + error.message);
        }
    }

    showScanDialog() {
        if (this.scanner.getAvailableScanners().length === 0) {
            this.showError('No scanners available. Please check your scanner connections and try refreshing.');
            return;
        }
        
        this.scanner.showScanDialog();
    }

    async performScan() {
        try {
            const scanOptions = this.scanner.getScanOptions();
            const result = await this.scanner.scan(scanOptions);
            
            // Hide the scan dialog
            this.scanner.hideScanDialog();
            
            this.showSuccess('Document scanned successfully');
            
        } catch (error) {
            this.showError('Scan failed: ' + error.message);
        }
    }

    toggleAnnotation() {
        if (!this.viewer.getCurrentFile()) {
            this.showError('Please load a file first');
            return;
        }
        
        this.annotator.toggle();
        
        const annotateBtn = document.getElementById('annotateBtn');
        if (annotateBtn) {
            if (this.annotator.isActive()) {
                annotateBtn.classList.add('active');
                annotateBtn.innerHTML = '<i class="bi bi-pencil-fill"></i> Exit Annotate';
            } else {
                annotateBtn.classList.remove('active');
                annotateBtn.innerHTML = '<i class="bi bi-pencil"></i> Annotate';
            }
        }
    }

    async uploadCurrentFile() {
        try {
            if (!this.viewer.getCurrentFile()) {
                this.showError('No file to upload');
                return;
            }
            
            let result;
            if (this.annotator.isActive() && this.annotator.getAnnotations().length > 0) {
                // Upload with annotations
                result = await this.uploader.uploadWithAnnotations();
            } else {
                // Upload original file
                result = await this.uploader.uploadCurrentFile();
            }
            
            this.showSuccess('File uploaded successfully');
            console.log('Upload result:', result);
            
        } catch (error) {
            this.showError('Upload failed: ' + error.message);
        }
    }

    async processPDFFile(fileName) {
        try {
            if (window.electronAPI) {
                const result = await window.electronAPI.processFile(fileName, {
                    outputFormat: 'png',
                    thumbnailSize: 800
                });
                
                this.showSuccess('PDF processed successfully');
                console.log('Process result:', result);
                
                // Load the first processed image
                if (result.imagePaths && result.imagePaths.length > 0) {
                    this.scanner.loadScannedImage(result.imagePaths[0]);
                }
            }
        } catch (error) {
            this.showError('Failed to process PDF: ' + error.message);
        }
    }

    saveConfiguration() {
        const apiBaseUrl = document.getElementById('apiBaseUrl')?.value;
        const uploadEndpoint = document.getElementById('uploadEndpoint')?.value;
        
        if (apiBaseUrl) {
            this.config.apiBaseUrl = apiBaseUrl;
            this.uploader.setApiBaseUrl(apiBaseUrl);
            
            if (window.electronAPI) {
                window.electronAPI.setAPIBaseUrl(apiBaseUrl);
            }
        }
        
        if (uploadEndpoint) {
            this.uploader.setUploadEndpoint(uploadEndpoint);
        }
        
        // Hide the config modal
        const modal = document.getElementById('configModal');
        if (modal) {
            const bsModal = bootstrap.Modal.getInstance(modal);
            if (bsModal) {
                bsModal.hide();
            }
        }
        
        this.showSuccess('Configuration saved');
    }

    showError(message) {
        this.showStatus(message, 'error');
        console.error(message);
    }

    showSuccess(message) {
        this.showStatus(message, 'success');
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

    // Public API methods for SDK usage
    getViewer() {
        return this.viewer;
    }

    getScanner() {
        return this.scanner;
    }

    getAnnotator() {
        return this.annotator;
    }

    getUploader() {
        return this.uploader;
    }

    setConfiguration(config) {
        this.config = { ...this.config, ...config };
        
        if (config.apiBaseUrl) {
            this.uploader.setApiBaseUrl(config.apiBaseUrl);
        }
    }

    getConfiguration() {
        return { ...this.config };
    }
}

// Initialize the app when DOM is loaded
document.addEventListener('DOMContentLoaded', () => {
    window.agentDMSApp = new AgentDMSApp();
});

// Make available globally for SDK usage
window.AgentDMS = {
    createApp: (config = {}) => {
        const app = new AgentDMSApp();
        if (Object.keys(config).length > 0) {
            app.setConfiguration(config);
        }
        return app;
    },
    
    // Direct component access
    createViewer: window.agentDMSViewer.create,
    createScanner: window.agentDMSScanner.create,
    createAnnotator: window.agentDMSAnnotator.create,
    createUploader: window.agentDMSUploader.create
};