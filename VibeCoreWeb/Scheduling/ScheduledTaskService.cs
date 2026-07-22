using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Quartz;
using Quartz.Impl.Matchers;
using VibeCore.Data;
using VibeCore.Models;

namespace VibeCore.Scheduling;

public enum ScheduledTaskKind
{
    Cron,
    OneTime
}

public sealed record ScheduledTaskHandlerDto(
    string Key,
    string DisplayName,
    string Description,
    int RetryCount,
    bool AllowsConcurrentExecution);

public sealed record ScheduledTaskDto(
    Guid Id,
    string Name,
    string HandlerKey,
    ScheduledTaskKind Kind,
    string? CronExpression,
    string? TimeZoneId,
    DateTimeOffset? RunAt,
    string Status,
    DateTimeOffset? NextFireAt,
    DateTimeOffset? PreviousFireAt);

public sealed record ScheduledTaskRunDto(
    Guid Id,
    Guid ScheduleId,
    string HandlerKey,
    string Status,
    int Attempt,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    long? DurationMilliseconds,
    string? ErrorSummary);

public sealed record SaveScheduledTaskRequest(
    [param: Required, StringLength(120, MinimumLength = 1)] string Name,
    [param: Required, StringLength(100)] string HandlerKey,
    ScheduledTaskKind Kind,
    string? CronExpression,
    string? TimeZoneId,
    DateTimeOffset? RunAt);

public sealed class ScheduledTaskValidationException(string message) : Exception(message);

public sealed class ScheduledTaskService(
    ISchedulerFactory schedulerFactory,
    IScheduledTaskRegistry registry,
    ApplicationDbContext db)
{
    internal const string JobGroup = "vibecore-schedules";
    internal const string TriggerGroup = "vibecore-schedule-triggers";
    internal const string RetryGroup = "vibecore-schedule-retries";
    internal const string NameKey = "name";
    internal const string HandlerKey = "handlerKey";
    internal const string KindKey = "kind";
    internal const string CronKey = "cronExpression";
    internal const string TimeZoneKey = "timeZoneId";
    internal const string RunAtKey = "runAt";
    internal const string AttemptKey = "attempt";

    public IReadOnlyCollection<ScheduledTaskHandlerDto> GetHandlers() =>
        registry.Handlers.Select(handler => new ScheduledTaskHandlerDto(
            handler.Key,
            handler.DisplayName,
            handler.Description,
            handler.Options.RetryDelays?.Count ?? 0,
            handler.Options.AllowConcurrentExecution)).ToArray();

    public async Task<IReadOnlyCollection<ScheduledTaskDto>> GetSchedulesAsync(
        CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var keys = await scheduler.GetJobKeys(GroupMatcher<JobKey>.GroupEquals(JobGroup), ct);
        var schedules = new List<ScheduledTaskDto>(keys.Count);
        foreach (var key in keys.OrderBy(key => key.Name, StringComparer.Ordinal))
        {
            var item = await MapAsync(scheduler, key, ct);
            if (item is not null)
                schedules.Add(item);
        }
        return schedules.OrderBy(schedule => schedule.Name).ToArray();
    }

    public async Task<ScheduledTaskDto?> GetScheduleAsync(Guid id, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        return await MapAsync(scheduler, JobKeyFor(id), ct);
    }

    public async Task<ScheduledTaskDto> CreateAsync(
        SaveScheduledTaskRequest request,
        CancellationToken ct)
    {
        var id = Guid.NewGuid();
        await SaveAsync(id, request, replace: false, ct);
        return (await GetScheduleAsync(id, ct))!;
    }

    public async Task<ScheduledTaskDto?> UpdateAsync(
        Guid id,
        SaveScheduledTaskRequest request,
        CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        if (!await scheduler.CheckExists(JobKeyFor(id), ct))
            return null;
        await SaveAsync(id, request, replace: true, ct);
        return await GetScheduleAsync(id, ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var deleted = await scheduler.DeleteJob(JobKeyFor(id), ct);
        if (deleted)
        {
            await db.ScheduledTaskRuns
                .Where(run => run.ScheduleId == id)
                .ExecuteDeleteAsync(ct);
        }
        return deleted;
    }

    public Task<bool> PauseAsync(Guid id, CancellationToken ct) =>
        ChangeStateAsync(id, pause: true, ct);

    public Task<bool> ResumeAsync(Guid id, CancellationToken ct) =>
        ChangeStateAsync(id, pause: false, ct);

    public async Task<bool> RunNowAsync(Guid id, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var key = JobKeyFor(id);
        if (!await scheduler.CheckExists(key, ct))
            return false;
        await scheduler.TriggerJob(key, ct);
        return true;
    }

    public async Task<IReadOnlyCollection<ScheduledTaskRunDto>> GetRunsAsync(
        Guid id,
        CancellationToken ct)
    {
        // SQLite stores DateTimeOffset values but cannot translate ordering
        // them. History is retention-bounded, so sort after projection to
        // keep the same behavior across SQLite previews and PostgreSQL.
        var runs = await db.ScheduledTaskRuns.AsNoTracking()
            .Where(run => run.ScheduleId == id)
            .Select(run => new ScheduledTaskRunDto(
                run.Id,
                run.ScheduleId,
                run.HandlerKey,
                run.Status,
                run.Attempt,
                run.StartedAt,
                run.CompletedAt,
                run.DurationMilliseconds,
                run.ErrorSummary))
            .ToArrayAsync(ct);
        return runs.OrderByDescending(run => run.StartedAt).Take(100).ToArray();
    }

    private async Task SaveAsync(
        Guid id,
        SaveScheduledTaskRequest request,
        bool replace,
        CancellationToken ct)
    {
        var normalized = Validate(request);
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var key = JobKeyFor(id);
        if (!replace && await scheduler.CheckExists(key, ct))
            throw new ScheduledTaskValidationException("A schedule with this identifier already exists.");

        var descriptor = registry.Handlers.Single(handler =>
            string.Equals(handler.Key, normalized.HandlerKey, StringComparison.OrdinalIgnoreCase));
        var jobType = descriptor.Options.AllowConcurrentExecution
            ? typeof(ConcurrentScheduledTaskQuartzJob)
            : typeof(ScheduledTaskQuartzJob);
        var data = new JobDataMap
        {
            [NameKey] = normalized.Name,
            [HandlerKey] = descriptor.Key,
            [KindKey] = normalized.Kind.ToString(),
            [CronKey] = normalized.CronExpression ?? string.Empty,
            [TimeZoneKey] = normalized.TimeZoneId ?? string.Empty,
            [RunAtKey] = normalized.RunAt?.ToString("O") ?? string.Empty
        };
        var job = JobBuilder.Create(jobType)
            .WithIdentity(key)
            .WithDescription(normalized.Name)
            .UsingJobData(data)
            .StoreDurably()
            .Build();
        var trigger = BuildTrigger(id, normalized, key);

        if (replace)
            await scheduler.DeleteJob(key, ct);
        await scheduler.ScheduleJob(job, trigger, ct);
    }

    private SaveScheduledTaskRequest Validate(SaveScheduledTaskRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (name.Length is < 1 or > 120)
            throw new ScheduledTaskValidationException("Name must contain between 1 and 120 characters.");
        if (!registry.TryGet(request.HandlerKey?.Trim() ?? string.Empty, out var handler))
            throw new ScheduledTaskValidationException("Select a registered scheduled task handler.");

        if (request.Kind == ScheduledTaskKind.Cron)
        {
            var cron = request.CronExpression?.Trim() ?? string.Empty;
            if (!CronExpression.IsValidExpression(cron))
                throw new ScheduledTaskValidationException("Enter a valid Quartz cron expression.");
            var timeZoneId = request.TimeZoneId?.Trim() ?? string.Empty;
            try
            {
                _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                throw new ScheduledTaskValidationException("Select a valid IANA time zone.");
            }
            catch (InvalidTimeZoneException)
            {
                throw new ScheduledTaskValidationException("Select a valid IANA time zone.");
            }
            return request with
            {
                Name = name,
                HandlerKey = handler.Key,
                CronExpression = cron,
                TimeZoneId = timeZoneId,
                RunAt = null
            };
        }

        if (request.Kind != ScheduledTaskKind.OneTime)
            throw new ScheduledTaskValidationException("Select a supported schedule type.");
        if (!request.RunAt.HasValue || request.RunAt.Value <= DateTimeOffset.UtcNow)
            throw new ScheduledTaskValidationException("One-time schedules must run in the future.");
        return request with
        {
            Name = name,
            HandlerKey = handler.Key,
            CronExpression = null,
            TimeZoneId = null,
            RunAt = request.RunAt.Value.ToUniversalTime()
        };
    }

    private static ITrigger BuildTrigger(
        Guid id,
        SaveScheduledTaskRequest request,
        JobKey key)
    {
        var builder = TriggerBuilder.Create()
            .WithIdentity(id.ToString("N"), TriggerGroup)
            .ForJob(key);
        if (request.Kind == ScheduledTaskKind.OneTime)
        {
            return builder.StartAt(request.RunAt!.Value)
                .WithSimpleSchedule(schedule => schedule.WithMisfireHandlingInstructionFireNow())
                .Build();
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(request.TimeZoneId!);
        return builder.WithCronSchedule(
                request.CronExpression!,
                schedule => schedule
                    .InTimeZone(timeZone)
                    .WithMisfireHandlingInstructionDoNothing())
            .Build();
    }

    private static async Task<ScheduledTaskDto?> MapAsync(
        IScheduler scheduler,
        JobKey key,
        CancellationToken ct)
    {
        var job = await scheduler.GetJobDetail(key, ct);
        if (job is null || !Guid.TryParseExact(key.Name, "N", out var id))
            return null;
        var triggers = await scheduler.GetTriggersOfJob(key, ct);
        var trigger = triggers.OrderBy(item => item.GetNextFireTimeUtc()).FirstOrDefault();
        var state = trigger is null
            ? "Completed"
            : (await scheduler.GetTriggerState(trigger.Key, ct)).ToString();
        var data = job.JobDataMap;
        _ = Enum.TryParse<ScheduledTaskKind>(data.GetString(KindKey), out var kind);
        _ = DateTimeOffset.TryParse(data.GetString(RunAtKey), out var runAt);
        return new ScheduledTaskDto(
            id,
            data.GetString(NameKey) ?? job.Description ?? key.Name,
            data.GetString(HandlerKey) ?? string.Empty,
            kind,
            NullIfEmpty(data.GetString(CronKey)),
            NullIfEmpty(data.GetString(TimeZoneKey)),
            runAt == default ? null : runAt,
            state,
            trigger?.GetNextFireTimeUtc(),
            trigger?.GetPreviousFireTimeUtc());
    }

    private async Task<bool> ChangeStateAsync(Guid id, bool pause, CancellationToken ct)
    {
        var scheduler = await schedulerFactory.GetScheduler(ct);
        var key = JobKeyFor(id);
        if (!await scheduler.CheckExists(key, ct))
            return false;
        if (pause)
            await scheduler.PauseJob(key, ct);
        else
            await scheduler.ResumeJob(key, ct);
        return true;
    }

    internal static JobKey JobKeyFor(Guid id) => new(id.ToString("N"), JobGroup);
    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}

[DisallowConcurrentExecution]
public sealed class ScheduledTaskQuartzJob(ScheduledTaskExecutor executor) : IJob
{
    public Task Execute(IJobExecutionContext context) => executor.ExecuteAsync(context);
}

public sealed class ConcurrentScheduledTaskQuartzJob(ScheduledTaskExecutor executor) : IJob
{
    public Task Execute(IJobExecutionContext context) => executor.ExecuteAsync(context);
}

public sealed class ScheduledTaskExecutor(
    IScheduledTaskRegistry registry,
    IServiceProvider services,
    ApplicationDbContext db,
    IConfiguration configuration,
    ILogger<ScheduledTaskExecutor> logger)
{
    public async Task ExecuteAsync(IJobExecutionContext quartzContext)
    {
        if (!Guid.TryParseExact(quartzContext.JobDetail.Key.Name, "N", out var scheduleId))
            throw new InvalidOperationException("Scheduled task job key is invalid.");
        var handlerKey = quartzContext.MergedJobDataMap.GetString(ScheduledTaskService.HandlerKey);
        if (handlerKey is null || !registry.TryGet(handlerKey, out var descriptor))
            throw new InvalidOperationException($"Scheduled task handler '{handlerKey}' is not registered.");

        var attempt = quartzContext.MergedJobDataMap.ContainsKey(ScheduledTaskService.AttemptKey)
            ? quartzContext.MergedJobDataMap.GetInt(ScheduledTaskService.AttemptKey)
            : 0;
        var run = new ScheduledTaskRun
        {
            Id = Guid.NewGuid(),
            ScheduleId = scheduleId,
            HandlerKey = descriptor.Key,
            Status = "Running",
            Attempt = attempt,
            StartedAt = DateTimeOffset.UtcNow
        };
        db.ScheduledTaskRuns.Add(run);
        await db.SaveChangesAsync(quartzContext.CancellationToken);

        try
        {
            var handler = (IScheduledTaskHandler)services.GetRequiredService(descriptor.HandlerType);
            await handler.ExecuteAsync(
                new ScheduledTaskExecutionContext(
                    scheduleId,
                    run.Id,
                    attempt,
                    quartzContext.ScheduledFireTimeUtc ?? DateTimeOffset.UtcNow),
                quartzContext.CancellationToken);
            Complete(run, "Succeeded", null);
        }
        catch (OperationCanceledException) when (quartzContext.CancellationToken.IsCancellationRequested)
        {
            Complete(run, "Cancelled", null);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Scheduled task {HandlerKey} failed for schedule {ScheduleId} on attempt {Attempt}", descriptor.Key, scheduleId, attempt);
            Complete(run, "Failed", Sanitize(ex));
            var delays = descriptor.Options.RetryDelays ?? [];
            if (attempt < delays.Count)
            {
                var retry = TriggerBuilder.Create()
                    .WithIdentity($"{scheduleId:N}-{run.Id:N}", ScheduledTaskService.RetryGroup)
                    .ForJob(quartzContext.JobDetail.Key)
                    .UsingJobData(ScheduledTaskService.AttemptKey, attempt + 1)
                    .StartAt(DateTimeOffset.UtcNow.Add(delays[attempt]))
                    .WithSimpleSchedule(schedule => schedule.WithMisfireHandlingInstructionFireNow())
                    .Build();
                try
                {
                    await quartzContext.Scheduler.ScheduleJob(retry, quartzContext.CancellationToken);
                }
                catch (JobPersistenceException retryException)
                {
                    logger.LogWarning(retryException, "Could not persist retry for deleted schedule {ScheduleId}", scheduleId);
                }
            }
        }

        await db.SaveChangesAsync(quartzContext.CancellationToken);
        var retentionDays = Math.Clamp(
            configuration.GetValue("ScheduledTasks:HistoryRetentionDays", 30),
            1,
            3650);
        var cutoff = DateTimeOffset.UtcNow.AddDays(-retentionDays);
        if (db.Database.IsSqlite())
        {
            var retainedRows = await db.ScheduledTaskRuns.AsNoTracking()
                .Select(item => new { item.Id, item.StartedAt })
                .ToArrayAsync(quartzContext.CancellationToken);
            var expiredIds = retainedRows
                .Where(item => item.StartedAt < cutoff)
                .Select(item => item.Id)
                .ToArray();
            if (expiredIds.Length > 0)
            {
                await db.ScheduledTaskRuns
                    .Where(item => expiredIds.Contains(item.Id))
                    .ExecuteDeleteAsync(quartzContext.CancellationToken);
            }
        }
        else
        {
            await db.ScheduledTaskRuns
                .Where(item => item.StartedAt < cutoff)
                .ExecuteDeleteAsync(quartzContext.CancellationToken);
        }
    }

    private static void Complete(ScheduledTaskRun run, string status, string? error)
    {
        run.Status = status;
        run.CompletedAt = DateTimeOffset.UtcNow;
        run.DurationMilliseconds = Math.Max(0, (long)(run.CompletedAt.Value - run.StartedAt).TotalMilliseconds);
        run.ErrorSummary = error;
    }

    internal static string Sanitize(Exception exception)
    {
        // Exception messages can contain task inputs or secrets. Keep the API
        // history useful without exposing them; the full exception is logged.
        return $"{exception.GetType().Name}: Scheduled task execution failed. See server logs for details.";
    }
}
