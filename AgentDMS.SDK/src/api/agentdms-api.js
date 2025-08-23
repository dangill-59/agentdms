const axios = require('axios');
const fs = require('fs');
const FormData = require('form-data');
const https = require('https');

class AgentDMSAPI {
  constructor(baseUrl = 'http://localhost:5249', options = {}) {
    this.baseUrl = baseUrl;
    this.apiBase = `${baseUrl}/api/ImageProcessing`;
    this.options = {
      rejectUnauthorized: options.rejectUnauthorized !== false, // Default to true for security
      ...options
    };
    
    // Create axios instance with SSL configuration
    this.axiosInstance = this.createAxiosInstance();
  }

  /**
   * Create axios instance with appropriate SSL configuration
   * @returns {Object} Configured axios instance
   */
  createAxiosInstance() {
    const config = {};
    
    // For localhost connections, allow self-signed certificates in development
    if (this.baseUrl.includes('localhost') || this.baseUrl.includes('127.0.0.1')) {
      // Check if we're in development mode (Electron dev mode or NODE_ENV)
      const isDevelopment = process.argv.includes('--dev') || process.env.NODE_ENV === 'development';
      
      if (isDevelopment || !this.options.rejectUnauthorized) {
        config.httpsAgent = new https.Agent({
          rejectUnauthorized: false
        });
      }
    }
    
    return axios.create(config);
  }

  /**
   * Set the base URL for the AgentDMS API
   * @param {string} url - The base URL (e.g., 'http://localhost:5249')
   */
  setBaseUrl(url) {
    this.baseUrl = url;
    this.apiBase = `${url}/api/ImageProcessing`;
    // Recreate axios instance with new URL
    this.axiosInstance = this.createAxiosInstance();
  }

  /**
   * Get supported file formats
   * @returns {Promise<string[]>} Array of supported file extensions
   */
  async getSupportedFormats() {
    try {
      const response = await this.axiosInstance.get(`${this.apiBase}/formats`);
      return response.data;
    } catch (error) {
      console.error('Error getting supported formats:', error);
      throw new Error(`Failed to get supported formats: ${error.message}`);
    }
  }

  /**
   * Get available scanners
   * @returns {Promise<Object[]>} Array of scanner objects
   */
  async getAvailableScanners() {
    try {
      const response = await this.axiosInstance.get(`${this.apiBase}/scanners`);
      return response.data;
    } catch (error) {
      console.error('Error getting scanners:', error);
      throw new Error(`Failed to get scanners: ${error.message}`);
    }
  }

  /**
   * Get scanner capabilities
   * @returns {Promise<Object>} Scanner capabilities object
   */
  async getScannerCapabilities() {
    try {
      const response = await this.axiosInstance.get(`${this.apiBase}/scanners/capabilities`);
      return response.data;
    } catch (error) {
      console.error('Error getting scanner capabilities:', error);
      throw new Error(`Failed to get scanner capabilities: ${error.message}`);
    }
  }

  /**
   * Scan a document
   * @param {Object} options - Scan options
   * @param {string} options.scannerName - Name of the scanner to use
   * @param {number} options.resolution - Resolution in DPI (default: 300)
   * @param {string} options.colorMode - Color mode: 'Color', 'Grayscale', 'BlackAndWhite' (default: 'Color')
   * @param {string} options.paperSize - Paper size: 'A4', 'Letter', 'Legal', etc. (default: 'A4')
   * @param {boolean} options.showUserInterface - Whether to show scanner UI (default: false)
   * @returns {Promise<Object>} Scan result object
   */
  async scanDocument(options = {}) {
    try {
      const scanOptions = {
        scannerName: options.scannerName || '',
        resolution: options.resolution || 300,
        colorMode: options.colorMode || 'Color',
        paperSize: options.paperSize || 'A4',
        showUserInterface: options.showUserInterface || false,
        duplex: options.duplex || false,
        ...options
      };

      const response = await this.axiosInstance.post(`${this.apiBase}/scan`, scanOptions);
      return response.data;
    } catch (error) {
      console.error('Error scanning document:', error);
      throw new Error(`Failed to scan document: ${error.message}`);
    }
  }

  /**
   * Upload and process a file
   * @param {string} filePath - Path to the file to upload
   * @param {Object} options - Processing options
   * @param {number} options.thumbnailSize - Thumbnail size in pixels (default: 200)
   * @param {string} options.outputFormat - Output format: 'png', 'jpg' (default: 'png')
   * @returns {Promise<Object>} Processing result object
   */
  async uploadFile(filePath, options = {}) {
    try {
      if (!fs.existsSync(filePath)) {
        throw new Error(`File not found: ${filePath}`);
      }

      const form = new FormData();
      form.append('file', fs.createReadStream(filePath));
      
      // Add processing options
      if (options.thumbnailSize) {
        form.append('thumbnailSize', options.thumbnailSize.toString());
      }
      if (options.outputFormat) {
        form.append('outputFormat', options.outputFormat);
      }

      const response = await this.axiosInstance.post(`${this.apiBase}/upload`, form, {
        headers: {
          ...form.getHeaders(),
          'Content-Type': 'multipart/form-data'
        },
        maxContentLength: Infinity,
        maxBodyLength: Infinity
      });

      return response.data;
    } catch (error) {
      console.error('Error uploading file:', error);
      throw new Error(`Failed to upload file: ${error.message}`);
    }
  }

  /**
   * Process a file by path (server-side file)
   * @param {string} filePath - Path to the file on the server
   * @param {Object} options - Processing options
   * @returns {Promise<Object>} Processing result object
   */
  async processFile(filePath, options = {}) {
    try {
      const response = await this.axiosInstance.post(`${this.apiBase}/process`, {
        filePath: filePath,
        ...options
      });
      return response.data;
    } catch (error) {
      console.error('Error processing file:', error);
      throw new Error(`Failed to process file: ${error.message}`);
    }
  }

  /**
   * Get job status
   * @param {string} jobId - Job ID to check
   * @returns {Promise<Object>} Job status object
   */
  async getJobStatus(jobId) {
    try {
      const response = await this.axiosInstance.get(`${this.apiBase}/job/${jobId}/status`);
      return response.data;
    } catch (error) {
      console.error('Error getting job status:', error);
      throw new Error(`Failed to get job status: ${error.message}`);
    }
  }

  /**
   * Check API health
   * @returns {Promise<Object>} Health status object
   */
  async checkHealth() {
    try {
      const response = await this.axiosInstance.get(`${this.baseUrl}/api/apidocumentation/health`);
      return response.data;
    } catch (error) {
      console.error('Error checking API health:', error);
      throw new Error(`Failed to check API health: ${error.message}`);
    }
  }
}

module.exports = { AgentDMSAPI };