const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods that allow the renderer process to use
// the ipcRenderer without exposing the entire object
contextBridge.exposeInMainWorld('electronAPI', {
  // File operations
  openFile: () => ipcRenderer.invoke('dialog:openFile'),
  
  // API calls
  getSupportedFormats: () => ipcRenderer.invoke('api-get-formats'),
  getAvailableScanners: () => ipcRenderer.invoke('api-get-scanners'),
  scanDocument: (options) => ipcRenderer.invoke('api-scan-document', options),
  uploadFile: (filePath, options) => ipcRenderer.invoke('api-upload-file', filePath, options),
  processFile: (filePath, options) => ipcRenderer.invoke('api-process-file', filePath, options),

  // Menu events
  onMenuAction: (callback) => {
    ipcRenderer.on('menu-open-file', callback);
    ipcRenderer.on('menu-scan-document', callback);
    ipcRenderer.on('menu-zoom-in', callback);
    ipcRenderer.on('menu-zoom-out', callback);
    ipcRenderer.on('menu-zoom-reset', callback);
    ipcRenderer.on('menu-toggle-annotation', callback);
    ipcRenderer.on('menu-upload', callback);
  },

  // Remove listeners
  removeAllListeners: (channel) => ipcRenderer.removeAllListeners(channel),

  // Configuration
  setAPIBaseUrl: (url) => ipcRenderer.invoke('set-api-base-url', url)
});

// Expose AgentDMS SDK for standalone usage
contextBridge.exposeInMainWorld('AgentDMSSDK', {
  // Core viewer functionality
  createViewer: (containerId, options) => {
    return window.agentDMSViewer.create(containerId, options);
  },
  
  // Scanner functionality
  createScanner: (options) => {
    return window.agentDMSScanner.create(options);
  },
  
  // Annotation functionality
  createAnnotator: (containerId, options) => {
    return window.agentDMSAnnotator.create(containerId, options);
  },
  
  // Upload functionality
  createUploader: (options) => {
    return window.agentDMSUploader.create(options);
  }
});