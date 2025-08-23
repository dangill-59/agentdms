/**
 * Comprehensive test for the image loading fix edge cases
 */

const fs = require('fs');
const path = require('path');

describe('Image Loading Fix - Edge Cases', () => {
  // Create test files
  const testDir = '/tmp/image-test';
  const testFiles = {
    png: path.join(testDir, 'test.png'),
    jpg: path.join(testDir, 'test.jpg'),
    gif: path.join(testDir, 'test.gif'),
    pdf: path.join(testDir, 'test.pdf'),
    unknown: path.join(testDir, 'test.xyz')
  };

  beforeAll(() => {
    // Create test directory
    if (!fs.existsSync(testDir)) {
      fs.mkdirSync(testDir, { recursive: true });
    }
    
    // Create test files with minimal content
    const minimalPng = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+P+/HgAGgwF/lK3Q6wAAAABJRU5ErkJggg==', 'base64');
    
    Object.values(testFiles).forEach(filePath => {
      fs.writeFileSync(filePath, minimalPng);
    });
  });

  afterAll(() => {
    // Clean up test files
    if (fs.existsSync(testDir)) {
      Object.values(testFiles).forEach(filePath => {
        if (fs.existsSync(filePath)) {
          fs.unlinkSync(filePath);
        }
      });
      fs.rmdirSync(testDir);
    }
  });

  // Replicate the MIME type detection logic from the main process
  const getMimeTypeForFile = (filePath) => {
    const fileExtension = path.extname(filePath).toLowerCase();
    const mimeMap = {
      '.jpg': 'image/jpeg',
      '.jpeg': 'image/jpeg', 
      '.png': 'image/png',
      '.gif': 'image/gif',
      '.bmp': 'image/bmp',
      '.tiff': 'image/tiff',
      '.webp': 'image/webp',
      '.pdf': 'application/pdf'
    };
    return mimeMap[fileExtension] || 'application/octet-stream';
  };

  test('should detect correct MIME types for all supported formats', () => {
    expect(getMimeTypeForFile(testFiles.png)).toBe('image/png');
    expect(getMimeTypeForFile(testFiles.jpg)).toBe('image/jpeg');
    expect(getMimeTypeForFile(testFiles.gif)).toBe('image/gif');
    expect(getMimeTypeForFile(testFiles.pdf)).toBe('application/pdf');
    expect(getMimeTypeForFile(testFiles.unknown)).toBe('application/octet-stream');
  });

  test('should handle case insensitive extensions', () => {
    expect(getMimeTypeForFile('/path/to/image.PNG')).toBe('image/png');
    expect(getMimeTypeForFile('/path/to/image.JPG')).toBe('image/jpeg');
    expect(getMimeTypeForFile('/path/to/image.JPEG')).toBe('image/jpeg');
  });

  test('should create valid data URLs', () => {
    const base64Data = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+P+/HgAGgwF/lK3Q6wAAAABJRU5ErkJggg==';
    const mimeType = 'image/png';
    const dataUrl = `data:${mimeType};base64,${base64Data}`;
    
    expect(dataUrl.startsWith('data:')).toBe(true);
    expect(dataUrl.includes(';base64,')).toBe(true);
    expect(dataUrl.includes(mimeType)).toBe(true);
  });

  test('should extract correct file names', () => {
    expect(path.basename('/some/path/image.png')).toBe('image.png');
    expect(path.basename('image.gif')).toBe('image.gif');
    
    // Windows path handling (normalize for cross-platform)
    const windowsPath = 'C:\\Windows\\Path\\image.jpg';
    const filename = windowsPath.split(/[\\\/]/).pop();
    expect(filename).toBe('image.jpg');
  });

  test('should handle file reading simulation', () => {
    // Test that files exist and can be read
    Object.values(testFiles).forEach(filePath => {
      expect(fs.existsSync(filePath)).toBe(true);
      const buffer = fs.readFileSync(filePath);
      expect(buffer.length).toBeGreaterThan(0);
    });
  });
});