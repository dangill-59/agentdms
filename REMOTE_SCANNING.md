# Remote Scanning Guide

This document explains the different approaches and infrastructure needed for scanning from remote computers with AgentDMS.

## Overview

Scanning from remote computers presents unique challenges because scanners are typically connected via USB to a specific computer and require local drivers. AgentDMS provides several approaches to handle remote scanning scenarios.

## Scanning Architecture Options

### Option 1: Server-Side Scanning (Recommended)

**Setup**: Connect the scanner to the computer running AgentDMS server.

**How it works**:
- Scanner is physically connected to the server machine
- Remote clients access the AgentDMS web interface
- Scan operations are initiated from remote browsers but executed on the server
- Scanned files are served back to remote clients via HTTP

**Advantages**:
- ✅ Simple setup - no additional software needed on client machines
- ✅ Centralized scanner management
- ✅ Works with any device that can access the web interface
- ✅ Supports all scanner features through TWAIN/WIA drivers

**Requirements**:
- Scanner connected to server machine
- Scanner drivers installed on server
- Network access between clients and server

**Example Setup**:
```
Scanner (USB) ──→ Server Computer (AgentDMS) ──→ Network ──→ Remote Client (Browser)
```

### Option 2: Network Scanner Access

**Setup**: Use network-capable scanners that can be accessed directly over the network.

**How it works**:
- Scanner connects to network via Ethernet or Wi-Fi
- AgentDMS connects to scanner using network protocols
- Multiple clients can potentially access the same scanner

**Advantages**:
- ✅ Scanner can be shared across multiple computers
- ✅ No physical connection to server required
- ✅ Flexible scanner placement

**Requirements**:
- Network-capable scanner
- Scanner configured for network access
- AgentDMS enhanced with network scanner support (future enhancement)

**Current Status**: 🚧 **Not yet implemented** - requires additional development

### Option 3: Client-Side Scanning with Local Agent

**Setup**: Install a local agent on each client computer with connected scanners.

**How it works**:
- Local agent software on client communicates with local scanner
- Agent exposes scanner via local web service or API
- AgentDMS web interface communicates with local agent

**Advantages**:
- ✅ Scanner stays connected to user's computer
- ✅ Multiple users can scan from their own devices

**Disadvantages**:
- ❌ Requires additional software installation on each client
- ❌ Complex setup and maintenance
- ❌ Potential security and firewall issues

**Current Status**: 🚧 **Not implemented** - complex architectural change required

## Recommended Setup Guide

### For Most Users: Server-Side Scanning

1. **Connect Scanner to Server**:
   - Physically connect scanner to the computer running AgentDMS
   - Install scanner drivers on the server machine
   - Test scanner works locally first

2. **Configure AgentDMS for Remote Access**:
   - Ensure AgentDMS is configured for remote access (see [REMOTE_ACCESS.md](REMOTE_ACCESS.md))
   - Test web interface accessibility from remote computers

3. **Access Scanner Remotely**:
   - Open web browser on remote computer
   - Navigate to `http://[SERVER-IP]:5249`
   - Use the Scanner tab to select and configure scanner
   - Initiate scans from the web interface

### For Network Scanners (Future Enhancement)

Network scanner support is planned for future releases. This will enable:
- Direct connection to network-capable scanners
- Support for common network scanning protocols
- Multi-user scanner sharing

## Troubleshooting Remote Scanning

### Common Issues and Solutions

#### "No scanners found"
- **Check**: Is scanner connected to the server machine (not client)?
- **Check**: Are scanner drivers installed on the server?
- **Check**: Is scanner powered on and ready?
- **Try**: Refresh scanners in the web interface

#### "Scanning failed" or "Scanner not responding"
- **Check**: Scanner is not in use by another application
- **Check**: USB cable is properly connected
- **Try**: Restart scanner and refresh the page
- **Try**: Test scanner with manufacturer's software first

#### "Cannot access scanner interface"
- **Note**: Scanner UI/configuration dialogs appear on the server machine
- **Workaround**: Use remote desktop to access server for scanner configuration
- **Alternative**: Use web interface scanner settings instead of native UI

#### Cross-Platform Limitations
- **Windows**: Full TWAIN and WIA support
- **Linux**: Limited scanner support (SANE drivers where available)
- **macOS**: Mock scanners only (TWAIN support possible in future)

### Diagnostic Tools

AgentDMS provides diagnostic tools to help troubleshoot scanning issues:

1. **Scanner Information**: View detected scanners and their capabilities
2. **Platform Diagnostics**: Check TWAIN/WIA driver status and registry entries
3. **Test Scanning**: Use mock scanners to test the scanning workflow

Access these tools through the web interface Scanner tab.

## Security Considerations

### For Server-Side Scanning
- Scanner access is controlled by network access to AgentDMS
- No authentication is currently implemented - consider network-level security
- Scanned files are stored on server and served via HTTP

### For Network Scanners (Future)
- Network scanners may have their own security protocols
- Consider scanner-specific authentication and encryption
- Monitor network traffic for scanner communications

## Platform Support

| Platform | TWAIN Support | WIA Support | SANE Support | Network Scanners |
|----------|---------------|-------------|--------------|------------------|
| Windows  | ✅ Full       | ✅ Full     | ❌ No        | 🚧 Planned       |
| Linux    | ❌ Limited    | ❌ No       | ✅ Yes       | 🚧 Planned       |
| macOS    | 🚧 Possible   | ❌ No       | ❌ No        | 🚧 Planned       |

## API Support for Remote Scanning

AgentDMS provides REST API endpoints that can be used for integrating scanning into other applications:

### Scanner Discovery
```http
GET /api/imageprocessing/scanners
```

### Scan Document
```http
POST /api/imageprocessing/scan
Content-Type: application/json

{
  "scannerDeviceId": "scanner_id",
  "resolution": 300,
  "colorMode": 2,
  "format": 0,
  "showUserInterface": false,
  "autoProcess": true
}
```

### Get Scanner Capabilities
```http
GET /api/imageprocessing/scanners/capabilities
```

See the API documentation at `/swagger` for complete details.

## Future Enhancements

### Planned Features
- **Network Scanner Support**: Direct integration with network-capable scanners
- **Authentication System**: User-based access control for scanning
- **Scanner Sharing**: Multi-user scanner management and queuing
- **Mobile Scanning**: Integration with mobile device cameras as scanners
- **Cloud Scanning**: Integration with cloud-based scanning services

### Contribution Opportunities
- Cross-platform scanner driver improvements
- Network scanner protocol implementations
- Authentication and authorization systems
- Scanner management and sharing features

---

For technical support or feature requests, please open an issue on the GitHub repository.