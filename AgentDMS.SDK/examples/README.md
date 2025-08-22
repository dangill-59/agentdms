# AgentDMS SDK Integration Examples

This directory contains examples of how to integrate the AgentDMS SDK into different types of projects.

## Examples

- **basic-integration/** - Simple HTML/JavaScript integration
- **electron-app/** - Full Electron application example
- **react-app/** - React component integration
- **node-server/** - Node.js server-side usage

## Quick Start

Each example includes its own README with specific setup instructions. Generally:

1. Navigate to the example directory
2. Run `npm install`
3. Follow the example-specific setup instructions
4. Run the example application

## Configuration

All examples require a running AgentDMS server. Update the configuration in each example to point to your server:

```javascript
const config = {
    apiBaseUrl: 'http://your-agentdms-server:5249'
};
```