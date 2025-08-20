# TWAIN Scanner Detection Troubleshooting

If your TWAIN scanner is not being detected by AgentDMS, this guide will help you troubleshoot the issue.

## Problem Description

The most common issue is that a scanner driver file exists (e.g., `C:\Windows\twain_64\sample2\TWAINDS_Sample64.ds`) but the scanner is not being detected by the application.

## Enhanced Detection Features

AgentDMS now includes enhanced TWAIN scanner detection with multiple fallback methods:

### 1. Standard TWAIN API Detection
- Uses the Windows TWAIN Source Manager to enumerate registered scanners
- Enhanced with detailed logging to show what scanners are found

### 2. Directory Scanning Fallback
- Scans standard TWAIN directories for `.ds` files:
  - `C:\Windows\twain_32`
  - `C:\Windows\twain_64`
  - `C:\TWAIN_32`
  - `C:\TWAIN_64`
- Finds scanners that may not be properly registered

### 3. Registry Scanning Fallback
- Scans Windows registry for TWAIN data source entries
- Checks both 32-bit and 64-bit registry locations:
  - `HKLM\SOFTWARE\TWAIN_32\`
  - `HKLM\SOFTWARE\WOW6432Node\TWAIN_32\`
  - `HKLM\SOFTWARE\TWAIN\`
  - `HKLM\SOFTWARE\WOW6432Node\TWAIN\`

## Using Diagnostic Information

To troubleshoot scanner detection issues, you can use the new diagnostic functionality:

```csharp
using var scannerService = new ScannerService(logger);
var diagnostics = await scannerService.GetDiagnosticInfoAsync();

// Convert to JSON for easy reading
var json = JsonSerializer.Serialize(diagnostics, new JsonSerializerOptions 
{ 
    WriteIndented = true 
});
Console.WriteLine(json);
```

### Diagnostic Information Includes:

1. **Platform Information**
   - Operating system version
   - Whether running on Windows
   - Real scanner support availability

2. **TWAIN Directory Analysis**
   - Which TWAIN directories exist
   - Number of `.ds` files in each directory
   - List of found `.ds` files

3. **Registry Analysis**
   - Which TWAIN registry keys exist
   - Number of registered scanner entries
   - List of registered scanner names

4. **TWAIN Session Status**
   - Whether TWAIN session can be initialized
   - Number of scanners found via TWAIN API
   - Details of each detected scanner

## Common Issues and Solutions

### Issue: Scanner Driver Exists but Not Detected

**Symptoms:**
- `.ds` file exists in a TWAIN directory
- Scanner not showing up in application

**Solution:**
1. Check diagnostic output for directory scanning results
2. Verify the scanner appears in the diagnostic information
3. If found by directory scan, the scanner will be available with detection method "Directory Scan"

### Issue: Scanner Registered but TWAIN API Fails

**Symptoms:**
- Scanner appears in Windows Device Manager
- Registry entries exist for the scanner
- TWAIN session initialization fails

**Solution:**
1. Check diagnostic output for TWAIN session errors
2. Verify registry scanning found the scanner
3. Scanner may still be usable via registry detection method

### Issue: 32-bit vs 64-bit Compatibility

**Symptoms:**
- Scanner works with some applications but not others
- Only appears in 32-bit or 64-bit TWAIN directories

**Solution:**
1. Check both `twain_32` and `twain_64` directories in diagnostics
2. Ensure application architecture matches scanner driver architecture
3. Consider installing both 32-bit and 64-bit drivers if available

## Example Diagnostic Output

```json
{
  "Platform": "Microsoft Windows NT 10.0.19044.0",
  "IsWindows": true,
  "RealScannerSupport": true,
  "Timestamp": "2024-01-20T10:30:00Z",
  "TwainDirectories": [
    {
      "Directory": "C:\\Windows\\twain_64",
      "Exists": true,
      "DsFileCount": 1,
      "DsFiles": ["C:\\Windows\\twain_64\\sample2\\TWAINDS_Sample64.ds"]
    }
  ],
  "RegistryKeys": [
    {
      "RegistryPath": "HKLM\\SOFTWARE\\TWAIN_32\\",
      "Exists": true,
      "SubKeyCount": 1,
      "SubKeys": ["Sample Scanner Driver"]
    }
  ],
  "TwainSession": {
    "Success": true,
    "SourceCount": 1,
    "Sources": [
      {
        "Name": "Sample Scanner",
        "Manufacturer": "Sample Corp",
        "ProductFamily": "Sample Scanner Family",
        "Version": "1.0"
      }
    ]
  }
}
```

## Getting Help

If you're still having issues after checking the diagnostic information:

1. **Check the logs** - Enable detailed logging to see what the scanner service is doing
2. **Verify scanner installation** - Ensure the scanner works with other TWAIN applications
3. **Check Windows compatibility** - Some older scanners may not work with newer Windows versions
4. **Review diagnostic output** - Look for error messages in the TWAIN session section

## Code Examples

### Basic Scanner Detection with Logging

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
    builder.AddConsole().SetMinimumLevel(LogLevel.Information));
var logger = loggerFactory.CreateLogger<ScannerService>();

using var scannerService = new ScannerService(logger);
var scanners = await scannerService.GetAvailableScannersAsync();

foreach (var scanner in scanners)
{
    Console.WriteLine($"Found: {scanner.Name} by {scanner.Manufacturer}");
    
    if (scanner.Capabilities.ContainsKey("DetectionMethod"))
    {
        Console.WriteLine($"Detection Method: {scanner.Capabilities["DetectionMethod"]}");
    }
}
```

### Running Full Diagnostics

```csharp
using var scannerService = new ScannerService();
var diagnostics = await scannerService.GetDiagnosticInfoAsync();

// Check if TWAIN is working
if (diagnostics.ContainsKey("TwainSession"))
{
    var twainSession = diagnostics["TwainSession"];
    // Analyze TWAIN session results
}

// Check directory scanning results
if (diagnostics.ContainsKey("TwainDirectories"))
{
    var directories = diagnostics["TwainDirectories"];
    // Analyze directory scan results
}
```