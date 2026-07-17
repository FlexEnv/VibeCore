# ASP.NET Core with React and Vite Template

This is a dotnet template for creating a full-stack ASP.NET Core application with React frontend using Vite.

## Features

- ASP.NET Core 10.0 backend
- React 18 frontend with Vite
- ASP.NET Core Identity for authentication
- Entity Framework Core with PostgreSQL in production and SQLite in development
- Swagger/OpenAPI integration
- Auto-generated API clients from OpenAPI spec
- Tailwind CSS support
- Docker support

## Installation

From this directory, install the template:

```bash
dotnet new install .
```

Or from a NuGet package (if published):

```bash
dotnet new install <PackageName>
```

## Usage

Create a new project:

```bash
dotnet new vibe-core -n MyAwesomeApp
```

This will create a new project called "MyAwesomeApp" with all files properly renamed.

## Uninstall

To uninstall the template:

```bash
dotnet new uninstall .
```

Or if installed from NuGet:

```bash
dotnet new uninstall <PackageName>
```

## After Creating a Project

1. Navigate to your project directory
2. Navigate to the project folder (e.g., `cd MyAwesomeApp/MyAwesomeApp`)
3. Install npm packages: `cd ClientApp && npm install && cd ..`
4. Run the development server: `pwsh ./start-dev.ps1` (or `./start-dev.sh` on Linux/Mac)
5. Open https://localhost:7154

## What Gets Renamed

When you create a new project with `-n YourProjectName`, the following will be automatically replaced:

- Solution file name
- Project folder name
- .csproj file name
- Namespace references
- Docker image names
- API titles
- And all other references to "VibeCore"

## License

MIT
