# Against The Spread v1.1 - College Football Pick'em PWA

[![Build and Test](https://github.com/YOUR_USERNAME/against-the-spread/actions/workflows/build-test.yml/badge.svg)](https://github.com/YOUR_USERNAME/against-the-spread/actions/workflows/build-test.yml)
[![codecov](https://codecov.io/gh/YOUR_USERNAME/against-the-spread/branch/main/graph/badge.svg)](https://codecov.io/gh/YOUR_USERNAME/against-the-spread)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A Progressive Web Application (PWA) for managing a weekly college football pick'em game. Upload weekly betting lines, select 6 games against the spread, and download formatted picks - all from your mobile device.

## 🎯 Features

- **Admin Panel**: Upload weekly betting lines with Google OAuth authentication
- **Mobile-First PWA**: Install as native app on iOS/Android with custom football icon
- **Excel Upload/Download**: Upload betting lines and generate formatted picks
- **Offline Capable**: Works without internet connection (service worker)
- **Secure Authentication**: Google OAuth with email-based authorization
- **Dev/Prod Environments**: Branch-based deployments (main = production, dev = staging)
- Automated scoring and leaderboards

## 🏗️ Architecture

**Frontend**: Blazor WebAssembly PWA  
**Backend**: Azure Functions (C# .NET 8)  
**Storage**: Azure Blob Storage  
**Infrastructure**: Terraform  
**CI/CD**: GitHub Actions

```
┌─────────────────┐
│  Mobile/Web     │
│  Blazor PWA     │
└────────┬────────┘
         │ HTTPS
         ▼
┌─────────────────┐
│ Azure Functions │
│   REST API      │
└────────┬────────┘
         │
         ▼
┌─────────────────┐
│  Blob Storage   │
│  Excel Files    │
└─────────────────┘
```

## 🚀 Quick Start

### 🤖 GitHub Copilot Development Environment (Recommended)

**Get started in 5 minutes with zero local setup!**

We provide multiple ways to work with this repository using GitHub Copilot:

- ✅ **GitHub Copilot Coding Agent**: Automatically configured via `.github/workflows/copilot-setup-steps.yml` - when the agent works on code, it gets a pre-configured environment with all tools
- ✅ **GitHub Codespaces**: Cloud-based development environment, no local installation needed
- ✅ **VS Code Dev Container**: Docker-based local development with all tools pre-installed
- ✅ **Pre-configured**: .NET 8, Node.js, Azure Functions, Azurite, Playwright
- ✅ **No Firewall Issues**: All services run locally/in-cloud
- ✅ **One-Click Start**: `./start-local.sh` starts everything

**👉 [See COPILOT_ENVIRONMENT.md for detailed instructions](COPILOT_ENVIRONMENT.md)**

### Manual Setup

If you prefer to install tools manually:

#### Prerequisites

- .NET 8 SDK
- Azure CLI
- Azure Functions Core Tools (v4)
- Node.js (for Azure Static Web Apps CLI)
- Azure Static Web Apps CLI: `npm install -g @azure/static-web-apps-cli`
- Azure subscription (free tier works)

#### Environment Setup

1. **Clone the repository**
   ```bash
   git clone https://github.com/quaz579/against-the-spread-v1.1.git
   cd against-the-spread-v1.1
   ```

2. **Create local Functions settings**
   ```bash
   cp src/AgainstTheSpread.Functions/local.settings.example.json \
      src/AgainstTheSpread.Functions/local.settings.json
   ```
   Set `GOOGLE_CLIENT_ID`, `ADMIN_EMAILS`, and the local storage connection.
   This flow has no Google client secret.

3. **Configure Google OAuth** (see `GOOGLE_AUTH_SETUP.md` for detailed instructions)
   - Create OAuth 2.0 Client in Google Cloud Console
   - Add authorized JavaScript origins for the production host and
     `http://localhost:4280`

4. **Configure Azure resources**
   ```bash
   # Create resource group
   az group create --name rg-against-the-spread --location centralus

   # Create storage account
   az storage account create \
     --name stprdagnstthesprd \
     --resource-group rg-against-the-spread \
     --location centralus \
     --sku Standard_LRS

   # Create blob container
   az storage container create \
     --name weeklypicks \
     --account-name stprdagnstthesprd
   ```

5. **Deploy Azure Static Web App** (via GitHub Actions)
   - Push to `main` branch for production
   - Push to `dev` branch for staging environment
   - GitHub Actions will automatically build and deploy

6. **Configure SWA App Settings** (for production/staging)
   ```bash
   az staticwebapp appsettings set \
     --name swa-against-the-spread \
     --setting-names \
       GOOGLE_CLIENT_ID="your-client-id" \
       ADMIN_EMAILS="admin@example.com" \
       AZURE_STORAGE_CONNECTION_STRING="your-connection-string"
   ```

### Local Development

Run the app locally with Google Identity Services:

1. **Publish the Blazor app** (generates static files)
   ```bash
   dotnet publish src/AgainstTheSpread.Web -c Debug
   ```

2. **Start SWA CLI**
   ```bash
   swa start src/AgainstTheSpread.Web/bin/Debug/net8.0/publish/wwwroot \
     --api-location src/AgainstTheSpread.Functions
   ```

3. **Access the app**
   - Web app: `http://localhost:4280`
   - API: `http://localhost:4280/api`
   - Functions (direct): `http://localhost:7071/api`

4. **Test authentication locally**
   - Use a real Google sign-in only when `http://localhost:4280` is an
     authorized JavaScript origin.
   - Automated tests inject a fake GIS callback and intercept admin APIs;
     they do not mint real Google credentials or use SWA mock principals.

**Note**: The Blazor app must be published (not just built) for the SWA CLI to serve it correctly. The publish step generates the static files in `bin/Debug/net8.0/publish/wwwroot/`.

See `LOCAL_DEV_AUTH.md` for detailed authentication testing instructions.

### Run Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsDir=./coverage
```

## 📁 Project Structure

```
against-the-spread/
├── .github/
│   └── workflows/           # GitHub Actions CI/CD
├── src/
│   ├── AgainstTheSpread.Core/       # Shared models and services
│   ├── AgainstTheSpread.Functions/  # Azure Functions API
│   ├── AgainstTheSpread.Web/        # Blazor WASM PWA
│   └── AgainstTheSpread.Tests/      # Unit & integration tests
├── infrastructure/
│   ├── terraform/           # Infrastructure as Code
│   └── scripts/             # Deployment scripts
├── reference-docs/          # Excel templates and examples
├── docs/                    # Additional documentation
├── .agents.md              # Agent development guide
├── implementation-plan.md  # Detailed implementation plan
└── README.md               # This file
```

## 🧪 Testing

The project follows a Test-Driven Development (TDD) approach:

- **Unit Tests**: All business logic in Core library
- **Integration Tests**: API endpoints with Azurite
- **Component Tests**: Blazor components with bUnit
- **E2E Tests**: Full user flows

### Test Coverage Goals
- Core Library: >90%
- Functions: >80%
- Web Components: >70%

## 🚢 Deployment

### Deploy Infrastructure

```bash
cd infrastructure/terraform
terraform init
terraform apply -var-file="environments/dev.tfvars"
```

### Deploy Application

Deployments are automated via GitHub Actions on merge to `main`.

Manual deployment:
```bash
# Deploy Functions
cd src/AgainstTheSpread.Functions
func azure functionapp publish <function-app-name>

# Deploy Web App
cd src/AgainstTheSpread.Web
dotnet publish -c Release
# Upload to Azure Static Web Apps
```

## 📱 PWA Installation

### iOS (Safari)
1. Open the app in Safari
2. Tap the Share button
3. Tap "Add to Home Screen"
4. Tap "Add"

### Android (Chrome)
1. Open the app in Chrome
2. Tap the menu (⋮)
3. Tap "Install app" or "Add to Home screen"

## 🎮 Usage

### Admin: Upload Weekly Lines

**Option 1: Using helper script (recommended)**
```bash
./infrastructure/scripts/upload-lines.sh path/to/Week1.xlsx 1
```

**Option 2: Using Azure CLI directly**
```bash
az storage blob upload \
  --account-name <storage-account-name> \
  --container-name gamefiles \
  --name "lines/week-1.xlsx" \
  --file "Week 1 Lines.xlsx" \
  --auth-mode login
```

### User: Make Picks

1. Navigate to the web app
2. Select the current week from dropdown
3. Tap 6 games to select them
4. Enter your name
5. Click "Download Picks"
6. Open Excel file and email to admin

## 🔒 Security

- HTTPS enforced for all connections
- CORS configured for known origins
- Input validation on all endpoints
- File upload size limits enforced
- Rate limiting on API endpoints
- No authentication required for MVP (trust-based)

## 💰 Cost Analysis

Running on Azure free tier:
- **Azure Functions**: Free (1M executions/month)
- **Static Web Apps**: Free (100GB bandwidth/month)
- **Blob Storage**: ~$0.02-0.10/month

**Total Monthly Cost**: Effectively FREE

## 🤝 Contributing

This project uses AI-assisted development. Please review the following before contributing:

1. Read [`.agents.md`](.agents.md) for development guidelines
2. Follow the [implementation plan](implementation-plan.md)
3. Write tests first (TDD approach)
4. Ensure all tests pass before submitting PR
5. Update documentation as needed

### Development Workflow

1. Create a feature branch
2. Implement changes with tests
3. Run `dotnet build` and `dotnet test`
4. Create PR with detailed description
5. Wait for CI/CD checks to pass
6. Request code review

## 📊 Monitoring

Application Insights is configured for:
- API request/response times
- Error tracking
- Custom events (uploads, downloads)
- User analytics

Access dashboards in Azure Portal.

## 🗺️ Roadmap

### MVP (Current Phase)
- [x] Project setup and infrastructure
- [ ] Excel parsing and generation (exact format match)
- [ ] Azure Functions API (user endpoints only)
- [ ] Blazor PWA UI (user interface only)
- [ ] Admin upload script
- [ ] Infrastructure deployment
- [ ] CI/CD pipeline

### Future Enhancements
- Web-based admin upload interface
- User authentication (Azure AD B2C)
- Pick history and tracking
- Automated scoring with game results
- Leaderboards
- Push notifications
- Bowl games with confidence points
- Playoff bracket management
- Native mobile apps (.NET MAUI)

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- Built with [Blazor](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor)
- Excel processing by [EPPlus](https://github.com/EPPlusSoftware/EPPlus)
- Hosted on [Azure](https://azure.microsoft.com)
- Automated with [GitHub Actions](https://github.com/features/actions)

## 📞 Support

For issues, questions, or contributions:
- Create an [Issue](https://github.com/YOUR_USERNAME/against-the-spread/issues)
- Submit a [Pull Request](https://github.com/YOUR_USERNAME/against-the-spread/pulls)

---

**Built with ❤️ for college football fans**
