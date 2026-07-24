# Role-aware workflow

Flex SSO supplies `Reader`, `Editor`, `Operator`, and `Administrator` application
roles. Treat backend authorization as the security boundary; hiding a button is
only a usability improvement.

```csharp
[HttpPost("{id:int}/approve")]
[Authorize(Policy = AppPolicies.Operator)]
public async Task<IActionResult> Approve(int id, CancellationToken ct)
{
    var entity = await db.DomainRecords.SingleOrDefaultAsync(x => x.Id == id, ct);
    if (entity is null) return NotFound();
    if (entity.Status != DomainStatus.Pending)
        return Conflict(new ProblemDetails { Detail = "Only pending records can be approved." });

    entity.Status = DomainStatus.Approved;
    entity.DecidedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);
    return NoContent();
}
```

In React, read `/api/user/current`, derive capabilities from `roles`, and omit or
disable unavailable actions with a short explanation. Still handle `403` from
every mutation because roles can change and UI checks can be bypassed. Test the
same action as an unauthorized reader and an authorized operator.
