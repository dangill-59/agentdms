const { contextBridge, ipcRenderer } = require('electron');

// Expose protected methods that allow the renderer process to use
// the ipcRenderer without exposing the entire object
contextBridge.exposeInMainWorld('electronAPI', {
  // File operations
  openFile: () => ipcRenderer.invoke('dialog:openFile'),
  readFileContent: (filePath) => ipcRenderer.invoke('file:readContent', filePath),
  
  // API calls
  getSupportedFormats: () => ipcRenderer.invoke('api-get-formats'),
  getAvailableScanners: () => ipcRenderer.invoke('api-get-scanners'),
  scanDocument: (options) => ipcRenderer.invoke('api-scan-document', options),
  uploadFile: (filePath, options) => ipcRenderer.invoke('api-upload-file', filePath, options),
  processFile: (filePath, options) => ipcRenderer.invoke('api-process-file', filePath, options),

  // Menu events
  onMenuAction: (callback) => {
    // Set up handlers for each menu action that call the callback with the action name
    ipcRenderer.on('menu-open-file', () => callback(null, 'menu-open-file'));
    ipcRenderer.on('menu-scan-document', () => callback(null, 'menu-scan-document'));
    ipcRenderer.on('menu-zoom-in', () => callback(null, 'menu-zoom-in'));
    ipcRenderer.on('menu-zoom-out', () => callback(null, 'menu-zoom-out'));
    ipcRenderer.on('menu-zoom-reset', () => callback(null, 'menu-zoom-reset'));
    ipcRenderer.on('menu-toggle-annotation', () => callback(null, 'menu-toggle-annotation'));
    ipcRenderer.on('menu-upload', () => callback(null, 'menu-upload'));
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