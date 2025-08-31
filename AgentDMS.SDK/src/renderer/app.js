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
            resetZoomBtn.addEventListener('click', () => {
                this.viewer.resetView();
                this.syncRotationDropdown();
            });
        }

        // Enhanced rotation controls
        const rotateLeftBtn = document.getElementById('rotateLeftBtn');
        if (rotateLeftBtn) {
            rotateLeftBtn.addEventListener('click', () => {
                this.viewer.rotateCounterClockwise();
                this.syncRotationDropdown();
            });
        }

        const rotateRightBtn = document.getElementById('rotateRightBtn');
        if (rotateRightBtn) {
            rotateRightBtn.addEventListener('click', () => {
                this.viewer.rotateClockwise();
                this.syncRotationDropdown();
            });
        }

        const rotationSelect = document.getElementById('rotationSelect');
        if (rotationSelect) {
            rotationSelect.addEventListener('change', (e) => this.setPreciseRotation(parseInt(e.target.value)));
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
        
        console.log('Setting up Electron menu handlers...');

        window.electronAPI.onMenuAction((event, action) => {
            console.log('Menu action received:', action);
            
            switch (action) {
                case 'menu-open-file':
                    console.log('Menu: Open File triggered');
                    this.openFile();
                    break;
                case 'menu-scan-document':
                    console.log('Menu: Scan Document triggered');
                    this.showScanDialog();
                    break;
                case 'menu-zoom-in':
                    console.log('Menu: Zoom In triggered');
                    this.viewer.zoomIn();
                    break;
                case 'menu-zoom-out':
                    console.log('Menu: Zoom Out triggered');
                    this.viewer.zoomOut();
                    break;
                case 'menu-zoom-reset':
                    console.log('Menu: Reset Zoom triggered');
                    this.viewer.resetView();
                    break;
                case 'menu-toggle-annotation':
                    console.log('Menu: Toggle Annotation triggered');
                    this.toggleAnnotation();
                    break;
                case 'menu-upload':
                    console.log('Menu: Upload triggered');
                    this.uploadCurrentFile();
                    break;
                default:
                    console.warn('Unknown menu action:', action);
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
            this.showStatus('Opening file dialog...', 'info');
            
            if (window.electronAPI) {
                // In Electron, use native file dialog
                console.log('Opening file dialog...');
                const result = await window.electronAPI.openFile();
                console.log('File dialog result:', result);
                
                if (result && !result.canceled && result.filePaths && result.filePaths.length > 0) {
                    // Read file content through main process
                    const filePath = result.filePaths[0];
                    console.log('Reading file content for:', filePath);
                    this.showStatus('Reading file...', 'info');
                    
                    const fileContent = await window.electronAPI.readFileContent(filePath);
                    console.log('File content result:', {
                        success: fileContent.success,
                        fileName: fileContent.fileName,
                        mimeType: fileContent.mimeType,
                        size: fileContent.size,
                        dataUrlLength: fileContent.dataUrl ? fileContent.dataUrl.length : 0
                    });
                    
                    if (fileContent.success) {
                        this.showStatus('Processing file...', 'info');
                        
                        // Create File object from data URL
                        console.log('Creating File object from data URL...');
                        console.log('Data URL length:', fileContent.dataUrl.length);
                        console.log('Data URL prefix:', fileContent.dataUrl.substring(0, 100));
                        
                        try {
                            const response = await fetch(fileContent.dataUrl);
                            console.log('Fetch response status:', response.status, response.statusText);
                            
                            const blob = await response.blob();
                            console.log('Blob created:', {
                                size: blob.size,
                                type: blob.type
                            });
                            
                            const file = new File([blob], fileContent.fileName, { type: fileContent.mimeType });
                            console.log('File object created:', {
                                name: file.name,
                                type: file.type,
                                size: file.size,
                                lastModified: file.lastModified
                            });
                            
                            // Validate File object
                            if (file.size === 0) {
                                throw new Error('Created file object has zero size');
                            }
                            if (!file.type) {
                                console.warn('File object has no MIME type, setting from fileContent');
                                // Create a new File object with explicit type
                                const correctedFile = new File([blob], fileContent.fileName, { 
                                    type: fileContent.mimeType,
                                    lastModified: Date.now()
                                });
                                console.log('Corrected file object:', {
                                    name: correctedFile.name,
                                    type: correctedFile.type,
                                    size: correctedFile.size
                                });
                                file = correctedFile;
                            }
                            
                            console.log('Loading file in viewer...');
                            this.showStatus('Loading image...', 'info');
                            await this.viewer.loadFile(file);
                            console.log('File loaded in viewer successfully');
                            this.showSuccess(`Successfully loaded ${fileContent.fileName}`);
                            
                        } catch (fileCreationError) {
                            console.error('Error during File object creation:', fileCreationError);
                            throw new Error(`Failed to create File object: ${fileCreationError.message}`);
                        }
                    } else {
                        throw new Error(fileContent.error || 'Failed to read file content');
                    }
                } else if (result && result.canceled) {
                    console.log('File dialog was canceled by user');
                    this.showStatus('File selection canceled', 'warning', true);
                } else {
                    console.log('No file selected or invalid dialog result');
                    this.showError('No file was selected');
                }
            } else {
                // In browser, use HTML file input
                const input = document.createElement('input');
                input.type = 'file';
                input.accept = '.jpg,.jpeg,.png,.gif,.bmp,.tiff,.pdf,.webp';
                input.onchange = async (e) => {
                    try {
                        if (e.target.files.length > 0) {
                            this.showStatus('Loading image...', 'info');
                            await this.viewer.loadFile(e.target.files[0]);
                            this.showSuccess(`Successfully loaded ${e.target.files[0].name}`);
                        } else {
                            this.showError('No file was selected');
                        }
                    } catch (error) {
                        console.error('Error loading file from input:', error);
                        this.showError('Failed to load file: ' + error.message);
                    }
                };
                input.click();
            }
        } catch (error) {
            console.error('Error in openFile:', error);
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

    setPreciseRotation(targetRotation) {
        if (!this.viewer.getCurrentFile()) {
            this.showError('Please load a file first');
            return;
        }
        
        // Calculate the rotation needed to reach target
        const currentRotation = this.viewer.getRotation();
        let rotationDiff = targetRotation - currentRotation;
        
        // Normalize the rotation difference to the shortest path
        while (rotationDiff > 180) rotationDiff -= 360;
        while (rotationDiff < -180) rotationDiff += 360;
        
        // Apply the rotation
        this.viewer.rotate(rotationDiff);
        
        this.showStatus(`Rotated to ${targetRotation}°`, 'success');
    }

    syncRotationDropdown() {
        const rotationSelect = document.getElementById('rotationSelect');
        if (rotationSelect && this.viewer.getCurrentFile()) {
            const currentRotation = this.viewer.getRotation();
            // Normalize rotation to 0-360 range
            const normalizedRotation = ((currentRotation % 360) + 360) % 360;
            rotationSelect.value = normalizedRotation;
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