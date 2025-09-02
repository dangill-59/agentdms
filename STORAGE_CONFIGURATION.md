# AgentDMS Configurable Storage Documentation

## Overview

AgentDMS now supports configurable output destinations with three storage providers:
- **Local** - File system storage (default)
- **AWS** - Amazon S3 cloud storage (placeholder implementation)
- **Azure** - Azure Blob Storage (placeholder implementation)

## Configuration

Storage configuration is managed dynamically through the web interface. The application uses runtime configuration stored in `App_Data/storageconfig.json` which can be updated via the web API, ensuring that changes take effect immediately without requiring application restart.

### Runtime Configuration (Primary)

The storage configuration is managed through the web interface and stored in `App_Data/storageconfig.json`. This configuration takes precedence and is used by the actual storage operations.

#### API Endpoints
- **GET** `/api/storageconfig` - Get current storage configuration
- **POST** `/api/storageconfig` - Update storage configuration
- **POST** `/api/storageconfig/test` - Test storage configuration

### Fallback Configuration (appsettings.json)

The application also reads initial configuration from `appsettings.json`, but this is only used as a fallback when no runtime configuration exists.

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

### Storage Configuration Fix (September 2025)

**Issue Fixed**: Storage settings configured through the web interface were not being used for actual file storage operations.

**Root Cause**: The application had two separate storage configuration systems that didn't communicate:
1. Static configuration from `appsettings.json` (used by storage operations)
2. Runtime configuration from `App_Data/storageconfig.json` (updated by web interface)

**Solution**: Modified the `StorageService` to use runtime configuration from `StorageConfigService`, ensuring that configuration changes made through the web interface are immediately applied to storage operations.

**Verification**: Files are now saved to the correct location specified in the storage configuration.

## AWS S3 Storage (Available)

AWS S3 storage is now fully implemented and ready for production use.

### Configuration

#### Basic Configuration (appsettings.json)
```json
{
  "Storage": {
    "Provider": "AWS",
    "Aws": {
      "BucketName": "my-document-bucket",
      "Region": "us-east-1"
    }
  }
}
```

#### With Explicit Credentials
```json
{
  "Storage": {
    "Provider": "AWS",
    "Aws": {
      "BucketName": "my-document-bucket",
      "Region": "us-east-1",
      "AccessKeyId": "YOUR_ACCESS_KEY",
      "SecretAccessKey": "YOUR_SECRET_KEY"
    }
  }
}
```

#### With Temporary Credentials
```json
{
  "Storage": {
    "Provider": "AWS",
    "Aws": {
      "BucketName": "my-document-bucket",
      "Region": "us-east-1",
      "AccessKeyId": "YOUR_ACCESS_KEY",
      "SecretAccessKey": "YOUR_SECRET_KEY",
      "SessionToken": "YOUR_SESSION_TOKEN"
    }
  }
}
```

### Credential Management

The AWS S3 storage provider supports multiple credential sources (in order of precedence):

1. **Explicit credentials** in configuration (AccessKeyId + SecretAccessKey)
2. **Environment variables**: `AWS_ACCESS_KEY_ID`, `AWS_SECRET_ACCESS_KEY`, `AWS_SESSION_TOKEN`
3. **AWS credentials file** (`~/.aws/credentials`)
4. **IAM roles** (when running on EC2, ECS, Lambda, etc.)
5. **EC2 instance metadata service**

For production deployments, IAM roles are recommended for security.

### Environment Variable Configuration

Set these environment variables instead of storing credentials in configuration files:

```bash
export AWS_ACCESS_KEY_ID=your_access_key
export AWS_SECRET_ACCESS_KEY=your_secret_key
export AWS_DEFAULT_REGION=us-east-1
```

### Features

- ✅ **File Upload**: Support for file path, byte array, and stream uploads
- ✅ **File Download**: Generate S3 URLs for file access
- ✅ **File Management**: Check existence, delete files, list files in directories
- ✅ **Cleanup**: Automatic cleanup of old files based on age
- ✅ **Security**: Server-side encryption (AES256) enabled by default
- ✅ **Error Handling**: Comprehensive error handling and logging
- ✅ **Content Types**: Automatic content type detection based on file extensions

### S3 Bucket Requirements

Your S3 bucket should have:

1. **Proper IAM permissions** for the user/role:
   ```json
   {
     "Version": "2012-10-17",
     "Statement": [
       {
         "Effect": "Allow",
         "Action": [
           "s3:GetObject",
           "s3:PutObject",
           "s3:DeleteObject",
           "s3:ListBucket"
         ],
         "Resource": [
           "arn:aws:s3:::your-bucket-name",
           "arn:aws:s3:::your-bucket-name/*"
         ]
       }
     ]
   }
   ```

2. **CORS configuration** (if accessing from web applications):
   ```json
   [
     {
       "AllowedHeaders": ["*"],
       "AllowedMethods": ["GET", "PUT", "POST", "DELETE"],
       "AllowedOrigins": ["*"],
       "ExposeHeaders": []
     }
   ]
   ```

## Future Enhancements

To further enhance cloud storage providers:

1. **~AWS S3~**: ✅ **Completed** - Full S3 operations implemented
2. **Azure Blob**: Add Azure Storage packages and implement blob operations
3. **Hybrid Storage**: Support different providers for different file types
4. **Caching**: Add local caching for cloud-stored files
5. **Advanced Security**: Implement custom encryption and access controls

## Security Considerations

- Store credentials securely (environment variables, Azure Key Vault, AWS Secrets Manager)
- Use IAM roles and managed identities when possible
- Implement proper access controls for cloud storage
- Consider encryption at rest and in transit
- **AWS S3**: Server-side encryption is enabled by default (AES256)
- **Bucket policies**: Restrict access to specific IP ranges or VPCs when possible