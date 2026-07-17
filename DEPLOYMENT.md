# Production deployment

VibeCore uses PostgreSQL, ASP.NET Core Identity username/password accounts, and
database-backed ASP.NET Core Data Protection in production. SQLite remains
available in the Development environment.

The SQLite database is a disposable developer convenience and is created from
the current model with `EnsureCreated`. PostgreSQL is the migration source of
truth; do not apply the PostgreSQL migrations through the SQLite provider.

## Required production configuration

Set configuration through environment variables or a secret manager:

```text
ConnectionStrings__DefaultConnection=Host=...;Database=...;Username=...;Password=...
Database__Provider=PostgreSql

DataProtection__PersistKeysToDatabase=true
```

Users register and sign in through the built-in ASP.NET Core Identity pages.
Email confirmation is intentionally not required in the starter configuration,
so local accounts work without an email service. Before exposing self-registration
outside a trusted environment, add an `IEmailSender` and require confirmed
accounts. Authorization policies for future role-based features remain in
`Security/AppPolicies.cs`.

## Database migrations

Migrations are generated for PostgreSQL and committed under
`VibeCoreWeb/Data/Migrations`. Generate and check migrations using production
provider configuration:

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

For production, generate a reviewed idempotent SQL script or an EF migration
bundle rather than applying migrations when the web process starts:

```powershell
dotnet ef migrations script --idempotent `
  --project VibeCoreWeb/VibeCoreWeb.csproj `
  --startup-project VibeCoreWeb/VibeCoreWeb.csproj `
  --output migrations.sql
```

Run the migration before starting a new application version. The initial
migration creates the business tables, Identity tables, and shared
`DataProtectionKeys` table.

## Container

Build and run the production image:

```powershell
docker build --tag vibecore:local .
docker run --rm --publish 8080:8080 `
  --env "ConnectionStrings__DefaultConnection=<postgres connection string>" `
  vibecore:local
```

The application listens on port `8080` and exposes a process liveness endpoint
at `/health/live`.

Run the isolated image smoke test with:

```powershell
pwsh ./scripts/smoke-container.ps1
```

The smoke test deliberately disables database-backed Data Protection because it
only checks that the immutable application image can boot. Deployed
environments should keep `DataProtection__PersistKeysToDatabase=true`.
