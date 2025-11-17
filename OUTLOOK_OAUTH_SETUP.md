# Outlook OAuth2 Setup Guide

This guide explains how to set up Microsoft OAuth2 authentication for sending emails from patient Outlook accounts to family medics.

## Prerequisites

1. Azure Active Directory (Azure AD) account
2. App registration in Azure Portal

## Step 1: Register Application in Azure Portal

1. Go to [Azure Portal](https://portal.azure.com)
2. Navigate to **Azure Active Directory** > **App registrations**
3. Click **New registration**
4. Fill in:
   - **Name**: Digital Triage App
   - **Supported account types**: Accounts in any organizational directory and personal Microsoft accounts
   - **Redirect URI**: 
     - Type: Web
     - URI: `https://localhost:7266/signin-microsoft` (for development)
     - For production: `https://yourdomain.com/signin-microsoft`
5. Click **Register**

## Step 2: Configure API Permissions

1. In your app registration, go to **API permissions**
2. Click **Add a permission**
3. Select **Microsoft Graph**
4. Choose **Delegated permissions**
5. Add the following permissions:
   - `Mail.Send` - Send mail as the user
   - `User.Read` - Sign in and read user profile
   - `offline_access` - Maintain access to data you have given it access to
6. Click **Add permissions**
7. Click **Grant admin consent** (if you have admin rights)

## Step 3: Create Client Secret (Optional - for app-only scenarios)

If you need app-only authentication:
1. Go to **Certificates & secrets**
2. Click **New client secret**
3. Add description and expiration
4. **Copy the secret value immediately** (you won't see it again)

## Step 4: Update appsettings.json

Add the following configuration:

```json
{
  "MicrosoftGraph": {
    "TenantId": "your-tenant-id",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret" // Optional
  },
  "Encryption": {
    "Key": "your-32-byte-encryption-key-here!!" // Must be exactly 32 bytes
  }
}
```

## Step 5: Install Required NuGet Packages

```bash
dotnet add package Microsoft.AspNetCore.Authentication.MicrosoftAccount
dotnet add package Microsoft.Graph
```

## Implementation Notes

The OAuth2 flow will:
1. Redirect patient to Microsoft login
2. Patient authenticates with their Outlook account
3. Application receives authorization code
4. Exchange code for access token and refresh token
5. Store encrypted refresh token in database
6. Use refresh token to get new access tokens when sending emails

## Security Considerations

- Store encryption keys securely (use Azure Key Vault in production)
- Refresh tokens should be encrypted at rest
- Implement token refresh logic
- Handle token expiration gracefully

