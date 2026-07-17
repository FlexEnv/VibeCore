# VibeCore

A modern full-stack web application template combining ASP.NET Core with React and Vite. This template provides authentication, API development with automatic TypeScript client generation, and a responsive frontend with Tailwind CSS.

## Flex previews

The repository contains a preview contract at `.flexenv/app.json`. Flex runs
the application with `scripts/flex-preview.sh`, exposing ASP.NET Core on port
3000 while Vite remains an internal development server with HMR.

When `FlexSso__Enabled=true`, unauthenticated users are redirected to the
configured `FlexSso__Authority` (normally `https://flexenv.com`). The preview
uses an authorization-code flow with S256 PKCE and creates a secure session
cookie scoped to its own `flexenv.ai` hostname. Local ASP.NET Core Identity
remains available when Flex SSO is disabled.

## 🏗️ Project Architecture

This is a monorepo structure combining:

- **Backend**: ASP.NET Core 10.0 with Razor Pages and API Controllers
- **Frontend**: React 18 + Vite with TypeScript
- **Database**: Entity Framework Core with PostgreSQL in production and SQLite for local development
- **Authentication**: ASP.NET Core Identity with username/password accounts
- **API Documentation**: Swagger/OpenAPI with automatic client generation

## ✨ Features

- ✅ Full-stack development with hot reload for both backend and frontend
- ✅ ASP.NET Core Identity for user authentication
- ✅ PostgreSQL production persistence with a lightweight SQLite development option
- ✅ Policy-based authorization foundations
- ✅ Swagger/OpenAPI integration with UI
- ✅ Automatic TypeScript API client generation from OpenAPI spec
- ✅ Tailwind CSS for styling
- ✅ Tanstack Query for data fetching and caching
- ✅ Docker support
- ✅ Hybrid Razor Pages + React SPA architecture

## 🚀 Quick Start

### Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)
- [Node.js](https://nodejs.org/) v22.18 or higher
- [npm](https://www.npmjs.com/) or [yarn](https://yarnpkg.com/)

### Initial Setup

1. **Clone or create from template**:

   ```bash
   git clone <your-repo-url>
   cd VibeCore
   ```

2. **Install frontend dependencies**:

   ```bash
   cd VibeCore/ClientApp
   npm install
   cd ../..
   ```

3. **Set up the database**:

   The Development profile creates its local SQLite database automatically.
   PostgreSQL deployments use the committed migrations described in
   [DEPLOYMENT.md](DEPLOYMENT.md).

### Development

#### Option 1: Quick Start with PowerShell Script (Recommended)

```bash
cd VibeCore
pwsh ./start-dev.ps1
```

This script automatically:

- Starts the Vite dev server in the background
- Starts the ASP.NET Core application with hot reload
- Handles proper shutdown of both processes

#### Option 2: Manual Start

**Terminal 1 - Frontend (Vite Dev Server)**:

```bash
cd VibeCore/ClientApp
npm run dev
```

**Terminal 2 - Backend (ASP.NET Core)**:

```bash
cd VibeCore
dotnet watch run
```

### Access Points

- **Razor Pages**: https://localhost:7184 (main site, HTTPS) or http://localhost:5036 (HTTP)
- **React App**: https://localhost:7184/app (SPA)
- **Swagger UI**: https://localhost:7184/swagger (API documentation)
- **Vite Dev Server**: http://localhost:5173 (direct access during development)

## 💾 Database & Migrations

This project uses PostgreSQL migrations in production and SQLite for lightweight
local development. Here's how to work with the database:

### Creating Migrations

When you modify entity models in the `Models/` directory or change the `ApplicationDbContext`, create a new migration:

```bash
cd VibeCore
dotnet ef migrations add [MigrationName]
```

Example:

```bash
dotnet ef migrations add AddUserProfileFields
```

### Applying PostgreSQL Migrations

Set the Production environment and PostgreSQL connection string before applying
pending migrations. Development SQLite databases are created automatically and
are not a migration target.

```bash
dotnet ef database update
```

### Common Migration Commands

```bash
# List all migrations
dotnet ef migrations list

# Remove the last migration (if not applied)
dotnet ef migrations remove

# Update to a specific migration
dotnet ef database update [MigrationName]

# Revert all migrations (warning: data loss!)
dotnet ef database update 0

# Generate SQL script for a migration
dotnet ef migrations script
```

### Migration Workflow

1. Modify your entity models or DbContext
2. Create a migration: `dotnet ef migrations add DescriptiveName`
3. Review the generated migration in `Data/Migrations/`
4. Apply the migration: `dotnet ef database update`

## 🔄 API Development Workflow

### How Automatic API Client Generation Works

This project includes an automated workflow for keeping your frontend TypeScript API client in sync with your backend:

1. **Modify/Add API Controllers** in `Areas/Api/Controllers/`
2. **Save your changes** - `dotnet watch` detects the change
3. **PostBuild Hook Runs**:
   - `generate-swagger.sh` fetches the OpenAPI spec from the running app
   - Saves to `ClientApp/swagger.json`
   - Runs `npm run generate-client` to generate TypeScript client
   - Generated client appears in `ClientApp/src/api/`

### Manual API Client Regeneration

If you need to manually regenerate the API client:

```bash
cd VibeCore/ClientApp
npm run update-api
```

Or watch for changes:

```bash
npm run watch-api
```

### Creating a New API Endpoint

1. **Create or modify a controller** in `Areas/Api/Controllers/`:

   ```csharp
   [ApiController]
   [Route("api/[controller]")]
   [Authorize]
   public class MyController : ControllerBase
   {
       [HttpGet]
       public ActionResult<IEnumerable<MyModel>> GetAll()
       {
           // Your logic here
       }
   }
   ```

2. **Add the model** in `Models/`:

   ```csharp
   public class MyModel
   {
       public int Id { get; set; }
       public string Name { get; set; }
   }
   ```

3. **Add DbSet to ApplicationDbContext** if needed:

   ```csharp
   public DbSet<MyModel> MyModels { get; set; }
   ```

4. **Create and apply migration**:

   ```bash
   dotnet ef migrations add AddMyModel
   dotnet ef database update
   ```

5. **Use in React**:

   ```javascript
   import { MyService } from "@/api";

   const { data } = useQuery({
     queryKey: ["myModels"],
     queryFn: () => MyService.getAll(),
   });
   ```

## 📁 Project Structure

```
VibeCore/
├── VibeCore.sln                 # Solution file
├── Dockerfile                   # Docker configuration
├── README.md                    # This file
└── VibeCore/
    ├── Program.cs              # Application entry point & configuration
    ├── appsettings.json        # Configuration
    ├── VibeCore.csproj         # Project file with PostBuild hooks
    ├── Areas/
    │   ├── Api/
    │   │   └── Controllers/    # API Controllers (RESTful endpoints)
    │   └── Identity/
    │       └── Pages/          # Identity UI pages
    ├── ClientApp/              # React + Vite frontend
    │   ├── index.html
    │   ├── package.json
    │   ├── vite.config.js
    │   ├── swagger.json        # Auto-generated OpenAPI spec
    │   ├── src/
    │   │   ├── main.jsx        # React entry point
    │   │   ├── App.jsx         # Main App component
    │   │   ├── api/            # Auto-generated TypeScript API client
    │   │   ├── components/     # React components
    │   │   ├── pages/          # Page components
    │   │   └── providers/      # React context providers
    │   └── scripts/
    │       └── fetch-swagger.js # Script to fetch OpenAPI spec
    ├── Data/
    │   ├── ApplicationDbContext.cs  # EF Core DbContext
    │   └── Migrations/              # EF Core migrations
    ├── Models/                 # Entity models
    ├── Pages/                  # Razor Pages
    ├── Properties/
    │   └── launchSettings.json # Development launch configuration
    └── wwwroot/                # Static files
        ├── app/                # Built React app (production)
        └── css/, js/, lib/     # Razor Pages assets
```

## 🔧 Configuration

### Backend Configuration

Edit `VibeCore/appsettings.json` or `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "Database": {
    "Provider": "PostgreSql"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information"
    }
  }
}
```

### Frontend Configuration

Edit `VibeCore/ClientApp/vite.config.js` for Vite settings.

### Launch Settings

Modify ports and other launch settings in `VibeCore/Properties/launchSettings.json`.

## 🔐 Authentication & Email

This project uses ASP.NET Core Identity with local username/password accounts.
The starter configuration does not require email confirmation, allowing a new
installation to work without an email provider.

Before enabling public self-registration, implement `IEmailSender`, set
`options.SignIn.RequireConfirmedAccount` to `true`, and keep provider
credentials in a secret manager. An email sender is also needed for password
reset messages.

## 🏭 Production Build

### Build Frontend

```bash
cd VibeCore/ClientApp
npm run build
```

This creates optimized production files in `wwwroot/app/`.

### Build Backend

```bash
cd VibeCore
dotnet build -c Release
```

### Publish

```bash
dotnet publish -c Release -o ./publish
```

### Run Production Build

```bash
cd publish
./VibeCore
```

Or using Docker:

```bash
docker build -t vibecore .
docker run -p 8080:8080 vibecore
```

## 🧪 Testing

```bash
# Run backend tests
dotnet test

# Run frontend tests (if configured)
cd VibeCore/ClientApp
npm test
```

## 📚 Additional Documentation

- [API Setup Guide](API-SETUP.md) - Detailed API configuration and examples
- [React Integration Guide](VibeCore/README-React.md) - React + Vite integration details
- [Template Guide](TEMPLATE-README.md) - Using this as a dotnet template
- [Production Deployment](DEPLOYMENT.md) - PostgreSQL, Identity, migrations, and container deployment

## 🛠️ Tech Stack

### Backend

- ASP.NET Core 10.0
- Entity Framework Core
- ASP.NET Core Identity
- Swashbuckle (Swagger/OpenAPI)
- PostgreSQL (production) and SQLite (development)

### Frontend

- React 18
- Vite 6
- Tanstack Query
- React Router
- Tailwind CSS 4
- TypeScript
- OpenAPI TypeScript Codegen

## 🤝 Contributing

1. Create a feature branch
2. Make your changes
3. Test thoroughly
4. Submit a pull request

## 📄 License

[Your License Here]

---

## 🤖 For LLMs: Project Overview

This is a hybrid ASP.NET Core + React application with the following key characteristics:

### Architecture Pattern

- **Backend**: ASP.NET Core with Razor Pages (traditional pages) + API Controllers (RESTful API)
- **Frontend**: React SPA served under `/app` route, coexisting with Razor Pages
- **Database**: Entity Framework Core with PostgreSQL in production and SQLite in development

### Development Workflow

1. Entity models are defined in `Models/` folder
2. `ApplicationDbContext` in `Data/` folder manages DbSets
3. API Controllers in `Areas/Api/Controllers/` expose REST endpoints
4. On build, OpenAPI spec is auto-generated and TypeScript client is created
5. React components consume the generated API client

### Key Commands for Development

```bash
# Database changes
dotnet ef migrations add [MigrationName]
dotnet ef database update

# Start dev environment
pwsh ./start-dev.ps1  # or dotnet watch run + npm run dev

# Regenerate API client
cd ClientApp && npm run update-api
```

### Important Files

- `Program.cs`: App configuration, middleware setup
- `ApplicationDbContext.cs`: Database context and entity configuration
- `VibeCore.csproj`: PostBuild hooks for API generation
- `ClientApp/package.json`: Frontend scripts and dependencies
- `generate-swagger.sh`: Script that fetches OpenAPI spec and triggers client generation

### When Adding New Features

1. Create model in `Models/`
2. Add DbSet to `ApplicationDbContext`
3. Run: `dotnet ef migrations add <Name>` then `dotnet ef database update`
4. Create controller in `Areas/Api/Controllers/`
5. API client auto-generates on build
6. Use generated client in React components

This structure enables type-safe API calls from React to ASP.NET Core with automatic synchronization.
