using Microsoft.EntityFrameworkCore;
using VibeCore.Data;

namespace VibeCore.Scheduling;

public sealed class TodoSummaryTask(
    ApplicationDbContext db,
    ILogger<TodoSummaryTask> logger) : IScheduledTaskHandler
{
    public async Task ExecuteAsync(
        ScheduledTaskExecutionContext context,
        CancellationToken cancellationToken)
    {
        var incompleteCount = await db.Todos.CountAsync(
            todo => !todo.IsCompleted,
            cancellationToken);
        logger.LogInformation(
            "Scheduled todo summary {RunId}: {IncompleteCount} incomplete todos",
            context.RunId,
            incompleteCount);
    }
}
