# Production deployment

VibeCore uses PostgreSQL, Flex SSO, and database-backed ASP.NET Core Data
Protection in production. It has no local user store.

Quartz executes scheduled tasks inside the ASP.NET Core process. Production
hosting must keep at least one application instance running. PostgreSQL-backed
Quartz clustering coordinates multiple instances; handlers must still be
idempotent because execution is at-least-once across retries and failures.

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

The committed PostgreSQL migrations create application data, Data Protection,
scheduled-task history, and the Quartz `qrtz_*` tables. Generate and review
future migrations with:

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
new application version. The disposable SQLite preview path creates the Quartz
schema idempotently at startup; production intentionally relies on the reviewed
PostgreSQL migration.

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
