# Connected-data dashboard

The Hosted App build prompt lists configured PostgreSQL and ClickHouse bindings
and their server-side environment-variable names. Use only the binding selected
for the build environment. Never send its value to the browser or log it.

```csharp
var connectionString = builder.Configuration["ANALYTICS_DATABASE_URL"]
    ?? throw new InvalidOperationException(
        "The configured analytics connection is unavailable.");
```

- The PostgreSQL binding value is a URL, not an Npgsql keyword connection
  string. Parse it as a `Uri` and build an `NpgsqlConnectionStringBuilder`
  explicitly before creating an `NpgsqlConnection` or `NpgsqlDataSource`.
  URL-decode the username, password, and database path, and translate recognized
  query parameters such as `sslmode` to typed builder properties. Never pass the
  raw `postgresql://...` value to an Npgsql connection constructor.
- Open connections only in backend services or controllers.
- Prefer a dedicated least-privilege, read-only database role.
- Use parameters for every value and an allowlist for sortable identifiers.
- Bound result counts, query duration, concurrency, and date ranges.
- Return purpose-built aggregate DTOs rather than raw rows.
- Support loading, empty, stale, partial, timeout, and unavailable states.
- Label the data's source and last successful refresh without claiming it is
  live unless the implementation actually refreshes it.

PostgreSQL bindings use a passwordless local PostgreSQL proxy URL. ClickHouse
bindings use a passwordless local HTTP URL; do not use the native protocol.
Never inspect schemas or issue writes unless the requested product explicitly
requires it and the configured role permits it.
