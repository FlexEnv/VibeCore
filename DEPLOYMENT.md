# Production deployment

VibeCore uses PostgreSQL, Flex SSO, and database-backed ASP.NET Core Data
Protection in production. It has no local user store.

## Required configuration

```text
ConnectionStrings__DefaultConnection=Host=...;Database=...;Username=...;Password=...
Database__Provider=PostgreSql
DataProtection__PersistKeysToDatabase=true
FlexSso__Authority=https://your-flex-host
```

Set `FlexSso__BackchannelAuthority` when the application must exchange the SSO
code through a different internal address. Both authority values are validated
at startup. Keep credentials in environment variables or a secret manager.

## Database migrations

The committed PostgreSQL migration creates application data and Data Protection
tables only. Generate and review future migrations with:

```powershell
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:ConnectionStrings__DefaultConnection = "<postgres connection string>"
dotnet ef migrations add <MigrationName> `
  --project VibeCoreWeb/VibeCoreWeb.csproj `
  --startup-project VibeCoreWeb/VibeCoreWeb.csproj `
  --output-dir Data/Migrations
dotnet ef migrations has-pending-model-changes `
  --project VibeCoreWeb/VibeCoreWeb.csproj `
  --startup-project VibeCoreWeb/VibeCoreWeb.csproj
```

Apply a reviewed idempotent SQL script or migration bundle before starting the
new application version.

## Container

```powershell
docker build --tag vibecore:local .
docker run --rm --publish 8080:8080 `
  --env "ConnectionStrings__DefaultConnection=<postgres connection string>" `
  --env "FlexSso__Authority=https://your-flex-host" `
  vibecore:local
```

The application exposes `/health/live` and `/health/ready`. Run
`pwsh ./scripts/smoke-container.ps1` to exercise the immutable image.
