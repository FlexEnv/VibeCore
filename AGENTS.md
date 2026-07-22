# VibeCore agent notes

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

## API changes

After changing controllers or API models, run the application and then run:

```bash
cd VibeCoreWeb/ClientApp
npm run update-api
```

Commit the updated `swagger.json` and generated TypeScript client with the API
change.

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
