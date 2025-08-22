# Scanner Remote Access Guide

This document explains how scanners work when accessing AgentDMS from remote machines and provides solutions for different scenarios.

## Understanding Scanner Connectivity

### How Scanner Detection Works

AgentDMS uses platform-specific scanner APIs to detect and interact with scanners:

- **Windows**: TWAIN (Technology Without An Interesting Name) API
- **Linux**: SANE (Scanner Access Now Easy) API  
- **macOS**: Image Capture framework

These APIs require **direct hardware access** to the machine where the scanner drivers are installed.

### Server-Side vs Client-Side Scanning

**Important**: Scanner detection and operation happen on the **server machine** (where AgentDMS is running), not on the client machine (where you're browsing from).

```
┌─────────────────┐     Network     ┌─────────────────┐
│  Your Computer  │ ◄──────────────► │ AgentDMS Server │
│   (Client)      │   HTTP/HTTPS    │                 │
│                 │                 │   [Scanner]     │
│  🖥️ Browser     │                 │   📄 Scanner    │
│                 │                 │   🖨️ Scanner    │
└─────────────────┘                 └─────────────────┘
```

## Remote Access Scenarios

### Scenario 1: Accessing from Another Computer on Same Network

**Situation**: You're using AgentDMS from Computer A, but AgentDMS server is running on Computer B.

**Scanner Location**: Scanners must be connected to Computer B (the server).

**What You See**: Only scanners connected to Computer B will be detected.

**Solution**: 
- Connect your scanners to Computer B, OR
- Install AgentDMS on Computer A (your machine)

### Scenario 2: Accessing Over Internet

**Situation**: You're accessing AgentDMS over the internet from a remote location.

**Scanner Location**: Scanners must be connected to the machine hosting AgentDMS.

**What You See**: Only scanners at the server location will be available.

**Solutions**:
- Use network-enabled scanners at the server location
- Use remote desktop to access the server machine directly
- Consider a different architecture for your scanning workflow

### Scenario 3: Development/Testing Environment

**Situation**: You're testing or developing with AgentDMS.

**What You See**: Mock/test scanners that simulate real scanning functionality.

**Features**: Mock scanners work from any location and generate sample scanned documents.

## Technical Limitations

### Why Browser-Based Scanning Is Limited

1. **Security Restrictions**: Web browsers cannot directly access local hardware for security reasons
2. **Driver Requirements**: Scanner drivers must be installed on the machine running the scanning software
3. **API Limitations**: TWAIN/SANE/Image Capture APIs require local system access

### Why AgentDMS Scans Server-Side

1. **Reliability**: Server-side scanning provides consistent behavior across different client machines
2. **Security**: Centralized scanning reduces security risks
3. **Processing**: Server can handle resource-intensive scanning and processing operations

## Recommended Solutions

### Option 1: Install AgentDMS Locally

**Best For**: Single-user scenarios or when you have full control over your machine.

```bash
# Install AgentDMS on the machine with your scanners
dotnet run --project AgentDMS.Web
# Access at http://localhost:5249
```

**Advantages**:
- Full access to local scanners
- No network latency
- Works offline

### Option 2: Central Server with Network Scanners

**Best For**: Multi-user environments or businesses.

**Setup**:
1. Use network-enabled scanners (WiFi, Ethernet)
2. Configure scanners on the server machine
3. Install scanner drivers on the server
4. Users access AgentDMS remotely

**Advantages**:
- Centralized management
- Shared scanning resources
- Consistent user experience

### Option 3: Remote Desktop Solution

**Best For**: Occasional remote access needs.

**Setup**:
1. Use Remote Desktop, VNC, or TeamViewer
2. Connect to the machine with AgentDMS and scanners
3. Use AgentDMS through the remote desktop session

**Advantages**:
- Full access to local resources
- No architecture changes needed

### Option 4: Hybrid Approach

**Best For**: Mixed environments with local and remote users.

**Setup**:
1. Run AgentDMS on a server with some scanners
2. Allow local installations for remote users
3. Use shared storage for scanned documents

## Troubleshooting

### "No Scanners Found" Message

If you see this message:

1. **Check Connection**: Verify scanners are connected to the correct machine
2. **Driver Installation**: Ensure scanner drivers are installed on the server
3. **API Endpoint**: Use `/api/ImageProcessing/scanners/connectivity-info` for diagnostic information
4. **Platform Support**: Verify your platform supports scanner APIs

### Remote Access Detection

AgentDMS can detect when you're accessing from a remote machine and will provide appropriate guidance:

- Check for IP address patterns (local vs remote)
- Display context-specific help messages
- Explain scanner connectivity requirements

### Testing Scanner Functionality

Use the mock scanners to test the scanning workflow:

1. Mock scanners are always available
2. They generate sample scanned documents  
3. All scanning features work with mock scanners
4. Perfect for testing the complete workflow

## API Reference

### Get Scanner Connectivity Information

```
GET /api/ImageProcessing/scanners/connectivity-info
```

Returns detailed information about scanner connectivity requirements and limitations.

### Get Available Scanners

```
GET /api/ImageProcessing/scanners
```

Returns list of scanners detected on the server machine.

### Get Scanner Capabilities

```
GET /api/ImageProcessing/scanners/capabilities
```

Returns platform-specific scanning capabilities and supported features.

## Best Practices

### For System Administrators

1. **Document Scanner Locations**: Clearly document which scanners are available on which machines
2. **Network Configuration**: Ensure proper network access between client and server machines
3. **Driver Maintenance**: Keep scanner drivers updated on server machines
4. **User Training**: Educate users about scanner connectivity requirements

### For Users

1. **Understand the Architecture**: Know where AgentDMS is running vs where you're accessing it from
2. **Check Scanner Status**: Use the connectivity info endpoint to understand your setup
3. **Use Mock Scanners**: Test workflows with mock scanners before using real ones
4. **Plan for Scanning**: Consider scanner location when planning your workflow

## Conclusion

Scanner remote access in AgentDMS is designed around server-side scanning for security and reliability. While this creates some limitations for remote access scenarios, there are several solutions available depending on your specific needs and environment.

The key is understanding that scanners must be connected to the machine running AgentDMS, not the machine you're browsing from.