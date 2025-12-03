# ?? Local Development Setup

## ?? IMPORTANT: API Keys Configuration

This project uses external APIs that require authentication. **Never commit real API keys to Git!**

## Setup Instructions

### Step 1: Create Local Configuration File

1. Copy `appsettings.Development.json.template` to `appsettings.Development.json`:
   ```powershell
   cd src\Presentation\DigitalTriage.Presentation
   Copy-Item appsettings.Development.json.template appsettings.Development.json
   ```

2. The `appsettings.Development.json` file is already in `.gitignore` and will not be committed.

### Step 2: Get API Keys

#### Gemini AI API Key
1. Visit [Google AI Studio](https://aistudio.google.com/app/apikey)
2. Sign in with your Google account
3. Click **"Create API Key"**
4. Copy the generated key
5. Paste it in `appsettings.Development.json`:
   ```json
   "Gemini": {
     "ApiKey": "YOUR_ACTUAL_API_KEY_HERE",
     "Model": "gemini-2.0-flash"
   }
   ```

#### Microsoft Graph API (Optional - for Outlook integration)
1. Visit [Azure Portal](https://portal.azure.com)
2. Register an application
3. Get Client ID and Client Secret
4. Add to `appsettings.Development.json`

### Step 3: Run the Application

```powershell
dotnet restore
dotnet build
cd src\Presentation\DigitalTriage.Presentation
dotnet run
```

## ?? Security Checklist

- ? `appsettings.Development.json` is in `.gitignore`
- ? Never commit real API keys
- ? Use environment variables for production
- ? Rotate API keys if accidentally exposed
- ? Use `.template` files for configuration examples

## ?? Files to Keep Private

These files should NEVER be committed to Git:
- `appsettings.Development.json` - Contains your local API keys
- `appsettings.Local.json` - Local overrides
- `.env` files - Environment variables
- `secrets.json` - User secrets

## ?? If You Accidentally Committed Secrets

1. **Immediately revoke/rotate** the exposed API keys
2. Remove the file from Git history:
   ```powershell
   git rm --cached src/Presentation/DigitalTriage.Presentation/appsettings.Development.json
   git commit -m "Remove sensitive configuration"
   git push
   ```
3. Generate new API keys
4. Update your local `appsettings.Development.json` with new keys

## ?? Additional Resources

- [Gemini AI Documentation](https://ai.google.dev/docs)
- [Microsoft Graph API](https://learn.microsoft.com/en-us/graph/)
- [ASP.NET Core User Secrets](https://learn.microsoft.com/en-us/aspnet/core/security/app-secrets)
