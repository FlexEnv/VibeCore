# VibeCore agent notes

## Reference recipes

The application starts domain-free. Before implementing a requested capability,
consult `examples/agent-recipes/README.md` and open only recipes that directly
match the requested product. Recipes are reference material, not current
requirements: never mount them, expose them in navigation, or copy them
wholesale. Adapt the smallest relevant pattern to the actual domain and delete
placeholder names from copied snippets.

## Flex preview contract

Flex previews run this repository with:

- runtime profile `vibecore-dotnet`;
- bootstrap command `/app/bootstrap-vibecore.sh`;
- preview command `./scripts/flex-preview.sh`;
- public application port `3000`;
- SQLite for disposable preview data; and
- Flex SSO enabled through `FlexSso__Authority`.

Keep `.flexenv/app.json`, the preview script, and the actual application commands
in sync. The ASP.NET process is the only externally routed process. Vite listens
internally on port 5173 and is proxied by Vite.AspNetCore.

Keep Vite development module URLs canonical. Do not append cache-busting query
strings to `/app/@vite/client`, `/app/@react-refresh`, or the Vite entry module.
Vite can propagate an entry query through only part of the import graph, loading
two instances of a React context and causing false "must be used within a
Provider" errors. Vite owns development caching and HMR invalidation.

## API changes

Normal Debug builds regenerate `swagger.json` and the TypeScript API client from
the compiled application:

```bash
dotnet build VibeCore.sln
```

Commit the updated `swagger.json` and generated TypeScript client with the API
change. Pass `-p:BuildClientApp=false` for backend-only builds that must not
invoke Node or update generated client artifacts.

## Scheduled tasks

Use the scheduler foundation in `VibeCoreWeb/Scheduling` instead of adding a
second scheduler package. Implement `IScheduledTaskHandler`, register it with a
stable lowercase `AddScheduledTask<THandler>` key, and keep handlers
cancellable, idempotent, and free of browser-visible secrets. The generic admin
page deliberately has no arbitrary JSON payload editor; add typed domain models,
API validation, and product UI when a task needs per-user or per-record input.

Do not promise guaranteed continuous availability in a Preview App. Preview
containers have no inactivity timeout, but may be stopped explicitly or evicted
for capacity and use disposable SQLite data. The later always-running Hosted App
runtime supplies the production continuity contract.

## ASP.NET Core positional request records

For positional records used as inbound MVC action models, target validation
attributes at the primary-constructor parameters:

```csharp
public sealed record ExampleRequest(
    [param: Required] string Value);
```

Do not use `[property: Required]` (or other property-targeted validation
attributes) on an inbound positional record. ASP.NET Core 10 rejects that
metadata during model binding and returns HTTP 500 before the controller action
runs. When the request is an SSO callback or another proxied flow, the caller
may surface that failure as 502. Add a regression test that exercises model
binding, or at minimum verifies the attribute targets through reflection.

Property-targeted attributes can still be intentional on response-only DTOs for
schema generation. Do not replace them globally; first determine whether the
record is accepted as an action input.

## Validation

```bash
dotnet build VibeCore.sln --no-restore -p:BuildClientApp=false
cd VibeCoreWeb/ClientApp
npm run build
```

Do not put credentials in the repository or browser-visible Vite environment
variables. Flex injects runtime credentials and Git authentication.
