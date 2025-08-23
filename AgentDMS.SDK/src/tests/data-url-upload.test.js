/**
 * Test for data URL upload functionality
 * Tests the fix for the issue where uploadFile receives data URLs instead of file paths
 */

const { AgentDMSAPI } = require('../api/agentdms-api');
const fs = require('fs');
const os = require('os');
const path = require('path');

// Mock axios to avoid actual network calls
jest.mock('axios', () => {
  return {
    create: jest.fn(() => ({
      post: jest.fn().mockResolvedValue({
        data: { success: true, message: 'Upload successful' }
      })
    }))
  };
});

describe('AgentDMS API Data URL Upload', () => {
  let api;

  beforeEach(() => {
    api = new AgentDMSAPI('http://localhost:5249');
  });

  describe('uploadFile with data URL', () => {
    test('should handle valid PNG data URL', async () => {
      // Create a simple 1x1 PNG data URL (smallest valid PNG)
      const pngDataUrl = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==';
      
      const result = await api.uploadFile(pngDataUrl, { thumbnailSize: 200 });
      
      expect(result).toEqual({ success: true, message: 'Upload successful' });
    });

    test('should handle valid JPEG data URL', async () => {
      // Create a simple JPEG data URL
      const jpegDataUrl = 'data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDADIiJSwlHzIsKSw4NTI7S31RS0VFS5ltc1p9tZ++u7Sh/2wBDTI7SDVATkpNS5JRS0hVWUtES1VPTUtPT0tVTU/+wgARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAX/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAGaAf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAQUCf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Bf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Bf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEABj8Cf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAT8hf//aAAwDAQACAAMAAAAQn//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Qf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Qf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAT8Qf//Z';
      
      const result = await api.uploadFile(jpegDataUrl);
      
      expect(result).toEqual({ success: true, message: 'Upload successful' });
    });

    test('should handle regular file path (backward compatibility)', async () => {
      // Create a temporary test file
      const tempDir = os.tmpdir();
      const testFilePath = path.join(tempDir, 'test_file.txt');
      fs.writeFileSync(testFilePath, 'test content');

      try {
        const result = await api.uploadFile(testFilePath);
        expect(result).toEqual({ success: true, message: 'Upload successful' });
      } finally {
        // Clean up
        if (fs.existsSync(testFilePath)) {
          fs.unlinkSync(testFilePath);
        }
      }
    });

    test('should reject invalid data URL', async () => {
      const invalidDataUrl = 'data:invalid';
      
      await expect(api.uploadFile(invalidDataUrl)).rejects.toThrow(
        'Failed to upload file: Failed to create temporary file from data URL: Invalid data URL format'
      );
    });

    test('should reject non-existent file path', async () => {
      const nonExistentPath = '/path/that/does/not/exist.txt';
      
      await expect(api.uploadFile(nonExistentPath)).rejects.toThrow(
        'Failed to upload file: File not found:'
      );
    });
  });

  describe('createTempFileFromDataUrl', () => {
    test('should create temporary file from PNG data URL', async () => {
      const pngDataUrl = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8/5+hHgAHggJ/PchI7wAAAABJRU5ErkJggg==';
      
      const tempFilePath = await api.createTempFileFromDataUrl(pngDataUrl);
      
      // Verify file was created
      expect(fs.existsSync(tempFilePath)).toBe(true);
      expect(tempFilePath.endsWith('.png')).toBe(true);
      
      // Verify file content
      const buffer = fs.readFileSync(tempFilePath);
      expect(buffer.length).toBeGreaterThan(0);
      
      // Clean up
      fs.unlinkSync(tempFilePath);
    });

    test('should create temporary file from JPEG data URL', async () => {
      const jpegDataUrl = 'data:image/jpeg;base64,/9j/4AAQSkZJRgABAQEAYABgAAD/2wBDADIiJSwlHzIsKSw4NTI7S31RS0VFS5ltc1p9tZ++u7Sh/2wBDTI7SDVATkpNS5JRS0hVWUtES1VPTUtPT0tVTU/+wgARCAABAAEDASIAAhEBAxEB/8QAFQABAQAAAAAAAAAAAAAAAAAAAAX/xAAUAQEAAAAAAAAAAAAAAAAAAAAA/9oADAMBAAIQAxAAAAGaAf/EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAQUCf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Bf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Bf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEABj8Cf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAT8hf//aAAwDAQACAAMAAAAQn//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQMBAT8Qf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQIBAT8Qf//EABQQAQAAAAAAAAAAAAAAAAAAAAD/2gAIAQEAAT8Qf//Z';
      
      const tempFilePath = await api.createTempFileFromDataUrl(jpegDataUrl);
      
      // Verify file was created
      expect(fs.existsSync(tempFilePath)).toBe(true);
      expect(tempFilePath.endsWith('.jpg')).toBe(true);
      
      // Clean up
      fs.unlinkSync(tempFilePath);
    });

    test('should handle unknown MIME types with .bin extension', async () => {
      const unknownDataUrl = 'data:application/unknown;base64,dGVzdCBkYXRh'; // "test data" in base64
      
      const tempFilePath = await api.createTempFileFromDataUrl(unknownDataUrl);
      
      // Verify file was created
      expect(fs.existsSync(tempFilePath)).toBe(true);
      expect(tempFilePath.endsWith('.bin')).toBe(true);
      
      // Verify content
      const content = fs.readFileSync(tempFilePath, 'utf8');
      expect(content).toBe('test data');
      
      // Clean up
      fs.unlinkSync(tempFilePath);
    });

    test('should reject invalid data URL format', async () => {
      const invalidDataUrl = 'invalid-data-url';
      
      await expect(api.createTempFileFromDataUrl(invalidDataUrl)).rejects.toThrow(
        'Failed to create temporary file from data URL: Invalid data URL format'
      );
    });
  });
});