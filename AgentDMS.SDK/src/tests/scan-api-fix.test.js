/**
 * Test for scan API field mapping fix
 * Validates that the SDK sends correct field names to match ScanRequest.cs model
 */

// Mock axios before importing AgentDMSAPI
const mockAxios = {
  create: jest.fn(),
  post: jest.fn()
};

// Make create return the mock instance
mockAxios.create.mockReturnValue(mockAxios);

jest.mock('axios', () => mockAxios);

const { AgentDMSAPI } = require('../api/agentdms-api');

describe('Scan API Field Mapping Fix', () => {
  let api;

  beforeEach(() => {
    api = new AgentDMSAPI();
    mockAxios.post.mockClear();
  });

  test('should map scan options to correct field names for ScanRequest.cs', async () => {
    // Arrange
    const scanOptions = {
      scannerName: 'Test Scanner',
      resolution: 600,
      colorMode: 'Grayscale',
      paperSize: 'A4',
      showUserInterface: true,
      duplex: false
    };

    const expectedResponse = { success: true, scannedFilePath: '/test/path.png' };
    mockAxios.post.mockResolvedValue({ data: expectedResponse });

    // Act
    await api.scanDocument(scanOptions);

    // Assert
    expect(mockAxios.post).toHaveBeenCalledTimes(1);
    const [url, requestData] = mockAxios.post.mock.calls[0];
    
    expect(url).toBe('http://localhost:5249/api/ImageProcessing/scan');
    expect(requestData).toEqual({
      scannerDeviceId: 'Test Scanner', // scannerName -> scannerDeviceId
      resolution: 600,
      colorMode: 1, // 'Grayscale' -> 1 (enum value)
      format: 0, // Default PNG
      showUserInterface: true,
      autoProcess: true // Default value
    });
  });

  test('should map color mode strings to correct enum values', async () => {
    const mockResponse = { data: { success: true } };
    mockAxios.post.mockResolvedValue(mockResponse);

    // Test Black and White
    await api.scanDocument({ colorMode: 'BlackAndWhite' });
    expect(mockAxios.post.mock.calls[0][1].colorMode).toBe(0);

    // Test Grayscale 
    await api.scanDocument({ colorMode: 'Grayscale' });
    expect(mockAxios.post.mock.calls[1][1].colorMode).toBe(1);

    // Test Color
    await api.scanDocument({ colorMode: 'Color' });
    expect(mockAxios.post.mock.calls[2][1].colorMode).toBe(2);

    // Test alternative formats
    await api.scanDocument({ colorMode: 'bw' });
    expect(mockAxios.post.mock.calls[3][1].colorMode).toBe(0);

    await api.scanDocument({ colorMode: 'gray' });
    expect(mockAxios.post.mock.calls[4][1].colorMode).toBe(1);
  });

  test('should handle missing or undefined options gracefully', async () => {
    const mockResponse = { data: { success: true } };
    mockAxios.post.mockResolvedValue(mockResponse);

    // Test with minimal options
    await api.scanDocument({});

    const [, requestData] = mockAxios.post.mock.calls[0];
    expect(requestData).toEqual({
      scannerDeviceId: '',
      resolution: 300, // Default
      colorMode: 2, // Default Color
      format: 0, // Default PNG
      showUserInterface: false, // Default
      autoProcess: true // Default
    });
  });

  test('should preserve custom format and autoProcess values', async () => {
    const mockResponse = { data: { success: true } };
    mockAxios.post.mockResolvedValue(mockResponse);

    await api.scanDocument({
      format: 1, // JPEG
      autoProcess: false
    });

    const [, requestData] = mockAxios.post.mock.calls[0];
    expect(requestData.format).toBe(1);
    expect(requestData.autoProcess).toBe(false);
  });

  test('should handle both scannerName and scannerDeviceId options', async () => {
    const mockResponse = { data: { success: true } };
    mockAxios.post.mockResolvedValue(mockResponse);

    // Test with scannerDeviceId
    await api.scanDocument({ scannerDeviceId: 'Device123' });
    expect(mockAxios.post.mock.calls[0][1].scannerDeviceId).toBe('Device123');

    // Test with scannerName (should map to scannerDeviceId)
    await api.scanDocument({ scannerName: 'Scanner456' });
    expect(mockAxios.post.mock.calls[1][1].scannerDeviceId).toBe('Scanner456');
  });

  test('should throw error with descriptive message on API failure', async () => {
    const errorResponse = {
      response: {
        status: 400,
        data: { error: 'Invalid scanner configuration' }
      },
      message: 'Request failed with status code 400'
    };
    
    mockAxios.post.mockRejectedValue(errorResponse);

    await expect(api.scanDocument({ scannerName: 'Invalid' }))
      .rejects.toThrow('Failed to scan document: Request failed with status code 400');
  });
});

console.log('✓ Scan API field mapping tests validate the fix for 400 status code errors');