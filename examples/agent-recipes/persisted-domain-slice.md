# Persisted domain slice

Use this pattern only when the requested app needs preview-local persistence.
Preview SQLite data is disposable; never describe it as durable production
storage.

1. Add a domain model with explicit limits, timestamps, and tenant/user fields
   only when the product requires them.
2. Add its `DbSet` and indexes in `ApplicationDbContext`, then create an EF
   migration.
3. Use separate request and response records. Put validation attributes on
   positional record constructor parameters.
4. Authorize every controller and enforce object-level access in the query.
5. Run a normal Debug build to regenerate OpenAPI and the TypeScript client.
6. Build React states for loading, failure, empty data, validation failure,
   success, and mutation-in-progress.

```csharp
public sealed record CreateRecordRequest(
    [param: Required, param: StringLength(160)] string Name);

[HttpPost]
[Authorize(Policy = AppPolicies.Editor)]
public async Task<ActionResult<RecordDto>> Create(
    CreateRecordRequest request,
    CancellationToken ct)
{
    var entity = new DomainRecord
    {
        Name = request.Name.Trim(),
        CreatedAt = DateTimeOffset.UtcNow
    };
    db.DomainRecords.Add(entity);
    await db.SaveChangesAsync(ct);
    return CreatedAtAction(nameof(Get), new { id = entity.Id }, ToDto(entity));
}
```

Do not accept client-supplied IDs, ownership, audit timestamps, or completion
state. Return `ValidationProblemDetails` for domain validation and keep secrets
out of response DTOs.
