/**
 * AgentDMS Scanner Component
 * Handles scanning functionality through the AgentDMS API
 */
class AgentDMSScanner {
    constructor(options = {}) {
        this.options = {
            autoLoadScanners: true,
            defaultResolution: 300,
            defaultColorMode: 'Color',
            defaultPaperSize: 'A4',
            showProgressDialog: true,
            ...options
        };
        
        this.availableScanners = [];
        this.selectedScanner = null;
        this.isScanning = false;
        
        if (this.options.autoLoadScanners) {
            this.loadScanners();
        }
    }

    async loadScanners() {
        try {
            this.showStatus('Loading available scanners...', 'info');
            
            if (window.electronAPI) {
                // Running in Electron
                this.availableScanners = await window.electronAPI.getAvailableScanners();
            } else {
                // Running in browser/web context
                throw new Error('Scanner functionality requires Electron environment');
            }
            
            this.updateScannerUI();
            this.showStatus(`Found ${this.availableScanners.length} scanner(s)`, 'success');
            
        } catch (error) {
            this.showStatus(`Error loading scanners: ${error.message}`, 'error');
            console.error('Error loading scanners:', error);
        }
    }

    updateScannerUI() {
        const scannerSelect = document.getElementById('scannerSelect');
        if (scannerSelect) {
            scannerSelect.innerHTML = '';
            
            if (this.availableScanners.length === 0) {
                scannerSelect.innerHTML = '<option value="">No scanners found</option>';
            } else {
                scannerSelect.innerHTML = '<option value="">Select a scanner...</option>';
                this.availableScanners.forEach(scanner => {
                    const option = document.createElement('option');
                    option.value = scanner.name || scanner;
                    option.textContent = scanner.displayName || scanner.name || scanner;
                    scannerSelect.appendChild(option);
                });
            }
        }
    }

    async scan(options = {}) {
        if (this.isScanning) {
            throw new Error('Scan already in progress');
        }

        const scanOptions = {
            scannerName: options.scannerName || this.getSelectedScanner(),
            resolution: options.resolution || this.options.defaultResolution,
            colorMode: options.colorMode || this.options.defaultColorMode,
            paperSize: options.paperSize || this.options.defaultPaperSize,
            showUserInterface: options.showUserInterface || false,
            duplex: options.duplex || false,
            ...options
        };

        if (!scanOptions.scannerName) {
            throw new Error('No scanner selected');
        }

        try {
            this.isScanning = true;
            this.showProgress('Initializing scan...');
            
            let result;
            if (window.electronAPI) {
                // Running in Electron
                result = await window.electronAPI.scanDocument(scanOptions);
            } else {
                // Running in browser - would need to call API directly
                throw new Error('Browser scanning not implemented - use Electron version');
            }
            
            this.hideProgress();
            this.showStatus('Scan completed successfully', 'success');
            
            // If we have a viewer instance, load the scanned image
            if (window.agentDMSApp && window.agentDMSApp.viewer && result.imagePaths && result.imagePaths.length > 0) {
                // Convert the first scanned image to a File object and load it
                this.loadScannedImage(result.imagePaths[0]);
            }
            
            return result;
            
        } catch (error) {
            this.hideProgress();
            this.showStatus(`Scan failed: ${error.message}`, 'error');
            throw error;
        } finally {
            this.isScanning = false;
        }
    }

    async loadScannedImage(imagePath) {
        try {
            // For Electron, we need to read the file through the main process
            if (window.electronAPI) {
                const fileContent = await window.electronAPI.readFileContent(imagePath);
                
                if (fileContent.success) {
                    // Create File object from data URL
                    const response = await fetch(fileContent.dataUrl);
                    const blob = await response.blob();
                    const file = new File([blob], fileContent.fileName, { type: fileContent.mimeType });
                    window.agentDMSApp.viewer.loadFile(file);
                } else {
                    throw new Error(fileContent.error);
                }
            }
        } catch (error) {
            console.error('Error loading scanned image:', error);
        }
    }

    getSelectedScanner() {
        const scannerSelect = document.getElementById('scannerSelect');
        return scannerSelect ? scannerSelect.value : '';
    }

    getScanOptions() {
        return {
            scannerName: this.getSelectedScanner(),
            resolution: this.getResolution(),
            colorMode: this.getColorMode(),
            paperSize: this.getPaperSize(),
            duplex: this.getDuplex(),
            showUserInterface: this.getShowUserInterface()
        };
    }

    getResolution() {
        const resolutionSelect = document.getElementById('resolutionSelect');
        return resolutionSelect ? parseInt(resolutionSelect.value) : this.options.defaultResolution;
    }

    getColorMode() {
        const colorModeSelect = document.getElementById('colorModeSelect');
        return colorModeSelect ? colorModeSelect.value : this.options.defaultColorMode;
    }

    getPaperSize() {
        const paperSizeSelect = document.getElementById('paperSizeSelect');
        return paperSizeSelect ? paperSizeSelect.value : this.options.defaultPaperSize;
    }

    getDuplex() {
        const duplexCheck = document.getElementById('duplexCheck');
        return duplexCheck ? duplexCheck.checked : false;
    }

    getShowUserInterface() {
        const showUICheck = document.getElementById('showUICheck');
        return showUICheck ? showUICheck.checked : false;
    }

    showScanDialog() {
        const modal = document.getElementById('scanModal');
        if (modal) {
            const bsModal = new bootstrap.Modal(modal);
            bsModal.show();
        }
    }

    hideScanDialog() {
        const modal = document.getElementById('scanModal');
        if (modal) {
            const bsModal = bootstrap.Modal.getInstance(modal);
            if (bsModal) {
                bsModal.hide();
            }
        }
    }

    showProgress(message = 'Scanning...') {
        if (!this.options.showProgressDialog) return;
        
        const statusElement = document.getElementById('statusMessage');
        const progressContainer = document.getElementById('progressContainer');
        const progressBar = document.getElementById('progressBar');
        
        if (statusElement) {
            statusElement.textContent = message;
            statusElement.className = 'status-info';
        }
        
        if (progressContainer) {
            progressContainer.style.display = 'block';
        }
        
        if (progressBar) {
            progressBar.style.width = '100%';
            progressBar.classList.add('progress-bar-animated', 'progress-bar-striped');
        }
    }

    hideProgress() {
        const progressContainer = document.getElementById('progressContainer');
        const progressBar = document.getElementById('progressBar');
        
        if (progressContainer) {
            progressContainer.style.display = 'none';
        }
        
        if (progressBar) {
            progressBar.classList.remove('progress-bar-animated', 'progress-bar-striped');
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

    // Preset scan configurations
    async scanDocument() {
        return await this.scan({
            resolution: 300,
            colorMode: 'Color',
            paperSize: 'A4'
        });
    }

    async scanPhoto() {
        return await this.scan({
            resolution: 600,
            colorMode: 'Color',
            paperSize: 'A4'
        });
    }

    async scanText() {
        return await this.scan({
            resolution: 300,
            colorMode: 'BlackAndWhite',
            paperSize: 'A4'
        });
    }

    async quickScan() {
        return await this.scan({
            resolution: 150,
            colorMode: 'Color',
            paperSize: 'A4'
        });
    }

    // Public API methods
    getAvailableScanners() {
        return this.availableScanners;
    }

    isReady() {
        return this.availableScanners.length > 0 && !this.isScanning;
    }

    isBusy() {
        return this.isScanning;
    }

    refresh() {
        return this.loadScanners();
    }
}

// Make available globally
window.agentDMSScanner = {
    create: (options) => new AgentDMSScanner(options)
};