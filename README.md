# VibeCore

VibeCore is a full-stack application template built with ASP.NET Core, React,
Vite, Entity Framework Core, and Flex SSO. It includes a small authenticated
todo application as a working API and UI example.

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

After changing controllers or API models, run the application and then:

```bash
cd VibeCoreWeb/ClientApp
npm run update-api
```

## Validation

```bash
dotnet build VibeCore.sln --no-restore -p:BuildClientApp=false
dotnet test VibeCore.sln --no-restore
cd VibeCoreWeb/ClientApp
npm run build
```

See [DEPLOYMENT.md](DEPLOYMENT.md) for production configuration and
[TEMPLATE-README.md](TEMPLATE-README.md) for template usage.
