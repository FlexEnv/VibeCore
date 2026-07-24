# VibeCore

VibeCore is a domain-free full-stack application template built with ASP.NET
Core, React, Vite, Entity Framework Core, and Flex SSO. It provides platform
infrastructure without imposing a sample business model on generated apps.

## Authentication

Flex SSO is the only authentication system. The application does not maintain
local user accounts, passwords, registration pages, or account-management
pages. Authenticated identity and application roles come from the claims issued
by Flex.

Every environment must provide a reachable Flex authority. Flex previews and
hosted applications inject these values automatically. For direct local runs,
configure the local Flex instance explicitly:

```text
FlexSso__Authority=https://your-flex-host
FlexSso__BackchannelAuthority=http://your-container-accessible-flex-host
```

`FlexSso__BackchannelAuthority` is optional when the public authority is also
reachable from the application process. Use HTTPS for the browser-facing app;
the SSO session cookie is always Secure and supports the embedded preview flow.

## Development

Prerequisites are .NET 10 and Node.js 22 or newer.

```bash
cd VibeCoreWeb/ClientApp
npm install
cd ../..
./scripts/flex-preview.sh
```

The preview command runs ASP.NET Core on port 3000 and proxies Vite through
`/app`. Direct `dotnet run` also requires a connection string and Flex SSO
authority configuration.

## Architecture

- ASP.NET Core hosts Razor, the authenticated API, health checks, and Flex SSO.
- React and Vite provide the application UI under `/app`.
- Entity Framework Core uses PostgreSQL in production and SQLite for disposable
  development previews.
- Data Protection keys may be persisted in the application database.
- Swagger/OpenAPI generates the TypeScript API client.
- Quartz.NET runs persistent one-off and recurring server-side tasks from the
  same SQLite or PostgreSQL database through a typed management API.

## Scheduled tasks

Scheduled work is registered as a typed server-side handler and is then managed
through the API or React page. Handlers receive cancellation and execution IDs,
must be idempotent, and should read typed application data rather than accepting
arbitrary dashboard payloads.

```csharp
public sealed class RefreshReportTask : IScheduledTaskHandler
{
    public Task ExecuteAsync(
        ScheduledTaskExecutionContext context,
        CancellationToken cancellationToken) => RefreshAsync(cancellationToken);
}

builder.Services.AddScheduledTask<RefreshReportTask>(
    "refresh-report",
    "Refresh report",
    "Rebuilds the report from current application data.");
```

The default policy prevents overlapping runs and retries failures after 1, 5,
and 15 minutes. Pass `ScheduledTaskHandlerOptions` during registration to change
that policy. Preview containers have no inactivity timeout, but can still be
stopped explicitly or evicted for capacity. They are suitable for building and
testing schedules but do not carry a continuous-availability guarantee.

After changing controllers or API models, run a normal Debug build:

```bash
dotnet build VibeCore.sln
```

The build generates `VibeCoreWeb/ClientApp/swagger.json` from the compiled
assembly and then regenerates the Orval TypeScript client. Use
`-p:BuildClientApp=false` when a backend-only build should skip Node and
generated-client updates.

## Validation

```bash
dotnet build VibeCore.sln --no-restore -p:BuildClientApp=false
dotnet test VibeCore.sln --no-restore
cd VibeCoreWeb/ClientApp
npm run build
```

See [DEPLOYMENT.md](DEPLOYMENT.md) for production configuration and
[TEMPLATE-README.md](TEMPLATE-README.md) for template usage.
