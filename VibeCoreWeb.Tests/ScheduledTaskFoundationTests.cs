using Microsoft.Extensions.DependencyInjection;
using Quartz;
using VibeCore.Scheduling;
using Xunit;

namespace VibeCoreWeb.Tests;

public sealed class ScheduledTaskFoundationTests
{
    [Fact]
    public void Empty_registry_is_available_without_a_sample_handler()
    {
        var services = new ServiceCollection();
        services.AddScheduledTasks();
        using var provider = services.BuildServiceProvider();

        Assert.Empty(provider.GetRequiredService<IScheduledTaskRegistry>().Handlers);
    }

    [Fact]
    public void Registry_rejects_duplicate_and_invalid_keys()
    {
        var services = new ServiceCollection();
        services.AddScheduledTask<NoOpTask>("valid-task", "Valid", "A valid task.");

        Assert.Throws<InvalidOperationException>(() =>
            services.AddScheduledTask<NoOpTask>("valid-task", "Duplicate", "Duplicate task."));
        Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddScheduledTask<NoOpTask>("Not Valid", "Invalid", "Invalid task."));
    }

    [Fact]
    public void Error_summaries_are_bounded_and_single_line()
    {
        var result = ScheduledTaskExecutor.Sanitize(
            new InvalidOperationException("secret\n" + new string('x', 2500)));

        Assert.True(result.Length <= 2000);
        Assert.DoesNotContain('\n', result);
        Assert.DoesNotContain("secret", result);
    }

    [Fact]
    public void Registration_resolves_handlers_and_applies_safe_defaults()
    {
        var services = new ServiceCollection();
        services.AddScheduledTask<NoOpTask>("safe-task", "Safe", "A safe task.");
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var registry = scope.ServiceProvider.GetRequiredService<IScheduledTaskRegistry>();
        var handler = scope.ServiceProvider.GetRequiredService<NoOpTask>();
        var descriptor = Assert.Single(registry.Handlers);

        Assert.NotNull(handler);
        Assert.False(descriptor.Options.AllowConcurrentExecution);
        Assert.Equal(
            [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15)],
            descriptor.Options.RetryDelays);
        Assert.True(Attribute.IsDefined(
            typeof(ScheduledTaskQuartzJob),
            typeof(DisallowConcurrentExecutionAttribute)));
    }

    [Fact]
    public void Registration_rejects_non_positive_retry_delays()
    {
        var services = new ServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            services.AddScheduledTask<NoOpTask>(
                "bad-retry",
                "Bad retry",
                "An invalid task.",
                new ScheduledTaskHandlerOptions([TimeSpan.Zero])));
    }

    [Fact]
    public async Task Scheduling_validation_rejects_bad_cron_time_zones_and_past_runs()
    {
        var services = new ServiceCollection();
        services.AddScheduledTask<NoOpTask>("safe-task", "Safe", "A safe task.");
        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IScheduledTaskRegistry>();
        var service = new ScheduledTaskService(null!, registry, null!);

        await Assert.ThrowsAsync<ScheduledTaskValidationException>(() => service.CreateAsync(
            new SaveScheduledTaskRequest(
                "Bad cron", "safe-task", ScheduledTaskKind.Cron, "not cron", "UTC", null),
            CancellationToken.None));
        await Assert.ThrowsAsync<ScheduledTaskValidationException>(() => service.CreateAsync(
            new SaveScheduledTaskRequest(
                "Bad zone", "safe-task", ScheduledTaskKind.Cron, "0 0 9 ? * * *", "Not/AZone", null),
            CancellationToken.None));
        await Assert.ThrowsAsync<ScheduledTaskValidationException>(() => service.CreateAsync(
            new SaveScheduledTaskRequest(
                "Past", "safe-task", ScheduledTaskKind.OneTime, null, null, DateTimeOffset.UtcNow.AddMinutes(-1)),
            CancellationToken.None));
    }

    private sealed class NoOpTask : IScheduledTaskHandler
    {
        public Task ExecuteAsync(
            ScheduledTaskExecutionContext context,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
