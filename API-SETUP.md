# API Setup

## Backend

VibeCore exposes authenticated ASP.NET Core controllers under `/api`. Swagger
UI is available at `/swagger` in Development and the runtime document is served
from `/swagger/v1/swagger.json`.

## Generated TypeScript client

The Debug build generates the OpenAPI document directly from the compiled
application assembly, so an application server and HTTP port are not required:

```bash
dotnet build VibeCore.sln
```

The build performs these steps:

1. Restores the solution-local Swashbuckle CLI tool.
2. Writes the `v1` document to `VibeCoreWeb/ClientApp/swagger.json`.
3. Runs Orval to regenerate the client under
   `VibeCoreWeb/ClientApp/src/api`.
4. Formats the generated client with Prettier.

Commit the document and generated TypeScript client with API controller or model
changes. Consecutive builds should produce identical generated files.

The build-time host does not initialize the application database, start Quartz,
map static assets, or start Vite. It registers the same controllers and Swagger
services as the runtime application.

## Build variants

- Normal Debug builds regenerate the OpenAPI document and TypeScript client.
- `dotnet build VibeCore.sln -p:BuildClientApp=false` performs a backend-only
  build without invoking Node or changing generated client artifacts.
- Release and publish builds consume the committed document and retain the
  existing production client build flow.

From `VibeCoreWeb/ClientApp`, `npm run update-api` is a convenience alias for
building the parent web project. `npm run generate-client` can rerun Orval from
the existing committed document without rebuilding .NET.
