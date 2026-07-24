# Scheduled work

Use the existing scheduling foundation instead of adding another scheduler.
Implement a typed handler and register it with a stable lowercase key.

```csharp
public sealed class RefreshDomainReport(
    ApplicationDbContext db,
    ILogger<RefreshDomainReport> logger) : IScheduledTaskHandler
{
    public async Task ExecuteAsync(
        ScheduledTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Re-read current domain state and make the operation safe to retry.
        await RebuildAsync(db, context.RunId, cancellationToken);
        logger.LogInformation("Report refresh {RunId} completed", context.RunId);
    }
}

builder.Services
    .AddScheduledTasks()
    .AddScheduledTask<RefreshDomainReport>(
        "refresh-domain-report",
        "Refresh report",
        "Rebuilds the report from current application data.");
```

Handlers must be cancellable, idempotent, safe for at-least-once execution, and
free of browser-visible secrets. Use typed domain tables for per-record inputs;
do not add arbitrary JSON payload execution. When the product needs schedule
management, adapt the APIs under `/api/scheduled-tasks` into domain language and
provide loading, empty, failure, read-only, pause/resume, run-now, and run-history
states. Preview processes can stop or be evicted, so never promise continuous
execution.
