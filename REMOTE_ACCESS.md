# Remote Access Configuration

This document explains how to access AgentDMS from another computer on your network.

## Quick Start

For scanner-specific remote access information, see **[SCANNER_REMOTE_ACCESS.md](SCANNER_REMOTE_ACCESS.md)** for detailed guidance on using scanners when accessing AgentDMS from remote machines.

## Configuration Changes Made

The application has been configured to allow remote access with the following changes:

### 1. Network Binding
- The application now binds to all network interfaces (`0.0.0.0`) instead of just localhost
- Default URLs: `http://0.0.0.0:5249` and `https://0.0.0.0:7249`

### 2. CORS Configuration
- Cross-Origin Resource Sharing (CORS) has been configured to allow connections from any origin
- SignalR (real-time communication) is specifically configured to accept remote connections

## How to Access from Another Computer

### Step 1: Find the Server's IP Address
On the computer running AgentDMS, find its IP address:
- **Windows**: Open Command Prompt and run `ipconfig`
- **Linux/Mac**: Open Terminal and run `ifconfig` or `ip addr show`
- Look for the IPv4 address (usually something like `192.168.1.100`)

### Step 2: Access from Remote Computer
From another computer on the same network, open a web browser and navigate to:
- **HTTP**: `http://[SERVER-IP]:5249`
- **HTTPS**: `https://[SERVER-IP]:7249`

For example, if the server IP is `192.168.1.100`:
- HTTP: `http://192.168.1.100:5249`
- HTTPS: `https://192.168.1.100:7249`

## Customizing URLs

You can customize the URLs by:

### Option 1: Environment Variable
Set the `ASPNETCORE_URLS` environment variable:
```bash
ASPNETCORE_URLS="http://0.0.0.0:8080;https://0.0.0.0:8443"
```

### Option 2: Command Line
Run with custom URLs:
```bash
dotnet run --urls "http://0.0.0.0:8080;https://0.0.0.0:8443"
```

### Option 3: Configuration File
Edit `appsettings.json` and modify the `Urls` setting:
```json
{
  "Urls": "http://0.0.0.0:8080;https://0.0.0.0:8443"
}
```

## Firewall Considerations

Make sure your firewall allows incoming connections on the ports you're using:
- **Windows**: Windows Defender Firewall may need to allow the application
- **Linux**: Use `ufw` or `iptables` to open the ports
- **Router**: If accessing from outside your local network, configure port forwarding

## Security Notes

⚠️ **Important Security Considerations:**

1. **Local Network Only**: This configuration is intended for use within a trusted local network
2. **No Authentication**: The current setup doesn't include authentication - anyone who can reach the server can use the application
3. **HTTPS Certificates**: For HTTPS access, you may need to configure proper SSL certificates for production use
4. **Production Deployment**: For production environments, consider implementing proper authentication, authorization, and security measures

## Scanner Remote Access

**Important**: When accessing AgentDMS from a remote machine, scanners must be connected to the computer running the AgentDMS server, not your local machine.

### Scanner Connectivity Requirements

- Scanners must be physically connected to the server machine
- Scanner drivers must be installed on the server machine  
- Browser security prevents direct access to scanners on remote client machines

### Solutions for Remote Scanner Access

1. **Connect scanners to the server machine** where AgentDMS is running
2. **Install AgentDMS locally** on the machine with your scanners
3. **Use network-enabled scanners** configured on the server machine
4. **Use remote desktop software** to access the server machine directly

📖 **For detailed scanner remote access guidance, see [SCANNER_REMOTE_ACCESS.md](SCANNER_REMOTE_ACCESS.md)**

## Troubleshooting

### Common Issues:

1. **Connection Refused**: Check that the application is running and firewall allows the ports
2. **HTTPS Certificate Warnings**: In development, browsers may warn about self-signed certificates - this is normal
3. **SignalR Connection Issues**: Ensure WebSocket connections are allowed through firewalls and proxies

### Testing Connectivity:

From the remote computer, test basic connectivity:
```bash
# Test if the port is accessible
telnet [SERVER-IP] 5249

# Or use curl
curl http://[SERVER-IP]:5249
```

## Default Ports

- **HTTP**: 5249
- **HTTPS**: 7249
- **API Documentation**: Available at `/swagger` (development) or `/api-docs` (production)