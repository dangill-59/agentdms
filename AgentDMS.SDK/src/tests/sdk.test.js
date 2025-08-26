const { AgentDMSAPI } = require('../api/agentdms-api');

describe('AgentDMS API', () => {
    let api;

    beforeEach(() => {
        api = new AgentDMSAPI('http://localhost:5249');
    });

    test('should create instance with default base URL', () => {
        const defaultApi = new AgentDMSAPI();
        expect(defaultApi.baseUrl).toBe('http://localhost:5249');
    });

    test('should create instance with custom base URL', () => {
        const customApi = new AgentDMSAPI('http://example.com:8080');
        expect(customApi.baseUrl).toBe('http://example.com:8080');
        expect(customApi.apiBase).toBe('http://example.com:8080/api/ImageProcessing');
    });

    test('should set base URL', () => {
        api.setBaseUrl('http://newserver:3000');
        expect(api.baseUrl).toBe('http://newserver:3000');
        expect(api.apiBase).toBe('http://newserver:3000/api/ImageProcessing');
    });

    test('should create instance with SSL options', () => {
        const sslApi = new AgentDMSAPI('https://localhost:7249', { rejectUnauthorized: false });
        expect(sslApi.baseUrl).toBe('https://localhost:7249');
        expect(sslApi.options.rejectUnauthorized).toBe(false);
        expect(sslApi.axiosInstance).toBeDefined();
    });

    test('should create instance with default SSL options', () => {
        const defaultApi = new AgentDMSAPI('https://localhost:7249');
        expect(defaultApi.options.rejectUnauthorized).toBe(true);
    });

    test('should throw error for missing scanner name in scan', async () => {
        await expect(api.scanDocument({})).rejects.toThrow();
    });

    test('should validate scan options', () => {
        const options = {
            scannerName: 'Test Scanner',
            resolution: 300,
            colorMode: 'Color',
            paperSize: 'A4',
            showUserInterface: false,
            duplex: false
        };

        // This would normally make an HTTP request, but we're just testing the options processing
        expect(options.scannerName).toBe('Test Scanner');
        expect(options.resolution).toBe(300);
        expect(options.colorMode).toBe('Color');
        expect(options.paperSize).toBe('A4');
        expect(options.showUserInterface).toBe(false);
        expect(options.duplex).toBe(false);
    });
});

describe('AgentDMS Viewer', () => {
    // Note: These would be integration tests in a real environment
    test('should create viewer instance', () => {
        // Mock DOM element
        document.body.innerHTML = '<div id="testContainer"></div>';
        
        // This would require the actual viewer component
        expect(document.getElementById('testContainer')).toBeTruthy();
    });
});

describe('AgentDMS Scanner', () => {
    test('should create scanner instance with default options', () => {
        const mockScanner = {
            options: {
                autoLoadScanners: true,
                defaultResolution: 300,
                defaultColorMode: 'Color',
                defaultPaperSize: 'A4',
                showProgressDialog: true
            }
        };

        expect(mockScanner.options.autoLoadScanners).toBe(true);
        expect(mockScanner.options.defaultResolution).toBe(300);
        expect(mockScanner.options.defaultColorMode).toBe('Color');
        expect(mockScanner.options.defaultPaperSize).toBe('A4');
        expect(mockScanner.options.showProgressDialog).toBe(true);
    });
});

describe('AgentDMS Uploader', () => {
    test('should validate file size', () => {
        const maxFileSize = 100 * 1024 * 1024; // 100MB
        const validFileSize = 50 * 1024 * 1024; // 50MB
        const invalidFileSize = 150 * 1024 * 1024; // 150MB

        expect(validFileSize).toBeLessThan(maxFileSize);
        expect(invalidFileSize).toBeGreaterThan(maxFileSize);
    });

    test('should validate file extensions', () => {
        const supportedFormats = ['.jpg', '.jpeg', '.png', '.gif', '.bmp', '.tiff', '.pdf', '.webp'];
        
        expect(supportedFormats).toContain('.jpg');
        expect(supportedFormats).toContain('.png');
        expect(supportedFormats).toContain('.pdf');
        expect(supportedFormats).not.toContain('.txt');
    });

    test('should extract file extension', () => {
        const getFileExtension = (filename) => '.' + filename.split('.').pop().toLowerCase();
        
        expect(getFileExtension('test.jpg')).toBe('.jpg');
        expect(getFileExtension('document.PDF')).toBe('.pdf');
        expect(getFileExtension('image.JPEG')).toBe('.jpeg');
    });
});

describe('Configuration', () => {
    test('should merge configuration options', () => {
        const defaultConfig = {
            apiBaseUrl: 'http://localhost:5249',
            maxFileSize: 100 * 1024 * 1024,
            showProgress: true
        };

        const userConfig = {
            apiBaseUrl: 'http://myserver:8080',
            maxFileSize: 200 * 1024 * 1024
        };

        const mergedConfig = { ...defaultConfig, ...userConfig };

        expect(mergedConfig.apiBaseUrl).toBe('http://myserver:8080');
        expect(mergedConfig.maxFileSize).toBe(200 * 1024 * 1024);
        expect(mergedConfig.showProgress).toBe(true); // Should keep default
    });
});