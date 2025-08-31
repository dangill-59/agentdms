# AgentDMS Configurable Storage Documentation

## Overview

AgentDMS now supports configurable output destinations with three storage providers:
- **Local** - File system storage (default)
- **AWS** - Amazon S3 cloud storage (placeholder implementation)
- **Azure** - Azure Blob Storage (placeholder implementation)

## Configuration

### 1. Web Application (appsettings.json)

#### Local Storage (Default)
```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "BaseDirectory": null
    }
  }
}
```

#### AWS S3 Storage
```json
{
  "Storage": {
    "Provider": "AWS",
    "Aws": {
      "BucketName": "my-agentdms-bucket",
      "Region": "us-east-1",
      "AccessKeyId": "",
      "SecretAccessKey": "",
      "SessionToken": ""
    }
  }
}
```

#### Azure Blob Storage
```json
{
  "Storage": {
    "Provider": "Azure",
    "Azure": {
      "AccountName": "myagentdmsaccount",
      "ContainerName": "agentdms",
      "AccountKey": "",
      "ConnectionString": ""
    }
  }
}
```

### 2. CLI Application

#### Local Storage (Default)
```bash
AgentDMS.UI.exe --process image.jpg --output "C:\MyOutput"
```

#### AWS S3 Storage
```bash
AgentDMS.UI.exe --process image.jpg --storage-provider AWS --aws-bucket my-bucket --aws-region us-west-2
```

#### Azure Blob Storage
```bash
AgentDMS.UI.exe --process image.jpg --storage-provider Azure --azure-account myaccount --azure-container mycontainer
```

### 3. Environment Variables

You can also configure storage using environment variables:

```bash
# AWS Configuration
export AWS_ACCESS_KEY_ID=your_access_key
export AWS_SECRET_ACCESS_KEY=your_secret_key
export AWS_SESSION_TOKEN=your_session_token  # Optional for temporary credentials

# Azure Configuration
export AZURE_STORAGE_KEY=your_storage_key
```

## Implementation Status

### ✅ Local Storage (Fully Implemented)
- Saves files to local file system
- Configurable base directory
- Defaults to system temp directory + "AgentDMS_Output"
- Supports all file operations
- Web application serves files via static file middleware

### 🚧 AWS S3 Storage (Placeholder)
- Interface defined and ready for implementation
- Returns proper S3 URLs for file access
- Throws `NotImplementedException` with helpful messages
- Ready for AWS SDK integration

### 🚧 Azure Blob Storage (Placeholder)
- Interface defined and ready for implementation
- Returns proper Azure Blob URLs for file access
- Throws `NotImplementedException` with helpful messages
- Ready for Azure Storage SDK integration

## Usage Examples

### Processing an Image with Custom Local Directory
```bash
# CLI
AgentDMS.UI.exe --process photo.jpg --output "/home/user/processed"

# Results will be saved to /home/user/processed/
```

### Web Application with Custom Storage
```json
{
  "Storage": {
    "Provider": "Local",
    "Local": {
      "BaseDirectory": "/var/agentdms/storage"
    }
  }
}
```

### Testing Storage Configuration
The application includes comprehensive tests for storage providers:
```bash
dotnet test --filter "StorageProviderTests"
```

## Migration from Previous Version

The changes are **backward compatible**:
- Existing code continues to work unchanged
- Default behavior remains the same (local temp directory)
- Legacy constructors preserved for `ImageProcessingService`

## Future Enhancements

To fully implement cloud storage providers:

1. **AWS S3**: Add AWS SDK packages and implement actual S3 operations
2. **Azure Blob**: Add Azure Storage packages and implement blob operations
3. **Hybrid Storage**: Support different providers for different file types
4. **Caching**: Add local caching for cloud-stored files
5. **Security**: Implement proper authentication and authorization

## Security Considerations

- Store credentials securely (environment variables, Azure Key Vault, AWS Secrets Manager)
- Use IAM roles and managed identities when possible
- Implement proper access controls for cloud storage
- Consider encryption at rest and in transit